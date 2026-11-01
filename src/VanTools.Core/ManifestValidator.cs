using System.Text.Json;
using System.Text.RegularExpressions;

namespace VanTools.Core;

// Validates the contents of a manifest.json document, collecting every problem instead of stopping at the first one.
public static class ManifestValidator {
    private const int MaxPackageNameLength = 214;

    private static readonly Regex PackageNamePattern = new(
        "^[a-z0-9][a-z0-9._-]*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex SemVerPattern = new(
        "^" +
        "(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)" +
        "(?:-((?:0|[1-9]\\d*|\\d*[A-Za-z-][0-9A-Za-z-]*)" +
        "(?:\\.(?:0|[1-9]\\d*|\\d*[A-Za-z-][0-9A-Za-z-]*))*))?" +
        "(?:\\+([0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*))?" +
        "$",
        RegexOptions.CultureInvariant);

    // Validates a manifest.json document.
    // jsonContent: the raw manifest.json text.
    // Returns: an empty list when the manifest is valid; otherwise one message per problem found.
    // Throws ArgumentNullException: when jsonContent is null.
    public static List<string> Validate(string jsonContent) {
        return Validate(jsonContent, out _);
    }

    // Validates a manifest.json document and reports the values it could read.
    // jsonContent: the raw manifest.json text.
    // manifest: the parsed manifest when the document is an object that carries a name and version, even when other problems are reported; otherwise null.
    // Returns: an empty list when the manifest is valid; otherwise one message per problem found.
    // Throws ArgumentNullException: when jsonContent is null.
    public static List<string> Validate(string jsonContent, out Manifest? manifest) {
        ArgumentNullException.ThrowIfNull(jsonContent);

        List<string> errors = [];
        manifest = null;

        JsonDocument document;
        try {
            document = JsonDocument.Parse(jsonContent);
        } catch (JsonException exception) {
            errors.Add($"Invalid JSON format: {exception.Message}");
            return errors;
        }

        using (document) {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) {
                errors.Add("Manifest root must be a JSON object.");
                return errors;
            }

            string? name = ReadRequiredString(root, "name", errors);
            string? version = ReadRequiredString(root, "version", errors);
            string? entrypoint = ReadRequiredString(root, "entrypoint", errors);

            if (name is not null) {
                ValidateName(name, errors);
            }

            if (version is not null && !SemVerPattern.IsMatch(version)) {
                errors.Add($"Manifest member 'version' is not a valid SemVer 2.0.0 version: '{version}'.");
            }

            if (entrypoint is not null) {
                ValidateEntrypoint(entrypoint, errors);
            }

            if (name is not null && version is not null) {
                manifest = new Manifest(name, version, entrypoint ?? string.Empty);
            }
        }

        return errors;
    }

    // Reads a required string member and records a message when it is absent, not a string, or blank.
    // root: the manifest object.
    // memberName: the member to read.
    // errors: the list that receives the problem description.
    // Returns: the member value, or null when it is unusable.
    private static string? ReadRequiredString(JsonElement root, string memberName, List<string> errors) {
        if (!root.TryGetProperty(memberName, out JsonElement value)
            || value.ValueKind != JsonValueKind.String) {
            errors.Add($"Manifest member '{memberName}' is missing or is not a string.");
            return null;
        }

        string? text = value.GetString();
        if (string.IsNullOrWhiteSpace(text)) {
            errors.Add($"Manifest member '{memberName}' must not be empty.");
            return null;
        }

        return text;
    }

    // Records a message when the name is not an npm package name, so it can never carry a path separator or a traversal segment.
    // name: the name value from the manifest.
    // errors: the list that receives the problem description.
    private static void ValidateName(string name, List<string> errors) {
        if (name.Length > MaxPackageNameLength || !PackageNamePattern.IsMatch(name)) {
            errors.Add($"Manifest member 'name' is not a valid npm package name: '{name}'.");
        }
    }

    // Records a message when the entrypoint is absolute, carries a drive letter, or contains a ".." segment.
    // entrypoint: the entrypoint value from the manifest.
    // errors: the list that receives the problem description.
    private static void ValidateEntrypoint(string entrypoint, List<string> errors) {
        if (entrypoint.StartsWith('/') || entrypoint.StartsWith('\\')) {
            errors.Add($"Manifest member 'entrypoint' must be a relative path: '{entrypoint}'.");
            return;
        }

        if (entrypoint.Length >= 2 && char.IsLetter(entrypoint[0]) && entrypoint[1] == ':') {
            errors.Add($"Manifest member 'entrypoint' must not contain a drive letter: '{entrypoint}'.");
            return;
        }

        foreach (string segment in entrypoint.Split('/', '\\')) {
            if (segment == "..") {
                errors.Add($"Manifest member 'entrypoint' must not contain '..' segments: '{entrypoint}'.");
                return;
            }
        }
    }
}
