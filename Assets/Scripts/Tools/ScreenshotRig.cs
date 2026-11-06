using System;
using System.Collections;
using System.Globalization;
using PipeWorld.Player;
using PipeWorld.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PipeWorld.Tools
{
    public class ScreenshotRig : MonoBehaviour
    {
        // URP's own Lit material, used by -shotLitTest to check shadows independently of the custom shaders.
        public Material litTestMaterial;

        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            var path = Arg(args, "-shot");
            if (path == null) yield break;

            var timeArg = Arg(args, "-shotTime");
            var cycle = FindAnyObjectByType<DayNightCycle>();
            if (cycle != null && timeArg != null)
            {
                cycle.timeOfDay = Float(timeArg);
                cycle.paused = true;
            }

            var rangeArg = Arg(args, "-shotShadowRange");
            if (rangeArg != null)
                foreach (var flicker in FindObjectsByType<PipeWorld.Landscape.TorchFlicker>())
                    flicker.shadowDistance = Float(rangeArg);

            var disableFeature = Arg(args, "-shotDisableFeature");
            if (disableFeature != null)
                SetRendererFeaturesActive(disableFeature, false);

            if (Array.IndexOf(args, "-shotLightTest") >= 0 && litTestMaterial != null)
            {
                // A from-scratch point light and cube over a plain URP Lit plane, far from the torches.
                var centre = new Vector3(32f, 0.06f, 0f);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.transform.position = centre;
                ground.transform.localScale = new Vector3(2f, 1f, 2f);
                ground.GetComponent<Renderer>().sharedMaterial = litTestMaterial;
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.position = centre + new Vector3(0f, 1.5f, 0f);
                cube.transform.localScale = new Vector3(2f, 3f, 2f);
                cube.GetComponent<Renderer>().sharedMaterial = litTestMaterial;
                // One cube per custom material beside the Lit one, so a missing shadow can be pinned to a caster shader.
                var world = FindAnyObjectByType<PipeWorld.Landscape.DemoWorld>();
                if (world != null)
                {
                    var scatterCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    scatterCube.transform.position = centre + new Vector3(0f, 1.5f, 5f);
                    scatterCube.transform.localScale = new Vector3(2f, 3f, 2f);
                    scatterCube.GetComponent<Renderer>().sharedMaterial = world.scatterMaterial;
                    var terrainCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    terrainCube.transform.position = centre + new Vector3(0f, 1.5f, -5f);
                    terrainCube.transform.localScale = new Vector3(2f, 3f, 2f);
                    terrainCube.GetComponent<Renderer>().sharedMaterial = world.terrainMaterials[0];
                }
                var lamp = new GameObject("Test Lamp").AddComponent<Light>();
                lamp.transform.position = centre + new Vector3(-4f, 4f, -3f);
                lamp.type = LightType.Point;
                lamp.range = 30f;
                lamp.intensity = 40f;
                lamp.shadows = LightShadows.Hard;
            }

            var lightScaleArg = Arg(args, "-shotLightScale");
            if (lightScaleArg != null)
                foreach (var flicker in FindObjectsByType<PipeWorld.Landscape.TorchFlicker>())
                    flicker.baseIntensity *= Float(lightScaleArg);

            if (Array.IndexOf(args, "-shotHardShadows") >= 0)
                foreach (var flicker in FindObjectsByType<PipeWorld.Landscape.TorchFlicker>())
                    flicker.shadowType = LightShadows.Hard;

            if (Array.IndexOf(args, "-shotLitTest") >= 0 && litTestMaterial != null)
            {
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.transform.position = new Vector3(4.5f, 0.08f, 6f);
                plane.transform.localScale = new Vector3(3f, 1f, 3f);
                plane.GetComponent<Renderer>().sharedMaterial = litTestMaterial;
                Destroy(plane.GetComponent<Collider>());
            }

            if (Array.IndexOf(args, "-shotLightsOnly") >= 0)
                Shader.SetGlobalFloat("_PW_DebugLights", 1f);
            var debugViewArg = Arg(args, "-shotDebugView");
            if (debugViewArg != null)
                Shader.SetGlobalFloat("_PW_DebugLights", Float(debugViewArg));

            var ambientArg = Arg(args, "-shotAmbient");
            if (cycle != null && ambientArg != null)
                cycle.ambientScale = Float(ambientArg);

            var gradeArg = Arg(args, "-shotGrade");
            var gradeSystem = FindAnyObjectByType<LightGradeSystem>();
            if (gradeSystem != null && gradeArg != null)
                gradeSystem.globalStrength = Float(gradeArg);
            if (gradeSystem != null && Array.IndexOf(args, "-shotNoShadowedGrade") >= 0)
                gradeSystem.shadowedGrade = false;

            var keysArg = Arg(args, "-shotKeys");
            Keyboard rigKeyboard = null;
            Key[] keys = null;
            if (keysArg != null)
            {
                keys = Array.ConvertAll(keysArg.Split(','), k => (Key)Enum.Parse(typeof(Key), k, true));
                rigKeyboard = InputSystem.AddDevice<Keyboard>();
            }

            var player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                var pos = Floats(Arg(args, "-shotPos"));
                var look = Floats(Arg(args, "-shotLook"));
                if (pos != null)
                    player.Teleport(new Vector3(pos[0], pos[1], pos[2]), look != null ? look[0] : 0f, look != null ? look[1] : 0f);
                // Disabling releases the cursor lock so an unattended run never grabs the mouse.
                if (keys == null) player.enabled = false;
            }

            var respawn = Arg(args, "-shotRespawn");
            if (respawn != null)
            {
                var debugMenu = FindAnyObjectByType<DebugMenu>();
                if (respawn == "outer") debugMenu.RespawnInsideOuterWall(); else if (respawn == "edge") debugMenu.RespawnAtOpenEdge(); else debugMenu.RespawnAtCentre();
                Debug.Log($"[ShotRig] respawned at {player.transform.position}");
            }

            if (Array.IndexOf(args, "-shotMenu") >= 0)
            {
                var menu = FindAnyObjectByType<DebugMenu>();
                if (menu != null)
                    menu.Visible = true;
            }

            var frames = Arg(args, "-shotFrames") is { } f ? int.Parse(f, CultureInfo.InvariantCulture) : 30;
            var measureStart = 0f;
            for (var i = 0; i < frames; i++)
            {
                if (i == 10) measureStart = Time.realtimeSinceStartup;
                yield return null;
            }
            if (frames > 20)
                Debug.Log($"[ShotRig] average frame {(Time.realtimeSinceStartup - measureStart) / (frames - 10) * 1000f:F1} ms");

            var click = Floats(Arg(args, "-shotClick"));
            if (click != null)
            {
                // Screen pixels, origin bottom-left. A virtual mouse becomes Mouse.current, which is what Dear ImGui reads.
                var mouse = InputSystem.AddDevice<Mouse>();
                var point = new Vector2(click[0], click[1]);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                for (var i = 0; i < 5; i++) yield return null;
            }

            var maxY = float.NegativeInfinity;
            if (keys != null)
            {
                var holdSeconds = Arg(args, "-shotHold") is { } h ? Float(h) : 1.5f;
                var end = Time.time + holdSeconds;
                while (Time.time < end)
                {
                    InputSystem.QueueStateEvent(rigKeyboard, new KeyboardState(keys));
                    if (player != null) maxY = Mathf.Max(maxY, player.transform.position.y);
                    yield return null;
                }
            }

            if (keys != null && player != null)
            {
                Debug.Log($"[ShotRig] player position {player.transform.position} maxY {maxY} crouching {player.IsCrouching}");
                player.enabled = false;
            }

            yield return new WaitForEndOfFrame();
            if (Array.IndexOf(args, "-shotProbe") >= 0)
                LogShadowState();
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            Application.Quit();
        }

        // The renderer list is not public API, so it is read by reflection. "all" targets every feature.
        // Logs the global shadow state URP left after the last frame, to tell missing shadow maps from unused ones.
        static void LogShadowState()
        {
            foreach (var name in new[] { "_MAIN_LIGHT_SHADOWS", "_MAIN_LIGHT_SHADOWS_CASCADE", "_ADDITIONAL_LIGHT_SHADOWS", "_SHADOWS_SOFT", "_SHADOWS_SOFT_HIGH", "_CLUSTER_LIGHT_LOOP", "_ADDITIONAL_LIGHTS" })
                Debug.Log($"[ShotRig] keyword {name} {Shader.IsKeywordEnabled(name)}");
            foreach (var name in new[] { "_MainLightShadowmapTexture", "_AdditionalLightsShadowmapTexture" })
            {
                var tex = Shader.GetGlobalTexture(name);
                Debug.Log($"[ShotRig] {name} {(tex != null ? $"{tex.name} {tex.width}x{tex.height}" : "null")}");
            }
            var shadowParams = Shader.GetGlobalVectorArray("_AdditionalShadowParams");
            if (shadowParams != null)
                for (var i = 0; i < Mathf.Min(shadowParams.Length, 24); i++)
                    Debug.Log($"[ShotRig] _AdditionalShadowParams[{i}] {shadowParams[i]}");
            else
                Debug.Log("[ShotRig] _AdditionalShadowParams null");
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset rp)
                Debug.Log($"[ShotRig] pipeline {rp.name} hdr {rp.supportsHDR} precision {rp.hdrColorBufferPrecision} msaa {rp.msaaSampleCount}, camera hdr {Camera.main?.allowHDR}");
            var lights = FindObjectsByType<Light>();
            var shadowed = 0;
            foreach (var l in lights)
                if (l.enabled && l.type != LightType.Directional && l.shadows != LightShadows.None) shadowed++;
            Debug.Log($"[ShotRig] lights {lights.Length}, enabled punctual with shadows {shadowed}");
        }

        static void SetRendererFeaturesActive(string featureName, bool active)
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            var field = pipeline.GetType().GetField("m_RendererDataList", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (!(field?.GetValue(pipeline) is UnityEngine.Rendering.Universal.ScriptableRendererData[] rendererDataList))
                return;

            foreach (var data in rendererDataList)
            foreach (var feature in data.rendererFeatures)
                if (featureName == "all" || feature.name == featureName)
                {
                    feature.SetActive(active);
                    Debug.Log($"[ShotRig] feature {feature.name} active = {active}");
                }
        }

        static string Arg(string[] args, string name)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static float Float(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        static float[] Floats(string csv) => csv == null ? null : Array.ConvertAll(csv.Split(','), Float);
    }
}
