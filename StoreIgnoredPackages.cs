// WinGetMGui - minimal GUI wrapper for winget
// Copyright (C) 2026 Maxim Chirikov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinGetMGui
{
    public enum IgnoreScope
    {
        Package,
        Version,
    }

    public class IgnoredPackage : IPackageRow
    {
        public IgnoreScope Scope { get; init; }
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;

        // The fixed version ignored at ignore-time; shown in the Version column on the Ignored tab.
        public string Version { get; init; } = string.Empty;

        // Not saved: refreshed at runtime from the installed list. WhenWriting keeps old files (which
        // stored the ignored version in AvailableVersion) readable.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
        public string AvailableVersion { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
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

        public void Load()
        {
            _entries.Clear();
            Changed = false;
            if (!File.Exists(FilePath))
                return;

            try
            {
                var loaded = JsonSerializer.Deserialize<List<IgnoredPackage>>(File.ReadAllText(FilePath), StoreCommon.JsonOptions);
                if (loaded is not null)
                    // New files store the fixed version in Version; it wins on load, and only
                    // old Version-less files fall back to their AvailableVersion.
                    // AvailableVersion is not set here; it is refreshed at runtime via UpdateFromInstalled.
                    _entries.AddRange(loaded.Select(e =>
                    {
                        string version = string.IsNullOrWhiteSpace(e.Version) ? e.AvailableVersion : e.Version;
                        return new IgnoredPackage
                        {
                            Scope = e.Scope,
                            Id = e.Id,
                            Name = e.Name,
                            Version = version,
                            AvailableVersion = string.Empty,
                            Source = e.Source
                        };
                    }));
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

        public bool Remove(IgnoredPackage entry)
        {
            var removed = _entries.RemoveAll(e => IdEquals(e.Id, entry.Id) &&
                (e.Scope == entry.Scope || (entry.Scope == IgnoreScope.Package && e.Scope == IgnoreScope.Version)));
            Changed |= removed > 0;
            return removed > 0;
        }

        public void UpdateFromInstalled(IEnumerable<PackageInfo> installed)
        {
            // Build the dictionary manually to handle potential duplicate IDs safely.
            var installedDict = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in installed)
            {
                // Using the indexer [p.Id] ensures that if a duplicate ID exists, 
                // the last one encountered simply overwrites the previous one instead of throwing.
                installedDict[p.Id] = p;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                installedDict.TryGetValue(entry.Id, out var match);

                // Derived refresh: Name comes from the installed list (falls back to the stored
                // snapshot); AvailableVersion shows only a version differing from the fixed
                // Version (trimmed, like IsIgnored) - a package no longer installed shows none.
                string name = !string.IsNullOrWhiteSpace(match?.Name) ? match!.Name : entry.Name;
                string availableVersion = match is not null && !VersionEquals(match.AvailableVersion, entry.Version)
                    ? match.AvailableVersion
                    : string.Empty;

                if (name == entry.Name && availableVersion == entry.AvailableVersion)
                    continue;

                _entries[i] = new IgnoredPackage
                {
                    Scope = entry.Scope,
                    Id = entry.Id,
                    Name = name,
                    Version = entry.Version,
                    AvailableVersion = availableVersion,
                    Source = entry.Source
                };
            }
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
