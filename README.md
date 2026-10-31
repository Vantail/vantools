# VanTools

A command-line toolset for working with `.van` plugin archives.

`.van` is a ZIP-based package format for plugins. VanTools provides commands to build and inspect these archives.

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) or later.

## Building

Restore dependencies and build the solution:

```bash
dotnet restore VanTools.sln
dotnet build VanTools.sln --configuration Release
```

## Usage

### Pack

Packs a source directory into a `.van` archive.

```bash
dotnet run --project src/VanTools.csproj -- pack --s <source-dir> --t <target-dir>
```

- `--s`: The source directory containing `manifest.json` and a `dist/` directory.
- `--t`: The target directory where the `<name>-<version>.van` archive will be written.

Example:

```bash
dotnet run --project src/VanTools.csproj -- pack --s examples/empty-archive --t ./output
```

This reads the manifest from `examples/empty-archive/`, validates it, and creates `output/empty-archive-0.1.0.van`.

## The `.van` Format

The archive format, validation rules, and path-safety requirements are defined in [SPEC.md](./docs/SPEC.md).

## Development

Run the test suite:

```bash
dotnet test VanTools.sln --configuration Release
```

Continuous Integration runs the tests on Windows, macOS, and Linux via GitHub Actions.

## Contributing

See [CONTRIBUTING.md](./CONTRIBUTING.md) for guidelines on how to contribute.

## Security

See [SECURITY.md](./SECURITY.md) for our security policy and instructions on how to report vulnerabilities.

## License

See [LICENSE](./LICENSE) for the project's license.
