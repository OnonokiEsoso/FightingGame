using System.Collections;
using UnityEngine;

[RequireComponent(typeof(FighterController))]
public class FighterCombat : MonoBehaviour
{
    public enum AttackPhase { None, Charging, Startup, Active, Recovery }
    public AttackPhase Phase { get; private set; }
    public bool IsRecovering => Phase == AttackPhase.Recovery;
    public AttackData Punch = new AttackData(10, 9, 7, 15, .8f, 4, 12);
    public AttackData Kick = new AttackData(16, 15, 8, 21, 1.2f, 6, 16) { ForwardMovement = .35f, ShakeFrames = 5, ShakeStrength = .06f, GuardDamage = 18, GuardRecoveryOnHit = 12 };
    public AttackData QuickPunch = new AttackData(6, 4, 5, 8, .5f, 2.5f, 8) { GuardDamage = 8, GuardRecoveryOnHit = 5, GuardStun = GuardStunType.Light };
    public AttackData ChargePunch = new AttackData(12, 11, 10, 24, 1, 7, 22) { ForwardMovement = .75f, ShakeFrames = 8, ShakeStrength = .12f, GuardDamage = 25, GuardRecoveryOnHit = 18, GuardStun = GuardStunType.Heavy };
    public AttackData JumpAttack = new AttackData(12, 7, 6, 12, .765f, 4, 12) { Level = AttackLevel.Mid, HitBoxYOffset = -.65f, HitBoxHeight = .765f, GuardDamage = 14, GuardRecoveryOnHit = 10 };
    public AttackData CrouchKick = new AttackData(8, 8, 5, 14, .7f, 2.5f, 10) { Level = AttackLevel.Low, HitBoxYOffset = -.6f, HitBoxHeight = .45f, GuardDamage = 10, GuardRecoveryOnHit = 6, GuardStun = GuardStunType.Light };
    public bool IsCrouchAttack { get; private set; }
    public bool AirAttackUsed { get; private set; }
    public bool IsAirAttack { get; private set; }
    [Min(1)] public int MaxChargeFrames = 90;
    [Min(1)] public float MaxChargeDamageMultiplier = 3;
    public HitBox HitBox;
    [SerializeField] Animator quickPunchAnimator;
    public bool Busy { get; private set; }
    public bool Charging { get; private set; }
    public float ChargeFraction => Mathf.Clamp01(chargeTime / FrameTiming.Seconds(Mathf.Max(1, MaxChargeFrames)));
    FighterController fighter;
    float chargeTime;

    static readonly int QuickPunchTrigger = Animator.StringToHash("QuickPunch");

    void Awake()
    {
        fighter = GetComponent<FighterController>();

        // The current humanoid visual keeps the arm Animator under this visual-only pivot.
        // Keep the Inspector field available so the reference can be replaced later without code changes.
        if (!quickPunchAnimator)
        {
            Transform armPivot = transform.Find("Visual Root/Facing Pivot/FrontArmPivot");
            if (armPivot) quickPunchAnimator = armPivot.GetComponent<Animator>();
        }

        // Fallback for older/newer prefab layouts while there is only one limb Animator.
        if (!quickPunchAnimator)
            quickPunchAnimator = GetComponentInChildren<Animator>(true);
    }

    void Update()
    {
        if (HitStop.IsActive) return;
        if (!fighter.CanAct) { CancelAttack(); return; }
        var keys = fighter.Keys;
        if (Charging)
        {
            chargeTime = Mathf.Min(chargeTime + Time.deltaTime, FrameTiming.Seconds(Mathf.Max(1, MaxChargeFrames)));
            if (keys.Released(keys.ChargePunch) || !keys.Held(keys.ChargePunch)) ReleaseCharge();
            return;
        }
        if (Busy) return;
        if (fighter.Grounded && fighter.IsCrouching)
        {
            if (keys.Pressed(keys.Punch)) TryAttack(CrouchKick);
            return;
        }
        if (!fighter.CanAttack) return;
        if (!fighter.Grounded)
        {
            if (keys.Pressed(keys.Punch)) TryAttack(JumpAttack);
            return;
        }
        if (keys.Pressed(keys.ChargePunch)) { Charging = Busy = true; Phase = AttackPhase.Charging; chargeTime = 0; }
        else if (keys.Pressed(keys.QuickPunch)) TryAttack(QuickPunch);
        else if (keys.Pressed(keys.Punch)) TryAttack(Punch);
        else if (keys.Pressed(keys.Kick)) TryAttack(Kick);
    }

    public bool TryAttack(AttackData attack)
    {
        if (Busy || attack == null || !fighter.CanAct || HitStop.IsActive) return false;
        bool crouch = attack == CrouchKick;
        if (crouch ? !fighter.Grounded || !fighter.IsCrouching : !fighter.CanAttack) return false;
        bool aerial = attack == JumpAttack;
        if (aerial ? fighter.Grounded || AirAttackUsed : !fighter.Grounded) return false;
        IsAirAttack = aerial;
        IsCrouchAttack = crouch;
        if (aerial) AirAttackUsed = true;

        if (attack == QuickPunch && quickPunchAnimator)
            quickPunchAnimator.SetTrigger(QuickPunchTrigger);

        StartCoroutine(Attack(attack, attack.Damage));
        return true;
    }

    void ReleaseCharge()
    {
        float damage = ChargePunch.Damage * Mathf.Lerp(1, MaxChargeDamageMultiplier, ChargeFraction);
        Charging = false;
        StartCoroutine(Attack(ChargePunch, damage));
    }

    IEnumerator Attack(AttackData attack, float damage)
    {
        Busy = true;
        Phase = AttackPhase.Startup;
        int facing = fighter.Facing;
        fighter.BeginAttackStep(IsCrouchAttack ? 0 : attack.ForwardMovement, FrameTiming.Seconds(attack.StartupFrames + Mathf.Max(1, attack.ActiveFrames)), facing);
        if (attack.StartupFrames > 0) yield return new WaitForSeconds(FrameTiming.Seconds(attack.StartupFrames));
        Phase = AttackPhase.Active;
        HitBox.Begin(attack, damage, facing);
        yield return new WaitForSeconds(FrameTiming.Seconds(Mathf.Max(1, attack.ActiveFrames)));
        HitBox.End();
        fighter.EndAttackStep();
        Phase = AttackPhase.Recovery;
        if (attack.RecoveryFrames > 0) yield return new WaitForSeconds(FrameTiming.Seconds(attack.RecoveryFrames));
        Busy = false;
        IsAirAttack = false;
        IsCrouchAttack = false;
        Phase = AttackPhase.None;
    }

    public void CancelAttack()
    {
        StopAllCoroutines();
        if (HitBox) HitBox.End();
        if (fighter) fighter.EndAttackStep();
        if (quickPunchAnimator) quickPunchAnimator.ResetTrigger(QuickPunchTrigger);
        Phase = AttackPhase.None;
        Busy = Charging = false; chargeTime = 0;
        IsAirAttack = false;
        IsCrouchAttack = false;
    }
    public void OnLanded()
    {
        if (IsAirAttack) CancelAttack();
        AirAttackUsed = false;
    }
    void OnDisable() => CancelAttack();
    void OnApplicationFocus(bool focus) { if (!focus) CancelAttack(); }
}
