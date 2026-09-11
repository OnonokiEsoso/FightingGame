# Simple 2D Fighting Game

Open `Assets/Scenes/SampleScene.unity`, press Play, then click the Game view.

| Action | Player1 | Player2 |
|---|---|---|
| Move | A / D | Left / Right arrows |
| Jump | W | Up arrow |
| Crouch (ground only) | S | Down arrow |
| Guard (ground only) | Hold away from opponent | Hold away from opponent |
| Jump attack | G in air | K in air |
| Crouch kick | Hold S, press G | Hold Down, press K |
| Quick punch | F | J |
| Punch | G | K |
| Kick | H | L |
| Charge punch | Hold R, release to attack | Hold U, release to attack |
| Rematch after KO | Enter | Enter |

Change keys in each player's **FighterController > Keys**. Change movement and jump speed on the same component. Change attacks and per-attack hit stun on **FighterCombat**, and maximum HP on **FighterHealth**.

| Attack | Damage | Startup (F) | Active (F) | Recovery (F) | HitStun (F) | Range | Knockback |
|---|---:|---:|---:|---:|---:|---:|---:|
| Punch | 10 | 9 | 7 | 15 | 12 | .8 | 4 |
| Kick | 16 | 15 | 8 | 21 | 16 | 1.2 | 6 |
| Quick punch | 6 | 4 | 5 | 8 | 8 | .5 | 2.5 |
| Charge punch | 12–36 | 11 | 10 | 24 | 22 | 1 | 7 |
| Jump attack | 12 | 7 | 6 | 12 | 12 | .765 | 4 |
| Crouch kick | 8 | 8 | 5 | 14 | 10 | .7 | 2.5 |

Grounded back input retreats at normal movement speed while guarding. Forward input exits guard and moves normally. Down input guards in place while crouching, including down+back. Standing guard rejects jump/attack input; crouch rejects jump and normal attacks but allows G/K to start CrouchKick. Guard direction is computed from the opponent's current position, including after a side swap. Successful blocks do zero HP damage and no normal hit stun/white flash; they cause a cyan 5F flash and 25% horizontal knockback. There is no chip damage, air guard or guard-break animation.

`AttackLevel { High, Mid, Low }` is selected through each AttackData's `Level` dropdown. Quick punch, punch, kick and charge punch are High; JumpAttack is Mid; CrouchKick is Low.

| Level | Standing guard | Crouching guard |
|---|---|---|
| High | Blocks | Blocks |
| Mid | Blocks | Takes hit |
| Low | Takes hit | Blocks |

Block evaluation checks guard posture, gauge >= 1, then the level/posture table. Failed checks take the normal damage/hit stun/knockback/visual path and never consume guard gauge or apply guard stun. Existing on-hit gauge recovery, hitstop and camera feedback use the normal-hit result. Thus a Mid jump attack breaks crouch guard and a Low crouch kick breaks standing guard.

CrouchKick requires grounded crouch with no KO/hit stun/guard stun. Its 0.7-wide, 0.45-high hitbox is centered at local Y -0.6 (leg height), with no forward movement or shake. It uses 3F hitstop, Light guard stun, guard cost 10 and normal-hit gauge recovery 6. Startup/Active/Recovery hold the crouched visual and HurtBox even if down is released; movement/jump/other attacks/guard are disabled. At recovery end it follows current down input. Hit/KO cancellation immediately clears the forced crouch-attack state.

Each fighter's `FighterGuard` owns a gauge, maximum 100, shown directly below the HP bar. The bar remains at zero and returns to 100 on Enter rematch. Manual forward movement that actually changes position restores 12 units per second (0.2 per 60 Hz step); standing still, retreating, crouching, attacking and being pushed do not restore it. Normal hits restore the attacker's gauge; blocked hits do not. All restoration clamps at the maximum.

| Attack | Guard cost | Attacker recovery on normal hit | Guard stun |
|---|---:|---:|---|
| Quick punch | 8 | 5 | Light: 6F |
| Punch | 12 | 8 | Medium: 10F |
| Kick | 18 | 12 | Medium: 10F |
| Charge punch | 25 | 18 | Heavy: 16F |
| Jump attack | 14 | 10 | Medium: 10F |
| Crouch kick | 10 | 6 | Light: 6F |

`AttackData.GuardDamage`, `GuardRecoveryOnHit` and `GuardStun` configure each attack. `FighterGuard` exposes maximum gauge, forward recovery rate and the Light/Medium/Heavy frame values. Successful blocks lock movement, jump, attacks and stance changes for the selected duration while physical pushback continues. A crouching block keeps the crouch until stun expires even if down is released. Hit stun takes priority over guard stun.

At least 1 gauge is required to block. A hit that exhausts a positive gauge is still blocked; subsequent hits at zero (or below 1) use ordinary damage, hit stun, knockback and hit visuals. Back/down still requests the guard posture with an empty gauge; restoring at least 1 enables blocking again. There is no passive recovery.

JumpAttack uses air G/K once per airborne period. It cannot start on the ground. Its hitbox points forward/down (`HitBoxYOffset = -0.65`); width and height are now 0.765, reduced 15% from 0.9. The downward offset is unchanged; horizontal center moves inward only 0.0675 due to the existing edge-based placement. Gravity and existing horizontal momentum continue. Landing cancels any remaining air attack and resets its usage. Ground attacks cannot be started in the air. JumpAttack has no step or camera shake.

Crouch scales the visual root, body collider and HurtBox height to 65%, lowering their local centers to keep the feet at the same world position. Releasing down or entering a higher-priority state restores the cached standing sizes/offsets. FighterController exposes `CrouchHeightRatio`, the HurtCollider/visual references, `IsCrouching` and `IsGuarding`. Movement, jump and attack are disabled while crouching; crouch is unavailable in the air, during attacks/hit stun or outside Fighting.

The previous project did not contain a hitstop service. `HitStop` now pauses game time for each attack's `HitStopFrames` (default 3F), then resumes via unscaled time; guarded hits use half rounded up (2F). Physics, attack waits, hit stun and KO reaction clocks pause together. Input is ignored during the stop without canceling the attacker's attack. Cleanup restores timeScale, including on scene reload.

`CameraShake.Shake(frames, strength)` offsets Main Camera relative to its cached original local position and restores that exact position afterward. It uses unscaled time so impact shake is visible during hitstop. Only successful unguarded kick (5F, 0.06 strength) and charge punch (8F, 0.12) trigger it. Misses, guards and other attacks do not. Repeated shakes combine without accumulating camera displacement; scene reload restores the original camera.

`AttackData.ForwardMovement` sets total grounded step distance: Quick punch 0, Punch 0, Kick 0.35, Charge punch 0.75 world units. The charge step starts on release, not while holding. Movement is spread across Startup/Active using the existing continuous Rigidbody2D in FixedUpdate; walls and opposing bodies still block it. Blocked distance is consumed rather than stored. No attack step is applied in the air. Recovery, attack cancellation or being hit cancels the remaining step without overwriting hit knockback. Damage and the existing frame data remain unchanged.

`FighterCombat.Phase` exposes None/Charging/Startup/Active/Recovery and `IsRecovering`. FighterHealth captures Recovery before ReceiveHit cancels the attack, then passes that fact and knockback direction to FighterVisualFeedback. `LastHitWasRecovery` and the `RecoveryHit` event are available for future effects/audio.

Each fighter has a `Visual Root` containing Body Visual and Facing Marker only. HurtBox, HitBox and the physical body remain outside it. FighterVisualFeedback caches original colors, visibility and local rotation. Normal hits flash white for `HitFlashFrames = 5`; recovery hits additionally lean the visual root 12 degrees away from the impact for `RecoveryTiltFrames = 10`. These effects do not change physical rotation, hit stun or knockback.

KO blinking starts at ConfirmKO, after the existing 39F reaction and winner reveal. Only the loser's visual sprites blink: `KOLitFrames = 6`, `KODarkFrames = 6`, `KOBlinkCount = 4`. They finish visible. Priority is KO blinking over recovery tilt over normal flash; tilt and flash can coexist before KO. KO restores the normal color/pose and rejects further hit effects. Rematch creates fresh visuals before READY. All color/tilt/blink settings are editable on FighterVisualFeedback; durations remain integer frames.

All configurable combat/intro/KO durations are integer frames. `FrameTiming.Seconds(frames)` converts at 60F = 1 second; these are time units, not Update or FixedUpdate counters. Coroutine attack waits and elapsed-time hit stun use Unity game time (so pausing game time pauses them); display/physics changes occur at the next relevant callback. Old seconds were rounded to the nearest 60 Hz frame in the scene and both prefabs.

Charge reaches its maximum at `MaxChargeFrames = 90` (1.5 seconds) and waits for release. `MaxChargeDamageMultiplier` controls maximum damage. Startup begins on release. Range measures outward from the body edge. Attacks stop horizontal movement; being hit cancels attacks and charging. Facing locks during attacks. Yellow rectangles show active hitboxes. A health target is damaged only once per attack. No draw or simultaneous-trade rules are implemented.

HitBox passes each attack's `HitStunFrames` through FighterHealth to FighterController. `IsInHitStun` exposes the time-based lock and `CanAct` blocks movement, jumping and attacks. Queued jump input is cleared on hit. Velocity is not overwritten during stun, allowing knockback, gravity and collisions to continue.

At initial start and every rematch, `GameManager.MatchPhase.PreFight` displays READY for `ReadyFrames = 60`, then FIGHT for `FightFrames = 45`. Both players' input stays locked until the text hides and Fighting begins. Physics stays active. `Match Intro Canvas/READY FIGHT` uses TextMeshProUGUI and a bundled LiberationSans font. `IntroCueChanged` is an extension point for later animation/audio.

Lethal damage applies the normal knockback, then starts `GameManager.MatchPhase.KOReaction`. Both players' input, attacks and further damage are blocked while physics continues unchanged. The defeated fighter remains in Hit state until `KOReactionFrames` expires (39F = 0.65 seconds, adjustable on GameManager). The manager then confirms KO, freezes the bodies in FixedUpdate, and shows the winner. Enter is accepted only after this final phase. `IsDefeated` means HP reached zero; `IsKO` means the reaction has completed.

`GameManager.Awake` sets `QualitySettings.vSyncCount = 0`, `Application.targetFrameRate = 60`, and `Time.fixedDeltaTime = 1f / 60f`, including on rematch. Input remains in Update; movement, hitbox physics queries and match-end body freezing run in FixedUpdate. The render setting is a target rather than a guarantee under load.

## Files and scene

- `Assets/Scripts/`: AttackData, FrameTiming, FighterController (including FighterKeys), FighterCombat, FighterHealth, HitBox, HurtBox, GameManager.
- `Assets/Scripts/FightingGameSmokeTest.cs`: Editor-only Play mode input smoke test; not attached to the saved scene or included in builds. Run from **Tools > Simple Fighting Game > Run Play Mode Smoke Test** in Edit mode. It starts automatically with the intro, tests the real keyboard path and destroys its temporary object on completion.
- `Assets/Editor/FightingGameFrameSetup.cs`: frame-default migration, TMP intro construction, test launcher. The configuration menu resets attack frame values to the defaults above; avoid rerunning it after custom tuning.
- `Assets/Scripts/FighterVisualFeedback.cs`, `FightingGameFeedbackTest.cs`: visual effects and their Editor-only Play mode regression checks.
- `Assets/Editor/FightingGameFeedbackSetup.cs`: configures step defaults and visual roots/components on the scene and both prefabs; avoid rerunning after tuning step distances.
- `Assets/Scripts/HitStop.cs`, `CameraShake.cs`, `FightingGameExpansionTest.cs`: hitstop, camera feedback and focused Editor-only Play mode regression tests.
- `Assets/Editor/FightingGameExpansionSetup.cs`: wires the scene/prefabs for crouch, air attack and impact services. Avoid rerunning after custom tuning.
- `Assets/Scripts/FighterGuard.cs`, `FightingGameGuardTest.cs`: guard gauge/stun configuration and focused Play mode regression.
- `Assets/Editor/FightingGameGuardSetup.cs`: migrates scene/prefab guard costs, recovery and air hitbox defaults. Avoid rerunning after custom tuning.
- `Assets/Editor/FightingGameAttackLevelSetup.cs`: configures default attack levels on the scene and both prefabs.
- `Assets/Scripts/FightingGameAttackLevelTest.cs`: Editor-only guard-table and crouch-kick Play mode regression.

Attack-level update verification: all High/Mid/Low versus standing/crouching combinations were checked with full and empty gauge. Focused Play mode checks confirmed that CrouchKick starts while crouched, deals exactly 8 damage through standing guard without spending defender gauge, stays crouched when down remains held, restores the standing HurtBox after release, and is rejected while standing. The full automated input regression was interrupted by input-test failures; focused checks were used for this delivery as requested. The scene and both prefabs contain the saved level and crouch-kick data.
- `Assets/TextMesh Pro/`: imported standard TMP Essential Resources (font, materials, settings and shaders).
- `Assets/Editor/FightingGameSetup.cs`: scene construction utility; refuses to duplicate an existing arena.
- `Assets/Prefabs/Player1.prefab`, `Player2.prefab`: reusable configured templates. Scene fighters are standalone objects; prefab edits do not automatically update them. Assign Opponent and GameManager references when placing new copies.
- `Assets/FightingGame/Square.png`, `FighterNoFriction.physicsMaterial2D`: placeholder sprite and zero-friction physics material.
- `Assets/Scenes/SampleScene.unity`: completed arena. Existing MCP_Test and Global Light 2D remain. Main Camera framing/background updated.
- `ProjectSettings/EditorBuildSettings.asset`: scene enabled for rematch/reload and builds.
- `Captures/fight-test.png`, `fight-ko.png`: visual verification captures.
- Unity-generated `.meta` files accompany assets and folders.

Hierarchy: Fighting Arena contains Ground, Left Wall, Right Wall, Player1, Player2, GameManager and Match Intro Canvas. Each fighter contains Body Visual, Facing Marker, HurtBox, HitBox. HP and winner UI still use GameManager.OnGUI; intro text uses the Canvas.

## Verification

Play mode smoke test passed with zero failures: gravity/landing, both players moving, body collision, jump, rejection of second airborne jump, wall containment, each normal attack dealing exactly one hit, no self damage, knockback, miss outside range, charge clamping/waiting for release, full/partial charge damage, Player2 attack/facing, KO/winner, match-end input lock.

Frame/intro regression also passed: READY and FIGHT input locks with normal landing, hidden intro and unlocked controls afterward, 8/12/16/22F per-attack stun with movement/jump/attack rejection while knockback continues, 39F delayed KO, both winners, and Enter rematch restarting READY/FIGHT. Measured stun durations were approximately 0.135/0.199/0.267/0.367 seconds; KO delay was 0.649 seconds. Scene and both prefab assets serialize integer frame fields. `Captures/ready.png` and `Captures/fight.png` show the rendered TMP intro.

Step/feedback regression checks both facing directions, exact unobstructed step distances (0, 0.35, 0.75), wall/opponent containment, normal flash with no tilt, recovery tilt direction and pose restoration, unchanged physical root rotation, interrupted step yielding to knockback, KO-only four-cycle blinking and its priority, plus both players' original color/rotation/visibility on rematch. `Captures/recovery-hit.png` shows the tilted visual child during its hit flash.

Air/crouch/guard/shake regression passed with zero failures, alongside the full existing-feature regression. The focused test checks air-only attack and reuse on landing, actual 12-damage downward hit with gravity, crouch feet/size restoration, P2 down input, back guard and side swaps, zero damage/no hit stun/cyan flash/quarter knockback, hitstop pause/resume, kick/charge shake and exact restoration, misses/repeated shakes, delayed KO and Enter resets. Console had no errors or warnings at completion. `Captures/crouch.png` and `Captures/guard.png` show the new feedback. To rerun the focused test, attach `FightingGameExpansionTest` to a temporary GameObject in Play mode; it reloads the current scene for isolated cases and removes its temporary object at completion.

Guard-gauge regression passed: both players retreat while guarding; down holds crouch guard; all five guard costs and on-hit recovery values; no attacker recovery on block; forward-only regeneration and maximum clamp; zero-gauge normal damage and reactivation at 1; Light/Medium/Heavy stun (measured about 0.101/0.167/0.266 seconds) with locked crouch and ongoing pushback; smaller air hitbox; delayed KO and gauge reset before READY. Attach `FightingGameGuardTest` to a temporary Play mode GameObject to rerun. `Captures/guard-gauge-empty.png` and `guard-gauge-ready.png` verify the gauge UI at zero and after rematch.

Manually check keyboard comfort and simultaneous key presses on your keyboard (hardware rollover varies). The game is intended for a landscape Game view. Test movement/jumps, approach the opponent, use all four attacks, verify HP reduction, then KO and press Enter to rematch.

Not implemented: throws, special/super moves, combo scaling, super meter, parry, CPU, online play, character selection, rounds, timer, animation, complex command input, dash, advanced guard options or air-combo systems. No audio or production artwork.
