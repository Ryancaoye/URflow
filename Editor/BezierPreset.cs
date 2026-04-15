using System;
using UnityEngine;

namespace URflow
{
    /// <summary>
    /// Represents a single cubic-bezier easing preset.
    /// </summary>
    [Serializable]
    public class BezierPreset
    {
        public string name;
        public string category;
        public float x1;
        public float y1;
        public float x2;
        public float y2;
        public bool isFavorite;

        public BezierPreset() { }

        public BezierPreset(string name, string category, float x1, float y1, float x2, float y2)
        {
            this.name = name;
            this.category = category;
            this.x1 = x1;
            this.y1 = y1;
            this.x2 = x2;
            this.y2 = y2;
            this.isFavorite = false;
        }

        public string ToCSSString()
        {
            return $"cubic-bezier({x1:F2}, {y1:F2}, {x2:F2}, {y2:F2})";
        }

        public string ToParamString()
        {
            return $"{x1:F2}, {y1:F2}, {x2:F2}, {y2:F2}";
        }

        public BezierPreset Clone()
        {
            return new BezierPreset
            {
                name = this.name,
                category = this.category,
                x1 = this.x1,
                y1 = this.y1,
                x2 = this.x2,
                y2 = this.y2,
                isFavorite = this.isFavorite
            };
        }
    }
}
