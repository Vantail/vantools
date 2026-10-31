using System.IO.Compression;
using VanTools.Core;

namespace VanTools.Pack;

public sealed class PackCommand : ICommand {
    private const string ManifestFileName = "manifest.json";
    private const string DistDirectoryName = "dist";

    public string Name => "pack";

    public int Execute(string[] args) {
        if (!TryParseArguments(args, out string sourceDirectory, out string targetDirectory)) {
            return 1;
        }

        string manifestPath = Path.Combine(sourceDirectory, ManifestFileName);
        string distDirectory = Path.Combine(sourceDirectory, DistDirectoryName);
        Manifest manifest;

        try {
            if (!Directory.Exists(distDirectory)) {
                throw new ManifestException($"dist directory not found at '{distDirectory}'.");
            }

            manifest = Manifest.LoadFrom(sourceDirectory);
        } catch (ManifestException exception) {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 1;
        }

        string archiveName = $"{manifest.Name}-{manifest.Version}.van";
        Directory.CreateDirectory(targetDirectory);
        string archivePath = Path.Combine(targetDirectory, archiveName);

        using FileStream stream = new(archivePath, FileMode.Create, FileAccess.Write);
        using ZipArchive archive = new(stream, ZipArchiveMode.Create);

        archive.CreateEntryFromFile(manifestPath, ManifestFileName, CompressionLevel.Optimal);

        foreach (string file in Directory.EnumerateFiles(distDirectory, "*", SearchOption.AllDirectories)) {
            string relativePath = Path.GetRelativePath(distDirectory, file).Replace('\\', '/');
            archive.CreateEntryFromFile(file, $"{DistDirectoryName}/{relativePath}", CompressionLevel.Optimal);
        }

        return 0;
    }

    private static bool TryParseArguments(string[] args, out string sourceDirectory, out string targetDirectory) {
        sourceDirectory = string.Empty;
        targetDirectory = string.Empty;

        for (int index = 0; index < args.Length; index++) {
            string option = args[index];
            if (option is not ("--s" or "--t")) {
                Console.Error.WriteLine($"pack: unknown option '{option}'.");
                PrintUsage();
                return false;
            }

            if (index + 1 >= args.Length) {
                Console.Error.WriteLine($"pack: option '{option}' requires a value.");
                PrintUsage();
                return false;
            }

            if (option == "--s") {
                sourceDirectory = args[++index];
            } else {
                targetDirectory = args[++index];
            }
        }

        if (string.IsNullOrEmpty(sourceDirectory)) {
            Console.Error.WriteLine("pack: missing required option '--s'.");
            PrintUsage();
            return false;
        }

        if (string.IsNullOrEmpty(targetDirectory)) {
            Console.Error.WriteLine("pack: missing required option '--t'.");
            PrintUsage();
            return false;
        }

        return true;
    }

    private static void PrintUsage() {
        Console.Error.WriteLine("Usage: vantools pack --s <source> --t <target>");
    }
}
