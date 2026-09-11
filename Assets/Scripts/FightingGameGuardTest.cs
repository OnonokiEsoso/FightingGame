#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public class FightingGameGuardTest : MonoBehaviour
{
    FighterController a, b;
    int failures;
    void Keys(params Key[] keys) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
    void Check(bool ok, string msg) { if (ok) Debug.Log("GUARD PASS: " + msg); else { failures++; Debug.LogError("GUARD FAIL: " + msg); } }
    IEnumerator Fresh()
    {
        Keys(); SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); yield return null;
        a = GameManager.Instance.Player1; b = GameManager.Instance.Player2;
        float until = Time.unscaledTime + 5;
        while (GameManager.InputLocked && Time.unscaledTime < until) yield return null;
        yield return new WaitForSeconds(.2f);
    }
    IEnumerator Place(float x1 = -.58f, float x2 = .58f)
    {
        Keys(); a.Combat.CancelAttack(); b.Combat.CancelAttack();
        a.Body.position = new Vector2(x1, -2.09f); b.Body.position = new Vector2(x2, -2.09f);
        a.Body.linearVelocity = b.Body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(.5f);
    }
    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject); yield return Fresh(); yield return Place(-2, 2);
        Keys(Key.A, Key.RightArrow); yield return new WaitForSeconds(.2f);
        Check(a.IsGuarding && b.IsGuarding && a.State == FighterController.FighterState.Guard && a.Body.position.x < -2.8f && b.Body.position.x > 2.8f, "both players retreat while guarding");
        Keys(Key.S, Key.DownArrow); yield return new WaitForSeconds(.1f);
        float x1 = a.Body.position.x, x2 = b.Body.position.x;
        Keys(Key.S, Key.D, Key.W, Key.F, Key.DownArrow, Key.LeftArrow, Key.UpArrow, Key.J); yield return new WaitForSeconds(.15f);
        Check(a.IsCrouching && b.IsCrouching && a.IsGuarding && b.IsGuarding && !a.Combat.Busy && !b.Combat.Busy && a.Grounded && b.Grounded && Mathf.Abs(a.Body.position.x - x1) < .01f && Mathf.Abs(b.Body.position.x - x2) < .01f, "down guards in crouch and blocks movement jump attack");
        yield return Place(2, -2);
        Keys(Key.D, Key.LeftArrow); yield return new WaitForSeconds(.1f);
        Check(a.IsGuarding && b.IsGuarding && a.Body.position.x > 2 && b.Body.position.x < -2, "retreat direction updates after side swap");

        foreach (var attack in new[] { a.Combat.QuickPunch, a.Combat.Punch, a.Combat.Kick, a.Combat.ChargePunch, a.Combat.JumpAttack })
        {
            yield return Place();
            a.Guard.Consume(100); b.Guard.Restore(100);
            bool crouched = attack.Level != AttackLevel.Mid;
            Keys(crouched ? Key.DownArrow : Key.RightArrow); yield return new WaitForSeconds(.06f);
            float hp = b.Health.CurrentHP;
            if (attack == a.Combat.JumpAttack)
            { a.Body.position += Vector2.up * .5f; a.Body.linearVelocity = Vector2.zero; }
            // Exercise the real HitBox -> health/guard -> hitstop pipeline for every AttackData.
            a.Combat.HitBox.Begin(attack, attack.Damage, 1);
            float deadline = Time.unscaledTime + 2;
            while (b.Guard.CurrentGauge == 100 && Time.unscaledTime < deadline) yield return null;
            a.Combat.HitBox.End();
            float start = Time.time;
            int frames = b.Guard.StunFrames(attack.GuardStun);
            Check(Mathf.Approximately(b.Guard.CurrentGauge, 100 - attack.GuardDamage) && b.Health.CurrentHP == hp, "guard costs " + attack.GuardDamage + " without HP damage");
            Check(a.Guard.CurrentGauge == 0 && HitStop.IsActive && !CameraShake.Instance.IsShaking, "blocked hit gives no attacker gauge, short hitstop, no shake");
            Check(b.IsInGuardStun && b.IsCrouching == crouched && !b.IsInHitStun, "guard starts " + attack.GuardStun + " " + frames + "F");
            float hitX = b.Body.position.x;
            Keys(Key.LeftArrow, Key.UpArrow, Key.K); // Release down and attempt to move/jump/attack during stun.
            yield return new WaitForSeconds(FrameTiming.Seconds(frames) * .45f);
            Check(b.IsInGuardStun && b.IsCrouching == crouched && !b.CanAct && !b.Combat.Busy && b.Body.position.x > hitX && b.Body.linearVelocity.x > 0, "guard stun locks stance and input while pushback continues");
            Keys();
            while (b.IsInGuardStun && Time.unscaledTime < deadline) yield return null;
            Check(Mathf.Abs(Time.time - start - FrameTiming.Seconds(frames)) < .04f, "guard stun measured " + frames + "F = " + (Time.time - start).ToString("F3") + "s");
            yield return new WaitForSeconds(.05f);
            Check(!b.IsCrouching && b.HurtCollider.size.y == 1.8f, "crouch releases after guard stun");
        }
        yield return Place(-4, 4);
        a.Guard.Consume(100); a.Guard.Restore(50);
        Keys(Key.D); yield return new WaitForSeconds(.5f); Keys();
        Check(a.Guard.CurrentGauge > 55.5f && a.Guard.CurrentGauge < 56.5f, "manual forward movement restores about 12 gauge per second");
        float gauge = a.Guard.CurrentGauge;
        Keys(Key.A); yield return new WaitForSeconds(.2f); Keys();
        Check(Mathf.Abs(a.Guard.CurrentGauge - gauge) < .3f, "retreat guard does not regenerate");
        yield return new WaitForSeconds(.05f); gauge = a.Guard.CurrentGauge;
        yield return new WaitForSeconds(.2f);
        Check(a.Guard.CurrentGauge == gauge, "idle does not regenerate");
        a.Guard.Restore(1000); Check(a.Guard.CurrentGauge == 100, "gauge clamps at 100");

        yield return Place(); Keys(Key.DownArrow); yield return new WaitForSeconds(.05f);
        b.Guard.Consume(100); b.Guard.Restore(1);
        var result = b.Health.TakeDamage(10, 4, 1, 12, a.Combat.Punch);
        Check(result == FighterHealth.HitResult.Guarded && b.Guard.CurrentGauge == 0, "last positive gauge blocks exhausting hit");
        yield return new WaitForSeconds(.25f);
        Check(b.IsGuarding && b.IsCrouching && !b.Guard.CanBlock, "empty gauge retains crouch guard posture");
        float before = b.Health.CurrentHP;
        result = b.Health.TakeDamage(10, 4, 1, 12, a.Combat.Punch);
        Check(result == FighterHealth.HitResult.Hit && before - b.Health.CurrentHP == 10 && b.IsInHitStun && !b.IsInGuardStun && b.Body.linearVelocity.x == 4 && b.GetComponent<FighterVisualFeedback>().Sprites[0].color == Color.white, "empty gauge takes normal damage hitstun knockback flash");
        yield return new WaitForSeconds(.3f);
        b.Guard.Restore(.9f);
        Check(!b.Guard.CanBlock, "fractional gauge below one cannot block");
        b.Guard.Restore(.1f); before = b.Health.CurrentHP;
        result = b.Health.TakeDamage(10, 4, 1, 12, a.Combat.Punch);
        Check(result == FighterHealth.HitResult.Guarded && b.Health.CurrentHP == before, "one restored gauge enables guard again");
        yield return new WaitForSeconds(.3f); ScreenCapture.CaptureScreenshot("Captures/guard-gauge-empty.png");

        yield return Fresh();
        foreach (var attack in new[] { a.Combat.QuickPunch, a.Combat.Punch, a.Combat.Kick, a.Combat.ChargePunch, a.Combat.JumpAttack })
        {
            yield return Place(); a.Guard.Consume(100);
            if (attack == a.Combat.JumpAttack) a.Body.position += Vector2.up * .6f;
            float hp = b.Health.CurrentHP;
            a.Combat.HitBox.Begin(attack, attack.Damage, 1);
            float deadline = Time.unscaledTime + 2;
            while (b.Health.CurrentHP == hp && Time.unscaledTime < deadline) yield return null;
            a.Combat.HitBox.End();
            Check(a.Guard.CurrentGauge == attack.GuardRecoveryOnHit && hp - b.Health.CurrentHP == attack.Damage, "normal hit restores " + attack.GuardRecoveryOnHit + " gauge once");
            Check(HitStop.IsActive && (attack.ShakeFrames == 0 || CameraShake.Instance.IsShaking), "hitstop and strong attack shake preserved");
            yield return new WaitForSeconds(.4f);
        }
        Check(Mathf.Approximately(a.Combat.JumpAttack.Range, .765f) && Mathf.Approximately(a.Combat.JumpAttack.HitBoxHeight, .765f) && a.Combat.JumpAttack.HitBoxYOffset == -.65f, "jump hitbox shrunk 15 percent on both axes, downward offset unchanged");
        yield return Place(); b.Health.TakeDamage(b.Health.CurrentHP - 1, 0, 1); yield return new WaitForSeconds(.1f);
        a.Combat.TryAttack(a.Combat.Kick); yield return new WaitForSeconds(1.1f);
        Check(GameManager.MatchOver && b.Health.IsKO && Time.timeScale == 1, "delayed KO still completes after normal hit");
        Keys(Key.Enter); yield return new WaitForSeconds(.2f); Keys();
        a = GameManager.Instance.Player1; b = GameManager.Instance.Player2;
        Check(a.Guard.CurrentGauge == 100 && b.Guard.CurrentGauge == 100 && !a.IsInGuardStun && !b.IsInGuardStun && GameManager.Instance.Phase == GameManager.MatchPhase.PreFight, "Enter resets both gauges and guard stun before READY");
        ScreenCapture.CaptureScreenshot("Captures/guard-gauge-ready.png");
        Check(!HitStop.IsActive && !CameraShake.Instance.IsShaking && Camera.main.transform.localPosition == new Vector3(0, 0, -10), "restart restores camera and hitstop");
        Debug.Log("GUARD COMPLETE: " + failures + " failures"); Destroy(gameObject);
    }
    void OnDisable() { if (Keyboard.current != null) Keys(); }
}
#endif
