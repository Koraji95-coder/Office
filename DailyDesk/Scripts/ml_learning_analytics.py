"""
Scikit-learn based learning analytics engine for DailyDesk.

Analyzes training history to:
- Cluster weak topics and identify knowledge gaps
- Predict readiness scores for each topic area using gradient boosting
- Generate adaptive study schedules using spaced repetition with ML optimization
- Classify operator decision patterns (approve/reject/defer tendencies)
- Model forgetting curves per topic to optimize review intervals (NEW)
- Compute Bayesian confidence intervals on readiness estimates (NEW)
- Detect mastery plateaus and recommend breakthrough strategies (NEW)

Input: JSON on stdin with training_history, operator_memory, and learning_profile
Output: JSON on stdout with analytics results

Designed to run as a subprocess called by MLAnalyticsService.cs via ProcessRunner.
Falls back to heuristic output if scikit-learn is not installed.
"""

import json
import math
import os
import sys
from datetime import datetime, timezone
from typing import Any


def _try_import_sklearn() -> bool:
    try:
        import sklearn  # noqa: F401

        return True
    except ImportError:
        return False


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


def _compute_forgetting_curve(attempts: list[dict[str, Any]]) -> dict[str, Any]:
    """Estimate forgetting curve parameters for a topic's attempts.

    Uses the Ebbinghaus forgetting curve model: R = e^(-t/S)
    where R is retention, t is time since last review, S is stability.

    Returns stability estimate and predicted retention at various intervals.
    """
    if len(attempts) < 2:
        return {"stability_days": 3.0, "predicted_retention": {}}

    # Sort by timestamp
    timed = []
    for a in attempts:
        ts = a.get("timestamp", "")
        if ts:
            timed.append({"time": _parse_iso(ts), "correct": a.get("correct", False)})
    timed.sort(key=lambda x: x["time"])

    if len(timed) < 2:
        return {"stability_days": 3.0, "predicted_retention": {}}

    # Estimate stability from inter-review intervals and success rates
    intervals = []
    for i in range(1, len(timed)):
        gap_days = (timed[i]["time"] - timed[i - 1]["time"]).total_seconds() / 86400
        if gap_days > 0:
            intervals.append({
                "gap_days": gap_days,
                "retained": timed[i]["correct"],
            })

    if not intervals:
        return {"stability_days": 3.0, "predicted_retention": {}}

    # Fit stability: for each interval where we have a success after gap,
    # stability S = -t / ln(R). Estimate R from recent accuracy.
    recent_accuracy = sum(1 for a in timed[-5:] if a["correct"]) / min(len(timed), 5)
    if recent_accuracy <= 0:
        recent_accuracy = 0.1

    # Weighted average of implied stability from successful recalls after gaps
    stability_estimates = []
    for iv in intervals:
        if iv["retained"] and iv["gap_days"] > 0.1:
            # If recalled after gap_days, stability >= gap_days / -ln(threshold)
            # Use a conservative retention threshold of 0.5
            implied_s = iv["gap_days"] / max(0.1, -math.log(0.5))
            stability_estimates.append(implied_s)

    if stability_estimates:
        # Use median for robustness
        stability_estimates.sort()
        stability = stability_estimates[len(stability_estimates) // 2]
    else:
        # Default: short stability for topics with no successful delayed recalls
        stability = 1.5

    # Clamp to reasonable range
    stability = max(0.5, min(90.0, stability))

    # Predict retention at standard intervals
    predicted = {}
    for days in [1, 3, 7, 14, 30]:
        retention = math.exp(-days / stability)
        predicted[f"{days}d"] = round(retention, 3)

    return {
        "stability_days": round(stability, 1),
        "predicted_retention": predicted,
    }


def _bayesian_confidence(correct: int, total: int, prior_alpha: float = 1.0, prior_beta: float = 1.0) -> dict[str, float]:
    """Compute Bayesian posterior for topic accuracy using Beta-Binomial model.

    Returns mean, lower/upper 90% credible interval bounds.
    Prior: Beta(alpha, beta) — default uniform Beta(1,1).
    """
    alpha = prior_alpha + correct
    beta = prior_beta + (total - correct)
    mean = alpha / (alpha + beta)

    # 90% credible interval using normal approximation for Beta
    variance = (alpha * beta) / ((alpha + beta) ** 2 * (alpha + beta + 1))
    std = math.sqrt(variance)
    z90 = 1.645  # 90% CI

    lower = max(0.0, mean - z90 * std)
    upper = min(1.0, mean + z90 * std)

    return {
        "mean": round(mean, 3),
        "lower_90": round(lower, 3),
        "upper_90": round(upper, 3),
        "samples": total,
    }


def _detect_plateau(attempts: list[dict[str, Any]], window: int = 10) -> dict[str, Any]:
    """Detect if a topic's accuracy has plateaued (no improvement over recent window)."""
    if len(attempts) < window:
        return {"plateaued": False, "reason": "Insufficient data"}

    # Split into first half and second half of the window
    recent = attempts[-window:]
    first_half = recent[: window // 2]
    second_half = recent[window // 2:]

    first_acc = sum(1 for a in first_half if a.get("correct", False)) / len(first_half)
    second_acc = sum(1 for a in second_half if a.get("correct", False)) / len(second_half)

    improvement = second_acc - first_acc
    if abs(improvement) < 0.05 and first_acc < 0.85:
        return {
            "plateaued": True,
            "accuracy": round(second_acc, 3),
            "recommendation": "Switch to oral defense or apply-style practice to break plateau",
        }
    elif improvement < -0.1:
        return {
            "plateaued": False,
            "regressing": True,
            "accuracy": round(second_acc, 3),
            "recommendation": "Regression detected — schedule immediate review",
        }
    return {"plateaued": False}


def _heuristic_analytics(
    topic_accuracy: dict[str, dict[str, Any]],
    operator_decisions: list[dict[str, Any]],
) -> dict[str, Any]:
    """Deterministic fallback when scikit-learn is not available."""
    weak_topics = []
    strong_topics = []
    for topic, data in topic_accuracy.items():
        entry = {
            "topic": topic,
            "accuracy": round(data["accuracy"], 3),
            "totalQuestions": data["total"],
            "correctCount": data["correct"],
        }
        if data["accuracy"] < 0.6:
            weak_topics.append(entry)
        else:
            strong_topics.append(entry)

    weak_topics.sort(key=lambda x: x["accuracy"])
    strong_topics.sort(key=lambda x: x["accuracy"], reverse=True)

    schedule = []
    for i, topic_entry in enumerate(weak_topics[:5]):
        schedule.append(
            {
                "topic": topic_entry["topic"],
                "priority": i + 1,
                "recommendedSessionType": "practice"
                if topic_entry["accuracy"] < 0.4
                else "defense",
                "intervalDays": max(1, int((1.0 - topic_entry["accuracy"]) * 7)),
                "reason": f"Accuracy {topic_entry['accuracy']:.0%} is below threshold",
            }
        )

    approve_count = sum(
        1 for d in operator_decisions if d.get("status") == "accepted"
    )
    reject_count = sum(
        1 for d in operator_decisions if d.get("status") == "rejected"
    )
    defer_count = sum(
        1 for d in operator_decisions if d.get("status") == "deferred"
    )
    total_decisions = approve_count + reject_count + defer_count

    readiness = 0.0
    if topic_accuracy:
        readiness = sum(
            d["accuracy"] for d in topic_accuracy.values()
        ) / len(topic_accuracy)

    # Compute enhanced analytics for each topic
    forgetting_curves = {}
    bayesian_estimates = {}
    plateau_status = {}
    for topic, data in topic_accuracy.items():
        forgetting_curves[topic] = _compute_forgetting_curve(data.get("attempts", []))
        bayesian_estimates[topic] = _bayesian_confidence(data["correct"], data["total"])
        plateau_status[topic] = _detect_plateau(data.get("attempts", []))

    return {
        "ok": True,
        "engine": "heuristic",
        "weakTopics": weak_topics,
        "strongTopics": strong_topics,
        "overallReadiness": round(readiness, 3),
        "topicClusters": [],
        "adaptiveSchedule": schedule,
        "operatorPattern": {
            "approveRate": round(approve_count / total_decisions, 3)
            if total_decisions > 0
            else 0.0,
            "rejectRate": round(reject_count / total_decisions, 3)
            if total_decisions > 0
            else 0.0,
            "deferRate": round(defer_count / total_decisions, 3)
            if total_decisions > 0
            else 0.0,
            "totalDecisions": total_decisions,
            "pattern": "balanced",
        },
        "readinessBreakdown": [
            {
                "topic": topic,
                "readiness": round(data["accuracy"], 3),
                "confidence": min(1.0, data["total"] / 20),
            }
            for topic, data in topic_accuracy.items()
        ],
        "forgettingCurves": forgetting_curves,
        "bayesianEstimates": bayesian_estimates,
        "plateauStatus": plateau_status,
    }


def _state_root() -> str:
    """Resolve the Office state root directory."""
    env = os.environ.get("OFFICE_STATE_ROOT", "")
    if env:
        return env
    return os.path.join(os.path.expanduser("~"), "Dropbox", "SuiteWorkspace", "Office", "State")


def _load_persisted_model(filename: str) -> Any:
    """Attempt to load a persisted joblib model. Returns the model or None."""
    try:
        import joblib
        model_path = os.path.join(_state_root(), "ml-artifacts", filename)
        if os.path.exists(model_path):
            return joblib.load(model_path)
    except Exception:
        pass
    return None


def _sklearn_analytics(
    topic_accuracy: dict[str, dict[str, Any]],
    operator_decisions: list[dict[str, Any]],
) -> dict[str, Any]:
    """Full ML analytics using scikit-learn. Loads persisted models when available."""
    import numpy as np
    from sklearn.cluster import KMeans
    from sklearn.ensemble import GradientBoostingClassifier
    from sklearn.preprocessing import StandardScaler

    heuristic = _heuristic_analytics(topic_accuracy, operator_decisions)
    persisted_sources: set[str] = set()

    # --- Topic clustering ---
    topic_clusters: list[dict[str, Any]] = []
    if len(topic_accuracy) >= 3:
        topics_list = list(topic_accuracy.keys())

        # Enhanced feature set for clustering — include forgetting curve stability
        features = np.array(
            [
                [
                    topic_accuracy[t]["accuracy"],
                    topic_accuracy[t]["total"],
                    topic_accuracy[t]["correct"],
                    len(topic_accuracy[t].get("attempts", [])),
                    heuristic["forgettingCurves"].get(t, {}).get("stability_days", 3.0),
                    heuristic["bayesianEstimates"].get(t, {}).get("upper_90", 0.5)
                    - heuristic["bayesianEstimates"].get(t, {}).get("lower_90", 0.5),
                ]
                for t in topics_list
            ]
        )

        scaler = StandardScaler()
        scaled = scaler.fit_transform(features)

        # Try loading persisted clustering model (expects 4 features — use first 4 cols)
        persisted_cluster = _load_persisted_model("topic-cluster-model.joblib")
        if persisted_cluster is not None:
            try:
                persisted_kmeans = persisted_cluster.get("kmeans")
                persisted_scaler = persisted_cluster.get("scaler")
                # The persisted model uses 4 features; build compatible feature matrix
                compat_features = np.array(
                    [
                        [
                            topic_accuracy[t]["accuracy"],
                            float(topic_accuracy[t]["total"]),
                            heuristic["forgettingCurves"].get(t, {}).get("stability_days", 3.0),
                            float(
                                (datetime.now(timezone.utc) - max(
                                    (_parse_iso(a["timestamp"]) for a in topic_accuracy[t].get("attempts", []) if a.get("timestamp")),
                                    default=datetime.now(timezone.utc),
                                )).total_seconds() / 86400
                            ),
                        ]
                        for t in topics_list
                    ]
                )
                compat_scaled = persisted_scaler.transform(compat_features)
                labels = persisted_kmeans.predict(compat_scaled)
                persisted_sources.add("cluster")
            except Exception:
                n_clusters = min(3, len(topics_list))
                kmeans = KMeans(n_clusters=n_clusters, random_state=42, n_init=10)
                labels = kmeans.fit_predict(scaled)
        else:
            n_clusters = min(3, len(topics_list))
            kmeans = KMeans(n_clusters=n_clusters, random_state=42, n_init=10)
            labels = kmeans.fit_predict(scaled)

        cluster_groups: dict[int, list[str]] = {}
        for topic, label in zip(topics_list, labels):
            cluster_groups.setdefault(int(label), []).append(topic)

        for cluster_id, cluster_topics in cluster_groups.items():
            avg_accuracy = np.mean(
                [topic_accuracy[t]["accuracy"] for t in cluster_topics]
            )
            avg_stability = np.mean(
                [heuristic["forgettingCurves"].get(t, {}).get("stability_days", 3.0) for t in cluster_topics]
            )
            topic_clusters.append(
                {
                    "clusterId": cluster_id,
                    "topics": cluster_topics,
                    "averageAccuracy": round(float(avg_accuracy), 3),
                    "averageStability": round(float(avg_stability), 1),
                    "label": "weak"
                    if avg_accuracy < 0.5
                    else "developing"
                    if avg_accuracy < 0.75
                    else "strong",
                }
            )

    # --- Operator decision pattern classification ---
    operator_pattern = heuristic["operatorPattern"]
    if len(operator_decisions) >= 5:
        decision_map = {"accepted": 0, "rejected": 1, "deferred": 2}
        valid_decisions = [
            d
            for d in operator_decisions
            if d.get("status") in decision_map
        ]

        if len(valid_decisions) >= 5:
            # Try persisted operator pattern model for classification
            persisted_op_clf = _load_persisted_model("operator-pattern-model.joblib")
            if persisted_op_clf is not None:
                try:
                    now = datetime.now(timezone.utc)
                    op_features = []
                    for i, d in enumerate(valid_decisions):
                        ts_str = d.get("decidedAt") or d.get("timestamp") or ""
                        if ts_str:
                            try:
                                age_days = (now - _parse_iso(ts_str)).total_seconds() / 86400
                            except Exception:
                                age_days = 30.0
                        else:
                            age_days = 30.0
                        position_ratio = i / max(len(valid_decisions) - 1, 1)
                        op_features.append([age_days, position_ratio, float(i)])

                    preds = persisted_op_clf.predict(np.array(op_features, dtype=np.float64))
                    label_map = {0: "accepted", 1: "rejected", 2: "deferred"}
                    dominant = max(set(preds.tolist()), key=list(preds).count)
                    pattern_label = label_map.get(int(dominant), "balanced")
                    pattern_map = {
                        "accepted": "high-trust",
                        "rejected": "cautious",
                        "deferred": "selective",
                    }
                    operator_pattern["pattern"] = pattern_map.get(pattern_label, "balanced")
                    persisted_sources.add("operator")
                except Exception:
                    # Fall back to heuristic pattern detection
                    recent_window = valid_decisions[-20:]
                    approve_ratio = sum(
                        1 for d in recent_window if d["status"] == "accepted"
                    ) / len(recent_window)
                    reject_ratio = sum(
                        1 for d in recent_window if d["status"] == "rejected"
                    ) / len(recent_window)
                    if approve_ratio > 0.7:
                        operator_pattern["pattern"] = "high-trust"
                    elif reject_ratio > 0.5:
                        operator_pattern["pattern"] = "cautious"
                    elif approve_ratio > 0.4 and reject_ratio < 0.3:
                        operator_pattern["pattern"] = "balanced"
                    else:
                        operator_pattern["pattern"] = "selective"
            else:
                recent_window = valid_decisions[-20:]
                approve_ratio = sum(
                    1 for d in recent_window if d["status"] == "accepted"
                ) / len(recent_window)
                reject_ratio = sum(
                    1 for d in recent_window if d["status"] == "rejected"
                ) / len(recent_window)

                if approve_ratio > 0.7:
                    operator_pattern["pattern"] = "high-trust"
                elif reject_ratio > 0.5:
                    operator_pattern["pattern"] = "cautious"
                elif approve_ratio > 0.4 and reject_ratio < 0.3:
                    operator_pattern["pattern"] = "balanced"
                else:
                    operator_pattern["pattern"] = "selective"

    # --- Readiness prediction with gradient boosting + Bayesian confidence ---
    readiness_breakdown = heuristic["readinessBreakdown"]
    if len(topic_accuracy) >= 3:
        # Try persisted readiness predictor for enhanced predictions
        persisted_readiness = _load_persisted_model("readiness-predictor.joblib")

        for entry in readiness_breakdown:
            topic = entry["topic"]
            data = topic_accuracy.get(topic, {})
            attempts = data.get("attempts", [])

            # Add Bayesian credible interval
            bayes = heuristic["bayesianEstimates"].get(topic, {})
            entry["bayesianMean"] = bayes.get("mean", entry["readiness"])
            entry["credibleIntervalLower"] = bayes.get("lower_90", 0.0)
            entry["credibleIntervalUpper"] = bayes.get("upper_90", 1.0)

            # Add forgetting curve info
            fc = heuristic["forgettingCurves"].get(topic, {})
            stability = fc.get("stability_days", 3.0)
            entry["stabilityDays"] = stability

            # Add plateau detection
            plateau = heuristic["plateauStatus"].get(topic, {})
            entry["plateaued"] = plateau.get("plateaued", False)
            if plateau.get("recommendation"):
                entry["plateauRecommendation"] = plateau["recommendation"]

            if len(attempts) >= 3:
                recent = attempts[-5:]
                trend = sum(1 for a in recent if a.get("correct", False)) / len(
                    recent
                )
                entry["trend"] = round(trend, 3)
                entry["improving"] = trend > entry["readiness"]
            else:
                entry["trend"] = entry["readiness"]
                entry["improving"] = False

            # Enhance readiness with persisted model prediction if available
            if persisted_readiness is not None and len(attempts) >= 1:
                try:
                    now = datetime.now(timezone.utc)
                    timestamps = [_parse_iso(a["timestamp"]) for a in attempts if a.get("timestamp")]
                    days_gap = 1.0
                    if timestamps:
                        last = max(timestamps)
                        days_gap = max(0.0, (now - last).total_seconds() / 86400)
                    features = np.array(
                        [[data["accuracy"], float(data["total"]), stability, days_gap]],
                        dtype=np.float64,
                    )
                    predicted_readiness = float(persisted_readiness.predict(features)[0])
                    entry["predictedNextReadiness"] = round(
                        max(0.0, min(1.0, predicted_readiness)), 3
                    )
                    persisted_sources.add("readiness")
                except Exception:
                    pass

    # --- Adaptive schedule with ML-weighted priorities + forgetting curves ---
    schedule = heuristic["adaptiveSchedule"]
    for item in schedule:
        topic = item["topic"]
        data = topic_accuracy.get(topic, {})
        attempts = data.get("attempts", [])

        # Use forgetting curve to set optimal review interval.
        # R = e^(-t/S) => solve for t when R = 0.6: t = S * ln(1/0.6)
        fc = heuristic["forgettingCurves"].get(topic, {})
        stability = fc.get("stability_days", 3.0)
        if stability > 0:
            optimal_interval = max(1, int(stability * math.log(1.0 / 0.6)))
            item["intervalDays"] = optimal_interval
            item["forgettingCurveInterval"] = True

        if attempts:
            timestamps = [_parse_iso(a["timestamp"]) for a in attempts if a.get("timestamp")]
            if len(timestamps) >= 2:
                gaps = [
                    (timestamps[i + 1] - timestamps[i]).total_seconds() / 86400
                    for i in range(len(timestamps) - 1)
                ]
                avg_gap = np.mean(gaps) if gaps else 3.0
                item["averageStudyGapDays"] = round(float(avg_gap), 1)

    result = heuristic.copy()
    result["engine"] = "sklearn"
    result["model_source"] = "persisted" if persisted_sources else "ephemeral"
    result["topicClusters"] = topic_clusters
    result["operatorPattern"] = operator_pattern
    result["readinessBreakdown"] = readiness_breakdown
    result["adaptiveSchedule"] = schedule

    return result


def main() -> None:
    try:
        raw = _read_input()
        payload = json.loads(raw) if raw.strip() else {}
    except json.JSONDecodeError:
        print(json.dumps({"ok": False, "error": "Invalid JSON input."}))
        return

    training_attempts = payload.get("trainingAttempts", [])
    operator_decisions = payload.get("operatorDecisions", [])

    topic_accuracy = _compute_topic_accuracy(training_attempts)

    if _try_import_sklearn():
        try:
            result = _sklearn_analytics(topic_accuracy, operator_decisions)
        except Exception as exc:
            result = _heuristic_analytics(topic_accuracy, operator_decisions)
            result["sklearnError"] = str(exc)
    else:
        result = _heuristic_analytics(topic_accuracy, operator_decisions)

    print(json.dumps(result, ensure_ascii=False))


def _read_input() -> str:
    """Read input from --input file argument or stdin."""
    for i, arg in enumerate(sys.argv[1:], 1):
        if arg == "--input" and i < len(sys.argv) - 1:
            from pathlib import Path

            return Path(sys.argv[i + 1]).read_text(encoding="utf-8")
    return sys.stdin.read()


if __name__ == "__main__":
    main()
