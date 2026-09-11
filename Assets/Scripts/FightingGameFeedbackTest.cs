#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;

public static class FightingGameFeedbackTest
{
    static IEnumerator Place(FighterController a, FighterController b, float x1, float x2)
    {
        a.Combat.CancelAttack(); b.Combat.CancelAttack();
        a.Body.position = new Vector2(x1, -2.09f); b.Body.position = new Vector2(x2, -2.09f);
        a.Body.linearVelocity = b.Body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(.4f);
    }
    public static IEnumerator Run(FighterController a, FighterController b, Action<bool, string> check)
    {
        var visual = a.GetComponent<FighterVisualFeedback>();
        var color = visual.Sprites[0].color;
        var rotation = visual.VisualRoot.localRotation;
        foreach (int direction in new[] { 1, -1 })
        {
            foreach (var attack in new[] { a.Combat.QuickPunch, a.Combat.Punch, a.Combat.Kick, a.Combat.ChargePunch })
            {
                yield return Place(a, b, -2 * direction, 4 * direction);
                float start = a.Body.position.x;
                a.Combat.TryAttack(attack);
                check(Mathf.Abs(a.Body.position.x - start) < .001f, "attack step does not teleport");
                yield return new WaitForSeconds(FrameTiming.Seconds(attack.StartupFrames + attack.ActiveFrames + attack.RecoveryFrames) + .15f);
                float moved = (a.Body.position.x - start) * direction;
                check(Mathf.Abs(moved - attack.ForwardMovement) < .06f, "step " + attack.ForwardMovement + " direction " + direction + " measured " + moved.ToString("F3"));
            }
            yield return Place(a, b, 7.95f * direction, 10 * direction);
            a.Combat.TryAttack(a.Combat.ChargePunch);
            yield return new WaitForSeconds(.9f);
            check(Mathf.Abs(a.Body.position.x) <= 8.07f, "step cannot cross wall direction " + direction);
            yield return Place(a, b, 7 * direction, 8.04f * direction);
            var hurtCollider = b.GetComponentInChildren<HurtBox>().GetComponent<Collider2D>();
            hurtCollider.enabled = false; // Isolate body blocking from hit knockback/HP.
            a.Combat.TryAttack(a.Combat.ChargePunch);
            float until = Time.time + .9f;
            bool separated = true;
            while (Time.time < until)
            {
                separated &= (b.Body.position.x - a.Body.position.x) * direction >= .85f;
                yield return null;
            }
            check(separated, "step cannot cross opponent pinned to wall direction " + direction);
            hurtCollider.enabled = true;
        }
        yield return Place(a, b, -2, 4);
        a.Health.TakeDamage(1, 0, 1, 12);
        check(visual.Sprites[0].color == visual.HitColor && !visual.LastHitWasRecovery && Quaternion.Angle(rotation, visual.VisualRoot.localRotation) < .01f, "normal hit flashes without tilt");
        yield return new WaitForSeconds(FrameTiming.Seconds(visual.HitFlashFrames) + .04f);
        check(visual.Sprites[0].color == color && visual.Sprites[0].enabled, "normal hit restores original color and never blinks");
        foreach (int direction in new[] { 1, -1 })
        {
            yield return Place(a, b, -2, 4);
            a.Combat.TryAttack(a.Combat.Punch);
            float deadline = Time.time + 2;
            while (!a.Combat.IsRecovering && Time.time < deadline) yield return null;
            a.Health.TakeDamage(1, 4, direction, 12);
            float angle = Mathf.DeltaAngle(0, visual.VisualRoot.localEulerAngles.z);
            check(visual.LastHitWasRecovery && Mathf.Abs(angle + direction * visual.RecoveryTiltDegrees) < .1f && a.Combat.Phase == FighterCombat.AttackPhase.None && a.IsInHitStun, "recovery hit captured before cancel; tilt direction " + direction);
            check(Mathf.Abs(a.Body.rotation) < .01f && Mathf.Abs(a.transform.eulerAngles.z) < .01f, "recovery tilt leaves physics root rotation unchanged");
            UnityEngine.ScreenCapture.CaptureScreenshot("Captures/recovery-hit.png");
            yield return new WaitForSeconds(FrameTiming.Seconds(visual.RecoveryTiltFrames) + .05f);
            check(Quaternion.Angle(rotation, visual.VisualRoot.localRotation) < .01f && visual.Sprites[0].color == color, "recovery pose and color restore");
        }
        yield return Place(a, b, -2, 4);
        a.Combat.TryAttack(a.Combat.ChargePunch);
        yield return new WaitForSeconds(.06f);
        a.Health.TakeDamage(1, 4, -1, 22);
        float hitX = a.Body.position.x;
        yield return new WaitForSeconds(.15f);
        check(a.Body.position.x < hitX - .4f && a.Body.linearVelocity.x < -3 && !a.Combat.Busy && !visual.LastHitWasRecovery, "startup hit cancels forward step; knockback wins without recovery tilt");
        yield return Place(a, b, -3, 3);
    }
}
#endif
