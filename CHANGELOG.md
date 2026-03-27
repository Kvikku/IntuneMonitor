# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-03-27

### Added

- **Export command** — download all 13 supported Intune content types to JSON backup files
- **Import command** — restore policies from a backup into a target tenant (with `--dry-run` support)
- **Monitor command** — compare live state against backup and detect configuration drift with severity levels
- **Audit Log command** — fetch and summarize Intune audit log events (1–30 days)
- **List Types command** — display all supported Intune content types
- **Interactive mode** — menu-driven UI with arrow-key navigation when run with no arguments
- **HTML reports** — self-contained dashboards for export summaries, change reports, and audit logs
- **Storage backends** — local file system or Git repository (auto-commit and push)
- **Authentication** — client secret or X.509 certificate (PFX/PEM file or Windows cert store thumbprint)
- **Configuration** — `appsettings.json`, environment variables (`INTUNEMONITOR_` prefix), and CLI flags
- **Structured logging** — configurable verbosity via `--verbosity` flag
- **Cross-platform** — self-contained builds for Windows, macOS, and Linux
- 13 Intune policy types: Settings Catalog, Compliance, Device Configuration, Group Policy, App Configuration, Assignment Filters, Autopilot Deployment Profiles, Apple BYOD Enrollment Profiles, Device Enrollment Configurations, PowerShell Scripts, Shell Scripts, Driver Update Profiles, Feature Update Profiles
