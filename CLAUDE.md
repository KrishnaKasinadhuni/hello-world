# CLAUDE.md — Agent Guidelines for Claude

This file provides concise project instructions for Claude, aligned with canonical guidelines in [AGENTS.md](file:///Users/krishnakasinadhuni/projects/hello-world/AGENTS.md).

---

## 🗺️ Repo Map & Subproject Status

- `frontend/` — **[WORKING POC]** React 19 + TypeScript + MUI + MobX. Proxies to `localhost:3000`.
- `services/nodejs/imageClassification/` — **[WORKING POC]** Node.js + Hapi + Cohere AI backend.
- `reverse-image-search-aws/backend/` — **[PARTIAL POC]** FastAPI + Bedrock embedding engine (S3 works, OpenSearch stubbed in `app/search.py`).
- `reverse-image-search-aws/frontend/` & `infrastructure/` — **[SCAFFOLDS]** Placeholders only.
- `cdk-patterns/` — **[SCAFFOLD]** AWS CDK TypeScript app. Active DynamoDB work on `feat/cdk-dynamo`.
- `services/python/logAnalyzer/` & `configManager/` — **[WORKING POCs]** Python Docker microservices.
- `services/aws/iam-setup/` — **[WORKING TOOLING]** IAM role assumption scripts (`admin-access-role`).
- `mcp-gateway-instructions/` — **[DOCS-ONLY]** 6-phase guide for building an MCP Gateway. No executable code.

---

## ⚡ Key Commands

```bash
# Frontend UI
cd frontend && npm start                     # Dev server (http://localhost:3001)
cd frontend && npm test -- --watchAll=false  # Unit tests

# Node.js Image Service
cd services/nodejs/imageClassification && npm run dev   # Server (http://localhost:3000)

# Reverse Image Search Backend
cd reverse-image-search-aws/backend && python main.py    # FastAPI (http://localhost:8000/docs)

# CDK Stack
cd cdk-patterns && npm run build && npm run synth
cd cdk-patterns && npm run deploy:dev   # Express mode (rapid iteration, rollback disabled)
cd cdk-patterns && npm run deploy:prod  # Standard mode (production safe)

# AWS Role Refresh
refresh-admin # or ./services/aws/iam-setup/scripts/assume-role.sh
```

---

## 🛡️ Coding Rules

1. **Isolation:** Do not share dependencies across subdirectories. Keep edit boundaries within the target subproject.
2. **Scaffolds:** Mark scaffolding explicitly (`reverse-image-search-aws/frontend/`, `infrastructure/`, `cdk-patterns/`). Do not assume scaffold directories contain working code.
3. **Secrets:** Never hardcode AWS keys, API keys, or tokens. Use `.env` files based on `.env.example`.
4. **Proxy Compatibility:** `frontend/` proxies API calls (`/api/upload`, `/api/classify`, `/api/similar`) to `services/nodejs/imageClassification/` on port 3000. Do not break these signatures.
5. **Docs Update:** Keep `README.md`, `AGENTS.md`, and `CLAUDE.md` updated when subprojects or commands change.
