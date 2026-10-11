using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NeonPrism
{
    /// <summary>
    /// Тонкая обёртка над вводом. Работает и с новым Input System, и со старым Input Manager
    /// (зависит от Project Settings > Player > Active Input Handling).
    /// </summary>
    public static class GameInput
    {
        /// <summary>W = +1, S = -1.</summary>
        public static float Move
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Keyboard k = Keyboard.current;
                if (k == null) return 0f;
                return (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
#else
                return (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
#endif
            }
        }

        /// <summary>A (влево, против часовой) = +1, D (вправо) = -1. Знак совпадает с положительным углом Unity 2D.</summary>
        public static float Turn
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Keyboard k = Keyboard.current;
                if (k == null) return 0f;
                return (k.aKey.isPressed ? 1f : 0f) - (k.dKey.isPressed ? 1f : 0f);
#else
                return (Input.GetKey(KeyCode.A) ? 1f : 0f) - (Input.GetKey(KeyCode.D) ? 1f : 0f);
#endif
            }
        }

        public static Vector2 MouseScreenPosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Mouse m = Mouse.current;
                return m != null ? m.position.ReadValue() : Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }

        public static bool FirePressedThisFrame
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Mouse m = Mouse.current;
                return m != null && m.leftButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }

        public static bool FireHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Mouse m = Mouse.current;
                return m != null && m.leftButton.isPressed;
#else
                return Input.GetMouseButton(0);
#endif
            }
        }
    }
}
