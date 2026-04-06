using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Map;

namespace STS2Advisor;

/// <summary>
/// Builds game state snapshots for the advisor API.
/// Read-only - no game manipulation.
/// </summary>
public static class StateBuilder
{
    #region Full State
    
    public static Dictionary<string, object?> BuildFullState()
    {
        var result = new Dictionary<string, object?>();

        if (!RunManager.Instance.IsInProgress)
        {
            result["state"] = "menu";
            result["message"] = "No run in progress.";
            return result;
        }

        var runState = RunManager.Instance.DebugOnlyGetState();
        if (runState == null)
        {
            result["state"] = "unknown";
            return result;
        }

        // Run info
        result["run"] = new Dictionary<string, object?>
        {
            ["act"] = runState.CurrentActIndex + 1,
            ["floor"] = runState.TotalFloor,
            ["ascension"] = runState.AscensionLevel
        };

        // Determine current state type
        var topOverlay = NOverlayStack.Instance?.Peek();
        var currentRoom = runState.CurrentRoom;
        bool mapIsOpen = NMapScreen.Instance is { IsOpen: true };

        if (!mapIsOpen && topOverlay is NCardRewardSelectionScreen)
        {
            result["state"] = "card_reward";
            result["card_reward"] = BuildCardRewardState();
        }
        else if (currentRoom is CombatRoom && CombatManager.Instance.IsInProgress)
        {
            result["state"] = "combat";
            result["combat"] = BuildCombatState();
        }
        else if (currentRoom is MerchantRoom)
        {
            result["state"] = "shop";
            result["shop"] = BuildShopState();
        }
        else if (mapIsOpen || currentRoom is MapRoom)
        {
            result["state"] = "map";
            result["map"] = BuildMapState();
        }
        else
        {
            result["state"] = currentRoom?.GetType().Name.ToLower() ?? "unknown";
        }

        // Always include player summary
        var player = LocalContext.GetMe(runState);
        if (player != null)
        {
            result["player"] = BuildPlayerSummary(player);
        }

        return result;
    }
    
    #endregion

    #region Combat State
    
    public static Dictionary<string, object?> BuildCombatState()
    {
        var result = new Dictionary<string, object?>();

        if (!RunManager.Instance.IsInProgress)
        {
            result["error"] = "No run in progress";
            return result;
        }

        if (!CombatManager.Instance.IsInProgress)
        {
            result["error"] = "Not in combat";
            return result;
        }

        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (combatState == null)
        {
            result["error"] = "Combat state unavailable";
            return result;
        }

        var runState = RunManager.Instance.DebugOnlyGetState();
        var player = runState != null ? LocalContext.GetMe(runState) : null;

        result["round"] = combatState.RoundNumber;
        result["turn"] = combatState.CurrentSide.ToString().ToLower();
        result["is_play_phase"] = CombatManager.Instance.IsPlayPhase;

        // Player combat state
        if (player?.PlayerCombatState is { } pcs)
        {
            result["energy"] = pcs.Energy;
            result["max_energy"] = pcs.MaxEnergy;
            result["hp"] = player.Creature.CurrentHp;
            result["max_hp"] = player.Creature.MaxHp;
            result["block"] = player.Creature.Block;

            // Stars (Regent resource)
            if (player.Character.ShouldAlwaysShowStarCounter || pcs.Stars > 0)
            {
                result["stars"] = pcs.Stars;
            }

            // Hand
            var hand = new List<Dictionary<string, object?>>();
            int idx = 0;
            foreach (var card in pcs.Hand.Cards)
            {
                hand.Add(BuildCardInfo(card, idx++));
            }
            result["hand"] = hand;

            // Pile counts
            result["draw_pile_count"] = pcs.DrawPile.Cards.Count;
            result["discard_pile_count"] = pcs.DiscardPile.Cards.Count;
            result["exhaust_pile_count"] = pcs.ExhaustPile.Cards.Count;

            // Player status effects
            result["status"] = BuildPowers(player.Creature);

            // Orbs (Defect)
            var orbQueue = pcs.OrbQueue;
            if (orbQueue != null && orbQueue.Capacity > 0)
            {
                var orbs = new List<Dictionary<string, object?>>();
                foreach (var orb in orbQueue.Orbs)
                {
                    orbs.Add(new Dictionary<string, object?>
                    {
                        ["name"] = SafeGetText(() => orb.Title),
                        ["passive"] = orb.PassiveVal,
                        ["evoke"] = orb.EvokeVal
                    });
                }
                result["orbs"] = orbs;
                result["orb_slots"] = orbQueue.Capacity;
            }
        }

        // Enemies
        var enemies = new List<Dictionary<string, object?>>();
        var entityCounts = new Dictionary<string, int>();
        foreach (var creature in combatState.Enemies)
        {
            if (creature.IsAlive)
            {
                enemies.Add(BuildEnemyInfo(creature, entityCounts));
            }
        }
        result["enemies"] = enemies;

        return result;
    }
    
    #endregion

    #region Shop State
    
    public static Dictionary<string, object?> BuildShopState()
    {
        var result = new Dictionary<string, object?>();

        if (!RunManager.Instance.IsInProgress)
        {
            result["error"] = "No run in progress";
            return result;
        }

        var runState = RunManager.Instance.DebugOnlyGetState();
        if (runState?.CurrentRoom is not MerchantRoom merchantRoom)
        {
            result["error"] = "Not in shop";
            return result;
        }

        // Note: Auto-open inventory removed - NMerchantRoom may not exist in current API
        // If shop isn't showing items, player may need to interact with merchant first

        var inventory = merchantRoom.Inventory;
        if (inventory == null)
        {
            result["error"] = "Shop inventory not ready. Try again.";
            return result;
        }

        var player = LocalContext.GetMe(runState);
        result["gold"] = player?.Gold ?? 0;

        var items = new List<Dictionary<string, object?>>();

        // Cards
        foreach (var entry in inventory.CardEntries)
        {
            if (!entry.IsStocked) continue;
            
            var item = new Dictionary<string, object?>
            {
                ["type"] = "card",
                ["cost"] = entry.Cost,
                ["can_afford"] = entry.EnoughGold,
                ["on_sale"] = entry.IsOnSale
            };
            
            if (entry.CreationResult?.Card is { } card)
            {
                item["name"] = SafeGetText(() => card.Title);
                item["card_type"] = card.Type.ToString();
                item["rarity"] = card.Rarity.ToString();
                item["energy_cost"] = GetCostDisplay(card);
                item["description"] = SafeGetCardDescription(card);
                item["is_upgraded"] = card.IsUpgraded;
            }
            items.Add(item);
        }

        // Relics
        foreach (var entry in inventory.RelicEntries)
        {
            if (!entry.IsStocked) continue;
            
            var item = new Dictionary<string, object?>
            {
                ["type"] = "relic",
                ["cost"] = entry.Cost,
                ["can_afford"] = entry.EnoughGold
            };
            
            if (entry.Model is { } relic)
            {
                item["name"] = SafeGetText(() => relic.Title);
                item["description"] = SafeGetText(() => relic.DynamicDescription);
            }
            items.Add(item);
        }

        // Potions
        foreach (var entry in inventory.PotionEntries)
        {
            if (!entry.IsStocked) continue;
            
            var item = new Dictionary<string, object?>
            {
                ["type"] = "potion",
                ["cost"] = entry.Cost,
                ["can_afford"] = entry.EnoughGold
            };
            
            if (entry.Model is { } potion)
            {
                item["name"] = SafeGetText(() => potion.Title);
                item["description"] = SafeGetText(() => potion.DynamicDescription);
            }
            items.Add(item);
        }

        // Card removal
        if (inventory.CardRemovalEntry is { IsStocked: true } removal)
        {
            items.Add(new Dictionary<string, object?>
            {
                ["type"] = "card_removal",
                ["cost"] = removal.Cost,
                ["can_afford"] = removal.EnoughGold
            });
        }

        result["items"] = items;
        return result;
    }
    
    #endregion

    #region Card Reward State
    
    public static Dictionary<string, object?> BuildCardRewardState()
    {
        var result = new Dictionary<string, object?>();

        var topOverlay = NOverlayStack.Instance?.Peek();
        if (topOverlay is not NCardRewardSelectionScreen cardScreen)
        {
            result["error"] = "No card reward screen active";
            return result;
        }

        var cardHolders = FindAllSortedByPosition<NCardHolder>(cardScreen);
        var cards = new List<Dictionary<string, object?>>();
        
        foreach (var holder in cardHolders)
        {
            var card = holder.CardModel;
            if (card == null) continue;

            cards.Add(new Dictionary<string, object?>
            {
                ["name"] = SafeGetText(() => card.Title),
                ["type"] = card.Type.ToString(),
                ["rarity"] = card.Rarity.ToString(),
                ["energy_cost"] = GetCostDisplay(card),
                ["star_cost"] = GetStarCostDisplay(card),
                ["description"] = SafeGetCardDescription(card),
                ["is_upgraded"] = card.IsUpgraded,
                ["keywords"] = BuildKeywords(card.HoverTips)
            });
        }
        
        result["cards"] = cards;
        
        var altButtons = FindAll<NCardRewardAlternativeButton>(cardScreen);
        result["can_skip"] = altButtons.Count > 0;

        return result;
    }
    
    #endregion

    #region Deck State
    
    public static Dictionary<string, object?> BuildDeckState()
    {
        var result = new Dictionary<string, object?>();

        if (!RunManager.Instance.IsInProgress)
        {
            result["error"] = "No run in progress";
            return result;
        }

        var runState = RunManager.Instance.DebugOnlyGetState();
        var player = runState != null ? LocalContext.GetMe(runState) : null;

        if (player == null)
        {
            result["error"] = "Player not found";
            return result;
        }

        var cards = new List<Dictionary<string, object?>>();
        foreach (var card in player.Deck.Cards)
        {
            cards.Add(new Dictionary<string, object?>
            {
                ["name"] = SafeGetText(() => card.Title),
                ["type"] = card.Type.ToString(),
                ["rarity"] = card.Rarity.ToString(),
                ["energy_cost"] = GetCostDisplay(card),
                ["star_cost"] = GetStarCostDisplay(card),
                ["description"] = SafeGetCardDescription(card),
                ["is_upgraded"] = card.IsUpgraded
            });
        }

        result["cards"] = cards;
        result["count"] = cards.Count;

        // Group by type for summary
        var byType = cards.GroupBy(c => c["type"]?.ToString() ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Count());
        result["by_type"] = byType;

        return result;
    }
    
    #endregion

    #region Relics State
    
    public static Dictionary<string, object?> BuildRelicsState()
    {
        var result = new Dictionary<string, object?>();

        if (!RunManager.Instance.IsInProgress)
        {
            result["error"] = "No run in progress";
            return result;
        }

        var runState = RunManager.Instance.DebugOnlyGetState();
        var player = runState != null ? LocalContext.GetMe(runState) : null;

        if (player == null)
        {
            result["error"] = "Player not found";
            return result;
        }

        var relics = new List<Dictionary<string, object?>>();
        foreach (var relic in player.Relics)
        {
            relics.Add(new Dictionary<string, object?>
            {
                ["name"] = SafeGetText(() => relic.Title),
                ["description"] = SafeGetText(() => relic.DynamicDescription),
                ["counter"] = relic.ShowCounter ? relic.DisplayAmount : null
            });
        }

        result["relics"] = relics;
        result["count"] = relics.Count;

        return result;
    }
    
    #endregion

    #region Map State
    
    public static Dictionary<string, object?> BuildMapState()
    {
        var result = new Dictionary<string, object?>();

        if (!RunManager.Instance.IsInProgress)
        {
            result["error"] = "No run in progress";
            return result;
        }

        var runState = RunManager.Instance.DebugOnlyGetState();
        if (runState == null)
        {
            result["error"] = "Run state unavailable";
            return result;
        }

        var map = runState.Map;
        var visitedCoords = runState.VisitedMapCoords;

        // Current position
        if (visitedCoords.Count > 0)
        {
            var cur = visitedCoords[^1];
            result["current"] = new Dictionary<string, object?>
            {
                ["col"] = cur.col,
                ["row"] = cur.row,
                ["type"] = map.GetPoint(cur)?.PointType.ToString()
            };
        }

        // Next options from UI
        var nextOptions = new List<Dictionary<string, object?>>();
        var mapScreen = NMapScreen.Instance;
        if (mapScreen != null)
        {
            var travelable = FindAll<NMapPoint>(mapScreen)
                .Where(mp => mp.State == MapPointState.Travelable && mp.Point != null)
                .OrderBy(mp => mp.Point!.coord.col)
                .ToList();

            foreach (var nmp in travelable)
            {
                var pt = nmp.Point;
                var option = new Dictionary<string, object?>
                {
                    ["col"] = pt.coord.col,
                    ["row"] = pt.coord.row,
                    ["type"] = pt.PointType.ToString()
                };

                // What does this path lead to?
                var children = pt.Children
                    .OrderBy(c => c.coord.col)
                    .Select(c => $"{c.PointType}@({c.coord.col},{c.coord.row})")
                    .ToList();
                if (children.Count > 0)
                    option["leads_to"] = children;

                nextOptions.Add(option);
            }
        }
        result["next_options"] = nextOptions;

        result["floor"] = runState.TotalFloor;
        result["act"] = runState.CurrentActIndex + 1;

        return result;
    }
    
    #endregion

    #region Event State
    
    public static Dictionary<string, object?> BuildEventState()
    {
        var result = new Dictionary<string, object?>{};

        if (!RunManager.Instance.IsInProgress)
        {
            result["error"] = "No run in progress";
            return result;
        }

        var runState = RunManager.Instance.DebugOnlyGetState();
        if (runState == null)
        {
            result["error"] = "Run state unavailable";
            return result;
        }

        var currentRoom = runState.CurrentRoom;
        if (currentRoom is not EventRoom eventRoom)
        {
            result["error"] = "Not in an event room";
            result["room_type"] = currentRoom?.GetType().Name;
            return result;
        }

        var eventModel = eventRoom.CanonicalEvent;
        bool isAncient = eventModel is AncientEventModel;
        
        result["event_id"] = eventModel.Id.Entry;
        result["event_name"] = SafeGetText(() => eventModel.Title);
        result["is_ancient"] = isAncient;
        result["body"] = SafeGetText(() => eventModel.Description);

        // Get UI room for option buttons
        var uiRoom = NEventRoom.Instance;

        // Options from UI
        var options = new List<Dictionary<string, object?>>();
        if (uiRoom != null)
        {
            var buttons = FindAll<NEventOptionButton>(uiRoom);
            int index = 0;
            foreach (var button in buttons)
            {
                var opt = button.Option;
                var optData = new Dictionary<string, object?>
                {
                    ["index"] = index,
                    ["title"] = SafeGetText(() => opt.Title),
                    ["description"] = SafeGetText(() => opt.Description),
                    ["is_locked"] = opt.IsLocked,
                    ["is_proceed"] = opt.IsProceed,
                    ["was_chosen"] = opt.WasChosen
                };
                if (opt.Relic != null)
                {
                    optData["relic_name"] = SafeGetText(() => opt.Relic.Title);
                    optData["relic_description"] = SafeGetText(() => opt.Relic.DynamicDescription);
                }
                optData["keywords"] = BuildKeywords(opt.HoverTips);
                options.Add(optData);
                index++;
            }
        }
        result["options"] = options;

        return result;
    }
    
    #endregion

    #region Helper Methods
    
    private static Dictionary<string, object?> BuildPlayerSummary(Player player)
    {
        return new Dictionary<string, object?>
        {
            ["character"] = SafeGetText(() => player.Character.Title),
            ["hp"] = player.Creature.CurrentHp,
            ["max_hp"] = player.Creature.MaxHp,
            ["gold"] = player.Gold,
            ["deck_size"] = player.Deck.Cards.Count,
            ["relic_count"] = player.Relics.Count(),
            ["potion_count"] = player.PotionSlots.Count(p => p != null)
        };
    }

    private static Dictionary<string, object?> BuildCardInfo(CardModel card, int index)
    {
        card.CanPlay(out var unplayableReason, out _);

        return new Dictionary<string, object?>
        {
            ["index"] = index,
            ["name"] = SafeGetText(() => card.Title),
            ["type"] = card.Type.ToString(),
            ["energy_cost"] = GetCostDisplay(card),
            ["star_cost"] = GetStarCostDisplay(card),
            ["description"] = SafeGetCardDescription(card),
            ["target"] = card.TargetType.ToString(),
            ["can_play"] = unplayableReason == UnplayableReason.None,
            ["unplayable_reason"] = unplayableReason != UnplayableReason.None ? unplayableReason.ToString() : null,
            ["is_upgraded"] = card.IsUpgraded
        };
    }

    private static Dictionary<string, object?> BuildEnemyInfo(Creature creature, Dictionary<string, int> entityCounts)
    {
        var monster = creature.Monster;
        string baseId = monster?.Id.Entry ?? "unknown";

        // Generate unique id like "jaw_worm_0"
        if (!entityCounts.TryGetValue(baseId, out int count))
            count = 0;
        entityCounts[baseId] = count + 1;

        var state = new Dictionary<string, object?>
        {
            ["id"] = $"{baseId}_{count}",
            ["name"] = SafeGetText(() => monster?.Title),
            ["hp"] = creature.CurrentHp,
            ["max_hp"] = creature.MaxHp,
            ["block"] = creature.Block,
            ["status"] = BuildPowers(creature)
        };

        // Intents
        if (monster?.NextMove is MoveState moveState)
        {
            var intents = new List<string>();
            foreach (var intent in moveState.Intents)
            {
                try
                {
                    var targets = creature.CombatState?.PlayerCreatures;
                    if (targets != null)
                    {
                        string label = intent.GetIntentLabel(targets, creature).GetFormattedText();
                        intents.Add(StripRichTextTags(label));
                    }
                    else
                    {
                        intents.Add(intent.IntentType.ToString());
                    }
                }
                catch
                {
                    intents.Add(intent.IntentType.ToString());
                }
            }
            state["intents"] = intents;
        }

        return state;
    }

    private static List<Dictionary<string, object?>> BuildPowers(Creature creature)
    {
        var powers = new List<Dictionary<string, object?>>();
        foreach (var power in creature.Powers)
        {
            powers.Add(new Dictionary<string, object?>
            {
                ["name"] = SafeGetText(() => power.Title),
                ["amount"] = power.Amount,
                ["is_debuff"] = IsDebuff(power)
            });
        }
        return powers;
    }

    private static List<string> BuildKeywords(IEnumerable<IHoverTip> tips)
    {
        var keywords = new List<string>();
        try
        {
            foreach (var tip in IHoverTip.RemoveDupes(tips))
            {
                if (tip is HoverTip ht && ht.Title != null)
                {
                    keywords.Add(StripRichTextTags(ht.Title));
                }
            }
        }
        catch { }
        return keywords;
    }

    // Try to determine if a power is a debuff - fallback heuristics since API may vary
    private static bool IsDebuff(PowerModel power)
    {
        try
        {
            // Try accessing IsDebuff property via reflection in case it exists
            var prop = power.GetType().GetProperty("IsDebuff");
            if (prop != null) return (bool)(prop.GetValue(power) ?? false);
            
            // Try PowerType enum
            var typeProp = power.GetType().GetProperty("PowerType");
            if (typeProp != null)
            {
                var val = typeProp.GetValue(power)?.ToString() ?? "";
                return val.Contains("Debuff");
            }
            
            // Fallback: common debuff names
            var name = SafeGetText(() => power.Title)?.ToLower() ?? "";
            return name.Contains("weak") || name.Contains("vulnerable") || 
                   name.Contains("frail") || name.Contains("poison") ||
                   name.Contains("wound") || name.Contains("burn") ||
                   name.Contains("dazed") || name.Contains("slime");
        }
        catch { return false; }
    }

    private static string GetCostDisplay(CardModel card)
        => card.EnergyCost.CostsX ? "X" : card.EnergyCost.GetAmountToSpend().ToString();

    private static string? GetStarCostDisplay(CardModel card)
    {
        if (card.HasStarCostX) return "X";
        if (card.CurrentStarCost >= 0) return card.GetStarCostWithModifiers().ToString();
        return null;
    }

    private static string? SafeGetCardDescription(CardModel card)
    {
        try { return StripRichTextTags(card.GetDescriptionForPile(PileType.Hand)).Replace("\n", " "); }
        catch { return SafeGetText(() => card.Description)?.Replace("\n", " "); }
    }

    private static string? SafeGetText(Func<object?> getter)
    {
        try
        {
            var result = getter();
            if (result == null) return null;
            if (result is MegaCrit.Sts2.Core.Localization.LocString locString)
                return StripRichTextTags(locString.GetFormattedText());
            return result.ToString();
        }
        catch { return null; }
    }

    private static string StripRichTextTags(string text)
    {
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '[')
            {
                int end = text.IndexOf(']', i);
                if (end >= 0) { i = end + 1; continue; }
            }
            sb.Append(text[i]);
            i++;
        }
        return sb.ToString();
    }

    private static List<T> FindAll<T>(Node start) where T : Node
    {
        var list = new List<T>();
        if (GodotObject.IsInstanceValid(start))
            FindAllRecursive(start, list);
        return list;
    }

    private static T? FindFirst<T>(Node start) where T : Node
    {
        if (!GodotObject.IsInstanceValid(start))
            return null;
        if (start is T result)
            return result;
        foreach (var child in start.GetChildren())
        {
            var val = FindFirst<T>(child);
            if (val != null) return val;
        }
        return null;
    }

    private static List<T> FindAllSortedByPosition<T>(Node start) where T : Control
    {
        var list = FindAll<T>(start);
        list.Sort((a, b) =>
        {
            int cmp = a.GlobalPosition.Y.CompareTo(b.GlobalPosition.Y);
            return cmp != 0 ? cmp : a.GlobalPosition.X.CompareTo(b.GlobalPosition.X);
        });
        return list;
    }

    private static void FindAllRecursive<T>(Node node, List<T> found) where T : Node
    {
        if (!GodotObject.IsInstanceValid(node)) return;
        if (node is T item) found.Add(item);
        foreach (var child in node.GetChildren())
            FindAllRecursive(child, found);
    }
    
    #endregion
}
