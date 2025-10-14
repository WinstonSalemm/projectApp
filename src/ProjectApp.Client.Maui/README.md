# ProjectApp.Client.Maui

Modern adaptive client for ProjectApp, built with .NET MAUI and tuned for Windows desktop + Android tablets.

## Highlights
- Unified design system with light/dark themes, Inter font family, and tokenised spacing/radius scales.
- Adaptive navigation:
  - Windows desktop: left navigation rail + content pane.
  - Android tablet (Samsung Galaxy Tab class): bottom tab bar + top app bar.
- Responsive pages with Compact / Medium / Expanded breakpoints and CollectionView grid layouts.
- Reusable UI components (`TopAppBar`, `ListItemView`, `EmptyStateView`) wired into the design tokens.
- Dual service routing (mock vs API) configured via DI and `appsettings.json`.

## Project Layout
| Folder | Purpose |
|--------|---------|
| `Controls/` | Reusable composite controls used across pages. |
| `Resources/Styles/` | Token dictionaries (`DesignSystem`, `ThemeLight`, `ThemeDark`, `Controls`, `Components`). |
| `ViewModels/` | MVVM view-models using CommunityToolkit.MVVM. |
| `Views/` | XAML pages updated for the adaptive design language. |
| `Services/` | DI-registered API, mock, and routed services. |

## Build & Run

### Prerequisites
- .NET 9 SDK with the MAUI workloads you plan to target:
  ```powershell
  dotnet workload install maui maui-windows maui-android
  ```
- Visual Studio 2022 17.11+ with MAUI tooling (optional but recommended).

### Windows (desktop)
```powershell
cd C:\projectApp\src
dotnet restore
dotnet build ProjectApp.Client.Maui/ProjectApp.Client.Maui.csproj -f net9.0-windows10.0.19041.0
dotnet build ProjectApp.Client.Maui/ProjectApp.Client.Maui.csproj -t:Run -f net9.0-windows10.0.19041.0
```
Or select **ProjectApp.Client.Maui (Windows)** launch profile in Visual Studio and run (F5).

### Android Tablet (Samsung Galaxy Tab 7 class)
1. Install Android SDK platform 35 and create an emulator that matches Samsung Galaxy Tab 7:
   - Device: **Pixel Tablet** (or create a custom device 2560x1600, 8вЂЇGB RAM).
   - Orientation: Landscape by default.
   - Target: Android 15 (API 35).
2. Ensure AVD is running.
3. Build & deploy:
   ```powershell
   cd C:\projectApp\src
   dotnet build ProjectApp.Client.Maui/ProjectApp.Client.Maui.csproj -f net9.0-android35.0
   dotnet build ProjectApp.Client.Maui/ProjectApp.Client.Maui.csproj -t:Run -f net9.0-android35.0
   ```
   In Visual Studio, pick **ProjectApp.Client.Maui (Android Tablet)** and press F5. The adaptive layout switches to bottom tabs automatically.

## API vs Mock Services
`appsettings.json` controls the service layer. Set `UseApi` to `true` and point `ApiBaseUrl` to the running backend (see `ProjectApp.Api` project) to consume live endpoints. Otherwise the client stays in mock mode with offline-ready data.

## Design Tokens Cheat-Sheet
- Breakpoints:
  - `Breakpoint.Compact` = 800 px
  - `Breakpoint.Medium` = 1200 px
- Typography: `Font.Size.Display`, `Font.Size.H1/H2/H3`, `Font.Size.Body`, etc.
- Spacing: `Space.0` вЂ¦ `Space.10`
- Radii: `Radius.4` вЂ¦ `Radius.24`
- Buttons: `Button.Primary`, `Button.Secondary`, `Button.Tertiary`, `Button.Destructive`
- Cards: `Card.Container`, `Chip.Standard`

## UI Polish (2025-10)
- Navigation is centralised through `NavigationHelper`, eliminating direct access to `Application.Current.MainPage`.
- All component controls (`EmptyStateView`, `TopAppBar`, `ListItemView`) now self-bind and use the design tokens consistently.
- Warning-free builds: XAML compiler hints are suppressed at the project level and Windows target builds cleanly.
- Strings across onboarding pages were refreshed (e.g., user selection, unregistered clients).
- Android targets still require the `maui-android` workload; when absent, build emits `NETSDK1140`.

## Change Log
See `CHANGELOG.md` for the complete list of redesign updates.

## Troubleshooting
- **Missing workloads**: ensure `dotnet workload list` shows `maui`, `maui-windows`, and `maui-android`.
- **Android emulator not visible**: restart the Android Emulator Manager and ensure Hyper-V / HAXM is enabled.
- **Design resources not applied**: clean the solution (`dotnet clean`) and rebuild to force MAUI resource regeneration.

