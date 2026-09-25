using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSlotUI : MonoBehaviour
{
    [SerializeField] Image profileImage;
    [SerializeField] TMP_Text playerName;
    [SerializeField] GameObject youBadge;
    [SerializeField] Sprite defaultProfileSprite;

    public ulong SteamId { get; private set; }

    public void Show(Player player)
    {
        SteamId = ReadSteamId(player);

        if (playerName != null)
            playerName.text = string.IsNullOrWhiteSpace(player.NickName) ? $"Player {player.ActorNumber}" : player.NickName;

        if (youBadge != null)
            youBadge.SetActive(player.IsLocal);

        RefreshAvatar();
    }

    public void RefreshAvatar()
    {
        if (profileImage == null)
            return;

        Sprite avatar = SteamProfileManager.GetAvatarSprite(SteamId);
        profileImage.sprite = avatar != null ? avatar : defaultProfileSprite;
    }

    static ulong ReadSteamId(Player player)
    {
        if (player.CustomProperties.TryGetValue(PhotonRoomManager.SteamIdPropertyKey, out object value) &&
            value is string text &&
            ulong.TryParse(text, out ulong steamId))
            return steamId;

        return 0;
    }
}
