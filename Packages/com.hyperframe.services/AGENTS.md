# AGENTS.md — com.hyperframe.services

## Persisting game data
```csharp
[Serializable] public class BoosterData { public int undo = 3; public int hint = 1; }
var save = ServiceLocator.Get<ISaveService>();
var data = save.Get<BoosterData>("boosters");   // new BoosterData() when absent
data.undo--; save.Set("boosters", data);         // autosaved on pause/quit; call save.Save() at checkpoints
```
Changing the shape of saved data? Bump `GameDefinition.saveSchemaVersion` and add an `ISaveMigration`
(from the old version) in the game's `GameplayModule.GetSaveMigrations()`. Add an EditMode test that
loads an old JSON string and checks the migrated values.

## Analytics
- Use the typed helpers: `analytics.LevelStart(...)`, `analytics.BoosterUse(...)`.
- New event? Add it to `AnalyticsSchema.Required` (framework) or `AnalyticsSchema.RegisterCustom` (game) first.

## Money
- `wallet.Add(Currencies.Coins, 50, "level_win")` / `wallet.TrySpend(...)`. Always pass a `source`.

## Ads / IAP in tests
- `ServiceLocator.Get<IAdsService>() as MockAdsService` → set `NextResult = AdResult.Skipped` to test the skip path.

## Pitfalls
- Section objects are copies: change them, then `Set` them back.
- Never store UnityEngine.Object references in save data.
