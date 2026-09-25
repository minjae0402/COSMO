using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SteamInvitePanel : MonoBehaviour
{
    [SerializeField] Button inviteButton;
    [SerializeField] TMP_Text playerCountText;
    [SerializeField] TMP_Text statusText;

    void OnEnable()
    {
        PhotonRoomManager.RoomChanged += Refresh;
        PhotonRoomManager.StatusChanged += ShowStatus;
        SteamInviteManager.StatusChanged += ShowStatus;
        Refresh();
    }

    void OnDisable()
    {
        PhotonRoomManager.RoomChanged -= Refresh;
        PhotonRoomManager.StatusChanged -= ShowStatus;
        SteamInviteManager.StatusChanged -= ShowStatus;
    }

    public void OnClickInvite()
    {
        if (SteamInviteManager.Instance == null)
        {
            ShowStatus("Steam 초대 관리자가 씬에 없습니다.");
            return;
        }

        SteamInviteManager.Instance.OpenInviteDialog();
    }

    void Refresh()
    {
        int count = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.PlayerCount : 0;

        if (playerCountText != null)
            playerCountText.text = $"현재 {count} / {PhotonRoomManager.MaxPlayers}";

        if (inviteButton != null)
            inviteButton.interactable = PhotonNetwork.InRoom && count < PhotonRoomManager.MaxPlayers;
    }

    void ShowStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
