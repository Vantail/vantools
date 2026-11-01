using System.Text;

namespace VanTools.Core;

// The validated contents of a manifest.json file: package name, SemVer 2.0.0 version, and entrypoint path relative to dist/.
public sealed record Manifest(string Name, string Version, string Entrypoint) {
    private const string ManifestFileName = "manifest.json";
    private const string DistDirectoryName = "dist";
    private const int MaxManifestBytes = 1024 * 1024;

    // Loads and validates the manifest.json of a plugin source directory.
    // sourceDirectory: the directory that contains manifest.json and dist/.
    // Returns: the validated manifest.
    // Throws ManifestException: when the file is missing, is not valid JSON, is not a JSON object, has a missing, non-string, or blank member, has a version that is not SemVer 2.0.0, has an entrypoint that is absolute, carries a drive letter, or contains a ".." segment, or has an entrypoint that does not exist under dist/.
    // Side effects: reads the manifest file from disk.
    public static Manifest LoadFrom(string sourceDirectory) {
        ArgumentNullException.ThrowIfNull(sourceDirectory);

        string manifestPath = Path.Combine(sourceDirectory, ManifestFileName);
        if (!File.Exists(manifestPath)) {
            throw new ManifestException($"manifest.json not found at '{manifestPath}'.");
        }

        using FileStream stream = new(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaxManifestBytes) {
            throw new ManifestException(
                $"manifest.json is {stream.Length} bytes; the limit is {MaxManifestBytes}.");
        }

        string jsonContent;
        using (StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true)) {
            jsonContent = reader.ReadToEnd();
        }

        List<string> errors = ManifestValidator.Validate(jsonContent, out Manifest? manifest);

        if (manifest is null) {
            if (errors.Count == 0) {
                errors.Add("Manifest could not be parsed.");
            }

            throw new ManifestException(errors);
        }

        if (errors.Count == 0) {
            string entrypointPath = Path.Combine(sourceDirectory, DistDirectoryName, manifest.Entrypoint);
            if (!File.Exists(entrypointPath)) {
                errors.Add($"Manifest entrypoint file not found under dist/: '{manifest.Entrypoint}'.");
            }
        }

        if (errors.Count > 0) {
            throw new ManifestException(errors);
        }

        return manifest;
    }
}
