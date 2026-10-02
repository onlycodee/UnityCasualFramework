---
description: Add a sound effect or music track by ID
---
Add the sound $ARGUMENTS.
1. Put the processed audio file (trimmed, loudness-normalized; see `Tools/asset-pipeline/README.md`) in `Assets/_Game/Audio/`.
2. Record source, tool, prompt, date and license in `ASSET_LICENSES.md` (assets without a license entry may not ship).
3. Add an entry to the game's SoundTable asset (the one referenced by `GameDefinition.sounds`): id in snake_case, clips, volume, pitch range, max voices.
4. Play it with `ServiceLocator.Get<IAudioService>().PlaySfx("<id>")` (or `Context.Audio` inside gameplay).
5. Run /verify.
