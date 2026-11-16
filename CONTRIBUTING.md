# Developing Slopsidian Linux

Use Linux and the .NET 10 SDK selected by `global.json`. No Node.js or browser
host is required. The solution contains three projects:

| Project | Responsibility |
| --- | --- |
| `RonSijm.Slopsidian.Core` | Vault services, guarded I/O and portable data formats |
| `RonSijm.Slopsidian.Linux` | Native Avalonia application and Linux workflows |
| `RonSijm.Slopsidian.Linux.Tests` | xUnit v3 and headless Avalonia regression tests |

The product, packages and release line are Slopsidian Linux. Internal namespaces
retain their historical names to keep source history and comparisons useful.

## Make a change

1. Create a short-lived branch from `main`.
2. Keep changes focused and preserve unknown vault/configuration fields.
3. Format with the pinned local tool and run the quality gates below.
4. Capture real native screens for UI changes, using synthetic vaults and a
   private X server rather than interfering with someone’s desktop.
5. Commit in reviewable pieces and merge when the checks pass. Remove the branch.
   `main` requires a pull request and both architecture
   build checks; force-pushes and branch deletion are blocked.

Use semantic commit messages: `<type>(<scope>): <concise English subject>`.
Types are `feat`, `fix`, `docs`, `style`, `refactor`, `test` and `chore`.
`style` denotes source formatting; a visible behavior change uses `feat` or `fix`.

```sh
dotnet tool restore
dotnet csharpier format src/RonSijm.Slopsidian.Linux tests/RonSijm.Slopsidian.Linux.Tests
./tools/lint-native.sh
dotnet test tests/RonSijm.Slopsidian.Linux.Tests -c Release --no-build
```

CSharpier owns layout. `dotnet format style` checks code style without fighting
the formatter. Recommended .NET analyzers and SonarAnalyzer C# inspect the native
app; the solution build treats warnings as errors. The tests use xUnit v3 with
the VSTest adapter and Avalonia Headless. Do not pass MTP-specific switches.

## Work on the assistant

Use an executable fixture for repeatable CLI tests. Exercise stdin, cancellation,
failed login, oversized output, provider switching and stale-vault guards without
calling a paid model or using real notes. Use an isolated profile and private
Xvfb display for UI captures, and label synthetic replies as demonstrations.

The assistant guide documents the CLI contract and context limits. Keep requests
in stdin, preserve exact attachment snapshots and keep provider credentials out
of application state. Do not silently enable writes, shell tools or external MCP
tools when a provider changes its CLI. Record actual account-backed verification
separately from fixture tests.

## Prepare a version

Update the native project’s `Version`, changelog and visible documentation in a
focused commit. Run lint, tests and `tools/package-linux.sh` and smoke-test the
result. A `linux-v<version>` tag must match the project version. CI builds both
architectures and creates a **draft** release for tags; a maintainer reviews its
notes and artifacts before publishing. Never replace published release contents.

## Shared storage and formats

Compatible fixes to shared vault services and file formats can be incorporated
selectively. See [the shared-fix workflow](Docs/Engineering/UPSTREAM-SYNC.md).
Linux interface development and releases stay in this repository.
