using San9.Core.Domain;

namespace San9.Core.Tests;

public sealed class BattleCaptureRulesTests
{
    [Theory]
    [InlineData(0, BattleRank.Front, false, 58)]
    [InlineData(0, BattleRank.Middle, false, 39)]
    [InlineData(0, BattleRank.Rear, false, 19)]
    [InlineData(80, BattleRank.Front, true, 69)]
    [InlineData(80, BattleRank.Middle, true, 46)]
    [InlineData(80, BattleRank.Rear, true, 23)]
    [InlineData(100, BattleRank.Middle, false, 6)]
    [InlineData(105, BattleRank.Middle, false, 1)]
    [InlineData(106, BattleRank.Middle, false, 1)]
    public void PreservesAbilityTransformAndRankTruncation(int war, BattleRank rank, bool doubleCapture, int expected)
    {
        Assert.Equal(expected, BattleCaptureRules.Chance(war, 1, rank, true, false, doubleCapture, 100));
    }

    [Fact]
    public void MountPreventsProbabilisticCaptureButCannotCreateARetreatRoute()
    {
        Assert.Equal(0, BattleCaptureRules.Chance(0, 5, BattleRank.Front, true, true, true, 100));
        Assert.Equal(100, BattleCaptureRules.Chance(100, 1, BattleRank.Rear, false, true, false, 100));
        Assert.Equal(0, BattleCaptureRules.Chance(0, 0, BattleRank.Front, true, false, true, 100));
        Assert.Equal(0, BattleCaptureRules.Chance(106, 1, BattleRank.Rear, true, false, false, 100));
    }

    [Theory]
    [InlineData(2, 34)]
    [InlineData(3, 46)]
    [InlineData(4, 53)]
    [InlineData(6, 69)]
    [InlineData(7, 74)]
    public void AdditionalCaptorsUseTriangularSegmentsBeforeFormationAndRankModifiers(int captors, int expected)
    {
        Assert.Equal(expected, BattleCaptureRules.Chance(80, captors, BattleRank.Middle, true, false, false, 100));
        Assert.Equal(expected, BattleCaptureRules.Chance(80, captors, BattleRank.Rear, true, false, true, 100));
    }

    [Theory]
    [InlineData(0, false, false, 23)]
    [InlineData(20, false, false, 43)]
    [InlineData(80, false, false, 100)]
    [InlineData(20, false, true, 20)]
    [InlineData(20, true, false, 23)]
    public void NoRetreatBonusAddsPercentagePointsAfterTheRegularModifiers(int bonus, bool hasRetreatCity,
        bool mount, int expected)
    {
        Assert.Equal(expected, BattleCaptureRules.Chance(80, 1, BattleRank.Middle, hasRetreatCity, mount, false, bonus));
    }

    [Fact]
    public void ExtremeInputsRemainBoundedAndHigherWarCannotIncreaseCaptureChance()
    {
        Assert.Equal(100, BattleCaptureRules.Chance(0, int.MaxValue, BattleRank.Front, true, false, true, 100));
        Assert.Equal(100, BattleCaptureRules.Chance(int.MaxValue, int.MaxValue, BattleRank.Front, true, false, true, 100));
        Assert.Equal(21, BattleCaptureRules.Chance(int.MaxValue, 1, BattleRank.Front, false, false, false, 20));
        int previous = 100;
        for (int war = 0; war <= 200; war++)
        {
            int current = BattleCaptureRules.Chance(war, 2, BattleRank.Front, true, false, false, 100);
            Assert.True(current <= previous);
            previous = current;
        }
    }

    [Fact]
    public void InvalidInputsAreRejectedEvenWhenCaptureWouldBeGuaranteed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleCaptureRules.Chance(-1, 1, BattleRank.Middle, false, false, false, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleCaptureRules.Chance(80, -1, BattleRank.Middle, false, false, false, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleCaptureRules.Chance(80, 1, (BattleRank)99, false, false, false, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleCaptureRules.Chance(80, 1, BattleRank.Middle, true, false, false, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleCaptureRules.Chance(80, 1, BattleRank.Middle, true, false, false, 101));
    }
}
