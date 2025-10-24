using System;
using System.Globalization;
using System.Linq;
using System.Text;
using VampireCommandFramework;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Services.FactionInfamy;

namespace VeinWares.SubtleByte.Commands;

public static class FactionInfamyCommands
{
    [Command("infamy me", description: "Display your current faction hate values or inspect another player by Steam ID.")]
    public static void ShowInfamy(ChatCommandContext ctx, string targetSteamId = "")
    {
        if (!EnsureEnabled(ctx))
        {
            return;
        }

        if (!TryResolveTarget(ctx, targetSteamId, out var steamId, out var displayName))
        {
            ctx.Reply($"[Infamy] Unable to resolve target '{targetSteamId}'. Provide a Steam ID or leave blank to inspect yourself.");
            return;
        }

        if (!FactionInfamySystem.TryGetPlayerHate(steamId, out var snapshot) || snapshot.HateByFaction.Count == 0)
        {
            ctx.Reply($"[Infamy] No hate tracked for {displayName}.");
            return;
        }

        var builder = new StringBuilder();
        builder.Append($"[Infamy] Hate for {displayName}:");

        foreach (var entry in snapshot.HateByFaction.OrderByDescending(static pair => pair.Value.Hate))
        {
            builder.Append(' ');
            builder.Append(entry.Key);
            builder.Append('=');
            builder.Append(entry.Value.Hate.ToString("0.##", CultureInfo.InvariantCulture));
        }

        ctx.Reply(builder.ToString());
    }

    [Command("infamy clear", adminOnly: true, description: "Clear all hate data for a player by Steam ID.")]
    public static void ClearInfamy(ChatCommandContext ctx, string targetSteamId)
    {
        if (!EnsureEnabled(ctx))
        {
            return;
        }

        if (!TryResolveTarget(ctx, targetSteamId, out var steamId, out var displayName))
        {
            ctx.Reply($"[Infamy] Unable to resolve target '{targetSteamId}'. Provide a valid Steam ID.");
            return;
        }

        FactionInfamySystem.ClearPlayerHate(steamId);
        ctx.Reply($"[Infamy] Cleared hate for {displayName}.");
    }

    [Command("infamy add", adminOnly: true, description: "Grant hate to a player or all tracked players. Accepts Steam ID or 'all'.")]
    public static void AddInfamy(ChatCommandContext ctx, string target, string factionId, float amount)
    {
        if (!EnsureEnabled(ctx))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(factionId))
        {
            ctx.Reply("[Infamy] Provide a faction identifier to apply hate against.");
            return;
        }

        if (amount <= 0f)
        {
            ctx.Reply("[Infamy] Amount must be greater than zero.");
            return;
        }

        if (string.Equals(target, "all", StringComparison.OrdinalIgnoreCase))
        {
            var players = FactionInfamySystem.GetAllPlayerHate();
            if (players.Count == 0)
            {
                ctx.Reply("[Infamy] No players currently tracked by the infamy system.");
                return;
            }

            var ids = players.Select(static snapshot => snapshot.SteamId).ToList();
            FactionInfamySystem.RegisterHateGain(ids, factionId, amount);
            ctx.Reply($"[Infamy] Granted {amount.ToString("0.##", CultureInfo.InvariantCulture)} hate ({factionId}) to {ids.Count} tracked players.");
            return;
        }

        if (!TryResolveTarget(ctx, target, out var steamId, out var displayName))
        {
            ctx.Reply($"[Infamy] Unable to resolve target '{target}'. Provide a Steam ID or use 'all'.");
            return;
        }

        FactionInfamySystem.RegisterHateGain(steamId, factionId, amount);
        ctx.Reply($"[Infamy] Granted {amount.ToString("0.##", CultureInfo.InvariantCulture)} hate ({factionId}) to {displayName}.");
    }

    [Command("infamy factions", adminOnly: true, description: "List factions currently tracked by the infamy system.")]
    public static void ListTrackedFactions(ChatCommandContext ctx)
    {
        if (!EnsureEnabled(ctx))
        {
            return;
        }

        var factions = FactionInfamySystem.GetTrackedFactions();
        if (factions.Count == 0)
        {
            ctx.Reply("[Infamy] No faction hate has been recorded yet.");
            return;
        }

        ctx.Reply("[Infamy] Tracked factions: " + string.Join(", ", factions.OrderBy(static f => f, StringComparer.OrdinalIgnoreCase)));
    }

    [Command("infamy reload", adminOnly: true, description: "Reload ambush squad and loot configurations from disk.")]
    public static void ReloadAmbushDefinitions(ChatCommandContext ctx)
    {
        if (!EnsureEnabled(ctx))
        {
            return;
        }

        if (FactionInfamyAmbushData.TryReloadFromCommand(out var message))
        {
            ctx.Reply($"[Infamy] {message}");
        }
        else
        {
            ctx.Reply($"[Infamy] {message}");
        }
    }

    private static bool EnsureEnabled(ChatCommandContext ctx)
    {
        if (FactionInfamySystem.Enabled)
        {
            return true;
        }

        ctx.Reply("[Infamy] The Faction Infamy system is disabled.");
        return false;
    }

    private static bool TryResolveTarget(ChatCommandContext ctx, string input, out ulong steamId, out string displayName)
    {
        if (string.IsNullOrWhiteSpace(input) || string.Equals(input, "me", StringComparison.OrdinalIgnoreCase) || string.Equals(input, "self", StringComparison.OrdinalIgnoreCase))
        {
            steamId = ctx.Event.User.PlatformId;
            displayName = ctx.Event.SenderCharacterEntity.Exists() ? ctx.Event.SenderCharacterEntity.GetPlayerName() : ctx.Name;
            return steamId != 0;
        }

        if (ulong.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out steamId))
        {
            displayName = input;
            return true;
        }

        steamId = 0UL;
        displayName = input;
        return false;
    }
}
