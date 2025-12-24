using UnityEngine;
using UnityEngine.EventSystems;

public class FloatingIsland : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("設定")]
    public float floatSpeed = 1.0f; // 浮遊速度
    public float floatAmount = 5.0f; // 浮遊幅
    public float hoverRiseAmount = 10.0f; // ホバー時の上昇量

    private Vector3 initialPos;
    private float timeOffset;
    private bool isHovering = false;

    void Start()
    {
        initialPos = transform.localPosition;
        timeOffset = Random.Range(0f, 10f); // 個体差
    }

    void Update()
    {
        // 常にプカプカさせる（建築済み判定を削除）
        float newY = initialPos.y + Mathf.Sin((Time.time + timeOffset) * floatSpeed) * floatAmount;
        
        // ホバー時はさらに高く
        if (isHovering) newY += hoverRiseAmount;

        transform.localPosition = new Vector3(initialPos.x, newY, initialPos.z);
    }

    public void OnPointerEnter(PointerEventData eventData) => isHovering = true;
    public void OnPointerExit(PointerEventData eventData) => isHovering = false;
}