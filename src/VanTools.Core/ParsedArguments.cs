namespace VanTools.Core;

// The option values produced by ArgumentParser.
public sealed class ParsedArguments {
    private readonly IReadOnlyDictionary<string, string> values;

    // values: the option name to value map; the map is used directly, not copied.
    public ParsedArguments(IReadOnlyDictionary<string, string> values) {
        ArgumentNullException.ThrowIfNull(values);

        this.values = values;
    }

    // Returns the value supplied for the named option.
    // name: the option name without leading dashes.
    // Throws KeyNotFoundException: when the option was not supplied.
    public string Get(string name) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!values.TryGetValue(name, out string? value)) {
            throw new KeyNotFoundException($"Option '--{name}' was not provided.");
        }

        return value;
    }
}
