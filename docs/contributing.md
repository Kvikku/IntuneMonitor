# Contributing

Thanks for your interest in contributing to IntuneMonitor! This guide will get you set up.

## Quick Setup

```bash
git clone https://github.com/Kvikku/IntuneMonitor.git
cd IntuneMonitor
dotnet build src/IntuneMonitor/
dotnet test tests/IntuneMonitor.Tests/
```

## Coding Conventions

The definitive style guide lives in [`.github/copilot-instructions.md`](../.github/copilot-instructions.md). It's written for both humans and AI coding agents. Key points:

- **Naming**: PascalCase classes, `_camelCase` private fields, `Async` suffix on async methods
- **Namespaces**: Follow folder structure (`IntuneMonitor.Commands`, `IntuneMonitor.UI`, etc.)
- **Nullable**: Enabled project-wide — respect nullable annotations
- **Logging**: Structured `ILogger` templates with named placeholders, not string interpolation
- **UI**: Use `ConsoleUI` helpers for terminal output, `ILogger` for structured logs — both together

## Project Structure

See [Architecture](architecture.md) for the full layout and design decisions.

## Common Tasks

### Adding a New Content Type

1. Add a constant to `IntuneContentTypes` in `Models/IntuneContentTypes.cs`
2. Add entries to `GraphEndpoints`, `FileNames`, and `FolderNames` dictionaries
3. Export, import, monitor, and the interactive menu pick it up automatically

### Adding a New Command

1. Create a class in `Commands/` following the `ExportCommand` pattern
2. Register it in `Program.cs` under the CLI section
3. Add a menu option in `UI/InteractiveMenu.cs`
4. Update `docs/commands.md`

### Adding Terminal UI

- Use `ConsoleUI.StatusAsync()` for spinners on long operations
- Use `Markup.Escape()` on all dynamic strings in Spectre markup
- Keep `ConsoleUI` calls alongside `ILogger` calls (they serve different purposes)

## Running Tests

```bash
dotnet test tests/IntuneMonitor.Tests/
```

Tests use xUnit. The `PolicyComparerTests` class has helpers like `MakeItem()` and `MakeBackup()` for constructing test data.

## Documentation Updates

The project has three doc layers:

| Layer | Path | Audience | When to update |
|---|---|---|---|
| **README** | `README.md` | First-time visitors | New top-level feature or significant change |
| **Docs** | `docs/` | Users & operators | Feature changes, new options, new guides |
| **Copilot** | `.github/copilot-instructions.md` | AI agents + contributors | Code pattern changes, new conventions |

Keep the README concise — link to `docs/` for details rather than duplicating.

## Pull Request Guidelines

1. **Build and test pass** — `dotnet build` and `dotnet test` should succeed
2. **Follow existing patterns** — match the style in copilot-instructions.md
3. **Update docs** — if you change behavior, update the relevant docs page
4. **Keep PRs focused** — one feature or fix per PR
