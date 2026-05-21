using System.Text.Json;
using System.Text.Json.Nodes;

namespace TyriaUploader.Gw2Ei;

internal static class EiJsonSlimmer
{
    private static readonly HashSet<string> TopLevelKeep = new(StringComparer.Ordinal)
    {

        "encounterID", "eiEncounterID", "triggerID", "timeStart",

        "players", "success", "bossName", "fightName",
        "fightDurationMs", "durationMS",
    };

    private static readonly HashSet<string> PlayerKeep = new(StringComparer.Ordinal)
    {
        "name", "account", "specialization", "profession",
        "dpsAll", "dpsTargets", "defenses", "support",
        "activeTime",
        "buffUptimes",
        // Outbound boon stats · the server adapter walks groupBuffs →
        // groupBuffsActive → squadBuffs → squadBuffsActive for the
        // generation %. Dropping these silently broke qDPS / aDPS / heal
        // role inference on every uploaded log.
        "groupBuffs", "groupBuffsActive",
        "squadBuffs", "squadBuffsActive",
    };

    // Boon IDs the adapter reads outbound generation for. Filtering the
    // group/squad arrays to just these four trims ~90% of the bytes those
    // arrays would otherwise add (~12-30 boon entries per player become 4).
    private static readonly HashSet<int> OutboundBoonIdsKeep = new()
    {
        740,    // Might
        725,    // Fury
        1187,   // Quickness
        30328,  // Alacrity
    };

    public static string Slim(string eiJsonText)
    {
        if (string.IsNullOrEmpty(eiJsonText)) return eiJsonText;
        JsonNode? root;
        try { root = JsonNode.Parse(eiJsonText); }
        catch (JsonException) { return eiJsonText;  }
        if (root is not JsonObject obj) return eiJsonText;

        DropKeysNotIn(obj, TopLevelKeep);

        if (obj["players"] is JsonArray players)
        {
            foreach (var entry in players)
            {
                if (entry is not JsonObject player) continue;
                DropKeysNotIn(player, PlayerKeep);
                SlimBuffUptimes(player);
                SlimOutboundBuffs(player, "groupBuffs");
                SlimOutboundBuffs(player, "groupBuffsActive");
                SlimOutboundBuffs(player, "squadBuffs");
                SlimOutboundBuffs(player, "squadBuffsActive");
                SlimDpsTargets(player);
            }
        }

        return obj.ToJsonString();
    }

    private static readonly HashSet<string> BuffEntryKeep = new(StringComparer.Ordinal) { "id", "buffData" };
    private static readonly HashSet<string> BuffDataKeep = new(StringComparer.Ordinal) { "uptime", "generation" };
    private static readonly HashSet<string> OutboundBuffDataKeep = new(StringComparer.Ordinal) { "generation" };

    // Filter groupBuffs / squadBuffs to just the four boons the adapter
    // reads generation for, and inside each buffData entry keep only the
    // generation field. Inbound-side arrays (buffUptimes) keep their own
    // shape via SlimBuffUptimes.
    private static void SlimOutboundBuffs(JsonObject player, string field)
    {
        if (player[field] is not JsonArray arr) return;
        for (int i = arr.Count - 1; i >= 0; i--)
        {
            if (arr[i] is not JsonObject e
                || e["id"] is not JsonValue idVal
                || !idVal.TryGetValue<int>(out var id)
                || !OutboundBoonIdsKeep.Contains(id))
            {
                arr.RemoveAt(i);
                continue;
            }
            DropKeysNotIn(e, BuffEntryKeep);
            if (e["buffData"] is JsonArray data)
            {
                foreach (var d in data)
                {
                    if (d is JsonObject bd)
                        DropKeysNotIn(bd, OutboundBuffDataKeep);
                }
            }
        }
    }

    private static void SlimBuffUptimes(JsonObject player)
    {
        if (player["buffUptimes"] is not JsonArray buffs) return;
        foreach (var be in buffs)
        {
            if (be is not JsonObject entry) continue;
            DropKeysNotIn(entry, BuffEntryKeep);
            if (entry["buffData"] is JsonArray data)
            {
                foreach (var d in data)
                {
                    if (d is JsonObject bd)
                        DropKeysNotIn(bd, BuffDataKeep);
                }
            }
        }
    }

    private static void SlimDpsTargets(JsonObject player)
    {
        if (player["dpsTargets"] is not JsonArray targets) return;
        if (targets.Count == 0 || targets[0] is not JsonArray phases || phases.Count == 0)
        {
            player.Remove("dpsTargets");
            return;
        }

        var phase0 = phases[0];
        phases.RemoveAt(0);
        player["dpsTargets"] = new JsonArray { new JsonArray { phase0 } };
    }

    private static void DropKeysNotIn(JsonObject obj, HashSet<string> keep)
    {

        List<string>? toRemove = null;
        foreach (var kv in obj)
        {
            if (!keep.Contains(kv.Key))
                (toRemove ??= new List<string>()).Add(kv.Key);
        }
        if (toRemove == null) return;
        foreach (var k in toRemove) obj.Remove(k);
    }
}
