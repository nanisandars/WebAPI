using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Threading.Tasks;
using LTSAPI.Controllers;
using LTSAPI.CC_Classes;
using Newtonsoft.Json;
using System.Text;

/* This is used to display Graph and NPS score in Integrated Apps */
namespace LTSAPI.Controllers
{
    public class AverageNPSByUserController : ApiController
    {
        [HttpGet]
        public async Task<int> getAverageNPSByUser(string ccAccessToken, string reqEmail, string reqMobile)
        {
            try
            {
                //Get ALL the Responses for the current requester using either name, mobile or email
                return await getResponsesByRequeser(ccAccessToken, reqEmail, reqMobile);
            }
            catch (Exception ex)
            {
                return 0;
            }
        }

        [HttpGet]
        public async Task<int> getResponsesByRequeser(string ccAccessToken, string reqEmail, string reqMobile)
        {
            reqMobile = "+" + reqMobile;
            reqMobile = reqMobile.Trim().Replace(" ", "");
            double avgNPS = 0;
            try
            {
                int nps = 0;
                string ccURLQtns = "https://api.getcloudcherry.com/api/Questions/Active";
                HttpClient clientReq = new HttpClient();
                HttpRequestMessage reqMsgQtnsActive = new HttpRequestMessage();
                reqMsgQtnsActive.RequestUri = new Uri(ccURLQtns);
                reqMsgQtnsActive.Method = HttpMethod.Get;
                reqMsgQtnsActive.Headers.Add("Authorization", "Bearer " + ccAccessToken);
                HttpResponseMessage resMsgQtnsActive = new HttpResponseMessage();
                resMsgQtnsActive = await clientReq.SendAsync(reqMsgQtnsActive);
                string resMsgQtnsActiveContent = await resMsgQtnsActive.Content.ReadAsStringAsync();
                List<Question> qtnActive = JsonConvert.DeserializeObject<List<Question>>(resMsgQtnsActiveContent);
                List<string> qtnTags = new List<string>() { "NAME", "EMAIL", "MOBILE", "NPS" };
                qtnActive = qtnActive.Where(x => x.QuestionTags.Any(tag => qtnTags.Contains(tag.ToString().ToUpper()))).ToList();

                string qtnIDMobile = "";
                string qtnIDEmail = "";
                string qtnIDNPSTag = "";

                foreach (Question qtnInput in qtnActive)
                {
                    if (qtnInput.QuestionTags.Count == 1)
                    {
                        switch (qtnInput.QuestionTags.FirstOrDefault().ToString().ToUpper())
                        {                          
                            case "MOBILE":
                                {
                                    qtnIDMobile = qtnInput.Id;
                                    break;
                                }
                            case "EMAIL":
                                {
                                    qtnIDEmail = qtnInput.Id;
                                    break;
                                }
                            case "NPS":
                                {
                                    qtnIDNPSTag = qtnInput.Id;
                                    break;
                                }
                        }
                    }
                }


                string ccURLResponses = "https://api.getcloudcherry.com/api/Answers";

                FilterByQuestions filterQtnsEmail = new FilterByQuestions();
                if (reqEmail != "")
                {
                    filterQtnsEmail.questionId = qtnIDEmail;
                    filterQtnsEmail.answerCheck = new List<string>() { reqEmail };
                }

                FilterByQuestions filterQtnsMobile = new FilterByQuestions();
                if (reqMobile != "")
                {
                    filterQtnsMobile.questionId = qtnIDMobile;
                    filterQtnsMobile.answerCheck = new List<string>() { reqMobile };
                }

                FilterBy filter = new FilterBy();
                filter.filterquestions = new List<FilterByQuestions>() {  filterQtnsEmail, filterQtnsMobile }; //

                string jsonFilter = JsonConvert.SerializeObject(filter);
                HttpRequestMessage reqMsgAllResponses = new HttpRequestMessage();
                reqMsgAllResponses.Method = HttpMethod.Post;
                reqMsgAllResponses.RequestUri = new Uri(ccURLResponses);
                reqMsgAllResponses.Headers.Add("Authorization", "Bearer " + ccAccessToken);
                reqMsgAllResponses.Content = new StringContent(jsonFilter, Encoding.UTF8, "application/json");

                HttpResponseMessage resMsgAllResponses = new HttpResponseMessage();
                resMsgAllResponses = await clientReq.SendAsync(reqMsgAllResponses);
                string resMsgAllResponsesContent = await resMsgAllResponses.Content.ReadAsStringAsync();

                try
                {
                    Answer answerNPS = JsonConvert.DeserializeObject<Answer>(resMsgAllResponsesContent);
                    avgNPS = answerNPS.responses.Where(qtnId => qtnId.QuestionId == qtnIDNPSTag).Average(x => x.NumberInput);

                }
                catch (JsonSerializationException ex)
                {
                    List<Answer> answersMultipleNPS = JsonConvert.DeserializeObject<List<Answer>>(resMsgAllResponsesContent);
                    foreach (Answer ansRes in answersMultipleNPS)
                    {
                        nps = nps + ansRes.responses.Where(qtnId => qtnId.QuestionId == qtnIDNPSTag).Select(x => x.NumberInput).FirstOrDefault();
                    }
                    try
                    {
                        nps = nps / answersMultipleNPS.Count;
                    }
                    catch (Exception zeroEx)
                    {
                        nps = 0;
                    }
                }
                return nps;
            }
            catch (Exception ex)
            {
                return 0;
            }
        }

        //Gets the recent 5 NPS Scores by Email and Mobile of the requester
        [HttpGet]
        [Route("api/FDCC/getLastFiveNPSByRequester")]
        public async Task<string> getLastFiveNPSByRequester(string ccAccessToken, string reqEmail, string reqMobile)
        {
            reqMobile = reqMobile == "null" || reqMobile == "" ? "" : "+" + reqMobile;
            reqMobile = reqMobile.Trim().Replace(" ", "");
            
            try
            {
                int nps = 0;
                string ccURLQtns = "https://api.getcloudcherry.com/api/Questions/Active";
                HttpClient clientReq = new HttpClient();
                HttpRequestMessage reqMsgQtnsActive = new HttpRequestMessage();
                reqMsgQtnsActive.RequestUri = new Uri(ccURLQtns);
                reqMsgQtnsActive.Method = HttpMethod.Get;
                reqMsgQtnsActive.Headers.Add("Authorization", "Bearer " + ccAccessToken);
                HttpResponseMessage resMsgQtnsActive = new HttpResponseMessage();
                resMsgQtnsActive = await clientReq.SendAsync(reqMsgQtnsActive);
                string resMsgQtnsActiveContent = await resMsgQtnsActive.Content.ReadAsStringAsync();
                List<Question> qtnActive = JsonConvert.DeserializeObject<List<Question>>(resMsgQtnsActiveContent);
                
                if(qtnActive == null)
                {
                    return "Nodatafound";
                }

                if (qtnActive == null)
                {
                    return "Nodatafound";
                }

                //Gets the NPS, Email , Mobile QuestionIDs

                List<string> qtnTags = new List<string>() { "NAME", "EMAIL", "MOBILE", "NPS" };
                qtnActive = qtnActive.Where(x => x.QuestionTags.Any(tag => qtnTags.Contains(tag.ToString().ToUpper()))).ToList();

                string qtnIDMobile = "";
                string qtnIDEmail = "";
                string qtnIDNPSTag = "";

                foreach (Question qtnInput in qtnActive)
                {
                    if (qtnInput.QuestionTags.Count == 1)
                    {
                        switch (qtnInput.QuestionTags.FirstOrDefault().ToString().ToUpper())
                        {
                            case "MOBILE":
                                {
                                    qtnIDMobile = qtnInput.Id;
                                    break;
                                }
                            case "EMAIL":
                                {
                                    qtnIDEmail = qtnInput.Id;
                                    break;
                                }
                            case "NPS":
                                {
                                    qtnIDNPSTag = qtnInput.Id;
                                    break;
                                }
                        }
                    }
                }


                string ccURLResponses = "https://api.getcloudcherry.com/api/Answers";
                FilterBy filter = new FilterBy();
                filter.filterquestions = new List<FilterByQuestions>(); 

                //Filter by questions for email
                FilterByQuestions filterQtnsEmail = new FilterByQuestions();
                if (reqEmail != "")
                {
                    filterQtnsEmail.questionId = qtnIDEmail;
                    filterQtnsEmail.answerCheck = new List<string>() { reqEmail };
                    filter.filterquestions.Add(filterQtnsEmail);
                }

                //Filter by questions for mobile
                FilterByQuestions filterQtnsMobile = new FilterByQuestions();
                if (reqMobile != "")
                {
                    filterQtnsMobile.questionId = qtnIDMobile;
                    filterQtnsMobile.answerCheck = new List<string>() { reqMobile };
                    filter.filterquestions.Add(filterQtnsMobile);
                }


                string jsonFilter = JsonConvert.SerializeObject(filter);

                //Gets the ANSWERS by applying the filters of mobile and email
                HttpRequestMessage reqMsgAllResponses = new HttpRequestMessage();
                reqMsgAllResponses.Method = HttpMethod.Post;
                reqMsgAllResponses.RequestUri = new Uri(ccURLResponses);
                reqMsgAllResponses.Headers.Add("Authorization", "Bearer " + ccAccessToken);
                reqMsgAllResponses.Content = new StringContent(jsonFilter, Encoding.UTF8, "application/json");

                HttpResponseMessage resMsgAllResponses = new HttpResponseMessage();
                resMsgAllResponses = await clientReq.SendAsync(reqMsgAllResponses);
                string resMsgAllResponsesContent = await resMsgAllResponses.Content.ReadAsStringAsync();

                string htmlLast5NPS = string.Empty;

                try
                {
                    //If only one ANSWER in the response
                    Answer answerNPS = JsonConvert.DeserializeObject<Answer>(resMsgAllResponsesContent);
                    string responseNPS = answerNPS.responses.Where(qtnId => qtnId.QuestionId == qtnIDNPSTag).Select(x=>x.NumberInput).SingleOrDefault().ToString();
                    string responseDate = answerNPS.responseDateTime.ToShortDateString();
                    htmlLast5NPS = htmlLast5NPS + "<tr vertical-align='middle'><td colspan='2' align='right'> <span style='color:black;'>" + responseDate + "</span></td><td style='width:2px;'><div></div></td>";
                    string htmlCell = "<td ' + setClass + '></td><td class='default'><span style='color:black;'><b class='value'>(" + responseNPS + ")</b></span></td>";
                    htmlLast5NPS = htmlLast5NPS + htmlCell + "</tr><tr><td><div style='height:3px;'></div></td></tr>";
                }
                catch (JsonSerializationException ex)
                {
                    //If more than one ANSWER in the response
                    List<Answer> answersMultipleNPS = JsonConvert.DeserializeObject<List<Answer>>(resMsgAllResponsesContent);

                    if (answersMultipleNPS.Count > 5)
                    {
                        answersMultipleNPS = answersMultipleNPS.Take(5).ToList();
                    }


                    foreach (Answer ansRes in answersMultipleNPS)
                    {
                        string responseNPS = ansRes.responses.Where(qtnId => qtnId.QuestionId == qtnIDNPSTag).Select(x => x.NumberInput).SingleOrDefault().ToString();
                        int currentNPS = Int32.Parse(responseNPS);
                        string responseDate = ansRes.responseDateTime.ToShortDateString();
                        htmlLast5NPS = htmlLast5NPS + "<tr vertical-align='middle'><td colspan='2' align='right'> <span style='color:black;'>" + responseDate + "</span></td><td style='width:2px;'><div></div></td>";
                        var htmlCells = "";
                        string setClass = "";
                        if (currentNPS >= 0 && currentNPS <= 6)
                        {
                            setClass = "class='red' style='width:2px;'";
                        }
                        else if (currentNPS >= 7 && currentNPS <= 8)
                        {
                            setClass = "class='yellow' style='width:2px;'";
                        }
                        else if (currentNPS >= 9 && currentNPS <= 10)
                        {
                            setClass = "class='green' style='width:2px;'";
                        }
                        for (var index = 0; index < currentNPS; index++)
                        {
                            if (currentNPS == 1)
                            {
                                htmlCells = htmlCells + "<td ' + setClass + '></td><td class='default'><span style='color:black;'><b class='value'>(" + currentNPS + ")</b></span></td>";
                            }
                            else if (index != currentNPS - 1)
                            {
                                htmlCells = htmlCells + "<td " + setClass + "></td>";
                            }
                            else
                            {
                                htmlCells = htmlCells + "<td class='default'><span style='color:black;'><b class='value'>(" + currentNPS + ")</b></span></td>";
                            }
                        }
                        htmlLast5NPS = htmlLast5NPS + htmlCells + "</tr><tr><td><div style='height:3px;'></div></td></tr>";
                      
                    }
                   
                }
                return htmlLast5NPS == "" ? "Nodatafound" : htmlLast5NPS;
            }
            catch (Exception ex)
            {
                return "Nodatafound";
            }
        }

    }
}
