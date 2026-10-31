using System.IO.Compression;
using VanTools.Pack;

namespace VanTest;

internal sealed class TempDirectory : IDisposable {
    public TempDirectory() {
        FullPath = Path.Combine(Path.GetTempPath(), "vantools-it-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(FullPath);
    }

    public string FullPath { get; }

    public string Combine(params string[] parts) {
        string[] all = [FullPath, .. parts];

        return Path.Combine(all);
    }

    public void Dispose() {
        if (Directory.Exists(FullPath)) {
            Directory.Delete(FullPath, recursive: true);
        }
    }
}

internal sealed record PackResult(int ExitCode, string StandardOut, string StandardError);

internal static class PackTestHelper {
    public const string ValidManifest =
        """{ "name": "sample", "version": "0.1.0", "entrypoint": "app.ts" }""";

    public static string CreateSource(
        TempDirectory temp,
        string? manifestJson = ValidManifest,
        bool createDist = true) {
        string source = temp.Combine("source");
        Directory.CreateDirectory(source);

        if (manifestJson is not null) {
            File.WriteAllText(Path.Combine(source, "manifest.json"), manifestJson);
        }

        if (createDist) {
            Directory.CreateDirectory(Path.Combine(source, "dist"));
        }

        return source;
    }

    public static string CreateTarget(TempDirectory temp) {
        string target = temp.Combine("target");
        Directory.CreateDirectory(target);

        return target;
    }

    public static void WriteDistFile(string source, string relativePath, string content = "") {
        string fullPath = Path.Combine(source, "dist", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    public static PackResult Run(params string[] args) {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        using StringWriter standardOut = new();
        using StringWriter standardError = new();
        Console.SetOut(standardOut);
        Console.SetError(standardError);

        try {
            int exitCode = new PackCommand().Execute(args);

            return new PackResult(exitCode, standardOut.ToString(), standardError.ToString());
        } finally {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    public static IReadOnlyList<string> ReadArchiveEntries(string targetDirectory) {
        using ZipArchive archive = ZipFile.OpenRead(FindArchive(targetDirectory));

        return [.. archive.Entries.Select(entry => entry.FullName)];
    }

    public static string FindArchive(string targetDirectory) {
        string[] archives = Directory.GetFiles(targetDirectory, "*.van");

        return Assert.Single(archives);
    }

    public static void AssertNoArchive(string targetDirectory) {
        if (!Directory.Exists(targetDirectory)) {
            return;
        }

        Assert.Empty(Directory.GetFiles(targetDirectory, "*.van"));
    }

    public static string[] SnapshotDirectory(string directory) {
        return
        [
            .. Directory
                .EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(directory, path))
                .OrderBy(name => name, StringComparer.Ordinal)
        ];
    }

    public static string FindRepositoryRoot() {
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
