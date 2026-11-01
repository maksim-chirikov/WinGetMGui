// WinGetMGui - minimal GUI wrapper for winget
// Copyright (C) 2026 Maxim Chirikov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace WinGetMGui
{
    public partial class MainForm
    {
        private const int WM_SYSCOMMAND = 0x0112;
        private const int MF_STRING = 0x0000;
        private const int MF_SEPARATOR = 0x0800;

        // WM_SYSCOMMAND wParam keeps only bits above 0xFFF0 (low 4 bits carry extended info),
        // so the custom id must be mask-aligned; 0x1000 does not collide with any SC_* value.
        private const int AboutCommandId = 0x1000;

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, int uFlags, int uID, string lpNewString);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            AddAboutToSystemMenu();
        }

        private void AddAboutToSystemMenu()
        {
            IntPtr hMenu = GetSystemMenu(Handle, false);
            if (hMenu == IntPtr.Zero)
                return;

            if (!AppendMenu(hMenu, MF_SEPARATOR, 0, "") ||
                !AppendMenu(hMenu, MF_STRING, AboutCommandId, "About"))
                return;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SYSCOMMAND && (m.WParam.ToInt64() & 0xFFF0) == AboutCommandId)
            {
                ShowAbout();
                return;
            }

            base.WndProc(ref m);
        }

        private void ShowAbout()
        {
            var version = GetType().Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            MessageBox.Show(
                this,
                $"WinGet Minimal GUI {version}\n\nCopyright (C) 2026 Maxim Chirikov\nLicensed under GPL-3.0-or-later\n\nThis program comes with ABSOLUTELY NO WARRANTY.\nThis is free software, and you are welcome to redistribute it\nunder certain conditions.",
                "About WinGetMGui",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
