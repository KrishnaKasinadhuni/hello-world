import os
import json
from typing import Dict, Any, List, Optional
from google.cloud import storage

GCS_BUCKET_NAME = os.getenv("MEMORY_GCS_BUCKET", None)
LOCAL_MEMORY_FILE = os.getenv("LOCAL_MEMORY_FILE", "/tmp/mcp_memory.json")

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

async def remember_entity(name: str, category: str, observations: List[str]) -> Dict[str, Any]:
    """
    Stores an entity with observations into long-term knowledge graph memory.
    
    Args:
        name: Name of the entity (e.g., 'GCP Cloud Run', 'Krishna').
        category: Type/category (e.g., 'Infrastructure', 'Developer').
        observations: List of factual facts or notes about the entity.
    """
    memory = _load_memory()
    entities = memory.setdefault("entities", {})
    
    if name not in entities:
        entities[name] = {
            "name": name,
            "category": category,
            "observations": []
        }
        
    for obs in observations:
        if obs not in entities[name]["observations"]:
            entities[name]["observations"].append(obs)
            
    saved = _save_memory(memory)
    return {
        "status": "success" if saved else "warning_local_only",
        "entity": entities[name]
    }

async def recall_entities(query: Optional[str] = None) -> Dict[str, Any]:
    """
    Recalls entities and observations stored in long-term memory.
    
    Args:
        query: Optional search term to filter entity names or observations.
    """
    memory = _load_memory()
    entities = memory.get("entities", {})
    
    if not query:
        return {"total_count": len(entities), "entities": entities}
        
    query_lower = query.lower()
    filtered = {}
    for name, data in entities.items():
        if query_lower in name.lower() or any(query_lower in obs.lower() for obs in data.get("observations", [])):
            filtered[name] = data
            
    return {"query": query, "matches": len(filtered), "entities": filtered}
