using PosElectro.Desktop.Services;
using Xunit;

namespace PosElectro.Desktop.Tests
{
    /// <summary>Tema tanlovi bazada matn sifatida saqlanadi; noma'lum qiymat yorug' temaga tushadi.</summary>
    public sealed class ThemeSettingTests
    {
        [Theory]
        [InlineData("light", ThemeMode.Light)]
        [InlineData("dark", ThemeMode.Dark)]
        [InlineData("system", ThemeMode.System)]
        [InlineData(" Dark ", ThemeMode.Dark)]
        [InlineData("", ThemeMode.Light)]
        [InlineData(null, ThemeMode.Light)]
        [InlineData("garbage", ThemeMode.Light)]
        public void Parses_saved_setting(string? saved, ThemeMode expected) => Assert.Equal(expected, ThemeManager.Parse(saved));

        [Theory]
        [InlineData(ThemeMode.Light)]
        [InlineData(ThemeMode.Dark)]
        [InlineData(ThemeMode.System)]
        public void Round_trips(ThemeMode mode) => Assert.Equal(mode, ThemeManager.Parse(ThemeManager.ToSetting(mode)));
    }
}
