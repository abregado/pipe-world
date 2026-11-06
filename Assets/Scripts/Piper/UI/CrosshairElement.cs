using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public enum CrosshairStyle {
        Default,
        Interact,
        DeconstructHold,
        Collecting,
        PipeStart,
        PipeEdit,
        PipePlace,
        PipeNotAllowed,
        PipeRemove,
        PropPlace,
        PropRemove,
        PlaceNotAllowed,
        AddPart,
    }

    public class CrosshairElement : VisualElement {
        public new class UxmlFactory : UxmlFactory<CrosshairElement> {}

        private class CircularProgress : VisualElement {
            private float _progress;

            public float progress {
                get => _progress;
                set { _progress = value; MarkDirtyRepaint(); }
            }

            public CircularProgress() {
                generateVisualContent += Draw;
                style.position = Position.Absolute;
                style.left = 0;
                style.top = 0;
            }

            private void Draw(MeshGenerationContext ctx) {
                float w = resolvedStyle.width;
                float h = resolvedStyle.height;
                if (w <= 0 || h <= 0) return;

                var painter = ctx.painter2D;
                var center = new Vector2(w / 2, h / 2);
                float stroke = 8f;
                float radius = Mathf.Min(w, h) / 8 - stroke;

                painter.lineWidth = stroke;
                painter.lineCap = LineCap.Round;

                if (_progress > 0.001f) {
                    painter.strokeColor = new Color(1f, 107f / 255f, 91f / 255f, 0.9f);
                    painter.BeginPath();
                    painter.Arc(center, radius, -90, -90 + 360f * _progress, ArcDirection.Clockwise);
                    painter.Stroke();
                }
            }
        }

        private static readonly Dictionary<CrosshairStyle, string> StylePaths = new Dictionary<CrosshairStyle, string> {
            { CrosshairStyle.Default,         "Crosshairs/crosshair_default" },
            { CrosshairStyle.Interact,        "Crosshairs/crosshair_interact" },
            { CrosshairStyle.DeconstructHold, "Crosshairs/crosshair_deconstruct_hold" },
            { CrosshairStyle.Collecting,      "Crosshairs/crosshair_collecting" },
            { CrosshairStyle.PipeStart,       "Crosshairs/crosshair_pipe_start" },
            { CrosshairStyle.PipeEdit,        "Crosshairs/crosshair_pipe_edit" },
            { CrosshairStyle.PipePlace,       "Crosshairs/crosshair_pipe_place" },
            { CrosshairStyle.PipeNotAllowed,  "Crosshairs/crosshair_not_allowed" },
            { CrosshairStyle.PipeRemove,      "Crosshairs/crosshair_pipe_remove" },
            { CrosshairStyle.PropPlace,       "Crosshairs/crosshair_prop_place" },
            { CrosshairStyle.PropRemove,      "Crosshairs/crosshair_prop_remove" },
            { CrosshairStyle.PlaceNotAllowed, "Crosshairs/crosshair_not_allowed" },
            { CrosshairStyle.AddPart,         "Crosshairs/crosshair_add_part" },
        };

        private static readonly Dictionary<CrosshairStyle, Sprite> SpriteCache = new Dictionary<CrosshairStyle, Sprite>();

        private VisualElement imageEl;
        private CircularProgress _progressRing;

        private float _squareSize = 256f;
        private float _imageSize = 64f;
        private CrosshairStyle _currentStyle = CrosshairStyle.Default;

        public float squareSize {
            get => _squareSize;
            set {
                _squareSize = value;
                style.width = value;
                style.height = value;
                _progressRing.style.width = value;
                _progressRing.style.height = value;
                RecalculateMargin();
            }
        }

        public float imageSize {
            get => _imageSize;
            set {
                _imageSize = value;
                RecalculateMargin();
            }
        }

        public Sprite sprite {
            set {
                imageEl.style.backgroundImage = new StyleBackground(value);
            }
        }

        public void SetStyle(CrosshairStyle style) {
            if (style == _currentStyle) return;
            _currentStyle = style;
            Sprite s = LoadStyleSprite(style);
            if (s == null) return;
            imageEl.style.backgroundImage = new StyleBackground(s);
        }

        public void SetProgress(float progress) {
            _progressRing.style.display = DisplayStyle.Flex;
            _progressRing.progress = progress;
        }

        public void ClearProgress() {
            _progressRing.style.display = DisplayStyle.None;
        }

        private void RecalculateMargin() {
            float margin = (_squareSize - _imageSize) / 2f;
            imageEl.style.width = _imageSize;
            imageEl.style.height = _imageSize;
            imageEl.style.marginTop = margin;
            imageEl.style.marginBottom = margin;
            imageEl.style.marginLeft = margin;
            imageEl.style.marginRight = margin;
        }

        public CrosshairElement() {
            style.width = _squareSize;
            style.height = _squareSize;

            imageEl = new VisualElement { name = "crosshair-image" };
            Add(imageEl);

            _progressRing = new CircularProgress();
            _progressRing.style.width = _squareSize;
            _progressRing.style.height = _squareSize;
            _progressRing.style.display = DisplayStyle.None;
            Add(_progressRing);

            Sprite defaultSprite = LoadStyleSprite(CrosshairStyle.Default);
            if (defaultSprite != null)
                imageEl.style.backgroundImage = new StyleBackground(defaultSprite);

            RecalculateMargin();
        }

        private static Sprite LoadStyleSprite(CrosshairStyle style) {
            if (SpriteCache.TryGetValue(style, out Sprite cached)) return cached;
            Sprite loaded = Resources.Load<Sprite>(StylePaths[style]);
            SpriteCache[style] = loaded;
            return loaded;
        }
    }
}
