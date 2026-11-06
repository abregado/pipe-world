using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public class UiPanel {
        public readonly VisualElement content;
        public event Action Attached;
        public UiSkin skin { get; private set; }

        private readonly List<StyleSheet> sheets = new();
        private VisualElement panelRoot;

        private UiPanel(string name) {
            content = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            content.style.position = Position.Absolute;
            content.style.left = 0;
            content.style.top = 0;
            content.style.right = 0;
            content.style.bottom = 0;
        }

        // The UIDocument root is not guaranteed to exist the frame the panel is created, so callers build into `content` straight away.
        public static UiPanel Create(Transform parent, string name, PanelSettings settings, int sortingOrder) {
            var panel = new UiPanel(name);

            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = settings;
            document.sortingOrder = sortingOrder;
            go.SetActive(true);

            panel.Attach(document.rootVisualElement);
            return panel;
        }

        public void SetSkin(UiSkin newSkin) {
            skin = newSkin;
            SetStyleSheets(newSkin.sheets);
        }

        public void SetStyleSheets(params StyleSheet[] newSheets) {
            sheets.Clear();
            foreach (var sheet in newSheets)
                if (sheet != null) sheets.Add(sheet);
            ApplyStyleSheets();
        }

        private void Attach(VisualElement root) {
            panelRoot = root;
            root.Clear();
            ApplyStyleSheets();
            root.Add(content);
            Attached?.Invoke();
        }

        // Sheets go on the panel root, not `content`, so `:root` token rules match.
        private void ApplyStyleSheets() {
            if (panelRoot == null) return;
            panelRoot.styleSheets.Clear();
            foreach (var sheet in sheets) panelRoot.styleSheets.Add(sheet);
        }
    }
}
