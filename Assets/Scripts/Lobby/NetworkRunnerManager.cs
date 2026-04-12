using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkRunnerManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkRunnerManager Instance { get; private set; }

    [SerializeField] private NetworkPrefabRef lobbyManagerPrefab;

    public event Action<List<SessionInfo>> SessionListUpdated;
    public event Action OnJoinedSession;
    public event Action<string> OnJoinFailed;
    public event Action OnDisconnected;
    public event Action OnReturnedToLobby;

    public NetworkRunner Runner { get; private set; }
    public List<SessionInfo> CachedSessionList { get; private set; } = new List<SessionInfo>();
    public bool IsHost => Runner != null && Runner.IsServer;
    public bool IsBusy { get; private set; }

    private bool pendingLobbyManagerSpawn;
    private bool isMigrating;
    private bool isReturningToLobby;

    private async Task ShutdownRunner()
    {
        if (Runner != null)
        {
            Runner.RemoveCallbacks(this);
            await Runner.Shutdown(destroyGameObject: true);
            Runner = null;
            await Task.Yield();
        }
    }

    private NetworkRunner CreateRunner()
    {
        GameObject go = new GameObject("NetworkRunner");
        go.transform.SetParent(transform);
        NetworkRunner runner = go.AddComponent<NetworkRunner>();
        runner.AddCallbacks(this);
        runner.ProvideInput = false;
        return runner;
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public async void JoinLobby()
    {
        if (IsBusy)
        {
            Debug.LogWarning("[NetworkRunnerManager] JoinLobby skipped — busy.");
            return;
        }

        IsBusy = true;
        try
        {
            await ShutdownRunner();

            Runner = CreateRunner();

            Debug.Log("[NetworkRunnerManager] Joining session lobby...");
            StartGameResult result = await Runner.JoinSessionLobby(SessionLobby.ClientServer);
            if (result.Ok)
            {
                Debug.Log("[NetworkRunnerManager] Successfully joined lobby.");
            }
            else
            {
                Debug.LogError($"[NetworkRunnerManager] Failed to join lobby: {result.ShutdownReason}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkRunnerManager] Exception in JoinLobby: {e}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async void CreateRoom(string roomName, string password, int maxPlayers)
    {
        if (IsBusy) return;
        IsBusy = true;
        bool shouldRejoinLobby = false;
        try
        {
            await ShutdownRunner();

            Runner = CreateRunner();
            Runner.ProvideInput = true;

            Dictionary<string, SessionProperty> sessionProps = new Dictionary<string, SessionProperty>();
            if (!string.IsNullOrEmpty(password))
            {
                sessionProps["pw"] = password.ToUpperInvariant().GetHashCode();
            }
            else
            {
                sessionProps["pw"] = 0;
            }
            sessionProps["gs"] = 0;

            NetworkSceneInfo sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/MainMenuScene.unity")));

            StartGameResult result = await Runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Host,
                SessionName = roomName,
                PlayerCount = maxPlayers,
                SessionProperties = sessionProps,
                Scene = sceneInfo,
            });

            if (result.Ok)
            {
                pendingLobbyManagerSpawn = true;
                OnJoinedSession?.Invoke();
                // Push an initial snapshot immediately so host migration works even before
                // Fusion's auto-update timer fires for the first time.
                PushSnapshot();
            }
            else
            {
                Debug.LogError($"Failed to create room: {result.ShutdownReason}");
                OnJoinFailed?.Invoke("Failed to create room.");
                shouldRejoinLobby = true;
            }
        }
        finally
        {
            IsBusy = false;
        }
        if (shouldRejoinLobby) JoinLobby();
    }

    public async void JoinRoom(string sessionName, string password)
    {
        if (IsBusy) return;
        IsBusy = true;
        bool shouldRejoinLobby = false;
        try
        {
            await ShutdownRunner();

            Runner = CreateRunner();
            Runner.ProvideInput = true;

            NetworkSceneInfo sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/MainMenuScene.unity")));

            StartGameResult result = await Runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = sessionName,
                Scene = sceneInfo,
            });

            if (result.Ok)
            {
                OnJoinedSession?.Invoke();
            }
            else
            {
                Debug.LogError($"Failed to join room: {result.ShutdownReason}");
                OnJoinFailed?.Invoke("Failed to join room.");
                shouldRejoinLobby = true;
            }
        }
        finally
        {
            IsBusy = false;
        }
        if (shouldRejoinLobby) JoinLobby();
    }

    public async void LeaveSession()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await ShutdownRunner();
            OnDisconnected?.Invoke();
        }
        finally
        {
            IsBusy = false;
        }
        JoinLobby();
    }

    public void StartGame()
    {
        if (Runner == null || !Runner.IsServer) return;

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.SnapshotPlayerColors();
        }

        SetGameInSession(true);

        SceneRef sceneRef = SceneRef.FromIndex(SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/GameScene.unity"));
        Runner.LoadScene(sceneRef);
    }

    public async void PushSnapshot()
    {
        if (Runner == null || !Runner.IsRunning || !Runner.IsServer) return;
        try
        {
            bool ok = await Runner.PushHostMigrationSnapshot();
            if (!ok)
            {
                Debug.LogWarning("[NetworkRunnerManager] PushHostMigrationSnapshot returned false.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkRunnerManager] PushHostMigrationSnapshot threw: {e}");
        }
    }

    public void ReturnToLobby()
    {
        if (Runner == null || !Runner.IsServer || isReturningToLobby) return;
        isReturningToLobby = true;

        SceneRef sceneRef = SceneRef.FromIndex(SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/MainMenuScene.unity"));
        Runner.LoadScene(sceneRef);
    }

    public void KickPlayer(PlayerRef player)
    {
        if (Runner != null && Runner.IsServer)
        {
            Runner.Disconnect(player);
        }
    }

    public bool ValidatePassword(SessionInfo session, string password)
    {
        if (session.Properties.TryGetValue("pw", out SessionProperty pwProp))
        {
            int storedHash = (int)pwProp;
            if (storedHash == 0)
                return true;

            int inputHash = string.IsNullOrEmpty(password) ? 0 : password.ToUpperInvariant().GetHashCode();
            return storedHash == inputHash;
        }
        return true;
    }

    public bool SessionHasPassword(SessionInfo session)
    {
        if (session.Properties.TryGetValue("pw", out SessionProperty pwProp))
        {
            return (int)pwProp != 0;
        }
        return false;
    }

    public bool SessionIsInGame(SessionInfo session)
    {
        if (session.Properties.TryGetValue("gs", out SessionProperty gsProp))
        {
            return (int)gsProp != 0;
        }
        return false;
    }

    private void SetGameInSession(bool inSession)
    {
        if (Runner != null && Runner.IsServer)
        {
            Runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty>
            {
                { "gs", inSession ? 1 : 0 }
            });
        }
    }

    // INetworkRunnerCallbacks
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log($"[NetworkRunnerManager] OnSessionListUpdated: {sessionList.Count} sessions");
        CachedSessionList = sessionList;
        SessionListUpdated?.Invoke(sessionList);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer && LobbyManager.Instance != null)
        {
            LobbyManager.Instance.AddPlayer(player);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[NRM] OnPlayerLeft: {player}, IsServer={runner.IsServer}");
        if (runner.IsServer && LobbyManager.Instance != null)
        {
            LobbyManager.Instance.RemovePlayer(player);
        }
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (runner.IsServer)
        {
            if (pendingLobbyManagerSpawn)
            {
                pendingLobbyManagerSpawn = false;
                runner.Spawn(lobbyManagerPrefab, Vector3.zero, Quaternion.identity, runner.LocalPlayer);
            }

            if (KitchenGameManager.Instance != null)
            {
                // TryStartGame is now called by MapManager after the map is loaded and counters are spawned
            }
            else if (LobbyManager.Instance == null)
            {
                // Returned to MainMenuScene from a game — LobbyManager was despawned during scene load
                // Re-spawn it; Spawned() will re-add the host and active players will be re-added via OnPlayerJoined
                runner.Spawn(lobbyManagerPrefab, Vector3.zero, Quaternion.identity, runner.LocalPlayer);
            }
            else
            {
                LobbyManager.Instance.ResetReadyStates();
            }
        }

        // Fire on all clients when returning to lobby (no KitchenGameManager means we're in MainMenuScene)
        if (KitchenGameManager.Instance == null && LobbyManager.Instance != null)
        {
            if (runner.IsServer)
            {
                SetGameInSession(false);
                // Capture post-return snapshot so a host disconnect now doesn't
                // resurrect stale game objects into the lobby scene.
                PushSnapshot();
            }
            isReturningToLobby = false;
            OnReturnedToLobby?.Invoke();
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[NRM] OnShutdown: reason={shutdownReason}, isMigrating={isMigrating}");
        if (shutdownReason == ShutdownReason.HostMigration) return;

        if (isMigrating) return;

        if (shutdownReason != ShutdownReason.Ok)
        {
            OnDisconnected?.Invoke();
        }
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"[NRM] OnDisconnectedFromServer: reason={reason}, isMigrating={isMigrating}");
        if (isMigrating)
        {
            Debug.Log("[NetworkRunnerManager] OnDisconnectedFromServer during migration — ignoring.");
            return;
        }

        OnDisconnected?.Invoke();
        SceneManager.LoadScene("MainMenuScene");
    }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public async void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        Debug.Log("[NetworkRunnerManager] OnHostMigration triggered. Starting migration...");
        isMigrating = true;

        try
        {
            // Signal KitchenGameMultiplayer to skip player spawning after migration
            KitchenGameMultiplayer.SetHostMigrationFlag();

            // Shutdown old runner with HostMigration reason so OnShutdown knows to ignore it
            await runner.Shutdown(shutdownReason: ShutdownReason.HostMigration);
            Runner = null;

            // Create new runner
            Runner = CreateRunner();
            Runner.ProvideInput = true;

            // Start as new host with migration token + resume callback
            StartGameResult result = await Runner.StartGame(new StartGameArgs
            {
                HostMigrationToken = hostMigrationToken,
                HostMigrationResume = HostMigrationResume,
            });

            if (!result.Ok)
            {
                Debug.LogError($"[NetworkRunnerManager] Host migration failed: {result.ShutdownReason}");
                OnDisconnected?.Invoke();
                SceneManager.LoadScene("MainMenuScene");
            }
            else
            {
                Debug.Log("[NetworkRunnerManager] Host migration succeeded. New host is running.");

                // If the host disconnected during gameplay, route everyone back to the lobby UI
                // instead of trying to preserve match state.
                if (SceneManager.GetActiveScene().name != "MainMenuScene")
                {
                    Debug.Log("[NetworkRunnerManager] Migrated mid-game — returning everyone to lobby.");
                    pendingLobbyManagerSpawn = true;
                    SetGameInSession(false);
                    SceneRef lobbyRef = SceneRef.FromIndex(SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/MainMenuScene.unity"));
                    Runner.LoadScene(lobbyRef);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkRunnerManager] Exception during host migration: {e}");
            OnDisconnected?.Invoke();
            SceneManager.LoadScene("MainMenuScene");
        }
        finally
        {
            isMigrating = false;
        }
    }

    private void HostMigrationResume(NetworkRunner runner)
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        bool isLobbyScene = activeSceneName == "MainMenuScene";

        if (!isLobbyScene)
        {
            // Mid-game migration: discard all snapshot state. OnHostMigration will load
            // MainMenuScene and spawn a fresh LobbyManager; all players re-join cleanly.
            Debug.Log("[HostMigration] Mid-game migration — discarding game state, will load lobby.");
            return;
        }

        // Lobby migration: preserve the LobbyManager (Players list, ready/colour/name).
        foreach (NetworkObject resumeNO in runner.GetResumeSnapshotNetworkObjects())
        {
            if (resumeNO.GetComponent<LobbyManager>() == null) continue;
            runner.Spawn(resumeNO,
                position: resumeNO.transform.position,
                rotation: resumeNO.transform.rotation,
                inputAuthority: resumeNO.InputAuthority,
                onBeforeSpawned: (NetworkRunner r, NetworkObject newNO) =>
                {
                    newNO.CopyStateFrom(resumeNO);
                }
            );
            Debug.Log("[HostMigration] Restored LobbyManager from snapshot.");
        }
    }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
}
