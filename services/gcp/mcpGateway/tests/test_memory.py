import os
import pytest
from datetime import datetime, timedelta, timezone
from tools.memory import remember_entity, recall_entities, prune_expired_memories, sanitize_text, LOCAL_MEMORY_FILE

@pytest.fixture(autouse=True)
def cleanup_local_memory():
    """Ensure temporary memory file is cleaned up before and after tests."""
    if os.path.exists(LOCAL_MEMORY_FILE):
        os.remove(LOCAL_MEMORY_FILE)
    yield
    if os.path.exists(LOCAL_MEMORY_FILE):
        os.remove(LOCAL_MEMORY_FILE)

def test_sanitize_text():
    raw_text = "API Key: sk-proj-1234567890abcdef1234567890 and Google Key AIzaSyD1234567890abcdef1234567890abc"
    clean = sanitize_text(raw_text)
    assert "sk-proj" not in clean
    assert "[REDACTED_API_KEY]" in clean
    assert "[REDACTED_GOOGLE_KEY]" in clean

@pytest.mark.asyncio
async def test_remember_entity_sanitization_and_timestamps():
    res = await remember_entity(
        name="Cloud Run API Key: sk-1234567890abcdef1234567890",
        category="GCP Compute",
        observations=["Serverless container execution platform.", "Key is Bearer my_secret_token_1234567890"],
        ttl_days=30,
        pinned=False
    )
    
    assert res["status"] in ["success", "warning_local_only"]
    entity = res["entity"]
    assert "sk-1234567890" not in entity["name"]
    assert "[REDACTED_API_KEY]" in entity["name"]
    assert "[REDACTED_BEARER_TOKEN]" in entity["observations"][1]
    assert entity["created_at"] is not None
    assert entity["expires_at"] is not None


@pytest.mark.asyncio
async def test_prune_expired_memories():
    # 1. Store unpinned entity with past expiration
    await remember_entity(
        name="Expired Event",
        category="Temporary",
        observations=["This event has passed."],
        ttl_days=-1,  # Expired immediately
        pinned=False
    )

    # 2. Store pinned entity
    await remember_entity(
        name="Core Architecture",
        category="Permanent",
        observations=["Must use HTTPS transport."],
        ttl_days=-1,
        pinned=True
    )

    # 3. Recall active entities (expired excluded by default)
    recalled = await recall_entities()
    assert "Expired Event" not in recalled["entities"]
    assert "Core Architecture" in recalled["entities"]

    # 4. Prune storage
    prune_res = await prune_expired_memories()
    assert prune_res["pruned_count"] == 1
    assert "Expired Event" in prune_res["pruned_entities"]
