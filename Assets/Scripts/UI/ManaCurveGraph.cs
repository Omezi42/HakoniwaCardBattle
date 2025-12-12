using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ManaCurveGraph : MonoBehaviour
{
    [Header("UIパーツ")]
    [Tooltip("コスト0, 1, 2, 3, 4, 5, 6, 7+ の順で8本の棒(Image)を登録")]
    public RectTransform[] bars; 
    
    [Tooltip("各棒の横（または上）に表示する数値テキスト（任意）")]
    public TextMeshProUGUI[] countTexts;

    [Header("設定")]
    public float maxWidth = 200f;  // ★変更：グラフが一番伸びた時の「横幅」
    public int graphScaleMax = 10; // グラフの最大値（この枚数でMAXの幅になる）

    public void UpdateGraph(List<CardData> deck)
    {
        // 1. コストごとの枚数を集計 (0～7+)
        int[] counts = new int[8]; 
        foreach (var card in deck)
        {
            if (card == null) continue;
            int cost = card.cost;
            if (cost >= 7) cost = 7; // 7以上はまとめて集計
            counts[cost]++;
        }

        // 2. グラフの更新
        for (int i = 0; i < bars.Length; i++)
        {
            if (bars[i] == null) continue;

            int count = counts[i];

            // 幅計算（0～1の割合 × maxWidth）
            float ratio = Mathf.Clamp01((float)count / graphScaleMax);
            float targetWidth = ratio * maxWidth;
            
            // 最低限の幅を確保（見た目が潰れないように）
            if (count > 0 && targetWidth < 10f) targetWidth = 10f;

            // ★変更：幅(x)を適用し、高さ(y)はそのまま維持
            bars[i].sizeDelta = new Vector2(targetWidth, bars[i].sizeDelta.y);

            // 色の変化
            var img = bars[i].GetComponent<Image>();
            if (img != null)
            {
                Color c = img.color;
                c.a = (count > 0) ? 1.0f : 0.3f; 
                img.color = c;
            }

            // 数値テキスト更新
            if (countTexts != null && i < countTexts.Length && countTexts[i] != null)
            {
                countTexts[i].text = (count > 0) ? count.ToString() : "";
            }
        }
    }
}