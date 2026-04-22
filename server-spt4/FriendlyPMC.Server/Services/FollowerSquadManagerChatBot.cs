using System.Text.RegularExpressions;
using FriendlyPMC.Server.Models;
using FriendlyPMC.Server.Models.Responses;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Dialogue;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Dialog;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;

namespace FriendlyPMC.Server.Services;

[Injectable(InjectionType.Singleton)]
public sealed class FollowerSquadManagerChatBot(
    FollowerManagerService managerService,
    IFollowerChatMessageSender chatMessageSender,
    ItemHelper? itemHelper = null) : IDialogueChatBot
{
    private static readonly Regex CommandSplitPattern = new("\"([^\"]+)\"|(\\S+)", RegexOptions.Compiled);
    private static readonly MongoId ChatBotId = new("67b0f29e151899410b04aacb");

    public UserDialogInfo GetChatBot()
    {
        return new UserDialogInfo
        {
            Id = ChatBotId,
            Aid = 0,
            Info = new UserDialogDetails
            {
                Nickname = "Squad Manager",
                Side = "Usec",
                Level = 0,
                MemberCategory = MemberCategory.Default,
                SelectedMemberCategory = MemberCategory.Default,
            },
        };
    }

    public async ValueTask<string> HandleMessage(MongoId sessionId, SendMessageRequest request)
    {
        var text = request.Text?.Trim() ?? string.Empty;
        string response;
        if (string.IsNullOrWhiteSpace(text))
        {
            response = BuildHelpText();
            SendResponse(sessionId, response);
            return ResolveDialogId(request);
        }

        var arguments = SplitArguments(text);
        if (arguments.Count == 0)
        {
            response = BuildHelpText();
            SendResponse(sessionId, response);
            return ResolveDialogId(request);
        }

        var command = arguments[0].TrimStart('/').ToLowerInvariant();
        try
        {
            response = command switch
            {
                "help" => BuildHelpText(),
                "list" => await BuildRosterListAsync(sessionId.ToString()),
                "add" => await HandleAddAsync(sessionId.ToString(), arguments),
                "rename" => await HandleRenameAsync(sessionId.ToString(), arguments),
                "delete" => await HandleDeleteAsync(sessionId.ToString(), arguments),
                "autojoin" => await HandleAutoJoinAsync(sessionId.ToString(), arguments),
                "kit" => await HandleKitAsync(sessionId.ToString(), arguments),
                "equiplist" => HandleEquipList(sessionId.ToString()),
                "equip" => await HandleEquipAsync(sessionId.ToString(), arguments),
                "gear" => await HandleGearAsync(sessionId.ToString(), arguments),
                "info" => await HandleInfoAsync(sessionId.ToString(), arguments),
                _ => $"Unknown command '{command}'.\n\n{BuildHelpText()}",
            };
        }
        catch (InvalidOperationException ex)
        {
            response = ex.Message;
        }

        SendResponse(sessionId, response);
        return ResolveDialogId(request);
    }

    private static string BuildHelpText()
    {
        return string.Join(
            "\n",
            "/help",
            "/list",
            "/add [nickname]",
            "/rename <nickname> <newnickname>",
            "/delete <nickname>",
            "/autojoin <nickname> on|off",
            "/kit <nickname> persisted|generated",
            "/equiplist",
            "/equip <nickname> <buildname>",
            "/gear <nickname>",
            "/info <nickname>");
    }

    private async Task<string> BuildRosterListAsync(string sessionId)
    {
        var roster = await managerService.GetRosterViewAsync(sessionId);
        if (roster.Count == 0)
        {
            return "No followers in the roster yet.";
        }

        return string.Join(
            "\n",
            roster.Select(member =>
                $"{member.Nickname} | side={member.Side} | autojoin={(member.AutoJoin ? "on" : "off")} | kit={FormatKit(member)} | lvl={(member.Level > 0 ? member.Level : 0)}"));
    }

    private async Task<string> HandleAddAsync(string sessionId, IReadOnlyList<string> arguments)
    {
        var created = await managerService.AddFollowerAsync(sessionId, arguments.Count > 1 ? arguments[1] : null);
        return $"Added follower {created.Nickname}. autojoin=on, kit={created.LoadoutMode.ToLowerInvariant()}.";
    }

    private async Task<string> HandleRenameAsync(string sessionId, IReadOnlyList<string> arguments)
    {
        EnsureArgumentCount(arguments, 3, "/rename <nickname> <newnickname>");
        var member = await ResolveMemberAsync(sessionId, arguments[1]);
        var renamed = await managerService.RenameFollowerAsync(sessionId, member.Aid, arguments[2]);
        return $"Renamed follower to {renamed.Nickname}.";
    }

    private async Task<string> HandleDeleteAsync(string sessionId, IReadOnlyList<string> arguments)
    {
        EnsureArgumentCount(arguments, 2, "/delete <nickname>");
        var member = await ResolveMemberAsync(sessionId, arguments[1]);
        await managerService.DeleteFollowerAsync(sessionId, member.Aid);
        return $"Deleted follower {member.Nickname}.";
    }

    private async Task<string> HandleAutoJoinAsync(string sessionId, IReadOnlyList<string> arguments)
    {
        EnsureArgumentCount(arguments, 3, "/autojoin <nickname> on|off");
        var member = await ResolveMemberAsync(sessionId, arguments[1]);
        var autoJoin = ParseToggle(arguments[2], "/autojoin <nickname> on|off");
        var updated = await managerService.SetAutoJoinAsync(sessionId, member.Aid, autoJoin);
        return $"{updated.Nickname} autojoin is now {(updated.AutoJoin ? "on" : "off")}.";
    }

    private async Task<string> HandleKitAsync(string sessionId, IReadOnlyList<string> arguments)
    {
        EnsureArgumentCount(arguments, 3, "/kit <nickname> persisted|generated");
        var member = await ResolveMemberAsync(sessionId, arguments[1]);
        var updated = await managerService.SetLoadoutModeAsync(sessionId, member.Aid, arguments[2]);
        return $"{updated.Nickname} kit mode is now {updated.LoadoutMode.ToLowerInvariant()}.";
    }

    private string HandleEquipList(string sessionId)
    {
        var builds = managerService.GetAvailableEquipmentBuildNames(sessionId);
        if (builds.Count == 0)
        {
            return "No saved equipment builds were found on the player profile.";
        }

        return string.Join("\n", builds);
    }

    private async Task<string> HandleEquipAsync(string sessionId, IReadOnlyList<string> arguments)
    {
        EnsureArgumentCount(arguments, 3, "/equip <nickname> <buildname>");
        var member = await ResolveMemberAsync(sessionId, arguments[1]);
        var updated = await managerService.ApplyEquipmentBuildAsync(sessionId, member.Aid, arguments[2]);
        return $"{updated.Nickname} equipment is now set from build '{arguments[2]}'. kit mode is now {updated.LoadoutMode.ToLowerInvariant()}.";
    }

    private async Task<string> HandleGearAsync(string sessionId, IReadOnlyList<string> arguments)
    {
        EnsureArgumentCount(arguments, 2, "/gear <nickname>");
        var member = await ResolveMemberAsync(sessionId, arguments[1]);
        var profile = await managerService.TryGetFollowerForManagementAsync(sessionId, member.Aid)
            ?? throw new InvalidOperationException($"Follower '{member.Nickname}' does not have a stored profile.");

        if (profile.Equipment is null || profile.Equipment.Items.Count == 0)
        {
            return $"{profile.Nickname} does not have a stored equipment snapshot yet. Open their Messenger profile after they have spawned once to inspect gear visually.";
        }

        return BuildGearSummary(profile, itemHelper);
    }

    private async Task<string> HandleInfoAsync(string sessionId, IReadOnlyList<string> arguments)
    {
        EnsureArgumentCount(arguments, 2, "/info <nickname>");
        var member = await ResolveMemberAsync(sessionId, arguments[1]);
        return string.Join(
            "\n",
            $"Name: {member.Nickname}",
            $"Side: {member.Side}",
            $"Autojoin: {(member.AutoJoin ? "on" : "off")}",
            $"Kit: {FormatKit(member)}",
            $"Level: {(member.Level > 0 ? member.Level : 0)}",
            $"Experience: {member.Experience}",
            $"Stored Profile: {(member.HasStoredProfile ? "yes" : "no")}");
    }

    private async Task<FollowerManagerMemberDto> ResolveMemberAsync(string sessionId, string nickname)
    {
        return await managerService.TryGetRosterMemberByNicknameAsync(sessionId, nickname)
            ?? throw new InvalidOperationException($"Follower '{nickname}' was not found.");
    }

    private static List<string> SplitArguments(string input)
    {
        return CommandSplitPattern.Matches(input)
            .Select(match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static void EnsureArgumentCount(IReadOnlyList<string> arguments, int minimumCount, string usage)
    {
        if (arguments.Count < minimumCount)
        {
            throw new InvalidOperationException($"Usage: {usage}");
        }
    }

    private static bool ParseToggle(string value, string usage)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "on" => true,
            "off" => false,
            _ => throw new InvalidOperationException($"Usage: {usage}"),
        };
    }

    private static string FormatKit(FollowerManagerMemberDto member)
    {
        var mode = member.LoadoutMode.ToLowerInvariant();
        return string.IsNullOrWhiteSpace(member.AssignedEquipmentBuildName)
            ? mode
            : $"{mode}:{member.AssignedEquipmentBuildName}";
    }

    private static string BuildGearSummary(FollowerProfileSnapshot profile, ItemHelper? itemHelper)
    {
        var equipment = profile.Equipment!;
        var itemsByParent = equipment.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentId))
            .GroupBy(item => item.ParentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var rootChildren = itemsByParent.TryGetValue(equipment.EquipmentId, out var children)
            ? children
            : Array.Empty<FollowerEquipmentItemSnapshot>();

        var lines = new List<string>
        {
            $"Name: {profile.Nickname}",
            $"Kit: stored snapshot ({equipment.Items.Count} items)",
            "Visual inspection: Messenger -> follower -> View User's Profile",
            "Top-level equipment:",
        };

        foreach (var item in rootChildren
                     .OrderBy(item => item.SlotId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.TemplateId, StringComparer.Ordinal))
        {
            var childCount = itemsByParent.TryGetValue(item.Id, out var nestedChildren)
                ? nestedChildren.Length
                : 0;
            var itemName = ResolveItemName(item.TemplateId, itemHelper);
            lines.Add(
                $"- {(string.IsNullOrWhiteSpace(item.SlotId) ? "<root>" : item.SlotId)}: {itemName}{(childCount > 0 ? $" attachments={childCount}" : string.Empty)}");
        }

        return string.Join("\n", lines);
    }

    private static string ResolveItemName(string templateId, ItemHelper? itemHelper)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return "Unknown item";
        }

        if (itemHelper is null)
        {
            return templateId;
        }

        try
        {
            return itemHelper.GetItemName(new MongoId(templateId));
        }
        catch
        {
            return templateId;
        }
    }

    private void SendResponse(MongoId sessionId, string response)
    {
        chatMessageSender.SendUserMessage(sessionId, GetChatBot(), response);
    }

    private static string ResolveDialogId(SendMessageRequest request)
    {
        var dialogId = request.DialogId?.Trim();
        return string.IsNullOrWhiteSpace(dialogId) ? ChatBotId.ToString() : dialogId;
    }
}
