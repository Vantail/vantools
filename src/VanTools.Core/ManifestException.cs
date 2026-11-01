namespace VanTools.Core;

// Reports one or more problems with a manifest.json that is missing, unreadable, or invalid.
public sealed class ManifestException : Exception {
    // message: the user-facing description of a single problem.
    public ManifestException(string message) : base(message) {
        Errors = [message];
    }

    // errors: every user-facing problem, one entry per message.
    // Throws ArgumentNullException: when errors is null.
    public ManifestException(IReadOnlyList<string> errors) : base(JoinErrors(errors)) {
        Errors = errors;
    }

    // Every problem found, in the order it was collected.
    public IReadOnlyList<string> Errors { get; }

    // Joins the collected problems into the exception message, one problem per line.
    // errors: every user-facing problem.
    // Returns: the problems joined by newlines.
    // Throws ArgumentNullException: when errors is null.
    private static string JoinErrors(IReadOnlyList<string> errors) {
        ArgumentNullException.ThrowIfNull(errors);

        return string.Join(Environment.NewLine, errors);
    }
}
