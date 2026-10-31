using VanTools.Core;

namespace VanTest;

public sealed class ArgumentParserTests {
    [Fact]
    public void Parse_SeparateValues_ReturnsBoth() {
        ParsedArguments parsed = CreateParser().Parse(["--s", "a", "--t", "b"]);

        Assert.Equal("a", parsed.Get("s"));
        Assert.Equal("b", parsed.Get("t"));
    }

    [Fact]
    public void Parse_EqualsForm_ReturnsBoth() {
        ParsedArguments parsed = CreateParser().Parse(["--s=a", "--t=b"]);

        Assert.Equal("a", parsed.Get("s"));
        Assert.Equal("b", parsed.Get("t"));
    }

    [Fact]
    public void Parse_MixedForms_ReturnsBoth() {
        ParsedArguments parsed = CreateParser().Parse(["--s=a", "--t", "b"]);

        Assert.Equal("a", parsed.Get("s"));
        Assert.Equal("b", parsed.Get("t"));
    }

    [Fact]
    public void Parse_AnyOrder_ReturnsBoth() {
        ParsedArguments parsed = CreateParser().Parse(["--t", "b", "--s", "a"]);

        Assert.Equal("a", parsed.Get("s"));
        Assert.Equal("b", parsed.Get("t"));
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Parse_HelpFlag_ThrowsHelpRequested(string flag) {
        Assert.Throws<HelpRequestedException>(() => CreateParser().Parse([flag]));
    }

    [Fact]
    public void Parse_HelpFlagAfterOptions_ThrowsHelpRequested() {
        Assert.Throws<HelpRequestedException>(() => CreateParser().Parse(["--s", "a", "--help"]));
    }

    [Fact]
    public void Parse_UnknownOption_ThrowsUsageExceptionNamingIt() {
        UsageException exception = Assert.Throws<UsageException>(
            () => CreateParser().Parse(["--s", "a", "--t", "b", "--x", "c"]));

        Assert.Contains("--x", exception.Message);
    }

    [Fact]
    public void Parse_MissingRequiredOption_ThrowsUsageExceptionNamingIt() {
        UsageException exception = Assert.Throws<UsageException>(
            () => CreateParser().Parse(["--s", "a"]));

        Assert.Contains("--t", exception.Message);
    }

    [Fact]
    public void Parse_MissingValue_ThrowsUsageException() {
        UsageException exception = Assert.Throws<UsageException>(
            () => CreateParser().Parse(["--s", "a", "--t"]));

        Assert.Contains("--t", exception.Message);
    }

    [Fact]
    public void Parse_DuplicateOption_ThrowsUsageException() {
        Assert.Throws<UsageException>(
            () => CreateParser().Parse(["--s", "a", "--s", "b", "--t", "c"]));
    }

    [Fact]
    public void Get_UndefinedOption_ThrowsKeyNotFoundException() {
        ParsedArguments parsed = CreateParser().Parse(["--s", "a", "--t", "b"]);

        Assert.Throws<KeyNotFoundException>(() => parsed.Get("missing"));
    }

    [Fact]
    public void RenderHelp_IncludesOptionsAndDescriptions() {
        string help = CreateParser().RenderHelp("pack", "Pack a plugin directory into a .van archive.");

        Assert.Contains("Usage: pack", help);
        Assert.Contains("Pack a plugin directory into a .van archive.", help);
        Assert.Contains("--s <value>", help);
        Assert.Contains("source directory", help);
        Assert.Contains("--t <value>", help);
        Assert.Contains("target directory", help);
        Assert.Contains("--help", help);
    }

    private static ArgumentParser CreateParser() =>
        new ArgumentParser()
            .Required("s", "source directory")
            .Required("t", "target directory");
}
