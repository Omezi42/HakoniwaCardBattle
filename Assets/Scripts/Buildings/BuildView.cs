using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

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

    // ★修正：敵のビルドを左右反転しつつ、テキストは正位置に戻す
    public void SetFlip(bool flip)
    {
        if (flip)
        {
            // 親オブジェクト（アイコン全体）を左右反転
            transform.localScale = new Vector3(-1, 1, 1);
            
            // 子要素のテキストはさらに反転させて、鏡文字を防ぐ（-1 * -1 = 1）
            if (lifeText != null)
            {
                lifeText.rectTransform.localScale = new Vector3(-1, 1, 1);
            }
        }
        else
        {
            // 通常状態（自分用）
            transform.localScale = Vector3.one;
            if (lifeText != null)
            {
                lifeText.rectTransform.localScale = Vector3.one;
            }
        }
    }

    // --- 以下、ドラッグ操作やツールチップ表示のコード（変更なし） ---
    public void OnBeginDrag(PointerEventData eventData) { if (myBuild == null || !myBuild.isPlayerOwner || myBuild.isUnderConstruction || myBuild.hasActed) return; if (!HasTargetableAbility()) return; if (GameManager.instance != null) { GameManager.instance.OnClickCloseDetail(); dragStartPos = transform.position; GameManager.instance.ShowArrow(dragStartPos); GameManager.instance.SetArrowColor(Color.gray); } canvasGroup.blocksRaycasts = false; }
    public void OnDrag(PointerEventData eventData) { if (myBuild == null || !myBuild.isPlayerOwner || myBuild.isUnderConstruction || myBuild.hasActed) return; if (!HasTargetableAbility()) return; if (GameManager.instance != null) { GameManager.instance.UpdateArrow(dragStartPos, eventData.position); UpdateArrowColor(eventData); } }
    public void OnEndDrag(PointerEventData eventData) { if (GameManager.instance != null) GameManager.instance.HideArrow(); if (canvasGroup != null) canvasGroup.blocksRaycasts = true; if (myBuild == null || !myBuild.isPlayerOwner || myBuild.isUnderConstruction || myBuild.hasActed) return; if (!HasTargetableAbility()) return; GameObject targetObj = eventData.pointerCurrentRaycast.gameObject; if (targetObj != null) { UnitMover targetUnit = targetObj.GetComponentInParent<UnitMover>(); Leader targetLeader = targetObj.GetComponentInParent<Leader>(); if (targetUnit != null) TryActivateAbility(targetUnit); else if (targetLeader != null) { if (targetLeader.transform.parent.name == "EnemyBoard" || targetLeader.name == "EnemyInfo") { TryActivateAbility(targetLeader); } } } }
    bool HasTargetableAbility() { if (myBuild == null || myBuild.data == null) return false; foreach (var ability in myBuild.data.abilities) { if (ability.target == EffectTarget.SELECT_ENEMY_UNIT || ability.target == EffectTarget.SELECT_ENEMY_LEADER || ability.target == EffectTarget.SELECT_ANY_ENEMY || ability.target == EffectTarget.SELECT_ALLY_UNIT || ability.target == EffectTarget.SELECT_ANY_UNIT) { return true; } } return false; }
    void TryActivateAbility(object target) { foreach (var ability in myBuild.data.abilities) { if (IsTargetValid(ability.target, target)) { AbilityManager.instance.ActivateBuildAbility(ability, target, myBuild); GameManager.instance.PlaySE(GameManager.instance.seAttack); return; } } }
    bool IsTargetValid(EffectTarget targetType, object target) { if (target is UnitMover) { UnitMover unit = (UnitMover)target; if (!unit.isPlayerUnit) return targetType == EffectTarget.SELECT_ENEMY_UNIT || targetType == EffectTarget.SELECT_ANY_ENEMY || targetType == EffectTarget.SELECT_ANY_UNIT; else return targetType == EffectTarget.SELECT_ALLY_UNIT || targetType == EffectTarget.SELECT_ANY_UNIT; } else if (target is Leader) { return targetType == EffectTarget.SELECT_ENEMY_LEADER || targetType == EffectTarget.SELECT_ANY_ENEMY; } return false; }
    void UpdateArrowColor(PointerEventData eventData) { GameObject hoverObj = eventData.pointerCurrentRaycast.gameObject; Color targetColor = Color.gray; string labelText = ""; bool showLabel = false; if (hoverObj != null) { UnitMover targetUnit = hoverObj.GetComponentInParent<UnitMover>(); Leader targetLeader = hoverObj.GetComponentInParent<Leader>(); if (targetUnit != null && !targetUnit.isPlayerUnit) { targetColor = Color.red; labelText = "攻撃"; showLabel = true; } else if (targetLeader != null) { if (targetLeader.transform.parent.name == "EnemyBoard" || targetLeader.name == "EnemyInfo") { targetColor = Color.red; labelText = "攻撃"; showLabel = true; } } } GameManager.instance.SetArrowColor(targetColor); GameManager.instance.SetArrowLabel(labelText, showLabel); }
    public void OnPointerEnter(PointerEventData eventData) { if (eventData.pointerDrag != null) return; if (!myBuild.isUnderConstruction) return; if (myBuild != null && GameManager.instance != null) { GameManager.instance.ShowBuildDetail(myBuild.data, myBuild.remainingTurns); } }
    public void OnPointerExit(PointerEventData eventData) { if (GameManager.instance != null) GameManager.instance.OnClickCloseDetail(); }
}