using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public enum MatchPhase { PreFight, Fighting, KOReaction, Finished }
    public static GameManager Instance { get; private set; }
    public static bool MatchOver => Instance && Instance.Finished;
    public static bool InputLocked => Instance && Instance.Phase != MatchPhase.Fighting;
    public FighterController Player1, Player2;
    [Min(0)] public int KOReactionFrames = 39;
    [Min(0)] public int ReadyFrames = 60;
    [Min(0)] public int FightFrames = 45;
    public TMP_Text IntroText;
    public event System.Action<string> IntroCueChanged;
    public MatchPhase Phase { get; private set; }
    public bool Finished => Phase == MatchPhase.Finished;
    public string Winner { get; private set; }
    FighterHealth defeatedFighter;
    float koFinishTime;
    float introStartTime;
    string introCue;
    GUIStyle label, title;

    void Awake()
    {
        Instance = this;
        Phase = MatchPhase.PreFight;
        introStartTime = Time.time;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        Time.fixedDeltaTime = 1f / 60f;
        SetIntroCue("READY");
    }
    void SetIntroCue(string cue)
    {
        if (introCue == cue) return;
        introCue = cue;
        if (IntroText) { IntroText.text = cue; IntroText.gameObject.SetActive(!string.IsNullOrEmpty(cue)); }
        IntroCueChanged?.Invoke(cue);
    }
    void OnDestroy() { if (Instance == this) Instance = null; }
    // Called after the final damage and normal hit impulse have been applied.
    public void BeginKOReaction(FighterHealth loser)
    {
        if (Phase != MatchPhase.Fighting || !loser || !loser.IsDefeated) return;
        defeatedFighter = loser;
        Phase = MatchPhase.KOReaction;
        koFinishTime = Time.time + FrameTiming.Seconds(KOReactionFrames);
        Player1.Combat.CancelAttack();
        Player2.Combat.CancelAttack();
        // Do not touch either body's velocity or simulation during the reaction.
    }
    void FixedUpdate()
    {
        if (Phase == MatchPhase.KOReaction && Time.fixedTime >= koFinishTime)
            CompleteMatch();
    }
    void CompleteMatch()
    {
        defeatedFighter.ConfirmKO();
        Phase = MatchPhase.Finished;
        Winner = defeatedFighter == Player1.Health ? "Player2 WIN" : "Player1 WIN";
        foreach (var fighter in new[] { Player1, Player2 })
        {
            fighter.Combat.CancelAttack();
            fighter.Body.linearVelocity = Vector2.zero;
            fighter.Body.simulated = false;
        }
    }
    void Update()
    {
        if (Phase == MatchPhase.PreFight)
        {
            float elapsed = Time.time - introStartTime;
            if (elapsed >= FrameTiming.Seconds(ReadyFrames) + FrameTiming.Seconds(FightFrames))
            {
                SetIntroCue("");
                Phase = MatchPhase.Fighting;
            }
            else SetIntroCue(elapsed < FrameTiming.Seconds(ReadyFrames) ? "READY" : "FIGHT");
        }
        if (Finished && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    void LateUpdate()
    {
        if (Camera.main) Camera.main.orthographicSize = Mathf.Max(5.5f, 9.5f / Camera.main.aspect);
    }
    void OnGUI()
    {
        if (!Player1 || !Player2 || !Player1.Health || !Player2.Health) return;
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            title = new GUIStyle(label) { fontSize = 42, fontStyle = FontStyle.Bold };
        }
        var previous = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
        Bar(Player1, 40, new Color(.2f, .65f, 1)); Bar(Player2, 790, new Color(1, .35f, .35f));
        GUI.Label(new Rect(510, 25, 260, 40), "SIMPLE FIGHT", label);
        GUI.Label(new Rect(20, 625, 600, 85), Controls(Player1), label);
        GUI.Label(new Rect(660, 625, 600, 85), Controls(Player2), label);
        if (Finished)
        {
            GUI.Box(new Rect(390, 240, 500, 145), "");
            GUI.Label(new Rect(390, 255, 500, 65), Winner, title);
            GUI.Label(new Rect(390, 325, 500, 40), "ENTER : REMATCH", label);
        }
        GUI.matrix = previous;
    }
    string Controls(FighterController f)
    {
        var k = f.Keys;
        return $"{f.name}  {k.Left}/{k.Right}: Move / Guard  {k.Jump}: Jump  {k.Crouch}: Crouch\n{k.QuickPunch}: Quick  {k.Punch}: Punch / Air / CrouchKick  {k.Kick}: Kick  {k.ChargePunch}: Charge";
    }
    void Bar(FighterController f, float x, Color color)
    {
        GUI.Label(new Rect(x, 15, 450, 30), $"{f.name}   {f.Health.CurrentHP:0} / {f.Health.MaxHP:0}", label);
        GUI.color = new Color(.15f, .16f, .2f); GUI.DrawTexture(new Rect(x, 50, 450, 25), Texture2D.whiteTexture);
        GUI.color = color; GUI.DrawTexture(new Rect(x, 50, 450 * f.Health.CurrentHP / f.Health.MaxHP, 25), Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (f.Guard)
        {
            GUI.color = new Color(.15f, .16f, .2f); GUI.DrawTexture(new Rect(x, 83, 450, 12), Texture2D.whiteTexture);
            GUI.color = f.Guard.CanBlock ? new Color(.25f, .9f, .8f) : new Color(1, .35f, .2f);
            GUI.DrawTexture(new Rect(x, 83, 450 * f.Guard.CurrentGauge / Mathf.Max(1, f.Guard.MaxGauge), 12), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, 97, 450, 25), $"GUARD {f.Guard.CurrentGauge:0} / {f.Guard.MaxGauge:0}", label);
        }
        if (f.Combat.Charging) GUI.Label(new Rect(x, 124, 450, 30), $"CHARGE {f.Combat.ChargeFraction:P0}", label);
    }
}
