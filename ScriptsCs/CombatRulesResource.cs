#nullable enable
using Godot;

namespace GodotGdc.V1;

[GlobalClass]
public partial class CombatRulesResource : Resource
{
    [Export] public int StartingBits { get; set; } = 100;
    [Export] public int BasicIncome { get; set; } = 10;
    [Export] public int CardsDrawnPerRound { get; set; } = 1;
    [Export] public int PlayerStartingHp { get; set; } = 100;
    [Export] public int EnemyStartingHp { get; set; } = 100;
    [Export] public float StockVolatilityMin { get; set; } = -0.10f;
    [Export] public float StockVolatilityMax { get; set; } = 0.20f;
    [Export] public int BuyLowStackCap { get; set; } = 2;
    [Export] public int SellHighStackCap { get; set; } = 2;
}
