---
name: Slopsidian Linux
description: Native Avalonia note workspace with neutral surfaces and optional subtle gradients or classic Matrix colors.
colors:
  dark-surface: "#1e1e1e"
  dark-sidebar: "#262626"
  dark-ribbon: "#242424"
  dark-divider: "#383838"
  dark-text: "#dadada"
  dark-muted: "#a3a3a3"
  dark-native-accent: "#a68af9"
  dark-fluent-accent: "#8c70c9"
  dark-error: "#f28b82"
  dark-error-surface: "#3c2929"
  dark-create: "#a3d9b1"
  dark-assistant-action: "#a3d9b1"
  light-surface: "#ffffff"
  light-sidebar: "#f2f2f2"
  light-ribbon: "#e8e8e8"
  light-divider: "#d4d4d4"
  light-text: "#222222"
  light-muted: "#646464"
  light-native-accent: "#7051a8"
  light-error: "#a32e25"
  light-error-surface: "#ffebe9"
  light-create: "#216b36"
  light-assistant-action: "#185625"
  gradient-dark-surface-start: "#252525"
  gradient-dark-surface-middle: "#1e1e1e"
  gradient-dark-surface-end: "#171717"
  gradient-dark-sidebar-start: "#2b2b2b"
  gradient-dark-sidebar-end: "#222222"
  gradient-dark-ribbon-start: "#292929"
  gradient-dark-ribbon-end: "#202020"
  gradient-light-surface-start: "#ffffff"
  gradient-light-surface-middle: "#f9f9f9"
  gradient-light-surface-end: "#eeeeee"
  gradient-light-sidebar-start: "#f5f5f5"
  gradient-light-sidebar-end: "#e8e8e8"
  gradient-light-ribbon-start: "#eeeeee"
  gradient-light-ribbon-end: "#e5e5e5"
  matrix-surface: "#030803"
  matrix-sidebar: "#071109"
  matrix-ribbon: "#050c07"
  matrix-divider: "#183820"
  matrix-text: "#b5f7c2"
  matrix-muted: "#78ba88"
  matrix-native-accent: "#00ff41"
  matrix-create: "#00ff41"
  matrix-assistant-action: "#97e9b0"
typography:
  body:
    fontFamily: "Segoe UI, Noto Sans, sans-serif"
    fontSize: "14px"
  title:
    fontFamily: "Segoe UI, Noto Sans, sans-serif"
    fontSize: "24px"
    fontWeight: 600
  tab:
    fontFamily: "Segoe UI, Noto Sans, sans-serif"
    fontSize: "13px"
  metadata:
    fontFamily: "Segoe UI, Noto Sans, sans-serif"
    fontSize: "12px"
  editor:
    fontFamily: "DejaVu Sans Mono, monospace"
    fontSize: "16px"
  terminal:
    fontFamily: "DejaVu Sans Mono, monospace"
    fontSize: "14px"
rounded:
  document-tab: "5px 5px 0 0"
spacing:
  icon-padding: "7px"
  button-horizontal: "12px"
  button-vertical: "7px"
  field-gap: "10px"
  accent-row-gap: "12px"
  settings-section-gap: "20px"
  settings-page-margin: "28px"
components:
  button:
    typography: "{typography.body}"
    padding: "7px 12px"
  button-icon-dark:
    backgroundColor: "transparent"
    textColor: "{colors.dark-muted}"
    padding: "{spacing.icon-padding}"
  button-create-dark:
    textColor: "{colors.dark-create}"
    padding: "7px 12px"
  button-dismiss-dark:
    textColor: "{colors.dark-error}"
    padding: "7px 12px"
  document-tab-selected-dark:
    backgroundColor: "{colors.dark-surface}"
    textColor: "{colors.dark-text}"
    rounded: "{rounded.document-tab}"
    padding: "6px 8px"
  editor-dark:
    backgroundColor: "{colors.dark-surface}"
    textColor: "{colors.dark-text}"
    typography: "{typography.editor}"
    padding: "20px 12px"
  editor-light:
    backgroundColor: "{colors.light-surface}"
    textColor: "{colors.light-text}"
    typography: "{typography.editor}"
    padding: "20px 12px"
  editor-matrix:
    backgroundColor: "{colors.matrix-surface}"
    textColor: "{colors.matrix-text}"
    typography: "{typography.editor}"
    padding: "20px 12px"
---

# Design System: Slopsidian Linux

## Overview

Slopsidian Linux is a native Avalonia workspace for operating on notes. Preserve
the incumbent typography, icon geometry, pane layout and Fluent controls. The
approved appearance expansion adds a custom accent, subtle neutral gradients and
a classic black/green Matrix preset to the existing Dark, Light and System modes.

The direction is neutral and restrained, with static, readable green content for
Matrix. Theme choice changes color and surface treatment within the same native
interface. This document extracts the implementation at commit `bb1ac4e`; native
source remains authoritative. `.impeccable/design.json` maps these tokens to
resource keys, gradient stops and native behavior without an HTML recreation.

**Key Characteristics:**

- Existing flat Dark and Light palettes, with System following the actual OS theme.
- Subtle gradients made exclusively from gray, white and black stops.
- Near-black Matrix surfaces, green text and a green default accent.
- Native controls, visible labels and immediate appearance previews.

## Colors

The frontmatter contains the exact source RGB values. Token prefixes identify
the variant; repeated values retain distinct source roles where applicable.

### Primary

The native violet accent identifies emphasis and links in the flat palettes.
Dark's `dark-native-accent` and `dark-fluent-accent` deliberately differ: the
former is `AccentBrush`, while the latter supplies Fluent control selection.
Light uses `light-native-accent` for both default roles. Preserve this split.
Matrix uses `matrix-native-accent` for its default native accent; Fluent selection
uses the contrast-derived version of that green.

A selected custom accent is opaque `#RRGGBB`. Keep its RGB channels exactly when
storing it, formatting the hex as uppercase. Derive presentation colors in
`NativeAppearance`, rather than replacing the stored choice:

- `ReadableAccent` returns the original color when it reaches contrast 4.5:1.
  Otherwise it blends toward black for Light or toward white for Dark/Matrix,
  using a 20-step binary search and rounded RGB channels. Its conservative pane
  anchors are `#E5E5E5` for Light, `#2D2D2D` for Dark and `#0E1B11` for Matrix.
- `ControlAccent` checks the chosen color against white at 4.5:1 and blends
  toward black as needed, preserving readable small white selection text.
- `App.ApplyAccent` applies the control shade to Fluent palettes and the text
  shade to the custom `AccentBrush`; an OS theme change recalculates these roles.
  Reset removes the custom override and restores the preset's default roles.

These are derivation policies, not a fixed set of replacement accent hexes.

### Semantic colors

`CreateBrush` and `AssistantActionBrush` use the extracted green roles. Errors
retain separate foreground and surface roles. Matrix inherits Dark error colors;
a custom accent does not replace create, assistant or error roles.

### Neutral

| Native resource | Dark token | Light token | Matrix token |
| --- | --- | --- | --- |
| `SurfaceBrush` | `dark-surface` | `light-surface` | `matrix-surface` |
| `SidebarBrush` | `dark-sidebar` | `light-sidebar` | `matrix-sidebar` |
| `RibbonBrush` | `dark-ribbon` | `light-ribbon` | `matrix-ribbon` |
| `DividerBrush` | `dark-divider` | `light-divider` | `matrix-divider` |
| `TextBrush` | `dark-text` | `light-text` | `matrix-text` |
| `MutedBrush` | `dark-muted` | `light-muted` | `matrix-muted` |
| `AccentBrush` | `dark-native-accent` | `light-native-accent` | `matrix-native-accent` |
| `CreateBrush` | `dark-create` | `light-create` | `matrix-create` |
| `AssistantActionBrush` | `dark-assistant-action` | `light-assistant-action` | `matrix-assistant-action` |
| `ErrorBrush` | `dark-error` | `light-error` | inherited Dark |
| `ErrorSurfaceBrush` | `dark-error-surface` | `light-error-surface` | inherited Dark |
| `TerminalSurfaceBrush` | `dark-surface` | `light-surface` | `matrix-surface` |

Gradient dark inherits Dark; Gradient light inherits Light. Only `SurfaceBrush`,
`SidebarBrush` and `RibbonBrush` become linear gradients. Surface runs diagonally
from `(0%,0%)` to `(100%,100%)`, with start/middle/end stops at `0/0.5/1`.
Sidebar and ribbon run vertically from `(0%,0%)` to `(0%,100%)`, with start/end
stops at `0/1`. The matching `gradient-<variant>-<role>-<stop>` tokens provide
each exact color. Every stop has equal red, green and blue channels.

**The Solid Terminal Rule.** `TerminalSurfaceBrush` always remains a solid brush,
including under gradient themes, because the native terminal's ANSI renderer
requires a solid background. Theme changes keep the existing PTY alive.

## Typography

The UI uses Segoe UI with Noto Sans and sans-serif fallbacks. Editors and the
terminal use DejaVu Sans Mono with a monospace fallback. Matrix retains these
fonts; it introduces no novelty typeface.

The extracted hierarchy is a 14-unit UI body, 24-unit semibold Settings section
title, 13-unit tabs and 12-unit supporting metadata. Source editing starts at
16 units and respects the saved editor size, clamped to 10–32 units. The terminal
uses 14 units. Frontmatter `px` values describe Avalonia logical units; they are
documentation values, not a CSS runtime. Do not infer a new type scale, tracking
or line-height policy from these samples.

## Layout

Keep the desktop workspace's ribbon, resizable sidebars, document tabs, editor
and inspector arrangement. `MainWindow.axaml` starts at 1400×950, with a 760×520
minimum; its initial columns are `44,260,4,*,4,260`. Existing sidebar adaptation
remains responsible for narrow windows.

Settings starts at 900×700 and supports 700×520. Its two columns are `200,*`;
the content scrolls while Done remains in the bottom row. The content margin is
28 units with 20-unit section spacing. Field label/control gaps are 10 units;
the accent swatch/value row has a 12-unit gap. The theme selector is 240 units
wide; the native picker control is 100 units wide. These are observed settings
measurements, not universal breakpoints or a new application grid.

## Elevation & Depth

App-owned panes use tonal surfaces and dividers. The approved gradients add
subtle shading to those same panes. Native Fluent templates own control, flyout,
hover, selection and focus treatments; this expansion defines no new shadow,
blur, elevation or animation tokens. Matrix is static.

## Shapes

Keep the incumbent native icon geometry and Fluent control shapes. App-owned
document tab items have rounded top corners (5 units) and square lower corners,
with a 36-unit minimum height. Icon buttons have no border, 7-unit padding and
28×28 minimum dimensions; tab-close buttons use 4-unit padding and 22×22 minima.
These minima describe existing controls and do not redefine Fluent templates.

## Components

### Buttons

Default buttons use horizontal/vertical padding of 12/7 units. `Button.icon`
uses a transparent background and `MutedBrush`; create and dismiss classes use
`CreateBrush` and `ErrorBrush`. A combined class keeps both roles, such as a
transparent icon dismiss button with error foreground. Fluent owns interactive
states, keyboard focus and disabled treatment.

### Navigation and document tabs

Settings navigation is the native `ListBox`; its selected state uses the Fluent
control palette. Document tab items use padding of 8/6 units and the extracted
top-corner shape. Selected document-tab resources resolve `SurfaceBrush` and
`TextBrush` for the active variant. Do not introduce a separate navigation style.

### Inputs and Appearance picker

Keep the native ComboBox and Avalonia `ColorPicker`, with Fluent styles. The
picker exposes spectrum, palette and components tabs; RGB components and hex
input are enabled, the color-model selector is hidden, and alpha is disabled
and hidden. Its accessible name is “Accent color”. The adjacent value shows the
selected RGB hex, not its derived display shade.

Changing a preset or accent previews immediately. A custom choice survives
preset changes. Use theme default clears that choice and previews the current
preset's default; the reset action is disabled when no custom choice exists.
Done returns preferences to the owner for profile persistence. Closing Settings
without Done restores the saved appearance; independently saved native-module
choices remain effective. Profile persistence works without an open vault.

### Editor and terminal

The editor binds `SurfaceBrush` and `TextBrush`; reading and cached canvas views
refresh when appearance changes. The terminal binds solid `TerminalSurfaceBrush`
and `TextBrush`, with the cursor following its foreground. Retain these native
bindings rather than caching a fixed appearance color in new components.

## Do's and Don'ts

### Do

- Do preserve the flat palettes, native fonts, icon geometry and existing pane layout.
- Do use the active native resource role and variant inheritance for new controls.
- Do keep every gradient stop monochrome and its shading subtle.
- Do retain the exact selected RGB while deriving readable text and control accents.
- Do keep terminal buffers solid and preserve the live PTY during theme changes.

### Don't

- Don't merge Dark's native and Fluent default violet roles.
- Don't replace semantic create, assistant or error colors with the custom accent.
- Don't add Matrix rain, a new font or new motion to the approved static preset.
- Don't substitute an HTML, Electron or WebView rendering for native controls.
