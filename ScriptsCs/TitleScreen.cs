#nullable enable
using Godot;

namespace GodotGdc.V1;

public partial class TitleScreen : Control
{
    private Control _achievementsPage = null!;
    private Control _infoPage = null!;
    private Control _settingsPage = null!;

    public event System.Action? StartGameRequested;
    public event System.Action? DeckBuilderRequested;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var title = GetNode<Label>("%AsciiTitleLabel");
        title.AddThemeFontOverride("font", UiStyles.GetMonoFont());

        GetNode<Button>("%StartGameButton").Pressed += () => StartGameRequested?.Invoke();
        GetNode<Button>("%DeckBuilderButton").Pressed += () => DeckBuilderRequested?.Invoke();
        GetNode<Button>("%AchievementsButton").Pressed += () => ShowPage(_achievementsPage);
        GetNode<Button>("%SettingsButton").Pressed += () => ShowPage(_settingsPage);
        GetNode<Button>("%InformationButton").Pressed += () => ShowPage(_infoPage);
        GetNode<Button>("%QuitButton").Pressed += () => GetTree().Quit();

        _achievementsPage = GetNode<Control>("%AchievementsPage");
        _infoPage = GetNode<Control>("%InformationPage");
        _settingsPage = GetNode<Control>("%SettingsPage");

        foreach (var button in GetTree().GetNodesInGroup("title_page_close"))
        {
            if (button is Button closeButton)
            {
                closeButton.Pressed += HidePages;
            }
        }

        HidePages();
    }

    private void ShowPage(Control page)
    {
        HidePages();
        page.Visible = true;
    }

    private void HidePages()
    {
        _achievementsPage.Visible = false;
        _infoPage.Visible = false;
        _settingsPage.Visible = false;
    }
}
