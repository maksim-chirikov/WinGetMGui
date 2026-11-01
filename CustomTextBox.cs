// WinGetMGui - minimal GUI wrapper for winget
// Copyright (C) 2026 Maxim Chirikov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace WinGetMGui
{
    public class CustomTextBox : ToolStripTextBox
    {
        private const int GWL_STYLE = -16;
        private const int WS_CLIPCHILDREN = 0x02000000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public event EventHandler? Cleared;
        public event EventHandler? Submitted;
        public event EventHandler? FilterTextChanged;

        private Label? _clearLabel;
        private TextBox? _textBox;

        protected override void OnSubscribeControlEvents(Control? control)
        {
            base.OnSubscribeControlEvents(control);

            if (control is TextBox textBox)
            {
                _textBox = textBox;

                _clearLabel = new Label
                {
                    Text = "\u2715",
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = textBox.BackColor,
                    ForeColor = Color.Gray,
                    Cursor = Cursors.Default,
                    Size = new Size(18, textBox.ClientSize.Height),
                    Visible = false
                };
                _clearLabel.Click += ClearLabel_Click;
                _clearLabel.MouseEnter += (s, e) => _clearLabel.ForeColor = Color.Black;
                _clearLabel.MouseLeave += (s, e) => _clearLabel.ForeColor = Color.Gray;

                textBox.HandleCreated += (s, e) => ApplyClipChildren(textBox);
                ApplyClipChildren(textBox);

                textBox.Controls.Add(_clearLabel);
                textBox.TextChanged += TextBox_TextChanged;
                textBox.Resize += TextBox_Resize;
                textBox.KeyDown += TextBox_KeyDown;

                UpdateClearButton();
            }
        }

        protected override void OnUnsubscribeControlEvents(Control? control)
        {
            if (control is TextBox textBox && _clearLabel is not null)
            {
                textBox.TextChanged -= TextBox_TextChanged;
                textBox.Resize -= TextBox_Resize;
                textBox.KeyDown -= TextBox_KeyDown;
                textBox.Controls.Remove(_clearLabel);
                _clearLabel.Dispose();
                _clearLabel = null;
                _textBox = null;
            }

            base.OnUnsubscribeControlEvents(control);
        }

        private static void ApplyClipChildren(TextBox textBox)
        {
            if (!textBox.IsHandleCreated)
                return;

            var style = GetWindowLong(textBox.Handle, GWL_STYLE);
            SetWindowLong(textBox.Handle, GWL_STYLE, style | WS_CLIPCHILDREN);
        }

        private void TextBox_TextChanged(object? sender, EventArgs e)
        {
            UpdateClearButton();
            FilterTextChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true;
            Submitted?.Invoke(this, EventArgs.Empty);
        }

        private void TextBox_Resize(object? sender, EventArgs e) => UpdateClearButton();

        private void ClearLabel_Click(object? sender, EventArgs e)
        {
            if (_textBox is null)
                return;

            _textBox.Clear();
            _textBox.Focus();
            Cleared?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateClearButton()
        {
            if (_textBox is null || _clearLabel is null)
                return;

            _clearLabel.Size = new Size(18, _textBox.ClientSize.Height);
            _clearLabel.Location = new Point(_textBox.ClientSize.Width - _clearLabel.Width - 1, 0);
            _clearLabel.Visible = _textBox.TextLength > 0;
            _clearLabel.BringToFront();
        }
    }
}
