# Art Bible — Pixel Loop (AI-10)

Every image prompt for this game must include the **Style prompt** below.

- **Mood**: glossy toy-like pieces on a deep violet night background; the picture board is a light card so
  the pixel art pops.
- **Background**: vertical gradient #382F6B → #171433 with faint drifting bokeh.
- **Board**: #F5F2FC card on a #2B2649 conveyor track, soft drop shadow.
- **Pixels**: rounded squares with a top-left bevel highlight; colours come from each level's palette.
- **Shooters**: glossy balls in the shooter colour, darker outline and barrel, bold white ammo number
  (dark ink on light colours).
- **Style prompt**: "glossy 2D casual game asset, soft rounded shapes, subtle top-left highlight, gentle
  bevel, no text, centered, transparent background"
- **Camera**: orthographic, front view.
- Every sprite in the current build is generated in code (`PixelLoopArt`); replacing one with a drawn asset
  needs an `ASSET_LICENSES.md` entry.

## 3D version
- **Pieces**: glossy toy plastic (`PixelLoop/Toy`: smoothness ~0.6, soft rim light), rounded-edge voxels,
  turrets with a belly band, barrel and two eyes; dark disc badges with white ammo numbers.
- **Light**: warm key light from the upper left behind the camera with soft shadows, violet trilight ambient,
  fog fading the ground into the #1A1436 background.
- **Camera**: perspective, 56° pitch, 34° FOV, framed to fit under the HUD.
