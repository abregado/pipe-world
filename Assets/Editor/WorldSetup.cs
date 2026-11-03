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
        const string ProfilePath = "Assets/Settings/WorldVolumeProfile.asset";
        const string SkyMatPath = "Assets/Art/Materials/Skybox.mat";
        const string ScenePath = "Assets/Scenes/World.unity";
        const int LutSize = 32;

        [MenuItem("PipeWorld/Setup Pipeline, Lighting and Post")]
        public static void Run()
        {
            ConfigureUrpAsset();
            ConfigureSsao();
            var lut = BuildNightLut();
            var profile = BuildVolumeProfile(lut);
            var sky = BuildSkyMaterial();
            ConfigureEdgeHighlight();
            BuildScene(profile, sky);
            AssetDatabase.SaveAssets();
            Debug.Log("[WorldSetup] Done.");
        }

        static void ConfigureUrpAsset()
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RpAssetPath);
            rp.supportsHDR = true;
            rp.msaaSampleCount = 4;
            var so = new SerializedObject(rp);
            so.FindProperty("m_SoftShadowsSupported").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            rp.shadowDistance = 220f;
            rp.shadowCascadeCount = 4;
            rp.mainLightShadowmapResolution = 4096;
            rp.shadowDepthBias = 1f;
            rp.shadowNormalBias = 1f;
            rp.maxAdditionalLightsCount = 8;
            so = new SerializedObject(rp);
            so.FindProperty("m_AdditionalLightsRenderingMode").intValue = 2;
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
            material.SetFloat("_Strength", 0.35f);
            material.SetFloat("_Threshold", 0.05f);
            material.SetFloat("_Thickness", 1f);
            material.SetFloat("_FadeStart", 120f);
            material.SetFloat("_FadeEnd", 500f);
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
                if (asset is T existing)
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

        static Texture2D BuildNightLut()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LutPath));
            var tex = new Texture2D(LutSize * LutSize, LutSize, TextureFormat.RGBA32, false, true);
            var shadowTint = new Color(0.55f, 0.28f, 0.50f);
            var highlightTint = new Color(1.00f, 0.66f, 0.34f);
            for (int b = 0; b < LutSize; b++)
            for (int g = 0; g < LutSize; g++)
            for (int r = 0; r < LutSize; r++)
            {
                var c = new Color(r / (LutSize - 1f), g / (LutSize - 1f), b / (LutSize - 1f));
                float l = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
                var grey = new Color(l, l, l);
                var desat = Color.Lerp(grey, c, 0.5f);
                var tint = Color.Lerp(shadowTint, highlightTint, Mathf.SmoothStep(0f, 1f, l));
                var o = new Color(desat.r * tint.r, desat.g * tint.g, desat.b * tint.b);
                o = Color.Lerp(desat, o * 1.7f, 0.85f) * 0.85f;
                o.a = 1f;
                tex.SetPixel(b * LutSize + r, g, o);
            }
            tex.Apply();
            File.WriteAllBytes(LutPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(LutPath);

            var imp = (TextureImporter)AssetImporter.GetAtPath(LutPath);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = false;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(LutPath);
        }

        static VolumeProfile BuildVolumeProfile(Texture2D lut)
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
            dof.gaussianStart.Override(400f);
            dof.gaussianEnd.Override(1500f);
            dof.gaussianMaxRadius.Override(1f);

            var look = profile.Add<ColorLookup>(true);
            look.texture.Override(lut);
            look.contribution.Override(0f);

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
                mat.SetFloat("_DetailFadeStart", 150f);
                mat.SetFloat("_DetailFadeEnd", 520f);
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

            var pivotGo = new GameObject("Camera Pivot");
            pivotGo.transform.SetParent(playerGo.transform, false);
            pivotGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            player.cameraPivot = pivotGo.transform;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(pivotGo.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 2500f;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.requiresDepthTexture = true;

            new GameObject("Screenshot Rig").AddComponent<ScreenshotRig>();
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
            cycle.volume = vol;
            cycle.skyboxSource = sky;

            var world = new GameObject("Demo World").AddComponent<DemoWorld>();
            world.terrainMaterials = BuildTerrainMaterials();
            world.scatterMaterial = BuildScatterMaterial();
            world.grassMaterial = LoadOrCreateMaterial("Assets/Art/Materials/Grass.mat", "PipeWorld/Grass");
            EditorUtility.SetDirty(world.grassMaterial);

            var menu = new GameObject("Debug Menu").AddComponent<DebugMenu>();
            menu.cycle = cycle;
            menu.player = player;
            menu.world = world;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
