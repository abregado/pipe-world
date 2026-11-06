using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public unsafe class LoadGamePanel : MenuViewPanel {
        private VisualElement list;
        private Button loadButton;
        private Label errorLabel;
        private string selectedFile;
        private bool selectedFileCompatible;

        private readonly IMenuActions actions;

        public LoadGamePanel(IMenuActions actions) {
            this.actions = actions;
            AddToClassList("menu-view-panel--load-game");

            var title = new Label("Load Game");
            title.AddToClassList("load-game-title");
            Add(title);

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.AddToClassList("load-game-list");
            Add(scrollView);
            list = scrollView.contentContainer;

            var actionBar = new VisualElement();
            actionBar.AddToClassList("load-game-action-bar");
            Add(actionBar);

            var versionInfo = new VisualElement();
            versionInfo.AddToClassList("load-game-version-info");
            actionBar.Add(versionInfo);

            var versionLabel = new Label($"Current version: v{Constants.saveVersion}");
            versionLabel.AddToClassList("load-game-version-label");
            versionInfo.Add(versionLabel);

            errorLabel = new Label("");
            errorLabel.AddToClassList("load-game-error-label");
            versionInfo.Add(errorLabel);

            loadButton = new Button(OnLoad) { text = "Load" };
            actionBar.Add(loadButton);
            loadButton.SetEnabled(false);

        }

        public override void Show() {
            base.Show();
            RefreshList();
        }

        public void RefreshList() {
            list.Clear();
            selectedFile = null;
            selectedFileCompatible = false;
            loadButton.SetEnabled(false);
            errorLabel.text = "";

            string saveDir = Constants.saveGameFolder;

            FileInfo[] files = Array.Empty<FileInfo>();
            if (Directory.Exists(saveDir)) {
                files = new DirectoryInfo(saveDir)
                    .GetFiles("*.bin")
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToArray();
            }

            if (files.Length == 0) {
                var empty = new Label("No save files found.");
                empty.AddToClassList("load-game-empty");
                list.Add(empty);
                return;
            }

            foreach (var file in files)
                AddItem(file);
        }

        private static unsafe string ReadPlaytime(byte[] bytes, int dataOffset, int version) {
            try {
                if (version != Constants.saveVersion || bytes.Length < dataOffset + sizeof(WorldData)) return "—";
                ulong currentTick;
                fixed (byte* ptr = bytes) {
                    WorldData* data = (WorldData*)(ptr + dataOffset);
                    currentTick = data->currentTick;
                }

                double seconds = currentTick / (double)Constants.TICKS_PER_SECOND;
                return Utils.formatSecondsAsTime(seconds);
            }
            catch (Exception) {
                return "—";
            }
        }

        // The prop slab is the first part after WorldData: a 4-byte length, then Prop structs.
        private static unsafe int ReadPropCount(byte[] bytes, int dataOffset, int version) {
            if (version != Constants.saveVersion) return 0;
            int partOffset = dataOffset + sizeof(WorldData);
            if (bytes.Length < partOffset + 4) return 0;

            int partSize = BitConverter.ToInt32(bytes, partOffset);
            if (bytes.Length < partOffset + 4 + partSize) return 0;

            int count = 0;
            fixed (byte* ptr = bytes) {
                Prop* props = (Prop*)(ptr + partOffset + 4);
                for (int i = 0; i < partSize / sizeof(Prop); i++)
                    if (props[i].slabHeader.allocated) count++;
            }
            return count;
        }

        private void AddItem(FileInfo file) {
            byte[] bytes = File.ReadAllBytes(file.FullName);
            World.TryReadSaveVersion(bytes, out int version, out int dataOffset);
            bool compatible = version == Constants.saveVersion;

            var item = new VisualElement();
            item.AddToClassList("load-game-item");
            if (!compatible) item.AddToClassList("load-game-item--incompatible");
            item.focusable = true;
            item.tabIndex = 0;

            var left = new VisualElement();
            left.AddToClassList("load-game-item-left");
            item.Add(left);

            var nameLabel = new Label(Path.GetFileNameWithoutExtension(file.Name));
            nameLabel.AddToClassList("load-game-item-name");
            left.Add(nameLabel);

            var propsLabel = new Label($"{ReadPropCount(bytes, dataOffset, version)} props");
            propsLabel.AddToClassList("load-game-item-pipe-metres");
            left.Add(propsLabel);

            var meta = new VisualElement();
            meta.AddToClassList("load-game-item-meta");

            var playtimeLabel = new Label(ReadPlaytime(bytes, dataOffset, version));
            playtimeLabel.AddToClassList("load-game-item-playtime");
            meta.Add(playtimeLabel);

            var versionLabel = new Label($"v{version}");
            versionLabel.AddToClassList("load-game-item-version");
            meta.Add(versionLabel);

            var dateLabel = new Label(file.LastWriteTime.ToString("yyyy-MM-dd  HH:mm"));
            dateLabel.AddToClassList("load-game-item-date");
            meta.Add(dateLabel);

            item.Add(meta);

            item.RegisterCallback<ClickEvent>(_ => SelectItem(item, file.Name, compatible, version));
            item.RegisterCallback<NavigationSubmitEvent>(_ => SelectItem(item, file.Name, compatible, version));

            list.Add(item);
        }

        private void SelectItem(VisualElement item, string fileName, bool compatible, int version) {
            foreach (var child in list.Children())
                child.RemoveFromClassList("selected");

            item.AddToClassList("selected");
            selectedFile = fileName;
            selectedFileCompatible = compatible;
            loadButton.SetEnabled(compatible);

            errorLabel.text = compatible
                ? ""
                : $"This save is version {version}, current version is {Constants.saveVersion} — cannot load.";

        }

        private void OnLoad() {
            if (selectedFile != null && selectedFileCompatible)
                actions.LoadGame(selectedFile);
        }

    }
}
