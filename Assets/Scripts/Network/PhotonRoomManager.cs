using System;
using System.Text.RegularExpressions;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PhotonRoomManager : MonoBehaviourPunCallbacks
{
    public const int MaxPlayers = 6;
    public const string SteamIdPropertyKey = "steamId";

    static readonly Regex RoomNamePattern = new Regex("^[A-Za-z0-9_-]{1,64}$");

    public static PhotonRoomManager Instance { get; private set; }

    public static event Action RoomChanged;
    public static event Action<string> StatusChanged;

    public static string LastStatus { get; private set; } = string.Empty;

    [SerializeField] bool connectOnStart = true;

    string pendingJoinRoomName;
    bool pendingCreate;

    public bool InRoom => PhotonNetwork.InRoom;
    public string CurrentRoomName => PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.Name : string.Empty;
    public int CurrentPlayerCount => PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.PlayerCount : 0;

    public bool IsReadyForRoomOperation =>
        PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer ||
        PhotonNetwork.NetworkClientState == ClientState.JoinedLobby;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (connectOnStart)
            Connect();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static bool IsValidRoomName(string roomName)
    {
        return !string.IsNullOrEmpty(roomName) && RoomNamePattern.IsMatch(roomName);
    }

    public void Connect()
    {
        if (PhotonNetwork.IsConnected)
            return;

        ApplyLocalPlayerInfo();
        SetStatus("Photon 서버에 연결 중...");

        if (!PhotonNetwork.ConnectUsingSettings())
            SetStatus("Photon 연결을 시작하지 못했습니다.");
    }

    public void CreateRoom()
    {
        if (PhotonNetwork.InRoom)
        {
            SetStatus("이미 방에 들어와 있습니다.");
            return;
        }

        pendingJoinRoomName = null;

        if (!IsReadyForRoomOperation)
        {
            pendingCreate = true;
            Connect();
            return;
        }

        DoCreateRoom();
    }

    public void JoinInvitedRoom(string roomName)
    {
        if (!IsValidRoomName(roomName))
        {
            SetStatus("잘못된 방 정보입니다.");
            return;
        }

        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.Name == roomName)
        {
            SetStatus("이미 초대받은 방에 있습니다.");
            return;
        }

        pendingCreate = false;
        pendingJoinRoomName = roomName;

        if (PhotonNetwork.InRoom)
        {
            SetStatus("현재 방에서 나간 뒤 초대받은 방으로 이동합니다...");
            PhotonNetwork.LeaveRoom(false);
            return;
        }

        if (IsReadyForRoomOperation)
        {
            DoJoinPendingRoom();
            return;
        }

        if (!PhotonNetwork.IsConnected)
            Connect();
        else
            SetStatus("Photon 연결을 기다리는 중...");
    }

    public void LeaveRoom()
    {
        pendingJoinRoomName = null;
        pendingCreate = false;

        if (!PhotonNetwork.InRoom)
            return;

        SetStatus("방에서 나가는 중...");
        PhotonNetwork.LeaveRoom(false);
    }

    void DoCreateRoom()
    {
        pendingCreate = false;
        ApplyLocalPlayerInfo();

        string roomName = "COSMO-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
        RoomOptions options = new RoomOptions
        {
            MaxPlayers = MaxPlayers,
            IsOpen = true,
            IsVisible = false,
            PlayerTtl = 0,
            EmptyRoomTtl = 0
        };

        SetStatus("방을 만드는 중...");
        if (!PhotonNetwork.CreateRoom(roomName, options))
            SetStatus("방 생성 요청을 보내지 못했습니다.");
    }

    void DoJoinPendingRoom()
    {
        string roomName = pendingJoinRoomName;
        pendingJoinRoomName = null;
        ApplyLocalPlayerInfo();

        SetStatus("초대받은 방에 입장하는 중...");
        if (!PhotonNetwork.JoinRoom(roomName))
            SetStatus("방 입장 요청을 보내지 못했습니다.");
    }

    void ApplyLocalPlayerInfo()
    {
        if (string.IsNullOrEmpty(PhotonNetwork.NickName))
        {
            string steamName = SteamProfileManager.GetLocalPersonaName();
            PhotonNetwork.NickName = string.IsNullOrEmpty(steamName)
                ? "Player " + UnityEngine.Random.Range(1000, 10000)
                : steamName;
        }

        ulong steamId = SteamProfileManager.GetLocalSteamId();
        if (steamId == 0)
            return;

        Hashtable properties = new Hashtable { { SteamIdPropertyKey, steamId.ToString() } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(properties);
    }

    void SetStatus(string message)
    {
        LastStatus = message;
        Debug.Log($"[PhotonRoom] {message}");
        StatusChanged?.Invoke(message);
    }

    static void NotifyRoomChanged()
    {
        RoomChanged?.Invoke();
    }

    public override void OnConnectedToMaster()
    {
        if (!string.IsNullOrEmpty(pendingJoinRoomName))
            DoJoinPendingRoom();
        else if (pendingCreate)
            DoCreateRoom();
        else
            SetStatus("Photon 연결됨");

        NotifyRoomChanged();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        pendingCreate = false;
        pendingJoinRoomName = null;
        SetStatus($"Photon 연결이 끊어졌습니다. ({cause})");
        NotifyRoomChanged();
    }

    public override void OnCreatedRoom()
    {
        SetStatus($"방 생성 완료: {CurrentRoomName}");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        SetStatus($"방 생성 실패: {message}");
        NotifyRoomChanged();
    }

    public override void OnJoinedRoom()
    {
        SetStatus($"방 입장: {CurrentRoomName} ({CurrentPlayerCount} / {MaxPlayers})");
        NotifyRoomChanged();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        switch (returnCode)
        {
            case ErrorCode.GameFull:
                SetStatus("방이 가득 찼습니다. (6 / 6)");
                break;
            case ErrorCode.GameClosed:
                SetStatus("방이 닫혀 있어 입장할 수 없습니다.");
                break;
            case ErrorCode.GameDoesNotExist:
                SetStatus("방이 존재하지 않거나 이미 종료되었습니다.");
                break;
            default:
                SetStatus($"방 입장 실패: {message}");
                break;
        }

        NotifyRoomChanged();
    }

    public override void OnLeftRoom()
    {
        if (string.IsNullOrEmpty(pendingJoinRoomName))
            SetStatus("방에서 나왔습니다.");

        NotifyRoomChanged();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        NotifyRoomChanged();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        NotifyRoomChanged();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey(SteamIdPropertyKey))
            NotifyRoomChanged();
    }
}
