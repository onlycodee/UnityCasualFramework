using System;
using System.Collections.Generic;
using HyperFrame.Core;
using HyperFrame.Services;
using HyperFrame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HyperFrame.App
{
    /// <summary>Home: title, current level, coins, Play / Levels / Settings (UI-03).</summary>
    public sealed class HomeScreen : UIScreen
    {
        readonly EventSubscriptions _subs = new EventSubscriptions();

        protected override void BuildPlaceholder(UIBuilder ui)
        {
            ui.Background(transform);
            var top = UIBuilder.CreateRect("TopBar", transform);
            UIBuilder.Anchor(top, new Vector2(0.5f, 1f), new Vector2(0f, -40f));
            top.sizeDelta = new Vector2(1000f, 140f);
            var coins = ui.Label(top, "txt_Coins", "0", 0, ThemeColor.Accent);
            UIBuilder.Anchor((RectTransform)coins.transform, new Vector2(1f, 0.5f), Vector2.zero);
            coins.alignment = TextAnchor.MiddleRight;
            var settings = ui.Button(top, "btn_Settings", "Opt", ThemeColor.Secondary, new Vector2(140f, 140f));
            UIBuilder.Anchor((RectTransform)settings.transform, new Vector2(0f, 0.5f), Vector2.zero);

            var column = ui.Column(transform);
            ui.Title(column, "txt_Title", "HyperFrame", ThemeColor.TextOnPrimary);
            ui.Spacer(column, 200f);
            ui.Label(column, "txt_Level", "Level 1", 0, ThemeColor.TextOnPrimary);
            ui.Button(column, "btn_Play", "PLAY", ThemeColor.Primary, new Vector2(640f, 180f));
            ui.Button(column, "btn_Levels", "Levels", ThemeColor.Secondary);
        }

        protected override void OnCreated()
        {
            Bind("btn_Play", () => ServiceLocator.Get<GameFlow>().Play());
            Bind("btn_Settings", () => UI.ShowPopup<SettingsPopup>().Forget("Home.Settings"));
            Bind("btn_Levels", () => UI.PushScreen<LevelSelectScreen>());
        }

        protected override void OnShow(object args)
        {
            if (HyperFrameApp.Instance != null) SetText("txt_Title", HyperFrameApp.Instance.Definition.gameTitle);
            SetText("txt_Level", $"Level {ServiceLocator.Get<IProgressionService>().CurrentLevelIndex + 1}");
            SetActive("btn_Levels", HyperFrameApp.Instance == null || HyperFrameApp.Instance.Definition.showLevelSelect);
            RefreshCoins();
            _subs.Add(ServiceLocator.Get<IEventBus>().Subscribe<CurrencyChangedEvent>(_ => RefreshCoins()));
        }

        protected override void OnHide() => _subs.Dispose();
        void OnDestroy() => _subs.Dispose();

        void RefreshCoins() => SetText("txt_Coins", ServiceLocator.Get<IWallet>().Get(Currencies.Coins).ToString());
    }

    /// <summary>Gameplay HUD: level, status line from the gameplay, coins, pause (UI-03).</summary>
    public sealed class GameplayScreen : UIScreen, IGameplayHud
    {
        readonly EventSubscriptions _subs = new EventSubscriptions();

        public GameplayScreen() => transition = ViewTransition.None; // gameplay world stays visible behind it

        protected override void BuildPlaceholder(UIBuilder ui)
        {
            var top = UIBuilder.CreateRect("TopBar", transform);
            UIBuilder.Anchor(top, new Vector2(0.5f, 1f), new Vector2(0f, -40f));
            top.sizeDelta = new Vector2(1000f, 140f);
            var pause = ui.Button(top, "btn_Pause", "II", ThemeColor.Secondary, new Vector2(140f, 140f));
            UIBuilder.Anchor((RectTransform)pause.transform, new Vector2(0f, 0.5f), Vector2.zero);
            var level = ui.Label(top, "txt_Level", "Level 1", 0, ThemeColor.TextOnPrimary, 500f);
            UIBuilder.Anchor((RectTransform)level.transform, new Vector2(0.5f, 0.5f), Vector2.zero);
            var coins = ui.Label(top, "txt_Coins", "0", 0, ThemeColor.Accent, 260f);
            UIBuilder.Anchor((RectTransform)coins.transform, new Vector2(1f, 0.5f), Vector2.zero);
            coins.alignment = TextAnchor.MiddleRight;
            var status = ui.Label(transform, "txt_Status", "", 0, ThemeColor.TextOnPrimary);
            UIBuilder.Anchor((RectTransform)status.transform, new Vector2(0.5f, 1f), new Vector2(0f, -200f));
        }

        protected override void OnCreated() => Bind("btn_Pause", () => ServiceLocator.Get<GameFlow>().Pause());

        protected override void OnShow(object args)
        {
            int index = args is int i ? i : 0;
            SetText("txt_Level", $"Level {index + 1}");
            SetText("txt_Status", "");
            RefreshCoins();
            _subs.Add(ServiceLocator.Get<IEventBus>().Subscribe<CurrencyChangedEvent>(_ => RefreshCoins()));
        }

        protected override void OnHide() => _subs.Dispose();
        void OnDestroy() => _subs.Dispose();

        public override bool OnBack()
        {
            ServiceLocator.Get<GameFlow>().Pause();
            return true;
        }

        public void SetStatus(string text) => SetText("txt_Status", text);

        public RectTransform CoinTarget => Get<RectTransform>("txt_Coins");

        void RefreshCoins() => SetText("txt_Coins", ServiceLocator.Get<IWallet>().Get(Currencies.Coins).ToString());
    }

    /// <summary>Grid of levels; locked ones are disabled (UI-03).</summary>
    public sealed class LevelSelectScreen : UIScreen
    {
        const int Columns = 4;
        const int MaxShown = 40;
        readonly List<Button> _buttons = new List<Button>();

        public LevelSelectScreen() => transition = ViewTransition.SlideLeft;

        protected override void BuildPlaceholder(UIBuilder ui)
        {
            ui.Background(transform);
            var title = ui.Title(transform, "txt_Title", "Levels", ThemeColor.TextOnPrimary);
            UIBuilder.Anchor((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0f, -60f));
            var back = ui.Button(transform, "btn_Back", "Back", ThemeColor.Secondary, new Vector2(300f, 120f));
            UIBuilder.Anchor((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(40f, -60f));

            var grid = UIBuilder.CreateRect("list_Levels", transform);
            UIBuilder.Anchor(grid, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f));
            grid.sizeDelta = new Vector2(900f, 1400f);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(190f, 150f);
            layout.spacing = new Vector2(20f, 20f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Columns;
            layout.childAlignment = TextAnchor.UpperCenter;
        }

        protected override void OnCreated() => Bind("btn_Back", () => UI.PopScreen());

        protected override void OnShow(object args)
        {
            var grid = Find("list_Levels");
            var flow = ServiceLocator.Get<GameFlow>();
            var progression = ServiceLocator.Get<IProgressionService>();
            int count = Mathf.Min(flow.Levels.Count, MaxShown);
            var builder = new UIBuilder(Theme);
            while (_buttons.Count < count)
            {
                int index = _buttons.Count;
                var b = builder.Button(grid, $"btn_Level_{index + 1}", (index + 1).ToString(), ThemeColor.Primary, new Vector2(190f, 150f));
                b.onClick.AddListener(() => flow.Play(index));
                _buttons.Add(b);
            }
            for (int i = 0; i < _buttons.Count; i++)
            {
                bool unlocked = progression.IsUnlocked(i);
                _buttons[i].interactable = unlocked;
                int stars = progression.GetStars(i);
                UIBuilder.SetLabel(_buttons[i].transform, unlocked ? $"{i + 1}\n{new string('*', stars)}" : "Locked");
            }
        }
    }
}
