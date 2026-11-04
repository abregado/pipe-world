using System.Collections.Generic;
using UnityEngine;

namespace PipeWorld.World
{
    public class LightGradeSystem : MonoBehaviour
    {
        const int MaxLights = 32;

        static readonly int PositionId = Shader.PropertyToID("_PW_GradeLightPos");
        static readonly int TintId = Shader.PropertyToID("_PW_GradeLightTint");
        static readonly int ParamsId = Shader.PropertyToID("_PW_GradeLightParams");
        static readonly int CountId = Shader.PropertyToID("_PW_GradeLightCount");
        static readonly int UseLightMaskId = Shader.PropertyToID("_PW_GradeUseLightMask");

        public float globalStrength = 1f;
        // Masks each light's grade by the shadowed point light the pixel received, so shadows keep the night grade.
        public bool shadowedGrade = true;

        readonly Vector4[] positions = new Vector4[MaxLights];
        readonly Vector4[] tints = new Vector4[MaxLights];
        readonly Vector4[] parameters = new Vector4[MaxLights];
        readonly List<GradeLight> chosen = new List<GradeLight>();
        Vector3 cameraPosition;

        void LateUpdate()
        {
            var cam = Camera.main;
            cameraPosition = cam != null ? cam.transform.position : Vector3.zero;

            chosen.Clear();
            foreach (var light in GradeLight.All)
                if (light.isActiveAndEnabled && light.strength > 0f)
                    chosen.Add(light);

            // Only the lights nearest the camera are uploaded; the shader loops a fixed-size array.
            if (chosen.Count > MaxLights)
            {
                chosen.Sort((a, b) => SqrDistance(a).CompareTo(SqrDistance(b)));
                chosen.RemoveRange(MaxLights, chosen.Count - MaxLights);
            }

            for (var i = 0; i < chosen.Count; i++)
            {
                var light = chosen[i];
                var p = light.transform.position;
                positions[i] = new Vector4(p.x, p.y, p.z, light.Radius);
                tints[i] = new Vector4(light.tint.r, light.tint.g, light.tint.b, light.strength * globalStrength);
                parameters[i] = new Vector4(light.warmLut, light.glow, 0f, 0f);
            }

            Shader.SetGlobalVectorArray(PositionId, positions);
            Shader.SetGlobalVectorArray(TintId, tints);
            Shader.SetGlobalVectorArray(ParamsId, parameters);
            Shader.SetGlobalFloat(CountId, chosen.Count);
            Shader.SetGlobalFloat(UseLightMaskId, shadowedGrade ? 1f : 0f);
        }

        float SqrDistance(GradeLight light) => (light.transform.position - cameraPosition).sqrMagnitude;
    }
}
