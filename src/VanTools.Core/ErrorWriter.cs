namespace VanTools.Core;

// Writes CLI error lines to stderr in the format every command uses.
public static class ErrorWriter {
    // Writes one "error: <message>" line to stderr.
    // message: the user-facing description of the problem.
    // Side effects: writes to stderr.
    public static void Write(string message) {
        Console.Error.WriteLine($"error: {message}");
    }
}
