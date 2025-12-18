using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Fusion; // [NEW]

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
            obj.transform.localScale = Vector3.one; // スケールリセット
            
            // ★修正: UI用カードがNetworkObjectを持っている場合、Destroy時にネットワーク同期されてしまう恐れがあるため削除
            var netObj = obj.GetComponent<NetworkObject>();
            if (netObj != null) Destroy(netObj);

            // ドラッグ機能を削除（クリックの邪魔になるため）
            var drag = obj.GetComponent<Draggable>();
            if (drag != null) Destroy(drag);
            
            var deckDrag = obj.GetComponent<DeckDraggable>();
            if (deckDrag != null) Destroy(deckDrag);

            // カードの見た目をセット
            var view = obj.GetComponent<CardView>();
            if(view != null) 
            {
                view.SetCard(data);
                
                // ★修正：拡大表示系をすべてオフにする
                view.enableHoverScale = false;  // マウスを乗せても大きくならない
                view.enableHoverDetail = false; // マウスを乗せても詳細パネルを出さない
            }

            // 選択状態の表示
            var selectionOverlay = obj.transform.Find("SelectionOverlay");
            if (selectionOverlay != null) selectionOverlay.gameObject.SetActive(replaceFlags[index]);

            // クリック処理
            var btn = obj.GetComponent<Button>();
            if (btn == null) btn = obj.AddComponent<Button>();

            // Imageがないとクリックできないので透明なImageを追加
            if (obj.GetComponent<Image>() == null)
            {
                var img = obj.AddComponent<Image>();
                img.color = Color.clear;
            }

            btn.onClick.RemoveAllListeners();
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