# Agent Guidelines for `hello-world`

This document provides canonical repository-wide instructions for AI coding assistants (Claude, Cursor, Antigravity, etc.).

---

## 📅 Last Reviewed
- **Date:** 2026-07-22
- **Areas Inspected:** `frontend/`, `reverse-image-search-aws/` (`backend/`, `frontend/`, `infrastructure/`), `cdk-patterns/` (Express mode updated), `services/` (`aws/`, `nodejs/`, `python/`, `curls/`), `mcp-gateway-instructions/`.

---

## 🗺️ Repository Overview & Map

`hello-world` is a personal multi-stack sandbox and Proof-of-Concept (POC) repository containing distinct subprojects ranging from working microservices to initial infrastructure scaffolds.

```
hello-world/
├── frontend/                         # [WORKING POC] React 19 + TypeScript + MUI + MobX
├── reverse-image-search-aws/         # [PARTIAL POC] AWS Bedrock + S3 + OpenSearch backend engine
│   ├── backend/                      #   → FastAPI app (S3/Bedrock working, OpenSearch stubbed)
│   ├── frontend/                     #   → [SCAFFOLD] React UI placeholder
│   └── infrastructure/               #   → [SCAFFOLD] AWS CDK TypeScript stack placeholder
├── cdk-patterns/                     # [SCAFFOLD] AWS CDK TypeScript boilerplate stack (supports Express mode)
├── services/
│   ├── aws/iam-setup/                # [WORKING] AWS IAM admin setup & assume-role scripts
│   ├── nodejs/imageClassification/   # [WORKING POC] Node.js + Hapi + Cohere AI REST service
│   ├── python/
│   │   ├── logAnalyzer/              # [WORKING POC] Python ML log analyzer (Dockerized)
│   │   └── configManager/            # [WORKING POC] Python config manager (Dockerized)
│   ├── gcp/mcpGateway/               # [WORKING] GCP Cloud Run Remote MCP Gateway (FastAPI + Google OAuth)
│   └── curls/
│       └── helloworld-apigw.sh       # [WORKING] Sample script querying API Gateway
├── mcp-gateway-instructions/         # [DOCS-ONLY] Step-by-step instructions for building an MCP Gateway
├── docs/                             # Repository architecture and validation docs
├── .ai/skills/                       # Task playbooks for agent workflows
└── .cursor/rules/                    # Domain-specific Cursor rule files
```

---

## 🚦 Subproject Classification & Edit Boundaries

| Path | Stack | Classification | Purpose & Edit Scope |
|---|---|---|---|
| `frontend/` | React 19, TypeScript, MUI v7, MobX | Working POC | Main image comparison & classification web app. Proxies to `localhost:3000`. |
| `reverse-image-search-aws/backend/` | Python 3.10+, FastAPI, boto3, Pillow | Partial POC | AWS Bedrock Titan embedding + S3 indexing. *Note: OpenSearch functions in `app/search.py` are stubs.* |
| `reverse-image-search-aws/frontend/` | README only | Scaffold | Planned dedicated reverse image search frontend. Do not assume working React code here. |
| `reverse-image-search-aws/infrastructure/` | AWS CDK, TypeScript | Scaffold | Planned AWS resource provisioning. Currently initialized empty CDK app. |
| `cdk-patterns/` | AWS CDK v2, TypeScript | Scaffold | Infrastructure pattern playground. Supports `npm run deploy:dev` (Express Mode) & `npm run deploy:prod` (Standard Mode). Active work on `feat/cdk-dynamo`. |
| `services/aws/iam-setup/` | Bash, AWS CLI | Working Tooling | System admin IAM role assumption (`admin-access-role`). Used by `assume-role.sh`. |
| `services/nodejs/imageClassification/` | Node.js v18+, Hapi, Cohere SDK | Working POC | Node backend powering Cohere AI image uploads & similarity. Target of `frontend/` proxy. |
| `services/python/logAnalyzer/` | Python, scikit-learn, Docker | Working POC | Log anomaly detection microservice. |
| `services/python/configManager/` | Python, Docker | Working POC | Configuration management service. |
| `services/gcp/mcpGateway/` | Python 3.11, FastAPI, Google OAuth 2.0 | Working Service | Remote MCP Gateway on GCP Cloud Run with SSE & FastMCP tools. |
| `services/curls/` | Bash, AWS CLI | Reference Script | Shell scripts testing external AWS API Gateway endpoints. |
| `mcp-gateway-instructions/` | Markdown | Docs-Only | 6-phase instruction set for building an MCP Gateway. Contains NO executable code. |

---

## 🛡️ Safe Editing Rules & Guidelines

1. **Respect Project Boundaries:** Do not cross-import dependencies or write code across subproject boundaries without clear intent. Each directory under `services/`, `frontend/`, `cdk-patterns/`, and `reverse-image-search-aws/` has its own package manifest or `requirements.txt`.
2. **Explicit Uncertainty:** Mark scaffolds and partial implementations clearly. Do not write production deployment code expecting `reverse-image-search-aws/infrastructure/` or `reverse-image-search-aws/frontend/` to already contain implemented code.
3. **No Environment/Secret Hardcoding:** Never hardcode AWS keys, Cohere API keys (`COHERE_API_KEY`), or Secret Strings in code. Use environment variables or `.env` files matching `.env.example`.
4. **Preserve API Contracts:** The root `frontend/` relies on API contracts exposed by `services/nodejs/imageClassification/` (ports 3001 -> 3000 proxy via `setupProxy.js`). Keep endpoint responses compatible (`/api/upload`, `/api/classify`, `/api/similar`).
5. **AWS Credential Isolation:** Use profile `assumed-admin` for CLI operations. Do not hardcode account `908027415245` credentials.

---

## 🧪 Validation & Command Rules

Before finishing any task, run the appropriate validation commands:

- **Frontend React (`frontend/`):**
  ```bash
  cd frontend && npm test -- --watchAll=false
  ```
- **Node.js Service (`services/nodejs/imageClassification/`):**
  ```bash
  cd services/nodejs/imageClassification && npm start # or check syntax via node --check
  ```
- **Python Services (`services/python/logAnalyzer/`, `services/python/configManager/`, `reverse-image-search-aws/backend/`):**
  ```bash
  python3 -m py_compile main.py
  ```
- **CDK Infrastructure (`cdk-patterns/`):**
  ```bash
  cd cdk-patterns && npm run build && npm test
  # Dev deployment (Express mode): cd cdk-patterns && npm run deploy:dev
  # Prod deployment (Standard mode): cd cdk-patterns && npm run deploy:prod
  ```

See [docs/validation.md](file:///Users/krishnakasinadhuni/projects/hello-world/docs/validation.md) for complete details.

---

## 📝 Documentation Update Policy

Whenever repository structure, commands, or subproject statuses change:
1. Update `README.md` to reflect new services or changed features.
2. Update `AGENTS.md` (and `CLAUDE.md`) under the "Last Reviewed" section with the date and revised map.
3. Update relevant `.cursor/rules/` and `docs/` files if architectural boundaries change.
