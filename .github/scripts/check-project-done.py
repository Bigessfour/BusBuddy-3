#!/usr/bin/env python3
"""Deterministic project-done checker for BusBuddy-3.

Exit 0 only when every must-pass check is pass, deferred, or N/A.
See docs/done-checklist.md and docs/done-catalog.json.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Iterable

ROOT = Path(__file__).resolve().parents[2]
CATALOG_PATH = ROOT / "docs" / "done-catalog.json"

STUB_RE = re.compile(
    r"throw new NotImplementedException|NotImplementedException\(",
    re.MULTILINE,
)
TODO_RE = re.compile(r"\b(TODO|FIXME|HACK)\b")
CLICK_XAML_RE = re.compile(r'\bClick="(\w+)"')
CLICK_HANDLER_RE = re.compile(
    r"(?:private|protected|internal|public)\s+(?:async\s+)?void\s+(\w+_Click)\s*\("
)
DBSET_RE = re.compile(r"DbSet<(\w+)>")
INTERFACE_RE = re.compile(r"\binterface\s+(I\w+Service)\b")
XCLASS_RE = re.compile(r'x:Class="([^"]+)"')
SECRET_RE = re.compile(
    r"(AIza[0-9A-Za-z_-]{20,}|sk-[A-Za-z0-9]{20,}|-----BEGIN (?:RSA |OPENSSH )?PRIVATE KEY-----)"
)
ACTION_BOX_RE = re.compile(r"^- \[( |x|X)\]\s+(.*)$")


@dataclass
class Finding:
    check: str
    status: str  # pass | fail | deferred | na
    message: str
    evidence: str = ""


@dataclass
class Report:
    findings: list[Finding] = field(default_factory=list)

    def add(self, check: str, status: str, message: str, evidence: str = "") -> None:
        self.findings.append(Finding(check, status, message, evidence))

    def failed(self) -> list[Finding]:
        return [f for f in self.findings if f.status == "fail"]


def load_catalog() -> dict:
    return json.loads(CATALOG_PATH.read_text(encoding="utf-8"))


def skip_dir(path: Path, catalog: dict) -> bool:
    rel = path.relative_to(ROOT).as_posix() if path.is_relative_to(ROOT) else str(path)
    for part in catalog.get("skip_dirs", []):
        if rel == part or rel.startswith(part.rstrip("/") + "/"):
            return True
    return any(p in {"bin", "obj", ".git"} for p in path.parts)


def iter_files(
    catalog: dict, suffixes: tuple[str, ...], roots: Iterable[str] | None = None
) -> Iterable[Path]:
    bases = [ROOT / r for r in roots] if roots else [ROOT]
    for base in bases:
        if not base.exists():
            continue
        for path in base.rglob("*"):
            if not path.is_file() or path.suffix not in suffixes:
                continue
            if skip_dir(path.parent, catalog):
                continue
            yield path


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def rel(path: Path) -> str:
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def deferred_symbol(catalog: dict, symbol: str) -> str | None:
    for item in catalog.get("deferrals", []):
        if symbol in item.get("symbols", []):
            return item["id"]
    return None


def parse_action_items() -> list[tuple[bool, str]]:
    path = ROOT / "docs" / "action-items.md"
    rows: list[tuple[bool, str]] = []
    for line in read(path).splitlines():
        match = ACTION_BOX_RE.match(line.strip())
        if match:
            rows.append((match.group(1).lower() == "x", match.group(2).strip()))
    return rows


def box_done(rows: list[tuple[bool, str]], marker: str) -> bool | None:
    """True if a matching box is checked, False if unchecked, None if missing."""
    hits = [(done, text) for done, text in rows if marker in text]
    if not hits:
        return None
    return all(done for done, _ in hits)


def check_required_files(report: Report, catalog: dict) -> None:
    missing = []
    for name in catalog.get("required_files", []):
        if not (ROOT / name).is_file():
            missing.append(name)
    if missing:
        report.add(
            "K01_works",
            "fail",
            "Required launch/docs files missing",
            ", ".join(missing),
        )
    else:
        report.add("K01_works", "pass", "Required launch and docs files exist")


def check_readme(report: Report) -> None:
    text = read(ROOT / "README.md")
    if "run-wpf.sh" in text and "GOOGLE_MAPS_API_KEY" in text:
        report.add("K04_readme", "pass", "README has hybrid launch and Maps env")
    else:
        report.add(
            "K04_readme",
            "fail",
            "README must document ./run-wpf.sh and GOOGLE_MAPS_API_KEY",
            "README.md",
        )


def check_observability(report: Report) -> None:
    path = ROOT / "BusBuddy.WPF" / "Logging" / "RuntimeCapabilityLogger.cs"
    if path.is_file() and "GOOGLE_MAPS_API_KEY" in read(path):
        report.add(
            "K05_observability",
            "pass",
            "Serilog capability logger present (analytics N/A)",
        )
    else:
        report.add(
            "K05_observability",
            "fail",
            "RuntimeCapabilityLogger missing or incomplete",
            rel(path),
        )


def check_na(report: Report, catalog: dict) -> None:
    for item in catalog.get("karlsson_na", []):
        report.add(item["id"], "na", item["reason"])


def check_ci(report: Report) -> None:
    path = ROOT / ".github" / "workflows" / "ci.yml"
    text = read(path)
    if "dotnet test" in text and "Build & Test" in text:
        report.add("K08_ci", "pass", "CI Build & Test workflow present")
    else:
        report.add(
            "K08_ci", "fail", "ci.yml missing Build & Test / dotnet test", rel(path)
        )
    if "EnableWindowsTargeting" in text:
        report.add("K12_release", "pass", "CI builds Windows-targeted Release")
    else:
        report.add(
            "K12_release", "fail", "CI does not set EnableWindowsTargeting", rel(path)
        )


def check_e2e_harness(report: Report) -> None:
    harness = ROOT / "Scripts" / "utm-wpf-test.sh"
    tests = ROOT / "BusBuddy.Tests"
    if harness.is_file() and tests.is_dir():
        report.add("K03_e2e", "pass", "UTM WPF test harness and BusBuddy.Tests exist")
    else:
        report.add("K03_e2e", "fail", "Missing utm-wpf-test.sh or BusBuddy.Tests")


def check_inventory_proof(report: Report) -> None:
    inv = json.loads((ROOT / ".function-inventory.json").read_text(encoding="utf-8"))
    generated = ROOT / "docs" / "function-inventory.generated.md"
    gen_text = read(generated) if generated.is_file() else ""
    test_paths = list((ROOT / "BusBuddy.Tests").rglob("*Tests.cs"))
    test_blobs = [read(p) for p in test_paths]
    missing_proof = []
    stale = []
    for surface in inv.get("surfaces", []):
        stem = (
            Path(surface)
            .name.replace(".xaml.cs", "")
            .replace(".cs", "")
            .replace(".xaml", "")
        )
        names = {stem}
        if stem.endswith("ViewModel"):
            names.add(stem[: -len("ViewModel")] + "View")
        mentioned = any(
            any(ident_mentioned(blob, n) for n in names) for blob in test_blobs
        )
        if not mentioned:
            missing_proof.append(surface)
        if generated.is_file() and Path(surface).name.split(".")[0] not in gen_text:
            stale.append(surface)
    if missing_proof:
        report.add(
            "K02_tests",
            "fail",
            "Inventory surfaces are never mentioned in *Tests.cs",
            "; ".join(missing_proof[:12]),
        )
    else:
        report.add(
            "K02_tests",
            "pass",
            "Every inventory surface is mentioned in at least one test",
        )
    if not generated.is_file():
        report.add(
            "B12_inventory", "fail", "docs/function-inventory.generated.md missing"
        )
    elif stale:
        report.add(
            "B12_inventory",
            "fail",
            "Generated inventory is stale vs .function-inventory.json — re-run update-function-inventory.py",
            f"{len(stale)} surfaces missing from generated file",
        )
    else:
        report.add(
            "B12_inventory", "pass", "Generated inventory lists configured surfaces"
        )


def check_stubs_and_todos(report: Report, catalog: dict) -> None:
    stub_hits: list[str] = []
    todo_hits: list[str] = []
    for path in iter_files(catalog, (".cs",), ("BusBuddy.Core", "BusBuddy.WPF")):
        text = read(path)
        for i, line in enumerate(text.splitlines(), 1):
            if STUB_RE.search(line):
                stub_hits.append(f"{rel(path)}:{i}: {line.strip()}")
            if TODO_RE.search(line) and not line.strip().startswith(
                "// TODO: Re-implement"
            ):
                # Keep noisy BaseEntity TODOs as findings — they are incomplete work.
                todo_hits.append(f"{rel(path)}:{i}: {line.strip()}")
    if stub_hits:
        report.add(
            "B01_stubs",
            "fail",
            "NotImplementedException in product code",
            "; ".join(stub_hits[:8]),
        )
    else:
        report.add("B01_stubs", "pass", "No NotImplementedException in Core/WPF")
    if todo_hits:
        report.add(
            "B02_todos",
            "fail",
            "TODO/FIXME/HACK in product code",
            "; ".join(todo_hits[:12]),
        )
    else:
        report.add("B02_todos", "pass", "No TODO/FIXME/HACK in Core/WPF")


def ident_mentioned(text: str, name: str) -> bool:
    """True if `name` appears as a whole identifier (not a prefix of NameModel)."""
    for match in re.finditer(re.escape(name), text):
        after = text[match.end() : match.end() + 1]
        if after.isalpha() or after == "_":
            continue
        return True
    return False


def view_class_name(xaml: Path) -> str | None:
    match = XCLASS_RE.search(read(xaml))
    if not match:
        return None
    return match.group(1).split(".")[-1]


def check_orphan_views(report: Report, catalog: dict) -> None:
    views = list((ROOT / "BusBuddy.WPF").rglob("*View.xaml")) + list(
        (ROOT / "BusBuddy.WPF").rglob("*Form.xaml")
    )
    product_files = list(iter_files(catalog, (".cs", ".xaml"), ("BusBuddy.WPF",)))
    blobs = {p: read(p) for p in product_files}
    orphans: list[str] = []
    for view in views:
        if skip_dir(view.parent, catalog):
            continue
        name = view_class_name(view)
        if not name:
            continue
        referenced = False
        own = {view, view.with_suffix(".xaml.cs")}
        for path, text in blobs.items():
            if path in own or path.name == view.name:
                continue
            if ident_mentioned(text, name):
                referenced = True
                break
        if not referenced:
            did = deferred_symbol(catalog, name)
            if did:
                report.add(
                    "B03_orphan_views",
                    "deferred",
                    f"{name} unused but deferred",
                    f"{rel(view)} ({did})",
                )
            else:
                orphans.append(rel(view))
    if orphans:
        report.add(
            "B03_orphan_views",
            "fail",
            "Views/forms never constructed or referenced",
            "; ".join(orphans),
        )
    else:
        report.add(
            "B03_orphan_views",
            "pass",
            "All views/forms are referenced outside themselves",
        )


def check_orphan_handlers(report: Report, catalog: dict) -> None:
    xaml_clicks: set[str] = set()
    for path in iter_files(catalog, (".xaml",), ("BusBuddy.WPF",)):
        xaml_clicks.update(CLICK_XAML_RE.findall(read(path)))
    orphans: list[str] = []
    for path in iter_files(catalog, (".cs",), ("BusBuddy.WPF",)):
        if not path.name.endswith(".xaml.cs"):
            continue
        for i, line in enumerate(read(path).splitlines(), 1):
            match = CLICK_HANDLER_RE.search(line)
            if match and match.group(1) not in xaml_clicks:
                orphans.append(f"{rel(path)}:{i} {match.group(1)}")
    if orphans:
        report.add(
            "B04_orphan_handlers",
            "fail",
            "Click handlers with no XAML Click= wiring",
            "; ".join(orphans),
        )
    else:
        report.add(
            "B04_orphan_handlers",
            "pass",
            "Every *_Click handler is referenced from XAML",
        )


def check_di(report: Report, catalog: dict) -> None:
    di_blobs = ""
    for path in [
        ROOT / "BusBuddy.WPF" / "App.xaml.cs",
        ROOT / "BusBuddy.Core" / "Extensions" / "ServiceCollectionExtensions.cs",
    ]:
        if path.is_file():
            di_blobs += read(path)
    missing: list[str] = []
    deferred: list[str] = []
    for path in iter_files(catalog, (".cs",), ("BusBuddy.Core/Services",)):
        for iface in INTERFACE_RE.findall(read(path)):
            if iface in di_blobs:
                continue
            did = deferred_symbol(catalog, iface)
            if did:
                deferred.append(f"{iface} ({did})")
            else:
                missing.append(f"{iface} ({rel(path)})")
    if missing:
        report.add(
            "B05_di_participation",
            "fail",
            "I*Service not registered in DI",
            "; ".join(missing),
        )
    else:
        report.add(
            "B05_di_participation",
            "pass",
            "Core I*Service types are registered or deferred",
        )
    for item in deferred:
        report.add("B05_di_participation", "deferred", item)


def check_dbsets(report: Report, catalog: dict) -> None:
    context = ROOT / "BusBuddy.Core" / "Data" / "BusBuddyDbContext.cs"
    text = read(context)
    unexplained: list[str] = []
    deferred: list[str] = []
    for name in DBSET_RE.findall(text):
        if name in {"Fuel", "Maintenance", "ActivitySchedule"}:
            continue  # alias properties
        did = deferred_symbol(catalog, name)
        if did:
            deferred.append(f"{name} ({did})")
            continue
        # Participating if a service/view mentions the set or type name.
        mentioned = False
        for path in iter_files(
            catalog, (".cs",), ("BusBuddy.Core/Services", "BusBuddy.WPF")
        ):
            if name in read(path) and path != context:
                mentioned = True
                break
        if not mentioned:
            unexplained.append(name)
    if unexplained:
        report.add(
            "B06_dead_dbsets",
            "fail",
            "DbSets with no service/UI mention",
            ", ".join(unexplained),
        )
    else:
        report.add("B06_dead_dbsets", "pass", "DbSets are used or explicitly deferred")
    for item in deferred:
        report.add("B06_dead_dbsets", "deferred", item)


def check_forbidden(report: Report, catalog: dict) -> None:
    failed = False
    for rule in catalog.get("forbidden_in_product", []):
        pattern = re.compile(rule["pattern"])
        hits: list[str] = []
        for root in rule.get("roots", []):
            target = ROOT / root
            if target.is_file():
                files = [target]
            else:
                files = [p for p in iter_files(catalog, (".cs", ".xaml"), (root,))]
            for path in files:
                for i, line in enumerate(read(path).splitlines(), 1):
                    stripped = line.strip()
                    if re.search(
                        r"\bno\b.{0,40}(OSM|OpenStreetMap)|\bnot\b.{0,48}OpenStreetMap",
                        stripped,
                        re.I,
                    ):
                        continue
                    if pattern.search(line):
                        hits.append(f"{rel(path)}:{i}")
        check_id = {
            "route-is-trip": "B07_spec_invariants",
            "osm-layer": "B07_spec_invariants",
            "osm-symbol": "K11_perf",
            "wpf-datagrid": "B09_syncfusion_only",
            "sample-demo": "B15_sample_data",
        }.get(rule["id"], "B07_spec_invariants")
        if hits:
            failed = True
            report.add(check_id, "fail", rule["reason"], "; ".join(hits[:8]))
        else:
            report.add(check_id, "pass", rule["reason"])
    live = ROOT / "BusBuddy.WPF" / "ViewModels" / "Map" / "MapViewModel.cs"
    if live.is_file() and "IsLiveTrackingEnabled" in read(live):
        report.add(
            "B07_spec_invariants",
            "fail",
            "IsLiveTrackingEnabled appears on MapViewModel (live GPS is deferred)",
            rel(live),
        )
    ux_tests = (
        list((ROOT / "BusBuddy.XamlCompliance.Tests").glob("*.cs"))
        if (ROOT / "BusBuddy.XamlCompliance.Tests").exists()
        else []
    )
    wpf_theme = list((ROOT / "BusBuddy.Tests").rglob("*Theme*Tests.cs"))
    if ux_tests or wpf_theme:
        report.add("K07_ux", "pass", "XAML/theme compliance tests exist")
    else:
        report.add("K07_ux", "fail", "No XAML compliance or theme tests found")


def check_secrets(report: Report, catalog: dict) -> None:
    hits: list[str] = []
    for path in iter_files(catalog, (".cs", ".json", ".xml", ".config", ".env"), None):
        if path.name in {"done-catalog.json"}:
            continue
        text = read(path)
        if SECRET_RE.search(text):
            hits.append(rel(path))
    if hits:
        report.add(
            "B08_secrets",
            "fail",
            "Possible secret material in tracked files",
            ", ".join(hits[:8]),
        )
    else:
        report.add(
            "B08_secrets", "pass", "No obvious API key / PEM material in product files"
        )


def check_docs_drift(report: Report) -> None:
    constitution = read(ROOT / ".specify" / "memory" / "constitution.md")
    agents = read(ROOT / "AGENTS.md")
    agents_forbids = "Do not re-add it" in agents or "No CodeQL gate" in agents
    if "CodeQL" in constitution and agents_forbids:
        report.add(
            "B11_docs_drift",
            "fail",
            "Constitution still requires CodeQL; AGENTS.md forbids re-adding it on a private repo",
            ".specify/memory/constitution.md § VI vs AGENTS.md CI/CD",
        )
    else:
        report.add(
            "B11_docs_drift", "pass", "Constitution and AGENTS merge-gate text agree"
        )


def check_hops_and_ui(report: Report, catalog: dict) -> None:
    rows = parse_action_items()
    core_fail: list[str] = []
    ui_fail: list[str] = []
    wiring_fail: list[str] = []
    for hop in catalog.get("clerk_hops", []):
        view = ROOT / "BusBuddy.WPF" / hop["view"]
        if not view.is_file():
            wiring_fail.append(f"{hop['id']}: missing {hop['view']}")
            continue
        text = read(view)
        missing = [token for token in hop.get("must_contain", []) if token not in text]
        if missing:
            wiring_fail.append(f"{hop['id']}: {rel(view)} missing {missing}")
        core = box_done(rows, hop["core_marker"])
        if core is False:
            core_fail.append(hop["id"])
        elif core is None and hop["id"] != "hop6-fuel-maintenance":
            # hop 6 core is checked as 'Hop 6 — Fuel' historically
            pass
        ui_markers = hop.get("ui_evidence_markers", [])
        if hop["id"] != "hop6-fuel-maintenance":
            for marker in ui_markers:
                state = box_done(rows, marker)
                if state is False:
                    ui_fail.append(f"{hop['id']} ({marker})")
                elif state is None:
                    ui_fail.append(
                        f"{hop['id']}: no action-items box matching {marker!r}"
                    )
    extra_fail: list[str] = []
    extra_pass: list[str] = []
    for item in catalog.get("ui_ops_extra", []):
        states = [box_done(rows, m) for m in item["markers"]]
        if any(s is False for s in states):
            extra_fail.append(item["title"])
        elif any(s is True for s in states):
            extra_pass.append(item["title"])
        else:
            extra_fail.append(f"{item['title']} (no matching action-items box)")

    if core_fail:
        report.add(
            "K01_works",
            "fail",
            "Clerk Core hops not checked in action-items",
            ", ".join(core_fail),
        )
    else:
        report.add("K01_works", "pass", "Clerk hops 1–6 Core boxes are checked")

    if wiring_fail:
        report.add(
            "B13_ui_wiring",
            "fail",
            "Clerk hop views missing required wiring tokens",
            "; ".join(wiring_fail),
        )
    else:
        report.add(
            "B13_ui_wiring",
            "pass",
            "Clerk hop views contain required command/click tokens",
        )

    if ui_fail or extra_fail:
        report.add(
            "B14_ui_operational_evidence",
            "fail",
            "UI operational proof still open in action-items",
            "; ".join(ui_fail + extra_fail),
        )
    else:
        report.add(
            "B14_ui_operational_evidence",
            "pass",
            "Hop UI evidence and map/settings smoke are checked",
        )

    if extra_fail and "District Map VM re-smoke" in extra_fail:
        report.add(
            "K09_manual_qa",
            "fail",
            "Manual VM map re-smoke is unchecked",
            "docs/action-items.md",
        )
    elif extra_pass or not extra_fail:
        report.add(
            "K09_manual_qa", "pass", "Manual VM smoke boxes that exist are checked"
        )


def check_action_item_boxes(report: Report, catalog: dict) -> None:
    rows = parse_action_items()
    prefixes = catalog.get("action_item_nonblocking", [])
    blocking: list[str] = []
    for done, text in rows:
        if done:
            continue
        if any(text.startswith(p) or p in text for p in prefixes):
            report.add("B10_action_items_blocking", "deferred", text)
            continue
        blocking.append(text)
    if blocking:
        report.add(
            "B10_action_items_blocking",
            "fail",
            "Unchecked action-items are still project-done blockers",
            "; ".join(blocking),
        )
    else:
        report.add(
            "B10_action_items_blocking", "pass", "No blocking unchecked action-items"
        )


def check_duplicate_writes(report: Report, catalog: dict) -> None:
    clerk = read(ROOT / "docs" / "clerk-path.md")
    if "AMVehicleId" in clerk and "AMDriverId" in clerk:
        report.add(
            "B16_duplicate_writes", "pass", "Hop 4 canonical write is Route.AM*/PM*"
        )
        report.add(
            "B16_duplicate_writes",
            "deferred",
            "RouteAssignments table retained unused by Assign Vehicle/Driver",
            "route-assignments-table",
        )
    else:
        report.add(
            "B16_duplicate_writes",
            "fail",
            "clerk-path.md no longer states Route.AM* as canonical",
        )


def print_markdown(report: Report) -> None:
    failed = report.failed()
    deferred = [f for f in report.findings if f.status == "deferred"]
    passed = [f for f in report.findings if f.status == "pass"]
    na = [f for f in report.findings if f.status == "na"]
    verdict = "DONE" if not failed else "NOT DONE"
    print(f"# BusBuddy project-done report")
    print()
    print(f"**Verdict:** {verdict}")
    print(
        f"**Failed:** {len(failed)} · **Deferred:** {len(deferred)} · "
        f"**Passed:** {len(passed)} · **N/A:** {len(na)}"
    )
    print()
    if failed:
        print("## Failures (project is not done)")
        for f in failed:
            ev = f" — `{f.evidence}`" if f.evidence else ""
            print(f"- **{f.check}**: {f.message}{ev}")
        print()
    if deferred:
        print("## Deferred (explained, allowed)")
        for f in deferred:
            ev = f" — `{f.evidence}`" if f.evidence else ""
            print(f"- **{f.check}**: {f.message}{ev}")
        print()
    print("## Passed / N/A")
    for f in passed + na:
        print(f"- **{f.check}** ({f.status}): {f.message}")
    print()
    print(
        "Checklist: `docs/done-checklist.md`. Re-run: `python3 .github/scripts/check-project-done.py`"
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="BusBuddy project-done checker")
    parser.add_argument(
        "--json", action="store_true", help="Emit JSON instead of markdown"
    )
    parser.add_argument(
        "--with-build",
        action="store_true",
        help="Reserved: local compile (not required)",
    )
    parser.add_argument(
        "--with-utm",
        action="store_true",
        help="Reserved: guest WPF tests (not required)",
    )
    args = parser.parse_args()
    if not CATALOG_PATH.is_file():
        print(f"ERROR: missing {CATALOG_PATH}", file=sys.stderr)
        return 2
    catalog = load_catalog()
    report = Report()
    check_required_files(report, catalog)
    check_readme(report)
    check_observability(report)
    check_na(report, catalog)
    check_ci(report)
    check_e2e_harness(report)
    check_inventory_proof(report)
    check_stubs_and_todos(report, catalog)
    check_orphan_views(report, catalog)
    check_orphan_handlers(report, catalog)
    check_di(report, catalog)
    check_dbsets(report, catalog)
    check_forbidden(report, catalog)
    check_secrets(report, catalog)
    check_docs_drift(report)
    check_hops_and_ui(report, catalog)
    check_action_item_boxes(report, catalog)
    check_duplicate_writes(report, catalog)
    if args.with_build or args.with_utm:
        report.add(
            "K03_e2e",
            "na",
            "--with-build/--with-utm are recorded as requested; run validate-ci-local.sh / utm-wpf-test.sh separately",
        )
    if args.json:
        print(json.dumps({"findings": [asdict(f) for f in report.findings]}, indent=2))
    else:
        print_markdown(report)
    return 0 if not report.failed() else 1


if __name__ == "__main__":
    raise SystemExit(main())
