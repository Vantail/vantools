using System.IO.Compression;
using System.Text;
using VanTools.Core;

namespace VanTools.Verify;

// Validates a .van archive and reports every problem it finds.
public static class ArchiveValidator {
    private const string ManifestFileName = "manifest.json";
    private const string DistDirectoryName = "dist";
    private const string NodeModulesDirectoryName = "node_modules";

    // Validates the archive at the given path.
    // archivePath: the path to the .van archive.
    // Returns: an empty list when the archive is valid; otherwise one message per problem found, including problems that prevent the archive from being opened or read.
    // Side effects: reads the archive from disk.
    public static List<string> Validate(string archivePath) {
        ArgumentNullException.ThrowIfNull(archivePath);

        ZipArchive archive;
        try {
            archive = ZipFile.OpenRead(archivePath);
        } catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or NotSupportedException) {
            return [$"Cannot open file '{archivePath}': {DescribeOpenFailure(exception)}"];
        }

        using (archive) {
            return Validate(archive, archivePath);
        }
    }

    // Validates an open archive.
    // archive: the opened archive.
    // archivePath: the path the archive was opened from, used in messages.
    // Returns: an empty list when the archive is valid; otherwise one message per problem found.
    private static List<string> Validate(ZipArchive archive, string archivePath) {
        ZipArchiveEntry? manifestEntry = FindManifestEntry(archive);
        if (manifestEntry is null) {
            return [$"{ManifestFileName} not found in archive root."];
        }

        string jsonContent;
        using (Stream stream = manifestEntry.Open())
        using (StreamReader reader = new(stream, Encoding.UTF8)) {
            jsonContent = reader.ReadToEnd();
        }

        List<string> errors = ManifestValidator.Validate(jsonContent, out Manifest? manifest);
        CollectFilenameErrors(archivePath, manifest, errors);
        CollectDistErrors(archive, errors);
        CollectNodeModulesErrors(archive, errors);
        CollectEntrypointErrors(archive, manifest, errors);

        return errors;
    }

    // Records a message when the archive filename is not "<name>-<version>.van" as derived from the manifest.
    // archivePath: the path the archive was opened from.
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

    // Records one message per unique node_modules directory, at any depth.
    // archive: the opened archive.
    // errors: the list that receives the problem descriptions.
    private static void CollectNodeModulesErrors(ZipArchive archive, List<string> errors) {
        HashSet<string> reportedDirectories = new(StringComparer.Ordinal);

        foreach (ZipArchiveEntry entry in archive.Entries) {
            string? directoryPath = FindNodeModulesDirectory(entry.FullName);
            if (directoryPath is null || !reportedDirectories.Add(directoryPath)) {
                continue;
            }

            errors.Add(
                $"The archive contains a 'node_modules' directory, which is not allowed (found in '{entry.FullName}').");
        }
    }

    // Finds the path of the first node_modules directory in an entry name.
    // entryName: the archive entry name, with "/" separators.
    // Returns: the directory path ending in "node_modules/", or null when no segment is named node_modules.
    private static string? FindNodeModulesDirectory(string entryName) {
        string[] segments = entryName.Split('/');

        for (int index = 0; index < segments.Length; index++) {
            if (string.Equals(segments[index], NodeModulesDirectoryName, StringComparison.Ordinal)) {
                return string.Join('/', segments[..(index + 1)]) + "/";
            }
        }

        return null;
    }

    // Records a message when the manifest entrypoint is not present as a file under dist/ in the archive.
    // archive: the opened archive.
    // manifest: the values parsed from the manifest, or null when the manifest could not be read.
    // errors: the list that receives the problem description.
    private static void CollectEntrypointErrors(ZipArchive archive, Manifest? manifest, List<string> errors) {
        if (manifest is null || manifest.Entrypoint.Length == 0) {
            return;
        }

        string entrypointPath = $"{DistDirectoryName}/{manifest.Entrypoint}";

        bool exists = archive.Entries.Any(entry =>
            string.Equals(entry.FullName, entrypointPath, StringComparison.Ordinal)
            && !entry.FullName.EndsWith('/')
            && entry.Name.Length > 0);

        if (exists) {
            return;
        }

        errors.Add(
            $"The entrypoint file '{entrypointPath}' specified in manifest.json does not exist in the archive.");
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
