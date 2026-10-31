using System.Text.Json;
using System.Text.RegularExpressions;

namespace VanTools.Pack;

public sealed record Manifest(string Name, string Version, string Entrypoint) {
    private const string ManifestFileName = "manifest.json";
    private const string DistDirectoryName = "dist";

    private static readonly Regex SemVerPattern = new(
        "^" +
        "(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)" +
        "(?:-((?:0|[1-9]\\d*|\\d*[A-Za-z-][0-9A-Za-z-]*)" +
        "(?:\\.(?:0|[1-9]\\d*|\\d*[A-Za-z-][0-9A-Za-z-]*))*))?" +
        "(?:\\+([0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*))?" +
        "$",
        RegexOptions.CultureInvariant);

    public static Manifest LoadFrom(string sourceDirectory) {
        ArgumentNullException.ThrowIfNull(sourceDirectory);

        string manifestPath = Path.Combine(sourceDirectory, ManifestFileName);
        if (!File.Exists(manifestPath)) {
            throw new ManifestException($"manifest.json not found at '{manifestPath}'.");
        }

        JsonDocument document;
        try {
            document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        } catch (JsonException exception) {
            throw new ManifestException($"Invalid manifest JSON: {exception.Message}");
        }

        using (document) {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) {
                throw new ManifestException("Manifest root must be a JSON object.");
            }

            string name = RequireString(root, "name");
            string version = RequireString(root, "version");
            string entrypoint = RequireString(root, "entrypoint");

            if (!SemVerPattern.IsMatch(version)) {
                throw new ManifestException(
                    $"Manifest member 'version' is not a valid SemVer 2.0.0 version: '{version}'.");
            }

            ValidateEntrypoint(sourceDirectory, entrypoint);

            return new Manifest(name, version, entrypoint);
        }
    }

    private static string RequireString(JsonElement root, string memberName) {
        if (!root.TryGetProperty(memberName, out JsonElement value)
            || value.ValueKind != JsonValueKind.String) {
            throw new ManifestException($"Manifest member '{memberName}' is missing or is not a string.");
        }

        string? text = value.GetString();
        if (string.IsNullOrWhiteSpace(text)) {
            throw new ManifestException($"Manifest member '{memberName}' must not be empty.");
        }

        return text;
    }

    private static void ValidateEntrypoint(string sourceDirectory, string entrypoint) {
        if (entrypoint.StartsWith('/') || entrypoint.StartsWith('\\')) {
            throw new ManifestException(
                $"Manifest member 'entrypoint' must be a relative path: '{entrypoint}'.");
        }

        if (entrypoint.Length >= 2 && char.IsLetter(entrypoint[0]) && entrypoint[1] == ':') {
            throw new ManifestException(
                $"Manifest member 'entrypoint' must not contain a drive letter: '{entrypoint}'.");
        }

        foreach (string segment in entrypoint.Split('/', '\\')) {
            if (segment == "..") {
                throw new ManifestException(
                    $"Manifest member 'entrypoint' must not contain '..' segments: '{entrypoint}'.");
            }
        }

        string entrypointPath = Path.Combine(sourceDirectory, DistDirectoryName, entrypoint);
        if (!File.Exists(entrypointPath)) {
            throw new ManifestException($"Manifest entrypoint file not found under dist/: '{entrypoint}'.");
        }
    }
}

public sealed class ManifestException : Exception {
    public ManifestException(string message) : base(message) {
    }
}
