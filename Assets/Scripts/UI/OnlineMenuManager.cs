using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OnlineMenuManager : MonoBehaviour
{
    [Header("Room Match")]
    public TMP_InputField roomNameInput;
    public Button joinRoomButton;
    public Button createRoomButton; // [NEW] 部屋作成ボタン

    [Header("Random Match")]
    public Button randomMatchButton;

    [Header("Status")]
    public TextMeshProUGUI statusText;

    void Start()
    {
        if (joinRoomButton) joinRoomButton.onClick.AddListener(OnClickJoinRoom);
        if (createRoomButton) createRoomButton.onClick.AddListener(OnClickCreateRoom);
        if (randomMatchButton) randomMatchButton.onClick.AddListener(OnClickRandomMatch);
    }

    void OnClickCreateRoom()
    {
        if (statusText) statusText.text = "Creating Private Room...";
        NetworkConnectionManager.instance.CreatePrivateRoom();
    }

    void OnClickJoinRoom()
    {
        string roomName = roomNameInput.text;
        if (string.IsNullOrEmpty(roomName))
        {
            if (statusText) statusText.text = "Room Name is empty";
            return;
        }

        if (statusText) statusText.text = $"Connecting to {roomName}...";
        NetworkConnectionManager.instance.StartRoomMatch(roomName);
    }

    void OnClickRandomMatch()
    {
        if (statusText) statusText.text = "Searching for random match...";
        NetworkConnectionManager.instance.StartRandomMatch();
    }
}
