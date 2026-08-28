---
name: gms-live-debug-setup
description: Live-debug the GMSRevitAddin add-in against a real Revit process — attach to Revit.exe, set up the per-user debug add-in manifest (DeployDebugManifest), or use the Visual Studio / VS Code launch profiles. Use when the user wants to debug, set a breakpoint in, or step through the add-in while Revit is running.
---

# Live debugging GMSRevitAddin

Yes — the project is wired for it. Because Revit loads the add-in in-process, "live debugging"
means getting Revit to load straight from a `bin\Debug ...\` folder and then attaching a .NET
debugger to the `Revit.exe` process, rather than anything that runs the add-in standalone.

- **`GenerateDebugAddinManifest`** (in [`GMSRevitAddin.csproj`](../../../GMSRevitAddin/GMSRevitAddin.csproj),
  Debug configs only, **and only when `-p:DeployDebugManifest=true` is passed** — see below) writes a
  **second, separate** add-in manifest — its own `AddInId`/`Name`, from
  [`GMSRevitAddin.Debug.addin.template`](../../../GMSRevitAddin/GMSRevitAddin.Debug.addin.template) — into
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
    [`.vscode/tasks.json`](../../../.vscode/tasks.json) (used only as the `preLaunchTask` for the "Launch Revit
    20##" configs below) pass the flag, so **F5 there just works**, same as before this flag existed.
  - **Visual Studio** F5 (via `Properties/launchSettings.json`) builds through VS's own build system,
    which has no equivalent way to pass an extra flag. Before your first F5 session for a given Revit
    version, run one manual build with the flag, e.g.
    `dotnet build "GMS Revit Addin.sln" -c "Debug R25" -p:DeployDebugManifest=true` (R24/R27: build the
    `.csproj` directly instead, same as any other build — see "Build & run" in root CLAUDE.md). After that, F5
    keeps working normally until the DLL path changes (e.g. a different Revit version).
- **To debug:** build the `Debug R##` config for the Revit version you want, then either
  (a) launch that Revit version normally and use **Debug > Attach to Process > `Revit.exe`** (pick
  the **.NET** code type for R25/R26/R27, or **.NET Framework** for R24) — simplest, but misses
  anything in `OnStartup` since it already ran before you attach; or
  (b) use the matching profile in [`Properties/launchSettings.json`](../../../GMSRevitAddin/Properties/launchSettings.json)
  (`"Revit 2024"`/`"2025"`/`"2026"`/`"2027"`, each an `Executable` launch pointing at that version's
  `Revit.exe`) so **F5 starts Revit with the debugger already attached** — this one hits
  `OnStartup` breakpoints too.
- **VS Code equivalent:** [`.vscode/launch.json`](../../../.vscode/launch.json) mirrors the same two modes —
  a generic `"Attach to Revit"` config (`processId": "${command:pickProcess}"`) and one `"Launch
  Revit 20##"` config per version, each with a `preLaunchTask` (in
  [`.vscode/tasks.json`](../../../.vscode/tasks.json)) that builds the matching `Debug R##` config first so
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
