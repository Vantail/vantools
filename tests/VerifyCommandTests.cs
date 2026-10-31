using System.IO.Compression;
using VanTools.Core;
using VanTools.Verify;

namespace VanTest;

[Collection("console")]
public sealed class VerifyCommandTests {
    private const string ValidManifest =
        """{ "name": "my-plugin", "version": "0.1.0", "entrypoint": "app.ts" }""";

    [Fact]
    public void Verify_ValidFilename_ReturnsSuccess() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(temp, "my-plugin-0.1.0.van", ValidManifest);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Equal("OK" + Environment.NewLine, result.StandardOut);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public void Verify_FileNameDiffersInCase_ReturnsSuccess() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(temp, "MY-PLUGIN-0.1.0.van", ValidManifest);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Equal("OK" + Environment.NewLine, result.StandardOut);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public void Verify_MismatchedFilename_ReturnsRuntimeError() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(temp, "wrong-name.van", ValidManifest);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains("does not match the expected name", result.StandardError);
        Assert.Contains("my-plugin-0.1.0.van", result.StandardError);
    }

    [Fact]
    public void Verify_InvalidManifestAndMismatchedFilename_ReportsBothErrors() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(
            temp,
            "wrong-name.van",
            """{ "name": "my-plugin", "version": "not-semver", "entrypoint": "app.ts" }""");

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains("is not a valid SemVer 2.0.0 version: 'not-semver'", result.StandardError);
        Assert.Contains("does not match the expected name", result.StandardError);
    }

    [Fact]
    public void Verify_MissingManifest_ReportsOnlyTheMissingManifest() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(temp, "wrong-name.van", manifestJson: null);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains("manifest.json not found in archive root.", result.StandardError);
        Assert.DoesNotContain("does not match the expected name", result.StandardError);
    }

    [Fact]
    public void Verify_ExplicitDistDirectoryEntry_ReturnsSuccess() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(
            temp,
            "my-plugin-0.1.0.van",
            ValidManifest,
            [("dist/", null)]);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Equal("OK" + Environment.NewLine, result.StandardOut);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public void Verify_ImplicitDistDirectoryEntry_ReturnsSuccess() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(
            temp,
            "my-plugin-0.1.0.van",
            ValidManifest,
            [("dist/app.ts", "// app")]);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Equal("OK" + Environment.NewLine, result.StandardOut);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public void Verify_MissingDistDirectory_ReturnsRuntimeError() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(temp, "my-plugin-0.1.0.van", ValidManifest, []);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains("The 'dist/' directory is missing from the archive root.", result.StandardError);
    }

    [Fact]
    public void Verify_MismatchedFilenameAndMissingDist_ReportsBothErrors() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(temp, "wrong-name.van", ValidManifest, []);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains("does not match the expected name", result.StandardError);
        Assert.Contains("The 'dist/' directory is missing from the archive root.", result.StandardError);
    }

    [Fact]
    public void Verify_NodeModulesAtRoot_ReturnsRuntimeError() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(
            temp,
            "my-plugin-0.1.0.van",
            ValidManifest,
            [("dist/app.ts", "// app"), ("node_modules/foo.js", "")]);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains("node_modules", result.StandardError);
    }

    [Fact]
    public void Verify_NestedNodeModules_ReturnsRuntimeError() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(
            temp,
            "my-plugin-0.1.0.van",
            ValidManifest,
            [("dist/app.ts", "// app"), ("dist/node_modules/foo.js", "")]);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains("node_modules", result.StandardError);
    }

    [Fact]
    public void Verify_DeeplyNestedNodeModules_ReturnsRuntimeError() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(
            temp,
            "my-plugin-0.1.0.van",
            ValidManifest,
            [("dist/app.ts", "// app"), ("dist/src/node_modules/foo.js", "")]);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains("node_modules", result.StandardError);
    }

    [Theory]
    [InlineData("dist/my-node_modules-file.js")]
    [InlineData("dist/node_modules_backup/foo.js")]
    public void Verify_SimilarDirectoryName_ReturnsSuccess(string entryName) {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(
            temp,
            "my-plugin-0.1.0.van",
            ValidManifest,
            [("dist/app.ts", "// app"), (entryName, "")]);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Equal("OK" + Environment.NewLine, result.StandardOut);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public void Verify_MultipleFilesInNodeModules_ReportsOneError() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(
            temp,
            "my-plugin-0.1.0.van",
            ValidManifest,
            [
                ("dist/app.ts", "// app"),
                ("dist/node_modules/a.js", ""),
                ("dist/node_modules/b.js", "")
            ]);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);

        string[] errorLines = result.StandardError.Split(
            Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(errorLines, line => line.Contains("node_modules"));
    }

    private static string CreateArchive(
        TempDirectory temp,
        string fileName,
        string? manifestJson,
        IReadOnlyList<(string Name, string? Content)>? distEntries = null) {
        distEntries ??= [("dist/app.ts", "// app")];

        string archivePath = temp.Combine(fileName);

        using (FileStream stream = new(archivePath, FileMode.Create, FileAccess.Write))
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create)) {
            if (manifestJson is not null) {
                ZipArchiveEntry manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using (StreamWriter writer = new(manifestEntry.Open())) {
                    writer.Write(manifestJson);
                }
            }

            foreach ((string name, string? content) in distEntries) {
                ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                if (content is null) {
                    continue;
                }

                using (StreamWriter writer = new(entry.Open())) {
                    writer.Write(content);
                }
            }
        }

        return archivePath;
    }

    private static VerifyResult Verify(string archivePath) {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        using StringWriter standardOut = new();
        using StringWriter standardError = new();
        Console.SetOut(standardOut);
        Console.SetError(standardError);

        try {
            int exitCode = new VerifyCommand().Execute(["--s", archivePath]);

            return new VerifyResult(exitCode, standardOut.ToString(), standardError.ToString());
        } finally {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed record VerifyResult(int ExitCode, string StandardOut, string StandardError);
}
