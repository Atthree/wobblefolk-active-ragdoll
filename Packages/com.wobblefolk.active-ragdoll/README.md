# Wobblefolk Active Ragdoll

![Unity](https://img.shields.io/badge/Unity-6000.3%2B-black?logo=unity)
![Physics](https://img.shields.io/badge/Physics-PhysX-blue)
![License](https://img.shields.io/badge/License-MIT-green)

**[English](#english)** · **[Türkçe](#türkçe)**

---

<a id="english"></a>

# English

A fully physics-driven **active ragdoll** character for Unity 6. It stands, walks, runs, jumps, reaches with
independent arms, grabs anything its hands touch, carries objects with real weight, and climbs ledges. It uses
**no animation clips and no Animator**: every pose comes from joint motors and forces.

The goal is the wobbly, physical feel of games like *Human: Fall Flat*, implemented from scratch with clean,
inspector-tunable components. No code or assets from any game or third-party project are included.

## Table of contents

1. [Features](#features)
2. [Requirements](#requirements)
3. [Installation](#installation)
4. [Quick start](#quick-start)
5. [Controls](#controls)
6. [How it works](#how-it-works)
7. [Component reference](#component-reference)
8. [Profile reference](#profile-reference)
9. [Events](#events)
10. [Scripting and NPCs](#scripting-and-npcs)
11. [Physics settings and jitter](#physics-settings-and-jitter)
12. [Why ConfigurableJoint](#why-configurablejoint)
13. [Performance and many characters](#performance-and-many-characters)
14. [Humanoid setup tool](#humanoid-setup-tool)
15. [Troubleshooting](#troubleshooting)
16. [Known limitations](#known-limitations)
17. [Project structure](#project-structure)
18. [Credits and license](#credits-and-license)

## Features

| Area | What you get |
|---|---|
| **Body** | 15-bone humanoid ragdoll (Rigidbody + ConfigurableJoint). Mass, joint limits and muscle strength per bone from a ScriptableObject. |
| **Balance** | PD-controlled upright and facing torques on the pelvis and chest, a spring that holds the pelvis at stand height above the ground, ground and foot contact probes. |
| **States** | `Normal`, `Fallen`, `GettingUp`, `Unconscious`. Falls from tilt or hard impacts, knock-outs from head hits, automatic get-up. |
| **Locomotion** | Camera-relative walking and sprinting driven by capped forces, a procedural gait cycle on hip / knee / ankle targets, arm swing, lean into acceleration, stair step-up, jump with coyote time and input buffering. |
| **Arms** | Left and right arms are independent. A held button makes that arm reach for the point the camera looks at; reaching low bends the body over. |
| **Grabbing** | The hand grabs the first surface it touches with a breakable ConfigurableJoint, on rigidbodies and on static colliders. Releasing the button lets go. |
| **Carrying** | Capacity per holding hand. Strain slows you down, disables sprint, weakens balance and makes you lean back. Overloaded hands slip. |
| **Climbing** | Hang from a ledge, pull up, then let go and mantle over the edge. |
| **Interaction** | Light objects are pushed by plain physics. `Breakable` raises an event when hit hard enough and can swap in a fractured prefab. |
| **Tooling** | One-click setup for any Humanoid model, a primitive test character, and a Playground sample scene. |
| **Tuning** | Every number lives in a ScriptableObject profile or the Inspector, and changes apply live in Play mode. |

## Requirements

- **Unity 6000.3** or newer (built and tested on 6000.3.25f1, PhysX).
- **Input System** package 1.20+. It is a dependency and installs automatically. Set *Project Settings > Player > Active Input Handling* to **Input System Package** or **Both**.
- **Render pipeline:** the core code is pipeline-agnostic. The sample's materials use the active pipeline's default shader (URP Lit in this repository).

## Installation

**Option A: clone this repository.** It is a complete Unity project. Open it in Unity 6000.3+; the package is
embedded under `Packages/com.wobblefolk.active-ragdoll`.

**Option B: add the package to your own project from Git.** In *Window > Package Manager > + > Add package from git
URL…* enter:

```
https://github.com/Atthree/wobblefolk-active-ragdoll.git?path=/Packages/com.wobblefolk.active-ragdoll
```

**Option C: copy the folder.** Copy `Packages/com.wobblefolk.active-ragdoll` into your project's `Packages/`
folder.

## Quick start

1. **Try the sample.** In *Package Manager > Wobblefolk Active Ragdoll > Samples*, import **Playground**, open
   `Playground.unity` and press Play. It has a flat ground, a 15° ramp, stairs, boxes (2 / 8 / 20 / 40 kg) and
   barrels to carry, a 1.6 m ledge to climb, a tower that topples and a tower that breaks.
2. **Add a test character to your own scene.** In the Hierarchy, right-click **Wobblefolk > Primitive Active Ragdoll
   (Player)**. This adds the character, a follow camera on the main camera and a physics settings object. The default
   profiles are created in `Assets/ActiveRagdoll/Profiles/` the first time.
   - Use **Wobblefolk > Primitive Active Ragdoll** for a character without input (an NPC).
3. **Use your own model.** Open **Tools > Wobblefolk > Build Active Ragdoll** and drag a Humanoid model into the
   window (see [Humanoid setup tool](#humanoid-setup-tool)).

## Controls

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move (camera-relative) | W A S D | Left stick |
| Sprint | Left Shift | Left stick press |
| Jump / climb while hanging | Space | South button (A / Cross) |
| Camera | Mouse | Right stick |
| Left arm: reach and grab | Left mouse button | Left trigger |
| Right arm: reach and grab | Right mouse button | Right trigger |

Bindings are inline `InputActionProperty` fields on `RagdollPlayerInput`; rebind them in the Inspector or point
them at actions in your own Input Actions asset.

For testing, the **⋮** menu of `RagdollDebugTools` has **Push Forward**, **Push Sideways**, **Fall** and
**Knock Out**.

## How it works

### The body

The ragdoll has 15 bones:

- Pelvis, Chest, Head
- Upper arm, lower arm, hand (left and right)
- Upper leg, lower leg, foot (left and right)

Each non-root bone has a `ConfigurableJoint` to its parent with locked linear motion, limited angular motion and a
**slerp drive**: the drive's spring and damper are the "muscle". `RagdollBone` hides the joint-space math and exposes
targets in joint, local or world space.

All joints follow one convention: **joint X = the character's right, joint Y = the character's up**. A positive X
rotation swings hanging limbs backwards and bends the chest and head forward. Because the convention comes from the
character, not from the rig's bone axes, the same profiles work on any skeleton.

### Balance

`BalanceController` runs every physics step:

- **Upright:** `torque = axis × angle × tiltSpring − tiltAngularVelocity × tiltDamper`. It is applied to the pelvis and, scaled by `chestWeight`, to the chest. The target "up" can be tilted for leaning and bending.
- **Facing:** a second PD loop turns the pelvis around the up axis towards `DesiredFacing`.
- **Stand height:** a sphere cast below the pelvis finds the ground. The pelvis is lifted with `acceleration = g × gravityCompensation + error × spring − verticalVelocity × damper`. The acceleration is clamped to `[0, maxLiftAcceleration]` and multiplied by the total mass plus any carried mass. The legs touch the ground, but most of the weight is carried by this spring. This is what makes the character stable and "floaty" in the right way.
- **Falling:** if the pelvis stays tilted more than `fallTiltAngle` away from its *intended* orientation for `fallTiltTime`, or a bone is hit faster than `knockdownImpactSpeed` along the contact normal, the state becomes `Fallen`.
  - A head hit faster than `knockoutImpactSpeed` causes `Unconscious`.
  - Muscles weaken in both states.
- **Getting up:** after `delay`, once grounded and slow enough, balance and muscle strength ramp back in along `strengthCurve` over `duration`.

```
Normal ──tilt / impact──▶ Fallen ──delay, settled──▶ GettingUp ──upright──▶ Normal
   ▲                        ▲                            │ timeout
   │                        └────────────────────────────┘
   └─── head impact ──▶ Unconscious ──timer──▶ Fallen
```

### Locomotion

- **Movement is a force, not a velocity.** The controller moves the horizontal velocity towards `input × speed` at most `acceleration × dt` per step. The resulting acceleration × total mass is applied to the pelvis and chest. On slopes the target follows the ground plane.
- **Gait:** a phase advances at `speed / strideLength` cycles per second, clamped to `[minCadence, maxCadence]`. Each leg is half a cycle apart:
  - `hip = −hipSwing × cos(phase)`
  - `knee = kneeStance + kneeSwing × max(0, −sin(phase))`
  - `ankle = −(hip + knee) × ankleCompensation`
  - The amplitudes blend from walk to sprint values with speed, and the whole gait fades in and out.
- **Lean:** the up target tilts towards the acceleration and forward with speed, up to `max` degrees.
- **Step-up:** a low ray that hits and a high ray that misses in the move direction mean a step. The stand height is raised and the knees bend more.
- **Jump:** an upward velocity change `√(2 g h)` is applied to every bone. The stand-height lift is suspended while rising. Coyote time and an input buffer make it forgiving.

### Arms and grabbing

- **Aim:** the aim point is where the aim ray (the camera's view ray for the player) first hits something, or a fixed distance ahead of the chest.
- **Reaching:** the upper arm gets a world-space target rotation that points from the shoulder to the aim point, and the elbow stays almost straight. Arm muscles are multiplied by `muscleStrength` while reaching.
- **Bending:** aiming below `bendStartAngle` bends the body. The pelvis takes `pelvisBendShare` of the bend and the spine the rest. Balance targets the bend instead of fighting it, and locomotion flexes the hips so the legs stay underneath.
- **Grabbing:** while reaching, an overlap sphere around the hand looks for the closest collider. A `ConfigurableJoint` is added to the hand at the contact point.
  - Linear motion is locked. Rotation is locked by default.
  - The grip has a `breakForce` / `breakTorque`; when it tears loose, the `released` event reports `broken = true`.
- **Held objects** are registered with the ragdoll:
  - Ground, step, aim, camera and impact checks ignore them, so the character can never "stand on" what it carries and launch itself.
  - Collisions with the holding arm are disabled.
  - Their depenetration velocity is capped while held, so an object pulled into the torso is never shot out.

### Carrying

`CarryController` sums the held objects (anchors used for climbing are excluded):

- **Lifted vs. dragged:** an object counts fully when lifted, and `dragMassFactor` of its mass while it still rests on the ground.
- **Capacity** scales with the number of holding hands:
  - `comfortable = hands × comfortableMassPerHand`
  - `maximum = hands × maxMassPerHand`
  - `strain = clamp01((carried − comfortable) / (maximum − comfortable))`
- **Effects of strain** (shaped by `strainCurve`):
  - Speed and jump height drop towards `minSpeedMultiplier`.
  - Sprint turns off above `noSprintStrain`.
  - Balance torque drops towards `minBalanceWeight`.
  - The body leans away from a lifted load in proportion to its mass × horizontal offset.
  - Lifted mass is added to the stand-height lift.
- **Overload:** a lifted load above the maximum for `overloadSlipTime` slips out of the hands.

### Climbing

`ClimbController` uses grips on *anchors*: static colliders, kinematic bodies, or bodies of at least `minAnchorMass`.

1. **Hanging:** holding an anchor above the chest with the feet off the ground. Balance is mostly switched off and the body dangles.
2. **Pulling up:** a climb request (jump) first looks down just beyond the grip to find the ledge top. A ledge more than `maxHeightAboveGrip` above the grip fails. The chest is then driven up with a capped PD acceleration plus gravity compensation.
3. **Mantling:** once the chest is high enough the hands let go and the pelvis is pushed up and over the edge. The climb finishes when the character stands on the top or is far enough past the edge.

### Breakables

`Breakable` measures each impact as the approach speed along the contact normal:

- At or above `breakSpeed` the object breaks immediately.
- Between `minDamageSpeed` and `breakSpeed`, hits add damage until `durability` is used up (0 = only single hard hits count).
- On break it raises `broken(point, speed)`, optionally spawns `brokenPrefab` with the original velocity, and optionally destroys itself.

## Component reference

| Component | Lives on | Key public API |
|---|---|---|
| `ActiveRagdoll` | Root | `State`, `Bones`, `GetBone(BoneId)`, `Pelvis` / `Chest` / `Head`, `TotalMass`, `CenterOfMass`, `AverageVelocity`, `SetState()`, `SetMuscleStrength()`, `ApplyProfile()`, `IsOwnCollider()`, `IsOwnOrHeld()`, events `StateChanged`, `BoneCollided` |
| `RagdollBone` | Each bone | `Body`, `Joint`, `SetTargetJointRotation()`, `SetTargetLocalRotation()`, `SetTargetWorldRotation()`, `ResetTarget()`, `SetLocalMuscleStrength()` |
| `BalanceController` | Root | `Ground`, `Tilt`, `DesiredFacing`, `TargetUp`, `HeightOffset`, `ChestBend`, `PelvisBend`, `SetModifier()` / `ClearModifier()`, `Fall()`, `Knockout(seconds)` |
| `LocomotionController` | Root | `MoveInput`, `Sprint`, `RequestJump()`, `SpeedMultiplier`, `SprintAllowed`, `AdditionalLean`, `IsGrounded`, `HorizontalVelocity`, `SetArmSwingSuppressed()` |
| `ArmController` | Root | `SetReach(side, held)`, `AimRay`, `HasAim`, `AimPoint`, `IsReaching()`, `IsHolding()`, `TryGetGrab()`, `Release(side)`, `BlockGrabs(seconds)`, events `Grabbed`, `Released` |
| `CarryController` | Root | `Strain`, `CarriedMass`, `LiftedMass`, `IsOverloaded` |
| `ClimbController` | Root | `Phase`, `RequestClimb()`, `LedgeTop`, `IsAnchor(body)` |
| `RagdollPlayerInput` | Root | Input System driver: move, look, jump, sprint, arms; `FollowCamera` |
| `RagdollFollowCamera` | Camera | `Target`, `AddLookInput()`, `AimRay`, `PlanarForward`, `ToWorldDirection()` |
| `RagdollPhysicsSettings` | Any scene object | Applies the fixed timestep and solver defaults on Awake |
| `RagdollDebugTools` | Root | Context-menu Push / Fall / Knock Out |
| `Breakable` | Any rigidbody | `Break()`, `IsBroken`, `broken` event |

**Execution order (FixedUpdate):** `ActiveRagdoll` (−100) → `BalanceController` (0) → `LocomotionController` (10)
→ `ArmController` (20) → `CarryController` (30) → `ClimbController` (40).

Systems never change balance directly. They call `BalanceController.SetModifier(source, balanceWeight, liftWeight,
supportedMass)`; the weights of all sources are multiplied and the masses added.

Only `ActiveRagdoll` and `BalanceController` are required. Leave out the others (for example arms on a simple NPC) and
everything else keeps working.

## Profile reference

Create profiles with *Create > Wobblefolk > …*. Several characters can share one profile, and edits made in Play mode
apply immediately (Unity keeps them in the asset).

### RagdollProfile: body and muscles

Default bones (≈65 kg, 1.75 m). *X* is flex / extend around the character's right axis; *Y* and *Z* are symmetric
limits. *Max torque* caps the muscle so limbs cannot fling objects.

| Bone | Mass (kg) | X limits (°) | Y / Z (°) | Spring | Damper | Max torque (N·m) |
|---|---|---|---|---|---|---|
| Pelvis | 12 | (root, no joint) | | | | |
| Chest | 15 | −25 … 75 | 25 / 20 | 1800 | 90 | 900 |
| Head | 5 | −40 … 40 | 50 / 30 | 400 | 25 | 120 |
| Upper arm | 2.5 | −175 … 60 | 90 / 90 | 300 | 20 | 140 |
| Lower arm | 1.5 | −145 … 0 | 60 / 10 | 200 | 12 | 90 |
| Hand | 0.5 | −60 … 60 | 30 / 30 | 50 | 3 | 30 |
| Upper leg | 7 | −110 … 40 | 20 / 35 | 1500 | 70 | 450 |
| Lower leg | 4 | 0 … 145 | 5 / 5 | 1200 | 60 | 350 |
| Foot | 1 | −35 … 45 | 15 / 20 | 400 | 20 | 120 |

| Physics setting | Default | Notes |
|---|---|---|
| Solver iterations / velocity iterations | 12 / 4 | Per bone |
| Max angular velocity | 30 rad/s | |
| Max depenetration velocity | 5 m/s | |
| Linear / angular damping | 0.05 / 0.5 | |
| Interpolation | Interpolate | |
| Collision detection | Continuous Speculative | |
| Projection | on, 0.05 m / 10° | |
| Enable preprocessing | off | |
| Connected mass scale | 1 | |
| Ignore self collisions | on | |
| Muscle strength | 1 | Global multiplier |

### BalanceProfile

| Group | Settings (defaults) |
|---|---|
| Upright | tilt spring 900, tilt damper 90, yaw spring 400, yaw damper 60, max torque 3000 N·m, chest weight 0.6 |
| Height | stand height 0.96 m, spring 150, damper 20, gravity compensation 0.9, max lift acceleration 30 m/s² |
| Ground | layers: everything, probe radius 0.15 m, start offset 0.3 m, extra distance 0.25 m, foot probe 0.08 m, max ground angle 55° |
| Fall | tilt angle 55°, tilt time 0.2 s, knockdown impact 9 m/s, knockout (head) impact 13 m/s, ignore foot impacts, fallen muscle strength 0.3 |
| Get up | delay 1.2 s, max start speed 1.5 m/s, duration 1 s, strength curve ease-in-out, complete tilt 20°, timeout 3 s |
| Knockout | default duration 4 s, muscle strength 0.05 |

### LocomotionProfile

| Group | Settings (defaults) |
|---|---|
| Movement | walk 2.4 m/s, sprint 4.2 m/s, acceleration 14, deceleration 18, air acceleration 4 m/s², turn 540°/s, chest force share 0.4 |
| Lean | 0.8° per m/s², 8° at sprint speed, max 15°, response 8 |
| Gait | stride 1.1 m, cadence 1.1–2.4 Hz, idle speed 0.25 m/s, blend 6; hip swing 30° / 45°, knee swing 55° / 85° (walk / sprint), stance knee 6°, ankle compensation 0.8; arm swing 20° / 40°, elbow 15°; foot friction 0.25 |
| Step-up | max step 0.35 m, probe 0.45 m, height boost 0.18 m, extra knee bend 35° |
| Jump | height 0.8 m, coyote 0.12 s, buffer 0.15 s, cooldown 0.35 s, lift suspend 0.5 s, grounded tolerance 0.15 m, air pose hip −25° / knee 50° |

### ArmProfile

| Group | Settings (defaults) |
|---|---|
| Reach | muscle ×4, elbow bend 5°, aim layers: everything, max aim distance 8 m, default reach 1.5 m, face aim at 360°/s |
| Bending | starts 10° below horizontal, 1.6° of bend per degree, max 90°, pelvis share 0.4 (max 35°), response 6 |
| Grab | layers: everything, radius 0.06 m, allow static surfaces, break force 4000 N, break torque unlimited, lock rotation on (angular spring 0 / damper 2 when off), solver mass ratio off |
| Held objects | max depenetration 2 m/s, solver iterations 12, restore delay 1 s |
| Behaviour | regrab delay 0.25 s, release on fall, ignore collisions with the holding arm |

### CarryProfile

| Setting | Default |
|---|---|
| Comfortable / maximum mass per hand | 6 kg / 18 kg |
| Drag mass factor | 0.35 |
| Lifted clearance | 0.05 m |
| Strain curve | linear |
| Min speed multiplier | 0.3 |
| Min balance weight | 0.7 |
| No-sprint strain | 0.5 |
| Lift support | 1 |
| Counter-lean | 1.2° per kg·m, max 12° |
| Overload slip time | 1.5 s |
| Regrab block after slip | 0.75 s |

### ClimbProfile

| Group | Settings (defaults) |
|---|---|
| Anchors | kinematic bodies count, min anchor mass 200 kg, grip ≥ 0.05 m above the chest |
| Ledge probe | inset 0.2 m, max height above grip 0.9 m, max top angle 40° |
| Hang | balance weight 0.15 |
| Pull-up | chest 0.3 m above ledge (max 0.45 m above grip), wall offset 0.3 m, spring 40, damper 9, max acceleration 18 m/s², gravity compensation 1, driven share 0.6, tolerance 0.1 m, timeout 2.5 s |
| Mantle | forward 0.6 m, pelvis 0.75 m above ledge, complete clearance 0.3 m, top tolerance 0.1 m, timeout 2 s |
| Input | request buffer 0.25 s, regrab block 0.6 s |

## Events

| Component | UnityEvents |
|---|---|
| `ActiveRagdoll` | `stateChanged(previous, current)`, `fell`, `gettingUp`, `recovered`, `knockedOut`, `wokeUp` |
| `ArmController` | `grabbed(side, collider)`, `released(side, collider, broken)` |
| `CarryController` | `strainChanged(0..1)`, `overloadSlipped` |
| `ClimbController` | `startedHanging`, `startedClimbing`, `finishedClimbing`, `failedClimbing` |
| `Breakable` | `broken(point, impactSpeed)` |

C# events: `ActiveRagdoll.StateChanged`, `ActiveRagdoll.BoneCollided`, `ArmController.Grabbed`,
`ArmController.Released`.

## Scripting and NPCs

`RagdollPlayerInput` is only a driver. AI uses the same API. A minimal follower:

```csharp
using UnityEngine;
using Wobblefolk.Ragdoll;

public sealed class FollowAndGrab : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float stopDistance = 0.8f;

    ActiveRagdoll ragdoll;
    LocomotionController locomotion;
    ArmController arms;

    void Awake()
    {
        ragdoll = GetComponent<ActiveRagdoll>();
        locomotion = GetComponent<LocomotionController>();
        arms = GetComponent<ArmController>();
        ragdoll.StateChanged += (previous, current) => Debug.Log($"{name}: {previous} -> {current}");
    }

    void Update()
    {
        Vector3 pelvis = ragdoll.Pelvis.Body.position;
        Vector3 toTarget = Vector3.ProjectOnPlane(target.position - pelvis, Vector3.up);
        bool close = toTarget.magnitude < stopDistance;

        locomotion.MoveInput = close ? Vector3.zero : toTarget.normalized; // world direction, length 0..1
        locomotion.Sprint = toTarget.magnitude > 5f;

        // Aim from the chest at the target and reach with both hands when close.
        Vector3 chest = ragdoll.Chest.Body.position;
        arms.HasAim = true;
        arms.AimRay = new Ray(chest, (target.position - chest).normalized);
        arms.SetReach(BodySide.Left, close);
        arms.SetReach(BodySide.Right, close);
    }
}
```

Other useful calls:

```csharp
GetComponent<BalanceController>().Knockout(3f);   // limp for 3 s, then get up
GetComponent<BalanceController>().Fall();         // knock down
GetComponent<ClimbController>().RequestClimb();   // pull up while hanging
GetComponent<LocomotionController>().RequestJump();

// Build a primitive character at runtime.
var profiles = new RagdollProfileSet { ragdoll = ragdollProfile, balance = balanceProfile,
    locomotion = locomotionProfile, arms = armProfile, carry = carryProfile, climb = climbProfile };
ActiveRagdoll npc = PrimitiveRagdollBuilder.Build(new PrimitiveBodyShape(), profiles, spawnPoint, Quaternion.identity);
```

## Physics settings and jitter

Jitter is the main enemy of active ragdolls. These values were tested with this package. If you do not want to change
the project settings, add `RagdollPhysicsSettings` to the scene; it applies the first four rows on Awake.

| Setting | Recommended | Why |
|---|---|---|
| Fixed Timestep | **0.01** (100 Hz) | At the default 0.02, long joint chains jitter and spring muscles become unstable. |
| Maximum Allowed Timestep | 0.05 | Stops physics steps from piling up after a frame hitch (spiral of death). |
| Solver / velocity iterations | **12 / 4** per bone | Stiffer joint chains, steadier contacts. |
| Rigidbody interpolation | **Interpolate** on every visible bone | Smooths 100 Hz physics against the render frame rate. |
| Collision detection | Continuous Speculative | Cheap protection against fast limbs tunnelling through thin objects. |
| Max angular velocity | 30 rad/s | Limits spinning bones after an unstable step. |
| Max depenetration velocity | 5 m/s on bones, **2 m/s on held objects** | Unity's default is unlimited; overlapping bodies shoot each other away. |
| Joint projection | on, 0.05 m / 10° | Keeps limbs from stretching apart. |
| Enable preprocessing | **off** | Less jitter in long chains and against hard limits. |
| Muscle max torque | human-scale (arm 140, hip 450 N·m) | Unlimited muscles turn limbs into battering rams. |
| Muscle damping | near critical: `damper ≈ 2·√(spring·inertia)` | Too little oscillates; too much feels like moving under water. |
| Mass ratio of connected bodies | below 10:1 | The PhysX solver gets unstable at large ratios. |

Rules the code follows:

- **Velocities are never assigned directly.** All motion goes through capped forces or accelerations.
- **Held objects are invisible to the sensors** (ground, steps, aim, camera, impacts). Otherwise the character stands on the box in its hands and lifts itself forever.
- **Grip anchors are computed from transforms.** Adding a joint syncs each body to its transform, so transform-space anchors match what the solver sees. Anchors computed from interpolated rigidbody poses disagree, and the joint snaps the gap shut in one step and flings the object.
- **Mass scaling (`massScale`) is off by default.** It does not conserve momentum and makes heavy objects artificially light. Enable it with *ArmProfile > Max Solver Mass Ratio* only if a very heavy object jitters.

## Why ConfigurableJoint

| | ConfigurableJoint | CharacterJoint | HingeJoint |
|---|---|---|---|
| Degrees of freedom | 6 axes, each locked, limited or free | Twist + two swing limits | One axis |
| Driving towards a pose (muscle) | **Yes:** `targetRotation` + slerp drive | No, limits only | Motor or spring on one axis |
| Changing strength at runtime | Drive spring, damper and max force | No | Limited |
| Projection | Yes | Yes | No |
| Setup complexity | High (axis and target math) | Low | Low |
| Best for | **Active ragdolls** | Passive (dead) ragdolls | Doors, hinges, simple single-axis parts |

**Choice:** an active ragdoll drives every joint towards a target pose with a muscle and changes that muscle's
strength with the situation: limp when falling, stiff when reaching. Only `ConfigurableJoint` offers that. Single-axis
joints such as the knee use the same type as well, so every bone is driven by one code path and the profile format is
uniform. The axis and target math is hidden inside `RagdollBone`.

## Performance and many characters

**Cost of one character:** 15 rigidbodies, 14 joints, plus 1 grip joint per holding hand.

**Queries each physics step:**
- 1 sphere cast and 2 foot rays for the ground
- 2 step rays while moving
- 1 aim ray and 1 overlap per reaching hand while reaching
- 1 ray per held object while carrying

No component allocates memory per frame; all queries use the `NonAlloc` variants with preallocated buffers.

For many characters (NPCs, multiplayer):

- **The solver is the main cost.** Distant or background characters do well with 6–8 / 2 solver iterations; use a separate "NPC" profile.
- **Self-collision:** each character calls `Physics.IgnoreCollision` for its own collider pairs on Awake (105 pairs, once). If hundreds spawn at once, a dedicated ragdoll layer with self-collision disabled in the Layer Collision Matrix is cheaper. Note that ragdolls then no longer collide with each other.
- **Sleeping:** muscle forces keep characters awake. Disabling off-screen NPCs (`SetActive(false)`) is the biggest saving.
- **Multiplayer:** physics is not deterministic. Simulate each character on the server or its owner and interpolate bone poses on other clients. The input surface (`MoveInput`, `SetReach`, `AimRay`, jump) is small and easy to replicate.

## Humanoid setup tool

**Tools > Wobblefolk > Build Active Ragdoll**

1. Put a model whose *Rig > Animation Type* is **Humanoid** into the scene and drag it into the window.
2. Choose profiles (the defaults are found or created) and adjust collider ratios if needed.
3. Click **Build Active Ragdoll**.

The tool:

- **Maps bones:** the avatar's bones become the 15 ragdoll bones. Chest falls back to Spine. In-between bones (spine, neck, shoulders) are not simulated and follow their parent.
- **Re-poses arms:** T-pose arms are lowered to the relaxed rest pose the profiles are tuned for (*Arms Down Angle*).
- **Creates colliders:** sized from bone lengths, placed on aligned `RagdollCollider` child objects, so they fit whatever the rig's bone axes are.
- **Sets joint axes:** derived from the character's own right / up directions.
- **Fits stand height:** if the model's pelvis height differs from the balance profile's stand height, it creates a copy of the balance profile for that model.
- **Disables the Animator:** the component stays because the avatar needs it.
- **Adds extras (optional):** player input, a follow camera and debug tools.

**Remove Active Ragdoll** in the same window strips everything and re-enables the Animator. Both build and remove are
a single undo step (Ctrl+Z).

The tool was tested on a generated Humanoid skeleton with a Mixamo-style layout and deliberately inconsistent bone
axes: it stood, walked, reached, grabbed and carried. It has not yet been tested with a real FBX character.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Input does nothing | Set *Player > Active Input Handling* to *Input System Package* or *Both*. Check that `RagdollPlayerInput` is enabled and its *Follow Camera* is assigned. |
| The character jitters or shakes | Add `RagdollPhysicsSettings` or set *Fixed Timestep* to 0.01. Avoid non-uniform scale on bones. Do not raise muscle springs without raising dampers. |
| Falls over immediately or sinks | The stand height does not match the model. Use the setup tool's per-model profile copy or set *BalanceProfile > Height > Stand Height* to the pelvis height. Make sure the ground is on a *Ground Layer*. |
| Objects get flung when grabbed | Keep *Held Max Depenetration Velocity* low (2 m/s) and leave the arm *Max Torque* values human-scale. |
| Cannot climb | The grip must be static, kinematic or at least 200 kg, above the chest, and the ledge top within 0.9 m of the grip. The ledge must be on a ground layer. Hold both arms on the edge and press Jump. |
| Pink materials in the sample | Your project uses another render pipeline; reassign the sample materials. |
| Sprinting falls on every stride | Keep *BalanceProfile > Fall > Ignore Foot Impacts* on. |

## Known limitations

- **Climbing is basic.** A force on the torso lifts the body; the arms only hold.
- **Carried objects sway** a little with each step while walking and settle when standing.
- **Idle arms** hold the rest pose and swing while walking.
- **`Breakable` does not fracture meshes.** It raises an event and can swap in a pre-fractured prefab.
- **No real model tested yet.** Different body proportions may need tuning of collider ratios and stand height.

## Project structure

```
Packages/com.wobblefolk.active-ragdoll/
├─ Runtime/
│  ├─ Core/          ActiveRagdoll, RagdollBone, BoneId, RagdollState, RagdollPhysicsSettings
│  ├─ Balance/       BalanceController, GroundProbe
│  ├─ Locomotion/    LocomotionController
│  ├─ Arms/          ArmController, CarryController, ClimbController
│  ├─ Interaction/   Breakable
│  ├─ Camera/        RagdollFollowCamera
│  ├─ Input/         RagdollPlayerInput (separate assembly, depends on the Input System)
│  ├─ Building/      PrimitiveRagdollBuilder, HumanoidRagdollBuilder, RagdollProfileSet
│  ├─ Profiles/      Ragdoll, Balance, Locomotion, Arm, Carry, Climb profiles
│  ├─ Debugging/     RagdollDebugTools
│  └─ Utilities/     JointMath
├─ Editor/           Build Active Ragdoll window, GameObject menu items
└─ Samples~/Playground/  sample scene, its profiles and materials
```

## Credits and license

Released under the **MIT License**; see `LICENSE.md` in the package, or `LICENSE` at the repository root.

All code was written from scratch for this project. Several open-source projects (MIT / Apache-2.0) were studied
for architecture ideas only; they are listed in `THIRD_PARTY_NOTICES.md`.

---

<a id="türkçe"></a>

# Türkçe

Unity 6 için tamamen fizikle çalışan bir **active ragdoll** karakter sistemi. Karakter ayakta durur, yürür, koşar,
zıplar, birbirinden bağımsız iki koluyla uzanır, elinin değdiği her şeyi tutar, eşyaları gerçek ağırlıklarıyla taşır
ve kenarlara tırmanır. **Hiç animasyon klibi ve Animator kullanılmaz**; her poz eklem motorlarından ve kuvvetlerden
gelir.

Amaç, *Human: Fall Flat* gibi oyunlardaki sallantılı ve fiziksel hissi, Inspector'dan ayarlanabilen temiz
bileşenlerle sıfırdan yazmaktır. Herhangi bir oyundan ya da üçüncü taraf projeden kod veya asset içermez.

## İçindekiler

1. [Özellikler](#özellikler)
2. [Gereksinimler](#gereksinimler)
3. [Kurulum](#kurulum)
4. [Hızlı başlangıç](#hızlı-başlangıç)
5. [Kontroller](#kontroller)
6. [Nasıl çalışır](#nasıl-çalışır)
7. [Bileşen başvurusu](#bileşen-başvurusu)
8. [Profil başvurusu](#profil-başvurusu)
9. [Olaylar](#olaylar)
10. [Kod ve NPC kullanımı](#kod-ve-npc-kullanımı)
11. [Fizik ayarları ve titreme](#fizik-ayarları-ve-titreme)
12. [Neden ConfigurableJoint](#neden-configurablejoint)
13. [Performans ve çok sayıda karakter](#performans-ve-çok-sayıda-karakter)
14. [Humanoid kurulum aracı](#humanoid-kurulum-aracı)
15. [Sorun giderme](#sorun-giderme)
16. [Bilinen sınırlamalar](#bilinen-sınırlamalar)
17. [Proje yapısı](#proje-yapısı)
18. [Atıflar ve lisans](#atıflar-ve-lisans)

## Özellikler

| Alan | Neler var |
|---|---|
| **Gövde** | 15 kemikli insansı ragdoll (Rigidbody + ConfigurableJoint). Kemik başına kütle, eklem limitleri ve kas gücü bir ScriptableObject'ten gelir. |
| **Denge** | Pelvis ve göğse PD kontrollü dik durma ve yön torku, pelvisi zeminden ayakta durma yüksekliğinde tutan bir yay, zemin ve ayak teması algılama. |
| **Durumlar** | `Normal`, `Fallen` (düşmüş), `GettingUp` (kalkıyor), `Unconscious` (bayılmış). Eğilme veya sert darbeyle düşme, kafaya darbeyle bayılma, kendiliğinden kalkma. |
| **Hareket** | Sınırlı kuvvetlerle kameraya göre yürüme ve koşma, kalça / diz / bilek hedefleriyle prosedürel adım döngüsü, kol sallama, ivmeye doğru eğilme, basamak çıkma, coyote time ve tuş tamponlu zıplama. |
| **Kollar** | Sol ve sağ kol bağımsızdır. Tuşu basılı tutulan kol kameranın baktığı noktaya uzanır; aşağı uzanınca gövde eğilir. |
| **Tutma** | El, değdiği ilk yüzeyi kopabilir bir ConfigurableJoint ile tutar; rigidbody'leri de statik collider'ları da. Tuş bırakılınca el de bırakır. |
| **Taşıma** | Tutan el başına kapasite. Zorlanma yavaşlatır, koşmayı kapatır, dengeyi zayıflatır ve karakteri geriye yaslar. Aşırı yükte eller kayar. |
| **Tırmanma** | Kenara asılma, yukarı çekilme, sonra elleri bırakıp kenarın üstüne geçme. |
| **Etkileşim** | Hafif nesneler doğrudan fizikle itilir. `Breakable` yeterince sert çarpınca olay fırlatır ve isteğe bağlı olarak parçalanmış bir prefab koyar. |
| **Araçlar** | Herhangi bir Humanoid model için tek tıkla kurulum, primitive test karakteri ve Playground örnek sahnesi. |
| **Ayar** | Her sayı bir ScriptableObject profilinde ya da Inspector'dadır ve Play modunda anında etkili olur. |

## Gereksinimler

- **Unity 6000.3** veya üstü (6000.3.25f1 ve PhysX ile geliştirildi ve test edildi).
- **Input System** paketi 1.20+. Bağımlılık olarak kendiliğinden kurulur. *Project Settings > Player > Active Input Handling* ayarı **Input System Package** ya da **Both** olmalıdır.
- **Render pipeline:** çekirdek kod pipeline'dan bağımsızdır. Örnek sahnenin malzemeleri aktif pipeline'ın varsayılan shader'ını kullanır (bu repoda URP Lit).

## Kurulum

**Seçenek A: bu repoyu klonla.** Repo eksiksiz bir Unity projesidir. Unity 6000.3+ ile aç; paket
`Packages/com.wobblefolk.active-ragdoll` altında gömülü durur.

**Seçenek B: paketi Git ile kendi projene ekle.** *Window > Package Manager > + > Add package from git URL…*
penceresine şunu yaz:

```
https://github.com/Atthree/wobblefolk-active-ragdoll.git?path=/Packages/com.wobblefolk.active-ragdoll
```

**Seçenek C: klasörü kopyala.** `Packages/com.wobblefolk.active-ragdoll` klasörünü projenin `Packages/` klasörüne
kopyala.

## Hızlı başlangıç

1. **Örnek sahneyi dene.** *Package Manager > Wobblefolk Active Ragdoll > Samples* altından **Playground**'ı içe
   aktar, `Playground.unity` sahnesini aç ve Play'e bas. Sahnede düz zemin, 15° rampa, merdiven, taşınacak kutular
   (2 / 8 / 20 / 40 kg) ve variller, tırmanılacak 1.6 m'lik kenar, devrilen bir kule ve kırılan bir kule vardır.
2. **Kendi sahnene test karakteri ekle.** Hierarchy'de sağ tık > **Wobblefolk > Primitive Active Ragdoll (Player)**.
   Bu, karakteri, ana kamerada takip kamerasını ve bir fizik ayarları nesnesini ekler. Varsayılan profiller ilk
   seferde `Assets/ActiveRagdoll/Profiles/` altında oluşturulur.
   - Girdisi olmayan bir karakter (NPC) için **Wobblefolk > Primitive Active Ragdoll** kullan.
3. **Kendi modelini kullan.** **Tools > Wobblefolk > Build Active Ragdoll** penceresini aç ve Humanoid modelini
   içine sürükle (bkz. [Humanoid kurulum aracı](#humanoid-kurulum-aracı)).

## Kontroller

| Eylem | Klavye / fare | Gamepad |
|---|---|---|
| Yürü (kameraya göre) | W A S D | Sol çubuk |
| Koş | Sol Shift | Sol çubuğa basma |
| Zıpla / asılıyken tırman | Space | Güney tuşu (A / Çarpı) |
| Kamera | Fare | Sağ çubuk |
| Sol kol: uzan ve tut | Sol fare tuşu | Sol tetik |
| Sağ kol: uzan ve tut | Sağ fare tuşu | Sağ tetik |

Tuş atamaları `RagdollPlayerInput` üzerindeki `InputActionProperty` alanlarıdır; Inspector'dan yeniden atanabilir ya
da kendi Input Actions asset'indeki eylemlere bağlanabilir.

Test için `RagdollDebugTools` bileşeninin **⋮** menüsünde **Push Forward**, **Push Sideways**, **Fall** ve
**Knock Out** komutları vardır.

## Nasıl çalışır

### Gövde

Ragdoll'da 15 kemik var:

- Pelvis, göğüs, kafa
- Üst kol, alt kol, el (sol ve sağ)
- Üst bacak, alt bacak, ayak (sol ve sağ)

Kök dışındaki her kemik, üst kemiğine bir `ConfigurableJoint` ile bağlıdır. Doğrusal hareket kilitli, açısal hareket
sınırlıdır ve eklemde bir **slerp drive** vardır; bu drive'ın yay ve sönümü "kas"tır. `RagdollBone` eklem uzayı
matematiğini gizler ve hedefleri eklem, yerel veya dünya uzayında verir.

Bütün eklemler tek bir kurala uyar: **eklem X = karakterin sağı, eklem Y = karakterin yukarısı**. Pozitif X dönüşü
sarkan uzuvları geriye savurur, göğsü ve kafayı öne büker. Bu kural rigin kemik eksenlerinden değil karakterin
kendisinden geldiği için, aynı profiller her iskelette çalışır.

### Denge

`BalanceController` her fizik adımında çalışır:

- **Dik durma:** `tork = eksen × açı × tiltSpring − eğilmeAçısalHızı × tiltDamper`. Pelvise, `chestWeight` ile ölçeklenmiş olarak da göğse uygulanır. Hedef "yukarı" yönü eğilme ve bükülme için yatırılabilir.
- **Yön:** ikinci bir PD döngüsü pelvisi yukarı eksen etrafında `DesiredFacing` yönüne çevirir.
- **Ayakta durma yüksekliği:** pelvisin altına bir küre taraması zemini bulur. Pelvis şu ivmeyle kaldırılır: `ivme = g × gravityCompensation + hata × spring − dikeyHız × damper`. İvme `[0, maxLiftAcceleration]` aralığına sıkıştırılır ve toplam kütle artı taşınan kütleyle çarpılır. Bacaklar yere değer ama ağırlığın çoğunu bu yay taşır. Karakteri hem kararlı hem de doğru şekilde "hafif" yapan budur.
- **Düşme:** pelvis, *olması gereken* yönünden `fallTiltAngle`'dan fazla saparak `fallTiltTime` boyunca kalırsa ya da bir kemik temas yönünde `knockdownImpactSpeed`'den hızlı bir darbe alırsa durum `Fallen` olur.
  - Kafaya `knockoutImpactSpeed`'den hızlı darbe gelirse durum `Unconscious` olur.
  - İki durumda da kaslar zayıflar.
- **Kalkma:** `delay` süresinden sonra, karakter yerdeyse ve yeterince yavaşsa, denge ve kas gücü `duration` boyunca `strengthCurve` eğrisiyle geri gelir.

```
Normal ──eğilme / darbe──▶ Fallen ──bekleme, durulma──▶ GettingUp ──dik──▶ Normal
   ▲                          ▲                              │ zaman aşımı
   │                          └──────────────────────────────┘
   └─── kafaya darbe ──▶ Unconscious ──süre──▶ Fallen
```

### Hareket

- **Hareket hız değil kuvvettir.** Kontrolcü yatay hızı her adımda en fazla `acceleration × dt` kadar `girdi × hız` hedefine yaklaştırır. Ortaya çıkan ivme × toplam kütle pelvise ve göğse uygulanır. Eğimlerde hedef zemin düzlemini takip eder.
- **Adım döngüsü:** bir faz, saniyede `hız / strideLength` tur hızla ilerler ve `[minCadence, maxCadence]` aralığında tutulur. Bacaklar yarım tur arayla çalışır:
  - `kalça = −hipSwing × cos(faz)`
  - `diz = kneeStance + kneeSwing × max(0, −sin(faz))`
  - `bilek = −(kalça + diz) × ankleCompensation`
  - Genlikler hıza göre yürüme değerlerinden koşma değerlerine geçer; döngünün tamamı yumuşakça açılıp kapanır.
- **Eğilme:** hedef yukarı yönü ivmeye doğru ve hızla öne, en fazla `max` derece yatırılır.
- **Basamak:** hareket yönünde alçak ışın çarpıp yüksek ışın çarpmıyorsa önde basamak var demektir. Ayakta durma yüksekliği yükseltilir ve dizler daha fazla bükülür.
- **Zıplama:** her kemiğe yukarı doğru `√(2 g h)` hız değişimi uygulanır. Yükselirken ayakta durma kuvveti durdurulur. Coyote time ve tuş tamponu zıplamayı affedici yapar.

### Kollar ve tutma

- **Nişan:** nişan noktası, nişan ışınının (oyuncu için kameranın bakış ışını) ilk çarptığı yerdir. Hiçbir şeye çarpmazsa göğsün önünde sabit bir mesafededir.
- **Uzanma:** üst kola omuzdan nişan noktasına bakan bir dünya uzayı hedef dönüşü verilir, dirsek neredeyse düz kalır. Uzanırken kol kasları `muscleStrength` ile çarpılır.
- **Eğilme:** `bendStartAngle`'ın altına nişan almak gövdeyi büker. Bükülmenin `pelvisBendShare` kadarını pelvis, kalanını omurga yapar. Denge bu bükülmeyle savaşmaz, onu hedefler; hareket kontrolcüsü de bacaklar gövdenin altında kalsın diye kalçaları büker.
- **Tutma:** uzanırken elin çevresindeki bir küre sorgusu en yakın collider'ı arar. Temas noktasında ele bir `ConfigurableJoint` eklenir.
  - Doğrusal hareket kilitlidir. Dönme varsayılan olarak kilitlidir.
  - Tutuşun bir `breakForce` / `breakTorque` sınırı vardır; tutuş koparsa `released` olayı `broken = true` bildirir.
- **Tutulan cisimler** ragdoll'a kaydedilir:
  - Zemin, basamak, nişan, kamera ve darbe algılama onları yok sayar. Böylece karakter taşıdığı şeyin "üstünde durup" kendini fırlatamaz.
  - Tutan kolla çarpışmaları kapatılır.
  - Ayrışma hızları sınırlanır; gövdeye çekilen bir cisim fırlatılmaz.

### Taşıma

`CarryController` tutulan cisimleri toplar (tırmanma tutamakları hariç):

- **Kaldırılmış mı sürükleniyor mu:** kaldırılmış bir cisim tam sayılır, hâlâ yere değen bir cismin ise kütlesinin `dragMassFactor` kadarı sayılır.
- **Kapasite** tutan el sayısıyla ölçeklenir:
  - `rahat = el × comfortableMassPerHand`
  - `azami = el × maxMassPerHand`
  - `zorlanma = clamp01((taşınan − rahat) / (azami − rahat))`
- **Zorlanmanın etkileri** (`strainCurve` ile şekillenir):
  - Hız ve zıplama yüksekliği `minSpeedMultiplier`'a doğru düşer.
  - `noSprintStrain` üstünde koşma kapanır.
  - Denge torku `minBalanceWeight`'e doğru düşer.
  - Gövde, kaldırılan yükün kütlesi × yatay uzaklığıyla orantılı olarak yükün tersine yaslanır.
  - Kaldırılan kütle ayakta durma kuvvetine eklenir.
- **Aşırı yük:** azami sınırın üstündeki kaldırılmış bir yük `overloadSlipTime` boyunca tutulursa ellerden kayar.

### Tırmanma

`ClimbController` *tutamak* olan yüzeylerdeki tutuşları kullanır: statik collider'lar, kinematik gövdeler ya da en az
`minAnchorMass` ağırlığındaki gövdeler.

1. **Asılma:** göğsün üstünde bir tutamağı tutarken ayaklar yerden kesik. Denge büyük ölçüde kapanır ve gövde sarkar.
2. **Yukarı çekilme:** tırmanma isteği (zıplama) önce tutuşun hemen ötesine aşağı bakarak kenarın üstünü bulur. Kenar tutuştan `maxHeightAboveGrip`'ten yüksekse tırmanma başarısız olur. Sonra göğüs, sınırlı bir PD ivmesi ve yerçekimi telafisiyle yukarı sürülür.
3. **Kenarın üstüne geçme:** göğüs yeterince yükselince eller bırakır ve pelvis yukarı ve kenarın üstüne itilir. Karakter üstte ayakta durunca veya kenarı yeterince geçince tırmanma biter.

### Kırılabilir nesneler

`Breakable` her darbeyi temas yönündeki yaklaşma hızı olarak ölçer:

- `breakSpeed` ve üstünde nesne hemen kırılır.
- `minDamageSpeed` ile `breakSpeed` arasındaki darbeler `durability` bitene kadar hasar biriktirir (0 = sadece tek sert darbe sayılır).
- Kırılınca `broken(nokta, hız)` olayını fırlatır, isteğe bağlı olarak `brokenPrefab`'ı aynı hızla oluşturur ve isteğe bağlı olarak kendini yok eder.

## Bileşen başvurusu

| Bileşen | Nerede | Önemli genel API |
|---|---|---|
| `ActiveRagdoll` | Kök | `State`, `Bones`, `GetBone(BoneId)`, `Pelvis` / `Chest` / `Head`, `TotalMass`, `CenterOfMass`, `AverageVelocity`, `SetState()`, `SetMuscleStrength()`, `ApplyProfile()`, `IsOwnCollider()`, `IsOwnOrHeld()`, olaylar `StateChanged`, `BoneCollided` |
| `RagdollBone` | Her kemik | `Body`, `Joint`, `SetTargetJointRotation()`, `SetTargetLocalRotation()`, `SetTargetWorldRotation()`, `ResetTarget()`, `SetLocalMuscleStrength()` |
| `BalanceController` | Kök | `Ground`, `Tilt`, `DesiredFacing`, `TargetUp`, `HeightOffset`, `ChestBend`, `PelvisBend`, `SetModifier()` / `ClearModifier()`, `Fall()`, `Knockout(saniye)` |
| `LocomotionController` | Kök | `MoveInput`, `Sprint`, `RequestJump()`, `SpeedMultiplier`, `SprintAllowed`, `AdditionalLean`, `IsGrounded`, `HorizontalVelocity`, `SetArmSwingSuppressed()` |
| `ArmController` | Kök | `SetReach(taraf, basılı)`, `AimRay`, `HasAim`, `AimPoint`, `IsReaching()`, `IsHolding()`, `TryGetGrab()`, `Release(taraf)`, `BlockGrabs(saniye)`, olaylar `Grabbed`, `Released` |
| `CarryController` | Kök | `Strain`, `CarriedMass`, `LiftedMass`, `IsOverloaded` |
| `ClimbController` | Kök | `Phase`, `RequestClimb()`, `LedgeTop`, `IsAnchor(gövde)` |
| `RagdollPlayerInput` | Kök | Input System sürücüsü: hareket, bakış, zıplama, koşma, kollar; `FollowCamera` |
| `RagdollFollowCamera` | Kamera | `Target`, `AddLookInput()`, `AimRay`, `PlanarForward`, `ToWorldDirection()` |
| `RagdollPhysicsSettings` | Herhangi bir sahne nesnesi | Fizik adımını ve solver varsayılanlarını Awake'te uygular |
| `RagdollDebugTools` | Kök | Bağlam menüsünden Push / Fall / Knock Out |
| `Breakable` | Herhangi bir rigidbody | `Break()`, `IsBroken`, `broken` olayı |

**Çalışma sırası (FixedUpdate):** `ActiveRagdoll` (−100) → `BalanceController` (0) → `LocomotionController` (10) →
`ArmController` (20) → `CarryController` (30) → `ClimbController` (40).

Sistemler dengeyi doğrudan değiştirmez. `BalanceController.SetModifier(kaynak, dengeAğırlığı, kaldırmaAğırlığı,
ekKütle)` çağırırlar; bütün kaynakların ağırlıkları çarpılır, kütleleri toplanır.

Sadece `ActiveRagdoll` ve `BalanceController` zorunludur. Diğerlerini (örneğin basit bir NPC'de kolları) çıkarırsan
geri kalan her şey çalışmaya devam eder.

## Profil başvurusu

Profiller *Create > Wobblefolk > …* ile oluşturulur. Birden fazla karakter aynı profili paylaşabilir. Play modunda
yapılan değişiklikler anında etkili olur ve Unity bunları asset'te tutar.

### RagdollProfile: gövde ve kaslar

Varsayılan kemikler (≈65 kg, 1.75 m). *X* karakterin sağ ekseni etrafında bükme / açmadır; *Y* ve *Z* simetrik
limitlerdir. *Azami tork* kası sınırlar, böylece uzuvlar nesneleri fırlatamaz.

| Kemik | Kütle (kg) | X limitleri (°) | Y / Z (°) | Yay | Sönüm | Azami tork (N·m) |
|---|---|---|---|---|---|---|
| Pelvis | 12 | (kök, eklem yok) | | | | |
| Göğüs | 15 | −25 … 75 | 25 / 20 | 1800 | 90 | 900 |
| Kafa | 5 | −40 … 40 | 50 / 30 | 400 | 25 | 120 |
| Üst kol | 2.5 | −175 … 60 | 90 / 90 | 300 | 20 | 140 |
| Alt kol | 1.5 | −145 … 0 | 60 / 10 | 200 | 12 | 90 |
| El | 0.5 | −60 … 60 | 30 / 30 | 50 | 3 | 30 |
| Üst bacak | 7 | −110 … 40 | 20 / 35 | 1500 | 70 | 450 |
| Alt bacak | 4 | 0 … 145 | 5 / 5 | 1200 | 60 | 350 |
| Ayak | 1 | −35 … 45 | 15 / 20 | 400 | 20 | 120 |

| Fizik ayarı | Varsayılan | Not |
|---|---|---|
| Solver / velocity iterasyonu | 12 / 4 | Kemik başına |
| Azami açısal hız | 30 rad/sn | |
| Azami ayrışma hızı | 5 m/sn | |
| Doğrusal / açısal sönüm | 0.05 / 0.5 | |
| Interpolasyon | Interpolate | |
| Çarpışma algılama | Continuous Speculative | |
| Projeksiyon | açık, 0.05 m / 10° | |
| Enable preprocessing | kapalı | |
| Connected mass scale | 1 | |
| Kendi kendine çarpışmayı yok say | açık | |
| Kas gücü | 1 | Genel çarpan |

### BalanceProfile

| Grup | Ayarlar (varsayılanlar) |
|---|---|
| Dik durma | eğilme yayı 900, eğilme sönümü 90, yön yayı 400, yön sönümü 60, azami tork 3000 N·m, göğüs ağırlığı 0.6 |
| Yükseklik | ayakta durma yüksekliği 0.96 m, yay 150, sönüm 20, yerçekimi telafisi 0.9, azami kaldırma ivmesi 30 m/sn² |
| Zemin | katmanlar: hepsi, tarama yarıçapı 0.15 m, başlangıç ofseti 0.3 m, ek mesafe 0.25 m, ayak ışını 0.08 m, azami zemin eğimi 55° |
| Düşme | eğilme açısı 55°, eğilme süresi 0.2 sn, düşüren darbe 9 m/sn, bayıltan (kafa) darbe 13 m/sn, ayak darbelerini yok say, düşmüşken kas gücü 0.3 |
| Kalkma | bekleme 1.2 sn, azami başlama hızı 1.5 m/sn, süre 1 sn, güç eğrisi ease-in-out, tamamlanma eğimi 20°, zaman aşımı 3 sn |
| Bayılma | varsayılan süre 4 sn, kas gücü 0.05 |

### LocomotionProfile

| Grup | Ayarlar (varsayılanlar) |
|---|---|
| Hareket | yürüme 2.4 m/sn, koşma 4.2 m/sn, ivme 14, fren 18, havada ivme 4 m/sn², dönüş 540°/sn, göğse giden kuvvet payı 0.4 |
| Eğilme | m/sn² başına 0.8°, koşma hızında 8°, en fazla 15°, tepki 8 |
| Adım | adım uzunluğu 1.1 m, tempo 1.1–2.4 Hz, durma hızı 0.25 m/sn, geçiş 6; kalça salınımı 30° / 45°, diz salınımı 55° / 85° (yürüme / koşma), basan diz 6°, bilek telafisi 0.8; kol salınımı 20° / 40°, dirsek 15°; ayak sürtünmesi 0.25 |
| Basamak | en yüksek basamak 0.35 m, ileri bakış 0.45 m, yükseklik artışı 0.18 m, ek diz bükme 35° |
| Zıplama | yükseklik 0.8 m, coyote 0.12 sn, tampon 0.15 sn, bekleme 0.35 sn, kaldırmayı durdurma 0.5 sn, yerde sayılma toleransı 0.15 m, havada kalça −25° / diz 50° |

### ArmProfile

| Grup | Ayarlar (varsayılanlar) |
|---|---|
| Uzanma | kas ×4, dirsek bükme 5°, nişan katmanları: hepsi, azami nişan mesafesi 8 m, varsayılan uzanma 1.5 m, nişana dönme 360°/sn |
| Eğilme | yatayın 10° altında başlar, derece başına 1.6° bükülme, en fazla 90°, pelvis payı 0.4 (en fazla 35°), tepki 6 |
| Tutma | katmanlar: hepsi, yarıçap 0.06 m, statik yüzeylere izin, kopma kuvveti 4000 N, kopma torku sınırsız, dönme kilidi açık (kapalıyken açısal yay 0 / sönüm 2), solver kütle oranı kapalı |
| Tutulan cisim | azami ayrışma hızı 2 m/sn, solver iterasyonu 12, eski ayarlara dönüş 1 sn sonra |
| Davranış | tekrar tutma bekleme 0.25 sn, düşünce bırak, tutan kolla çarpışmayı yok say |

### CarryProfile

| Ayar | Varsayılan |
|---|---|
| El başına rahat / azami kütle | 6 kg / 18 kg |
| Sürükleme katsayısı | 0.35 |
| Kaldırılmış sayılma boşluğu | 0.05 m |
| Zorlanma eğrisi | doğrusal |
| En düşük hız çarpanı | 0.3 |
| En düşük denge ağırlığı | 0.7 |
| Koşmayı kapatan zorlanma | 0.5 |
| Kaldırma desteği | 1 |
| Ters yaslanma | kg·m başına 1.2°, en fazla 12° |
| Aşırı yükte kayma süresi | 1.5 sn |
| Kaymadan sonra tutma engeli | 0.75 sn |

### ClimbProfile

| Grup | Ayarlar (varsayılanlar) |
|---|---|
| Tutamaklar | kinematik gövdeler sayılır, en az 200 kg, tutuş göğsün en az 0.05 m üstünde |
| Kenar arama | içeri mesafe 0.2 m, tutuşun en fazla 0.9 m üstü, en dik üst yüzey 40° |
| Asılma | denge ağırlığı 0.15 |
| Yukarı çekilme | göğüs kenarın 0.3 m üstüne (tutuşun en fazla 0.45 m üstü), duvardan uzaklık 0.3 m, yay 40, sönüm 9, azami ivme 18 m/sn², yerçekimi telafisi 1, sürülen gövde payı 0.6, tolerans 0.1 m, zaman aşımı 2.5 sn |
| Kenara geçme | ileri 0.6 m, pelvis kenarın 0.75 m üstüne, tamamlanma boşluğu 0.3 m, üst yüzey toleransı 0.1 m, zaman aşımı 2 sn |
| Girdi | istek tamponu 0.25 sn, tutma engeli 0.6 sn |

## Olaylar

| Bileşen | UnityEvent'ler |
|---|---|
| `ActiveRagdoll` | `stateChanged(önceki, yeni)`, `fell`, `gettingUp`, `recovered`, `knockedOut`, `wokeUp` |
| `ArmController` | `grabbed(taraf, collider)`, `released(taraf, collider, koptuMu)` |
| `CarryController` | `strainChanged(0..1)`, `overloadSlipped` |
| `ClimbController` | `startedHanging`, `startedClimbing`, `finishedClimbing`, `failedClimbing` |
| `Breakable` | `broken(nokta, darbeHızı)` |

C# olayları: `ActiveRagdoll.StateChanged`, `ActiveRagdoll.BoneCollided`, `ArmController.Grabbed`,
`ArmController.Released`.

## Kod ve NPC kullanımı

`RagdollPlayerInput` yalnızca bir sürücüdür; yapay zekâ da aynı API'yi kullanır. Yukarıdaki
[İngilizce bölümdeki örnek](#scripting-and-npcs), hedefi takip eden ve yakına gelince iki eliyle uzanıp tutan
eksiksiz bir NPC betiğidir. Temel çağrılar:

```csharp
locomotion.MoveInput = (hedef - pelvis).normalized; // dünya yönü, büyüklük 0..1
locomotion.Sprint = true;
locomotion.RequestJump();

arms.HasAim = true;
arms.AimRay = new Ray(gogus, (tutulacakNokta - gogus).normalized);
arms.SetReach(BodySide.Right, true);   // uzan ve değdiğini tut

GetComponent<BalanceController>().Knockout(3f);   // 3 sn bayılt, sonra kalkar
GetComponent<BalanceController>().Fall();         // yere düşür
GetComponent<ClimbController>().RequestClimb();   // asılıyken yukarı çık

// Çalışma zamanında primitive karakter oluşturma
ActiveRagdoll npc = PrimitiveRagdollBuilder.Build(new PrimitiveBodyShape(), profiller, dogmaNoktasi, Quaternion.identity);
```

## Fizik ayarları ve titreme

Active ragdoll'un baş düşmanı titremedir. Aşağıdaki değerler bu paketle test edilmiştir. Proje ayarlarını
değiştirmek istemiyorsan sahneye `RagdollPhysicsSettings` ekle; ilk dört satırı Awake'te uygular.

| Ayar | Önerilen | Neden |
|---|---|---|
| Fixed Timestep | **0.01** (100 Hz) | Varsayılan 0.02'de uzun eklem zincirleri titrer ve yaylı kaslar kararsızlaşır. |
| Maximum Allowed Timestep | 0.05 | Kare takılmasından sonra fizik adımlarının birikmesini ("ölüm sarmalı") önler. |
| Solver / velocity iterasyonu | Kemik başına **12 / 4** | Eklem zincirleri daha az esner, temaslar daha kararlı olur. |
| Rigidbody interpolasyonu | Görünen her kemikte **Interpolate** | 100 Hz fiziği ekran kare hızına göre yumuşatır. |
| Çarpışma algılama | Continuous Speculative | Hızlı uzuvların ince nesnelerden geçmesini ucuza önler. |
| Azami açısal hız | 30 rad/sn | Kararsız bir adımdan sonra kemiklerin fırıl fırıl dönmesini sınırlar. |
| Azami ayrışma hızı | Kemiklerde 5 m/sn, **tutulan cisimde 2 m/sn** | Unity'nin varsayılanı sınırsızdır; iç içe geçen gövdeler birbirini fırlatır. |
| Eklem projeksiyonu | açık, 0.05 m / 10° | Uzuvların birbirinden kopup uzamasını engeller. |
| Enable preprocessing | **kapalı** | Uzun zincirlerde ve sert limitlerde titremeyi azaltır. |
| Kas azami torku | İnsan ölçeğinde (kol 140, kalça 450 N·m) | Sınırsız kas uzuvları koç başına çevirir. |
| Kas sönümü | Kritik sönüme yakın: `sönüm ≈ 2·√(yay·atalet)` | Az sönüm salınım, fazla sönüm "su içinde" hissi verir. |
| Bağlı gövdeler arası kütle oranı | 10:1'in altında | PhysX solver büyük oranlarda kararsızlaşır. |

Kodda uyulan kurallar:

- **Hız asla doğrudan atanmaz.** Her hareket sınırlı bir kuvvet veya ivmeyle yapılır.
- **Tutulan cisimler algılayıcılara görünmez** (zemin, basamak, nişan, kamera, darbe). Aksi halde karakter elindeki kutunun üstünde durup kendini sonsuza kadar yukarı kaldırır.
- **Tutuş bağlantı noktaları transform'dan hesaplanır.** Eklem eklenirken her gövde transform'una senkronize edilir; bu yüzden transform uzayındaki bağlantı noktaları solver'ın gördüğüyle eşleşir. Interpolasyonlu rigidbody pozundan hesaplanan noktalar farklı çıkar ve eklem aradaki boşluğu tek adımda kapatıp cismi fırlatır.
- **Kütle ölçekleme (`massScale`) varsayılan olarak kapalıdır.** Momentumu korumaz ve ağır cisimleri yapay olarak hafifletir. Sadece çok ağır bir cisim titriyorsa *ArmProfile > Max Solver Mass Ratio* ile aç.

## Neden ConfigurableJoint

| | ConfigurableJoint | CharacterJoint | HingeJoint |
|---|---|---|---|
| Serbestlik | 6 eksen, her biri kilitli, sınırlı veya serbest | Twist + iki swing limiti | Tek eksen |
| Poza doğru sürme (kas) | **Var:** `targetRotation` + slerp drive | Yok, sadece limitler | Tek eksende motor veya yay |
| Çalışma zamanında güç değişimi | Drive yayı, sönümü ve azami kuvveti | Yok | Sınırlı |
| Projeksiyon | Var | Var | Yok |
| Kurulum karmaşıklığı | Yüksek (eksen ve hedef matematiği) | Düşük | Düşük |
| En uygun olduğu yer | **Active ragdoll** | Pasif (ölü) ragdoll | Kapı, menteşe, basit tek eksenli parçalar |

**Seçim:** active ragdoll her eklemi bir kasla hedef poza sürer ve bu kasın gücünü duruma göre değiştirir: düşünce
gevşer, uzanırken sertleşir. Bunu yalnızca `ConfigurableJoint` sağlar. Diz gibi tek eksenli eklemler de aynı tipte
tutuldu; böylece bütün kemikler tek bir kod yoluyla sürülür ve profil formatı tektir. Eksen ve hedef matematiği
`RagdollBone` içinde gizlidir.

## Performans ve çok sayıda karakter

**Bir karakterin maliyeti:** 15 rigidbody, 14 eklem, artı tutan her el için 1 tutuş eklemi.

**Her fizik adımındaki sorgular:**
- Zemin için 1 küre taraması ve 2 ayak ışını
- Hareket ederken 2 basamak ışını
- Uzanırken 1 nişan ışını ve uzanan el başına 1 çakışma sorgusu
- Taşırken tutulan cisim başına 1 ışın

Hiçbir bileşen her karede bellek ayırmaz; bütün sorgular önceden ayrılmış dizilerle `NonAlloc` sürümleriyle yapılır.

Çok sayıda karakter için (NPC, multiplayer):

- **Asıl maliyet solver'dır.** Uzaktaki ya da arka plandaki karakterler için 6–8 / 2 solver iterasyonu yeterlidir; ayrı bir "NPC" profili kullan.
- **Kendi kendine çarpışma:** her karakter Awake'te kendi collider çiftleri için `Physics.IgnoreCollision` çağırır (105 çift, tek seferlik). Yüzlercesi aynı anda doğuyorsa, Layer Collision Matrix'te kendisiyle çarpışması kapalı özel bir ragdoll katmanı daha ucuzdur. Bu durumda ragdoll'ların birbirine de çarpmayacağını unutma.
- **Uyku:** kas kuvvetleri karakterleri uyutmaz. Ekran dışındaki NPC'leri devre dışı bırakmak (`SetActive(false)`) en büyük kazancı sağlar.
- **Multiplayer:** fizik deterministik değildir. Her karakteri sunucuda ya da sahibinde simüle et, diğer istemcilerde kemik pozlarını interpolasyonla göster. Girdi yüzeyi (`MoveInput`, `SetReach`, `AimRay`, zıplama) küçüktür ve ağ üzerinden kolay taşınır.

## Humanoid kurulum aracı

**Tools > Wobblefolk > Build Active Ragdoll**

1. *Rig > Animation Type* ayarı **Humanoid** olan bir modeli sahneye koy ve pencereye sürükle.
2. Profilleri seç (varsayılanlar otomatik bulunur veya oluşturulur), gerekirse collider oranlarını değiştir.
3. **Build Active Ragdoll**'a bas.

Araç şunları yapar:

- **Kemikleri eşler:** avatarın kemikleri 15 ragdoll kemiğine dönüşür. Chest yoksa Spine kullanılır. Ara kemikler (omurga, boyun, omuz) simüle edilmez, bağlı oldukları kemikle birlikte hareket eder.
- **Kolları indirir:** T-pozundaki kollar, profillerin ayarlandığı rahat dinlenme pozuna indirilir (*Arms Down Angle*).
- **Collider'ları kurar:** kemik uzunluklarından boyutlanır ve hizalı `RagdollCollider` alt nesnelerine konur; böylece rigin kemik eksenleri ne olursa olsun uzva oturur.
- **Eklem eksenlerini hesaplar:** karakterin kendi sağ / yukarı yönünden.
- **Ayakta durma yüksekliğini uydurur:** modelin pelvis yüksekliği denge profilindeki ayakta durma yüksekliğinden farklıysa, o model için denge profilinin bir kopyasını oluşturur.
- **Animator'ü kapatır:** bileşen avatar için yerinde kalır.
- **Ek bileşenler (isteğe bağlı):** oyuncu girdisi, takip kamerası ve debug araçlarını ekler.

Aynı penceredeki **Remove Active Ragdoll** her şeyi kaldırır ve Animator'ü tekrar açar. Kurma da kaldırma da tek bir
geri alma adımıdır (Ctrl+Z).

Araç, Mixamo düzeninde ve kemik eksenleri kasıtlı olarak tutarsız döndürülmüş, kodla üretilmiş bir Humanoid iskelet
üzerinde test edildi: ayakta durdu, yürüdü, uzandı, tuttu ve taşıdı. Henüz gerçek bir FBX karakterle denenmedi.

## Sorun giderme

| Belirti | Çözüm |
|---|---|
| Girdi hiçbir şey yapmıyor | *Player > Active Input Handling* ayarını *Input System Package* ya da *Both* yap. `RagdollPlayerInput`'un açık olduğunu ve *Follow Camera* alanının dolu olduğunu kontrol et. |
| Karakter titriyor veya sarsılıyor | `RagdollPhysicsSettings` ekle ya da *Fixed Timestep*'i 0.01 yap. Kemiklerde düzgün olmayan ölçekten kaçın. Kas yaylarını sönümleri artırmadan yükseltme. |
| Hemen düşüyor veya yere gömülüyor | Ayakta durma yüksekliği modele uymuyor. Kurulum aracının modele özel profil kopyasını kullan ya da *BalanceProfile > Height > Stand Height*'ı pelvis yüksekliğine ayarla. Zeminin bir *Ground Layer* üzerinde olduğundan emin ol. |
| Tutulan nesneler fırlıyor | *Held Max Depenetration Velocity*'yi düşük tut (2 m/sn) ve kol *Max Torque* değerlerini insan ölçeğinde bırak. |
| Tırmanamıyor | Tutuş statik, kinematik ya da en az 200 kg bir cisimde, göğsün üstünde olmalı ve kenarın üstü tutuştan en fazla 0.9 m yukarıda olmalı. Kenar bir zemin katmanında olmalı. İki kolla kenarı tut ve Zıpla'ya bas. |
| Örnek sahnede pembe malzemeler | Projen başka bir render pipeline kullanıyor; sample malzemelerini yeniden ata. |
| Koşarken her adımda düşüyor | *BalanceProfile > Fall > Ignore Foot Impacts* açık kalmalı. |

## Bilinen sınırlamalar

- **Tırmanma basittir.** Gövdeyi gövdeye uygulanan bir kuvvet kaldırır; kollar sadece tutar.
- **Taşınan cisim sallanır.** Yürürken taşınan cisim adımlarla biraz sallanır, dururken sakinleşir.
- **Boştaki kollar** dinlenme pozunu korur ve yürürken sallanır.
- **`Breakable` mesh parçalamaz.** Olay fırlatır ve isteğe bağlı olarak önceden parçalanmış bir prefab koyar.
- **Gerçek modelle test edilmedi.** Farklı vücut oranları collider oranlarının ve ayakta durma yüksekliğinin ayarlanmasını gerektirebilir.

## Proje yapısı

```
Packages/com.wobblefolk.active-ragdoll/
├─ Runtime/
│  ├─ Core/          ActiveRagdoll, RagdollBone, BoneId, RagdollState, RagdollPhysicsSettings
│  ├─ Balance/       BalanceController, GroundProbe
│  ├─ Locomotion/    LocomotionController
│  ├─ Arms/          ArmController, CarryController, ClimbController
│  ├─ Interaction/   Breakable
│  ├─ Camera/        RagdollFollowCamera
│  ├─ Input/         RagdollPlayerInput (ayrı assembly, Input System'e bağlı)
│  ├─ Building/      PrimitiveRagdollBuilder, HumanoidRagdollBuilder, RagdollProfileSet
│  ├─ Profiles/      Ragdoll, Balance, Locomotion, Arm, Carry, Climb profilleri
│  ├─ Debugging/     RagdollDebugTools
│  └─ Utilities/     JointMath
├─ Editor/           Build Active Ragdoll penceresi, GameObject menüleri
└─ Samples~/Playground/  örnek sahne, profilleri ve malzemeleri
```

## Atıflar ve lisans

**MIT Lisansı** ile yayınlanmıştır; paketteki `LICENSE.md` dosyasına ya da repo kökündeki `LICENSE` dosyasına bakın.

Bütün kod bu proje için sıfırdan yazılmıştır. Bazı açık kaynak projeler (MIT / Apache-2.0) yalnızca mimari fikir
için incelenmiştir; `THIRD_PARTY_NOTICES.md` dosyasında listelenmiştir.
