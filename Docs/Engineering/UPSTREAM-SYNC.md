# Incorporate shared fixes

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
support. Record remaining native workflow work in `Docs/Project/NATIVE-PORT.md`.

Keep any contribution to the original project small and independently reviewable.

