// WinGetMGui - minimal GUI wrapper for winget
// Copyright (C) 2026 Maxim Chirikov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace WinGetMGui
{
    public enum PackageScope
    {
        Default,
        User,
        Machine
    }

    public static class PackageScopeArgument
    {
        public static string? Flag(this PackageScope scope) => scope switch
        {
            PackageScope.Default => null,
            PackageScope.User => "user",
            PackageScope.Machine => "machine",
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
        };
    }

    public class WingetService
    {
        private const string WingetPath = "winget";

        public async Task<List<PackageInfo>> GetInstalledPackagesAsync()
        {
            var output = await ExecuteWingetCommandAsync(await WithOutputLocaleAsync(["list", "--disable-interactivity"]));
            return ParseWingetListOutput(output);
        }

        public async Task<List<PackageInfo>> SearchPackagesAsync(string query)
        {
            ValidateArgument(query, nameof(query));
            var output = await ExecuteWingetCommandAsync(await WithOutputLocaleAsync(["search", query, "--disable-interactivity"]));
            return ParseWingetSearchOutput(output);
        }

        public async Task InstallPackageAsync(string packageId, PackageScope scope)
        {
            ValidateArgument(packageId, nameof(packageId));
            await RunVisibleAsync(await WithOutputLocaleAsync(BuildInstallArguments(packageId, scope)));
        }

        internal static List<string> BuildInstallArguments(string packageId, PackageScope scope)
        {
            var arguments = new List<string> { "install", "--id", packageId };
            if (scope.Flag() is string scopeFlag)
                arguments.AddRange(["--scope", scopeFlag]);
            arguments.AddRange(["--silent", "--disable-interactivity", "--accept-source-agreements", "--accept-package-agreements", "--force"]);
            return arguments;
        }

        public async Task UninstallPackageAsync(string packageId)
        {
            ValidateArgument(packageId, nameof(packageId));
            await RunVisibleAsync(await WithOutputLocaleAsync(["uninstall", "--id", packageId, "--silent", "--disable-interactivity", "--accept-source-agreements", "--force"]));
        }

        public async Task UpdatePackageAsync(string packageId)
        {
            ValidateArgument(packageId, nameof(packageId));
            await RunVisibleAsync(await WithOutputLocaleAsync(["upgrade", "--id", packageId, "--silent", "--disable-interactivity", "--accept-source-agreements", "--accept-package-agreements", "--force"]));
        }

        public async Task UpdateSourcesAsync()
        {
            await ExecuteWingetCommandAsync(await WithOutputLocaleAsync(["source", "update", "--disable-interactivity"]));
        }

        private static readonly SemaphoreSlim OutputLocaleProbeLock = new(1, 1);
        private static bool? outputLocaleSupported;

        internal static async Task<bool> SupportsOutputLocaleAsync()
        {
            if (outputLocaleSupported.HasValue)
                return outputLocaleSupported.Value;

            await OutputLocaleProbeLock.WaitAsync();
            try
            {
                if (outputLocaleSupported.HasValue)
                    return outputLocaleSupported.Value;

                try
                {
                    await ExecuteWingetCommandAsync(["--version", "--output-locale", "en-US"], 15);
                    outputLocaleSupported = true;
                }
                catch (Exception ex) when (ex is WingetException or TimeoutException)
                {
                    outputLocaleSupported = false;
                }
            }
            finally
            {
                OutputLocaleProbeLock.Release();
            }

            return outputLocaleSupported.Value;
        }

        internal static List<string> WithOutputLocale(IReadOnlyList<string> arguments, bool supported) =>
            supported ? ["--output-locale", "en-US", .. arguments] : [.. arguments];

        private static async Task<List<string>> WithOutputLocaleAsync(IReadOnlyList<string> arguments) =>
            WithOutputLocale(arguments, await SupportsOutputLocaleAsync());

        internal static void ValidateArgument(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Argument must not be empty.", paramName);
            if (value.Any(char.IsControl))
                throw new ArgumentException("Argument must not contain control characters.", paramName);
            if (value.StartsWith("-"))
                throw new ArgumentException("Argument must not start with '-'.", paramName);
        }

        private static async Task RunVisibleAsync(IReadOnlyList<string> arguments)
        {
            string command = string.Join(' ', arguments);
            var processStartInfo = new ProcessStartInfo
            {
                FileName = WingetPath,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            foreach (string argument in arguments)
                processStartInfo.ArgumentList.Add(argument);

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new WingetException($"Winget command failed with exit code {process.ExitCode}: {command}");
        }

        private static Process CreateProcess(IReadOnlyList<string> arguments)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = WingetPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };

            foreach (string argument in arguments)
                processStartInfo.ArgumentList.Add(argument);

            return new Process { StartInfo = processStartInfo };
        }

        internal static async Task WaitWithTimeoutAsync(Task exitTask, Action killOnTimeout, int timeoutSeconds, string command)
        {
            if (timeoutSeconds == 0)
            {
                await exitTask;
                return;
            }

            using var timeoutCts = new CancellationTokenSource();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), timeoutCts.Token);
            var completedTask = await Task.WhenAny(exitTask, timeoutTask);
            timeoutCts.Cancel();

            if (completedTask == timeoutTask)
            {
                killOnTimeout();
                throw new TimeoutException($"Winget command {command} timed out after {timeoutSeconds} seconds.");
            }

            await exitTask;
        }

        private static async Task<string> ExecuteWingetCommandAsync(IReadOnlyList<string> arguments, int timeoutSeconds = 60)
        {
            var output = new StringBuilder();
            string command = string.Join(' ', arguments);

            using var process = CreateProcess(arguments);
            process.OutputDataReceived += (s, e) => output.AppendLine(e.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await WaitWithTimeoutAsync(
                process.WaitForExitAsync(),
                () => { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } },
                timeoutSeconds,
                command);

            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new WingetException($"Winget command failed with exit code {process.ExitCode}: {command}");

            return output.ToString();
        }

        internal static List<PackageInfo> ParseWingetListOutput(string output)
        {
            var table = ParseTable(output, ListLayout);
            return Enumerable.Range(0, Rows(table)).Select(i => new PackageInfo
            {
                Kind = PackageKind.Installed,
                Name = GetField(table, "Name", i),
                Id = GetField(table, "Id", i),
                Version = GetField(table, "Version", i),
                AvailableVersion = GetField(table, "Available", i),
                Source = GetField(table, "Source", i)
            }).ToList();
        }

        internal static List<PackageInfo> ParseWingetSearchOutput(string output)
        {
            var table = ParseTable(output, SearchLayout);
            return Enumerable.Range(0, Rows(table)).Select(i => new PackageInfo
            {
                Kind = PackageKind.Catalog,
                Name = GetField(table, "Name", i),
                Id = GetField(table, "Id", i),
                Version = GetField(table, "Version", i),
                Source = GetField(table, "Source", i)
            }).ToList();
        }

        private static readonly string[] RequiredColumns = ["Name", "Id", "Version", "Source"];
        private static readonly string[] ListLayout = ["Name", "Id", "Version", "Available", "Source"];
        private static readonly string[] SearchLayout = ["Name", "Id", "Version", "Match", "Source"];
        private static readonly string[] CommonLayout = ["Name", "Id", "Version", "Source"];

        internal static bool UsesLocalizedOutput() =>
            !CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase);

        private static Dictionary<string, List<string>> ParseTable(string output, string[] layout)
        {
            var table = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 3) return table;

            List<(string Name, int Start)> columns = GetColumns(lines[0]);
            if (!RequiredColumns.All(name => columns.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))))
            {
                var mapped = MapLocalizedColumns(columns, layout);
                if (mapped is null) return table;
                columns = mapped;
            }

            foreach (var column in columns)
                table[column.Name] = new List<string>();

            foreach (string line in lines.Skip(2))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('-')) continue;

                for (int k = 0; k < columns.Count; k++)
                {
                    int end = k + 1 < columns.Count ? columns[k + 1].Start : line.Length;
                    table[columns[k].Name].Add(GetColumn(line, columns[k].Start, end));
                }
            }
            return table;
        }

        private static List<(string Name, int Start)>? MapLocalizedColumns(List<(string Name, int Start)> columns, string[] layout)
        {
            if (!UsesLocalizedOutput()) return null;

            string[]? mapped = columns.Count switch
            {
                int n when n == layout.Length => layout,
                int n when n == CommonLayout.Length => CommonLayout,
                _ => null,
            };
            if (mapped is null) return null;

            return Enumerable.Range(0, mapped.Length).Select(i => (mapped[i], columns[i].Start)).ToList();
        }

        private static List<(string Name, int Start)> GetColumns(string headerLine)
        {
            var columns = new List<(string Name, int Start)>();
            int i = 0;
            while (i < headerLine.Length)
            {
                if (headerLine[i] != ' ')
                {
                    int start = i;
                    while (i < headerLine.Length && headerLine[i] != ' ') i++;
                    columns.Add((headerLine.Substring(start, i - start), start));
                }
                else
                {
                    i++;
                }
            }
            return columns;
        }

        private static int Rows(Dictionary<string, List<string>> table) =>
            table.TryGetValue("Name", out var names) ? names.Count : 0;

        private static string GetField(Dictionary<string, List<string>> table, string name, int row) =>
            table.TryGetValue(name, out var values) ? values[row] : string.Empty;

        private static string GetColumn(string line, int start, int end)
        {
            if (start >= line.Length) return string.Empty;
            if (end <= start) return string.Empty;
            if (end > line.Length) end = line.Length;
            return line.Substring(start, end - start).Trim();
        }
    }
}
