using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace VanTools.Core;

// Holds the available subcommands and resolves them by name.
public sealed class CommandRegistry {
    private readonly List<ICommand> commands = [];
    private readonly string executableName;

    // executableName: the executable name used in the top-level usage line.
    // Throws ArgumentException: when executableName is null, empty, or whitespace.
    public CommandRegistry(string executableName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);

        this.executableName = executableName;
    }

    // The registered commands in registration order.
    public IReadOnlyList<ICommand> Commands => commands;

    // Adds a command to the registry.
    // command: the command to add.
    // Throws ArgumentNullException: when command is null.
    // Throws InvalidOperationException: when a command with the same name is already registered.
    public void Register(ICommand command) {
        ArgumentNullException.ThrowIfNull(command);

        if (commands.Any(existing => existing.Name == command.Name)) {
            throw new InvalidOperationException($"A command named '{command.Name}' is already registered.");
        }

        commands.Add(command);
    }

    // Resolves a command by its name.
    // name: the command name to look up.
    // command: the matching command when found; otherwise null.
    // Returns: true when a command matches name.
    // Throws ArgumentException: when name is null, empty, or whitespace.
    public bool TryGet(string name, [NotNullWhen(true)] out ICommand? command) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        command = commands.Find(existing => existing.Name == name);

        return command is not null;
    }

    // Renders the top-level usage listing.
    // Returns: the usage line followed by every registered command with its description.
    public string RenderTopLevelUsage() {
        StringBuilder builder = new();

        builder.AppendLine($"Usage: {executableName} <command> [options]");
        builder.AppendLine();
        builder.AppendLine("Commands:");

        foreach (ICommand command in commands) {
            builder.AppendLine($"  {command.Name}  {command.Description}");
        }

        return builder.ToString();
    }
}
