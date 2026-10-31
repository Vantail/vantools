namespace VanTools.Core;

// A CLI subcommand that can be dispatched by name.
public interface ICommand {
    // The subcommand name used on the command line, for example "pack".
    string Name { get; }

    // A one-line description shown in the top-level usage listing.
    string Description { get; }

    // Executes the subcommand.
    // args: the arguments that follow the subcommand name.
    // Returns: a process exit code; see ExitCodes.
    int Execute(string[] args);
}
