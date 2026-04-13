#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GodotGdc.V1;

public sealed class CardCatalogData
{
    [JsonPropertyName("cards")]
    public List<CardDefinition> Cards { get; set; } = new();
}

public sealed class EnemyCatalogData
{
    [JsonPropertyName("enemies")]
    public List<EnemyDefinition> Enemies { get; set; } = new();
}

public sealed class CardDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "utility";

    [JsonPropertyName("cost_bits")]
    public int CostBits { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("max_copies")]
    public int MaxCopies { get; set; } = -1;

    [JsonPropertyName("effects")]
    public List<CardEffectSpec> Effects { get; set; } = new();

    public bool RequiresSecondaryCard =>
        Effects.Any(effect => effect.Operation == "custom" && V1Json.GetString(effect.Params, "id") == "expose");
}

public sealed class CardEffectSpec
{
    [JsonPropertyName("trigger")]
    public string Trigger { get; set; } = "on_play";

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = "";

    [JsonPropertyName("target")]
    public string Target { get; set; } = "enemy";

    [JsonPropertyName("params")]
    public Dictionary<string, JsonElement> Params { get; set; } = new();
}

public sealed class DeckDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "V1 Deck";

    [JsonPropertyName("card_ids")]
    public List<string> CardIds { get; set; } = new();

    public bool IsValid(int exactSize) => CardIds.Count == exactSize;

    public DeckDefinition Copy() => new()
    {
        Name = Name,
        CardIds = new List<string>(CardIds)
    };
}

public sealed class EnemyDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("max_health")]
    public int MaxHealth { get; set; } = 100;

    [JsonPropertyName("actions")]
    public List<EnemyActionDefinition> Actions { get; set; } = new();
}

public sealed class EnemyActionDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("base_weight")]
    public float BaseWeight { get; set; } = 1.0f;

    [JsonPropertyName("conditions")]
    public Dictionary<string, JsonElement> Conditions { get; set; } = new();

    [JsonPropertyName("effects")]
    public List<EnemyEffectSpec> Effects { get; set; } = new();
}

public sealed class EnemyEffectSpec
{
    [JsonPropertyName("operation")]
    public string Operation { get; set; } = "";

    [JsonPropertyName("amount")]
    public float Amount { get; set; }

    [JsonPropertyName("ratio")]
    public float Ratio { get; set; }
}

public sealed class HandCardView
{
    public int Index { get; init; }
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public int Cost { get; init; }
    public string Description { get; init; } = "";
    public bool Playable { get; init; }
    public bool RequiresSecondary { get; init; }
}

public sealed class EconomyView
{
    public float BaseIncome { get; init; }
    public float FlatBonus { get; init; }
    public float MultiplierBonus { get; init; }
    public int VStacks { get; init; }
    public float CostReduction { get; init; }
}

public sealed class CombatSnapshot
{
    public bool CombatActive { get; init; }
    public bool PlayerTurnActive { get; init; }
    public int Round { get; init; }
    public int PlayerHp { get; init; }
    public int EnemyHp { get; init; }
    public int PlayerBits { get; init; }
    public int CurrentTurnIncome { get; init; }
    public int LastIncomeGained { get; init; }
    public string DeckName { get; init; } = "No Deck";
    public string EnemyName { get; init; } = "No Enemy";
    public string LastEnemyAction { get; init; } = "None";
    public int DrawPileCount { get; init; }
    public int DiscardPileCount { get; init; }
    public EconomyView Economy { get; init; } = new();
    public List<HandCardView> Hand { get; init; } = new();
}

public static class V1Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static string GetString(Dictionary<string, JsonElement>? map, string key, string fallback = "")
    {
        if (map == null || !map.TryGetValue(key, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? fallback,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => fallback
        };
    }

    public static float GetFloat(Dictionary<string, JsonElement>? map, string key, float fallback = 0.0f)
    {
        if (map == null || !map.TryGetValue(key, out var value))
        {
            return fallback;
        }

        return value.ValueKind == JsonValueKind.Number ? value.GetSingle() : fallback;
    }

    public static int GetInt(Dictionary<string, JsonElement>? map, string key, int fallback = 0)
    {
        if (map == null || !map.TryGetValue(key, out var value))
        {
            return fallback;
        }

        return value.ValueKind == JsonValueKind.Number ? value.GetInt32() : fallback;
    }
}
