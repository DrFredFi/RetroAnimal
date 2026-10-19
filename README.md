# Retro Animal Puzzle

A Windows desktop falling-block puzzle game in the spirit of mid-90s animal-and-food arcade puzzlers:
cartoon animals, food that gets eaten, chain reactions, versus against the computer or a second player, and the
classic two-field arena layout. Built with C# / .NET 10, Windows Forms as host and SkiaSharp for all rendering.

All graphics are original vector drawings made in code and all sounds and music are synthesized at start-up.
No asset from any commercial game is used.

## Requirements

- Windows 10 version 2004 (build 19041) or later
- .NET 10 SDK (for building)

## Build, test and run

```
dotnet restore
dotnet build
dotnet test
dotnet run --project src/RetroAnimalPuzzle
```

For a faster build, use `dotnet run --project src/RetroAnimalPuzzle -c Release`.
The solution opens in Visual Studio 2022 and in JetBrains Rider (`RetroAnimalPuzzle.sln`).

## How to play

Pairs of blocks fall into your 6 × 11 field. Each block is an **animal**, a **food** or, occasionally, a **coin**.

| Animal | Eats |
|---|---|
| Monkey | Banana |
| Rabbit | Carrot |
| Panda | Bamboo |
| Dog | Bone |

(A Cat → Fish pair is also implemented; enable it in `GameRules.Animals`.)

- **Eating**: when an animal touches its food orthogonally (up, down, left or right, never diagonally), it eats
  that food *and every food of the same kind connected to it*: it hops from tile to tile over the food, chewing
  each one, then flies to its heart leaving a trail of stars. The points and the heart counter (food eaten) go up
  when it arrives. It works both ways: the animal can land next to the food, or the food next to the animal.
- **Chains**: after a meal, unsupported blocks fall. If they bring another animal into contact with its food, that
  is eaten too. Each further step multiplies the points (×1, ×2, ×4, ×6, ×8, ×12, ×16).
- **Coins**: a coin that lands on a block removes every block of that exact kind on the whole field.
  A coin that lands on the floor just disappears.
- **No instant drop**: as in the classic game, Down only accelerates the fall (progressively); there is no hard
  drop (`GameRules.HardDropEnabled` turns one on). The landing ghost is off by default (Options).
- **Pairs split**: if one half of a horizontal pair lands on something and the other half hangs over a gap, the
  hanging half falls on its own.
- **Game over**: when blocks are left above the visible field, or a new pair has no room to appear.

### Modes

- **Start Game (Endless)**: single player. Every 15 food eaten raises the level and the falling speed.
  You get bonus points for each level completed and for rows dropped faster.
- **Versus CPU**: the classic arena with two fields. Food you eat becomes extra food dropped onto the CPU's field
  (`food × chain ÷ 2` blocks per step), with a sound that grows with the amount sent. The heart on the bar above
  each field shows food on its way: the further it travels from the centre, the more is coming. Food sent to you
  is first cancelled by what you eat. Top out the CPU to win a round (stars beside the hearts); two rounds win the stage. Each stage brings a faster, smarter opponent and the
  falling speed also rises during a round. The CPU level (Easy / Normal / Hard) is set in Options.
- **2 Players**: the same versus rules for two people sharing one keyboard. The left player uses W A S D (Q rotates
  the other way), the right player the arrow keys; each player's keys are shown under the fields during "READY?".
  The match ends after two won rounds with a rematch option; two-player scores are not ranked in the high scores.

### Controls (defaults)

| Key | Action |
|---|---|
| Left / Right | Move (hold to auto-repeat) |
| Up or X | Rotate clockwise (falling tiles keep turning on themselves: food tiles like a card, animals turning their head; rotating adds a quick extra half turn) |
| Z | Rotate counter-clockwise |
| Down (hold) | Drop faster: the speed builds up the longer you hold it |
| P or Esc | Pause / resume |
| Enter | Confirm |
| Esc | Back |
| R | Retry on the game-over panel |
| F1 | Help (pauses the game) |
| F11 or Alt+Enter | Toggle fullscreen |
| F3 | FPS / debug overlay |

Left player in **2 Players**: A / D move, W rotates clockwise, Q counter-clockwise, S (hold) drops faster.
The right player uses the main keys above. Pause, Esc and the menu keys are shared.

### Controller (Xbox-compatible, XInput)

Any Xbox-style controller works, wired or wireless, up to four. By default the gameplay uses the digital controls:
D-pad left / right move, D-pad down (hold) drops faster, A (or D-pad up) rotates clockwise, B counter-clockwise,
Menu (Start) pauses. In menus the D-pad or left stick navigates, A or Menu confirms, B goes back; View always
pauses and goes back. The gameplay buttons, sticks and triggers included, can be remapped in
**Options → Controller**, which also turns controllers on or off. One mapping is shared by every controller.

In **2 Players** the first connected controller plays the left field and the second the right (the first one
can be moved to the right in Options → Controller); the keyboard keeps working for both players, so one player can
use a controller and the other the keyboard. In the other modes every controller drives the single player.

Gameplay keys (move, drop faster, rotate, pause) can be reassigned in **Options → Keyboard**, in two groups: the
main keys (one player, and the right player in 2 Players) and the left player's keys. Assigning a key that is
already in use, in either group, swaps the two actions. Menu keys (arrows, Enter, Esc) are fixed. The game auto-pauses when
the window loses focus.

## Saved data

Saved under `%LOCALAPPDATA%\RetroAnimalPuzzle\`:

- `settings.json`: volumes, animation intensity, ghost piece, fullscreen, CPU level, key bindings
- `highscores.json`: top 10 per mode

Both files are versioned JSON written atomically. Missing, corrupt, unreadable or future-version files are
ignored and defaults are used; the game never fails because of them.

## Optional custom assets

The game needs no asset files, but it picks up replacements if you add them next to the executable:

- `Assets/Sprites/<name>.png` replaces a block drawing. Names: `monkey`, `rabbit`, `panda`, `dog`, `cat`
  (plus `<animal>_eat` for the eating pose), `banana`, `carrot`, `bamboo`, `bone`, `fish`, `coin`.
- `Assets/Audio/<sound>.wav` replaces a sound effect (`eat.wav`, `chain.wav`, `coin.wav`... one per value of the
  `Sound` enum, lower-case), and `music_menu.wav` / `music_game.wav` replace the music. Any sample rate and
  mono/stereo are accepted.

Only use assets you own or that are properly licensed.

## Architecture

```
src/RetroAnimalPuzzle.Core/        net10.0, no UI dependencies (all game rules, fully unit-tested)
  Core/        Block types, Position, Grid, Piece, GameRules (every tunable number), RandomPieceGenerator, Viewport
  Gameplay/    CollisionSystem, PieceController (input → movement, auto-repeat, wall kicks), MatchDetector (BFS),
               GravitySystem, MatchResolver, ScoreSystem, LevelSystem, GameOverDetector,
               PlayerBoard (per-board state machine), SinglePlayerSession, VersusSession, CPU (CpuPlanner,
               CpuController, CpuSkill), GameEvent
  Input/       GameAction, ActionSet / InputState (allocation-free per-tick input snapshot), GamepadButtons,
               GamepadBindings (controller mapping, pad roles)
  Persistence/ JsonStore, SaveData / SaveService, SettingsData / SettingsService

src/RetroAnimalPuzzle/             net10.0-windows10.0.19041.0, WinForms + SkiaSharp + NAudio
  Application/ GameApplication (composition root), GameLoop (fixed 60 Hz step), GameContext (shared services)
  Host/        GameForm, GameSurface (SKControl), NativeMethods (idle loop, timer resolution)
  Input/       InputManager (keys + controllers → InputState), KeyboardBindings (defaults + rebinding),
               XInputGamepads (controller polling)
  Rendering/   GameRenderer (letterboxed 800×600), ArenaRenderer, PlayfieldRenderer, BlockArt, MascotRenderer,
               UiRenderer, AnimationSystem, ParticleSystem, EmbossedPattern, Fonts, Palette, ArenaLayout
  Audio/       IAudioService, AudioService (NAudio mixer), NullAudioService, Synth (effects + music)
  UI/          ScreenManager (stack + fades), Title, MainMenu, Endless, Versus, Help, Options, Controls
               (keyboard), GamepadControls, HighScores

tests/RetroAnimalPuzzle.Tests/     xUnit tests for Core: grid, movement, matching, coins, gravity, chains,
                                   scoring, levels, board state machine, game over, generator, versus/CPU,
                                   persistence
```

Key design points:

- **The grid is the single source of truth.** Blocks are value types (`default` = empty cell), stored in a flat
  array. Screen coordinates are derived only in the renderers.
- **Deterministic state machines.** Each `PlayerBoard` moves explicitly through
  `Spawning → Falling → (lock) → Settling ⇄ Eating → [garbage] → Spawning` or `ToppedOut`. Every step of a chain is
  its own timed phase, so animations never decide gameplay. The same seed and inputs always give the same game.
  `VersusSession` adds `Ready → Playing → RoundOver → MatchOver`, and the screens add pause and confirmation on top.
- **Fixed timestep.** The simulation advances in exact 1/60 s steps from an `Application.Idle` loop. Rendering is
  separate, capped at 60 FPS, and waits with `MsgWaitForMultipleObjectsEx` (no `Thread.Sleep`, input stays
  responsive). Nothing gameplay-related happens in paint events.
- **Events, not inspection.** Boards emit `GameEvent`s (moved, landed, eat with chain and points, garbage...). Sound,
  particles, score popups and announcements react to events only.
- **Human and CPU are equal.** The CPU produces the same `InputState` a keyboard does. It brute-forces every column
  and rotation, simulates the full chain resolution, and scores the result; skill controls reaction time, key
  speed, evaluation noise and blunders.
- **Rendering cost.** Block drawings are recorded once into `SKPicture`s. The static arena (embossed background,
  wells, frames) is rasterised once per window size, so a frame costs about 1.5 to 6 ms of drawing.

## Developer tools

The executable has four hidden command-line switches that never open a window or touch saved data:

| Switch | Output |
|---|---|
| `--snapshot <dir>` | PNG of every screen, including a simulated endless game, a versus match and a two-player match |
| `--gallery <file.png>` | All block drawings in both poses |
| `--bench <file.txt>` | Draw cost per frame for each screen, plus CPU-opponent strength per difficulty |
| `--icon <file.ico>` | Regenerates the application icon (the bamboo tile, 16 to 256 px); the project uses `src/RetroAnimalPuzzle/Assets/app.ico` |

## Known limitations

- Rendering is CPU-based (`SKControl`). Drawing is cheap, but copying each frame to the window through GDI makes the
  process use roughly a quarter of one CPU core at 1024×768. There is no vsync, so occasional tearing is possible.
- The CPU opponent picks the best immediate placement (including chains it can see happen right away), but does
  not deliberately build multi-step chains.
- Only XInput (Xbox-style) controllers are supported; other gamepads need an XInput wrapper. There is no mouse
  support. Some keyboards cannot register many keys held at once, so when two players share a keyboard a held key
  may occasionally be missed.
- Only gameplay keys can be rebound; menu keys are fixed.
- High-score entries have no player name.
- Fonts come from Windows (Arial Black, Comic Sans MS, Consolas, Arial), with a fallback to the default typeface.
- Scoring, garbage and speed formulas are original design choices, not a reproduction of any existing game.
