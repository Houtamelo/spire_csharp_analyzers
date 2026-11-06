#nullable enable

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Spire.UnityVariantPayloadCore
{
    internal static class WindowsInspectorInput
    {
        private static string lastOperation = string.Empty;
        internal static string DescribeAvailability()
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out var foreground);
            return "platform=" + Application.platform + ";optIn=" + Environment.GetEnvironmentVariable("SPIRE_UNITY_NATIVE_INPUT") +
                ";interactive=" + Environment.UserInteractive + ";foreground=" + foreground + ";unity=" + GetCurrentProcessId() + ";last=" + lastOperation;
        }

        internal static bool IsAvailable =>
            Application.platform == RuntimePlatform.WindowsEditor &&
            Environment.GetEnvironmentVariable("SPIRE_UNITY_NATIVE_INPUT") == "1" &&
            // Unity Mono reports UserInteractive=false even on the visible desktop.
            IsUnityForeground();

        internal static bool QueueMove(int x, int y)
        {
            if (!IsAvailable) return false;
            GetWindowThreadProcessId(WindowFromPoint(new Point { x = x, y = y }), out var target);
            if (target != GetCurrentProcessId())
            {
                lastOperation = "MoveTarget(" + x + "," + y + ")=" + target;
                return false;
            }
            var result = SetCursorPos(x, y);
            lastOperation = "SetCursorPos(" + x + "," + y + ")=" + result + ";error=" + Marshal.GetLastWin32Error();
            return result;
        }

        internal static bool QueueClick(uint button)
        {
            var down = button switch { 1 => 0x0002u, 2 => 0x0020u, 3 => 0x0008u, _ => 0u };
            if (down == 0 || !IsAvailable) return false;
            return Send(
                new Input { type = 0, data = new InputData { mouse = new MouseInput { flags = down } } },
                new Input { type = 0, data = new InputData { mouse = new MouseInput { flags = down * 2 } } });
        }

        internal static bool QueueButton(uint button, bool pressed)
        {
            var flag = button switch
            {
                1 => pressed ? 0x0002u : 0x0004u,
                2 => pressed ? 0x0020u : 0x0040u,
                3 => pressed ? 0x0008u : 0x0010u,
                _ => 0u,
            };
            if (flag == 0 || !IsAvailable) return false;
            return Send(new Input { type = 0, data = new InputData { mouse = new MouseInput { flags = flag } } });
        }

        internal static bool QueueKeystroke(string symbol)
        {
            if (string.IsNullOrEmpty(symbol) || !IsAvailable) return false;
            ushort key = symbol switch
            {
                "BackSpace" => 0x08,
                "Tab" => 0x09,
                "Return" => 0x0d,
                "Escape" => 0x1b,
                "Left" => 0x25,
                "Up" => 0x26,
                "Right" => 0x27,
                "Down" => 0x28,
                "Delete" => 0x2e,
                _ => 0,
            };
            if (key != 0)
                return Send(Keyboard(key, 0, 0), Keyboard(key, 0, 0x0002));
            if (symbol.Length != 1 || char.IsControl(symbol[0])) return false;
            // Unity's native event loop needs actual virtual keys for ordinary typing.
            var mapped = VkKeyScan(symbol[0]);
            if (mapped != -1 && (mapped & 0xff00) == 0)
                return Send(Keyboard((ushort)(mapped & 0xff), 0, 0), Keyboard((ushort)(mapped & 0xff), 0, 0x0002));
            return Send(Keyboard(0, symbol[0], 0x0004), Keyboard(0, symbol[0], 0x0006));
        }

        private static bool IsUnityForeground()
        {
            var window = GetForegroundWindow();
            return window != IntPtr.Zero && GetWindowThreadProcessId(window, out var process) != 0 &&
                process == GetCurrentProcessId();
        }

        private static Input Keyboard(ushort key, ushort scan, uint flags)
            => new Input { type = 1, data = new InputData { keyboard = new KeyboardInput { key = key, scan = scan, flags = flags } } };

        private static bool Send(params Input[] input)
        {
            var size = Marshal.SizeOf(typeof(Input));
            var count = SendInput((uint)input.Length, input, size);
            lastOperation = "SendInput=" + count + "/" + input.Length + ";size=" + size + ";error=" + Marshal.GetLastWin32Error();
            return count == (uint)input.Length;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            public uint type;
            public InputData data;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputData
        {
            [FieldOffset(0)] public MouseInput mouse;
            [FieldOffset(0)] public KeyboardInput keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int x, y;
            public uint mouseData, flags, time;
            public UIntPtr extraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort key, scan;
            public uint flags, time;
            public UIntPtr extraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Point { public int x, y; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern short VkKeyScan(char character);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(Point point);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, Input[] inputs, int size);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);
    }
}
