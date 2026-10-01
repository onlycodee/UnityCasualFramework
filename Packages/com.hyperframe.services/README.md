# com.hyperframe.services (L2)

Every service is an interface with a working mock (AR-2), so the Editor and tests run with no SDKs.

| Service | Interface | Implementations | PRD |
|---|---|---|---|
| Save | `ISaveService` | `SaveService` over `FileSaveStorage` / `InMemorySaveStorage`; versioned migrations; autosave on pause/quit | SV-01 |
| Settings | `ISettingsService` | `SettingsService` (observables persisted to save) | ST-01 |
| Economy | `IWallet` | `Wallet` + `CurrencyChangedEvent` | EC-01 |
| Progression | `IProgressionService` | `ProgressionService` (current level, unlocks, stars) | PG-01 |
| Levels | `ILevelProvider`, `LevelSet`, `LevelAsset` | `LevelSetProvider`, `FuncLevelProvider`, endless loop | LV-01, LV-06 |
| Level tools | `ILevelGenerator<T>`, `ILevelSolver<T,M>` | `LevelBatchValidator` + CSV/Markdown report | LV-02/03/07 (interfaces) |
| Analytics | `IAnalyticsService` | `AnalyticsDispatcher`, `AnalyticsSchema` (Appendix C), memory/log backends | AN-01, AN-02 |
| Ads | `IAdsService` | `MockAdsService` (Complete/Skip/Fail) | AD-03 |
| IAP | `IIAPService` | `MockIAPService` | IAP-01 (mock) |
| Consent | `IConsentService` | `MockConsentService` | CN-01 (mock) |
| Remote config | `IRemoteConfig` | `LocalRemoteConfig` | RC-01 (local) |

Real SDK adapters (MAX/LevelPlay, Firebase, Unity IAP, UMP/ATT) are Phase 2 and go in separate
packages, e.g. `com.hyperframe.services.ads.max`, so a game only pulls the SDKs it uses.
