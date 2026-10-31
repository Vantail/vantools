namespace VanTools.Core;

// Reports invalid or incomplete command-line arguments.
public sealed class UsageException : Exception {
    // message: the user-facing description of the argument problem.
    public UsageException(string message) : base(message) {
    }
}
