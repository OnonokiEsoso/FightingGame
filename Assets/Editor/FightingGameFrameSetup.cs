using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class FightingGameFrameSetup
{
    [MenuItem("Tools/Simple Fighting Game/Configure Frame Data and Intro")]
    public static void Configure()
    {
        foreach (var fighter in Object.FindObjectsByType<FighterCombat>(FindObjectsSortMode.None))
        { ConfigureAttacks(fighter); EditorUtility.SetDirty(fighter); }
        foreach (string path in new[] { "Assets/Prefabs/Player1.prefab", "Assets/Prefabs/Player2.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { ConfigureAttacks(root.GetComponent<FighterCombat>()); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var manager = Object.FindFirstObjectByType<GameManager>();
        CreateIntro(manager);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.SaveScene(manager.gameObject.scene);
        AssetDatabase.SaveAssets();
    }

    static void ConfigureAttacks(FighterCombat combat)
    {
        // Explicit migration of the requested old defaults. Damage/range/knockback stay intact.
        Frames(combat.Punch, 9, 7, 15, 12);
        Frames(combat.Kick, 15, 8, 21, 16);
        Frames(combat.QuickPunch, 4, 5, 8, 8);
        Frames(combat.ChargePunch, 11, 10, 24, 22);
        combat.MaxChargeFrames = 90;
    }
    static void Frames(AttackData data, int startup, int active, int recovery, int stun)
    { data.StartupFrames = startup; data.ActiveFrames = active; data.RecoveryFrames = recovery; data.HitStunFrames = stun; }

    public static void CreateIntro(GameManager manager)
    {
        if (manager.IntroText) return;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (!font) throw new System.InvalidOperationException("Import TMP Essential Resources first.");
        var canvasObject = new GameObject("Match Intro Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(manager.transform.parent, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
        var textObject = new GameObject("READY FIGHT", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvasObject.transform, false);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = font; text.text = "READY"; text.fontSize = 88;
        text.fontStyle = FontStyles.Bold; text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1, .85f, .3f); text.raycastTarget = false;
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, .5f);
        text.rectTransform.sizeDelta = new Vector2(900, 180);
        text.rectTransform.anchoredPosition = Vector2.zero;
        manager.IntroText = text;
    }

    [MenuItem("Tools/Simple Fighting Game/Run Play Mode Smoke Test")]
    public static void RunSmokeTest()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        EditorApplication.playModeStateChanged -= StartTest;
        EditorApplication.playModeStateChanged += StartTest;
        SessionState.SetBool("FightingGame.RunSmokeTest", true);
        EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod]
    static void RestoreTestCallback()
    {
        if (SessionState.GetBool("FightingGame.RunSmokeTest", false))
            EditorApplication.playModeStateChanged += StartTest;
    }
    static void StartTest(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.playModeStateChanged -= StartTest;
        if (!SessionState.GetBool("FightingGame.RunSmokeTest", false)) return;
        SessionState.SetBool("FightingGame.RunSmokeTest", false);
        Application.runInBackground = true;
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        new GameObject("Temporary Smoke Test").AddComponent<FightingGameSmokeTest>();
    }
}
