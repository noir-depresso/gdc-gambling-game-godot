#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace GodotGdc.V1;

public partial class DeckBuilderScreen : Control
{
    private AppSession _session = null!;
    private UiStyles.Palette _palette = null!;

    private ColorRect _background = null!;
    private OptionButton _deckSelector = null!;
    private LineEdit _deckNameEdit = null!;
    private LineEdit _searchEdit = null!;
    private Label _deckCountLabel = null!;
    private Label _selectionHintLabel = null!;
    private ItemList _poolList = null!;
    private ItemList _deckList = null!;
    private RichTextLabel _detailLabel = null!;
    private Button _enterCombatButton = null!;
    private Label _statusLabel = null!;

    private ColorRect _overlay = null!;
    private PanelContainer _overlayPanel = null!;
    private VBoxContainer _overlayBody = null!;
    private Label _overlayTitleLabel = null!;
    private VBoxContainer _overlayContent = null!;
    private Button _overlayCloseButton = null!;
    private System.Action? _overlayCloseAction;
    private bool _uiBound;

    public event System.Action? StartCombatRequested;
    public event System.Action? ReturnToTitleRequested;

    public void Initialize(AppSession session)
    {
        _session = session;
    }

    public override void _Ready()
    {
        RebuildScreen();
    }

    private void RebuildScreen()
    {
        _palette = UiStyles.BuildPalette(_session.AccentColor, _session.HackerMode);
        Theme = UiStyles.GetTheme(_palette);
        SetAnchorsPreset(LayoutPreset.FullRect);
        BindSceneUi();
        RefreshDeckSelector();
        RefreshPoolList();
        RefreshDeckList();
        RefreshDetailFromSelection();
        UpdateStatus("Ctrl/Shift click works for batch selection.");
    }

    private void BindSceneUi()
    {
        _background = GetNode<ColorRect>("%Background");
        _deckSelector = GetNode<OptionButton>("%DeckSelector");
        _deckNameEdit = GetNode<LineEdit>("%DeckNameEdit");
        _searchEdit = GetNode<LineEdit>("%SearchEdit");
        _deckCountLabel = GetNode<Label>("%DeckCountLabel");
        _selectionHintLabel = GetNode<Label>("%SelectionHintLabel");
        _poolList = GetNode<ItemList>("%PoolList");
        _deckList = GetNode<ItemList>("%DeckList");
        _detailLabel = GetNode<RichTextLabel>("%DetailLabel");
        _enterCombatButton = GetNode<Button>("%EnterCombatButton");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _overlay = GetNode<ColorRect>("%Overlay");
        _overlayPanel = GetNode<PanelContainer>("%OverlayPanel");
        _overlayTitleLabel = GetNode<Label>("%OverlayTitleLabel");
        _overlayContent = GetNode<VBoxContainer>("%OverlayContent");
        _overlayCloseButton = GetNode<Button>("%OverlayCloseButton");

        _background.Color = Colors.Black;
        _overlay.Color = new Color(0, 0, 0, 0.86f);
        _overlay.Visible = false;
        _overlayPanel.Visible = false;

        if (_uiBound)
        {
            return;
        }

        _uiBound = true;

        GetNode<Button>("%MenuButton").Pressed += ShowMainMenu;
        _deckSelector.ItemSelected += OnDeckSelected;
        _deckNameEdit.TextSubmitted += text => _session.CurrentDeck.Name = text.Trim();
        GetNode<Button>("%NewDeckButton").Pressed += OnNewDeckPressed;
        GetNode<Button>("%SaveDeckButton").Pressed += OnSaveDeckPressed;
        _enterCombatButton.Pressed += OnEnterCombatPressed;
        _searchEdit.TextChanged += _ => RefreshPoolList();

        _poolList.ItemSelected += _ => RefreshDetailFromSelection();
        _poolList.ItemActivated += OnPoolItemActivated;
        GetNode<Button>("%AddSelectedButton").Pressed += () => AddSelectedCards(1);
        GetNode<Button>("%AddFourButton").Pressed += () => AddSelectedCards(4);

        _deckList.ItemSelected += _ => RefreshDetailFromSelection();
        _deckList.ItemActivated += OnDeckItemActivated;
        GetNode<Button>("%RemoveSelectedButton").Pressed += OnRemoveSelectedPressed;
        GetNode<Button>("%SortDeckButton").Pressed += OnSortDeckPressed;
        GetNode<Button>("%ClearDeckButton").Pressed += OnClearDeckPressed;
        _overlayCloseButton.Pressed += HandleOverlayClose;
    }

    private void BuildUi()
    {
        _background = UiStyles.MakeBackground(_palette);
        _background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_background);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 22);
        margin.AddThemeConstantOverride("margin_bottom", 22);
        AddChild(margin);

        var root = new VBoxContainer();
        root.SizeFlagsVertical = SizeFlags.ExpandFill;
        root.AddThemeConstantOverride("separation", 16);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        root.AddChild(header);

        var headingBox = new VBoxContainer();
        headingBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(headingBox);
        headingBox.AddChild(UiStyles.MakeHeading("Deck Builder", 34, _palette));
        headingBox.AddChild(UiStyles.MakeSubtle("Build a legal 32-card deck, inspect the card pool, and launch a fight when the list is ready.", 17, _palette));

        var menuButton = UiStyles.MakeAccentButton("Menu", _palette);
        menuButton.Pressed += ShowMainMenu;
        header.AddChild(menuButton);

        var toolbar = UiStyles.MakeSurface("", _palette);
        root.AddChild(toolbar);
        var toolbarBody = (VBoxContainer)toolbar.GetChild(0);

        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", 10);
        toolbarBody.AddChild(topRow);

        _deckSelector = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(180, 0) };
        _deckSelector.ItemSelected += OnDeckSelected;
        topRow.AddChild(_deckSelector);

        _deckNameEdit = new LineEdit { PlaceholderText = "Deck Name", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _deckNameEdit.TextSubmitted += text => _session.CurrentDeck.Name = text.Trim();
        topRow.AddChild(_deckNameEdit);

        var newDeckButton = new Button { Text = "New Deck" };
        newDeckButton.Pressed += OnNewDeckPressed;
        topRow.AddChild(newDeckButton);

        var saveDeckButton = new Button { Text = "Save Deck" };
        saveDeckButton.Pressed += OnSaveDeckPressed;
        topRow.AddChild(saveDeckButton);

        _enterCombatButton = UiStyles.MakeAccentButton("Enter Combat", _palette, _palette.Accent);
        _enterCombatButton.Pressed += OnEnterCombatPressed;
        topRow.AddChild(_enterCombatButton);

        var utilityRow = new HBoxContainer();
        utilityRow.AddThemeConstantOverride("separation", 10);
        toolbarBody.AddChild(utilityRow);

        _searchEdit = new LineEdit { PlaceholderText = "Search by name, type, or tag...", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _searchEdit.TextChanged += _ => RefreshPoolList();
        utilityRow.AddChild(_searchEdit);

        _deckCountLabel = UiStyles.MakeMonoValue("0 / 32", 28, _palette);
        _deckCountLabel.CustomMinimumSize = new Vector2(200, 0);
        utilityRow.AddChild(_deckCountLabel);

        var contentRow = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(contentRow);

        var poolPanel = UiStyles.MakeSurface("Card Pool", _palette);
        contentRow.AddChild(poolPanel);
        var poolBody = (VBoxContainer)poolPanel.GetChild(0);
        _selectionHintLabel = UiStyles.MakeSubtle("Ctrl click toggles. Shift click selects ranges. Double-click adds one card.", 14, _palette);
        poolBody.AddChild(_selectionHintLabel);
        _poolList = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Multi,
            AllowReselect = true,
            AllowRmbSelect = true
        };
        _poolList.ItemSelected += _ => RefreshDetailFromSelection();
        _poolList.ItemActivated += OnPoolItemActivated;
        poolBody.AddChild(_poolList);

        var poolActions = new HBoxContainer();
        poolActions.AddThemeConstantOverride("separation", 10);
        poolBody.AddChild(poolActions);
        var addSelectedButton = new Button { Text = "Add Selected" };
        addSelectedButton.Pressed += () => AddSelectedCards(1);
        poolActions.AddChild(addSelectedButton);
        var addFourButton = new Button { Text = "Add 4x Selected" };
        addFourButton.Pressed += () => AddSelectedCards(4);
        poolActions.AddChild(addFourButton);

        var deckPanel = UiStyles.MakeSurface("Current Deck", _palette, true);
        contentRow.AddChild(deckPanel);
        var deckBody = (VBoxContainer)deckPanel.GetChild(0);
        deckBody.AddChild(UiStyles.MakeSubtle("Each row is one copy in order. Multi-select batches to remove them.", 14, _palette));
        _deckList = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Multi,
            AllowReselect = true,
            AllowRmbSelect = true
        };
        _deckList.ItemSelected += _ => RefreshDetailFromSelection();
        _deckList.ItemActivated += OnDeckItemActivated;
        deckBody.AddChild(_deckList);

        var deckActions = new HBoxContainer();
        deckActions.AddThemeConstantOverride("separation", 10);
        deckBody.AddChild(deckActions);
        var removeButton = new Button { Text = "Remove Selected" };
        removeButton.Pressed += OnRemoveSelectedPressed;
        deckActions.AddChild(removeButton);
        var sortButton = new Button { Text = "Sort by Cost" };
        sortButton.Pressed += OnSortDeckPressed;
        deckActions.AddChild(sortButton);
        var clearButton = new Button { Text = "Clear Deck" };
        clearButton.Pressed += OnClearDeckPressed;
        deckActions.AddChild(clearButton);

        var detailPanel = UiStyles.MakeSurface("Card Details", _palette);
        contentRow.AddChild(detailPanel);
        var detailBody = (VBoxContainer)detailPanel.GetChild(0);
        _detailLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ScrollActive = true
        };
        detailBody.AddChild(_detailLabel);

        _statusLabel = UiStyles.MakeSubtle("", 15, _palette);
        root.AddChild(_statusLabel);

        BuildOverlay();
    }

    private void BuildOverlay()
    {
        _overlay = new ColorRect
        {
            Color = new Color(0.01f, 0.02f, 0.01f, 0.86f),
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop
        };
        _overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_overlay);

        _overlayPanel = new PanelContainer
        {
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(760, 520)
        };
        _overlayPanel.SetAnchorsPreset(LayoutPreset.Center);
        _overlayPanel.Position = new Vector2(-380, -260);
        _overlayPanel.Size = new Vector2(760, 520);
        _overlayPanel.AddThemeStyleboxOverride("panel", UiStyles.MakeFlatBox(_palette.SurfaceAlt, 16, _palette.AccentDark.Lightened(0.28f), 2));
        AddChild(_overlayPanel);

        _overlayBody = new VBoxContainer();
        _overlayBody.AddThemeConstantOverride("separation", 14);
        _overlayPanel.AddChild(_overlayBody);
    }

    private void RefreshDeckSelector()
    {
        _deckSelector.Clear();
        for (var i = 0; i < _session.SavedDecks.Count; i++)
        {
            _deckSelector.AddItem(_session.SavedDecks[i].Name, i);
        }

        var selectedIndex = _session.SavedDecks.FindIndex(deck => deck.Name == _session.CurrentDeck.Name);
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }
        if (_session.SavedDecks.Count > 0)
        {
            _deckSelector.Select(selectedIndex);
        }

        _deckNameEdit.Text = _session.CurrentDeck.Name;
        UpdateDeckCountState();
    }

    private void RefreshPoolList()
    {
        var query = _searchEdit.Text?.Trim().ToLowerInvariant() ?? "";
        _poolList.Clear();

        foreach (var card in _session.Content.ListCards())
        {
            if (!string.IsNullOrEmpty(query))
            {
                var haystack = $"{card.Name} {card.Type} {string.Join(' ', card.Tags)}".ToLowerInvariant();
                if (!haystack.Contains(query))
                {
                    continue;
                }
            }

            _poolList.AddItem($"{Shorten(card.Name, 24),-24} {card.CostBits,3}b  {ShortType(card.Type)}");
            _poolList.SetItemMetadata(_poolList.ItemCount - 1, card.Id);
        }
    }

    private void RefreshDeckList()
    {
        _deckList.Clear();
        for (var i = 0; i < _session.CurrentDeck.CardIds.Count; i++)
        {
            var card = _session.Content.GetCard(_session.CurrentDeck.CardIds[i]);
            if (card == null)
            {
                continue;
            }

            _deckList.AddItem($"{i + 1:00} {Shorten(card.Name, 12),-12} {card.CostBits,3}b");
            _deckList.SetItemMetadata(_deckList.ItemCount - 1, i);
        }

        UpdateDeckCountState();
    }

    private void UpdateDeckCountState()
    {
        var count = _session.CurrentDeck.CardIds.Count;
        _deckCountLabel.Text = $"{count} / 32";
        _deckCountLabel.AddThemeColorOverride("font_color", count == 32 ? _palette.Highlight : _palette.TextStrong);
        _enterCombatButton.Disabled = count != 32;
    }

    private static string Shorten(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..System.Math.Max(0, maxLength - 1)] + "…";
    }

    private static string ShortType(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "investment" => "INV",
            "medicate" => "MED",
            "bruiser" => "BRU",
            _ => Shorten(value.ToUpperInvariant(), 3)
        };
    }

    private void RefreshDetailFromSelection()
    {
        CardDefinition? card = null;
        var poolSelection = _poolList.GetSelectedItems();
        var deckSelection = _deckList.GetSelectedItems();
        if (poolSelection.Length > 0)
        {
            card = _session.Content.GetCard((string)_poolList.GetItemMetadata(poolSelection[0]));
        }
        else if (deckSelection.Length > 0)
        {
            var deckIndex = (int)_deckList.GetItemMetadata(deckSelection[0]);
            if (deckIndex >= 0 && deckIndex < _session.CurrentDeck.CardIds.Count)
            {
                card = _session.Content.GetCard(_session.CurrentDeck.CardIds[deckIndex]);
            }
        }

        if (card == null)
        {
            _detailLabel.Text = "[font_size=28][b]No card selected[/b][/font_size]\n\nPick a card from the pool or deck to see its cost, role, and rules text.";
            return;
        }

        var maxCopiesText = card.MaxCopies >= 0 ? card.MaxCopies.ToString() : "Unlimited";
        _detailLabel.Text =
            $"[font_size=30][b]{card.Name}[/b][/font_size]\n" +
            $"[color=#c8f1d4]Cost[/color] [b]{card.CostBits}b[/b]\n" +
            $"[color=#b9cabd]Type[/color] {card.Type}\n" +
            $"[color=#b9cabd]Tags[/color] {string.Join(", ", card.Tags)}\n" +
            $"[color=#b9cabd]Deck Limit[/color] {maxCopiesText}\n\n" +
            $"[font_size=20]{card.Description}[/font_size]";
    }

    private void AddSelectedCards(int copiesPerSelected)
    {
        var selected = _poolList.GetSelectedItems();
        if (selected.Length == 0)
        {
            UpdateStatus("Select one or more cards in the pool first.");
            return;
        }

        var added = 0;
        foreach (var selectedIndex in selected)
        {
            var cardId = (string)_poolList.GetItemMetadata(selectedIndex);
            var card = _session.Content.GetCard(cardId);
            if (card == null)
            {
                continue;
            }

            for (var i = 0; i < copiesPerSelected; i++)
            {
                if (_session.CurrentDeck.CardIds.Count >= 32)
                {
                    break;
                }
                if (card.MaxCopies >= 0 && CountCopies(cardId) >= card.MaxCopies)
                {
                    break;
                }

                _session.CurrentDeck.CardIds.Add(cardId);
                added += 1;
            }
        }

        RefreshDeckList();
        RefreshDetailFromSelection();
        UpdateStatus(added > 0 ? $"Added {added} card(s) to the deck." : "No cards were added. You may be at deck size or copy limits.");
    }

    private void OnRemoveSelectedPressed()
    {
        var selected = _deckList.GetSelectedItems().Cast<int>().OrderDescending().ToList();
        if (selected.Count == 0)
        {
            UpdateStatus("Select one or more cards in the current deck first.");
            return;
        }

        foreach (var itemIndex in selected)
        {
            var deckIndex = (int)_deckList.GetItemMetadata(itemIndex);
            if (deckIndex >= 0 && deckIndex < _session.CurrentDeck.CardIds.Count)
            {
                _session.CurrentDeck.CardIds.RemoveAt(deckIndex);
            }
        }

        RefreshDeckList();
        RefreshDetailFromSelection();
        UpdateStatus($"Removed {selected.Count} card(s) from the deck.");
    }

    private void OnSortDeckPressed()
    {
        _session.CurrentDeck.CardIds = _session.CurrentDeck.CardIds
            .OrderBy(id => _session.Content.GetCard(id)?.CostBits ?? 999)
            .ThenBy(id => _session.Content.GetCard(id)?.Name ?? id)
            .ToList();
        RefreshDeckList();
        UpdateStatus("Sorted the deck by cost, then by name.");
    }

    private void OnClearDeckPressed()
    {
        _session.CurrentDeck.CardIds.Clear();
        RefreshDeckList();
        RefreshDetailFromSelection();
        UpdateStatus("Cleared the current deck.");
    }

    private void OnSaveDeckPressed()
    {
        _session.CurrentDeck.Name = string.IsNullOrWhiteSpace(_deckNameEdit.Text) ? "V1 Deck" : _deckNameEdit.Text.Trim();
        var replaced = false;
        for (var i = 0; i < _session.SavedDecks.Count; i++)
        {
            if (_session.SavedDecks[i].Name != _session.CurrentDeck.Name)
            {
                continue;
            }
            _session.SavedDecks[i] = _session.CurrentDeck.Copy();
            replaced = true;
            break;
        }

        if (!replaced)
        {
            _session.SavedDecks.Add(_session.CurrentDeck.Copy());
        }

        _session.Storage.SaveDecks(_session.SavedDecks);
        RefreshDeckSelector();
        UpdateStatus($"Saved deck \"{_session.CurrentDeck.Name}\".");
    }

    private void OnNewDeckPressed()
    {
        _session.CurrentDeck = new DeckDefinition { Name = "New Deck" };
        _deckNameEdit.Text = _session.CurrentDeck.Name;
        RefreshDeckList();
        RefreshDetailFromSelection();
        UpdateStatus("Started a fresh deck.");
    }

    private void OnDeckSelected(long index)
    {
        if (index < 0 || index >= _session.SavedDecks.Count)
        {
            return;
        }

        _session.CurrentDeck = _session.SavedDecks[(int)index].Copy();
        _deckNameEdit.Text = _session.CurrentDeck.Name;
        RefreshDeckList();
        RefreshDetailFromSelection();
        UpdateStatus($"Loaded deck \"{_session.CurrentDeck.Name}\".");
    }

    private void OnEnterCombatPressed()
    {
        if (_session.CurrentDeck.CardIds.Count != 32)
        {
            UpdateStatus("Decks must contain exactly 32 cards before combat.");
            return;
        }

        StartCombatRequested?.Invoke();
    }

    private void OnPoolItemActivated(long index)
    {
        _poolList.Select((int)index, false);
        AddSelectedCards(1);
    }

    private void OnDeckItemActivated(long index)
    {
        _deckList.Select((int)index, false);
        OnRemoveSelectedPressed();
    }

    private void ShowMainMenu()
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 10);
        body.AddChild(UiStyles.MakeSubtle("Use these in-game documents and settings any time while building.", 16, _palette));

        var tutorialButton = new Button { Text = "Open Tutorial" };
        tutorialButton.Pressed += () => ShowDocument("How V1 Works", BuildTutorialText(), ShowMainMenu);
        body.AddChild(tutorialButton);

        var cardInfoButton = new Button { Text = "Open Card Reference" };
        cardInfoButton.Pressed += () => ShowDocument("Card Reference", BuildCardReferenceText(), ShowMainMenu);
        body.AddChild(cardInfoButton);

        body.AddChild(UiStyles.MakeSubtle("UI Scale", 15, _palette));
        var slider = new HSlider { MinValue = 0.8, MaxValue = 1.5, Step = 0.05, Value = _session.UiScale };
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
            RebuildScreen();
            ShowMainMenu();
        };
        body.AddChild(picker);

        var hackerButton = new Button { Text = _session.HackerMode ? "Hacker Mode: ON" : "Hacker Mode: OFF" };
        hackerButton.Pressed += () =>
        {
            _session.HackerMode = !_session.HackerMode;
            RebuildScreen();
            ShowMainMenu();
        };
        body.AddChild(hackerButton);

        var titleButton = new Button { Text = "Return To Title Screen" };
        titleButton.Pressed += () =>
        {
            HideOverlay();
            ReturnToTitleRequested?.Invoke();
        };
        body.AddChild(titleButton);

        var closeButton = UiStyles.MakeAccentButton("Resume", _palette, _palette.Accent);
        closeButton.Pressed += HideOverlay;
        body.AddChild(closeButton);

        ShowOverlay("Menu", body, HideOverlay);
    }

    private void ShowDocument(string title, string text, System.Action? onClose)
    {
        var document = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ScrollActive = true,
            FitContent = false,
            SelectionEnabled = true,
            CustomMinimumSize = new Vector2(0, 330),
            Text = text
        };

        var wrapper = new VBoxContainer();
        wrapper.SizeFlagsVertical = SizeFlags.ExpandFill;
        wrapper.AddThemeConstantOverride("separation", 12);
        wrapper.AddChild(document);

        var closeButton = UiStyles.MakeAccentButton("Back", _palette, _palette.Accent);
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
        foreach (var child in _overlayContent.GetChildren())
        {
            child.QueueFree();
        }

        _overlayTitleLabel.Text = title;
        _overlayContent.AddChild(content);

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

    private string BuildTutorialText()
    {
        return "[font_size=28][b]V1 Tutorial[/b][/font_size]\n\n" +
               "[b]Deck Builder[/b]\nCreate a deck with exactly 32 cards. Use Ctrl/Shift click in the pool or deck lists to select batches. Double-click adds or removes one card quickly.\n\n" +
               "[b]Combat Flow[/b]\nCombat starts with 6 cards in hand and your configured starting bits. Each round: start-of-turn effects resolve, bits income is gained, cards are drawn, you play cards, the enemy acts, then the round ends.\n\n" +
               "[b]Bits Economy[/b]\nV1 only tracks bits in combat. Income follows the formula (B + A) x (1 + M + V). B is base income, A is flat bonus, M is multiplier bonus, and V is stock-like volatility.\n\n" +
               "[b]Cards[/b]\nInvestment cards improve future turns. Medicate cards stabilize health or incoming damage. Bruiser cards deal direct or conditional damage. Some cards, like Expose, require a second card to sacrifice.\n\n" +
               "[b]Goal[/b]\nReduce the enemy to 0 HP before your own HP reaches 0.";
    }

    private string BuildCardReferenceText()
    {
        var builder = new StringBuilder();
        builder.Append("[font_size=28][b]Card Reference[/b][/font_size]\n\n");
        foreach (var card in _session.Content.ListCards())
        {
            builder.Append($"[b]{card.Name}[/b]  [color=#c8f1d4]{card.CostBits}b[/color]  [color=#b9cabd]{card.Type}[/color]\n");
            builder.Append($"{card.Description}\n\n");
        }
        return builder.ToString();
    }

    private void UpdateStatus(string message)
    {
        _statusLabel.Text = message;
    }

    private int CountCopies(string cardId) => _session.CurrentDeck.CardIds.Count(existing => existing == cardId);
}
