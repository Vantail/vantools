using VanTools.Core;

namespace VanTest;

[Collection("console")]
public sealed class PackCommandIntegrationTests {
    [Fact]
    public void Pack_SingleFile_WritesManifestAndEntry() {
        using TempDirectory temp = new();
        string source = PackTestHelper.CreateSource(temp);
        PackTestHelper.WriteDistFile(source, "app.ts", "// app");
        string target = PackTestHelper.CreateTarget(temp);

        PackResult result = PackTestHelper.Run("--s", source, "--t", target);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Empty(result.StandardError);

        IReadOnlyList<string> entries = PackTestHelper.ReadArchiveEntries(target);
        Assert.Equal(2, entries.Count);
        Assert.Contains("manifest.json", entries);
        Assert.Contains("dist/app.ts", entries);
    }

    [Fact]
    public void Pack_NestedDirectory_PreservesPath() {
        using TempDirectory temp = new();
        string source = PackTestHelper.CreateSource(temp);
        PackTestHelper.WriteDistFile(source, "app.ts");
        PackTestHelper.WriteDistFile(source, "a/b/c/file.txt", "deep");
        string target = PackTestHelper.CreateTarget(temp);

        PackResult result = PackTestHelper.Run("--s", source, "--t", target);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Empty(result.StandardError);
        Assert.Contains("dist/a/b/c/file.txt", PackTestHelper.ReadArchiveEntries(target));
    }

    [Fact]
    public void Pack_MixedDepths_WritesEveryEntry() {
        using TempDirectory temp = new();
        string source = PackTestHelper.CreateSource(temp);
        PackTestHelper.WriteDistFile(source, "app.ts");
        PackTestHelper.WriteDistFile(source, "one.txt");
        PackTestHelper.WriteDistFile(source, "a/two.txt");
        PackTestHelper.WriteDistFile(source, "a/b/three.txt");
        string target = PackTestHelper.CreateTarget(temp);

        PackResult result = PackTestHelper.Run("--s", source, "--t", target);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Empty(result.StandardError);

        IReadOnlyList<string> entries = PackTestHelper.ReadArchiveEntries(target);
        Assert.Contains("dist/one.txt", entries);
        Assert.Contains("dist/a/two.txt", entries);
        Assert.Contains("dist/a/b/three.txt", entries);
    }

    [Fact]
    public void Pack_DistWithOnlyGitkeep_WritesManifestAndGitkeep() {
        using TempDirectory temp = new();
        string source = PackTestHelper.CreateSource(
            temp,
            """{ "name": "sample", "version": "0.1.0", "entrypoint": ".gitkeep" }""");
        PackTestHelper.WriteDistFile(source, ".gitkeep");
        string target = PackTestHelper.CreateTarget(temp);

        PackResult result = PackTestHelper.Run("--s", source, "--t", target);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Empty(result.StandardError);

        IReadOnlyList<string> entries = PackTestHelper.ReadArchiveEntries(target);
        Assert.Equal(2, entries.Count);
        Assert.Contains("manifest.json", entries);
        Assert.Contains("dist/.gitkeep", entries);
    }

    [Fact]
    public void Pack_ExampleProject_IsReadOnly() {
        using TempDirectory temp = new();
        string source = Path.Combine(PackTestHelper.FindRepositoryRoot(), "examples", "empty-archive");
        string target = PackTestHelper.CreateTarget(temp);
        string[] before = PackTestHelper.SnapshotDirectory(source);

        PackResult result = PackTestHelper.Run("--s", source, "--t", target);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Empty(result.StandardError);

        IReadOnlyList<string> entries = PackTestHelper.ReadArchiveEntries(target);
        Assert.Contains("manifest.json", entries);
        Assert.Contains("dist/.gitkeep", entries);
        Assert.Contains("dist/app.ts", entries);
        Assert.Equal(before, PackTestHelper.SnapshotDirectory(source));
    }

    [Fact]
    public void Pack_NonAsciiNfcFileName_IsPreserved() {
        using TempDirectory temp = new();
        string source = PackTestHelper.CreateSource(temp);
        PackTestHelper.WriteDistFile(source, "app.ts");
        PackTestHelper.WriteDistFile(source, "\u00e4.txt", "umlaut");
        string target = PackTestHelper.CreateTarget(temp);

        PackResult result = PackTestHelper.Run("--s", source, "--t", target);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Empty(result.StandardError);
        Assert.Contains("dist/\u00e4.txt", PackTestHelper.ReadArchiveEntries(target));
    }

    [Fact]
    public void Pack_ExistingArchive_IsOverwritten() {
        using TempDirectory temp = new();
        string source = PackTestHelper.CreateSource(temp);
        PackTestHelper.WriteDistFile(source, "app.ts");
        string target = PackTestHelper.CreateTarget(temp);
        File.WriteAllText(Path.Combine(target, "sample-0.1.0.van"), "stale content");

        PackResult result = PackTestHelper.Run("--s", source, "--t", target);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Empty(result.StandardError);

        IReadOnlyList<string> entries = PackTestHelper.ReadArchiveEntries(target);
        Assert.Contains("manifest.json", entries);
        Assert.Contains("dist/app.ts", entries);
    }

    [Fact]
    public void Pack_MissingManifest_ReturnsRuntimeError() {
        using TempDirectory temp = new();
        string source = PackTestHelper.CreateSource(temp, manifestJson: null);
        PackTestHelper.WriteDistFile(source, "app.ts");
        string target = PackTestHelper.CreateTarget(temp);

        PackResult result = PackTestHelper.Run("--s", source, "--t", target);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        AssertStartsWithError(result);
        PackTestHelper.AssertNoArchive(target);
    }

    [Fact]
    public void Pack_MalformedJson_ReturnsRuntimeError() {
        using TempDirectory temp = new();
        string source = PackTestHelper.CreateSource(temp, "{ not json");
        PackTestHelper.WriteDistFile(source, "app.ts");
        string target = PackTestHelper.CreateTarget(temp);

        PackResult result = PackTestHelper.Run("--s", source, "--t", target);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        AssertStartsWithError(result);
        PackTestHelper.AssertNoArchive(target);
    }

    [Fact]
    public void Pack_ManifestWithoutVersion_ReturnsRuntimeError() {
        using TempDirectory temp = new();
        string source = PackTestHelper.CreateSource(
            temp,
            """{ "name": "sample", "entrypoint": "app.ts" }""");
        PackTestHelper.WriteDistFile(source, "app.ts");
        string target = PackTestHelper.CreateTarget(temp);

        PackResult result = PackTestHelper.Run("--s", source, "--t", target);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        AssertStartsWithError(result);
        PackTestHelper.AssertNoArchive(target);
    }

    [Fact]
    public void Pack_EntrypointFileMissing_ReturnsRuntimeError() {
        using TempDirectory temp = new();
        string source = PackTestHelper.CreateSource(temp);
        string target = PackTestHelper.CreateTarget(temp);

        PackResult result = PackTestHelper.Run("--s", source, "--t", target);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        AssertStartsWithError(result);
        PackTestHelper.AssertNoArchive(target);
    }

    [Fact]
    public void Pack_MissingDistDirectory_ReturnsRuntimeError() {
        using TempDirectory temp = new();
        string source = PackTestHelper.CreateSource(temp, createDist: false);
        string target = PackTestHelper.CreateTarget(temp);

        PackResult result = PackTestHelper.Run("--s", source, "--t", target);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        AssertStartsWithError(result);
        PackTestHelper.AssertNoArchive(target);
    }

    [Fact]
    public void Pack_UnknownOption_ReturnsUsageError() {
        using TempDirectory temp = new();
        string source = PackTestHelper.CreateSource(temp);
        PackTestHelper.WriteDistFile(source, "app.ts");
        string target = PackTestHelper.CreateTarget(temp);

        PackResult result = PackTestHelper.Run("--s", source, "--t", target, "--x", "value");

        Assert.Equal(ExitCodes.UsageError, result.ExitCode);
        AssertStartsWithError(result);
        Assert.Contains("--x", result.StandardError);
        PackTestHelper.AssertNoArchive(target);
    }

    [Fact]
    public void Pack_NoArguments_ReturnsUsageError() {
        PackResult result = PackTestHelper.Run();

        Assert.Equal(ExitCodes.UsageError, result.ExitCode);
        AssertStartsWithError(result);
    }

    [Fact]
    public void Pack_MissingTargetOption_ReturnsUsageError() {
        using TempDirectory temp = new();
        string source = PackTestHelper.CreateSource(temp);
        PackTestHelper.WriteDistFile(source, "app.ts");

        PackResult result = PackTestHelper.Run("--s", source);

        Assert.Equal(ExitCodes.UsageError, result.ExitCode);
        AssertStartsWithError(result);
        Assert.Contains("--t", result.StandardError);
    }

    [Fact]
    public void Pack_OptionWithoutValue_ReturnsUsageError() {
        PackResult result = PackTestHelper.Run("--s");

        Assert.Equal(ExitCodes.UsageError, result.ExitCode);
        AssertStartsWithError(result);
    }

    [Fact]
    public void Pack_Help_PrintsUsageAndDescriptions() {
        PackResult result = PackTestHelper.Run("--help");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Empty(result.StandardError);
        Assert.Contains("Usage:", result.StandardOut);
        Assert.Contains("--s", result.StandardOut);
        Assert.Contains("--t", result.StandardOut);
        Assert.Contains("Pack a plugin directory into a .van archive.", result.StandardOut);
    }

    private static void AssertStartsWithError(PackResult result) {
        Assert.Empty(result.StandardOut);
        Assert.StartsWith("error:", result.StandardError);
    }
}
