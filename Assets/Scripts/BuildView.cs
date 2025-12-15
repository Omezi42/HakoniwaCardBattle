using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

// ドラッグ系インターフェースを追加
public class BuildView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UIパーツ")]
    public Image iconImage;
    public TextMeshProUGUI lifeText;

    [Header("画像リソース")]
    public Sprite constructionSprite;

    private ActiveBuild myBuild;
    private CanvasGroup canvasGroup;
    private Vector3 dragStartPos;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void SetBuild(ActiveBuild build)
    {
        myBuild = build;

        if (build.isUnderConstruction)
        {
            if (constructionSprite != null)
            {
                iconImage.sprite = constructionSprite;
            }
            iconImage.color = Color.white;
        }
        else
        {
            // 完成後: data.icon -> data.cardIcon
            if (build.data.cardIcon != null)
            {
                iconImage.sprite = build.data.cardIcon;
            }
            
            if (build.hasActed)
            {
                iconImage.color = Color.gray;
            }
            else
            {
                iconImage.color = Color.white;
            }
        }

        if (lifeText != null) lifeText.text = build.remainingTurns.ToString();
    }
    // --- ドラッグ処理 ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        // ★修正：行動済み (hasActed) ならドラッグできないようにする
        if (myBuild == null || !myBuild.isPlayerOwner || myBuild.isUnderConstruction || myBuild.hasActed) return;

        // ターゲット可能な能力がなければドラッグ不可
        if (!HasTargetableAbility()) return;

        if (GameManager.instance != null)
        {
            GameManager.instance.OnClickCloseDetail();
            // 矢印表示
            dragStartPos = transform.position;
            GameManager.instance.ShowArrow(dragStartPos);
            GameManager.instance.SetArrowColor(Color.gray);
        }

        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (myBuild == null || !myBuild.isPlayerOwner || myBuild.isUnderConstruction || myBuild.hasActed) return;
        if (!HasTargetableAbility()) return;

        if (GameManager.instance != null)
        {
            GameManager.instance.UpdateArrow(dragStartPos, eventData.position);
            UpdateArrowColor(eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (GameManager.instance != null) GameManager.instance.HideArrow();
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        if (myBuild == null || !myBuild.isPlayerOwner || myBuild.isUnderConstruction || myBuild.hasActed) return;
        if (!HasTargetableAbility()) return;

        // ドロップ先の判定
        GameObject targetObj = eventData.pointerCurrentRaycast.gameObject;
        if (targetObj != null)
        {
            UnitMover targetUnit = targetObj.GetComponentInParent<UnitMover>();
            Leader targetLeader = targetObj.GetComponentInParent<Leader>();

            if (targetUnit != null) TryActivateAbility(targetUnit);
            else if (targetLeader != null)
            {
                // 敵リーダー判定（簡易）
                if (targetLeader.transform.parent.name == "EnemyBoard" || targetLeader.name == "EnemyInfo")
                {
                    TryActivateAbility(targetLeader);
                }
            }
        }
    }

    // 能力を持っているかチェック
    bool HasTargetableAbility()
    {
        if (myBuild == null || myBuild.data == null) return false;
        foreach (var ability in myBuild.data.abilities)
        {
            if (ability.target == EffectTarget.SELECT_ENEMY_UNIT || 
                ability.target == EffectTarget.SELECT_ENEMY_LEADER ||
                ability.target == EffectTarget.SELECT_ANY_ENEMY ||
                // ★追加
                ability.target == EffectTarget.SELECT_ALLY_UNIT ||
                ability.target == EffectTarget.SELECT_ANY_UNIT)
            {
                return true;
            }
        }
        return false;
    }

    // 発動試行
    void TryActivateAbility(object target)
    {
        foreach (var ability in myBuild.data.abilities)
        {
            if (IsTargetValid(ability.target, target))
            {
                // ★修正：GameManager → AbilityManager に変更
                AbilityManager.instance.ActivateBuildAbility(ability, target, myBuild);
                GameManager.instance.PlaySE(GameManager.instance.seAttack);
                return;
            }
        }
    }

    bool IsTargetValid(EffectTarget targetType, object target)
    {
        if (target is UnitMover)
        {
            UnitMover unit = (UnitMover)target;
            if (!unit.isPlayerUnit) 
                return targetType == EffectTarget.SELECT_ENEMY_UNIT || targetType == EffectTarget.SELECT_ANY_ENEMY || targetType == EffectTarget.SELECT_ANY_UNIT; // 敵
            else
                return targetType == EffectTarget.SELECT_ALLY_UNIT || targetType == EffectTarget.SELECT_ANY_UNIT; // 味方
        }
        else if (target is Leader)
        {
            return targetType == EffectTarget.SELECT_ENEMY_LEADER || targetType == EffectTarget.SELECT_ANY_ENEMY;
        }
        return false;
    }

    void UpdateArrowColor(PointerEventData eventData)
    {
        GameObject hoverObj = eventData.pointerCurrentRaycast.gameObject;
        Color targetColor = Color.gray;
        string labelText = "";
        bool showLabel = false;

        if (hoverObj != null)
        {
            UnitMover targetUnit = hoverObj.GetComponentInParent<UnitMover>();
            Leader targetLeader = hoverObj.GetComponentInParent<Leader>();

            if (targetUnit != null && !targetUnit.isPlayerUnit)
            {
                targetColor = Color.red;
                labelText = "攻撃";
                showLabel = true;
            }
            else if (targetLeader != null)
            {
                if (targetLeader.transform.parent.name == "EnemyBoard" || targetLeader.name == "EnemyInfo")
                {
                    targetColor = Color.red;
                    labelText = "攻撃";
                    showLabel = true;
                }
            }
        }

        GameManager.instance.SetArrowColor(targetColor);
        GameManager.instance.SetArrowLabel(labelText, showLabel);
    }

    // --- 既存のホバー処理 ---
    // マウスが乗った時
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null) return;

        // ★追加：建築中 (isUnderConstruction == true) の時だけ表示する
        // （つまり、建築完了していたらここで帰る）
        if (!myBuild.isUnderConstruction) return;

        if (myBuild != null && GameManager.instance != null)
        {
            GameManager.instance.ShowBuildDetail(myBuild.data, myBuild.remainingTurns);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (GameManager.instance != null) GameManager.instance.OnClickCloseDetail();
    }

    public void SetFlip(bool flip)
    {
        if (iconImage != null)
        {
            // 現在のスケールの絶対値を取得（元の大きさを維持するため）
            float currentX = Mathf.Abs(iconImage.transform.localScale.x);
            float currentY = iconImage.transform.localScale.y;
            float currentZ = iconImage.transform.localScale.z;

            // 反転させるならXをマイナスに、そうでなければプラスにする
            float newX = flip ? -currentX : currentX;

            iconImage.transform.localScale = new Vector3(newX, currentY, currentZ);
        }
    }
}