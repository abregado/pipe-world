using System.Collections.Generic;
using UnityEngine;

namespace PipeWorld.World
{
    // Marks a light whose surroundings are colour-graded by the LightGrade pass.
    public class GradeLight : MonoBehaviour
    {
        static readonly List<GradeLight> Active = new List<GradeLight>();

        public static IReadOnlyList<GradeLight> All => Active;

        public Light source;
        public Color tint = new Color(1f, 0.62f, 0.3f);
        public float strength = 1f;
        // 0 tints only; 1 also runs the surroundings through the warm LUT.
        [Range(0f, 1f)] public float warmLut = 1f;
        public float glow = 0.12f;
        public float radiusScale = 1f;

        public float Radius => (source != null ? source.range : 20f) * radiusScale;

        void OnEnable() => Active.Add(this);

        void OnDisable() => Active.Remove(this);
    }
}
