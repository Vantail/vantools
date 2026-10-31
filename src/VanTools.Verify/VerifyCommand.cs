using System.IO.Compression;
using System.Text;
using VanTools.Core;

namespace VanTools.Verify;

// The "verify" subcommand, which validates the manifest.json inside a .van archive.
public sealed class VerifyCommand : ICommand {
    private const string ManifestFileName = "manifest.json";
    private const string DistDirectoryName = "dist";

    private static readonly ArgumentParser Parser = new ArgumentParser()
        .Required("s", "source archive");

    // The subcommand name.
    public string Name => "verify";

    // A one-line description shown in the top-level usage listing.
    public string Description => "Verify the structure and contents of a .van archive.";

    // Verifies the archive named by the "--s" option.
    // args: the arguments that follow "verify"; expects "--s".
    // Returns: ExitCodes.Success when the manifest is valid, ExitCodes.UsageError for invalid arguments, or ExitCodes.RuntimeError when the archive cannot be read or its manifest is invalid.
    // Side effects: reads the archive, writes "error:" lines to stderr on failure, and writes "OK" to stdout on success.
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

        try {
            return Verify(parsed.Get("s"));
        } catch (Exception exception) {
            ErrorWriter.Write(exception.Message);
            return ExitCodes.RuntimeError;
        }
    }

    private static int Verify(string archivePath) {
        ZipArchive archive;
        try {
            archive = ZipFile.OpenRead(archivePath);
        } catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or NotSupportedException) {
            ErrorWriter.Write($"Cannot open file '{archivePath}': {DescribeOpenFailure(exception)}");
            return ExitCodes.RuntimeError;
        }

        using (archive) {
            ZipArchiveEntry? manifestEntry = FindManifestEntry(archive);
            if (manifestEntry is null) {
                ErrorWriter.Write($"{ManifestFileName} not found in archive root.");
                return ExitCodes.RuntimeError;
            }

            string jsonContent;
            using (Stream stream = manifestEntry.Open())
            using (StreamReader reader = new(stream, Encoding.UTF8)) {
                jsonContent = reader.ReadToEnd();
            }

            List<string> errors = ManifestValidator.Validate(jsonContent, out Manifest? manifest);
            CollectFilenameErrors(archivePath, manifest, errors);
            CollectDistErrors(archive, errors);

            if (errors.Count > 0) {
                foreach (string error in errors) {
                    ErrorWriter.Write(error);
                }

                return ExitCodes.RuntimeError;
            }
        }

        Console.Out.WriteLine("OK");

        return ExitCodes.Success;
    }

    // Records a message when the archive filename is not "<name>-<version>.van" as derived from the manifest.
    // archivePath: the path given to "--s".
    // manifest: the values parsed from the manifest, or null when the manifest could not be read.
    // errors: the list that receives the problem description.
    private static void CollectFilenameErrors(string archivePath, Manifest? manifest, List<string> errors) {
        if (manifest is null || manifest.Name.Length == 0 || manifest.Version.Length == 0) {
            return;
        }

        string expectedName = $"{manifest.Name}-{manifest.Version}.van";
        string actualName = Path.GetFileName(archivePath);

        if (string.Equals(actualName, expectedName, StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        errors.Add(
            $"The archive filename '{actualName}' does not match the expected name '{expectedName}' (derived from manifest.json).");
    }

    // Records a message when the archive root has no dist/ directory, either as an explicit entry or as the parent of a file entry.
    // archive: the opened archive.
    // errors: the list that receives the problem description.
    private static void CollectDistErrors(ZipArchive archive, List<string> errors) {
        const string distEntryName = DistDirectoryName + "/";

        bool hasDist = archive.Entries.Any(entry =>
            string.Equals(entry.FullName, distEntryName, StringComparison.Ordinal)
            || string.Equals(entry.FullName, DistDirectoryName, StringComparison.Ordinal)
            || entry.FullName.StartsWith(distEntryName, StringComparison.Ordinal));

        if (!hasDist) {
            errors.Add("The 'dist/' directory is missing from the archive root.");
        }
    }

    // Finds the archive entry named exactly "manifest.json" at the archive root.
    // archive: the opened archive.
    // Returns: the matching entry, or null when the archive root has no manifest.json.
    private static ZipArchiveEntry? FindManifestEntry(ZipArchive archive) {
        return archive.Entries.FirstOrDefault(
            entry => string.Equals(entry.FullName, ManifestFileName, StringComparison.Ordinal));
    }

    // Describes why the archive could not be opened, using a short reason for the common failures.
    // exception: the failure raised while opening the archive.
    // Returns: the text shown after "Cannot open file '<path>': ".
    private static string DescribeOpenFailure(Exception exception) {
        return exception switch {
            FileNotFoundException => "the file does not exist.",
            DirectoryNotFoundException => "the directory does not exist.",
            _ => exception.Message,
        };
    }
}
