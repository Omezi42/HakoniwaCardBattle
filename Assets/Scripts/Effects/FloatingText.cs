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

    public void Setup(int val)
    {
        if (textMesh == null)
        {
            textMesh = GetComponentInChildren<TextMeshProUGUI>();
            if (textMesh == null) return;
        }

        // val > 0 はダメージ（通常）、val < 0 は回復
        if (val > 0)
        {
            textMesh.text = "-" + val.ToString();
            textMesh.color = Color.red;
        }
        else if (val < 0)
        {
            textMesh.text = "+" + Mathf.Abs(val).ToString();
            textMesh.color = Color.green;
        }
        else
        {
            textMesh.text = "0";
            textMesh.color = Color.gray;
        }

        // 確実に反映させる
        textMesh.ForceMeshUpdate();
    }

    public float lifeTime = 1.5f;

    void Update()
    {
        // 上に移動
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;

        // フェードアウト
        if (canvasGroup != null)
        {
            canvasGroup.alpha -= fadeSpeed * Time.deltaTime;
        }

        lifeTime -= Time.deltaTime;

        // 透明になったら、あるいは時間が経過したら消す
        if (lifeTime <= 0 || (canvasGroup != null && canvasGroup.alpha <= 0))
        {
            Destroy(gameObject);
        }
    }
}