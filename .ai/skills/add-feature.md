# Playbook: Add Feature

Use this playbook when adding a new feature to any subproject in `hello-world`.

---

## Workflow Steps

1. **Identify Edit Boundary:**
   - Determine which subproject the feature belongs to (`frontend/`, `services/nodejs/imageClassification/`, `reverse-image-search-aws/backend/`, or `cdk-patterns/`).
   - Do NOT add dependencies to parent directories. Update only the subproject manifest (`package.json` or `requirements.txt`).

2. **Check Current Status:**
   - Verify if the target directory is an active POC or scaffold (refer to `AGENTS.md`).

3. **Implement Feature:**
   - Maintain environment variable patterns (`.env.example`).
   - Do not hardcode secret keys or AWS credentials.

4. **Validate & Test:**
   - Run the subproject validation commands listed in `docs/validation.md`.

5. **Update Docs:**
   - Follow `update-documentation.md` playbook to update `README.md` and agent instruction files.
