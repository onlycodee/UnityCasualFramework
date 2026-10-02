using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperFrame.App.Editor
{
    /// <summary>
    /// "HyperFrame/Setup Project": makes sure the Boot scene exists and is first in Build Settings,
    /// and that Assets/_Game/Resources/GameDefinition.asset exists. Safe to run repeatedly.
    /// Batchmode: Unity -batchmode -quit -projectPath . -executeMethod HyperFrame.App.Editor.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        public const string BootScenePath = "Assets/_Game/Scenes/Boot.unity";
        public const string DefinitionPath = "Assets/_Game/Resources/GameDefinition.asset";

        [MenuItem("HyperFrame/Setup Project", priority = 0)]
        public static void Run()
        {
            EnsureBootScene();
            EnsureBuildSettings();
            EnsureDefinition();
            AssetDatabase.SaveAssets();
            Debug.Log("[HyperFrame] Project setup complete: Boot scene, build settings and GameDefinition are in place.");
        }

        static void EnsureBootScene()
        {
            if (File.Exists(BootScenePath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(BootScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Empty on purpose: HyperFrameApp boots itself in the scene named "Boot" and creates camera, UI and services.
            EditorSceneManager.SaveScene(scene, BootScenePath);
        }

        static void EnsureBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != BootScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(BootScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void EnsureDefinition()
        {
            if (AssetDatabase.LoadAssetAtPath<GameDefinition>(DefinitionPath) != null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(DefinitionPath));
            var def = ScriptableObject.CreateInstance<GameDefinition>();
            AssetDatabase.CreateAsset(def, DefinitionPath);
            Debug.LogWarning("[HyperFrame] Created an empty GameDefinition. Assign a GameplayModule before pressing Play.");
        }

        [MenuItem("HyperFrame/Open Boot Scene", priority = 1)]
        static void OpenBoot()
        {
            if (File.Exists(BootScenePath)) EditorSceneManager.OpenScene(BootScenePath);
        }

        [MenuItem("HyperFrame/Select Game Definition", priority = 2)]
        static void SelectDefinition() =>
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameDefinition>(DefinitionPath);

        [MenuItem("HyperFrame/Clear Save Data", priority = 20)]
        static void ClearSave()
        {
            var dir = Path.Combine(Application.persistentDataPath, "hyperframe");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Debug.Log($"[HyperFrame] Deleted {dir}");
        }
    }
}
