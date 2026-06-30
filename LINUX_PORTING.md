# Linux Compatibility — Implementation Plan

> **Status legend:** ✅ Done · 🔄 In progress · ⬜ Not started  
> This file is updated as work is completed. All items start as ⬜.

---

## Background & Scope

Simple VS Manager is currently a Windows-only WPF application targeting `net8.0-windows`.
The goal is to make it run natively on **Linux** (`linux-x64` only).
macOS is explicitly **out of scope**.

The biggest blocker is the UI framework: **WPF does not run on Linux**.
The plan adopts **Avalonia UI** as the replacement — it provides near-identical XAML syntax,
works with `CommunityToolkit.Mvvm`, and has first-class Linux support.

The Windows build must continue to work unchanged throughout the migration.

---

## Summary of Windows-Specific Blockers

| Area | Detail | Files |
|------|---------|-------|
| UI Framework | WPF (`UseWPF=true`, `net8.0-windows`) | All 34 XAML files + code-behind |
| WPF theme library | `ModernWpfUI` (WPF-only) | `VintageStoryModManager.csproj` |
| P/Invoke | `user32.dll` — `SetForegroundWindow`, `ShowWindow` | `App.xaml.cs` |
| File dialogs | `Microsoft.Win32.OpenFileDialog/SaveFileDialog` | `MainWindow.xaml.cs`, `ModConfigEditorWindow.xaml.cs` |
| Folder picker | `System.Windows.Forms.FolderBrowserDialog` | `MainWindow.xaml.cs` (×3) |
| Recycle Bin | `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(…, SendToRecycleBin)` | `MainWindow.xaml.cs` |
| Clipboard | `System.Windows.Forms.Clipboard` | `MainWindow.xaml.cs`, `UpdateModsDialog.xaml.cs` |
| Screen info | `System.Windows.Forms.Screen.AllScreens` | `MainWindow.xaml.cs` |
| InputBox | `Microsoft.VisualBasic.Interaction.InputBox` | `MainWindow.xaml.cs` |
| Color picker | `System.Windows.Forms.ColorDialog` | `ThemePaletteEditorDialog.xaml.cs` |
| Shell/explorer | `explorer.exe` hardcoded in `OpenFolderWithShell` | `MainWindow.xaml.cs` |
| Windows paths | `C:\\Games`, `D:\\Games`, `C:\\Program Files` hardcoded | `DataDirectoryLocator.cs`, `GameDirectoryLocator.cs` |
| Application manifest | `app.manifest` (Windows-only XML) | `VintageStoryModManager.csproj` |

---

## Phase 1 — Project Configuration ✅

Goal: Make the project buildable for both `linux-x64` and `win-x64` without breaking either.

- ✅ **1.1** Change the primary `TargetFramework` from `net8.0-windows` to `net8.0`; keep a conditional
  `net8.0-windows` TFM for WPF-specific code until the Avalonia migration is complete.
- ✅ **1.2** Add `linux-x64` to the list of recognised `RuntimeIdentifier` values.
- ✅ **1.3** Move `UseWPF`, `UseWindowsForms`, `EnableWindowsTargeting`, and `ApplicationManifest`
  into a `Condition="'$(OS)'=='Windows_NT'"` (or `IsWindows`) property group so they are
  only set on Windows builds.
- ✅ **1.4** Add Avalonia NuGet packages:
  - `Avalonia` (LTS line)
  - `Avalonia.Desktop`
  - `FluentAvalonia`
  - `Avalonia.ReactiveUI` or keep `CommunityToolkit.Mvvm` (Avalonia supports both)
- ✅ **1.5** Keep WPF/WinForms packages conditional on Windows builds.
- ✅ **1.6** Verify `dotnet restore` succeeds on Linux with `--runtime linux-x64`.

---

## Phase 2 — Cross-Platform Backend Fixes 🔄

These are pure C# changes with no UI involvement. They can be done before or in parallel with the
Avalonia migration.

### 2.1 — Data & Game Directory Detection ⬜

Vintage Story on Linux stores data in `~/.config/VintagestoryData` by default
and is typically installed to a user-chosen directory (often `~/games/vintagestory` or via Steam).

- ✅ **2.1.1** Add Linux candidates to `DataDirectoryLocator.EnumerateCandidates()`:
  - `~/.config/VintagestoryData`
  - `~/.local/share/VintagestoryData`
  - `XDG_CONFIG_HOME/VintagestoryData` (fallback to `~/.config`)
- ✅ **2.1.2** Remove or guard the Windows-hardcoded paths (`C:\\Games` etc.) in
  `DataDirectoryLocator.EnumerateAdditionalWindowsRoots()` so they are only included on Windows.
- ✅ **2.1.3** Add Linux candidates to `GameDirectoryLocator.EnumerateDefaultInstallPaths()`:
  - `~/.local/share/vintagestory`
  - `~/games/vintagestory`
  - `/opt/vintagestory`
  - XDG data dirs (`~/.local/share/Steam/steamapps/common/Vintage Story`)
- ✅ **2.1.4** Guard the Windows-hardcoded paths in `GameDirectoryLocator.EnumerateAdditionalWindowsRoots()`
  with `OperatingSystem.IsWindows()`.
- ✅ **2.1.5** The `ExecutableCandidates` array in `GameDirectoryLocator` already includes `Vintagestory`
  (no `.exe`) — verify this is sufficient for Linux and add a check for the shell wrapper script
  (`vintagestory.sh`) if needed.

### 2.2 — Recycle Bin → Permanent Delete with Confirmation ⬜

Per the project brief, "Confirmation-then-permanent-delete is acceptable" on Linux.

- ✅ **2.2.1** Extract the two deletion call-sites in `MainWindow.xaml.cs` (lines ~7626 and ~7641)
  into a shared helper method `DeleteFileCrossPlatform(string path)`.
- ✅ **2.2.2** On Windows the helper continues to use `FileSystem.DeleteFile(…, SendToRecycleBin)`.
- ✅ **2.2.3** On Linux the helper shows a confirmation dialog ("This will permanently delete the file.
  Continue?") and, if confirmed, calls `File.Delete(path)` or `Directory.Delete(path, recursive: true)`.
- ⬜ **2.2.4** Confirmation dialog must be an Avalonia-compatible dialog (not WPF `MessageBox`).

### 2.3 — Single-Instance Mutex ⬜

`System.Threading.Mutex` works cross-platform on Linux with no changes needed.
`WindowActivator` (user32.dll P/Invoke) is the only part that needs attention.

- ✅ **2.3.1** Guard the `WindowActivator` inner class in `App.xaml.cs` with
  `[SupportedOSPlatform("windows")]` and wrap every call site with `if (OperatingSystem.IsWindows())`.
- ✅ **2.3.2** On Linux, when a second instance is detected, simply show a message and exit
  (bringing the existing window to front is not reliably possible without a display-server-specific
  solution; this is acceptable for an initial release).

### 2.4 — Process / Shell Launching ⬜

- ⬜ **2.4.1** `OpenFolderWithShell` already has the Windows branch correct. Verify the Linux fallback
  (`UseShellExecute = true` with a directory path) works via `xdg-open` on common distros.
- ⬜ **2.4.2** Audit all other `Process.Start` call-sites for Windows-only assumptions
  (e.g. launching the game with `.exe`).
- ⬜ **2.4.3** On Linux, game launch should call the `Vintagestory` binary (or wrapper script) directly.

### 2.5 — Configuration & App Data Paths ⬜

`DevConfig.GetManagerDirectory()` uses `Environment.SpecialFolder.LocalApplicationData`.
On Linux this maps to `~/.local/share` via .NET's cross-platform implementation — which is correct.
No changes are needed here, but verify:

- ⬜ **2.5.1** Confirm `Environment.SpecialFolder.LocalApplicationData` returns `~/.local/share` on Linux.
- ⬜ **2.5.2** Confirm `Environment.SpecialFolder.ApplicationData` returns `~/.config` on Linux.
- ⬜ **2.5.3** Ensure config/cache files written under these directories use `/` as path separator
  (already guaranteed by `Path.Combine`).

---

## Phase 3 — UI Migration: WPF → Avalonia ⬜

This is the largest phase. All 34 XAML files and their code-behind must be migrated.

### Migration strategy

Avalonia XAML syntax is very close to WPF but with these key differences:

| WPF | Avalonia |
|-----|----------|
| `System.Windows.*` namespaces | `Avalonia.*` namespaces |
| `Window`, `UserControl` | Same names, different namespace |
| `ResourceDictionary`, `Style`, `ControlTemplate` | Same, with minor differences |
| `Binding`, `ICommand` | Same |
| `DependencyProperty` | `StyledProperty` / `DirectProperty` |
| `Grid`, `StackPanel`, `ScrollViewer` | Same names |
| `TextBlock`, `Button`, `ComboBox` | Same names |
| `ModernWpfUI` controls | Avalonia built-in or FluentAvalonia equivalents |
| `Dispatcher` | `Dispatcher` (Avalonia has its own) |
| `BitmapImage` | `Bitmap` |
| `MessageBox` (system) | Custom dialog (Avalonia has no system MessageBox) |

### 3.1 — Application Bootstrap 🔄

- 🔄 **3.1.1** Create `Program.cs` as the Avalonia entry point:
  ```csharp
  AppBuilder.Configure<App>()
      .UsePlatformDetect()
      .WithInterFont()
      .LogToTrace()
      .StartWithClassicDesktopLifetime(args);
  ```
- ⬜ **3.1.2** Rewrite `App.xaml` / `App.xaml.cs`:
  - Remove WPF `Application` base, use Avalonia `Application`.
  - Move theme loading to Avalonia `RequestedThemeVariant` / `Styles`.
  - Remove `DispatcherUnhandledException` (use Avalonia's equivalent).
  - Guard `WindowActivator` (user32 P/Invoke) with `OperatingSystem.IsWindows()`.
- ⬜ **3.1.3** Replace `ModernWpfUI` theme with `FluentAvalonia`.
  Reproduce the existing `DarkVsTheme.xaml` colour palette as an Avalonia `ResourceDictionary`.

### 3.2 — Main Window ⬜

`MainWindow.xaml` / `MainWindow.xaml.cs` (~9,000 lines) is the most complex file.

- ⬜ **3.2.1** Migrate `MainWindow.xaml` to Avalonia AXAML (namespace changes, control renames).
- ⬜ **3.2.2** Replace `Microsoft.Win32.OpenFileDialog` with Avalonia `OpenFileDialog` (async API).
- ⬜ **3.2.3** Replace `WinForms.FolderBrowserDialog` (×3) with Avalonia `OpenFolderDialog`.
- ⬜ **3.2.4** Replace `WinForms.Clipboard.SetText/SetDataObject` with Avalonia `TopLevel.Clipboard`.
- ⬜ **3.2.5** Replace `WinForms.Screen.AllScreens` (used for window positioning) with
  Avalonia `Screens.All` from the owning `Window`.
- ⬜ **3.2.6** Replace `Microsoft.VisualBasic.Interaction.InputBox` with a custom Avalonia
  inline text-input dialog.
- ⬜ **3.2.7** Apply the `DeleteFileCrossPlatform` helper from Phase 2.2 to the deletion call-sites.
- ⬜ **3.2.8** Update `OpenFolderWithShell` — already partially cross-platform; ensure it compiles
  without `explorer.exe` on Linux paths.

### 3.3 — All Other Views ⬜

Migrate each view in order of complexity (simplest first):

- ⬜ **3.3.1** `MultiSelectDropdown.xaml` / `.cs`
- ⬜ **3.3.2** `BulkUpdateChangelogWindow.xaml` / `.cs`
- ⬜ **3.3.3** `BulkCompatibilityPromptWindow.xaml` / `.cs`
- ⬜ **3.3.4** `ModConfigEditorWindow.xaml` / `.cs` (contains `Microsoft.Win32.OpenFileDialog`)
- ⬜ **3.3.5** `ModBrowserView.xaml` / `.cs`
- ⬜ **3.3.6** Controls: `InstalledModsView.xaml`, `NavigationHeader.xaml`

### 3.4 — All Dialogs ⬜

- ⬜ **3.4.1** `MessageDialogWindow.xaml` / `.cs` — replace WPF `MessageBox`-style dialog with Avalonia
- ⬜ **3.4.2** `HelpDialogWindow.xaml` / `.cs`
- ⬜ **3.4.3** `GuideDialogWindow.xaml` / `.cs`
- ⬜ **3.4.4** `ThemePaletteEditorDialog.xaml` / `.cs` — replace `System.Windows.Forms.ColorDialog`
  with a custom Avalonia colour picker (e.g. `ColorPicker` from `Avalonia.Controls.ColorPicker`)
- ⬜ **3.4.5** `UpdateModsDialog.xaml` / `.cs` — replace `WinForms.Clipboard`
- ⬜ **3.4.6** `LocalModlistEditDialog.xaml` / `.cs`
- ⬜ **3.4.7** `DeleteGameProfilesDialog.xaml` / `.cs`
- ⬜ **3.4.8** `CloudSlotSelectionDialog.xaml` / `.cs`
- ⬜ **3.4.9** `CloudModlistRenameDialog.xaml` / `.cs`
- ⬜ **3.4.10** `CloudModlistManagementDialog.xaml` / `.cs`
- ⬜ **3.4.11** `CloudModlistDetailsDialog.xaml` / `.cs`
- ⬜ **3.4.12** `CompatibilityResultsDialog.xaml` / `.cs`
- ⬜ **3.4.13** `GameProfileDialog.xaml` / `.cs`
- ⬜ **3.4.14** `ChangeManagerFolderDialog.xaml` / `.cs`
- ⬜ **3.4.15** `RestoreBackupDialog.xaml` / `.cs`
- ⬜ **3.4.16** `SaveInstalledModsDialog.xaml` / `.cs`
- ⬜ **3.4.17** `VintageStoryVersionSelectionDialog.xaml` / `.cs`
- ⬜ **3.4.18** `ModVoteDialog.xaml` / `.cs`
- ⬜ **3.4.19** `ModVoteReasonDialog.xaml` / `.cs`
- ⬜ **3.4.20** `ModUsageNoIssuesDialog.xaml` / `.cs`
- ⬜ **3.4.21** `ExperimentalModDebugDialog.xaml` / `.cs`
- ⬜ **3.4.22** `ThemeNameDialog.xaml` / `.cs`

### 3.5 — Resources & Theming ⬜

- ⬜ **3.5.1** Migrate `Resources/Themes/DarkVsTheme.xaml` colour tokens to Avalonia `ResourceDictionary`.
- ⬜ **3.5.2** Migrate `Resources/MainWindowStyles.xaml` — control templates, styles.
- ⬜ **3.5.3** Migrate `Resources/BoxIconsResources.xaml` — icon paths/geometry.
- ⬜ **3.5.4** Verify all image resources (`warning.png`, `error.png`, `mod-default.png`,
  `manager-icon.png`, `Discord.png`, `SVSM.ico`) are correctly embedded and loaded by Avalonia.

### 3.6 — Converters & Helpers ⬜

- ⬜ **3.6.1** Migrate all value converters in `Converters/` to implement `IValueConverter`
  from `Avalonia.Data.Converters`.
- ⬜ **3.6.2** `OverlappingTagPanel.cs` (custom `Panel`) — migrate from WPF `Panel` to Avalonia `Panel`.
- ⬜ **3.6.3** `HoverEffectHelper.cs`, `BindingProxy.cs` — update namespace references.

---

## Phase 4 — Windows Compatibility Guard ⬜

Ensure the Windows build still compiles and behaves as before.

- ⬜ **4.1** Run full build and test matrix on Windows (`win-x64`) after all phases.
- ⬜ **4.2** Keep Windows-only code behind `OperatingSystem.IsWindows()` guards and/or
  `[SupportedOSPlatform("windows")]` attributes rather than removing it.
- ⬜ **4.3** Verify WPF-specific NuGet packages are only restored on Windows builds
  (conditional `ItemGroup` in `.csproj`).

---

## Phase 5 — Build Packaging for Linux ⬜

All packaging scripts live in a new `packaging/linux/` directory.

### 5.1 — Self-Contained tar.gz ⬜

Simplest format; required as input for all others.

- ⬜ **5.1.1** Add a `publish:linux` target to `build.sh` that runs:
  ```sh
  dotnet publish VintageStoryModManager/VintageStoryModManager.csproj \
      --configuration Release \
      --runtime linux-x64 \
      --self-contained true \
      -p:PublishSingleFile=true \
      -p:IncludeNativeLibrariesForSelfExtract=true \
      --output dist/linux-x64
  ```
  (target `linux-x64` only)
- ⬜ **5.1.2** Create `packaging/linux/make-tarball.sh` that:
  - Copies the published output plus a wrapper shell script and `.desktop` file.
  - Produces `SimpleVSManager-{version}-linux-x64.tar.gz`.
- ⬜ **5.1.3** Write a launcher wrapper `simplevsmanager` shell script that sets `LD_LIBRARY_PATH`
  if needed and then exec's the binary.

### 5.2 — AppImage ⬜

AppImage is a single-file, distribution-agnostic executable.

- ⬜ **5.2.1** Create `packaging/linux/AppDir/` structure:
  ```
  AppDir/
    AppRun                  ← entry-point script
    simplevsmanager.desktop ← desktop integration file
    simplevsmanager.png     ← 256×256 icon
    usr/
      bin/
        Simple VS Manager   ← copied from publish output
      lib/                  ← any extra native libs
  ```
- ⬜ **5.2.2** Write `packaging/linux/make-appimage.sh` that:
  - Runs `dotnet publish` to produce the binary.
  - Populates the `AppDir`.
  - Downloads or uses the system `appimagetool` to produce `SimpleVSManager-x86_64.AppImage`.
- ⬜ **5.2.3** Create `simplevsmanager.desktop`:
  ```ini
  [Desktop Entry]
  Type=Application
  Name=Simple VS Manager
  Comment=Portable, user-friendly Vintage Story mod manager
  Exec=simplevsmanager
  Icon=simplevsmanager
  Categories=Game;Utility;
  ```

### 5.3 — Flatpak ⬜

Flatpak provides a sandboxed distribution format supported by most major distros.

- ⬜ **5.3.1** Create `packaging/linux/flatpak/at.vintagestory.SimpleVSManager.yml` manifest:
  - Runtime: `org.gnome.Platform` (or KDE/freedesktop) — whichever provides GTK/X11/Wayland.
  - SDK: matching `org.gnome.Sdk`.
  - Build-system: `simple` (copy pre-built binaries from `dotnet publish`).
  - Finish-args: network, filesystem (home for mod/data directories), X11/Wayland.
- ⬜ **5.3.2** Document the required `flatpak-builder` build command.
- ⬜ **5.3.3** Decide on sandbox permissions (network for mod DB, `--filesystem=home` for mod folders).
- ⬜ **5.3.4** Create Flatpak `metainfo.xml` (AppStream metadata) if distribution outside internal testing is needed.

### 5.4 — .deb (Debian/Ubuntu) ⬜

- ⬜ **5.4.1** Create `packaging/linux/deb/DEBIAN/control`:
  ```
  Package: simplevsmanager
  Version: 2.1.0
  Architecture: amd64
  Maintainer: …
  Depends: libgtk-3-0 | libgtk-4-1
  Description: Portable, user-friendly Vintage Story mod manager
  ```
- ⬜ **5.4.2** Create `packaging/linux/make-deb.sh` that:
  - Publishes the app.
  - Assembles a `deb` staging tree (`usr/lib/simplevsmanager/`, `usr/bin/`, `usr/share/applications/`).
  - Runs `dpkg-deb --build` to produce `simplevsmanager_2.1.0_amd64.deb`.
- ⬜ **5.4.3** Include `.desktop` file and icon in `usr/share/applications/` and `usr/share/icons/`.
- ⬜ **5.4.4** Add a post-install script to update the icon/desktop cache.

### 5.5 — .rpm (Fedora/openSUSE/RHEL) ⬜

- ⬜ **5.5.1** Create `packaging/linux/rpm/simplevsmanager.spec` RPM spec file.
- ⬜ **5.5.2** Create `packaging/linux/make-rpm.sh` that calls `rpmbuild`.
- ⬜ **5.5.3** Ensure `Requires:` lists the GTK runtime dependency.

### 5.6 — CI Integration ⬜

- ⬜ **5.6.1** Add a GitHub Actions workflow `.github/workflows/build-linux.yml` that:
  - Builds and tests on an `ubuntu-latest` runner.
  - Produces `tar.gz` and `AppImage` artifacts on every push to `master`.
  - Produces `.deb` and `.rpm` on tagged releases.
- ⬜ **5.6.2** The existing Windows build workflow must remain unaffected.

---

## Phase 6 — Testing ⬜

- ⬜ **6.1** Verify application starts on Ubuntu 22.04 LTS (the primary test target).
- ⬜ **6.2** Verify application starts on Fedora 40.
- ⬜ **6.3** Verify Vintage Story data directory is auto-detected correctly.
- ⬜ **6.4** Verify Vintage Story game directory is auto-detected correctly (or fallback prompt works).
- ⬜ **6.5** Verify mod installation, enable/disable, and deletion work.
- ⬜ **6.6** Verify the online mod database is reachable.
- ⬜ **6.7** Verify file/folder picker dialogs open and return correct paths.
- ⬜ **6.8** Verify clipboard operations work (copy server command).
- ⬜ **6.9** Verify single-instance detection — second launch shows warning and exits.
- ⬜ **6.10** Verify theme switching works.
- ⬜ **6.11** Install and run each package format (tar.gz, AppImage, .deb, .rpm, Flatpak) on a clean VM.
- ⬜ **6.12** Smoke-test the Windows build to confirm it is unaffected.

---

## Decisions Applied

1. **Avalonia version**: Use **LTS**.
2. **Theme library**: Use **FluentAvalonia**.
3. **ARM64**: **Out of scope**; target `linux-x64` only.
4. **Flatpak sandbox**: Broad permissions are acceptable for test builds.
5. **Wayland-specific work**: None planned.
6. **Flathub submission**: Out of scope for now.

---

## Dependency Reference

| Package | Status | Notes |
|---------|--------|-------|
| `Avalonia` | ⬜ To add | Core framework |
| `Avalonia.Desktop` | ⬜ To add | Provides desktop integration |
| `Avalonia.Themes.Fluent` | ⬜ Optional | Keep only if required by selected FluentAvalonia setup |
| `FluentAvalonia` | ✅ Selected | Preferred theme/control stack |
| `Avalonia.Controls.ColorPicker` | ⬜ To evaluate | Replaces WinForms ColorDialog |
| `CommunityToolkit.Mvvm` | ✅ Keep | Already cross-platform |
| `HtmlAgilityPack` | ✅ Keep | Already cross-platform |
| `QuestPDF` | ✅ Keep | Already cross-platform |
| `UglyToad.PdfPig` | ✅ Keep | Already cross-platform |
| `YamlDotNet` | ✅ Keep | Already cross-platform |
| `ModernWpfUI` | ❌ Remove | WPF-only |

---

*Last updated: 2026-06-30*
