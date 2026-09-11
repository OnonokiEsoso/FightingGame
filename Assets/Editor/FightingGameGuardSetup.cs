using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FightingGameGuardSetup
{
    [MenuItem("Tools/Simple Fighting Game/Configure Guard Gauge")]
    public static void Configure()
    {
        foreach (var f in Object.FindObjectsByType<FighterController>(FindObjectsSortMode.None)) ConfigureFighter(f);
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
        if (!fighter.GetComponent<FighterGuard>()) fighter.gameObject.AddComponent<FighterGuard>();
        var c = fighter.GetComponent<FighterCombat>();
        Set(c.QuickPunch, 8, 5, GuardStunType.Light); Set(c.Punch, 12, 8, GuardStunType.Medium);
        Set(c.Kick, 18, 12, GuardStunType.Medium); Set(c.ChargePunch, 25, 18, GuardStunType.Heavy);
        Set(c.JumpAttack, 14, 10, GuardStunType.Medium);
        c.JumpAttack.Range = .765f; c.JumpAttack.HitBoxHeight = .765f;
        FightingGameAttackLevelSetup.ConfigureCombat(c);
        EditorUtility.SetDirty(c);
    }
    static void Set(AttackData a, float damage, float recovery, GuardStunType type)
    { a.GuardDamage = damage; a.GuardRecoveryOnHit = recovery; a.GuardStun = type; }
}
