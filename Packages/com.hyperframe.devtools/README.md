# com.hyperframe.devtools

In-game debug console (DV-01). Tap **DBG** (bottom-left) or press F1 / backquote.

- Level: Win, Lose, Next/Previous, +10 levels
- Economy: +1000 coins, +100 gems
- Ads: cycle mock result (Completed → Skipped → Failed), toggle No Ads
- Time: slow-mo x0.25 · Save: save now, reset progress + reboot
- Tools: FPS counter, live analytics log (AN-03), app state line (state, popups, input-lock holders)

The assembly has the define constraint `UNITY_EDITOR || DEVELOPMENT_BUILD || HF_CHEATS`, so it is not
compiled into release builds at all (DV-02), even though the package is always installed.
