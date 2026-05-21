#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace GodotGdc.V1;

public partial class CombatScreen : Control
{
    private AppSession _session = null!;
    private CombatController _controller = null!;
    private UiStyles.Palette _palette = null!;

    private ColorRect _background = null!;
    private Label _enemyNameLabel = null!;
    private Label _enemySubLabel = null!;
    private Label _turnHintLabel = null!;

    private Label _playerHpValue = null!;
    private Label _bitsValue = null!;
    private Label _enemyHpValue = null!;
    private Label _roundValue = null!;

    private Label _incomeValue = null!;
    private Label _drawPileValue = null!;
    private Label _discardPileValue = null!;
    private Label _intentValue = null!;
    private Label _statusValue = null!;

    private Label _economyBaseLabel = null!;
    private Label _economyAddLabel = null!;
    private Label _economyMultLabel = null!;
    private Label _economyVolLabel = null!;
    private Label _economyGainLabel = null!;
    private Label _economyCostLabel = null!;

    private HBoxContainer _handRow = null!;
    private RichTextLabel _logOutput = null!;
    private Button _endTurnButton = null!;
    private Button _viewDeckButton = null!;

    private ColorRect _overlay = null!;
    private PanelContainer _overlayPanel = null!;
    private VBoxContainer _overlayBody = null!;
    private System.Action? _overlayCloseAction;

    private readonly List<string> _logHistory = new();
    private readonly RandomNumberGenerator _runRng = new();
    private int _pendingSecondarySource = -1;
    private DeckDefinition _runDeck = new();
    private List<string> _encounterIds = new();
    private int _currentEncounterIndex;
    private int _runStartingBitsBonus;
    private int _runIncomeBonus;
    private int _runVictories;

    public event System.Action? BackToDeckBuilderRequested;

    public void Initialize(AppSession session)
    {
        _session = session;
    }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        _runRng.Randomize();
        RebuildScreen(true);
    }

    private void RebuildScreen(bool createController)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _palette = UiStyles.BuildPalette(_session.AccentColor, _session.HackerMode);
        Theme = UiStyles.GetTheme(_palette);
        BuildUi();

        if (createController)
        {
            _controller = new CombatController();
            _controller.Setup(_session.Content, _session.Rules);
            _controller.SnapshotChanged += OnSnapshotChanged;
            _controller.LogAdded += AppendLog;
            _controller.CombatEnded += OnCombatEnded;
            _controller.CombatStarted += OnCombatStarted;
            InitializeRun();
            StartCurrentEncounter();
        }
        else
        {
            RepaintLog();
            OnSnapshotChanged(_controller.GetSnapshot());
        }
    }

    private void BuildUi()
    {
        _background = UiStyles.MakeBackground(_palette);
        _background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_background);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        AddChild(margin);

        var root = new VBoxContainer();
        root.SizeFlagsVertical = SizeFlags.ExpandFill;
        root.AddThemeConstantOverride("separation", 14);
        margin.AddChild(root);

        var headerRow = new HBoxContainer();
        headerRow.AddThemeConstantOverride("separation", 12);
        root.AddChild(headerRow);

        var enemyBanner = UiStyles.MakeSurface("", _palette, true);
        enemyBanner.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        headerRow.AddChild(enemyBanner);
        var enemyBannerBody = (VBoxContainer)enemyBanner.GetChild(0);
        enemyBannerBody.AddChild(UiStyles.MakeSubtle("Enemy Encounter", 15, _palette));
        _enemyNameLabel = UiStyles.MakeHeading("Enemy", 34, _palette);
        enemyBannerBody.AddChild(_enemyNameLabel);
        _enemySubLabel = UiStyles.MakeSubtle("Your turn. Spend bits and shape the round.", 16, _palette);
        enemyBannerBody.AddChild(_enemySubLabel);

        var headerButtons = new VBoxContainer();
        headerButtons.AddThemeConstantOverride("separation", 10);
        headerRow.AddChild(headerButtons);

        _viewDeckButton = UiStyles.MakeAccentButton("Inspect Deck", _palette, _palette.AccentSoft);
        _viewDeckButton.CustomMinimumSize = new Vector2(150, 64);
        _viewDeckButton.Pressed += () => ShowRemainingDeck(HideOverlay);
        headerButtons.AddChild(_viewDeckButton);

        var menuButton = UiStyles.MakeAccentButton("Menu", _palette, _palette.Accent);
        menuButton.CustomMinimumSize = new Vector2(150, 64);
        menuButton.Pressed += ShowMainMenu;
        headerButtons.AddChild(menuButton);

        var statRow = new HBoxContainer();
        statRow.AddThemeConstantOverride("separation", 12);
        root.AddChild(statRow);
        CreatePrimaryStatCard(statRow, "PLAYER HP", _palette.Highlight, "Stay alive through the round.", out _playerHpValue);
        CreatePrimaryStatCard(statRow, "BITS", _palette.Accent.Lightened(0.12f), "Spend these to play cards.", out _bitsValue);
        CreatePrimaryStatCard(statRow, "ENEMY HP", _palette.Warning.Lightened(0.1f), "Drop this to zero to win.", out _enemyHpValue);
        CreatePrimaryStatCard(statRow, "ROUND", _palette.TextStrong, "Upkeep-first round ticker.", out _roundValue);

        var infoRow = new HBoxContainer();
        infoRow.AddThemeConstantOverride("separation", 12);
        root.AddChild(infoRow);
        CreateSecondaryStatCard(infoRow, "Income This Turn", out _incomeValue);
        CreateSecondaryStatCard(infoRow, "Draw Pile", out _drawPileValue);
        CreateSecondaryStatCard(infoRow, "Discard", out _discardPileValue);
        CreateSecondaryStatCard(infoRow, "Enemy Intent", out _intentValue);
        CreateSecondaryStatCard(infoRow, "Encounter", out _statusValue);

        var guidePanel = UiStyles.MakeSurface("Turn Guide", _palette);
        root.AddChild(guidePanel);
        var guideBody = (VBoxContainer)guidePanel.GetChild(0);
        _turnHintLabel = UiStyles.MakeCenteredLabel("Pick cards from the hand row, then end the turn when you are done.", 22, _palette);
        _turnHintLabel.AddThemeColorOverride("font_color", _palette.Highlight);
        guideBody.AddChild(_turnHintLabel);
        guideBody.AddChild(UiStyles.MakeSubtle("Some cards ask for a second selection. When that happens, pick the source card first, then click the sacrifice target.", 15, _palette));

        var economyPanel = UiStyles.MakeSurface("Bits Economy", _palette);
        root.AddChild(economyPanel);
        var economyBody = (VBoxContainer)economyPanel.GetChild(0);
        economyBody.AddChild(UiStyles.MakeSubtle("Formula: (B + A) x (1 + M + V)", 14, _palette));
        var economyRow = new HBoxContainer();
        economyRow.AddThemeConstantOverride("separation", 8);
        economyBody.AddChild(economyRow);
        _economyBaseLabel = CreateCompactEconomyTile(economyRow, "B");
        _economyAddLabel = CreateCompactEconomyTile(economyRow, "A");
        _economyMultLabel = CreateCompactEconomyTile(economyRow, "M");
        _economyVolLabel = CreateCompactEconomyTile(economyRow, "V");
        _economyGainLabel = CreateCompactEconomyTile(economyRow, "Gain");
        _economyCostLabel = CreateCompactEconomyTile(economyRow, "Cost");

        var handPanel = UiStyles.MakeSurface("Hand", _palette, true);
        handPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        root.AddChild(handPanel);
        var handBody = (VBoxContainer)handPanel.GetChild(0);
        handBody.AddChild(UiStyles.MakeSubtle("Cards stay docked on the bottom row. Scroll sideways if the hand gets wide.", 15, _palette));
        var handScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.ShowAlways,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 250)
        };
        handBody.AddChild(handScroll);

        _handRow = new HBoxContainer();
        _handRow.AddThemeConstantOverride("separation", 12);
        handScroll.AddChild(_handRow);

        var bottomBar = new HBoxContainer();
        bottomBar.AddThemeConstantOverride("separation", 12);
        root.AddChild(bottomBar);

        var logPanel = UiStyles.MakeSurface("Combat Log", _palette);
        logPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        logPanel.CustomMinimumSize = new Vector2(380, 130);
        bottomBar.AddChild(logPanel);
        var logBody = (VBoxContainer)logPanel.GetChild(0);
        _logOutput = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = true,
            SelectionEnabled = true,
            CustomMinimumSize = new Vector2(0, 82)
        };
        logBody.AddChild(_logOutput);

        bottomBar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _endTurnButton = UiStyles.MakeAccentButton("End Turn", _palette, _palette.Warning);
        _endTurnButton.CustomMinimumSize = new Vector2(180, 58);
        _endTurnButton.Pressed += () =>
        {
            _pendingSecondarySource = -1;
            _controller.EndPlayerTurn();
        };
        bottomBar.AddChild(_endTurnButton);

        BuildOverlay();
    }

    private void CreatePrimaryStatCard(HBoxContainer row, string title, Color valueColor, string subtitle, out Label valueLabel)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 120)
        };
        panel.AddThemeStyleboxOverride("panel", _palette.HackerMode
            ? UiStyles.MakeFlatBox(new Color(0, 0, 0, 0), 0, new Color(0, 0, 0, 0), 0)
            : UiStyles.MakeFlatBox(_palette.SurfaceAlt, 14, _palette.AccentDark.Lightened(0.24f), 2));
        row.AddChild(panel);

        var body = new VBoxContainer();
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.Alignment = BoxContainer.AlignmentMode.Center;
        body.AddThemeConstantOverride("separation", 4);
        panel.AddChild(body);

        body.AddChild(UiStyles.MakeCenteredLabel(_palette.HackerMode ? $"[ {title} ]" : title, 15, _palette));
        valueLabel = UiStyles.MakeMonoValue("-", 40, _palette);
        valueLabel.AddThemeColorOverride("font_color", valueColor);
        body.AddChild(valueLabel);
        body.AddChild(UiStyles.MakeCenteredLabel(_palette.HackerMode ? $"* {subtitle}" : subtitle, 13, _palette));
    }

    private void CreateSecondaryStatCard(HBoxContainer row, string title, out Label valueLabel)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 92)
        };
        panel.AddThemeStyleboxOverride("panel", _palette.HackerMode
            ? UiStyles.MakeFlatBox(new Color(0, 0, 0, 0), 0, new Color(0, 0, 0, 0), 0)
            : UiStyles.MakeFlatBox(_palette.Surface, 12, _palette.AccentDark.Lightened(0.18f), 1));
        row.AddChild(panel);

        var body = new VBoxContainer();
        body.Alignment = BoxContainer.AlignmentMode.Center;
        body.AddThemeConstantOverride("separation", 3);
        panel.AddChild(body);

        body.AddChild(UiStyles.MakeCenteredLabel(_palette.HackerMode ? $"| {title} |" : title, 14, _palette));
        valueLabel = UiStyles.MakeMonoValue("-", 24, _palette);
        body.AddChild(valueLabel);
    }

    private Label CreateCompactEconomyTile(HBoxContainer row, string title)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(110, 74)
        };
        panel.AddThemeStyleboxOverride("panel", _palette.HackerMode
            ? UiStyles.MakeFlatBox(new Color(0, 0, 0, 0), 0, new Color(0, 0, 0, 0), 0)
            : UiStyles.MakeFlatBox(_palette.SurfaceSoft, 10, _palette.AccentDark.Lightened(0.26f), 1));
        row.AddChild(panel);

        var body = new VBoxContainer();
        body.Alignment = BoxContainer.AlignmentMode.Center;
        body.AddThemeConstantOverride("separation", 2);
        panel.AddChild(body);
        body.AddChild(UiStyles.MakeCenteredLabel(_palette.HackerMode ? $"/ {title} \\" : title, 14, _palette));
        var value = UiStyles.MakeMonoValue("-", 22, _palette);
        body.AddChild(value);
        return value;
    }

    private void BuildOverlay()
    {
        _overlay = new ColorRect
        {
            Color = new Color(0.01f, 0.03f, 0.01f, 0.86f),
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop
        };
        _overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_overlay);

        _overlayPanel = new PanelContainer
        {
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(820, 560)
        };
        _overlayPanel.SetAnchorsPreset(LayoutPreset.Center);
        _overlayPanel.Position = new Vector2(-410, -280);
        _overlayPanel.Size = new Vector2(820, 560);
        _overlayPanel.AddThemeStyleboxOverride("panel", UiStyles.MakeFlatBox(_palette.SurfaceAlt, 18, _palette.AccentDark.Lightened(0.30f), 2));
        AddChild(_overlayPanel);

        _overlayBody = new VBoxContainer();
        _overlayBody.SizeFlagsVertical = SizeFlags.ExpandFill;
        _overlayBody.AddThemeConstantOverride("separation", 14);
        _overlayPanel.AddChild(_overlayBody);
    }

    private void InitializeRun()
    {
        _runDeck = _session.CurrentDeck.Copy();
        _encounterIds = BuildEncounterSequence();
        _currentEncounterIndex = 0;
        _runStartingBitsBonus = 0;
        _runIncomeBonus = 0;
        _runVictories = 0;
        _pendingSecondarySource = -1;
    }

    private List<string> BuildEncounterSequence()
    {
        return new List<string>
        {
            "street_enforcer",
            "signal_ghost",
            "district_overseer"
        };
    }

    private void StartCurrentEncounter()
    {
        if (_currentEncounterIndex < 0 || _currentEncounterIndex >= _encounterIds.Count)
        {
            return;
        }

        HideOverlay();
        _pendingSecondarySource = -1;
        _controller.SetActiveDeck(_runDeck);
        _controller.SetActiveEnemy(_encounterIds[_currentEncounterIndex]);
        _controller.ConfigureRunBonuses(_runStartingBitsBonus, _runIncomeBonus);
        _controller.StartCombat();
    }

    private void OnCombatStarted()
    {
        _logHistory.Clear();
        RepaintLog();
    }

    private void OnSnapshotChanged(CombatSnapshot snapshot)
    {
        _enemyNameLabel.Text = snapshot.EnemyName;
        var encounterText = $"Fight {_currentEncounterIndex + 1}/{Mathf.Max(1, _encounterIds.Count)}";
        _enemySubLabel.Text = snapshot.PlayerTurnActive
            ? $"{encounterText}. Next intent: {snapshot.EnemyIntentLabel}. {snapshot.EnemyIntentDescription}"
            : $"{encounterText}. Enemy phase is resolving.";

        _playerHpValue.Text = snapshot.PlayerHp.ToString();
        _bitsValue.Text = snapshot.PlayerBits.ToString();
        _enemyHpValue.Text = snapshot.EnemyHp.ToString();
        _roundValue.Text = snapshot.Round.ToString();

        _incomeValue.Text = $"+{snapshot.CurrentTurnIncome}";
        _drawPileValue.Text = snapshot.DrawPileCount.ToString();
        _discardPileValue.Text = snapshot.DiscardPileCount.ToString();
        _intentValue.Text = snapshot.EnemyIntentLabel;
        _statusValue.Text = $"{_currentEncounterIndex + 1}/{Mathf.Max(1, _encounterIds.Count)}";

        _turnHintLabel.Text = _pendingSecondarySource >= 0
            ? "Choose the second card to sacrifice and finish this play."
            : snapshot.PlayerTurnActive
                ? "Play any cards you want from the hand row, then end the turn when you are ready."
                : "Enemy action is happening now. Your next turn begins automatically after the round resolves.";

        _economyBaseLabel.Text = $"{snapshot.Economy.BaseIncome:0}";
        _economyAddLabel.Text = $"+{snapshot.Economy.FlatBonus:0}";
        _economyMultLabel.Text = $"+{snapshot.Economy.MultiplierBonus * 100.0f:0}%";
        _economyVolLabel.Text = $"{snapshot.Economy.VStacks}";
        _economyGainLabel.Text = $"+{snapshot.CurrentTurnIncome}";
        _economyCostLabel.Text = $"-{snapshot.Economy.CostReduction * 100.0f:0}%";

        _endTurnButton.Disabled = !snapshot.PlayerTurnActive;
        _viewDeckButton.Disabled = !snapshot.CombatActive;
        RebuildHand(snapshot);
    }

    private void RebuildHand(CombatSnapshot snapshot)
    {
        foreach (var child in _handRow.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var card in snapshot.Hand)
        {
            var borderColor = card.Index == _pendingSecondarySource
                ? _palette.Highlight
                : _palette.AccentDark.Lightened(0.28f);
            var backgroundColor = card.Index == _pendingSecondarySource
                ? _palette.SurfaceSoft
                : _palette.Surface;

            var panel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(218, 208)
            };
            panel.AddThemeStyleboxOverride("panel", _palette.HackerMode
                ? UiStyles.MakeFlatBox(new Color(0, 0, 0, 0), 0, new Color(0, 0, 0, 0), 0)
                : UiStyles.MakeFlatBox(backgroundColor, 14, borderColor, 2));
            _handRow.AddChild(panel);

            var body = new VBoxContainer();
            body.SizeFlagsVertical = SizeFlags.ExpandFill;
            body.AddThemeConstantOverride("separation", 8);
            panel.AddChild(body);

            var titleRow = new HBoxContainer();
            titleRow.AddThemeConstantOverride("separation", 8);
            body.AddChild(titleRow);

            var titleBox = new VBoxContainer();
            titleBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            titleRow.AddChild(titleBox);
            titleBox.AddChild(UiStyles.MakeHeading(card.Name, 24, _palette));
            titleBox.AddChild(UiStyles.MakeSubtle(card.Type.ToUpperInvariant(), 13, _palette));

            var costBadge = new PanelContainer
            {
                CustomMinimumSize = new Vector2(64, 40)
            };
            costBadge.AddThemeStyleboxOverride("panel", _palette.HackerMode
                ? UiStyles.MakeFlatBox(new Color(0, 0, 0, 0), 0, new Color(0, 0, 0, 0), 0)
                : UiStyles.MakeFlatBox(_palette.AccentSoft, 10, _palette.Accent.Lightened(0.15f), 1));
            titleRow.AddChild(costBadge);
            var costText = UiStyles.MakeMonoValue($"{card.Cost}b", 20, _palette);
            costText.AddThemeColorOverride("font_color", _palette.TextStrong);
            costBadge.AddChild(costText);

            var description = new RichTextLabel
            {
                BbcodeEnabled = true,
                FitContent = false,
                ScrollActive = true,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 88),
                Text = $"[font_size=17]{card.Description}[/font_size]"
            };
            body.AddChild(description);

            var footer = new VBoxContainer();
            footer.AddThemeConstantOverride("separation", 6);
            body.AddChild(footer);

            if (card.RequiresSecondary)
            {
                footer.AddChild(UiStyles.MakeSubtle("Needs another card from hand to resolve.", 13, _palette));
            }
            else
            {
                footer.AddChild(UiStyles.MakeSubtle(card.Playable ? "Ready to play now." : "Not enough bits right now.", 13, _palette));
            }

            var actionText = card.RequiresSecondary && _pendingSecondarySource < 0
                ? "Choose + Sacrifice"
                : _pendingSecondarySource >= 0 && card.Index == _pendingSecondarySource
                    ? "Selected"
                    : "Play Card";
            var playButton = UiStyles.MakeAccentButton(actionText, _palette, card.Playable ? _palette.Accent : _palette.SurfaceSoft);
            playButton.Disabled = !card.Playable;
            playButton.Pressed += () => OnHandCardPressed(card.Index);
            footer.AddChild(playButton);
        }
    }

    private void OnHandCardPressed(int index)
    {
        var snapshot = _controller.GetSnapshot();
        if (index < 0 || index >= snapshot.Hand.Count)
        {
            return;
        }

        var card = snapshot.Hand[index];
        if (_pendingSecondarySource >= 0)
        {
            if (_pendingSecondarySource == index)
            {
                _pendingSecondarySource = -1;
                OnSnapshotChanged(snapshot);
                return;
            }

            _controller.PlayCard(_pendingSecondarySource, index);
            _pendingSecondarySource = -1;
            return;
        }

        if (card.RequiresSecondary)
        {
            _pendingSecondarySource = index;
            OnSnapshotChanged(snapshot);
            return;
        }

        _controller.PlayCard(index);
    }

    private void ShowMainMenu()
    {
        var body = new VBoxContainer();
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddThemeConstantOverride("separation", 12);
        body.AddChild(UiStyles.MakeSubtle("Combat is effectively paused while this menu is open. Restart begins the whole V2 run again.", 16, _palette));

        var tutorialButton = new Button { Text = "Tutorial" };
        tutorialButton.Pressed += () => ShowDocument("How Combat Works", BuildTutorialText(), ShowMainMenu);
        body.AddChild(tutorialButton);

        var cardInfoButton = new Button { Text = "Card Information" };
        cardInfoButton.Pressed += () => ShowDocument("Card Reference", BuildCardReferenceText(), ShowMainMenu);
        body.AddChild(cardInfoButton);

        var deckButton = new Button { Text = "Inspect Deck" };
        deckButton.Pressed += () => ShowRemainingDeck(ShowMainMenu);
        body.AddChild(deckButton);

        body.AddChild(UiStyles.MakeSubtle("UI Scale", 15, _palette));
        var slider = new HSlider
        {
            MinValue = 0.8,
            MaxValue = 1.5,
            Step = 0.05,
            Value = _session.UiScale
        };
        slider.ValueChanged += value =>
        {
            _session.UiScale = (float)value;
            UiStyles.ApplyWindowScale(GetWindow(), _session.UiScale);
        };
        body.AddChild(slider);

        body.AddChild(UiStyles.MakeSubtle("Theme Accent", 15, _palette));
        var picker = UiStyles.MakeThemePicker(_palette, _session.AccentColor);
        picker.ColorChanged += color =>
        {
            _session.AccentColor = color;
            RebuildScreen(false);
            ShowMainMenu();
        };
        body.AddChild(picker);

        var hackerButton = new Button { Text = _session.HackerMode ? "Hacker Mode: ON" : "Hacker Mode: OFF" };
        hackerButton.Pressed += () =>
        {
            _session.HackerMode = !_session.HackerMode;
            RebuildScreen(false);
            ShowMainMenu();
        };
        body.AddChild(hackerButton);

        var actionRow = new HBoxContainer();
        actionRow.AddThemeConstantOverride("separation", 10);
        body.AddChild(actionRow);

        var restartButton = new Button { Text = "Restart Combat" };
        restartButton.Pressed += () =>
        {
            InitializeRun();
            StartCurrentEncounter();
        };
        actionRow.AddChild(restartButton);

        var backButton = new Button { Text = "Back To Deck Builder" };
        backButton.Pressed += () =>
        {
            HideOverlay();
            BackToDeckBuilderRequested?.Invoke();
        };
        actionRow.AddChild(backButton);

        var closeButton = UiStyles.MakeAccentButton("Resume", _palette, _palette.Accent);
        closeButton.Pressed += HideOverlay;
        body.AddChild(closeButton);

        ShowOverlay("Menu", body, HideOverlay);
    }

    private void ShowRemainingDeck(System.Action? onClose)
    {
        var builder = new StringBuilder();
        builder.Append("[font_size=30][b]Remaining Draw Pile[/b][/font_size]\n\n");
        var remaining = _controller.GetRemainingDeckCounts();
        if (remaining.Count == 0)
        {
            builder.Append("No cards left in the draw pile.\n");
        }
        else
        {
            foreach (var row in remaining)
            {
                var card = _session.Content.GetCard(row.CardId);
                builder.Append($"[b]{card?.Name ?? row.CardId}[/b] x{row.Count}\n");
            }
        }

        builder.Append("\n[font_size=26][b]Discard Pile[/b][/font_size]\n\n");
        var discard = _controller.GetDiscardCounts();
        if (discard.Count == 0)
        {
            builder.Append("Discard pile is empty.\n");
        }
        else
        {
            foreach (var row in discard)
            {
                var card = _session.Content.GetCard(row.CardId);
                builder.Append($"[b]{card?.Name ?? row.CardId}[/b] x{row.Count}\n");
            }
        }

        ShowDocument("Deck Inspection", builder.ToString(), onClose);
    }

    private void ShowDocument(string title, string text, System.Action? onClose)
    {
        var document = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectionEnabled = true,
            CustomMinimumSize = new Vector2(0, 360),
            Text = text
        };

        var wrapper = new VBoxContainer();
        wrapper.SizeFlagsVertical = SizeFlags.ExpandFill;
        wrapper.AddThemeConstantOverride("separation", 12);
        wrapper.AddChild(document);

        var closeButton = UiStyles.MakeAccentButton(onClose == ShowMainMenu ? "Back" : "Close", _palette, _palette.Accent);
        closeButton.Pressed += () =>
        {
            HideOverlay();
            onClose?.Invoke();
        };
        wrapper.AddChild(closeButton);

        ShowOverlay(title, wrapper, () =>
        {
            HideOverlay();
            onClose?.Invoke();
        });
    }

    private void ShowOverlay(string title, Control content, System.Action? onClose)
    {
        _overlayCloseAction = onClose;
        foreach (var child in _overlayBody.GetChildren())
        {
            child.QueueFree();
        }

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        header.AddChild(UiStyles.MakeHeading(title, 30, _palette));
        header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var close = new Button { Text = "X", CustomMinimumSize = new Vector2(44, 40) };
        close.Pressed += HandleOverlayClose;
        header.AddChild(close);
        _overlayBody.AddChild(header);
        _overlayBody.AddChild(content);

        _overlay.Visible = true;
        _overlayPanel.Visible = true;
    }

    private void HandleOverlayClose()
    {
        var closeAction = _overlayCloseAction;
        HideOverlay();
        if (closeAction != null && closeAction != (System.Action)HideOverlay)
        {
            closeAction.Invoke();
        }
    }

    private void HideOverlay()
    {
        _overlayCloseAction = null;
        _overlay.Visible = false;
        _overlayPanel.Visible = false;
    }

    private void AppendLog(string message)
    {
        _logHistory.Add(message);
        if (_logOutput != null)
        {
            _logOutput.AppendText($"{message}\n");
            _logOutput.ScrollToLine(_logOutput.GetLineCount());
        }
    }

    private void RepaintLog()
    {
        if (_logOutput == null)
        {
            return;
        }

        _logOutput.Clear();
        foreach (var line in _logHistory)
        {
            _logOutput.AppendText($"{line}\n");
        }
    }

    private void OnCombatEnded(bool victory)
    {
        _pendingSecondarySource = -1;
        if (victory)
        {
            _runVictories += 1;
            if (_currentEncounterIndex >= _encounterIds.Count - 1)
            {
                _turnHintLabel.Text = "Run clear. Review your run summary or restart from the menu.";
                ShowRunSummary(true);
                return;
            }

            _turnHintLabel.Text = "Victory. Pick one bits-based reward, then move to the next fight.";
            ShowRewardSelection();
            return;
        }

        _turnHintLabel.Text = "Defeat. Review the run summary, restart the run, or head back to the deck builder.";
        ShowRunSummary(false);
    }

    private string BuildTutorialText()
    {
        return "[font_size=30][b]V2 Combat Run Tutorial[/b][/font_size]\n\n" +
               "[b]Run Flow[/b]\nA run is a short chain of fights. Win a fight, choose one reward, then carry that advantage into the next encounter.\n\n" +
               "[b]Round Flow[/b]\n1. Start-of-turn effects resolve.\n2. Bits income is calculated.\n3. You draw cards.\n4. You play cards from the hand row.\n5. You end the turn.\n6. The enemy acts.\n7. End-of-round effects resolve.\n\n" +
               "[b]Bits Economy[/b]\nV2 still uses only bits. Income follows (B + A) x (1 + M + V). Rewards can add starting bits for future fights or permanently raise base income for the run.\n\n" +
               "[b]Intent + Rewards[/b]\nThe enemy intent box now shows the next action being telegraphed. After a win, rewards can add bits setup, increase run income, or draft a new card into the run deck.";
    }

    private string BuildCardReferenceText()
    {
        var builder = new StringBuilder();
        builder.Append("[font_size=30][b]Card Reference[/b][/font_size]\n\n");
        foreach (var card in _session.Content.ListCards())
        {
            builder.Append($"[b]{card.Name}[/b]  [color=#d6ffe3]{card.CostBits}b[/color]  [color=#bdd0c1]{card.Type}[/color]\n");
            builder.Append($"{card.Description}\n\n");
        }
        return builder.ToString();
    }

    private void ShowRewardSelection()
    {
        var rewards = GenerateRewardOptions();
        var body = new VBoxContainer();
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddThemeConstantOverride("separation", 12);
        body.AddChild(UiStyles.MakeSubtle("Choose one reward to carry into the next encounter.", 16, _palette));

        foreach (var reward in rewards)
        {
            var rewardPanel = UiStyles.MakeSurface(reward.Title, _palette, true);
            var rewardBody = (VBoxContainer)rewardPanel.GetChild(0);
            rewardBody.AddChild(UiStyles.MakeSubtle(reward.Description, 15, _palette));
            var chooseButton = UiStyles.MakeAccentButton("Take Reward", _palette, _palette.Accent);
            chooseButton.Pressed += () =>
            {
                ApplyReward(reward);
                _currentEncounterIndex += 1;
                StartCurrentEncounter();
            };
            rewardBody.AddChild(chooseButton);
            body.AddChild(rewardPanel);
        }

        ShowOverlay("Fight Reward", body, null);
    }

    private List<RunRewardOption> GenerateRewardOptions()
    {
        var rewards = new List<RunRewardOption>
        {
            new()
            {
                Kind = RunRewardKind.BitsBonus,
                Title = "Bits Cache",
                Description = $"+{20 + (_currentEncounterIndex * 10)} starting bits for the rest of this run.",
                Amount = 20 + (_currentEncounterIndex * 10)
            },
            new()
            {
                Kind = RunRewardKind.IncomeBonus,
                Title = "Income Spike",
                Description = $"+{2 + _currentEncounterIndex} base income each round for the rest of this run.",
                Amount = 2 + _currentEncounterIndex
            }
        };

        var cardOffer = _session.Content.ListCards()
            .OrderBy(_ => _runRng.Randi())
            .FirstOrDefault(card => !_runDeck.CardIds.Contains(card.Id) || card.MaxCopies < 0 || _runDeck.CardIds.Count(id => id == card.Id) < card.MaxCopies);
        if (cardOffer != null)
        {
            rewards.Add(new RunRewardOption
            {
                Kind = RunRewardKind.CardDraft,
                Title = $"Draft: {cardOffer.Name}",
                Description = $"Add {cardOffer.Name} to this run deck. Cost {cardOffer.CostBits}b.",
                CardId = cardOffer.Id
            });
        }

        return rewards;
    }

    private void ApplyReward(RunRewardOption reward)
    {
        switch (reward.Kind)
        {
            case RunRewardKind.BitsBonus:
                _runStartingBitsBonus += reward.Amount;
                break;
            case RunRewardKind.IncomeBonus:
                _runIncomeBonus += reward.Amount;
                break;
            case RunRewardKind.CardDraft:
                if (!string.IsNullOrEmpty(reward.CardId))
                {
                    _runDeck.CardIds.Add(reward.CardId);
                }
                break;
        }
    }

    private void ShowRunSummary(bool victory)
    {
        var summary = new StringBuilder();
        summary.Append(victory ? "[font_size=30][b]Run Clear[/b][/font_size]\n\n" : "[font_size=30][b]Run Over[/b][/font_size]\n\n");
        summary.Append($"Fights won: {_runVictories}/{_encounterIds.Count}\n");
        summary.Append($"Starting bits bonus: {_runStartingBitsBonus}\n");
        summary.Append($"Base income bonus: {_runIncomeBonus}\n");
        summary.Append($"Run deck size: {_runDeck.CardIds.Count}\n");

        var body = new VBoxContainer();
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddThemeConstantOverride("separation", 12);
        body.AddChild(new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectionEnabled = true,
            CustomMinimumSize = new Vector2(0, 220),
            Text = summary.ToString()
        });

        var actionRow = new HBoxContainer();
        actionRow.AddThemeConstantOverride("separation", 10);
        body.AddChild(actionRow);

        var restartButton = UiStyles.MakeAccentButton("Restart Run", _palette, _palette.Accent);
        restartButton.Pressed += () =>
        {
            InitializeRun();
            StartCurrentEncounter();
        };
        actionRow.AddChild(restartButton);

        var deckBuilderButton = new Button { Text = "Back To Deck Builder" };
        deckBuilderButton.Pressed += () =>
        {
            HideOverlay();
            BackToDeckBuilderRequested?.Invoke();
        };
        actionRow.AddChild(deckBuilderButton);

        ShowOverlay(victory ? "Run Clear" : "Run Over", body, null);
    }
}
