using System.IO.Compression;
using VanTools.Pack;

namespace VanTest;

public sealed class PackCommandTests {
    [Fact]
    public void Pack_WritesManifestAndDistAtArchiveRoot() {
        string repositoryRoot = FindRepositoryRoot();
        string source = Path.Combine(repositoryRoot, "examples", "empty-archive");
        string target = Path.Combine(repositoryRoot, "tests");
        string archivePath = Path.Combine(target, "empty-archive-0.1.0.van");

        if (File.Exists(archivePath)) {
            File.Delete(archivePath);
        }

        PackCommand command = new();

        int exitCode = command.Execute(["--s", source, "--t", target]);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(archivePath), $"Expected archive at '{archivePath}'.");

        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        string[] entryNames = [.. archive.Entries.Select(entry => entry.FullName)];

        Assert.Contains("manifest.json", entryNames);
        Assert.Contains("dist/.gitkeep", entryNames);
    }

    private static string FindRepositoryRoot() {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null) {
            if (File.Exists(Path.Combine(directory.FullName, "VanTools.sln"))) {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root (VanTools.sln).");
    }
}
