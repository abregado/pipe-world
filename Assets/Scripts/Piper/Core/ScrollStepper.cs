using UnityEngine;

namespace PipeWorld.Piper
{
    // Turns the mouse wheel into whole steps. A step happens once half a notch has been scrolled in one direction, so one notch is one step
    // and a wheel that sends many small deltas (smooth-scroll mice, trackpads) steps as it passes the threshold. Scrolling that pauses or
    // changes direction starts counting again.
    public class ScrollStepper
    {
        private const float Threshold = 0.5f;
        private const float IdleReset = 0.3f;

        private float accumulated;
        private float lastScroll;

        // Returns the whole steps this tick; positive is wheel up.
        public int Step(float delta)
        {
            if (delta == 0f)
                return 0;

            float now = Time.unscaledTime;
            if (now - lastScroll > IdleReset || (accumulated != 0f && Mathf.Sign(delta) != Mathf.Sign(accumulated)))
                accumulated = 0f;
            lastScroll = now;

            accumulated += delta;
            if (Mathf.Abs(accumulated) < Threshold)
                return 0;

            int steps = (int)(accumulated + 0.5f * Mathf.Sign(accumulated));
            accumulated -= steps;
            return steps;
        }

        public void Reset() => accumulated = 0f;
    }
}
