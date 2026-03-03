# TopDown_Mechanics

> A Unity 6 third-person/first-person hybrid shooter inspired by the perspective-shifting gameplay of **Rainbow Six Vegas**

![Gameplay Preview](docs/gifs/Unity_R6Nir8YDUr.gif)

---

## Concept

Most shooters pick a lane you're either top-down or first-person. **This Project** doesn't.

Movement, traversal, and melee happen in a **top-down third-person view**. The moment you raise a gun to aim, the camera **smoothly blends into first-person** putting you directly behind the sights. Release aim, and you're pulled back out into the tactical overview.

The result is a game that feels strategic and spatial when moving, and tense and grounded when shooting.

---

## Showcase

### Perspective Switch
![Perspective Switch](docs/gifs/PerspectiveSwitch.gif)

### Raycast Shooting & Line Trace
![Shooting](docs/gifs/LineTraceShooting.gif)

### Top-Down Movement
![Movement](docs/gifs/Movement.gif)

---

## Systems

###  Perspective System
Cinemachine-driven blend between two camera rigs top-down third-person for movement, first-person for aiming. Managed by a central `CameraManager` with smooth lerp transition.

###  Player Movement
Rigidbody-based movement with smooth acceleration and deceleration. Character rotates to face the mouse cursor in top-down mode. Speed automatically reduces when entering FPS aim mode.

###  Weapon System
Raycast-based shooting with semi-auto and full-auto fire mode toggle. Includes procedural Aim Down Sights (ADS) via weapon mesh lerp, muzzle-origin line trace visualization, and per-shot recoil with recovery curve.

![ADS](docs/gifs/ads.gif)

---

## Controls

| Action | Keyboard | Controller |
|--------|----------|------------|
| Move | WASD | Left Stick |
| Sprint | Left Shift | Button South (A) |
| Aim (FPS Mode) | Right Mouse Button | Left Trigger |
| Fire | Left Mouse Button | Right Trigger |
| Toggle Fire Mode | B | D-Pad Up |

---

## Tech Stack

| Tool | Version |
|------|---------|
| Unity | 6000.3.10f1 LTS |
| Render Pipeline | Universal Render Pipeline (URP) |
| Camera System | Cinemachine |
| Input | Unity New Input System |
| Language | C# |

---

## Project Structure

```
Assets/
├── Scripts/
│   ├── Player/
│   │   └── PlayerController.cs
│   ├── Managers/
│   │   └── CameraManager.cs
│   └── Weapons/
│       └── WeaponHandler.cs
├── Inputs/
│   └── PlayerInputActions.inputactions
└── Scenes/
    └── ...
```

---

## Roadmap

- [x] PlayerController — movement, sprint, cursor rotation
- [x] CameraManager — top-down to FPS perspective blend
- [x] WeaponHandler — raycast shooting, ADS, line trace, fire mode toggle
- [ ] AnimatorController — walk, aim, shoot, hurt, death states
- [ ] DashAbility — directional dash with i-frames
- [ ] DodgeRoll — roll with AnimationCurve feel
- [ ] KnockbackHandler — external force receiver
- [ ] HealthComponent — damage, death, hit reactions
- [ ] Enemy AI — behavior tree driven

---

## Author

**Jake Fernandez** — [@JakeeUp](https://github.com/JakeeUp)  
M.F.A. Game Programming — University of the Incarnate Word
