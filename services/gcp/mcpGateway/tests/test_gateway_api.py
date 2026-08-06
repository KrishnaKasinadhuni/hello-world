import os
import pytest
from unittest.mock import patch, MagicMock
from fastapi.testclient import TestClient
from main import app

client = TestClient(app)

def test_health_check():
    response = client.get("/health")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "ok"
    assert data["tools_count"] == 5


def test_auth_verify_unauthorized():
    with patch.dict(os.environ, {"DISABLE_AUTH": "false"}):
        response = client.get("/api/auth/verify")
        assert response.status_code == 401
        assert "Missing Authorization bearer token" in response.json()["detail"]


def test_auth_verify_disabled_auth():
    with patch("auth.DISABLE_AUTH", True):
        response = client.get("/api/auth/verify")
        assert response.status_code == 200
        data = response.json()
        assert data["status"] == "authenticated"
        assert data["user"]["email"] == "dev-user@example.com"


def test_auth_verify_email_whitelisted():
    mock_id_info = {
        "iss": "https://accounts.google.com",
        "email": "krishna.kasinadhuni@gmail.com",
        "sub": "user-123"
    }
    with patch("auth.DISABLE_AUTH", False), \
         patch("google.oauth2.id_token.verify_oauth2_token", return_value=mock_id_info):
        response = client.get(
            "/api/auth/verify",
            headers={"Authorization": "Bearer fake-valid-token"}
        )
        assert response.status_code == 200
        assert response.json()["user"]["email"] == "krishna.kasinadhuni@gmail.com"


def test_auth_verify_email_forbidden():
    mock_id_info = {
        "iss": "https://accounts.google.com",
        "email": "unauthorized-hacker@example.com",
        "sub": "user-456"
    }
    with patch("auth.DISABLE_AUTH", False), \
         patch("google.oauth2.id_token.verify_oauth2_token", return_value=mock_id_info):
        response = client.get(
            "/api/auth/verify",
            headers={"Authorization": "Bearer fake-unauthorized-token"}
        )
        assert response.status_code == 403
        assert "Access denied" in response.json()["detail"]


def test_list_tools_api():
    with patch("auth.DISABLE_AUTH", True):
        response = client.get("/api/tools")
        assert response.status_code == 200
        tools = response.json()["tools"]
        tool_names = [t["name"] for t in tools]
        assert "fetch_web_page" in tool_names
        assert "store_memory" in tool_names
        assert "query_memory" in tool_names
        assert "run_tech_radar" in tool_names
        assert "prune_memory" in tool_names


def test_call_tool_api_store_and_query():
    with patch("auth.DISABLE_AUTH", True):
        # 1. Call store_memory
        store_res = client.post(
            "/api/tools/call",
            json={
                "name": "store_memory",
                "arguments": {
                    "name": "Pytest Tool",
                    "category": "Testing",
                    "observation": "Verified via TestClient"
                }
            }
        )
        assert store_res.status_code == 200
        assert store_res.json()["success"] is True
        assert "Pytest Tool" in store_res.json()["result"]

        # 2. Call query_memory
        query_res = client.post(
            "/api/tools/call",
            json={
                "name": "query_memory",
                "arguments": {"query": "Pytest Tool"}
            }
        )
        assert query_res.status_code == 200
        assert query_res.json()["success"] is True
        assert "Pytest Tool" in query_res.json()["result"]


def test_mcp_jsonrpc_initialize():
    from main import sse_sessions
    import asyncio
    
    session_id = "test-session-123"
    queue = asyncio.Queue()
    sse_sessions[session_id] = queue

    try:
        response = client.post(
            f"/messages?session_id={session_id}",
            json={
                "jsonrpc": "2.0",
                "id": 1,
                "method": "initialize",
                "params": {}
            }
        )
        assert response.status_code == 200
        assert response.json()["status"] == "accepted"
        
        res_msg = queue.get_nowait()
        assert res_msg["id"] == 1
        assert res_msg["result"]["protocolVersion"] == "2024-11-05"
    finally:
        sse_sessions.pop(session_id, None)
