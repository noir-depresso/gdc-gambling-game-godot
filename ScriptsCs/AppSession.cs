#nullable enable
using System.Collections.Generic;
using Godot;

namespace GodotGdc.V1;

public sealed class AppSession
{
    public CombatRulesResource Rules { get; init; } = null!;
    public ContentLibrary Content { get; init; } = null!;
    public DeckStorage Storage { get; init; } = null!;
    public List<DeckDefinition> SavedDecks { get; init; } = new();
    public DeckDefinition CurrentDeck { get; set; } = new();
    public float UiScale { get; set; } = 1.0f;
    public Color AccentColor { get; set; } = new Color(0.32f, 0.82f, 0.47f);
    public bool HackerMode { get; set; }
}
