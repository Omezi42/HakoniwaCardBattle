using NUnit.Framework;


namespace Tests
{
    public class BattleCalculatorTest
    {
        // 攻撃力が防御力より高い場合のダメージ計算テスト
        [Test]
        public void CalculateDamage_AttackHigherThanDefense_ReturnsDifference()
        {
            // Arrange
            int attack = 5;
            int defense = 2;


            // Act
            int result = BattleCalculator.CalculateDamage(attack, defense);

            // Assert
            Assert.AreEqual(3, result);
        }

        // 攻撃力が防御力以下の場合のテスト（0になるべき）
        [Test]
        public void CalculateDamage_AttackLowerThanDefense_ReturnsZero()
        {
            // Arrange
            int attack = 2;
            int defense = 5;

            // Act
            int result = BattleCalculator.CalculateDamage(attack, defense);

            // Assert
            Assert.AreEqual(0, result);
        }

        // マナ消費のテスト（正常系）
        [Test]
        public void ConsumeMana_SufficientMana_ReducesMana()
        {
            // Arrange
            int current = 10;
            int cost = 3;

            // Act
            int result = BattleCalculator.ConsumeMana(current, cost);

            // Assert
            Assert.AreEqual(7, result);
        }

        // マナ不足のテスト（異常系）
        [Test]
        public void ConsumeMana_InsufficientMana_ReturnsMinusOne()
        {
            // Arrange
            int current = 2;
            int cost = 3;

            // Act
            int result = BattleCalculator.ConsumeMana(current, cost);

            // Assert
            Assert.AreEqual(-1, result);
        }
    }
}
