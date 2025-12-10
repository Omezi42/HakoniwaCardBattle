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
    
    // 説明文が必要なら残す
    public TextMeshProUGUI descText;

    [Header("画像パーツ")]
    public Image iconImage;       // キャラクター/魔法の絵
    public Image frameImage;      // 枠
    public Image jobBgImage;      // ジョブ背景

    [Header("ステータス・タイプアイコン")]
    public Image costOrbImage;
    public Image attackOrbImage;
    public Image healthOrbImage;
    public Image magicTypeIcon;   // ★追加：魔法カード用のアイコン（魔導書や魔法陣など）

    [Header("枠素材リスト (Inspectorで登録)")]
    // [0]Common, [1]Rare, [2]Epic, [3]Legend
    public Sprite[] unitFrames;   // ユニット用枠 4種
    public Sprite[] spellFrames;  // 魔法用枠 4種

    [Header("背景素材リスト")]
    // [0]Neutral, [1]Knight, [2]Mage, [3]Priest, [4]Rogue
    public Sprite[] jobBackgrounds; 

    // オーブ画像（固定ならInspectorでセットしておけばコードでの代入は不要ですが念のため）
    [Header("オーブ画像素材")]
    public Sprite costOrbSprite;
    public Sprite attackOrbSprite;
    public Sprite healthOrbSprite;
    public Sprite magicIconSprite; // ★追加：魔法アイコンの画像

    [Header("UX演出")]
    public GameObject glowPanel;
    public CanvasGroup canvasGroup;
    public GameObject cardBackObject; // 裏面

    // ホバー拡大用
    [Header("機能設定")]
    public bool enableHoverScale = true;  // 拡大するか
    public bool enableHoverDetail = true; // ★追加：詳細ウィンドウを出すか

    private float hoverScale = 1.5f;
    private Canvas myCanvas;

    void Awake()
    {
        myCanvas = GetComponent<Canvas>();
        if (myCanvas == null) myCanvas = gameObject.AddComponent<Canvas>();
    }

    public void SetCard(CardData data)
    {
        this.cardData = data;

        // --- テキスト反映 ---
        if (nameText != null) nameText.text = data.cardName;
        if (costText != null) costText.text = data.cost.ToString();
        if (descText != null) descText.text = data.description;

        // --- アイコン反映 ---
        if (data.cardIcon != null && iconImage != null)
        {
            iconImage.sprite = data.cardIcon;
        }

        // --- ★修正：タイプ別の表示切り替え ---
        if (data.type == CardType.SPELL)
        {
            // 魔法の場合
            // 1. 攻撃・体力のアイコンと数値を隠す
            if (attackOrbImage != null) attackOrbImage.gameObject.SetActive(false);
            if (healthOrbImage != null) healthOrbImage.gameObject.SetActive(false);
            if (attackText != null) attackText.gameObject.SetActive(false);
            if (healthText != null) healthText.gameObject.SetActive(false);

            // 2. 魔法アイコンを表示する
            if (magicTypeIcon != null)
            {
                magicTypeIcon.gameObject.SetActive(true);
                if (magicIconSprite != null) magicTypeIcon.sprite = magicIconSprite;
            }

            // 3. 魔法用の枠を適用
            int rarityIndex = (int)data.rarity;
            if (frameImage != null && spellFrames != null && rarityIndex < spellFrames.Length)
            {
                frameImage.sprite = spellFrames[rarityIndex];
            }
        }
        else
        {
            // ユニットの場合
            // 1. 攻撃・体力のアイコンと数値を表示する
            if (attackOrbImage != null) attackOrbImage.gameObject.SetActive(true);
            if (healthOrbImage != null) healthOrbImage.gameObject.SetActive(true);
            if (attackText != null)
            {
                attackText.gameObject.SetActive(true);
                attackText.text = data.attack.ToString();
            }
            if (healthText != null)
            {
                healthText.gameObject.SetActive(true);
                healthText.text = data.health.ToString();
            }

            // 2. 魔法アイコンを隠す
            if (magicTypeIcon != null) magicTypeIcon.gameObject.SetActive(false);

            // 3. ユニット用の枠を適用
            int rarityIndex = (int)data.rarity;
            if (frameImage != null && unitFrames != null && rarityIndex < unitFrames.Length)
            {
                frameImage.sprite = unitFrames[rarityIndex];
            }
        }

        // --- ジョブ背景の適用 ---
        int jobIndex = (int)data.job;
        if (jobBgImage != null && jobBackgrounds != null && jobIndex < jobBackgrounds.Length)
        {
            jobBgImage.sprite = jobBackgrounds[jobIndex];
        }

        // --- その他初期化 ---
        if (costOrbImage != null && costOrbSprite != null) costOrbImage.sprite = costOrbSprite;
        if (attackOrbImage != null && attackOrbSprite != null) attackOrbImage.sprite = attackOrbSprite;
        if (healthOrbImage != null && healthOrbSprite != null) healthOrbImage.sprite = healthOrbSprite;

        if (glowPanel != null) glowPanel.SetActive(false);
    }

    public void ShowBack(bool show)
    {
        if (cardBackObject != null) cardBackObject.SetActive(show);
    }

    public void SetPlayableState(bool isPlayable)
    {
        if (glowPanel != null) glowPanel.SetActive(isPlayable);
        if (canvasGroup != null) canvasGroup.alpha = 1.0f; 
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null) return;
        if (!enableHoverDetail) return;
        if (GameManager.instance == null) return;
        
        if (GameManager.instance != null) 
        {
            GameManager.instance.ShowUnitDetail(cardData);
        }

        if (enableHoverScale)
        {
            if (myCanvas != null)
            {
                myCanvas.overrideSorting = true;
                myCanvas.sortingOrder = 100;
            }
            transform.localScale = Vector3.one * hoverScale;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!enableHoverDetail) return;
        if (GameManager.instance != null)
        {
            GameManager.instance.OnClickCloseDetail();
        }

        if (enableHoverScale)
        {
            if (myCanvas != null)
            {
                myCanvas.overrideSorting = false;
                myCanvas.sortingOrder = 0;
            }
            transform.localScale = Vector3.one;
        }
    }
}