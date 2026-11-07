using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    // The eye closing effect from Piperspace (its EyeClose shader), drawn as two black eyelids over the whole screen: intensity 0 is open,
    // 1 is closed. The lids follow an elliptical curve so the opening narrows toward the corners as it shuts.
    public class EyeCloseElement : VisualElement
    {
        private const float EyeWidth = 4.5f;
        private const float MaxOpenHeight = 2f;
        private const int Samples = 48;

        private float intensity;
        private IVisualElementScheduledItem tick;

        public float Intensity
        {
            get => intensity;
            set
            {
                intensity = Mathf.Clamp01(value);
                style.display = intensity > 0.0001f ? DisplayStyle.Flex : DisplayStyle.None;
                MarkDirtyRepaint();
            }
        }

        public bool IsPlaying => tick != null;

        public EyeCloseElement()
        {
            name = "eye-close";
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            pickingMode = PickingMode.Ignore;
            style.display = DisplayStyle.None;
            generateVisualContent += Draw;
        }

        public static float EaseInQuad(float t) => t * t;
        public static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        // Animates the intensity to `target` over `duration` seconds, then calls `done`.
        public void Tween(float target, float duration, Func<float, float> ease, Action done = null)
        {
            tick?.Pause();
            float from = intensity;
            float start = Time.unscaledTime;
            tick = schedule.Execute(() =>
            {
                float t = duration <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - start) / duration);
                Intensity = Mathf.Lerp(from, target, ease(t));
                if (t < 1f)
                    return;

                tick.Pause();
                tick = null;
                done?.Invoke();
            }).Every(8);
        }

        private void Draw(MeshGenerationContext context)
        {
            float w = contentRect.width, h = contentRect.height;
            if (intensity <= 0.0001f || w <= 1f || h <= 1f)
                return;

            var painter = context.painter2D;
            painter.fillColor = Color.black;

            float aspect = w / h;
            float open = Mathf.Lerp(MaxOpenHeight, 0f, intensity);
            float halfWidth = EyeWidth * 0.5f;

            // How far each lid has come in from its screen edge at a given x.
            float Gap(float x)
            {
                float centred = (x / w - 0.5f) * aspect * 2f;
                float t = Mathf.Clamp01(Mathf.Abs(centred) / halfWidth);
                float half = Mathf.Sqrt(Mathf.Clamp01(1f - t * t)) * open * h * 0.5f;
                return Mathf.Max(0f, h * 0.5f - half);
            }

            painter.BeginPath();
            painter.MoveTo(new Vector2(0f, 0f));
            painter.LineTo(new Vector2(w, 0f));
            for (int i = Samples; i >= 0; i--)
            {
                float x = w * i / Samples;
                painter.LineTo(new Vector2(x, Gap(x)));
            }
            painter.ClosePath();
            painter.Fill();

            painter.BeginPath();
            painter.MoveTo(new Vector2(0f, h));
            painter.LineTo(new Vector2(w, h));
            for (int i = Samples; i >= 0; i--)
            {
                float x = w * i / Samples;
                painter.LineTo(new Vector2(x, h - Gap(x)));
            }
            painter.ClosePath();
            painter.Fill();
        }
    }
}
