# hello-world

> **A personal sandbox and learning repository** — started as a "learn to code" journey (The Odin Project), evolved into a collection of POCs spanning AI/ML, AWS infrastructure, image processing, and agentic systems. Every folder here represents an idea, experiment, or working prototype.

---

## 🔗 Git & Remote References

| | |
|---|---|
| **Remote** | `origin` |
| **Fetch / Push URL** | `https://github.com/KrishnaKasinadhuni/hello-world.git` |
| **Default branch** | `main` |
| **Active branches** | `main`, `feat/cdk-dynamo` (in-progress CDK + DynamoDB work) |

---

## 🗺️ Repository Map

```
hello-world/
├── frontend/                         # React + TypeScript image classification UI (working)
├── reverse-image-search-aws/         # Full-stack reverse image search engine on AWS (partial POC)
│   ├── backend/                      #   → FastAPI + Bedrock + OpenSearch (working core, placeholders in search)
│   ├── frontend/                     #   → scaffold only (not implemented)
│   └── infrastructure/               #   → AWS CDK scaffold (not implemented)
├── cdk-patterns/                     # CDK TypeScript boilerplate (blank stack, scaffold for patterns)
├── services/
│   ├── aws/
│   │   └── iam-setup/                # AWS IAM scripts: user/role/group setup + assume-role.sh
│   ├── nodejs/
│   │   └── imageClassification/      # Node.js + Hapi + Cohere AI image classifier (working POC)
│   ├── python/
│   │   ├── logAnalyzer/              # Python ML log analyzer (working POC, Dockerized)
│   │   └── configManager/            # Python config manager service (working POC, Dockerized)
│   └── curls/
│       └── helloworld-apigw.sh       # Curl snippet for a deployed API Gateway endpoint
└── mcp-gateway-instructions/         # Structured agent-readable instructions for building an MCP Gateway
```

---

## 📦 Projects

### 1. `frontend/` — Image Classification & Similarity UI
**Status:** ✅ Working POC  
**Stack:** React 19, TypeScript, MUI v7, MobX, Axios  
**What it does:**  
A full-featured web UI for uploading, classifying, and comparing two images side by side. The app proxies to the Node.js image classification backend.

**Key capabilities:**
- Drag-and-drop or click-to-upload for two images simultaneously
- Real-time AI classification with confidence scores per image
- Three comparison methods: Basic, Histogram, Feature Matching
- Image filters (brightness, contrast, saturation) with toggle
- Similarity threshold slider
- Zoomable image preview dialogs
- Image metadata viewer (dimensions, size, type, modified date)
- Comparison history panel
- Dark / Light mode toggle

**Architecture:**
- State management via MobX `ImageStore` (`src/stores/ImageStore.ts`)
- Proxied API calls to `localhost:3000` (Node image classification service) via `setupProxy.js`
- Single-file component (`App.tsx`) — candidate for componentization

**To run:**
```bash
cd frontend
npm install
npm start        # http://localhost:3001 (proxies to :3000)
```

---

### 2. `reverse-image-search-aws/` — AWS Reverse Image Search Engine
**Status:** 🔨 Partial POC — backend core works, AWS integrations are placeholders  
**Reference:** [AWS Blog: Reverse Image Search with Amazon Titan + Bedrock](https://aws.amazon.com/blogs/machine-learning/build-a-reverse-image-search-engine-with-amazon-titan-multimodal-embeddings-in-amazon-bedrock-and-aws-managed-services/)

#### `backend/` — FastAPI + AWS Bedrock + OpenSearch
**Status:** ✅ API scaffold complete, ⚠️ OpenSearch indexing/search are placeholder stubs  
**Stack:** Python, FastAPI, Uvicorn, boto3, Pillow  
**AWS Services:** S3, Amazon Bedrock (Titan Multimodal Embeddings), OpenSearch Serverless

**Endpoints:**
| Method | Path | Status |
|--------|------|--------|
| `GET` | `/` | ✅ Health check |
| `POST` | `/index` | ✅ Uploads to S3 + generates Bedrock embedding (OpenSearch write is a stub) |
| `POST` | `/search` | ✅ Generates query embedding (OpenSearch k-NN search is a stub) |
| `POST` | `/extract` | 🚫 501 — Rekognition object detection not yet implemented |

**What works today:**
- Image validation (size limit, format via Pillow)
- S3 upload via `boto3`
- Embedding generation via Amazon Titan Multimodal (`amazon.titan-embed-image-v1`)
- FastAPI schema + response models

**What's stubbed / TODO:**
- OpenSearch Serverless indexing (`index_image_in_opensearch` is commented-out)
- OpenSearch k-NN search (`search_similar_images` returns dummy data)
- Pre-computed embedding ingestion endpoint
- Rekognition object extraction

**To run:**
```bash
cd reverse-image-search-aws/backend
cp .env.example .env       # fill in AWS creds, S3_BUCKET_NAME, OPENSEARCH_COLLECTION_ENDPOINT
pip install -r requirements.txt
python main.py             # http://localhost:8000/docs
# OR
docker build -t ris-backend .
docker run -p 8000:8000 --env-file .env ris-backend
```

#### `frontend/` — React UI (scaffold only)
**Status:** 📋 Scaffold — contains only a placeholder `README.md`. Not implemented.  
**Intent:** A dedicated React search UI for the reverse image search engine (separate from the `frontend/` root app).

#### `infrastructure/` — AWS CDK (scaffold only)
**Status:** 📋 Scaffold — CDK TypeScript project initialized with no resources defined.  
**Intent:** Provision all AWS resources (S3 bucket, OpenSearch Serverless collection, Bedrock access, IAM roles) via CDK.

---

### 3. `cdk-patterns/` — AWS CDK TypeScript Boilerplate
**Status:** 📋 Scaffold — blank CDK stack, no resources deployed  
**Stack:** AWS CDK v2, TypeScript, Jest  
**Branch with active work:** `feat/cdk-dynamo` (DynamoDB pattern in progress)

**Intent:** A playground for learning and prototyping reusable AWS CDK infrastructure patterns (queues, tables, lambdas, etc.). The `CdkPatternsStack` is intentionally empty — add patterns here.

**Commands:**
```bash
cd cdk-patterns
npm install
npx cdk synth       # preview CloudFormation template
npx cdk deploy      # deploy to AWS (account: 908027415245, region: us-east-1)
npx cdk diff        # diff against deployed stack
```

---

### 4. `services/nodejs/imageClassification/` — Node.js AI Image Classification Service
**Status:** ✅ Working POC  
**Stack:** Node.js (v18+), Hapi.js server, Cohere AI (embeddings + classification), Docker  
**What it does:** A REST API that accepts image uploads, runs them through Cohere's models to classify content and find visually similar images.

**Endpoints:**
| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/health` | Service status |
| `POST` | `/api/upload` | Upload image → returns embedding |
| `POST` | `/api/classify` | Classify image → returns text description |
| `POST` | `/api/similar` | Find top 5 similar images by embedding |

**This is the backend the root `frontend/` proxies to.**

**To run:**
```bash
cd services/nodejs/imageClassification
npm install
echo "COHERE_API_KEY=your_key" > .env
npm run dev          # http://localhost:3000
# OR
docker-compose up
```

---

### 5. `services/python/logAnalyzer/` — Python ML Log Analyzer
**Status:** ✅ Working POC (Dockerized)  
**Stack:** Python, scikit-learn, Docker Compose  
**What it does:** Analyzes log files using machine learning to detect anomalies or patterns.

**To run:**
```bash
cd services/python/logAnalyzer
docker-compose up
```

---

### 6. `services/python/configManager/` — Python Config Manager
**Status:** ✅ Working POC (Dockerized)  
**Stack:** Python, Docker Compose  
**What it does:** A configuration management microservice with environment-driven settings.

**To run:**
```bash
cd services/python/configManager
cp .env.example .env
docker-compose up
```

---

### 7. `services/aws/iam-setup/` — AWS IAM Setup & Role Assumption
**Status:** ✅ Implemented and in active use  
**What it does:** Documents and scripts for setting up IAM users, groups, and roles for safe AWS access.

**Key file: `scripts/assume-role.sh`**  
The shell script that assumes `admin-access-role` (ARN: `arn:aws:iam::908027415245:role/admin-access-role`) and refreshes `~/.aws/credentials` for the `assumed-admin` profile. Referenced by the `assume-role` alias in `~/.zshrc`.

**AWS account:** `908027415245`  
**IAM user:** `application-admin`  
**Role:** `admin-access-role` (1-hour session tokens)

> 💡 Use `refresh-admin` (added to `~/.zshrc`) to refresh the `assumed-admin` profile without leaving your terminal.

---

### 8. `services/curls/helloworld-apigw.sh` — API Gateway Curl Snippet
**Status:** ✅ Reference script  
**What it does:** Fetches an API key from AWS Secrets Manager and calls a deployed API Gateway endpoint.  
**Endpoint:** `https://9se90ih4xg.execute-api.us-east-2.amazonaws.com/dev/`  
**Region:** `us-east-2`

---

### 9. `mcp-gateway-instructions/` — MCP Gateway Agent Instructions
**Status:** 📋 Instruction set — no code, fully agent-readable  
**What it is:** A 6-phase, numbered markdown instruction set designed for AI agents to build a complete **MCP (Model Context Protocol) Gateway** from scratch.

**Phases:**
1. **Setup** — Project structure, dependencies, base Docker config
2. **Core Gateway** — Architecture, API server, MCP server registry, request routing
3. **Security** — JWT auth, RBAC, TLS/SSL, network isolation, rate limiting, sandboxing, audit logging
4. **Deployment** — Full Docker Compose, env config, health checks, production guide
5. **Testing** — Unit, integration, and security tests
6. **Documentation** — API docs and operations guide

**Tech stack described:** FastAPI or Express.js gateway, PostgreSQL, Redis, Nginx, Docker Compose, Let's Encrypt

> 🤖 **For Agents:** Navigate `mcp-gateway-instructions/` in numerical order (01→06). Each file is self-contained with prerequisites, code examples, and verification steps.

---

## 🔧 AWS Account & Infrastructure Context

| Resource | Value |
|---|---|
| **AWS Account** | `908027415245` |
| **Default region** | `us-east-1` |
| **IAM user** | `application-admin` |
| **Admin role** | `arn:aws:iam::908027415245:role/admin-access-role` |
| **Active AWS profile** | `assumed-admin` (set via `AWS_PROFILE` in `~/.zshrc`) |
| **API Gateway (us-east-2)** | `https://9se90ih4xg.execute-api.us-east-2.amazonaws.com/dev/` |

---

## 🚦 Project Status Summary

| Project | Type | Status |
|---|---|---|
| `frontend/` | React UI | ✅ Working POC |
| `reverse-image-search-aws/backend/` | Python FastAPI | 🔨 Partial (OpenSearch stubbed) |
| `reverse-image-search-aws/frontend/` | React UI | 📋 Scaffold only |
| `reverse-image-search-aws/infrastructure/` | AWS CDK | 📋 Scaffold only |
| `cdk-patterns/` | AWS CDK | 📋 Scaffold (DynamoDB WIP on branch) |
| `services/nodejs/imageClassification/` | Node.js API | ✅ Working POC |
| `services/python/logAnalyzer/` | Python ML | ✅ Working POC |
| `services/python/configManager/` | Python service | ✅ Working POC |
| `services/aws/iam-setup/` | Bash scripts | ✅ In active use |
| `services/curls/` | Shell scripts | ✅ Reference |
| `mcp-gateway-instructions/` | Agent instructions | ✅ Complete instruction set |

---

## 🤖 Notes for AI Agents

- **Scaffolds are intentional:** `reverse-image-search-aws/frontend/`, `reverse-image-search-aws/infrastructure/`, and `cdk-patterns/` contain boilerplate/empty stubs. They exist as placeholders for planned future work — do not treat them as complete implementations.
- **The root `frontend/` is the primary working UI** and connects to `services/nodejs/imageClassification/` via a dev proxy.
- **OpenSearch integration in `reverse-image-search-aws/backend/`** is commented out in `app/search.py`. The S3 + Bedrock embedding pipeline works; the k-NN indexing and search need `opensearch-py` and AWS SigV4 signing to be wired up.
- **AWS credentials** are managed via the `assumed-admin` profile. Sessions expire every hour — use `refresh-admin` to renew.
- **Branch `feat/cdk-dynamo`** has in-progress CDK DynamoDB patterns — check it out before adding new CDK resources.
- **`mcp-gateway-instructions/`** is a standalone instruction corpus for agents — treat each numbered markdown file as an executable task step.
