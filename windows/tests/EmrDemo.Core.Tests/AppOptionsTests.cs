using EmrDemo.Core;

namespace EmrDemo.Core.Tests;

public class AppOptionsTests
{
    const string DefaultDir = "/default/data";

    [Fact]
    public void NoArgs_UsesStandardModeAndDefaultDataDir()
    {
        var options = AppOptions.Parse([], DefaultDir);

        Assert.Equal(UiMode.Standard, options.UiMode);
        Assert.Equal(DefaultDir, options.DataDir);
    }

    [Theory]
    [InlineData("--ui=standard", UiMode.Standard)]
    [InlineData("--ui=custom", UiMode.Custom)]
    [InlineData("--ui=CUSTOM", UiMode.Custom)]
    public void UiArg_SelectsMode(string arg, UiMode expected)
    {
        Assert.Equal(expected, AppOptions.Parse([arg], DefaultDir).UiMode);
    }

    [Fact]
    public void DataDirArg_OverridesDefault()
    {
        var options = AppOptions.Parse(["--data-dir=C:\\probe\\run 1"], DefaultDir);

        Assert.Equal("C:\\probe\\run 1", options.DataDir);
    }

    [Theory]
    [InlineData("--ui=vision")]
    [InlineData("--ui=")]
    [InlineData("--data-dir=")]
    [InlineData("--layout=v2")]
    [InlineData("custom")]
    public void InvalidArg_Throws(string arg)
    {
        Assert.Throws<ArgumentException>(() => AppOptions.Parse([arg], DefaultDir));
    }

    [Fact]
    public void RepeatedArgs_LastWins()
    {
        var options = AppOptions.Parse(["--ui=custom", "--data-dir=a", "--ui=standard", "--data-dir=b"], DefaultDir);

        Assert.Equal(UiMode.Standard, options.UiMode);
        Assert.Equal("b", options.DataDir);
    }
}
