// WinGetMGui - minimal GUI wrapper for winget
// Copyright (C) 2026 Maxim Chirikov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;

namespace WinGetMGui
{
    public class WingetOptions
    {
        public bool Silent { get; set; } = true;
        public bool AcceptSourceAgreements { get; set; } = true;
        public bool AcceptPackageAgreements { get; set; } = true;
        public bool Force { get; set; }
    }

    public class WingetOptionsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
        };

        private sealed class SettingsFile
        {
            public WingetOptions? WingetOptions { get; init; }
        }

        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinGetMGui",
            "settings.json");

        public string FilePath { get; }

        public WingetOptions Options { get; private set; } = new();

        public bool Changed { get; internal set; }

        public WingetOptionsStore(string path)
        {
            FilePath = path;
        }

        public void Load()
        {
            Options = new WingetOptions();
            Changed = false;
            if (!File.Exists(FilePath))
                return;

            try
            {
                var loaded = JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(FilePath), JsonOptions);
                if (loaded?.WingetOptions is not null)
                    Options = loaded.WingetOptions;
            }
            catch (JsonException)
            {
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new SettingsFile { WingetOptions = Options }, JsonOptions));
            Changed = false;
        }
    }
}
