using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Threading.Tasks;
using LTSAPI.Controllers;
using LTSAPI.CC_Classes;
using LTSAPI.FD_Classes;
using Newtonsoft.Json;
using System.Text;

/* This is used to display Graph and NPS score in Integrated Apps */
namespace LTSAPI.Controllers
{
    public class AverageNPSByUserController : ApiController
    {
        [HttpGet]
        public async Task<int> getAverageNPSByUser(string ccAccessToken, string reqName, string reqEmail, string reqMobile)
        {
            try
            {
                //Get ALL the Responses for the current requester using either name, mobile or email
                return await getResponsesByRequeser(ccAccessToken, reqName, reqEmail, reqMobile);
            }
            catch (Exception ex)
            {
                return 0;
            }
        }

        [HttpGet]
        public async Task<int> getResponsesByRequeser(string ccAccessToken, string reqName, string reqEmail, string reqMobile)
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

                string qtnIDName = "";
                string qtnIDMobile = "";
                string qtnIDEmail = "";
                string qtnIDNPSTag = "";

                foreach (Question qtnInput in qtnActive)
                {
                    if (qtnInput.QuestionTags.Count == 1)
                    {
                        switch (qtnInput.QuestionTags.FirstOrDefault().ToString().ToUpper())
                        {
                            case "NAME":
                                {
                                    qtnIDName = qtnInput.Id;
                                    break;
                                }
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

                FilterByQuestions filterQtnsName = new FilterByQuestions();
                filterQtnsName.questionId = qtnIDName;
                filterQtnsName.answerCheck = new List<string>() { reqName };

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
                filter.filterquestions = new List<FilterByQuestions>() { filterQtnsName, filterQtnsEmail, filterQtnsMobile }; //

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
    }
}
