using UnityEngine;

public class FighterVisualFeedback : MonoBehaviour
{
    public Transform VisualRoot;
    public SpriteRenderer[] Sprites;
    public Color HitColor = Color.white;
    public Color GuardColor = new Color(.3f, 1, 1);
    [Min(0)] public int GuardFlashFrames = 5;
    public bool IsGuardFlashing => !KOStarted && Time.time < guardUntil;
    [Min(0)] public int HitFlashFrames = 5;
    [Range(0, 30)] public float RecoveryTiltDegrees = 12;
    [Min(0)] public int RecoveryTiltFrames = 10;
    [Min(1)] public int KOLitFrames = 6;
    [Min(1)] public int KODarkFrames = 6;
    [Min(1)] public int KOBlinkCount = 4;
    public bool LastHitWasRecovery { get; private set; }
    public bool KOStarted { get; private set; }
    public bool IsBlinking => KOStarted && Time.time < blinkUntil;
    public event System.Action RecoveryHit;
    Color[] originalColors;
    bool[] originalEnabled;
    Quaternion originalRotation;
    float flashUntil, tiltUntil, blinkStart, blinkUntil, tilt;
    float guardUntil;

    void Awake()
    {
        originalRotation = VisualRoot.localRotation;
        originalColors = new Color[Sprites.Length]; originalEnabled = new bool[Sprites.Length];
        for (int i = 0; i < Sprites.Length; i++)
        { originalColors[i] = Sprites[i].color; originalEnabled[i] = Sprites[i].enabled; }
        ResetFeedback();
    }
    public void PlayHit(bool duringRecovery, int knockbackDirection)
    {
        if (KOStarted) return;
        LastHitWasRecovery = duringRecovery;
        guardUntil = 0;
        flashUntil = Time.time + FrameTiming.Seconds(HitFlashFrames);
        if (duringRecovery)
        {
            tiltUntil = Time.time + FrameTiming.Seconds(RecoveryTiltFrames);
            // A push to the right leans the head right (clockwise on Z).
            tilt = -knockbackDirection * RecoveryTiltDegrees;
            RecoveryHit?.Invoke();
        }
        ApplyFeedback();
    }
    public void PlayGuard()
    {
        if (KOStarted) return;
        guardUntil = Time.time + FrameTiming.Seconds(GuardFlashFrames);
        ApplyFeedback();
    }
    public void BeginKO()
    {
        if (KOStarted) return;
        KOStarted = true;
        flashUntil = tiltUntil = guardUntil = 0;
        blinkStart = Time.time;
        blinkUntil = blinkStart + FrameTiming.Seconds((Mathf.Max(1, KOLitFrames) + Mathf.Max(1, KODarkFrames)) * Mathf.Max(1, KOBlinkCount));
        ApplyFeedback();
    }
    void LateUpdate() => ApplyFeedback();
    void ApplyFeedback()
    {
        if (originalColors == null) return;
        bool lit = true;
        if (IsBlinking)
        {
            float cycle = FrameTiming.Seconds(Mathf.Max(1, KOLitFrames) + Mathf.Max(1, KODarkFrames));
            lit = (Time.time - blinkStart) % cycle < FrameTiming.Seconds(Mathf.Max(1, KOLitFrames));
        }
        VisualRoot.localRotation = originalRotation * Quaternion.Euler(0, 0, !KOStarted && Time.time < tiltUntil ? tilt : 0);
        for (int i = 0; i < Sprites.Length; i++)
        {
            Sprites[i].enabled = originalEnabled[i] && lit;
            Sprites[i].color = !KOStarted && Time.time < flashUntil ? HitColor : IsGuardFlashing ? GuardColor : originalColors[i];
        }
    }
    public void ResetFeedback()
    {
        KOStarted = LastHitWasRecovery = false;
        flashUntil = tiltUntil = blinkStart = blinkUntil = guardUntil = 0;
        ApplyFeedback();
    }
    void OnDisable() => ResetFeedback();
}
