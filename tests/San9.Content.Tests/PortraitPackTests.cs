using San9.Content.Images;

namespace San9.Content.Tests;

public sealed class PortraitPackTests
{
    private static readonly PortraitDefinition Base = new(7, new("portrait/7", "images/7.png", 240, 240, 3), new(.1, 0), new(.3, .125));
    private static AuthoredPortrait Portrait(string id) => new(id, id, new($"images/{id}.png", 512, 512, 3), new(.125, .0625), new(.3, .2));

    [Fact]
    public void BaseImageReplacementKeepsIdentityAndOwnsItsImageSource()
    {
        var replacement = new ImageFile("images/replacement.png", 512, 512, 3);
        var pack = PortraitPack.Empty with { Overrides = [new("portrait/7", new(.1, 0), new(.3, .125)) { Image = replacement }] };
        string root = Path.GetFullPath("user-portraits");
        var catalog = pack.Resolve([Base], root);
        var portrait = Assert.Single(catalog.Portraits);
        Assert.Equal(Base.Id, portrait.Id); Assert.Equal(Base.Image.Id, portrait.Image.Id);
        Assert.Equal(512, portrait.Image.Width);
        Assert.Equal(Path.Combine(root, "images", "replacement.png"), catalog.ImageSources[portrait.Image.Path]);
        Assert.Equal(240, Base.Image.Width);
        foreach (var invalid in new[] { replacement with { Path = "../escape.png" }, replacement with { Height = 500 }, replacement with { Channels = 4 } })
            Assert.Throws<InvalidDataException>(() => (pack with { Overrides = [pack.Overrides[0] with { Image = invalid }] }).Validate());
    }

    [Fact]
    public void CompositionKeepsStableKeysAndResolvesImagesWithinTheirOwningPack()
    {
        string root = Path.GetFullPath("user-portraits");
        var pack = PortraitPack.Empty with { Portraits = [Portrait("first"), Portrait("second")], Overrides = [new("portrait/7", new(.2, .1), new(.35, .15))] };
        var catalog = pack.Resolve([Base], root);
        var authored = catalog.Portraits.Single(p => p.Image.Id == "user/my-portraits/first");
        Assert.Equal(Path.Combine(root, "images", "first.png"), catalog.ImageSources[authored.Image.Path]);
        Assert.Equal((.2, .1), (catalog.Portraits[0].BustAnchor.X, catalog.Portraits[0].BustAnchor.Y));
        Assert.Equal(.1, Base.BustAnchor.X);
        foreach (var view in Enum.GetValues<PortraitView>()) authored.GetView(view).Validate();
        var reordered = (pack with { Portraits = pack.Portraits.Reverse().ToArray() }).Resolve([Base], root);
        Assert.Equal(authored.Image, reordered.Portraits.Single(p => p.Image.Id == authored.Image.Id).Image);
    }

    [Fact]
    public void InvalidPathsIdentitiesAnchorsAndMissingOverrideTargetsAreRejected()
    {
        var entry = Portrait("first");
        foreach (var invalid in new[] {
            PortraitPack.Empty with { Portraits = [entry, entry] },
            PortraitPack.Empty with { Id = "../outside" },
            PortraitPack.Empty with { Portraits = [entry with { Image = entry.Image with { Path = "../outside.png" } }] },
            PortraitPack.Empty with { Portraits = [entry with { BustAnchor = new(.125, .3) }] },
            PortraitPack.Empty with { Overrides = [new("portrait/missing", new(0, 0), new(.2, .1))] },
            PortraitPack.Empty with { Portraits = [entry], Overrides = [new("user/my-portraits/first", new(0, 0), new(.2, .1))] }
        }) Assert.Throws<InvalidDataException>(() => invalid.Resolve([Base], "."));
    }
}
