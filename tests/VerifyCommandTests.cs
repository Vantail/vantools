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
            [("dist/", null), ("dist/app.ts", "// app")]);

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
    public void Verify_OversizedManifest_ReturnsRuntimeErrorBeforeParsing() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(
            temp,
            "my-plugin-0.1.0.van",
            new string(' ', (1024 * 1024) + 1),
            [("dist/app.ts", "// app")]);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains("limit is 1048576", result.StandardError);
        Assert.DoesNotContain("Invalid JSON format", result.StandardError);
    }

    [Theory]
    [InlineData("../evil.txt", "must not contain '.' or '..' segments")]
    [InlineData("/abs/evil.sh", "must be relative")]
    [InlineData("C:/evil.sh", "must not contain a drive letter")]
    [InlineData("dist\\..\\evil", "must use '/' as the path separator")]
    [InlineData("dist/./dotfile", "must not contain '.' or '..' segments")]
    [InlineData("dist//double", "must not contain empty segments")]
    public void Verify_UnsafeEntryName_ReturnsRuntimeError(string entryName, string reason) {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(
            temp,
            "my-plugin-0.1.0.van",
            ValidManifest,
            [("dist/app.ts", "// app"), (entryName, "")]);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains($"The archive entry '{entryName}' {reason}.", result.StandardError);
    }

    [Fact]
    public void Verify_NonNfcEntryName_ReturnsRuntimeError() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(
            temp,
            "my-plugin-0.1.0.van",
            ValidManifest,
            [("dist/app.ts", "// app"), ("dist/cafe\u0301.ts", "")]);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains("is not in Unicode NFC form", result.StandardError);
    }

    [Fact]
    public void Verify_TraversalEntry_DoesNotSatisfyDistRequirement() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(
            temp,
            "my-plugin-0.1.0.van",
            ValidManifest,
            [("dist/../secret.ts", "")]);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains("must not contain '.' or '..' segments", result.StandardError);
        Assert.Contains("The 'dist/' directory is missing from the archive root.", result.StandardError);
    }

    [Theory]
    [InlineData(0xA1FF)] // symbolic link
    [InlineData(0x11A4)] // FIFO
    [InlineData(0x21A4)] // character device
    [InlineData(0xC1FF)] // socket
    public void Verify_NonRegularEntryType_ReturnsRuntimeError(int unixMode) {
        using TempDirectory temp = new();
        string archivePath = CreateArchiveWithEntryMode(temp, "dist/app.ts", unixMode);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains("The archive entry 'dist/app.ts' is not a regular file or directory", result.StandardError);
    }

    [Fact]
    public void Verify_RegularEntryMode_ReturnsSuccess() {
        using TempDirectory temp = new();
        string archivePath = CreateArchiveWithEntryMode(temp, "dist/app.ts", 0x81A4);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Equal("OK" + Environment.NewLine, result.StandardOut);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public void Verify_DuplicateManifestEntries_ReturnsRuntimeError() {
        using TempDirectory temp = new();
        string archivePath = CreateArchiveWithEntries(
            temp,
            ("manifest.json", ValidManifest),
            ("manifest.json", """{ "name": "my-plugin", "version": "0.1.0", "entrypoint": "../../../etc/passwd" }"""),
            ("dist/app.ts", "// app"));

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains("The archive contains duplicate entries named 'manifest.json'.", result.StandardError);
    }

    [Fact]
    public void Verify_DuplicateEntrypointEntries_ReturnsRuntimeError() {
        using TempDirectory temp = new();
        string archivePath = CreateArchiveWithEntries(
            temp,
            ("manifest.json", ValidManifest),
            ("dist/app.ts", "// app"),
            ("dist/app.ts", "// attacker"));

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains("The archive contains duplicate entries named 'dist/app.ts'.", result.StandardError);
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

    [Fact]
    public void Verify_EntrypointFilePresent_ReturnsSuccess() {
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
    public void Verify_MissingEntrypointFile_ReturnsRuntimeError() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(
            temp,
            "my-plugin-0.1.0.van",
            ValidManifest,
            [("dist/other.ts", "")]);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains(
            "The entrypoint file 'dist/app.ts' specified in manifest.json does not exist in the archive.",
            result.StandardError);
    }

    [Fact]
    public void Verify_EntrypointIsDirectory_ReturnsRuntimeError() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(
            temp,
            "my-plugin-0.1.0.van",
            ValidManifest,
            [("dist/app.ts/", null)]);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains("The entrypoint file 'dist/app.ts'", result.StandardError);
    }

    [Fact]
    public void Verify_MalformedManifest_SkipsEntrypointCheck() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(
            temp,
            "my-plugin-0.1.0.van",
            "{ not json",
            [("dist/other.ts", "")]);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);

        string[] errorLines = result.StandardError.Split(
            Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(errorLines);
        Assert.Contains("Invalid JSON format", result.StandardError);
        Assert.DoesNotContain("entrypoint file", result.StandardError);
    }

    [Fact]
    public void Verify_MissingDistAndEntrypoint_ReportsBothErrors() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(temp, "my-plugin-0.1.0.van", ValidManifest, []);

        VerifyResult result = Verify(archivePath);

        Assert.Equal(ExitCodes.RuntimeError, result.ExitCode);
        Assert.Empty(result.StandardOut);
        Assert.Contains("The 'dist/' directory is missing from the archive root.", result.StandardError);
        Assert.Contains("The entrypoint file 'dist/app.ts'", result.StandardError);
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

    private static string CreateArchiveWithEntryMode(
        TempDirectory temp,
        string entryName,
        int unixMode) {
        string archivePath = temp.Combine("my-plugin-0.1.0.van");

        using (FileStream stream = new(archivePath, FileMode.Create, FileAccess.Write))
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create)) {
            ZipArchiveEntry manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            using (StreamWriter writer = new(manifestEntry.Open())) {
                writer.Write(ValidManifest);
            }

            ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            entry.ExternalAttributes = unchecked((int)((uint)unixMode << 16));
            using StreamWriter entryWriter = new(entry.Open());
            entryWriter.Write("../../../../etc/passwd");
        }

        return archivePath;
    }

    private static string CreateArchiveWithEntries(
        TempDirectory temp,
        params (string Name, string Content)[] entries) {
        string archivePath = temp.Combine("my-plugin-0.1.0.van");

        using (FileStream stream = new(archivePath, FileMode.Create, FileAccess.Write))
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create)) {
            foreach ((string name, string content) in entries) {
                ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using StreamWriter writer = new(entry.Open());
                writer.Write(content);
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
