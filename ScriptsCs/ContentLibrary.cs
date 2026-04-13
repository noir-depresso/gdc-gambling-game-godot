#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace GodotGdc.V1;

public sealed class ContentLibrary
{
    private const string CardsPath = "res://data/cards_v1.json";
    private const string EnemiesPath = "res://data/enemies_v1.json";
    private const string DefaultDeckPath = "res://data/default_deck_v1.json";

    private readonly Dictionary<string, CardDefinition> _cardsById = new();
    private readonly List<string> _cardOrder = new();
    private readonly Dictionary<string, EnemyDefinition> _enemiesById = new();

    public DeckDefinition DefaultDeck { get; private set; } = new();

    public void LoadAll()
    {
        _cardsById.Clear();
        _cardOrder.Clear();
        _enemiesById.Clear();

        var cards = JsonSerializer.Deserialize<CardCatalogData>(FileAccess.GetFileAsString(CardsPath), V1Json.Options) ?? new CardCatalogData();
        foreach (var card in cards.Cards)
        {
            _cardsById[card.Id] = card;
            _cardOrder.Add(card.Id);
        }

        var enemies = JsonSerializer.Deserialize<EnemyCatalogData>(FileAccess.GetFileAsString(EnemiesPath), V1Json.Options) ?? new EnemyCatalogData();
        foreach (var enemy in enemies.Enemies)
        {
            _enemiesById[enemy.Id] = enemy;
        }

        DefaultDeck = JsonSerializer.Deserialize<DeckDefinition>(FileAccess.GetFileAsString(DefaultDeckPath), V1Json.Options) ?? new DeckDefinition();
    }

    public CardDefinition? GetCard(string cardId) => _cardsById.TryGetValue(cardId, out var card) ? card : null;

    public EnemyDefinition? GetEnemy(string enemyId) => _enemiesById.TryGetValue(enemyId, out var enemy) ? enemy : null;

    public List<CardDefinition> ListCards() => _cardOrder.Select(id => _cardsById[id]).ToList();
}
