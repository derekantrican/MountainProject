using MountainProjectAPI;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MountainProjectBot
{
    public static class ApprovalServerRequestHandler
    {
        public static string HandleRequest(ServerRequest request)
        {
            string result = $"Path '{request.Path}' not understood";

            if (request.RequestMethod == HttpMethod.Get && !request.IsFaviconRequest && !request.IsDefaultPageRequest)
            {
                Dictionary<string, string> parameters = request.GetParameters();
                if (parameters.ContainsKey("status")) //UpTimeRobot will ping this
                {
                    return "UP";
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
                        return GetApproval(parameters, new ApprovalRequest { Force = true, SearchResult = searchResult });
                    }
                    else
                    {
                        result = $"Post '{parameters["postid"]}' expired<br><br><input type=\"button\" onclick=\"force()\" value=\"Force\">" +
                                 $"<script>" +
                                 $"  function force(){{" +
                                 $"    window.location.replace(\"{(Debugger.IsAttached ? "http://localhost" : BotUtilities.WebServerURL)}:{BotUtilities.ApprovalServer.Port}?" +
                                 $"{string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}").Concat(new[] { "force" }))}\");" +
                                 $"}}" +
                                 $"</script>";
                    }
                }
            }

            return $"<h1>{result}</h1>";
        }

        private static string GetApproval(Dictionary<string, string> parameters, ApprovalRequest approvalRequest)
        {
            string result = "";

            if (parameters.ContainsKey("approveother"))
            {
                return ApproveOther(parameters, approvalRequest);
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
                result = $"Approved {(approvalRequest.ApprovedResults.Count > 1 ? " all" : $" {approvalRequest.ApprovedResults[0].Name} ({approvalRequest.ApprovedResults[0].ID})")}";
            }

            return $"<h1>{result}</h1>";
        }

        private static string ApproveOther(Dictionary<string, string> parameters, ApprovalRequest approvalRequest)
        {
            string result;

            if (parameters.ContainsKey("option"))
            {
                result = "";

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
                string htmlPicker = "<html><form>";
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
                                $"     window.location.replace(\"{(Debugger.IsAttached ? "http://localhost" : BotUtilities.WebServerURL)}:{BotUtilities.ApprovalServer.Port}?approveother&postid={parameters["postid"]}&option=\" + chosen);" +
                                "      break;" +
                                "    }" +
                                "  }" +
                                "}" +
                                "</script></html>";

                return htmlPicker;
            }

            return $"<h1>{result}</h1>";
        }
    }
}
