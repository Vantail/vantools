using System.IO.Compression;
using VanTools.Core;

namespace VanTools.Pack;

public sealed class PackCommand : ICommand {
    private const string ManifestFileName = "manifest.json";
    private const string DistDirectoryName = "dist";

    private static readonly ArgumentParser Parser = new ArgumentParser()
        .Required("s", "source directory")
        .Required("t", "target directory");

    public string Name => "pack";

    public string Description => "Pack a plugin directory into a .van archive.";

    public int Execute(string[] args) {
        ParsedArguments parsed;
        try {
            parsed = Parser.Parse(args);
        } catch (HelpRequestedException) {
            Console.Out.Write(Parser.RenderHelp(Name, Description));
            return ExitCodes.Success;
        } catch (UsageException exception) {
            Console.Error.WriteLine($"error: {exception.Message}");
            Console.Error.WriteLine($"Usage: {Name} {Parser.UsageLine}");
            return ExitCodes.UsageError;
        }

        string sourceDirectory = parsed.Get("s");
        string targetDirectory = parsed.Get("t");

        try {
            return Pack(sourceDirectory, targetDirectory);
        } catch (Exception exception) {
            Console.Error.WriteLine($"error: {exception.Message}");
            return ExitCodes.RuntimeError;
        }
    }

    private static int Pack(string sourceDirectory, string targetDirectory) {
        string manifestPath = Path.Combine(sourceDirectory, ManifestFileName);
        string distDirectory = Path.Combine(sourceDirectory, DistDirectoryName);

        if (!Directory.Exists(distDirectory)) {
            throw new ManifestException($"dist directory not found at '{distDirectory}'.");
        }

        Manifest manifest = Manifest.LoadFrom(sourceDirectory);
        IReadOnlyList<PathEntry> distEntries = PathSafety.BuildEntries(distDirectory, Console.Error);

        string archiveName = $"{manifest.Name}-{manifest.Version}.van";
        Directory.CreateDirectory(targetDirectory);
        string archivePath = Path.Combine(targetDirectory, archiveName);

        using FileStream stream = new(archivePath, FileMode.Create, FileAccess.Write);
        using ZipArchive archive = new(stream, ZipArchiveMode.Create);

        archive.CreateEntryFromFile(manifestPath, ManifestFileName, CompressionLevel.Optimal);

        foreach (PathEntry entry in distEntries) {
            archive.CreateEntryFromFile(entry.FullPath, entry.EntryName, CompressionLevel.Optimal);
        }

        return ExitCodes.Success;
    }
}
