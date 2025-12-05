using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshProを使うので必須

public class TargetArrow : MonoBehaviour
{
    [Header("パーツ設定")]
    public GameObject arrowHead;
    public GameObject dotPrefab;
    public int dotCount = 10;

    [Header("アクションラベル")] // ★追加
    public GameObject labelGroup;   // ラベル全体（背景＋文字）の親
    public TextMeshProUGUI labelText; // 文字を表示するコンポーネント

    [Header("微調整")]
    public float rotationOffset = -90f; // ★追加：向きのズレをここで直す！（-90, 0, 90, 180 など試す）
    public Vector3 labelOffset = new Vector3(0, 50, 0); // ★追加：ラベルを少し上にずらす量

    private GameObject[] dots;
    private bool isActive = false;
    private Image headImage;
    private Image[] dotImages;

    void Awake()
    {
        // ★修正：原本のドットが表示されっぱなしにならないように隠す
        if(dotPrefab != null) dotPrefab.SetActive(false);

        dots = new GameObject[dotCount];
        dotImages = new Image[dotCount];

        for (int i = 0; i < dotCount; i++)
        {
            // 原本が非表示だとInstantiateした瞬間に非表示になるので、生成後にtrueにする必要あり
            dots[i] = Instantiate(dotPrefab, transform);
            // dots[i].SetActive(false); // ← Show()でやるのでここでは不要、あるいは初期化としてfalseでもOK
            dotImages[i] = dots[i].GetComponent<Image>();
        }
        
        if(arrowHead != null) 
        {
            headImage = arrowHead.GetComponent<Image>();
            arrowHead.SetActive(false);
        }
        
        if (labelGroup != null) labelGroup.SetActive(false);
        gameObject.SetActive(false);
    }

    public void Show(Vector3 startWorldPos)
    {
        gameObject.SetActive(true);
        isActive = true;
        if(arrowHead) arrowHead.SetActive(true);
        foreach (var dot in dots) dot.SetActive(true);
        
        // ★追加：表示した瞬間に一度位置を更新しないと、一瞬(0,0)に表示されることがある
        // （ただし終点が決まってないので、ここでは最低限の処理か、UpdatePositionが呼ばれるのを待つ）
    }

    public void SetLabel(string text, bool visible)
    {
        if (labelGroup != null)
        {
            labelGroup.SetActive(visible);
            if (visible && labelText != null)
            {
                labelText.text = text;
            }
        }
    }
    public void SetColor(Color color)
    {
        if (headImage != null) headImage.color = color;
        foreach (var img in dotImages)
        {
            if (img != null) img.color = color;
        }
    }

    public void UpdatePosition(Vector3 startWorldPos, Vector3 endWorldPos)
    {
        if (!isActive) return;

        // 制御点の高さ調整（画面サイズに合わせて調整してください：100f～300fくらい）
        Vector3 controlPos = (startWorldPos + endWorldPos) / 2 + Vector3.up * 200f; 

        for (int i = 0; i < dotCount; i++)
        {
            float t = (float)i / dotCount;
            Vector3 pos = CalculateBezier(t, startWorldPos, controlPos, endWorldPos);
            dots[i].transform.position = pos;
            
            float scale = 0.5f + t * 0.5f;
            dots[i].transform.localScale = Vector3.one * scale;
        }

        if(arrowHead != null)
        {
            arrowHead.transform.position = endWorldPos;
            if (dotCount > 0)
            {
                Vector3 dir = endWorldPos - dots[dotCount - 1].transform.position;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                
                // ★修正：補正値(rotationOffset)を足して、向きを合わせる
                arrowHead.transform.rotation = Quaternion.Euler(0, 0, angle + rotationOffset);
            }
        }

        if (labelGroup != null && labelGroup.activeSelf)
        {
            labelGroup.transform.position = endWorldPos + labelOffset;
            // 回転はさせない（文字が読みづらくなるため）
            labelGroup.transform.rotation = Quaternion.identity; 
        }
    }

    public void Hide()
    {
        isActive = false;
        gameObject.SetActive(false);
        if (labelGroup != null) labelGroup.SetActive(false);
    }

    Vector3 CalculateBezier(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        return (uu * p0) + (2 * u * t * p1) + (tt * p2);
    }
}