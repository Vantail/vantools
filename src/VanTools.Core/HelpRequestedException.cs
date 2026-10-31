namespace VanTools.Core;

// Signals that the user asked for help instead of a normal run.
public sealed class HelpRequestedException : Exception {
    // Creates the signal; it carries no message.
    public HelpRequestedException() {
    }
}
