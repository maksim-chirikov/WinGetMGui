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

    public class IgnoredPackage
    {
        public IgnoreScope Scope { get; init; }
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string AvailableVersion { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
    }

    public class IgnoredPackagesStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly List<IgnoredPackage> _entries = new();

        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinGetMGui",
            "ignored.json");

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
                var loaded = JsonSerializer.Deserialize<List<IgnoredPackage>>(File.ReadAllText(FilePath), JsonOptions);
                if (loaded is not null)
                    _entries.AddRange(loaded);
            }
            catch (JsonException)
            {
                _entries.Clear();
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_entries, JsonOptions));
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
                AvailableVersion = package.AvailableVersion,
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

        public bool IsIgnored(PackageInfo package)
        {
            return _entries.Any(e => IdEquals(e.Id, package.Id) &&
                (e.Scope == IgnoreScope.Package ||
                    (e.Scope == IgnoreScope.Version && VersionEquals(e.AvailableVersion, package.AvailableVersion))));
        }

        private static bool IdEquals(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static bool VersionEquals(string a, string b) =>
            a.Trim().Equals(b.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
