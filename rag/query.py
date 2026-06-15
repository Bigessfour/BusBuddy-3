#!/usr/bin/env python3
"""
Simple local query tool for BusBuddy RAG.

Usage:
  python -m rag.query "how does the Postgres DbContext and SeedDataService work together?"
  python -m rag.query "Grok route optimization integration" --top-k 6

Useful for quick manual checks. The MCP server provides the same capability to agents.
"""

import argparse
from pathlib import Path

import chromadb
from sentence_transformers import SentenceTransformer

DB_PATH = Path(__file__).parent / "chroma_db"
COLLECTION_NAME = "busbuddy_codebase"
EMBED_MODEL = "all-MiniLM-L6-v2"

def main():
    parser = argparse.ArgumentParser(description="Query the BusBuddy RAG index")
    parser.add_argument("query", help="Natural language query for codebase context")
    parser.add_argument("--top-k", type=int, default=8, help="Number of results")
    args = parser.parse_args()

    print(f"Loading model + collection from {DB_PATH}...")
    model = SentenceTransformer(EMBED_MODEL)
    client = chromadb.PersistentClient(path=str(DB_PATH))
    collection = client.get_collection(COLLECTION_NAME)

    q_emb = model.encode([args.query]).tolist()

    results = collection.query(
        query_embeddings=q_emb,
        n_results=args.top_k,
        include=["documents", "metadatas", "distances"]
    )

    print(f"\n=== Top {args.top_k} results for: {args.query} ===\n")
    for i, (doc, meta, dist) in enumerate(zip(
        results["documents"][0],
        results["metadatas"][0],
        results["distances"][0]
    ), 1):
        file = meta.get("file", "unknown")
        start = meta.get("start_line", "?")
        end = meta.get("end_line", "?")
        lang = meta.get("language", "")
        score = 1 - dist  # cosine similarity approx
        print(f"[{i}] {file}:{start}-{end} (sim={score:.3f}, {lang})")
        # Show first ~600 chars of the chunk
        snippet = doc[:600].replace("\n", "↵ ")
        print(f"    {snippet}...\n")

if __name__ == "__main__":
    main()
