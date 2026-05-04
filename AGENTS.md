# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Project Overview

**Logophile** - a 2-player networked typing game built in Unity 6000.3.6f1. Players compete by typing valid English words matching prompted constraints (StartWith, Contains, EndWith patterns). Words are validated against a Scrabble dictionary loaded from `Assets/Resources/`. If both players submit the same word, a Clash mini-game triggers.

## Build & Editor Workflow

No CI/CD, build scripts, or automated tests. Builds are done manually through the Unity Editor (File -> Build Settings). Target platforms: Windows (native) and WebGL.

Because there is no command-line test or build path, **Codex cannot self-verify code changes**. Edits compile only when the user opens the Editor. Flag this explicitly rather than claiming a change "works."

- **Main gameplay scene:** `Assets/Scenes/Game.unity`
- **UI implementation branch work scene:** `Assets/Scenes/NewUI_TestScene.unity`
- **Other scenes** (`Offseted`, `On Top`, `Test Host`, `temp`) are scratch/experimental. Do not modify them without checking with the user.
- **Local multiplayer testing:** Unity Multiplayer Play Mode (MPPM, `com.unity.multiplayer.playmode` 2.0.1) is installed. Use the *Multiplayer Play Mode* window to spawn virtual players; Editor + virtual player connect via the `ConnectionManager` auto-host/client fallback described below.

## Architecture

### Networking Model

Server-authoritative using **Netcode for GameObjects (NGO) 2.8.0**. The host (clientId=0) runs all game logic. Clients submit answers via `[Rpc(SendTo.Server)]` and receive state via NetworkVariables/NetworkLists. Server broadcasts via `[Rpc(SendTo.ClientsAndHost)]`.

**Connection flow:** `ConnectionManager` attempts `StartClient()` with a 2-second timeout. If no host responds, it auto-promotes to `StartHost()`. This enables ad-hoc P2P without dedicated server infrastructure.

### Singleton Hierarchy

Three-tier pattern in `Assets/Scripts/Utilities/`:

- **`Singleton<T>`** - standard MonoBehaviour singleton, destroys duplicates
- **`PersistentSingleton<T>`** - survives scene loads via DontDestroyOnLoad, lazy-creates if missing
- **`NetworkSingleton<T>`** - extends NetworkBehaviour, lazy-loads via FindAnyObjectByType, logs warnings on duplicates instead of destroying

### Core Managers (all NetworkBehaviours)

- **GameManager** - game state, constants: WinGameScore=50, MaxGameScore=70, MaxPlayerHp=20. Uses `GameStartedState` NetworkVariable to trigger start when 2 players connect.
- **RoundManager** - round state machine (see below).
- **PlayerManager** - tracks connected players (max 2), maps clientId -> Player. Triggers `StartGameServerRpc()` when player count reaches 2.
- **PromptGenerator** - creates word prompts using `Prompt` struct (INetworkSerializable) containing `PromptType` enum (None/StartWith/Contains/EndWith) and `PromptContent` enum (single letters A-Z minus F/J/Q/U/V/W/X/Z, plus common digraphs ER/ST/OR/IN/AN). Avoids repeats and consecutive same types. Filters out content containing banned letters.
- **ScoreManager** - player scores via `NetworkList<PlayerScoreData>`.
- **UIManager** - manages screen transitions.
- **AudioManager / SoundManager** - FMOD audio integration.

### Round State Machine (`RoundManager`)

Three-phase cycle:

1. **Round Phase** (~30s) - players type and submit. Timer accelerates 2x after the first player submits. `SubmitAnswerServerRpc()` tracks submissions, then triggers resolution when count >= 2 or timeout.
2. **Resolution Phase** (3s) - review answers, calculate letter-count difference as HP damage via `ResoluteServerRpc()`. Both players confirm with Space (`ConfirmResolutionServerRpc`).
3. **Clash Phase** (10s, optional) - triggered when both players submit the same word.

After resolution: check win condition (HP <= 0 = loss) -> either `EndGameClientRpc()` or `EnterNextRound()`.

**Banned Letter Mechanic:** Every 3rd round, the most-frequent letter from submitted answers is banned. Players can toggle this with the equals key.

### Input Submission Pipeline (`Client.cs`)

1. Player types -> `OnLocalInputFieldChanged()` -> `UpdateServerAnswerServerRpc()` (syncs to server).
2. Enter pressed -> `TrySubmitAnswer()` validates locally:
   - Dictionary check (`WordChecker.CheckWordDictionaryValidity`) - HashSet<string> for O(1) lookup, case-insensitive
   - Prompt constraint check (`CheckWordPromptValidity`) - StartsWith/Contains/EndsWith based on PromptType
   - Already-used word check (local `m_usedAnswers` list)
   - Banned letter check (`RoundManager.HasBannedLetterInAnswer`)
3. If valid -> `_roundManager.SubmitAnswerServerRpc()`.

## UI Screen Flow

Two flows coexist in the repo. Check the current branch before editing UI.

**Legacy flow** (on `main`, prefabs in `Assets/Prefabs/UI/`: `ConnectScreen`, `WaitingScreen`, `GameScreen`, `ResolveScreen`, `WinScreen`):

```text
MainMenuUI -> ConnectionScreenUI -> WaitingScreenUI -> GameScreenUI
                                                      |
                                             ResolutionScreenUI
                                                      |
                                             ClashScreenUI (optional)
                                                      |
                                             WinScreenUI
```

**New XD-driven flow** (implementation branches, prefabs in `Assets/Prefabs/UIDesign/` and `Assets/Prefabs/UI/MainUI.prefab`):

```text
StartScreen -> Tutorial -> CreateJoin -> WaitingRoom -> Loading ->
PromptShowcase -> Gameplay -> RoundResult -> GameEnd
```

The new flow uses `Assets/Prefabs/UI/MainUI.prefab` with a single `MainUIController` and state enum for animation previews. It is presentation-only until gameplay/network logic is explicitly wired.

Current implementation notes:

- Shared objects morph across states via DOTween instead of swapping independent panels.
- State visibility is controlled by serialized `State Groups` on `MainUIController`; new page groups should be added there instead of being spawned into the scene at runtime.
- Prompt and gameplay UI content lives under prefab-owned groups (`PromptSharedGroup`, `GameplayElementsGroup`) inside `MainUI.prefab`. `MainUIController` may build missing prefab-owned UI while editing the prefab asset, but runtime scene-owned UI creation is disabled.
- Waiting -> Loading uses a white/off-white wipe layer (`LoadingScreenRoot`). Loading auto-advances to PromptShowcase after a short delay.
- PromptShowcase uses shared prompt text/mask elements. The black mask enters, prompt labels fade in place, then the mask slides to reveal the final prompt and banned-letter highlight.
- PromptShowcase -> Gameplay is automatic after a short delay. The prompt/banned text morph to gameplay positions, player icons move into gameplay positions, and the shared `InputField (TMP)` morphs into the gameplay input area. Gameplay auto-focuses this input field after the transition.
- Gameplay currently implements presentation-only timer bar preview, player icons, and per-player letter-count blocks. Letter-count blocks reflect typed word length; differential blocks use player colors, and banned-letter input flashes blocks red/gray.
- The debug navigation key `Y` advances through the new flow for animation review.

UI uses Unity UI (Canvas + TMP) for the project's own screens. `Assets/Blocks/` is a **third-party Unity sample kit** (Multiplayer Widgets / Sessions building blocks - `CopySessionCode`, `LeaveSession`, `PlayerList`, etc.) and is not the project's own UI code; treat it as a vendored package.

## Key Dependencies

- **Netcode for GameObjects** 2.8.0 - multiplayer networking
- **FMOD Studio** - audio middleware (plugin in `Assets/Plugins/FMOD/`, not in manifest)
- **DOTween** (Demigiant) - tweening/animation (asset, not in manifest)
- **Odin Inspector** (Sirenix) - editor tooling (asset, not in manifest)
- **Unity Input System** 1.18.0

## Asset Layout

- **Scenes:** `Assets/Scenes/Game.unity` (main), `Assets/Scenes/NewUI_TestScene.unity` (UI rewrite)
- **Prefabs:**
  - `Assets/Prefabs/Managers.prefab` - container for all manager NetworkBehaviours, spawned in scene
  - `Assets/Prefabs/Player.prefab` - player NetworkObject
  - `Assets/Prefabs/UI/` - legacy screen prefabs plus `Menu.prefab` and new `MainUI.prefab`
  - `Assets/Prefabs/UIDesign/` - new UI design/reference prefabs and screenshots
  - `Assets/Prefabs/Obsolete/` - do not modify
- **Resources** (loaded at runtime via `Resources.Load`):
  - `Assets/Resources/Scrabble Dictionary.txt` - word list (loaded by `WordChecker`)
  - `Assets/Resources/EntryPrompts.json` - themed prompt data
  - XD/reference images currently live as loose Resources images such as `ui reference.png`, `1.0 1.png`, `2.0 1.png`, and `3.0 1.png`; there is no `Assets/Resources/Design/` folder in this checkout.
  - Audio/image assets also live at the root of `Resources/`.

## Project Conventions

- Managers use the three-tier singleton pattern from `Assets/Scripts/Utilities/`.
- Network-synced state: NetworkVariables for continuous sync (HP, timers, letter count), NetworkLists for collections (scores), RPCs for discrete events.
- Word dictionary is a TextAsset loaded via `Resources.Load("Scrabble Dictionary")` into a HashSet.
- `Assets/Scripts/Obsolete/` contains deprecated code (old grid-based letter system). Do not modify it.
- `Assets/Scripts/Network Test/` contains networking test scripts. Treat it as non-production code.
- Custom event system via `EventBetter` (publish/subscribe by message type) is used for scene-load notifications and other cross-system events where a direct reference would couple unrelated managers. Prefer direct references for one-to-one manager wiring; reach for EventBetter when the publisher should not know who the listeners are.
- Extension methods live in `Assets/Scripts/Utilities/ExtensionMethods.cs`.
