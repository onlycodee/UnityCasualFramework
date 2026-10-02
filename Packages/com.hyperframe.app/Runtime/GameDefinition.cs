using HyperFrame.Audio;
using HyperFrame.Input;
using HyperFrame.Services;
using HyperFrame.UI;
using UnityEngine;

namespace HyperFrame.App
{
    /// <summary>
    /// The single declarative description of a game (AI-06). Lives at
    /// Assets/_Game/Resources/GameDefinition.asset so the app can find it at boot.
    /// Agents: read this first; most per-game changes are data changes here.
    /// </summary>
    [CreateAssetMenu(menuName = "HyperFrame/Game Definition", fileName = "GameDefinition")]
    public sealed class GameDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string gameId = "my_game";
        public string gameTitle = "My Game";
        [Tooltip("Gameplay Kits this game uses (documentation for agents and reports).")]
        public string[] kits = new string[0];

        [Header("Gameplay (the only required game code)")]
        public GameplayModule gameplay;
        [Tooltip("Optional when the GameplayModule generates its own levels.")]
        public LevelSet levels;

        [Header("Presentation")]
        public UITheme theme;
        [Tooltip("Prefabs for screens/popups. Views without a prefab use their code-built placeholder.")]
        public ViewRegistry views;
        public SoundTable sounds;
        public GestureSettingsAsset gestures;

        [Header("Economy")]
        public int startingCoins = 0;
        public int winRewardCoins = 25;
        [Tooltip("Multiplier offered on the Win popup in exchange for a rewarded ad.")]
        public int rewardedMultiplier = 2;

        [Header("Flow")]
        public bool showLevelSelect = true;
        [Tooltip("After the Win reward, start the next level instead of returning Home.")]
        public bool continueToNextLevel = false;

        [Header("Save")]
        [Tooltip("Bump when saved data changes shape, and add a migration in the GameplayModule.")]
        public int saveSchemaVersion = 1;

        [Header("Settings")]
        public string privacyPolicyUrl = "";

        [Header("Boot")]
        [Tooltip("Boot from any scene when entering Play Mode (otherwise only from the scene named 'Boot').")]
        public bool autoBootInAnyScene = false;
    }
}
