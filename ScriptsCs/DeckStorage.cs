#nullable enable
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace GodotGdc.V1;

public sealed class DeckStorage
{
    private const string SavePath = "user://decks_v1.json";

    public List<DeckDefinition> LoadDecks(DeckDefinition defaultDeck)
    {
        if (!FileAccess.FileExists(SavePath))
        {
            return new List<DeckDefinition> { defaultDeck.Copy() };
        }

        var raw = FileAccess.GetFileAsString(SavePath);
        var decks = JsonSerializer.Deserialize<List<DeckDefinition>>(raw, V1Json.Options);
        if (decks == null || decks.Count == 0)
        {
            return new List<DeckDefinition> { defaultDeck.Copy() };
        }

        return decks;
    }

    public void SaveDecks(List<DeckDefinition> decks)
    {
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        file.StoreString(JsonSerializer.Serialize(decks, V1Json.Options));
    }
}
