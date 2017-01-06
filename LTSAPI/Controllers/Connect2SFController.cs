using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using LTSAPI.CC_Classes;
using System.Configuration;
using System.Text;
using System.Web;
using System.IO;
using Cherry.HelperClasses;
using System.Threading;

namespace LTSAPI.Controllers
{
    public class Connect2SFController : ApiController
    {

        CloudCherryController cloudCherryController = new CloudCherryController();
        SalesForce salesForce = new SalesForce();
        SentryController sentry = new SentryController();
        string[] defaultQuestionTags = new string[] { "EMAIL", "MOBILE" };
        string[] defaultSFFields = new string[] { "suppliedemail", "suppliedphone" };


        string Clientid = "";
        string Secret = "";
        string Rtoken = "";
        string InstanceUrl = "";
        string ccusername = "";
        string Error = "";
        private string SFKey = ConfigurationManager.AppSettings["SFKey"];
        string SettingsUrl = ConfigurationManager.AppSettings["UIURL"];

        IntegrationType integrationType = IntegrationType.salesforce;

        string apiKey = "";
        string RefreshToken = "";

        #region QuestionMapping

        /// <summary>
        /// Inserts given single map  details into existing mapping list in CC
        /// </summary>
        /// <param name="Tag">CC Tagname</param>
        /// <param name="Field">Sales Force Field</param>
        /// <param name="CCAccessToken">CC Acccesstoken</param>
        /// <param name="noneditable">Is editabe  or not</param>
        /// <param name="ccUserName">Username</param>
        /// <returns>None </returns>
        [HttpGet]
        [Route("api/Connect2SF/AddInsertMapping")]
        public async Task AddInsertMapping(string TagName, string Field, string CCAccessToken, bool noneditable, string ccUserName)
        {
            try
            {
                Random r = new Random();
                string tagid = "tag_" + r.Next(10000) + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + DateTime.Now.Hour + DateTime.Now.Minute + DateTime.Now.Second + DateTime.Now.Millisecond;
                await cloudCherryController.Insertmapping(tagid, TagName, "", Field, "False", "", CCAccessToken, SFKey);

            }
            catch (Exception ex) { LogThisError(ex.Message); }

        }

        /// <summary>
        /// Retreiving the mapping from CC
        /// </summary>
        /// <param name="accesstoken">CC Accesstoken</param>
        /// <returns>Mapping List</returns>
        [HttpGet]
        [Route("api/Sentry/GetCCMappings")]
        public async Task<Dictionary<string, object>> GetCCMappings(string accesstoken)
        {

            try
            {
                return await cloudCherryController.GetCCMappings(accesstoken, SFKey);

            }
            catch { }
            return new Dictionary<string, object>();
        }

        #endregion //QuestionMapping

        /// <summary>
        /// Gets the first question for the given Tag
        /// </summary>
        /// <param name="Tag">Tag</param>
        /// <param name="Access_token">CC Accesstoken</param>
        /// <returns></returns>
        public async Task<string> GetTagQuestions(string Tag, string Access_token)
        {
            try
            {
                return await cloudCherryController.GetTagQuestions(Tag, Access_token);

            }
            catch (Exception ex) { LogThisError(ex.Message); }
            return "";
        }

        //Retreives  first questionid for the tag specified.
        public async Task<string> GetSingleTagQuestionID(string Tag, string Access_token)
        {
            try
            {
                return await cloudCherryController.GetSingleTagQuestionID(Tag, Access_token);

            }
            catch (Exception ex) { LogThisError(ex.Message); }
            return "";
        }

        //Retreives Tags for the given question list.
        public string GetTagofQuestions(string Questionlist, List<Question> Ques)
        {
            try
            {
                foreach (var q in Ques)
                {
                    if (Questionlist.Contains(q.Id))
                        return q.QuestionTags[0];
                }
            }
            catch (Exception ex) { LogThisError(ex.Message); }
            return Questionlist;
        }

        //RetreivesQuestions for the given question list.
        public string GetQuestiontext(string Qid, List<Question> Ques)
        {
            try
            {
                foreach (var q in Ques)
                {
                    if (Qid == q.Id)
                        return q.QuestionTags[0]; ;
                }
            }
            catch (Exception ex) { LogThisError(ex.Message); }
            return "";
        }

        /// <summary>
        /// Connecting to SF using Refresh Token
        /// </summary>
        /// <param name="Rtoken">Refresh Token</param>
        /// <returns>Returns list of Existing CC Tags and SF Fields </returns>
        [HttpGet]
        [Route("api/Connect2SF/ConnectNRetrieveQidsNfields")]
        public async Task<List<Dictionary<string, object>>> ConnectNRetrieveQidsNfields(string Rtoken)
        {
            try
            {
                List<Dictionary<string, object>> finallist = new List<Dictionary<string, object>>();
                Dictionary<string, object> Connectinfo = new Dictionary<string, object>();
                Dictionary<string, object> Exceptiondata = cloudCherryController.getClientsettingsByKey("rtoken", Rtoken, IntegrationType.salesforce);
                if (Exceptiondata == null)
                    return null;
                Clientid = Exceptiondata["clientid"].ToString();
                Secret = Exceptiondata["secret"].ToString();
                Rtoken = Exceptiondata["rtoken"].ToString();
                InstanceUrl = Exceptiondata["instanceurl"].ToString();
                ccusername = Exceptiondata["username"].ToString();
                apiKey = Exceptiondata["apikey"].ToString();
                List<string> SFToken = await ConnectSF(Clientid, Secret, Rtoken);
                if (SFToken[0] != "")
                {
                    Connectinfo.Add("SFToken", SFToken[0]);
                    string CCToken = await getCCAccessToken(ccusername, apiKey);
                    Connectinfo.Add("CCToken", CCToken);
                    Connectinfo.Add("Qids", await GetQuestionsTagsandId(CCToken));
                    List<string> casefields = await getSFCaseFields(SFToken[1], SFToken[0]);
                    Connectinfo.Add("caseFields", casefields);
                    List<string> tokenlist = new List<string>();
                    Connectinfo.Add("TokenList", tokenlist);
                    finallist.Add(Connectinfo);
                    return finallist;
                }
            }
            catch (Exception ex) { LogThisError(ex.Message); }
            return null;
        }

        /// <summary>
        /// Connecting to Sales Force with given clientid ,secret and refresh token
        /// </summary>    

        public async Task<List<string>> ConnectSF(string clientid, string secret, string refreshtoken)
        {
            try
            {
                clientid = clientid.Trim();
                secret = secret.Trim();
                refreshtoken = refreshtoken.Trim();
                List<string> SFdetails = new List<string>();
                HttpClient client = new HttpClient();
                string authEndPoint = "https://login.salesforce.com/services/oauth2/token";
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                var postCrendentials = new[] {
new KeyValuePair<string,string>("grant_type","refresh_token"),
new KeyValuePair<string,string>("client_id", clientid),    
new KeyValuePair<string,string>("client_secret", secret),  
new KeyValuePair<string,string>("refresh_token", refreshtoken)
};

                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, new Uri(authEndPoint));
                req.Content = new FormUrlEncodedContent(postCrendentials);
                HttpResponseMessage response = await client.SendAsync(req);
                string responseAsString = await response.Content.ReadAsStringAsync();
                var tokenStructure = new { access_token = string.Empty, expires_in = 0, instance_url = string.Empty };
                var token = JsonConvert.DeserializeAnonymousType(responseAsString, tokenStructure);
                SFdetails.Add(token.access_token);
                SFdetails.Add(token.instance_url);
                if (token.access_token == null)
                {
                    var tokenStructure1 = new { error = "invalid_grant", error_description = "expired access/refresh token" };
                    var token1 = JsonConvert.DeserializeAnonymousType(responseAsString, tokenStructure1);
                    SFdetails.Add(token1.error);
                    SFdetails.Add(token1.error_description);
                }
                return SFdetails;
            }
            catch (Exception ex) { LogThisError(ex.Message); }
            return new List<string>();

        }

        //retreiving list of question from CC
        [HttpGet]
        [Route("api/Connect2SF/GetQuestions")]
        public async Task<List<Question>> GetQuestions(string Access_token)
        {
            try
            {
                return await cloudCherryController.GetQuestions(Access_token);

            }
            catch (Exception ex) { LogThisError(ex.Message); }
            return new List<Question>();
        }

        //Calling CC API to  retiving Tag and Field mapping
        [HttpGet]
        [Route("api/Connect2SF/GetTagFieldMapping")]
        public async Task<List<Dictionary<string, object>>> GetTagFieldMapping(string Username, string password)
        {
            try
            {
                return await cloudCherryController.GetTagFieldMapping(Username, password, SFKey);

            }
            catch (Exception ex) { LogThisError(ex.Message); }
            return null;
        }

        //Retreives the CC Life time API Key
        [HttpGet]
        [Route("api/Connect2SF/GetAPIKey")]
        public async Task<string> GetAPIKey(string Username, string Password)
        {
            try
            {
                return await cloudCherryController.GetAPIKey(Username, Password);
            }
            catch { }
            return "";
        }

        [HttpGet]
        [Route("api/Connect2SF/GetQuestionsTags")]
        ///Retreiving the list of question
        public async Task<List<string>> GetQuestionsTags(string Access_token)
        {
            try
            {
                return await cloudCherryController.GetQuestionsTags(Access_token);
            }
            catch (Exception ex) { LogThisError(ex.Message); }
            return new List<string>();
        }

        //Retreiving question tags and question ids  from CC
        [HttpGet]
        [Route("api/Connect2SF/GetQuestionsTagsandId")]
        public async Task<List<Dictionary<string, object>>> GetQuestionsTagsandId(string Access_token)
        {
            try
            {
                return await cloudCherryController.GetQuestionsTagsandId(Access_token);

            }
            catch (Exception ex) { LogThisError(ex.Message); }
            return new List<Dictionary<string, object>>();
        }

        //Adds given Exception into salesforce exception list for the given user
        //Call CC Post API call to  insert given data 
        public async Task PostIntegrationData(string Username, string Password, Dictionary<string, object> CompleteExceptionData)
        {
            await cloudCherryController.PostIntegrationData(Username, Password, SFKey, CompleteExceptionData);
        }

        //Retreives the given CC Accesstoken  from CC
        [HttpGet]
        [Route("api/Connect2SF/getCCAccessToken")]
        public async Task<string> getCCAccessToken(string ccuserName, string ccpassword)
        {
            return await cloudCherryController.getCCAccessToken(ccuserName, ccpassword);
        }

        //retreives the  Sales force Instance Url from given salesforce account
        [HttpGet]
        [Route("api/Connect2SF/getSFInstanceURL")]
        public async Task<string> getSFInstanceURL(string ClientId, string ConsumerSecret, string UserName, string Password)
        {
            try
            {
                HttpClient client = new HttpClient();
                string authEndPoint = "https://login.salesforce.com/services/oauth2/token";
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                var postCrendentials = new[] {
new KeyValuePair<string,string>("grant_type","password"),
new KeyValuePair<string,string>("client_id", ClientId),   
new KeyValuePair<string,string>("client_secret", ConsumerSecret),
new KeyValuePair<string,string>("username",UserName),
new KeyValuePair<string,string>("password",Password)
};

                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, new Uri(authEndPoint));
                req.Content = new FormUrlEncodedContent(postCrendentials);

                HttpResponseMessage response = await client.SendAsync(req);
                string responseAsString = await response.Content.ReadAsStringAsync();
                var tokenStructure = new { access_token = string.Empty, expires_in = 0, instance_url = string.Empty };
                var token = JsonConvert.DeserializeAnonymousType(responseAsString, tokenStructure);

                if (token.instance_url != "")
                {
                    return token.instance_url;
                }
                else
                    return string.Empty;
            }
            catch (Exception ex)
            {
                return "Error";
            }
        }

        //Retreining the sales force Fields from given account
        [HttpGet]
        [Route("api/Connect2SF/getSFCaseFields")]
        public async Task<List<string>> getSFCaseFields(string instanceurl, string accessToken)
        {
            List<string> fields = new List<string>();
            try
            {
                HttpClient client = new HttpClient();

                string serviceEndPoint = instanceurl + "/services/apexrest/CCService/getCaseFields";

                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, new Uri(serviceEndPoint));
                req.Headers.Add("Authorization", "Bearer " + accessToken);

                HttpResponseMessage response = await client.SendAsync(req);
                if (!response.IsSuccessStatusCode)
                {
                    fields.Add("Unsupported Client");
                    return fields;
                }
                string respString = await response.Content.ReadAsStringAsync();
                respString = respString.Replace("\"", "");
                respString = respString.Replace("[", "");
                respString = respString.Replace("]", "");

                if (respString != "")
                {
                    string[] fieldsArr = respString.Split(',');

                    foreach (string field in fieldsArr.ToList())
                    {
                        fields.Add(field);
                    }


                    return fields.OrderBy(item=>item.ToString()).ToList();
                }
                else
                    return null;
            }
            catch (Exception ex)
            {
                LogThisError(ex.Message);
                fields = new List<string>();
                fields.Add(ex.Message);
                return fields;
            }
        }

        //Survey from CC is posted to this method with inturns logs exception to sentry and create a ticket in Salesforce
        [HttpPost]
        [Route("api/Connect2SF/getPostedData")]
        public async Task<IHttpActionResult> getDataFromAPI(NotificationData surveyData)
        {
            string obj = "";
            string ExceptionRaised = "";
            string ExceptionRaisedMessage = "";
            try
            {
                ccusername = surveyData.answer.user;
                string jsonAnswer = JsonConvert.SerializeObject(surveyData.answer);
                string serializeSurveyData = JsonConvert.SerializeObject(surveyData.answer);
                Dictionary<string, object> Exceptiondata = cloudCherryController.getClientsettingsByKey("username", ccusername, IntegrationType.salesforce);

                Clientid = Exceptiondata["clientid"].ToString();
                Secret = Exceptiondata["secret"].ToString();
                RefreshToken = Exceptiondata["rtoken"].ToString();
                InstanceUrl = Exceptiondata["instanceurl"].ToString();
                apiKey = Exceptiondata["apikey"].ToString();
                //Checks whether the CC user has authenticated with SF or not
                if (Exceptiondata == null)
                {
                    sentry.LogTheFailedRecord("Authentication with SF has not happened yet for this user.", "Token", "", ExceptionType.General, surveyData.answer.id, ccusername, IntegrationType.salesforce);
                    return Ok();
                }


                //Checks whether the CC user's SF details are existed in DB or not
                if (Clientid == "")
                {
                    await sentry.LogTheFailedRecord("Client Id is not found for this user.", "Token", "", ExceptionType.General, surveyData.answer.id, ccusername, IntegrationType.salesforce);
                    return Ok();
                }
                else if (Secret == "")
                {
                    await sentry.LogTheFailedRecord("SecretKey is not found for this user.", "Token", "", ExceptionType.General, surveyData.answer.id, ccusername, IntegrationType.salesforce);



                    return Ok();
                }
                else if (RefreshToken == "")
                {
                    await sentry.LogTheFailedRecord("Refresh token is not found for this user.", "Token", "", ExceptionType.General, surveyData.answer.id, ccusername, IntegrationType.salesforce);
                    return Ok();
                }
                else if (InstanceUrl == "")
                {
                    await sentry.LogTheFailedRecord("Instance URL is not found for this user.", "Token", "", ExceptionType.General, surveyData.answer.id, ccusername, IntegrationType.salesforce);
                    return Ok();
                }


                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                HttpClient client = new HttpClient();
                //Get SF Access Token
                List<string> tokendetails = await ConnectSF(Clientid, Secret, RefreshToken);

                if (tokendetails.Contains("invalid_grant"))
                {
                    await sentry.LogTheFailedRecord("Refresh Token has expired.", "TokenExpired", "", ExceptionType.General, surveyData.answer.id, ccusername, IntegrationType.salesforce);
                    return Ok();
                }

                //Get question mappings
                List<Dictionary<string, object>> qmap = await GetTagFieldMapping(ccusername, apiKey);

                if (qmap != null)
                {
                    List<string> Existingtaglist = await GetQuestionsTags(await getCCAccessToken(ccusername, apiKey));
                    Dictionary<string, string> jsonmap = new Dictionary<string, string>();
                    foreach (var map in qmap)
                    {




                        string Field = map["field"].ToString();
                        string F1 = Field.ToString();
                        string Qid = map["qid"].ToString();
                        string tag = map["tag"].ToString();
                        if (map["disabled"].ToString() == "True")
                            continue;
                        foreach (var res in surveyData.answer.responses)
                        {
                            if (Qid.Contains(res.QuestionId))
                            {
                                string ans = res.TextInput == "" || res.TextInput == null ? res.NumberInput.ToString() : res.TextInput.ToString();
                                jsonmap.Add(Field, ans);
                            }
                        }
                    }

                    if (jsonmap.Count == 0)
                    {
                        LogThisError("No Field mappings were found while  inserting ticket into SF.");
                        await sentry.LogTheFailedRecord("No Insert Field Mapping Found for this USER.", "Token", "", ExceptionType.General, surveyData.answer.id, ccusername, IntegrationType.salesforce);

                        return BadRequest();
                    }

                    //Passing default values to insert a CASE object in SF
                    jsonmap.Add("Status", "New");
                    jsonmap.Add("Origin", "Web");
                    jsonmap.Add("CCTicket__c", surveyData.answer.id); //Sending Unique Identifier to SF [AnswerID]

                    //Converting mapping object into json
                    obj = JsonConvert.SerializeObject(jsonmap);

                    //Specifying the SF API to insert CASE
                    string serviceEndPoint = InstanceUrl + "/services/apexrest/CCService/insertCCTicket";
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, new Uri(serviceEndPoint));
                    request.Headers.Add("Authorization", "Bearer " + tokendetails[0]);
                    request.Content = new StringContent(obj, Encoding.UTF8, "application/json");

                    //Sending the request to SF to insert CASE
                    client.Timeout = TimeSpan.FromSeconds(10);
                    Task<HttpResponseMessage> resp = client.SendAsync(request);
                    string respString = await resp.Result.Content.ReadAsStringAsync();

                    if (resp.Result.StatusCode != HttpStatusCode.OK)
                    {
                        await sentry.LogTheFailedRecord(obj + "<br/>" + respString, "CallOut Exception", obj, ExceptionType.Create, surveyData.answer.id, ccusername, IntegrationType.salesforce);
                        return BadRequest();
                    }

                    if (resp.Result.StatusCode == HttpStatusCode.OK)
                    {
                        Dictionary<string, string> responseMsg = new Dictionary<string, string>();
                        responseMsg = JsonConvert.DeserializeObject<Dictionary<string, string>>(respString);
                        //when the ticket insertion into Sf is not 100% successfull, the  particular ticket in  logged as Partially successfull record.
                        if (responseMsg.Keys.Count > 1)
                        {
                            string errFieldList = "";
                            string errFieldVsValues = "";
                            string fLevelExceptionMsg = "";
                            string successCaseNo = "";
                            string dateTime = "";

                            foreach (var item in responseMsg)
                            {

                                switch (item.Key)
                                {

                                    case "ErrorFields":
                                        {
                                            errFieldList = item.Value.ToString();
                                            break;
                                        }
                                    case "ErrorFieldsVsValues":
                                        {
                                            errFieldVsValues = item.Value.ToString();
                                            break;
                                        }
                                    case "FieldLevelException":
                                        {
                                            fLevelExceptionMsg = item.Value.ToString();
                                            break;
                                        }
                                    case "SUCCESS":
                                        {
                                            successCaseNo = item.Value.ToString();
                                            break;
                                        }
                                    case "DateTime":
                                        {
                                            dateTime = item.Value.ToString();
                                            break;
                                        }
                                }
                            }
                            if (dateTime == "")
                                dateTime = DateTime.Now.ToShortDateString();

                            await sentry.LogThePartialSuccessRecord(surveyData.answer.id, dateTime, errFieldList, errFieldVsValues, fLevelExceptionMsg, successCaseNo, ExceptionType.FieldLevel, ccusername, integrationType);
                            return Ok();
                        }

                        else
                        {
                            //if Ticket insertion it sucessful in sf Details then the survey id and ticket id are logged 
                            string resposneKey = responseMsg.Keys.FirstOrDefault().ToString();
                            string responseValue = responseMsg.Values.FirstOrDefault().ToString();
                            if (resposneKey == "SUCCESS")
                            {
                                return Ok();
                            }
                            else
                            {
                                await sentry.LogTheFailedRecord(obj + "<br/>" + respString, "UnKnown Exceptions", "", ExceptionType.Create, surveyData.answer.id, ccusername, IntegrationType.salesforce);
                                return BadRequest();
                            }
                        }

                    }

                    return Ok();
                }
                else
                {
                   
                    LogThisError("No Field mappings were found while  inserting ticket into SF.");
                    await sentry.LogTheFailedRecord("No Insert Field Mapping Found for this USER.", "Token", "", ExceptionType.General, surveyData.answer.id, ccusername, IntegrationType.salesforce);
                    return BadRequest();
                }

            }
            catch (TimeoutException ex)
            {
                ExceptionRaised = "Timeout";
                ExceptionRaisedMessage = ex.Message;
            }
            catch (Exception ex)
            {
                ExceptionRaised = "Unknown";
                ExceptionRaisedMessage = ex.Message;
            }
            if (ExceptionRaised == "Timeout")
            {
                await sentry.LogTheFailedRecord(ExceptionRaisedMessage, "TimedOut Exception", "", ExceptionType.Create, surveyData.answer.id, ccusername, IntegrationType.salesforce);
                return BadRequest();

            }
            if (ExceptionRaised == "Unknown")
            {
                await sentry.LogTheFailedRecord(ExceptionRaisedMessage, "Unknown Exception", "", ExceptionType.General, surveyData.answer.id, ccusername, IntegrationType.salesforce);
            
            }
            return Ok();
        }

        public void LogThisError(string Errormessage)
        {
            string logPath = System.Configuration.ConfigurationManager.AppSettings["logFilePath"];
            logPath = HttpContext.Current.Server.MapPath("~/" + logPath);
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(logPath, true))
            {

                file.WriteLine("Message :" + Errormessage + "<br/>" + Environment.NewLine + "Date :" + DateTime.Now.ToString());
                file.WriteLine(Environment.NewLine + "-----------------------------------------------------------------------------" + Environment.NewLine);
            }
        }

        //Retreives all the user credentials that are logged in setting.json file during authentication
        public List<Dictionary<string, object>> GetClientCredentials()
        {

            string logPath = System.Configuration.ConfigurationManager.AppSettings["CredentialsPath"];
            logPath = HttpContext.Current.Server.MapPath("~/" + logPath);

            using (StreamReader r = new StreamReader(logPath))
            {
                string json = r.ReadToEnd();
                var tokenStructure = new { CCUserName = string.Empty, CCPassword = string.Empty, SFClientID = 0, SFSecretKey = string.Empty, APIKey = string.Empty, SFRefreshToken = string.Empty };
                var token = JsonConvert.DeserializeAnonymousType(json, tokenStructure);

            }
            return new List<Dictionary<string, object>>();
        }

        //Retreives the first question for the given tag from the given question list.
        /*           
        *  tags related to locations of given question  are returned as  output
        */
        /**
        * On editing of the tag in UI, Locations of the given tags are retreived,
        * then tags related to given location are  returned as output
        */
        [HttpGet]
        [Route("api/Connect2SF/GetTagonEdit")]
        public async Task<List<string>> GetTagonEdit(string tag, string Username)
        {


            Dictionary<string, object> Exceptiondata = cloudCherryController.getClientsettingsByKey("username", Username, IntegrationType.salesforce);

            Clientid = Exceptiondata["clientid"].ToString();
            Secret = Exceptiondata["secret"].ToString();
            string Rtoken = Exceptiondata["rtoken"].ToString();
            string apikey = Exceptiondata["apikey"].ToString();
            string accessToken = await cloudCherryController.getCCAccessToken(Username, apikey);
            List<string> finallist = new List<string>();
            try
            {
                // string accessToken = await getCCAccessToken();
                if (accessToken == null || accessToken == "")
                {
                    finallist.Add("Error:Unable to connect to Cloudcherry");
                    return finallist;
                }

                List<Question> questionList = await GetQuestions(accessToken);
                if (questionList == null || questionList.Count == 0)
                {
                    finallist.Add("Error:Unable to retreive questions");
                    return finallist;
                }

                string errorMessage = "";
                //Reteiving the first question of the given tag
                Question question = cloudCherryController.GetFirstQuestionofTag(tag, accessToken, questionList, ref errorMessage);
                if (question == null)
                {
                    finallist.Add("Error:" + errorMessage);
                    return finallist;
                }

                //then tags related to given location (location exist in question object) are retreived
                finallist = cloudCherryController.GetQuestionTagsforLocation(question, questionList);
                return finallist;
            }
            catch { }
            finallist.Add("Error:Unable to retreive question tags");
            return finallist;
        }

        //Add a note at CC after update in SF
        [HttpPost]
        [Route("api/Connect2SF/AddNotesAtCC")]
        public async Task<IHttpActionResult> AddNoteByAnswer()
        {
            string userName = "";
            string response = "";
            string apiKey = "";
            try
            {
                HttpClient client = new HttpClient();
                MessageNotes notes = new MessageNotes();
                //Read the response from the request
                using (var contentStream = await this.Request.Content.ReadAsStreamAsync())
                {
                    contentStream.Seek(0, SeekOrigin.Begin);
                    using (var sr = new StreamReader(contentStream))
                    {
                        response = sr.ReadToEnd();
                    }
                }

                if (response == "")
                    return Ok();
                //deserializing the  updation data from CC
                Dictionary<string, object> caseDeserialize = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
                string CCTicketId = "";
                StringBuilder noteStructure = new StringBuilder();

                if (caseDeserialize == null)
                    return Ok();

                foreach (var res in caseDeserialize)
                {
                    StringBuilder propertiesNote = new StringBuilder();

                    if (res.Key == "CCTicket__c")
                    {
                        CCTicketId = Convert.ToString(res.Value);
                    }
                    else if (res.Key == "LastUpdatedOn__c")
                    {
                        // noteStructure.Append(res.Key + ":" + res.Value + "      ");
                        notes.noteTime = res.Value.ToString();
                    }
                    else if (res.Key == "apiKey")
                    {
                        apiKey = res.Value.ToString();
                    }
                    else if (!(res.Key == "attributes"))
                    {
                        propertiesNote.Append(res.Key + ":" + res.Value + "      " + "\\n" + "      ");

                    }

                    noteStructure.Append(propertiesNote);
                }

                //Retreiving User credentials from log using  the given "APIKey"
                Dictionary<string, object> Exceptiondata = cloudCherryController.getClientsettingsByKey("apikey", apiKey, IntegrationType.salesforce);


                userName = Exceptiondata["username"].ToString();


                //Assing the respective values for the parameters sent  from CCSFsetting  object
                notes.note = noteStructure.ToString();
                var serializeNote = JsonConvert.SerializeObject(notes);

                if (noteStructure.Length == 0)
                    return Ok();

                //posting the updation data to CC, This Data is inserted as Note to a response data with answer id associated with Updation data from SF
                //Specifying the add Note URL at CC
                string ccURLforAddNote = "https://api.getcloudcherry.com/api/Answers/Note/" + CCTicketId;
                HttpRequestMessage addNoteRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(ccURLforAddNote));

                //Get AccessToken from CC
                string ccAccessToken = await getCCAccessToken(userName, apiKey);
                addNoteRequest.Headers.Add("Authorization", "Bearer " + ccAccessToken);
                addNoteRequest.Content = new StringContent(serializeNote, Encoding.UTF8, "application/json");

                //Get Response from the CC API
                HttpResponseMessage addNoteResponse = await client.SendAsync(addNoteRequest);
                string addNoteRespContent = await addNoteResponse.Content.ReadAsStringAsync();
                //if any exception occurs that data is logged.
                if (addNoteResponse.StatusCode != HttpStatusCode.OK)
                {
                    sentry.LogTheFailedRecord(addNoteRespContent, "Unknown Exception", "", ExceptionType.Update, "", ccusername, IntegrationType.salesforce);
                    return BadRequest();
                }

                //Need to handle exceptions
                return Ok();

            }
            catch (Exception ex)
            {
                sentry.LogTheFailedRecord("Unknown Exception raised at CC Into API while requesting CC API To add NOTE", "Unknown Exception", "", ExceptionType.Update, "", ccusername, IntegrationType.salesforce);


                return BadRequest();
            }

        }

        /**
        * 
        * connectToSFAccount is a WebAPI method which accepts the following parameters to authenticate with sales force.
        * 
        * 1. sfClientId        SalesForce ClientID
        * 2. sfSecret          SalesForce Secret Key
        * 3. ccUserName        Username of Cloud cherry
        * 4. ccApiKey          Cloud cherry's API key which is generated using api call 'GetAPIKey'
        * 
        * ccUserName, ccApiKey are stored in Json format to use them further when ever needed.
        * 
        * this method will return a login url to salesforce when ever we click on connect @ integrations >> Salesforce page.
        * 
        **/
        [HttpGet]
        [Route("api/CCTOSF/connectToSFAccount")]
        public async Task<List<Dictionary<string, object>>> connectToSFAccount(string sfClientId, string sfSecret, string ccUserName, string ccApiKey, string sfRefreshToken)
        {
            List<Dictionary<string, object>> Mapping = new List<Dictionary<string, object>>();
            try
            {
                HttpResponseMessage response = null;
                if (sfRefreshToken == null)
                    sfRefreshToken = "";

                Dictionary<string, object> Connect = new Dictionary<string, object>();

                SalesForce sfObject = new SalesForce(sfClientId, sfSecret, ccUserName, ccApiKey, sfRefreshToken);

                if (sfRefreshToken == "")
                {
                    response = await sfObject.setSFRefreshToken();
                    Dictionary<string, object> singleuserdata = new Dictionary<string, object>();
                    singleuserdata.Add("username", ccUserName);
                    singleuserdata.Add("apikey", ccApiKey);
                    singleuserdata.Add("clientid", sfClientId);
                    singleuserdata.Add("secret", sfSecret);
                    singleuserdata.Add("rtoken", "");
                    singleuserdata.Add("instanceurl", "");
                    singleuserdata.Add("error", "");
                    cloudCherryController.AddNewUserCredentials(ccUserName, IntegrationType.salesforce, singleuserdata);

                    Connect.Add("SFurl", response.RequestMessage.RequestUri.AbsoluteUri);
                    Connect.Add("Error", "");
                }
                else
                {
                    Connect.Add("SFurl", "");
                    Connect.Add("Error", "");
                }
                Mapping.Add(Connect);

            }
            catch { }
            return Mapping;

        }



        #region salesforceauthentication

        /*
* Called from SF, while trying to authenticate to SF from CC, thsi  method verifies  SF authentication and establish the connection between SF and CC
*/
        [HttpGet]
        [Route("api/Connect2SF/AuthCode")]
        public async Task<HttpResponseMessage> AuthCode(string ccAccessToken, string code)
        {
            Connect2SFController objc = new Connect2SFController();
            try
            {
                string apiKey = ccAccessToken;
                SalesForce salesForce = new SalesForce();
                Cherry.HelperClasses.Cherry cherry = new Cherry.HelperClasses.Cherry();


                Dictionary<string, object> Exceptiondata = cloudCherryController.getClientsettingsByKey("apikey", apiKey, IntegrationType.salesforce);
                if (Exceptiondata == null)
                {
                    Redirecturl();
                }
                Clientid = Exceptiondata["clientid"].ToString();
                Secret = Exceptiondata["secret"].ToString();
                Rtoken = Exceptiondata["rtoken"].ToString();
                apiKey = Exceptiondata["apikey"].ToString();
                ccusername = Exceptiondata["username"].ToString();

                if (ccusername == "")
                {
                    var response1 = Request.CreateResponse(HttpStatusCode.Moved);
                    response1.Headers.Location = new Uri(SettingsUrl);
                    return response1;
                }

                HttpClient client = new HttpClient();
                string authEndPoint = "https://login.salesforce.com/services/oauth2/token";
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                var postCrendentials = new[] {
new KeyValuePair<string,string>("grant_type","authorization_code"),
new KeyValuePair<string,string>("client_id", Clientid),    
new KeyValuePair<string,string>("client_secret", Secret),  
new KeyValuePair<string,string>("redirect_uri", System.Configuration.ConfigurationManager.AppSettings["SFCallbackURL"]),
new KeyValuePair<string,string>("code",code)
};

                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, new Uri(authEndPoint));
                req.Content = new FormUrlEncodedContent(postCrendentials);
                HttpResponseMessage response = await client.SendAsync(req);
                string responseAsString = await response.Content.ReadAsStringAsync();
                var tokenStructure = new { refresh_token = string.Empty, access_token = string.Empty, expires_in = 0, instance_url = string.Empty };
                var token = JsonConvert.DeserializeAnonymousType(responseAsString, tokenStructure);
                string sId = token.access_token;

                if (token.access_token == null)
                {
                    Dictionary<string, object> singleuserdata = cloudCherryController.GetUserCredentials(ccusername, IntegrationType.salesforce);

                    if (singleuserdata != null)
                    {
                        singleuserdata["rtoken"] = token.refresh_token;
                        singleuserdata["instanceurl"] = token.instance_url;
                        singleuserdata["error"] = "Authentication failed, please enter valid clientid  or secret key";
                        cloudCherryController.UpdateUserCredentials(ccusername, singleuserdata, IntegrationType.salesforce);

                    }


                    return Redirecturl();
                }

                List<string> tokendet = await objc.ConnectSF(Clientid, Secret, token.refresh_token);
                List<string> casefields = await objc.getSFCaseFields(tokendet[1], tokendet[0]);
                if (casefields.Count > 1)
                {
                    string accesstoken = await objc.getCCAccessToken(ccusername, apiKey);
                    List<string> QuestionTags = await objc.GetQuestionsTags(accesstoken);
                    Dictionary<string, object> singleuserdata = cloudCherryController.GetUserCredentials(ccusername, IntegrationType.salesforce);

                    if (singleuserdata != null)
                    {
                        singleuserdata["rtoken"] = token.refresh_token;
                        singleuserdata["instanceurl"] = token.instance_url;
                        singleuserdata["error"] = "";
                        cloudCherryController.UpdateUserCredentials(ccusername, singleuserdata, IntegrationType.salesforce);

                    }
                    string integrationkey = ConfigurationManager.AppSettings[IntegrationType.salesforce.ToString()];
                    await cloudCherryController.PostIntegrationData(ccusername, apiKey, integrationkey, null);

                    for (int defaultcounter = 0; defaultcounter < defaultSFFields.Length; defaultcounter++)
                    {
                        foreach (string Qtag in QuestionTags)
                        {
                            if (defaultQuestionTags[defaultcounter].ToUpper() == Qtag.ToUpper())
                            {
                                await objc.AddInsertMapping(Qtag, defaultSFFields[defaultcounter], accesstoken, false, ccusername);
                                break;
                            }
                        }

                    }
                }
                else
                {
                    Dictionary<string, object> singleuserdata = cloudCherryController.GetUserCredentials(ccusername, IntegrationType.salesforce);
                    singleuserdata["rtoken"] = token.refresh_token;
                    singleuserdata["instanceurl"] = token.instance_url;
                    singleuserdata["error"] = "Refresh token was not created, please login with valid credentials";
                    string integrationkey = ConfigurationManager.AppSettings[IntegrationType.salesforce.ToString()];
                }

                var response2 = Request.CreateResponse(HttpStatusCode.Moved);
                response2.Headers.Location = new Uri(SettingsUrl);
                return response2;
            }
            catch (Exception ex) { objc.LogThisError(ex.Message); }
            return new HttpResponseMessage();
        }

        HttpResponseMessage Redirecturl()
        {
            var response2 = Request.CreateResponse(HttpStatusCode.Moved);
            response2.Headers.Location = new Uri(SettingsUrl);
            return response2;
        }

        #endregion
    }

}
