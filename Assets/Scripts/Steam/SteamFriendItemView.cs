using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SteamFriendItemView : MonoBehaviour
{
    [SerializeField] Image avatarImage;
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] TMP_Text statusLabel;

    public string SteamId { get; private set; }

    public void Apply(SPlatformUserInfo info)
    {
        SteamId = info.ID;

        if (nameLabel != null)
            nameLabel.text = string.IsNullOrEmpty(info.Name) ? "알 수 없음" : info.Name;

        if (statusLabel != null)
            statusLabel.text = StatusLabel(info.Status);

        if (avatarImage == null || info.Avatar == null)
            return;

        avatarImage.sprite = Sprite.Create(
            info.Avatar,
            new Rect(0f, 0f, info.Avatar.width, info.Avatar.height),
            new Vector2(0.5f, 0.5f));
    }

    static string StatusLabel(EPlatformUserStatus status)
    {
        switch (status)
        {
            case EPlatformUserStatus.Online:
                return "온라인";
            case EPlatformUserStatus.Busy:
                return "다른 용무 중";
            case EPlatformUserStatus.Away:
                return "자리 비움";
            case EPlatformUserStatus.Snooze:
                return "수면";
            case EPlatformUserStatus.LookingToTrade:
                return "거래 중";
            case EPlatformUserStatus.LookingToPlay:
                return "게임 찾는 중";
            case EPlatformUserStatus.Invisible:
                return "오프라인으로 표시";
            default:
                return "오프라인";
        }
    }
}
