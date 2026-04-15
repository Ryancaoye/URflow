using System.Collections.Generic;

namespace URflow
{
    /// <summary>
    /// Built-in preset library with common easing curves.
    /// </summary>
    public static class PresetLibrary
    {
        public static List<BezierPreset> GetBuiltInPresets()
        {
            return new List<BezierPreset>
            {
                // ── Standard CSS Easings ──
                new BezierPreset("Linear",       "Standard", 0.00f, 0.00f, 1.00f, 1.00f),
                new BezierPreset("Ease",         "Standard", 0.25f, 0.10f, 0.25f, 1.00f),
                new BezierPreset("Ease In",      "Standard", 0.42f, 0.00f, 1.00f, 1.00f),
                new BezierPreset("Ease Out",     "Standard", 0.00f, 0.00f, 0.58f, 1.00f),
                new BezierPreset("Ease In Out",  "Standard", 0.42f, 0.00f, 0.58f, 1.00f),

                // ── Penner / Classic ──
                new BezierPreset("Sine In",      "Penner", 0.47f, 0.00f, 0.745f, 0.715f),
                new BezierPreset("Sine Out",     "Penner", 0.39f, 0.575f, 0.565f, 1.00f),
                new BezierPreset("Sine In Out",  "Penner", 0.445f, 0.05f, 0.55f, 0.95f),
                new BezierPreset("Quad In",      "Penner", 0.55f, 0.085f, 0.68f, 0.53f),
                new BezierPreset("Quad Out",     "Penner", 0.25f, 0.46f, 0.45f, 0.94f),
                new BezierPreset("Quad In Out",  "Penner", 0.455f, 0.03f, 0.515f, 0.955f),
                new BezierPreset("Cubic In",     "Penner", 0.55f, 0.055f, 0.675f, 0.19f),
                new BezierPreset("Cubic Out",    "Penner", 0.215f, 0.61f, 0.355f, 1.00f),
                new BezierPreset("Cubic In Out", "Penner", 0.645f, 0.045f, 0.355f, 1.00f),
                new BezierPreset("Quart In",     "Penner", 0.895f, 0.03f, 0.685f, 0.22f),
                new BezierPreset("Quart Out",    "Penner", 0.165f, 0.84f, 0.44f, 1.00f),
                new BezierPreset("Quart In Out", "Penner", 0.77f, 0.00f, 0.175f, 1.00f),
                new BezierPreset("Quint In",     "Penner", 0.755f, 0.05f, 0.855f, 0.06f),
                new BezierPreset("Quint Out",    "Penner", 0.23f, 1.00f, 0.32f, 1.00f),
                new BezierPreset("Quint In Out", "Penner", 0.86f, 0.00f, 0.07f, 1.00f),
                new BezierPreset("Expo In",      "Penner", 0.95f, 0.05f, 0.795f, 0.035f),
                new BezierPreset("Expo Out",     "Penner", 0.19f, 1.00f, 0.22f, 1.00f),
                new BezierPreset("Expo In Out",  "Penner", 1.00f, 0.00f, 0.00f, 1.00f),
                new BezierPreset("Circ In",      "Penner", 0.60f, 0.04f, 0.98f, 0.335f),
                new BezierPreset("Circ Out",     "Penner", 0.075f, 0.82f, 0.165f, 1.00f),
                new BezierPreset("Circ In Out",  "Penner", 0.785f, 0.135f, 0.15f, 0.86f),
                new BezierPreset("Back In",      "Penner", 0.60f, -0.28f, 0.735f, 0.045f),
                new BezierPreset("Back Out",     "Penner", 0.175f, 0.885f, 0.32f, 1.275f),
                new BezierPreset("Back In Out",  "Penner", 0.68f, -0.55f, 0.265f, 1.55f),

                // ── UI / App Motion ──
                new BezierPreset("Snappy",         "UI Motion", 0.07f, 0.00f, 0.00f, 1.00f),
                new BezierPreset("Smooth",         "UI Motion", 0.40f, 0.00f, 0.20f, 1.00f),
                new BezierPreset("Soft",           "UI Motion", 0.25f, 0.00f, 0.15f, 1.00f),
                new BezierPreset("Bounce Enter",   "UI Motion", 0.18f, 1.40f, 0.40f, 1.00f),
                new BezierPreset("Bounce Exit",    "UI Motion", 0.60f, 0.00f, 0.82f, -0.40f),
                new BezierPreset("Spring",         "UI Motion", 0.15f, 1.60f, 0.30f, 1.00f),
                new BezierPreset("Anticipate",     "UI Motion", 0.36f, -0.20f, 0.00f, 1.00f),
                new BezierPreset("Overshoot",      "UI Motion", 0.10f, 0.00f, 0.20f, 1.30f),
            };
        }
    }
}
