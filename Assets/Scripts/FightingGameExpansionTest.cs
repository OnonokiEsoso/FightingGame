#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public class FightingGameExpansionTest : MonoBehaviour
{
    FighterController a, b;
    int failures;
    void Keys(params Key[] keys) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
    void Check(bool ok, string message) { if (ok) Debug.Log("EXPANSION PASS: " + message); else { failures++; Debug.LogError("EXPANSION FAIL: " + message); } }
    IEnumerator ResetMatch()
    {
        Keys();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        yield return null;
        a = GameManager.Instance.Player1; b = GameManager.Instance.Player2;
        float deadline = Time.unscaledTime + 5;
        while (GameManager.InputLocked && Time.unscaledTime < deadline) yield return null;
        yield return new WaitForSeconds(.15f);
    }
    IEnumerator Place(float x1 = -.6f, float x2 = .6f)
    {
        Keys(); a.Combat.CancelAttack(); b.Combat.CancelAttack();
        a.Body.position = new Vector2(x1, -2.09f); b.Body.position = new Vector2(x2, -2.09f);
        a.Body.linearVelocity = b.Body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(.4f);
    }
    IEnumerator WaitHit(float hp)
    {
        float deadline = Time.unscaledTime + 2;
        while (b.Health.CurrentHP == hp && Time.unscaledTime < deadline) yield return null;
    }
    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        yield return ResetMatch();
        yield return Place(-2, 2);
        var standingSize = a.HurtCollider.size;
        var standingOffset = a.HurtCollider.offset;
        float feet = a.HurtCollider.bounds.min.y;
        Keys(Key.S, Key.D, Key.W, Key.F); yield return new WaitForSeconds(.2f);
        Check(a.IsCrouching && !a.Combat.Busy && a.Grounded && Mathf.Abs(a.Body.position.x + 2) < .01f, "S crouches and blocks movement jump attack");
        Check(Mathf.Abs(a.HurtCollider.size.y - standingSize.y * .65f) < .01f && Mathf.Abs(a.HurtCollider.bounds.min.y - feet) < .03f && a.StanceVisual.localScale.y == .65f, "crouch shrinks hurtbox and visual while keeping feet fixed");
        ScreenCapture.CaptureScreenshot("Captures/crouch.png");
        Keys(); yield return new WaitForSeconds(.15f);
        Check(a.HurtCollider.size == standingSize && a.HurtCollider.offset == standingOffset && a.StanceVisual.localScale == Vector3.one, "standing restores collider hurtbox and visual");
        Keys(Key.DownArrow); yield return new WaitForSeconds(.1f);
        Check(b.IsCrouching && b.HurtCollider.size.y < standingSize.y, "Player2 down arrow crouches");
        Keys(); yield return Place();

        Keys(Key.DownArrow, Key.UpArrow, Key.J); yield return new WaitForSeconds(.1f);
        Check(b.IsGuarding && b.IsCrouching && b.Grounded && !b.Combat.Busy, "crouch guard blocks jump and attack");
        float hp = b.Health.CurrentHP;
        a.Combat.TryAttack(a.Combat.Kick);
        float deadline = Time.unscaledTime + 2;
        var feedback = b.GetComponent<FighterVisualFeedback>();
        while (!feedback.IsGuardFlashing && Time.unscaledTime < deadline) yield return null;
        Check(b.Health.CurrentHP == hp && !b.IsInHitStun && feedback.IsGuardFlashing && feedback.Sprites[0].color == feedback.GuardColor, "guard takes zero damage, no hit stun, cyan guard flash");
        Check(HitStop.IsActive && !CameraShake.Instance.IsShaking, "guard uses short hitstop and no camera shake");
        Check(b.Body.linearVelocity.x > 0 && b.Body.linearVelocity.x <= a.Combat.Kick.Knockback * .26f, "guard applies quarter knockback");
        ScreenCapture.CaptureScreenshot("Captures/guard.png");
        Keys(Key.RightArrow); yield return new WaitForSeconds(.8f);
        b.Health.TakeDamage(1000, 1, 1, 60);
        Check(b.Health.CurrentHP == hp && !b.Health.IsDefeated, "guard cannot chip or KO");
        yield return Place(.6f, -.6f);
        Keys(Key.LeftArrow); yield return new WaitForSeconds(.1f);
        Check(b.IsGuarding, "back guard updates when left/right swap");
        float x = b.Body.position.x;
        Keys(Key.RightArrow); yield return new WaitForSeconds(.08f);
        Check(!b.IsGuarding && b.Body.position.x > x, "forward input exits guard and moves");
        Keys(Key.LeftArrow, Key.DownArrow); yield return new WaitForSeconds(.1f);
        Check(b.IsCrouching && b.IsGuarding, "down enables crouching guard");

        yield return ResetMatch(); yield return Place();
        Check(!a.Combat.TryAttack(a.Combat.JumpAttack), "JumpAttack rejected on ground");
        Keys(Key.W); yield return new WaitForSeconds(.12f); Keys(); yield return null;
        Check(!a.Grounded, "jump leaves ground");
        hp = b.Health.CurrentHP;
        Keys(Key.G); yield return new WaitForSeconds(.03f); Keys();
        Check(a.Combat.AirAttackUsed && a.Combat.IsAirAttack, "air G starts JumpAttack");
        float vy = a.Body.linearVelocity.y;
        Check(!a.Combat.TryAttack(a.Combat.JumpAttack), "second air attack rejected");
        yield return WaitHit(hp);
        Check(hp - b.Health.CurrentHP == 12 && !CameraShake.Instance.IsShaking, "downward air hit deals 12 once with no shake");
        Check(a.Body.linearVelocity.y < vy && !a.Grounded, "air attack continues gravity");
        float gameTime = Time.time, realTime = Time.unscaledTime;
        while (HitStop.IsActive && Time.unscaledTime < realTime + 1) yield return null;
        Check(Time.time - gameTime < .035f && Time.timeScale == 1, "hitstop pauses game time then restores it");
        deadline = Time.unscaledTime + 2;
        while (!a.Grounded && Time.unscaledTime < deadline) yield return null;
        Check(a.Grounded && !a.Combat.AirAttackUsed && !a.Combat.Busy && !a.Combat.HitBox.GetComponent<Collider2D>().enabled, "landing cancels air attack and resets usage");
        yield return Place(-2, 2);
        Keys(Key.W, Key.S); yield return new WaitForSeconds(.1f);
        Check(a.IsCrouching && a.Grounded, "crouch prevents jump");
        Keys(); yield return new WaitForSeconds(.05f);
        Keys(Key.W); yield return new WaitForSeconds(.1f); Keys(Key.S, Key.A); yield return new WaitForSeconds(.06f);
        Check(!a.IsCrouching && !a.IsGuarding && !a.Grounded, "no air crouch or air guard");
        Keys();
        Check(a.Combat.TryAttack(a.Combat.JumpAttack), "next jump can attack again");
        yield return new WaitForSeconds(.8f);

        yield return ResetMatch(); yield return Place();
        hp = b.Health.CurrentHP; a.Combat.TryAttack(a.Combat.Kick); yield return WaitHit(hp);
        float kickStrength = CameraShake.Instance.CurrentStrength;
        Check(CameraShake.Instance.IsShaking && Mathf.Approximately(kickStrength, .06f), "kick hit starts 5F small shake");
        yield return new WaitForSecondsRealtime(.2f);
        Check(!CameraShake.Instance.IsShaking && Camera.main.transform.localPosition == CameraShake.Instance.RestPosition, "camera returns exactly after kick");
        yield return Place();
        hp = b.Health.CurrentHP;
        Keys(Key.R); yield return new WaitForSeconds(1.55f); Keys(); yield return WaitHit(hp);
        Check(CameraShake.Instance.IsShaking && CameraShake.Instance.CurrentStrength > kickStrength && hp - b.Health.CurrentHP == 36, "full charge hit has stronger 8F shake and unchanged damage");
        yield return new WaitForSeconds(.9f);
        yield return Place(-3, 3);
        a.Combat.TryAttack(a.Combat.Kick); yield return new WaitForSeconds(.35f);
        Check(!CameraShake.Instance.IsShaking, "miss does not shake");
        CameraShake.Instance.Shake(8, .1f); CameraShake.Instance.Shake(8, .2f);
        yield return new WaitForSecondsRealtime(.2f);
        Check(Camera.main.transform.localPosition == CameraShake.Instance.RestPosition, "repeated shake never accumulates displacement");

        yield return Place();
        b.Health.TakeDamage(b.Health.CurrentHP - 1, 0, 1);
        yield return new WaitForSeconds(.1f);
        hp = b.Health.CurrentHP; a.Combat.TryAttack(a.Combat.Kick); yield return WaitHit(hp);
        Check(GameManager.Instance.Phase == GameManager.MatchPhase.KOReaction && b.Body.simulated, "lethal hit retains KO reaction with physics");
        yield return new WaitForSeconds(.75f);
        Check(GameManager.MatchOver && b.Health.IsKO && Time.timeScale == 1, "hitstop does not break delayed KO");
        Keys(Key.Enter); yield return new WaitForSeconds(.2f); Keys();
        a = GameManager.Instance.Player1; b = GameManager.Instance.Player2;
        Check(GameManager.Instance.Phase == GameManager.MatchPhase.PreFight && !a.IsCrouching && !a.IsGuarding && !a.Combat.AirAttackUsed && a.HurtCollider.size.y == 1.8f && a.StanceVisual.localScale == Vector3.one, "Enter READY resets crouch guard and air use");
        Check(Camera.main.transform.localPosition == new Vector3(0, 0, -10) && !CameraShake.Instance.IsShaking && !HitStop.IsActive && Time.timeScale == 1, "rematch resets camera and hitstop");
        Check(Application.targetFrameRate == 60 && QualitySettings.vSyncCount == 0 && Mathf.Approximately(Time.fixedDeltaTime, 1f / 60f), "60 FPS settings preserved");
        Debug.Log("EXPANSION COMPLETE: " + failures + " failures");
        Destroy(gameObject);
    }
    void OnDisable() { if (Keyboard.current != null) Keys(); }
}
#endif
