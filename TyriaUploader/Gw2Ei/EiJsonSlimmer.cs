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
                SlimDpsTargets(player);
            }
        }

        return obj.ToJsonString();
    }

    private static readonly HashSet<string> BuffEntryKeep = new(StringComparer.Ordinal) { "id", "buffData" };
    private static readonly HashSet<string> BuffDataKeep = new(StringComparer.Ordinal) { "uptime", "generation" };

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
