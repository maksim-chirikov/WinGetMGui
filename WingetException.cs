// WinGetMGui - minimal GUI wrapper for winget
// Copyright (C) 2026 Maxim Chirikov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace WinGetMGui
{
    public class WingetException : Exception
    {
        public WingetException(string message) : base(message)
        {
        }
    }
}
