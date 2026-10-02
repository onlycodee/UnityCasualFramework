# com.hyperframe.ui

Screen stack, popup queue, overlays, theme, safe area (UI-01, 02, 06, 08, 09, 11).

- `IUIService.ShowScreen<T>()` / `PushScreen<T>()` / `PopScreen()`
- `await IUIService.ShowPopup<T>(args, options)` → `PopupResult` (`result.Is("retry")`)
- Popups queue by priority; `PopupOptions.Stacked` shows over the current popup.
- While any popup is open the gameplay `InputLock` is held and a dimmer blocks clicks behind it.
- `Toast`, `SetLoading`, `FloatingText` on the overlay layer.
- `UITheme` asset + `ThemedGraphic` component reskins everything; `SetTheme` re-applies live.
- `SafeArea` on the Screens and Popups layers; canvas uses Expand matching so 3:4…9:21 all fit.
- Views bind children by name (`btn_`, `txt_`, `img_`), the same names the Figma pipeline produces (AI-07).
  With no prefab registered in `ViewRegistry`, the view's `BuildPlaceholder` builds a working layout in code.
