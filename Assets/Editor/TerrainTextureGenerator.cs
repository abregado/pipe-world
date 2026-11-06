using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PipeWorld.EditorTools
{
    public static class TerrainTextureGenerator
    {
        const int Size = 256;
        public const string Directory = "Assets/Art/Textures/Terrain";

        public static Texture2D Generate(string name, Func<float, float, Color> shader)
        {
            System.IO.Directory.CreateDirectory(Directory);
            var path = $"{Directory}/{name}.png";

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var c = shader(x / (float)Size, y / (float)Size);
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Texture2D[] GenerateTundra() => new[]
        {
            Generate("Tundra_Top", TundraTop),
            Generate("Tundra_Side", TundraSide),
        };

        public static Texture2D[] GenerateRock() => new[]
        {
            Generate("Rock_Top", RockTop),
            Generate("Rock_Side", RockSide),
        };

        public static Texture2D[] GenerateIce() => new[]
        {
            Generate("Ice_Top", IceTop),
            Generate("Ice_Side", IceSide),
        };

        public static Texture2D[] GenerateGrass() => new[]
        {
            Generate("Grass_Top", GrassTop),
            Generate("Grass_Side", GrassSide),
        };

        static Color GrassTop(float u, float v)
        {
            var patch = Smooth(0.35f, 0.65f, Fbm(u, v, 4, 4, 21));
            var c = Color.Lerp(new Color(0.34f, 0.52f, 0.22f), new Color(0.50f, 0.62f, 0.28f), patch);
            var dry = Smooth(0.62f, 0.75f, Fbm(u, v, 3, 3, 23));
            c = Color.Lerp(c, new Color(0.62f, 0.60f, 0.34f), dry * 0.6f);
            var speckle = Value(u * 48f, v * 48f, 48, 48, 25);
            return c * (0.9f + 0.16f * speckle);
        }

        static Color GrassSide(float u, float v)
        {
            var bands = 0.5f + 0.5f * Mathf.Sin((v * 7f + Fbm(u, v, 3, 3, 27) * 1.3f) * Mathf.PI * 2f);
            var c = Color.Lerp(new Color(0.36f, 0.30f, 0.24f), new Color(0.50f, 0.42f, 0.32f), bands);
            return c * (0.88f + 0.22f * Fbm(u, v, 6, 3, 29));
        }

        static Color TundraTop(float u, float v)
        {
            var patch = Smooth(0.40f, 0.60f, Fbm(u, v, 4, 4, 1));
            var c = Color.Lerp(new Color(0.84f, 0.88f, 0.90f), new Color(0.50f, 0.52f, 0.38f), patch);
            var speckle = Value(u * 32f, v * 32f, 32, 32, 9);
            return c * (0.94f + 0.1f * speckle);
        }

        static Color TundraSide(float u, float v)
        {
            var bands = 0.5f + 0.5f * Mathf.Sin((v * 6f + Fbm(u, v, 3, 3, 2) * 1.2f) * Mathf.PI * 2f);
            var c = Color.Lerp(new Color(0.40f, 0.37f, 0.34f), new Color(0.54f, 0.50f, 0.45f), bands);
            return c * (0.9f + 0.2f * Fbm(u, v, 6, 3, 3));
        }

        static Color RockTop(float u, float v)
        {
            Worley(u, v, 24, 3, out var f1, out var f2);
            var edge = Smooth(0f, 0.2f, f2 - f1);
            var tint = Fbm(u, v, 3, 4, 4);
            var c = Color.Lerp(new Color(0.50f, 0.47f, 0.45f), new Color(0.58f, 0.50f, 0.43f), tint);
            var grain = Value(u * 64f, v * 64f, 64, 64, 12);
            return c * (0.9f + 0.1f * (1f - f1) + 0.12f * grain) * Mathf.Lerp(0.94f, 1f, edge);
        }

        static Color RockSide(float u, float v)
        {
            var bands = 0.5f + 0.5f * Mathf.Sin((v * 8f + Fbm(u, v, 3, 3, 5) * 1.5f) * Mathf.PI * 2f);
            var fine = Value(u * 6f, v * 20f, 6, 20, 6);
            var c = Color.Lerp(new Color(0.42f, 0.37f, 0.35f), new Color(0.58f, 0.48f, 0.41f), bands);
            return c * (0.88f + 0.24f * fine);
        }

        static Color IceTop(float u, float v)
        {
            var frost = Smooth(0.5f, 0.7f, Fbm(u, v, 4, 4, 7));
            var c = Color.Lerp(new Color(0.66f, 0.84f, 0.92f), new Color(0.90f, 0.97f, 1f), frost * 0.6f);
            Worley(u, v, 6, 5, out var f1, out var f2);
            var crack = 1f - Smooth(0f, 0.06f, f2 - f1);
            return Color.Lerp(c, new Color(0.97f, 1f, 1f), crack * 0.7f);
        }

        static Color IceSide(float u, float v)
        {
            var streak = Value(u * 14f, v * 3f, 14, 3, 8);
            var c = Color.Lerp(new Color(0.40f, 0.60f, 0.70f), new Color(0.58f, 0.78f, 0.86f), streak);
            var bands = 0.5f + 0.5f * Mathf.Sin((v * 5f + Fbm(u, v, 3, 3, 11) * 1.2f) * Mathf.PI * 2f);
            return Color.Lerp(c, new Color(0.72f, 0.88f, 0.95f), bands * 0.35f);
        }

        static float Smooth(float e0, float e1, float x)
        {
            var t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        // Value noise that tiles every px cells horizontally and py cells vertically.
        static float Value(float x, float y, int px, int py, int seed)
        {
            var xi = Mathf.FloorToInt(x);
            var yi = Mathf.FloorToInt(y);
            var fx = x - xi;
            var fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            var a = Hash(Mod(xi, px), Mod(yi, py), seed);
            var b = Hash(Mod(xi + 1, px), Mod(yi, py), seed);
            var c = Hash(Mod(xi, px), Mod(yi + 1, py), seed);
            var d = Hash(Mod(xi + 1, px), Mod(yi + 1, py), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float u, float v, int frequency, int octaves, int seed)
        {
            float sum = 0f, amplitude = 0.5f, norm = 0f;
            for (var o = 0; o < octaves; o++)
            {
                sum += amplitude * Value(u * frequency, v * frequency, frequency, frequency, seed + o * 17);
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2;
            }
            return sum / norm;
        }

        static void Worley(float u, float v, int cells, int seed, out float f1, out float f2)
        {
            var x = u * cells;
            var y = v * cells;
            var xi = Mathf.FloorToInt(x);
            var yi = Mathf.FloorToInt(y);
            f1 = f2 = 9f;
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var cx = xi + dx;
                var cy = yi + dy;
                var px = cx + Hash(Mod(cx, cells), Mod(cy, cells), seed);
                var py = cy + Hash(Mod(cx, cells), Mod(cy, cells), seed + 1);
                var d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                if (d < f1) { f2 = f1; f1 = d; }
                else if (d < f2) f2 = d;
            }
        }
    }
}
