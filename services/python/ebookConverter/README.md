# MOBI to EPUB eBook Converter Tool

A Python tool designed to extract ZIP archives containing MOBI format eBooks, parse book metadata, convert them to EPUB format using Calibre's `ebook-convert` engine, and save them in a designated folder formatted as `AuthorName-BookName.epub`.

## Features
- **ZIP Extraction**: Inspects and extracts `.mobi` ebooks directly from `.zip` archives.
- **OPF & Regex Metadata Parsing**: Reads Calibre `metadata.opf` XML or falls back to file/folder parsing for accurate author and title names.
- **Clean Naming**: Formats output filenames as `AuthorName-BookName.epub`, removing trailing database IDs (e.g. `(5912)`).
- **Parallel Batch Processing**: Concurrently converts eBooks using Python multi-threading and process execution.
- **Resumable**: Skips already converted EPUB files unless `--force` is specified.
- **Dry Run Mode**: Test metadata parsing and preview output names without modifying disk files.

## Prerequisites
- Python 3.9+
- [Calibre](https://calibre-ebook.com/) (provides `ebook-convert` binary):
  ```bash
  brew install --cask calibre
  ```

## Usage

### 1. Dry Run (Preview conversion names)
```bash
/usr/bin/python3 services/python/ebookConverter/convert_mobi_to_epub.py --dry-run --limit 10
```

### 2. Convert Ebooks (Default Folders)
Source: `~/Google Drive/My Drive/BooksFromOneDrive/Ebooks/Kindle e books/Kindle Library 12-26-10/Library`  
Destination: `~/Documents/Books`

```bash
/usr/bin/python3 services/python/ebookConverter/convert_mobi_to_epub.py
```

### 3. Custom Directories and Options
```bash
/usr/bin/python3 services/python/ebookConverter/convert_mobi_to_epub.py \
  --src-dir "/path/to/zip/library" \
  --dest-dir "/path/to/output/epubs" \
  --jobs 8 \
  --force
```
