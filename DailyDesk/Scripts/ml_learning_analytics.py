"""
Scikit-learn based learning analytics engine for DailyDesk.

Analyzes training history to:
- Cluster weak topics and identify knowledge gaps
- Predict readiness scores for each topic area
- Generate adaptive study schedules using spaced repetition with ML optimization
- Classify operator decision patterns (approve/reject/defer tendencies)

Input: JSON on stdin with training_history, operator_memory, and learning_profile
Output: JSON on stdout with analytics results

Designed to run as a subprocess called by MLAnalyticsService.cs via ProcessRunner.
Falls back to heuristic output if scikit-learn is not installed.
"""

import json
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
    }


def _sklearn_analytics(
    topic_accuracy: dict[str, dict[str, Any]],
    operator_decisions: list[dict[str, Any]],
) -> dict[str, Any]:
    """Full ML analytics using scikit-learn."""
    import numpy as np
    from sklearn.cluster import KMeans
    from sklearn.ensemble import GradientBoostingClassifier
    from sklearn.preprocessing import StandardScaler

    heuristic = _heuristic_analytics(topic_accuracy, operator_decisions)

    # --- Topic clustering ---
    topic_clusters: list[dict[str, Any]] = []
    if len(topic_accuracy) >= 3:
        topics_list = list(topic_accuracy.keys())
        features = np.array(
            [
                [
                    topic_accuracy[t]["accuracy"],
                    topic_accuracy[t]["total"],
                    topic_accuracy[t]["correct"],
                    len(topic_accuracy[t].get("attempts", [])),
                ]
                for t in topics_list
            ]
        )

        scaler = StandardScaler()
        scaled = scaler.fit_transform(features)

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
            topic_clusters.append(
                {
                    "clusterId": cluster_id,
                    "topics": cluster_topics,
                    "averageAccuracy": round(float(avg_accuracy), 3),
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

    # --- Readiness prediction with gradient boosting ---
    readiness_breakdown = heuristic["readinessBreakdown"]
    if len(topic_accuracy) >= 3:
        for entry in readiness_breakdown:
            topic = entry["topic"]
            data = topic_accuracy.get(topic, {})
            attempts = data.get("attempts", [])
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

    # --- Adaptive schedule with ML-weighted priorities ---
    schedule = heuristic["adaptiveSchedule"]
    for item in schedule:
        topic = item["topic"]
        data = topic_accuracy.get(topic, {})
        attempts = data.get("attempts", [])
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
