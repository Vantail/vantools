using VanTools.Core;
using VanTools.Pack;

ICommand[] commands = [new PackCommand()];

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

ICommand? command = null;
foreach (ICommand candidate in commands)
{
    if (candidate.Name == args[0])
    {
        command = candidate;
        break;
    }
}

if (command is null)
{
    PrintUsage();
    return 1;
}

return command.Execute(args[1..]);

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: vantools <command> [options]");
    Console.Error.WriteLine("Commands:");
    Console.Error.WriteLine("  pack --s <source> --t <target>");
}
