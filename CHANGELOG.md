# Changelog

All notable changes to URflow will be documented in this file.

## [Unreleased]

### v1.0.4 development — not released

- 2026-10-29 — Implement Keyframe Move in the development worktree: align selected position/scale curves at their first or last key to the static layout value, using translation or proportional scaling; support Undo/Redo and reject invalid zero baselines.
- 2026-10-29 — Isolate the tool host, panel, curve math and Unity adapter; retain the independent branch/worktree and staged validation workflow.
- 2026-10-30 — Remove the unused plugin field that becomes CS0414 under warnings-as-errors. Strict compiler checks pass; the user confirmed normal project startup.
- 2026-10-30 — Integrate the approved visuals into the v1.0.4 development package: preserve the original logo/icons; use a compact dark layout, a 260 px graph that adapts to short windows, a saturated green/cyan/blue 1.8 px curve with a soft halo, same-row XY fields and the four-value paste field, amber preset underlines and a 15 px bold white APPLY label.
- 2026-10-30 — Use an 84 px tool strip with one 44×44 px Keyframe Move icon and a bottom panel with a preferred height of 420 px. The panel opens in 400 ms; one edge sweep lasts 1900 ms. Overflow supports mouse drag and wheel scrolling; no timing parameters or animation-settings entry are exposed.
- 2026-10-30 — Require a dated change record for every completed feature, fix, visual, wording, documentation or workflow stage; synchronize the tutorial's final update-history section, Markdown, Word and generator.
- Validation — Strict compilation of 15 source files passes with warning level 4 and warnings as errors, without diagnostics. Unity 2022.3.56f1 confirms the staged package path; 19 math checks, 19 service checks and 27 native UI event checks pass. The UI run uses 336 SendEvent calls at actual 320×820, 320×560 and 480×720 sizes under D3D11, without GUI/GUILayout errors. Pixel appearance checks were not executed because the screenshot API was unavailable and desktop helpers failed to start in the sandbox. Strict compilation passed again after the amber-underline adjustment.
- References — Existing development commits include 9f560f8 (curve math), 1e7c2da (Unity service), ef39a3c (tool panel), 7b74d54 (strict compilation) and f8cd7eb (user startup confirmation). Visual integration is saved as 32a6bd6 on feature/v1.0.4 and synchronized to the D: development package; all 15 source hashes match the final strict-compile inputs. Documentation and workflow records are saved with the accompanying documentation commit; see Git history.
- Pending — Complete visual acceptance, production Unity refresh and production-scene trial before release. No production project file was manually changed; its latest log has not yet confirmed a reload. Version fields remain 1.0.3; target v1.0.4 is not released and no release archive or tag has been generated.

#### 2026-10-30 glow and panel feedback fixes

- Respond to the user's production screenshot showing weak curve glow, a panel transition that differs from the approved preview and clipped Refresh/Apply Move controls.
- Keep the saturated 1.8 px curve core and add Gaussian soft tails. Match the approved 24 px panel rise over 400 ms, a single 105-degree diagonal edge sweep over 1900 ms and a dim static border; timing options remain internal.
- Fix the panel header/footer in the visible sheet and scroll only its body. Dim the main content while keeping the logo and tool strip at normal brightness; background input remains isolated.
- Preserve the previous 19 math, 19 service and 27 native UI checks as their original validation stage. They did not cover footer containment or appearance pixels; the new feedback triggers explicit geometry, transition timing and offscreen rendering checks.
- Validation — Strict compilation of 15 source files passes with warning level 4 and warnings as errors, without diagnostics. Unity 2022.3.56f1 passes 19 mathematical checks, 19 current native service checks, 42 glow/footer/actual-animation checks and 30 established native UI checks: 110 checks in total. Production RenderCurve offscreen pixels pass: mean maximum RGB increase at 8 px is 0.051551 and at 12 px is 0.009266; core saturation is 0.881361. The production PNG was inspected at original resolution. Full native-window pixel capture was not performed.
- Saved as 01c1dba40a9d6aa6b6f57b5e6a344ab793f9be2b on feature/v1.0.4 and synchronized to the D: development package; all 15 source hashes match the strict compilation inputs. The user's screenshots confirm the previous new UI was shown in the production editor; the current fix still needs refresh and user trial. No G: project file was manually changed. Documentation is saved with its accompanying commit; see Git history. Target v1.0.4 remains unreleased and version fields remain 1.0.3.

#### 2026-10-31 current-clip batch tail alignment

- Add a Batch Scan mode inside Keyframe Move while retaining the Selected first/last alignment workflow. Scan every supported position/scale curve in the current Animation Window clip, without requiring property or key selection; compare each curve's last key against the scene instance's static layout value.
- Group candidates by object path and show the tail value, static value and delta, with search, differences-only filtering, group selection, selection of visible eligible differences and clear-all. Nothing is selected by default; filter-hidden selections remain included in the footer total, and rescanning clears selection. A numerical difference does not imply an animation is wrong.
- Apply only explicitly checked eligible curves with translation or proportional scaling for both position and scale, using one atomic Undo group. Preserve unselected curves and object-reference tracks, recheck stale context/curves/static values before writing, and retain invalid rows with their reasons without blocking eligible checked rows.
- Validation — Strict compilation of all 17 Editor sources passes with warning level 4 and warnings as errors, without diagnostics. Isolated Unity 2022.3.56f1 passes 19 math, 30 batch-service, 19 original selected-service, 30 native UI regression, 109 batch UI (60 native event and 49 reflection-state), and 9 native checkbox-to-Apply/Undo end-to-end checks: 216 checks in total. Full native-window pixel capture was not performed; production-scene appearance and interaction still need user trial after refreshing the development package. No G: project code, assets, configuration or cache was manually changed.
- Target v1.0.4 remains unreleased and version fields stay at 1.0.3. The batch feature is saved as 9389b88bb90a0a0528bd8fed15529db2d6a03de8 on feature/v1.0.4 and synchronized to the D: development package; compiled and installed source hashes match and original icons, logo and resource GUIDs are preserved. The tutorial and update history are saved with the accompanying documentation commit; see Git history. No stable-branch merge, push, tag or release was performed.

## [1.0.3] - 2026-09-03

> Code changes in this release landed between 2026-04-16 and 2026-04-22 but were
> never versioned. Released as 1.0.3 to give them a stable tag.

### Fixed
- **First-time Apply not working** — Tangent mode is now set to Broken/Free *before* writing weighted tangent data, preventing Unity from resetting `weightedMode`.
- **CurveGuard fighting manual edits** — CurveGuard now only restores curves when `weightedMode` is actually reset, so curves can be edited by hand in Curves view without being snapped back.
- **Icons not loading when installed as a UPM package** — Corrected asset paths for package-relative loading.
- **Logo aspect ratio distortion** — Fixed height plus `nPOTScale=None` on the texture import settings.
- **Version number shown in the UI was stuck at `1.0.1`** — The `VERSION` constant in `URflowWindow.cs` was never bumped, so the header, footer and About dialog all displayed the wrong version. Now sourced consistently with `package.json` / `version.txt`.

### Added
- `.meta` files committed for all assets — GUIDs are now stable across installs and upgrades, so references survive updating the package.
- **Apply auto-sets Broken + Free tangent mode** on affected keyframes, preventing Unity from auto-smoothing them on the next drag.
- **Updates entry in Settings** — Opens the Package Manager window focused on `com.liuyiran.urflow`.

### Changed
- **New logo** — Minimal curve design with gradient fill; asset size reduced 142 KB → 106 KB.
- **Removed automatic update checking.** The original implementation polled GitHub (API, then raw `version.txt`) and required disabling SSL certificate validation to get through a corporate firewall. That was fragile and unsafe, and the target Unity editors have no outbound network access anyway. The Updates entry now simply opens Package Manager.

### Known issues
- `Editor/Presets.meta` is an orphan `.meta` (git cannot track the empty folder it describes). Unity may log a harmless warning on import.
- `DrawRedDot` / the `showBadge` parameter on `DrawMenuCard` are leftovers from the removed update-check feature. No call site passes `showBadge: true`, so the code is unreachable. Left in place to keep this release limited to versioning.

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
