namespace MountainProjectBot
{
    public static class Markdown
    {
        public static string Bold(string text)
        {
            return $"**{text.Trim('*')}**";
        }

        public static string Italic(string text)
        {
            return $"*{text.Trim('*')}*";
        }

        public static string Link(string linkText, string linkUrl)
        {
            return $"[{linkText.Trim('[', ']')}]({linkUrl.Trim('(', ')')})";
        }

        public static string InlineCode(string text)
        {
            return $"`{text.Trim('`')}`";
        }

        public static string Spoiler(string text)
        {
            return $">!{text}!<";
        }

        public static string DoubleSuperscript(string text)
        {
            return $"^^{text}";
        }

        public static string HRule { get { return "\n\n-----\n\n"; } }

        public static string NewLine { get { return "\n\n"; } }
    }
}
