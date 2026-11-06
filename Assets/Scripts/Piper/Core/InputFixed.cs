using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PipeWorld.Piper
{
    // Input sampled every rendered frame and latched for the fixed-step game loop, so a click that lands between two
    // fixed steps is still seen exactly once. accumulateUpdate runs from Update, update from the end of FixedUpdate.
    public static class InputFixed
    {
        private static readonly int maxValue;
        private static readonly bool[] downLatch;
        private static readonly bool[] currentHeld;
        private static readonly bool[] wasHeldAtTickStart;
        private static readonly Key[] keyMap;
        private static readonly bool[] hasKey;

        private static Vector2 accumulatedScrollDelta;
        private static Vector2 scrollDelta;

        public static Vector2 mouseScrollDelta => scrollDelta;

        static InputFixed()
        {
            maxValue = 0;
            foreach (KeyCode code in Enum.GetValues(typeof(KeyCode)))
                maxValue = Math.Max(maxValue, (int)code);

            downLatch = new bool[maxValue + 1];
            currentHeld = new bool[maxValue + 1];
            wasHeldAtTickStart = new bool[maxValue + 1];
            keyMap = new Key[maxValue + 1];
            hasKey = new bool[maxValue + 1];

            foreach (KeyCode code in Enum.GetValues(typeof(KeyCode)))
            {
                if (TryMapKey(code, out Key key))
                {
                    keyMap[(int)code] = key;
                    hasKey[(int)code] = true;
                }
            }
        }

        private static bool TryMapKey(KeyCode code, out Key key)
        {
            string name = code.ToString();
            if (name.StartsWith("Alpha"))
                name = "Digit" + name.Substring(5);
            else if (code == KeyCode.Return)
                name = "Enter";
            else if (code == KeyCode.BackQuote)
                name = "Backquote";

            return Enum.TryParse(name, out key) && key != Key.None;
        }

        private static bool TryReadMouse(KeyCode code, out bool pressed, out bool pressedThisFrame)
        {
            pressed = false;
            pressedThisFrame = false;
            Mouse mouse = Mouse.current;
            if (code < KeyCode.Mouse0 || code > KeyCode.Mouse2)
                return false;

            if (mouse == null)
                return true;

            ButtonControl button = code == KeyCode.Mouse0 ? mouse.leftButton : code == KeyCode.Mouse1 ? mouse.rightButton : mouse.middleButton;
            pressed = button.isPressed;
            pressedThisFrame = button.wasPressedThisFrame;
            return true;
        }

        public static void accumulateUpdate()
        {
            Keyboard keyboard = Keyboard.current;
            for (int i = 0; i <= maxValue; i++)
            {
                if (TryReadMouse((KeyCode)i, out bool mousePressed, out bool mouseDown))
                {
                    if (mouseDown) downLatch[i] = true;
                    currentHeld[i] = mousePressed;
                    continue;
                }

                if (!hasKey[i] || keyboard == null)
                    continue;

                KeyControl key = keyboard[keyMap[i]];
                if (key.wasPressedThisFrame) downLatch[i] = true;
                currentHeld[i] = key.isPressed;
            }

            if (Mouse.current != null)
                accumulatedScrollDelta += Mouse.current.scroll.ReadValue() / 120f;
        }

        public static void update()
        {
            for (int i = 0; i <= maxValue; i++)
            {
                wasHeldAtTickStart[i] = currentHeld[i];
                downLatch[i] = false;
            }
            scrollDelta = accumulatedScrollDelta;
            accumulatedScrollDelta = Vector2.zero;
        }

        public static bool GetKeyDown(KeyCode key) => downLatch[(int)key];

        public static bool GetKey(KeyCode key) => currentHeld[(int)key];

        public static bool GetKeyUp(KeyCode key) => wasHeldAtTickStart[(int)key] && !currentHeld[(int)key];

        public static bool GetMouseButtonDown(int button) => GetKeyDown(KeyCode.Mouse0 + button);

        public static bool GetMouseButton(int button) => GetKey(KeyCode.Mouse0 + button);

        public static bool GetMouseButtonUp(int button) => GetKeyUp(KeyCode.Mouse0 + button);
    }
}
