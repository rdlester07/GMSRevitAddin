# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A Revit add-in (`GMSRevitAddin`) for Gardner Metal Systems — internal tooling for unit
drawing production, piece tagging/extraction, schedule updates, and graphical overrides. It is a
single SDK-style project (WinForms + WPF), loaded in-process by Revit, built **per Revit version**
from one source tree (see "Multi-version build" below). Revit 2025 and 2026 run on
`net8.0-windows`; Revit 2027 runs on `net10.0-windows`; Revit 2024 runs on `net48`
(.NET Framework 4.8 — the one version not on .NET Core; see "Revit 2024 / net48" below).

## Build & run

The build is **multi-version**: the active `$(Configuration)` carries a Revit-version suffix
(`R24` = 2024, `R25` = 2025, `R26` = 2026, `R27` = 2027), which drives `$(RevitVersion)`. Each
config produces a separate, side-by-side DLL deployed under its own Revit version's folders.

```bash
# Build a specific Revit version (Release). No local Revit install is required to BUILD:
# the Revit API comes from the versioned Nice3point.Revit.Api.* NuGet packages.
dotnet build "GMS Revit Addin.sln" -c "Release R25"
dotnet build "GMS Revit Addin.sln" -c "Release R26"
dotnet build "GMSRevitAddin\GMSRevitAddin.csproj" -c "Release R27"
dotnet build "GMSRevitAddin\GMSRevitAddin.csproj" -c "Release R24"
```

> **Note:** R27 and R24 must be built via the `.csproj` directly (not the `.sln`) because the
> solution-level NuGet restore evaluates `TargetFramework` before per-configuration overrides are
> applied — both override the default TFM (R27 → net10, R24 → net48).

- A bare `dotnet build` (no `-c`) defaults to `Debug R25` so the VS designers resolve a version.
- **There is no test project and no lint step.** Verification is a manual in-Revit smoke test:
  build, ensure the DLL is on the manifest's path (below), launch the matching Revit version,
  confirm the **GMS** ribbon tab loads with all panels/buttons, and exercise the touched commands.
- The build cannot be exercised by launching Revit from the CLI; a human runs Revit.

### Multi-version build (how versioning works)

- `$(RevitVersion)` is derived from the configuration suffix via
  `$(Configuration.Contains('R##'))` blocks in
  [`GMSRevitAddin.csproj`](GMSRevitAddin/GMSRevitAddin.csproj). It selects: the
  `Nice3point.Revit.Api.RevitAPI`/`RevitAPIUI` package versions (`$(RevitVersion).*`), the
  `REVIT<ver>` compile constant (for `#if REVIT####` branching where a version differs — the only
  current use is the `#if REVIT2024` net48 assembly-resolution block in `GMS_tools.cs`, below), the
  generated `.addin` manifest, and the deploy paths.
- The version is also surfaced to runtime code as an `AssemblyMetadata` attribute, read once by
  [`GmsVersion.Number`](GMSRevitAddin/GmsVersion.cs). Version-specific runtime paths
  (`GmsPaths.GmsRevitRoot`, `GmsLog`'s log folder, keyboard-shortcut/Addins folders) derive from
  it — **do not re-introduce a hard-coded "2025"** in paths; use `GmsVersion.Number`.
- **To add a future Revit version (e.g. 2028):** (1) add `Debug R28;Release R28` to
  `<Configurations>`; (2) add a `<PropertyGroup Condition="$(Configuration.Contains('R28'))">`
  setting `<RevitVersion>2028</RevitVersion>`; (3) if that Revit ships on a new .NET runtime, add a
  second `<PropertyGroup Condition="$(Configuration.Contains('R28'))">` block **after** the shared
  `<PropertyGroup>` (after the `<PlatformTarget>x64</PlatformTarget>` block) that overrides
  `<TargetFramework>` — placing it after the shared block is required so it wins at restore time;
  (4) confirm `Nice3point.Revit.Api.RevitAPI 2028.*` exists on NuGet; (5) add the four solution
  configs in [`GMS Revit Addin.sln`](GMS%20Revit%20Addin.sln) (or let VS regenerate them).
  Everything else flows from `$(RevitVersion)`. **Important:** if a new TFM is introduced, the
  new version must be built via `.csproj` directly (not `.sln`) — see the build note above.
  Also check whether Autodesk has changed the all-users manifest folder again (the 2027 change
  is already handled by the `$(RevitVersion) >= 2027` condition in `DeployAddin`; only act if
  a future version moves it somewhere else).

### Revit 2024 / net48 (the one .NET Framework version — extra care)

Revit 2025 was the first version on .NET Core; **Revit 2024 runs on .NET Framework 4.8**. The R24
config (`<TargetFramework>net48</TargetFramework>` + `<LangVersion>latest</LangVersion>`) therefore
needs several net48-only workarounds that the .NET Core versions don't. All are scoped in
[`GMSRevitAddin.csproj`](GMSRevitAddin/GMSRevitAddin.csproj) by `Condition="'$(TargetFramework)' == 'net48'"`
(or `$(Configuration.Contains('R24'))`), so they don't touch R25/R26/R27. **A future .NET Framework
Revit version (e.g. an R23) would reuse this same recipe.**

- **Framework references, not NuGet packages.** `System.Data.Odbc`, `System.Data.DataSetExtensions`,
  and `Microsoft.CSharp` live in the framework GAC on net48 — they're added as `<Reference Include=…>`
  while the NuGet `<PackageReference>`s are conditioned `!= net48`. Because `System.Data.Odbc` is part
  of `System.Data.dll` on Framework, **no `runtimes\` folder is produced and `FlattenOdbcWindowsRuntime`
  is a harmless no-op on net48** (the in-process ODBC-stub trap simply doesn't apply).
- **`Microsoft.VisualBasic` must be referenced explicitly** — `Microsoft.VisualBasic.ApplicationServices`
  is implicit on .NET Core but not auto-referenced under net48 (used in `UpdateOrigins`/`ManualUpdateOrigins`/`BySetForm`).
- **WinForms `.resx` resources need two settings**, or the build/run breaks:
  - `<GenerateResourceUsePreserializedResources>true</GenerateResourceUsePreserializedResources>` +
    a `System.Resources.Extensions` `<PackageReference>` — without it the SDK resource generator fails
    with MSB3822/MSB3823 on the binary (image) resources.
  - `<EmbeddedResourceUseDependentUponConvention>true</EmbeddedResourceUseDependentUponConvention>` —
    net8/net10 name embedded resources `<namespace>.<type>.resources` (what
    `ComponentResourceManager(typeof(Form))` looks up), but net48 defaults to
    `GMSRevitAddin.<filename>.resources`, so every form throws *"Could not find any resources … `<Form>.<Form>.resources` was correctly embedded"* at runtime. Forcing the convention makes net48 match net8.
    (Form namespaces == class name, e.g. `SettingsForm.SettingsForm`, per the one-namespace-per-file idiom.)
- **Runtime assembly resolution** ([`GMS_tools.cs`](GMSRevitAddin/GMS_tools.cs), guarded `#if REVIT2024`).
  The preserialized resources hard-depend on `System.Resources.Extensions`, deployed beside the DLL —
  but Revit loads the add-in in-process and the net48 CLR probes **Revit's install folder (appbase)**,
  not the add-in folder, with strict version binding (it requests 4.0.0.0; the shipped copy is 8.0.0.0).
  So it fails with *"Could not load file or assembly 'System.Resources.Extensions, Version=4.0.0.0…'"*.
  `OnStartup` registers `AppDomain.CurrentDomain.AssemblyResolve += ResolveFromAddinFolder` **first**,
  which `Assembly.LoadFrom`s the sibling `<simpleName>.dll` from the add-in's own directory (bypassing
  version binding). This is the **same in-process-resolution gap as the ODBC note below**, and the
  handler covers any future sibling dependency. This is the only `#if REVIT*` block in the codebase.

## Deployment (important, and non-obvious)

- This solution is a **de-versioned copy** of the original `GMS_Revit2025` add-in and is built to
  **coexist** with it: it has its own `AddInId` GUID, its own `.addin` name (`GMS Revit Addin`),
  and a **distinct** deploy path, so building it never clobbers the production `GMS_Revit2025`
  add-in. It now builds per Revit version (2024, 2025, 2026, and future) from one source tree.
- The add-in manifest is **generated per build**, not checked in. The template
  [`GMSRevitAddin.addin.template`](GMSRevitAddin/GMSRevitAddin.addin.template) holds a
  `$RevitVersion$` token in the `<Assembly>` path; the `GenerateAddinManifest` target writes the
  resolved `GMSRevitAddin.addin` into the output dir (e.g. `<Assembly>` →
  `C:\GMS\Revit\2026\AddIns\GMSRevitAddin\GMSRevitAddin.dll`). The `AddInId` is
  intentionally **stable across versions** (each Revit version reads its own Addins folder).
  Edit the template, never the generated file.
- The `DeployAddin` target runs **only for Release configs** (`Debug` builds skip deploy and stay
  in `bin\<Config>\<tfm>\`, e.g. `bin\Debug R26\net8.0-windows\`). On a Release build it
  copies the build output to `C:\GMS\Revit\$(RevitVersion)\AddIns\GMSRevitAddin\`
  and the generated manifest into Revit's machine-wide Addins folder (loads for all users).
  **Both target folders are created if absent** — so a Release build always populates them,
  provided the build user has write access (an access-denied fails the build).
  - **Revit 2025/2026** — manifest goes to `C:\ProgramData\Autodesk\Revit\Addins\<ver>\`;
    a normal (non-elevated) build suffices.
  - **Revit 2027+** — Autodesk changed the all-users manifest folder to
    `C:\Program Files\Autodesk\Revit\Addins\<ver>\`; the build must be run from an
    **elevated (Run as Administrator) terminal** to write there.
  - Note: if the matching Revit version is **running**, it locks the deployed DLL and the copy
    fails — close Revit before a Release build.

## Live debugging

Yes — the project is wired for it. Because Revit loads the add-in in-process, "live debugging"
means getting Revit to load straight from a `bin\Debug ...\` folder and then attaching a .NET
debugger to the `Revit.exe` process, rather than anything that runs the add-in standalone.

- **`GenerateDebugAddinManifest`** (in [`GMSRevitAddin.csproj`](GMSRevitAddin/GMSRevitAddin.csproj),
  Debug configs only, **and only when `-p:DeployDebugManifest=true` is passed** — see below) writes a
  **second, separate** add-in manifest — its own `AddInId`/`Name`, from
  [`GMSRevitAddin.Debug.addin.template`](GMSRevitAddin/GMSRevitAddin.Debug.addin.template) — into
  Revit's **per-user** Addins folder (`%AppData%\Autodesk\Revit\Addins\<ver>\`). Because it's a
  distinct `AddInId` from the real add-in's, it coexists safely with a Release deploy already sitting
  in the machine-wide folder — Revit loads both as independent add-ins. And because it's the
  per-user folder, **no elevation is needed for any version, including 2027+** (the elevation
  requirement is specific to the all-users folder `DeployAddin` writes to). The generated manifest's
  `<Assembly>` points straight at **this build's own `$(TargetPath)`** in `bin\Debug <cfg>\<tfm>\` —
  not a copy — so the PDB Visual Studio loads always matches what Revit has open.
- **⚠️ The debug manifest is opt-in, not automatic.** An ordinary `dotnet build ... -c "Debug R##"`
  (e.g. a quick compile check) does **not** deploy the debug manifest — only a build that also passes
  `-p:DeployDebugManifest=true` does. This exists so routine Debug builds don't silently leave a
  live-debug manifest sitting in the per-user Addins folder. The manifest only needs to exist **once**
  per Revit version (its `<Assembly>` path doesn't change between builds of the same config), so this
  isn't needed on every build — only before your first live-debug session for a given version:
  - **VS Code** already handles this — the `build-debug-r##` tasks in
    [`.vscode/tasks.json`](.vscode/tasks.json) (used only as the `preLaunchTask` for the "Launch Revit
    20##" configs below) pass the flag, so **F5 there just works**, same as before this flag existed.
  - **Visual Studio** F5 (via `Properties/launchSettings.json`) builds through VS's own build system,
    which has no equivalent way to pass an extra flag. Before your first F5 session for a given Revit
    version, run one manual build with the flag, e.g.
    `dotnet build "GMS Revit Addin.sln" -c "Debug R25" -p:DeployDebugManifest=true` (R24/R27: build the
    `.csproj` directly instead, same as any other build — see "Build & run" above). After that, F5
    keeps working normally until the DLL path changes (e.g. a different Revit version).
- **To debug:** build the `Debug R##` config for the Revit version you want, then either
  (a) launch that Revit version normally and use **Debug > Attach to Process > `Revit.exe`** (pick
  the **.NET** code type for R25/R26/R27, or **.NET Framework** for R24) — simplest, but misses
  anything in `OnStartup` since it already ran before you attach; or
  (b) use the matching profile in [`Properties/launchSettings.json`](GMSRevitAddin/Properties/launchSettings.json)
  (`"Revit 2024"`/`"2025"`/`"2026"`/`"2027"`, each an `Executable` launch pointing at that version's
  `Revit.exe`) so **F5 starts Revit with the debugger already attached** — this one hits
  `OnStartup` breakpoints too.
- **VS Code equivalent:** [`.vscode/launch.json`](.vscode/launch.json) mirrors the same two modes —
  a generic `"Attach to Revit"` config (`processId": "${command:pickProcess}"`) and one `"Launch
  Revit 20##"` config per version, each with a `preLaunchTask` (in
  [`.vscode/tasks.json`](.vscode/tasks.json)) that builds the matching `Debug R##` config first so
  the DLL is fresh before Revit starts. **⚠️ R24 (net48) is best-effort only from VS Code** — the
  C# extension's `coreclr` debug adapter targets .NET Core/5+, and VS Code has no first-party .NET
  Framework debugger equivalent to full Visual Studio's; use Visual Studio's Attach to Process for
  R24 instead.
- **⚠️ Keep the launch profile and the solution configuration in sync yourself** — picking the
  "Revit 2026" launch profile while "Debug R25" is the active solution configuration doesn't error,
  it just means Revit 2026 finds no debug manifest in its own Addins folder (safe no-op: the GMS
  ribbon tab simply won't appear) rather than loading a mismatched build.
- **⚠️ The same file-lock rule as `DeployAddin` now applies to Debug builds too**, once you've
  actually opened Revit against a Debug output: Revit has that exact `bin\Debug ...\GMSRevitAddin.dll`
  open in-process, so rebuilding that same config while Revit is still running fails on a locked
  file, same as a Release build. Attach-to-process debugging doesn't rebuild anything, so this only
  bites when you edit code and need a fresh build — close Revit first, same as Release.
- `FlattenOdbcWindowsRuntime` already runs for Debug builds too (see its comment in the csproj) —
  ODBC-backed commands (`UpdateSchedules.cs`, `ExportParts.cs`) work under live debugging with no
  extra setup.

## Architecture

- **Entry point:** [`GMS_tools.cs`](GMSRevitAddin/GMS_tools.cs) is the single `IExternalApplication`
  (registered as `FullClassName` in the `.addin`). `OnStartup` builds the ribbon (`AddRibbonPanel`)
  and registers application-level Revit events (document opened/saved/synced, view activated, etc.).
- **⚠️ Ribbon commands are wired by string class names.** Each `PushButtonData`/`PulldownButtonData`
  takes a `"Namespace.Class"` string (e.g. `"ShowTagsPerView.ShowTags"`). These strings are NOT
  checked at compile time — **renaming a command's namespace or class compiles fine but breaks the
  button only at runtime.** If you rename anything a button targets, update its string in
  `GMS_tools.cs` and smoke-test that button in Revit.
- **Command layout:** mostly flat, one feature per `.cs` file in `GMSRevitAddin/`, each implementing
  `IExternalCommand`. The codebase uses ~one namespace per file, often with the namespace name equal
  to the class name — and some names collide across files (e.g. a class `ShowTags` exists in BOTH
  the `ShowTags` and `ShowTagsPerView` namespaces). Do not "consolidate namespaces" casually; it
  forces class renames and the string-wiring breakage above.
- **Recurring per-command idioms:** a nested `RevitStartup` class holds the app-event handlers a
  command registers in `GMS_tools.cs`; form-based commands expose a `LaunchForm` command that opens
  a WinForms dialog and drives Revit changes via an `ExternalEvent` handler (Revit's API is
  single-threaded — model edits must run on Revit's thread, not from UI callbacks).
- **`GM Code/CustomCommand/CycleWorkSets/`** is the cleanest module (separated command / external
  event / `Helpers/`) and is the reference pattern for new, well-structured features.

### Shared infrastructure (prefer these over re-inventing)

- [`GmsVersion`](GMSRevitAddin/GmsVersion.cs) — `GmsVersion.Number` is the Revit major version
  ("2025"/"2026"/"2027") this DLL was built for, read from build-injected assembly metadata. Use it
  for any version-specific path instead of a literal. `GmsVersion.BuildTimestamp` is a sibling
  property (same `AssemblyMetadata` mechanism, a second `<AssemblyMetadata>` item in the csproj
  evaluated fresh via an MSBuild `$([System.DateTime]::Now...)` property function on every compile)
  — added because the csproj never sets `<Version>`/`<AssemblyVersion>`/`<FileVersion>`, so every
  build's `AssemblyFileVersion` is the SDK's unchanging default `1.0.0.0` and can't identify which
  build is deployed. `SettingsForm` shows both in a read-only text box.
- [`GmsLog`](GMSRevitAddin/GmsLog.cs) — thread-safe file logger (`%LOCALAPPDATA%\GMS\Revit<ver>\Logs`,
  e.g. `Revit2025`), never throws. Use `GmsLog.Error(context, ex)` in catch blocks instead of swallowing exceptions.
- [`GmsPaths`](GMSRevitAddin/GmsPaths.cs) — all UNC/local paths (the `\\gmsfs01\Common` share, the
  Access extrusion DB, PDF output root, the version-specific `C:\GMS\Revit\<ver>` install root). Add
  new fixed paths here, not as inline literals.
- [`RevitParameterHelper`](GMSRevitAddin/RevitParameterHelper.cs) — null-safe `GetString` and a
  logging `TrySetString` for the `LookupParameter` get/set pattern that recurs ~120 times.
- [`GmsUi`](GMSRevitAddin/GmsUi.cs) — WinForms dialog helpers (`Show`/`ShowWarning`/`ShowError`)
  that **parent dialogs to Revit's main window** via `GmsUi.Owner`. Prefer these over a bare
  `MessageBox.Show(...)`: an owner-less dialog can open *behind* Revit and lock the ribbon/menus
  (modal loop) while the model stays pannable. The `ShowError` overloads also log via `GmsLog`.
  Most legacy call sites (~250) still use bare `MessageBox.Show`; route new dialogs through `GmsUi`.
  - **Where the owner HWND comes from.** `GmsUi.OwnerHandle` prefers `GmsUi.RevitMainWindowHandle`,
    captured from `UIControlledApplication.MainWindowHandle` in `GMS_tools.OnStartup` (retried from
    `UIApplication.MainWindowHandle` on first `Idling`). It falls back to
    `Process.GetCurrentProcess().MainWindowHandle` **only** if that's unavailable — that call returns
    the first *visible unowned top-level window in the process*, which is not guaranteed to be Revit's
    frame and can pick up a transient window. Don't make it the primary source again.
  - **⚠️ Never `NativeWindow.AssignHandle(...)` on Revit's main window.** `AssignHandle` **subclasses**
    the target HWND (installs WinForms' window proc on Revit's frame); if the `NativeWindow` is then
    collected without `ReleaseHandle()`, its finalizer unsubclasses off-thread and out of order, wedging
    the frame. `ProgressForm` did exactly this and produced the signature failure below. Use
    `Show(GmsUi.Owner)` — a plain `IWin32Window` handle wrapper that parents via `GWL_HWNDPARENT` and
    touches nothing else.
  - **Diagnosing a "locked UI".** Ribbon/menus dead while the drawing canvas still pans is *always* a
    window-ownership bug, not a hung command — the canvas is a separate child HWND with its own window
    proc, so it survives a wedged or never-reactivated main frame. Two distinct causes: a bad/stale
    owner HWND, or an unbalanced `EnableWindow(false)` from a modal dialog. **`GmsUi.ActivateRevit()`**
    recovers both and logs a distinct line when it finds the main window *disabled* — that log line in
    `%LOCALAPPDATA%\GMS\Revit<ver>\Logs` is what tells the two apart after the fact.
  - **Modeless forms need explicit teardown.** A modal dialog's message loop restores activation on its
    own; a modeless form outlives the command that showed it and does not. Show it with `GmsUi.Owner`
    and call `GmsUi.ActivateRevit()` from its `FormClosed` (see `ExportResultsController.OnFormClosed`).
    Watch the ordering if that handler clears shared state: `Close()` raises `FormClosed`
    **synchronously**, so close any previous instance *before* setting up the new one, or the teardown
    wipes the state you just assigned.
- [`DarkTheme`](GMSRevitAddin/DarkTheme.cs) — `DarkTheme.Apply(Form)` recursively themes a WinForms
  form to **match Revit's current UI theme** (it reads `UIThemeManager.CurrentTheme` and picks a
  light or dark variant of the palette; the name is legacy — it is no longer dark-only). 11 forms
  call it after `InitializeComponent()` (constructor or `*_Load`). **New WinForms dialogs should call
  `DarkTheme.Apply(this)`** so they match Revit. Two surfaces theme themselves instead: the
  `StockOptimizerForm` (its own `Theme` class) and the WPF `DetailItemPalettePane`/`WorksetCyclerWindow`
  (their own palettes) — all keyed off `UIThemeManager` the same way. To retune colors, edit the one
  `Palette` in `DarkTheme.cs`.
  - **Palette:** an **11-shade neutral gray scale** (Tailwind-gray-style: `50 f9fafb` … `950 030912`)
    used for every surface/background/border/text — **no blue/purple accent**; selection, hover, and
    pressed states sit on distinct mid-to-dark gray steps so they stay distinguishable without hue. The
    same gray values are mirrored in `StockOptimizerForm.Theme`, `DetailItemPalettePane`, and
    `WorksetCyclerWindow` (none of those three have been migrated to the monochrome-primary-button or
    borderless-field conventions below yet — they're still on their pre-existing self-themed styling).
    The **only** deliberate non-gray exceptions are `StockOptimizerForm`'s `Good`/`Warn`/`Bad`
    (green/amber/red, pass/warn/fail meaning) and `DarkTheme`'s own `ErrorText` (`CurrentErrorText`,
    same red hex pair as `Bad` — inline field-validation messages, see `CreateUnitSheetForm`). Keep any
    new accent needs on the gray scale unless it's a genuine semantic status indicator like those.
    An earlier pass gave the "primary" button below a true accent color (GMS's ribbon-icon red); that
    was reverted by request, so **the whole add-in is monochrome-only today** — there is no accent hue.
  - **One emphasized "primary" button per dialog, still monochrome.** `DarkTheme.MarkPrimary(button)`
    — call it *before* `Apply`, which reads the marker rather than pushing a live update — paints that
    one button with `CurrentPrimaryFill`/`CurrentPrimaryText`: the near-inverse of the form background
    (lightest gray step in dark mode, darkest in light mode), not a color. This is wired on 8 of the 11
    `DarkTheme.Apply` dialogs' main action (`CreateUnitSheetForm.buttonOK`, `CollectDies.button_Start`,
    `UnitReleaseForm.buttonApply`, `BuildBunkForm.buttonApply`, `BuildBunkSmallForm.buttonApply`,
    `ShowHideSheetSets.buttonStart`, `BySetForm.button_Start`, `SettingsForm.buttonClose`).
    Deliberately **not** wired on `DimensionNoteForm` (every preset button is an equal-weight pick from
    a selector grid, not a fill-then-submit form — no single button should stand out) or on
    `ProgressForm`/`ExportResultsForm` (neither has any buttons).
  - **Borderless "flat" fields.** `TextBox.BorderStyle` is `None`, not `FixedSingle` — legibility comes
    from `ControlBack`'s fill contrast instead of a drawn border. `ControlBack` is deliberately **one
    gray step past `PanelBack`, not level with it** — a field level with `PanelBack` disappears
    completely when its parent is a `GroupBox`/`Panel` (which also paint `PanelBack`), not just when
    it's a bare form background. (`ComboBox` was *not* flattened the same way — WinForms exposes no
    border-style hook on it short of owner-drawing the whole control.)
  - **⚠️ Never hard-code `SystemColors.*` in a form.** `SystemColors.ControlText`/`ControlDark` don't
    follow Revit's theme, so a form that assigns them (typically a placeholder that swaps color on
    focus) renders dark-on-dark once `Apply` has themed the background. `Palette` is `private`; use the
    `internal static` accessors **`DarkTheme.CurrentForeText`** (primary text) and
    **`DarkTheme.CurrentMutedText`** (placeholder/hint — gray-400 dark / gray-500 light) instead. Note
    `Apply` **overwrites** a designer-set `ForeColor`, so a muted placeholder must be re-applied *after*
    `DarkTheme.Apply(this)` in the form's `Load` (see `CreateUnitSheetForm`). Also don't reach for
    `TextBox.PlaceholderText` — it's .NET Core 3.0+ only and **breaks the net48/R24 build**.
  - **Rounded corners:** `Apply` also rounds buttons (6px), and cards/panels/single-line inputs (10px)
    via a shared region-rounding helper (`ApplyRoundedCorners`/`RoundedRect`) wired into
    `ApplyToControls`, so all `DarkTheme.Apply` forms get the "iPad-app" look for free. The self-themed
    surfaces reproduce it: `StockOptimizerForm` has its own `RoundControl`/`RoundedPath`; the WPF
    templates use native `CornerRadius`. New WinForms dialogs get rounding automatically through `Apply`.
  - **Font:** `Apply` also re-fonts the form and every control to **`DarkTheme.UiFontName`**, preserving
    each control's existing size/unit/style (via `ApplyFont`), so all forms share one font in both
    themes. This runs regardless of theme (font is theme-independent; only colors switch). `UiFontName`
    is **not a `const`** — it's a resolved `static readonly` that prefers **`Segoe UI Variable Text`**
    (Windows 11's modern UI font — crisp, neutral terminals, chosen to match a reference design) and
    **falls back to `Segoe UI`** at runtime if that family isn't installed (Windows 10). It's the
    **single source of truth**: `StockOptimizerForm`, `ExportResultsForm`, and the WPF
    `WorksetCyclerWindow` all reference it; `DetailItemPalettePane.xaml` repeats the literal
    `FontFamily="Segoe UI Variable Text, Segoe UI"` (XAML can't bind the field, so it hard-codes the
    same fallback list — **keep these two in sync by hand** if `UiFontName` changes again). Keep new
    dialogs on the shared font by routing through `DarkTheme.Apply` rather than pinning a font in the
    designer. Previously preferred `Segoe UI Rounded` (an iPadOS-like bubble look) — dropped when the
    add-in's visual language moved to a crisper, non-rounded reference design. (Note: this is the Revit
    *UI* font; Arial is Revit's typical *annotation/text-note* font — different things.)

### Ribbon icon theming (light/dark)

The GMS ribbon icons have two variants: the original dark glyphs (`Resources/<name>.png`, used in
Revit's light theme) and **monochrome light-gray `Resources/<name>_dark.png`** variants (used in dark
theme so they read on the dark ribbon). [`GMS_tools.cs`](GMSRevitAddin/GMS_tools.cs) routes every button
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

### Notable feature modules

- **`DetailItemPalette/`** — a **dockable pane** (WPF `IDockablePaneProvider`) migrated from the
  retired "GMS Tools" add-in. Selecting a detail item/group lists the sheets it appears on; clicking a
  sheet navigates + selects. Dockable panes **must be registered in `OnStartup`**
  (`PaletteModule.RegisterPane`), but `UIApplication` isn't available then, so `GMS_tools.OnStartup`
  grabs it on the first `Idling` event (one-shot `OnFirstIdle_DetailItemPalette`) to wire the
  `ExternalEvent` + `SelectionChanged` listener. Keeps its original `GMS.Tools.DetailItemPalette`
  namespace; logs via `GmsLog`.
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
- **[`UpdateFramingWeights.cs`](GMSRevitAddin/UpdateFramingWeights.cs)** — `"UpdateFramingWeights.UpdateWeights"`
  (GMS Tools panel, "Update Weights" button). Iterates every **generic-model family type**
  (`OfCategory(OST_GenericModel).WhereElementIsElementType()`) that has the **`Framing - Weight / Ft`**
  parameter, reads its **`Framing - Die Number`**, and from the GMS Extrusion Access DB
  (`GmsPaths.ExtrusionDatabase`, table `Extrusion Data`, matched on `Die Number`) writes `Weight` →
  `Framing - Weight / Ft` (storage-type aware) and `<Alloy>-<Temper>` → `Framing - Alloy / Temper`.
  Reuses the **`UpdateSchedules` ODBC recipe**: copy DB to `%TEMP%\GMS`, ODBC `Driver={Microsoft Access
  Driver (*.mdb, *.accdb)};Dbq=…`, then `cn.Close(); Thread.Sleep(500); OdbcConnection.ReleaseObjectPool();
  GC.Collect();`. **`TransactionMode.Manual`** (DB reads happen before the transaction; one transaction
  wraps all writes; queries are **parameterized** by die and cached). Unmatched/blank dies are skipped
  and reported via `GmsUi`. Shows the shared modeless `ProgressForm` across the read + write loops. (Note
  the family/schedule param is `Framing - Weight / Ft` — distinct from the `Schedule - Weight / Ft` that
  `UpdateSchedules` writes.)
- **[`CreateUnitSheet.cs`](GMSRevitAddin/CreateUnitSheet.cs)** — `"CreateUnitSheet.CreateUnitSheet"`
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

### WinForms gotchas worth knowing (learned the hard way)

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

### External dependencies & data

- **NuGet dependencies are deliberately minimal.** Beyond the Revit API packages (and `Microsoft.CSharp`
  for `dynamic`), the only "extra" framework assembly the add-in actually calls is
  **`System.Data.Odbc`** (Access DB access in `UpdateSchedules.cs` and `ExportParts.cs`). The project once referenced
  the `Microsoft.Windows.Compatibility` meta-package, which pulled ~25 unused assemblies
  (ServiceModel/Speech/Management/etc.) into the deploy — that was removed in favor of the single
  direct `System.Data.Odbc` reference, and the `DeployAddin` target strips the non-Windows
  `runtimes\` copies. A Release deploy is now ~7 files. **Do not re-add a broad meta-package** to
  satisfy one type; add the specific package instead. If a command throws `FileNotFoundException`
  for a `System.*` assembly at runtime, add that one package — don't reintroduce the meta-package.
- **⚠️ `System.Data.Odbc` and in-process RID loading.** `System.Data.Odbc` ships a cross-platform
  **stub** at the output root (`lib/net*`, ~100 KB) that throws *"System.Data.ODBC is not supported on
  this platform"*, plus the **real Windows implementation** (~300 KB) under
  `runtimes\win\lib\<tfm>\`. Normal .NET apps resolve the right one via `.deps.json` RID resolution,
  but **Revit loads the add-in in-process and does NOT do that** — it loads whatever
  `System.Data.Odbc.dll` is in the root folder, i.e. the stub, so every `OdbcConnection` throws at
  runtime even though the build/deploy succeeded. The `FlattenOdbcWindowsRuntime` target in
  [`GMSRevitAddin.csproj`](GMSRevitAddin/GMSRevitAddin.csproj) fixes this by **overwriting the root
  copy with the `runtimes\win` implementation** after every build (Debug and Release). **Do not
  remove that target** without an equivalent fix (e.g. a `win-x64` `RuntimeIdentifier`), or ODBC
  breaks at runtime. The same trap applies to any future native/RID-specific package — flatten its
  `runtimes\win` asset into the root. Verify the fix by checking the root DLL is ~300 KB, not ~100 KB.
- **Extrusion data** (`UpdateSchedules.cs`) is read from an Access `.accdb` on the file share via
  ODBC (the `OdbcConnection`/`OdbcCommand` path; the OLEDB lines are commented out). The code copies
  the DB to `%TEMP%\GMS` and uses a deliberate `Sleep`+`ReleaseObjectPool`+`GC.Collect` per query to
  work around ACE file locking — keep that workaround if editing.
- **Parts export** — ⚠️ **the "Export Pieces" ribbon button is wired to `ExportParts.ExportPieces`,
  NOT `ExportParts.Export` directly.** `ExportPieces` (a thin orchestrator at the bottom of
  [`ExportParts.cs`](GMSRevitAddin/ExportParts.cs)) runs a **Parts Manager pre-flight check**, then
  `ManualUpdateOrigins.ManualUpdate.Execute` (update **all** tag origins doc-wide), and, **unless
  that returns `Result.Cancelled`** (elements checked out by other users → abort), then
  `new ExportParts.Export().Execute(...)`. **Pre-flight:** `ExportPieces` calls
  `Export.GetPartsManagerDbPath(doc)` (now `internal static`, accepts a `Document` so it is callable
  outside `Execute`); if a path is resolved it checks whether the `.laccdb` lock file exists (ACE
  creates it the moment the DB is opened and removes it when the last connection closes), reads it
  with `FileShare.ReadWrite` to parse machine\user holder names (`ReadLockFileHolders` — 64-byte
  records: 32 bytes machine + 32 bytes user, null-terminated ASCII), and attempts an `Exclusive=1`
  ODBC connection test (a default shared-mode open always succeeds even with the file open, so it
  cannot detect an in-use DB). If the lock file exists **or** the exclusive open fails, a
  `TaskDialog` ("Parts Manager In Use") lists who has it open and offers **Cancel** / **Proceed
  anyway** — giving the user a chance to close it before the long tag-update runs. If the user
  cancels, `Result.Cancelled` is returned immediately. The write-time catch in `WriteRowsToAccess`
  remains as a backstop (someone could open it during the tag update). There is **no set-selection
  form** in this path; the only other validation is `Export`'s own path check (unset/missing →
  `Result.Failed` + `GmsUi.ShowError`). **History (2026-06-29):** the button previously ran
  `BySetForm.LaunchEPForm` (Parts-Manager validation + a by-set picker → by-set tag update → export).
  That was replaced because `ExportParts.Export` always reads the **whole** schedule, so the set
  picker never scoped the export. `BySetForm.LaunchEPForm` and the `BySetForm` "Export Parts by Set"
  path (`button_Start_Click` → `ExportPartsBySet.Export` → `UpdateTagsBySet.UpdateTags`) are now
  **dead code** (left in place; the "Update Tags → By Set" button still uses `BySetForm.LaunchForm`).
  The legacy `ExportParts.Export.partsManagerLocation`/`contExport` statics are likewise unused by the
  live path.
  `ExportParts.Export.Execute` reads the `"(DO NOT OPEN) Parts Collection - Pieces"` schedule directly
  via the Revit API (`ViewSchedule.GetTableData()` / `GetCellText(SectionType.Body, …)`) and
  **writes** the rows into a per-project Access `.accdb` via ODBC. It clears the target table
  (`PieceInstance`) and re-inserts inside a single `OdbcTransaction` (all-or-nothing; rolls back on a
  mid-write lock/failure), using **parameterized** inserts (schedule cell text is arbitrary — never
  string-concat into SQL). The DB path comes from the `"Parts Manager Location"` GlobalParameter (see
  Configuration below), not `GmsPaths`. It connects **directly** to the target DB (no `%TEMP%` copy,
  since it's the write target) but keeps the same `Sleep`+`ReleaseObjectPool`+`GC.Collect` ACE-lock
  release. The destination table name + column order are constants at the top of `ExportParts.cs` and
  must match the schedule's visible columns left-to-right. (This replaced an older flat-text `.txt`
  export.) **Type coercion / row-skipping:** `WriteRowsToAccess` reads each destination column's CLR
  type from the live schema (`GetColumnTypes`) and binds values accordingly (blank → `NULL`), because
  Access rejects text→Number with `[22018]`. The `PieceInstance.Number`/`Quantity` columns are
  **Long Integer**, but piece numbers can be **alphanumeric** (e.g. `02A`); such rows are **skipped**
  (`RowSkipReason`), logged at INFO, and counted in the success dialog — they are silently **omitted**
  from the export, not stored. (The `PieceInstanceImport` staging table has the same `Int32` `Number`,
  so it shares the limitation; storing alphanumeric numbers would require a text column — a Parts
  Manager schema change the team declined.)
- **Parts export results form** ([`ExportResultsForm.cs`](GMSRevitAddin/ExportResultsForm.cs)) — when
  the export skips any pieces, `ExportParts.Execute` shows a **modeless** WinForms results window
  (summary line + a grid of the skipped pieces' Prefix/Number/Origin/OriginSheet) instead of the plain
  `GmsUi.Show` dialog; a clean run still uses the simple dialog. The grid shows **one row per matching
  tag instance** (not per schedule row), so clicking a row selects + zooms to **exactly one** instance;
  each row's Origin/Origin Sheet is read from **that instance's own** parameters. Because of this the
  grid's row count can exceed the schedule-row skip count, so the summary reports **both** (e.g.
  "Skipped 3 pieces … (7 tag instances listed below)"). Because Revit's API is single-threaded, the row
  click **raises an `ExternalEvent`** (`ShowPieceHandler` → `Selection.SetElementIds` + `ShowElements`)
  rather than calling the API from the WinForms callback — same idiom as `CycleWorkSets`
  (`ExportResultsController` holds the single open form, the `UIDocument`, and the event). Skipped rows
  are matched back to `FamilyInstance`s in `ResolveSkippedPieces` via
  `new FilteredElementCollector(doc, schedule.Id)` + the `"Prefix"`/`"Number"` instance parameters; a
  schedule row matching several instances becomes **several rows** (one `SkippedPiece` per id), while a
  row with no match is kept once as a gray, non-clickable entry. `ShowElements` raises a "no open view shows
  the highlighted elements … Continue?" prompt when the piece isn't in an open view; the handler
  **auto-confirms it** by subscribing to `UIApplication.DialogBoxShowing` and calling `OverrideResult(1)`
  **only around the `ShowElements` call** (so no unrelated dialog is suppressed). Note: `ExportResultsForm.cs`
  aliases `ElementId` and avoids `using Autodesk.Revit.DB;` because `Form`/`Color` collide between
  `Autodesk.Revit.DB` and WinForms/`System.Drawing`.
- **Configuration** is split: WinForms/UI state lives in `Properties.Settings` (`app.config`), while
  some project settings (e.g. Parts Manager location, fastener image scale) are stored as Revit
  `GlobalParameters` and read in `SettingsForm.cs` (the `"Parts Manager Location"` parameter is set
  there and consumed by `ExportParts.cs` as the Access target path). `SettingsForm` also shows a
  read-only "which build is this" line (`GmsVersion.Number` + `GmsVersion.BuildTimestamp`) — its title
  bar used to hard-code a static, never-updated `"v1.0.0"` instead; that's gone now.
- The `Resources/` PNGs are embedded as WPF resources and referenced via
  `pack://application:,,,/GMSRevitAddin;component/Resources/...` URIs in the ribbon.
