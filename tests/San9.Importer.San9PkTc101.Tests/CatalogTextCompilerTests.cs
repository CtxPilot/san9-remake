using San9.Content.Scenarios;
using San9.Content.Text;
using San9.Importer.San9PkTc101.Scenarios;

namespace San9.Importer.San9PkTc101.Tests;

public sealed class CatalogTextCompilerTests
{
    [Fact]
    public void RankMessagesReadDefinitionsWithoutChangingReferencesOrTheGenericNoneLabel()
    {
        var nested = new MessageDefinition([new MessageStyled("yellow", [
            new MessageReference("messages.1652", false), new MessageLiteral(" / "),
            new MessageReference("messages.7761", true)])]);
        Dictionary<string, MessageDefinition> messages = new()
        {
            ["dialogue"] = nested, ["messages.1652"] = new([new MessageLiteral("旧名")]),
            ["messages.7761"] = new([new MessageLiteral("旧说明")]),
            ["messages.5217"] = new([new MessageLiteral("无")])
        };
        var scenario = new ScenarioInitialState(ScenarioInitialState.CurrentSchemaVersion, "catalog", 190, 1, 1, null, [], [], [], [])
        { Information = InformationDefinitions.Empty with
        { Ranks = [new(0, "新名", 10000, 0, 0, 1) { Description = "新说明" }] } };
        CatalogTextCompiler.BindMessages(messages, scenario);
        MessageGraph.Validate(messages);
        Assert.Same(nested, messages["dialogue"]);
        Assert.Equal("无", MessageMarkup.Static(messages["messages.5217"].Nodes));
        foreach (string key in new[] { "messages.1652", "messages.7761" })
        {
            var value = Assert.IsType<MessageValue>(Assert.Single(messages[key].Nodes));
            Assert.Equal(key.EndsWith("1652") ? "rank.name" : "rank.description", value.Expression.Binding);
            Assert.Equal(new MessageExpression("entity", "Rank", 0), Assert.Single(value.Expression.Arguments!));
        }
    }
}
