---
name: gms-feature-modules-reference
description: Reference notes for specific GMSRevitAddin feature modules — DetailItemPalette, TaggingPalette, StockLengthOptimizer, UpdateFramingWeights.cs, and CreateUnitSheet.cs (including CreateUnitSheetForm's ComboBox type-ahead filtering gotchas). Use when reading, modifying, or debugging any of these specific files/features.
---

# Notable feature modules

- **`DetailItemPalette/`** — a **dockable pane** (WPF `IDockablePaneProvider`) migrated from the
  retired "GMS Tools" add-in. Selecting a detail item/group lists the sheets it appears on; clicking a
  sheet navigates + selects. Dockable panes **must be registered in `OnStartup`**
  (`PaletteModule.RegisterPane`), but `UIApplication` isn't available then, so `GMS_tools.OnStartup`
  grabs it on the first `Idling` event (one-shot `OnFirstIdle_DetailItemPalette`) to wire the
  `ExternalEvent` + `SelectionChanged` listener. Keeps its original `GMS.Tools.DetailItemPalette`
  namespace; logs via `GmsLog`.
- **`TaggingPalette/`** — a second **dockable pane** (`GMS.Tools.TaggingPalette` namespace, button
  `"ShowTaggingPalette"` placed right after the Detail Item Palette button, same panel), registered
  unconditionally in `OnStartup` (`TaggingPaletteModule.RegisterPane`) since — unlike the Detail Item
  Palette — it needs no `UIApplication`-dependent listener wired up later; its `ExternalEvent` is
  created lazily on first `ShowPaletteCommand.Execute`. Three tabs list family types (grouped by
  family name) as clickable buttons: **Detail Items** (`OST_DetailComponentTags`), **Generic
  Models** (`OST_GenericModelTags`), and **Unit/Pieces** (`OST_CurtainWallPanelTags` + the
  `"GAIT - Piece Tag"` family, matched by name, merged together). Clicking a type button sets it as
  the document's default type for its category (`Document.SetDefaultFamilyTypeId`) then posts the
  matching native command — decided **three-way**, not two, in `TaggingPaletteModule.ResolveCommand`:
  `Category.IsTagCategory` → `PostableCommand.TagByCategory`; `CategoryType.Annotation` (a plain
  Generic Annotation family like `"GAIT - Piece Tag"`) → `PostableCommand.Symbol`; anything else →
  `PostableCommand.PlaceAComponent` — because `PlaceAComponent` only places elements "in the building
  model" and silently no-ops for a 2D annotation-only family. A persistent `OnIdling` handler (not
  `ViewActivated`, which misses "Activate View" inside a sheet — a known Revit API gap) re-evaluates
  button enablement whenever the active view changes: a Drafting View has no model geometry, so
  types tagging a real model category (`RequiresModelView`) grey out there. Also ships
  `TagLeaderDefaults.cs`, which defaults every newly-placed Detail Item Tags/Generic Model Tags tag's
  Leader Type to Free End (`IndependentTag.LeaderEndCondition`), document-wide — not scoped to this
  palette, applies no matter how the tag was placed. There's no public API to pre-seed the
  interactive Tag tool's Options Bar leader default, so this caches new tag ids on `DocumentChanged`
  (can't modify the document from inside that event) and corrects them on the next `Idling` tick in
  its own transaction — the same cache-then-fix-on-idle idiom as `DuplicateViewOptions.RevitStartup`.
  Custom icons `tagpalette_32/16(.png/_dark.png)` follow the flat-glyph recipe in the
  `gms-ribbon-icon-workflow` skill.
- **`StockLengthOptimizer/`** — cutting-stock optimizer (a native C#/WinForms port of an external
  `index.html` tool) reachable from the **end of the "GMS Tools" ribbon panel**
  (`"StockLengthOptimizer.LaunchForm"`). Reads the **`(DO NOT OPEN) Framing Stock Lengths`** schedule
  (`CutListReader`, same `GetTableData`/`GetCellText` idiom as `ExportParts`, with **keyword-based
  column auto-detection** off the header row), groups by Finish+Die, runs First-Fit-Decreasing
  bin-packing (`StockOptimizer`, pure logic, no Revit refs), and shows a theme-aware WinForms results
  form + a 4-sheet `.xlsx` export. Two things to know: (1) the command is **`TransactionMode.Manual`,
  not `ReadOnly`** — reading schedule cells can force a schedule **regen** (a doc change) that ReadOnly
  blocks with *"Changes are disabled for the active document"*; (2) the `.xlsx` is written by a
  **self-contained OOXML writer** (`XlsxWriter`, via `System.IO.Compression.ZipArchive`) — **no
  spreadsheet NuGet** (keeps with the minimal-dependency rule; on net48/R24 the two
  `System.IO.Compression*` framework assemblies are added as `<Reference>`s). The form
  (`StockOptimizerForm`) follows Revit's light/dark theme via `UIThemeManager.CurrentTheme` — note it
  must **not** `using Autodesk.Revit.UI;` (its `TextBox`/`ComboBox` collide with WinForms;
  fully-qualify `UIThemeManager`/`UITheme` instead). Both result grids use
  `AutoSizeColumnsMode.AllCells` (autofit to content) with `TextCol`/`NumCol` setting `MinimumWidth`
  rather than a fixed `Width`, so short numeric columns stay narrow while long text isn't clipped.
  - **Two-stock-length optimization (per die, automatic).** `RunRecommend` doesn't just pick the
    single least-drop stock length anymore — it also runs a **heuristic 2-length search**
    (`OptimizeDieMulti` + `OpenBestFitBin` in `StockOptimizer.cs`): anchor on the best single length,
    then search for the best complementary second length (O(N), not an exhaustive O(N²) pair search).
    `Bin` and `PatternInfo` now carry their own `StockIn`, so a die's patterns can legitimately belong
    to either of 2 purchased lengths — short pieces route to the shorter length, long pieces to the
    longer one, via `OpenBestFitBin` picking whichever allowed length wastes the least per new bin.
    A 2-length result is only adopted over the single-length one if it clears **both** an absolute
    floor (`MinTwoLengthSavingsIn`, 24") **and** a relative floor (`MinTwoLengthSavingsFraction`, 3%
    of the die's required length) — the relative floor exists because a fixed absolute-only threshold
    let a near-uniform cut list trigger a pointless second length for a ~2% gain (caught via a
    standalone sanity run of `StockOptimizer.cs` outside Revit — the file has no Revit references, so
    it can be exercised in a throwaway console project for quick algorithm checks). A die that
    previously errored "exceeds search range" can now be rescued if 2 lengths together cover it.
    UI reflects this: the die grid's Stock column shows `"18'-0" + 26'-0""` for 2-length dies with a
    `★ 2 lengths — saves …` badge, the pattern grid gained a **Stock** column (each pattern's own
    length — this also fixed a latent bug where per-pattern drop was computed against the die's
    primary length instead of that pattern's own), and the Excel Summary sheet emits **one row per
    purchased length** (not one row per die) with a "2 Lengths" note column.
- **[`UpdateFramingWeights.cs`](../../../GMSRevitAddin/UpdateFramingWeights.cs)** — `"UpdateFramingWeights.UpdateWeights"`
  (GMS Tools panel, "Update Weights" button). Iterates every **generic-model family type**
  (`OfCategory(OST_GenericModel).WhereElementIsElementType()`) **and every detail-item family type**
  (`OfCategory(OST_DetailComponents).WhereElementIsElementType()`), resolves each type's die number, and
  from the GMS Extrusion Access DB (`GmsPaths.ExtrusionDatabase`, table `Extrusion Data`, matched on
  `Die Number`) writes `Weight` (storage-type aware) and `<Alloy>-<Temper>` into that category's own
  weight/material parameters — **the two categories use different parameter names for the same data**:
  generic-model types have the gating **`Framing - Weight / Ft`** parameter and write
  `Framing - Alloy / Temper`; detail-item types have the gating **`Component - Weight / Ft`** parameter
  and write **`Component - Material`** (a type only participates if it carries its category's gating
  weight param — otherwise left untouched). **Die resolution also differs by category:** generic-model
  types read it straight from their own **`Framing - Die Number`** type parameter; detail-item types have
  no such parameter, so `ParseDieFromFamilyName` derives it from the family name instead — split on `-`,
  second segment is the die (`"Extrusion-1234"` → `"1234"`), with a `"GMD"` segment folded into the next
  one (`"Extrusion-GMD-5678"` → `"GMD-5678"`) — the same convention `UpdateSchedules.cs`'s legacy
  extrusion-data path uses for detail items (though that *third* path writes yet another pair,
  `Schedule - Weight / Ft`/`Schedule - Material` — three differently-named param pairs now exist across
  the codebase for the same conceptual weight/material data; don't assume they're interchangeable).
  Reuses the **`UpdateSchedules` ODBC recipe**: copy DB to `%TEMP%\GMS`, ODBC `Driver={Microsoft Access
  Driver (*.mdb, *.accdb)};Dbq=…`, then `cn.Close(); Thread.Sleep(500); OdbcConnection.ReleaseObjectPool();
  GC.Collect();`. **`TransactionMode.Manual`** (DB reads happen before the transaction; one transaction
  wraps all writes; queries are **parameterized** by die and cached across both categories). Unmatched
  dies, generic-model types with a blank `Framing - Die Number`, and detail-item types whose name doesn't
  parse are each skipped and reported separately via `GmsUi`. Shows the shared modeless `ProgressForm`
  across the read + write loops.
- **[`CreateUnitSheet.cs`](../../../GMSRevitAddin/CreateUnitSheet.cs)** — `"CreateUnitSheet.CreateUnitSheet"`
  ("New Unit Sheet" button). Prompts via `CreateUnitSheetForm`, then either creates a blank unit sheet
  (`createNewUnit`) or duplicates an existing unit (`duplicateUnit` → a fresh `ViewDrafting` +
  `ElementTransformUtils.CopyElements`, a "No Title" viewport at the source's position, then retags
  every `GAIT - Piece Tag` to the new unit). **It no longer generates a per-unit schedule** (changed
  2026-07-31): the model holds **one shared schedule named `"(Do Not Open) Unit Pieces"`** and both
  paths just place another `ScheduleSheetInstance` of it — the old `UNIT_<n> PIECES` note-block
  construction (fields/sort/Origin filter/column styling/`Schedule - Unit Drawing` template) is gone.
  Resolve it with **`CreateUnitSheet.FindUnitPiecesSchedule(doc)`** (matches `UnitScheduleName`
  case-insensitively). ⚠️ **Because one schedule is shared across every unit sheet, it only shows
  per-unit rows if its definition has "filter by sheet" enabled** — the old per-unit Origin filter is
  what used to do that. Both **pre-flight checks run before any transaction** so an abort leaves no
  half-built sheet: `Execute` verifies the schedule exists (`CUS7`), and `duplicateUnit` resolves the
  source sheet (`CUS8`) **and** its placed schedule instance (`CUS9`) *before* calling `createNewUnit`
  — hence the source-sheet lookup sits at the top of the method, not after. The duplicate path places
  the schedule at the source instance's `Point`; the from-scratch path uses the fixed
  `DefaultSchedulePlacement`. `AddUnitSchedule.cs` (`AddUnitSchedule.AddSchedule`), the other per-unit
  schedule builder, is **retired** — commented out in place (it was already unwired: no ribbon string
  referenced it). Its form, `CreateUnitSheetForm`, has a **type-to-filter** unit combo box
  (`DropDownStyle.DropDown` + custom substring filtering on `TextChanged`); see the WinForms gotchas
  below. **Redesigned 2026-08** (the add-in's pilot for the current UI pass): whether to duplicate is
  now an explicit `CheckBox` (`checkBoxDuplicate`), not inferred from the combo box being blank —
  unchecking it clears any typed/selected value so a stale choice can't survive. New-unit-number
  validation is centralized in `TryValidateNewUnitNumber` (still just "contains `U-`, no spaces/braces,
  not already taken" — deliberately not tightened to a stricter format regex, since other unit-number
  matching elsewhere in the codebase only ever checks "contains `U-`" too) and runs live as the user
  types, showing an inline hint/error label (`DarkTheme.CurrentErrorText` when invalid) instead of a
  blocking popup, with `buttonOK` (marked primary — see `DarkTheme.MarkPrimary`) disabled until valid.

## WinForms gotchas worth knowing (learned the hard way)

Filtering a `ComboBox` as the user types (`CreateUnitSheetForm.comboBox1_TextChanged`) needs four
non-obvious guards — all four were real bugs, so keep them if you touch that code:
- **`DropDownList` can't be typed into at all**, and it silently ignores `AutoCompleteMode`/
  `AutoCompleteSource` — those settings look active but do nothing. Editable filtering needs
  `ComboBoxStyle.DropDown`, and the native `AutoComplete*` must stay **off** or its suggestion popup
  competes with the filtered dropdown.
- **Re-entrancy:** rewriting `Items`/`Text` re-raises `TextChanged` — guard with a `suppressFilter` flag.
- **Caret reset:** repopulating `Items` *and* setting `DroppedDown` both reset the caret to 0, which
  makes typing look reversed. Capture `SelectionStart` first, restore it last.
- **`DroppedDown = true` hides the mouse cursor** (Windows' "hide pointer while typing"), leaving the
  filtered list unclickable. Call `System.Windows.Forms.Cursor.Show()` right after — `ShowCursor` is
  ref-counted, so keep it inside the `!DroppedDown` guard so it fires once per open, not per keystroke.
- Picking a list item also raises `TextChanged`; re-filtering there reopens the dropdown the user just
  closed. Flag it in `SelectionChangeCommitted` and skip that one change.
- Validate with the **`Validating` event + `e.Cancel`**, not `Leave`, and set
  `CausesValidation = false` on the Cancel button so a bad value can still be abandoned.
