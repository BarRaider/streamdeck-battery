using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace Battery.Internal
{
    class ToolbarScanner
    {
        public static List<string> ScanToolbarButtons()
        {
            List<string> titles = new List<string>();

            try
            {
                var handle = GetSystemTrayHandle();
                if (handle == IntPtr.Zero)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, "ScanToolbarButtons - GetSystemTrayHandle returned null");
                    return null;
                }

                var count = SendMessage(handle, TB_BUTTONCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
                if (count == 0)
                {
                    titles = AETaskbarScan();
                    if (titles == null || titles.Count == 0)
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, "ScanToolbarButtons - SendMessage returned null & AETaskbarScan failed");
                        return null;
                    }
                    return titles;                }

                GetWindowThreadProcessId(handle, out var pid);
                var hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, pid);
                if (hProcess == IntPtr.Zero)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, "ScanToolbarButtons - OpenProcess returned null");
                    return null;
                }

                var size = (IntPtr)Marshal.SizeOf<TBBUTTONINFOW>();
                var buffer = VirtualAllocEx(hProcess, IntPtr.Zero, size, MEM_COMMIT, PAGE_READWRITE);
                if (buffer == IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                    Logger.Instance.LogMessage(TracingLevel.ERROR, "ScanToolbarButtons - VirtualAllocEx returned null");
                    return null;
                }

                for (int i = 0; i < count; i++)
                {
                    var btn = new TBBUTTONINFOW();
                    btn.cbSize = size.ToInt32();
                    btn.dwMask = TBIF_BYINDEX | TBIF_COMMAND;
                    if (WriteProcessMemory(hProcess, buffer, ref btn, size, out var written))
                    {
                        // we want the identifier
                        var res = SendMessage(handle, TB_GETBUTTONINFOW, (IntPtr)i, buffer);
                        if (res.ToInt32() >= 0)
                        {
                            if (ReadProcessMemory(hProcess, buffer, ref btn, size, out var read))
                            {
                                // now get display text using the identifier
                                // first pass we ask for size
                                var textSize = SendMessage(handle, TB_GETBUTTONTEXTW, (IntPtr)btn.idCommand, IntPtr.Zero);
                                if (textSize.ToInt32() != -1)
                                {
                                    // we need to allocate for the terminating zero and unicode
                                    var utextSize = (IntPtr)((1 + textSize.ToInt32()) * 2);
                                    var textBuffer = VirtualAllocEx(hProcess, IntPtr.Zero, utextSize, MEM_COMMIT, PAGE_READWRITE);
                                    if (textBuffer != IntPtr.Zero)
                                    {
                                        res = SendMessage(handle, TB_GETBUTTONTEXTW, (IntPtr)btn.idCommand, textBuffer);
                                        if (res == textSize)
                                        {
                                            var localBuffer = Marshal.AllocHGlobal(utextSize.ToInt32());
                                            if (ReadProcessMemory(hProcess, textBuffer, localBuffer, utextSize, out read))
                                            {
                                                var text = Marshal.PtrToStringUni(localBuffer);
                                                titles.Add(text);
                                            }
                                            Marshal.FreeHGlobal(localBuffer);
                                        }
                                        VirtualFreeEx(hProcess, textBuffer, IntPtr.Zero, MEM_RELEASE);
                                    }
                                }
                            }
                        }
                    }
                }

                VirtualFreeEx(hProcess, buffer, IntPtr.Zero, MEM_RELEASE);
                CloseHandle(hProcess);

                return titles;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"ScanToolbarButtons Exception: {ex}");
                return null;
            }
        }

        private static IntPtr GetSystemTrayHandle()
        {
            var hwnd = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null);
            hwnd = FindWindowEx(hwnd, IntPtr.Zero, "TrayNotifyWnd", null);
            hwnd = FindWindowEx(hwnd, IntPtr.Zero, "SysPager", null);
            return FindWindowEx(hwnd, IntPtr.Zero, "ToolbarWindow32", null);
        }

        private static List<string> AETaskbarScan()
        {
            List<string> taskbarItems = new List<string>();
            try
            {
                var trayWnd = AutomationElement.RootElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
                if (trayWnd == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"AETaskbarScan Shell_TrayWnd returned null");
                    return null;
                }
                var panes = trayWnd.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Pane));
                foreach (var item in panes)
                {
                    if (item is AutomationElement pane)
                    {
                        foreach (var button in pane.EnumChildButtons())
                        {
                            if (button is AutomationElement ae)
                            taskbarItems.Add(ae.GetCurrentPropertyValue(AutomationElement.NameProperty).ToString());
                        }
                    }
                }
                return taskbarItems;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"ToolbarScanner.AETaskbarScan Exception: {ex}");
            }
            return null;
        }


        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, ref TBBUTTONINFOW lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, ref TBBUTTONINFOW lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("user32", SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, int flAllocationType, int flProtect);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, int dwFreeType);

        [DllImport("user32")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpClassName, string lpWindowName);

        private const int TBIF_BYINDEX = unchecked((int)0x80000000); // this specifies that the wparam in Get/SetButtonInfo is an index, not id
        private const int TBIF_COMMAND = 0x20;
        private const int MEM_COMMIT = 0x1000;
        private const int MEM_RELEASE = 0x8000;
        private const int PAGE_READWRITE = 0x4;
        private const int TB_GETBUTTONINFOW = 1087;
        private const int TB_GETBUTTONTEXTW = 1099;
        private const int TB_BUTTONCOUNT = 1048;

        private static bool IsWindowsVistaOrAbove() => Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Major >= 6;
        private static int PROCESS_ALL_ACCESS => IsWindowsVistaOrAbove() ? 0x001FFFFF : 0x001F0FFF;

        [StructLayout(LayoutKind.Sequential)]
        private struct TBBUTTONINFOW
        {
            public int cbSize;
            public int dwMask;
            public int idCommand;
            public int iImage;
            public byte fsState;
            public byte fsStyle;
            public short cx;
            public IntPtr lParam;
            public IntPtr pszText;
            public int cchText;
        }
    }
}
