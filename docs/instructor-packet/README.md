# Instructor packet

Curated review set for an external technical reader. Two files are written for the packet; the rest are the canonical repo documents, referenced in place so they cannot drift from what agents and CI use.

## Reading order

| #   | Document                                                                                                     | Why it is in the packet                                                       |
| --- | ------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------- |
| 1   | [00-brief.md](./00-brief.md)                                                                                 | What BusBuddy is, who it is for, stack, size, how the clerk path works        |
| 2   | [01-status.md](./01-status.md)                                                                               | Ship-ready vs project-done, the open gaps by name, self-reported design debts |
| 3   | [specs/README.md](../../specs/README.md)                                                                     | Domain contract index and hard rules                                          |
| 4   | [specs/students.md](../../specs/students.md)                                                                 | Students: pickup mode, AM/PM, special needs, PII rule                         |
| 5   | [specs/routes.md](../../specs/routes.md)                                                                     | Daily published routes, stop order, rider exceptions                          |
| 6   | [specs/trips.md](../../specs/trips.md)                                                                       | One-off trips and why Route ≠ Trip                                            |
| 7   | [specs/maps.md](../../specs/maps.md)                                                                         | Google-only tiles, validated coordinates, deferred GPS                        |
| 8   | [docs/clerk-path.md](../clerk-path.md)                                                                       | The six hops and the duplicate-write decisions                                |
| 9   | [Documentation/diagrams/busbuddy-3-architecture.md](../../Documentation/diagrams/busbuddy-3-architecture.md) | Architecture map (Mermaid)                                                    |
| 10  | [.specify/memory/constitution.md](../../.specify/memory/constitution.md)                                     | Non-negotiables and governance                                                |

## Build the send-able bundle

```bash
docs/instructor-packet/build.sh            # → ~/Desktop/BusBuddy-Instructor-Packet/
```

Produces one Markdown file, one HTML file (Mermaid rendered), and one PDF (via headless Chrome) with the ten documents above in order. Output is written outside the repo; nothing under `dist/` is committed.

## Not included, on purpose

- `README.md` at repo root (long, mixes deprecated tooling with Quick Start)
- `.github/copilot-instructions.md` (agent tactical rules, 2,700 lines)
- `Documentation/Archive`, `Humor`, `Learning`, Trunk / hyperthreading guides
- Any student data, `docker/.env`, keys, or `keys/` files
