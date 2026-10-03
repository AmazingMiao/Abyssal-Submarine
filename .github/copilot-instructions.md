# Abyssal Submarine - AI Coding Instructions

## Project Overview
- **Engine:** Unity 2D (C#)
- **Core Systems:** Physics-based movement, Procedural Radar UI, Hardware Integration (Thermal Printer, ESP32).
- **Input System:** `UnityEngine.InputSystem` (Primary) & Custom Serial Port (Experimental/Hardware).
- **Physics:** 2D Physics (`Rigidbody2D`, `Collider2D`).
- **UI:** Unity UI & TextMeshPro (`TMPro`).

## Architecture & Core Systems

### Player Controller (`PlayerController.cs`)
- **Movement:** Physics-based in `FixedUpdate`. Uses custom inertia logic to blend `currentVelocity` with target velocity.
- **Input:** Uses generated `PlayerInputActions` class.
  - **Events:** Subscribe in `OnEnable`, unsubscribe in `OnDisable`.
  - **Logic:** `inputActions.Player.Move` drives velocity; `inputActions.Player.Rotate` drives rotation.
- **Health:** Integer-based (`currentHealth`) with `UnityEvent onDeath` for external hooks (UI, Game Over).
- **Collision:** Detects "Wall" tag in `OnCollisionEnter2D` for damage and knockback.

### Hardware Integration
#### Thermal Printer (`PrinterController.cs`)
- **Driver:** Uses `RawPrinterHelper` to send raw bytes to Windows printer spooler.
- **Formatting:** `EscPosUtil` builds ESC/POS commands (Text, Images, Cuts).
- **Tags:** Supports custom tags in text: `[BOLD]`, `[CENTER]`, `[INVERSE]`, `[Feed lines & Cut]`.
- **Setup:** `printerName` in Inspector must match Windows Control Panel name exactly.

#### Custom Controller (`ESP32InputController.cs`)
- **Communication:** Reads raw serial data (COM port) on a separate thread.
- **Data Format:** Expects CSV string: `x,y,buttonState` (e.g., "0.5,-0.2,1").
- **Access:** Exposes public `JoystickInput` (Vector2) and `ButtonPressed` (bool) for other scripts to poll.
- **Note:** Currently decoupled from `PlayerController`. To use, scripts must reference this component directly.

### Radar System (`SubmarineRadarGenerator.cs`)
- **Procedural UI:** Generates radar segments dynamically from templates (`segmentBarTemplate`).
- **Detection:** Raycasts to detect obstacles and updates UI elements (distance text, bar color/alpha).
- **Audio:** Directional audio filtering (LowPassFilter) based on obstacle position relative to player.
- **Configuration:** Heavily relies on Inspector settings (`[Header]`, `[Tooltip]`).

### Interaction (`CameraController.cs`)
- **Trigger-based:** Uses `Collider2D` triggers to detect interaction zones.
- **State:** Persists interaction state (e.g., assigned photos) using `Dictionary<Collider2D, Sprite>`.
- **Input:** Checks `Keyboard.current.gKey` directly in `Update`.

## Coding Standards

### Unity Conventions
- **Inspector:** Use `[Header]`, `[Tooltip]`, and `[Range]` to organize public fields.
- **Components:** Use `[RequireComponent(typeof(...))]` to ensure dependencies.
- **Fields:** Public fields are used for Inspector access (preferred over `[SerializeField] private` in this codebase).
- **Math:** Use `Mathf` for calculations (Lerp, Clamp).
- **Tags:** Use `CompareTag("Tag")` instead of string equality.

### Input Handling
- **Standard:** Prefer `inputActions.Player.Action.performed += ...` for continuous/event-based input.
- **Direct Polling:** `Keyboard.current.key.wasPressedThisFrame` is acceptable for simple toggles.
- **Hardware:** For ESP32, ensure thread safety when reading `lastReceivedData`. Use the provided `JoystickInput` public field in `Update`.

### Physics
- **Update Loop:** Apply forces/velocity in `FixedUpdate`.
- **Inertia:** Use `Vector2.Lerp` or `MoveTowards` to smooth velocity changes, simulating submarine weight.

## Key Workflows

### Adding New Features
1.  **Input:**
    - Standard: Update `.inputactions`, regenerate C#, subscribe in `PlayerController`.
    - Custom: Update ESP32 firmware to send new data, parse in `ESP32InputController`.
2.  **Printing:**
    - Add new commands to `EscPosUtil`.
    - Call `DoPrint()` on `PrinterController` with formatted string.
3.  **UI:** Create Prefab/Template, assign in Inspector, instantiate at runtime.
4.  **Events:** Use `UnityEvent` to decouple logic (e.g., UI updates on player death).

### Debugging
- **Gizmos:** Use `OnDrawGizmosSelected` to visualize physics/logic (e.g., velocity vectors).
- **Logs:** `Debug.Log` for critical state changes (damage, death, interaction).
- **Printer:** Check `Debug.LogError` in `PrinterController` if printing fails (usually name mismatch).
