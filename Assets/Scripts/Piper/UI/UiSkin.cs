using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    [CreateAssetMenu(menuName = "UI/Skin")]
    public class UiSkin : ScriptableObject {
        public UiSurface surface;

        // A later sheet wins over an earlier one at equal specificity.
        public StyleSheet[] sheets;
    }

    public static class UiSkins {
        private static readonly Dictionary<UiSurface, UiPanel> panels = new();
        private static readonly Dictionary<UiSurface, UiSkin> chosen = new();
        private static readonly Dictionary<UiSurface, UiSkin> defaults = new();

        public static event Action<UiSurface, UiSkin> Changed;

        public static UiSkin Load(UiSurface surface, string variant) {
            var skin = Resources.Load<UiSkin>($"UiSkins/{surface}{variant}");
            if (skin == null) Debug.LogError($"Missing skin Resources/UiSkins/{surface}{variant}");
            return skin;
        }

        public static UiSkin LoadDefault(UiSurface surface) => Load(surface, "Default");

        public static UiSkin Chosen(UiSurface surface) => chosen.TryGetValue(surface, out var skin) ? skin : null;

        public static void Register(UiSurface surface, UiPanel panel) {
            var defaultSkin = LoadDefault(surface);
            panels[surface] = panel;
            defaults[surface] = defaultSkin;
            panel.SetSkin(chosen.TryGetValue(surface, out var skin) ? skin : defaultSkin);
        }

        public static void SetSkin(UiSurface surface, UiSkin skin) {
            chosen[surface] = skin;
            if (panels.TryGetValue(surface, out var panel)) panel.SetSkin(skin);
            Changed?.Invoke(surface, skin);
        }

        public static void ResetSkin(UiSurface surface) {
            chosen.Remove(surface);
            if (!defaults.TryGetValue(surface, out var skin)) return;
            if (panels.TryGetValue(surface, out var panel)) panel.SetSkin(skin);
            Changed?.Invoke(surface, skin);
        }

        public static UiSkin Current(UiSurface surface) => panels.TryGetValue(surface, out var panel) ? panel.skin : null;
    }
}
