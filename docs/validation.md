# Validation Guide

This document lists the specific validation commands and checks required before committing or opening a pull request in `hello-world`.

---

## 🧪 Validation Commands by Subproject

### 1. Root React Frontend (`frontend/`)
```bash
cd frontend

# Install dependencies if needed
npm install

# Run unit tests in non-interactive mode
npm test -- --watchAll=false

# Validate production build bundle
npm run build
```

### 2. Node.js Image Classification Service (`services/nodejs/imageClassification/`)
```bash
cd services/nodejs/imageClassification

# Install dependencies if needed
npm install

# Syntax check main entrypoint
node --check src/server.js

# Test running in dev mode (requires COHERE_API_KEY in .env)
npm run dev

# Run automated upload/classify test scripts (if backend is running)
node test-images/upload-test.js /path/to/test.jpg
```

### 3. Reverse Image Search Backend (`reverse-image-search-aws/backend/`)
```bash
cd reverse-image-search-aws/backend

# Verify Python syntax across app modules
python3 -m py_compile main.py app/*.py

# Check local startup (requires AWS credentials & .env)
python main.py

# Docker build test
docker build -t ris-backend .
```

### 4. CDK Infrastructure (`cdk-patterns/`)
```bash
cd cdk-patterns

# Compile TypeScript and run Jest unit tests
npm run build
npm run test

# Synthesize CloudFormation template
npm run synth

# Diff changes against deployed stack
npm run diff

# Dev deployment (Express mode - rapid iteration, rollback disabled by default)
npm run deploy:dev

# Production deployment (Standard mode - full stabilization and automatic rollback)
npm run deploy:prod
```

### 5. Python Microservices (`services/python/logAnalyzer/`, `services/python/configManager/`)
```bash
# Log Analyzer
cd services/python/logAnalyzer
python3 -m py_compile src/*.py

# Config Manager
cd services/python/configManager
python3 -m py_compile src/*.py
```

---

## 📋 Pre-Commit & Pre-PR Checklist

Before submitting changes:

- [ ] **No Hardcoded Credentials:** Verify no AWS keys, Cohere API keys, or secret strings are committed.
- [ ] **Subproject Isolation:** Verify `package.json` or `requirements.txt` changes are isolated to their specific subproject directory.
- [ ] **Build / Compilation:** Confirm the modified subproject compiles cleanly.
- [ ] **Documentation Sync:** If folder structure, commands, or feature status changed, update:
  - `README.md`
  - `AGENTS.md`
  - `CLAUDE.md`
  - `docs/architecture.md`
