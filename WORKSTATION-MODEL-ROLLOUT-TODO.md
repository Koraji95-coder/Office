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
