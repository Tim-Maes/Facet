# Contributing to Facet

Thanks for helping improve Facet. This guide explains how to propose changes, prepare a development environment, and work with maintainers in a way that keeps the project healthy and welcoming.

Before contributing, please read:

- [Code of Conduct](CODE_OF_CONDUCT.md)
- [Governance](GOVERNANCE.md)
- [Security Policy](SECURITY.md)
- [Support Guide](SUPPORT.md)

## Ways to contribute

Contributions are welcome in several forms:

- Bug reports with clear reproductions
- Documentation and sample improvements
- New features and usability enhancements
- Performance work
- Tests that improve confidence in existing behavior

## Before you start

For anything larger than a trivial fix, open an issue or discussion first so maintainers and contributors can align on scope and approach.

Use the right channel:

- **GitHub Issues** for reproducible bugs and concrete feature work
- **GitHub Discussions** for questions, design exploration, and broader ideas
- **Discord** for community chat and quick feedback
- **SECURITY.md** for private vulnerability reports

## Development setup

1. Fork the repository and clone your fork.
   ```bash
   git clone https://github.com/YOUR_USERNAME/Facet.git
   cd Facet
   ```
2. Install a recent .NET SDK that can build the repository's supported target frameworks.
3. Restore, build, and test the solution.
   ```bash
   dotnet restore
   dotnet build Facet.sln
   dotnet test Facet.sln
   ```

## Repository layout

```text
Facet/
|- src/
|  |- Facet/                           # Core source generator
|  |- Facet.Attributes/                # Attribute definitions
|  |- Facet.Extensions/                # LINQ extensions
|  |- Facet.Extensions.EFCore/         # EF Core integration
|  |- Facet.Extensions.EFCore.Mapping/ # Advanced EF Core mapping
|  |- Facet.Mapping/                   # Mapping configuration interfaces
|  |- Facet.Mapping.Expressions/       # Expression transformation
|  `- Facet.Dashboard/                 # Visualization tool
`- test/
   |- Facet.Tests/                     # Main test suite
   `- Facet.Tests.ExternalLib/         # External library tests
```

## Workflow expectations

1. Create a descriptive branch from `master`.
2. Keep changes focused. Avoid mixing unrelated refactors with feature or bug-fix work.
3. Add or update tests when behavior changes.
4. Update documentation, samples, or XML docs when the user-facing experience changes.
5. Run the relevant build and test commands before opening a pull request.

## Coding guidelines

- Follow existing project conventions and file organization.
- Prefer clear, maintainable code over clever shortcuts.
- Keep public APIs deliberate and compatibility-aware.
- Reuse existing abstractions and helpers when possible instead of duplicating behavior.
- Add comments only when they clarify non-obvious intent.

## Pull requests

When opening a pull request:

1. Explain the problem, the approach you chose, and any trade-offs.
2. Link related issues or discussions.
3. Call out breaking changes or migration considerations explicitly.
4. Include screenshots, generated output, or examples when they help reviewers understand the change.

Maintainers may ask for revisions before merge, including additional tests, documentation updates, or design adjustments.

## Commit messages

- Use clear, descriptive summaries.
- Prefer imperative mood, such as `Add`, `Fix`, `Update`, or `Remove`.
- Reference issue numbers when relevant.

## Licensing

By contributing to Facet, you agree that your contributions will be licensed under the same MIT license as the project.
