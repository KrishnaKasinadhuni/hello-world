# CDK Patterns (AWS CDK TypeScript Project)

This project provides AWS CDK infrastructure patterns built with TypeScript.

The `cdk.json` file tells the CDK Toolkit how to execute your app.

---

## 🚀 Available Scripts & Workflows

| Script / Command | Purpose | Mode / Behavior |
|---|---|---|
| `npm run build` | Compile TypeScript to JavaScript | Local build verification |
| `npm run watch` | Watch source files and recompile | Active development |
| `npm run test` | Run Jest unit tests | Automated testing |
| `npm run synth` | Synthesize CloudFormation template | `cdk synth` |
| `npm run diff` | Compare local stack with deployed state | `cdk diff` |
| `npm run deploy:dev` | Rapid development deployment | **Express mode (`cdk deploy --express`)** |
| `npm run deploy:prod` | Standard production-safe deployment | **Standard deployment (`cdk deploy`)** |
| `npm run deploy` | Standard deployment alias | `cdk deploy` |

---

## ⚡ Fast Development with CDK Express Mode (`npm run deploy:dev`)

CDK Express Mode (`cdk deploy --express`) optimizes local development iteration by streaming CloudFormation updates without waiting for prolonged resource stabilization checks.

### Why Express Mode is Faster
- **Optimized Deployment Flow:** Bypasses extended CloudFormation resource post-stabilization polling loops.
- **Fast Local Feedback:** Gives rapid confirmation when stack updates are applied.

### Key Caveats & Tradeoffs
> [!WARNING]
> - **Completes Before Full Stabilization:** Express mode reports completion as soon as resource changes are applied. Resource initialization/stabilization may still be settling in the background.
> - **Disabled Automatic Rollback:** Automatic rollback on deployment failure is **disabled by default** in Express mode to allow rapid inline debugging of failed states.
> - **Not for Production:** Do not use Express mode for production releases or environments requiring guaranteed CloudFormation state stabilization.

### When to Use Which Mode

- **Use `npm run deploy:dev` (Express Mode) during:**
  - Rapid local iteration and prototyping
  - Non-production development environments
  - Quick parameter or template updates

- **Use `npm run deploy:prod` (Standard Deployment) for:**
  - Production deployments
  - Staging / pre-production releases
  - Deployments requiring automatic rollback on failure and full CloudFormation resource stabilization
