#!/usr/bin/env bash
# Inventory Syncfusion control types in a WPF XAML file.
# Usage: .cursor/skills/wpf-page-audit/scripts/inventory-syncfusion.sh path/to/View.xaml
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <path-to-xaml>" >&2
  exit 1
fi

XAML="$1"
if [[ ! -f "$XAML" ]]; then
  echo "File not found: $XAML" >&2
  exit 1
fi

echo "# Syncfusion controls in: $XAML"
echo
echo "## Control types (unique)"
rg -o 'syncfusion:[A-Za-z0-9]+' "$XAML" | sort -u | sed 's/^/- /'
echo
echo "## Named instances"
rg -n 'x:Name="[^"]+".*syncfusion:|syncfusion:[A-Za-z0-9]+[^>]*x:Name=' "$XAML" || true
echo
echo "## Bindings on syncfusion elements (sample)"
rg -n 'Binding |Command=' "$XAML" | head -40 || true
