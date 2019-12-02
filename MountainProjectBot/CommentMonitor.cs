using RedditSharp.Things;
using System;

namespace MountainProjectBot
{
    public class CommentMonitor
    {
        public CommentMonitor()
        {
            Created = DateTime.Now;
            ExpirationMinutes = 60;
        }

        public DateTime Created { get; set; }
        public int ExpirationMinutes { get; set; }
        public VotableThing Parent { get; set; }
        public Comment BotResponseComment { get; set; }
    }
}
