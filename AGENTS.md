# Repository guidance

- Product: Slopsidian Linux, an independent native Avalonia/.NET 10 fork.
- Retain Ron Sijm’s attribution and upstream history. The full native port is unfinished.
- Keep the interface native: no Electron, Blazor or WebView host.
- Follow the build, formatting, test and upstream-sync commands in CONTRIBUTING.md.
- CSharpier owns native app/test formatting; run `./tools/lint-native.sh` and the
  Release xUnit suite for relevant changes. Do not suppress analyzer findings broadly.
- Preserve exact note bytes, drafts, version guards and unknown configuration fields.
- Use temporary vaults and private displays for automation; do not drive the user’s desktop.
- Use semantic commits and never rewrite published history.
- Never publish a release or upstream contribution without explicit authorization.
