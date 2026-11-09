using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace URflow
{
    /// <summary>Cached IMGUI visuals; animation and editing behavior stay with their callers.</summary>
    internal static class URflowVisuals
    {
        internal const float PanelOpenSeconds = 0.4f;
        internal const float EdgeSweepSeconds = 1.9f;
        internal const float CornerRadius = 7f;

        internal static readonly Color Background = RGB(0x242424);
        internal static readonly Color GraphBackground = RGB(0x202020);
        internal static readonly Color PresetBackground = RGB(0x292929);
        internal static readonly Color PanelBackground = RGB(0x292929);
        internal static readonly Color Divider = RGB(0x444444);
        internal static readonly Color Amber = RGB(0xFFB826);
        internal static readonly Color CurveGreen = RGB(0x20E7A1);
        internal static readonly Color CurveCyan = RGB(0x12CDE0);
        internal static readonly Color CurveBlue = RGB(0x168CFF);
        internal static readonly Color ButtonGreen = RGB(0x5DF0AF);
        internal static readonly Color ButtonBlue = RGB(0x0071FF);

        private const int CurveSegments = 128;
        private const int CornerSegments = 12;
        private const int MaxStraightSegments = 128;
        private const int EdgeCapacity = (CornerSegments + 1 + MaxStraightSegments) * 4 + 1;
        private const int GlowBands = 16;
        private const float GlowSigma = 4.25f;
        private const float GlowStrokeHalfWidth = 3.5f;
        private const float GlowOpacity = 0.46f;
        private const float CurveEndpointGlowStrength = 0.4f;
        private const float CurveEndpointGlowFadePixels = 24f;
        private static readonly Dictionary<int, SurfaceAssets> Surfaces = new Dictionary<int, SurfaceAssets>();
        private static readonly Vector2[] CurvePoints = new Vector2[CurveSegments + 1];
        private static readonly Vector2[] CurveNormals = new Vector2[CurveSegments + 1];
        private static readonly Color[] CurveColors = new Color[CurveSegments + 1];
        private static readonly float[] CurveGlowStrengths = new float[CurveSegments + 1];
        private static readonly Vector2[] EdgePoints = new Vector2[EdgeCapacity];
        private static readonly Vector2[] EdgeNormals = new Vector2[EdgeCapacity];
        private static readonly Color[] EdgeColors = new Color[EdgeCapacity];
        private static readonly RectOffset ButtonBorder = new RectOffset(8, 8, 8, 8);
        private static readonly float[] GlowProfile = CreateGlowProfile();
        private static readonly Color EdgeAccent = RGB(0x78E2DC);
        private static readonly Color EdgeBlue = RGB(0x709DF0);
        private static readonly Color EdgePeak = RGB(0xD9FFFB);
        private static readonly Vector2 PanelEdgeAxis = new Vector2(0.9396926f, 0.3420201f); // 110 degrees in CSS.
        private static readonly Vector2 SweepEdgeAxis = new Vector2(0.9659258f, 0.2588190f); // 105 degrees in CSS.

        private static ButtonAssets _secondary;
        private static ButtonAssets _primary;
        private static ButtonAssets _selected;
        private static Material _lineMaterial;
        private static Shader _lineShader;
        private static bool _shaderLookupDone;
        private static bool _curveValid;
        private static Rect _curveRect;
        private static Vector4 _curveParameters;
        private static bool _edgeValid;
        private static Rect _edgeRect;
        private static float _edgeRadius;
        private static int _edgeCount;

        static URflowVisuals()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
        }

        internal static void DrawSurface(Rect r, Color fill, Color border, float radius = CornerRadius)
        {
            if (!IsRepaint() || !ValidRect(r)) return;
            if (!Finite(radius)) radius = CornerRadius;
            int corner = Mathf.RoundToInt(Mathf.Clamp(radius, 0f, Mathf.Min(32f, Mathf.Min(r.width, r.height) * 0.5f)));
            SurfaceAssets assets;
            if (!Surfaces.TryGetValue(corner, out assets))
            {
                assets = new SurfaceAssets(corner);
                Surfaces.Add(corner, assets);
            }

            Color previousColor = GUI.color;
            Color previousBackground = GUI.backgroundColor;
            try
            {
                GUI.backgroundColor = Color.white;
                if (fill.a > 0f)
                {
                    GUI.color = previousColor * fill;
                    assets.FillStyle.Draw(r, false, false, false, false);
                }
                if (border.a > 0f)
                {
                    GUI.color = previousColor * border;
                    assets.BorderStyle.Draw(r, false, false, false, false);
                }
            }
            finally
            {
                GUI.color = previousColor;
                GUI.backgroundColor = previousBackground;
            }
        }

        // Apply backgrounds without changing the caller's typography, padding, or layout.
        internal static void StyleButton(GUIStyle style, bool primary = false)
        {
            if (style == null) return;
            if (primary)
            {
                if (_primary == null)
                    _primary = new ButtonAssets(ButtonGreen, ButtonBlue,
                        RGB(0x71F4BB), RGB(0x188CFF), RGB(0x44C88D), RGB(0x0057CD), true);
                _primary.Apply(style);
            }
            else
            {
                if (_secondary == null)
                    _secondary = new ButtonAssets(RGB(0x3A3A3A), RGB(0x333333),
                        RGB(0x424242), RGB(0x383838), RGB(0x2A2A2A), RGB(0x252525), false);
                _secondary.Apply(style);
            }
        }

        internal static void StyleSelectedButton(GUIStyle style)
        {
            if (style == null) return;
            if (_selected == null)
                _selected = new ButtonAssets(Amber, RGB(0xE69D19),
                    RGB(0xFFCB5A), RGB(0xFFB826), RGB(0xD7941D), RGB(0xC38312), true);
            _selected.Apply(style);
            Color text = RGB(0x282119);
            style.normal.textColor = text;
            style.hover.textColor = text;
            style.active.textColor = text;
            style.focused.textColor = text;
            style.onNormal.textColor = text;
            style.onHover.textColor = text;
            style.onActive.textColor = text;
            style.onFocused.textColor = text;
        }

        internal static void DrawCurve(Rect r, float x1, float y1, float x2, float y2)
        {
            if (!IsRepaint()) return;
            GL.PushMatrix();
            try
            {
                GL.LoadPixelMatrix();
                RenderCurve(r, x1, y1, x2, y2, GUI.color);
            }
            finally { GL.PopMatrix(); }
        }

        // The same renderer is used by IMGUI and by offscreen RenderTexture validation.
        // The caller owns its render target, projection, and clipping; no GUI state is read here.
        internal static void RenderCurve(Rect r, float x1, float y1, float x2, float y2, Color tint)
        {
            if (!ValidRect(r) || !Finite(x1) || !Finite(y1) || !Finite(x2) || !Finite(y2)) return;
            Vector4 parameters = new Vector4(x1, y1, x2, y2);
            if (!_curveValid || _curveRect != r || !_curveParameters.Equals(parameters))
            {
                float distanceFromStart = 0f;
                for (int i = 0; i <= CurveSegments; i++)
                {
                    float t = (float)i / CurveSegments;
                    // This is the same normalized-x easing evaluator used by the editor.
                    float value = CubicBezierConverter.Evaluate(x1, y1, x2, y2, t);
                    if (!Finite(value)) { _curveValid = false; return; }
                    CurvePoints[i] = new Vector2(Mathf.LerpUnclamped(r.x, r.xMax, t),
                        Mathf.LerpUnclamped(r.yMax, r.y, value));
                    CurveColors[i] = CurveColor(t);
                    if (i > 0) distanceFromStart += Vector2.Distance(CurvePoints[i - 1], CurvePoints[i]);
                    CurveGlowStrengths[i] = distanceFromStart;
                }
                for (int i = 0; i <= CurveSegments; i++)
                {
                    // Fade only the halo near either endpoint; the core and geometry stay intact.
                    float distanceToEndpoint = Mathf.Min(CurveGlowStrengths[i], distanceFromStart - CurveGlowStrengths[i]);
                    CurveGlowStrengths[i] = Mathf.Lerp(CurveEndpointGlowStrength, 1f,
                        Mathf.SmoothStep(0f, 1f, distanceToEndpoint / CurveEndpointGlowFadePixels));
                }
                BuildNormals(CurvePoints, CurveNormals, CurveSegments + 1, false);
                _curveRect = r;
                _curveParameters = parameters;
                _curveValid = true;
            }
            if (!SetLinePass()) return;

            GL.Begin(GL.TRIANGLES);
            try
            {
                // Adjacent translucent bands cover each pixel once, approximating a blurred
                // 7-pixel stroke. Its colored soft tail now reaches 12-16 pixels from the core.
                DrawGlowRibbon(tint);
                DrawRibbonFringe(CurvePoints, CurveNormals, CurveColors, CurveSegments + 1, 0.9f, 0.55f, tint);
                DrawRibbon(CurvePoints, CurveNormals, CurveColors, CurveSegments + 1, 0.9f, 1f, tint);
            }
            finally { GL.End(); }
        }

        // Only tool/settings sheets opt into the preview's restrained permanent edge glow.
        internal static void DrawPanelEdge(Rect rect, float radius = CornerRadius)
        {
            if (!IsRepaint() || !ValidRect(rect)) return;
            PrepareEdge(rect, radius);
            if (!SetLinePass()) return;
            for (int i = 0; i < _edgeCount; i++)
            {
                float position = EdgeProjection(EdgePoints[i], rect, PanelEdgeAxis);
                Color color;
                if (position <= 0.35f)
                {
                    color = Color.Lerp(EdgeAccent, EdgeBlue, position / 0.35f);
                    color.a = Mathf.Lerp(0.63f, 0.35f, position / 0.35f);
                }
                else
                {
                    float amount = Mathf.Clamp01((position - 0.35f) / 0.30f);
                    color = Color.Lerp(EdgeBlue, Color.white, amount);
                    color.a = Mathf.Lerp(0.35f, 0.025f, amount);
                }
                EdgeColors[i] = color;
            }

            GL.PushMatrix();
            try
            {
                GL.LoadPixelMatrix();
                GL.Begin(GL.TRIANGLES);
                try
                {
                    DrawRibbon(EdgePoints, EdgeNormals, EdgeColors, _edgeCount, 0.5f, 1f, GUI.color);
                    for (int i = 0; i < _edgeCount; i++)
                    {
                        float x = Mathf.Clamp01((EdgePoints[i].x - rect.x) / rect.width);
                        float y = Mathf.Max(0f, EdgePoints[i].y - rect.y);
                        Color color = Color.Lerp(EdgeAccent, EdgeBlue, x / 0.55f);
                        color.a = (1f - Mathf.SmoothStep(0f, 1f, x / 0.8f))
                            * Mathf.Exp(-y * y / 128f) * 0.08f;
                        EdgeColors[i] = color;
                    }
                    DrawRibbon(EdgePoints, EdgeNormals, EdgeColors, _edgeCount, 7f, 0.15f, GUI.color);
                    DrawRibbon(EdgePoints, EdgeNormals, EdgeColors, _edgeCount, 4f, 0.25f, GUI.color);
                    DrawRibbon(EdgePoints, EdgeNormals, EdgeColors, _edgeCount, 2f, 0.4f, GUI.color);
                }
                finally { GL.End(); }
            }
            finally { GL.PopMatrix(); }
        }

        internal static bool DrawEdgeSweep(Rect rect, double started, float radius = CornerRadius)
        {
            if (started < 0d || double.IsNaN(started) || double.IsInfinity(started) || !ValidRect(rect)) return false;
            double elapsed = EditorApplication.timeSinceStartup - started;
            if (elapsed < 0d || elapsed >= EdgeSweepSeconds) return false;
            if (!IsRepaint()) return true;

            PrepareEdge(rect, radius);
            if (!SetLinePass()) return false;

            float progress = (float)(elapsed / EdgeSweepSeconds);
            float eased = CubicBezierConverter.Evaluate(0.2f, 0.65f, 0.35f, 1f, progress);
            // CSS positioning is relative to container minus image size. Project the
            // complete 3W x H gradient so the 105-degree band also matches tall sheets.
            float backgroundPosition = Mathf.LerpUnclamped(1.6f, -0.6f, eased);
            float offsetX = -2f * rect.width * backgroundPosition;
            float gradientLength = 3f * rect.width * SweepEdgeAxis.x + rect.height * SweepEdgeAxis.y;
            float fade = Mathf.Min(Mathf.Clamp01(progress / 0.14f), Mathf.Clamp01((1f - progress) / 0.15f));
            for (int i = 0; i < _edgeCount; i++)
            {
                Vector2 point = EdgePoints[i];
                float distance = ((point.x - rect.x - offsetX) * SweepEdgeAxis.x
                    + (point.y - rect.y) * SweepEdgeAxis.y) / gradientLength - 0.5f;
                Color color = SweepColor(distance);
                color.a *= fade;
                EdgeColors[i] = color;
            }

            GL.PushMatrix();
            try
            {
                GL.LoadPixelMatrix();
                GL.Begin(GL.TRIANGLES);
                try
                {
                    Color tint = GUI.color;
                    DrawRibbon(EdgePoints, EdgeNormals, EdgeColors, _edgeCount, 3.5f, 0.06f, tint);
                    DrawRibbon(EdgePoints, EdgeNormals, EdgeColors, _edgeCount, 1.8f, 0.14f, tint);
                    DrawRibbonFringe(EdgePoints, EdgeNormals, EdgeColors, _edgeCount, 0.75f, 0.45f, tint);
                    DrawRibbon(EdgePoints, EdgeNormals, EdgeColors, _edgeCount, 0.75f, 1f, tint);
                }
                finally { GL.End(); }
            }
            finally { GL.PopMatrix(); }
            return true;
        }

        internal static void Dispose()
        {
            foreach (SurfaceAssets assets in Surfaces.Values) assets.Dispose();
            Surfaces.Clear();
            if (_secondary != null) _secondary.Dispose();
            if (_primary != null) _primary.Dispose();
            if (_selected != null) _selected.Dispose();
            _secondary = null;
            _primary = null;
            _selected = null;
            if (_lineMaterial != null) Object.DestroyImmediate(_lineMaterial);
            _lineMaterial = null;
            _lineShader = null;
            _shaderLookupDone = false;
            _curveValid = false;
            _edgeValid = false;
            _edgeCount = 0;
        }

        private static bool IsRepaint()
        {
            return Event.current != null && Event.current.type == EventType.Repaint;
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool ValidRect(Rect rect)
        {
            return rect.width > 0f && rect.height > 0f && Finite(rect.x) && Finite(rect.y)
                && Finite(rect.width) && Finite(rect.height);
        }

        private static Color RGB(int hex)
        {
            return new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, 1f);
        }

        private static Color CurveColor(float t)
        {
            return t <= 0.5f ? Color.Lerp(CurveGreen, CurveCyan, t * 2f)
                : Color.Lerp(CurveCyan, CurveBlue, (t - 0.5f) * 2f);
        }

        private static float[] CreateGlowProfile()
        {
            float[] profile = new float[GlowBands + 1];
            float denominator = GlowSigma * Mathf.Sqrt(2f);
            for (int i = 0; i <= GlowBands; i++)
                profile[i] = GlowOpacity * 0.5f * (Erf((i + GlowStrokeHalfWidth) / denominator)
                    - Erf((i - GlowStrokeHalfWidth) / denominator));
            return profile;
        }

        private static float Erf(float value)
        {
            float sign = value < 0f ? -1f : 1f;
            float x = Mathf.Abs(value);
            float t = 1f / (1f + 0.3275911f * x);
            float polynomial = (((((1.0614054f * t - 1.4531521f) * t + 1.4214138f) * t
                - 0.28449672f) * t + 0.2548296f) * t);
            return sign * (1f - polynomial * Mathf.Exp(-x * x));
        }

        private static void DrawGlowRibbon(Color tint)
        {
            for (int i = 1; i <= CurveSegments; i++)
            {
                Color first = CurveColors[i - 1] * tint;
                Color second = CurveColors[i] * tint;
                first.a *= CurveGlowStrengths[i - 1];
                second.a *= CurveGlowStrengths[i];
                for (int band = 0; band < GlowBands; band++)
                {
                    Color firstInner = first;
                    Color firstOuter = first;
                    Color secondInner = second;
                    Color secondOuter = second;
                    firstInner.a *= GlowProfile[band];
                    secondInner.a *= GlowProfile[band];
                    firstOuter.a *= GlowProfile[band + 1];
                    secondOuter.a *= GlowProfile[band + 1];
                    Vector2 firstNear = CurveNormals[i - 1] * band;
                    Vector2 secondNear = CurveNormals[i] * band;
                    Vector2 firstFar = CurveNormals[i - 1] * (band + 1);
                    Vector2 secondFar = CurveNormals[i] * (band + 1);
                    Quad(CurvePoints[i - 1] + firstNear, CurvePoints[i - 1] + firstFar,
                        CurvePoints[i] + secondFar, CurvePoints[i] + secondNear,
                        firstInner, firstOuter, secondOuter, secondInner);
                    Quad(CurvePoints[i - 1] - firstNear, CurvePoints[i - 1] - firstFar,
                        CurvePoints[i] - secondFar, CurvePoints[i] - secondNear,
                        firstInner, firstOuter, secondOuter, secondInner);
                }
            }
        }

        private static void PrepareEdge(Rect rect, float radius)
        {
            if (!Finite(radius)) radius = CornerRadius;
            float corner = Mathf.Clamp(radius, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);
            if (!_edgeValid || _edgeRect != rect || !Mathf.Approximately(_edgeRadius, corner))
                BuildEdge(rect, corner);
        }

        private static float EdgeProjection(Vector2 point, Rect rect, Vector2 axis)
        {
            return ((point.x - rect.x) * axis.x + (point.y - rect.y) * axis.y)
                / (rect.width * axis.x + rect.height * axis.y);
        }

        private static Color SweepColor(float distance)
        {
            if (distance < -0.14f || distance > 0.15f) return Color.clear;
            Color color;
            if (distance < -0.04f)
            {
                color = EdgeAccent;
                color.a = 0.65f * Mathf.InverseLerp(-0.14f, -0.04f, distance);
            }
            else if (distance < 0f)
            {
                float amount = (distance + 0.04f) / 0.04f;
                color = Color.Lerp(EdgeAccent, EdgePeak, amount);
                color.a = Mathf.Lerp(0.65f, 1f, amount);
            }
            else if (distance < 0.04f)
            {
                float amount = distance / 0.04f;
                color = Color.Lerp(EdgePeak, EdgeBlue, amount);
                color.a = Mathf.Lerp(1f, 0.6f, amount);
            }
            else
            {
                color = EdgeBlue;
                color.a = 0.6f * (1f - Mathf.InverseLerp(0.04f, 0.15f, distance));
            }
            return color;
        }

        private static bool SetLinePass()
        {
            if (_lineMaterial == null)
            {
                if (!_shaderLookupDone)
                {
                    _lineShader = Shader.Find("Hidden/Internal-Colored");
                    _shaderLookupDone = true;
                }
                if (_lineShader == null) return false;
                _lineMaterial = new Material(_lineShader) { hideFlags = HideFlags.HideAndDontSave };
                _lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                _lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                _lineMaterial.SetInt("_Cull", (int)CullMode.Off);
                _lineMaterial.SetInt("_ZWrite", 0);
                _lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            }
            return _lineMaterial.SetPass(0);
        }

        private static void BuildNormals(Vector2[] points, Vector2[] normals, int count, bool closed)
        {
            for (int i = 0; i < count; i++)
            {
                int previous = i > 0 ? i - 1 : (closed ? count - 2 : 0);
                int next = i < count - 1 ? i + 1 : (closed ? 1 : count - 1);
                Vector2 tangent = (points[next] - points[previous]).normalized;
                normals[i] = new Vector2(-tangent.y, tangent.x);
            }
        }

        private static void DrawRibbon(Vector2[] points, Vector2[] normals, Color[] colors, int count,
            float halfWidth, float alpha, Color tint)
        {
            for (int i = 1; i < count; i++)
            {
                Vector2 firstOffset = normals[i - 1] * halfWidth;
                Vector2 secondOffset = normals[i] * halfWidth;
                Color first = colors[i - 1] * tint;
                Color second = colors[i] * tint;
                first.a *= alpha;
                second.a *= alpha;
                Quad(points[i - 1] + firstOffset, points[i - 1] - firstOffset,
                    points[i] - secondOffset, points[i] + secondOffset, first, first, second, second);
            }
        }

        private static void DrawRibbonFringe(Vector2[] points, Vector2[] normals, Color[] colors, int count,
            float halfWidth, float fringe, Color tint)
        {
            for (int i = 1; i < count; i++)
            {
                Color first = colors[i - 1] * tint;
                Color second = colors[i] * tint;
                Color clearFirst = first;
                Color clearSecond = second;
                clearFirst.a = 0f;
                clearSecond.a = 0f;
                Vector2 firstInner = normals[i - 1] * halfWidth;
                Vector2 secondInner = normals[i] * halfWidth;
                Vector2 firstOuter = normals[i - 1] * (halfWidth + fringe);
                Vector2 secondOuter = normals[i] * (halfWidth + fringe);
                Quad(points[i - 1] + firstInner, points[i - 1] + firstOuter,
                    points[i] + secondOuter, points[i] + secondInner, first, clearFirst, clearSecond, second);
                Quad(points[i - 1] - firstInner, points[i - 1] - firstOuter,
                    points[i] - secondOuter, points[i] - secondInner, first, clearFirst, clearSecond, second);
            }
        }

        private static void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
        {
            Vertex(a, ca); Vertex(b, cb); Vertex(c, cc);
            Vertex(a, ca); Vertex(c, cc); Vertex(d, cd);
        }

        private static void Vertex(Vector2 point, Color color)
        {
            GL.Color(color);
            GL.Vertex3(point.x, point.y, 0f);
        }

        private static void BuildEdge(Rect rect, float radius)
        {
            _edgeCount = 0;
            // The one-pixel border is centered half a pixel inside the surface.
            Rect inner = new Rect(rect.x + 0.5f, rect.y + 0.5f,
                Mathf.Max(0f, rect.width - 1f), Mathf.Max(0f, rect.height - 1f));
            float corner = Mathf.Max(0f, radius - 0.5f);
            AddCorner(new Vector2(inner.xMax - corner, inner.y + corner), corner, -90f);
            AddStraight(new Vector2(inner.xMax, inner.yMax - corner));
            AddCorner(new Vector2(inner.xMax - corner, inner.yMax - corner), corner, 0f);
            AddStraight(new Vector2(inner.x + corner, inner.yMax));
            AddCorner(new Vector2(inner.x + corner, inner.yMax - corner), corner, 90f);
            AddStraight(new Vector2(inner.x, inner.y + corner));
            AddCorner(new Vector2(inner.x + corner, inner.y + corner), corner, 180f);
            AddStraight(EdgePoints[0]);
            EdgePoints[_edgeCount++] = EdgePoints[0];
            BuildNormals(EdgePoints, EdgeNormals, _edgeCount, true);
            _edgeRect = rect;
            _edgeRadius = radius;
            _edgeValid = true;
        }

        private static void AddCorner(Vector2 center, float radius, float startDegrees)
        {
            if (radius <= 0f)
            {
                EdgePoints[_edgeCount++] = center;
                return;
            }
            for (int i = 0; i <= CornerSegments; i++)
            {
                float angle = (startDegrees + 90f * i / CornerSegments) * Mathf.Deg2Rad;
                EdgePoints[_edgeCount++] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
        }

        private static void AddStraight(Vector2 end)
        {
            Vector2 start = EdgePoints[_edgeCount - 1];
            int segments = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(start, end) / 8f), 1, MaxStraightSegments);
            for (int i = 1; i < segments; i++)
                EdgePoints[_edgeCount++] = Vector2.Lerp(start, end, (float)i / segments);
        }

        private static float RoundedDistance(float x, float y, int width, int height, float radius)
        {
            float qx = Mathf.Abs(x - width * 0.5f) - (width * 0.5f - radius);
            float qy = Mathf.Abs(y - height * 0.5f) - (height * 0.5f - radius);
            float outsideX = Mathf.Max(qx, 0f);
            float outsideY = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY)
                + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static Texture2D Texture(int width, int height, Color[] pixels, string name)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private sealed class SurfaceAssets
        {
            private readonly Texture2D _fill;
            private readonly Texture2D _border;
            internal readonly GUIStyle FillStyle;
            internal readonly GUIStyle BorderStyle;

            internal SurfaceAssets(int radius)
            {
                int size = Mathf.Max(4, radius * 2 + 4);
                Color[] fill = new Color[size * size];
                Color[] border = new Color[fill.Length];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float distance = RoundedDistance(x + 0.5f, y + 0.5f, size, size, radius);
                        float coverage = Mathf.Clamp01(0.5f - distance);
                        float inset = Mathf.Clamp01(-0.5f - distance);
                        int index = y * size + x;
                        fill[index] = new Color(1f, 1f, 1f, coverage);
                        border[index] = new Color(1f, 1f, 1f, coverage - inset);
                    }
                _fill = Texture(size, size, fill, "URflow surface fill");
                _border = Texture(size, size, border, "URflow surface border");
                RectOffset slices = new RectOffset(radius + 1, radius + 1, radius + 1, radius + 1);
                FillStyle = new GUIStyle { border = slices };
                BorderStyle = new GUIStyle { border = slices };
                FillStyle.normal.background = _fill;
                BorderStyle.normal.background = _border;
            }

            internal void Dispose()
            {
                Object.DestroyImmediate(_fill);
                Object.DestroyImmediate(_border);
            }
        }

        private sealed class ButtonAssets
        {
            private readonly Texture2D _normal;
            private readonly Texture2D _hover;
            private readonly Texture2D _active;

            internal ButtonAssets(Color normalStart, Color normalEnd, Color hoverStart, Color hoverEnd,
                Color activeStart, Color activeEnd, bool horizontal)
            {
                _normal = MakeButton(normalStart, normalEnd, horizontal, "URflow button normal");
                _hover = MakeButton(hoverStart, hoverEnd, horizontal, "URflow button hover");
                _active = MakeButton(activeStart, activeEnd, horizontal, "URflow button active");
            }

            internal void Apply(GUIStyle style)
            {
                style.normal.background = _normal;
                style.hover.background = _hover;
                style.active.background = _active;
                style.focused.background = _normal;
                style.onNormal.background = _normal;
                style.onHover.background = _hover;
                style.onActive.background = _active;
                style.onFocused.background = _normal;
                // Skin-provided HiDPI images must not override these procedural backgrounds.
                style.normal.scaledBackgrounds = null;
                style.hover.scaledBackgrounds = null;
                style.active.scaledBackgrounds = null;
                style.focused.scaledBackgrounds = null;
                style.onNormal.scaledBackgrounds = null;
                style.onHover.scaledBackgrounds = null;
                style.onActive.scaledBackgrounds = null;
                style.onFocused.scaledBackgrounds = null;
                style.border = ButtonBorder;
            }

            internal void Dispose()
            {
                Object.DestroyImmediate(_normal);
                Object.DestroyImmediate(_hover);
                Object.DestroyImmediate(_active);
            }

            private static Texture2D MakeButton(Color start, Color end, bool horizontal, string name)
            {
                const int width = 128;
                const int height = 32;
                Color[] pixels = new Color[width * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        float distance = RoundedDistance(x + 0.5f, y + 0.5f, width, height, CornerRadius);
                        float coverage = Mathf.Clamp01(0.5f - distance);
                        float t = horizontal ? (float)x / (width - 1) : 1f - (float)y / (height - 1);
                        Color color = Color.Lerp(start, end, t);
                        // A restrained rim gives depth without washing the gradient out.
                        float rim = Mathf.Clamp01(distance + 1.5f) * 0.10f;
                        color = Color.Lerp(color, Color.white, rim);
                        color.a = coverage;
                        pixels[y * width + x] = color;
                    }
                return Texture(width, height, pixels, name);
            }
        }
    }
}
