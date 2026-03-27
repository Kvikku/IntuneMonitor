# IntuneMonitor Documentation

Welcome to the IntuneMonitor documentation. These guides cover everything from first-time setup to advanced CI/CD integration.

> **New here?** Start with [Getting Started](getting-started.md) to have your first backup running in under 5 minutes.

## Guides

| Document | Description |
|---|---|
| [Getting Started](getting-started.md) | Prerequisites, app registration, first export |
| [Commands](commands.md) | Detailed reference for every CLI command and option |
| [Interactive Mode](interactive-mode.md) | Using the menu-driven interactive UI |
| [Configuration](configuration.md) | All settings — appsettings.json, environment variables, CLI flags |
| [Authentication](authentication.md) | Client secret, certificate file, or cert-store thumbprint |
| [Git Storage](git-storage.md) | Version-controlled backups with auto-commit and push |
| [Monitoring & Scheduling](monitoring.md) | Drift detection, scheduling, severity filtering |
| [CI/CD Integration](cicd.md) | GitHub Actions, Azure DevOps, Azure Automation examples |
| [Architecture](architecture.md) | Project structure, design decisions, extensibility |
| [Contributing](contributing.md) | How to contribute — setup, conventions, PR workflow |
| [Troubleshooting](troubleshooting.md) | Common issues and solutions |

## Quick Links

- [Supported Content Types](commands.md#supported-content-types)
- [API Permissions](getting-started.md#required-api-permissions)
- [Environment Variables](configuration.md#environment-variables)
- [HTML Reports](monitoring.md#html-reports)
- [Adding a New Content Type](architecture.md#adding-a-new-content-type)

## For Contributors & AI Coding Agents

Coding conventions, patterns, and guardrails are in [`.github/copilot-instructions.md`](../.github/copilot-instructions.md). This file is automatically loaded by GitHub Copilot and other AI coding agents working in this repo. Human contributors should also read it for the definitive coding style guide.
