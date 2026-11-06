using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public class SettingsPanel : MenuViewPanel
    {
        public SettingsPanel()
        {
            AddToClassList("menu-view-panel--settings");

            var title = new Label("Settings");
            title.AddToClassList("load-game-title");
            Add(title);

            var tabBar = new VisualElement();
            tabBar.AddToClassList("settings-tab-bar");
            Add(tabBar);

            string[] names = { "Audio", "Graphics", "Interface", "Controls" };
            var tabs = new Button[names.Length];
            var contents = new VisualElement[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                contents[i] = MakeContent();
                Add(contents[i]);
                tabs[i] = MakeTab(names[i]);
                tabBar.Add(tabs[i]);
            }

            void SelectTab(int index)
            {
                for (int i = 0; i < tabs.Length; i++)
                {
                    tabs[i].EnableInClassList("selected", i == index);
                    contents[i].EnableInClassList("active", i == index);
                }
            }

            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                tabs[i].RegisterCallback<ClickEvent>(_ => SelectTab(index));
            }

            BuildAudio(contents[0]);
            BuildGraphics(contents[1]);
            BuildInterface(contents[2]);
            BuildControls(contents[3]);
            SelectTab(0);
        }

        private static Button MakeTab(string text)
        {
            var btn = new Button { text = text };
            btn.AddToClassList("settings-tab");
            return btn;
        }

        private static VisualElement MakeContent()
        {
            var ve = new VisualElement();
            ve.AddToClassList("settings-content");
            return ve;
        }

        private static void Save() => PiperMain.p.settings.save();

        private static void BuildAudio(VisualElement content)
        {
            var s = PiperMain.p.settings;
            var d = new Settings();
            var resets = new List<Action>();

            resets.Add(AddSliderRow(content, "Master Volume", 0f, 1f, s.masterVolume, d.masterVolume,
                v => Mathf.RoundToInt(v * 100) + "%",
                v =>
                {
                    PiperMain.p.settings.masterVolume = v;
                    AudioListener.volume = v;
                    Save();
                }));

            AddResetBar(content, resets);
        }

        private static void BuildGraphics(VisualElement content)
        {
            var s = PiperMain.p.settings;
            var d = new Settings();
            var resets = new List<Action>();

            var vsyncToggle = new Toggle { value = s.vsyncEnabled };
            content.Add(MakeToggleRow("VSync", vsyncToggle, d.vsyncEnabled));
            resets.Add(() => vsyncToggle.value = d.vsyncEnabled);
            vsyncToggle.RegisterValueChangedCallback(evt =>
            {
                PiperMain.p.settings.vsyncEnabled = evt.newValue;
                PiperMain.p.ApplyDisplaySettings();
                Save();
            });

            resets.Add(AddSliderRow(content, "Field of View", 60f, 110f, s.fieldOfView, d.fieldOfView,
                v => $"{Mathf.RoundToInt(v)}°",
                v =>
                {
                    PiperMain.p.settings.fieldOfView = v;
                    PiperMain.p.game?.ApplySettings();
                    Save();
                }));

            AddResetBar(content, resets);
        }

        private static void BuildInterface(VisualElement content)
        {
            var s = PiperMain.p.settings;
            var d = new Settings();
            var resets = new List<Action>();

            resets.Add(AddChoiceRow(content, "Font Size",
                (HudFontSize[])Enum.GetValues(typeof(HudFontSize)), v => v.ToString(), s.hudFontSize, d.hudFontSize,
                v =>
                {
                    PiperMain.p.settings.hudFontSize = v;
                    Save();
                    UiSettings.NotifyChanged();
                }));

            AddResetBar(content, resets);
        }

        private static void BuildControls(VisualElement content)
        {
            var s = PiperMain.p.settings;
            var d = new Settings();
            var resets = new List<Action>();

            resets.Add(AddSliderRow(content, "Mouse Sensitivity", 0.5f, 3f, s.mouseSensitivity, d.mouseSensitivity,
                v => v.ToString("F1"),
                v =>
                {
                    PiperMain.p.settings.mouseSensitivity = v;
                    PiperMain.p.game?.ApplySettings();
                    Save();
                }));

            var nameField = new TextField { value = s.playerName, maxLength = Constants.MAX_PLAYER_NAME_LENGTH_UTF8_BYTES };
            var nameRow = new VisualElement();
            nameRow.AddToClassList("settings-row");
            var nameLabel = new Label("Player Name");
            nameLabel.AddToClassList("settings-row-label");
            nameRow.Add(nameLabel);
            nameRow.Add(nameField);
            content.Add(nameRow);
            nameField.RegisterValueChangedCallback(evt =>
            {
                PiperMain.p.settings.playerName = evt.newValue;
                Save();
            });

            AddResetBar(content, resets);
        }

        private static VisualElement MakeToggleRow(string labelText, Toggle toggle, bool defaultValue)
        {
            var row = new VisualElement();
            row.AddToClassList("settings-row");
            var lbl = new Label(labelText);
            lbl.AddToClassList("settings-row-label");
            row.Add(lbl);
            row.Add(toggle);
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            row.Add(spacer);
            var resetBtn = new Button(() => toggle.value = defaultValue) { text = "Reset" };
            resetBtn.AddToClassList("settings-reset-btn");
            row.Add(resetBtn);
            return row;
        }

        private static Action AddSliderRow(VisualElement content,
            string labelText, float min, float max, float value, float defaultValue,
            Func<float, string> format, Action<float> onChange)
        {
            var row = new VisualElement();
            row.AddToClassList("settings-row");

            var lbl = new Label(labelText);
            lbl.AddToClassList("settings-row-label");
            row.Add(lbl);

            var slider = new Slider { lowValue = min, highValue = max, value = value };
            row.Add(slider);

            var valueLbl = new Label(format(value));
            valueLbl.AddToClassList("settings-row-value");
            row.Add(valueLbl);

            slider.RegisterValueChangedCallback(evt =>
            {
                valueLbl.text = format(evt.newValue);
                onChange(evt.newValue);
            });

            var resetBtn = new Button(() => slider.value = defaultValue) { text = "Reset" };
            resetBtn.AddToClassList("settings-reset-btn");
            row.Add(resetBtn);

            content.Add(row);
            return () => slider.value = defaultValue;
        }

        private static Action AddChoiceRow<T>(VisualElement content,
            string labelText, T[] options, Func<T, string> name, T value, T defaultValue, Action<T> onChange)
        {
            var row = new VisualElement();
            row.AddToClassList("settings-row");

            var lbl = new Label(labelText);
            lbl.AddToClassList("settings-row-label");
            row.Add(lbl);

            var group = new VisualElement();
            group.AddToClassList("settings-choice-group");
            row.Add(group);

            var buttons = new Button[options.Length];

            void Select(T selected, bool notify)
            {
                for (int i = 0; i < options.Length; i++)
                    buttons[i].EnableInClassList("selected", EqualityComparer<T>.Default.Equals(options[i], selected));
                if (notify) onChange(selected);
            }

            for (int i = 0; i < options.Length; i++)
            {
                T option = options[i];
                buttons[i] = new Button(() => Select(option, true)) { text = name(option) };
                buttons[i].AddToClassList("settings-choice");
                group.Add(buttons[i]);
            }
            Select(value, false);

            var resetBtn = new Button(() => Select(defaultValue, true)) { text = "Reset" };
            resetBtn.AddToClassList("settings-reset-btn");
            row.Add(resetBtn);

            content.Add(row);
            return () => Select(defaultValue, true);
        }

        private static void AddResetBar(VisualElement content, List<Action> resets)
        {
            var bar = new VisualElement();
            bar.AddToClassList("settings-action-bar");

            var btn = new Button(() => { foreach (Action r in resets) r(); }) { text = "Reset to Defaults" };
            btn.AddToClassList("settings-reset-all-btn");
            bar.Add(btn);

            content.Add(bar);
        }
    }
}
