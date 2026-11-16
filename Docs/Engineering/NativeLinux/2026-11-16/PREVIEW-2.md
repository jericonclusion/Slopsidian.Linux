# Slopsidian Linux 0.2.0-preview.2 checkpoint

Date: 2026-11-16. This completes the current preview checkpoint. Full upstream
feature and community-plugin parity remains unfinished.

The independent Linux fork retains Ron Sijm's attribution and shared history.
The original upstream PR is closed without merging. The Linux fork is maintained
at https://github.com/jericonclusion/Slopsidian.Linux; Ron's project remains at
https://github.com/RonSijm/RonSijm.Slopsidian.

## Preview scope

The application uses native Avalonia/.NET 10 widgets throughout. This checkpoint
adds bounded Canvas note/heading/block and raster file-card previews, group image
backgrounds, a read-only native image window, independent Linux About/version
information, and pinned CSharpier formatting alongside the strict analyzer gate.
Existing vault, editing, recovery, search and Canvas workflows remain documented
in `README.md`, `Docs/Features/NATIVE-LINUX.md` and `Docs/Project/NATIVE-PORT.md`.

## Inherited visual system

This is an extension of the existing Slopsidian workspace. The ribbon, file tree,
document tabs, central workspace, side panels and status bar retain their native
arrangement. Neutral dark/light surfaces, violet accents, source icon geometry
and the revised Tux notebook artwork remain the visual authority.

Evidence is in `src/RonSijm.Slopsidian.Linux/App.axaml`, `MainWindow.axaml` in that
directory, `Assets/icons.svg`, and `Assets/tux-notebook.png`. The asset README
records the inherited icon sprite and license; `Assets/tux-notebook.provenance.json`
records the artwork generation and localized foot correction. The captures show
tonal surface separation, compact native controls, the inherited typography and
the narrow Canvas toolbar/flyout adaptation.

No `PRODUCT.md`, `DESIGN.md`, persisted direction contract, formal quality bar or
visual comp exists. This report records the incumbent implementation; it creates
no new visual identity, token specification or design-system authority.

## Capture evidence

All six review captures were inspected. The copies in
`Docs/Engineering/NativeLinux/2026-11-16/Screenshots/` match their corresponding
`.impeccable/review/` files byte-for-byte.

| Review file | Repository screenshot | Demonstrated state |
| --- | --- | --- |
| `desktop-dark.png` | `canvas-previews-dark.png` | 1400 × 950, native workspace, note section, raster card and group background |
| `desktop-light.png` | `canvas-previews-light.png` | 1400 × 950, light theme, sidebar and fitted Canvas |
| `narrow.png` | `canvas-previews-narrow.png` | 900 × 800, wrapped toolbar and fitted board |
| `narrow-inspector.png` | `canvas-previews-narrow-inspector.png` | 900 × 800, native selection inspector flyout |
| `image-preview.png` | `image-preview.png` | 1000 × 800, read-only raster image window |
| `about.png` | `about-linux.png` | 900 × 700, Linux version, fork attribution and explicit port status |

These are real application captures from an isolated Linux Xvfb display with
synthetic notes. The capture record states that Canvas source and image bytes
were retained. They establish the shown preview states, not full workflow parity.

## Fresh finish review

The independent reviewer returned **ship** for this preview checkpoint:

- **Persistence:** pass for the preview checkpoint, with missing formal product
  documentation disclosed.
- **Fidelity:** all six captures valid; native workspace, typography, materials,
  ground, note sections, images and About match the inherited direction. Narrow
  toolbar wrapping, inspector flyout and Fit behavior preserve that direction.
- **Ceiling:** reached for the inherited preview scope.
- **Material fixes:** none within the reviewed preview; full parity is unclaimed.
- **Keep:** native workspace, neutral themes, violet accents, revised Tux artwork
  and explicit limitations.

## Local verification

The recorded local checks passed:

| Evidence | Result |
| --- | --- |
| `.runtime/native-review/final-lint.log` | CSharpier 1.4.0 checked 66 files; `dotnet format style`, recommended .NET analyzers and SonarAnalyzer C# gate; solution Release build with warnings as errors, zero warnings/errors |
| `.runtime/native-review/final-tests.log` | 176 tests passed, zero failures and zero skips |
| `.runtime/native-review/final-package-clean.log` | Self-contained `linux-x64` publish, archive and Debian package completed |
| `.runtime/native-review/packaged-smoke.png`, `archive-ui.log` | Extracted x64 archive launcher opened the synthetic Canvas on a private Xvfb display; note section, raster card and Tux group background rendered; application log empty |
| `.runtime/native-linux/linux-x64/out/SHA256SUMS-linux-x64.txt` | Both package checksums independently rechecked successfully |
| Packaged executable `--version` | Independently rechecked: `0.2.0-preview.2` |

The lint script and `Directory.Build.props` confirm the recorded gate's scope.
Analyzer policy targets the native project; inherited Core code is not broadly
reformatted. Tests and successful builds complement native acceptance; they do
not establish complete parity.

## Limits and follow-up

Full Canvas/Bases, advanced Markdown/Live Preview, attachment workflows, slides,
audio, complete module lifecycles, hotkey editing, bookmark/workspace
compatibility and community-plugin support remain open in the native roadmap.
Publish and Sync are unavailable in the upstream shared runtime.

The configured GitHub workflow covers x64 and arm64, but its new main-branch run
has not yet been verified at this checkpoint. Only x64 packages have local
evidence here. Tag-triggered draft releases are configured; no tag or public
release has been created. Packages are unsigned and have no automatic updater.

Earlier screenshots retain historical window titles and versions that predate
the independent Linux branding. They remain historical evidence. The roadmap
still lists broad fresh-capture, lint, packaging and independent-review acceptance
work; this report supplies the preview-specific evidence without closing that
full-parity requirement. No new visual-system drift requiring a preview fix was
identified, and no pre-existing documentation or visual drift was repaired here.
