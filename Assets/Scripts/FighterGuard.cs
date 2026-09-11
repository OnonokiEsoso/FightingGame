using UnityEngine;

public enum GuardStunType { Light, Medium, Heavy }

public class FighterGuard : MonoBehaviour
{
    [Min(1)] public float MaxGauge = 100;
    [Min(0)] public float ForwardRecoveryPerSecond = 12;
    [Min(0)] public int GuardStunLight = 6;
    [Min(0)] public int GuardStunMedium = 10;
    [Min(0)] public int GuardStunHeavy = 16;
    public float CurrentGauge { get; private set; }
    public bool CanBlock => CurrentGauge >= 1;
    public bool CanBlockLevel(AttackLevel level, bool crouching) => level == AttackLevel.High || (crouching ? level == AttackLevel.Low : level == AttackLevel.Mid);
    FighterController fighter;
    Vector2 lastPosition;

    void Awake()
    {
        fighter = GetComponent<FighterController>();
        CurrentGauge = MaxGauge;
        lastPosition = transform.position;
    }
    void FixedUpdate()
    {
        Vector2 position = fighter.Body.position;
        // Require actual manual forward travel, not attacks, pushback, or pushing a wall.
        if (fighter.IsAdvancing && (position.x - lastPosition.x) * fighter.DirectionToOpponent > .0001f)
            Restore(ForwardRecoveryPerSecond * Time.fixedDeltaTime);
        lastPosition = position;
    }
    public void Restore(float amount) => CurrentGauge = Mathf.Clamp(CurrentGauge + Mathf.Max(0, amount), 0, MaxGauge);
    public void Consume(float amount) => CurrentGauge = Mathf.Max(0, CurrentGauge - Mathf.Max(0, amount));
    public int StunFrames(GuardStunType type) => type == GuardStunType.Light ? GuardStunLight : type == GuardStunType.Heavy ? GuardStunHeavy : GuardStunMedium;
}
