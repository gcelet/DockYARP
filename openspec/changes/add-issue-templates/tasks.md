## 1. Issue templates (AG-DEP)

- [x] 1.1 Add `.github/ISSUE_TEMPLATE/bug_report.yml`: what happened, expected behavior, DockYarp version,
      relevant labels/env vars, logs; `labels: [bug]`.
- [x] 1.2 Add `.github/ISSUE_TEMPLATE/feature_request.yml`: problem/motivation, proposed behavior, a dropdown
      for nginx-proxy parity vs. DockYarp-specific; `labels: [enhancement]`.
- [x] 1.3 Add `.github/ISSUE_TEMPLATE/question.yml`: free-form question field, with a note pointing at the docs
      site and `openspec/backlog/parity.md`; `labels: [question]`.
- [x] 1.4 Add `.github/ISSUE_TEMPLATE/config.yml` with `blank_issues_enabled: false`.

## 2. Validation (AG-DEP)

- [x] 2.1 Parsed all four YAML files with `yaml.safe_load` via `uvx --with pyyaml` — all valid, structure
      (`name`/`description`/`title`/`labels`/`body`) matches GitHub's Issue Forms schema. Cross-checked the docs
      site URL in `question.yml` against `README.md`/`hugo.toml` — matches.
- [x] 2.2 Run `npx @fission-ai/openspec@latest validate add-issue-templates --strict`.
