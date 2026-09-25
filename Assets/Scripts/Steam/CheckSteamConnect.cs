#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

public enum EPlatformUserStatus
{
    Offline = 0,
    Online = 1,
    Busy = 2,
    Away = 3,
    Snooze = 4,
    LookingToTrade = 5,
    LookingToPlay = 6,
    Invisible = 7
}

public struct SPlatformUserInfo
{
    public string ID;
    public string Name;
    public EPlatformUserStatus Status;
    public Texture2D Avatar;
}

public class CheckSteamConnect : MonoBehaviour
{
    [SerializeField] TMP_Text statusLabel;
    [SerializeField] TMP_Text myNameLabel;
    [SerializeField] Image myAvatarImage;
    [SerializeField] TMP_Text noticeLabel;
    [SerializeField] RectTransform friendsContent;
    [SerializeField] SteamFriendItemView friendItemPrefab;

    public List<SPlatformUserInfo> Friends = new List<SPlatformUserInfo>();

#if !DISABLESTEAMWORKS
    Callback<PersonaStateChange_t> personaStateChange;
    Callback<GameLobbyJoinRequested_t> lobbyJoinRequested;
    string localSteamId;
#endif

    void Start()
    {
#if !DISABLESTEAMWORKS
        if (!Init())
            return;

        Subscribe();
        ShowLocalUser();
        InitFriends();
        RebuildFriendViews();
#else
        SetStatus("이 플랫폼에서는 Steam을 사용할 수 없습니다.");
#endif
    }

#if !DISABLESTEAMWORKS
    void OnDestroy()
    {
        personaStateChange?.Dispose();
        lobbyJoinRequested?.Dispose();
        SteamProfileManager.AvatarUpdated -= OnAvatarUpdated;
    }

    public bool Init()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError($"[Steam] 초기화 실패 - {SteamManager.LastInitError}");
            SetStatus($"Steam 초기화 실패\n{SteamManager.LastInitError}");
#if !UNITY_EDITOR
            Application.Quit();
#endif
            return false;
        }

        Debug.Log("[Steam] Initialized");
        SetStatus("Steam 연결됨");
        return true;
    }

    public bool InitFriends()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam API not initialized!");
            return false;
        }

        Friends.Clear();

        int friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
        for (int i = 0; i < friendCount; i++)
        {
            CSteamID friendSteamID = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
            SteamFriends.RequestUserInformation(friendSteamID, false);

            string name = SteamFriends.GetFriendPersonaName(friendSteamID);
            EPlatformUserStatus status = (EPlatformUserStatus)(int)SteamFriends.GetFriendPersonaState(friendSteamID);
            Texture2D avatar = GetFriendAvatar(friendSteamID);

            Friends.Add(new SPlatformUserInfo()
            {
                ID = friendSteamID.ToString(),
                Name = name,
                Status = status,
                Avatar = avatar
            });
        }

        return true;
    }

    public void Subscribe()
    {
        personaStateChange = Callback<PersonaStateChange_t>.Create(OnPersonaStateChange);
        lobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequested);
        SteamProfileManager.AvatarUpdated += OnAvatarUpdated;
    }

    public void ListenCallbacks()
    {
        SteamAPI.RunCallbacks();
    }

    void OnPersonaStateChange(PersonaStateChange_t data)
    {
        Debug.Log($"[Steam] 친구 상태 변경: {data.m_ulSteamID}");

        if (data.m_ulSteamID.ToString() == localSteamId)
            ShowLocalUser();

        if (!InitFriends())
            return;

        RebuildFriendViews();
    }

    void OnLobbyJoinRequested(GameLobbyJoinRequested_t data)
    {
        Debug.Log($"[Steam] 로비 초대 수신: {data.m_steamIDLobby}");
        SetNotice($"로비 초대: {data.m_steamIDLobby}");
    }

    void OnAvatarUpdated(ulong steamId)
    {
        string id = steamId.ToString();
        Texture2D avatar = GetFriendAvatar(new CSteamID(steamId));

        if (id == localSteamId)
            SetMyAvatar(avatar);

        for (int i = 0; i < Friends.Count; i++)
        {
            if (Friends[i].ID != id)
                continue;

            SPlatformUserInfo info = Friends[i];
            info.Avatar = avatar;
            Friends[i] = info;
            UpdateFriendView(info);
            return;
        }
    }

    void ShowLocalUser()
    {
        CSteamID steamId = SteamUser.GetSteamID();
        localSteamId = steamId.ToString();

        string name = SteamFriends.GetPersonaName();
        SetMyName(string.IsNullOrEmpty(name) ? "Steam 사용자" : name);
        SetMyAvatar(GetFriendAvatar(steamId));
    }

    Texture2D GetFriendAvatar(CSteamID steamId)
    {
        return SteamProfileManager.GetMediumAvatarTexture(steamId);
    }
#endif

    void RebuildFriendViews()
    {
        if (friendsContent == null || friendItemPrefab == null)
            return;

        for (int i = friendsContent.childCount - 1; i >= 0; i--)
            Destroy(friendsContent.GetChild(i).gameObject);

        for (int i = 0; i < Friends.Count; i++)
        {
            SteamFriendItemView view = Instantiate(friendItemPrefab, friendsContent);
            view.Apply(Friends[i]);
        }
    }

    void UpdateFriendView(SPlatformUserInfo info)
    {
        if (friendsContent == null)
            return;

        for (int i = 0; i < friendsContent.childCount; i++)
        {
            SteamFriendItemView view = friendsContent.GetChild(i).GetComponent<SteamFriendItemView>();
            if (view != null && view.SteamId == info.ID)
            {
                view.Apply(info);
                return;
            }
        }
    }

    void SetStatus(string message)
    {
        if (statusLabel != null)
            statusLabel.text = message;
    }

    void SetNotice(string message)
    {
        if (noticeLabel != null)
            noticeLabel.text = message;
    }

    void SetMyName(string name)
    {
        if (myNameLabel != null)
            myNameLabel.text = name;
    }

    void SetMyAvatar(Texture2D avatar)
    {
        if (myAvatarImage == null || avatar == null)
            return;

        myAvatarImage.sprite = Sprite.Create(
            avatar,
            new Rect(0f, 0f, avatar.width, avatar.height),
            new Vector2(0.5f, 0.5f));
    }
}
