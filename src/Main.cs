using VanTools.Core;
using VanTools.Pack;

CommandRegistry registry = new("vantools");
registry.Register(new PackCommand());

if (args.Length == 0) {
    Console.Out.Write(registry.RenderTopLevelUsage());
    return ExitCodes.UsageError;
}

if (args[0] is "--help" or "-h") {
    Console.Out.Write(registry.RenderTopLevelUsage());
    return ExitCodes.Success;
}

if (!registry.TryGet(args[0], out ICommand? command)) {
    ErrorWriter.Write($"unknown command '{args[0]}'");
    Console.Error.Write(registry.RenderTopLevelUsage());
    return ExitCodes.UsageError;
}

return command.Execute(args[1..]);
