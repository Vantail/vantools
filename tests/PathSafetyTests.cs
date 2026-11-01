using System.IO.Compression;
using System.Text;
using VanTools.Pack;

namespace VanTest;

[Collection("console")]
public sealed class PathSafetyTests : IDisposable {
    private readonly string rootDirectory;
    private readonly string sourceDirectory;
    private readonly string targetDirectory;

    public PathSafetyTests() {
        rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "vantools-pathsafety-" + Guid.NewGuid().ToString("N"));
        sourceDirectory = Path.Combine(rootDirectory, "source");
        targetDirectory = Path.Combine(rootDirectory, "target");

        Directory.CreateDirectory(Path.Combine(sourceDirectory, "dist"));
        File.WriteAllText(Path.Combine(sourceDirectory, "manifest.json"), ManifestJson);
        File.WriteAllText(DistPath("app.ts"), string.Empty);
    }

    public void Dispose() {
        if (Directory.Exists(rootDirectory)) {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    private static string ManifestJson =>
        """{ "name": "safety", "version": "0.1.0", "entrypoint": "app.ts" }""";

    private string DistPath(string relativePath) =>
        Path.Combine(sourceDirectory, "dist", relativePath);

    private string ArchivePath => Path.Combine(targetDirectory, "safety-0.1.0.van");

    [Fact]
    public void Pack_SkipsNodeModulesAtDistRoot_AndWarns() {
        WriteDistFile("node_modules/foo.js");

        (int exitCode, string standardError) = RunPack();

        Assert.Equal(0, exitCode);
        Assert.Contains(PathSafety.NodeModulesWarning, standardError);
        Assert.DoesNotContain(ReadEntryNames(), name => name.Contains("node_modules", StringComparison.Ordinal));
    }

    [Fact]
    public void Pack_SkipsNestedNodeModules_AndWarnsOncePerDirectory() {
        WriteDistFile("sub/keep.ts");
        WriteDistFile("sub/node_modules/foo.js");

        (int exitCode, string standardError) = RunPack();

        Assert.Equal(0, exitCode);
        Assert.Equal(1, CountOccurrences(standardError, PathSafety.NodeModulesWarning));
        Assert.DoesNotContain(ReadEntryNames(), name => name.Contains("node_modules", StringComparison.Ordinal));
        Assert.Contains("dist/sub/keep.ts", ReadEntryNames());
    }

    [Fact]
    public void Pack_DeeplyNestedTree_IsWalkedWithoutRecursion() {
        const int depth = 120;
        string directory = DistPath(string.Empty);

        try {
            for (int index = 0; index < depth; index++) {
                directory = Path.Combine(directory, "d");
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(Path.Combine(directory, "leaf.ts"), string.Empty);
        } catch (IOException) {
            // Some file systems reject paths beyond their maximum length; the walk itself is still covered elsewhere.
            return;
        }

        (int exitCode, string standardError) = RunPack();

        Assert.Equal(0, exitCode);
        Assert.Empty(standardError);
        Assert.Contains(ReadEntryNames(), name => name.EndsWith("/leaf.ts", StringComparison.Ordinal));
    }

    [Fact]
    public void Pack_SymlinkToFile_IsRejected() {
        WriteDistFile("target.txt");
        if (!TryCreateSymlink(DistPath("link.txt"), "target.txt")) {
            return;
        }

        (int exitCode, string standardError) = RunPack();

        Assert.NotEqual(0, exitCode);
        Assert.StartsWith("error:", standardError);
        Assert.False(File.Exists(ArchivePath));
    }

    [Fact]
    public void Pack_SymlinkToDirectory_IsRejected() {
        Directory.CreateDirectory(DistPath("realfolder"));
        File.WriteAllText(DistPath(Path.Combine("realfolder", "inner.ts")), string.Empty);
        if (!TryCreateSymlink(DistPath("linkfolder"), "realfolder", directory: true)) {
            return;
        }

        (int exitCode, string standardError) = RunPack();

        Assert.NotEqual(0, exitCode);
        Assert.StartsWith("error:", standardError);
        Assert.False(File.Exists(ArchivePath));
    }

    [Fact]
    public void Pack_CaseInsensitiveDuplicates_AreRejected() {
        WriteDistFile("Foo.ts");
        WriteDistFile("foo.ts");

        if (!HasTwoDistinctFiles()) {
            return;
        }

        (int exitCode, string standardError) = RunPack();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Case-insensitive duplicate", standardError);
        Assert.False(File.Exists(ArchivePath));
    }

    [Fact]
    public void Pack_NonNfcFileName_IsRejected() {
        string decomposedName = "cafe\u0301.ts";
        File.WriteAllText(DistPath(decomposedName), string.Empty);

        if (!FileHasDecomposedName(decomposedName)) {
            return;
        }

        (int exitCode, string standardError) = RunPack();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("NFC", standardError);
        Assert.False(File.Exists(ArchivePath));
    }

    [Fact]
    public void Pack_NonNfcDirectoryName_IsRejected() {
        string decomposedDirectory = "cafe\u0301";
        Directory.CreateDirectory(DistPath(decomposedDirectory));
        File.WriteAllText(DistPath(Path.Combine(decomposedDirectory, "x.ts")), string.Empty);

        if (!FileHasDecomposedName(Path.Combine(decomposedDirectory, "x.ts"))) {
            return;
        }

        (int exitCode, string standardError) = RunPack();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("NFC", standardError);
        Assert.False(File.Exists(ArchivePath));
    }

    [Fact]
    public void IsNfc_DistinguishesNormalizationForms() {
        Assert.True(PathSafety.IsNfc("caf\u00e9"));
        Assert.False(PathSafety.IsNfc("cafe\u0301"));
    }

    [Theory]
    [InlineData("dist/app.ts", true)]
    [InlineData("dist/sub/app.ts", true)]
    [InlineData("dist//app.ts", false)]
    [InlineData("dist/./app.ts", false)]
    [InlineData("dist/../app.ts", false)]
    [InlineData("dist\\app.ts", false)]
    public void IsSafeArchivePath_ValidatesSegments(string entryName, bool expected) {
        Assert.Equal(expected, PathSafety.IsSafeArchivePath(entryName, out _));
    }

    private void WriteDistFile(string relativePath) {
        string fullPath = DistPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, string.Empty);
    }

    private (int ExitCode, string StandardError) RunPack() {
        TextWriter originalError = Console.Error;
        using StringWriter capture = new();
        Console.SetError(capture);

        try {
            PackCommand command = new();
            int exitCode = command.Execute(["--s", sourceDirectory, "--t", targetDirectory]);
            return (exitCode, capture.ToString());
        } finally {
            Console.SetError(originalError);
        }
    }

    private string[] ReadEntryNames() {
        using ZipArchive archive = ZipFile.OpenRead(ArchivePath);
        return [.. archive.Entries.Select(entry => entry.FullName)];
    }

    private bool HasTwoDistinctFiles() {
        return Directory.GetFiles(Path.Combine(sourceDirectory, "dist"))
            .Count(path => path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)) == 3;
    }

    private bool FileHasDecomposedName(string relativePath) {
        return !PathSafety.IsNfc(Path.GetFileName(relativePath));
    }

    private static bool TryCreateSymlink(string linkPath, string targetPath, bool directory = false) {
        try {
            if (directory) {
                Directory.CreateSymbolicLink(linkPath, targetPath);
            } else {
                File.CreateSymbolicLink(linkPath, targetPath);
            }

            return true;
        } catch (UnauthorizedAccessException) {
            return false;
        } catch (IOException) {
            return false;
        } catch (PlatformNotSupportedException) {
            return false;
        }
    }

    private static int CountOccurrences(string text, string value) {
        int count = 0;
        int index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0) {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }
}

[CollectionDefinition("console", DisableParallelization = true)]
public sealed class ConsoleCollectionDefinition {
}
