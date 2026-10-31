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

    private static string CreateArchive(TempDirectory temp, string fileName, string? manifestJson) {
        string archivePath = temp.Combine(fileName);

        using (FileStream stream = new(archivePath, FileMode.Create, FileAccess.Write))
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create)) {
            if (manifestJson is not null) {
                ZipArchiveEntry manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using (StreamWriter writer = new(manifestEntry.Open())) {
                    writer.Write(manifestJson);
                }
            }

            ZipArchiveEntry entrypointEntry = archive.CreateEntry("dist/app.ts", CompressionLevel.Optimal);
            using (Stream entrypointStream = entrypointEntry.Open()) {
                entrypointStream.Write("// app"u8);
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
