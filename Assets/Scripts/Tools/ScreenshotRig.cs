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
                if (respawn == "outer") debugMenu.RespawnInsideOuterWall(); else debugMenu.RespawnAtCentre();
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
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            Application.Quit();
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
