using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

public static class FightingGameSetup
{
    [MenuItem("Tools/Simple Fighting Game/Build Arena")]
    public static void Build()
    {
        if (Object.FindFirstObjectByType<GameManager>()) throw new System.InvalidOperationException("An arena already exists; setup was not repeated.");
        Directory.CreateDirectory("Assets/FightingGame");
        Directory.CreateDirectory("Assets/Prefabs");
        var texture = new Texture2D(8, 8);
        var pixels = new Color[64]; for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels(pixels); texture.Apply();
        File.WriteAllBytes("Assets/FightingGame/Square.png", texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset("Assets/FightingGame/Square.png");
        var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/FightingGame/Square.png");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 8; importer.filterMode = FilterMode.Point;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FightingGame/Square.png");
        var material = new PhysicsMaterial2D("FighterNoFriction") { friction = 0, bounciness = 0 };
        AssetDatabase.CreateAsset(material, "Assets/FightingGame/FighterNoFriction.physicsMaterial2D");
        var root = new GameObject("Fighting Arena");
        CreateBlock("Ground", root.transform, sprite, new Vector2(0, -3.5f), new Vector2(18, 1), new Color(.25f, .3f, .38f), material);
        CreateBlock("Left Wall", root.transform, sprite, new Vector2(-9, 1), new Vector2(1, 10), new Color(.16f, .2f, .28f), material);
        CreateBlock("Right Wall", root.transform, sprite, new Vector2(9, 1), new Vector2(1, 10), new Color(.16f, .2f, .28f), material);
        var p1 = CreateFighter("Player1", root.transform, sprite, material, -3, new Color(.2f, .65f, 1));
        var p2 = CreateFighter("Player2", root.transform, sprite, material, 3, new Color(1, .35f, .35f));
        p2.Keys = new FighterKeys { Left = Key.LeftArrow, Right = Key.RightArrow, Jump = Key.UpArrow, QuickPunch = Key.J, Punch = Key.K, Kick = Key.L, ChargePunch = Key.U };
        PrefabUtility.SaveAsPrefabAsset(p1.gameObject, "Assets/Prefabs/Player1.prefab");
        PrefabUtility.SaveAsPrefabAsset(p2.gameObject, "Assets/Prefabs/Player2.prefab");
        p1.Opponent = p2; p2.Opponent = p1;
        var manager = new GameObject("GameManager").AddComponent<GameManager>();
        manager.transform.SetParent(root.transform); manager.Player1 = p1; manager.Player2 = p2;
        FightingGameFrameSetup.CreateIntro(manager);
        var camera = Camera.main;
        if (!camera) camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 5.5f;
        camera.transform.position = new Vector3(0, 0, -10); camera.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f, .07f, .11f);
        FightingGameExpansionSetup.ConfigureServices(manager);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int index = scenes.FindIndex(s => s.path == scene.path);
        if (index < 0) scenes.Add(new EditorBuildSettingsScene(scene.path, true)); else scenes[index].enabled = true;
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Fighting arena created and saved. Press Play to fight.");
    }

    static void CreateBlock(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size, Color color, PhysicsMaterial2D material)
    {
        var go = new GameObject(name); go.transform.SetParent(parent); go.transform.position = position;
        go.transform.localScale = new Vector3(size.x, size.y, 1);
        var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = color;
        go.AddComponent<BoxCollider2D>().sharedMaterial = material;
    }

    static FighterController CreateFighter(string name, Transform parent, Sprite sprite, PhysicsMaterial2D material, float x, Color color)
    {
        var go = new GameObject(name); go.transform.SetParent(parent); go.transform.position = new Vector3(x, -2.05f, 0);
        var rb = go.AddComponent<Rigidbody2D>(); rb.gravityScale = 3; rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var body = go.AddComponent<BoxCollider2D>(); body.size = new Vector2(.9f, 1.8f); body.sharedMaterial = material;
        var health = go.AddComponent<FighterHealth>();
        var fighter = go.AddComponent<FighterController>(); var combat = go.AddComponent<FighterCombat>();
        var visual = Child("Body Visual", go.transform, sprite, color, new Vector3(.9f, 1.8f, 1));
        visual.GetComponent<SpriteRenderer>().sortingOrder = 1;
        var eye = Child("Facing Marker", go.transform, sprite, Color.white, new Vector3(.16f, .16f, 1));
        eye.transform.localPosition = new Vector3(.36f, .32f, 0); eye.GetComponent<SpriteRenderer>().sortingOrder = 2;
        fighter.FacingMarker = eye.transform;
        var hurt = new GameObject("HurtBox"); hurt.transform.SetParent(go.transform, false);
        var hc = hurt.AddComponent<BoxCollider2D>(); hc.size = body.size; hc.isTrigger = true;
        hurt.AddComponent<HurtBox>().Health = health;
        var hit = Child("HitBox", go.transform, sprite, new Color(1, .85f, .2f, .65f), Vector3.one);
        var collider = hit.AddComponent<BoxCollider2D>(); collider.isTrigger = true; collider.enabled = false;
        var hitBox = hit.AddComponent<HitBox>(); hitBox.Owner = fighter; hitBox.Visual = hit.GetComponent<SpriteRenderer>();
        hitBox.Visual.sortingOrder = 3; hitBox.Visual.enabled = false; combat.HitBox = hitBox;
        FightingGameFeedbackSetup.ConfigureFighter(fighter);
        FightingGameExpansionSetup.ConfigureFighter(fighter);
        return fighter;
    }
    static GameObject Child(string name, Transform parent, Sprite sprite, Color color, Vector3 scale)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localScale = scale;
        var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = color;
        return go;
    }
}
