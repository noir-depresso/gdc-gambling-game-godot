#nullable enable
using System;
using System.Threading.Tasks;
using Godot;

namespace GodotGdc.V1;

public partial class MainScreen : Control
{
    private readonly PackedScene _titleScene = ResourceLoader.Load<PackedScene>("res://scenes/title_screen.tscn");
    private readonly PackedScene _deckBuilderScene = ResourceLoader.Load<PackedScene>("res://scenes/deck_builder_screen.tscn");
    private readonly PackedScene _combatScene = ResourceLoader.Load<PackedScene>("res://scenes/combat_screen.tscn");

    private AppSession _session = null!;
    private Control? _activeScreen;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var content = new ContentLibrary();
        content.LoadAll();
        var storage = new DeckStorage();
        var rules = ResourceLoader.Load<CombatRulesResource>("res://data/combat_rules.tres");
        var decks = storage.LoadDecks(content.DefaultDeck);
        if (decks.Count == 0)
        {
            decks.Add(content.DefaultDeck.Copy());
        }

        _session = new AppSession
        {
            Content = content,
            Storage = storage,
            Rules = rules,
            SavedDecks = decks,
            CurrentDeck = decks[0].Copy(),
            UiScale = 1.0f,
            AccentColor = new Color(0.32f, 0.82f, 0.47f),
            HackerMode = false
        };

        Theme = UiStyles.GetTheme(UiStyles.BuildPalette(_session.AccentColor, _session.HackerMode));
        UiStyles.ApplyWindowScale(GetWindow(), _session.UiScale);
        ShowTitle();
        _ = SaveViewportScreenshotAsync("startup");
        if (FileAccess.FileExists("res://.codex/capture_combat.flag"))
        {
            _ = AutoOpenCombatForScreenshotAsync();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F12 })
        {
            return;
        }

        _ = SaveViewportScreenshotAsync("manual");
    }

    private void ShowDeckBuilder()
    {
        ClearActiveScreen();
        var screen = _deckBuilderScene.Instantiate<DeckBuilderScreen>();
        screen.Initialize(_session);
        screen.StartCombatRequested += OnStartCombatRequested;
        screen.ReturnToTitleRequested += ShowTitle;
        AddChild(screen);
        _activeScreen = screen;
    }

    private void ShowTitle()
    {
        ClearActiveScreen();
        var screen = _titleScene.Instantiate<TitleScreen>();
        screen.StartGameRequested += OnStartCombatRequested;
        screen.DeckBuilderRequested += ShowDeckBuilder;
        AddChild(screen);
        _activeScreen = screen;
    }

    private void ShowCombat()
    {
        ClearActiveScreen();
        var screen = _combatScene.Instantiate<CombatScreen>();
        screen.Initialize(_session);
        screen.BackToDeckBuilderRequested += OnBackToDeckBuilderRequested;
        screen.ReturnToTitleRequested += ShowTitle;
        AddChild(screen);
        _activeScreen = screen;
        _ = SaveViewportScreenshotAsync("combat");
    }

    private void ClearActiveScreen()
    {
        if (_activeScreen == null)
        {
            return;
        }

        _activeScreen.QueueFree();
        _activeScreen = null;
    }

    private void OnStartCombatRequested()
    {
        ShowCombat();
    }

    private void OnBackToDeckBuilderRequested()
    {
        ShowDeckBuilder();
        _ = SaveViewportScreenshotAsync("deck_builder");
    }

    private async Task SaveViewportScreenshotAsync(string label)
    {
        // Wait a few frames so containers, textures, and runtime data have settled.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var directory = ProjectSettings.GlobalizePath("res://screenshots");
        DirAccess.MakeDirRecursiveAbsolute(directory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var path = $"{directory}/ui_{label}_{timestamp}.png";
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(path);

        if (error == Error.Ok)
        {
            GD.Print($"Saved UI screenshot: {path}");
        }
        else
        {
            GD.PushWarning($"Failed to save UI screenshot ({error}): {path}");
        }
    }

    private async Task AutoOpenCombatForScreenshotAsync()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
        OnStartCombatRequested();
    }
}
