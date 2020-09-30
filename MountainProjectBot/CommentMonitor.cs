using RedditSharp.Things;
using System;
using System.Runtime.CompilerServices;

namespace MountainProjectBot
{
    public class CommentMonitor
    {
        public CommentMonitor([CallerMemberName] string callingMethod = null)
        {
            Created = DateTime.Now;
            ExpirationHours = 24;
            CreatorMethodName = callingMethod;
        }

        public DateTime Created { get; set; }
        public TimeSpan Age
        {
            get { return DateTime.Now - Created; }
        }
        public int ExpirationHours { get; set; }
        public VotableThing Parent { get; set; }
        public Comment BotResponseComment { get; set; }
        public bool Alerted { get; set; }
        public int FailedTimes { get; set; } = 0;
        public string CreatorMethodName { get; set; }
    }
}
