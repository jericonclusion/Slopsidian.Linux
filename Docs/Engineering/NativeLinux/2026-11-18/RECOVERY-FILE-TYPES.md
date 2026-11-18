# Automatic recovery file eligibility

Prepared for 0.3.0-preview.5.

## Cause

The recovery queue and persisted history accept Markdown (.md), Canvas
(.canvas) and Bases (.base). The periodic background pass additionally visited
all open editor tabs, including TXT and Python files. Capturing those files
reached strict snapshot validation and raised “Invalid file-recovery snapshot.
Existing history was retained.” Each timer pass retried the same open tabs, so
dismissing the banner did not resolve the cause. The error could describe a
rejected new capture while the existing history was valid.

## Change

NativeRecoveryHistory.SupportsFileType is the shared extension policy for
history validation, queue eligibility and periodic tab selection. The capture
service also skips unsupported automatic requests before reading files or
history, so other callers cannot reintroduce this failure. An explicit request
for an unsupported type explains the supported formats.

This does not reset or migrate history, disable File recovery or broaden the
history format. Existing retention, exact UTF-8/BOM handling, unknown-field
preservation and restore/version/vault-epoch guards remain in place. No user
vault or recovery-history file was edited to work around the error.

## Regression evidence

Four synthetic TXT/Python regression cases failed on the original code with
the exact reported exception. The cases exercise three consecutive mixed-file
capture sweeps, unchanged history bytes, rejected manual captures with a clear
explanation, continued supported-note recovery and skipped captures creating
no history. Real vaults and note contents are not used as test fixtures.

The strict native lint gate passes CSharpier 1.4, style checks, recommended .NET
analyzers and SonarAnalyzer with no warnings/errors. Focused file-recovery,
history and binary-restore tests cover the fix alongside existing data and
stale-review guards. Full-suite and release-pipeline results are recorded in
the pull request and build runs. No native layout or visual-system change is
part of this correction.
