#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace GodotGdc.V1;

public sealed class CombatController
{
    private sealed class TimedEffect
    {
        public string EventName { get; init; } = "";
        public int RoundsLeft { get; set; }
        public string Id { get; init; } = "";
        public float Amount { get; init; }
        public int Damage { get; init; }
        public int Heal { get; init; }
        public int Count { get; init; }
    }

    private readonly RandomNumberGenerator _rng = new();
    private readonly List<string> _drawPile = new();
    private readonly List<string> _hand = new();
    private readonly List<string> _discardPile = new();
    private readonly List<TimedEffect> _timedEffects = new();

    private ContentLibrary? _content;
    private CombatRulesResource? _rules;
    private DeckDefinition? _activeDeck;
    private EnemyDefinition? _activeEnemy;
    private EnemyActionDefinition? _previewEnemyAction;

    private bool _combatActive;
    private bool _playerTurnActive;
    private int _roundNumber;
    private int _playerHp;
    private int _enemyHp;
    private int _playerBits;
    private int _lastIncomeGained;
    private int _currentTurnIncome;
    private int _runStartingBitsBonus;
    private int _runBaseIncomeBonus;
    private string _lastEnemyActionLabel = "None";
    private string _lastEnemyActionId = "";

    private float _economyAdd;
    private int _bankStacks;
    private int _sellHighStacks;
    private int _stockStacks;
    private int _buyLowStacks;
    private float _costReduction;
    private int _socialPressureStacks;

    private float _tempIncomeAdd;
    private float _tempIncomeMult;
    private float _tempIncomeVol;
    private float _playerDamageBonusActive;
    private float _enemyAttackMultTemp;
    private float _playerIncomingReductionTemp;
    private float _playerReflectTemp;
    private float _enemyGuardRatioTemp;

    public event Action<CombatSnapshot>? SnapshotChanged;
    public event Action<string>? LogAdded;
    public event Action<bool>? CombatEnded;
    public event Action? CombatStarted;

    public CombatController()
    {
        _rng.Randomize();
    }

    public void Setup(ContentLibrary content, CombatRulesResource rules)
    {
        _content = content;
        _rules = rules;
        _activeDeck = content.DefaultDeck.Copy();
        _activeEnemy = content.GetEnemy("street_enforcer");
        EmitSnapshot();
    }

    public void SetActiveDeck(DeckDefinition deck)
    {
        _activeDeck = deck.Copy();
        EmitSnapshot();
    }

    public void SetActiveEnemy(string enemyId)
    {
        if (_content == null)
        {
            return;
        }

        _activeEnemy = _content.GetEnemy(enemyId);
        EmitSnapshot();
    }

    public void ConfigureRunBonuses(int startingBitsBonus, int baseIncomeBonus)
    {
        _runStartingBitsBonus = startingBitsBonus;
        _runBaseIncomeBonus = baseIncomeBonus;
        EmitSnapshot();
    }

    public List<(string CardId, int Count)> GetRemainingDeckCounts()
    {
        var counts = new Dictionary<string, int>();
        foreach (var cardId in _drawPile)
        {
            counts[cardId] = counts.TryGetValue(cardId, out var count) ? count + 1 : 1;
        }

        var rows = new List<(string CardId, int Count)>();
        foreach (var entry in counts)
        {
            rows.Add((entry.Key, entry.Value));
        }

        rows.Sort((a, b) => string.CompareOrdinal(a.CardId, b.CardId));
        return rows;
    }

    public List<(string CardId, int Count)> GetDiscardCounts()
    {
        var counts = new Dictionary<string, int>();
        foreach (var cardId in _discardPile)
        {
            counts[cardId] = counts.TryGetValue(cardId, out var count) ? count + 1 : 1;
        }

        var rows = new List<(string CardId, int Count)>();
        foreach (var entry in counts)
        {
            rows.Add((entry.Key, entry.Value));
        }

        rows.Sort((a, b) => string.CompareOrdinal(a.CardId, b.CardId));
        return rows;
    }

    public CombatSnapshot GetSnapshot() => BuildSnapshot();

    public void StartCombat()
    {
        if (_rules == null || _content == null || _activeDeck == null)
        {
            return;
        }

        if (_activeDeck.CardIds.Count < 32)
        {
            Log("Deck must contain at least 32 cards before combat starts.");
            return;
        }

        if (_activeEnemy == null)
        {
            Log("No enemy selected.");
            return;
        }

        ResetCombatState();
        _drawPile.AddRange(_activeDeck.CardIds);
        Shuffle(_drawPile);
        DrawCards(6);
        _playerBits = _rules.StartingBits + _runStartingBitsBonus;
        _playerHp = _rules.PlayerStartingHp;
        _enemyHp = _activeEnemy.MaxHealth > 0 ? _activeEnemy.MaxHealth : _rules.EnemyStartingHp;
        _combatActive = true;
        Log($"Combat started against {_activeEnemy.Name}.");
        CombatStarted?.Invoke();
        BeginPlayerTurn();
    }

    public void PlayCard(int handIndex, int? sacrificeIndex = null)
    {
        if (!_combatActive || !_playerTurnActive || _content == null)
        {
            Log("You can only play cards during your turn.");
            return;
        }

        if (handIndex < 0 || handIndex >= _hand.Count)
        {
            Log("Invalid hand selection.");
            return;
        }

        var cardId = _hand[handIndex];
        var card = _content.GetCard(cardId);
        if (card == null)
        {
            Log($"Missing card definition for {cardId}.");
            return;
        }

        var cost = GetEffectiveCost(card);
        if (_playerBits < cost)
        {
            Log($"Not enough bits to play {card.Name}.");
            return;
        }

        if (card.RequiresSecondaryCard)
        {
            if (sacrificeIndex == null || sacrificeIndex.Value < 0 || sacrificeIndex.Value >= _hand.Count || sacrificeIndex.Value == handIndex)
            {
                Log($"{card.Name} needs another card from hand.");
                return;
            }
        }

        _playerBits -= cost;
        var adjustedSacrificeIndex = sacrificeIndex;
        if (adjustedSacrificeIndex.HasValue && adjustedSacrificeIndex.Value > handIndex)
        {
            adjustedSacrificeIndex -= 1;
        }

        _hand.RemoveAt(handIndex);
        var returnToHand = false;
        foreach (var effect in card.Effects)
        {
            returnToHand |= ResolveCardEffect(effect, adjustedSacrificeIndex);
            if (!_combatActive)
            {
                break;
            }
        }

        if (_combatActive)
        {
            if (returnToHand)
            {
                _hand.Add(cardId);
            }
            else
            {
                _discardPile.Add(cardId);
            }
        }

        Log($"Played {card.Name} for {cost} bits.");
        EmitSnapshot();
    }

    public void EndPlayerTurn()
    {
        if (!_combatActive || !_playerTurnActive)
        {
            return;
        }

        _playerTurnActive = false;
        ResolveTimedEffects("end_player_turn");
        if (!_combatActive)
        {
            return;
        }

        EnemyPhase();
        if (_combatActive)
        {
            BeginPlayerTurn();
        }
    }

    private void BeginPlayerTurn()
    {
        if (_rules == null)
        {
            return;
        }

        _playerTurnActive = true;
        _roundNumber += 1;
        _tempIncomeAdd = 0.0f;
        _tempIncomeMult = 0.0f;
        _tempIncomeVol = 0.0f;
        _playerDamageBonusActive = 0.0f;
        _enemyAttackMultTemp = 0.0f;
        _playerIncomingReductionTemp = 0.0f;
        _playerReflectTemp = 0.0f;

        ResolveTimedEffects("start_player_turn");
        _currentTurnIncome = GainBitsIncome();
        DrawCards(_rules.CardsDrawnPerRound);
        _previewEnemyAction = ChooseEnemyAction();
        Log($"Round {_roundNumber} starts. Gained {_currentTurnIncome} bits.");
        EmitSnapshot();
    }

    private void EnemyPhase()
    {
        ResolveTimedEffects("before_enemy_action");
        if (!_combatActive || _activeEnemy == null)
        {
            return;
        }

        var action = _previewEnemyAction ?? ChooseEnemyAction();
        if (action == null)
        {
            Log($"{_activeEnemy.Name} hesitates.");
            ResolveTimedEffects("end_round");
            EmitSnapshot();
            return;
        }

        _lastEnemyActionId = action.Id;
        _lastEnemyActionLabel = action.Label;
        _previewEnemyAction = null;
        Log($"{_activeEnemy.Name} uses {action.Label}.");
        foreach (var effect in action.Effects)
        {
            ApplyEnemyEffect(effect);
            if (!_combatActive)
            {
                break;
            }
        }

        ResolveTimedEffects("after_enemy_action");
        ResolveTimedEffects("end_round");
        EmitSnapshot();
    }

    private void ResetCombatState()
    {
        _combatActive = false;
        _playerTurnActive = false;
        _roundNumber = 0;
        _playerHp = _rules?.PlayerStartingHp ?? 100;
        _enemyHp = _rules?.EnemyStartingHp ?? 100;
        _playerBits = 0;
        _lastIncomeGained = 0;
        _currentTurnIncome = 0;
        _lastEnemyActionLabel = "None";
        _lastEnemyActionId = "";
        _previewEnemyAction = null;

        _drawPile.Clear();
        _hand.Clear();
        _discardPile.Clear();
        _timedEffects.Clear();

        _economyAdd = 0.0f;
        _bankStacks = 0;
        _sellHighStacks = 0;
        _stockStacks = 0;
        _buyLowStacks = 0;
        _costReduction = 0.0f;
        _socialPressureStacks = 0;

        _tempIncomeAdd = 0.0f;
        _tempIncomeMult = 0.0f;
        _tempIncomeVol = 0.0f;
        _playerDamageBonusActive = 0.0f;
        _enemyAttackMultTemp = 0.0f;
        _playerIncomingReductionTemp = 0.0f;
        _playerReflectTemp = 0.0f;
        _enemyGuardRatioTemp = 0.0f;
    }

    private void DrawCards(int amount)
    {
        for (var i = 0; i < amount; i++)
        {
            if (_drawPile.Count == 0)
            {
                if (_discardPile.Count == 0)
                {
                    return;
                }

                _drawPile.AddRange(_discardPile);
                _discardPile.Clear();
                Shuffle(_drawPile);
                Log("Reshuffled the discard pile into the deck.");
            }

            var nextCard = _drawPile[^1];
            _drawPile.RemoveAt(_drawPile.Count - 1);
            _hand.Add(nextCard);
        }
    }

    private int GainBitsIncome()
    {
        if (_rules == null)
        {
            return 0;
        }

        var bankBonus = _bankStacks * 0.05f;
        var sellBonus = _sellHighStacks * 0.25f;
        var stockBonus = 0.0f;
        for (var i = 0; i < _stockStacks; i++)
        {
            stockBonus += _rng.RandfRange(_rules.StockVolatilityMin, _rules.StockVolatilityMax);
        }

        var addValue = _rules.BasicIncome + _runBaseIncomeBonus + _economyAdd + _tempIncomeAdd;
        var multValue = bankBonus + sellBonus + _tempIncomeMult;
        var volatilityValue = stockBonus + _tempIncomeVol;
        var income = Mathf.Max(0, Mathf.RoundToInt(addValue * (1.0f + multValue + volatilityValue)));
        _playerBits += income;
        _lastIncomeGained = income;
        return income;
    }

    private bool ResolveCardEffect(CardEffectSpec effect, int? sacrificeIndex)
    {
        switch (effect.Operation)
        {
            case "damage":
                ApplyDamageToEnemy(V1Json.GetFloat(effect.Params, "amount"));
                return false;
            case "heal":
                HealPlayer(V1Json.GetInt(effect.Params, "amount"));
                return false;
            case "draw":
                DrawCards(V1Json.GetInt(effect.Params, "amount", 1));
                return false;
            case "modify_bits_income_mult":
            {
                var stackKey = V1Json.GetString(effect.Params, "stack_key");
                if (stackKey == "bank")
                {
                    _bankStacks += 1;
                }
                else if (stackKey == "sell" && _rules != null)
                {
                    _sellHighStacks = Mathf.Min(_sellHighStacks + 1, _rules.SellHighStackCap);
                }

                return false;
            }
            case "modify_cost":
                if (_rules != null)
                {
                    _buyLowStacks = Mathf.Min(_buyLowStacks + 1, _rules.BuyLowStackCap);
                    _costReduction = _buyLowStacks * V1Json.GetFloat(effect.Params, "amount_per_stack", 0.2f);
                }
                return false;
            case "custom":
                return ResolveCustomEffect(V1Json.GetString(effect.Params, "id"), effect.Params, sacrificeIndex);
            default:
                Log($"Unhandled effect operation: {effect.Operation}");
                return false;
        }
    }

    private bool ResolveCustomEffect(string customId, Dictionary<string, System.Text.Json.JsonElement> parameters, int? sacrificeIndex)
    {
        switch (customId)
        {
            case "crypto":
            {
                var bonus = Mathf.RoundToInt(_lastIncomeGained * V1Json.GetFloat(parameters, "carry_percent", 0.5f));
                ScheduleEffect("start_player_turn", 1, "income_add_temp", bonus);
                Log($"Crypto will add {bonus} bits next turn.");
                return false;
            }
            case "stocks":
                _stockStacks += 1;
                return false;
            case "real_estate":
            {
                var gain = _rng.RandiRange(V1Json.GetInt(parameters, "min", 2), V1Json.GetInt(parameters, "max", 8));
                _economyAdd += gain;
                Log($"Real Estate raised base income by {gain}.");
                return false;
            }
            case "hedging":
                ScheduleEffect("before_enemy_action", 1, "incoming_reduction_temp", V1Json.GetFloat(parameters, "block", 0.75f));
                ScheduleEffect("start_player_turn", 1, "income_mult_temp", -Mathf.Abs(V1Json.GetFloat(parameters, "income_penalty", 0.5f)));
                return false;
            case "chaos":
            {
                var redraw = _hand.Count;
                _discardPile.AddRange(_hand);
                _hand.Clear();
                DrawCards(redraw);
                return false;
            }
            case "guard_down":
                ScheduleEffect("before_enemy_action", 1, "enemy_attack_mult_temp", -Mathf.Abs(V1Json.GetFloat(parameters, "amount", 0.3f)));
                return false;
            case "enchantment":
                ScheduleEffect("start_player_turn", 1, "player_damage_bonus_temp", V1Json.GetFloat(parameters, "amount", 0.3f));
                return false;
            case "social_pressure":
                ApplyDamageToEnemy(V1Json.GetFloat(parameters, "base", 10.0f) + (V1Json.GetFloat(parameters, "per_stack", 10.0f) * _socialPressureStacks));
                _socialPressureStacks += 1;
                return false;
            case "expose":
                if (!sacrificeIndex.HasValue || sacrificeIndex.Value < 0 || sacrificeIndex.Value >= _hand.Count)
                {
                    Log("Expose fizzled because it had no sacrifice.");
                    return false;
                }

                _discardPile.Add(_hand[sacrificeIndex.Value]);
                _hand.RemoveAt(sacrificeIndex.Value);
                ApplyDamageToEnemy(V1Json.GetFloat(parameters, "amount", 75.0f));
                return false;
            case "hired_gun":
                ApplyDamageToEnemy(V1Json.GetFloat(parameters, "amount", 20.0f));
                return true;
            case "trauma_team":
                ScheduleEffect("after_enemy_action", 1, "trauma_team_check", damage: V1Json.GetInt(parameters, "damage", 50), heal: V1Json.GetInt(parameters, "heal", 15));
                return false;
            case "coin_flip":
                ApplyDamageToEnemy(V1Json.GetFloat(parameters, "amount", 20.0f) * (_rng.Randf() >= 0.5f ? 2.0f : 0.5f));
                return false;
            case "raan":
                ScheduleEffect("start_player_turn", _rng.RandiRange(2, 3), "draw_cards", count: V1Json.GetInt(parameters, "amount", 1));
                Log("Raan is circulating. Extra draw queued in a few turns.");
                return false;
            case "firewall":
                ScheduleEffect("before_enemy_action", 1, "reflect_temp", amount: V1Json.GetFloat(parameters, "amount", 0.25f));
                return false;
            case "suture_kit":
                HealPlayer(V1Json.GetInt(parameters, "heal", 25));
                ScheduleEffect("before_enemy_action", 1, "incoming_reduction_temp", V1Json.GetFloat(parameters, "block", 0.95f));
                return false;
            case "release_files":
                if (_rules != null && _playerHp <= Mathf.RoundToInt(_rules.PlayerStartingHp * V1Json.GetFloat(parameters, "hp_threshold", 0.2f)))
                {
                    ScheduleEffect("start_player_turn", 1, "player_damage_bonus_temp", V1Json.GetFloat(parameters, "amount", 1.0f));
                    Log("Release the Files primes a desperate spike for next turn.");
                }
                else
                {
                    Log("Release the Files needs low health to trigger.");
                }
                return false;
            case "roulette":
            {
                var outcome = _rng.RandiRange(0, 2);
                if (outcome == 0)
                {
                    ApplyDamageToPlayer(V1Json.GetFloat(parameters, "self_damage", 12.0f));
                    Log("Roulette backfires.");
                }
                else if (outcome == 1)
                {
                    HealPlayer(V1Json.GetInt(parameters, "heal", 18));
                    Log("Roulette lands on recovery.");
                }
                else
                {
                    ApplyDamageToEnemy(V1Json.GetFloat(parameters, "enemy_damage", 30.0f));
                    Log("Roulette lands on violence.");
                }
                return false;
            }
            default:
                Log($"Unhandled custom effect: {customId}");
                return false;
        }
    }

    private void ScheduleEffect(string eventName, int roundsUntilTrigger, string id, float amount = 0.0f, int damage = 0, int heal = 0, int count = 0)
    {
        _timedEffects.Add(new TimedEffect
        {
            EventName = eventName,
            RoundsLeft = roundsUntilTrigger,
            Id = id,
            Amount = amount,
            Damage = damage,
            Heal = heal,
            Count = count
        });
    }

    private void ResolveTimedEffects(string eventName)
    {
        var remaining = new List<TimedEffect>();
        foreach (var effect in _timedEffects)
        {
            if (effect.EventName != eventName)
            {
                remaining.Add(effect);
                continue;
            }

            effect.RoundsLeft -= 1;
            if (effect.RoundsLeft > 0)
            {
                remaining.Add(effect);
                continue;
            }

            ExecuteTimedEffect(effect);
            if (!_combatActive && eventName != "start_player_turn")
            {
                break;
            }
        }

        _timedEffects.Clear();
        _timedEffects.AddRange(remaining);
    }

    private void ExecuteTimedEffect(TimedEffect effect)
    {
        switch (effect.Id)
        {
            case "income_add_temp":
                _tempIncomeAdd += effect.Amount;
                break;
            case "income_mult_temp":
                _tempIncomeMult += effect.Amount;
                break;
            case "player_damage_bonus_temp":
                _playerDamageBonusActive += effect.Amount;
                break;
            case "enemy_attack_mult_temp":
                _enemyAttackMultTemp += effect.Amount;
                break;
            case "incoming_reduction_temp":
                _playerIncomingReductionTemp = Mathf.Max(_playerIncomingReductionTemp, effect.Amount);
                break;
            case "reflect_temp":
                _playerReflectTemp = Mathf.Max(_playerReflectTemp, effect.Amount);
                break;
            case "draw_cards":
                DrawCards(Mathf.Max(1, effect.Count));
                Log($"Drew {Mathf.Max(1, effect.Count)} bonus card.");
                break;
            case "trauma_team_check":
                if (LastEnemyActionWillAttack())
                {
                    ApplyDamageToEnemy(effect.Damage);
                    Log("Trauma Team punishes the attack.");
                }
                else
                {
                    HealPlayer(effect.Heal);
                    Log("Trauma Team stabilizes you.");
                }
                break;
            default:
                Log($"Unknown timed effect: {effect.Id}");
                break;
        }
    }

    private void ApplyDamageToEnemy(float amount)
    {
        var finalDamage = Mathf.RoundToInt(amount * (1.0f + _playerDamageBonusActive));
        if (_enemyGuardRatioTemp > 0.0f)
        {
            finalDamage = Mathf.RoundToInt(finalDamage * (1.0f - _enemyGuardRatioTemp));
            _enemyGuardRatioTemp = 0.0f;
        }

        finalDamage = Mathf.Max(0, finalDamage);
        _enemyHp -= finalDamage;
        Log($"Enemy takes {finalDamage} damage.");
        CheckForCombatEnd();
    }

    private void ApplyDamageToPlayer(float amount)
    {
        var attackMultiplier = Mathf.Max(0.0f, 1.0f + _enemyAttackMultTemp);
        var reduction = Mathf.Clamp(_playerIncomingReductionTemp, 0.0f, 0.95f);
        var finalDamage = Mathf.Max(0, Mathf.RoundToInt(amount * attackMultiplier * (1.0f - reduction)));
        _playerHp -= finalDamage;
        Log($"Player takes {finalDamage} damage.");

        if (_playerReflectTemp > 0.0f)
        {
            var reflected = Mathf.RoundToInt(finalDamage * _playerReflectTemp);
            if (reflected > 0)
            {
                _enemyHp -= reflected;
                Log($"Reflected {reflected} damage back.");
            }
        }

        _enemyAttackMultTemp = 0.0f;
        _playerIncomingReductionTemp = 0.0f;
        _playerReflectTemp = 0.0f;
        CheckForCombatEnd();
    }

    private void HealPlayer(int amount)
    {
        if (_rules == null)
        {
            return;
        }

        _playerHp = Mathf.Min(_rules.PlayerStartingHp, _playerHp + amount);
        Log($"Recovered {amount} health.");
    }

    private EnemyActionDefinition? ChooseEnemyAction()
    {
        if (_activeEnemy == null || _rules == null)
        {
            return null;
        }

        var weighted = new List<(EnemyActionDefinition Action, float Weight)>();
        foreach (var action in _activeEnemy.Actions)
        {
            var weight = GetActionWeight(action);
            if (weight > 0.0f)
            {
                weighted.Add((action, weight));
            }
        }

        if (weighted.Count == 0)
        {
            return null;
        }

        var totalWeight = 0.0f;
        foreach (var item in weighted)
        {
            totalWeight += item.Weight;
        }

        var roll = _rng.Randf() * totalWeight;
        foreach (var item in weighted)
        {
            roll -= item.Weight;
            if (roll <= 0.0f)
            {
                return item.Action;
            }
        }

        return weighted[^1].Action;
    }

    private float GetActionWeight(EnemyActionDefinition action)
    {
        if (_activeEnemy == null || _rules == null)
        {
            return 0.0f;
        }

        var weight = action.BaseWeight;
        var conditions = action.Conditions;
        if (conditions.TryGetValue("avoid_repeat", out var avoidRepeat) && avoidRepeat.ValueKind == System.Text.Json.JsonValueKind.True && action.Id == _lastEnemyActionId)
        {
            weight *= 0.35f;
        }

        if (conditions.TryGetValue("min_round", out var minRound) && _roundNumber < minRound.GetInt32())
        {
            return 0.0f;
        }

        if (conditions.TryGetValue("max_round", out var maxRound) && _roundNumber > maxRound.GetInt32())
        {
            return 0.0f;
        }

        var enemyHpPct = _enemyHp / Mathf.Max(1.0f, _activeEnemy.MaxHealth);
        var playerHpPct = _playerHp / Mathf.Max(1.0f, _rules.PlayerStartingHp);

        if (conditions.TryGetValue("enemy_hp_below", out var enemyHpBelow) && enemyHpPct >= enemyHpBelow.GetSingle())
        {
            return 0.0f;
        }

        if (conditions.TryGetValue("enemy_hp_above", out var enemyHpAbove) && enemyHpPct <= enemyHpAbove.GetSingle())
        {
            return 0.0f;
        }

        if (conditions.TryGetValue("player_hp_below", out var playerHpBelow) && playerHpPct >= playerHpBelow.GetSingle())
        {
            return 0.0f;
        }

        if (conditions.TryGetValue("player_hp_above", out var playerHpAbove) && playerHpPct <= playerHpAbove.GetSingle())
        {
            return 0.0f;
        }

        if (conditions.TryGetValue("player_bits_above", out var bitsAbove) && _playerBits <= bitsAbove.GetInt32())
        {
            return 0.0f;
        }

        if (conditions.TryGetValue("weight_if_enemy_low", out var enemyLowBonus) && enemyHpPct < 0.5f)
        {
            weight += enemyLowBonus.GetSingle();
        }

        if (conditions.TryGetValue("weight_if_player_low", out var playerLowBonus) && playerHpPct < 0.4f)
        {
            weight += playerLowBonus.GetSingle();
        }

        if (conditions.TryGetValue("weight_if_round_high", out var roundHighBonus))
        {
            var threshold = conditions.TryGetValue("round_threshold", out var roundThreshold) ? roundThreshold.GetInt32() : 3;
            if (_roundNumber >= threshold)
            {
                weight += roundHighBonus.GetSingle();
            }
        }

        return Mathf.Max(0.0f, weight);
    }

    private void ApplyEnemyEffect(EnemyEffectSpec effect)
    {
        switch (effect.Operation)
        {
            case "damage":
                ApplyDamageToPlayer(effect.Amount);
                break;
            case "heal":
                if (_activeEnemy != null)
                {
                    _enemyHp = Mathf.Min(_activeEnemy.MaxHealth, _enemyHp + Mathf.RoundToInt(effect.Amount));
                    Log($"{_activeEnemy.Name} recovers {Mathf.RoundToInt(effect.Amount)} health.");
                }
                break;
            case "defend":
                if (_activeEnemy != null)
                {
                    _enemyGuardRatioTemp = Mathf.Max(_enemyGuardRatioTemp, effect.Ratio);
                    Log($"{_activeEnemy.Name} braces for the next hit.");
                }
                break;
            case "attack_buff":
                _enemyAttackMultTemp += effect.Amount;
                break;
            case "reflect":
                _playerReflectTemp = Mathf.Max(_playerReflectTemp, effect.Ratio);
                break;
            default:
                Log($"Unhandled enemy effect: {effect.Operation}");
                break;
        }

        CheckForCombatEnd();
    }

    private bool LastEnemyActionWillAttack()
    {
        if (_activeEnemy == null)
        {
            return false;
        }

        foreach (var action in _activeEnemy.Actions)
        {
            if (action.Label != _lastEnemyActionLabel)
            {
                continue;
            }

            foreach (var effect in action.Effects)
            {
                if (effect.Operation == "damage")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private int GetEffectiveCost(CardDefinition card) => Mathf.Max(0, Mathf.CeilToInt(card.CostBits * (1.0f - Mathf.Clamp(_costReduction, 0.0f, 0.8f))));

    private void CheckForCombatEnd()
    {
        if (!_combatActive || _activeEnemy == null)
        {
            return;
        }

        if (_playerHp <= 0 && _enemyHp <= 0)
        {
            FinishCombat(false, "Both combatants fall. V1 rules count that as defeat.");
        }
        else if (_enemyHp <= 0)
        {
            FinishCombat(true, $"{_activeEnemy.Name} is defeated.");
        }
        else if (_playerHp <= 0)
        {
            FinishCombat(false, "You were defeated.");
        }
    }

    private void FinishCombat(bool victory, string message)
    {
        _combatActive = false;
        _playerTurnActive = false;
        Log(message);
        CombatEnded?.Invoke(victory);
        EmitSnapshot();
    }

    private CombatSnapshot BuildSnapshot()
    {
        var handView = new List<HandCardView>();
        if (_content != null)
        {
            for (var index = 0; index < _hand.Count; index++)
            {
                var card = _content.GetCard(_hand[index]);
                if (card == null)
                {
                    continue;
                }

                handView.Add(new HandCardView
                {
                    Index = index,
                    Id = card.Id,
                    Name = card.Name,
                    Type = card.Type,
                    Cost = GetEffectiveCost(card),
                    Description = card.Description,
                    Playable = _combatActive && _playerTurnActive && _playerBits >= GetEffectiveCost(card),
                    RequiresSecondary = card.RequiresSecondaryCard
                });
            }
        }

        return new CombatSnapshot
        {
            CombatActive = _combatActive,
            PlayerTurnActive = _playerTurnActive,
            Round = _roundNumber,
            PlayerHp = _playerHp,
            EnemyHp = _enemyHp,
            PlayerBits = _playerBits,
            CurrentTurnIncome = _currentTurnIncome,
            LastIncomeGained = _lastIncomeGained,
            DeckName = _activeDeck?.Name ?? "No Deck",
            EnemyName = _activeEnemy?.Name ?? "No Enemy",
            LastEnemyAction = _lastEnemyActionLabel,
            EnemyIntentLabel = _previewEnemyAction?.Label ?? (_combatActive ? "Pending" : _lastEnemyActionLabel),
            EnemyIntentDescription = _previewEnemyAction?.Description ?? "",
            DrawPileCount = _drawPile.Count,
            DiscardPileCount = _discardPile.Count,
            Economy = new EconomyView
            {
                BaseIncome = (_rules?.BasicIncome ?? 0) + _runBaseIncomeBonus,
                FlatBonus = _economyAdd,
                MultiplierBonus = (_bankStacks * 0.05f) + (_sellHighStacks * 0.25f),
                VStacks = _stockStacks,
                CostReduction = _costReduction
            },
            Hand = handView
        };
    }

    private void EmitSnapshot() => SnapshotChanged?.Invoke(BuildSnapshot());

    private void Log(string message) => LogAdded?.Invoke(message);

    private void Shuffle(List<string> values)
    {
        for (var i = values.Count - 1; i > 0; i--)
        {
            var swapIndex = _rng.RandiRange(0, i);
            (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
        }
    }
}
