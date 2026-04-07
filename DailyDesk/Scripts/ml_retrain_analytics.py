"""
Retrain pipeline for DailyDesk learning analytics models.

Trains and persists the following models from accumulated training history:
  - Topic clustering model (KMeans)
  - Readiness predictor (GradientBoostingRegressor)
  - Operator pattern classifier (GradientBoostingClassifier)

Input: JSON on stdin or --input <path> with trainingAttempts and operatorDecisions
Output: JSON on stdout with metrics; models and metrics file written to State/ml-artifacts/

Designed to run nightly via Windows Scheduled Tasks.
Falls back to { "ok": false } output if scikit-learn is not installed.
"""

import json
import math
import os
import sys
from datetime import datetime, timezone
from typing import Any

# Precomputed constant: -ln(0.5) used in stability estimation
_LN_2 = math.log(2.0)


# ---------------------------------------------------------------------------
# State root resolution
# ---------------------------------------------------------------------------

def _state_root() -> str:
    env = os.environ.get("OFFICE_STATE_ROOT", "")
    if env:
        return env
    return os.path.join(os.path.expanduser("~"), "Dropbox", "SuiteWorkspace", "Office", "State")


def _artifacts_dir() -> str:
    return os.path.join(_state_root(), "ml-artifacts")


# ---------------------------------------------------------------------------
# sklearn guard
# ---------------------------------------------------------------------------

def _try_import_sklearn() -> bool:
    try:
        import sklearn  # noqa: F401
        return True
    except ImportError:
        return False


# ---------------------------------------------------------------------------
# Input helpers (same pattern as ml_learning_analytics.py)
# ---------------------------------------------------------------------------

def _read_input() -> str:
    """Read input from --input file argument or stdin."""
    for i, arg in enumerate(sys.argv[1:], 1):
        if arg == "--input" and i < len(sys.argv) - 1:
            from pathlib import Path
            return Path(sys.argv[i + 1]).read_text(encoding="utf-8")
    return sys.stdin.read()


# ---------------------------------------------------------------------------
# Feature extraction — mirrors ml_learning_analytics.py for model compatibility
# ---------------------------------------------------------------------------

def _parse_iso(value: str) -> datetime:
    cleaned = value.replace("Z", "+00:00")
    try:
        return datetime.fromisoformat(cleaned)
    except ValueError:
        return datetime.now(timezone.utc)


def _compute_topic_accuracy(attempts: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    """Aggregate accuracy per topic from training attempt question records."""
    topics: dict[str, dict[str, Any]] = {}
    for attempt in attempts:
        questions = attempt.get("questions", [])
        for question in questions:
            topic = question.get("topic", "general")
            correct = question.get("correct", False)
            if topic not in topics:
                topics[topic] = {"correct": 0, "total": 0, "attempts": []}
            topics[topic]["total"] += 1
            if correct:
                topics[topic]["correct"] += 1
            timestamp = attempt.get("completedAt", "")
            if timestamp:
                topics[topic]["attempts"].append(
                    {"correct": correct, "timestamp": timestamp}
                )

    for topic_data in topics.values():
        total = topic_data["total"]
        topic_data["accuracy"] = topic_data["correct"] / total if total > 0 else 0.0

    return topics


def _compute_stability_days(attempts: list[dict[str, Any]]) -> float:
    """Estimate memory stability from successful delayed recalls (mirrors ml_learning_analytics)."""
    if len(attempts) < 2:
        return 3.0

    timed = []
    for a in attempts:
        ts = a.get("timestamp", "")
        if ts:
            timed.append({"time": _parse_iso(ts), "correct": a.get("correct", False)})
    timed.sort(key=lambda x: x["time"])

    if len(timed) < 2:
        return 3.0

    stability_estimates = []
    for i in range(1, len(timed)):
        gap_days = (timed[i]["time"] - timed[i - 1]["time"]).total_seconds() / 86400
        if timed[i]["correct"] and gap_days > 0.1:
            implied_s = gap_days / max(0.1, _LN_2)
            stability_estimates.append(implied_s)

    if not stability_estimates:
        return 1.5

    stability_estimates.sort()
    stability = stability_estimates[len(stability_estimates) // 2]
    return max(0.5, min(90.0, stability))


def _days_since_last_review(attempts: list[dict[str, Any]]) -> float:
    """Return days since the most recent attempt."""
    if not attempts:
        return 30.0
    timestamps = [_parse_iso(a["timestamp"]) for a in attempts if a.get("timestamp")]
    if not timestamps:
        return 30.0
    last = max(timestamps)
    delta = datetime.now(timezone.utc) - last
    return delta.total_seconds() / 86400


def _build_topic_features(topic_accuracy: dict[str, dict[str, Any]]) -> tuple[list, list]:
    """Build feature matrix for topic clustering and readiness prediction.

    Features: [accuracy, attempt_count, stability_days, days_since_last_review]
    Returns (feature_rows, topic_names).
    """
    rows = []
    names = []
    for topic, data in topic_accuracy.items():
        stability = _compute_stability_days(data.get("attempts", []))
        days_since = _days_since_last_review(data.get("attempts", []))
        rows.append([
            data["accuracy"],
            float(data["total"]),
            stability,
            days_since,
        ])
        names.append(topic)
    return rows, names


def _build_readiness_training_data(
    topic_accuracy: dict[str, dict[str, Any]],
) -> tuple[list, list]:
    """Build X/y for the readiness predictor.

    For each topic that has at least 2 time-ordered attempts, use the first N-1
    attempts as features and the accuracy of the last attempt as the target.

    Features: [accuracy_up_to_N-1, attempt_count, stability_days, days_since_last]
    Target:   accuracy of the final attempt (proxy for next-session performance)
    """
    X: list[list[float]] = []
    y: list[float] = []

    for data in topic_accuracy.values():
        attempts = data.get("attempts", [])
        if len(attempts) < 2:
            continue

        timed = sorted(
            [a for a in attempts if a.get("timestamp")],
            key=lambda a: _parse_iso(a["timestamp"]),
        )
        if len(timed) < 2:
            continue

        for split_idx in range(1, len(timed)):
            prefix = timed[:split_idx]
            prefix_correct = sum(1 for a in prefix if a.get("correct", False))
            prefix_acc = prefix_correct / len(prefix)
            stability = _compute_stability_days(prefix)
            last_ts = _parse_iso(prefix[-1]["timestamp"])
            next_ts = _parse_iso(timed[split_idx]["timestamp"])
            days_gap = (next_ts - last_ts).total_seconds() / 86400
            next_correct = 1.0 if timed[split_idx].get("correct", False) else 0.0

            X.append([prefix_acc, float(len(prefix)), stability, days_gap])
            y.append(next_correct)

    return X, y


def _build_operator_training_data(
    operator_decisions: list[dict[str, Any]],
) -> tuple[list, list]:
    """Build X/y for the operator pattern classifier.

    Each decision becomes a sample. Features encode recency and decision type.
    Label: 0=accepted, 1=rejected, 2=deferred
    """
    decision_map = {"accepted": 0, "rejected": 1, "deferred": 2}
    valid = [d for d in operator_decisions if d.get("status") in decision_map]
    if not valid:
        return [], []

    now = datetime.now(timezone.utc)
    X: list[list[float]] = []
    y: list[int] = []

    for i, d in enumerate(valid):
        ts_str = d.get("decidedAt") or d.get("timestamp") or ""
        if ts_str:
            try:
                ts = _parse_iso(ts_str)
                age_days = (now - ts).total_seconds() / 86400
            except Exception:
                age_days = 30.0
        else:
            age_days = 30.0

        position_ratio = i / max(len(valid) - 1, 1)
        label = decision_map[d["status"]]
        X.append([age_days, position_ratio, float(i)])
        y.append(label)

    return X, y


# ---------------------------------------------------------------------------
# Training helpers
# ---------------------------------------------------------------------------

def _train_topic_clustering(
    topic_accuracy: dict[str, dict[str, Any]],
) -> dict[str, Any]:
    """Train KMeans topic clustering model and return metrics."""
    import numpy as np
    from sklearn.cluster import KMeans
    from sklearn.metrics import silhouette_score
    from sklearn.preprocessing import StandardScaler

    rows, names = _build_topic_features(topic_accuracy)
    n_topics = len(rows)

    if n_topics < 3:
        return {
            "saved": False,
            "reason": f"Insufficient topics for clustering ({n_topics} < 3)",
            "n_topics": n_topics,
        }

    X = np.array(rows, dtype=np.float64)
    scaler = StandardScaler()
    X_scaled = scaler.fit_transform(X)

    n_clusters = min(3, n_topics)
    kmeans = KMeans(n_clusters=n_clusters, random_state=42, n_init=10)
    labels = kmeans.fit_predict(X_scaled)

    if n_topics > n_clusters:
        sil = float(silhouette_score(X_scaled, labels))
    else:
        sil = 0.0

    meets_threshold = sil >= 0.1
    saved = False

    if meets_threshold:
        import joblib
        artifacts_path = _artifacts_dir()
        os.makedirs(artifacts_path, exist_ok=True)
        model_path = os.path.join(artifacts_path, "topic-cluster-model.joblib")
        joblib.dump({"kmeans": kmeans, "scaler": scaler}, model_path)
        saved = True

    return {
        "n_clusters": n_clusters,
        "silhouette_score": round(sil, 4),
        "n_topics": n_topics,
        "saved": saved,
    }


def _train_readiness_predictor(
    topic_accuracy: dict[str, dict[str, Any]],
) -> dict[str, Any]:
    """Train GradientBoostingRegressor readiness predictor and return metrics."""
    import numpy as np
    from sklearn.ensemble import GradientBoostingRegressor
    from sklearn.model_selection import cross_val_score

    X_list, y_list = _build_readiness_training_data(topic_accuracy)
    n_samples = len(X_list)

    if n_samples < 5:
        return {
            "saved": False,
            "reason": f"Insufficient training samples ({n_samples} < 5)",
            "training_samples": n_samples,
        }

    X = np.array(X_list, dtype=np.float64)
    y = np.array(y_list, dtype=np.float64)

    reg = GradientBoostingRegressor(
        n_estimators=100, max_depth=3, random_state=42, min_samples_split=2
    )

    cv_folds = min(5, n_samples)
    cv_scores = cross_val_score(reg, X, y, cv=cv_folds, scoring="r2")
    cv_r2 = float(np.mean(cv_scores))
    cv_r2_std = float(np.std(cv_scores))

    meets_threshold = cv_r2 >= 0.1
    saved = False

    if meets_threshold:
        import joblib
        reg.fit(X, y)
        artifacts_path = _artifacts_dir()
        os.makedirs(artifacts_path, exist_ok=True)
        model_path = os.path.join(artifacts_path, "readiness-predictor.joblib")
        joblib.dump(reg, model_path)
        saved = True

    return {
        "cv_r2": round(cv_r2, 4),
        "cv_r2_std": round(cv_r2_std, 4),
        "training_samples": n_samples,
        "saved": saved,
    }


def _train_operator_classifier(
    operator_decisions: list[dict[str, Any]],
) -> dict[str, Any]:
    """Train GradientBoostingClassifier operator pattern model and return metrics."""
    import numpy as np
    from sklearn.ensemble import GradientBoostingClassifier
    from sklearn.model_selection import cross_val_score

    X_list, y_list = _build_operator_training_data(operator_decisions)
    n_samples = len(X_list)

    if n_samples < 5:
        return {
            "saved": False,
            "reason": f"Insufficient training samples ({n_samples} < 5)",
            "training_samples": n_samples,
        }

    classes = sorted(set(y_list))
    if len(classes) < 2:
        return {
            "saved": False,
            "reason": "Single-class labels — cannot train classifier",
            "training_samples": n_samples,
            "classes": classes,
        }

    X = np.array(X_list, dtype=np.float64)
    y = np.array(y_list, dtype=np.int64)

    clf = GradientBoostingClassifier(
        n_estimators=100, max_depth=3, random_state=42, min_samples_split=2
    )

    cv_folds = min(5, n_samples)
    avg = "binary" if len(classes) == 2 else "weighted"
    cv_scores = cross_val_score(clf, X, y, cv=cv_folds, scoring=f"f1_{avg}")
    cv_f1 = float(np.mean(cv_scores))
    cv_f1_std = float(np.std(cv_scores))

    meets_threshold = cv_f1 >= 0.4
    saved = False

    if meets_threshold:
        import joblib
        clf.fit(X, y)
        artifacts_path = _artifacts_dir()
        os.makedirs(artifacts_path, exist_ok=True)
        model_path = os.path.join(artifacts_path, "operator-pattern-model.joblib")
        joblib.dump(clf, model_path)
        saved = True

    label_map = {0: "accepted", 1: "rejected", 2: "deferred"}
    class_names = [label_map.get(c, str(c)) for c in classes]

    return {
        "cv_f1": round(cv_f1, 4),
        "cv_f1_std": round(cv_f1_std, 4),
        "training_samples": n_samples,
        "classes": class_names,
        "saved": saved,
    }


# ---------------------------------------------------------------------------
# Metrics export
# ---------------------------------------------------------------------------

def _export_metrics(metrics: dict[str, Any]) -> None:
    """Write analytics-model-metrics.json to the artifacts directory."""
    artifacts_path = _artifacts_dir()
    os.makedirs(artifacts_path, exist_ok=True)
    metrics_path = os.path.join(artifacts_path, "analytics-model-metrics.json")
    with open(metrics_path, "w", encoding="utf-8") as f:
        json.dump(metrics, f, ensure_ascii=False, indent=2)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main() -> None:
    if not _try_import_sklearn():
        print(json.dumps({"ok": False, "error": "scikit-learn not installed"}))
        return

    try:
        raw = _read_input()
        payload = json.loads(raw) if raw.strip() else {}
    except json.JSONDecodeError:
        print(json.dumps({"ok": False, "error": "Invalid JSON input."}))
        return

    training_attempts = payload.get("trainingAttempts", [])
    operator_decisions = payload.get("operatorDecisions", [])

    topic_accuracy = _compute_topic_accuracy(training_attempts)

    try:
        clustering_metrics = _train_topic_clustering(topic_accuracy)
    except Exception as exc:
        clustering_metrics = {"saved": False, "reason": str(exc)}

    try:
        readiness_metrics = _train_readiness_predictor(topic_accuracy)
    except Exception as exc:
        readiness_metrics = {"saved": False, "reason": str(exc)}

    try:
        operator_metrics = _train_operator_classifier(operator_decisions)
    except Exception as exc:
        operator_metrics = {"saved": False, "reason": str(exc)}

    metrics: dict[str, Any] = {
        "retrained_at": datetime.now(timezone.utc).isoformat(),
        "readiness_predictor": readiness_metrics,
        "operator_classifier": operator_metrics,
        "topic_clusters": clustering_metrics,
    }

    try:
        _export_metrics(metrics)
    except Exception:
        pass

    result = {"ok": True, **metrics}
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    main()
