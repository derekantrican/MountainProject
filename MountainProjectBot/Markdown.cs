
namespace MountainProjectBot
{
    public static class Markdown
    {
        public static string Bold(string textToBold)
        {
            return $"**{textToBold}**";
        }

        public static string Link(string linkText, string linkUrl)
        {
            return $"[{linkText}]({linkUrl})";
        }

        public static string InlineCode(string text)
        {
            return $"`{text}`";
        }

        public static string HRule { get { return "\n\n-----\n\n"; } }

        public static string NewLine { get { return "\n\n"; } }
    }
}
