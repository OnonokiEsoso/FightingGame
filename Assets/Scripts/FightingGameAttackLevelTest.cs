#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public class FightingGameAttackLevelTest : MonoBehaviour
{
    FighterController a, b;
    int failures;
    void Keys(params Key[] keys) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
    void Check(bool ok, string msg) { if (ok) Debug.Log("LEVEL PASS: " + msg); else { failures++; Debug.LogError("LEVEL FAIL: " + msg); } }
    IEnumerator Fresh()
    {
        Keys(); SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); yield return null;
        a = GameManager.Instance.Player1; b = GameManager.Instance.Player2;
        while (GameManager.InputLocked) yield return null;
        yield return new WaitForSeconds(.2f);
    }
    IEnumerator Place()
    {
        Keys(); a.Combat.CancelAttack(); b.Combat.CancelAttack();
        a.Body.position = new Vector2(-.58f, -2.09f); b.Body.position = new Vector2(.58f, -2.09f);
        a.Body.linearVelocity = b.Body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(.4f);
    }
    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject); yield return Fresh();
        Check(a.Combat.QuickPunch.Level == AttackLevel.High && a.Combat.Punch.Level == AttackLevel.High && a.Combat.Kick.Level == AttackLevel.High && a.Combat.ChargePunch.Level == AttackLevel.High && a.Combat.JumpAttack.Level == AttackLevel.Mid && a.Combat.CrouchKick.Level == AttackLevel.Low, "all six attack levels configured");
        foreach (bool empty in new[] { false, true })
        foreach (bool crouch in new[] { false, true })
        foreach (AttackLevel level in new[] { AttackLevel.High, AttackLevel.Mid, AttackLevel.Low })
        {
            yield return Fresh(); yield return Place();
            b.MoveSpeed = 0; // Hold standing guard without moving out of this matrix fixture.
            Keys(crouch ? Key.DownArrow : Key.RightArrow); yield return new WaitForSeconds(.05f);
            if (empty) b.Guard.Consume(100);
            var attack = new AttackData(8, 1, 3, 1, .7f, 2.5f, 10) { Level = level, GuardDamage = 10 };
            float hp = b.Health.CurrentHP, gauge = b.Guard.CurrentGauge;
            var result = b.Health.TakeDamage(attack.Damage, attack.Knockback, 1, attack.HitStunFrames, attack);
            bool blocked = !empty && (level == AttackLevel.High || (crouch ? level == AttackLevel.Low : level == AttackLevel.Mid));
            Check((result == FighterHealth.HitResult.Guarded) == blocked, level + " vs " + (crouch ? "crouch" : "stand") + " gauge " + gauge);
            if (blocked)
                Check(b.Health.CurrentHP == hp && b.Guard.CurrentGauge == gauge - 10 && b.IsInGuardStun && !b.IsInHitStun, "valid block spends gauge and applies guard stun");
            else
                Check(b.Health.CurrentHP == hp - 8 && b.Guard.CurrentGauge == gauge && b.IsInHitStun && !b.IsInGuardStun && b.Body.linearVelocity.x == 2.5f && b.GetComponent<FighterVisualFeedback>().Sprites[0].color == Color.white, "failed block has normal damage stun knockback flash and no gauge cost");
        }
        yield return Fresh(); yield return Place();
        Check(!a.Combat.TryAttack(a.Combat.CrouchKick), "crouch kick rejected while standing");
        Keys(Key.W); yield return new WaitForSeconds(.12f); Keys(Key.S); yield return null;
        Check(!a.Combat.TryAttack(a.Combat.CrouchKick), "crouch kick rejected in air");
        yield return new WaitForSeconds(.8f); yield return Place();
        Keys(Key.S); yield return new WaitForSeconds(.06f);
        Check(!a.Combat.TryAttack(a.Combat.Punch) && !a.Combat.TryAttack(a.Combat.QuickPunch) && !a.Combat.TryAttack(a.Combat.Kick), "crouch rejects normal attacks");
        float x = a.Body.position.x, hpBefore = b.Health.CurrentHP;
        b.MoveSpeed = 0;
        Keys(Key.S, Key.G, Key.RightArrow); yield return new WaitForSeconds(.03f);
        Check(a.Combat.IsCrouchAttack && a.Combat.Busy && !a.IsGuarding, "S plus G starts crouch kick and disables own guard");
        Keys(Key.D, Key.W, Key.H, Key.RightArrow);
        float until = Time.unscaledTime + 2;
        while (a.Combat.Phase == FighterCombat.AttackPhase.Startup && Time.unscaledTime < until) yield return null;
        Check(a.IsCrouching && a.Combat.HitBox.transform.localPosition.y == -.6f && a.HurtCollider.size.y < 1.8f, "crouch kick holds low pose and low hitbox after down release");
        ScreenCapture.CaptureScreenshot("Captures/crouch-kick.png");
        while (a.Combat.Busy && Time.unscaledTime < until)
        {
            Check(a.IsCrouching && a.Grounded && Mathf.Abs(a.Body.position.x - x) < .02f, "startup active recovery stay crouched and stationary");
            yield return null;
        }
        Keys(); yield return new WaitForSeconds(.06f);
        Check(hpBefore - b.Health.CurrentHP == 8 && b.Guard.CurrentGauge == 100, "actual low kick hits standing guard once without guard gauge loss");
        Check(!a.IsCrouching && a.HurtCollider.size.y == 1.8f, "released down stands after recovery");
        yield return Place(); b.Guard.Restore(100); a.Guard.Consume(100);
        Keys(Key.S, Key.DownArrow); yield return new WaitForSeconds(.06f);
        hpBefore = b.Health.CurrentHP;
        Keys(Key.S, Key.G, Key.DownArrow); yield return new WaitForSeconds(.03f); Keys(Key.S, Key.DownArrow);
        yield return new WaitForSeconds(.7f);
        Check(b.Health.CurrentHP == hpBefore && b.Guard.CurrentGauge == 90 && a.Guard.CurrentGauge == 0, "actual crouch kick blocked by crouch guard with cost 10");
        Check(a.IsCrouching && !a.Combat.Busy, "held down remains crouching after recovery");
        yield return Place();
        Keys(Key.DownArrow, Key.K); yield return new WaitForSeconds(.03f); Keys(Key.DownArrow);
        Check(b.Combat.IsCrouchAttack, "Player2 down plus K starts crouch kick");
        b.Health.TakeDamage(1, 2, 1, 12);
        Check(b.IsInHitStun && !b.Combat.IsCrouchAttack && !b.Combat.Busy, "hit stun interrupts crouch kick");
        yield return new WaitForSeconds(.4f);
        yield return Fresh(); yield return Place();
        Keys(Key.DownArrow); yield return new WaitForSeconds(.05f);
        // Mid geometry goes through the same HitBox path as the jump attack.
        a.Body.position += Vector2.up * .5f;
        a.Combat.HitBox.Begin(a.Combat.JumpAttack, 12, 1);
        yield return new WaitForSeconds(.06f); a.Combat.HitBox.End();
        Check(b.Health.CurrentHP == 88 && b.Guard.CurrentGauge == 100 && b.IsInHitStun, "actual Mid jump hitbox breaks crouch guard");
        yield return new WaitForSeconds(.5f); yield return Place();
        Keys(Key.S); yield return new WaitForSeconds(.05f);
        b.Health.TakeDamage(b.Health.CurrentHP - 1, 0, 1);
        Keys(Key.S, Key.G); yield return new WaitForSeconds(.03f); Keys(Key.S);
        yield return new WaitForSeconds(1);
        Check(GameManager.MatchOver && b.Health.IsKO && Time.timeScale == 1, "crouch kick supports existing delayed KO");
        Keys(Key.Enter); yield return new WaitForSeconds(.2f); Keys();
        a = GameManager.Instance.Player1; b = GameManager.Instance.Player2;
        Check(!a.Combat.IsCrouchAttack && !a.IsCrouching && a.Guard.CurrentGauge == 100 && GameManager.Instance.Phase == GameManager.MatchPhase.PreFight, "rematch resets crouch attack state");
        Debug.Log("LEVEL COMPLETE: " + failures + " failures"); Destroy(gameObject);
    }
    void OnDisable() { if (Keyboard.current != null) Keys(); }
}
#endif
