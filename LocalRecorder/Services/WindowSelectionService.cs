using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LocalRecorder.Services;

public sealed class WindowSelectionService
{
    public List<WindowInfo> GetOpenWindows()
    {
        var windows = new List<WindowInfo>();

        IntPtr ownWindow = GetShellWindow();
        uint currentProcessId = (uint)Environment.ProcessId;

        EnumWindows((handle, _) =>
        {
            if (handle == IntPtr.Zero ||
                handle == ownWindow ||
                !IsWindowVisible(handle))
            {
                return true;
            }

            GetWindowThreadProcessId(handle, out uint processId);

            // LocalRecorder uygulamasını listede gösterme.
            if (processId == currentProcessId)
            {
                return true;
            }

            int length = GetWindowTextLength(handle);

            if (length <= 0)
            {
                return true;
            }

            var title = new StringBuilder(length + 1);
            GetWindowText(handle, title, title.Capacity);

            string windowTitle = title.ToString().Trim();

            if (!string.IsNullOrWhiteSpace(windowTitle))
            {
                windows.Add(new WindowInfo(windowTitle, handle));
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    public static bool IsWindowValid(IntPtr handle)
    {
        return handle != IntPtr.Zero && IsWindow(handle);
    }

    public sealed class WindowInfo
    {
        public string Title { get; }
        public IntPtr Handle { get; }

        public WindowInfo(string title, IntPtr handle)
        {
            Title = title;
            Handle = handle;
        }

        public override string ToString() => Title;
    }

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(
        EnumWindowsProc callback,
        IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(
        IntPtr handle,
        StringBuilder text,
        int maxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr handle,
        out uint processId);
}