using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class FighterKeys
{
    public Key Left = Key.A, Right = Key.D, Jump = Key.W;
    public Key Crouch = Key.S;
    public Key QuickPunch = Key.F, Punch = Key.G, Kick = Key.H, ChargePunch = Key.R;
    public bool Held(Key key) => Keyboard.current != null && key != Key.None && Keyboard.current[key].isPressed;
    public bool Pressed(Key key) => Keyboard.current != null && key != Key.None && Keyboard.current[key].wasPressedThisFrame;
    public bool Released(Key key) => Keyboard.current != null && key != Key.None && Keyboard.current[key].wasReleasedThisFrame;
}

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class FighterController : MonoBehaviour
{
    public enum FighterState { Idle, Move, Jump, Attack, Hit, KO, Guard, Crouch, JumpAttack, GuardStun }
    public FighterKeys Keys = new FighterKeys();
    public FighterController Opponent;
    public Transform FacingMarker;
    public float MoveSpeed = 5, JumpSpeed = 10;
    [Range(.3f, .9f)] public float CrouchHeightRatio = .65f;
    public BoxCollider2D HurtCollider;
    public Transform StanceVisual;
    [Range(0, 1)] public float GuardKnockbackRatio = .25f;
    public int Facing { get; private set; } = 1;
    public bool Grounded { get; private set; }
    public FighterState State { get; private set; }
    public Rigidbody2D Body { get; private set; }
    public FighterHealth Health { get; private set; }
    public FighterCombat Combat { get; private set; }
    public FighterGuard Guard { get; private set; }
    public bool IsInHitStun => Time.time < hitStunUntil;
    public bool IsInGuardStun => Time.time < guardStunUntil;
    bool CanDefend => Health != null && !Health.IsDefeated && !IsInHitStun && !GameManager.InputLocked && Grounded && !Combat.Busy;
    public bool CanAct => Health != null && !Health.IsDefeated && !IsInHitStun && !IsInGuardStun && !GameManager.InputLocked;
    public bool IsGuarding => CanDefend && (IsInGuardStun || Keys.Held(Keys.Crouch) || (Opponent && HorizontalInput * OpponentDirection < 0));
    public bool IsCrouching => (CanAct && Grounded && Combat.IsCrouchAttack) || (CanDefend && (IsInGuardStun ? guardCrouched : Keys.Held(Keys.Crouch)));
    public int DirectionToOpponent => OpponentDirection;
    public bool IsAdvancing => CanAct && Grounded && !Combat.Busy && !IsCrouching && HorizontalInput * OpponentDirection > 0;
    public bool CanAttack => CanAct && !IsGuarding && !IsCrouching && !HitStop.IsActive;
    float HorizontalInput => (Keys.Held(Keys.Right) ? 1 : 0) - (Keys.Held(Keys.Left) ? 1 : 0);
    int OpponentDirection => Opponent && Mathf.Abs(Opponent.transform.position.x - transform.position.x) > .01f ? (Opponent.transform.position.x > transform.position.x ? 1 : -1) : Facing;
    float movement, hitStunUntil;
    float guardStunUntil;
    bool guardCrouched;
    BoxCollider2D bodyCollider;
    Vector2 bodySize, bodyOffset, hurtSize, hurtOffset;
    Vector3 visualScale, visualPosition;
    float stepRemaining, stepSpeed;
    int stepDirection;
    bool jumpQueued;
    readonly ContactPoint2D[] contacts = new ContactPoint2D[12];

    void Awake()
    {
        Body = GetComponent<Rigidbody2D>(); Health = GetComponent<FighterHealth>(); Combat = GetComponent<FighterCombat>();
        Guard = GetComponent<FighterGuard>();
        bodyCollider = GetComponent<BoxCollider2D>(); bodySize = bodyCollider.size; bodyOffset = bodyCollider.offset;
        if (HurtCollider) { hurtSize = HurtCollider.size; hurtOffset = HurtCollider.offset; }
        if (StanceVisual) { visualScale = StanceVisual.localScale; visualPosition = StanceVisual.localPosition; }
    }

    void Update()
    {
        if (HitStop.IsActive) return;
        movement = CanAct && !IsCrouching ? HorizontalInput : 0;
        if (CanAttack && !Combat.Busy && Keys.Pressed(Keys.Jump)) jumpQueued = true;
        if (Opponent && !Combat.Busy && CanAct)
        {
            float dx = Opponent.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > .05f) Facing = dx > 0 ? 1 : -1;
        }
        if (FacingMarker) FacingMarker.localPosition = new Vector3(Facing * .36f, .32f, 0);
    }

    void FixedUpdate()
    {
        bool wasGrounded = Grounded;
        Grounded = false;
        int count = Body.GetContacts(contacts);
        for (int i = 0; i < count; i++)
            if (contacts[i].normal.y > .6f) Grounded = true;
        if (!wasGrounded && Grounded) Combat.OnLanded();
        ApplyCrouch(IsCrouching);
        if (GameManager.MatchOver || Health.IsKO) { jumpQueued = false; State = Health.IsKO ? FighterState.KO : FighterState.Idle; return; }
        if (GameManager.InputLocked || Health.IsDefeated)
        {
            jumpQueued = false;
            State = Health.IsDefeated ? FighterState.Hit : FighterState.Idle;
            // Preserve the final hit velocity even after ordinary hit stun expires.
            return;
        }
        if (CanAct)
        {
            float horizontal = Combat.Busy ? (Combat.IsAirAttack ? Body.linearVelocity.x : AttackStepVelocity()) :
                IsCrouching ? 0 : movement * MoveSpeed;
            Body.linearVelocity = new Vector2(horizontal, Body.linearVelocity.y);
            if (jumpQueued && Grounded && !Combat.Busy && CanAttack)
            { Body.linearVelocity = new Vector2(Body.linearVelocity.x, JumpSpeed); Grounded = false; }
        }
        jumpQueued = false;
        State = IsInHitStun ? FighterState.Hit : IsInGuardStun ? FighterState.GuardStun : Combat.Busy ? (Combat.IsAirAttack ? FighterState.JumpAttack : FighterState.Attack) : IsGuarding ? FighterState.Guard : IsCrouching ? FighterState.Crouch : !Grounded ? FighterState.Jump : Mathf.Abs(movement) > 0 ? FighterState.Move : FighterState.Idle;
    }

    public void ReceiveHit(float knockback, int direction, int hitStunFrames)
    {
        Combat.CancelAttack();
        hitStunUntil = Time.time + FrameTiming.Seconds(hitStunFrames);
        jumpQueued = false;
        movement = 0;
        guardStunUntil = 0;
        ApplyCrouch(false);
        Body.linearVelocity = new Vector2(direction * knockback, 2);
    }
    public void ReceiveGuard(float knockback, int direction, int stunFrames)
    {
        guardCrouched = IsCrouching;
        jumpQueued = false;
        movement = 0;
        guardStunUntil = Time.time + FrameTiming.Seconds(stunFrames);
        Body.linearVelocity = new Vector2(direction * knockback * GuardKnockbackRatio, Body.linearVelocity.y);
    }
    void ApplyCrouch(bool crouched)
    {
        float ratio = crouched ? CrouchHeightRatio : 1;
        bodyCollider.size = new Vector2(bodySize.x, bodySize.y * ratio);
        bodyCollider.offset = bodyOffset + Vector2.down * bodySize.y * (1 - ratio) * .5f;
        if (HurtCollider)
        {
            HurtCollider.size = new Vector2(hurtSize.x, hurtSize.y * ratio);
            HurtCollider.offset = hurtOffset + Vector2.down * hurtSize.y * (1 - ratio) * .5f;
        }
        if (StanceVisual)
        {
            StanceVisual.localScale = new Vector3(visualScale.x, visualScale.y * ratio, visualScale.z);
            StanceVisual.localPosition = visualPosition + Vector3.down * bodySize.y * (1 - ratio) * .5f;
        }
    }

    public void BeginAttackStep(float distance, float duration, int direction)
    {
        stepRemaining = Grounded ? Mathf.Max(0, distance) : 0;
        stepSpeed = stepRemaining / Mathf.Max(Time.fixedDeltaTime, duration);
        stepDirection = direction;
    }
    public void EndAttackStep() { stepRemaining = 0; stepSpeed = 0; }
    float AttackStepVelocity()
    {
        if (!Grounded || (Combat.Phase != FighterCombat.AttackPhase.Startup && Combat.Phase != FighterCombat.AttackPhase.Active))
        { EndAttackStep(); return 0; }
        float distance = Mathf.Min(stepRemaining, stepSpeed * Time.fixedDeltaTime);
        stepRemaining -= distance;
        // Drive the existing continuous Rigidbody2D, never teleport the Transform.
        // Blocked distance is consumed, so a wall cannot store up a later lunge.
        return stepDirection * distance / Time.fixedDeltaTime;
    }
}
