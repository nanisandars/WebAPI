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

/* This is used for CC API calls and to get required data */
namespace LTSAPI.Controllers
{
    public class CloudCherryController : ApiController
    {
               
        SalesForce salesForce = new SalesForce();
     
        string Usercredentialspath = ConfigurationManager.AppSettings["Credentials"];
        
        internal async Task<HttpResponseMessage> callApiEndPoint(KeyValuePair<string, string>[] postCrendentials, string authEndPoint)
        {
            HttpClient client = new HttpClient();
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, new Uri(authEndPoint));
            req.Content = new FormUrlEncodedContent(postCrendentials);
            HttpResponseMessage response = await client.SendAsync(req);
            string responseAsString = await response.Content.ReadAsStringAsync();
            return response;
        }

        internal void saveCredentials(string _ccUserName, string _ccApiKey, string _sfClientID, string _sfSecretKey)
        {
            string settingsPath = HttpContext.Current.Server.MapPath("~/" + ConfigurationManager.AppSettings["SettingsPath"]);
            List<CCSFSettings> CCSFSettingsList = getClientsettings(settingsPath);
            bool userExists = checkUserExists(_ccUserName, CCSFSettingsList);
            CCSFSettingsList = prepareClientSettings(CCSFSettingsList, userExists, _ccUserName, _ccApiKey, _sfClientID, _sfSecretKey);
            storeClientSettings(CCSFSettingsList, settingsPath);
        }

        internal void updateCredentials(string _ccUserName, string _refreshToken, string _instanceURL, string _error)
        {
            string settingsPath = HttpContext.Current.Server.MapPath("~/" + ConfigurationManager.AppSettings["SettingsPath"]);
            List<CCSFSettings> CCSFSettingsList = getClientsettings(settingsPath);

            foreach (CCSFSettings setting in CCSFSettingsList)
            {
                if (setting.Username == _ccUserName)
                {
                    setting.settings.Refreshtoken = _refreshToken;
                    setting.settings.SFInstanceurl = _instanceURL;
                    setting.settings.Errorstring = _error;
                }
            }

            storeClientSettings(CCSFSettingsList, settingsPath);
        }

        private void storeClientSettings(List<CCSFSettings> CCSFSettingsList, string settingsPath)
        {
            string settingsJson = JsonConvert.SerializeObject(CCSFSettingsList.ToArray());
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(settingsPath, false))
            {
                file.WriteLine(settingsJson);
            }
        }

        public  bool UpdateUserCredentials(string username, Dictionary<string, object> singleuserExceptiondata, IntegrationType iType)
        {
            try {
                string AllUsersData = RetreiveIntegrationData(Usercredentialspath);
                Dictionary<string, object> Userdata = JsonConvert.DeserializeObject<Dictionary<string, object>>(AllUsersData);
                if (!Userdata.Keys.Contains(username))
                    return false;
                Dictionary<string, object> Integrationdata = JsonConvert.DeserializeObject<Dictionary<string, object>>(Userdata[username].ToString());
                if (Integrationdata.Keys.Contains(iType.ToString()))
                {
                    Integrationdata.Remove(iType.ToString());
                }
                Integrationdata.Add(iType.ToString(), singleuserExceptiondata);

                Userdata.Remove(username);
                Userdata.Add(username, Integrationdata);
                InsertIntegrationData(JsonConvert.SerializeObject(Userdata), Usercredentialspath);
                return true;
            }
            catch { }
            return false;
        }

        public bool AddNewUserCredentials(string userName, IntegrationType IntegrationType, Dictionary<string, object> ClientCredentials)
        {
            try
            {
                string AllUsersData = RetreiveIntegrationData(Usercredentialspath);
                Dictionary<string, object> Userdata = JsonConvert.DeserializeObject<Dictionary<string, object>>(AllUsersData);
                Dictionary<string, object> Integrationdata = new Dictionary<string, object>();
                if (AllUsersData != "")
                {
                    if (Userdata.Keys.Contains(userName))
                    {
                        Integrationdata = JsonConvert.DeserializeObject<Dictionary<string, object>>(Userdata[userName].ToString());
                        if (Integrationdata.Keys.Contains(IntegrationType.ToString()))
                            return false;
                        Integrationdata.Add(IntegrationType.ToString(), ClientCredentials);
                        Userdata.Add(userName, Integrationdata);
                    }
                    else
                    {
                        Integrationdata.Add(IntegrationType.ToString(), ClientCredentials);
                        Userdata.Add(userName, Integrationdata);
                    }
                }

                else
                {
                    Userdata = new Dictionary<string, object>();
                    Integrationdata.Add(IntegrationType.ToString(), ClientCredentials);
                    Userdata.Add(userName, Integrationdata);
                }

                InsertIntegrationData(JsonConvert.SerializeObject(Userdata), Usercredentialspath);
                return true;

            }
            catch { }
            return false;
        }

        public Dictionary<string, object> GetUserCredentials(string userName, IntegrationType integrationtype)
        {
            try
            {
                string AllUsersData = RetreiveIntegrationData(Usercredentialspath);
                if (AllUsersData != "")
                {
                    Dictionary<string, object> Userdata = JsonConvert.DeserializeObject<Dictionary<string, object>>(AllUsersData);
                    if (Userdata.Keys.Contains(userName))
                    {
                        Dictionary<string, object> Integrationdata = JsonConvert.DeserializeObject<Dictionary<string, object>>(Userdata[userName].ToString());
                        return JsonConvert.DeserializeObject<Dictionary<string, object>>(Integrationdata[integrationtype.ToString()].ToString());
                    }
                }
            }
            catch { }
            return null;
        }
                
        public void InsertIntegrationData(string Integrationdata, string Settingpath)
        {
            string logPath = HttpContext.Current.Server.MapPath("~/" + Settingpath);
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(logPath, false))
            {
                file.WriteLine(Integrationdata);
            }
        }

        public string RetreiveIntegrationData(string settingspath)
        {
            string logPath = HttpContext.Current.Server.MapPath("~/" + settingspath);
            using (StreamReader r = new StreamReader(logPath))
            {
                return r.ReadToEnd();
            }
        }

        private List<CCSFSettings> prepareClientSettings(List<CCSFSettings> CCSFSettingsList, bool userExists, string ccUserName, string ccApiKey, string sfClientID, string sfSecretKey)
        {
            CCSFSettings ccSfSettings = new CCSFSettings();
            if (CCSFSettingsList == null)
            {
                CCSFSettingsList = new List<CCSFSettings>();
            }
            if (userExists)
            {
                foreach (CCSFSettings usersetting in CCSFSettingsList)
                {
                    if (usersetting.Username == ccUserName)
                    {
                        ccSfSettings = usersetting;
                        CCSFSettingsList.Remove(usersetting);
                        break;
                    }


                }
            }

            ccSfSettings.Username = ccUserName;
            ccSfSettings.settings.Apikey = ccApiKey;
            ccSfSettings.settings.Username = ccUserName;
            ccSfSettings.settings.SFClientid = sfClientID;
            ccSfSettings.settings.SFSecret = sfSecretKey;
            ccSfSettings.settings.Refreshtoken = null;
            ccSfSettings.settings.SFInstanceurl = null;
            ccSfSettings.settings.Errorstring = null;
            CCSFSettingsList.Add(ccSfSettings);
            return CCSFSettingsList;
        }

        private bool checkUserExists(string _ccUserName, List<CCSFSettings> CCSFSettingsList)
        {
            bool userExists = false;
            string userNames = "|";
            string Encyptusername = _ccUserName;
            if (CCSFSettingsList != null)
            {
                foreach (CCSFSettings ccSFSetting in CCSFSettingsList)
                {
                    userNames = userNames + ccSFSetting.Username + "|";
                }
            }
            else
            {
                userExists = false;
            }
            if (userNames.Contains("|" + Encyptusername + "|"))
            {
                userExists = true;
            }
            return userExists;
        }

        internal List<CCSFSettings> getClientsettings(string settingsPath)
        {
            List<CCSFSettings> CCSFSettingsList;
            using (StreamReader r = new StreamReader(settingsPath))
            {
                string json = r.ReadToEnd();
                CCSFSettingsList = JsonConvert.DeserializeObject<List<CCSFSettings>>(json);
            }
            return CCSFSettingsList;
        }

        internal Dictionary<string, object> getFDClientsettings(string settingsPath)
        {
            Dictionary<string, object> CCFDSettingsList;
            using (StreamReader r = new StreamReader(settingsPath))
            {
                string json = r.ReadToEnd();
                CCFDSettingsList = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            }
            return CCFDSettingsList;
        }

        public string getFDClientsettingsByCCAPI(string Key, string Value, ref string UserName)
        {
            string settingsPath = HttpContext.Current.Server.MapPath("~/" + ConfigurationManager.AppSettings["FDSettingsPath"]);
            Dictionary<string, object> ccfduser = getFDClientsettings(settingsPath);

            if (ccfduser == null)
                return null;

            foreach (var item in ccfduser)
            {

                Dictionary<string, object> obj = new Dictionary<string, object>();
                obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(item.Value.ToString());
                foreach (var objkey in obj)
                {
                    if ((objkey.Key.ToString().ToUpper() == Key.ToUpper()) && (objkey.Value.ToString() == Value))
                    {
                        UserName = item.Key.ToString();
                        return item.Value.ToString();
                    }
                }

            }
            return null;
        }

        public string getFDClientsettingsByUserName(string Key, string Value)
        {
            string settingsPath = HttpContext.Current.Server.MapPath("~/" + ConfigurationManager.AppSettings["FDSettingsPath"]);
            Dictionary<string, object> ccfduser = getFDClientsettings(settingsPath);

            if (ccfduser == null)
                return null;

            foreach (var item in ccfduser)
            {
                if (item.Key.ToString() == Value)
                {
                    return item.Value.ToString();
                }
            }

            return null;
        }
        
        public Dictionary<string, object> getClientsettingsByKey(string Key, string Value, IntegrationType integrationtype)
        {
            string AllUsersData = RetreiveIntegrationData(Usercredentialspath);
            if (AllUsersData == "")
                return null;
           Dictionary<string, object> Userdata = JsonConvert.DeserializeObject<Dictionary<string, object>>(AllUsersData);
           Dictionary<string, object> Integrationdata = new Dictionary<string, object>();
           Dictionary<string, object> Exceptiondata = new Dictionary<string, object>();

           foreach (KeyValuePair<string, object> KP in Userdata)
           {
               Integrationdata = JsonConvert.DeserializeObject<Dictionary<string, object>>(Userdata[KP.Key].ToString());
               if (Integrationdata.Keys.Contains(integrationtype.ToString()))
               {
                   Exceptiondata = JsonConvert.DeserializeObject<Dictionary<string, object>>(Integrationdata[integrationtype.ToString()].ToString());

                   if (Exceptiondata.Keys.Contains(Key))
                   {
                       if (Exceptiondata[Key].ToString() == Value)
                       {
                           return Exceptiondata;
                       }
                   }
               }
            }
          return null;
        }
        
        [HttpGet]
        [Route("api/CloudCherry/GetQuestions")]
        public async Task<List<Question>> GetQuestions(string Access_token)
        {
            try
            {
                string apiendpoint = "https://api.getcloudcherry.com";
                var client = new HttpClient();
                string url = apiendpoint + "/api/Questions/Active";
                HttpRequestMessage queryrequest = new HttpRequestMessage(HttpMethod.Get, url);
               
                //Add Bearer Token To Authenticate This Stateless Request
                queryrequest.Headers.Add("Authorization", "Bearer " + Access_token);
                var queryresponse = await client.SendAsync(queryrequest);
                var responseBody = await queryresponse.Content.ReadAsStringAsync();
               
                //Deseralize
                var QueStructure = new { LocationId = string.Empty, ResponseDateTime = new DateTime(), Responses = new List<Response>() };
                var Ques = JsonConvert.DeserializeObject<List<Question>>(responseBody);
                return Ques;
            }
            catch (Exception ex) { LogThisError(ex.Message); }
            return new List<Question>();
        }

        [HttpGet]
        [Route("api/CloudCherry/GetTagFieldMapping")]
        public async Task<List<Dictionary<string, object>>> GetTagFieldMapping(string Username, string password, string Key)
        {
            try
            {
                string Access_token = await getCCAccessToken(Username, password);
                var Integrationdata = await GetCCMappings(Access_token, Key);
                var map = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(Integrationdata["Mappings"].ToString());
                return map;
            }
            catch (Exception ex) { LogThisError(ex.Message); }
            return null;
        }

        [HttpGet]
        [Route("api/CloudCherry/GetAPIKey")]
        public async Task<string> GetAPIKey(string Username, string Password)
        {
            try
            {
                string apiendpoint = "https://api.getcloudcherry.com";
                string Access_token = await getCCAccessToken(Username, Password);

                var client = new HttpClient();
                string url = apiendpoint + "/api/GetAPIKey";
                HttpRequestMessage queryrequest = new HttpRequestMessage(HttpMethod.Get, url);

                //Add Bearer Token To Authenticate This Stateless Request
                queryrequest.Headers.Add("Authorization", "Bearer " + Access_token);
                var queryresponse = await client.SendAsync(queryrequest);
                var responseBody = await queryresponse.Content.ReadAsStringAsync();
                responseBody = responseBody.Replace("\"", "");
                return responseBody.ToString();
            }
            catch { }
            return "";
        }

        [HttpGet]
        [Route("api/CloudCherry/GetExistingQuestionTags")]
        public async Task<List<string>> GetExistingQuestionTags(string Username)
        {
            try
            {
                Dictionary<string, object> exceptiondata = getClientsettingsByKey("username", Username, IntegrationType.salesforce);
                if (exceptiondata == null)
                    return null;
                string Clientid = exceptiondata["clientid"].ToString();
                string Secret = exceptiondata["secret"].ToString();
                string Rtoken = exceptiondata["rtoken"].ToString();
                string apikey = exceptiondata["apikey"].ToString();

                string AccessToken = await getCCAccessToken(Username, apikey);
                return await GetQuestionsTags(AccessToken);

            }
            catch { }
            return new List<string>();

        }
        
        [HttpGet]
        [Route("api/CloudCherry/GetQuestionsTags")]
        ///Retreiving the list of question
        public async Task<List<string>> GetQuestionsTags(string Access_token)
        {
            try
            {
                var Ques = await GetQuestions(Access_token);
                List<string> Qids = new List<string>();
                foreach (var q in Ques)
                {
                    if (q.QuestionTags == null)
                        continue;
                    foreach (var tag in q.QuestionTags)
                    {
                        if (Qids.Contains(tag))
                            continue;
                        Qids.Add(tag);
                    }

                }
                return Qids;
            }
            catch (Exception ex) { LogThisError(ex.Message); }
            return new List<string>();
        }
        
        [HttpGet]
        [Route("api/CloudCherry/GetQuestionsTagsandId")]
        ///Retreiving the list of question
        public async Task<List<Dictionary<string, object>>> GetQuestionsTagsandId(string Access_token)
        {
            try
            {
                var Ques = await GetQuestions(Access_token);
                string Questionstring = "|";
                List<Dictionary<string, object>> tagDetails = new List<Dictionary<string, object>>();
                foreach (var q in Ques)
                {
                    if (q.QuestionTags == null)
                        continue;
                    Dictionary<string, object> Qids = new Dictionary<string, object>();
                    foreach (var tag in q.QuestionTags)
                    {
                        if (Questionstring.Contains("|" + tag + "|"))
                            continue;
                        Questionstring = Questionstring + tag + "|";
                        Qids.Add("tag", tag);
                        Qids.Add("qid", q.Id);
                        tagDetails.Add(Qids);

                    }

                }
                return tagDetails;
            }
            catch (Exception ex) { LogThisError(ex.Message); }
            return new List<Dictionary<string, object>>();
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
        
        [HttpGet]
        [Route("api/CloudCherry/getCCAccessToken")]
        public async Task<string> getCCAccessToken(string ccuserName, string ccpassword)
        {
            HttpClient client = new HttpClient();

            string ccAuthEndPoint = "https://api.getcloudcherry.com/api/LoginToken";

            var CCCrendentials = new[] {
                        new KeyValuePair<string,string>("grant_type","password"),
                        new KeyValuePair<string,string>("username",ccuserName),
                        new KeyValuePair<string,string>("password",ccpassword)
                    };

            HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, new Uri(ccAuthEndPoint));
            req.Content = new FormUrlEncodedContent(CCCrendentials);

            HttpResponseMessage ccresponse = await client.SendAsync(req);
            string responseAsString = await ccresponse.Content.ReadAsStringAsync();
            var tokenStructure = new { access_token = string.Empty, expires_in = 0, instance_url = string.Empty };
            var token = JsonConvert.DeserializeAnonymousType(responseAsString, tokenStructure);
            return token.access_token;
        }

        public async Task<string> GetTagQuestions(string Tag, string Access_token)
        {
            try
            {
                string IdList = "";
                var Ques = await GetQuestions(Access_token);
                foreach (var q in Ques)
                {
                    if (q.QuestionTags == null)
                        continue;
                    if (q.QuestionTags.Contains(Tag))
                        return q.Id;

                }
                return IdList;

            }
            catch (Exception ex) { LogThisError(ex.Message); }
            return "";
        }
                
        //Retreives  first questionid for the tag specified.
        public async Task<string> GetSingleTagQuestionID(string Tag, string Access_token)
        {
            try
            {
                string IdList = "";
                var Ques = await GetQuestions(Access_token);
                foreach (var q in Ques)
                {
                    if (q.QuestionTags == null)
                        continue;
                    if (q.QuestionTags.Contains(Tag))
                    {
                        IdList = q.Id;
                        break;
                    }
                }
                return IdList;
            }
            catch (Exception ex) { LogThisError(ex.Message); }
            return "";
        }

        public async Task saveDefaultMappings(string userName, string key)
        {
            string httpResponse = "";
            //REad the resposne from the request

            using (var contentStream = await this.Request.Content.ReadAsStreamAsync())
            {
                contentStream.Seek(0, SeekOrigin.Begin);
                using (var sr = new StreamReader(contentStream))
                {
                    httpResponse = sr.ReadToEnd();
                }
            }

            if (httpResponse == "")
            {
                return;
            }
            List<Dictionary<string, object>> retryRecords = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(httpResponse); //Converting the response into dictionary collection 

        }

        public async Task PostIntegrationData(string Username, string Password, string Key, object Mappings)
        {
            try
            {
                //Get AccessToken from CC
                string ccAccessToken = await getCCAccessToken(Username, Password);
                var client = new HttpClient();
                string apiendpoint = "https://api.getcloudcherry.com";
                string ccURLforAddNote = apiendpoint + "/api/UserData/" + Key;
                HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Post, new Uri(ccURLforAddNote));
                               
                Request.Headers.Add("Authorization", "Bearer " + ccAccessToken);
                Dictionary<string, string> Dicobject = new Dictionary<string, string>();
                Dicobject.Add("value", JsonConvert.SerializeObject(Mappings));
                var serializeNote = JsonConvert.SerializeObject(Dicobject);
                Request.Content = new StringContent(serializeNote, Encoding.UTF8, "application/json");
                HttpResponseMessage addNoteResponse = await client.SendAsync(Request);
            }
            catch { }

        }

        public async Task PostIntegrationData(string Username, string Password, string Key, string Mappings)
        {
            try
            {
                //Get AccessToken from CC
                string ccAccessToken = await getCCAccessToken(Username, Password);
                var client = new HttpClient();
                string apiendpoint = "https://api.getcloudcherry.com";
                string ccURLforAddNote = apiendpoint + "/api/UserData/" + Key;
                HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Post, new Uri(ccURLforAddNote));
               
               
                Request.Headers.Add("Authorization", "Bearer " + ccAccessToken);
                Dictionary<string, string> Dicobject = new Dictionary<string, string>();
                Dicobject.Add("value", Mappings);
                var serializeNote = JsonConvert.SerializeObject(Dicobject);
                Request.Content = new StringContent(serializeNote, Encoding.UTF8, "application/json");
                HttpResponseMessage addNoteResponse = await client.SendAsync(Request);
            }
            catch { }
        }

        public async Task createIntegrationList(string Username, string Password, string Key)
        {
            try
            {
                string accesstoken = await getCCAccessToken(Username, Password);
                string responseBody = await GetCCUserdata(accesstoken, "integrations.list");
                bool containskey = false;
                if (responseBody != "null")
                {
                    responseBody = JsonConvert.DeserializeObject(responseBody).ToString();
                    Key = Key.Replace("integrations.", "");
                    containskey = responseBody.Contains(Key);
                    if (!containskey)
                        responseBody = responseBody.Replace("]", ",\"" + Key.Replace("integrations.", "") + "\"]");
                    else
                        containskey = true;
                }
                else
                {
                    responseBody = "";
                    responseBody = "[\"" + Key.Replace("integrations.", "") + "\"]";
                    containskey = false;
                }
                if (!containskey)
                    await PostIntegrationData(Username, Password, "integrations.list", responseBody);
            }
            catch
            {

            }
        }

        [HttpGet]
        [Route("api/Sentry/GetCCMappings")]
        public async Task<Dictionary<string, object>> GetCCMappings(string accesstoken, string Key)
        {

            try
            {
                string responseBody = await GetCCUserdata(accesstoken, Key);
                responseBody = responseBody.Replace("\\", "");
                responseBody = responseBody.Replace("\"", "'");
                responseBody = responseBody.Replace("'{", "{");
                responseBody = responseBody.Replace("}'", "}");

                Dictionary<string, object> Integrationdata = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);

                return Integrationdata;
            }
            catch { }
            return new Dictionary<string, object>();
        }

        public async Task<string> GetCCUserdata(string accesstoken, string Key)
        {
            try
            {
                string path = "https://api.getcloudcherry.com/api/UserData/" + Key;               
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, path);
                req.Headers.Add("Authorization", "Bearer " + accesstoken);
                HttpResponseMessage res = new HttpResponseMessage();
                HttpClient client = new HttpClient();
                res = await client.SendAsync(req);
                string responseBody = await res.Content.ReadAsStringAsync();
                return responseBody;
            }
            catch { return ""; }

        }

        public Question GetFirstQuestionofTag(string tag, string accessToken, List<Question> questionsList, ref string errorMessage)
        {
            try
            {
                if (tag == null || tag == "")
                {
                    errorMessage = "Invalid tag";
                    return null;
                }
                if (accessToken == null || accessToken == "")
                {
                    errorMessage = "Unable to connect to Cloud Cherry";
                    return null;
                }

                foreach (Question question in questionsList)
                {
                    if ((question.QuestionTags == null || question.QuestionTags.Count == 0))
                        continue;
                    string tagtext = "";
                    for (int cntr = 0; cntr < question.QuestionTags.Count; cntr++)
                    {
                        tagtext = tagtext + "|" + question.QuestionTags[cntr].ToLower();
                    }

                    if (tagtext.ToString().ToUpper().Contains(tag.ToUpper()))
                        return question;
                }
                errorMessage = "No question exist";
            }
            catch (TimeoutException timeoutException)
            {
                errorMessage = timeoutException.Message;
                return null;
            }
            catch (Exception exception)
            {
                errorMessage = exception.Message;
                return null;
            }

            return null;
        }

        /// <summary>
        /// Inserts Given mapping in to CC
        /// </summary>
        /// <param name="_id">Uniqueid</param>
        /// <param name="Tagid">CC Tagname</param>
        /// <param name="qid">Questionid</param>
        /// <param name="Field">Sales force field</param>
        /// <param name="disabled">Is disabled or not</param>
        /// <param name="error">Error Message (if exist)</param>
        /// <param name="accesstoken">CC Access token</param>
        /// <returns></returns>
        public async Task Insertmapping(string _id, string Tagid, string qid, string Field, string disabled, string error, string accesstoken, string Key)
        {

            try
            {
                Dictionary<string, object> Singlemap = new Dictionary<string, object>();
               
                //Retreiving the existing mapping  from CC
                Dictionary<string, object> Mappings = await GetCCMappings(accesstoken, Key);

                if (qid == "")
                {
                    string errorMessage = "";
                    var ques = await GetQuestions(accesstoken);
                   
                    //Retreiving  the first question for the given  CC Tag
                    Question Q = GetFirstQuestionofTag(Tagid, accesstoken, ques, ref  errorMessage);
                    qid = Q.Id;
                }
                
                //Forming a single map
                Singlemap.Add("_id", _id);
                Singlemap.Add("tag", Tagid);
                Singlemap.Add("qid", qid);
                Singlemap.Add("field", Field);
                Singlemap.Add("disabled", disabled);
                Singlemap.Add("error", error);
                Singlemap.Add("time", DateTime.Now);

                List<Dictionary<string, object>> NewMapping = new List<Dictionary<string, object>>();
                NewMapping.Add(Singlemap);
              
                //Verifying  if  mapping already exist in CC  if exist appends the new map to existing esle creates the new map
                if (Mappings==null|| Mappings.Count == 0)
                {
                    Mappings = new Dictionary<string, object>();
                    Mappings.Add("Mappings", NewMapping);
                    Mappings.Add("Login", null);
                    Mappings.Add("MappingsBackup", NewMapping);
                }
                else
                {
                    List<Dictionary<string, object>> Existingmap = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(Mappings["Mappings"].ToString());
                    Existingmap.Add(Singlemap);
                    Mappings.Remove("Mappings");
                    Mappings.Remove("MappingsBackup");
                    Mappings.Add("MappingsBackup", Existingmap);
                    Mappings.Add("Mappings", Existingmap);
                }
                
                //Calling POST API to insert the mapping data
                await PostMappings(Mappings, accesstoken, Key);
            }
            catch { }
        }
        
        /// <summary>
        /// Posting the Mapping list into CC
        /// </summary>
        /// <param name="Mappings">Mapping list</param>
        /// <param name="accesstoken">CC Accesstoken</param>
        /// <returns></returns>
        public async Task PostMappings(Dictionary<string, object> Mappings, string accesstoken, string Key)
        {
            Dictionary<string, string> Mapdata = new Dictionary<string, string>();
            Mapdata.Add("value", JsonConvert.SerializeObject(Mappings));

            string ccurl = "https://api.getcloudcherry.com/api/UserData/" + Key;
            //POST /api/UserData/{key}

            HttpRequestMessage addNoteRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(ccurl));
            //Get AccessToken from CC
            var serializeNote = JsonConvert.SerializeObject(Mapdata);

            addNoteRequest.Headers.Add("Authorization", "Bearer " + accesstoken);
            addNoteRequest.Content = new StringContent(serializeNote, Encoding.UTF8, "application/json");
            HttpClient client = new HttpClient();

            //Get Response from the CC API
            HttpResponseMessage addNoteResponse = await client.SendAsync(addNoteRequest);
            string addNoteRespContent = await addNoteResponse.Content.ReadAsStringAsync();
        }

        public List<string> GetQuestionTagsforLocation(Question question, List<Question> questionList)
        {
            try
            {

                List<string> finalQuestionTagList = new List<string>();

                finalQuestionTagList.AddRange(question.QuestionTags);

                foreach (Question currentQuestion in questionList)
                {
                    if (currentQuestion.QuestionTags == null)
                        continue;
                    IEnumerable<string> remaingingLocationList = currentQuestion.DisplayLocation.ToList().Except(question.DisplayLocation.ToList());

                    if ((remaingingLocationList.ToList().Count != currentQuestion.DisplayLocation.Count) || (question.DisplayLocation.Count == 0))
                    {
                        IEnumerable<string> remainingQuestionTagList = currentQuestion.QuestionTags.Except(finalQuestionTagList);
                        finalQuestionTagList.AddRange(remainingQuestionTagList);
                    }

                }

                return finalQuestionTagList.OrderBy(q => q).ToList();

            }
            catch { }
            return null;
        }

        [HttpGet]
        [Route("api/CloudCherry/GetTagonEdit")]
        public async Task<List<string>> GetTagonEdit(string tag, string accessToken)
        {
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
                Question question = GetFirstQuestionofTag(tag, accessToken, questionList, ref errorMessage);
                if (question == null)
                {
                    finallist.Add("Error:" + errorMessage);
                    return finallist;
                }

                finallist = GetQuestionTagsforLocation(question, questionList);
                return finallist;
            }
            catch { }
            finallist.Add("Error:Unable to retreive question tags");
            return finallist;
        }

        [HttpGet]
        [Route("api/CloudCherry/GetTagonEditbyQuestion")]
        public async Task<List<string>> GetTagonEditbyQuestion(string Questionid, string accessToken)
        {
            try
            {
                List<string> finallist = new List<string>();
                List<Question> questionList = await GetQuestions(accessToken);
                Question singleQuestiondata = new Question();

                foreach (Question SingleQuestion in questionList)
                {
                    if (SingleQuestion.Id == Questionid)
                    {
                        singleQuestiondata = SingleQuestion;
                        break;
                    }
                }

                finallist = GetQuestionTagsforLocation(singleQuestiondata, questionList);
                return finallist;
            }
            catch { }
            return new List<string>();

        }
    }
}
