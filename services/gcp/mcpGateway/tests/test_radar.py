import os
import pytest
from unittest.mock import patch, MagicMock
from tools.radar import run_tech_radar
from tools.memory import recall_entities, LOCAL_MEMORY_FILE

@pytest.fixture(autouse=True)
def cleanup_local_memory():
    if os.path.exists(LOCAL_MEMORY_FILE):
        os.remove(LOCAL_MEMORY_FILE)
    yield
    if os.path.exists(LOCAL_MEMORY_FILE):
        os.remove(LOCAL_MEMORY_FILE)

@pytest.mark.asyncio
async def test_run_tech_radar():
    sample_html = """
    <html>
      <head><title>Cloud Run Release Notes</title></head>
      <body>
        <h1>Cloud Run August 2026 Updates</h1>
        <p>Added native WebSocket support for all regions.</p>
        <p>Reduced cold start times by 30%.</p>
      </body>
    </html>
    """
    
    mock_res = MagicMock()
    mock_res.status_code = 200
    mock_res.text = sample_html
    mock_res.raise_for_status = MagicMock()

    with patch("httpx.AsyncClient.get", return_value=mock_res):
        radar_res = await run_tech_radar(
            urls=["https://cloud.google.com/run/docs/release-notes"],
            category="GCP Release",
            ttl_days=60
        )

    assert radar_res["status"] == "completed"
    assert radar_res["processed_count"] == 1
    assert radar_res["indexed_count"] == 1
    assert "Cloud Run Release Notes" in radar_res["indexed_entities"]

    # Verify entity stored in memory
    recalled = await recall_entities(query="Cloud Run Release Notes")
    assert recalled["matches"] >= 1
    entity = recalled["entities"]["Cloud Run Release Notes"]
    assert entity["category"] == "GCP Release"
    assert entity["expires_at"] is not None
