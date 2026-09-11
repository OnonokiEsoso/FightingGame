using UnityEngine;

public class FighterHealth : MonoBehaviour
{
    public enum HitResult { Ignored, Hit, Guarded }
    [Min(1)] public float MaxHP = 100;
    public float CurrentHP { get; private set; }
    public bool IsDefeated => CurrentHP <= 0;
    public bool IsKO { get; private set; }
    void Awake() => CurrentHP = MaxHP;

    public HitResult TakeDamage(float damage, float knockback, int direction, int hitStunFrames = 0, AttackData attack = null)
    {
        if (IsDefeated || GameManager.InputLocked) return HitResult.Ignored;
        var fighter = GetComponent<FighterController>();
        if (fighter.IsGuarding && fighter.Guard && fighter.Guard.CanBlock &&
            fighter.Guard.CanBlockLevel(attack != null ? attack.Level : AttackLevel.High, fighter.IsCrouching))
        {
            fighter.ReceiveGuard(knockback, direction, fighter.Guard.StunFrames(attack != null ? attack.GuardStun : GuardStunType.Medium));
            // The hit that exhausts a positive gauge is blocked; the next hit is not.
            fighter.Guard.Consume(attack != null ? attack.GuardDamage : 12);
            GetComponent<FighterVisualFeedback>()?.PlayGuard();
            return HitResult.Guarded;
        }
        bool duringRecovery = fighter.Combat.IsRecovering;
        CurrentHP = Mathf.Max(0, CurrentHP - Mathf.Max(0, damage));
        fighter.ReceiveHit(knockback, direction, hitStunFrames);
        GetComponent<FighterVisualFeedback>()?.PlayHit(duringRecovery, direction);
        if (IsDefeated && GameManager.Instance) GameManager.Instance.BeginKOReaction(this);
        return HitResult.Hit;
    }

    public void ConfirmKO()
    {
        if (!IsDefeated || IsKO) return;
        IsKO = true;
        GetComponent<FighterVisualFeedback>()?.BeginKO();
    }
}
