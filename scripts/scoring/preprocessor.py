"""
PR Preprocessor v1 — Deterministic scoring before LLM.

GATES (pass/fail — fail = skip LLM entirely):
  - Builds?       → CI status via GitHub API
  - Duplicate?    → file overlap + title similarity vs decision memory

SIGNALS (0-6 points):
  - Has tests?       → 0, 1, or 2
  - PR size          → 0 or 1
  - Commit format    → 0 or 1
  - Churn risk       → 0 or 1

Output: JSON with gate results, signal breakdown, pre-score
Pre-score formula: 4 (gate bonus) + signals (0-5) = 4-9 range
LLM adjusts final ±1 = possible 3-10 range
"""

import json
import sys
import os
import re
import subprocess
from datetime import datetime, timedelta, timezone
from difflib import SequenceMatcher

MEMORY_FILE = os.path.join(os.path.expanduser("~"), ".office-rag-db", "decision-memory.json")


def load_memory(hours=4):
    if not os.path.exists(MEMORY_FILE):
        return []
    with open(MEMORY_FILE, "r") as f:
        memory = json.load(f)
    cutoff = datetime.now(timezone.utc) - timedelta(hours=hours)
    recent = []
    for entry in memory:
        if entry.get("decision") == "auto-merged":
            try:
                ts = entry["timestamp"].replace("Z", "+00:00")
                if "+" not in ts and "-" not in ts[10:]:
                    ts += "+00:00"
                parsed = datetime.fromisoformat(ts)
                if parsed > cutoff:
                    recent.append(entry)
            except:
                pass
    return recent


# ========== GATES ==========

def gate_builds(ci_status):
    if ci_status == "failure":
        return False, "CI checks failed"
    return True, f"CI status: {ci_status}"


def gate_duplicate(pr_title, pr_files, recent_merges):
    for merged in recent_merges:
        # File overlap
        merged_files = merged.get("files") or []
        if pr_files and merged_files:
            overlap = set(pr_files) & set(merged_files)
            if len(pr_files) > 0:
                overlap_pct = len(overlap) / len(pr_files)
                if overlap_pct >= 0.5:
                    return False, f"50%+ file overlap with #{merged.get('pr_number')} ({merged.get('title')})"

        # Title similarity
        merged_title = merged.get("title", "")
        sim = SequenceMatcher(None, pr_title.lower(), merged_title.lower()).ratio()
        if sim > 0.7:
            return False, f"Title {sim:.0%} similar to #{merged.get('pr_number')} ({merged.get('title')})"

    return True, "No duplicates found"


# ========== SIGNALS ==========

def signal_has_tests(files):
    test_patterns = [r'test', r'spec', r'\.test\.', r'\.spec\.', r'Tests\.cs$', r'_test\.']
    test_files = [f for f in files if any(re.search(p, f, re.IGNORECASE) for p in test_patterns)]
    prod_files = [f for f in files if f not in test_files]

    if test_files and prod_files:
        return 2, f"{len(test_files)} test + {len(prod_files)} prod files"
    elif test_files:
        return 1, f"{len(test_files)} test files only"
    else:
        return 0, "No test files in diff"


def signal_pr_size(additions, deletions):
    total = additions + deletions
    if total > 500:
        return 0, f"{total} lines — large PR"
    return 1, f"{total} lines — reasonable"


def signal_commit_format(commit_messages):
    if not commit_messages:
        return 0, "No commits"
    pattern = r'^(feat|fix|test|chore|docs|refactor|style|ci|perf|build)[\(:]'
    good = sum(1 for m in commit_messages if re.match(pattern, m, re.IGNORECASE))
    ratio = good / len(commit_messages)
    if ratio >= 0.5:
        return 1, f"{good}/{len(commit_messages)} conventional"
    return 0, f"{good}/{len(commit_messages)} conventional"


def signal_churn_risk(files, repo_path=None):
    if not repo_path or not os.path.exists(repo_path):
        return 1, "Skipped — no local repo"
    try:
        high_churn = []
        for f in files[:10]:
            result = subprocess.run(
                ["git", "log", "--since=30 days ago", "--format=%H", "--", f],
                capture_output=True, text=True, cwd=repo_path
            )
            count = len(result.stdout.strip().split("\n")) if result.stdout.strip() else 0
            if count >= 5:
                high_churn.append(f"{f} ({count}x)")
        if high_churn:
            return 0, f"High churn: {', '.join(high_churn[:3])}"
        return 1, "Low churn"
    except:
        return 1, "Churn check failed"


# ========== MAIN ==========

def preprocess(pr_data):
    title = pr_data.get("title", "")
    files = pr_data.get("files", [])
    additions = pr_data.get("additions", 0)
    deletions = pr_data.get("deletions", 0)
    ci_status = pr_data.get("ci_status", "none")
    commit_messages = pr_data.get("commit_messages", [])
    repo_path = pr_data.get("repo_path", None)

    recent_merges = load_memory()

    result = {
        "gates": {},
        "signals": {},
        "pre_score": 0,
        "gate_passed": True,
        "gate_failure_reason": None,
        "signal_summary": ""
    }

    # Gates
    build_ok, build_reason = gate_builds(ci_status)
    result["gates"]["builds"] = {"passed": build_ok, "reason": build_reason}

    dup_ok, dup_reason = gate_duplicate(title, files, recent_merges)
    result["gates"]["not_duplicate"] = {"passed": dup_ok, "reason": dup_reason}

    if not build_ok:
        result["gate_passed"] = False
        result["gate_failure_reason"] = build_reason
        return result

    if not dup_ok:
        result["gate_passed"] = False
        result["gate_failure_reason"] = dup_reason
        return result

    # Signals
    test_score, test_reason = signal_has_tests(files)
    result["signals"]["tests"] = {"score": test_score, "max": 2, "reason": test_reason}

    size_score, size_reason = signal_pr_size(additions, deletions)
    result["signals"]["size"] = {"score": size_score, "max": 1, "reason": size_reason}

    commit_score, commit_reason = signal_commit_format(commit_messages)
    result["signals"]["commits"] = {"score": commit_score, "max": 1, "reason": commit_reason}

    churn_score, churn_reason = signal_churn_risk(files, repo_path)
    result["signals"]["churn"] = {"score": churn_score, "max": 1, "reason": churn_reason}

    # Pre-score: 4 base (gates passed) + signals (0-5)
    signal_total = test_score + size_score + commit_score + churn_score
    result["pre_score"] = 4 + signal_total

    # Build human-readable summary for LLM
    lines = []
    for name, sig in result["signals"].items():
        lines.append(f"  {name}: {sig['score']}/{sig['max']} — {sig['reason']}")
    result["signal_summary"] = "\n".join(lines)

    return result


if __name__ == "__main__":
    if len(sys.argv) > 1:
        with open(sys.argv[1], "r") as f:
            pr_data = json.load(f)
    else:
        pr_data = json.load(sys.stdin)

    result = preprocess(pr_data)
    print(json.dumps(result, indent=2))
