# Asset pipeline (Phase 1)

Planned scripts (AI-11, AI-12), invoked by the agent with ImageMagick and ffmpeg:
- `image.sh <in.png> <out-name>`: background removal (if needed) → trim → resize to POT-friendly size → name by convention → `Assets/_Game/Art/`.
- `audio.sh <in.wav> <id>`: trim silence → loudness normalize (≈ −16 LUFS SFX) → OGG → `Assets/_Game/Audio/`.

Every generated or third-party asset must get a row in `ASSET_LICENSES.md` (AI-13). Until real assets exist,
the framework generates placeholder sprites, particles and sounds in code (AI-14).
