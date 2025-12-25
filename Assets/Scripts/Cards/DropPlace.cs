using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DropPlace : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject unitPrefab;
    public bool isEnemySlot = false;

    private Image myImage;
    private Color defaultColor;
    public Color highlightColor = new Color(1f, 1f, 0.5f, 1f);

    void Start()
    {
        myImage = GetComponent<Image>();
        if (myImage != null)
        {
            myImage.alphaHitTestMinimumThreshold = 0.1f;
            defaultColor = myImage.color;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        CardView card = eventData.pointerDrag.GetComponent<CardView>();
        if (card != null && !isEnemySlot && transform.childCount == 0)
        {
            if (card.cardData.type == CardType.UNIT && GameManager.instance.currentMana >= card.cardData.cost)
            {
                card.transform.localScale = Vector3.one * 1.1f;
                if (myImage != null) myImage.color = highlightColor;
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null)
        {
            eventData.pointerDrag.transform.localScale = Vector3.one;
        }
        if (myImage != null) myImage.color = defaultColor;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (isEnemySlot) return;
        if (transform.childCount > 0) return;

        UnitMover draggedUnit = eventData.pointerDrag.GetComponent<UnitMover>();
        CardView draggedCard = eventData.pointerDrag.GetComponent<CardView>();

        if (draggedUnit != null && draggedUnit.canMove)
        {
            draggedUnit.MoveToSlot(transform);
            if (myImage != null) myImage.color = defaultColor; 
            return;
        }

        if (draggedCard != null && draggedCard.cardData != null)
        {
            // ★重要：ターゲットが必要なユニットの場合
            if (draggedCard.cardData.type == CardType.UNIT && GameManager.instance.HasSelectTargetAbility(draggedCard.cardData))
            {
                // ドロップ処理を中断し、GameManager側でターゲット選択モードを開始する
                // 召喚予定地（このスロット）を渡す
                GameManager.instance.StartUnitTargeting(draggedCard, this.transform);
                
                if (myImage != null) myImage.color = defaultColor;
                return;
            }

            // 通常召喚（ターゲット不要）
            if (draggedCard.cardData.type == CardType.UNIT)
            {
                // Online Logic Check
                bool isOnline = NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null && NetworkConnectionManager.instance.Runner.IsRunning;
                
                if (isOnline)
                {
                    // Host Authority: Send RPC
                    var gameState = FindObjectOfType<GameStateController>();
                    if (gameState != null)
                    {
                        // Validate Mana (Local Predication)
                        if (GameManager.instance.currentMana >= draggedCard.cardData.cost)
                        {
                    // Send Request
                    gameState.RPC_RequestPlayUnit(draggedCard.cardData.id, transform.GetSiblingIndex());
                    
                    // Local Cleanup (Visuals/State)
                    // ★FIX: Host (StateAuthority) should NOT do local cleanup here, 
                    // because generic logic in ProcessOnlinePlayUnit will do it.
                    // Doing it here causes Double Mana Consumption for Host.
                    // Guest (Proxy) needs Prediction, so keep it.
                    bool isHost = (NetworkConnectionManager.instance.Runner.IsServer || NetworkConnectionManager.instance.Runner.IsSharedModeMasterClient);
                    if (!isHost)
                    {
                        GameManager.instance.TryUseMana(draggedCard.cardData.cost);
                        GameManager.instance.hand.Remove(draggedCard.cardData);
                        Destroy(draggedCard.gameObject);
                    }
                    else
                    {
                        // Host: Just return. ProcessOnlinePlayUnit (RPC) will execute and handle Logic + Visuals.
                        // But we might want to hide the dragged card immediately to prevent dragability?
                        // Usually RPC is fast enough. Or we can just set alpha to 0?
                        // For now, trust RPC.
                    }
                    // Do NOT spawn unit locally. Host will spawn NetworkObject.
                    return;
                }
            } // End if (gameState != null)
        } // End if (isOnline)
                
                // Offline Logic
                if (GameManager.instance.TryUseMana(draggedCard.cardData.cost))
                {
                    GameManager.instance.PlaySE(GameManager.instance.seSummon);
                    
                    // ★追加：手札データから確実に削除
                    if (GameManager.instance.hand.Contains(draggedCard.cardData))
                    {
                        GameManager.instance.hand.Remove(draggedCard.cardData);
                    }

                    GameObject prefab = GameManager.instance.playerUnitPrefab;
                    if(prefab == null) prefab = GameManager.instance.unitPrefabForEnemy;

                    GameObject newUnit = GameManager.instance.SpawnUnit(prefab, transform);
                    UnitMover mover = newUnit.GetComponent<UnitMover>();
                    if (mover != null)
                    {
                        mover.Initialize(draggedCard.cardData, true);
                        // ★追加：スロット座標を設定（Mirroring用）
                        SlotInfo info = transform.GetComponent<SlotInfo>();
                        if (info != null)
                        {
                            // オフライン時はNetworkObjectが無効なのでアクセスしない
                            if (mover.Object != null && mover.Object.IsValid)
                            {
                                mover.NetworkedSlotX = info.x;
                                mover.NetworkedSlotY = info.y;
                            }
                        } 
                    }
                    
                    if (newUnit.GetComponent<UnitView>() != null) newUnit.GetComponent<UnitView>().SetUnit(draggedCard.cardData);
                    AbilityManager.instance.ProcessAbilities(draggedCard.cardData, EffectTrigger.ON_SUMMON, mover, null);
                    
                    newUnit.GetComponent<UnitMover>().PlaySummonAnimation();

                    if (GameManager.instance != null) GameManager.instance.BroadcastLog($" {draggedCard.cardData.cardName} を召喚した", true, draggedCard.cardData);

                    Destroy(draggedCard.gameObject);
                }
            }
        }
        if (myImage != null) myImage.color = defaultColor;
    }
}