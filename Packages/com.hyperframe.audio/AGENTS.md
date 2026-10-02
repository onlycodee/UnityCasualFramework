# AGENTS.md — com.hyperframe.audio

- Add a sound: add an entry to the game's `SoundTable` asset (`id`, clips, volume, pitch range, maxVoices). Never load clips in code.
- Play: `ServiceLocator.Get<IAudioService>().PlaySfx("my_id")`. Unknown IDs are silent and log one warning.
- New generated audio must go through `Tools/asset-pipeline` (trim, normalize) and be recorded in `ASSET_LICENSES.md`.
- Do not set volumes on the service directly from game code; change `ISettingsService` and the app applies it.
