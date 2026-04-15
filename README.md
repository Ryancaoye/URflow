# URflow

A Flow-like bezier curve editor for Unity Animation Curves.

Inspired by the [Flow](https://aescripts.com/flow/) plugin for After Effects — bringing the same intuitive easing curve workflow to Unity.

## Features

- **Visual Bezier Editor** — Interactive curve with draggable control points (P1/P2)
- **cubic-bezier() Input** — Paste CSS-style parameters directly (e.g. `0.20, 0.00, 0.00, 1.00`)
- **40+ Built-in Presets** — Standard, Material Design 3, Penner easings, UI Motion
- **Apply to Animation** — One-click apply to selected keyframes in the Animation window
- **Direction Control** — Apply ease-in only (◀), ease-out only (▶), or both
- **Favorites** — Star your most-used curves for quick access
- **Custom Presets** — Save your own curves with custom names
- **Import/Export** — Share preset collections as JSON files
- **Curve Guard** — Automatically preserves bezier curves when keyframes are moved (prevents Unity from resetting weighted tangent data)

## Requirements

- Unity 2022.3+ (tested on 2022.3.20f1)

## Installation

1. Copy the `URflow` folder into your Unity project's `Assets/` directory
2. Open via menu: **Window > URflow** (or press `Ctrl+Shift+E`)

## Usage

1. Open the URflow window (`Window > URflow`)
2. Edit the curve by dragging the orange (P1) and green (P2) control points
3. Or paste cubic-bezier values in the text field
4. Or click a preset from the library
5. Select keyframes in the Animation window
6. Click **APPLY** to apply the curve

### Direction Buttons

- **◀ (Left)** — Apply only the ease-in portion (modifies the incoming tangent of the last keyframe)
- **APPLY** — Apply both ease-in and ease-out
- **▶ (Right)** — Apply only the ease-out portion (modifies the outgoing tangent of the first keyframe)

### Preset Categories

| Category    | Description                                     |
|-------------|------------------------------------------------|
| Standard    | CSS standard easings (ease, ease-in, ease-out)  |
| Material 3  | Google Material Design 3 motion curves          |
| Penner      | Robert Penner's classic easing functions        |
| UI Motion   | Common UI animation curves (snappy, spring...)  |
| Custom      | Your saved presets                               |

## File Structure

```
URflow/
├── Editor/
│   ├── URflow.Editor.asmdef
│   ├── URflowWindow.cs          # Main editor window
│   ├── URflowApplyHelper.cs     # Animation window integration
│   ├── URflowAnimHelper.cs      # Keyframe selection resolver
│   ├── URflowReadHelper.cs      # Read curve from keyframes
│   ├── URflowWeightedHelper.cs  # Set keys to weighted mode
│   ├── URflowCurveGuard.cs      # Auto-restore curves after keyframe moves
│   ├── CubicBezierConverter.cs  # Math: bezier ↔ AnimationCurve
│   ├── BezierPreset.cs          # Preset data model
│   ├── PresetLibrary.cs         # Built-in preset collection
│   ├── PresetManager.cs         # User preset persistence
│   ├── Icons/                   # Custom PNG icons
│   └── Presets/                 # (reserved for preset assets)
├── docs/                        # Changelogs and documentation
└── README.md
```

## License

MIT
