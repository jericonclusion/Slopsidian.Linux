# Native Linux review screenshots — 2026-11-17

This directory contains captures from three separate native review passes. The
assistant/docking images below are new evidence for ASSISTANT-DOCK-REVIEW.md.
The earlier error-action and appearance images retain their original scope.

## Assistant refinement and terminal docking

These eight PNGs were copied byte for byte from frames captured in the temporary
.impeccable/review/workspace/frames directory. Each copy was opened and its dimensions,
size and SHA256 checked during documentation. The reviewed functional UI is
ccc3a521f1a18e669b41bf2b7ccc8592f65b48f2; 0.3.0-preview.4 release preparation
does not imply these images were captured from a published release.

The current app assembly ran in real Avalonia windows on a private Xvfb display
with a 2000×1200×24 screen. The capture runner verified assembly identity before
launch. Images show native window content at the listed dimensions. They are
not browser renders or reconstructed mockups. Notes, vault paths and CLI output
are synthetic fixtures, labeled as demonstration content. No paid AI-service
request or user desktop input was used. Synthetic note SHA256s stayed unchanged.

| Retained file | Original scratch frame | Pixels | Bytes | Evidence shown |
| --- | --- | --- | --- | --- |
| assistant-workspace-dark.png | assistant-dark-conversation.png | 860×860 | 74,418 | Dark reply at its real scroll position, copy/save actions, cited sources, snapshot rows and composer. |
| assistant-workspace-light.png | assistant-light-conversation.png | 860×860 | 71,164 | Light conversation, source links, attachments and composer. |
| assistant-workspace-narrow.png | assistant-narrow-context.png | 560×680 | 46,168 | Minimum assistant size with transcript, scrollable snapshot rows and composer controls. |
| assistant-workspace-settings.png | assistant-narrow-settings-error.png | 560×680 | 53,716 | Compact CLI settings, Save action, transcript and long recoverable error in its bounded scroll area. |
| terminal-docked-dark.png | terminal-dock-dark.png | 1400×950 | 96,810 | Wide editor workspace with a live bottom terminal and Open in window action. |
| terminal-docked-narrow.png | terminal-dock-narrow.png | 760×520 | 51,500 | Minimum workspace with visible note text and reachable dock/session controls. |
| terminal-docked-matrix.png | terminal-dock-matrix.png | 1400×950 | 93,821 | Matrix workspace and live terminal using the existing solid terminal surface. |
| terminal-detached-dark.png | terminal-window-dark.png | 1000×700 | 37,982 | Separate live terminal with Dock at bottom action. |

The actual origin of assistant-workspace-dark.png was
assistant-dark-conversation.png. The separate assistant-dark-reply-actions.png
scratch frame happened to be byte-identical; it was not the copy source.

Copy SHA256s:

| Retained file | SHA256 |
| --- | --- |
| assistant-workspace-dark.png | bd7ea9bc44a1971637b3d3a9e5a0724276ee2dcc2742166d3d33cd7c46a5226a |
| assistant-workspace-light.png | f5c0e4cf038503eae209045a3be0133b95019d58c97a67644b511005760334f5 |
| assistant-workspace-narrow.png | 7d6a6b9db18678e2421cf17f2fd3082237142c41ab1ae6d9e9d3c54c98b998d7 |
| assistant-workspace-settings.png | 2b482b4e09cca07f84f600d7117821c3e5fc9f897bbbb1428c6d9561e980c49a |
| terminal-docked-dark.png | 5a8eab5a113bf4c1b7c64da7490ca8996b23b709c4a4e887082d27c75fa67153 |
| terminal-docked-narrow.png | 50f8da43f6554a3be5ada2346a53abd15ce4d7e1ac3abcbe1a82b3652328a5be |
| terminal-docked-matrix.png | 55a0399635e8790434966d94456232449b4d7fc3d84deeb95ae28585761c05a3 |
| terminal-detached-dark.png | 3788e04392c46f9dbdf55e3915cdde751261b568f5d66f7612784aaf97dbb628 |

The final independent finish reviewer opened all **14** required scratch frames
and returned **disposition: ship**, with no material fixes or recapture required
within the assistant-refinement/terminal-docking scope. All **15** source/app
assembly hashes in capture-source-hashes.json matched current files. This
directory preserves eight representative frames, not the entire review matrix.
The additional scratch states covered empty conversation, the byte-identical
Dark reply-actions frame, the settings scroll position with Save reachable,
running/Stop, stopped/retry and detached Light. Geometry and live mouse-divider
evidence were verified in the temporary
.impeccable/review/workspace/capture-report.json report. The scratch matrix and
reports are subject to release cleanup; these eight representative images and
the review records are the retained evidence.

The native divider drag changed height from 280 to 350 logical units, with 350
remembered in the profile. Dock/window transfers kept the live PID, emulator/
buffer and current directory. Those interaction claims are supported by the
capture report, source inspection and tests, rather than by a single still
image. No web detector applies to native XAML.

## Earlier captures retained in this directory

| Files | Original review scope |
| --- | --- |
| error-module-dark.png; error-vault-dark.png; error-vault-narrow-long.png | ERROR-ACTIONS-REVIEW.md: native error states and recovery actions. |
| gradient-dark-workspace.png; gradient-light-workspace.png; matrix-workspace.png; appearance-color-picker.png | APPEARANCE-REVIEW.md: native appearance presets and accent-color picker. |

Those seven earlier PNGs were not recaptured or reclassified for this pass.
Their associated reviews describe their provenance and validation scope.
