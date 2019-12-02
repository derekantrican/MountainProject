using RedditSharp.Things;
using System;

namespace MountainProjectBot
{
    public class CommentMonitor
    {
        public CommentMonitor()
        {
            Created = DateTime.Now;
            ExpirationHours = 24;
        }

        public DateTime Created { get; set; }
        public int ExpirationHours { get; set; }
        public VotableThing Parent { get; set; }
        public Comment BotResponseComment { get; set; }
    }
}
