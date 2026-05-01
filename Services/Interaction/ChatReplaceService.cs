namespace Glance.Services;

using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Glance.Core;
using Glance.Models;
using System;
using System.Linq;
using System.Text.RegularExpressions;

public sealed class ChatReplaceService : IDisposable
{
    // Matches full names: "Hitomi Aoki" or "Hitomi Aoki@Balmung"
    // Also matches shortened: "Hitomi A." or "H. Aoki" or "H. A."
    static readonly Regex NamePattern = new(
        @"([A-Z][a-z'']+|[A-Z]\.) ([A-Z][a-z'']+|[A-Z]\.)(?:@([A-Z][a-z]+))?",
        RegexOptions.Compiled);

    public bool Enabled
    {
        get => Globals.Config.ChatReplacementEnabled;
        set => Globals.Config.ChatReplacementEnabled = value;
    }

    public ChatReplaceService() => Globals.ChatGui.ChatMessage += OnChat;

    void OnChat(IHandleableChatMessage msg)
    {
        if (!Enabled || !IsRpChat(msg.LogKind)) return;

        try
        {
            var senderText = msg.Sender.TextValue;
            if (string.IsNullOrEmpty(senderText)) return;

            var replacementName = GetReplacementName(senderText);
            if (replacementName == null) return;

            msg.Sender = ReplaceTextPayloads(msg.Sender, replacementName);
        }
        catch { }
    }

    static SeString ReplaceTextPayloads(SeString original, string newDisplayName)
    {
        var builder = new SeStringBuilder();
        var textReplaced = false;

        foreach (var payload in original.Payloads)
        {
            if (payload is TextPayload && !textReplaced)
            {
                builder.Add(new TextPayload(newDisplayName));
                textReplaced = true;
            }
            else if (payload is TextPayload)
            {
                /* frieren blsted dis */
            }
            else
            {
                builder.Add(payload);
            }
        }

        if (!textReplaced)
            builder.AddText(newDisplayName);

        return builder.Build();
    }

    string? GetReplacementName(string name)
    {
        var me = Globals.PlayerState;
        var myFullName = me?.CharacterName.ToString();
        var myWorld = me?.HomeWorld.Value.Name.ToString();

        var activeId = Globals.Profiles.ActiveProfileId;
        var myProfile = activeId != null
            ? Globals.Profiles.Data?.Characters?.FirstOrDefault(c => c.Id == activeId)
            : null;
        var myProfileName = myProfile?.Name;

        if (myFullName != null && MatchesName(name, myFullName) && !string.IsNullOrEmpty(myProfileName))
            return myProfileName + " ★";

        var m = NamePattern.Match(name);
        if (!m.Success) return null;

        var charName = m.Groups[1].Value + " " + m.Groups[2].Value;
        var world = m.Groups[3].Success ? m.Groups[3].Value : myWorld;
        if (world == null) return null;

        var profile = FindProfile(charName, world);
        if (profile?.Data?.Name == null) return null;

        return profile.Data.Name + " ★";
    }

    CachedProfile? FindProfile(string charName, string world)
    {
        var profile = Globals.Cache.GetProfile(charName, world);
        if (profile?.Data?.Name != null)
            return profile;

        if (charName.Contains('.'))
        {
            var fullName = Globals.Cache.FindFullNameMatching(charName, world);
            if (fullName != null)
                return Globals.Cache.GetProfile(fullName, world);
        }

        return null;
    }

    static bool MatchesName(string possiblyShortened, string fullName)
    {
        if (possiblyShortened.Equals(fullName, StringComparison.OrdinalIgnoreCase))
            return true;

        var fullParts = fullName.Split(' ');
        if (fullParts.Length != 2) return false;

        var testParts = possiblyShortened.Split(' ');
        if (testParts.Length != 2) return false;

        return PartMatches(testParts[0], fullParts[0]) && PartMatches(testParts[1], fullParts[1]);
    }

    static bool PartMatches(string part, string fullPart)
    {
        if (part.Equals(fullPart, StringComparison.OrdinalIgnoreCase))
            return true;

        if (part.Length == 2 && part[1] == '.' &&
            char.ToUpperInvariant(part[0]) == char.ToUpperInvariant(fullPart[0]))
            return true;

        return false;
    }

    static bool IsRpChat(XivChatType t) => t is XivChatType.Say or XivChatType.Yell or XivChatType.Shout
        or XivChatType.Party or XivChatType.Alliance or XivChatType.FreeCompany
        or XivChatType.Ls1 or XivChatType.Ls2 or XivChatType.Ls3 or XivChatType.Ls4
        or XivChatType.Ls5 or XivChatType.Ls6 or XivChatType.Ls7 or XivChatType.Ls8
        or XivChatType.CustomEmote or XivChatType.StandardEmote
        or XivChatType.TellIncoming or XivChatType.TellOutgoing;

    public void Dispose() => Globals.ChatGui.ChatMessage -= OnChat;
}
