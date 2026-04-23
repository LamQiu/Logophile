# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Logophile** — a 2-player networked typing game built in Unity 6000.3.6f1. Players compete by typing valid English words matching prompted constraints (StartWith, Contains, EndWith patterns). Words are validated against a Scrabble dictionary loaded from `Assets/Resources/`. If both players submit the same word, a Clash mini-game triggers.

## Build & Editor Workflow

No CI/CD, build scripts, or automated tests. Builds are done manually through the Unity Editor (File → Build Settings). Target platforms: Windows (native) and WebGL.

Because there's no command-line test or build path, **Claude cannot self-verify code changes** — edits compile only when the user opens the Editor. Flag this explicitly rather than claiming a change "works."

- **Main gameplay scene:** `Assets/Scenes/Game.unity`
- **UI-Implementation branch work scene:** `Assets/Scenes/NewUI_TestScene.unity`
- **Other scenes** (`Offseted`, `On Top`, `Test Host`, `temp`) are scratch/experimental — do not modify without checking with the user.
- **Local multiplayer testing:** Unity Multiplayer Play Mode (MPPM, `com.unity.multiplayer.playmode` 2.0.1) is installed. Use the *Multiplayer Play Mode* window to spawn virtual players; Editor + virtual player connect via the `ConnectionManager` auto-host/client fallback described below.

## Architecture

### Networking Model
Server-authoritative using **Netcode for GameObjects (NGO) 2.8.0**. The host (clientId=0) runs all game logic. Clients submit answers via `[Rpc(SendTo.Server)]` and receive state via NetworkVariables/NetworkLists. Server broadcasts via `[Rpc(SendTo.ClientsAndHost)]`.

**Connection flow**: `ConnectionManager` attempts `StartClient()` with a 2-second timeout. If no host responds, auto-promotes to `StartHost()`. This enables ad-hoc P2P without dedicated server infrastructure.

### Singleton Hierarchy
Three-tier pattern in `Assets/Scripts/Utilities/`:
- **`Singleton<T>`** — standard MonoBehaviour singleton, destroys duplicates
- **`PersistentSingleton<T>`** — survives scene loads via DontDestroyOnLoad, lazy-creates if missing
- **`NetworkSingleton<T>`** — extends NetworkBehaviour, lazy-loads via FindAnyObjectByType, logs warnings on duplicates instead of destroying

### Core Managers (all NetworkBehaviours)
- **GameManager** — game state, constants: WinGameScore=50, MaxGameScore=70, MaxPlayerHp=20. Uses `GameStartedState` NetworkVariable to trigger start when 2 players connect.
- **RoundManager** — round state machine (see below)
- **PlayerManager** — tracks connected players (max 2), maps clientId → Player. Triggers `StartGameServerRpc()` when player count reaches 2.
- **PromptGenerator** — creates word prompts using `Prompt` struct (INetworkSerializable) containing `PromptType` enum (None/StartWith/Contains/EndWith) and `PromptContent` enum (single letters A-Z minus F/J/Q/U/V/W/X/Z, plus common digraphs ER/ST/OR/IN/AN). Avoids repeats and consecutive same types. Filters out content containing banned letters.
- **ScoreManager** — player scores via `NetworkList<PlayerScoreData>`
- **UIManager** — manages screen transitions
- **AudioManager / SoundManager** — FMOD audio integration

### Round State Machine (`RoundManager`)
Three-phase cycle:
1. **Round Phase** (~30s) — players type and submit. Timer accelerates 2x after first player submits. `SubmitAnswerServerRpc()` tracks submissions, triggers resolution when count ≥ 2 or timeout.
2. **Resolution Phase** (3s) — review answers, calculate letter count difference as HP damage via `ResoluteServerRpc()`. Both players confirm with Space (`ConfirmResolutionServerRpc`).
3. **Clash Phase** (10s, optional) — triggered when both players submit the same word.

After resolution: check win condition (HP ≤ 0 = loss) → either `EndGameClientRpc()` or `EnterNextRound()`.

**Banned Letter Mechanic**: Every 3rd round, the most-frequent letter from submitted answers is banned. Players can toggle this with the equals key.

### Input Submission Pipeline (`Client.cs`)
1. Player types → `OnLocalInputFieldChanged()` → `UpdateServerAnswerServerRpc()` (syncs to server)
2. Enter pressed → `TrySubmitAnswer()` validates locally:
   - Dictionary check (`WordChecker.CheckWordDictionaryValidity`) — HashSet<string> for O(1) lookup, case-insensitive
   - Prompt constraint check (`CheckWordPromptValidity`) — StartsWith/Contains/EndsWith based on PromptType
   - Already-used word check (local `m_usedAnswers` list)
   - Banned letter check (`RoundManager.HasBannedLetterInAnswer`)
3. If valid → `_roundManager.SubmitAnswerServerRpc()`

### UI Screen Flow

Two flows coexist in the repo — check the current branch before editing UI:

**Legacy flow** (on `main`, prefabs in `Assets/Prefabs/UI/`: `ConnectScreen`, `WaitingScreen`, `GameScreen`, `ResolveScreen`, `WinScreen`):
```
MainMenuUI → ConnectionScreenUI → WaitingScreenUI → GameScreenUI
                                                      ↓
                                             ResolutionScreenUI
                                                      ↓
                                             ClashScreenUI (optional)
                                                      ↓
                                             WinScreenUI
```

**New flow** (on `UI-Implementation` branch, driven by Adobe XD spec — see design brief in session context; prefabs in `Assets/Prefabs/UIDesign/` and `Assets/Prefabs/UI/MainUI.prefab`):
```
StartScreen → Tutorial → CreateJoin → WaitingRoom → Loading →
PromptShowcase → Gameplay → RoundResult → GameEnd
```

The new flow uses a single `GameUIManager` + state enum with shared GameObjects that morph between states via DOTween, rather than swapping independent panels.

UI uses Unity UI (Canvas + TMP) for the project's own screens. `Assets/Blocks/` is a **third-party Unity sample kit** (Multiplayer Widgets / Sessions building blocks — `CopySessionCode`, `LeaveSession`, `PlayerList`, etc.) and is not the project's own UI code; treat it as a vendored package.

## Key Dependencies
- **Netcode for GameObjects** 2.8.0 — multiplayer networking
- **FMOD Studio** — audio middleware (plugin in `Assets/Plugins/FMOD/`, not in manifest)
- **DOTween** (Demigiant) — tweening/animation (asset, not in manifest)
- **Odin Inspector** (Sirenix) — editor tooling (asset, not in manifest)
- **Unity Input System** 1.18.0

## Asset Layout
- **Scenes:** `Assets/Scenes/Game.unity` (main), `Assets/Scenes/NewUI_TestScene.unity` (UI rewrite)
- **Prefabs:**
  - `Assets/Prefabs/Managers.prefab` — container for all manager NetworkBehaviours, spawned in scene
  - `Assets/Prefabs/Player.prefab` — player NetworkObject
  - `Assets/Prefabs/UI/` — legacy screen prefabs + new `Menu.prefab` / `MainUI.prefab`
  - `Assets/Prefabs/UIDesign/` — new UI design prefabs (current UI-Implementation work)
  - `Assets/Prefabs/Obsolete/` — do not modify
- **Resources** (loaded at runtime via `Resources.Load`):
  - `Assets/Resources/Scrabble Dictionary.txt` — word list (loaded by `WordChecker`)
  - `Assets/Resources/EntryPrompts.json` — themed prompt data
  - `Assets/Resources/Design/` — reference screenshots from Adobe XD spec (`2p_1`…`2p_9`)
  - Audio/image assets also live at the root of `Resources/`

## Project Conventions
- Managers use the three-tier singleton pattern from `Assets/Scripts/Utilities/`
- Network-synced state: NetworkVariables for continuous sync (HP, timers, letter count), NetworkLists for collections (scores), RPCs for discrete events
- Word dictionary is a TextAsset loaded via `Resources.Load("Scrabble Dictionary")` into a HashSet
- `Assets/Scripts/Obsolete/` contains deprecated code (old grid-based letter system) — do not modify
- `Assets/Scripts/Network Test/` contains networking test scripts — not production code
- Custom event system via `EventBetter` (publish/subscribe by message type) — used for scene-load notifications and other cross-system events where a direct reference would couple unrelated managers. Prefer direct references for one-to-one manager wiring; reach for EventBetter when the publisher shouldn't know who the listeners are.
- Extension methods in `Assets/Scripts/Utilities/ExtensionMethods.cs`
