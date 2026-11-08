using San9.Importer.San9PkTc101.Interface;

namespace San9.Importer.San9PkTc101.Tests;

public sealed class PersonnelPresentationCompilerTests
{
    [Fact]
    public void ArrivalRecordNamesTheArrivingOfficerWhileDialogueAddressesTheRuler()
    {
        var content = PersonnelPresentationCompiler.Compile();
        var record = content.Messages["journey-arrived-record"];
        Assert.Equal("messages.10073", record.Key);
        Assert.Equal("actor", record.Bindings["person.0"]);
        var dialogue = content.Messages["journey-arrived"];
        Assert.Equal("ruler", dialogue.Bindings["person.0"]);
        Assert.Equal("actor", dialogue.Bindings["person.1"]);
    }
}
