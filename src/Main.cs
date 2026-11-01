using VanTools.Core;
using VanTools.Pack;
using VanTools.Unpack;
using VanTools.Verify;

CommandRegistry registry = new("vantools");
registry.Register(new PackCommand());
registry.Register(new VerifyCommand());
registry.Register(new UnpackCommand());

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
