# Knowledge

This folder holds repo-owned seed content that is indexed into the RAG system and embedded ML pipeline.

Supported file types:

- `.txt`
- `.md`
- `.pdf`
- `.docx`

Good uses for this folder:

- technical standards summaries
- project notes and reference docs
- written context about how agents should behave
- domain reference material for RAG retrieval

The RAG indexer (`scripts/rag/index.py`) scans this folder and builds a vector index used by the scoring pipeline and agent desks.
