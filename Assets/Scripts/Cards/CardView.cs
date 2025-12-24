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
    public Image magicTypeIcon;   // 魔法カード用のアイコン
    public Image buildTypeIcon;   // ★追加：ビルド用のアイコン（ハンマーなど）

    [Header("枠素材リスト (Inspectorで登録)")]
    // [0]Common, [1]Rare, [2]Epic, [3]Legend
    public Sprite[] unitFrames;   // ユニット用枠 4種
    public Sprite[] spellFrames;  // 魔法・ビルド用枠 4種

    [Header("背景素材リスト")]
    // [0]Neutral, [1]Knight, [2]Mage, [3]Priest, [4]Rogue
    public Sprite[] jobBackgrounds; 

    [Header("オーブ・アイコン画像素材")]
    public Sprite costOrbSprite;
    public Sprite attackOrbSprite;
    public Sprite healthOrbSprite;
    public Sprite magicIconSprite; 
    public Sprite buildIconSprite; // ★追加：ビルドアイコンの画像

    [Header("UX演出")]
    public GameObject glowPanel;
    public CanvasGroup canvasGroup;
    public GameObject cardBackObject; // 裏面

    // ホバー拡大用
    [Header("機能設定")]
    public bool enableHoverScale = true;  // 拡大するか
    public bool enableHoverDetail = true; // 詳細ウィンドウを出すか

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
        bool isUnit = (data.type == CardType.UNIT);
        bool isSpell = (data.type == CardType.SPELL);
        bool isBuild = (data.type == CardType.BUILD);

        // 1. 攻撃力 (ATK) - ユニットのみ表示
        if (attackOrbImage) attackOrbImage.gameObject.SetActive(isUnit);
        
        if (attackText != null)
        {
            attackText.gameObject.SetActive(isUnit);
            if(isUnit) attackText.text = data.attack.ToString();
        }
        
        // 2. 体力/耐久値 (HP/DUR) - ユニットまたはビルドで表示
        bool showHealth = isUnit || isBuild;

        if (healthOrbImage) healthOrbImage.gameObject.SetActive(showHealth);

        if (healthText != null)
        {
            healthText.gameObject.SetActive(showHealth);
            if (isUnit)
            {
                healthText.text = data.health.ToString();
            }
            else if (isBuild)
            {
                healthText.text = data.duration.ToString(); // ビルドなら持続ターンを表示
            }
        }

        // 3. スペルアイコン
        if (magicTypeIcon != null)
        {
            magicTypeIcon.gameObject.SetActive(isSpell);
            if(isSpell && magicIconSprite != null) magicTypeIcon.sprite = magicIconSprite;
        }

        // 4. ビルドアイコン
        if (buildTypeIcon != null)
        {
            buildTypeIcon.gameObject.SetActive(isBuild);
            if(isBuild && buildIconSprite != null) buildTypeIcon.sprite = buildIconSprite;
        }

        // 5. 枠の適用
        if (frameImage != null)
        {
            int rarityIndex = (int)data.rarity;
            
            if (isUnit)
            {
                if (unitFrames != null && rarityIndex < unitFrames.Length)
                    frameImage.sprite = unitFrames[rarityIndex];
            }
            else
            {
                // スペルとビルドは共通枠（必要ならここも分けられます）
                if (spellFrames != null && rarityIndex < spellFrames.Length)
                    frameImage.sprite = spellFrames[rarityIndex];
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
            // 必要に応じてビルド専用の詳細表示メソッドを呼ぶことも可能
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