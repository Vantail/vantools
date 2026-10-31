namespace VanTools.Core;

// Process exit codes shared by all commands.
public static class ExitCodes {
    // The run succeeded.
    public const int Success = 0;

    // The run failed while performing the operation.
    public const int RuntimeError = 1;

    // The arguments were invalid or incomplete.
    public const int UsageError = 2;
}
