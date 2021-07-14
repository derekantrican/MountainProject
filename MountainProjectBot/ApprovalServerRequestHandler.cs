using MountainProjectAPI;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MountainProjectBot
{
    public static class ApprovalServerRequestHandler
    {
        public static string HandleRequest(ServerRequest request)
        {
            if (request.RequestMethod == HttpMethod.Get && !request.IsFaviconRequest && !request.IsDefaultPageRequest)
            {
                Dictionary<string, string> parameters = request.GetParameters();
                if (parameters.ContainsKey("status")) //UpTimeRobot will ping this
                {
                    return "UP";
                }
                else if (parameters.ContainsKey("posthistory"))
                {
                    string query = parameters.ContainsKey("query") ? parameters["query"] : "";
                    int page = parameters.ContainsKey("page") ? Convert.ToInt32(parameters["page"]) : 1;
                    return ShowPostHistory(query, page);
                }
                else if (parameters.ContainsKey("postid") && (parameters.ContainsKey("approve") || parameters.ContainsKey("approveall") || parameters.ContainsKey("approveother")))
                {
                    if (BotFunctions.PostsPendingApproval.ContainsKey(parameters["postid"]))
                    {
                        ApprovalRequest approvalRequest = BotFunctions.PostsPendingApproval[parameters["postid"]];

                        return GetApproval(parameters, approvalRequest);
                    }
                    else if (parameters.ContainsKey("force"))
                    {
                        //Because the ApprovalRequest & SearchResult have been disposed by now, we need to recreate them. Maybe we can do this in a better way in the future
                        Post post = BotFunctions.RedditHelper.GetPost(parameters["postid"]).Result;
                        SearchResult searchResult = MountainProjectDataSearch.ParseRouteFromString(post.Title);
                        return GetApproval(parameters, new ApprovalRequest { Force = true, RedditPost = post, SearchResult = searchResult });
                    }
                    else
                    {
                        return WrapHtml($"<h1>Post '{parameters["postid"]}' expired</h1>" +
                                        $"<br>" +
                                        $"<br>" +
                                        $"<input type=\"button\" onclick=\"force()\" value=\"Force\">" +
                                        $"<script>" +
                                        $"  function force(){{" +
                                        $"    window.location.replace(\"{BotUtilities.ApprovalServerUrl}?{string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}").Concat(new[] { "force" }))}\");" +
                                        $"}}" +
                                        $"</script>");
                    }
                }
            }

            return WrapHtml($"<h1>Path '{request.Path}' not understood</h1>");
        }

        //Add viewport, charset, & darkmode for better experience approving via mobile
        private static string WrapHtml(string content, string otherStyles = "", string otherHead = "")
        {
            return "<html>" +
                   "  <head>" +
                   "    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">" +
                   "    <meta charset=\"UTF-8\">" +
                        otherHead +
                   "    <style>" +
                   "      :root {" +
                   "        color-scheme: dark;" +
                   "      }" +
                          otherStyles +
                   "    </style>" +
                   "  </head>" +
                     content +
                   "</html>";
        }

        private static string GetApproval(Dictionary<string, string> parameters, ApprovalRequest approvalRequest)
        {
            string result = "";

            if (parameters.ContainsKey("approveother"))
            {
                if (parameters.ContainsKey("option"))
                {
                    foreach (string approvedId in parameters["option"].Split(','))
                    {
                        MPObject matchingOption = approvalRequest.SearchResult.AllResults.Find(p => p.ID == approvedId) ?? MountainProjectDataSearch.GetItemWithMatchingID(approvedId);
                        if (matchingOption == null)
                        {
                            result += $"Option '{approvedId}' not found<br>";
                        }
                        else
                        {
                            approvalRequest.ApprovedResults.Add(matchingOption);
                        }
                    }
                }
                else
                {
                    return ShowApproveOtherPicker(parameters, approvalRequest);
                }
            }
            else if (parameters.ContainsKey("approve"))
            {
                approvalRequest.ApprovedResults = new List<MPObject> { approvalRequest.SearchResult.FilteredResult };
                approvalRequest.RelatedLocation = approvalRequest.SearchResult.RelatedLocation;
            }
            else if (parameters.ContainsKey("approveall"))
            {
                approvalRequest.ApprovedResults = approvalRequest.SearchResult.AllResults;
                approvalRequest.RelatedLocation = approvalRequest.SearchResult.RelatedLocation;
            }

            if (approvalRequest.IsApproved)
            {
                BotFunctions.PostsPendingApproval[parameters["postid"]] = approvalRequest;
                result = $"Approved:<br>{string.Join("<br>", approvalRequest.ApprovedResults.Select(r => $"&#8226; {r.Name} ({r.ID})"))}"; //Print out approved results as a bulleted list
            }

            return WrapHtml($"<h1>{result}</h1>");
        }

        private static string ShowApproveOtherPicker(Dictionary<string, string> parameters, ApprovalRequest approvalRequest)
        {
            string htmlPicker = "<form>";
            foreach (MPObject option in approvalRequest.SearchResult.AllResults)
            {
                htmlPicker += $"<input type=\"radio\" name=\"options\" value=\"{option.ID}\"{(approvalRequest.SearchResult.AllResults.IndexOf(option) == 0 ? " checked=\"true\"" : "")}>" +
                                $"<a href=\"{option.URL}\">{option.Name} ({(option as Route).GetRouteGrade(Grade.GradeSystem.YDS).ToString(false)})</a>" +
                                $" ({Regex.Replace(BotReply.GetLocationString(option, approvalRequest.SearchResult.RelatedLocation), @"\[|\]\(.*?\)", "").Replace("\n", "")})<br>";
            }

            htmlPicker += "<input type=\"radio\" name=\"options\" id=\"other_option\">Other: <input type=\"text\" id=\"other_option_value\" size=\"100\">&nbsp;(separate multiple urls with semicolons)" +
                            "<br><input type=\"button\" onclick=\"choose()\" value=\"Choose\"></form><script>" +
                            "function choose(){" +
                            "  var options = document.forms[0];" +
                            "  for (var i = 0; i < options.length; i++){" +
                            "    if (options[i].checked){" +
                            "      var chosen = options[i].id != \"other_option\" ? options[i].value : document.getElementById(\"other_option_value\").value.match(/(?<=\\/)\\d+(?=\\/)/g);" +
                            $"     window.location.replace(\"{BotUtilities.ApprovalServerUrl}?approveother&postid={parameters["postid"]}{(parameters.ContainsKey("force") ? "&force" : "")}&option=\" + chosen);" +
                            "      break;" +
                            "    }" +
                            "  }" +
                            "}" +
                            "</script>";

            return WrapHtml(htmlPicker);
        }

        private static string ShowPostHistory(string query, int page = 1)
        {
            string html = "<body>" +
                          "<div id=\"navigation\">" +
                          $"	<input type=\"text\" id=\"search\" placeholder=\"Find a specific post by id\"" +
                          $"{(!string.IsNullOrEmpty(query) ? $"value=\"{query}\"" : "")}>" +
                          $"    <a id=\"navButton\" href=\"{BotUtilities.ApprovalServerUrl}?posthistory{(!string.IsNullOrEmpty(query) ? $"&query={WebUtility.UrlEncode(query)}" : "")}&page={Math.Max(1, page - 1)}\">" +
                          $"&lt;&lt; Prev</a>" +
                          $"    <a id=\"navButton\" href=\"{BotUtilities.ApprovalServerUrl}?posthistory{(!string.IsNullOrEmpty(query) ? $"&query={WebUtility.UrlEncode(query)}" : "")}&page={page + 1}\">" +
                          $"Next &gt;&gt;</a>" +
                          "</div>" +
                          "<table id=\"data\">" +
                          "  <tr>" +
                          "    <th>Date</th>" +
                          "    <th>Subreddit</th>" +
                          "    <th style=\"width:75%\">Post</th>" +
                          "    <th>Reason</th>" +
                          "  </tr>";

            string formatPostToRow(Post post, string reason)
            {
                return "<tr>" +
                       $"  <td>{post.CreatedUTC.ToLocalTime():M/d/yy HH:mm:ss}</td>" +
                       $"  <td>{post.SubredditName}</td>" +
                       $"  <td><a href=\"{post.Shortlink}\">{post.Title}{(post.IsRemoved.HasValue && post.IsRemoved.Value ? " [DELETED]" : "")}</a></td>" +
                       $"  <td>{reason}</td>" +
                       "</tr>";
            }

            IEnumerable<string> seenPostLines = File.ReadAllLines(BotUtilities.SeenPostsPath).Reverse();
            if (!string.IsNullOrEmpty(query))
            {
                string matchingLine = seenPostLines.FirstOrDefault(l => l.Split('\t')[0] == query);
                if (!string.IsNullOrEmpty(matchingLine))
                {
                    string[] arr = matchingLine.Split('\t');
                    Post post = BotFunctions.RedditHelper.GetPost(arr[0]).Result;
                    html += formatPostToRow(post, arr.Length > 1 ? arr[1] : "");
                }
            }
            else
            {
                Dictionary<Task<Post>, string> getPostTasks = new Dictionary<Task<Post>, string>();
                foreach (string line in seenPostLines.Skip((page - 1) * 25).Take(25)) //25 per page
                {
                    string[] arr = line.Split('\t');
                    getPostTasks.Add(Task.Run(() => BotFunctions.RedditHelper.GetPost(arr[0])), arr.Length > 1 ? arr[1] : "");
                }

                Task.WaitAll(getPostTasks.Keys.ToArray());
                foreach (KeyValuePair<Task<Post>, string> postAndReason in getPostTasks)
                {
                    html += formatPostToRow(postAndReason.Key.Result, postAndReason.Value);
                }
            }

            html += "</table>" +
                    "<script>" +
                    "	$('#search').on('keyup', function (e){" +
                    "    	if (e.key === 'Enter'){" +
                    $"        	window.location.replace(\"{BotUtilities.ApprovalServerUrl}?posthistory&query=\" + encodeURI($('#search').val()));" +
                    "        }" +
                    "    });" +
                    "</script>" +
                    "</body>";

            return WrapHtml(html,
                            otherHead: "<script src=\"https://ajax.googleapis.com/ajax/libs/jquery/2.1.3/jquery.min.js\"></script>",
                            otherStyles: "#search {" +
                                      "  width: 75%;" +
                                      "  font-size: 16px;" +
                                      "  margin-bottom: 12px;" +
                                      "}" +
                                      "#navigation * {" +
                                      "	display: inline-block;" +
                                      "    padding: 8px;" +
                                      "}" +
                                      "#data {" +
                                      "  font-family: Arial, Helvetica, sans-serif;" +
                                      "  border-collapse: collapse;" +
                                      "  width: 100%;" +
                                      "}" +
                                      "" +
                                      "#data td, #data th {" +
                                      "  border: 1px solid #ddd;" +
                                      "  padding: 8px;" +
                                      "}" +
                                      "#data tr:nth-child(even){background-color: #f2f2f2;}" +
                                      "#data tr:hover {background-color: #ddd;}" +
                                      "#data th {" +
                                      "  padding-top: 12px;" +
                                      "  padding-bottom: 12px;" +
                                      "  text-align: left;" +
                                      "  background-color: #4287F5;" +
                                      "  color: white;" +
                                      "}");
        }
    }
}
