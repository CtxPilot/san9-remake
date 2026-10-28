using San9.Content.Interface;
using San9.Core.Domain;

namespace San9.Content.Tests;

public sealed class BattleTacticMessageTests
{
    [Fact]
    public void MissingSecondaryMessagesCannotPassAsAnOptionalPair()
    {
        var messages = new[] { "announcement", "action", "hit", "reduced", "failed", "failed-report", "reduced-report" }
            .ToDictionary(role => role, role => new DecisionMessageDefinition(role, []));
        var tactic = new TacticPresentationDefinition(messages, null, null);
        tactic.ValidateMessages(TacticEffect.Taunt);
        Assert.Throws<InvalidDataException>(() => tactic.ValidateMessages(TacticEffect.Defection));
        messages.Add("secondary-countered", new("countered", []));
        Assert.Throws<InvalidDataException>(() => tactic.ValidateMessages(TacticEffect.Defection));
        messages.Add("secondary-defensive", new("defensive", []));
        tactic.ValidateMessages(TacticEffect.Defection);
        Assert.Throws<InvalidDataException>(() => tactic.ValidateMessages(TacticEffect.Illusion));
        messages.Add("hit-report", new("damage", []));
        tactic.ValidateMessages(TacticEffect.Illusion);
    }
}
