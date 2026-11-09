// WinGetMGui - minimal GUI wrapper for winget
// Copyright (C) 2026 Maxim Chirikov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace WinGetMGui
{
    internal static class WingetFlags
    {
        internal const string Id = "--id";
        internal const string Name = "--name";
        internal const string Moniker = "--moniker";
        internal const string Tag = "--tag";
        internal const string Scope = "--scope";
        internal const string Silent = "--silent";
        internal const string AcceptSourceAgreements = "--accept-source-agreements";
        internal const string AcceptPackageAgreements = "--accept-package-agreements";
        internal const string Force = "--force";
        internal const string IncludeUnknown = "--include-unknown";
        internal const string DisableInteractivity = "--disable-interactivity";
        internal const string Wait = "--wait";
        internal const string OutputLocale = "--output-locale";
        internal const string OutputLocaleEnUs = "en-US";
    }
}
