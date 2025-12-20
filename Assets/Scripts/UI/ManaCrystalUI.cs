using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ManaCrystalUI : MonoBehaviour
{
    public Image image;
    private bool isOn = false;

    // 状態を更新する（GameManagerから呼ぶ）
    public void SetState(bool active, Sprite onSprite, Sprite offSprite)
    {
        if (image == null) image = GetComponent<Image>();

        // アクティブかどうかで処理を分岐
        if (gameObject.activeInHierarchy)
        {
            // ON -> OFF (消費)
            if (isOn && !active)
            {
                StopAllCoroutines();
                StartCoroutine(ConsumeAnimation(offSprite));
            }
            // OFF -> ON (回復)
            else if (!isOn && active)
            {
                StopAllCoroutines();
                StartCoroutine(RestoreAnimation(onSprite));
            }
            // 状態が変わらない、または初期化時など
            else if (image.sprite != (active ? onSprite : offSprite))
            {
                image.sprite = active ? onSprite : offSprite;
                transform.localScale = Vector3.one;
            }
        }
        else
        {
            // 非アクティブ時は即時反映
            image.sprite = active ? onSprite : offSprite;
            transform.localScale = Vector3.one;
        }
        
        isOn = active;
    }

    IEnumerator ConsumeAnimation(Sprite offSprite)
    {
        // 1. Anticipation (予備動作): ほんの少し膨らむ
        float t = 0;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.2f, t / 0.1f);
            yield return null;
        }

        // 2. Consume (消費): 急激に縮む
        t = 0;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            // EaseInBackのような動き
            float progress = t / 0.15f;
            float scale = Mathf.Lerp(1.2f, 0f, progress * progress);
            transform.localScale = Vector3.one * scale;
            yield return null;
        }
        transform.localScale = Vector3.zero;

        // 3. Swap & Recover (空スロット表示): バウンドして戻る
        image.sprite = offSprite;
        t = 0;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            float p = t / 0.2f;
            // EaseOutBack風
            float s = Mathf.Sin(p * Mathf.PI * 0.5f); 
            float scale = Mathf.Lerp(0.5f, 1.0f, s); // 0からではなく0.5から戻る方がテンポが良い
            transform.localScale = Vector3.one * scale;
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    IEnumerator RestoreAnimation(Sprite onSprite)
    {
        // 1. 画像を切り替え、少し小さくセット
        image.sprite = onSprite;
        transform.localScale = Vector3.one * 0.5f;

        // 2. 弾むように出現 (Elastic Out)
        float t = 0;
        float duration = 0.4f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            
            // シンプルなElastic表現
            float scale = 1.0f + Mathf.Sin(p * Mathf.PI * 3.0f) * Mathf.Exp(-5.0f * p) * 0.5f;
            if (p >= 1.0f) scale = 1.0f;
            
            transform.localScale = Vector3.one * scale;
            yield return null;
        }
        transform.localScale = Vector3.one;
    }
}