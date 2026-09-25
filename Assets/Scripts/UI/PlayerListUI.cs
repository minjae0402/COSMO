using System;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class PlayerListUI : MonoBehaviour
{
    [SerializeField] TMP_Text playerCountText;
    [SerializeField] PlayerSlotUI[] slots = new PlayerSlotUI[PhotonRoomManager.MaxPlayers];

    void OnEnable()
    {
        PhotonRoomManager.RoomChanged += Refresh;
        SteamProfileManager.AvatarUpdated += OnAvatarUpdated;
        Refresh();
    }

    void OnDisable()
    {
        PhotonRoomManager.RoomChanged -= Refresh;
        SteamProfileManager.AvatarUpdated -= OnAvatarUpdated;
    }

    public void Refresh()
    {
        Player[] players = PhotonNetwork.InRoom ? PhotonNetwork.PlayerList : Array.Empty<Player>();
        Array.Sort(players, (a, b) => a.ActorNumber.CompareTo(b.ActorNumber));

        if (playerCountText != null)
            playerCountText.text = $"{players.Length} / {PhotonRoomManager.MaxPlayers}";

        for (int i = 0; i < slots.Length; i++)
        {
            PlayerSlotUI slot = slots[i];
            if (slot == null)
                continue;

            bool used = i < players.Length;
            slot.gameObject.SetActive(used);
            if (used)
                slot.Show(players[i]);
        }
    }

    void OnAvatarUpdated(ulong steamId)
    {
        foreach (PlayerSlotUI slot in slots)
        {
            if (slot != null && slot.gameObject.activeSelf && slot.SteamId == steamId)
                slot.RefreshAvatar();
        }
    }
}
