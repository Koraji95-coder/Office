# Knowledge

This folder holds repo-owned seed content that is indexed into the RAG system and embedded ML pipeline.

---

## Supported File Formats

| Extension | Best suited for |
|-----------|----------------|
| `.md`     | Technical standards summaries, architecture notes, agent behaviour guidelines, and any structured reference material. Preferred format — renders in GitHub and is chunked cleanly by the indexer. |
| `.txt`    | Plain prose references, glossaries, or any content that does not need Markdown formatting. |
| `.pdf`    | Scanned or published documents (e.g. vendor specs, process standards). The indexer reads text content; embedded images are ignored. |
| `.docx`   | Word documents from external sources. Convert to `.md` or `.txt` when possible to improve chunking quality. |

> **Note:** The indexer (`scripts/rag/index.py`) walks the full `Office` repo for the extensions listed in the `EXTENSIONS` constant. `.md` files in this folder are indexed automatically because `.md` is already in that set. `.txt`, `.pdf`, and `.docx` files are **not** indexed by default — you must add their extensions to `EXTENSIONS` in `index.py` first (see [Adding extra file extensions](#adding-extra-file-extensions) below).

---

## RAG Indexer Configuration

The indexer is configured by constants at the top of `scripts/rag/index.py`:

| Variable | Default value | Description |
|----------|---------------|-------------|
| `DB_PATH` | `~/.office-rag-db` | ChromaDB persistent store location on the local workstation. |
| `REPO_ROOT` | `~/OneDrive/Documents/GitHub` | Parent directory that contains the `Office` repo folder. Update this if your repo is cloned to a different path. |
| `EXTENSIONS` | `.ts .tsx .js .jsx .py .ps1 .json .md .yml .yaml .css .html .sh` | File extensions the indexer scans. `.txt`, `.pdf`, and `.docx` are **not** in this set by default; add them to index these types (see [Adding extra file extensions](#adding-extra-file-extensions) below). |
| `SKIP_DIRS` | `node_modules .git dist build .next __pycache__ .venv venv` | Directory names skipped during the walk. |
| `MAX_FILE_SIZE` | `50_000 bytes` | Files larger than this are skipped to avoid oversized chunks. |
| Chunk size | `2_000 chars` (in `chunk_file`) | Maximum characters per chunk before the indexer splits the file. The last 5 lines of each chunk are repeated as context overlap in the next chunk. |

### Changing the repo root

If the `Office` repo lives somewhere other than `~/OneDrive/Documents/GitHub/Office`, edit the `REPO_ROOT` constant in `scripts/rag/index.py`:

```python
REPO_ROOT = "/path/to/your/repos"   # change this line
```

### Adding extra file extensions

To index `.txt` files alongside the existing extensions, add `".txt"` to the `EXTENSIONS` set:

```python
EXTENSIONS = {".ts", ".tsx", ".js", ".jsx", ".py", ".ps1", ".json",
              ".md", ".yml", ".yaml", ".css", ".html", ".sh", ".txt"}
```

---

## Re-Indexing

After adding or updating files in this folder, rebuild the vector index so the changes are picked up by the scoring pipeline and agent desks.

**Windows (PowerShell) — recommended:**

```powershell
# From the repo root
.\scripts\rag\reindex.ps1
```

`reindex.ps1` performs a safe `git pull --ff-only` first, then calls `index.py`. Progress and any errors are appended to `~/.office-rag-db/reindex.log`.

**Cross-platform (Python directly):**

```bash
python scripts/rag/index.py
```

The script prints a batch-by-batch progress summary and a final count of indexed files and chunks.

---

## Contribution Workflow

Follow these steps when adding new seed content to this folder:

1. **Choose the right format.** Prefer `.md` over `.docx` or `.pdf` — plain text chunks more cleanly and is easier to review in pull requests.

2. **Name the file descriptively.** Use lowercase kebab-case: `agent-behaviour-guidelines.md`, `scoring-rubric-reference.txt`.

3. **Keep files focused.** One topic per file. The indexer chunks at ~2_000 characters; a single monolithic document covering many unrelated topics produces noisy retrieval results.

4. **Add the file to a feature branch.** Branch naming follows the repo convention: `docs/{description}` (e.g. `docs/add-scoring-rubric-reference`).

5. **Open a PR.** Docs-only PRs do not require test changes (see `Docs/CONVENTIONS.md`). Describe what knowledge is being added and why it is useful for RAG retrieval.

6. **Re-index after merge.** Once the PR is merged, run `reindex.ps1` (or `index.py` directly) on your local workstation to incorporate the new content into the live vector index.

---

## Content Quality Guidelines

- **Be specific.** Vague or generic prose adds little value to retrieval. Prefer concrete facts, thresholds, process steps, and examples.
- **Avoid duplication.** Check whether the content already exists in `Docs/` before adding it here. If it does, link to the canonical doc rather than copying it.
- **No secrets.** Never include API keys, passwords, tokens, or personal data. The entire `Knowledge/` folder is version-controlled and public.
- **No customer-facing copy.** This folder is for internal ML pipeline context only.
