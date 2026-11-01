using System.Text;

namespace VanTools.Pack;

// One archive entry derived from a file under dist/: its archive name and the source file path it was built from.
public sealed record PathEntry(string EntryName, string FullPath);

// Validates the contents of dist/ and maps them to archive entry names.
public static class PathSafety {
    // The directory name that is skipped, along with its subtree, at any depth.
    public const string NodeModulesDirectoryName = "node_modules";

    // The warning written once for each skipped node_modules directory.
    public const string NodeModulesWarning = "warning: node_modules/ skipped";

    // Reports whether a name is already in Unicode NFC form.
    // name: the file or directory name to check.
    // Returns: true when name is NFC.
    public static bool IsNfc(string name) {
        ArgumentNullException.ThrowIfNull(name);

        return name.IsNormalized(NormalizationForm.FormC);
    }

    // Builds the archive entries for every regular file under dist/.
    // distDirectory: the dist directory to enumerate.
    // warnings: receives one line for each skipped node_modules directory.
    // Returns: the entries in ordinal name order, each named "dist/<relative path>".
    // Throws PathSafetyException: when an entry is a symlink or other non-regular file, a file or directory name is not NFC, an entry name breaks the segment rules, or two entries differ only by case.
    // Side effects: reads the file system and writes warnings to warnings.
    public static IReadOnlyList<PathEntry> BuildEntries(string distDirectory, TextWriter warnings) {
        ArgumentNullException.ThrowIfNull(distDirectory);
        ArgumentNullException.ThrowIfNull(warnings);

        List<PathEntry> entries = [];
        Dictionary<string, string> seen = new(StringComparer.OrdinalIgnoreCase);

        Walk(distDirectory, entries, seen, warnings);

        return entries;
    }

    // Reports whether an archive entry name satisfies the spec path rules.
    // entryName: the entry name to check.
    // reason: the reason for rejection when the result is false; otherwise an empty string.
    // Returns: true when the name uses "/" separators and contains no empty, ".", or ".." segments.
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
        string distDirectory,
        List<PathEntry> entries,
        Dictionary<string, string> seen,
        TextWriter warnings) {
        // An explicit work list keeps directory depth off the CLR stack, so a deeply nested tree cannot overflow it.
        Stack<(DirectoryInfo Directory, string ArchivePrefix)> pending = new();
        pending.Push((new DirectoryInfo(distDirectory), "dist"));

        while (pending.Count > 0) {
            (DirectoryInfo directory, string archivePrefix) = pending.Pop();
            List<(DirectoryInfo Directory, string ArchivePrefix)> nestedDirectories = [];

            foreach (FileSystemInfo child in directory
                         .EnumerateFileSystemInfos()
                         .OrderBy(info => info.Name, StringComparer.Ordinal)) {
                ThrowIfNotRegular(child);
                ThrowIfNotNfc(child);

                if (child is DirectoryInfo
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
                    nestedDirectories.Add((nestedDirectory, entryName));
                    continue;
                }

                FileInfo file = child as FileInfo
                    ?? throw new PathSafetyException(
                        $"Unsupported file system entry (not a regular file or directory): '{child.FullName}'.");

                AddEntry(entries, seen, entryName, file);
            }

            for (int index = nestedDirectories.Count - 1; index >= 0; index--) {
                pending.Push(nestedDirectories[index]);
            }
        }
    }

    private static void ThrowIfNotNfc(FileSystemInfo entry) {
        if (IsNfc(entry.Name)) {
            return;
        }

        throw new PathSafetyException(
            $"Name is not in Unicode NFC form and would be normalized silently: '{entry.FullName}'.");
    }

    private static void AddEntry(
        List<PathEntry> entries,
        Dictionary<string, string> seen,
        string entryName,
        FileInfo file) {
        if (seen.TryGetValue(entryName, out string? existingPath)) {
            throw new PathSafetyException(
                $"Case-insensitive duplicate archive entry '{entryName}' produced by "
                + $"'{existingPath}' and '{file.FullName}'.");
        }

        seen[entryName] = file.FullName;
        entries.Add(new PathEntry(entryName, file.FullName));
    }

    private static void ThrowIfNotRegular(FileSystemInfo entry) {
        if (entry.LinkTarget is not null
            || entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
            throw new PathSafetyException(
                $"Symbolic links and other non-regular files are not allowed in dist/: '{entry.FullName}'.");
        }
    }
}

// Reports dist/ content that cannot be packed safely.
public sealed class PathSafetyException : Exception {
    // message: the user-facing description of the problem.
    public PathSafetyException(string message) : base(message) {
    }
}
