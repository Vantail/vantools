using System.Text;

namespace VanTools.Pack;

public sealed record PathEntry(string EntryName, string FullPath);

public static class PathSafety {
    public const string NodeModulesDirectoryName = "node_modules";
    public const string NodeModulesWarning = "warning: node_modules/ skipped";

    public static bool IsNfc(string name) {
        ArgumentNullException.ThrowIfNull(name);

        return name.IsNormalized(NormalizationForm.FormC);
    }

    public static IReadOnlyList<PathEntry> BuildEntries(string distDirectory, TextWriter warnings) {
        ArgumentNullException.ThrowIfNull(distDirectory);
        ArgumentNullException.ThrowIfNull(warnings);

        List<PathEntry> entries = [];
        Dictionary<string, string> seen = new(StringComparer.OrdinalIgnoreCase);

        Walk(new DirectoryInfo(distDirectory), "dist", entries, seen, warnings);

        return entries;
    }

    public static bool IsSafeArchivePath(string entryName, out string reason) {
        ArgumentNullException.ThrowIfNull(entryName);

        if (entryName.Contains('\\')) {
            reason = "must use '/' as the path separator";
            return false;
        }

        foreach (string segment in entryName.Split('/')) {
            if (segment.Length == 0) {
                reason = "must not contain empty segments";
                return false;
            }

            if (segment is "." or "..") {
                reason = "must not contain '.' or '..' segments";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static void Walk(
        DirectoryInfo directory,
        string archivePrefix,
        List<PathEntry> entries,
        Dictionary<string, string> seen,
        TextWriter warnings) {
        foreach (FileSystemInfo child in directory
                     .EnumerateFileSystemInfos()
                     .OrderBy(info => info.Name, StringComparer.Ordinal)) {
            ThrowIfNotRegular(child);

            if (!IsNfc(child.Name)) {
                throw new PathSafetyException(
                    $"Name is not in Unicode NFC form and would be normalized silently: '{child.FullName}'.");
            }

            if (child is DirectoryInfo subdirectory
                && string.Equals(child.Name, NodeModulesDirectoryName, StringComparison.Ordinal)) {
                warnings.WriteLine(NodeModulesWarning);
                continue;
            }

            string entryName = $"{archivePrefix}/{child.Name}";
            if (!IsSafeArchivePath(entryName, out string reason)) {
                throw new PathSafetyException(
                    $"Archive entry '{entryName}' derived from '{child.FullName}' {reason}.");
            }

            if (child is DirectoryInfo nestedDirectory) {
                Walk(nestedDirectory, entryName, entries, seen, warnings);
                continue;
            }

            FileInfo file = child as FileInfo
                ?? throw new PathSafetyException(
                    $"Unsupported file system entry (not a regular file or directory): '{child.FullName}'.");

            if (seen.TryGetValue(entryName, out string? existingPath)) {
                throw new PathSafetyException(
                    $"Case-insensitive duplicate archive entry '{entryName}' produced by "
                    + $"'{existingPath}' and '{file.FullName}'.");
            }

            seen[entryName] = file.FullName;
            entries.Add(new PathEntry(entryName, file.FullName));
        }
    }

    private static void ThrowIfNotRegular(FileSystemInfo entry) {
        if (entry.LinkTarget is not null
            || entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
            throw new PathSafetyException(
                $"Symbolic links and other non-regular files are not allowed in dist/: '{entry.FullName}'.");
        }
    }
}

public sealed class PathSafetyException : Exception {
    public PathSafetyException(string message) : base(message) {
    }
}
