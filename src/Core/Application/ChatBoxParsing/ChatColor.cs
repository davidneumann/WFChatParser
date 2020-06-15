namespace Application.ChatLineExtractor
{
    public enum ChatColor
    {
        Unknown,
        ChatTimestampName,
        Redtext,
        Text,
        ItemLink,
        Ignored,
        ClanTimeStampName
    }

    public static class ChatColorExtensions
    {
        public static bool IsTimestamp(this ChatColor color)
        {
            if (color == ChatColor.ChatTimestampName || color == ChatColor.ClanTimeStampName)
                return true;
            return false;
        }
    }
}
