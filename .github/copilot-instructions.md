# GitHub Copilot — DockYarp

**The full conventions live in [`AGENTS.md`](../AGENTS.md) at the repository root. Read it first.**

Key reminders:

- **.NET 10 / modern C#**: file-scoped namespaces, primary constructors, collection expressions `[...]`,
  pattern matching, `required`/`init`, `record`/`readonly struct`, `is null`.
- **Quality enforced at build time**: `TreatWarningsAsErrors=true`, `Nullable=enable`, 6 strict analyzers.
  A warning breaks the build — fix the root cause, do not disable the rule.
- **Async/await** everywhere on I/O (Docker/HTTP/ACME): no `.Result`/`.Wait()`/`async void`,
  flow `CancellationToken`, `ValueTask` on hot paths.
- **Low allocation** on the proxy path: `Span<T>`/`Memory<T>`, `ArrayPool`, explicit `StringComparison`,
  avoid LINQ/closures/boxing on the hot path, `sealed` by default.
- **Packages**: Central Package Management — versions in `Directory.Packages.props` only,
  never a `Version=` in a `.csproj`.
- **English everywhere** in committed files. Comments must be concise and justify *why* (never paraphrase
  the code). XML docs use standard tags with a **short `<summary>`**; longer explanations go in `<remarks>`.
- **Spec-driven (OpenSpec)**: implement according to the change under `openspec/changes/<id>/`
  (`proposal.md`, `specs/`, `design.md`, `tasks.md`).
