using System.IO;
using PipeWorld.Landscape;
using PipeWorld.Piper;
using PipeWorld.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace PipeWorld.EditorTools
{
    // Builds the assets and scene objects the Piper systems need. WorldSetup calls this while it rebuilds the World scene.
    public static class PiperSetup
    {
        const string PropMaterialPath = "Assets/Art/Materials/Props.mat";
        const string PropMeshFolder = "Assets/Art/PropMeshes";
        const string PropPrefabFolder = "Assets/Resources/PropPrefabs";
        const string UiFolder = "Assets/UI/default";
        const string SkinFolder = "Assets/Resources/UiSkins";
        const string PanelSettingsPath = "Assets/UI/PipePanelSettings.asset";
        const string ThemePath = "Assets/UI/Theme/UnityDefaultRuntimeTheme.tss";

        static readonly Color32 Steel = new Color32(150, 156, 164, 0);
        static readonly Color32 DarkSteel = new Color32(86, 90, 98, 0);
        static readonly Color32 Crate = new Color32(214, 214, 214, 0);
        static readonly Color32 Hazard = new Color32(232, 176, 48, 0);
        static readonly Color32 Bulb = new Color32(255, 236, 170, 255);
        static readonly Color32 BulbDim = new Color32(80, 74, 56, 0);
        static readonly Color32 Fill = new Color32(110, 230, 120, 255);

        public static void BuildAssets()
        {
            EnsureFolder("Assets/Art", "PropMeshes");
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "PropPrefabs");
            EnsureFolder("Assets/Resources", "UiSkins");

            var material = BuildPropMaterial();
            BuildBuildingMaterials();
            BuildPropPrefabs(material);
            BuildUiAssets();
            AssetDatabase.SaveAssets();
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        static Material BuildPropMaterial()
        {
            var shader = Shader.Find("PipeWorld/Scatter");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PropMaterialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, PropMaterialPath);
            }
            mat.shader = shader;
            mat.SetFloat("_DistanceFade", 0f);
            mat.SetColor("_GlowTint", Color.white);
            mat.SetFloat("_EmissionStrength", 1.4f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void BuildBuildingMaterials()
        {
            EnsureFolder("Assets/Resources", "Materials");

            var building = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/Building.mat");
            if (building == null)
            {
                building = new Material(Shader.Find("PipeWorld/Scatter"));
                AssetDatabase.CreateAsset(building, "Assets/Resources/Materials/Building.mat");
            }
            building.shader = Shader.Find("PipeWorld/Scatter");
            building.SetFloat("_DistanceFade", 0f);
            building.SetColor("_GlowTint", Color.white);
            building.SetFloat("_EmissionStrength", 1.2f);
            EditorUtility.SetDirty(building);

            var gizmo = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/Gizmo.mat");
            if (gizmo == null)
            {
                gizmo = new Material(Shader.Find("PipeWorld/Gizmo"));
                AssetDatabase.CreateAsset(gizmo, "Assets/Resources/Materials/Gizmo.mat");
            }
            gizmo.shader = Shader.Find("PipeWorld/Gizmo");
            gizmo.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(gizmo);
        }

        // ---- Prop prefabs ----

        static void BuildPropPrefabs(Material material)
        {
            BuildCollectable(material);
            BuildDoor(material);
            BuildLamp(material);
            BuildSupplyBox(material);
            BuildBuildingProps(material);
            BuildOtherPlayer(material);
        }

        static void AddBox(MeshBuilder b, Vector3 center, Vector3 size, Color32 color)
        {
            var h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + new Vector3(x * h.x, y * h.y, z * h.z);
            void Face(Vector3 a, Vector3 bb, Vector3 c, Vector3 d, Vector3 normal)
            {
                b.AddTriangle(a, bb, c, 0, color, normal);
                b.AddTriangle(a, c, d, 0, color, normal);
            }
            Face(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), Vector3.forward);
            Face(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), Vector3.back);
            Face(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), Vector3.up);
            Face(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), Vector3.down);
            Face(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), Vector3.right);
            Face(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), Vector3.left);
        }

        static Mesh SaveMesh(MeshBuilder builder, string name)
        {
            var path = $"{PropMeshFolder}/{name}.asset";
            var mesh = builder.ToMesh(name);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static GameObject MakePart(Transform parent, string name, Mesh mesh, Material material, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static void SavePrefab(GameObject root, PropPrototypeId id)
        {
            PrefabUtility.SaveAsPrefabAsset(root, $"{PropPrefabFolder}/{id}.prefab");
            Object.DestroyImmediate(root);
        }

        static void BuildCollectable(Material material)
        {
            var b = new MeshBuilder();
            AddBox(b, new Vector3(0f, 0.175f, 0f), new Vector3(0.5f, 0.35f, 0.35f), Crate);
            AddBox(b, new Vector3(0f, 0.2f, 0f), new Vector3(0.52f, 0.05f, 0.37f), new Color32(120, 120, 120, 0));
            AddBox(b, new Vector3(0f, 0.375f, 0f), new Vector3(0.2f, 0.04f, 0.2f), new Color32(255, 255, 255, 255));
            var mesh = SaveMesh(b, "Collectable");

            var root = new GameObject(nameof(PropPrototypeId.CollectableGeneric));
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.2f, 0f);
            box.size = new Vector3(0.5f, 0.4f, 0.36f);
            root.AddComponent<CollectableGenericBehaviour>();
            SavePrefab(root, PropPrototypeId.CollectableGeneric);
        }

        static void BuildDoor(Material material)
        {
            var root = new GameObject(nameof(PropPrototypeId.DoorSingleItem));
            var behaviour = root.AddComponent<DoorSingleItemBehaviour>();

            var frame = new MeshBuilder();
            AddBox(frame, Vector3.zero, new Vector3(0.2f, 2.5f, 0.3f), DarkSteel);
            var postMesh = SaveMesh(frame, "DoorPost");
            var lintel = new MeshBuilder();
            AddBox(lintel, Vector3.zero, new Vector3(2.4f, 0.25f, 0.3f), DarkSteel);
            var lintelMesh = SaveMesh(lintel, "DoorLintel");
            var panelBuilder = new MeshBuilder();
            AddBox(panelBuilder, Vector3.zero, new Vector3(2f, 2.3f, 0.12f), new Color32(104, 112, 124, 0));
            AddBox(panelBuilder, new Vector3(0f, -0.4f, 0f), new Vector3(1.4f, 0.12f, 0.14f), Hazard);
            var panelMesh = SaveMesh(panelBuilder, "DoorPanel");

            var left = MakePart(root.transform, "PostLeft", postMesh, material, new Vector3(-1.1f, 1.25f, 0f));
            var right = MakePart(root.transform, "PostRight", postMesh, material, new Vector3(1.1f, 1.25f, 0f));
            var top = MakePart(root.transform, "Lintel", lintelMesh, material, new Vector3(0f, 2.625f, 0f));
            var panel = MakePart(root.transform, "Panel", panelMesh, material, new Vector3(0f, 1.15f, 0.23f));
            foreach (var part in new[] { left, right, top, panel })
            {
                part.AddComponent<BoxCollider>();
                var interact = part.AddComponent<IInteractablePassthrough>();
                interact.target = root;
                var tooltip = part.AddComponent<TooltipPassthrough>();
                tooltip.target = root;
            }
            behaviour.panel = panel.transform;
            SavePrefab(root, PropPrototypeId.DoorSingleItem);
        }

        static void BuildLamp(Material material)
        {
            var root = new GameObject(nameof(PropPrototypeId.PlaceableLamp));
            var behaviour = root.AddComponent<PlaceableLampBehavior>();

            var body = new MeshBuilder();
            AddBox(body, new Vector3(0f, 0.05f, 0f), new Vector3(0.45f, 0.1f, 0.45f), DarkSteel);
            AddBox(body, new Vector3(0f, 0.95f, 0f), new Vector3(0.12f, 1.8f, 0.12f), Steel);
            AddBox(body, new Vector3(0f, 2.02f, 0f), new Vector3(0.4f, 0.08f, 0.4f), DarkSteel);
            var bodyMesh = SaveMesh(body, "LampBody");
            var on = new MeshBuilder();
            AddBox(on, Vector3.zero, new Vector3(0.24f, 0.24f, 0.24f), Bulb);
            var onMesh = SaveMesh(on, "LampBulbOn");
            var off = new MeshBuilder();
            AddBox(off, Vector3.zero, new Vector3(0.24f, 0.24f, 0.24f), BulbDim);
            var offMesh = SaveMesh(off, "LampBulbOff");

            root.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.05f, 0f);
            collider.size = new Vector3(0.45f, 2.1f, 0.45f);

            behaviour.bulbOn = MakePart(root.transform, "BulbOn", onMesh, material, new Vector3(0f, 1.86f, 0f));
            behaviour.bulbOff = MakePart(root.transform, "BulbOff", offMesh, material, new Vector3(0f, 1.86f, 0f));

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.86f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.82f, 0.55f);
            light.intensity = 3f;
            light.range = 11f;
            light.shadows = LightShadows.None;
            behaviour.lampLight = light;
            SavePrefab(root, PropPrototypeId.PlaceableLamp);
        }

        static void BuildSupplyBox(Material material)
        {
            var root = new GameObject(nameof(PropPrototypeId.RefreshableBox));
            var behaviour = root.AddComponent<RefreshableBoxBehavior>();

            var body = new MeshBuilder();
            AddBox(body, new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.6f, 0.6f), Crate);
            AddBox(body, new Vector3(0f, 0.62f, 0f), new Vector3(0.94f, 0.06f, 0.64f), Steel);
            AddBox(body, new Vector3(0f, 0.15f, 0f), new Vector3(0.94f, 0.08f, 0.64f), DarkSteel);
            var bodyMesh = SaveMesh(body, "SupplyBoxBody");
            var bar = new MeshBuilder();
            AddBox(bar, new Vector3(0.5f, 0f, 0f), Vector3.one, Fill);
            var barMesh = SaveMesh(bar, "SupplyBoxFill");

            root.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.32f, 0f);
            collider.size = new Vector3(0.94f, 0.66f, 0.64f);

            var fill = MakePart(root.transform, "FillBar", barMesh, material, new Vector3(-0.35f, 0.38f, -0.325f));
            fill.transform.localScale = new Vector3(0.7f, 0.07f, 0.02f);
            behaviour.fillBar = fill.transform;
            behaviour.fillBarFullWidth = 0.7f;
            SavePrefab(root, PropPrototypeId.RefreshableBox);
        }

        // ---- Building props ----
        // Wall props have their origin on the wall's centre line with +Z pointing out of the face they hang on, so the wall is
        // 0.16 thick (z from -0.08 to 0.08). Floor props stand on the origin; the ceiling lamp hangs below it.

        static void BuildingProp<T>(Material material, PropPrototypeId id, System.Action<MeshBuilder> shape, params (Vector3 center, Vector3 size)[] colliders) where T : PropBehaviour
        {
            var b = new MeshBuilder();
            shape(b);
            var mesh = SaveMesh(b, "Building" + id);

            var root = new GameObject(id.ToString());
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            foreach (var (center, size) in colliders)
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = center;
                box.size = size;
            }
            root.AddComponent<T>();
            SavePrefab(root, id);
        }

        static void BuildBuildingProps(Material material)
        {
            var wood = new Color32(150, 108, 72, 0);
            var darkWood = new Color32(110, 80, 56, 0);
            var trim = new Color32(70, 74, 82, 0);
            var glow = new Color32(255, 236, 170, 255);

            BuildingProp<TableBehaviour>(material, PropPrototypeId.Table, b =>
            {
                AddBox(b, new Vector3(0f, 0.71f, 0f), new Vector3(0.95f, 0.08f, 0.95f), wood);
                foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                    AddBox(b, new Vector3(sx * 0.42f, 0.335f, sz * 0.42f), new Vector3(0.08f, 0.67f, 0.08f), darkWood);
            }, (new Vector3(0f, 0.375f, 0f), new Vector3(0.95f, 0.75f, 0.95f)));

            BuildingProp<ChairBehaviour>(material, PropPrototypeId.Chair, b =>
            {
                AddBox(b, new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.06f, 0.45f), wood);
                AddBox(b, new Vector3(0f, 0.7f, 0.2f), new Vector3(0.45f, 0.45f, 0.05f), wood);
                foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                    AddBox(b, new Vector3(sx * 0.19f, 0.21f, sz * 0.19f), new Vector3(0.05f, 0.42f, 0.05f), darkWood);
            }, (new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.9f, 0.45f)));

            BuildingProp<CeilingLampBehaviour>(material, PropPrototypeId.CeilingLamp, b =>
            {
                AddBox(b, new Vector3(0f, -0.1f, 0f), new Vector3(0.05f, 0.2f, 0.05f), trim);
                AddBox(b, new Vector3(0f, -0.25f, 0f), new Vector3(0.5f, 0.1f, 0.5f), trim);
                AddBox(b, new Vector3(0f, -0.32f, 0f), new Vector3(0.4f, 0.05f, 0.4f), glow);
            }, (new Vector3(0f, -0.2f, 0f), new Vector3(0.5f, 0.4f, 0.5f)));

            BuildingProp<PaintingBehaviour>(material, PropPrototypeId.Painting, b =>
            {
                AddBox(b, new Vector3(0f, 0f, 0.1f), new Vector3(0.9f, 0.6f, 0.04f), darkWood);
                AddBox(b, new Vector3(0f, 0f, 0.115f), new Vector3(0.78f, 0.48f, 0.02f), new Color32(92, 140, 170, 0));
                AddBox(b, new Vector3(-0.15f, -0.05f, 0.125f), new Vector3(0.3f, 0.2f, 0.01f), new Color32(220, 170, 90, 0));
            }, (new Vector3(0f, 0f, 0.1f), new Vector3(0.9f, 0.6f, 0.05f)));

            BuildingProp<BuildingDoorBehaviour>(material, PropPrototypeId.BuildingDoor, b =>
            {
                AddBox(b, new Vector3(-0.48f, 1.05f, 0f), new Vector3(0.06f, 2.1f, 0.2f), trim);
                AddBox(b, new Vector3(0.48f, 1.05f, 0f), new Vector3(0.06f, 2.1f, 0.2f), trim);
                AddBox(b, new Vector3(0f, 2.13f, 0f), new Vector3(1.02f, 0.06f, 0.2f), trim);
            }, (new Vector3(0f, 2.13f, 0f), new Vector3(1.02f, 0.06f, 0.2f)));

            BuildingProp<ArchwayBehaviour>(material, PropPrototypeId.Archway, b =>
            {
                AddBox(b, new Vector3(-0.68f, 1.15f, 0f), new Vector3(0.06f, 2.3f, 0.2f), trim);
                AddBox(b, new Vector3(0.68f, 1.15f, 0f), new Vector3(0.06f, 2.3f, 0.2f), trim);
                AddBox(b, new Vector3(0f, 2.33f, 0f), new Vector3(1.42f, 0.06f, 0.2f), trim);
                AddBox(b, new Vector3(-0.55f, 2.2f, 0f), new Vector3(0.2f, 0.2f, 0.2f), trim);
                AddBox(b, new Vector3(0.55f, 2.2f, 0f), new Vector3(0.2f, 0.2f, 0.2f), trim);
            }, (new Vector3(0f, 2.33f, 0f), new Vector3(1.42f, 0.06f, 0.2f)));

            // The exterior door is 0.4 thick against a 0.16 wall, so it stands proud of the wall on both sides.
            BuildingProp<ExteriorDoorBehaviour>(material, PropPrototypeId.ExteriorDoor, b =>
            {
                AddBox(b, new Vector3(-0.525f, 1.05f, 0f), new Vector3(0.1f, 2.1f, 0.4f), trim);
                AddBox(b, new Vector3(0.525f, 1.05f, 0f), new Vector3(0.1f, 2.1f, 0.4f), trim);
                AddBox(b, new Vector3(0f, 2.05f, 0f), new Vector3(1.1f, 0.1f, 0.4f), trim);
                AddBox(b, new Vector3(0f, 1.0f, 0f), new Vector3(0.96f, 2.0f, 0.3f), new Color32(60, 150, 90, 0));
                AddBox(b, new Vector3(0.3f, 1.0f, 0.18f), new Vector3(0.08f, 0.08f, 0.06f), Hazard);
                AddBox(b, new Vector3(0.3f, 1.0f, -0.18f), new Vector3(0.08f, 0.08f, 0.06f), Hazard);
            }, (new Vector3(0f, 1.05f, 0f), new Vector3(1.1f, 2.1f, 0.4f)));

            BuildingProp<TerminalBehaviour>(material, PropPrototypeId.Terminal, b =>
            {
                AddBox(b, new Vector3(0f, 0.6f, 0f), new Vector3(0.7f, 1.2f, 0.5f), new Color32(78, 84, 96, 0));
                AddBox(b, new Vector3(0f, 0.85f, -0.26f), new Vector3(0.5f, 0.4f, 0.02f), new Color32(110, 230, 150, 255));
                AddBox(b, new Vector3(0f, 0.55f, -0.3f), new Vector3(0.5f, 0.06f, 0.12f), trim);
            }, (new Vector3(0f, 0.6f, 0f), new Vector3(0.7f, 1.2f, 0.5f)));
        }

        static void BuildOtherPlayer(Material material)
        {
            var bodyBuilder = new MeshBuilder();
            AddBox(bodyBuilder, new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 1f, 0.3f), new Color32(90, 150, 220, 0));
            var bodyMesh = SaveMesh(bodyBuilder, "OtherPlayerBody");
            var headBuilder = new MeshBuilder();
            AddBox(headBuilder, Vector3.zero, new Vector3(0.32f, 0.32f, 0.32f), new Color32(230, 200, 170, 0));
            AddBox(headBuilder, new Vector3(0f, 0.04f, 0.17f), new Vector3(0.26f, 0.08f, 0.04f), new Color32(255, 255, 255, 255));
            var headMesh = SaveMesh(headBuilder, "OtherPlayerHead");

            var root = new GameObject("OtherPlayer");
            var behaviour = root.AddComponent<OtherPlayerBehaviour>();
            behaviour.body = MakePart(root.transform, "Body", bodyMesh, material, Vector3.zero).transform;
            behaviour.head = MakePart(root.transform, "Head", headMesh, material, new Vector3(0f, 1.4f, 0f)).transform;

            var label = new GameObject("Name");
            label.transform.SetParent(root.transform, false);
            var text = label.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.1f;
            text.fontSize = 48;
            text.color = Color.white;
            behaviour.nameLabel = text;

            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/OtherPlayer.prefab");
            Object.DestroyImmediate(root);
        }

        // ---- UI assets ----

        static void BuildUiAssets()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 1f;
            EditorUtility.SetDirty(settings);

            BuildSkin(UiSurface.Hud, "theme", "elements", "hud", "tooltips");
            BuildSkin(UiSurface.MainMenu, "theme", "menu-components", "menu-main", "menu-pipeworld");
            BuildSkin(UiSurface.EscapeMenu, "theme", "menu-components", "menu-escape");
        }

        static void BuildSkin(UiSurface surface, params string[] sheetNames)
        {
            var path = $"{SkinFolder}/{surface}Default.asset";
            var skin = AssetDatabase.LoadAssetAtPath<UiSkin>(path);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<UiSkin>();
                AssetDatabase.CreateAsset(skin, path);
            }

            skin.surface = surface;
            skin.sheets = new StyleSheet[sheetNames.Length];
            for (var i = 0; i < sheetNames.Length; i++)
            {
                skin.sheets[i] = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{UiFolder}/{sheetNames[i]}.uss");
                if (skin.sheets[i] == null)
                    Debug.LogError($"[PiperSetup] missing stylesheet {UiFolder}/{sheetNames[i]}.uss");
            }
            EditorUtility.SetDirty(skin);
        }

        // ---- Scene objects ----

        public static void AddToScene(PlayerController player)
        {
            player.gameObject.AddComponent<PlayerRaycaster>();

            var piper = new GameObject("Piper");
            piper.AddComponent<PiperMain>();
            piper.AddComponent<PiperGame>();

            var ui = new GameObject("UI");
            var holder = ui.AddComponent<ScreenUIHolder>();
            holder.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            ui.AddComponent<GameHud>();
            ui.AddComponent<MainMenu>();
            ui.AddComponent<EscapeMenu>();
            ui.AddComponent<EventSystem>();
            ui.AddComponent<InputSystemUIInputModule>();

            var interaction = new GameObject("Interaction Modes");
            var selector = interaction.AddComponent<InteractionModeSelector>();
            var superTool = interaction.AddComponent<SuperTool>();
            var finger = interaction.AddComponent<FingerInteraction>();
            var buildingPlacer = interaction.AddComponent<BuildingPlacerInteraction>();
            var objectPlacer = interaction.AddComponent<ObjectPlacerInteraction>();
            var buildingEdit = interaction.AddComponent<BuildingEditInteraction>();
            superTool.controlledModes = new PipeWorld.Piper.InteractionMode[] { finger };
            selector.allModes = new System.Collections.Generic.List<PipeWorld.Piper.InteractionMode> { superTool, finger, buildingPlacer, objectPlacer, buildingEdit };
        }
    }
}
