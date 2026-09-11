using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FightingGameAttackLevelSetup
{
    [MenuItem("Tools/Simple Fighting Game/Configure Attack Levels")]
    public static void Configure()
    {
        foreach (var c in Object.FindObjectsByType<FighterCombat>(FindObjectsSortMode.None)) ConfigureCombat(c);
        foreach (var path in new[] { "Assets/Prefabs/Player1.prefab", "Assets/Prefabs/Player2.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { ConfigureCombat(root.GetComponent<FighterCombat>()); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }
    public static void ConfigureCombat(FighterCombat c)
    {
        c.QuickPunch.Level = c.Punch.Level = c.Kick.Level = c.ChargePunch.Level = AttackLevel.High;
        c.JumpAttack.Level = AttackLevel.Mid;
        c.CrouchKick.Level = AttackLevel.Low;
        EditorUtility.SetDirty(c);
    }
}
