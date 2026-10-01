---
description: Create a new popup (view + placeholder layout + test) following HyperFrame conventions
---
Create a popup named $ARGUMENTS (PascalCase, ending in "Popup").

1. Read `Packages/com.hyperframe.ui/AGENTS.md` and an existing popup in
   `Packages/com.hyperframe.app/Runtime/Views/StandardPopups.cs` as the pattern.
2. Create `Assets/_Game/Scripts/UI/<Name>.cs` (namespace `Game`), deriving from `UIPopup`:
   - `BuildPlaceholder(UIBuilder ui)`: a themed panel; children named `txt_*`, `btn_*`, `img_*`.
   - `OnCreated()`: `Bind("btn_X", () => Close("x"))` for every button. Use action-string constants.
   - `OnShow(object args)`: fill labels from a small `Args` class.
3. Show it from the caller: `var r = await ServiceLocator.Get<IUIService>().ShowPopup<Name>(args); if (r.Is("x")) ...`
4. Add a PlayMode test in `Assets/_Game/Tests/PlayMode/` that opens the popup, clicks each button with
   `AppDriver.Click<Name>("btn_X")` and asserts the result.
5. Run /verify.

Do not edit `Packages/com.hyperframe.*` for this. Colors come from the theme, never literals.
