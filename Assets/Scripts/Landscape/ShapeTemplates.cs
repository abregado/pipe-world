using System;
using System.Collections.Generic;
using UnityEngine;

namespace PipeWorld.Landscape
{
    // All templates span local y 0..1 and roughly x/z -0.5..0.5, so a matrix scale gives the final size.
    public static class ShapeTemplates
    {
        static float R(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        public static Tri[] Prism(System.Random rng, int sides, int rings, bool flatTop, bool bottomCap, float jitter, float topTaper)
        {
            var tris = new List<Tri>();
            var angles = new float[sides];
            for (var i = 0; i < sides; i++)
                angles[i] = (i + R(rng, -0.25f, 0.25f)) / sides * Mathf.PI * 2f;

            var ring = new Vector3[rings + 1, sides];
            for (var j = 0; j <= rings; j++)
            {
                var y = j / (float)rings;
                var taper = Mathf.Lerp(1f, topTaper, y * y);
                var bulge = 1f + 0.12f * Mathf.Sin(y * Mathf.PI);
                for (var i = 0; i < sides; i++)
                {
                    var r = 0.5f * (1f + jitter * R(rng, -1f, 1f)) * taper * bulge;
                    var yy = j == rings && !flatTop ? y + R(rng, -0.06f, 0.06f) : y;
                    ring[j, i] = new Vector3(Mathf.Cos(angles[i]) * r, yy, Mathf.Sin(angles[i]) * r);
                }
            }

            for (var j = 0; j < rings; j++)
            for (var i = 0; i < sides; i++)
            {
                var n = (i + 1) % sides;
                tris.Add(new Tri { a = ring[j, i], b = ring[j, n], c = ring[j + 1, n], shade = R(rng, 0.93f, 1.07f) });
                tris.Add(new Tri { a = ring[j, i], b = ring[j + 1, n], c = ring[j + 1, i], shade = R(rng, 0.93f, 1.07f) });
            }

            var topCenter = new Vector3(0f, flatTop ? 1f : 1f + R(rng, 0.05f, 0.15f), 0f);
            for (var i = 0; i < sides; i++)
                tris.Add(new Tri { a = topCenter, b = ring[rings, i], c = ring[rings, (i + 1) % sides], shade = flatTop ? 1f : R(rng, 0.97f, 1.08f) });

            if (bottomCap)
                for (var i = 0; i < sides; i++)
                    tris.Add(new Tri { a = Vector3.zero, b = ring[0, i], c = ring[0, (i + 1) % sides], shade = 0.8f });

            return Finish(tris);
        }

        public static Tri[] Crystal(System.Random rng)
        {
            const int sides = 6;
            var tris = new List<Tri>();
            var baseRing = new Vector3[sides];
            var midRing = new Vector3[sides];
            for (var i = 0; i < sides; i++)
            {
                var angle = (i + R(rng, -0.1f, 0.1f)) / sides * Mathf.PI * 2f;
                var cos = Mathf.Cos(angle);
                var sin = Mathf.Sin(angle);
                var rb = 0.5f * R(rng, 0.9f, 1.1f);
                var rm = 0.46f * R(rng, 0.9f, 1.1f);
                baseRing[i] = new Vector3(cos * rb, 0f, sin * rb);
                midRing[i] = new Vector3(cos * rm, 0.7f, sin * rm);
            }
            var tip = new Vector3(R(rng, -0.08f, 0.08f), 1f, R(rng, -0.08f, 0.08f));

            for (var i = 0; i < sides; i++)
            {
                var n = (i + 1) % sides;
                tris.Add(new Tri { a = baseRing[i], b = baseRing[n], c = midRing[n], shade = 0.85f, emissive = 30 });
                tris.Add(new Tri { a = baseRing[i], b = midRing[n], c = midRing[i], shade = 0.9f, emissive = 30 });
                tris.Add(new Tri { a = midRing[i], b = midRing[n], c = tip, shade = R(rng, 1.08f, 1.22f), emissive = 255 });
                tris.Add(new Tri { a = Vector3.zero, b = baseRing[i], c = baseRing[n], shade = 0.7f });
            }
            return Finish(tris);
        }

        public static Tri[] Blade(System.Random rng)
        {
            const int sides = 3;
            var tris = new List<Tri>();
            var baseRing = new Vector3[sides];
            var midRing = new Vector3[sides];
            var offset = R(rng, 0f, Mathf.PI);
            for (var i = 0; i < sides; i++)
            {
                var angle = offset + i / (float)sides * Mathf.PI * 2f;
                baseRing[i] = new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f);
                midRing[i] = new Vector3(Mathf.Cos(angle) * 0.3f, 0.55f, Mathf.Sin(angle) * 0.3f);
            }
            var apex = new Vector3(R(rng, -0.25f, 0.25f), 1f, R(rng, -0.25f, 0.25f));

            for (var i = 0; i < sides; i++)
            {
                var n = (i + 1) % sides;
                tris.Add(new Tri { a = baseRing[i], b = baseRing[n], c = midRing[n], shade = 0.82f });
                tris.Add(new Tri { a = baseRing[i], b = midRing[n], c = midRing[i], shade = 0.86f });
                tris.Add(new Tri { a = midRing[i], b = midRing[n], c = apex, shade = R(rng, 1.12f, 1.25f) });
            }
            return Finish(tris);
        }

        // A flat tapered card in the XY plane; the grass shader is two-sided, so winding only has to be consistent.
        public static Tri[] GrassBlade(System.Random rng)
        {
            var lean = R(rng, -0.25f, 0.25f);
            var baseLeft = new Vector3(-0.5f, 0f, 0f);
            var baseRight = new Vector3(0.5f, 0f, 0f);
            var midLeft = new Vector3(-0.36f + lean * 0.4f, 0.5f, 0f);
            var midRight = new Vector3(0.36f + lean * 0.4f, 0.5f, 0f);
            var tip = new Vector3(lean, 1f, 0f);

            return new[]
            {
                new Tri { a = baseLeft, b = midRight, c = baseRight, shade = 0.8f, weights = new Vector3(0f, 0.35f, 0f) },
                new Tri { a = baseLeft, b = midLeft, c = midRight, shade = 0.86f, weights = new Vector3(0f, 0.35f, 0.35f) },
                new Tri { a = midLeft, b = tip, c = midRight, shade = R(rng, 1.1f, 1.25f), weights = new Vector3(0.35f, 1f, 0.35f) },
            };
        }

        // Flips any triangle that faces toward the template's centre, so winding never has to be authored by hand.
        static Tri[] Finish(List<Tri> tris)
        {
            var center = new Vector3(0f, 0.5f, 0f);
            var result = tris.ToArray();
            for (var i = 0; i < result.Length; i++)
            {
                var t = result[i];
                var normal = Vector3.Cross(t.b - t.a, t.c - t.a);
                var centroid = (t.a + t.b + t.c) / 3f;
                if (Vector3.Dot(normal, centroid - center) < 0f)
                    (t.b, t.c) = (t.c, t.b);
                result[i] = t;
            }
            return result;
        }
    }

    public sealed class ShapeLibrary
    {
        readonly Tri[][] flatBlocks;
        readonly Tri[][] rocks;
        readonly Tri[][] mounds;
        readonly Tri[][] crystals;
        readonly Tri[][] blades;
        readonly Tri[][] grassBlades;

        public ShapeLibrary(int seed)
        {
            var rng = new System.Random(seed);
            flatBlocks = Make(6, () => ShapeTemplates.Prism(rng, 8, 4, true, false, 0.12f, 1f));
            rocks = Make(8, () => ShapeTemplates.Prism(rng, 7, 2, false, true, 0.22f, 0.55f));
            mounds = Make(4, () => ShapeTemplates.Prism(rng, 9, 2, false, false, 0.15f, 0.35f));
            crystals = Make(6, () => ShapeTemplates.Crystal(rng));
            blades = Make(6, () => ShapeTemplates.Blade(rng));
            grassBlades = Make(8, () => ShapeTemplates.GrassBlade(rng));
        }

        public Tri[] GrassBlade(System.Random rng) => Pick(grassBlades, rng);

        public Tri[] FlatBlock(System.Random rng) => Pick(flatBlocks, rng);
        public Tri[] Rock(System.Random rng) => Pick(rocks, rng);
        public Tri[] Mound(System.Random rng) => Pick(mounds, rng);
        public Tri[] Crystal(System.Random rng) => Pick(crystals, rng);
        public Tri[] Blade(System.Random rng) => Pick(blades, rng);

        static Tri[][] Make(int count, Func<Tri[]> factory)
        {
            var result = new Tri[count][];
            for (var i = 0; i < count; i++)
                result[i] = factory();
            return result;
        }

        static Tri[] Pick(Tri[][] set, System.Random rng) => set[rng.Next(set.Length)];
    }
}
