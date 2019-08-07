using RedditSharp.Things;
using System;

namespace MountainProjectBot
{
    public class CommentMonitor
    {
        public CommentMonitor()
        {
            Created = DateTime.Now;
        }

        public DateTime Created { get; set; }
        public Comment ParentComment { get; set; }
        public Comment BotResponseComment { get; set; }
    }
}
