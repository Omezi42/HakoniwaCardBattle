using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DeckFigure : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UIパーツ")]
    public Image iconImage;

    [Tooltip("ユニットの時だけ表示する台座や影のオブジェクト")]
    public GameObject figureBase; // ★追加：台座オブジェクト（ビルド時は隠す）

    private CardData myData;
    private Transform originalParent;
    private CanvasGroup canvasGroup;
    private Vector3 startPos;
    private int originalSiblingIndex;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Setup(CardData data, int count)
    {
        myData = data;
        if (data.cardIcon != null)
        {
            iconImage.sprite = data.cardIcon;
            
            // アイコンの縦横比を維持（アイテム画像が歪まないように）
            iconImage.preserveAspect = true; 
        }

        // ★追加：タイプによる見た目の切り替え
        if (figureBase != null)
        {
            if (data.type == CardType.BUILD)
            {
                // ビルドなら台座を隠してアイコンだけにする
                figureBase.SetActive(false);
                
                // ビルドアイコンは少し小さめにするなど、必要ならサイズ調整
                // transform.localScale = Vector3.one * 0.8f; 
            }
            else
            {
                // ユニットなら台座を表示（フィギュア風）
                figureBase.SetActive(true);
                // transform.localScale = Vector3.one;
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (SimpleCardModal.instance != null) SimpleCardModal.instance.Open(myData);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (DeckEditManager.instance != null) DeckEditManager.instance.AddCardToDeck(myData);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        startPos = transform.position;
        originalSiblingIndex = transform.GetSiblingIndex();

        transform.SetParent(transform.root); 
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        // 画面下部40%より下なら削除（カード一覧エリアに戻したとみなす）
        if (Input.mousePosition.y < Screen.height * 0.4f)
        {
            if (DeckEditManager.instance != null)
            {
                DeckEditManager.instance.RemoveCardFromDeck(myData);
            }
            Destroy(gameObject); 
        }
        else
        {
            // キャンセルして元の場所に戻る
            transform.SetParent(originalParent);
            transform.SetSiblingIndex(originalSiblingIndex);
            transform.position = startPos;
        }
    }
}