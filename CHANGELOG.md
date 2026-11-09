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

#### 2026-11-06 keyframe scan and panel fixes

- Merge Selected and Batch Scan tabs into one checked-results workflow. Put First Frame/Last Frame and proportional adjustment above the list; the selected anchor drives both preview and apply, with Last Frame as default. Animation property selection becomes a filter; an empty selection never expands to all curves.
- Treat absolute position differences up to 0.0001 and scale differences up to 0.000001 as aligned floating-point residuals, without a magnitude-dependent relative tolerance. Write the chosen anchor key to the exact target, keep real nonzero deltas/full-precision tooltips and mark aligned rows when showing all properties. The original zero-delta screenshot cause has not been reproduced; stale curve/static-value checks remain exact.
- Close the panel with a continuous 280 ms fade and slight downward movement, retaining it until the transition finishes and reversing from the current progress when interrupted. Cancel unexecuted scan/apply callbacks on close and disable list actions during exit. Fade background dimming with panel opacity to avoid a final brightness jump. Keep the internal 400 ms opening and 1900 ms edge sweep.
- Validation — Implemented and validated in isolation: strict compilation of all 17 Editor sources passes with warning level 4 and warnings as errors, without diagnostics. Unity 2022.3.56f1 with Direct3D11 confirms the current C: staged package and passes 19 math, 19 original selected-service, 44 scan-service, 30 established main-UI (330 SendEvent calls), 14 closing-animation (5 state and 9 native-event), 252 unified-UI (156 native-event and 96 state), and 42 queued-apply/native-close cancellation (30 native-event and 12 state) checks: 420 in total. Unified UI and close cancellation cover 320x560, 320x820 and 480x720 in English/Chinese. Full-window pixel capture was not performed; production-scene appearance and closing feel remain for user trial. The original screenshot's exact values/filter state were not reproduced, so its precise cause is not claimed as proven. No G: project files were manually changed during this plugin-development stage.
- Target v1.0.4 remains unreleased and version fields stay at 1.0.3. Code is saved as e42a68b4baef1ab2ecdc46d3e5da7dd1b63f2a17 on feature/v1.0.4 and installed in the D: development package; hashes of all 7 changed source files match validated inputs and original icons/resource GUIDs are preserved. Tutorial/update records are saved with the accompanying documentation commit; see Git history. No stable-branch merge, push, tag or formal release was performed.

#### 2026-11-07 RectTransform stretched-layout alignment

- Explicitly include RectTransform Size Delta X/Y as UI Size, with the 0.0001 absolute position allowance. Keep raw Size Delta proportional adjustment distinct from scaling the final rendered width/height.
- For a stretched axis with differing animated position and size endpoints, require both same-object/same-axis differences to be explicitly checked before any write. Aligned or absent counterparts do not cause implicit changes, non-stretched axes remain independent, and no missing tracks are created.
- Reject position/size alignment with animated anchors/pivot or unreliable driven layout; snapshot the static anchor/pivot basis and validate current paired values before applying. Preserve checked-only atomic writes, curve metadata and batch Undo/Redo.
- Validation — Strict compilation of all 17 Editor sources passes at warning level 4 with warnings as errors and no diagnostics; frozen source hashes match. Isolated Unity 2022.3.56f1 with Direct3D11 confirms the current C: staged package and passes 19 math, 19 original selected-service, 44 scan-regression, 252 unified-UI (156 native-event and 96 state), 36 new RectTransform layout-service, and 56 new layout-UI (40 native-event and 16 state) checks: 426 in total. Unified UI covers 320x560, 320x820 and 480x720 in English/Chinese; new layout UI covers the first two sizes in both languages. The engine reproduces the old position-only 35/65 margins, and paired service/native-checkbox Apply restores 50/80; native Undo restores the full clip. Full-window pixel capture and production-scene trial were not performed; the original user clip's exact sampled time was not reproduced. No G: production files were manually written.
- Target v1.0.4 remains unreleased and formal version fields stay at 1.0.3. Code is saved as bacfc91507ca84c5358d91833a7bdfc2e8050e30 on feature/v1.0.4 and installed in the D: development package; all 3 changed source hashes match the tested staged inputs. Tutorial/update records are saved with the accompanying documentation commit; see Git history. Stable code was not changed, and no merge, push, tag or formal release was performed.

#### 2026-11-09 working-directory migration and confirmation policy

- Move all nine C: URFlow working roots under D:/插件/URFLOW: isolated Unity testing, plugin staging, documentation synchronization material and project-reference backups. Verify all 2,181 files by SHA-256 before removing the original directories; preserve dated reports as history.
- Update current validation entry points and package references to the D: locations. Require user confirmation before creating, moving, renaming or deleting working directories, changing file storage locations or creating a worktree; ordinary authorized edits in existing directories continue as agreed.
- Validation: the relocated Unity 2022.3.56f1 project starts successfully, PackageInfo confirms the D: staged package, 19 existing math checks pass, all 17 Editor sources compile with warnings as errors, and seven PowerShell entry points parse successfully. Editor source hashes are unchanged. The 426 feature checks from November 7 were not rerun today; no production project file was written.
- Save validation-path updates as c7f242a0823376c1b4a2de57b2ee969e8a6f8cf1 on feature/v1.0.4. Synchronize rules, guides and update records with the accompanying documentation commit. Formal version fields remain 1.0.3; target v1.0.4 is unreleased. No merge, push, tag or release.

#### 2026-11-09 default window and Keyframe Move layout

- Create a new URFlow window as a narrow floating window, preferring 350x1000 logical units and fitting it within the current desktop. Reuse an existing window without resetting manual resizing or docking; close the old instance and reopen from Window > URflow to see the new default.
- Raise the Keyframe Move sheet to a maximum of 740 logical units (about 736 at the default window height), constrained by the available window area. Keep header/footer actions visible and scroll the body only when it overflows.
- Simplify visible wording and place filters together. Show short object names with full paths in tooltips; move routine explanations, exact numbers and normal ratio factors into tooltips. Retain actionable errors, stale-result warnings, zero/negative ratios, stretched-axis pairing notices and the scope of hidden checked items.
- Keep original icons, colors, curve glow, animation durations and all scan/apply/Undo/curve algorithms unchanged.
- Validation: all 17 Editor sources compile at warning level 4 with warnings as errors, and source hashes match cb860096. A clean, user-approved v1.0.4-c1 project using the separately installed Unity 2022.3.56f1c1 confirms the D: staged package and passes 111 checks: math 19, window/sheet geometry 15, close animation 14, selected-property service 19 and scan 44. Legacy unified-page and stretched-layout UI automation each time out with zero checks because the global delayCall queue does not execute in this hidden startup; these runs are not counted as passed or treated as proof of a product defect. Native window pixel capture is unavailable, and full native interactions and production-scene appearance remain pending.
- Save the four UI source changes as cb860096ce861eab926c4273bd0156a7dbf5c96f on feature/v1.0.4; documentation is saved with its accompanying commit (see Git history). Preserve the seven unrelated pre-existing .meta edits. Formal version fields remain 1.0.3; target v1.0.4 is unreleased. No merge, push, tag or release.

#### 2026-11-09 curve endpoint halo refinement

- Reduce the halo at both the curve origin and the blue endpoint to 40% of its prior strength, smoothly returning to full strength over about 24 editor logical units along the sampled curve. Keep the central halo, opaque stroke, antialiasing fringe, gradient colors, geometry and editing/animation algorithms unchanged.
- Validation: all 17 Editor sources strictly compile with warning level 4 and warnings as errors; 21 offscreen before/after checks pass for ease, linear and steep curves. Actual endpoint halo pixels are weaker, while central halo and opaque stroke pixels and exact geometry values remain unchanged. The comparison images were inspected. These are offscreen checks, not complete native-window or production-scene acceptance; the prior 111 checks were not rerun.
- Save the single renderer-source change as f5469ae7555f932b21aa85924f35dad347f3d754 on feature/v1.0.4. Preserve previous evidence and all pre-existing .meta edits; synchronize this continuous micro-adjustment's final dual-end result to the tutorial and records. Formal version fields remain 1.0.3; target v1.0.4 is unreleased, with no merge, push, tag or release.


#### 2026-11-09 yellow control guide refinement

- Thicken both yellow guide cores to 2 editor logical units with a 0.55-unit transparent antialiasing fringe, retaining the existing gold color and opacity. Paint the curve first, then the yellow guides, then the control points.
- Validation: all 17 product sources strictly compile with warning level 4 and warnings as errors. The 14 actual Unity c1 offscreen drawing checks and one static window paint-order check pass; before/after images were inspected. Preserve curve-only pixels and both endpoint halos. Full native-window and animation-editing interaction acceptance remains pending; the prior 111 and 21 checks were not rerun.
- Save the renderer and window changes as 2227b1e976030e1a6991a262c51ecb1573c9cb3b on feature/v1.0.4. Curve values, control-point positions, dragging, READ/APPLY and preset logic remain unchanged. Formal version fields remain 1.0.3; target v1.0.4 is unreleased.

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
