using Godot;
using San9.Application.Authoring;
using San9.Content.Images;
using San9.Content.Packages;

namespace San9.Client.Godot;

public partial class PortraitEditor
{
    private void SelectView(PortraitView view)
    {
        FinishName(); ApplyCoordinates(); _canvas.CancelGesture(); CancelAnalysis();
        if (!_faceView.ButtonPressed && !_bustView.ButtonPressed) SyncView(view);
        _canvas.MoveBoth = _faceView.ButtonPressed && _bustView.ButtonPressed;
        _canvas.EditingView = _canvas.MoveBoth ? view : _faceView.ButtonPressed ? PortraitView.Face : PortraitView.Bust;
        ShowCurrent();
    }
    private void SyncView(PortraitView view)
    {
        _faceView.SetPressedNoSignal(view == PortraitView.Face);
        _bustView.SetPressedNoSignal(view == PortraitView.Bust);
    }

    private void UpdateCoordinates(PortraitDefinition portrait)
    {
        _refreshing = true;
        var anchor = portrait.Anchor(_canvas.EditingView);
        var (width, height) = PortraitGeometry.Size(_canvas.EditingView);
        // Existing fractional metadata is displayed at its actual rounded crop origin without rewriting it.
        _x.MaxValue = Math.Ceiling(portrait.Image.Width * (1 - width));
        _y.MaxValue = Math.Ceiling(portrait.Image.Height * (1 - height));
        _x.Value = Math.Round(anchor.X * portrait.Image.Width); _y.Value = Math.Round(anchor.Y * portrait.Image.Height);
        _x.TooltipText = $"{(_canvas.EditingView == PortraitView.Face ? "面部" : "半身")}框母图横坐标；归一化 {anchor.X:F6}";
        _y.TooltipText = $"{(_canvas.EditingView == PortraitView.Face ? "面部" : "半身")}框母图纵坐标；归一化 {anchor.Y:F6}";
        _refreshing = false; _xDirty = _yDirty = false;
    }
    private void CoordinateChanged()
    {
        if (_refreshing || _canvas.Portrait is not { } portrait) return;
        CancelAnalysis();
        var anchor = new PortraitAnchor(_x.Value / portrait.Image.Width, _y.Value / portrait.Image.Height)
            .Clamp(_canvas.EditingView).SnapToPixels(_canvas.EditingView, portrait.Image.Width, portrait.Image.Height);
        AnchorChanged(_canvas.MoveBoth ? portrait.MoveTogether(_canvas.EditingView, anchor)
            : portrait.WithAnchor(_canvas.EditingView, anchor), true);
    }
    private void AnchorChanged(PortraitDefinition next, bool commit)
    {
        if (_workspace is null || _current is null) return;
        _canvas.Portrait = _previews.Portrait = next; UpdateCoordinates(next);
        _canvas.QueueRedraw(); _previews.QueueRedraw();
        if (commit)
        {
            CancelAnalysis(); _workspace.SetAnchors(_current, next.BustAnchor, next.FaceAnchor); RefreshList(); UpdateHistory();
        }
    }
    private void CancelAnalysis()
    {
        _analysisVersion++; _analysisCancellation?.Cancel(); _analysisCancellation = null;
        if (_framingHint is not null) FramingHint("");
        _candidateKey = null;
        if (_canvas is not null) { _canvas.Candidates = []; _canvas.QueueRedraw(); }
        if (_automatic is not null) { _automatic.Text = "自动面部取景"; _automatic.Disabled = _workspace is null || _current is null; }
    }
    private void EstimateBust()
    {
        FinishName();
        if (_workspace is null || _current is null) return;
        ApplyCoordinates(); _canvas.CancelGesture(); CancelAnalysis();
        var face = _workspace.Portrait(_current).FaceAnchor;
        _workspace.SetAnchor(_current, PortraitView.Bust, PortraitGeometry.BustFromFace(face));
        RefreshList(); ShowCurrent();
    }
    private async Task DetectFace()
    {
        FinishName();
        if (_workspace is null || _current is null) return;
        ApplyCoordinates(); _canvas.CancelGesture(); CancelAnalysis();
        string key = _current;
        var (portrait, path, pending) = ImageSource(key);
        int version = _analysisVersion;
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _analysisCancellation = cancellation;
        _automatic.Disabled = true; _automatic.Text = "正在识别…";
        try
        {
            var faces = await _detector.DetectAsync(() => ReadAnalysisImage(portrait, path, pending), cancellation.Token);
            if (cancellation.IsCancellationRequested || version != _analysisVersion || _current != key) return;
            if (faces.Length == 0) FramingHint("未识别人脸，请手动取景");
            else if (faces.Length == 1) ApplyFace(faces[0]);
            else
            {
                _candidateKey = key; _canvas.Candidates = faces; _canvas.QueueRedraw();
                FramingHint("请选择绿色人脸框");
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        { if (!cancellation.IsCancellationRequested && version == _analysisVersion) ShowError("自动取景失败：" + error.Message); }
        finally
        {
            if (ReferenceEquals(_analysisCancellation, cancellation)) _analysisCancellation = null;
            cancellation.Dispose();
            if (!_lifetime.IsCancellationRequested && version == _analysisVersion)
            { _automatic.Text = "自动面部取景"; _automatic.Disabled = false; }
        }
    }
    private static San9.Content.Images.PngImage ReadAnalysisImage(PortraitDefinition portrait, string path, byte[]? pending)
    {
        using var image = new Image();
        var error = pending is null ? image.Load(path) : image.LoadPngFromBuffer(pending);
        if (error != Error.Ok || image.GetWidth() != portrait.Image.Width || image.GetHeight() != portrait.Image.Height)
            throw new InvalidDataException("头像读取失败或尺寸与资料不符。");
        image.Convert(Image.Format.Rgb8);
        if (image.GetWidth() != 240) image.Resize(240, 240, Image.Interpolation.Lanczos);
        return new(image.GetWidth(), image.GetHeight(), 3, image.GetData());
    }
    private void ApplyFace(PortraitFaceCandidate face)
    {
        if (_workspace is null || _current is null) return;
        var suggestion = AutomaticPortraitFraming.Suggest(face);
        CancelAnalysis(); _workspace.ApplyAutomaticFace(_current, suggestion.FaceAnchor);
        _canvas.EditingView = PortraitView.Face; if (!_canvas.MoveBoth) SyncView(PortraitView.Face);
        RefreshList(); ShowCurrent();
        if (suggestion.IsLimited) FramingHint("取景已到边界，请手动微调");
    }
    private void ApplyCoordinates()
    {
        string yText = _y.GetLineEdit().Text;
        bool yDirty = _yDirty;
        if (_xDirty) _x.Apply();
        if (yDirty) { _y.GetLineEdit().Text = yText; _y.Apply(); }
        _xDirty = _yDirty = false;
    }
}
