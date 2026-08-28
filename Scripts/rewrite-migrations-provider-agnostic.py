#!/usr/bin/env python3
"""One-shot rewriter: strip SQL Server-only store types from EF migration C#.

Already applied 2026-08-28. Do not re-run on converted files (identity annotations
would duplicate). Does not rewrite BusBuddyDbContextModelSnapshot.cs.
"""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "BusBuddy.Core" / "Migrations"
SKIP = {"BusBuddyDbContextModelSnapshot.cs", "MigrationSql.cs"}

# Store types that are invalid (or wrong) on PostgreSQL.
# Use a word boundary so `oldType` is not eaten as `old` + `type`.
TYPE_PATTERN = re.compile(
    r'\s*\btype:\s*"(?:bit|datetime2|nvarchar\([^"]+\)|rowversion)"\s*,?',
    re.IGNORECASE,
)
OLD_TYPE_PATTERN = re.compile(
    r'\s*\boldType:\s*"(?:bit|datetime2|nvarchar\([^"]+\)|rowversion)"\s*,?',
    re.IGNORECASE,
)
GETUTCDATE = re.compile(r'defaultValueSql:\s*"GETUTCDATE\(\)"')
FILTER_BRACKETS = re.compile(
    r'filter:\s*"\[([A-Za-z0-9_]+)\] IS NOT NULL"'
)
IDENTITY_LINE = re.compile(
    r'^(\s*)\.Annotation\("SqlServer:Identity", "1, 1"\),?\s*$',
    re.MULTILINE,
)
NPGSQL = (
    '.Annotation("Npgsql:ValueGenerationStrategy", '
    "Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),"
)


def strip_trailing_comma_glitch(text: str) -> str:
    """Remove dangling commas left when a type was the last named arg before ')'."""
    text = re.sub(r",(\s*)\)", r"\1)", text)
    text = re.sub(r",(\s*),", r",\1", text)
    return text


def add_npgsql_identity(text: str) -> str:
    if "Npgsql:ValueGenerationStrategy" in text and IDENTITY_LINE.search(text) is None:
        return text

    def inject(match: re.Match[str]) -> str:
        indent = match.group(1)
        # Skip if the following line is already the Npgsql annotation.
        return f'{indent}.Annotation("SqlServer:Identity", "1, 1")\n{indent}{NPGSQL}'

    return IDENTITY_LINE.sub(inject, text)


def transform(text: str) -> str:
    text = TYPE_PATTERN.sub("", text)
    text = OLD_TYPE_PATTERN.sub("", text)
    text = GETUTCDATE.sub("defaultValueSql: MigrationSql.UtcNow(migrationBuilder)", text)
    text = FILTER_BRACKETS.sub(
        r'filter: MigrationSql.NotNullFilter(migrationBuilder, "\1")', text
    )
    text = add_npgsql_identity(text)
    text = strip_trailing_comma_glitch(text)
    return text


def main() -> None:
    already = (ROOT / "20250804210443_InitialCreate.cs").read_text(encoding="utf-8")
    if "Npgsql:ValueGenerationStrategy" in already and "MigrationSql.BoolType" in already:
        print("Migrations already converted; refusing to re-run.")
        raise SystemExit(0)

    changed = 0
    for path in sorted(ROOT.glob("*.cs")):
        if path.name in SKIP:
            continue
        original = path.read_text(encoding="utf-8")
        updated = transform(original)
        if updated != original:
            path.write_text(updated, encoding="utf-8")
            changed += 1
            print(f"updated {path.name}")
    print(f"{changed} files rewritten")


if __name__ == "__main__":
    main()
