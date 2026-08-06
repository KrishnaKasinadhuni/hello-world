import os
import json
import uuid
import asyncio
from typing import Dict, Any, List, Optional
from fastapi import FastAPI, Depends, Request, HTTPException, Query
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import StreamingResponse
from pydantic import BaseModel, Field

from auth import verify_oauth_token, DISABLE_AUTH, GOOGLE_CLIENT_ID
from tools.web_fetch import fetch_web_markdown
from tools.memory import remember_entity, recall_entities, prune_expired_memories
from tools.radar import run_tech_radar

app = FastAPI(
    title="GCP Cloud Run MCP Gateway",
    description="Hosted Remote Model Context Protocol (MCP) Gateway with Google OAuth 2.0 OIDC Authentication & Tech Radar.",
    version="1.1.0"
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Active SSE Session Queues
sse_sessions: Dict[str, asyncio.Queue] = {}

# Tool Definitions
TOOLS_MANIFEST = [
    {
        "name": "fetch_web_page",
        "description": "Fetches a web URL and converts HTML content into clean Markdown text.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "url": {"type": "string", "description": "Target web URL to fetch."},
                "max_chars": {"type": "integer", "description": "Maximum character limit for output text.", "default": 10000}
            },
            "required": ["url"]
        }
    },
    {
        "name": "store_memory",
        "description": "Stores a fact or observation into long-term knowledge graph memory with automatic TTL retention and secret sanitization.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "name": {"type": "string", "description": "Entity name (e.g. 'Cloud Run')."},
                "category": {"type": "string", "description": "Category or type (e.g. 'Infrastructure')."},
                "observation": {"type": "string", "description": "Factual note or observation."},
                "ttl_days": {"type": "integer", "description": "Retention period in days (default 90).", "default": 90},
                "pinned": {"type": "boolean", "description": "If True, prevents automatic expiration.", "default": False}
            },
            "required": ["name", "category", "observation"]
        }
    },
    {
        "name": "query_memory",
        "description": "Recalls facts and entities from long-term knowledge graph memory.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "query": {"type": "string", "description": "Search filter query string.", "default": ""}
            }
        }
    },
    {
        "name": "run_tech_radar",
        "description": "Monitors target web URLs (docs/release notes), parses updates, and indexes intelligence facts into knowledge graph memory with TTL retention.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "urls": {
                    "type": "array",
                    "items": {"type": "string"},
                    "description": "List of URLs to monitor and ingest."
                },
                "category": {"type": "string", "description": "Category tag for intelligence entities.", "default": "Tech Intelligence"},
                "ttl_days": {"type": "integer", "description": "Retention TTL in days (default 90).", "default": 90}
            },
            "required": ["urls"]
        }
    },
    {
        "name": "prune_memory",
        "description": "Prunes expired unpinned entity entries from knowledge graph memory based on retention policies.",
        "inputSchema": {
            "type": "object",
            "properties": {}
        }
    }
]

async def execute_tool(name: str, arguments: Dict[str, Any]) -> str:
    """Executes the requested tool name with provided arguments."""
    if name == "fetch_web_page":
        url = arguments.get("url")
        max_chars = arguments.get("max_chars", 10000)
        res = await fetch_web_markdown(url, max_chars=max_chars)
        if "error" in res:
            return f"Error fetching URL: {res['error']}"
        return f"# {res.get('title')}\nURL: {res.get('url')}\n\n{res.get('markdown')}"

    elif name == "store_memory":
        entity_name = arguments.get("name")
        category = arguments.get("category")
        observation = arguments.get("observation")
        ttl_days = arguments.get("ttl_days", 90)
        pinned = arguments.get("pinned", False)
        res = await remember_entity(entity_name, category, [observation], ttl_days=ttl_days, pinned=pinned)
        return f"Successfully stored memory for '{entity_name}' [{category}]: {observation}"

    elif name == "query_memory":
        q = arguments.get("query", "")
        res = await recall_entities(query=q)
        entities = res.get("entities", {})
        if not entities:
            return f"No memories found matching query '{q}'."
        out = [f"Found {len(entities)} memory entry/entries:"]
        for item_name, item in entities.items():
            out.append(f"- **{item_name}** ({item.get('category')}): {', '.join(item.get('observations', []))}")
        return "\n".join(out)

    elif name == "run_tech_radar":
        urls = arguments.get("urls", [])
        cat = arguments.get("category", "Tech Intelligence")
        ttl = arguments.get("ttl_days", 90)
        res = await run_tech_radar(urls=urls, category=cat, ttl_days=ttl)
        indexed = res.get("indexed_entities", [])
        return f"Tech Radar completed! Processed {res.get('processed_count')} URLs and indexed {res.get('indexed_count')} entities into memory:\n- " + "\n- ".join(indexed)

    elif name == "prune_memory":
        res = await prune_expired_memories()
        return f"Memory pruning complete: Pruned {res.get('pruned_count')} expired entity/entities. Retained {res.get('retained_count')} active entries."

    else:
        raise ValueError(f"Unknown tool name: '{name}'")


# Health Check Endpoints
@app.get("/", tags=["Health"])
@app.get("/health", tags=["Health"])
async def health_check():
    return {
        "status": "ok",
        "service": "GCP Cloud Run MCP Gateway",
        "auth_enabled": not DISABLE_AUTH,
        "google_client_id_configured": bool(GOOGLE_CLIENT_ID),
        "tools_count": len(TOOLS_MANIFEST)
    }

# OAuth Token Verification Endpoint
@app.get("/api/auth/verify", tags=["Auth"])
async def verify_auth(user: dict = Depends(verify_oauth_token)):
    return {"status": "authenticated", "user": user}

# REST API Tool Endpoints
@app.get("/api/tools", tags=["Tools"])
async def list_tools(user: dict = Depends(verify_oauth_token)):
    return {"tools": TOOLS_MANIFEST}

class ToolCallRequest(BaseModel):
    name: str = Field(..., description="Name of the tool to execute.")
    arguments: Dict[str, Any] = Field(default_factory=dict, description="Arguments dictionary for the tool.")

@app.post("/api/tools/call", tags=["Tools"])
async def call_tool_api(body: ToolCallRequest, user: dict = Depends(verify_oauth_token)):
    try:
        result_text = await execute_tool(body.name, body.arguments)
        return {
            "success": True,
            "tool": body.name,
            "result": result_text
        }
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))

@app.post("/api/memory/prune", tags=["Memory Management"])
async def prune_memory_api(user: dict = Depends(verify_oauth_token)):
    """API endpoint to prune expired, unpinned memories based on retention policy."""
    res = await prune_expired_memories()
    return res

# MCP Remote Transport (SSE + JSON-RPC)
@app.get("/sse", tags=["MCP Remote Transport"])
async def handle_sse(request: Request, user: dict = Depends(verify_oauth_token)):
    session_id = str(uuid.uuid4())
    queue = asyncio.Queue()
    sse_sessions[session_id] = queue

    async def event_generator():
        try:
            endpoint_url = f"/messages?session_id={session_id}"
            yield f"event: endpoint\ndata: {endpoint_url}\n\n"

            while True:
                if await request.is_disconnected():
                    break
                try:
                    data = await asyncio.wait_for(queue.get(), timeout=20.0)
                    yield f"event: message\ndata: {json.dumps(data)}\n\n"
                except asyncio.TimeoutError:
                    yield ": keep-alive\n\n"
        finally:
            sse_sessions.pop(session_id, None)

    return StreamingResponse(event_generator(), media_type="text/event-stream")

@app.post("/messages", tags=["MCP Remote Transport"])
async def handle_messages(request: Request, session_id: str = Query(...)):
    if session_id not in sse_sessions:
        raise HTTPException(status_code=404, detail="SSE session not found or expired.")

    queue = sse_sessions[session_id]
    data = await request.json()

    msg_id = data.get("id")
    method = data.get("method")
    params = data.get("params", {})

    if method == "initialize":
        response = {
            "jsonrpc": "2.0",
            "id": msg_id,
            "result": {
                "protocolVersion": "2024-11-05",
                "capabilities": {"tools": {}},
                "serverInfo": {"name": "GCP Cloud Run Remote MCP Gateway", "version": "1.1.0"}
            }
        }
        await queue.put(response)

    elif method == "notifications/initialized":
        pass

    elif method == "tools/list":
        response = {
            "jsonrpc": "2.0",
            "id": msg_id,
            "result": {"tools": TOOLS_MANIFEST}
        }
        await queue.put(response)

    elif method == "tools/call":
        tool_name = params.get("name")
        arguments = params.get("arguments", {})
        try:
            res_text = await execute_tool(tool_name, arguments)
            response = {
                "jsonrpc": "2.0",
                "id": msg_id,
                "result": {
                    "content": [{"type": "text", "text": res_text}]
                }
            }
        except Exception as err:
            response = {
                "jsonrpc": "2.0",
                "id": msg_id,
                "error": {"code": -32603, "message": str(err)}
            }
        await queue.put(response)

    else:
        response = {
            "jsonrpc": "2.0",
            "id": msg_id,
            "error": {"code": -32601, "message": f"Method '{method}' not found."}
        }
        await queue.put(response)

    return {"status": "accepted"}


if __name__ == "__main__":
    port = int(os.getenv("PORT", 8080))
    print(f"Starting MCP Gateway Uvicorn server on port {port}...")
    uvicorn.run("main:app", host="0.0.0.0", port=port, reload=True)
