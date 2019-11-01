namespace MountainProjectBot
{
    public class Enums
    {
        public enum BlacklistLevel
        {
            None,               //No blacklist at all
            NoPostReplies,      //Don't automatically reply to posts (MP links and keyword OK)
            OnlyKeywordReplies, //Don't respond to the user's MP links or posts. Only when the user uses the keyword !MountainProject (this should be the maximum blacklist level for real users)
            Total               //Don't respond to user at all (should only be used to blacklist other bots)
        }
    }
}
