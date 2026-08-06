import os
import pytest
from tools.memory import remember_entity, recall_entities, LOCAL_MEMORY_FILE

@pytest.fixture(autouse=True)
def cleanup_local_memory():
    """Ensure temporary memory file is cleaned up before and after tests."""
    if os.path.exists(LOCAL_MEMORY_FILE):
        os.remove(LOCAL_MEMORY_FILE)
    yield
    if os.path.exists(LOCAL_MEMORY_FILE):
        os.remove(LOCAL_MEMORY_FILE)

@pytest.mark.asyncio
async def test_remember_entity():
    res = await remember_entity(
        name="Cloud Run",
        category="GCP Compute",
        observations=["Serverless container execution platform.", "Scales to zero."]
    )
    
    assert res["status"] in ["success", "warning_local_only"]
    assert res["entity"]["name"] == "Cloud Run"
    assert res["entity"]["category"] == "GCP Compute"
    assert len(res["entity"]["observations"]) == 2


@pytest.mark.asyncio
async def test_remember_entity_append():
    await remember_entity(
        name="GCP Cloud Run",
        category="Compute",
        observations=["Supports WebSockets and SSE."]
    )
    
    res = await remember_entity(
        name="GCP Cloud Run",
        category="Compute",
        observations=["Supports WebSockets and SSE.", "Pay per request billing."]
    )
    
    # Check deduplication
    assert len(res["entity"]["observations"]) == 2
    assert "Pay per request billing." in res["entity"]["observations"]


@pytest.mark.asyncio
async def test_recall_entities_query():
    await remember_entity("FastAPI", "Framework", ["High performance Python web framework."])
    await remember_entity("Cloud Run", "Compute", ["Google Cloud serverless hosting."])

    # Recall all
    all_res = await recall_entities()
    assert all_res["total_count"] == 2

    # Query filter match
    fastapi_res = await recall_entities(query="FastAPI")
    assert fastapi_res["matches"] == 1
    assert "FastAPI" in fastapi_res["entities"]

    # Query filter non-match
    none_res = await recall_entities(query="NonExistent")
    assert none_res["matches"] == 0
