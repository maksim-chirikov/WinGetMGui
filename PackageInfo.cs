// WinGetMGui - minimal GUI wrapper for winget
// Copyright (C) 2026 Maxim Chirikov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace WinGetMGui
{
    public enum PackageVerb
    {
        Install,
        Uninstall,
        Update
    }

    public static class PackageVerbProgress
    {
        public static string ProgressStatus(this PackageVerb verb) => verb switch
        {
            PackageVerb.Install => "Installing",
            PackageVerb.Uninstall => "Uninstalling",
            PackageVerb.Update => "Updating",
            _ => throw new ArgumentOutOfRangeException(nameof(verb)),
        };
    }

    public class PackageInfo
    {
        public string Name { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public string AvailableVersion { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
        public string Id { get; init; } = string.Empty;
        public bool Ignored { get; set; }

        public bool HasUpdate => !string.IsNullOrWhiteSpace(AvailableVersion);
    }
}
