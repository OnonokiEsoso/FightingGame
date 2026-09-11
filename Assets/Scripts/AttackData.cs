using UnityEngine;

public enum AttackLevel { High, Mid, Low }

[System.Serializable]
public class AttackData
{
    [Tooltip("High: either guard. Mid: standing guard. Low: crouching guard.")] public AttackLevel Level = AttackLevel.High;
    [Min(0)] public float Damage = 10;
    [Min(0), Tooltip("Startup in frames (60F = 1 second)")] public int StartupFrames = 9;
    [Min(1), Tooltip("Active hitbox duration in frames")] public int ActiveFrames = 7;
    [Min(0), Tooltip("Recovery in frames")] public int RecoveryFrames = 15;
    [Min(0), Tooltip("Victim's input lock duration in frames")] public int HitStunFrames = 12;
    [Min(.1f)] public float Range = .8f;
    [Min(0)] public float Knockback = 4;
    [Min(0), Tooltip("Grounded step distance during Startup/Active, in world units")] public float ForwardMovement;
    public float HitBoxYOffset = .15f;
    [Min(.1f)] public float HitBoxHeight = .7f;
    [Min(0)] public int HitStopFrames = 3;
    [Min(0)] public int ShakeFrames;
    [Min(0)] public float ShakeStrength;
    [Min(0)] public float GuardDamage = 12;
    [Min(0)] public float GuardRecoveryOnHit = 8;
    public GuardStunType GuardStun = GuardStunType.Medium;

    public AttackData(float damage, int startup, int active, int recovery, float range, float knockback, int hitStun)
    { Damage = damage; StartupFrames = startup; ActiveFrames = active; RecoveryFrames = recovery; Range = range; Knockback = knockback; HitStunFrames = hitStun; }
}
