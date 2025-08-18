# 🤖 AI Assistant Quick Reference - Non-Intrusive

# ===============================================

# Simple reference card - no execution, no interference

## ✅ WINDOWS POWERSHELL COMMANDS (Use These)

Get-Content file.txt | Select-Object -First 20 # Instead of: head -n 20
Get-Content file.txt | Select-Object -Last 20 # Instead of: tail -n 20
Select-String "pattern" file.txt # Instead of: grep "pattern"
Get-ChildItem -Force # Instead of: ls -la
Sort-Object -Unique # Instead of: uniq

## ⚡ EFFICIENCY CHECKLIST (30 seconds max)

□ Check for existing bb-\* functions first
□ Use simple commands, avoid complex pipes
□ Group similar fixes together (batch operations)
□ Verify with: dotnet build --verbosity quiet

## 🎯 HIGH-IMPACT TARGETS (Fix these first)

1. CA1860: .Any() → .Count > 0 (performance)
2. CA1862: string.Contains() → .Contains(StringComparison.OrdinalIgnoreCase)
3. CA1840: Thread.CurrentThread.ManagedThreadId → Environment.CurrentManagedThreadId
4. CA1854: ContainsKey() + indexer → TryGetValue()

## 🚫 AVOID THESE PATTERNS

❌ head, tail, grep, uniq, sed, awk (Unix commands on Windows)
❌ Complex pipe chains: cmd1 | cmd2 | cmd3 | cmd4
❌ Single-file fixes (group them instead)
❌ Parsing build output with regex

## ✅ SIMPLE SUCCESS PATTERN

1. Run: dotnet build --verbosity quiet
2. Count warnings: Select-String "warning"
3. Group by type: Focus on most frequent CA rules
4. Batch fix: Apply same fix pattern across multiple files
5. Validate: dotnet build --verbosity quiet

---

This is just a reference - no functions to load or break!
