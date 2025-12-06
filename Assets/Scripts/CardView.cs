using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public CardData cardData;

    [Header("テキストパーツ")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI healthText;
    
    [Header("画像パーツ")]
    public Image iconImage;       // キャラクター/魔法の絵
    public Image frameImage;      // ★枠 (Unit/Spell × Rarity)
    public Image jobBgImage;      // ★背景 (Job)

    [Header("枠素材リスト (Inspectorで登録)")]
    // [0]Common, [1]Rare, [2]Epic, [3]Legend
    public Sprite[] unitFrames;   // ユニット用枠 4種
    public Sprite[] spellFrames;  // 魔法用枠 4種

    [Header("背景素材リスト (Inspectorで登録)")]
    // [0]Neutral, [1]Knight, [2]Mage, [3]Priest, [4]Rogue
    public Sprite[] jobBackgrounds; 

    [Header("UX演出")]
    public GameObject glowPanel;
    public CanvasGroup canvasGroup;

    public void SetCard(CardData data)
    {
        this.cardData = data;

        // --- テキスト反映 ---
        if (nameText != null) nameText.text = data.cardName;
        if (costText != null) costText.text = data.cost.ToString();

        // --- アイコン反映 ---
        if (data.cardIcon != null && iconImage != null)
        {
            iconImage.sprite = data.cardIcon;
        }

        // --- タイプ別の表示切り替え ---
        if (data.type == CardType.SPELL)
        {
            // 魔法：ステータス非表示
            if (attackText != null) attackText.text = "";
            if (healthText != null) healthText.text = "";
        }
        else
        {
            // ユニット：ステータス表示
            if (attackText != null) attackText.text = data.attack.ToString();
            if (healthText != null) healthText.text = data.health.ToString();
        }

        // --- ★新デザインの適用 ---

        // 1. ジョブ背景の適用
        int jobIndex = (int)data.job;
        if (jobBgImage != null && jobBackgrounds != null && jobIndex < jobBackgrounds.Length)
        {
            jobBgImage.sprite = jobBackgrounds[jobIndex];
        }

        // 2. 枠（フレーム）の適用
        // 「ユニットか魔法か」で使うリストを変える
        Sprite[] targetFrames = (data.type == CardType.UNIT) ? unitFrames : spellFrames;
        int rarityIndex = (int)data.rarity; // 0~3

        if (frameImage != null && targetFrames != null && rarityIndex < targetFrames.Length)
        {
            frameImage.sprite = targetFrames[rarityIndex];
        }

        // 初期化
        if (glowPanel != null) glowPanel.SetActive(false);
    }

    // ... (以下の SetPlayableState, OnPointerEnter/Exit はそのまま) ...
    public void SetPlayableState(bool isPlayable)
    {
        if (glowPanel != null) glowPanel.SetActive(isPlayable);
        if (canvasGroup != null) canvasGroup.alpha = 1.0f; 
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null) return;
        if (GameManager.instance == null) return;
        GameManager.instance.ShowUnitDetail(cardData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        GameManager.instance.OnClickCloseDetail();
    }
}