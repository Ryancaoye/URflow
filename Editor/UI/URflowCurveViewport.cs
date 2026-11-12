using UnityEngine;

namespace URflow
{
    // Display coordinates only. Fitting never changes the stored Bezier parameters.
    internal sealed class URflowCurveViewport
    {
        internal const float MinimumY = -2f;
        internal const float MaximumY = 3f;
        private const double FitSeconds = 0.18d;
        private Vector2 _range = new Vector2(0f, 1f);
        private Vector2 _target = new Vector2(0f, 1f);
        private Vector2 _start;
        private double _started = -1d;
        private bool _initialized;
        private float _dragMinimumY = MinimumY;
        private float _dragMaximumY = MaximumY;

        internal Vector2 Range { get { return _range; } }

        internal static Vector2 FittedRange(float y1, float y2)
        {
            float lower = Mathf.Min(0f, y1, y2);
            float upper = Mathf.Max(1f, y1, y2);
            return new Vector2(Mathf.Floor(lower * 4f) / 4f, Mathf.Ceil(upper * 4f) / 4f);
        }

        internal void Fit(float y1, float y2, double now, bool animate)
        {
            Vector2 target = FittedRange(y1, y2);
            if (_initialized && _target == target) return;
            _initialized = true;
            _target = target;
            // A changed preset/READ value must be contained on its first frame.
            // Only contraction is animated; expanding from an old range would hide handles.
            _range = new Vector2(Mathf.Min(_range.x, target.x), Mathf.Max(_range.y, target.y));
            _start = _range;
            _started = animate && _range != target ? now : -1d;
            if (_started < 0d) _range = target;
        }

        internal bool Tick(double now)
        {
            if (_started < 0d) return false;
            float t = Mathf.Clamp01((float)((now - _started) / FitSeconds));
            _range = Vector2.Lerp(_start, _target, t * t * (3f - 2f * t));
            if (t >= 1f) { _range = _target; _started = -1d; }
            return true;
        }

        internal void BeginDrag(float y = 0f)
        {
            _started = -1d;
            _target = _range;
            // READ can supply a finite curve outside the manual editing range.
            // Keep its initial value reachable throughout this gesture, without
            // allowing a horizontal drag to silently clip it to the normal limits.
            _dragMinimumY = Mathf.Min(MinimumY, y);
            _dragMaximumY = Mathf.Max(MaximumY, y);
        }

        internal Vector2 ToScreen(float x, float y, Rect rect)
        {
            return new Vector2(rect.x + x * rect.width,
                rect.yMax - (y - _range.x) / (_range.y - _range.x) * rect.height);
        }

        internal Vector2 FromScreen(Vector2 position, Rect rect)
        {
            return new Vector2((position.x - rect.x) / rect.width,
                _range.x + (rect.yMax - position.y) / rect.height * (_range.y - _range.x));
        }

        internal Vector2 Drag(Vector2 value, Vector2 delta, Rect rect, bool shift, float lockedY)
        {
            float span = _range.y - _range.x;
            float y = shift ? lockedY : Mathf.Clamp(value.y - delta.y / rect.height * span, _dragMinimumY, _dragMaximumY);
            Vector2 result = new Vector2(Mathf.Clamp01(value.x + delta.x / rect.width), y);
            float lower = _range.x, upper = _range.y;
            // Integrate mouse deltas instead of remapping the absolute cursor after a fit.
            // The value stays continuous when the display scale expands near an edge.
            if (!shift && y < value.y && y < lower + span * 0.06f)
                lower = Mathf.Floor((y - span * 0.12f) * 4f) / 4f;
            if (!shift && y > value.y && y > upper - span * 0.06f)
                upper = Mathf.Ceil((y + span * 0.12f) * 4f) / 4f;
            _range = new Vector2(Mathf.Min(lower, y, 0f), Mathf.Max(upper, y, 1f));
            _target = _range;
            return result;
        }
    }
}
