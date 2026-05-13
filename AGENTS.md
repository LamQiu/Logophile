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

- **GameManager** - game state, constants: WinGameScore=50 and MaxGameScore=70. Max HP is read from `GameplayTestManager.EffectiveMaxPlayerHp` when a test manager exists, otherwise the fallback is 20. Tracks `GameStartedState` plus `P1Ready` / `P2Ready`; in the new MainUI lobby flow the match starts only after both clients submit `ready`.
- **RoundManager** - round state machine (see below). In the MainUI lobby flow it generates the first prompt before the UI leaves Loading and starts the round timer only after both clients report that Gameplay UI has been entered.
- **PlayerManager** - tracks connected players (max 2), maps clientId -> Player, and unregisters players on despawn/disconnect. It no longer auto-starts the game when player count reaches 2; ready flow is owned by `GameManager`.
- **PromptGenerator** - creates word prompts using `Prompt` struct (INetworkSerializable) containing `PromptType` enum (None/Entry/StartWith/Contains/EndWith) and `PromptContent` enum (single letters A-Z minus F/J/Q/U/V/W/X/Z, plus common digraphs ER/ST/OR/IN/AN). Avoids repeats and consecutive same types. Filters out content containing banned letters. Can exclude Entry prompts via `_excludeEntryPromptType`, and pushes prompt text/banned-letter data to MainUI through `RoundManager.UpdateMainUiPromptClientRpc`.
- **ScoreManager** - player scores via `NetworkList<PlayerScoreData>`.
- **UIManager** - manages screen transitions.
- **AudioManager / SoundManager** - FMOD audio integration.
- **GameplayTestManager** - optional dev/test singleton in `Assets/Scripts/Debug/`. Inspector flags control preset room word, skip-both-ready behavior, and match tuning values for max HP, round duration, post-submit speed multiplier, and resolution duration. Production code falls back when no instance exists.

### Round State Machine (`RoundManager`)

Three-phase cycle:

1. **Round Phase** (default 15s) - players type and submit. Timer accelerates 3x after the first player submits by default. `SubmitAnswerServerRpc()` tracks submissions, then triggers resolution when count >= 2 or timeout. Time values are read from `GameplayTestManager` when present.
2. **Resolution Phase** (default 12s) - review answers, calculate letter-count difference as HP damage via `ResoluteServerRpc()`. Both players confirm with Space (`ConfirmResolutionServerRpc`).
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

**New XD-driven / MainUI flow** (now wired on `main` as an optional gameplay flow, prefabs in `Assets/Prefabs/UIDesign/` and `Assets/Prefabs/UI/MainUI.prefab`):

```text
StartScreen -> Tutorial -> RoomId -> WaitingRoom -> Loading ->
PromptShowcase -> Gameplay -> RoundResult -> GameEnd
```

The new flow uses `Assets/Prefabs/UI/MainUI.prefab` with a single `MainUIController` and state enum. `UIManager.m_useMainUIForGameplay` selects whether gameplay routes through MainUI or the legacy screens. When enabled, MainUI shares one TMP input across the start commands, room-code entry, waiting-room `ready`, and gameplay word input.

Current implementation notes:

- Shared objects morph across states via DOTween instead of swapping independent panels.
- State visibility is controlled by serialized `State Groups` on `MainUIController`; new page groups should be added there instead of being spawned into the scene at runtime.
- Prompt and gameplay UI content lives under prefab-owned groups (`PromptSharedGroup`, `GameplayElementsGroup`) inside `MainUI.prefab`. `MainUIController` may build missing prefab-owned UI while editing the prefab asset, but runtime scene-owned UI creation is disabled.
- Start accepts text commands through the shared input: `create` starts session creation and `join` enters RoomId. RoomId submits the typed room word to `ConnectionScreenUI.TriggerJoinSession`.
- WaitingRoom accepts the shared-input command `ready`. `GameManager.SetClientReadyServerRpc` records `P1Ready` / `P2Ready`; after both ready flags are true the server calls `RoundManager.BeginMatchFromLobbyServer`, generates a prompt, and broadcasts Loading/PromptShowcase entry.
- Waiting -> Loading uses a white/off-white wipe layer (`LoadingScreenRoot`). In networked match flow, Loading holds until `PromptGenerator` pushes the server prompt; design-preview loading can still auto-advance after a short delay.
- PromptShowcase uses shared prompt text/mask elements. The black mask enters with mask-phase copy above it; during reveal the same prompt/banned-letter text switches to the final rich-text content while the masks are moved above the text in sibling order, so banned-letter highlight color is revealed by the moving black masks instead of by spawning replacement text objects. PromptShowcase -> Gameplay is automatic after a short delay; clients notify the server when PromptShowcase and Gameplay entry complete.
- Gameplay is now wired to the owner `Client` input listener. The shared `InputField (TMP)` morphs into the gameplay input area, auto-focuses after transition, validates submit attempts through `Client.TrySubmitAnswer`, and updates MainUI timer/hint/letter-block views via `UIManager`.
- Gameplay letter-count blocks reflect synced `Client.LetterCount`, differential blocks use player colors, and banned-letter input flashes blocks red/gray. The current-player row should not be permanently taller than the other row; `MainUIController` keeps row scale at `Vector3.one`. A tentative letter-block pop tween exists but is marked TODO because refresh timing can swallow the visible effect. The timer bar changes color when `RoundManager.AnyPlayerSubmittedThisRound` accelerates the round.
- Gameplay player icon boxes keep their existing size, while the P1/P2 label font is slightly smaller only in gameplay to preserve breathing room; RoundResult icon labels intentionally keep the previous larger sizing.
- Gameplay -> RoundResult is driven by `RoundManager.EnterResolutionPhaseClientRpc`: MainUI waits for local HP NetworkVariables to match the server resolution snapshot, then the shared `InputField (TMP)` expands upward as the black RoundResult panel to wipe away Gameplay without a white Loading-screen flash. Existing gameplay prompt, player indicators, timer/letter blocks, and input content stay visible until the expanding black panel has covered them; only then are the gameplay layers hidden and RoundResult content prepared. The three decorative stripes reveal after the panel morph, bottom stripe first from the black panel, then middle from bottom, then top from middle. RoundResult prompt and banned-letter labels use TMP `maxVisibleCharacters` typewriter reveal instead of direct fade; player/word/death/score elements still fade in. Invalid answers are displayed as their typed text but are flagged non-eligible for scoring.
- RoundResult -> next round returns to Loading and waits for the next server prompt. Game end uses `MainUIController.TransitionToGameEnd` when MainUI gameplay is enabled; legacy `WinScreenUI` is still used otherwise.
- RoundResult -> GameEnd reuses existing UI objects instead of spawning a separate GameEnd page: `_roundResultPanel` morphs into the bottom black stripe, `_decorativeLines` move as one group into the three gray stripes, `_roundResultDeathLabelText` becomes the winner text, and `_pressSpaceGroup` becomes `press space to restart`. Do not add runtime-created GameEnd stripe/title objects unless the design explicitly changes.
- Standard stripe layout is centralized in `MainUIController`: `GetStandardStripeSize()` returns 1800x20 and stripe-to-stripe spacing uses `StandardUiGap`. GameEnd bottom placement currently uses `_lockedResolution` as the 1920x1080 design-space source of truth; deriving the bottom Y from decorative-line parent `RectTransform` sizes has been observed to produce incorrect mid-screen placement and should be investigated before replacing the locked-resolution calculation.
- RoundResult layout tuning is centralized in `MainUIController` helper methods such as `GetRoundResultP1IconPosition`, `GetRoundResultP2IconPosition`, `GetRoundResultP1WordTopLeftPosition`, `GetRoundResultP2WordTopLeftPosition`, `GetRoundResultP1ScoreBarPosition`, `GetRoundResultP2ScoreBarPosition`, `GetRoundResultPanelPosition`, and `GetRoundResultPanelSize`.
- The older debug navigation key `Y` was removed from `MainUIController`; use the real text-command and ready flow unless adding a deliberate editor-only preview hook.
- Host debug reset: numpad `+` or Shift+`=` calls `GameManager.ResetGame()`.

UI uses Unity UI (Canvas + TMP) for the project's own screens. `Assets/Blocks/` is a **third-party Unity sample kit** (Multiplayer Widgets / Sessions building blocks - `CopySessionCode`, `LeaveSession`, `PlayerList`, etc.) and is not the project's own UI code; treat it as a vendored package.

## Key Dependencies

- **Netcode for GameObjects** 2.8.0 - multiplayer networking
- **FMOD Studio** - audio middleware (plugin in `Assets/Plugins/FMOD/`, not in manifest)
- **DOTween** (Demigiant) - tweening/animation (asset, not in manifest)
- **Odin Inspector** (Sirenix) - editor tooling (asset, not in manifest)
- **Unity Input System** 1.18.0
- **Unity Multiplayer Services** (`com.unity.services.multiplayer` 2.0.0) - session create/join by room word

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
- Shared TMP input ownership matters in the MainUI flow: use `UIManager.AnswerInputField` / `AddSubmitListenerToAnswerInputField` / `RemoveSubmitListenerFromAnswerInputField` instead of caching a specific legacy `GameScreenUI` input.
