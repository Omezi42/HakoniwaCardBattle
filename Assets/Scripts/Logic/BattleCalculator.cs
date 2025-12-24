using UnityEngine;

// Global namespace for simplicity
public static class BattleCalculator
{
    /// <summary>
    /// 単純なダメージ計算を行います。
    /// </summary>
    /// <param name="attack">攻撃力</param>
    /// <param name="defense">防御力（アーマー要素などがあれば）</param>
    /// <returns>最終的なダメージ値（0未満にはなりません）</returns>
    public static int CalculateDamage(int attack, int defense = 0)
    {
        int damage = attack - defense;
        return Mathf.Max(0, damage);
    }

    /// <summary>
    /// マナ消費後の値を計算します。
    /// </summary>
    /// <param name="currentMana">現在のマナ</param>
    /// <param name="cost">コスト</param>
    /// <returns>消費後のマナ。足りない場合は -1 を返します。</returns>
    public static int ConsumeMana(int currentMana, int cost)
    {
        if (currentMana < cost) return -1;
        return currentMana - cost;
    }
}
