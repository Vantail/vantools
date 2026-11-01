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

### Verify

Validates an existing `.van` archive against the format specification.

```bash
dotnet run --project src/VanTools.csproj -- verify --s <archive-path>
```

- `--s`: The path to the `.van` archive to verify.

Example:

```bash
dotnet run --project src/VanTools.csproj -- verify --s ./output/example-plugin-0.1.0.van
```

The `verify` command performs the following checks:

- Parses and validates `manifest.json` (required fields, semantic versioning, safe entrypoint path).
- Confirms the archive filename matches the `<name>-<version>.van` pattern derived from the manifest.
- Ensures a `dist/` directory exists at the archive root.
- Confirms no `node_modules` directory exists at any depth.
- Ensures the file specified by `entrypoint` exists inside `dist/`.

If all checks pass, it prints `OK` and exits with code `0`. If any check fails, it prints all detected errors to stderr and exits with a non-zero code.

### Unpack

Extracts an existing `.van` archive into a target directory.

```bash
dotnet run --project src/VanTools.csproj -- unpack --s <archive-path> --t <target-dir>
```

- `--s`: The path to the `.van` archive to extract.
- `--t`: The target directory to extract the contents into.

Example:

```bash
dotnet run --project src/VanTools.csproj -- unpack --s ./output/example-plugin-0.1.0.van --t ./extracted-plugin
```

Before extracting, the command validates the archive using the same checks as `verify`. If the archive is valid, it **completely wipes** the target directory (if it exists) and extracts `manifest.json` and the `dist/` folder into it.

The extraction process includes defenses against path traversal (Zip-Slip) to ensure no files are written outside the target directory. For safety, the command refuses to use a filesystem root (e.g., `/` or `C:\`) or the current working directory as the target.

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
