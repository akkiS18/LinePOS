using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PosElectro.Desktop.Services
{
    /// <summary>
    /// Soat yonidagi (tray) ikonka: X bosilganda dastur shu yerga yashirinadi va skanerni tinglashda davom etadi.
    /// Menyu: "Kassani ochish" va "Dasturdan chiqish".
    /// </summary>
    public sealed class TrayService : IDisposable
    {
        private readonly System.Windows.Forms.NotifyIcon _icon;
        private bool _balloonShown;

        public event Action? OpenRequested;
        public event Action? ExitRequested;

        public TrayService()
        {
            _icon = new System.Windows.Forms.NotifyIcon
            {
                Text = "Line kassa",
                Visible = true
            };
            try
            {
                var exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe)) _icon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
            }
            catch { }
            _icon.Icon ??= System.Drawing.SystemIcons.Application;

            var menu = new System.Windows.Forms.ContextMenuStrip();
            var open = menu.Items.Add("Kassani ochish");
            open.Font = new System.Drawing.Font(open.Font, System.Drawing.FontStyle.Bold);
            open.Click += (_, _) => OpenRequested?.Invoke();
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Dasturdan chiqish").Click += (_, _) => ExitRequested?.Invoke();
            _icon.ContextMenuStrip = menu;
            _icon.MouseClick += (_, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left) OpenRequested?.Invoke();
            };
        }

        /// <summary>Tray ustidagi maslahat (63 belgigacha).</summary>
        public void SetTooltip(string text)
        {
            if (text.Length > 63) text = text.Substring(0, 63);
            _icon.Text = text;
        }

        /// <summary>Birinchi marta trayga yashiringanda bir martalik eslatma.</summary>
        public void ShowHiddenHintOnce()
        {
            if (_balloonShown) return;
            _balloonShown = true;
            try
            {
                _icon.ShowBalloonTip(4000, "Line kassa ishlashda davom etyapti",
                    "Skanerlansa, tovar savatga tushadi. To'liq chiqish: ikonkada o'ng tugma → Dasturdan chiqish.",
                    System.Windows.Forms.ToolTipIcon.Info);
            }
            catch { }
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.Dispose();
        }
    }

    /// <summary>Oynani boshqa dastur (masalan Telegram) ustiga chiqarish.</summary>
    public static class WindowActivator
    {
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);

        public static void BringToFront(Window w)
        {
            if (!w.IsVisible) w.Show();
            if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;

            var hwnd = new WindowInteropHelper(w).Handle;
            try
            {
                // Windows boshqa dastur faol bo'lsa oynani oldinga chiqarishga ruxsat bermaydi —
                // faol oynaning kiritish oqimiga vaqtincha ulanib, ruxsat olinadi.
                var fg = GetForegroundWindow();
                uint fgThread = GetWindowThreadProcessId(fg, IntPtr.Zero);
                uint ours = GetCurrentThreadId();
                bool attached = fgThread != 0 && fgThread != ours && AttachThreadInput(ours, fgThread, true);
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
                if (attached) AttachThreadInput(ours, fgThread, false);
            }
            catch { }

            w.Activate();
            // Zaxira usul: bir lahzaga eng ustki qilib qo'yish
            w.Topmost = true;
            w.Topmost = false;
            w.Focus();
        }
    }
}
