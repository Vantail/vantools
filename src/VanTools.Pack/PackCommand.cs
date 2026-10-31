using System.IO.Compression;
using VanTools.Core;

namespace VanTools.Pack;

// The "pack" subcommand, which turns a plugin source directory into a .van archive.
public sealed class PackCommand : ICommand {
    private const string ManifestFileName = "manifest.json";
    private const string DistDirectoryName = "dist";

    private static readonly ArgumentParser Parser = new ArgumentParser()
        .Required("s", "source directory")
        .Required("t", "target directory");

    // The subcommand name.
    public string Name => "pack";

    // A one-line description shown in the top-level usage listing.
    public string Description => "Pack a plugin directory into a .van archive.";

    // Packs the source directory into a .van archive in the target directory.
    // args: the arguments that follow "pack"; expects "--s" and "--t".
    // Returns: ExitCodes.Success on success, ExitCodes.UsageError for invalid arguments, or ExitCodes.RuntimeError when packing fails.
    // Side effects: writes the archive file, writes "error:" lines to stderr on failure, and writes a warning to stderr for each skipped node_modules directory.
    public int Execute(string[] args) {
        ParsedArguments parsed;
        try {
            parsed = Parser.Parse(args);
        } catch (HelpRequestedException) {
            Console.Out.Write(Parser.RenderHelp(Name, Description));
            return ExitCodes.Success;
        } catch (UsageException exception) {
            ErrorWriter.Write(exception.Message);
            Console.Error.WriteLine($"Usage: {Name} {Parser.UsageLine}");
            return ExitCodes.UsageError;
        }

        string sourceDirectory = parsed.Get("s");
        string targetDirectory = parsed.Get("t");

        try {
            return Pack(sourceDirectory, targetDirectory);
        } catch (ManifestException exception) {
            foreach (string error in exception.Errors) {
                ErrorWriter.Write(error);
            }

            return ExitCodes.RuntimeError;
        } catch (Exception exception) {
            ErrorWriter.Write(exception.Message);
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
