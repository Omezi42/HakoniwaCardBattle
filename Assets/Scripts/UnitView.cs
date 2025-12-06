using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UnitView : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI healthText;
    
    [Header("状態アイコン")]
    public GameObject tauntIcon;   // 守護（盾）アイコン
    public GameObject stealthIcon; // 潜伏（隠密）アイコン

    public void RefreshStatusIcons(bool hasTaunt, bool hasStealth)
    {
        if (tauntIcon != null) tauntIcon.SetActive(hasTaunt);
        if (stealthIcon != null) stealthIcon.SetActive(hasStealth);
    }

    // UnitView.cs

    public void SetUnit(CardData data)
    {
        // ���ǉ��F�摜���ݒ肳��Ă���Ε\������I
        if (data.cardIcon != null)
        {
            iconImage.sprite = data.cardIcon;
            iconImage.color = Color.white; // ����������������F���t���Ă�ꍇ������̂Ŕ�(���F)��
        }
        else
        {
            // �摜���Ȃ��ꍇ�͂Ƃ肠���������n�ɂ��Ă���
            // (iconImage.sprite = null ���Ə������Ⴄ�̂ŁASprite����Ȃ�F�����ς���Ȃǂ��D�݂�)
        }

        attackText.text = data.attack.ToString();
        healthText.text = data.health.ToString();
    }
    public void RefreshDisplay()
    {
        // 同じオブジェクトについているUnitMover（ステータス管理）を取得
        var mover = GetComponent<UnitMover>();
        if (mover != null)
        {
            if (attackText != null) attackText.text = mover.attackPower.ToString();
            if (healthText != null) healthText.text = mover.health.ToString();
        }
    }
}