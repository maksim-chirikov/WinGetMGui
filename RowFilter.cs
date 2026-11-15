// WinGetMGui - minimal GUI wrapper for winget
// Copyright (C) 2026 Maxim Chirikov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace WinGetMGui
{
    public static class RowFilter
    {
        // Hot path (one call per row per filter keystroke): checks the row's five shared columns.
        public static bool Matches(string query, IPackageRow row)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            return row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Source.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Version.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.AvailableVersion.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }
}
