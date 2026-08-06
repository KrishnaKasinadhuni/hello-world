import asyncio
from typing import Dict, Any, List, Optional
from tools.web_fetch import fetch_web_markdown
from tools.memory import remember_entity, sanitize_text

async def run_tech_radar(
    urls: List[str],
    category: str = "Tech Intelligence",
    ttl_days: int = 30
) -> Dict[str, Any]:
    """
    Executes a Tech Intelligence Radar pass across target URLs, indexing updates into persistent memory with retention TTL.
    
    Args:
        urls: List of web URLs to monitor (e.g. release notes, product blogs, doc pages).
        category: Memory entity category tag (default 'Tech Intelligence').
        ttl_days: Retention period in days for intelligence entries (default 30 days).
    """
    results = []
    indexed_entities = []

    for url in urls:
        fetch_res = await fetch_web_markdown(url, max_chars=4000)
        
        if fetch_res.get("status") == "failed" or "error" in fetch_res:
            results.append({
                "url": url,
                "status": "error",
                "error": fetch_res.get("error", "Failed to fetch URL")
            })
            continue

        title = fetch_res.get("title", url)
        markdown = fetch_res.get("markdown", "")
        
        # Extract first 5 meaningful non-header lines as key observations
        lines = [l.strip() for l in markdown.splitlines() if l.strip() and not l.startswith("#")]
        observations = lines[:5] if lines else ["Page content ingested and analyzed."]
        
        # Add source attribution
        observations.append(f"Source URL: {url}")

        # Store entity in memory with TTL retention
        mem_res = await remember_entity(
            name=title,
            category=category,
            observations=observations,
            ttl_days=ttl_days,
            pinned=False
        )
        
        indexed_entities.append(title)
        results.append({
            "url": url,
            "title": title,
            "status": "indexed",
            "observations_count": len(observations),
            "retention_days": ttl_days
        })

    return {
        "status": "completed",
        "processed_count": len(urls),
        "indexed_count": len(indexed_entities),
        "indexed_entities": indexed_entities,
        "details": results
    }
