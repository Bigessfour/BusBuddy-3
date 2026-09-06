#!/usr/bin/env python3
"""Emit a View binding inventory from WPF XAML for vertical page audits."""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

BINDING_RE = re.compile(
    r"\{Binding\s+(?P<path>[^,}\s]+)(?:\s*,\s*Mode\s*=\s*(?P<mode>\w+))?",
    re.IGNORECASE,
)
SYNC_CONTROL_RE = re.compile(
    r"<(?:syncfusion|syncfusiontools):(?P<name>\w+)",
    re.IGNORECASE,
)
CLR_CONTROL_RE = re.compile(
    r"<(?P<ns>\w+):(?P<name>\w+)",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Audit WPF XAML bindings and Syncfusion controls.")
    parser.add_argument("xaml", type=Path, help="Path to .xaml file")
    parser.add_argument(
        "--viewmodel",
        type=Path,
        default=None,
        help="Optional ViewModel .cs to cross-check property names",
    )
    return parser.parse_args()


def load_vm_properties(vm_path: Path | None) -> set[str]:
    if vm_path is None or not vm_path.is_file():
        return set()
    props: set[str] = set()
    prop_re = re.compile(r"public\s+[\w<>,\?\[\]\s]+\s+(?P<name>\w+)\s*\{")
    for line in vm_path.read_text(encoding="utf-8", errors="replace").splitlines():
        match = prop_re.search(line)
        if match and not line.strip().startswith("//"):
            props.add(match.group("name"))
    return props


def audit_xaml(xaml_path: Path, vm_props: set[str]) -> int:
    text = xaml_path.read_text(encoding="utf-8", errors="replace")
    bindings = list(BINDING_RE.finditer(text))
    sync_controls = sorted(set(SYNC_CONTROL_RE.findall(text)))
    clr_controls = sorted(
        {f"{m.group('ns')}:{m.group('name')}" for m in CLR_CONTROL_RE.finditer(text)}
    )

    print(f"# Binding audit: {xaml_path}")
    print()
    print("## Syncfusion controls")
    for name in sync_controls:
        print(f"- {name}")
    print()
    print("## CLR / tools controls (sample)")
    for name in clr_controls[:20]:
        print(f"- {name}")
    if len(clr_controls) > 20:
        print(f"- ... and {len(clr_controls) - 20} more")
    print()
    print("## Bindings")
    print("| Path | Mode | VM property exists |")
    print("|------|------|--------------------|")
    seen: set[str] = set()
    missing_vm: list[str] = []
    for match in bindings:
        path = match.group("path")
        if path in seen:
            continue
        seen.add(path)
        mode = match.group("mode") or "Default"
        root = path.split(".")[0].strip("()")
        exists = "—" if not vm_props else ("yes" if root in vm_props else "**no**")
        if vm_props and root not in vm_props and not path.startswith("("):
            missing_vm.append(path)
        print(f"| `{path}` | {mode} | {exists} |")

    if missing_vm:
        print()
        print("## Possible orphan bindings (root not on ViewModel)")
        for path in missing_vm:
            print(f"- `{path}`")

    print()
    print(f"**Total distinct binding paths:** {len(seen)}")
    print(f"**Syncfusion control types:** {len(sync_controls)}")
    return 0


def main() -> int:
    args = parse_args()
    if not args.xaml.is_file():
        print(f"ERROR: not found: {args.xaml}", file=sys.stderr)
        return 1
    vm_props = load_vm_properties(args.viewmodel)
    return audit_xaml(args.xaml, vm_props)


if __name__ == "__main__":
    raise SystemExit(main())
