using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

public static class FightingGameExpansionSetup
{
    [MenuItem("Tools/Simple Fighting Game/Configure Air Crouch Guard Shake")]
    public static void Configure()
    {
        foreach (var fighter in Object.FindObjectsByType<FighterController>(FindObjectsSortMode.None)) ConfigureFighter(fighter);
        foreach (var path in new[] { "Assets/Prefabs/Player1.prefab", "Assets/Prefabs/Player2.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { ConfigureFighter(root.GetComponent<FighterController>()); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        ConfigureServices(Object.FindFirstObjectByType<GameManager>());
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }
    public static void ConfigureServices(GameManager manager)
    {
        if (!manager.GetComponent<HitStop>()) manager.gameObject.AddComponent<HitStop>();
        if (!Camera.main.GetComponent<CameraShake>()) Camera.main.gameObject.AddComponent<CameraShake>();
    }
    public static void ConfigureFighter(FighterController fighter)
    {
        fighter.Keys.Crouch = fighter.name == "Player2" ? Key.DownArrow : Key.S;
        fighter.HurtCollider = fighter.GetComponentInChildren<HurtBox>().GetComponent<BoxCollider2D>();
        fighter.StanceVisual = fighter.transform.Find("Visual Root");
        var combat = fighter.GetComponent<FighterCombat>();
        combat.JumpAttack = new AttackData(12, 7, 6, 12, .9f, 4, 12) { HitBoxYOffset = -.65f, HitBoxHeight = .9f };
        foreach (var a in new[] { combat.QuickPunch, combat.Punch, combat.Kick, combat.ChargePunch, combat.JumpAttack }) a.HitStopFrames = 3;
        combat.Kick.ShakeFrames = 5; combat.Kick.ShakeStrength = .06f;
        combat.ChargePunch.ShakeFrames = 8; combat.ChargePunch.ShakeStrength = .12f;
        FightingGameGuardSetup.ConfigureFighter(fighter);
        EditorUtility.SetDirty(fighter); EditorUtility.SetDirty(combat);
    }
}
