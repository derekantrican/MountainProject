using System.Text.RegularExpressions;

namespace MountainProjectAPI
{
    /* =========================================================
     * This class was created because somewhere around 12/23/2021 MP changed a bunch of their links such that:
     * - most anchor (<a> tags) on the site were downgraded to "http" rather than "https"
     * - many urls now return "301: Moved" response if the url is not exact (eg just ".../area/105907743" without the name on the end)
     *   (so, in parsing, the full url is saved in memory - not just the "id url" - but not serialized with the object)
     * =========================================================
     */

    public static class Url
    {
        public const string HTTP = "http://";
        public const string HTTPS = "https://";
        public const string WWW = "www.";

        public static bool Contains(string containingUrl, string innerUrl, bool ignoreProtocol = true, bool ignoreWWW = true)
        {
            containingUrl = CleanParts(containingUrl, ignoreProtocol, ignoreWWW);
            innerUrl = CleanParts(innerUrl, ignoreProtocol, ignoreWWW);

            return containingUrl.Contains(innerUrl);
        }

        public static bool TextContains(string containingText, string url, bool ignoreProtocol = true, bool ignoreWWW = true)
        {
            url = CleanParts(url, ignoreProtocol, ignoreWWW);

            return containingText.Contains(url);
        }

        public static bool Equals(string url1, string url2, bool ignoreProtocol = true, bool ignoreWWW = true)
        {
            url1 = CleanParts(url1, ignoreProtocol, ignoreWWW);
            url2 = CleanParts(url2, ignoreProtocol, ignoreWWW);

            if (url1 == null)
            {
                return url2 == null ? true : false;
            }
            else
            {
                return url1.Equals(url2);
            }
        }

        public static bool RegexMatch(Regex regex, string url, bool ignoreProtocol = true, bool ignoreWWW = true)
        {
            url = CleanParts(url, ignoreProtocol, ignoreWWW);

            return regex.IsMatch(url);
        }

        public static string Replace(string text, string urlToReplace, string replacementText, bool alsoReplaceProtocol = true, bool alsoReplaceWWW = true)
        {
            Regex regex = new Regex($"{(alsoReplaceProtocol ? @"(https?:\/\/)?" : "")}{(alsoReplaceWWW ? @"(www\.)?" : "")}{CleanParts(urlToReplace)}");
            return regex.Replace(text, replacementText);
        }

        public static string CleanParts(string url, bool removeProtocol = true, bool removeWWW = true)
        {
            if (url == null)
            {
                return url;
            }

            if (removeProtocol)
            {
                url = url.Replace(HTTPS, "").Replace(HTTP, "");
            }

            if (removeWWW)
            {
                url = url.Replace(WWW, "");
            }

            return url;
        }

        public static string BuildFullUrl(string url)
        {
            return $"{HTTPS}{WWW}{CleanParts(url)}"; //Cleaning & pre-pending eliminates any logic for what order to put things in if only some parts are present
        }
    }
}
