using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems; // マウスオーバー検知に必要

public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public CardData cardData;

    [Header("基本パーツ")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI healthText;
    public Image iconImage;
    
    // 説明文はカード上には出さないが、データとしては保持しておく（詳細表示用）
    // public TextMeshProUGUI descText; // 削除またはコメントアウト

    [Header("新デザインパーツ")]
    public Image baseFrameImage;      // 黒いカード地
    public Image jobNamePlateImage;   // ジョブごとの名前枠
    public Image portraitBgImage;     // レアリティごとの背景
    
    public Image costOrbImage;        // コストのオーブ
    public Image attackOrbImage;      // 攻撃力のオーブ
    public Image healthOrbImage;      // 体力のオーブ

    [Header("素材リスト（Inspectorでセット）")]
    // [0]Neutral, [1]Knight, [2]Mage, [3]Priest, [4]Rogue
    public Sprite[] jobNamePlates;    
    
    // [0]Common, [1]Rare, [2]Legend (Enumの定義数に合わせる)
    public Sprite[] rarityBackgrounds;

    [Header("オーブ画像（Inspectorでセット）")]
    public Sprite costOrbSprite;
    public Sprite attackOrbSprite;
    public Sprite healthOrbSprite;

    [Header("UX演出")]
    public GameObject glowPanel;
    public CanvasGroup canvasGroup;

    public void SetCard(CardData data)
    {
        this.cardData = data;

        // --- テキスト・アイコン設定 ---
        if (nameText != null) nameText.text = data.cardName;
        if (costText != null) costText.text = data.cost.ToString();

        if (data.cardIcon != null && iconImage != null)
        {
            iconImage.sprite = data.cardIcon;
        }

        // スペルの場合は攻/体を表示しない（オーブごと消す）
        if (data.type == CardType.SPELL)
        {
            if (attackText != null) attackText.text = "";
            if (healthText != null) healthText.text = "";
            if (attackOrbImage != null) attackOrbImage.gameObject.SetActive(false);
            if (healthOrbImage != null) healthOrbImage.gameObject.SetActive(false);
        }
        else
        {
            if (attackText != null) attackText.text = data.attack.ToString();
            if (healthText != null) healthText.text = data.health.ToString();
            if (attackOrbImage != null) attackOrbImage.gameObject.SetActive(true);
            if (healthOrbImage != null) healthOrbImage.gameObject.SetActive(true);
        }

        // --- 新デザインの反映 ---
        
        // 1. オーブ画像のセット（固定ならInspectorで入れておけばこの処理は不要だが念のため）
        if (costOrbImage != null && costOrbSprite != null) costOrbImage.sprite = costOrbSprite;
        if (attackOrbImage != null && attackOrbSprite != null) attackOrbImage.sprite = attackOrbSprite;
        if (healthOrbImage != null && healthOrbSprite != null) healthOrbImage.sprite = healthOrbSprite;

        // 2. ジョブ名プレートの切り替え
        int jobIndex = (int)data.job;
        if (jobNamePlateImage != null && jobNamePlates != null && jobIndex < jobNamePlates.Length)
        {
            jobNamePlateImage.sprite = jobNamePlates[jobIndex];
        }

        // 3. レアリティ背景の切り替え
        int rarityIndex = (int)data.rarity;
        if (portraitBgImage != null && rarityBackgrounds != null && rarityIndex < rarityBackgrounds.Length)
        {
            portraitBgImage.sprite = rarityBackgrounds[rarityIndex];
        }

        if (glowPanel != null) glowPanel.SetActive(false);
    }

    public void SetPlayableState(bool isPlayable)
    {
        if (glowPanel != null) glowPanel.SetActive(isPlayable);
        // カード自体の透明度は変えない（視認性維持のため）
        if (canvasGroup != null) canvasGroup.alpha = 1.0f; 
    }

    // --- マウスオーバー時の処理 ---

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null) return;
        if (GameManager.instance == null) return;
        // 詳細ウィンドウを表示する（GameManagerに依頼）
        // ※GameManager側に ShowDetailPopup のようなメソッドを作る必要があります
        GameManager.instance.ShowUnitDetail(cardData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 詳細ウィンドウを閉じる
        GameManager.instance.OnClickCloseDetail();
    }
}