using San9.Importer.San9PkTc101.Interface;

namespace San9.Importer.San9PkTc101.Tests;

public sealed class DecisionPresentationCompilerTests
{
    [Fact]
    public void DecisionSlotsBindToTheirDomainMeaning()
    {
        var people = new ushort[] { 671, 670, 666, 667 }.Select(biography => new San9ScenarioPersonRecord(
            0, 6000, 0, 0, 0, 0, 0, biography, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            [], 0, 0, [], 0)).ToArray();
        var decisions = DecisionPresentationCompiler.Compile(people);
        var succession = decisions.Shared["ending-succession-year"];
        Assert.Equal("messages.7628", succession.Key);
        Assert.Equal("ruler", succession.Bindings["person.0"]);
        Assert.Equal("succession-year", succession.Bindings["number.0"]);
        Assert.Equal("messages.6122", decisions.Shared["gift-return-confirmation"].Key);
        Assert.Equal("conflict-force", decisions.Shared["gift-return-confirmation"].Bindings["force.0"]);
        Assert.Equal("messages.6123", decisions.Shared["gift-corps-confirmation"].Key);
        Assert.Equal("conflict-corps-leader", decisions.Shared["gift-corps-confirmation"].Bindings["person.0"]);
        var progress = ExecutionPresentationCompiler.Compile();
        foreach (string role in new[] { "enemy-city", "enemy-place", "reinforce-from", "reinforce-to" })
            Assert.Equal("enemy-force", progress.Context[role].Bindings["force.0"]);
        foreach (var (role, key) in new[] { ("diplomacy-units-returned", "messages.7447"), ("diplomacy-corps-cleared", "messages.7448") })
        {
            Assert.Equal(key, progress.Records[role].Key); Assert.Equal(key, progress.Feedback[role].Key);
            Assert.Equal("force", progress.Records[role].Bindings["force.0"]);
            Assert.Equal("second-force", progress.Records[role].Bindings["force.1"]);
        }
        var volunteers = decisions.Events["volunteers"];

        Assert.Equal("messages.8013", volunteers.Prompt.Key);
        Assert.Equal("soldiers", volunteers.Prompt.Bindings["number.0"]);
        var offer = volunteers.Introduction.Single(m => m.Key == "messages.7925");
        Assert.Equal("soldiers", offer.Bindings["number.0"]);
        var result = volunteers.Results["Accept"].Single(m => m.Key == "messages.7926");
        Assert.Equal("soldiers", result.Bindings["number.0"]);
        Assert.Equal("soldiers", decisions.Events["bandit-mediation"].Results["success"]
            .Single(m => m.Key == "messages.7855").Bindings["number.0"]);
        Assert.Equal("dynasty-years", decisions.Events["emperor-transfer"].Results["decline"][0].Bindings["number.0"]);
        Assert.Equal("amount", decisions.Events["bribe"].Prompt.Bindings["number.0"]);
        var alliance = decisions.Events["alliance"];
        Assert.Equal("force", alliance.Prompt.Bindings["force.0"]);
        Assert.Equal("target-force", alliance.Prompt.Bindings["force.1"]);
        Assert.Equal("target-force", Assert.Single(alliance.Results["decline"]).Bindings["force.0"]);
    }
}
