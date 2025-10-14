## [Unreleased]

### Added
- Comprehensive design system (themes, tokens, component dictionaries) with Inter typography.
- Adaptive navigation shell with Windows navigation rail and Android tab bar experiences.
- Reusable UI components: top app bar, list item template, and empty state.
- Responsive layouts for dashboard, client list, and sales entry pages using breakpoint-driven visual states.
- Launch profiles for Windows and Android along with multi-targeting support.

### Changed
- Updated `App.xaml` resources to consume the new design system and component styles.
- Refreshed key MAUI pages to leverage the adaptive layouts and new visual language.
- Expanded `MauiProgram` to register fonts and shell dependencies.
- Reworked README with updated run books and emulator guidance.


### Polished
- Centralised navigation via NavigationHelper to eliminate Application.Current.MainPage usage.
- Rebuilt shared controls (TopAppBar, ListItemView, EmptyStateView) with self-binding and design tokens.
- Refined onboarding and empty-state pages (user select, unregistered clients) with readable copy and consistent spacing.
- Suppressed MAUI XAML hints at project level so Windows builds are warning-free.

### Fixed
- Cleaned up legacy styling artifacts and ensured consistent semantics across refreshed pages.
- Resolved startup crashes by registering Inter fonts via MAUI aliases and removing path-based references.
- Persisted auth tokens and introduced an HTTP message handler that adds the bearer token to every API request.
- Hardened category loading to log HTTP 500 responses and surface a retryable empty state in the UI.


