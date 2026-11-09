// WinGetMGui - minimal GUI wrapper for winget
// Copyright (C) 2026 Maxim Chirikov
// SPDX-License-Identifier: GPL-3.0-or-later

using WinGetMGui;
using Xunit;

namespace WinGetMGui.Tests
{
    public class SearchByRules
    {
        [Theory]
        [InlineData(SearchBy.Default, null)]
        [InlineData(SearchBy.Name, "--name")]
        [InlineData(SearchBy.Id, "--id")]
        [InlineData(SearchBy.Moniker, "--moniker")]
        [InlineData(SearchBy.Tag, "--tag")]
        public void SearchByMapsToWingetQueryOption(SearchBy searchBy, string? expected)
        {
            Assert.Equal(expected, searchBy.Flag());
        }

        [Theory]
        [InlineData(SearchBy.Default, null)]
        [InlineData(SearchBy.Name, "--name")]
        [InlineData(SearchBy.Id, "--id")]
        [InlineData(SearchBy.Moniker, "--moniker")]
        [InlineData(SearchBy.Tag, "--tag")]
        public void SearchArgumentsPlaceQueryOptionAfterSubcommand(SearchBy searchBy, string? flag)
        {
            var expected = new List<string> { "search" };
            if (flag is not null)
                expected.Add(flag);
            expected.Add("Git");

            Assert.Equal(expected, WingetService.BuildSearchArguments("Git", searchBy));

            var withLocale = WingetService.WithOutputLocale(WingetService.BuildSearchArguments("Git", searchBy), true);
            Assert.Equal(["--output-locale", "en-US", .. expected], withLocale);
        }

        [Fact]
        public void SearchQueryRejectedLikePackageId()
        {
            Assert.Throws<ArgumentException>(() => WingetService.ValidateArgument("--name", "query"));
        }
    }
}
