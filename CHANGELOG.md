# Changelog

All notable changes to URflow will be documented in this file.

## [1.0.2] - 2026-04-15

### Fixed
- **Curve Guard** — Fixed bezier curves being reset when moving keyframes in Dopesheet/Curves mode. URflow now automatically monitors and restores weighted tangent data after Unity resets it.

### Added
- `URflowCurveGuard.cs` — Auto-restore curves via `[InitializeOnLoad]` + `EditorApplication.update`

### Changed
- `URflowApplyHelper.cs` — Registers curve pairs with CurveGuard after successful apply

## [1.0.1] - 2026-04-14

### Added
- SAVE button — Save current curve to My Presets with naming dialog
- MY PRESETS action bar — Import/Export/Delete buttons with custom PNG icons
- Control points can exceed coordinate frame (Overshoot/Anticipation)
- Shift + drag horizontal snapping (P1→Y=0, P2→Y=1)
- Grid/List view toggle for presets
- Settings page with card-style layout
- Custom icon system (Read/Save/import/export/delete/Setting PNG icons)
- Chinese/English language switching
- All button tooltips (CN/EN)

### Changed
- Flow-style dark theme with gradient curve (#5EF0B0 → #0071FF)
- Gradient APPLY button matching curve colors
- Thicker curve lines (~3px) and control bars (~2px)
- Tab highlight color #FFB826
- X1/Y1/X2/Y2 changed to FloatField (replacing Slider)
- All English text uppercase + bold
- LOGO replaced with image asset
- Version bumped to 1.0.1

### Fixed
- GPU memory leak — Static cached Texture2D, fixed D3D11 swapchain error
- Thumbnail curve overflow — Switched from GL to DrawRect
- Steep curve discontinuity — DrawCurveInRect interpolation fill
- Settings bar obscured by ScrollView — Normal layout flow + FlexibleSpace
- Drag intercepted by UI — GUIUtility.hotControl lock
- Mathf.Lerp clamp — Changed to LerpUnclamped for out-of-range values

## [1.0.0] - 2026-04-14

### Added
- Initial release
- Visual bezier curve editor with draggable control points
- cubic-bezier() parameter input
- 60+ built-in easing presets (Penner/CSS/UI Motion)
- One-click Read/Apply to Animation Window keyframes
- Favorites system
- Custom preset save/load
- Import/Export presets as JSON
