# Playbook: Infrastructure Change

Use this playbook when modifying AWS CDK infrastructure (`cdk-patterns/` or `services/aws/iam-setup/`).

---

## Workflow Steps

1. **CDK Constructs (`cdk-patterns/`):**
   - Main stack is in `lib/cdk-patterns-stack.ts`.
   - Feature branches like `feat/cdk-dynamo` hold active pattern implementations.
   - Run `npm run build && npm test` inside `cdk-patterns/`.
   - Synthesize template with `npm run synth`.
   - Compare stack diff with `npm run diff`.
   - Rapid dev deployment: `npm run deploy:dev` (Express mode `cdk deploy --express`).
   - Standard prod deployment: `npm run deploy:prod` (Standard `cdk deploy`).

2. **IAM & AWS Role Management:**
   - Refer to `services/aws/iam-setup/README.md`.
   - Refresh role session credentials with `refresh-admin` shell command or `services/aws/iam-setup/scripts/assume-role.sh`.
   - Account ID: `908027415245`. Role: `admin-access-role`.
