using San9.Application.Authoring;
using San9.Content.Images;

namespace San9.Application.Tests;

public sealed class PortraitEditorWorkspaceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "san9-portrait-editor-" + Guid.NewGuid().ToString("N"));
    private static readonly PortraitDefinition Base = new(1, new("portrait/1", "base.png", 512, 512, 3), new(38d / 240, 0), new(70d / 240, 30d / 240));
    private static PngImage Image => new(32, 32, 3, Enumerable.Repeat((byte)80, 32 * 32 * 3).ToArray());

    [Fact]
    public async Task BaseAndAuthoredNameCollisionIsAmbiguousAndOverwriteOffStillAdds()
    {
        using var workspace = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        string added = new PortraitImportBatch(workspace, false).Import("1", Image);
        Assert.True(workspace.IsAuthored(added));
        Assert.Equal(Base, workspace.Catalog.Portraits.Single(p => p.Image.Id == Base.Image.Id));
        var before = workspace.Pack;
        var batch = new PortraitImportBatch(workspace, true);
        Assert.Throws<InvalidOperationException>(() => batch.Import("1", Image));
        Assert.Equal(before, workspace.Pack);
    }

    [Fact]
    public async Task SameNameOverwriteKeepsIdentityAndAlsoIndexesEarlierFilesInTheBatch()
    {
        using var workspace = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        string existing = workspace.Add("Hero", Image); workspace.Save();
        var batch = new PortraitImportBatch(workspace, true);
        string key = batch.Import(" hero ", new(64, 64, 3, new byte[64 * 64 * 3]));
        Assert.Equal(existing, key); Assert.Single(workspace.Pack.Portraits);
        Assert.Equal("Hero", workspace.Name(key));
        Assert.Equal(64, workspace.Catalog.Portraits.Single(p => p.Image.Id == key).Image.Width);
        workspace.Undo(); Assert.False(workspace.HasChanges); workspace.Redo();
        string added = batch.Import("New", Image);
        Assert.Equal(added, batch.Import("NEW", new(64, 64, 3, new byte[64 * 64 * 3])));
        Assert.Equal(2, workspace.Pack.Portraits.Length);
        workspace.Save();
        using var reopened = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        Assert.Equal(64, reopened.Catalog.Portraits.Single(p => p.Image.Id == existing).Image.Width);
    }

    [Fact]
    public async Task SameNameImportDefaultsToNewAndAmbiguousOverwriteLeavesExistingImagesUntouched()
    {
        using var workspace = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        string first = workspace.Add("同名", Image);
        string second = new PortraitImportBatch(workspace, false).Import("同名", Image);
        Assert.NotEqual(first, second);
        var before = workspace.Pack;
        var batch = new PortraitImportBatch(workspace, true);
        Assert.Throws<InvalidOperationException>(() => batch.Import("同名", Image));
        Assert.Equal(before, workspace.Pack);
        string next = batch.Import("其他", Image);
        Assert.True(workspace.IsAuthored(next));
        string numeric = batch.Import("1", Image);
        Assert.Equal(Base.Image.Id, numeric);
        Assert.False(workspace.IsAuthored(numeric));
        Assert.Equal(32, workspace.Catalog.Portraits.Single(p => p.Image.Id == numeric).Image.Width);
        Assert.Equal(512, Base.Image.Width);
        workspace.Save();
        using var reopened = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        Assert.Equal(32, reopened.Catalog.Portraits.Single(p => p.Image.Id == numeric).Image.Width);
        reopened.RestoreOriginal(numeric);
        Assert.Equal(Base, reopened.Catalog.Portraits.Single(p => p.Image.Id == numeric));
    }

    [Fact]
    public async Task BaseReplacementRetainsIdentityAndFramingEditsPreserveReplacementUntilRestoreOriginal()
    {
        using var workspace = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        workspace.Replace("portrait/1", Image);
        var replacement = Assert.Single(workspace.Catalog.Portraits);
        Assert.Equal(Base.Id, replacement.Id); Assert.Equal(Base.Image.Id, replacement.Image.Id);
        Assert.Equal(32, replacement.Image.Width);
        Assert.NotNull(workspace.PendingImage("portrait/1"));
        workspace.Undo(); Assert.Equal(Base, Assert.Single(workspace.Catalog.Portraits));
        workspace.Redo();
        workspace.SetAnchor("portrait/1", PortraitView.Face, new(.25, .25));
        Assert.Equal(replacement.Image, Assert.Single(workspace.Catalog.Portraits).Image);
        workspace.Save();
        using var reopened = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        Assert.Equal(replacement.Image, Assert.Single(reopened.Catalog.Portraits).Image);
        reopened.RestoreOriginal("portrait/1");
        Assert.Equal(Base, Assert.Single(reopened.Catalog.Portraits));
        reopened.Undo(); Assert.Equal(replacement.Image, Assert.Single(reopened.Catalog.Portraits).Image);
        reopened.Redo(); reopened.Save();
        Assert.False(File.Exists(Path.Combine(_root, "base.png")));
    }

    [Fact]
    public async Task AuthoredReplacementPreservesNameAndCanRestoreOpeningImageAfterSaving()
    {
        using (var initial = await PortraitEditorWorkspace.OpenAsync(_root, [Base])) { initial.Add("自建", Image); initial.Save(); }
        using var workspace = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        string key = workspace.Pack.Key(Assert.Single(workspace.Pack.Portraits).Id);
        var original = workspace.Catalog.Portraits.Single(p => p.Image.Id == key);
        string originalPath = workspace.Catalog.ImageSources[original.Image.Path];
        workspace.Replace(key, new(64, 64, 3, new byte[64 * 64 * 3]));
        Assert.Equal("自建", workspace.Name(key));
        workspace.Save();
        Assert.True(File.Exists(originalPath));
        workspace.Restore(key, false);
        Assert.Equal(original, workspace.Catalog.Portraits.Single(p => p.Image.Id == key));
        workspace.Save(); workspace.Dispose();
        Assert.Single(Directory.GetFiles(Path.Combine(_root, "images")));
    }

    [Fact]
    public async Task NewlyAddedOpeningImageRemainsRestorableWhenOnlyReplacementWasPublished()
    {
        using var workspace = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        string key = workspace.Add("自建", Image);
        var original = workspace.Catalog.Portraits.Single(p => p.Image.Id == key);
        workspace.Replace(key, new(64, 64, 3, new byte[64 * 64 * 3])); workspace.Save();
        workspace.Restore(key, false);
        Assert.Equal(original, workspace.Catalog.Portraits.Single(p => p.Image.Id == key));
        Assert.NotNull(workspace.PendingImage(key)); workspace.Save();
        Assert.Equal(32, PngImage.Decode(File.ReadAllBytes(workspace.Catalog.ImageSources[original.Image.Path])).Width);
    }

    [Fact]
    public async Task DeletionGuardsUsageAndBasePortraitsAndRechecksReferencesBeforePublication()
    {
        using var workspace = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        string key = workspace.Add("自建", Image); workspace.Save();
        string path = workspace.Catalog.ImageSources[workspace.Catalog.Portraits.Single(p => p.Image.Id == key).Image.Path];
        Assert.Throws<InvalidOperationException>(() => workspace.Delete("portrait/1", []));
        Assert.Throws<InvalidOperationException>(() => workspace.Delete(key, ["程大"]));
        workspace.Delete(key, []); Assert.Contains(key, workspace.RemovedKeys);
        workspace.Undo(); Assert.True(workspace.IsAuthored(key)); workspace.Redo();
        Assert.Throws<InvalidOperationException>(workspace.Save);
        Assert.Throws<InvalidOperationException>(() => workspace.Save(new HashSet<string> { key }));
        Assert.True(File.Exists(path));
        workspace.Save(new HashSet<string>()); workspace.Dispose();
        Assert.False(File.Exists(path));
        using var reopened = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        Assert.Equal(Base, Assert.Single(reopened.Catalog.Portraits));
    }

    [Fact]
    public async Task FailedReplacementPublicationKeepsOldImageAndLeavesDraftRetryable()
    {
        using var workspace = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        string key = workspace.Add("自建", Image); workspace.Save();
        string original = Assert.Single(workspace.Pack.Portraits).Image.Path;
        workspace.Replace(key, new(64, 64, 3, new byte[64 * 64 * 3]));
        string replacement = Assert.Single(workspace.Pack.Portraits).Image.Path;
        File.AppendAllText(workspace.ManifestPath, " ");
        Assert.Throws<IOException>(workspace.Save);
        Assert.True(File.Exists(Path.Combine(_root, original)));
        Assert.False(File.Exists(Path.Combine(_root, replacement)));
        Assert.NotNull(workspace.PendingImage(key));
    }

    [Fact]
    public async Task LinkedFramingIsOneUndoableEditAndPersistsBothAnchors()
    {
        var workspace = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        var moved = Base.MoveTogether(PortraitView.Face, new(.4, .2));
        workspace.SetAnchors("portrait/1", moved.BustAnchor, moved.FaceAnchor);
        Assert.Equal(moved, workspace.Catalog.Portraits[0]);
        workspace.Undo(); Assert.False(workspace.HasChanges); Assert.False(workspace.CanUndo);
        workspace.Redo(); Assert.Equal(moved, workspace.Catalog.Portraits[0]);
        workspace.Save();
        var reopened = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        Assert.Equal(moved, reopened.Catalog.Portraits[0]);
    }

    [Fact]
    public async Task AddedImageAndOverridePublishTogetherAndReloadWithTheSameReference()
    {
        var workspace = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        string key = workspace.Add("自建头像", Image);
        workspace.SetAnchor(key, PortraitView.Bust, new(.25, .125));
        workspace.SetAnchor("portrait/1", PortraitView.Bust, new(.2, .1));
        Assert.False(Directory.Exists(_root));
        Assert.Equal(2, workspace.ModifiedKeys.Count);
        workspace.Save();
        Assert.False(workspace.HasChanges);
        Assert.False(workspace.CanUndo);
        Assert.Null(workspace.PendingImage(key));
        var reopened = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        var added = reopened.Catalog.Portraits.Single(p => p.Image.Id == key);
        Assert.Equal((.25, .125), (added.BustAnchor.X, added.BustAnchor.Y));
        Assert.Equal("自建头像", reopened.Name(key));
        Assert.True(File.Exists(reopened.Catalog.ImageSources[added.Image.Path]));
        Assert.Equal(102d / 512, reopened.Catalog.Portraits.Single(p => p.Image.Id == "portrait/1").BustAnchor.X);
        Assert.Equal(38d / 240, Base.BustAnchor.X);
        Assert.False(File.Exists(Path.Combine(_root, "base.png")));
    }

    [Fact]
    public async Task HistoryAndRestoreKeepDraftsSeparateAndPreserveFractionalBaseAnchors()
    {
        var workspace = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        workspace.SetAnchor("portrait/1", PortraitView.Bust, new(.2, .1));
        workspace.Undo(); Assert.False(workspace.HasChanges);
        workspace.Redo(); Assert.True(workspace.HasChanges);
        workspace.Save();
        workspace.Restore("portrait/1", false);
        Assert.Equal(Base.BustAnchor.X, workspace.Catalog.Portraits[0].BustAnchor.X);
        Assert.Empty(workspace.Pack.Overrides);
        workspace.Save();
        string key = workspace.Add("未保存", Image);
        workspace.Undo(); Assert.DoesNotContain(workspace.Catalog.Portraits, p => p.Image.Id == key);
        workspace.Redo(); Assert.NotNull(workspace.PendingImage(key));
    }

    [Fact]
    public async Task CustomNameCanBeChangedAndRestoredWithoutChangingPortraitReferencesOrFiles()
    {
        var workspace = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        string key = workspace.Add("亮哥", Image);
        var original = workspace.Catalog.Portraits.Single(p => p.Image.Id == key);
        Assert.Equal("亮哥", original.DisplayName);
        workspace.Rename(key, "  诸葛亮·青年  ");
        var renamed = workspace.Catalog.Portraits.Single(p => p.Image.Id == key);
        Assert.Equal("诸葛亮·青年", renamed.DisplayName);
        Assert.Equal(original.Image, renamed.Image);
        Assert.Equal(original.BustAnchor, renamed.BustAnchor);
        Assert.Equal(original.FaceAnchor, renamed.FaceAnchor);
        workspace.Undo(); Assert.Equal("亮哥", workspace.Name(key));
        workspace.Redo(); Assert.Equal("诸葛亮·青年", workspace.Name(key));
        Assert.Throws<InvalidDataException>(() => workspace.Rename(key, "  "));
        Assert.Throws<InvalidOperationException>(() => workspace.Rename("portrait/1", "改原版"));
        workspace.Save();
        var reopened = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        Assert.Equal("诸葛亮·青年", reopened.Catalog.Portraits.Single(p => p.Image.Id == key).DisplayName);
        Assert.Equal(original.Image, reopened.Catalog.Portraits.Single(p => p.Image.Id == key).Image);
        workspace.Restore(key, false);
        Assert.Equal("亮哥", workspace.Name(key));
    }

    [Fact]
    public async Task AutomaticFaceAndEstimatedBustAreOneUndoableEditAndCanThenBeMovedIndependently()
    {
        var workspace = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        var face = new PortraitAnchor(.45, .3);
        workspace.ApplyAutomaticFace("portrait/1", face);
        var automatic = workspace.Catalog.Portraits[0];
        Assert.Equal(new PortraitAnchor(230d / 512, 154d / 512), automatic.FaceAnchor);
        Assert.Equal(new PortraitAnchor(162d / 512, 90d / 512), automatic.BustAnchor);
        workspace.Undo(); Assert.False(workspace.HasChanges);
        workspace.Redo(); Assert.Equal(automatic, workspace.Catalog.Portraits[0]);
        workspace.SetAnchor("portrait/1", PortraitView.Face, new(.1, .1));
        Assert.Equal(automatic.BustAnchor, workspace.Catalog.Portraits[0].BustAnchor);
        workspace.SetAnchor("portrait/1", PortraitView.Bust, new(0, 0));
        Assert.Equal(new PortraitAnchor(51d / 512, 51d / 512), workspace.Catalog.Portraits[0].FaceAnchor);
        workspace.Save();
        var restored = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        Assert.Equal(workspace.Catalog.Portraits[0], restored.Catalog.Portraits[0]);
    }

    [Fact]
    public async Task AutomaticPlacementSnapsToMotherPixelsBeforeDerivingBustAndSurvivesSave()
    {
        var workspace = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        string key = workspace.Add("高清头像", new(600, 600, 3, new byte[600 * 600 * 3]));
        workspace.ApplyAutomaticFace(key, new(76d / 240, 39d / 240));
        var portrait = workspace.Catalog.Portraits.Single(p => p.Image.Id == key);
        Assert.Equal(new PortraitAnchor(190d / 600, 98d / 600), portrait.FaceAnchor);
        Assert.Equal(new PortraitAnchor(110d / 600, 23d / 600), portrait.BustAnchor);
        workspace.Save();
        var reopened = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        Assert.Equal(portrait, reopened.Catalog.Portraits.Single(p => p.Image.Id == key));
    }

    [Fact]
    public async Task ExternalManifestChangeRejectsSaveWithoutWritingImagesOrLosingDraft()
    {
        var first = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        var second = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        first.SetAnchor("portrait/1", PortraitView.Bust, new(.2, .1)); first.Save();
        byte[] saved = File.ReadAllBytes(first.ManifestPath);
        string key = second.Add("另一个草稿", Image);
        Assert.Throws<IOException>(second.Save);
        Assert.True(second.HasChanges); Assert.NotNull(second.PendingImage(key));
        Assert.Equal(saved, File.ReadAllBytes(first.ManifestPath));
        Assert.False(Directory.Exists(Path.Combine(_root, "images")));
    }

    [Fact]
    public async Task FailedManifestPublicationRemovesOnlyNewImagesAndRetainsDraftForRetry()
    {
        var workspace = await PortraitEditorWorkspace.OpenAsync(_root, [Base]);
        string key = workspace.Add("草稿", Image);
        Directory.CreateDirectory(workspace.ManifestPath);
        var error = Record.Exception(workspace.Save);
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.True(workspace.HasChanges); Assert.NotNull(workspace.PendingImage(key));
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "images")));
        Directory.Delete(workspace.ManifestPath);
        workspace.Save();
        Assert.True(File.Exists(workspace.ManifestPath));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
