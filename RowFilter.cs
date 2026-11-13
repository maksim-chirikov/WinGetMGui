// WinGetMGui - minimal GUI wrapper for winget
// Copyright (C) 2026 Maxim Chirikov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace WinGetMGui
{
    public static class RowFilter
    {
        public static bool Matches(string? query, PackageInfo p) =>
            Matches(query, p.Name, p.Id, p.Source, p.Version, p.AvailableVersion);

        public static bool Matches(string? query, IgnoredPackage e) =>
            Matches(query, e.Name, e.Id, e.Source, e.Version, e.AvailableVersion);

        public static bool Matches(string? query, params string?[] fields)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            var q = query.Trim();
            return fields.Any(f => f is not null && f.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
    }
}
