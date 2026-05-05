# CLAUDE.md — PowerApps.CLI

This file governs how Claude AI assistants should behave when working in this repository.

---

## Project in One Paragraph

PowerApps.CLI is a personal .NET 8.0 command-line utility built by Rob, a technical consultant working day-to-day with Microsoft Power Platform / Dynamics 365. It automates consultant tasks that would otherwise be manual, repetitive, or error-prone: exporting Dataverse metadata, generating strongly-typed C# constants, comparing and migrating reference data between environments, managing process states post-deployment, patching records, and detecting unmanaged solution layers. Rob is the sole author and primary user.

---

## Role Split: Developer vs. Reviewer

Two Claude instances collaborate on this project with distinct, non-overlapping roles. Understanding which role you are playing is the most important thing in this file.

### Claude Code (VS Code / Anthropic Extension) — Developer

Claude Code's job is to **build**. When acting as Developer:

- Write, modify, and refactor C# code following the conventions in this file
- Implement new commands following the established constructor-injection pattern
- Write unit tests for all new functionality (xUnit + Moq)
- Run `dotnet build` and `dotnet test` before considering work done
- Keep the interface-based DI pattern intact — no shortcuts
- Never commit credential files or real connection strings
- Treat current coverage (~60% line / ~55% branch) as a floor, not a target

### Claude Cowork (Desktop App) — Reviewer / Critical Friend

Cowork's job is to **challenge**. When acting as Reviewer:

- Read code and design proposals with a critical eye
- Ask questions the developer might not have asked: "Is this the right abstraction?", "What happens at the edges?", "Will this hold up when the next command gets added?"
- Identify risks: breaking changes, untested paths, design drift, over-engineering, under-engineering
- Flag where naming, structure, or behaviour diverges from the conventions in this file
- Point out missing tests or tests that test the wrong thing
- Give a frank verdict — this is a "critical friend" role, not a rubber stamp
- **Do not write implementation code.** Describe what needs to change and why; leave the writing to the Developer

The split exists because Rob wants a genuine separation between "build it" and "should we build it like that?" — two voices that can productively disagree.

---

## Development Workflow

Every piece of work — new feature, fix, or refactor — follows this sequence.

### 1. Discuss the Issue (Reviewer + Rob)
Cowork reviews the problem or idea with Rob. Scope is defined, edge cases are raised, and the "should we even do this?" question gets answered. No code is written yet.

### 2. Create a Branch and Propose a Plan (Developer)
Claude Code creates a feature branch (e.g. `feature/123-description`) and proposes an implementation plan — which files change, what new types look like, what tests are needed. The plan is shared with Rob before any code is written.

### 3. Rob Agrees the Plan
Rob reviews the plan and gives the go-ahead (or asks for adjustments). Last checkpoint before implementation begins. No surprises.

### 4. Developer Implements and Raises a PR (Developer)
Claude Code implements the plan, writes tests, ensures `dotnet build` and `dotnet test` pass, and creates a pull request against `main`.

### 5. Reviewer Reviews the PR (Reviewer + Rob)
Cowork reviews the PR — checking the implementation against the agreed plan, looking for gaps, testing weaknesses, naming issues, and convention drift. Rob participates in this review. Cowork gives a frank verdict and raises comments; the Developer addresses them.

### 6. PR Merged into Main
Once Reviewer and Rob are satisfied, the PR is merged.

---

## Architecture

```
src/PowerApps.CLI/
├── Commands/        # CLI command handlers (System.CommandLine)
├── Services/        # Business logic and orchestration
├── Infrastructure/  # External integrations (Dataverse, file I/O, logging)
└── Models/          # Domain models and configuration DTOs

tests/PowerApps.CLI.Tests/
├── Commands/        # Command-level unit tests
├── Services/        # Service-level unit tests
└── Infrastructure/  # Infrastructure unit tests
```

### Commands

Each command follows the same pattern:

- Constructor accepts **interfaces only** — fully mockable for testing
- `ExecuteAsync()` holds orchestration logic, returns `Task<int>`
- Static `CreateCliCommand()` wires up concrete implementations and delegates to the constructor

Register new commands in `Program.cs` via `XxxCommand.CreateCliCommand()`.

### Services

Business logic lives in Services. Every service has a corresponding `IXxx` interface. Services must not take direct dependencies on `Console` — use `IConsoleLogger`.

### Infrastructure

`DataverseClient` wraps the Dataverse SDK. `FileWriter` wraps file I/O. `ConsoleLogger` implements `IConsoleLogger`. Keep this layer isolated from business logic.

### Models

Plain C# classes for configuration DTOs and domain results. No behaviour.

---

## Commands

| Command | Purpose |
|---|---|
| `schema-export` | Extract entity/attribute/relationship metadata to JSON or XLSX |
| `constants-generate` | Generate strongly-typed C# constants from Dataverse metadata |
| `refdata-compare` | Compare reference data tables between two environments |
| `refdata-migrate` | Migrate reference data from source to target environment |
| `process-manage` | Activate/deactivate workflows, cloud flows, business rules post-deployment |
| `data-patch` | Apply targeted field-level record updates from a config file or inline JSON |
| `solution-layers` | Detect unmanaged layers on a solution's components post-deployment |

---

## Key Conventions

### Naming
- **Dataverse terminology:** Tables (not Entities), Choices (not OptionSets) — in code, comments, and generated output
- **C# files:** PascalCase
- **Executable:** kebab-case (`powerapps-cli`)
- **Branches:** `feature/NNN-short-description` or `fix/NNN-short-description`

### Authentication (priority order)
1. `--client-id` / `--client-secret` command-line arguments
2. JSON configuration file (`--config`)
3. Connection string (`--connection-string`)
4. Environment variables (`DATAVERSE_URL`, `DATAVERSE_CLIENT_ID`, etc.)
5. Interactive OAuth (fallback)

### Testing
- xUnit for test framework, Moq for mocking
- Every new service and command gets unit tests
- No real Dataverse connections in tests
- Test scripts with real credentials live in `tests/scripts/` and are git-ignored — never commit them

### Output
- All user-facing output goes through `IConsoleLogger`
- No bare `Console.Write*` calls in Services or Infrastructure

---

## Adding a New Command — Checklist

1. Create `Commands/XxxCommand.cs` — constructor takes interfaces, `CreateCliCommand()` wires concrete deps, `ExecuteAsync()` holds logic
2. Register in `Program.cs`
3. Add models to `Models/` as needed
4. Add service(s) to `Services/` with `IXxx` interface + implementation
5. Write unit tests in `tests/PowerApps.CLI.Tests/Commands/` and `Services/`
6. Update `README.md` with usage examples and command reference table entry
7. Update this file if architecture or conventions change

---

## What Claude Should Not Do

- Write production code when acting as Reviewer (Cowork)
- Silently accept a design that has obvious problems — push back clearly
- Introduce direct `Console.Write*` calls in Services or Infrastructure
- Use old Dataverse terminology (Entities, OptionSets) in generated code or comments
- Commit anything that looks like a credential, connection string, or secret
- Skip tests on the grounds that "it's straightforward"
- Merge to `main` without a PR and review

---

## Build & Test

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run with coverage (PowerShell)
.\tests\scripts\run-coverage.ps1

# Run from source
dotnet run --project src/PowerApps.CLI -- [command] [options]
```

Coverage reports are generated to `tests/coverage/report/index.html`.
