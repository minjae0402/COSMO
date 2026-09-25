using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomControlUI : MonoBehaviour
{
    [SerializeField] Button createRoomButton;
    [SerializeField] Button leaveRoomButton;
    [SerializeField] TMP_Text roomInfoText;

    void OnEnable()
    {
        PhotonRoomManager.RoomChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        PhotonRoomManager.RoomChanged -= Refresh;
    }

    public void OnClickCreateRoom()
    {
        if (PhotonRoomManager.Instance != null)
            PhotonRoomManager.Instance.CreateRoom();
    }

    public void OnClickLeaveRoom()
    {
        if (PhotonRoomManager.Instance != null)
            PhotonRoomManager.Instance.LeaveRoom();
    }

    void Refresh()
    {
        bool inRoom = PhotonNetwork.InRoom;

        if (createRoomButton != null)
            createRoomButton.interactable = !inRoom;

        if (leaveRoomButton != null)
            leaveRoomButton.interactable = inRoom;

        if (roomInfoText != null)
            roomInfoText.text = inRoom ? $"ROOM  {PhotonNetwork.CurrentRoom.Name}" : "ROOM  -";
    }
}
