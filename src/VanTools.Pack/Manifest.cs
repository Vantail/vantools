using System.Text.Json;
using System.Text.RegularExpressions;

namespace VanTools.Pack;

// The validated contents of a manifest.json file: package name, SemVer 2.0.0 version, and entrypoint path relative to dist/.
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

    // Loads and validates the manifest.json of a plugin source directory.
    // sourceDirectory: the directory that contains manifest.json and dist/.
    // Returns: the validated manifest.
    // Throws ManifestException: when the file is missing, is not valid JSON, is not a JSON object, has a missing, non-string, or blank member, has a version that is not SemVer 2.0.0, or has an entrypoint that is absolute, carries a drive letter, contains a ".." segment, or does not exist under dist/.
    // Side effects: reads the manifest file from disk.
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

            ValidateVersion(version);
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

    private static void ValidateVersion(string version) {
        if (SemVerPattern.IsMatch(version)) {
            return;
        }

        throw new ManifestException(
            $"Manifest member 'version' is not a valid SemVer 2.0.0 version: '{version}'.");
    }

    private static void ValidateEntrypoint(string sourceDirectory, string entrypoint) {
        ThrowIfEntrypointIsUnsafe(entrypoint);

        string entrypointPath = Path.Combine(sourceDirectory, DistDirectoryName, entrypoint);
        if (!File.Exists(entrypointPath)) {
            throw new ManifestException($"Manifest entrypoint file not found under dist/: '{entrypoint}'.");
        }
    }

    private static void ThrowIfEntrypointIsUnsafe(string entrypoint) {
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
    }
}

// Reports a manifest.json that is missing, unreadable, or invalid.
public sealed class ManifestException : Exception {
    // message: the user-facing description of the problem.
    public ManifestException(string message) : base(message) {
    }
}
