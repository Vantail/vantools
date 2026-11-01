using System.IO.Compression;
using VanTools.Core;

namespace VanTools.Unpack;

// The "unpack" subcommand, which extracts a .van archive into a target directory.
public sealed class UnpackCommand : ICommand {
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static readonly ArgumentParser Parser = new ArgumentParser()
        .Required("s", "source archive")
        .Required("t", "target directory");

    // The subcommand name.
    public string Name => "unpack";

    // A one-line description shown in the top-level usage listing.
    public string Description => "Extract a .van archive into a target directory.";

    // Unpacks the archive named by the "s" option into the directory named by the "t" option.
    // args: the arguments that follow "unpack"; expects "s" and "t".
    // Returns: ExitCodes.Success when the archive was extracted, ExitCodes.UsageError for invalid arguments, or ExitCodes.RuntimeError when the archive is invalid or extraction fails.
    // Side effects: reads the archive, replaces the target directory, writes its files, and writes "error:" lines to stderr on failure.
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
            return Unpack(parsed.Get("s"), parsed.Get("t"));
        } catch (Exception exception) {
            ErrorWriter.Write(exception.Message);
            return ExitCodes.RuntimeError;
        }
    }

    // Validates the archive, replaces the target directory, and extracts the archive contents into it.
    // sourcePath: the path to the .van archive.
    // targetDirectory: the directory that receives the extracted files; existing contents are removed.
    // Returns: ExitCodes.Success when the archive was extracted; otherwise ExitCodes.RuntimeError.
    // Side effects: reads the archive, deletes and recreates the target directory, and writes the extracted files.
    private static int Unpack(string sourcePath, string targetDirectory) {
        if (!File.Exists(sourcePath)) {
            ErrorWriter.Write($"Cannot open file '{sourcePath}': the file does not exist.");
            return ExitCodes.RuntimeError;
        }

        List<string> errors = ArchiveValidator.Validate(sourcePath);
        if (errors.Count > 0) {
            foreach (string error in errors) {
                ErrorWriter.Write(error);
            }

            return ExitCodes.RuntimeError;
        }

        if (Directory.Exists(targetDirectory)) {
            Directory.Delete(targetDirectory, recursive: true);
        }

        Directory.CreateDirectory(targetDirectory);
        Extract(sourcePath, targetDirectory);

        Console.Out.WriteLine("OK");

        return ExitCodes.Success;
    }

    // Extracts every entry of the archive into the target directory.
    // sourcePath: the path to the .van archive.
    // targetDirectory: the directory that receives the extracted files; it must already exist.
    // Throws IOException: when an entry would be written outside the target directory.
    // Side effects: creates directories and writes files under the target directory.
    private static void Extract(string sourcePath, string targetDirectory) {
        string targetRoot = Path.GetFullPath(targetDirectory);
        string targetPrefix = targetRoot + Path.DirectorySeparatorChar;

        using ZipArchive archive = ZipFile.OpenRead(sourcePath);

        foreach (ZipArchiveEntry entry in archive.Entries) {
            string destinationPath = Path.GetFullPath(Path.Combine(targetRoot, entry.FullName));

            // Defense in depth: the validator rejects unsafe names, but refuse to write outside the target directory regardless.
            if (!destinationPath.StartsWith(targetPrefix, PathComparison)) {
                throw new IOException(
                    $"Archive entry '{entry.FullName}' would be extracted outside the target directory '{targetRoot}'.");
            }

            if (entry.FullName.EndsWith('/')) {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            string? parentDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(parentDirectory)) {
                Directory.CreateDirectory(parentDirectory);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }
}
