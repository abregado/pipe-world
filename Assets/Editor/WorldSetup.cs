using System.IO;
using PipeWorld.Landscape;
using PipeWorld.Player;
using PipeWorld.Tools;
using PipeWorld.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PipeWorld.EditorTools
{
    public static class WorldSetup
    {
        const string RpAssetPath = "Assets/Settings/PC_RPAsset.asset";
        const string LutPath = "Assets/Art/LUTs/NightLUT.png";
        const string WarmLutPath = "Assets/Art/LUTs/WarmLUT.png";
        const string ProfilePath = "Assets/Settings/WorldVolumeProfile.asset";
        const string SkyMatPath = "Assets/Art/Materials/Skybox.mat";
        const string ScenePath = "Assets/Scenes/World.unity";
        const int LutSize = 32;

        [MenuItem("PipeWorld/Setup Pipeline, Lighting and Post")]
        public static void Run()
        {
            ConfigureUrpAsset();
            ConfigureSsao();
            var nightLut = BuildLut(LutPath, NightGrade);
            var warmLut = BuildLut(WarmLutPath, WarmGrade);
            var profile = BuildVolumeProfile();
            var sky = BuildSkyMaterial();
            ConfigureEdgeHighlight();
            ConfigureLightGrade(nightLut, warmLut);
            BuildScene(profile, sky);
            AssetDatabase.SaveAssets();
            Debug.Log("[WorldSetup] Done.");
        }

        static void ConfigureUrpAsset()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/Settings/PC_Renderer.asset");
            var rendererSo = new SerializedObject(rendererData);
            rendererSo.FindProperty("m_RenderingMode").intValue = 2; // Forward+
            rendererSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);

            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RpAssetPath);
            rp.supportsHDR = true;
            // 64-bit is RGBA16F: the default B10G11R11 has no alpha, and alpha carries the light mask to the LightGrade pass.
            rp.hdrColorBufferPrecision = HDRColorBufferPrecision._64Bits;
            rp.msaaSampleCount = 4;
            var so = new SerializedObject(rp);
            so.FindProperty("m_SoftShadowsSupported").boolValue = true;
            // LightGrade runs after post-processing; without alpha processing the post pass overwrites the light mask with 1.
            so.FindProperty("m_AllowPostProcessAlphaOutput").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            rp.shadowDistance = 220f;
            rp.shadowCascadeCount = 4;
            rp.mainLightShadowmapResolution = 4096;
            rp.shadowDepthBias = 1f;
            rp.shadowNormalBias = 1f;
            rp.maxAdditionalLightsCount = 8;
            so = new SerializedObject(rp);
            // The enum is PerPixel = 1, PerVertex = 2. Per-vertex disables additional-light shadows even under Forward+.
            so.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            so.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = 4096;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rp);
        }

        // The terrain and scatter shaders write depth but no normals, so SSAO reads depth only.
        // It runs after opaques because the custom shaders never sample the occlusion texture themselves.
        static void ConfigureSsao()
        {
            const string rendererPath = "Assets/Settings/PC_Renderer.asset";
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(rendererPath))
            {
                if (asset == null || asset.GetType().Name != "ScreenSpaceAmbientOcclusion")
                    continue;
                var so = new SerializedObject(asset);
                so.FindProperty("m_Settings.Source").enumValueIndex = 0;
                so.FindProperty("m_Settings.AfterOpaque").boolValue = true;
                so.FindProperty("m_Settings.Intensity").floatValue = 0.9f;
                so.FindProperty("m_Settings.Radius").floatValue = 0.6f;
                so.FindProperty("m_Settings.DirectLightingStrength").floatValue = 0.15f;
                // Blue noise switches to a new noise texture at a random offset every frame, which is meant to be resolved
                // by temporal anti-aliasing. There is none here, so it crawls. Interleaved gradient noise is fixed per pixel;
                // 12 samples, high-quality reconstructed normals and the bilateral blur keep it smooth.
                so.FindProperty("m_Settings.AOMethod").enumValueIndex = 1;
                so.FindProperty("m_Settings.Samples").enumValueIndex = 0;
                so.FindProperty("m_Settings.NormalSamples").enumValueIndex = 2;
                so.FindProperty("m_Settings.BlurQuality").enumValueIndex = 0;
                so.FindProperty("m_Settings.Downsample").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }
        }

        // Adds (once) a full-screen pass that brightens depth discontinuities and creases before post-processing.
        static void ConfigureEdgeHighlight()
        {
            Directory.CreateDirectory("Assets/Art/Materials");
            var material = LoadOrCreateMaterial("Assets/Art/Materials/EdgeHighlight.mat", "PipeWorld/EdgeHighlight");
            material.SetColor("_EdgeColor", new Color(1f, 0.96f, 0.88f));
            material.SetFloat("_Strength", 0.28f);
            material.SetFloat("_Threshold", 0.05f);
            material.SetFloat("_Thickness", 1f);
            material.SetFloat("_FadeStart", 40f);
            material.SetFloat("_FadeEnd", 250f);
            EditorUtility.SetDirty(material);

            var feature = EnsureRendererFeature<FullScreenPassRendererFeature>("EdgeHighlight");
            feature.passMaterial = material;
            feature.passIndex = 0;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth;
            EditorUtility.SetDirty(feature);
            AssetDatabase.SaveAssets();
        }

        // Returns the renderer's feature of type T, adding it as a sub-asset of PC_Renderer the first time.
        static T EnsureRendererFeature<T>(string featureName) where T : ScriptableRendererFeature
        {
            const string rendererPath = "Assets/Settings/PC_Renderer.asset";
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(rendererPath))
                if (asset is T existing && existing.name == featureName)
                    return existing;

            var feature = ScriptableObject.CreateInstance<T>();
            feature.name = featureName;
            AssetDatabase.AddObjectToAsset(feature, data);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            var index = features.arraySize;
            features.arraySize = index + 1;
            features.GetArrayElementAtIndex(index).objectReferenceValue = feature;
            map.arraySize = index + 1;
            map.GetArrayElementAtIndex(index).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return feature;
        }

        // The Dear ImGui component draws through a renderer feature and reads three default assets shipped in the package.
        static void AddDearImGui(Camera camera)
        {
            const string packageResources = "Packages/com.psydack.uimgui/Resources/";
            var feature = EnsureRendererFeature<global::UImGui.Renderer.RenderImGui>("Dear ImGui");
            // Drawn just after the LightGrade pass so the debug window is never colour-graded.
            feature.RenderPassEvent = RenderPassEvent.AfterRenderingPostProcessing + 10;
            EditorUtility.SetDirty(feature);

            var go = new GameObject("Dear ImGui");
            var imGui = go.AddComponent<global::UImGui.UImGui>();
            var so = new SerializedObject(imGui);
            so.FindProperty("_camera").objectReferenceValue = camera;
            so.FindProperty("_renderFeature").objectReferenceValue = feature;
            so.FindProperty("_shaders").objectReferenceValue = AssetDatabase.LoadMainAssetAtPath(packageResources + "DefaultShader.asset");
            so.FindProperty("_style").objectReferenceValue = AssetDatabase.LoadMainAssetAtPath(packageResources + "DefaultStyle.asset");
            so.FindProperty("_cursorShapes").objectReferenceValue = AssetDatabase.LoadMainAssetAtPath(packageResources + "DefaultCursorShape.asset");
            so.FindProperty("_iniSettings").objectReferenceValue = LoadOrCreateImGuiIniAsset();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // With an ini asset assigned, ImGui keeps its layout in memory instead of writing imgui.ini into the working directory.
        // The asset type is internal to the package, so it is created by name.
        static Object LoadOrCreateImGuiIniAsset()
        {
            const string path = "Assets/Settings/ImGuiIni.asset";
            var type = typeof(global::UImGui.UImGui).Assembly.GetType("UImGui.Assets.IniSettingsAsset");
            var existing = AssetDatabase.LoadAssetAtPath(path, type);
            if (existing != null)
                return existing;

            var created = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        // Dusky plum shadows, amber highlights: the global night grade.
        static Color NightGrade(Color c)
        {
            var shadowTint = new Color(0.55f, 0.28f, 0.50f);
            var highlightTint = new Color(1.00f, 0.66f, 0.34f);
            float l = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
            var desat = Color.Lerp(new Color(l, l, l), c, 0.5f);
            var tint = Color.Lerp(shadowTint, highlightTint, Mathf.SmoothStep(0f, 1f, l));
            var o = new Color(desat.r * tint.r, desat.g * tint.g, desat.b * tint.b);
            return Color.Lerp(desat, o * 1.7f, 0.85f) * 0.85f;
        }

        // Applied near warm lights in place of the night grade: richer, brighter, and amber throughout.
        static Color WarmGrade(Color c)
        {
            var shadowTint = new Color(0.96f, 0.72f, 0.56f);
            var highlightTint = new Color(1.00f, 0.90f, 0.70f);
            float l = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
            var saturated = Color.Lerp(new Color(l, l, l), c, 1.2f);
            var tint = Color.Lerp(shadowTint, highlightTint, Mathf.SmoothStep(0f, 1f, l));
            return new Color(saturated.r * tint.r, saturated.g * tint.g, saturated.b * tint.b) * 1.12f;
        }

        static Texture2D BuildLut(string path, System.Func<Color, Color> grade)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tex = new Texture2D(LutSize * LutSize, LutSize, TextureFormat.RGBA32, false, true);
            for (int b = 0; b < LutSize; b++)
            for (int g = 0; g < LutSize; g++)
            for (int r = 0; r < LutSize; r++)
            {
                var o = grade(new Color(r / (LutSize - 1f), g / (LutSize - 1f), b / (LutSize - 1f)));
                o.r = Mathf.Clamp01(o.r);
                o.g = Mathf.Clamp01(o.g);
                o.b = Mathf.Clamp01(o.b);
                o.a = 1f;
                tex.SetPixel(b * LutSize + r, g, o);
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = false;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Replaces the volume's Color Lookup: the pass owns the night grade so lights can override it locally.
        static void ConfigureLightGrade(Texture2D nightLut, Texture2D warmLut)
        {
            Directory.CreateDirectory("Assets/Art/Materials");
            var material = LoadOrCreateMaterial("Assets/Art/Materials/LightGrade.mat", "PipeWorld/LightGrade");
            material.SetTexture("_NightLut", nightLut);
            material.SetTexture("_WarmLut", warmLut);
            material.SetFloat("_LightGradeStrength", 1f);
            EditorUtility.SetDirty(material);

            var feature = EnsureRendererFeature<FullScreenPassRendererFeature>("LightGrade");
            feature.passMaterial = material;
            feature.passIndex = 0;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth;
            EditorUtility.SetDirty(feature);
            AssetDatabase.SaveAssets();
        }

        static VolumeProfile BuildVolumeProfile()
        {
            AssetDatabase.DeleteAsset(ProfilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.25f);
            bloom.scatter.Override(0.6f);

            var adj = profile.Add<ColorAdjustments>(true);
            adj.contrast.Override(8f);
            adj.saturation.Override(6f);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.18f);
            vig.smoothness.Override(0.5f);

            var toning = profile.Add<SplitToning>(true);
            toning.shadows.Override(new Color(0.78f, 0.78f, 0.88f));
            toning.highlights.Override(new Color(1f, 0.95f, 0.87f));
            toning.balance.Override(0f);

            var dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            // Starts far out so the lowland beyond the open side stays readable; only the far hills soften.
            dof.gaussianStart.Override(900f);
            dof.gaussianEnd.Override(2600f);
            dof.gaussianMaxRadius.Override(1f);

            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static Material BuildSkyMaterial()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SkyMatPath));
            var shader = Shader.Find("PipeWorld/GradientSky");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, SkyMatPath);
            }
            mat.shader = shader;
            mat.SetFloat("_Bands", 10f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material[] BuildTerrainMaterials()
        {
            Directory.CreateDirectory("Assets/Art/Materials");
            var sets = new[]
            {
                (name: "Tundra", textures: TerrainTextureGenerator.GenerateTundra(), top: new Color(1f, 1f, 1f), side: new Color(1f, 1f, 1f)),
                (name: "Rock", textures: TerrainTextureGenerator.GenerateRock(), top: new Color(1f, 1f, 1f), side: new Color(1f, 1f, 1f)),
                (name: "Ice", textures: TerrainTextureGenerator.GenerateIce(), top: new Color(1f, 1f, 1f), side: new Color(1f, 1f, 1f)),
                (name: "Grass", textures: TerrainTextureGenerator.GenerateGrass(), top: new Color(1f, 1f, 1f), side: new Color(1f, 1f, 1f)),
            };

            var materials = new Material[sets.Length];
            for (var i = 0; i < sets.Length; i++)
            {
                var path = $"Assets/Art/Materials/Terrain_{sets[i].name}.mat";
                var mat = LoadOrCreateMaterial(path, "PipeWorld/TriplanarTerrain");
                mat.SetTexture("_TopTex", sets[i].textures[0]);
                mat.SetTexture("_SideTex", sets[i].textures[1]);
                mat.SetColor("_TopTint", sets[i].top);
                mat.SetColor("_SideTint", sets[i].side);
                mat.SetFloat("_TopScale", 14f);
                mat.SetFloat("_SideScale", 10f);
                mat.SetFloat("_WalkableCos", Walkable.MinNormalY);
                mat.SetFloat("_BlockedDarken", 0.9f);
                mat.SetFloat("_DetailFadeStart", 100f);
                mat.SetFloat("_DetailFadeEnd", 380f);
                EditorUtility.SetDirty(mat);
                materials[i] = mat;
            }
            return materials;
        }

        static Material BuildScatterMaterial()
        {
            var mat = LoadOrCreateMaterial("Assets/Art/Materials/Scatter.mat", "PipeWorld/Scatter");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material LoadOrCreateMaterial(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            return mat;
        }

        static void BuildScene(VolumeProfile profile, Material sky)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(0f, 0.05f, 0f);
            var body = playerGo.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.4f;
            body.center = new Vector3(0f, 0.9f, 0f);
            body.stepOffset = 0.4f;
            body.slopeLimit = Walkable.SlopeLimitDegrees;
            var player = playerGo.AddComponent<PlayerController>();
            playerGo.AddComponent<PlayerLantern>();

            var pivotGo = new GameObject("Camera Pivot");
            pivotGo.transform.SetParent(playerGo.transform, false);
            pivotGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            player.cameraPivot = pivotGo.transform;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(pivotGo.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.15f;
            // The lowland beyond the open side reaches 2600 m.
            cam.farClipPlane = 3200f;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.requiresDepthTexture = true;

            var rig = new GameObject("Screenshot Rig").AddComponent<ScreenshotRig>();
            var litTest = LoadOrCreateMaterial("Assets/Art/Materials/LitTest.mat", "Universal Render Pipeline/Lit");
            litTest.SetColor("_BaseColor", new Color(0.75f, 0.75f, 0.75f));
            EditorUtility.SetDirty(litTest);
            rig.litTestMaterial = litTest;
            AddDearImGui(cam);

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;

            var volGo = new GameObject("Global Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            var cycleGo = new GameObject("Day Night Cycle");
            var cycle = cycleGo.AddComponent<DayNightCycle>();
            cycle.sun = sun;
            cycle.skyboxSource = sky;

            var world = new GameObject("Demo World").AddComponent<DemoWorld>();
            world.terrainMaterials = BuildTerrainMaterials();
            world.scatterMaterial = BuildScatterMaterial();
            world.grassMaterial = LoadOrCreateMaterial("Assets/Art/Materials/Grass.mat", "PipeWorld/Grass");
            EditorUtility.SetDirty(world.grassMaterial);

            // Same shader as the scatter, but glowing in the flame's own colour and much brighter.
            world.flameMaterial = LoadOrCreateMaterial("Assets/Art/Materials/Flame.mat", "PipeWorld/Scatter");
            world.flameMaterial.SetColor("_GlowTint", Color.white);
            world.flameMaterial.SetFloat("_EmissionStrength", 3f);
            world.flameMaterial.SetFloat("_DistanceFade", 0f);
            EditorUtility.SetDirty(world.flameMaterial);

            var gradeSystem = new GameObject("Light Grade").AddComponent<LightGradeSystem>();

            var menu = new GameObject("Debug Menu").AddComponent<DebugMenu>();
            menu.cycle = cycle;
            menu.player = player;
            menu.world = world;
            menu.gradeSystem = gradeSystem;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
