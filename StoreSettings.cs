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
        public bool IncludeUnknown { get; set; } = true;
        public bool Force { get; set; }
        public bool DisableInteractivity { get; set; } = true;
        public bool Wait { get; set; } = true;
    }

    public class StoreSettings
    {
        private sealed class SettingsFile
        {
            public WingetOptions? WingetOptions { get; init; }
        }

        public static string DefaultPath => StoreCommon.AppDataFilePath("settings.json");

        public string FilePath { get; }

        public WingetOptions WingetOptions { get; private set; } = new();

        public bool Changed { get; internal set; }

        public StoreSettings(string path)
        {
            FilePath = path;
        }

        public void Load()
        {
            WingetOptions = new WingetOptions();
            Changed = false;
            if (!File.Exists(FilePath))
                return;

            try
            {
                var loaded = JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(FilePath), StoreCommon.JsonOptions);
                if (loaded?.WingetOptions is not null)
                    WingetOptions = loaded.WingetOptions;
            }
            catch (JsonException)
            {
            }
        }

        public void Save()
        {
            StoreCommon.SaveJson(FilePath, new SettingsFile { WingetOptions = WingetOptions });
            Changed = false;
        }
    }
}
