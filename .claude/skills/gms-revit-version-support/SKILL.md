---
name: gms-revit-version-support
description: Add support for a new Revit version to GMSRevitAddin's multi-version build, or troubleshoot Revit 2024 (net48 / .NET Framework 4.8)-specific build and runtime issues. Use when adding a Revit 2028+ configuration, or when a build/runtime error is specific to the R24 configuration (net48).
---

# Adding a new Revit version / Revit 2024 (net48) specifics

This covers the two "multi-version build system" tasks that don't come up every session:
extending the build matrix for a future Revit version, and the net48-only workarounds Revit 2024
needs. See the root [`CLAUDE.md`](../../../CLAUDE.md) "Multi-version build" section for how
`$(RevitVersion)` normally flows through the build.

## Adding a future Revit version

- **To add a future Revit version (e.g. 2028):** (1) add `Debug R28;Release R28` to
  `<Configurations>`; (2) add a `<PropertyGroup Condition="$(Configuration.Contains('R28'))">`
  setting `<RevitVersion>2028</RevitVersion>`; (3) if that Revit ships on a new .NET runtime, add a
  second `<PropertyGroup Condition="$(Configuration.Contains('R28'))">` block **after** the shared
  `<PropertyGroup>` (after the `<PlatformTarget>x64</PlatformTarget>` block) that overrides
  `<TargetFramework>` — placing it after the shared block is required so it wins at restore time;
  (4) confirm `Nice3point.Revit.Api.RevitAPI 2028.*` exists on NuGet; (5) add the four solution
  configs in [`GMS Revit Addin.sln`](../../../GMS%20Revit%20Addin.sln) (or let VS regenerate them).
  Everything else flows from `$(RevitVersion)`. **Important:** if a new TFM is introduced, the
  new version must be built via `.csproj` directly (not `.sln`) — see the build note in root
  CLAUDE.md.
  Also check whether Autodesk has changed the all-users manifest folder again (the 2027 change
  is already handled by the `$(RevitVersion) >= 2027` condition in `DeployAddin`; only act if
  a future version moves it somewhere else).

## Revit 2024 / net48 (the one .NET Framework version — extra care)

Revit 2025 was the first version on .NET Core; **Revit 2024 runs on .NET Framework 4.8**. The R24
config (`<TargetFramework>net48</TargetFramework>` + `<LangVersion>latest</LangVersion>`) therefore
needs several net48-only workarounds that the .NET Core versions don't. All are scoped in
[`GMSRevitAddin.csproj`](../../../GMSRevitAddin/GMSRevitAddin.csproj) by `Condition="'$(TargetFramework)' == 'net48'"`
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
- **Runtime assembly resolution** ([`GMS_tools.cs`](../../../GMSRevitAddin/GMS_tools.cs), guarded `#if REVIT2024`).
  The preserialized resources hard-depend on `System.Resources.Extensions`, deployed beside the DLL —
  but Revit loads the add-in in-process and the net48 CLR probes **Revit's install folder (appbase)**,
  not the add-in folder, with strict version binding (it requests 4.0.0.0; the shipped copy is 8.0.0.0).
  So it fails with *"Could not load file or assembly 'System.Resources.Extensions, Version=4.0.0.0…'"*.
  `OnStartup` registers `AppDomain.CurrentDomain.AssemblyResolve += ResolveFromAddinFolder` **first**,
  which `Assembly.LoadFrom`s the sibling `<simpleName>.dll` from the add-in's own directory (bypassing
  version binding). This is the **same in-process-resolution gap as the ODBC note in root CLAUDE.md's
  "External dependencies & data" section**, and the handler covers any future sibling dependency. This
  is the only `#if REVIT*` block in the codebase.
