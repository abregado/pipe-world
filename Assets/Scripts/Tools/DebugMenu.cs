using ImGuiNET;
using PipeWorld.Landscape;
using PipeWorld.Player;
using PipeWorld.World;
using UImGui;
using UnityEngine;
using UnityEngine.InputSystem;
using Numerics = System.Numerics;

namespace PipeWorld.Tools
{
    public class DebugMenu : MonoBehaviour
    {
        const float RespawnHeightLift = 0.1f;

        public DayNightCycle cycle;
        public PlayerController player;
        public DemoWorld world;

        bool visible;

        public bool Visible
        {
            get => visible;
            set
            {
                if (visible == value)
                    return;
                visible = value;
                // The controller owns the cursor lock, so disabling it frees the mouse for the menu.
                if (player != null)
                    player.enabled = !visible;
            }
        }

        void OnEnable() => UImGuiUtility.Layout += OnLayout;

        void OnDisable() => UImGuiUtility.Layout -= OnLayout;

        void Update()
        {
            if (Keyboard.current != null && (Keyboard.current.f1Key.wasPressedThisFrame || Keyboard.current.backquoteKey.wasPressedThisFrame))
                Visible = !Visible;
        }

        void OnLayout(UImGui.UImGui ui)
        {
            if (!visible)
                return;

            ImGui.SetNextWindowPos(new Numerics.Vector2(16f, 16f), ImGuiCond.FirstUseEver);
            var open = true;
            if (ImGui.Begin("Debug (F1)", ref open, ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize))
            {
                DrawReadout();
                if (cycle != null)
                    DrawTime();
                DrawRespawn();
            }
            ImGui.End();

            if (!open)
                Visible = false;
        }

        void DrawReadout()
        {
            var framerate = ImGui.GetIO().Framerate;
            ImGui.Text($"{framerate:F0} fps  ({1000f / Mathf.Max(framerate, 1f):F1} ms)");
            if (player != null)
            {
                var p = player.transform.position;
                ImGui.Text($"Position  {p.x:F0}, {p.y:F1}, {p.z:F0}   radius {Mathf.Sqrt(p.x * p.x + p.z * p.z):F0}");
            }
        }

        void DrawTime()
        {
            ImGui.SeparatorText("Time of day");

            var hours = cycle.timeOfDay * 24f;
            ImGui.Text($"{Mathf.FloorToInt(hours):00}:{Mathf.FloorToInt(hours % 1f * 60f):00}   fog end {cycle.FogEnd:F0} m");
            ImGui.ProgressBar(cycle.NightFactor, new Numerics.Vector2(-1f, 0f), $"night {cycle.NightFactor:P0}");
            ImGui.SliderFloat("##time", ref cycle.timeOfDay, 0f, 1f, "%.3f");

            if (ImGui.Button("Start of day")) cycle.timeOfDay = cycle.StartOfDayTime;
            ImGui.SameLine();
            if (ImGui.Button("Noon")) cycle.timeOfDay = 0.5f;
            ImGui.SameLine();
            if (ImGui.Button("Start of night")) cycle.timeOfDay = cycle.StartOfNightTime;
            ImGui.SameLine();
            if (ImGui.Button("Midnight")) cycle.timeOfDay = 0f;

            ImGui.Checkbox("Pause time", ref cycle.paused);
            ImGui.SliderFloat("Day length (s)", ref cycle.dayLengthSeconds, 30f, 1800f, "%.0f");

            ImGui.SeparatorText("Fog end");
            ImGui.SliderFloat("Day (m)", ref cycle.dayFogEnd, 300f, 3000f, "%.0f");
            ImGui.SliderFloat("Night (m)", ref cycle.nightFogEnd, 100f, 1500f, "%.0f");
        }

        void DrawRespawn()
        {
            if (player == null || world == null || world.Shape == null)
                return;

            ImGui.SeparatorText("Respawn");
            if (ImGui.Button("Centre of crater")) RespawnAtCentre();
            ImGui.SameLine();
            if (ImGui.Button("Inside outer wall")) RespawnInsideOuterWall();
        }

        public void RespawnAtCentre() => Respawn(0f, 0f, 0f);

        // Faces the crater centre so the whole world falls away in front of the player.
        public void RespawnInsideOuterWall()
        {
            const float angle = 225f * Mathf.Deg2Rad;
            var radius = world.Shape.OuterRadius - 25f;
            var x = Mathf.Cos(angle) * radius;
            var z = Mathf.Sin(angle) * radius;
            Respawn(x, z, Mathf.Atan2(-x, -z) * Mathf.Rad2Deg);
        }

        void Respawn(float x, float z, float yaw)
        {
            var y = world.Shape.Height(x, z) + RespawnHeightLift;
            player.Teleport(new Vector3(x, y, z), yaw, 0f);
        }
    }
}
