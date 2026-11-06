using Godot;
using San9.Application.Presentation;
using San9.Content.Interface;
using San9.Content.Text;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Rendering;

namespace San9.Client.Godot;

/// <summary>Samples a prepared plan; drawing has no random choices, publication, sound or gameplay side effects.</summary>
public partial class BattlePresentationView : Control
{
    public TextureService Textures { get; init; } = null!;
    public TextCatalog Texts { get; init; } = null!;
    public Func<BattleObjectContext, Vector2> Project { get; init; } = null!;
    public Action<string>? PlaySound { get; init; }
    public Action<string, Vector2>? PlayMapSound { get; init; }
    public Func<bool> IsPaused { get; init; } = () => false;
    public string ContentRoot { get; init; } = "";
    public IReadOnlyDictionary<string, string> Sounds { get; init; } = null!;
    public Func<bool> SoundEnabled { get; init; } = () => true;
    public bool IsZoomActive => _zoom is not null || _duel is not null;
    public bool IsSceneActive => IsZoomActive || _entering;
    public float MapAudioGain { get; private set; } = 1;
    public float SceneAudioGain { get; private set; } = 1;
    private SceneTransitionOverlay _transition = null!;
    private bool _entering;
    private Vector2 SceneOffset => (GetViewportRect().Size - new Vector2(1024, 768)) / 2;
    private void ViewportChanged()
    {
        Size = GetViewportRect().Size;
        UpdatePlayback(_day);
        QueueRedraw();
        _numberLayer.QueueRedraw();
        _mapNumberLayer.QueueRedraw();
    }
    private BattleNumberLayer _numberLayer = null!;
    private Node2D _mapLayer = null!;
    private BattleNumberLayer _mapNumberLayer = null!;
    private TextureScope? _assets;
    private TextureScope? _flags;
    private readonly List<TextureScope> _portraits = [];
    private TurnDayPresentation? _day;
    private readonly CanvasItemMaterial _additive = new() { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
    private readonly List<Sprite2D> _effects = [];
    private readonly List<Sprite2D> _mapEffectSprites = [];
    private const int FullScreenEffectZIndex = 2;
    private BattleVisualCue? _zoom;
    private BattleVisualCue? _preludeFrame;
    public void RetryResources() { _duelUi?.RetryFailed(); _assets?.RetryFailed(); _flags?.RetryFailed(); _numberAssets?.RetryFailed(); foreach (var portrait in _portraits) portrait.RetryFailed(); }
    public bool ResourcesReady => _duelUi?.IsReady != false && _assets?.IsReady != false && _flags?.IsReady != false && _portraits.All(p => p.IsReady);

    public bool Prepare(TurnDayPresentation? day)
    {
        if (ReferenceEquals(_day, day))
        {
            if (_duelUi?.Error is { } uiError) throw new InvalidDataException("Duel text resources failed.", uiError);
            if (_assets?.Error is { } error) throw new InvalidDataException("Battle presentation resources failed.", error);
            if (_flags?.Error is { } flagError) throw new InvalidDataException("Battle officer flag resources failed.", flagError);
            if (_portraits.Select(p => p.Error).FirstOrDefault(e => e is not null) is { } portraitError)
                throw new InvalidDataException("Battle speech portrait failed.", portraitError);
            return ResourcesReady;
        }
        _day?.EndSceneFastForward();
        ResetSceneAudio();
        _assets?.Dispose(); _assets = null; _day = day;
        _flags?.Dispose(); _flags = null;
        _duel = null; _duelSpeechLayout = null; _duelPortraits.Clear(); _duelText?.Hide();
        _duelPanels?.QueueRedraw(); _duelSpeechFrame?.QueueRedraw();
        _zoom = null; _preludeFrame = null; _entering = false;
        _mapLayer?.Show();
        _publishedNumbers.Clear();
        _publishedMorales.Clear();
        MapAudioGain = SceneAudioGain = 1;
        _transition?.Sample(0);
        foreach (var portrait in _portraits) portrait.Dispose(); _portraits.Clear();
        foreach (var sprite in _effects) sprite.Hide();
        foreach (var sprite in _mapEffectSprites) sprite.Hide();
        if (day?.Battle is not { } plan) { QueueRedraw(); return true; }
        HashSet<string> resources = [];
        HashSet<string> flagResources = [];
        void Sprite(BattleSprite? sprite) { if (sprite is not null) resources.Add(sprite.Asset); }
        void Body(BattleZoomBody body)
        {
            Sprite(body.Idle); Sprite(body.Attack); Sprite(body.Advance); Sprite(body.Ranged); Sprite(body.Wake);
            foreach (var effect in body.Effects.Concat(body.DamageEffects)) Sprite(effect.Sprite);
        }
        PrepareDuels(plan, resources, flagResources);
        foreach (var effect in plan.MapEffects) Sprite(effect.Sprite);
        foreach (var cue in plan.Visuals)
        {
            Sprite(cue.Effect);
            if (cue.Zoom is not { } zoom) continue;
            if (cue.Officers is { } officers)
            {
                Sprite(officers.Definition.Idle);
                foreach (string clip in officers.FlagClips) flagResources.UnionWith(Textures.AnimationClips[clip].Frames);
            }
            resources.UnionWith([zoom.SourcePanel, zoom.TargetPanel]);
            if (zoom.Numbers is { } digits) resources.UnionWith([digits.Small.Asset, digits.Total.Asset]);
            resources.UnionWith(cue.Backdrop.Select(layer => layer.Asset));
            Body(zoom.Source); Body(zoom.Target); Sprite(zoom.Impact); Sprite(zoom.Block);
            Sprite(zoom.Projectile); Sprite(zoom.Wave);
            foreach (var effect in zoom.WaveEffects) Sprite(effect.Sprite);
            foreach (var sprite in zoom.DoubleEffect) Sprite(sprite);
        }
        _assets = Textures.CreateScope(Textures.LibraryAssets); _assets.Request(resources);
        _flags = Textures.CreateScope(Textures.MapAssets); _flags.Request(flagResources);
        foreach (var portrait in plan.Messages.Where(c => c.Message.SpeakerId is not null && c.Channel != BattleMessageChannel.DuelDialogue)
            .Select(c => (Speaker: c.Message.SpeakerId!.Value, View: c.Channel == BattleMessageChannel.Dialogue
                ? San9.Content.Images.PortraitView.Bust : San9.Content.Images.PortraitView.Face)).Distinct())
        {
            var role = $"information-person-{portrait.Speaker}";
            if (!Textures.Roles.ContainsKey(role)) continue;
            var scope = Textures.CreatePortraitScope(role, portrait.View);
            scope.Request([role]); _portraits.Add(scope);
        }
        QueueRedraw(); return ResourcesReady;
    }
    public void UpdatePlayback(TurnDayPresentation? day)
    {
        if (!ReferenceEquals(_day, day)) Prepare(day);
        if (day?.Battle is not { } plan || !ResourcesReady || _assets?.Error is not null || _flags?.Error is not null || _portraits.Any(p => p.Error is not null)) return;
        double time = day.Elapsed;
        var visuals = plan.Visuals.Where(day.IsVisualReady);
        var mapEffects = plan.MapEffects.Where(c => day.IsFactReady(c.FactIndex) && day.IsDialogueComplete(c.AfterDialogueId));
        UpdateDuel(plan, time);
        var entrance = visuals.FirstOrDefault(c => c.EntranceStart is { } start && time >= start && time < c.End);
        double entranceElapsed = entrance is null ? 0 : time - entrance.EntranceStart!.Value;
        _entering = entrance is not null && time < entrance!.Start;
        _transition.Sample(entrance is null ? 0 : BattleSceneTransition.Opacity(entranceElapsed));
        MapAudioGain = entrance is null ? 1 : BattleSceneTransition.OutgoingGain(entranceElapsed);
        SceneAudioGain = entrance is null ? 1 : _entering ? MapAudioGain : BattleSceneTransition.IncomingGain(entranceElapsed);
        if (_duel is { } activeDuel)
        {
            double age = time - activeDuel.Start;
            _transition.Sample(BattleSceneTransition.Opacity(age));
            MapAudioGain = BattleSceneTransition.OutgoingGain(age);
            SceneAudioGain = BattleSceneTransition.IncomingGain(age);
        }
        bool Active(BattleVisualCue cue) => cue.IsMapEffect
            ? day.IsMapEffectPresented(cue.Id) && day.MapEffectElapsed(cue.Id) < cue.End - cue.Start
            : time >= cue.Start && time < cue.End;
        _preludeFrame = visuals.FirstOrDefault(c => c.PreludeId is not null && Active(c));
        _zoom = visuals.FirstOrDefault(c => c.Zoom is not null && time >= c.Start && time < c.End);
        _mapLayer.Visible = !IsSceneActive;
        UpdateSceneAudio(day, visuals.Where(Active), mapEffects);
        int slot = 0, mapSlot = 0;
        void Effect(BattleSprite asset, double elapsed, Vector2 point, float opacity = 1, int? frameIndex = null,
            bool sceneCoordinates = true, int zIndex = 0, bool mapEffect = false)
        {
            if (!_assets!.TryGetReady(asset.Asset, this, out var texture) && (_flags is null || !_flags.TryGetReady(asset.Asset, this, out texture))) return;
            var sprites = mapEffect ? _mapEffectSprites : _effects;
            int index = mapEffect ? mapSlot++ : slot++;
            Sprite2D sprite;
            if (index == sprites.Count)
            {
                sprite = new();
                if (mapEffect) _mapLayer.AddChild(sprite); else AddChild(sprite);
                sprites.Add(sprite);
            }
            else sprite = sprites[index];
            sprite.Show(); sprite.Texture = texture; sprite.Hframes = asset.Columns;
            sprite.Frame = frameIndex ?? Math.Clamp((int)(elapsed * 1000 / asset.FrameMilliseconds), 0, asset.Columns - 1);
            sprite.Centered = false; sprite.Position = (sceneCoordinates ? SceneOffset : Vector2.Zero) + point + new Vector2(asset.OffsetX - (asset.Centered ? texture.GetWidth() / asset.Columns / 2f : 0), asset.OffsetY);
            sprite.Modulate = new Color(1, 1, 1, asset.Opacity * opacity);
            sprite.Material = asset.Additive ? _additive : null;
            sprite.ZIndex = zIndex;
        }
        foreach (var cue in visuals.Where(c => c.Effect is not null && Active(c)))
        {
            double age = cue.IsMapEffect ? day.MapEffectElapsed(cue.Id) : time - cue.Start;
            if (cue.FadeOutStart is not null || age < cue.Effect!.Columns * cue.Effect.FrameMilliseconds / 1000.0)
                Effect(cue.Effect!, age, cue.ScreenEffect ? new(_zoom is null ? GetViewportRect().Size.X / 2 : 512, 0) : Project(cue.Target),
                    cue.FadeOutStart is { } fadeStart ? BattleEffectFade.Opacity(time - fadeStart) : 1,
                    sceneCoordinates: cue.ScreenEffect && _zoom is not null, zIndex: cue.ScreenEffect ? FullScreenEffectZIndex : 0,
                    mapEffect: cue.IsMapEffect);
        }
        if (!IsSceneActive)
            foreach (var cue in mapEffects.Where(c => day.IsMapEffectPresented(c.Id) && day.MapEffectElapsed(c.Id) < c.Sprite.Columns * c.Sprite.FrameMilliseconds / 1000.0))
                Effect(cue.Sprite, day.MapEffectElapsed(cue.Id), Project(cue.Target), sceneCoordinates: false, mapEffect: true);
        if (_zoom is { } active)
        {
            double elapsed = time - active.Start;
            if (active.Playback is { } actorPlayback)
            {
                var actors = active.Officers?.Sample(elapsed, Textures.AnimationClips) ?? [];
                foreach (var frame in actorPlayback.SampleBodies(elapsed).Concat(actorPlayback.SampleProjectiles(elapsed)).Concat(actorPlayback.SampleLocalEffects(elapsed)).Concat(actors).OrderBy(f => f.Depth ?? f.Y))
                {
                    var texture = _assets!.Assets.ContainsKey(frame.Sprite.Asset) ? _assets.Get(frame.Sprite.Asset, this) : _flags!.Get(frame.Sprite.Asset, this);
                    var point = new Vector2((float)frame.X, (float)frame.Y);
                    if (frame.Centered) point -= new Vector2(texture.GetWidth() / frame.Sprite.Columns / 2f, texture.GetHeight() / 2f);
                    Effect(frame.Sprite with { Centered = false }, frame.Frame * frame.Sprite.FrameMilliseconds / 1000.0, point, frame.Opacity, frame.Frame);
                }
            }
            foreach (var layer in active.Backdrop.Where(layer => layer.Foreground))
                Effect(new(layer.Asset, 1, 1) { Centered = false }, 0, new(layer.X, layer.Y));
            if (active.Playback is { } playback)
                foreach (var impact in playback.Impacts)
                {
                    var asset = active.Blocked ? active.Zoom!.Block : active.Zoom!.Impact;
                    if (elapsed < impact.Time || elapsed >= impact.Time + 16 * asset.FrameMilliseconds / 1000.0) continue;
                    var texture = _assets!.Get(asset.Asset, this);
                    int step = (int)((elapsed - impact.Time) * 1000 / asset.FrameMilliseconds);
                    Effect(asset with { Centered = impact.Centered }, step % asset.Columns * asset.FrameMilliseconds / 1000.0,
                        new(impact.X, impact.Y - (impact.Centered ? texture.GetHeight() / 2f : 0) - step * 2), Math.Max(0, 128 - step * 8) / 256f, step % asset.Columns);
                }
            if (active.Doubled && BattleReinforcementPlayback.Sample(active.Zoom!.DoubleEffect, elapsed) is { } reinforcement)
            {
                Effect(reinforcement.Sprite, 0, new(512, (768 - _assets!.Get(reinforcement.Sprite.Asset, this).GetHeight()) / 2f), reinforcement.Opacity, zIndex: FullScreenEffectZIndex);
            }
        }
        if (_duel is { } scene && scene.Sample(time) is { } duelStage)
        {
            double sceneTime = DuelTime(duelStage);
            foreach (var particle in scene.Particles.Where(p => time >= p.Start && sceneTime < p.End))
            {
                var animation = particle.Definition.Animation;
                foreach (var point in particle.Right ? particle.Definition.RightPositions : particle.Definition.LeftPositions)
                {
                    Effect(new(animation.Asset, animation.Columns, animation.FrameMilliseconds, Additive: animation.Additive)
                        { Centered = false, Opacity = animation.Opacity }, 0, new(point.X, point.Y),
                        frameIndex: DuelSceneCue.AnimationFrame(animation, sceneTime - particle.Start, false));
                    _effects[slot - 1].ZIndex = 2;
                }
            }
            if (DuelSceneCue.SampleEffect(duelStage.Effects, sceneTime - duelStage.Start) is { } effect)
            {
                var texture = _assets!.Get(effect.Sprite.Asset, this);
                Effect(effect.Sprite with { Centered = false }, 0,
                    new((1024 - texture.GetWidth() / (float)effect.Sprite.Columns) / 2, (768 - texture.GetHeight()) / 2f),
                    effect.Opacity, effect.Frame);
                _effects[slot - 1].ZIndex = 4;
            }
        }
        _duelPanels.QueueRedraw(); _duelSpeechFrame.QueueRedraw();
        for (int i = slot; i < _effects.Count; i++) _effects[i].Hide();
        for (int i = mapSlot; i < _mapEffectSprites.Count; i++) _mapEffectSprites[i].Hide();
        QueueRedraw();
    }
    public override void _GuiInput(InputEvent input)
    {
        if (!IsSceneActive || input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse) return;
        _day?.EndSceneFastForward();
        if (new Rect2(920, 700, 96, 44).HasPoint(mouse.Position - SceneOffset)) _day?.SkipVisual();
        else if (!IsPaused() && _day?.PendingDuelDialogues.FirstOrDefault() is { } dialogue)
            _day.CompleteDialogue(dialogue.Id);
        else if (!IsPaused()) _day?.BeginSceneFastForward();
        AcceptEvent();
    }
    public override void _Input(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }) EndSceneFastForward();
    }
    private void EndSceneFastForward() => _day?.EndSceneFastForward();
    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut ||
            what == NotificationVisibilityChanged && !IsVisibleInTree()) EndSceneFastForward();
    }
    private void DrawSceneControls()
    {
        var font = GetThemeFont("font");
        string? hint = IsPaused() ? "已暂停" : _day?.PendingDuelDialogues.Count > 0 ? "单击继续" : null;
        if (hint is not null) DrawString(font, new(560, 730), hint, fontSize: 18, modulate: Colors.LightGray);
        DrawString(font, new(932, 730), CommandText.Resolve(Texts, "battle.skip"), fontSize: 20);
    }
    private void DrawPreludeBackdrop()
    {
        if (_preludeFrame?.Effect is not { } sprite || _assets is null) return;
        var texture = _assets.Get(sprite.Asset, this);
        float width = texture.GetWidth() / (float)sprite.Columns;
        var viewport = GetViewportRect();
        var image = new Rect2(new(viewport.Size.X / 2 + sprite.OffsetX - (sprite.Centered ? width / 2 : 0), sprite.OffsetY),
            new(width, texture.GetHeight())).Intersection(viewport);
        // The original full-screen pictures contain a half-density shadow. Extend
        // that backdrop into the extra window area without stretching the artwork
        // or darkening its transparent pixels a second time.
        var shade = new Color(0, 0, 0, .5f * sprite.Opacity);
        if (!image.HasArea()) { DrawRect(viewport, shade); return; }
        DrawRect(new(viewport.Position, new(viewport.Size.X, image.Position.Y - viewport.Position.Y)), shade);
        DrawRect(new(new(viewport.Position.X, image.End.Y), new(viewport.Size.X, viewport.End.Y - image.End.Y)), shade);
        DrawRect(new(new(viewport.Position.X, image.Position.Y), new(image.Position.X - viewport.Position.X, image.Size.Y)), shade);
        DrawRect(new(new(image.End.X, image.Position.Y), new(viewport.End.X - image.End.X, image.Size.Y)), shade);
    }
    public override void _Draw()
    {
        MouseFilter = !IsSceneActive ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
        DrawPreludeBackdrop();
        _numberLayer.QueueRedraw();
        _mapNumberLayer.QueueRedraw();
        if (_duel is not null) { DrawDuel(); return; }
        if (_zoom is not { } cue || _assets is null || cue.Zoom is not { } zoom || _day is null) return;
        DrawRect(new(Vector2.Zero, GetViewportRect().Size), new Color(16 / 255f, 25 / 255f, 34 / 255f, 217 / 256f));
        DrawSetTransform(SceneOffset);
        foreach (var layer in cue.Backdrop.Where(layer => !layer.Foreground)) DrawTexture(_assets.Get(layer.Asset, this), new(layer.X, layer.Y));
        DrawTexture(_assets.Get(zoom.SourcePanel, this), new(0, 632)); DrawTexture(_assets.Get(zoom.TargetPanel, this), new(680, 0));
        var font = GetThemeFont("font");
        string Person(int id) => Texts.Resolve(_day.Before.MessageSpeaker(id).NameKey);
        string Name(BattleObjectContext obj) => obj.IsSettlement ? _day.Before.Settlements.First(s => s.Id == obj.Id).ResolveName(Texts) :
            obj.CommanderId is int commander ? CommandText.Resolve(Texts, "battle.unit-name", Person(commander)) : CommandText.Resolve(Texts, "battle.unit");
        string Force(BattleObjectContext obj) => obj.ForceId is int force
            ? CommandText.Resolve(Texts, "battle.force-name", Texts.Resolve(_day.Before.Forces.Single(f => f.Id == force).NameKey)) : "";
        void PanelText(string text, Vector2 topLeft) => DrawString(font, topLeft + new Vector2(0, font.GetAscent(24)), text, fontSize: 24);
        PanelText(Force(cue.Source), new(72, 676));
        PanelText(Name(cue.Source), new(72, 708));
        PanelText(_day.Before.Information.Tactics.Single(t => t.Id == cue.TacticId).Name, new(232, 692));
        PanelText(Force(cue.Target), new(816, 40));
        PanelText(Name(cue.Target), new(816, 72));
        DrawSceneControls();
        DrawSetTransform(Vector2.Zero);
    }
    private void DrawMapFeedback(BattleNumberLayer layer)
    {
        var font = GetThemeFont("font");
        foreach (var morale in _mapMorales.Where(c => c.Age >= 0.8))
        {
            var feedback = morale.Cue;
            string text = CommandText.Resolve(Texts, "battle.morale-change", $"+{feedback.MoraleGain}");
            if (text.Length > 0) layer.DrawString(font, Project(feedback.Target) + new Vector2(-28, -52), text, fontSize: 24,
                modulate: feedback.SoldierGain > 0 ? Colors.LightGreen : Colors.OrangeRed);
        }
        foreach (var number in _mapNumbers)
        {
            var numbers = number.Definition;
            if (_numberAssets?.TryGetReady(numbers.Asset, layer, out var texture) != true ||
                BattleMapNumberPlayback.Sample(numbers, number.Cue.Amount, number.Age) is not { } frame) continue;
            var point = Project(number.Cue.Target) + new Vector2((float)frame.X, (float)frame.Y);
            for (int i = 0; i < frame.Text.Length; i++)
                layer.DrawTextureRectRegion(texture, new(point + new Vector2(i * numbers.GlyphWidth, 0), new(numbers.GlyphWidth, numbers.GlyphHeight)),
                    new(BattleMapNumberPlayback.Column(frame.Text[i]) * numbers.GlyphWidth, frame.Row * numbers.GlyphHeight, numbers.GlyphWidth, numbers.GlyphHeight));
        }
    }
    private void DrawNumbers(BattleNumberLayer layer)
    {
        if (_day?.Battle is null || _assets is null || !ResourcesReady) return;
        if (_zoom is { Playback.Numbers: { } numberPlayback } cue)
        {
            double elapsed = _day.Elapsed - cue.Start;
            var glyphs = numberPlayback.SampleTotal(elapsed);
            if (!cue.Blocked) glyphs = glyphs.Concat(cue.Playback!.Impacts.SelectMany(p => numberPlayback.SampleSmall(p, elapsed)));
            foreach (var glyph in glyphs)
                layer.DrawTextureRectRegion(_assets.Get(glyph.Sheet.Asset, layer), new(SceneOffset + new Vector2((float)glyph.X, (float)glyph.Y), new(glyph.Sheet.Width, glyph.Sheet.Height)),
                    new(0, (float)glyph.SourceY, glyph.Sheet.Width, glyph.Sheet.Height), new Color(1, 1, 1, glyph.Opacity));
        }
    }
    public override void _Ready()
    {
        InitializeSceneAudio();
        // Map feedback remains in the UI canvas for screen-space projection, but has
        // its own ordering below HUD controls (0) and map speeches (-1).
        _mapLayer = new() { Name = "MapEffects", ZAsRelative = false, ZIndex = -2 }; AddChild(_mapLayer);
        _mapNumberLayer = new() { DrawNumbers = DrawMapFeedback, ZIndex = 1 }; _mapLayer.AddChild(_mapNumberLayer);
        _numberLayer = new() { DrawNumbers = DrawNumbers, ZIndex = 1 }; AddChild(_numberLayer);
        _duelUi = Textures.CreateScope();
        _duelPanels = new Node2D { ZIndex = 3 }; AddChild(_duelPanels); _duelPanels.Draw += DrawDuelPanels;
        _duelSpeechFrame = new Node2D { ZIndex = 5 }; AddChild(_duelSpeechFrame); _duelSpeechFrame.Draw += DrawDuelSpeech;
        _duelText = new GameRichTextLabel { Textures = _duelUi, MouseFilter = MouseFilterEnum.Ignore, ZIndex = 6,
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_duelText); _duelText.Hide();
        _transition = new() { ZIndex = 10 }; AddChild(_transition);
        GetViewport().SizeChanged += ViewportChanged;
        GetWindow().MouseExited += EndSceneFastForward;
        Size = GetViewportRect().Size;
    }
    public override void _ExitTree()
    {
        EndSceneFastForward();
        GetWindow().MouseExited -= EndSceneFastForward;
        GetViewport().SizeChanged -= ViewportChanged;
        ResetSceneAudio();
        _streams.Dispose();
        _duelUi?.Dispose(); _assets?.Dispose(); _flags?.Dispose(); _numberAssets?.Dispose();
        foreach (var portrait in _portraits) portrait.Dispose();
        Textures.Defer(_additive.Dispose);
    }
}
