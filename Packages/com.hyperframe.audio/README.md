# com.hyperframe.audio

- `IAudioService.PlaySfx("pop")`, `PlayMusic("music_home")` (crossfade), volumes and mute (AU-01).
- Sounds are addressed by ID from a `SoundTable` asset (AU-02). Standard IDs: `SoundIds`.
- Volume/mute are persisted by the Settings service and applied by the app (AU-03).
- `PlaceholderSounds.FillMissing(table)` generates blips for missing standard IDs (AI-14).
- `MockAudioService` records calls for tests.
