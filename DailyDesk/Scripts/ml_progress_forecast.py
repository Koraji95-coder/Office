"""
TensorFlow-based learning progress forecasting for DailyDesk.

Analyzes time-series training data to:
- Forecast future accuracy trends per topic
- Detect learning plateaus and breakthroughs
- Estimate days to mastery for each topic area
- Generate anomaly signals (sudden drops in performance)

Input: JSON on stdin with training_history time series
Output: JSON on stdout with forecasts, plateau detection, and mastery estimates

Falls back to linear trend analysis when TensorFlow is not installed.
"""

import json
import math
import sys
from datetime import datetime, timezone, timedelta
from typing import Any


def _try_import_tensorflow() -> bool:
    try:
        import tensorflow  # noqa: F401

        return True
    except ImportError:
        return False


def _parse_iso(value: str) -> datetime:
    cleaned = value.replace("Z", "+00:00")
    try:
        return datetime.fromisoformat(cleaned)
    except ValueError:
        return datetime.now(timezone.utc)


def _build_time_series(
    attempts: list[dict[str, Any]],
) -> dict[str, list[dict[str, Any]]]:
    """Build per-topic time series from training attempts."""
    series: dict[str, list[dict[str, Any]]] = {}

    for attempt in sorted(
        attempts, key=lambda a: a.get("completedAt", "")
    ):
        timestamp = attempt.get("completedAt", "")
        if not timestamp:
            continue

        questions = attempt.get("questions", [])
        topic_scores: dict[str, dict[str, int]] = {}
        for question in questions:
            topic = question.get("topic", "general")
            if topic not in topic_scores:
                topic_scores[topic] = {"correct": 0, "total": 0}
            topic_scores[topic]["total"] += 1
            if question.get("correct", False):
                topic_scores[topic]["correct"] += 1

        for topic, scores in topic_scores.items():
            if topic not in series:
                series[topic] = []
            accuracy = scores["correct"] / scores["total"] if scores["total"] > 0 else 0.0
            series[topic].append(
                {
                    "timestamp": timestamp,
                    "accuracy": accuracy,
                    "correct": scores["correct"],
                    "total": scores["total"],
                }
            )

    return series


def _linear_regression(
    x_values: list[float], y_values: list[float]
) -> tuple[float, float]:
    """Simple linear regression returning (slope, intercept)."""
    n = len(x_values)
    if n < 2:
        return 0.0, y_values[0] if y_values else 0.0

    mean_x = sum(x_values) / n
    mean_y = sum(y_values) / n
    numerator = sum((x - mean_x) * (y - mean_y) for x, y in zip(x_values, y_values))
    denominator = sum((x - mean_x) ** 2 for x in x_values)

    if denominator == 0:
        return 0.0, mean_y

    slope = numerator / denominator
    intercept = mean_y - slope * mean_x
    return slope, intercept


def _detect_plateau(accuracies: list[float], window: int = 4) -> bool:
    """Detect if recent performance is plateauing."""
    if len(accuracies) < window:
        return False

    recent = accuracies[-window:]
    mean_recent = sum(recent) / len(recent)
    variance = sum((a - mean_recent) ** 2 for a in recent) / len(recent)
    return variance < 0.005 and mean_recent < 0.9


def _detect_anomaly(accuracies: list[float], threshold: float = 0.25) -> bool:
    """Detect sudden drops in performance."""
    if len(accuracies) < 3:
        return False

    recent = accuracies[-1]
    avg_previous = sum(accuracies[-4:-1]) / min(3, len(accuracies) - 1)
    return (avg_previous - recent) > threshold


def _heuristic_forecast(
    time_series: dict[str, list[dict[str, Any]]],
) -> dict[str, Any]:
    """Linear trend-based fallback when TensorFlow is not available."""
    forecasts = []
    plateaus = []
    anomalies = []
    mastery_estimates = []

    for topic, series in time_series.items():
        if not series:
            continue

        accuracies = [p["accuracy"] for p in series]
        n = len(accuracies)

        x_vals = list(range(n))
        slope, intercept = _linear_regression(
            [float(x) for x in x_vals], accuracies
        )

        current = accuracies[-1] if accuracies else 0.0
        predicted_next = min(1.0, max(0.0, slope * n + intercept))
        predicted_5 = min(1.0, max(0.0, slope * (n + 4) + intercept))

        forecasts.append(
            {
                "topic": topic,
                "currentAccuracy": round(current, 3),
                "predictedNextSession": round(predicted_next, 3),
                "predicted5Sessions": round(predicted_5, 3),
                "trend": "improving"
                if slope > 0.02
                else "declining"
                if slope < -0.02
                else "stable",
                "trendSlope": round(slope, 4),
                "dataPoints": n,
                "confidence": round(min(1.0, n / 10), 2),
            }
        )

        if _detect_plateau(accuracies):
            plateaus.append(
                {
                    "topic": topic,
                    "plateauAccuracy": round(current, 3),
                    "sessionsSincePlateau": min(
                        4, len(accuracies)
                    ),
                    "recommendation": "Try a different approach or change difficulty level to break through.",
                }
            )

        if _detect_anomaly(accuracies):
            avg_prev = sum(accuracies[-4:-1]) / min(3, len(accuracies) - 1)
            anomalies.append(
                {
                    "topic": topic,
                    "previousAverage": round(avg_prev, 3),
                    "currentAccuracy": round(current, 3),
                    "drop": round(avg_prev - current, 3),
                    "severity": "high" if (avg_prev - current) > 0.4 else "moderate",
                }
            )

        if current < 0.9 and slope > 0:
            remaining = (0.9 - current) / slope if slope > 0.001 else 999
            timestamps = [_parse_iso(p["timestamp"]) for p in series]
            if len(timestamps) >= 2:
                avg_gap = sum(
                    (timestamps[i + 1] - timestamps[i]).total_seconds() / 86400
                    for i in range(len(timestamps) - 1)
                ) / (len(timestamps) - 1)
            else:
                avg_gap = 3.0

            estimated_days = int(remaining * avg_gap)
            mastery_estimates.append(
                {
                    "topic": topic,
                    "currentAccuracy": round(current, 3),
                    "targetAccuracy": 0.9,
                    "estimatedSessions": int(remaining),
                    "estimatedDays": min(estimated_days, 365),
                    "confidence": round(min(1.0, n / 8), 2),
                }
            )
        elif current >= 0.9:
            mastery_estimates.append(
                {
                    "topic": topic,
                    "currentAccuracy": round(current, 3),
                    "targetAccuracy": 0.9,
                    "estimatedSessions": 0,
                    "estimatedDays": 0,
                    "confidence": round(min(1.0, n / 8), 2),
                    "mastered": True,
                }
            )

    return {
        "ok": True,
        "engine": "linear",
        "forecasts": forecasts,
        "plateaus": plateaus,
        "anomalies": anomalies,
        "masteryEstimates": mastery_estimates,
    }


def _tensorflow_forecast(
    time_series: dict[str, list[dict[str, Any]]],
) -> dict[str, Any]:
    """TensorFlow-based forecasting with simple neural network."""
    import numpy as np
    import tensorflow as tf

    heuristic = _heuristic_forecast(time_series)

    enhanced_forecasts = []
    for forecast_entry in heuristic["forecasts"]:
        topic = forecast_entry["topic"]
        series = time_series.get(topic, [])
        accuracies = [p["accuracy"] for p in series]

        if len(accuracies) >= 5:
            data = np.array(accuracies, dtype=np.float32)

            window_size = min(3, len(data) - 1)
            x_train = []
            y_train = []
            for i in range(len(data) - window_size):
                x_train.append(data[i : i + window_size])
                y_train.append(data[i + window_size])

            x_train = np.array(x_train).reshape(-1, window_size, 1)
            y_train = np.array(y_train)

            model = tf.keras.Sequential(
                [
                    tf.keras.layers.SimpleRNN(
                        16,
                        input_shape=(window_size, 1),
                        activation="tanh",
                    ),
                    tf.keras.layers.Dense(8, activation="relu"),
                    tf.keras.layers.Dense(1, activation="sigmoid"),
                ]
            )
            model.compile(optimizer="adam", loss="mse")
            model.fit(
                x_train,
                y_train,
                epochs=50,
                verbose=0,
                batch_size=min(8, len(x_train)),
            )

            last_window = data[-window_size:].reshape(1, window_size, 1)
            prediction = float(model.predict(last_window, verbose=0)[0][0])

            forecast_entry["predictedNextSession"] = round(
                min(1.0, max(0.0, prediction)), 3
            )
            forecast_entry["confidence"] = round(
                min(1.0, len(accuracies) / 8), 2
            )

        enhanced_forecasts.append(forecast_entry)

    result = heuristic.copy()
    result["engine"] = "tensorflow"
    result["forecasts"] = enhanced_forecasts

    return result


def main() -> None:
    try:
        raw = _read_input()
        payload = json.loads(raw) if raw.strip() else {}
    except json.JSONDecodeError:
        print(json.dumps({"ok": False, "error": "Invalid JSON input."}))
        return

    training_attempts = payload.get("trainingAttempts", [])
    time_series = _build_time_series(training_attempts)

    if _try_import_tensorflow():
        try:
            result = _tensorflow_forecast(time_series)
        except Exception as exc:
            result = _heuristic_forecast(time_series)
            result["tensorflowError"] = str(exc)
    else:
        result = _heuristic_forecast(time_series)

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
