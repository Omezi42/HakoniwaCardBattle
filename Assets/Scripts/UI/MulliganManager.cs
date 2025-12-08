using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class MulliganManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject panelRoot;
    public Transform cardContainer; // カードを並べる親
    public Button confirmButton;
    public TextMeshProUGUI statusText; // "カードを選択してください"

    [Header("Prefab")]
    public GameObject mulliganCardPrefab; // 専用の表示用カード

    private List<CardData> initialHand = new List<CardData>();
    private List<bool> replaceFlags = new List<bool>(); // trueなら交換

    public void ShowMulligan(List<CardData> hand)
    {
        initialHand = hand;
        replaceFlags = new List<bool>(new bool[hand.Count]); // 全部falseで初期化

        panelRoot.SetActive(true);
        RefreshUI();
    }

    void RefreshUI()
    {
        foreach (Transform child in cardContainer) Destroy(child.gameObject);

        for (int i = 0; i < initialHand.Count; i++)
        {
            int index = i;
            CardData data = initialHand[i];
            
            GameObject obj = Instantiate(mulliganCardPrefab, cardContainer);
            
            // ★追加：生成後にスケールを確実に1にする
            obj.transform.localScale = Vector3.one;
            
            // カードの見た目をセット（CardViewなどを流用）
            // ※MulliganCardUIという専用スクリプトを作って操作しても良いですが、
            // 今回は簡易的に既存のCardViewを使い、クリック判定を被せます
            var view = obj.GetComponent<CardView>();
            if(view != null) 
            {
                view.SetCard(data);
                
                // ★追加：マリガン時は拡大しないように設定！
                view.enableHoverScale = false;
            }

            // 選択状態の表示（バツ印や暗転など）
            var selectionOverlay = obj.transform.Find("SelectionOverlay"); // プレハブに作っておく
            if (selectionOverlay != null) selectionOverlay.gameObject.SetActive(replaceFlags[index]);

            // クリック処理
            // クリック処理
            var btn = obj.GetComponent<Button>();
            if (btn == null) btn = obj.AddComponent<Button>();

            btn.onClick.AddListener(() => 
            {
                replaceFlags[index] = !replaceFlags[index];
                RefreshUI();
            });
        }
    }

    public void OnClickConfirm()
    {
        // GameManagerに結果を返す
        GameManager.instance.EndMulligan(replaceFlags);
        panelRoot.SetActive(false);
    }
}