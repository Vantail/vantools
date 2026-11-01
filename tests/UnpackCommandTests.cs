using System.IO.Compression;
using VanTools.Core;
using VanTools.Unpack;

namespace VanTest;

[Collection("console")]
public sealed class UnpackCommandTests {
    private const string ValidManifest =
        """{ "name": "sample", "version": "0.1.0", "entrypoint": "app.ts" }""";

    [Fact]
    public void Unpack_ValidArchive_ExtractsContents() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(temp);
        string target = temp.Combine("target");

        UnpackResult result = Unpack(archivePath, target);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Equal("OK" + Environment.NewLine, result.StandardOut);
        Assert.Empty(result.StandardError);

        Assert.Equal(
            ["dist", "dist/app.ts", "dist/subdir", "dist/subdir/file.txt", "manifest.json"],
            SnapshotTarget(target));
        Assert.Equal(ValidManifest, File.ReadAllText(Path.Combine(target, "manifest.json")));
        Assert.Equal("// app", File.ReadAllText(Path.Combine(target, "dist", "app.ts")));
        Assert.Equal("nested", File.ReadAllText(Path.Combine(target, "dist", "subdir", "file.txt")));
    }

    [Fact]
    public void Unpack_ExistingTarget_ReplacesPreviousContents() {
        using TempDirectory temp = new();
        string archivePath = CreateArchive(temp);
        string target = temp.Combine("target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "old.txt"), "old");
        Directory.CreateDirectory(Path.Combine(target, "old-dir"));
        File.WriteAllText(Path.Combine(target, "old-dir", "nested.txt"), "old");

        UnpackResult result = Unpack(archivePath, target);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Equal("OK" + Environment.NewLine, result.StandardOut);
        Assert.Empty(result.StandardError);

        Assert.False(File.Exists(Path.Combine(target, "old.txt")));
        Assert.False(Directory.Exists(Path.Combine(target, "old-dir")));
        Assert.Equal(
            ["dist", "dist/app.ts", "dist/subdir", "dist/subdir/file.txt", "manifest.json"],
            SnapshotTarget(target));
    }

    // The file system reports platform-specific separators; compare against forward slashes on every platform.
    private static string[] SnapshotTarget(string targetDirectory) {
        return
        [
            .. PackTestHelper
                .SnapshotDirectory(targetDirectory)
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
        ];
    }

    private static string CreateArchive(TempDirectory temp) {
        string archivePath = temp.Combine("sample-0.1.0.van");

        using (FileStream stream = new(archivePath, FileMode.Create, FileAccess.Write))
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create)) {
            WriteEntry(archive, "manifest.json", ValidManifest);
            WriteEntry(archive, "dist/app.ts", "// app");
            WriteEntry(archive, "dist/subdir/file.txt", "nested");
        }

        return archivePath;
    }

    private static void WriteEntry(ZipArchive archive, string name, string content) {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using StreamWriter writer = new(entry.Open());
        writer.Write(content);
    }

    private static UnpackResult Unpack(string archivePath, string targetDirectory) {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        using StringWriter standardOut = new();
        using StringWriter standardError = new();
        Console.SetOut(standardOut);
        Console.SetError(standardError);

        try {
            int exitCode = new UnpackCommand().Execute(["--s", archivePath, "--t", targetDirectory]);

            return new UnpackResult(exitCode, standardOut.ToString(), standardError.ToString());
        } finally {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed record UnpackResult(int ExitCode, string StandardOut, string StandardError);
}
