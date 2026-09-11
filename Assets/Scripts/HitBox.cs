using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class HitBox : MonoBehaviour
{
    public FighterController Owner;
    public SpriteRenderer Visual;
    BoxCollider2D box;
    readonly Collider2D[] overlaps = new Collider2D[16];
    readonly HashSet<FighterHealth> hitTargets = new HashSet<FighterHealth>();
    float damage, knockback;
    int direction, hitStunFrames;
    AttackData currentAttack;
    void Awake() { box = GetComponent<BoxCollider2D>(); End(); }

    public void Begin(AttackData attack, float scaledDamage, int facing)
    {
        damage = scaledDamage; knockback = attack.Knockback; direction = facing;
        currentAttack = attack;
        hitStunFrames = attack.HitStunFrames;
        hitTargets.Clear();
        transform.localPosition = new Vector3(facing * (.45f + attack.Range * .5f), attack.HitBoxYOffset, 0);
        transform.localScale = new Vector3(attack.Range, attack.HitBoxHeight, 1);
        box.enabled = true;
        if (Visual) Visual.enabled = true;
    }

    void FixedUpdate()
    {
        if (!box.enabled || GameManager.InputLocked) return;
        var filter = new ContactFilter2D { useTriggers = true };
        int count = box.Overlap(filter, overlaps);
        for (int i = 0; i < count; i++)
        {
            var hurt = overlaps[i].GetComponent<HurtBox>();
            if (!hurt || !hurt.Health || hurt.Health == Owner.Health || hurt.Health.IsDefeated) continue;
            if (hitTargets.Add(hurt.Health))
            {
                var result = hurt.Health.TakeDamage(damage, knockback, direction, hitStunFrames, currentAttack);
                if (result != FighterHealth.HitResult.Ignored)
                {
                    int stopFrames = result == FighterHealth.HitResult.Guarded ? Mathf.CeilToInt(currentAttack.HitStopFrames * .5f) : currentAttack.HitStopFrames;
                    HitStop.Instance?.StopForFrames(stopFrames);
                    if (result == FighterHealth.HitResult.Hit)
                    {
                        Owner.Guard?.Restore(currentAttack.GuardRecoveryOnHit);
                        CameraShake.Instance?.Shake(currentAttack.ShakeFrames, currentAttack.ShakeStrength);
                    }
                }
            }
            if (GameManager.InputLocked) break;
        }
    }

    public void End()
    {
        if (!box) box = GetComponent<BoxCollider2D>();
        box.enabled = false;
        if (Visual) Visual.enabled = false;
    }
}
