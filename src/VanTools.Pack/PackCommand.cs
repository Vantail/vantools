using System.IO.Compression;
using System.Text.Json;
using VanTools.Core;

namespace VanTools.Pack;

public sealed class PackCommand : ICommand
{
    private const string ManifestFileName = "manifest.json";
    private const string DistDirectoryName = "dist";

    public string Name => "pack";

    public int Execute(string[] args)
    {
        if (!TryParseArguments(args, out string sourceDirectory, out string targetDirectory))
        {
            return 1;
        }

        string manifestPath = Path.Combine(sourceDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine($"pack: manifest not found at '{manifestPath}'.");
            return 1;
        }

        string? name;
        string? version;
        try
        {
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            name = ReadStringMember(manifest.RootElement, "name");
            version = ReadStringMember(manifest.RootElement, "version");
            // Read for completeness; entrypoint is not validated in this task.
            _ = ReadStringMember(manifest.RootElement, "entrypoint");
        }
        catch (JsonException exception)
        {
            Console.Error.WriteLine($"pack: invalid manifest JSON: {exception.Message}");
            return 1;
        }

        if (string.IsNullOrEmpty(name))
        {
            Console.Error.WriteLine("pack: manifest member 'name' is missing or empty.");
            return 1;
        }

        if (string.IsNullOrEmpty(version))
        {
            Console.Error.WriteLine("pack: manifest member 'version' is missing or empty.");
            return 1;
        }

        string distDirectory = Path.Combine(sourceDirectory, DistDirectoryName);
        if (!Directory.Exists(distDirectory))
        {
            Console.Error.WriteLine($"pack: dist directory not found at '{distDirectory}'.");
            return 1;
        }

        string archiveName = $"{name}-{version}.van";
        Directory.CreateDirectory(targetDirectory);
        string archivePath = Path.Combine(targetDirectory, archiveName);

        using FileStream stream = new(archivePath, FileMode.Create, FileAccess.Write);
        using ZipArchive archive = new(stream, ZipArchiveMode.Create);

        archive.CreateEntryFromFile(manifestPath, ManifestFileName, CompressionLevel.Optimal);

        foreach (string file in Directory.EnumerateFiles(distDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(distDirectory, file).Replace('\\', '/');
            archive.CreateEntryFromFile(file, $"{DistDirectoryName}/{relativePath}", CompressionLevel.Optimal);
        }

        return 0;
    }

    private static bool TryParseArguments(string[] args, out string sourceDirectory, out string targetDirectory)
    {
        sourceDirectory = string.Empty;
        targetDirectory = string.Empty;

        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (option is not ("--s" or "--t"))
            {
                Console.Error.WriteLine($"pack: unknown option '{option}'.");
                PrintUsage();
                return false;
            }

            if (index + 1 >= args.Length)
            {
                Console.Error.WriteLine($"pack: option '{option}' requires a value.");
                PrintUsage();
                return false;
            }

            if (option == "--s")
            {
                sourceDirectory = args[++index];
            }
            else
            {
                targetDirectory = args[++index];
            }
        }

        if (string.IsNullOrEmpty(sourceDirectory))
        {
            Console.Error.WriteLine("pack: missing required option '--s'.");
            PrintUsage();
            return false;
        }

        if (string.IsNullOrEmpty(targetDirectory))
        {
            Console.Error.WriteLine("pack: missing required option '--t'.");
            PrintUsage();
            return false;
        }

        return true;
    }

    private static string? ReadStringMember(JsonElement root, string memberName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!root.TryGetProperty(memberName, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: vantools pack --s <source> --t <target>");
    }
}
