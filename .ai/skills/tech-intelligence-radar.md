---
name: tech-intelligence-radar
description: Automatically monitors technology release notes, developer documentation, and framework updates (GCP Cloud Run, AWS CDK, React, FastAPI) using the hosted Tech Intelligence Radar MCP gateway, indexing updates into long-term knowledge graph memory with TTL retention.
---

# Skill: Tech Intelligence Radar

Use this skill whenever the user asks to monitor release notes, check for framework or cloud updates, track breaking changes, or build/query long-term developer knowledge.

---

## 🛰️ Hosted Gateway Configuration

- **Cloud Run Service Endpoint:** `https://mcp-gateway-boq6jlznga-uc.a.run.app`
- **GCP Project / Region:** `precise-works-456015-h9` / `us-central1`
- **Authentication:** Google OAuth 2.0 Bearer Token via `Authorization: Bearer <ID_TOKEN>` header (or local dev mode with `DISABLE_AUTH=true`).

---

## 🎯 Default Monitoring Targets

| Technology | Category | Target URL |
|------------|----------|------------|
| **GCP Cloud Run** | GCP Compute | `https://cloud.google.com/run/docs/release-notes` |
| **AWS CDK v2** | Infrastructure | `https://github.com/aws/aws-cdk/releases` |
| **FastAPI** | Python Framework | `https://fastapi.tiangolo.com/release-notes/` |
| **React** | Frontend UI | `https://react.dev/blog` |
| **Model Context Protocol** | AI Tooling | `https://modelcontextprotocol.io/` |

---

## 🔄 Execution Workflow

### Step 1: Trigger Radar Ingestion (`run_tech_radar`)
Call the `run_tech_radar` tool over REST or MCP transport:

```bash
curl -X POST https://mcp-gateway-boq6jlznga-uc.a.run.app/api/tools/call \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <YOUR_GOOGLE_ID_TOKEN>" \
  -d '{
    "name": "run_tech_radar",
    "arguments": {
      "urls": [
        "https://cloud.google.com/run/docs/release-notes",
        "https://fastapi.tiangolo.com/release-notes/"
      ],
      "category": "Core Stack Updates",
      "ttl_days": 30
    }
  }'
```

### Step 2: Query Knowledge Graph Memory (`query_memory`)
Search indexed intelligence entities stored in long-term memory:

```bash
curl -X POST https://mcp-gateway-boq6jlznga-uc.a.run.app/api/tools/call \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <YOUR_GOOGLE_ID_TOKEN>" \
  -d '{
    "name": "query_memory",
    "arguments": {
      "query": "Cloud Run"
    }
  }'
```

### Step 3: Synthesize Developer Briefing
Present the ingested observations in a structured briefing table:
- **Service / Library**
- **New Feature / Deprecation**
- **Impact on `hello-world` Repository**
- **Actionable Upgrade Step**

---

## 🧹 Memory Maintenance & Pruning

To prune expired unpinned intelligence entries past their 90-day retention TTL, call `prune_memory`:

```bash
curl -X POST https://mcp-gateway-boq6jlznga-uc.a.run.app/api/memory/prune \
  -H "Authorization: Bearer <YOUR_GOOGLE_ID_TOKEN>"
```
