import os
import re
import json
from datetime import datetime, timedelta, timezone
from typing import Dict, Any, List, Optional
from google.cloud import storage

GCS_BUCKET_NAME = os.getenv("MEMORY_GCS_BUCKET", None)
LOCAL_MEMORY_FILE = os.getenv("LOCAL_MEMORY_FILE", "/tmp/mcp_memory.json")
DEFAULT_RETENTION_DAYS = int(os.getenv("MEMORY_RETENTION_DAYS", "90"))

# Secret and Credential Sanitization Regexes
SECRET_PATTERNS = [
    (r"(sk-[a-zA-Z0-9_-]{20,})", "[REDACTED_API_KEY]"),
    (r"(AIzaSy[a-zA-Z0-9_-]{30,})", "[REDACTED_GOOGLE_KEY]"),
    (r"(ghp_[a-zA-Z0-9]{36})", "[REDACTED_GITHUB_TOKEN]"),
    (r"(Bearer\s+[a-zA-Z0-9._-]{20,})", "[REDACTED_BEARER_TOKEN]"),
    (r"(password\s*=\s*['\"][^'\"]+['\"])", "password='[REDACTED]'"),
]

def sanitize_text(text: str) -> str:
    """Sanitizes sensitive patterns (API keys, bearer tokens, passwords) from text."""
    if not isinstance(text, str):
        return text
    sanitized = text
    for pattern, replacement in SECRET_PATTERNS:
        sanitized = re.sub(pattern, replacement, sanitized, flags=re.IGNORECASE)
    return sanitized


def _load_memory() -> Dict[str, Any]:
    """Loads memory dictionary from GCS or local storage."""
    if GCS_BUCKET_NAME:
        try:
            client = storage.Client()
            bucket = client.bucket(GCS_BUCKET_NAME)
            blob = bucket.blob("knowledge_graph.json")
            if blob.exists():
                return json.loads(blob.download_as_text())
        except Exception as e:
            print(f"Warning: Failed to load memory from GCS: {e}")

    if os.path.exists(LOCAL_MEMORY_FILE):
        try:
            with open(LOCAL_MEMORY_FILE, "r") as f:
                return json.load(f)
        except Exception:
            pass

    return {"entities": {}, "relations": []}


def _save_memory(memory: Dict[str, Any]) -> bool:
    """Saves memory dictionary to GCS or local storage."""
    data = json.dumps(memory, indent=2)

    if GCS_BUCKET_NAME:
        try:
            client = storage.Client()
            bucket = client.bucket(GCS_BUCKET_NAME)
            blob = bucket.blob("knowledge_graph.json")
            blob.upload_from_string(data, content_type="application/json")
            return True
        except Exception as e:
            print(f"Warning: Failed to save memory to GCS: {e}")

    try:
        os.makedirs(os.path.dirname(LOCAL_MEMORY_FILE), exist_ok=True)
        with open(LOCAL_MEMORY_FILE, "w") as f:
            f.write(data)
        return True
    except Exception as e:
        print(f"Error saving local memory: {e}")
        return False


async def remember_entity(
    name: str,
    category: str,
    observations: List[str],
    ttl_days: Optional[int] = DEFAULT_RETENTION_DAYS,
    pinned: bool = False
) -> Dict[str, Any]:
    """
    Stores an entity with observations into long-term knowledge graph memory, applying sanitization & TTL.
    
    Args:
        name: Name of the entity (e.g., 'GCP Cloud Run', 'Krishna').
        category: Type/category (e.g., 'Infrastructure', 'Developer').
        observations: List of factual facts or notes about the entity.
        ttl_days: Retention period in days (default 90 days). Set None for infinite.
        pinned: If True, prevents automatic expiration pruning.
    """
    memory = _load_memory()
    entities = memory.setdefault("entities", {})
    now = datetime.now(timezone.utc)
    now_iso = now.isoformat()

    clean_name = sanitize_text(name)
    clean_category = sanitize_text(category)
    clean_obs = [sanitize_text(obs) for obs in observations]

    expires_at = None
    if ttl_days and not pinned:
        expires_at = (now + timedelta(days=ttl_days)).isoformat()

    if clean_name not in entities:
        entities[clean_name] = {
            "name": clean_name,
            "category": clean_category,
            "observations": [],
            "created_at": now_iso,
            "last_updated_at": now_iso,
            "expires_at": expires_at,
            "pinned": pinned
        }
    else:
        entities[clean_name]["last_updated_at"] = now_iso
        if pinned:
            entities[clean_name]["pinned"] = True
            entities[clean_name]["expires_at"] = None

    existing_obs = entities[clean_name].setdefault("observations", [])
    for obs in clean_obs:
        if obs not in existing_obs:
            existing_obs.append(obs)

    saved = _save_memory(memory)
    return {
        "status": "success" if saved else "warning_local_only",
        "entity": entities[clean_name]
    }


async def recall_entities(query: Optional[str] = None, include_expired: bool = False) -> Dict[str, Any]:
    """
    Recalls unexpired entities and observations stored in long-term memory.
    
    Args:
        query: Optional search term to filter entity names or observations.
        include_expired: If True, includes items past their expiration date.
    """
    memory = _load_memory()
    entities = memory.get("entities", {})
    now_iso = datetime.now(timezone.utc).isoformat()

    valid_entities = {}
    for name, data in entities.items():
        expires_at = data.get("expires_at")
        if not include_expired and expires_at and expires_at < now_iso and not data.get("pinned", False):
            continue
        valid_entities[name] = data

    if not query:
        return {"total_count": len(valid_entities), "entities": valid_entities}

    query_lower = query.lower()
    filtered = {}
    for name, data in valid_entities.items():
        if query_lower in name.lower() or any(query_lower in obs.lower() for obs in data.get("observations", [])):
            filtered[name] = data

    return {"query": query, "matches": len(filtered), "entities": filtered}


async def prune_expired_memories() -> Dict[str, Any]:
    """Prunes expired, unpinned memories from storage based on retention policy."""
    memory = _load_memory()
    entities = memory.get("entities", {})
    now_iso = datetime.now(timezone.utc).isoformat()

    pruned = []
    retained = {}

    for name, data in entities.items():
        expires_at = data.get("expires_at")
        if expires_at and expires_at < now_iso and not data.get("pinned", False):
            pruned.append(name)
        else:
            retained[name] = data

    memory["entities"] = retained
    saved = _save_memory(memory)

    return {
        "status": "success" if saved else "warning_local_only",
        "pruned_count": len(pruned),
        "pruned_entities": pruned,
        "retained_count": len(retained)
    }
