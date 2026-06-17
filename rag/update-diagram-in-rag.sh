#!/bin/bash
# Script to update the architecture diagram in RAG when it changes.
# Run this after editing the diagram or architecture.

set -e
echo "Updating BusBuddy-3 architecture diagram in RAG..."

# Ensure diagram files are in place (PNG + .mmd)
mkdir -p Documentation/diagrams
# Assume PNG copied; .mmd should be updated manually or via tool

# Re-index the RAG to include/update diagram chunks (text from .mmd + description)
python3 -m rag.index

echo "RAG updated with latest diagram."
echo "Agents: Refer to Documentation/diagrams/busbuddy-3-architecture.mmd and PNG."
echo "Update this script or CI if diagram generation becomes automated."
