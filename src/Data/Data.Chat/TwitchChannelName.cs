using System.Text.RegularExpressions;

namespace ChatKnut.Data.Chat;

// Canonicalisation and validation for Twitch channel / username identifiers.
// Rules come from Twitch's username policy: 4–25 characters, lowercase
// letters, digits, and underscores. Channel names are the same string
// prefixed with '#' when sent on IRC, so the stored form drops the '#'.
public static partial class TwitchChannelName
{
    [GeneratedRegex("^[a-z0-9_]{4,25}$")]
    private static partial Regex ValidPattern();

    // Returns the canonical (lowercased, unprefixed) form if the input is a
    // valid Twitch channel / username identifier; throws ArgumentException
    // otherwise. Accepts inputs with or without a leading '#'.
    public static string Normalize(string? channelName, string paramName = "channelName")
    {
        if (string.IsNullOrWhiteSpace(channelName))
            throw new ArgumentException("Channel name is required", paramName);

        var trimmed = channelName.Trim().TrimStart('#').ToLowerInvariant();

        if (!ValidPattern().IsMatch(trimmed))
            throw new ArgumentException(
                "Channel must be 4–25 characters of lowercase letters, digits, or underscores",
                paramName);

        return trimmed;
    }
}