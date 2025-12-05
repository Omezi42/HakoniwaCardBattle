using UnityEngine;
using TMPro;

public class SimpleTooltip : MonoBehaviour
{
    [Header("UIパーツ")]
    public GameObject rootObject; // 背景画像などの親オブジェクト
    public TextMeshProUGUI labelText;
    
    [Header("設定")]
    public Vector3 offset = new Vector3(0, 50, 0); // マウスからのズレ

    void Awake()
    {
        // 最初は隠す
        Hide();
    }

    void Update()
    {
        // 表示中はマウスの位置に追従させる
        if (rootObject.activeSelf)
        {
            transform.position = Input.mousePosition + offset;
        }
    }

    public void Show(string text)
    {
        if (rootObject != null) rootObject.SetActive(true);
        if (labelText != null) labelText.text = text;
        
        // 表示した瞬間に位置を合わせる
        transform.position = Input.mousePosition + offset;
        
        // 最前面に表示
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (rootObject != null) rootObject.SetActive(false);
    }
}