# AGENTS.md — com.hyperframe.ui

## Make a new popup (recipe)
```csharp
using HyperFrame.UI;
public sealed class RewardPopup : UIPopup
{
    // Placeholder layout, used until a prefab is added to the ViewRegistry.
    protected override void BuildPlaceholder(UIBuilder ui)
    {
        var panel = ui.Panel(transform, new Vector2(860, 900));
        ui.Title(panel, "txt_Title", "Reward!");
        ui.Label(panel, "txt_Amount", "+0");
        ui.Button(panel, "btn_Claim", "Claim");
    }
    protected override void OnCreated() => Bind("btn_Claim", () => Close("claim"));
    protected override void OnShow(object args) => SetText("txt_Amount", $"+{(int)args}");
}
// Caller:
var result = await ServiceLocator.Get<IUIService>().ShowPopup<RewardPopup>(50);
if (result.Is("claim")) wallet.Add(Currencies.Coins, 50, "reward_popup");
```

## Rules
- Name children `btn_X`, `txt_X`, `img_X`, `list_X`. Bind with `Bind("btn_X", ...)`, set text with `SetText("txt_X", ...)`.
- Never call `SetActive` on views yourself; go through `IUIService`.
- Popups close with an action string (`Close("retry")`); callers branch on `result.Is(...)`.
- Colors come from the theme (`ThemeColor`), never hard-coded in views.
- Tests and bots press buttons with `view.Click("btn_X")`, which only works when the button is visible and interactable.

## Pitfalls
- One instance per view type. Showing a popup type that is already open queues it.
- `ShowPopup` awaits the popup *closing*, not opening.
- Back button: popups close with action `"back"` unless `closeOnBack` is false.
