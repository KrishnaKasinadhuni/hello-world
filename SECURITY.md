# Security Policy

## Supported Subprojects

Security updates are actively applied to the following subprojects on the `main` branch:

| Subproject | Path | Status |
| :--- | :--- | :--- |
| Quest 3 Tennis Trainer | `quest-tennis-trainer/` | Active |
| GCP Remote MCP Gateway | `services/gcp/mcpGateway/` | Active |
| Rust & TypeScript 7 Blockchain | `services/blockchain/` | Active |
| AWS CDK Infrastructure Patterns | `cdk-patterns/` | Active |
| Node.js & Python Microservices | `services/nodejs/`, `services/python/` | Active |

## Reporting a Vulnerability

Please **do not** report security vulnerabilities through public GitHub issues.

Instead, please report them privately using [**GitHub Private Vulnerability Reporting**](https://github.com/KrishnaKasinadhuni/hello-world/security/advisories/new).

## Repository Security Standards

1. **No Secrets or Cloud Account Identifiers in Git:** Never commit `.env` files, API keys (`COHERE_API_KEY`), GCP service account JSON keys, or real 12-digit AWS account IDs.
2. **Keyless Cloud Auth in CI/CD:** All GitHub Actions cloud deployments use short-lived OIDC tokens via **Google Cloud Workload Identity Federation** (`id-token: write`) or AWS OIDC role assumption.
3. **Hardened Containers:** Containerized workloads use minimal/distroless base images (such as `mcr.microsoft.com/dotnet/runtime:8.0-noble-chiseled`), non-root users, and dropped Linux capabilities (`--cap-drop=ALL`).
4. **Least-Privilege Workflows:** All GitHub Actions workflows default to `permissions: contents: read`.
