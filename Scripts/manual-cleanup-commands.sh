#!/bin/bash
# Manual GitHub CLI commands for workflow cleanup
# Run these commands one by one to clean up workflow runs

REPO="Bigessfour/BusBuddy-3"

echo "🚌 BusBuddy Manual Workflow Cleanup Commands"
echo "============================================="
echo ""

echo "📊 First, let's see what we have:"
echo ""
echo "# List all workflow runs (first 10)"
echo "gh run list --repo $REPO --limit 10"
echo ""

echo "# Count failed runs"
echo "gh run list --repo $REPO --status failure --limit 1000 --json id | jq length"
echo ""

echo "# Count total runs"
echo "gh run list --repo $REPO --limit 1000 --json id | jq length"
echo ""

echo "🎯 Cleanup commands:"
echo ""

echo "# 1. Delete all failed runs (one by one - safe but slow)"
echo "gh run list --repo $REPO --status failure --json id --jq '.[].id' | xargs -I {} gh run delete {} --repo $REPO --confirm"
echo ""

echo "# 2. Delete runs older than 30 days (requires date calculation)"
echo "# First get runs with dates:"
echo "gh run list --repo $REPO --limit 1000 --json id,createdAt | jq -r '.[] | select((.createdAt | fromdateiso8601) < (now - 2592000)) | .id'"
echo ""

echo "# Then delete them:"
echo "gh run list --repo $REPO --limit 1000 --json id,createdAt | jq -r '.[] | select((.createdAt | fromdateiso8601) < (now - 2592000)) | .id' | xargs -I {} gh run delete {} --repo $REPO --confirm"
echo ""

echo "# 3. Delete specific workflow runs (replace WORKFLOW_ID)"
echo "gh run list --repo $REPO --workflow 179562737 --status failure --json id --jq '.[].id' | xargs -I {} gh run delete {} --repo $REPO --confirm"
echo ""

echo "# 4. Nuclear option - delete ALL runs (DANGEROUS!)"
echo "gh run list --repo $REPO --limit 1000 --json id --jq '.[].id' | xargs -I {} gh run delete {} --repo $REPO --confirm"
echo ""

echo "⚠️  WARNING: These commands will actually delete workflow runs!"
echo "💡 TIP: Use the PowerShell script with -WhatIf for safer preview mode"
echo ""
echo "To execute these commands:"
echo "1. Copy the command you want to run"
echo "2. Paste it into your terminal"
echo "3. Press Enter to execute"
echo ""
echo "Prerequisites:"
echo "- GitHub CLI installed (gh --version)"
echo "- Authenticated (gh auth status)"
echo "- jq installed for JSON processing"