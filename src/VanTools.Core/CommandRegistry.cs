using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace VanTools.Core;

public sealed class CommandRegistry {
    private readonly List<ICommand> commands = [];
    private readonly string executableName;

    public CommandRegistry(string executableName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);

        this.executableName = executableName;
    }

    public IReadOnlyList<ICommand> Commands => commands;

    public void Register(ICommand command) {
        ArgumentNullException.ThrowIfNull(command);

        if (commands.Any(existing => existing.Name == command.Name)) {
            throw new InvalidOperationException($"A command named '{command.Name}' is already registered.");
        }

        commands.Add(command);
    }

    public bool TryGet(string name, [NotNullWhen(true)] out ICommand? command) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        command = commands.Find(existing => existing.Name == name);

        return command is not null;
    }

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
