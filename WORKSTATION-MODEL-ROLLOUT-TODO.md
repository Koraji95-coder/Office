# Other Workstation Model Rollout

- Pull the latest `Office` repo changes.
- Pull the latest `Suite` repo changes.
- Call the Office workspace reset once so the blank-slate shared default matches this workstation.
- Run `ollama pull qwen3:8b`.
- Run `ollama list` and confirm `qwen3:8b` is installed.
- Republish `DailyDesk.Broker` from the updated Office repo.
- Restart `DailyDesk.Broker` and `Suite Runtime Control`.
- Verify the library shows `0 docs` until you intentionally import the first source.
- Verify Office broker state shows `providerReady = true`.
- Verify Office broker state shows `installedModelCount >= 1`.

## ML Pipeline Setup (Optional)

- Install Python ML libraries: `pip install scikit-learn torch tensorflow`.
- Set `enableMLPipeline` to `true` in `dailydesk.settings.json` or `dailydesk.settings.local.json`.
- Verify `POST /api/ml/analytics` returns analytics with `"ok": true`.
- Verify `POST /api/ml/pipeline` runs all three ML engines and exports artifacts.
- Verify Office broker state shows the ML section with `"enabled": true`.
- Without ML libraries installed, verify all ML endpoints still return fallback/heuristic results.
- Optionally set `mlArtifactExportPath` in settings to a custom path for Suite artifact consumption.
