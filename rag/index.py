#!/usr/bin/env python3
"""
BusBuddy RAG Indexer

Walks the repository, creates semantic chunks of source code, docs, and configs,
embeds them locally with sentence-transformers, and stores in a persistent
ChromaDB vector database.

Run: python -m rag.index   (from project root)

This builds the context that the MCP RAG tool and agent rules rely on.
Always re-index after significant refactors or when adding major features.
"""

import os
import sys
from pathlib import Path
from typing import List, Dict, Any

import chromadb
from sentence_transformers import SentenceTransformer

# Configuration
DB_PATH = Path(__file__).parent / "chroma_db"
COLLECTION_NAME = "busbuddy_codebase"
EMBED_MODEL = "all-MiniLM-L6-v2"
MAX_CHUNK_LINES = 45
OVERLAP_LINES = 8
CHROMA_BATCH_SIZE = 5000  # ChromaDB default max batch ~5461

# Directories and patterns to completely ignore (first-attempt legacy + build artifacts)
IGNORE_DIRS = {
    "Archive", "bin", "obj", ".git", "__pycache__", ".vs", "TestResults",
    "Documentation/Archive", "node_modules", "rag/chroma_db", ".rag_db",
    "experiments", "Powershell", "Scripts/legacy", ".agents/skills"
}

# File extensions worth indexing for project context
INDEX_EXTENSIONS = {
    ".cs", ".xaml", ".md", ".py", ".json", ".yml", ".yaml", ".txt",
    ".csproj", ".sln", ".props", ".targets", ".config"
}

# Extra files to always include even if not perfect match
ALWAYS_INCLUDE = {
    "README.md",
    "AGENTS.md",
    "STEADY-STATE-AND-FINISH-ROADMAP.md",
    "DEVELOPMENT-GUIDE.md",
    "Documentation/GCP-GEE-SECRETS-AND-AUTH.md",
    ".cursor/mcp.json",
    ".github/copilot-instructions.md",
}

def should_ignore(path: str) -> bool:
    path_lower = path.lower()
    for ign in IGNORE_DIRS:
        if ign.lower() in path_lower:
            return True
    return False

def get_language(ext: str) -> str:
    mapping = {
        ".cs": "csharp", ".xaml": "xaml", ".md": "markdown",
        ".py": "python", ".json": "json", ".yml": "yaml", ".yaml": "yaml",
        ".csproj": "xml", ".sln": "text", ".props": "xml"
    }
    return mapping.get(ext.lower(), "text")

def chunk_text(content: str, file_path: str, max_lines: int = MAX_CHUNK_LINES, overlap: int = OVERLAP_LINES) -> List[Dict[str, Any]]:
    """Chunk by lines for code-friendliness with overlap and line metadata."""
    if not content.strip():
        return []
    lines = content.splitlines(keepends=True)
    chunks: List[Dict[str, Any]] = []
    i = 0
    chunk_id = 0
    while i < len(lines):
        chunk_lines = lines[i : i + max_lines]
        text = "".join(chunk_lines)
        start_line = i + 1
        end_line = i + len(chunk_lines)
        chunks.append({
            "id": f"{file_path}:{start_line}-{end_line}:{chunk_id}",
            "text": text,
            "metadata": {
                "file": file_path,
                "start_line": start_line,
                "end_line": end_line,
                "language": get_language(Path(file_path).suffix),
                "chunk_type": "code" if Path(file_path).suffix in {".cs", ".xaml"} else "doc"
            }
        })
        chunk_id += 1
        i += max_lines - overlap
    return chunks

def collect_files(root: Path) -> List[Path]:
    files: List[Path] = []
    for dirpath, dirnames, filenames in os.walk(root):
        # Prune ignored directories in-place
        dirnames[:] = [d for d in dirnames if not should_ignore(os.path.join(dirpath, d))]
        for fname in filenames:
            fpath = Path(dirpath) / fname
            rel = str(fpath.relative_to(root))
            if should_ignore(rel):
                continue
            ext = fpath.suffix.lower()
            if ext in INDEX_EXTENSIONS or fpath.name in ALWAYS_INCLUDE:
                files.append(fpath)
    return sorted(files)

def main():
    root = Path.cwd()
    print(f"BusBuddy RAG Indexer starting from: {root}")
    print("Collecting files (respecting ignores)...")
    files = collect_files(root)
    print(f"Found {len(files)} indexable files.")

    print(f"Loading embedding model: {EMBED_MODEL} (local, first run downloads ~90MB)")
    model = SentenceTransformer(EMBED_MODEL)

    client = chromadb.PersistentClient(path=str(DB_PATH))
    collection = client.get_or_create_collection(
        name=COLLECTION_NAME,
        metadata={"hnsw:space": "cosine"}
    )

    # Clear previous for clean re-index (idempotent for portfolio baseline)
    if collection.count() > 0:
        print(f"Clearing existing collection ({collection.count()} chunks)...")
        # Chroma doesn't have truncate, so delete by ids or recreate
        all_ids = collection.get()["ids"]
        if all_ids:
            collection.delete(ids=all_ids)

    all_chunks: List[Dict] = []
    for f in files:
        try:
            text = f.read_text(encoding="utf-8", errors="ignore")
        except Exception as e:
            print(f"  Skip unreadable {f}: {e}")
            continue
        chunks = chunk_text(text, str(f.relative_to(root)))
        all_chunks.extend(chunks)

    if not all_chunks:
        print("No chunks generated. Exiting.")
        return

    print(f"Generated {len(all_chunks)} chunks. Embedding + storing in Chroma...")

    for start in range(0, len(all_chunks), CHROMA_BATCH_SIZE):
        batch = all_chunks[start : start + CHROMA_BATCH_SIZE]
        texts = [c["text"] for c in batch]
        metadatas = [c["metadata"] for c in batch]
        ids = [c["id"] for c in batch]
        embeddings = model.encode(texts, show_progress_bar=True, convert_to_numpy=True).tolist()
        collection.add(
            ids=ids,
            documents=texts,
            embeddings=embeddings,
            metadatas=metadatas,
        )
        print(f"  Stored batch {start // CHROMA_BATCH_SIZE + 1} ({len(batch)} chunks)")

    print(f"✅ RAG index complete. Collection now has {collection.count()} chunks.")
    print(f"   DB location: {DB_PATH}")
    print("   Next: run the MCP server or query tool for agent use.")

if __name__ == "__main__":
    main()
