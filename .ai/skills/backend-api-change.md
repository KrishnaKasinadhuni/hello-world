# Playbook: Backend API Change

Use this playbook when modifying backend APIs (`services/nodejs/imageClassification/` or `reverse-image-search-aws/backend/`).

---

## Workflow Steps

1. **Node.js Service (`services/nodejs/imageClassification/`):**
   - Routes in `src/routes/`.
   - Ensure compatibility with `frontend/src/setupProxy.js`.
   - Validate with `node --check src/server.js` or run `npm run dev`.

2. **Python RIS Backend (`reverse-image-search-aws/backend/`):**
   - Endpoints in `main.py` and logic in `app/search.py`.
   - Remember OpenSearch k-NN search/indexing functions are currently placeholder stubs.
   - Validate with `python3 -m py_compile main.py app/*.py`.

3. **Verify API Contracts:**
   - Update API documentation or OpenAPI schemas if endpoint request/response payloads change.
