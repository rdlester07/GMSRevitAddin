---
name: gms-ribbon-icon-workflow
description: Add or edit a GMS ribbon icon in GMSRevitAddin — the light/dark theme variant pairing, the flat single-color glyph drawing recipe, and how icons are wired to buttons. Use when adding a new ribbon button's icon, redrawing an existing icon, or troubleshooting a ribbon icon not re-tinting for dark theme.
---

# Ribbon icon theming (light/dark)

The GMS ribbon icons have two variants: the original dark glyphs (`Resources/<name>.png`, used in
Revit's light theme) and **monochrome light-gray `Resources/<name>_dark.png`** variants (used in dark
theme so they read on the dark ribbon). [`GMS_tools.cs`](../../../GMSRevitAddin/GMS_tools.cs) routes every button
image through the **`Icon("<name>.png")`** helper, which swaps in the `_dark` variant when
`UIThemeManager.CurrentTheme == Dark`. **When adding a ribbon button:** add both the base PNG and a
`_dark` variant to `Resources/` (+ a `<Resource Include>` line each in the csproj) and set the image via
`Icon("file.png")`, not a raw `new BitmapImage(new Uri(...))`. The `_dark` files are generated from the
originals by recoloring every opaque pixel to light gray (`#D6D6D6`) while preserving alpha. **Caveat:**
the ribbon is built once in `OnStartup`, so the icon set is fixed at Revit startup — switching Revit's
theme mid-session needs a Revit restart to re-tint the GMS icons (forms, by contrast, re-read the theme
each time they open).

The newer GMS icons are **flat single-color glyphs drawn programmatically** (System.Drawing in a
PowerShell script) at 32px in the icon red **`#B91D08`**, replacing the legacy glossy / ICO-data-with-a-
`.png`-extension art. Redrawn this way: `purgeFamily` (trash can w/ lid), `reset` (circular reset arrow
around a paint-chip swatch), `calendar` (drop-shadow removed). **When you edit or redraw a base icon you
must regenerate its `<name>_dark.png` from the new base** (the `#D6D6D6` recipe above). Render internal
detail (trash-can ribs, the swatch's hanging hole, etc.) as **transparent gaps, not a darker shade**, so
it survives the monochrome dark recolor. (Full repeatable recipe — preview-by-upscaling, etc. — is in the
`gms-icon-design-workflow` memory.)

The **Help** button (Settings panel) and its `KeyboardShortcuts.xml` registration were removed
(2026-06-30); the `HelpMenu.Help` command class still exists (commented-out block in `GMS_tools.cs`).
