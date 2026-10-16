# Agent platform registry

This operator-managed directory is the canonical skill and tool registry. It can
be moved into a separate Git checkout; configure `Registry:RootPath` in both hosts
to that checkout and mount it read-only. Brain never edits these files.

Each `skills/<id>/` contains `manifest.yaml` and `SKILL.md`. Each `tools/<id>/`
contains `manifest.yaml`. See [the registry guide](../docs/skills-and-tools.md)
for the schema, API, authorization rules, and indexing behavior.

The samples describe integrations only. They contain no credentials, connection
configuration, or executable external adapters.
