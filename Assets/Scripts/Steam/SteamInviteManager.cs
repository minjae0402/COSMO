#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using Photon.Pun;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

public class SteamInviteManager : MonoBehaviour
{
    public const string ConnectArgument = "+cosmo_room";
    const string RichPresenceConnectKey = "connect";

    public static SteamInviteManager Instance { get; private set; }

    public static event Action<string> StatusChanged;

    public static string LastStatus { get; private set; } = string.Empty;

#if !DISABLESTEAMWORKS
    Callback<GameRichPresenceJoinRequested_t> joinRequested;
#endif

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

    void OnEnable()
    {
        PhotonRoomManager.RoomChanged += UpdateRichPresence;
    }

    void OnDisable()
    {
        PhotonRoomManager.RoomChanged -= UpdateRichPresence;
    }

    void Start()
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized)
        {
            SetStatus("Steam이 실행 중이 아니거나 초기화에 실패했습니다.");
            return;
        }

        joinRequested = Callback<GameRichPresenceJoinRequested_t>.Create(OnGameRichPresenceJoinRequested);
        CheckLaunchInvite();
#else
        SetStatus("이 플랫폼에서는 Steam 초대를 사용할 수 없습니다.");
#endif
    }

    void OnDestroy()
    {
#if !DISABLESTEAMWORKS
        joinRequested?.Dispose();
#endif
        if (Instance == this)
            Instance = null;
    }

    public static string BuildConnectString(string roomName)
    {
        return $"{ConnectArgument} {roomName}";
    }

    public static bool TryParseRoomName(string text, out string roomName)
    {
        roomName = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string[] tokens = text.Split(new[] { ' ', '\t', '\r', '\n', '"' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length - 1; i++)
        {
            if (tokens[i] != ConnectArgument)
                continue;

            if (!PhotonRoomManager.IsValidRoomName(tokens[i + 1]))
                return false;

            roomName = tokens[i + 1];
            return true;
        }

        return false;
    }

    public void OpenInviteDialog()
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized)
        {
            SetStatus("Steam이 실행 중이 아니거나 초기화에 실패했습니다.");
            return;
        }

        if (!PhotonNetwork.InRoom)
        {
            SetStatus("방에 들어간 뒤에 친구를 초대할 수 있습니다.");
            return;
        }

        if (PhotonNetwork.CurrentRoom.PlayerCount >= PhotonRoomManager.MaxPlayers)
        {
            SetStatus($"방이 가득 찼습니다. ({PhotonRoomManager.MaxPlayers} / {PhotonRoomManager.MaxPlayers})");
            return;
        }

        if (SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate) == 0)
        {
            SetStatus("초대할 Steam 친구가 없습니다.");
            return;
        }

        if (!SteamUtils.IsOverlayEnabled())
        {
            SetStatus("Steam 오버레이를 사용할 수 없습니다. Steam에서 게임을 실행하고 오버레이를 켜 주세요.");
            return;
        }

        UpdateRichPresence();
        SteamFriends.ActivateGameOverlayInviteDialogConnectString(BuildConnectString(PhotonNetwork.CurrentRoom.Name));
        SetStatus("Steam 초대 창을 열었습니다.");
#else
        SetStatus("이 플랫폼에서는 Steam 초대를 사용할 수 없습니다.");
#endif
    }

    void HandleInviteText(string text, string source)
    {
        if (!TryParseRoomName(text, out string roomName))
        {
            Debug.LogWarning($"[SteamInvite] 잘못된 초대 데이터 ({source}): {text}");
            SetStatus("잘못된 Steam 초대 정보입니다.");
            return;
        }

        SetStatus("Steam 초대를 수락했습니다. 방에 입장합니다...");

        if (PhotonRoomManager.Instance == null)
        {
            SetStatus("Photon 방 관리자가 없어 초대받은 방에 들어갈 수 없습니다.");
            return;
        }

        PhotonRoomManager.Instance.JoinInvitedRoom(roomName);
    }

    void UpdateRichPresence()
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized)
            return;

        string value = PhotonNetwork.InRoom ? BuildConnectString(PhotonNetwork.CurrentRoom.Name) : string.Empty;
        SteamFriends.SetRichPresence(RichPresenceConnectKey, value);
#endif
    }

#if !DISABLESTEAMWORKS
    void CheckLaunchInvite()
    {
        SteamApps.GetLaunchCommandLine(out string steamCommandLine, 1024);
        if (TryParseRoomName(steamCommandLine, out _))
        {
            HandleInviteText(steamCommandLine, "launch command line");
            return;
        }

        string processCommandLine = string.Join(" ", Environment.GetCommandLineArgs());
        if (processCommandLine.Contains(ConnectArgument))
            HandleInviteText(processCommandLine, "process arguments");
    }

    void OnGameRichPresenceJoinRequested(GameRichPresenceJoinRequested_t callback)
    {
        HandleInviteText(callback.m_rgchConnect, "join request");
    }
#endif

    void SetStatus(string message)
    {
        LastStatus = message;
        Debug.Log($"[SteamInvite] {message}");
        StatusChanged?.Invoke(message);
    }
}
