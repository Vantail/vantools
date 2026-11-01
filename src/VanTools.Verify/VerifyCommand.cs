using VanTools.Core;

namespace VanTools.Verify;

// The "verify" subcommand, which reports the problems found in a .van archive.
public sealed class VerifyCommand : ICommand {
    private static readonly ArgumentParser Parser = new ArgumentParser()
        .Required("s", "source archive");

    // The subcommand name.
    public string Name => "verify";

    // A one-line description shown in the top-level usage listing.
    public string Description => "Verify the structure and contents of a .van archive.";

    // Verifies the archive named by the "--s" option.
    // args: the arguments that follow "verify"; expects "--s".
    // Returns: ExitCodes.Success when the archive is valid, ExitCodes.UsageError for invalid arguments, or ExitCodes.RuntimeError when the archive is invalid.
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
        List<string> errors = ArchiveValidator.Validate(archivePath);

        if (errors.Count > 0) {
            foreach (string error in errors) {
                ErrorWriter.Write(error);
            }

            return ExitCodes.RuntimeError;
        }

        Console.Out.WriteLine("OK");

        return ExitCodes.Success;
    }
}
