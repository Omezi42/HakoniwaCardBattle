using UnityEngine;
using TMPro;
using Fusion;
using UnityEngine.UI;

public class RoomInfoUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI roomIDText;
    [SerializeField] private Button copyButton;

    private bool _hasUpdatedRoomInfo = false;

    void Start()
    {
        if (copyButton)
        {
            copyButton.onClick.AddListener(CopyRoomID);
        }
        UpdateRoomInfo();
    }

    void Update()
    {
        // まだ情報が取れていない場合、定期的にチェック
        if (!_hasUpdatedRoomInfo)
        {
            UpdateRoomInfo();
        }
    }

    void UpdateRoomInfo()
    {
        if (NetworkConnectionManager.instance == null || NetworkConnectionManager.instance.Runner == null)
        {
            if (roomIDText) roomIDText.text = "Offline";
            return;
        }

        var runner = NetworkConnectionManager.instance.Runner;
        if (runner.IsRunning && runner.SessionInfo.IsValid)
        {
            string sessionName = runner.SessionInfo.Name;
            if (roomIDText) roomIDText.text = $"Room ID: {sessionName}";
            _hasUpdatedRoomInfo = true;
        }
        else
        {
            if (roomIDText) roomIDText.text = "Connecting...";
        }
    }

    public void CopyRoomID()
    {
        if (NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null)
        {
             GUIUtility.systemCopyBuffer = NetworkConnectionManager.instance.Runner.SessionInfo.Name;
             Debug.Log("Room ID copied to clipboard!");
        }
    }
}
