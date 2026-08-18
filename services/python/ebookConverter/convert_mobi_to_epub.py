#!/usr/bin/env python3
"""
MOBI to EPUB eBook Converter & Organizer Tool
---------------------------------------------
Reads ZIP files, extracts MOBI ebooks, parses metadata (OPF/filename),
converts them to EPUB using Calibre's ebook-convert, and saves them
with clean 'AuthorName-BookName.epub' file names.
"""

import argparse
import concurrent.futures
import multiprocessing
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
import zipfile


DEFAULT_SRC_DIR = "/Users/krishnakasinadhuni/Google Drive/My Drive/BooksFromOneDrive/Ebooks/Kindle e books/Kindle Library 12-26-10/Library"
DEFAULT_DEST_DIR = "/Users/krishnakasinadhuni/Documents/Books"


def find_ebook_convert(custom_path=None) -> str:
    """Find the ebook-convert binary from Calibre."""
    if custom_path and os.path.isfile(custom_path) and os.access(custom_path, os.X_OK):
        return custom_path

    # Standard Calibre install paths on macOS & Linux
    candidates = [
        "/Applications/calibre.app/Contents/MacOS/ebook-convert",
        "/Applications/Calibre.app/Contents/MacOS/ebook-convert",
        os.path.expanduser("~/Applications/calibre.app/Contents/MacOS/ebook-convert"),
        os.path.expanduser("~/Applications/Calibre.app/Contents/MacOS/ebook-convert"),
    ]

    for cand in candidates:
        if os.path.isfile(cand) and os.access(cand, os.X_OK):
            return cand

    # Check system PATH
    which_path = shutil.which("ebook-convert")
    if which_path:
        return which_path

    return ""


def sanitize_filename(text: str) -> str:
    """Clean string for use in file names, removing IDs like '(5912)' and invalid characters."""
    if not text:
        return ""
    # Remove trailing Calibre numeric IDs in parentheses, e.g. "Title (5912)" -> "Title"
    text = re.sub(r"\s*\(\d+\)\s*$", "", text.strip())
    # Replace forbidden filesystem characters
    text = re.sub(r'[\\/:*?"<>|]', "-", text)
    # Collapse multiple whitespace/dashes
    text = re.sub(r"\s+", " ", text)
    text = re.sub(r"-+", "-", text)
    return text.strip(" -_")


def extract_metadata_from_opf(opf_content: str):
    """Extract author and title from metadata.opf XML content using regex (failsafe across Python versions)."""
    title_match = re.search(r'<dc:title[^>]*>(.*?)</dc:title>', opf_content, re.IGNORECASE | re.DOTALL)
    creator_match = re.search(r'<dc:creator[^>]*>(.*?)</dc:creator>', opf_content, re.IGNORECASE | re.DOTALL)

    title = title_match.group(1).strip() if title_match else None
    creator = creator_match.group(1).strip() if creator_match else None

    # Clean XML entity references if present
    if title:
        title = re.sub(r'&amp;', '&', title)
        title = re.sub(r'&lt;', '<', title)
        title = re.sub(r'&gt;', '>', title)
        title = re.sub(r'&#\d+;', '', title)
    if creator:
        creator = re.sub(r'&amp;', '&', creator)
        creator = re.sub(r'&lt;', '<', creator)
        creator = re.sub(r'&gt;', '>', creator)
        creator = re.sub(r'&#\d+;', '', creator)

    return creator, title


def scan_zip_file(zip_path: str):
    """
    Scan a zip file for MOBI files and metadata.
    Returns a list of dicts: [{ 'mobi_entry': ..., 'author': ..., 'title': ..., 'zip_path': ... }]
    """
    books = []
    zip_name = os.path.basename(zip_path)
    zip_author_fallback = os.path.splitext(zip_name)[0]

    try:
        with zipfile.ZipFile(zip_path, 'r') as z:
            all_entries = z.namelist()
            mobi_entries = [e for e in all_entries if e.lower().endswith('.mobi')]

            for mobi_entry in mobi_entries:
                mobi_dir = os.path.dirname(mobi_entry)

                # Look for metadata.opf in the same folder as the mobi file
                opf_entry = None
                for entry in all_entries:
                    if entry.lower().endswith('metadata.opf'):
                        if os.path.dirname(entry) == mobi_dir or opf_entry is None:
                            opf_entry = entry

                author = None
                title = None

                if opf_entry:
                    try:
                        opf_content = z.read(opf_entry).decode('utf-8', errors='ignore')
                        author, title = extract_metadata_from_opf(opf_content)
                    except Exception:
                        pass

                # Fallback 1: Parse from mobi file basename ("Book Title - Author Name.mobi")
                mobi_basename = os.path.splitext(os.path.basename(mobi_entry))[0]
                if not title or not author:
                    if " - " in mobi_basename:
                        parts = mobi_basename.split(" - ", 1)
                        if not title:
                            title = parts[0]
                        if not author:
                            author = parts[1]

                # Fallback 2: Parse from parent folder inside ZIP ("Author/Book Title (1234)/")
                if not title:
                    parent_dir_name = os.path.basename(mobi_dir)
                    title = parent_dir_name if parent_dir_name else mobi_basename

                if not author:
                    author = zip_author_fallback

                # Sanitize fields
                clean_author = sanitize_filename(author)
                clean_title = sanitize_filename(title)

                books.append({
                    "zip_path": zip_path,
                    "mobi_entry": mobi_entry,
                    "author": clean_author,
                    "title": clean_title,
                    "output_filename": f"{clean_author}-{clean_title}.epub",
                })
    except zipfile.BadZipFile:
        print(f"[WARN] Corrupt or invalid zip archive: {zip_path}")
    except Exception as e:
        print(f"[WARN] Error scanning {zip_path}: {e}")

    return books


import threading

# Limit concurrent ZIP extractions to 4 to prevent Google Drive Cloud Storage I/O throttling
EXTRACTION_SEMAPHORE = threading.BoundedSemaphore(4)


def convert_single_book(book_task: dict, dest_dir: str, converter_bin: str, force: bool = False, dry_run: bool = False) -> tuple:
    """
    Extract MOBI from zip to temp file and convert to EPUB via ebook-convert.
    Returns (status, output_filename, details)
    """
    zip_path = book_task["zip_path"]
    mobi_entry = book_task["mobi_entry"]
    output_filename = book_task["output_filename"]
    dest_path = os.path.join(dest_dir, output_filename)

    if os.path.exists(dest_path) and not force:
        return ("SKIPPED", output_filename, "Already exists in destination directory")

    if dry_run:
        return ("DRY-RUN", output_filename, f"Would extract '{mobi_entry}' from '{os.path.basename(zip_path)}'")

    # Retry zip extraction up to 5 times with exponential backoff to handle Google Drive cloud hydration
    max_retries = 5
    extracted_successfully = False
    temp_mobi_path = None

    with tempfile.TemporaryDirectory(prefix="mobi_convert_") as temp_dir:
        temp_mobi = os.path.join(temp_dir, "input.mobi")
        for attempt in range(1, max_retries + 1):
            try:
                with EXTRACTION_SEMAPHORE:
                    with zipfile.ZipFile(zip_path, 'r') as z:
                        with z.open(mobi_entry) as src, open(temp_mobi, 'wb') as dst:
                            shutil.copyfileobj(src, dst)
                extracted_successfully = True
                break
            except Exception as e:
                if attempt < max_retries:
                    backoff = 2 ** attempt
                    time.sleep(backoff)
                    continue
                return ("FAILED", output_filename, f"Extraction failed after {max_retries} retries: {e}")

        if not extracted_successfully or not os.path.exists(temp_mobi):
            return ("FAILED", output_filename, "Temporary MOBI file extraction failed")

        # Run Calibre ebook-convert
        cmd = [
            converter_bin,
            temp_mobi,
            dest_path,
            "--enable-heuristics",
            "--dont-split-on-page-breaks",
        ]

        try:
            result = subprocess.run(cmd, capture_output=True, text=True, timeout=300)
            if result.returncode == 0 and os.path.exists(dest_path):
                return ("CONVERTED", output_filename, "Successfully converted to EPUB")
            else:
                err_msg = result.stderr[-300:] if result.stderr else "Unknown ebook-convert error"
                return ("FAILED", output_filename, f"Conversion error: {err_msg.strip()}")
        except subprocess.TimeoutExpired:
            return ("FAILED", output_filename, "Conversion timed out after 300 seconds")
        except Exception as e:
            return ("FAILED", output_filename, f"Execution failed: {e}")


def main():
    parser = argparse.ArgumentParser(
        description="Extract MOBI ebooks from ZIP archives and convert to EPUB with AuthorName-BookName naming."
    )
    parser.add_argument("--src-dir", default=DEFAULT_SRC_DIR, help="Source directory containing ZIP files")
    parser.add_argument("--dest-dir", default=DEFAULT_DEST_DIR, help="Destination directory for converted EPUB files")
    parser.add_argument("--converter", default=None, help="Explicit path to Calibre 'ebook-convert' binary")
    parser.add_argument("-j", "--jobs", type=int, default=multiprocessing.cpu_count(), help="Number of parallel conversion jobs")
    parser.add_argument("--force", action="store_true", help="Overwrite existing EPUB files in destination directory")
    parser.add_argument("--limit", type=int, default=None, help="Limit maximum number of books to process")
    parser.add_argument("--dry-run", action="store_true", help="Scan and show files to convert without executing conversions")

    args = parser.parse_args()

    src_dir = os.path.abspath(args.src_dir)
    dest_dir = os.path.abspath(args.dest_dir)

    print("==========================================================")
    print("      MOBI to EPUB eBook Converter & Organizer Tool       ")
    print("==========================================================")
    print(f" Source Directory  : {src_dir}")
    print(f" Destination Dir   : {dest_dir}")
    print(f" Parallel Workers  : {args.jobs}")
    print(f" Dry Run Mode      : {args.dry_run}")
    print(f" Force Overwrite   : {args.force}")
    if args.limit:
        print(f" Processing Limit  : {args.limit} book(s)")

    if not os.path.isdir(src_dir):
        print(f"\n[ERROR] Source directory does not exist: {src_dir}")
        sys.exit(1)

    os.makedirs(dest_dir, exist_ok=True)

    # Locate Calibre converter
    converter_bin = find_ebook_convert(args.converter)
    if not args.dry_run:
        if not converter_bin:
            print("\n[ERROR] Could not find Calibre 'ebook-convert' binary.")
            print("Please ensure Calibre is installed (e.g. `brew install --cask calibre`)")
            print("or specify path explicitly via `--converter /path/to/ebook-convert`.")
            sys.exit(1)
        print(f" Converter Binary  : {converter_bin}")

    print("\nScanning source ZIP archives...")
    zip_files = [os.path.join(src_dir, f) for f in os.listdir(src_dir) if f.lower().endswith(".zip")]
    print(f"Found {len(zip_files)} ZIP archive(s).")

    all_books = []
    scan_workers = min(16, len(zip_files)) if zip_files else 1
    with concurrent.futures.ThreadPoolExecutor(max_workers=scan_workers) as scan_executor:
        future_to_zip = {scan_executor.submit(scan_zip_file, zf): zf for zf in zip_files}
        for i, future in enumerate(concurrent.futures.as_completed(future_to_zip), start=1):
            try:
                found = future.result()
                all_books.extend(found)
                if args.limit and len(all_books) >= args.limit:
                    # Cancel remaining pending scan futures
                    for pending in future_to_zip:
                        pending.cancel()
                    break
                if i % 50 == 0 or i == len(zip_files):
                    print(f"Scanned {i}/{len(zip_files)} ZIPs ({len(all_books)} MOBI ebook(s) found so far)...", flush=True)
            except Exception as e:
                pass

    if args.limit and args.limit > 0:
        all_books = all_books[:args.limit]
        print(f"Discovered {len(all_books)} MOBI eBook(s) matching limit of {args.limit}.")
    else:
        print(f"Discovered {len(all_books)} total MOBI eBook(s).")

    if not all_books:
        print("\nNo MOBI ebooks found to process.")
        return

    print("\n----------------------------------------------------------")
    print("Starting MOBI to EPUB conversion processing...")
    print("----------------------------------------------------------")

    start_time = time.time()
    converted_count = 0
    skipped_count = 0
    failed_count = 0

    with concurrent.futures.ThreadPoolExecutor(max_workers=args.jobs) as executor:
        futures = {
            executor.submit(
                convert_single_book, book, dest_dir, converter_bin, args.force, args.dry_run
            ): book for book in all_books
        }

        for i, future in enumerate(concurrent.futures.as_completed(futures), start=1):
            status, filename, details = future.result()
            prefix = f"[{i}/{len(all_books)}]"

            if status == "CONVERTED":
                converted_count += 1
                print(f"{prefix} \033[92m[CONVERTED]\033[0m {filename}")
            elif status == "SKIPPED":
                skipped_count += 1
                print(f"{prefix} \033[93m[SKIPPED]\033[0m   {filename} ({details})")
            elif status == "DRY-RUN":
                print(f"{prefix} [DRY-RUN]   {filename} -> {details}")
            else:
                failed_count += 1
                print(f"{prefix} \033[91m[FAILED]\033[0m    {filename} - {details}")

    elapsed = time.time() - start_time
    print("----------------------------------------------------------")
    print(f"Completed in {elapsed:.2f} seconds.")
    if args.dry_run:
        print(f"Dry run complete. Total eBook targets inspected: {len(all_books)}")
    else:
        print(f"Summary: {converted_count} Converted | {skipped_count} Skipped | {failed_count} Failed")
    print("==========================================================")


if __name__ == "__main__":
    main()
