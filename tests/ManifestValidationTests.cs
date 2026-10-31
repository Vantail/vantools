using VanTools.Pack;

namespace VanTest;

public sealed class ManifestValidationTests : IDisposable {
    private readonly string sourceDirectory;

    public ManifestValidationTests() {
        sourceDirectory = Path.Combine(
            Path.GetTempPath(),
            "vantools-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceDirectory);
    }

    public void Dispose() {
        if (Directory.Exists(sourceDirectory)) {
            Directory.Delete(sourceDirectory, recursive: true);
        }
    }

    [Fact]
    public void LoadFrom_ValidManifest_ReturnsValues() {
        WriteManifest("""{ "name": "example", "version": "0.1.0", "entrypoint": "app.ts" }""");
        WriteEntrypoint();

        Manifest manifest = Manifest.LoadFrom(sourceDirectory);

        Assert.Equal("example", manifest.Name);
        Assert.Equal("0.1.0", manifest.Version);
        Assert.Equal("app.ts", manifest.Entrypoint);
    }

    [Fact]
    public void LoadFrom_UnrecognizedMembers_IgnoresThem() {
        WriteManifest(
            """{ "name": "example", "version": "0.1.0", "entrypoint": "app.ts", "unknown": 42 }""");
        WriteEntrypoint();

        Manifest manifest = Manifest.LoadFrom(sourceDirectory);

        Assert.Equal("example", manifest.Name);
    }

    [Fact]
    public void LoadFrom_PrereleaseAndBuildMetadata_IsAccepted() {
        WriteManifest(
            """{ "name": "example", "version": "1.2.3-alpha.1+build.42", "entrypoint": "app.ts" }""");
        WriteEntrypoint();

        Manifest manifest = Manifest.LoadFrom(sourceDirectory);

        Assert.Equal("1.2.3-alpha.1+build.42", manifest.Version);
    }

    [Fact]
    public void LoadFrom_MissingManifest_Throws() {
        Assert.Throws<ManifestException>(() => Manifest.LoadFrom(sourceDirectory));
    }

    [Fact]
    public void LoadFrom_MalformedJson_Throws() {
        WriteManifest("""{ "name": "example", """);

        Assert.Throws<ManifestException>(() => Manifest.LoadFrom(sourceDirectory));
    }

    [Fact]
    public void LoadFrom_NonObjectRoot_Throws() {
        WriteManifest("[]");

        Assert.Throws<ManifestException>(() => Manifest.LoadFrom(sourceDirectory));
    }

    [Fact]
    public void LoadFrom_MissingName_Throws() {
        WriteManifest("""{ "version": "0.1.0", "entrypoint": "app.ts" }""");
        WriteEntrypoint();

        Assert.Throws<ManifestException>(() => Manifest.LoadFrom(sourceDirectory));
    }

    [Fact]
    public void LoadFrom_NonStringName_Throws() {
        WriteManifest("""{ "name": 7, "version": "0.1.0", "entrypoint": "app.ts" }""");
        WriteEntrypoint();

        Assert.Throws<ManifestException>(() => Manifest.LoadFrom(sourceDirectory));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LoadFrom_EmptyOrWhitespaceName_Throws(string name) {
        WriteManifest($$"""{ "name": "{{name}}", "version": "0.1.0", "entrypoint": "app.ts" }""");
        WriteEntrypoint();

        Assert.Throws<ManifestException>(() => Manifest.LoadFrom(sourceDirectory));
    }

    [Fact]
    public void LoadFrom_MissingVersion_Throws() {
        WriteManifest("""{ "name": "example", "entrypoint": "app.ts" }""");
        WriteEntrypoint();

        Assert.Throws<ManifestException>(() => Manifest.LoadFrom(sourceDirectory));
    }

    [Fact]
    public void LoadFrom_NonStringVersion_Throws() {
        WriteManifest("""{ "name": "example", "version": 100, "entrypoint": "app.ts" }""");
        WriteEntrypoint();

        Assert.Throws<ManifestException>(() => Manifest.LoadFrom(sourceDirectory));
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("v1.0.0")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0-")]
    public void LoadFrom_InvalidSemVer_Throws(string version) {
        WriteManifest($$"""{ "name": "example", "version": "{{version}}", "entrypoint": "app.ts" }""");
        WriteEntrypoint();

        Assert.Throws<ManifestException>(() => Manifest.LoadFrom(sourceDirectory));
    }

    [Fact]
    public void LoadFrom_MissingEntrypoint_Throws() {
        WriteManifest("""{ "name": "example", "version": "0.1.0" }""");
        WriteEntrypoint();

        Assert.Throws<ManifestException>(() => Manifest.LoadFrom(sourceDirectory));
    }

    [Fact]
    public void LoadFrom_NonStringEntrypoint_Throws() {
        WriteManifest("""{ "name": "example", "version": "0.1.0", "entrypoint": 3 }""");
        WriteEntrypoint();

        Assert.Throws<ManifestException>(() => Manifest.LoadFrom(sourceDirectory));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void LoadFrom_EmptyOrWhitespaceEntrypoint_Throws(string entrypoint) {
        WriteManifest($$"""{ "name": "example", "version": "0.1.0", "entrypoint": "{{entrypoint}}" }""");
        WriteEntrypoint();

        Assert.Throws<ManifestException>(() => Manifest.LoadFrom(sourceDirectory));
    }

    [Fact]
    public void LoadFrom_AbsoluteEntrypoint_Throws() {
        WriteManifest("""{ "name": "example", "version": "0.1.0", "entrypoint": "/app.ts" }""");
        WriteEntrypoint();

        Assert.Throws<ManifestException>(() => Manifest.LoadFrom(sourceDirectory));
    }

    [Fact]
    public void LoadFrom_ParentTraversalEntrypoint_Throws() {
        WriteManifest("""{ "name": "example", "version": "0.1.0", "entrypoint": "../app.ts" }""");
        WriteEntrypoint();

        Assert.Throws<ManifestException>(() => Manifest.LoadFrom(sourceDirectory));
    }

    [Fact]
    public void LoadFrom_MissingEntrypointFile_Throws() {
        WriteManifest("""{ "name": "example", "version": "0.1.0", "entrypoint": "missing.ts" }""");
        WriteEntrypoint();

        Assert.Throws<ManifestException>(() => Manifest.LoadFrom(sourceDirectory));
    }

    private void WriteManifest(string json) {
        File.WriteAllText(Path.Combine(sourceDirectory, "manifest.json"), json);
    }

    private void WriteEntrypoint() {
        string distDirectory = Path.Combine(sourceDirectory, "dist");
        Directory.CreateDirectory(distDirectory);
        File.WriteAllText(Path.Combine(distDirectory, "app.ts"), string.Empty);
    }
}
