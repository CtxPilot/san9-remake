using San9.Client.Godot.Persistence;
using San9.Content.Authoring;

namespace San9.Client.Godot.Tests;

public sealed class CreatedOfficerStoreTests
{
    [Fact]
    public void StorePersistsOnlyExplicitSavesAndRejectsInvalidReplacementWithoutLosingThePreviousFile()
    {
        string root = Path.Combine(Path.GetTempPath(), "san9-officer-store-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CreatedOfficerStore(Path.Combine(root, "people.json"));
            Assert.Empty(store.Load().Officers);
            var person = new CreatedOfficer(Guid.NewGuid(), "长姓名保存验证");
            var library = CreatedOfficerLibrary.Empty with { Officers = [person] };
            store.Save(library);
            Assert.Equal(person.Name, Assert.Single(store.Load().Officers).Name);
            Assert.Throws<UnauthorizedAccessException>(() => new CreatedOfficerStore(root).Load());
            Assert.Throws<InvalidDataException>(() => store.Save(library with { Officers = [person with { Name = "" }] }));
            Assert.Equal(person.Name, Assert.Single(store.Load().Officers).Name);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
