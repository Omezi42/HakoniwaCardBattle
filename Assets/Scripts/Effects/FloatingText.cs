using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    public TextMeshProUGUI textMesh;
    public float moveSpeed = 100f; // 上に登る速さ
    public float fadeSpeed = 2f;   // 消える速さ
    
    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (textMesh == null) textMesh = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void Setup(int damage)
    {
        // ダメージ量によって色を変えたりテキストを変えたりできる
        if (damage > 0)
        {
            textMesh.text = "-" + damage.ToString();
            textMesh.color = new Color(1f, 0.2f, 0.2f); // 赤色
        }
        else if (damage < 0) // 回復の場合
        {
            textMesh.text = "+" + Mathf.Abs(damage).ToString();
            textMesh.color = Color.green;
        }
        else
        {
            textMesh.text = "0";
            textMesh.color = Color.gray;
        }
    }

    void Update()
    {
        // 上に移動
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;

        // フェードアウト
        canvasGroup.alpha -= fadeSpeed * Time.deltaTime;

        // 透明になったら消す
        if (canvasGroup.alpha <= 0)
        {
            Destroy(gameObject);
        }
    }
}