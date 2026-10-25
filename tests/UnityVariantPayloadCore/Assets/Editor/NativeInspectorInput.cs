#nullable enable

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Spire.UnityVariantPayloadCore
{
    /// <summary>
    /// Queues pointer and keyboard input through XTest for Linux Editor tests.
    ///
    /// This helper is deliberately opt-in. It does nothing unless
    /// <c>SPIRE_UNITY_NATIVE_INPUT=1</c> and <c>DISPLAY</c> are both present,
    /// and the current Unity editor is running on Linux.
    /// </summary>
    public static class NativeInspectorInput
    {
        private const string X11Library = "libX11.so.6";
        private const string XTestLibrary = "libXtst.so.6";
        private const string EnableEnvironmentVariable = "SPIRE_UNITY_NATIVE_INPUT";

        private static readonly object Sync = new object();

        private static IntPtr _display;
        private static int _screen;
        private static bool _initializationAttempted;
        private static bool _disabled;

        /// <summary>
        /// Gets whether an opted-in XTest display connection can be opened.
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
                lock (Sync)
                    return TryEnsureDisplay(out _, out _);
            }
        }

        /// <summary>
        /// Queues a pointer move in screen coordinates and flushes it to X11.
        /// The next editor tick can then process the resulting native event.
        /// </summary>
        public static bool QueueMove(int screenX, int screenY)
        {
            lock (Sync)
            {
                if (!TryEnsureDisplay(out IntPtr display, out int screen))
                    return false;

                try
                {
                    if (XTestFakeMotionEvent(display, screen, screenX, screenY, UIntPtr.Zero) == 0)
                        return false;

                    XFlush(display);
                    return true;
                }
                catch (Exception)
                {
                    DisableConnection();
                    return false;
                }
            }
        }

        /// <summary>
        /// Queues one X11 button transition. Button 1 is the primary button,
        /// 2 is middle, and 3 is secondary.
        /// </summary>
        public static bool QueueButton(uint button, bool pressed)
        {
            if (button == 0)
                return false;

            lock (Sync)
            {
                if (!TryEnsureDisplay(out IntPtr display, out _))
                    return false;

                try
                {
                    if (XTestFakeButtonEvent(display, button, pressed ? 1 : 0, UIntPtr.Zero) == 0)
                        return false;

                    XFlush(display);
                    return true;
                }
                catch (Exception)
                {
                    DisableConnection();
                    return false;
                }
            }
        }

        /// <summary>Queues a button press.</summary>
        public static bool QueueButtonPress(uint button)
            => QueueButton(button, pressed: true);

        /// <summary>Queues a button release.</summary>
        public static bool QueueButtonRelease(uint button)
            => QueueButton(button, pressed: false);

        /// <summary>
        /// Queues a complete button click without waiting for an editor tick.
        /// </summary>
        public static bool QueueClick(uint button = 1)
            => QueueButton(button, pressed: true) && QueueButton(button, pressed: false);

        /// <summary>
        /// Resolves an X11 keysym such as <c>x</c> or <c>BackSpace</c>, then
        /// queues its press and release. No editor event is sent directly.
        /// </summary>
        public static bool QueueKeystroke(string keysym)
        {
            if (string.IsNullOrEmpty(keysym))
                return false;

            lock (Sync)
            {
                if (!TryEnsureDisplay(out IntPtr display, out _))
                    return false;

                try
                {
                    UIntPtr symbol = XStringToKeysym(keysym);
                    if (symbol == UIntPtr.Zero)
                        return false;

                    byte keycode = XKeysymToKeycode(display, symbol);
                    if (keycode == 0)
                        return false;

                    if (XTestFakeKeyEvent(display, keycode, 1, UIntPtr.Zero) == 0)
                        return false;
                    if (XTestFakeKeyEvent(display, keycode, 0, UIntPtr.Zero) == 0)
                        return false;

                    XFlush(display);
                    return true;
                }
                catch (Exception)
                {
                    DisableConnection();
                    return false;
                }
            }
        }

        /// <summary>
        /// Casing alias for callers that spell the operation as KeyStroke.
        /// </summary>
        public static bool QueueKeyStroke(string keysym)
            => QueueKeystroke(keysym);

        /// <summary>
        /// Closes the static X11 connection and permits a later opted-in retry.
        /// Call this from test teardown or before an editor domain reload.
        /// </summary>
        public static void Dispose()
        {
            lock (Sync)
            {
                CloseDisplayNoThrow();
                _initializationAttempted = false;
                _disabled = false;
            }
        }

        private static bool TryEnsureDisplay(out IntPtr display, out int screen)
        {
            display = _display;
            screen = _screen;

            if (!IsOptedInLinuxEditor())
            {
                CloseDisplayNoThrow();
                _disabled = true;
                return false;
            }

            string? displayName = Environment.GetEnvironmentVariable("DISPLAY");
            if (string.IsNullOrWhiteSpace(displayName))
            {
                CloseDisplayNoThrow();
                _disabled = true;
                return false;
            }

            if (_display != IntPtr.Zero)
                return true;
            if (_initializationAttempted || _disabled)
                return false;

            _initializationAttempted = true;

            IntPtr openedDisplay = IntPtr.Zero;
            try
            {
                openedDisplay = XOpenDisplay(displayName);
                if (openedDisplay == IntPtr.Zero)
                {
                    _disabled = true;
                    return false;
                }

                int eventBase;
                int errorBase;
                int majorVersion;
                int minorVersion;
                if (XTestQueryExtension(
                        openedDisplay,
                        out eventBase,
                        out errorBase,
                        out majorVersion,
                        out minorVersion) == 0)
                {
                    CloseDisplayNoThrow(openedDisplay);
                    _disabled = true;
                    return false;
                }

                _display = openedDisplay;
                _screen = XDefaultScreen(openedDisplay);
                display = _display;
                screen = _screen;
                return true;
            }
            catch (Exception)
            {
                if (openedDisplay != IntPtr.Zero)
                    CloseDisplayNoThrow(openedDisplay);

                _disabled = true;
                return false;
            }
        }

        private static bool IsOptedInLinuxEditor()
        {
            if (Environment.GetEnvironmentVariable(EnableEnvironmentVariable) != "1")
                return false;

            try
            {
                return Application.platform == RuntimePlatform.LinuxEditor;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void DisableConnection()
        {
            CloseDisplayNoThrow();
            _disabled = true;
        }

        private static void CloseDisplayNoThrow()
        {
            CloseDisplayNoThrow(_display);
            _display = IntPtr.Zero;
            _screen = 0;
        }

        private static void CloseDisplayNoThrow(IntPtr display)
        {
            if (display == IntPtr.Zero)
                return;

            try
            {
                XCloseDisplay(display);
            }
            catch (Exception)
            {
                // A missing or unloadable native library must not fail test teardown.
            }
        }

        [DllImport(X11Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr XOpenDisplay(string displayName);

        [DllImport(X11Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int XCloseDisplay(IntPtr display);

        [DllImport(X11Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int XDefaultScreen(IntPtr display);

        [DllImport(X11Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int XFlush(IntPtr display);

        [DllImport(XTestLibrary, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int XTestQueryExtension(
            IntPtr display,
            out int eventBase,
            out int errorBase,
            out int majorVersion,
            out int minorVersion);

        [DllImport(XTestLibrary, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int XTestFakeMotionEvent(
            IntPtr display,
            int screenNumber,
            int x,
            int y,
            UIntPtr delay);

        [DllImport(XTestLibrary, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int XTestFakeButtonEvent(
            IntPtr display,
            uint button,
            int isPress,
            UIntPtr delay);

        [DllImport(XTestLibrary, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int XTestFakeKeyEvent(
            IntPtr display,
            uint keycode,
            int isPress,
            UIntPtr delay);

        [DllImport(X11Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern UIntPtr XStringToKeysym(string keysym);

        [DllImport(X11Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern byte XKeysymToKeycode(IntPtr display, UIntPtr keysym);
    }
}
