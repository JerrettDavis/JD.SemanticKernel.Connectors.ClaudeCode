# Contributing

Thanks for your interest in contributing to **JD.SemanticKernel.Connectors.ClaudeCode**!

## Getting Started

1. Fork and clone the repository.
2. Install the .NET 8 and .NET 10 SDKs.
3. Restore tools and build:

   ```shell
   dotnet tool restore
   dotnet build
   ```

4. Run the tests:

   ```shell
   dotnet test --filter "Category!=Live"
   ```

## Development Workflow

- **Branching** — Create a feature branch from `main` (e.g. `feat/my-change`).
- **Commits** — Use [Conventional Commits](https://www.conventionalcommits.org/)
  (`feat:`, `fix:`, `docs:`, `build:`, `test:`, etc.). A Husky.Net commit-msg hook
  validates this automatically.
- **Formatting** — Run `dotnet format` before committing. The pre-commit hook
  enforces whitespace and style rules.
- **Tests** — Add or update tests for any behaviour changes. The test suite uses
  xUnit and targets both `net8.0` and `net10.0`.

## Pull Requests

1. Ensure the build passes locally with zero warnings:

   ```shell
   dotnet build --configuration Release /p:TreatWarningsAsErrors=true
   ```

2. Ensure all tests pass:

   ```shell
   dotnet test --configuration Release --filter "Category!=Live"
   ```

3. Open a PR against `main`. The CI pipeline runs build, format check, tests,
   and code coverage automatically.

## Code Style

- Code style is enforced via `.editorconfig` and Meziantou.Analyzer.
- Use `LangVersion=latest` features where appropriate (file-scoped namespaces,
  primary constructors, collection expressions, pattern matching).
- XML documentation is required on all public types and members in `src/`.

## Versioning

Version numbers are managed by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning).
Do not manually edit version numbers — NBGV computes them from `version.json`
and git history.

## License

By contributing, you agree that your contributions will be licensed under the
[MIT License](LICENSE).
