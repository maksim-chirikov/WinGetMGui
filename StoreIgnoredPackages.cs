// WinGetMGui - minimal GUI wrapper for winget
// Copyright (C) 2026 Maxim Chirikov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;

namespace WinGetMGui
{
    public enum IgnoreScope
    {
        Package,
        Version,
    }

    // The ignore rule exactly as it lives in ignored.json: what is ignored, plus the version that was
    // current at ignore-time. Immutable - the store never rewrites a rule, so nothing derived from the
    // installed list can leak into the file.
    public sealed record IgnoredPackage
    {
        public IgnoreScope Scope { get; init; }
        public string Id { get; init; } = string.Empty;

        // The fixed version ignored at ignore-time; shown in the Version column on the Ignored tab.
        public string Version { get; init; } = string.Empty;

        // Name/Source are snapshots taken at ignore-time: they are what the Ignored tab can show when the
        // package is no longer installed (the live values come from the installed list, see IgnoredRow).
        public string Name { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
    }

    // One row on the Ignored tab: the stored rule plus the live state read from the installed list.
    // Built by IgnoredPackagesStore.BuildRows, never persisted.
    public sealed record IgnoredRow(IgnoredPackage Rule, string Name, string AvailableVersion) : IPackageRow
    {
        public string Id => Rule.Id;
        public string Version => Rule.Version;
        public string Source => Rule.Source;
        public IgnoreScope Scope => Rule.Scope;
    }

    public class IgnoredPackagesStore
    {
        private readonly List<IgnoredPackage> _entries = new();

        public static string DefaultPath => StoreCommon.AppDataFilePath("ignored.json");

        public string FilePath { get; }

        public IReadOnlyList<IgnoredPackage> Entries => _entries;

        public bool Changed { get; private set; }

        public IgnoredPackagesStore(string path)
        {
            FilePath = path;
        }

        // The file shape, kept apart from the rule so the format can differ from the model: files written
        // before the fixed version had its own field keep it in AvailableVersion.
        private sealed record IgnoredFileEntry
        {
            public IgnoreScope Scope { get; init; }
            public string Id { get; init; } = string.Empty;
            public string Version { get; init; } = string.Empty;
            public string AvailableVersion { get; init; } = string.Empty;
            public string Name { get; init; } = string.Empty;
            public string Source { get; init; } = string.Empty;

            public IgnoredPackage ToRule() => new()
            {
                Scope = Scope,
                Id = Id,
                Name = Name,
                Source = Source,
                // The Version field wins; only old Version-less files fall back to their AvailableVersion.
                Version = string.IsNullOrWhiteSpace(Version) ? AvailableVersion : Version,
            };
        }

        public void Load()
        {
            _entries.Clear();
            Changed = false;
            if (!File.Exists(FilePath))
                return;

            try
            {
                var loaded = JsonSerializer.Deserialize<List<IgnoredFileEntry>>(File.ReadAllText(FilePath), StoreCommon.JsonOptions);
                if (loaded is not null)
                    _entries.AddRange(loaded.Select(e => e.ToRule()));
            }
            catch (JsonException)
            {
                _entries.Clear();
            }
        }

        public void Save()
        {
            StoreCommon.SaveJson(FilePath, _entries);
            Changed = false;
        }

        public void Add(PackageInfo package, IgnoreScope scope)
        {
            _entries.RemoveAll(e => IdEquals(e.Id, package.Id) && e.Scope == scope);
            _entries.Add(new IgnoredPackage
            {
                Scope = scope,
                Id = package.Id,
                Name = package.Name,
                Version = package.AvailableVersion,
                Source = package.Source,
            });
            Changed = true;
        }

        public bool Remove(IgnoredPackage rule)
        {
            var removed = _entries.RemoveAll(e => IdEquals(e.Id, rule.Id) &&
                (e.Scope == rule.Scope || (rule.Scope == IgnoreScope.Package && e.Scope == IgnoreScope.Version)));
            Changed |= removed > 0;
            return removed > 0;
        }

        // The Ignored-tab rows: one row per rule, with the live state read from the installed list.
        // Built by MainForm.RecalcIgnored when its inputs change (store load, installed reload, ignore
        // change), not on every search keystroke - reading rows never changes the store. Name comes from the
        // installed list (falls back to the stored snapshot); AvailableVersion shows only a version differing
        // from the fixed Version (trimmed, like IsIgnored) - a package no longer installed shows none.
        public IReadOnlyList<IgnoredRow> BuildRows(IEnumerable<PackageInfo> installed)
        {
            // Built with the indexer so a duplicate installed Id keeps the last one instead of throwing.
            var installedById = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var package in installed)
                installedById[package.Id] = package;

            var rows = new List<IgnoredRow>(_entries.Count);
            foreach (var rule in _entries)
            {
                installedById.TryGetValue(rule.Id, out var match);

                string name = !string.IsNullOrWhiteSpace(match?.Name) ? match!.Name : rule.Name;
                string availableVersion = match is not null && !VersionEquals(match.AvailableVersion, rule.Version)
                    ? match.AvailableVersion
                    : string.Empty;

                rows.Add(new IgnoredRow(rule, name, availableVersion));
            }
            return rows;
        }

        public bool IsIgnored(PackageInfo package)
        {
            return _entries.Any(e => IdEquals(e.Id, package.Id) &&
                (e.Scope == IgnoreScope.Package ||
                    (e.Scope == IgnoreScope.Version && VersionEquals(e.Version, package.AvailableVersion))));
        }

        private static bool IdEquals(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static bool VersionEquals(string a, string b) =>
            a.Trim().Equals(b.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
