// WinGetMGui - minimal GUI wrapper for winget
// Copyright (C) 2026 Maxim Chirikov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections;

namespace WinGetMGui
{
    // The installed list and its Id index (case-insensitive: winget's Id casing is display-only). The caller's
    // list is kept as given, not copied - replace this object on a reload, never mutate the rows it was built from.
    public sealed class InstalledPackages : IEnumerable<PackageInfo>
    {
        public static InstalledPackages Empty { get; } = new(Array.Empty<PackageInfo>());

        private readonly IReadOnlyList<PackageInfo> _rows;
        private readonly Dictionary<string, PackageInfo> _byId;

        public InstalledPackages(IReadOnlyList<PackageInfo> installed)
        {
            _rows = installed;

            // Indexer assignment, not ToDictionary: a duplicate installed Id keeps the last row, never throws.
            _byId = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var package in _rows)
                _byId[package.Id] = package;
        }

        public PackageInfo? Find(string id) => _byId.GetValueOrDefault(id);

        // The Install-tab join: the catalog package plus the installed version by Id (blank = not installed).
        public CatalogRow Match(PackageInfo catalog) => new(catalog, Find(catalog.Id)?.Version ?? string.Empty);

        public IEnumerator<PackageInfo> GetEnumerator() => _rows.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // The Install-tab row: a catalog package plus the live installed state, never stamped onto the package
    // itself (the same projection shape as IgnoredRow).
    public sealed record CatalogRow(PackageInfo Package, string InstalledVersion) : IPackageRow
    {
        public string Name => Package.Name;
        public string Id => Package.Id;
        public string Version => InstalledVersion;
        public string AvailableVersion => Package.AvailableVersion;
        public string Source => Package.Source;
    }
}
