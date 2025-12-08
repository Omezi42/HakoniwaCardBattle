using UnityEngine;
using TMPro;
using System.Collections;

public class TurnCutIn : MonoBehaviour
{
    [Header("UIパーツ")]
    public TextMeshProUGUI titleText; // "YOUR TURN" などの文字
    public CanvasGroup canvasGroup;   // 全体の透明度操作用

    [Header("設定")]
    public float animSpeed = 5f;      // 出現・消失の速さ
    public float stopTime = 1.0f;     // 画面中央で止まる時間

    private void Start()
    {
        // 最初は隠しておく
        canvasGroup.alpha = 0;
        gameObject.SetActive(false);
    }

    // 外部から呼ぶ関数
    // 外部から呼ぶ関数
    public void Show(string text, Color color)
    {
        // ★修正：確実にアクティブにする
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        
        // CanvasGroupのalphaも強制的に1からスタートしてみる（フェードインなら0からだが、見えない原因切り分けのため）
        // いや、フェードイン処理があるので0でOKです。
        
        titleText.text = text;
        titleText.color = color;

        StopAllCoroutines();
        StartCoroutine(PlayAnimation());
    }

    IEnumerator PlayAnimation()
    {
        // 1. フェードイン（＆少し拡大しながら出すとかっこいい）
        float t = 0;
        canvasGroup.alpha = 0;
        transform.localScale = Vector3.one * 1.2f; // 最初はちょっと大きく

        while (t < 1.0f)
        {
            t += Time.deltaTime * animSpeed;
            canvasGroup.alpha = t;
            // 1.2倍 → 1.0倍 に戻しながら表示
            transform.localScale = Vector3.Lerp(Vector3.one * 1.2f, Vector3.one, t);
            yield return null;
        }
        
        // 念のため値を整える
        canvasGroup.alpha = 1.0f;
        transform.localScale = Vector3.one;

        // 2. 一時停止（プレイヤーに見せる時間）
        yield return new WaitForSeconds(stopTime);

        // 3. フェードアウト
        t = 0;
        while (t < 1.0f)
        {
            t += Time.deltaTime * animSpeed;
            canvasGroup.alpha = 1.0f - t; // だんだん透明に
            yield return null;
        }

        gameObject.SetActive(false);
    }
}