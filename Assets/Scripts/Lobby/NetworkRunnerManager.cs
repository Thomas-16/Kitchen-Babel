using System;
using System.Collections.Generic;
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

    public NetworkRunner Runner { get; private set; }
    public List<SessionInfo> CachedSessionList { get; private set; } = new List<SessionInfo>();
    public bool IsHost => Runner != null && Runner.IsServer;

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
        if (Runner != null)
        {
            await Runner.Shutdown();
            Runner = null;
        }

        Runner = gameObject.AddComponent<NetworkRunner>();
        Runner.AddCallbacks(this);
        Runner.ProvideInput = false;

        var result = await Runner.JoinSessionLobby(SessionLobby.ClientServer);
        if (!result.Ok)
        {
            Debug.LogError($"Failed to join lobby: {result.ShutdownReason}");
        }
    }

    public async void CreateRoom(string roomName, string password, int maxPlayers)
    {
        if (Runner != null)
        {
            Runner.RemoveCallbacks(this);
            await Runner.Shutdown();
            Destroy(Runner);
            Runner = null;
        }

        Runner = gameObject.AddComponent<NetworkRunner>();
        Runner.AddCallbacks(this);
        Runner.ProvideInput = false;

        var sessionProps = new Dictionary<string, SessionProperty>();
        if (!string.IsNullOrEmpty(password))
        {
            sessionProps["pw"] = password.GetHashCode();
        }
        else
        {
            sessionProps["pw"] = 0;
        }

        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/MainMenuScene.unity")));

        var result = await Runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Host,
            SessionName = roomName,
            PlayerCount = maxPlayers,
            SessionProperties = sessionProps,
            Scene = sceneInfo,
        });

        if (result.Ok)
        {
            Runner.Spawn(lobbyManagerPrefab, Vector3.zero, Quaternion.identity, Runner.LocalPlayer);
            OnJoinedSession?.Invoke();
        }
        else
        {
            Debug.LogError($"Failed to create room: {result.ShutdownReason}");
            OnJoinFailed?.Invoke("Failed to create room.");
            JoinLobby();
        }
    }

    public async void JoinRoom(string sessionName, string password)
    {
        if (Runner != null)
        {
            Runner.RemoveCallbacks(this);
            await Runner.Shutdown();
            Destroy(Runner);
            Runner = null;
        }

        Runner = gameObject.AddComponent<NetworkRunner>();
        Runner.AddCallbacks(this);
        Runner.ProvideInput = false;

        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/MainMenuScene.unity")));

        var result = await Runner.StartGame(new StartGameArgs
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
            JoinLobby();
        }
    }

    public async void LeaveSession()
    {
        if (Runner != null)
        {
            Runner.RemoveCallbacks(this);
            await Runner.Shutdown();
            Destroy(Runner);
            Runner = null;
        }

        SceneManager.LoadScene("MainMenuScene");
    }

    public void StartGame()
    {
        if (Runner == null || !Runner.IsServer) return;

        var sceneRef = SceneRef.FromIndex(SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/GameScene.unity"));
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
        if (session.Properties.TryGetValue("pw", out var pwProp))
        {
            int storedHash = (int)pwProp;
            if (storedHash == 0)
                return true;

            int inputHash = string.IsNullOrEmpty(password) ? 0 : password.GetHashCode();
            return storedHash == inputHash;
        }
        return true;
    }

    public bool SessionHasPassword(SessionInfo session)
    {
        if (session.Properties.TryGetValue("pw", out var pwProp))
        {
            return (int)pwProp != 0;
        }
        return false;
    }

    // INetworkRunnerCallbacks
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
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
        if (runner.IsServer && LobbyManager.Instance != null)
        {
            LobbyManager.Instance.RemovePlayer(player);
        }
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (runner.IsServer && KitchenGameManager.Instance != null)
        {
            KitchenGameManager.Instance.TryStartGame();
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (shutdownReason != ShutdownReason.Ok)
        {
            OnDisconnected?.Invoke();
        }
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
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
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
}
