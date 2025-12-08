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

        // ON → OFF になる瞬間だけアニメーション
        if (isOn && !active)
        {
            StartCoroutine(ConsumeAnimation(offSprite));
        }
        else
        {
            // それ以外は即座に切り替え
            image.sprite = active ? onSprite : offSprite;
            image.transform.localScale = Vector3.one; // サイズ戻す
        }
        
        isOn = active;
    }

    IEnumerator ConsumeAnimation(Sprite offSprite)
    {
        // 1. シュッと小さくなる
        float duration = 0.2f;
        float t = 0;
        while (t < 1.0f)
        {
            t += Time.deltaTime / duration;
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
            yield return null;
        }

        // 2. 画像をOFFに変えて戻す
        image.sprite = offSprite;
        transform.localScale = Vector3.one; // 元の大きさに戻す（または0.8くらいで止める演出もあり）
        
        // オプション：パーティクルを出すならここで Instantiate
    }
}