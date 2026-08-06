import re
import httpx
from bs4 import BeautifulSoup
from typing import Dict, Any

async def fetch_web_markdown(url: str, max_chars: int = 10000) -> Dict[str, Any]:
    """
    Fetches a web page URL and converts the HTML content into clean Markdown text.
    
    Args:
        url: The target web URL to fetch.
        max_chars: Maximum character limit for output text (default 10000).
    """
    headers = {
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8"
    }
    
    try:
        async with httpx.AsyncClient(follow_redirects=True, timeout=15.0) as client:
            response = await client.get(url, headers=headers)
            response.raise_for_status()
            
        html = response.text
        soup = BeautifulSoup(html, "html.parser")
        
        # Extract title
        title = soup.title.string.strip() if soup.title and soup.title.string else url
        
        # Remove non-content elements
        for element in soup(["script", "style", "nav", "footer", "header", "svg", "noscript"]):
            element.decompose()
            
        # Get text content
        text = soup.get_text(separator="\n")
        
        # Clean up whitespace
        lines = (line.strip() for line in text.splitlines())
        chunks = (phrase.strip() for line in lines for phrase in line.split("  "))
        clean_text = "\n".join(chunk for chunk in chunks if chunk)
        
        # Truncate if needed
        truncated = False
        if len(clean_text) > max_chars:
            clean_text = clean_text[:max_chars] + "\n\n...[Truncated due to length]"
            truncated = True
            
        return {
            "url": url,
            "status_code": response.status_code,
            "title": title,
            "markdown": clean_text,
            "length": len(clean_text),
            "truncated": truncated
        }
    except Exception as e:
        return {
            "url": url,
            "error": str(e),
            "status": "failed"
        }
