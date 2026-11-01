using System.IO.Compression;

namespace VanTools.Unpack;

// Writes a .van archive into a target directory, refusing unsafe targets and unsafe entry paths.
public static class ArchiveExtractor {
    private const long MaxEntryBytes = 256L * 1024 * 1024;
    private const long MaxTotalBytes = 1024L * 1024 * 1024;

    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    // Directory paths compare case-insensitively on Windows and macOS, whose default file systems ignore case.
    private static readonly StringComparison DirectoryPathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    // Replaces the target directory with the contents of the archive.
    // archivePath: the path to the .van archive.
    // targetDir: the directory that receives the extracted files; existing contents are removed.
    // Throws InvalidOperationException: when targetDir is a filesystem root, the current working directory, or an existing file, or when an entry resolves outside targetDir.
    // Side effects: deletes and recreates targetDir, then writes the extracted files.
    public static void Extract(string archivePath, string targetDir) {
        ArgumentNullException.ThrowIfNull(archivePath);
        ArgumentNullException.ThrowIfNull(targetDir);

        string resolvedTarget = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDir));

        if (IsRootOrCurrentDirectory(resolvedTarget)) {
            throw new InvalidOperationException(
                "Refusing to use a root directory or the current working directory as the target.");
        }

        if (File.Exists(resolvedTarget)) {
            throw new InvalidOperationException(
                $"The target path '{targetDir}' exists but is a file, not a directory.");
        }

        if (Directory.Exists(resolvedTarget)) {
            Directory.Delete(resolvedTarget, recursive: true);
        }

        Directory.CreateDirectory(resolvedTarget);

        using ZipArchive archive = ZipFile.OpenRead(archivePath);

        long totalBytes = 0;
        foreach (ZipArchiveEntry entry in archive.Entries) {
            long remaining = MaxTotalBytes - totalBytes;
            if (entry.Length > MaxEntryBytes) {
                throw new InvalidOperationException(
                    $"Entry '{entry.FullName}' exceeds the size limit of {MaxEntryBytes} bytes.");
            }

            if (entry.Length > remaining) {
                throw new InvalidOperationException(
                    $"Extracting the archive would exceed the total size limit of {MaxTotalBytes} bytes.");
            }

            // The declared size is untrusted, so the copy itself is also bounded.
            totalBytes += ExtractEntryCore(entry, resolvedTarget, Math.Min(MaxEntryBytes, remaining));
        }
    }

    // Extracts one archive entry into the target directory.
    // entry: the archive entry to write.
    // targetDir: the directory that receives the entry; it must already exist.
    // Throws InvalidOperationException: when the entry would be written outside the target directory.
    // Side effects: creates directories and writes the entry under targetDir.
    internal static void ExtractEntry(ZipArchiveEntry entry, string targetDir) {
        ExtractEntryCore(entry, targetDir, MaxEntryBytes);
    }

    // Extracts one archive entry with an explicit uncompressed byte budget, used by tests to exercise the limit.
    // entry: the archive entry to write.
    // targetDir: the directory that receives the entry; it must already exist.
    // maxBytes: the maximum number of bytes that may be written for this entry.
    // Throws InvalidOperationException: when the entry would be written outside the target directory or exceeds maxBytes.
    // Side effects: creates directories and writes the entry under targetDir; removes a partial file on failure.
    internal static void ExtractEntry(ZipArchiveEntry entry, string targetDir, long maxBytes) {
        ExtractEntryCore(entry, targetDir, maxBytes);
    }

    private static long ExtractEntryCore(ZipArchiveEntry entry, string targetDir, long maxBytes) {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(targetDir);

        string fullTargetDir = Path.GetFullPath(targetDir);
        string destinationPath = Path.GetFullPath(Path.Combine(fullTargetDir, entry.FullName));

        // Defense in depth: the validator rejects unsafe names, but refuse to write outside the target directory regardless.
        if (!destinationPath.StartsWith(fullTargetDir + Path.DirectorySeparatorChar, PathComparison)) {
            throw new InvalidOperationException($"Zip-Slip detected for entry: '{entry.FullName}'");
        }

        if (entry.FullName.EndsWith('/')) {
            Directory.CreateDirectory(destinationPath);
            return 0;
        }

        string? parentDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(parentDirectory)) {
            Directory.CreateDirectory(parentDirectory);
        }

        long written = 0;
        try {
            using Stream source = entry.Open();
            using FileStream destination = new(
                destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

            byte[] buffer = new byte[81920];
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0) {
                written += read;
                if (written > maxBytes) {
                    throw new InvalidOperationException(
                        $"Entry '{entry.FullName}' exceeds the size limit of {maxBytes} bytes.");
                }

                destination.Write(buffer, 0, read);
            }
        } catch {
            TryDeletePartialFile(destinationPath);
            throw;
        }

        return written;
    }

    // Removes a partially written entry after extraction fails, ignoring cleanup errors that would mask the cause.
    // path: the file to remove.
    private static void TryDeletePartialFile(string path) {
        try {
            if (File.Exists(path)) {
                File.Delete(path);
            }
        } catch (IOException) {
            // The original extraction error is the one worth reporting.
        } catch (UnauthorizedAccessException) {
            // The original extraction error is the one worth reporting.
        }
    }

    // Reports whether the resolved path is a filesystem root or the current working directory.
    // resolvedTarget: the absolute target path, without a trailing directory separator.
    // Returns: true when deleting the path could remove a root or the working directory.
    private static bool IsRootOrCurrentDirectory(string resolvedTarget) {
        string? root = Path.GetPathRoot(resolvedTarget);
        if (root is not null
            && string.Equals(
                resolvedTarget,
                Path.TrimEndingDirectorySeparator(root),
                DirectoryPathComparison)) {
            return true;
        }

        string currentDirectory = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(Environment.CurrentDirectory));

        return string.Equals(resolvedTarget, currentDirectory, DirectoryPathComparison);
    }
}
