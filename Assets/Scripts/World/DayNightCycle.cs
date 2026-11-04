using UnityEngine;
using UnityEngine.Rendering;

namespace PipeWorld.World
{
    [ExecuteAlways]
    public class DayNightCycle : MonoBehaviour
    {
        static readonly int ZenithId = Shader.PropertyToID("_ZenithColor");
        static readonly int HorizonId = Shader.PropertyToID("_HorizonColor");
        static readonly int GroundId = Shader.PropertyToID("_GroundColor");
        static readonly int SunDirId = Shader.PropertyToID("_SunDir");
        static readonly int SunColorId = Shader.PropertyToID("_SunColor");
        static readonly int FogColorId = Shader.PropertyToID("_PW_FogColor");
        static readonly int FogParamsId = Shader.PropertyToID("_PW_FogParams");
        static readonly int NightFactorId = Shader.PropertyToID("_PW_NightFactor");

        public Light sun;
        public Material skyboxSource;

        // 0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset
        [Range(0f, 1f)] public float timeOfDay = 0.35f;
        public float dayLengthSeconds = 600f;
        public bool paused;

        public float sunHeading = 30f;
        // Ambient is kept well under the sun so sun shadows read; see Docs/lighting-and-day-night.md.
        public float maxSunIntensity = 1.4f;
        public Gradient sunColor = Make(
            (0.00f, new Color(1.00f, 0.55f, 0.35f)),
            (0.25f, new Color(1.00f, 0.80f, 0.65f)),
            (1.00f, new Color(1.00f, 0.98f, 0.95f)));

        // Gradients below are sampled by sun elevation remapped to 0..1: 0 = midnight, 0.5 = horizon, 1 = noon.
        public Gradient ambientColor = Make(
            (0.00f, new Color(0.48f, 0.38f, 0.44f)),
            (0.40f, new Color(0.48f, 0.38f, 0.44f)),
            (0.55f, new Color(0.62f, 0.64f, 0.70f)),
            (0.65f, new Color(0.64f, 0.70f, 0.77f)),
            (1.00f, new Color(0.68f, 0.74f, 0.80f)));

        public Gradient skyZenith = Make(
            (0.00f, new Color(0.14f, 0.08f, 0.20f)),
            (0.40f, new Color(0.20f, 0.12f, 0.26f)),
            (0.55f, new Color(0.40f, 0.48f, 0.78f)),
            (1.00f, new Color(0.30f, 0.52f, 0.88f)));

        public Gradient skyHorizon = Make(
            (0.00f, new Color(0.40f, 0.20f, 0.28f)),
            (0.40f, new Color(0.62f, 0.30f, 0.28f)),
            (0.52f, new Color(1.00f, 0.60f, 0.40f)),
            (0.62f, new Color(0.85f, 0.90f, 0.98f)),
            (1.00f, new Color(0.78f, 0.90f, 1.00f)));

        // Sun elevation (-1 midnight, +1 noon) at which the night grade starts and reaches full strength.
        public float nightStart = 0.12f;
        public float nightFull = -0.18f;

        // Debug multiplier on the flat ambient; lower it to see real-time lights and shadows without the ambient washing them out.
        public float ambientScale = 1f;

        public float dayFogStart = 120f;
        public float dayFogEnd = 1000f;
        public float nightFogStart = 30f;
        public float nightFogEnd = 420f;
        public float dayFogDarken = 0.62f;
        public float nightFogDarken = 0.5f;

        public float NightFactor { get; private set; }
        public float FogEnd { get; private set; }

        // Sun elevation crosses nightStart here, as the day ends and begins.
        public float StartOfDayTime => Mathf.Acos(-nightStart) / (Mathf.PI * 2f);
        public float StartOfNightTime => 1f - StartOfDayTime;

        Material skyInstance;

        static Gradient Make(params (float t, Color c)[] stops)
        {
            var keys = new GradientColorKey[stops.Length];
            for (int i = 0; i < stops.Length; i++)
                keys[i] = new GradientColorKey(stops[i].c, stops[i].t);
            var g = new Gradient();
            g.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        void OnEnable()
        {
            if (skyboxSource != null)
            {
                skyInstance = new Material(skyboxSource) { hideFlags = HideFlags.HideAndDontSave };
                RenderSettings.skybox = skyInstance;
            }
            Apply();
        }

        void OnDisable()
        {
            if (skyInstance != null)
            {
                if (Application.isPlaying) Destroy(skyInstance); else DestroyImmediate(skyInstance);
                skyInstance = null;
            }
        }

        void Update()
        {
            if (!paused && Application.isPlaying && dayLengthSeconds > 0f)
                timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime / dayLengthSeconds, 1f);
            Apply();
        }

        void OnValidate() => Apply();

        void Apply()
        {
            float elevation = -Mathf.Cos(timeOfDay * Mathf.PI * 2f);
            NightFactor = Mathf.Clamp01(Mathf.InverseLerp(nightStart, nightFull, elevation));

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(timeOfDay * 360f - 90f, sunHeading, 0f);
                sun.color = sunColor.Evaluate(Mathf.Clamp01(elevation));
                sun.intensity = maxSunIntensity * Mathf.Clamp01(elevation * 3f);
                // Below the horizon the light points up and would cast shadows from underneath.
                sun.enabled = elevation > 0.01f;
                RenderSettings.sun = sun;
            }

            float t = Mathf.Clamp01(elevation * 0.5f + 0.5f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor.Evaluate(t) * ambientScale;

            if (skyInstance != null)
            {
                var horizon = skyHorizon.Evaluate(t);
                skyInstance.SetColor(ZenithId, skyZenith.Evaluate(t));
                skyInstance.SetColor(HorizonId, horizon);
                skyInstance.SetColor(GroundId, horizon * 0.9f);
                if (sun != null)
                {
                    skyInstance.SetVector(SunDirId, -sun.transform.forward);
                    skyInstance.SetColor(SunColorId, sunColor.Evaluate(Mathf.Clamp01(elevation)));
                }
            }

            // Distant terrain converges on a colour darker than the sky horizon, so it reads as a silhouette against it.
            var fogStart = Mathf.Lerp(dayFogStart, nightFogStart, NightFactor);
            FogEnd = Mathf.Lerp(dayFogEnd, nightFogEnd, NightFactor);
            var fogColor = (Color.Lerp(skyHorizon.Evaluate(t), skyZenith.Evaluate(t), 0.2f) * Mathf.Lerp(dayFogDarken, nightFogDarken, NightFactor)).linear;
            Shader.SetGlobalVector(FogColorId, new Vector4(fogColor.r, fogColor.g, fogColor.b, 1f));
            Shader.SetGlobalVector(FogParamsId, new Vector4(fogStart, 1f / Mathf.Max(1f, FogEnd - fogStart), 0f, 0f));
            Shader.SetGlobalFloat(NightFactorId, NightFactor);
        }
    }
}
