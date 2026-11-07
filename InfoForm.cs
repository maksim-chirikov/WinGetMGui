// WinGetMGui - minimal GUI wrapper for winget
// Copyright (C) 2026 Maxim Chirikov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace WinGetMGui
{
    public class InfoForm : Form
    {
        private static readonly Font ContentFont = new(FontFamily.GenericMonospace, 9f);

        public InfoForm(string title, string text)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            ClientSize = new Size(900, 640);
            MinimumSize = new Size(360, 240);

            var content = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = ContentFont,
                Text = text,
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(6),
            };
            var closeButton = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                AutoSize = true,
            };
            buttons.Controls.Add(closeButton);

            Controls.Add(content);
            Controls.Add(buttons);

            AcceptButton = closeButton;
            CancelButton = closeButton;

            // Focus the Close button, not the text box: an active TextBox selects all its text on open.
            ActiveControl = closeButton;
        }
    }
}
