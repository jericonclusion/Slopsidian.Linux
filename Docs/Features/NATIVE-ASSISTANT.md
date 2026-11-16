# CLI assistants

Use **Codex CLI**, **Claude Code** or **GitHub Copilot CLI** from a native
Slopsidian Linux window. The app uses your installed executable and existing
provider login. It does not host a browser or bundle an AI SDK.

## Setup

Install and sign in to a CLI in a terminal first. Confirm a normal CLI request
works with your account, then open **Assistant** from the ribbon or command palette.

| Provider | Official setup | Check installation |
| --- | --- | --- |
| Codex | https://developers.openai.com/codex/cli | `codex --version` |
| Claude Code | https://code.claude.com/docs/en/setup | `claude --version` |
| GitHub Copilot CLI | https://docs.github.com/en/copilot/how-tos/copilot-cli/set-up-copilot-cli | `copilot --version` |

The assistant is enabled by default and can be switched independently under
**Settings → Native modules → CLI assistant**. Changes apply immediately and
are saved in the native app profile, even before opening a vault. Switching it
off hides its ribbon/command-palette entries, closes its window and cancels
requests. Switching it back on keeps executable/model settings; conversations
are not restored. These switches do not load Obsidian community plugins or
change vault `.obsidian/core-plugins.json`.

Choose the provider. In **CLI settings**, set an absolute executable path if the
command is absent from the desktop's `PATH`. An optional model name is passed as
one argument. Settings are stored in the private native profile; credentials are
managed by the CLI and are not copied into application state.

Adapter flags were checked with Codex **0.169.0**, Claude Code **2.1.325** and
Copilot CLI **1.0.102**. Older versions may lack required flags. These checks
establish command compatibility, not successful account access.

## Context and references

The active Markdown note is attached on opening when it fits the context limits.
Attachments are immutable **snapshots**, including unsaved text from open notes.
Editing a note afterwards does not update an existing attachment.

- **Active note** attaches or refreshes the current note.
- **Selection** attaches only the selected Source text, with its path and line range.
- **Note…** selects another Markdown note in the current vault.
- **Linked notes** and **Backlinks** attach one level of related Markdown notes
  from the currently active note. They do not recursively load the vault.
- **Inspect** shows the exact snapshot. Remove unwanted attachments before sending.

Wikilinks, aliases and relative Markdown links resolve through the native vault
model. Heading/block fragments remain in the reference map. Unresolved and
external references do not fetch content. References alone do not attach notes.

**Review message** shows the complete request, attachments and previous successful
question/answer pairs. Notes are serialized as quoted reference material separately
from the current request. The message goes through stdin, never command-line arguments.

## Conversation

Press **Send** or **Ctrl+Enter**. Output appears as the CLI provides it; some CLIs
return the entire answer at completion. **Stop**, **Esc**, closing the window or
switching vaults cancels a running request. Failed/stopped requests remain in the
composer, and partial replies are excluded from follow-up context.

Replies render as native Markdown. Citations open exact vault paths, headings or
blocks. **Copy reply** copies Markdown. **Save as new note…** asks for a free path
and uses the guarded vault API; it never replaces an existing note.

Follow-ups include previous successful questions/replies and current attachments.
Removing an attachment removes its content from the next request. Provider changes
start a fresh chat and retain attachments and independent provider settings.
**New conversation** clears the chat and retains attached snapshots.

## Scope and data

The assistant provides **read-and-suggest chat**, without automatic note edits,
shell commands, external MCP tools, images or a whole-vault index. A separate
optional native [Terminal module](NATIVE-TERMINAL.md) provides an interactive shell. Processes start in an empty private directory outside the vault. Codex uses
its read-only sandbox with shell/apps/plugins/hooks and user config discovery
disabled, while retaining standard authentication. Claude uses safe mode with
built-in/MCP tools disabled. Copilot is limited to read tools, with shell/writes
denied and remote export disabled. Requests instruct the model to use supplied context.

The app does not persist chat transcripts or write attachment snapshots into
temporary files or the vault. Conversations last while the window is open;
saved replies become ordinary notes. CLI/provider logs, retention, network access,
account eligibility and usage charges remain governed by the chosen provider.
Opening the window or reviewing context does not send a provider request.

Limits: 16 attachments, 32,000 characters per attachment, 80,000 attached characters
in total, 8,000 request characters, 12 successful turns, a 256,000-character
serialized message, a 64,000-character reply and a ten-minute timeout. Character
counts are not token estimates. Oversized context is refused without truncation.
Saved-note reads are capped at 128,000 bytes. Diagnostics retain 16,000 characters
while draining the remaining pipe.

## Verification and inspiration

Temporary-vault and executable-fixture tests cover drafts, selected ranges,
references, guarded reads, arguments/stdin, failures, cancellation and output
bounds. Native-window tests cover follow-ups, provider changes, reply export and
citation navigation. Screenshots show a synthetic vault and explicitly labelled
deterministic replies; they do not claim paid-model verification for every provider.

Interaction references, without bundled JavaScript or SDK code:

- Codex for Obsidian: https://github.com/lufie/codex-for-obsidian
- Obsidian Codex + Claude Code: https://github.com/heungson/obsidian-codex
- Copilot CLI for Obsidian: https://github.com/boschma1/copilot-cli-obsidian

Useful patterns were explicit attachments, visible reference paths, independent
provider settings, cancellation and deliberate reply export. Full agent permissions
and durable provider sessions remain future work.
