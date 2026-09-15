#!/usr/bin/env bash
# Bundle the instructor packet into Markdown, HTML (Mermaid rendered), and PDF.
# Output goes outside the repo so nothing derived is committed.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
OUT="${1:-$HOME/Desktop/BusBuddy-Instructor-Packet}"
STAMP="$(date +%Y-%m-%d)"
CHROME="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"

mkdir -p "$OUT"
MD="$OUT/BusBuddy-Instructor-Packet.md"
HTML="$OUT/BusBuddy-Instructor-Packet.html"
PDF="$OUT/BusBuddy-Instructor-Packet.pdf"

FILES=(
  "docs/instructor-packet/00-brief.md"
  "docs/instructor-packet/01-status.md"
  "specs/README.md"
  "specs/students.md"
  "specs/routes.md"
  "specs/trips.md"
  "specs/maps.md"
  "docs/clerk-path.md"
  "Documentation/diagrams/busbuddy-3-architecture.md"
  ".specify/memory/constitution.md"
)

{
  echo "# BusBuddy-3 — Documentation packet"
  echo
  echo "_Assembled ${STAMP} from \`master\`. Source files are listed above each section._"
  echo
  for f in "${FILES[@]}"; do
    echo
    echo "---"
    echo
    echo "<!-- source: $f -->"
    echo
    # Strip HTML comment blocks (constitution sync report) and demote headings one level.
    python3 - "$REPO/$f" <<'PY'
import re, sys
text = open(sys.argv[1], encoding="utf-8").read()
text = re.sub(r"<!--.*?-->\s*", "", text, flags=re.S)
lines = []
for line in text.splitlines():
    if re.match(r"^#{1,5} ", line):
        line = "#" + line
    lines.append(line)
print("\n".join(lines))
PY
  done
} > "$MD"

# Markdown -> HTML via marked (npx), Mermaid via CDN so headless Chrome renders diagrams.
if command -v npx >/dev/null 2>&1; then
  BODY_FILE="$(mktemp)"
  npx --yes marked@12 --gfm < "$MD" > "$BODY_FILE"
  {
  cat <<'HTML'
<!doctype html>
<html><head><meta charset="utf-8"><title>BusBuddy-3 Documentation packet</title>
<style>
 body{font:14px/1.5 -apple-system,Segoe UI,Helvetica,Arial,sans-serif;max-width:860px;margin:32px auto;padding:0 24px;color:#111}
 h1{font-size:26px;border-bottom:2px solid #ddd;padding-bottom:6px;page-break-before:always}
 h1:first-of-type{page-break-before:avoid}
 h2{font-size:20px;margin-top:28px} h3{font-size:16px}
 table{border-collapse:collapse;width:100%;margin:12px 0;font-size:13px}
 th,td{border:1px solid #ccc;padding:5px 8px;text-align:left;vertical-align:top}
 th{background:#f3f3f3}
 code{font:12px Menlo,Consolas,monospace;background:#f5f5f5;padding:1px 4px;border-radius:3px}
 pre{background:#f5f5f5;padding:10px;overflow:auto;font-size:12px}
 pre code{background:none;padding:0}
 .mermaid{background:#fff;margin:12px 0}
 hr{border:0;border-top:1px solid #ddd;margin:32px 0}
 a{color:#0b5cad}
 @media print{ a{color:#111;text-decoration:none} }
</style>
<script type="module">
 import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs';
 document.querySelectorAll('pre code.language-mermaid').forEach(c=>{
   const d=document.createElement('div'); d.className='mermaid'; d.textContent=c.textContent; c.parentElement.replaceWith(d);
 });
 await mermaid.run({querySelector:'.mermaid'});
 window.__mermaidDone = true;
</script>
</head><body>
HTML
  cat "$BODY_FILE"
  echo "</body></html>"
  } > "$HTML"
  rm -f "$BODY_FILE"
  echo "HTML: $HTML"
else
  echo "npx not found; skipping HTML/PDF" >&2
  exit 0
fi

if [[ -x "$CHROME" ]]; then
  "$CHROME" --headless=new --disable-gpu --no-pdf-header-footer \
    --virtual-time-budget=15000 \
    --print-to-pdf="$PDF" "file://$HTML" >/dev/null 2>&1 || echo "Chrome PDF step failed" >&2
  [[ -f "$PDF" ]] && echo "PDF:  $PDF"
else
  echo "Chrome not found; HTML only" >&2
fi

echo "MD:   $MD"
