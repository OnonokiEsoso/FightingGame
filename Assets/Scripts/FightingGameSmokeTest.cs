#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Manually attach only in Play mode. Uses the real keyboard input path; never saved in the scene.
public class FightingGameSmokeTest : MonoBehaviour
{
    FighterController p1, p2;
    int failures;
    void Keys(params Key[] keys) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
    void Check(bool condition, string text)
    { if (!condition) { failures++; Debug.LogError("FIGHT TEST FAIL: " + text); } else Debug.Log("FIGHT TEST PASS: " + text); }
    IEnumerator CheckIntro()
    {
        var manager = GameManager.Instance;
        var x1 = p1.Body.position.x; var x2 = p2.Body.position.x;
        Check(manager.Phase == GameManager.MatchPhase.PreFight && manager.IntroText && manager.IntroText.text == "READY", "READY is TMP text at match start");
        Keys(Key.D, Key.W, Key.G, Key.LeftArrow, Key.UpArrow, Key.K);
        yield return new WaitForSeconds(.25f);
        UnityEngine.ScreenCapture.CaptureScreenshot("Captures/ready.png");
        Check(GameManager.InputLocked && !p1.CanAct && !p2.CanAct && !p1.Combat.Busy && !p2.Combat.Busy && Mathf.Abs(p1.Body.position.x - x1) < .02f && Mathf.Abs(p2.Body.position.x - x2) < .02f && p1.Grounded, "READY rejects movement jump attacks while physics lands");
        float deadline = Time.time + 3;
        while (manager.IntroText.text == "READY" && Time.time < deadline) yield return null;
        Check(manager.IntroText.text == "FIGHT" && GameManager.InputLocked, "FIGHT shown before input unlock");
        UnityEngine.ScreenCapture.CaptureScreenshot("Captures/fight.png");
        yield return new WaitForSeconds(.2f);
        Check(!p1.Combat.Busy && !p2.Combat.Busy && Mathf.Abs(p1.Body.position.x - x1) < .02f && p1.Grounded && p2.Grounded, "FIGHT continues blocking both players");
        Keys();
        while (GameManager.InputLocked && Time.time < deadline) yield return null;
        Check(manager.Phase == GameManager.MatchPhase.Fighting && !manager.IntroText.gameObject.activeSelf && p1.CanAct && p2.CanAct, "intro hides and controls unlock");
    }
    IEnumerator CheckHitStun(float beforeHP, AttackData attack)
    {
        float deadline = Time.time + 2;
        while (p2.Health.CurrentHP == beforeHP && Time.time < deadline) yield return null;
        float hitTime = Time.time;
        var hitPosition = p2.Body.position;
        Check(p2.IsInHitStun && !p2.CanAct, "hit starts " + attack.HitStunFrames + "F stun");
        Keys(Key.LeftArrow, Key.UpArrow, Key.K);
        yield return new WaitForSeconds(FrameTiming.Seconds(attack.HitStunFrames) * .45f);
        Check(p2.IsInHitStun && !p2.CanAct && !p2.Combat.Busy && p2.Body.position.x > hitPosition.x && p2.Body.linearVelocity.x > 0 && p2.Body.linearVelocity.y < 3, "stun blocks move jump attack while knockback and gravity continue");
        Keys();
        while (p2.IsInHitStun && Time.time < deadline) yield return null;
        float duration = Time.time - hitTime;
        Check(p2.CanAct && Mathf.Abs(duration - FrameTiming.Seconds(attack.HitStunFrames)) < .04f, "stun duration " + attack.HitStunFrames + "F measured " + duration.ToString("F3") + "s");
        yield return new WaitForSeconds(.7f);
    }
    IEnumerator Place(float distance = 1.15f)
    {
        Keys(); p1.Combat.CancelAttack(); p2.Combat.CancelAttack();
        p1.Body.position = new Vector2(-distance / 2, -2.09f); p2.Body.position = new Vector2(distance / 2, -2.09f);
        p1.Body.linearVelocity = p2.Body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(.4f);
    }
    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        p1 = GameManager.Instance.Player1; p2 = GameManager.Instance.Player2;
        yield return CheckIntro();
        yield return new WaitForSeconds(.6f);
        Check(Application.targetFrameRate == 60 && QualitySettings.vSyncCount == 0 && Mathf.Approximately(Time.fixedDeltaTime, 1f / 60f), "60 FPS target, VSync off, 60 Hz physics");
        Check(p1.Grounded && p2.Grounded && p1.transform.position.y > -2.2f, "gravity and ground landing");
        yield return FightingGameFeedbackTest.Run(p1, p2, Check);
        Keys(Key.D, Key.LeftArrow); yield return new WaitForSeconds(1);
        Keys(); yield return new WaitForSeconds(.1f);
        Check(p2.Body.position.x - p1.Body.position.x >= .87f, "both players move and solid bodies do not pass through");
        Check(Mathf.Abs(p1.Body.position.x) < 1, "keyboard horizontal movement");
        Keys(Key.W); yield return new WaitForSeconds(.15f); Keys(); yield return new WaitForSeconds(.04f);
        float vy = p1.Body.linearVelocity.y;
        Check(!p1.Grounded && p1.Body.position.y > -1.5f, "keyboard jump");
        Keys(Key.W); yield return new WaitForSeconds(.08f); Keys();
        Check(p1.Body.linearVelocity.y < vy, "airborne second jump rejected");
        yield return new WaitForSeconds(1);
        // Back now means guard. Put the opponent beyond the left wall to test forward wall containment.
        p2.Body.position = new Vector2(-10, -2.09f);
        Keys(Key.A); yield return new WaitForSeconds(2.2f); Keys(); yield return new WaitForSeconds(.1f);
        Check(p1.Body.position.x > -8.1f && p1.Grounded, "left arena wall and floor containment");
        foreach (var key in new[] { Key.F, Key.G, Key.H })
        {
            yield return Place();
            float hp = p2.Health.CurrentHP, self = p1.Health.CurrentHP;
            float damage = key == Key.F ? 6 : key == Key.G ? 10 : 16;
            Keys(key); yield return new WaitForSeconds(.03f); Keys();
            yield return CheckHitStun(hp, key == Key.F ? p1.Combat.QuickPunch : key == Key.G ? p1.Combat.Punch : p1.Combat.Kick);
            Check(Mathf.Approximately(hp - p2.Health.CurrentHP, damage), key + " hits exactly once");
            Check(p1.Health.CurrentHP == self, "no self damage");
            Check(p2.Body.position.x > .6f, "knockback");
        }
        yield return Place(4);
        float beforeMiss = p2.Health.CurrentHP;
        Keys(Key.G); yield return new WaitForSeconds(.03f); Keys(); yield return new WaitForSeconds(.6f);
        Check(p2.Health.CurrentHP == beforeMiss, "out of range attack misses");
        yield return Place();
        float beforeCharge = p2.Health.CurrentHP;
        Keys(Key.R); yield return new WaitForSeconds(1.7f);
        Check(p1.Combat.Charging && p1.Combat.ChargeFraction == 1 && p2.Health.CurrentHP == beforeCharge, "charge clamps and waits for release");
        Keys(); yield return CheckHitStun(beforeCharge, p1.Combat.ChargePunch);
        Check(Mathf.Approximately(beforeCharge - p2.Health.CurrentHP, 36), "full charge deals 36 on release");
        yield return Place();
        float beforeShort = p2.Health.CurrentHP;
        Keys(Key.R); yield return new WaitForSeconds(.15f); Keys(); yield return new WaitForSeconds(.85f);
        float shortDamage = beforeShort - p2.Health.CurrentHP;
        Check(shortDamage >= 12 && shortDamage < 20, "short charge scales damage below full charge");
        yield return Place();
        float p1HP = p1.Health.CurrentHP;
        Keys(Key.K); yield return new WaitForSeconds(.03f); Keys(); yield return new WaitForSeconds(.7f);
        Check(p1HP - p1.Health.CurrentHP == 10 && p2.Facing == -1, "Player2 attack input and facing");
        // Arrange a lethal real hit, then inspect the entire reaction interval.
        p2.Health.TakeDamage(p2.Health.CurrentHP - 1, 0, 1);
        yield return Place();
        Keys(Key.H);
        float deadline = Time.time + 2;
        while (!p2.Health.IsDefeated && Time.time < deadline) yield return null;
        Keys();
        float lethalTime = Time.time;
        var hitPosition = p2.Body.position;
        Check(p2.Health.IsDefeated && !p2.Health.IsKO && !GameManager.MatchOver && string.IsNullOrEmpty(GameManager.Instance.Winner), "lethal damage starts reaction without KO or winner");
        Check(p2.Body.simulated && p2.Body.linearVelocity.x > 5, "lethal hit preserves kick knockback");
        Check(!p2.GetComponent<FighterVisualFeedback>().KOStarted, "KO blink waits for confirmed KO");
        Keys(Key.A, Key.W, Key.G, Key.LeftArrow, Key.UpArrow, Key.K, Key.Enter);
        float winnerHP = p1.Health.CurrentHP;
        yield return new WaitForSeconds(.35f);
        Check(!GameManager.MatchOver && p2.Body.simulated && p2.Body.position.x > hitPosition.x + 1 && p2.Body.linearVelocity.x > 5, "knockback continues beyond normal hit stun");
        Check(!p1.CanAct && !p2.CanAct && !p1.Combat.Busy && !p2.Combat.Busy && p1.Health.CurrentHP == winnerHP && p2.Health.CurrentHP == 0 && p2.State == FighterController.FighterState.Hit, "reaction blocks both inputs, damage and early rematch; remains Hit");
        Keys();
        deadline = Time.time + 2;
        while (!GameManager.MatchOver && Time.time < deadline) yield return null;
        float delay = Time.time - lethalTime;
        Check(Mathf.Abs(delay - FrameTiming.Seconds(GameManager.Instance.KOReactionFrames)) < .06f, "winner delay matches Inspector setting: " + delay.ToString("F3") + " s");
        Check(GameManager.MatchOver && p2.Health.IsKO && GameManager.Instance.Winner == "Player1 WIN", "KO and winner");
        var koVisual = p2.GetComponent<FighterVisualFeedback>();
        Check(koVisual.KOStarted && koVisual.IsBlinking && !p1.GetComponent<FighterVisualFeedback>().KOStarted, "only defeated fighter starts KO blinking");
        bool wasLit = koVisual.Sprites[0].enabled;
        int blinkTransitions = 0;
        float blinkDeadline = Time.time + 2;
        while (koVisual.IsBlinking && Time.time < blinkDeadline)
        {
            yield return null;
            if (wasLit != koVisual.Sprites[0].enabled) { blinkTransitions++; wasLit = koVisual.Sprites[0].enabled; }
        }
        yield return null;
        Check(blinkTransitions >= 7 && koVisual.Sprites[0].enabled && !p2.Body.simulated && GameManager.Instance.Winner == "Player1 WIN", "KO flashes four times then restores visibility, match remains finished");
        koVisual.PlayHit(true, 1);
        Check(Quaternion.Angle(koVisual.VisualRoot.localRotation, Quaternion.identity) < .01f && koVisual.Sprites[0].color != koVisual.HitColor, "KO feedback priority rejects later hit flash/tilt");
        var position = p1.Body.position; float hpAfter = p1.Health.CurrentHP;
        Keys(Key.D, Key.W, Key.K); yield return new WaitForSeconds(.3f); Keys();
        Check(p1.Body.position == position && p1.Health.CurrentHP == hpAfter && !p1.Combat.Busy, "match end blocks movement and attacks");
        Keys(Key.Enter); yield return new WaitForSeconds(.2f); Keys();
        p1 = GameManager.Instance.Player1; p2 = GameManager.Instance.Player2;
        foreach (var fighter in new[] { p1, p2 })
        {
            var v = fighter.GetComponent<FighterVisualFeedback>();
            var expected = fighter == p1 ? new Color(.2f, .65f, 1) : new Color(1, .35f, .35f);
            Check(!v.KOStarted && !v.LastHitWasRecovery && v.Sprites[0].enabled && v.Sprites[0].color == expected && Quaternion.Angle(v.VisualRoot.localRotation, Quaternion.identity) < .01f, "rematch READY resets visual color rotation visibility " + fighter.name);
        }
        Check(GameManager.InputLocked && p1.Health.CurrentHP == 100 && p2.Health.CurrentHP == 100 && p1.Body.simulated && p2.Body.simulated, "Enter rematch resets health, pre-fight phase and physics");
        yield return CheckIntro();
        Check(Application.targetFrameRate == 60 && QualitySettings.vSyncCount == 0 && Mathf.Approximately(Time.fixedDeltaTime, 1f / 60f), "60 FPS settings survive rematch");
        p1.Health.TakeDamage(99, 0, -1);
        yield return Place();
        Keys(Key.K); yield return new WaitForSeconds(.03f); Keys();
        yield return new WaitForSeconds(1);
        Check(GameManager.MatchOver && p1.Health.IsKO && GameManager.Instance.Winner == "Player2 WIN", "Player2 wins after delayed KO");
        Debug.Log("FIGHT TEST COMPLETE: " + failures + " failures");
        Destroy(gameObject);
    }
    void OnDisable() { if (Keyboard.current != null) Keys(); }
}
#endif
