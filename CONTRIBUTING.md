# Developing Slopsidian Linux

Use Linux and the .NET 10 SDK selected by `global.json`. No Node.js or browser
host is required. The solution contains three projects:

| Project | Responsibility |
| --- | --- |
| `RonSijm.Slopsidian.Core` | Shared vault services, guarded I/O and formats inherited from upstream |
| `RonSijm.Slopsidian.Linux` | Native Avalonia application and Linux workflows |
| `RonSijm.Slopsidian.Linux.Tests` | xUnit v3 and headless Avalonia regression tests |

Internal namespaces retain upstream names to keep future source comparisons
useful. Linux branding, CLI names and packages use Slopsidian Linux.

## Make a change

1. Create a short-lived branch from `main`.
2. Keep changes focused and preserve unknown vault/configuration fields.
3. Format with the pinned local tool and run the quality gates below.
4. Capture real native screens for UI changes, using synthetic vaults and a
   private X server rather than interfering with someone’s desktop.
5. Commit in reviewable pieces and merge when the checks pass. Remove the branch.
   After this checkpoint, `main` requires a pull request and both architecture
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

## Follow Ron’s upstream

Keep these remotes:

```sh
git remote -v
# origin:   https://github.com/jericonclusion/Slopsidian.Linux.git
# upstream: https://github.com/RonSijm/RonSijm.Slopsidian.git
```

Fetch upstream periodically, before a release or when a relevant fix lands:

```sh
git fetch upstream
git log --oneline upstream/main --not main
git diff main...upstream/main -- src/RonSijm.Slopsidian/Services shared/core-plugins.json
```

Review vault storage, security fixes, data formats and useful core logic first.
Take compatible, self-contained commits with `git cherry-pick -x <commit>` on a
new branch. For mixed commits, port the relevant change manually and record its
upstream commit in the commit message. Map old `Services/*.cs` paths to
`src/RonSijm.Slopsidian.Core/Services/`; keep the native API boundary intact.
Re-run lint, regression tests and the affected native workflow before merging.

Do not routinely merge the whole upstream branch: its browser/Windows hosts and
UI have intentionally been removed here. Translate UI improvements into native
widgets. A shared file or module setting alone is not evidence of native feature
support. Record remaining parity work in `Docs/Project/NATIVE-PORT.md`.

The original port PR is closed in favor of this independent fork. Upstream
contributions, if useful later, should be small shared fixes prepared separately.

## Prepare a version

Update the native project’s `Version`, changelog and visible documentation in a
focused commit. Run lint, tests and `tools/package-linux.sh` and smoke-test the
result. A `linux-v<version>` tag must match the project version. CI builds both
architectures and creates a **draft** release for tags; a maintainer reviews its
notes and artifacts before publishing. Never replace published release contents.
