using System;
using System.Collections;
using System.Collections.Generic;
using UI;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Utilities;

public class GameManager : NetworkSingleton<GameManager>
{
    public int MaxPlayerHp = 20;
    public int WinGameScore = 50;
    public int MaxGameScore = 70;
    public static int s_WinGameScore = 50;
    
    public NetworkVariable<bool> GameStartedState = new NetworkVariable<bool>();

    public NetworkVariable<bool> P1Ready = new NetworkVariable<bool>();
    public NetworkVariable<bool> P2Ready = new NetworkVariable<bool>();
    private SceneReloader m_sceneReloader;

    protected override void Awake()
    {
        base.Awake();
        
        m_sceneReloader = GetComponent<SceneReloader>();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void StartGameServerRpc()
    {
        GameStartedState.Value = true;
        Debug.Log("Game Started!");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void EndGameServerRpc()
    {
        GameStartedState.Value = false;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetClientReadyServerRpc(ulong clientId, bool ready)
    {
        if (!IsServer) return;

        var skipBothReady = GameplayTestManager.Instance != null && GameplayTestManager.Instance.SkipBothPlayerReady;
        if (skipBothReady && ready)
        {
            P1Ready.Value = true;
            P2Ready.Value = true;
        }
        else
        {
            if (clientId == 0) P1Ready.Value = ready;
            else if (clientId == 1) P2Ready.Value = ready;
            else Debug.LogWarning($"GameManager.SetClientReadyServerRpc: unexpected clientId={clientId}");
        }

        if (P1Ready.Value && P2Ready.Value)
        {
            // Start match on server + generate prompt BEFORE clients proceed.
            var rm = FindAnyObjectByType<RoundManager>();
            if (rm != null)
                rm.BeginMatchFromLobbyServer();
            else
                Debug.LogWarning("GameManager: RoundManager not found; cannot generate prompt.");

            GameStartedState.Value = true;
            BeginMatchAndEnterLoadingClientRpc();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void BeginMatchAndEnterLoadingClientRpc()
    {
        // UI side: enter a loading screen that waits for prompt.
        if (UIManager.Instance != null)
            UIManager.Instance.EnterMainUiLoadingHoldForPromptIfPresent();
    }

    public void NetworkReloadScene()
    {
        m_sceneReloader.ReloadCurrentScene();
        //NetworkReloadSceneClientRpc();
    }
    
    [Rpc(SendTo.ClientsAndHost)]
    public void NetworkReloadSceneClientRpc()
    {
        m_sceneReloader.ReloadCurrentScene();
        StartCoroutine(DelayReloadSceneRoutine());
    }

    private IEnumerator DelayReloadSceneRoutine()
    {
        yield return null;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
    // public override void OnNetworkSpawn()
    // {
    //     if (IsServer)
    //     {
    //         NetworkManager.SceneManager.OnLoadComplete += OnSceneLoaded;
    //     }
    // }
    //
    // private void OnSceneLoaded(ulong clientId, string sceneName, LoadSceneMode mode)
    // {
    //     if (clientId != NetworkManager.LocalClientId)
    //         return;
    //
    //     Debug.Log("Scene Loaded. Now start game.");
    //     StartGameServerRpc();
    // }
    //
    // public override void OnNetworkDespawn()
    // {
    //     if (IsServer && NetworkManager != null)
    //     {
    //         NetworkManager.SceneManager.OnLoadComplete -= OnSceneLoaded;
    //     }
    // }


    private void Update()
    {
        if (Keyboard.current == null) return;

        // Debug / test: full reset (host only). Numpad +, or Shift + = (US layout +).
        if (IsServer &&
            (Keyboard.current.numpadPlusKey.wasPressedThisFrame ||
             (Keyboard.current.equalsKey.wasPressedThisFrame &&
              (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed))))
        {
            ResetGame();
        }

        if (!IsServer) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame && false)
        {
            //StartCoroutine(RestartGame());
            ResetGame();
        }
    }

    public void ResetGame()
    {
        StartCoroutine(GameRestart());
        ResetClientRpc();
        Debug.Log("Game Reset!");
    }
    [Rpc(SendTo.ClientsAndHost)]
    private void ResetClientRpc()
    {
        var roundManager = FindAnyObjectByType<RoundManager>();
        if (roundManager)
        {
            roundManager.ResetRoundManager();
        }

        var clients = FindObjectsByType<Client>(FindObjectsSortMode.InstanceID);
        foreach (var client in clients)
        {
            client.ResetClient();
        }

        if (UIManager.Instance == null)
        {
            Debug.LogWarning("GameManager.ResetClientRpc: UIManager.Instance is null.");
            return;
        }

        UIManager.Instance.ResetUI();

        if (UIManager.Instance.UsesMainUiGameplayFlow)
        {
            // Same match entry as between rounds: Loading until server sends the next prompt, then PromptShowcase (do not call EnterGameScreen — that jumps straight to Gameplay).
            UIManager.Instance.ResetMainUiMatchFlowToLoadingAwaitingPromptIfPresent();
        }
        else
        {
            UIManager.Instance.EnterWinScreen();
            UIManager.Instance.EnterGameScreen();
        }
    }

    private IEnumerator GameRestart()
    {
        GameStartedState.Value = false;
        P1Ready.Value = false;
        P2Ready.Value = false;
        yield return null;
        GameStartedState.Value = true;
    }

    // public IEnumerator RestartGame()
    // {
    //     NetworkManager.Singleton.Shutdown();
    //     yield return null;
    //     RestartNetworkClientRpc();
    // }
    // [Rpc(SendTo.ClientsAndHost)]
    // private void RestartNetworkClientRpc()
    // {
    //     SceneManager.Instance.LoadTitleScene();
    // }
}