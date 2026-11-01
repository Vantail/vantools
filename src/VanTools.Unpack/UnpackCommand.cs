using VanTools.Core;

namespace VanTools.Unpack;

// The "unpack" subcommand, which extracts a .van archive into a target directory.
public sealed class UnpackCommand : ICommand {
    private static readonly ArgumentParser Parser = new ArgumentParser()
        .Required("s", "source archive")
        .Required("t", "target directory");

    // The subcommand name.
    public string Name => "unpack";

    // A one-line description shown in the top-level usage listing.
    public string Description => "Extract a .van archive into a target directory.";

    // Unpacks the archive named by the "s" option into the directory named by the "t" option.
    // args: the arguments that follow "unpack"; expects "s" and "t".
    // Returns: ExitCodes.Success when the archive is valid, ExitCodes.UsageError for invalid arguments, or ExitCodes.RuntimeError when the archive is invalid.
    // Side effects: reads the archive, creates the target directory, and writes "error:" lines to stderr on failure.
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

    // Validates the archive and prepares the target directory.
    // sourcePath: the path to the .van archive.
    // targetDirectory: the directory that will receive the extracted files.
    // Returns: ExitCodes.Success when the archive is valid; otherwise ExitCodes.RuntimeError.
    // Side effects: reads the archive and creates the target directory.
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

        // Extraction is not implemented yet, so only the target directory is prepared.
        Directory.CreateDirectory(targetDirectory);

        return ExitCodes.Success;
    }
}
