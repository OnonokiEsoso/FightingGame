using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FightingGameFeedbackSetup
{
    [MenuItem("Tools/Simple Fighting Game/Configure Step and Feedback")]
    public static void Configure()
    {
        foreach (var fighter in Object.FindObjectsByType<FighterController>(FindObjectsSortMode.None)) ConfigureFighter(fighter);
        foreach (var path in new[] { "Assets/Prefabs/Player1.prefab", "Assets/Prefabs/Player2.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { ConfigureFighter(root.GetComponent<FighterController>()); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }
    public static void ConfigureFighter(FighterController fighter)
    {
        var combat = fighter.GetComponent<FighterCombat>();
        combat.QuickPunch.ForwardMovement = combat.Punch.ForwardMovement = 0;
        combat.Kick.ForwardMovement = .35f; combat.ChargePunch.ForwardMovement = .75f;
        var visualRoot = fighter.transform.Find("Visual Root");
        if (!visualRoot)
        {
            visualRoot = new GameObject("Visual Root").transform;
            visualRoot.SetParent(fighter.transform, false);
            fighter.transform.Find("Body Visual").SetParent(visualRoot, false);
            fighter.FacingMarker.SetParent(visualRoot, false);
        }
        var feedback = fighter.GetComponent<FighterVisualFeedback>();
        if (!feedback) feedback = fighter.gameObject.AddComponent<FighterVisualFeedback>();
        feedback.VisualRoot = visualRoot;
        feedback.Sprites = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        EditorUtility.SetDirty(combat); EditorUtility.SetDirty(feedback);
    }
}
