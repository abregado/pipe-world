using System.Collections.Generic;
using PipeWorld.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace PipeWorld.Landscape
{
    // Tiles beyond their draw distance are switched off, and lose shadows sooner. Distances shrink toward their night values with the fog.
    public class ScatterCulling : MonoBehaviour
    {
        struct Entry
        {
            public MeshRenderer renderer;
            public float dayDistance;
            public float nightDistance;
            public float shadowDistance;
        }

        public float refreshSeconds = 0.25f;

        readonly List<Entry> tiles = new List<Entry>();
        DayNightCycle cycle;
        float nextRefresh;

        public void Register(MeshRenderer tile, float dayDistance, float nightDistance, float shadowDistance)
        {
            tiles.Add(new Entry { renderer = tile, dayDistance = dayDistance, nightDistance = nightDistance, shadowDistance = shadowDistance });
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefresh)
                return;
            nextRefresh = Time.unscaledTime + refreshSeconds;

            var cam = Camera.main;
            if (cam == null)
                return;
            if (cycle == null)
                cycle = FindAnyObjectByType<DayNightCycle>();

            var night = cycle != null ? cycle.NightFactor : 0f;
            var position = cam.transform.position;
            foreach (var tile in tiles)
            {
                var draw = Mathf.Lerp(tile.dayDistance, tile.nightDistance, night);
                var distance = Mathf.Sqrt(tile.renderer.bounds.SqrDistance(position));
                tile.renderer.enabled = distance < draw;
                tile.renderer.shadowCastingMode = distance < tile.shadowDistance ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }
    }
}
