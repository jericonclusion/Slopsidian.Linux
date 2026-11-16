# Native assistant review — 2026-11-16

Branch: feat/native-assistants. Planned release: 0.3.0-preview.1.

## Scope and disposition

The assistant extends the established Avalonia/Linux Operate interface with
Codex CLI, Claude Code and GitHub Copilot CLI read-and-suggest chat. Visible
attachments include saved notes, unsaved drafts, selected line ranges and one
level of linked notes or backlinks. Users can inspect snapshots, review the
serialized message, stop a request, ask follow-ups, open exact-path citations
with heading/block navigation, and deliberately copy or save a reply as a new
note. This scope does not provide an interactive terminal or automatic edits.

Fresh finish review by impeccable_finish_reviewer_assistants returned **SHIP for
the reviewed assistant UI** after the narrow-window layout and contrast findings
were resolved. Release publication, CI and merge remain pending. This disposition
does not establish full-app parity or completion of the unexposed Bases table UI.

## Design decisions

This is an extension of the existing neutral dark/light surfaces, violet accent,
system font stack and native controls/icons. No new visual world or mascot
artwork was introduced; screenshots document the native interface. The implementation remains the source of truth; this
review creates no global design-system specification or token sidecar.

App.axaml supplies the inherited palette and the assistant action resource:

| Resource | Dark | Light | Role |
| --- | --- | --- | --- |
| SurfaceBrush | #1e1e1e | #ffffff | Window surface |
| TextBrush | #dadada | #222222 | Main text |
| MutedBrush | #a3a3a3 | #646464 | Supporting text and assistant placeholders |
| AccentBrush | #a68af9 | #7051a8 | Existing link/accent treatment |
| AssistantActionBrush | #a3d9b1 | #185625 | Send and Save as new note |

The assistant-specific action brush preserves the existing global CreateBrush.
Its light-theme action text measured 5.46:1 against the captured button surface.
NativeAssistantLayout.axaml explicitly uses MutedBrush for both placeholders and
locally sets Fluent TextControlPlaceholderOpacity to 1; measured placeholder
contrast was 7.43:1 dark and 5.92:1 light.

Typography inherits the existing Segoe UI, Noto Sans, sans-serif stack at 14 px,
with a 24 px semibold window heading and smaller supporting labels. The layout
uses a 24 px outer margin and 16 px row spacing. Native Fluent controls retain
their established states and shapes.

The transcript occupies the flexible row above context and the composer. CLI
settings and attached context expand mutually exclusively. Context actions,
attachment rows and their status share one scroller. A panel budget measured
after native layout caps the expanded panel at 200 px while reserving transcript
space. The two 560 × 680 captures retain a 168 px transcript viewport and put the
Send button's bottom at y=656; these are observed capture measurements.

## Evidence

These are actual native-window captures under private Xvfb, using a synthetic
vault. Completed replies are visibly labelled deterministic CLI fixtures rather
than live provider responses. Paths below are relative to this note.

| Capture | Size | State |
| --- | --- | --- |
| Screenshots/assistant-dark-empty.png | 860 × 900 | Empty chat, attached unsaved snapshot and composer placeholder |
| Screenshots/assistant-dark.png | 860 × 900 | Reply, citations, attached sources and export actions |
| Screenshots/assistant-light.png | 860 × 900 | Equivalent light-theme reply and actions |
| Screenshots/assistant-narrow-context.png | 560 × 680 | Expanded context with reachable composer and Send |
| Screenshots/assistant-narrow-error.png | 560 × 680 | Missing CLI error, expanded settings and retained request |

Source verification covered NativeAssistantLayout.axaml, NativeAssistantWindow
partials, MainWindow.Assistant.cs, NativeAssistantContext.cs and App.axaml.
The capture harness reported source notes unchanged. Existing final verification
logs report 335 passing tests, including 26 assistant-focused cases, and strict
CSharpier, dotnet format style, recommended .NET and SonarAnalyzer checks with
zero warnings and errors. Fixture/window tests cover drafts, selection bounds,
references, guarded reads, stdin/arguments, failures, cancellation, follow-ups,
provider changes, reply export and citation navigation.

## Verification limits

CLI help/adapter flags were checked with Codex 0.169.0, Claude Code 2.1.325 and
Copilot CLI 1.0.102. No paid or authenticated provider conversation was verified.
Fixture results demonstrate local process and UI behavior; they do not establish
account eligibility, remote model quality or provider retention behavior.
The native UI has no browser detector result or approved composition to compare.
Bases formula, YAML and query foundations are tested; their table UI remains
unexposed and unfinished.

The detailed operating scope and limits are in
../../../Features/NATIVE-ASSISTANT.md.
