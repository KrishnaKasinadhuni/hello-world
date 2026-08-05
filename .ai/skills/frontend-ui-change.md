# Playbook: Frontend UI Change

Use this playbook when editing the React UI in `frontend/`.

---

## Workflow Steps

1. **Check Component & Store Structure:**
   - App main view is in `frontend/src/App.tsx`.
   - MobX store is in `frontend/src/stores/ImageStore.ts`.
   - Proxy configuration is in `frontend/src/setupProxy.js`.

2. **Make UI Modifications:**
   - Use Material UI (MUI v7) components and ThemeProvider.
   - Maintain dark/light mode compatibility.
   - Ensure MobX `observer` is used on reactive UI components.

3. **Validate:**
   - Run `cd frontend && npm test -- --watchAll=false`
   - Run `cd frontend && npm run build` to verify production bundling.
