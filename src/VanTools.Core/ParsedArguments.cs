namespace VanTools.Core;

public sealed class ParsedArguments {
    private readonly IReadOnlyDictionary<string, string> values;

    public ParsedArguments(IReadOnlyDictionary<string, string> values) {
        ArgumentNullException.ThrowIfNull(values);

        this.values = values;
    }

    public string Get(string name) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!values.TryGetValue(name, out string? value)) {
            throw new KeyNotFoundException($"Option '--{name}' was not provided.");
        }

        return value;
    }
}
