using Cherry.HelperClasses;
using LTSAPI.CC_Classes;
using LTSAPI.Controllers;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using LTSAPI.HelperClasses;
using LTSAPI.FD_Classes;
using System.Net.Security;

/* This is used to get and update information between the systems */

namespace LTSAPI.Controllers
{
    public class FDCCController : ApiController
    {
        //Sentry 
        string previousissueid = "";
        //Gets the FD integration key from config file
        CloudCherryController cloudCherryController = new CloudCherryController();
        string FDSettingsPath = ConfigurationManager.AppSettings["Credentials"];
        string ExceptionRaised = "";
        string ExceptionRaisedMessage = "";
        SentryController sentry = new SentryController();
        //Gets User's data from the FD JSON File

        FreshDesk freshDesk = new FreshDesk();
        IntegrationType integrationType = IntegrationType.freshdesk;



        [HttpGet]
        [Route("api/FDCC/IsCCUserFDAuthenticated")]
        public async Task<Dictionary<string, object>> IsCCUserFDAuthenticated(string ccUsername)
        {
            try
            {  Dictionary<string, object> FDlogindata = cloudCherryController.GetUserCredentials(ccUsername, IntegrationType.freshdesk);
              
                char[] splitstr = { '|' };
                string[] integrationdetails = FDlogindata["integrationDetails"].ToString().Split(splitstr);
           	string     FDAPIKey = integrationdetails[1];
	        string     FDURL = integrationdetails[0];
              
                if (FDlogindata != null)
                {
                    return await GetCCTagsandFdFields(ccUsername, FDlogindata["ccapikey"].ToString(), FDAPIKey,FDURL);
                }
            }
            catch { return new Dictionary<string, object>(); }
            return new Dictionary<string, object>();
        }


        //Gets the Tags and their related DataTypes in CC 
        [HttpGet]
        [Route("api/cloudCherryController/GetCCQTagsNDType")]
        ///Retreiving the list of question
        public async Task<Dictionary<string, object>> GetCCQTagsNDType(string Access_token)
        {
            try
            {
                List<Question> Ques = await cloudCherryController.GetQuestions(Access_token); //Gets the CC all active questions from CC
                Dictionary<string, object> qtnTagNType = new Dictionary<string, object>();
                foreach (var q in Ques)
                {
                    if (q.QuestionTags == null)
                        continue;

                    foreach (var tag in q.QuestionTags)
                    {
                        if (qtnTagNType.Keys.Contains(tag))
                            continue;
                        if (tag.ToString().ToUpper() != "NPS")
                        {
                            qtnTagNType.Add(tag.ToLower(), tag.ToLower() + "::" + q.DisplayType.ToLower() + "::" + q.Id);
                        }
                    }

                }

                return qtnTagNType;
            }
            catch (Exception ex)
            {
            }
            return new Dictionary<string, object>();
        }

        public async Task<Dictionary<string, object>> GetCCTagsandFdFields(string Username, string CCAPIKey, string FDKey, string FDUrl)
        {
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                //Gets the CC tags and FD fields                 
                Dictionary<string, object> CCFDdata = new Dictionary<string, object>();
                Dictionary<string, object> objFDFields = await getFDTicketFields(FDKey, FDUrl);

                //Removing the standard fields and NPSSCORE and CCTICKET custom fields from the dropdown in the settings screen

                objFDFields.Remove("requester");
                objFDFields.Remove("status");
                objFDFields.Remove("priority");
                objFDFields.Remove("source");
                objFDFields.Remove("ticket_type");
                objFDFields.Remove("group");
                objFDFields.Remove("agent");
                objFDFields.Remove("company");
                objFDFields.Remove("product");



                CCFDdata.Add("FDTicketField", objFDFields);

                string accesstoken = await cloudCherryController.getCCAccessToken(Username, CCAPIKey);

                Dictionary<string, object> CCTag = new Dictionary<string, object>();
                Dictionary<string, object> tmpTags = new Dictionary<string, object>();
                List<string> CCAdditionalTags = new List<string>();
                CCTag = await GetCCQTagsNDType(accesstoken);
                tmpTags = await GetCCQTagsNDType(accesstoken);
                List<string> tempCCQTags = new List<string>();
                int counter = 0;

                //Checks whether the 5 required tags are existed in CC or NOT.
                List<string> tags = new List<string>() { "NAME", "EMAIL", "MOBILE", "SUBJECT", "DESCRIPTION" };
                foreach (var item in tmpTags)
                {

                    switch (item.Key.ToUpper())
                    {
                        case "NAME":
                            {
                                tempCCQTags.Add(item.Key.ToString());
                                tags.Remove("NAME");
                                CCTag.Remove(item.Key);
                                counter++;
                                break;
                            }

                        case "EMAIL":
                            {
                                tempCCQTags.Add(item.Key.ToString());
                                tags.Remove("EMAIL");
                                CCTag.Remove(item.Key);
                                counter++;
                                break;
                            }
                        case "MOBILE":
                            {
                                tempCCQTags.Add(item.Key.ToString());
                                tags.Remove("MOBILE");
                                CCTag.Remove(item.Key);
                                counter++;
                                break;
                            }
                        case "DESCRIPTION":
                            {
                                tempCCQTags.Add(item.Key.ToString());
                                tags.Remove("DESCRIPTION");
                                CCTag.Remove(item.Key);
                                counter++;
                                break;
                            }
                        case "SUBJECT":
                            {
                                tempCCQTags.Add(item.Key.ToString());
                                tags.Remove("SUBJECT");
                                CCTag.Remove(item.Key);
                                counter++;
                                break;
                            }
                    }
                }

                //Displays the missing required CC tags 
                if (counter != 5)
                {
                    string tempTag = "";
                    foreach (string tag in tags)
                    {
                        tempTag = tempTag + tag + ", ";
                    }
                    CCFDdata.Add("Error", "Please create the following tags in CC .  " + tempTag + " .    Note: FD needs  the NAME, EMAIL , MOBILE , SUBJECT , DESCRIPTION  fields as compulsory");
                }
                else if (counter == 5)
                {

                    bool checkDefaultMappings = false;
                    int checkCountOfMappings = 0;
                    Dictionary<string, object> existingMappings = await GetFDMappings(accesstoken);
                    if (existingMappings != null)
                    {
                        foreach (var item in existingMappings)
                        {
                            if (item.Key == "Mappings")
                            {
                                List<Dictionary<string, object>> existingFDMappings = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(item.Value.ToString());
                                foreach (Dictionary<string, object> dicOjb in existingFDMappings)
                                {
                                    foreach (var key in dicOjb)
                                    {
                                        if (key.Key.ToUpper() == "TAG")
                                        {

                                            if ((key.Value.ToString().ToUpper() == "MOBILE") || (key.Value.ToString().ToUpper() == "EMAIL") || (key.Value.ToString().ToUpper() == "NAME") || (key.Value.ToString().ToUpper() == "SUBJECT") || (key.Value.ToString().ToUpper() == "DESCRIPTION"))
                                            {
                                                checkCountOfMappings++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }


                    if (checkCountOfMappings == 5)
                    {
                        checkDefaultMappings = true;
                    }

                    //Checks whether the default mappings are created or not if existed then saves the default mappings
                    if (!checkDefaultMappings)
                        await saveDefaultFDFieldMappings(Username, CCAPIKey, tempCCQTags, FDKey, FDUrl);
                    CCFDdata.Add("Error", "");
                }

                //CCFDdata.Add("CCTag", CCTag);
                return CCFDdata;
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>();
            }
        }

        [HttpGet]
        [Route("api/FDCC/GetExistingQuestionTags")]
        public async Task<Dictionary<string, object>> GetExistingQuestionTags(string Username)
        {
            string FDAPIKey = "";
            string FDURL = "";
            string CCApikey = "";
            try
            {
                Dictionary<string, object> Userdata = cloudCherryController.GetUserCredentials(Username, IntegrationType.freshdesk);
                CCApikey = Userdata["ccapikey"].ToString();
                char[] splitstr = { '|' };
                string[] integrationdetails = Userdata["integrationDetails"].ToString().Split(splitstr);
                FDAPIKey = integrationdetails[1];
                FDURL = integrationdetails[0];
                string AccessToken = await cloudCherryController.getCCAccessToken(Username, CCApikey);
                return await GetCCQTagsNDType(AccessToken);

            }
            catch { }
            return new Dictionary<string, object>();

        }

        //Saves the default mappings in CC user data
        public async Task<HttpStatusCode> saveDefaultFDFieldMappings(string CCUName, string CCAPIKey, List<string> CCQTags, string FDKey, string FDUrl)
        {
            try
            {

                string ccAccessToken = await cloudCherryController.getCCAccessToken(CCUName, CCAPIKey);
                int counter = 0;
                //Creating the default mappings 
                foreach (string qTag in CCQTags)
                {
                    List<Dictionary<string, object>> map = new List<Dictionary<string, object>>();
                    Dictionary<string, object> Mappings = await GetFDMappings(ccAccessToken);

                    Dictionary<string, object> defaultMappings = new Dictionary<string, object>();
                    Random r = new Random();
                    string tagid = "tag_" + r.Next(10000) + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + DateTime.Now.Hour + DateTime.Now.Minute + DateTime.Now.Second + DateTime.Now.Millisecond;
                    string errorMessage = "";
                    var ques = await cloudCherryController.GetQuestions(ccAccessToken);
                    Question Q = cloudCherryController.GetFirstQuestionofTag(qTag, ccAccessToken, ques, ref  errorMessage);
                    defaultMappings.Add("_Id", tagid);
                    defaultMappings.Add("Tag", qTag);
                    defaultMappings.Add("QtnID", Q.Id);
                    defaultMappings.Add("Field", qTag);
                    defaultMappings.Add("disabled", false);
                    map.Add(defaultMappings);
                    if (Mappings == null)
                    {
                        Mappings = new Dictionary<string, object>();
                        Mappings.Add("FDAPIKey", FDKey);
                        Mappings.Add("FDURL", FDUrl);
                        Mappings.Add("Mappings", map);
                    }
                    else
                    {

                        List<Dictionary<string, object>> Existingmap = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(Mappings["Mappings"].ToString());
                        Existingmap.Add(defaultMappings);
                        Mappings.Remove("Mappings");
                        Mappings.Remove("FDAPIKey");
                        Mappings.Remove("FDURL");
                        Mappings.Add("FDAPIKey", FDKey);
                        Mappings.Add("FDURL", FDUrl);
                        Mappings.Add("Mappings", Existingmap);
                    }

                    string serializeMapping = JsonConvert.SerializeObject(Mappings);

                    Dictionary<string, object> jsonMappings = new Dictionary<string, object>();
                    jsonMappings.Add("value", serializeMapping);
                    string userDataFD = JsonConvert.SerializeObject(jsonMappings);

                    //Sends the request to cc to save the default mappings in cc
                    string apiPostUserData = "https://api.getcloudcherry.com/api/UserData/" + ConfigurationManager.AppSettings["FDKey"].ToString();
                    HttpRequestMessage requestPostUserData = new HttpRequestMessage(HttpMethod.Post, apiPostUserData);
                    requestPostUserData.Headers.Add("Authorization", "Bearer " + ccAccessToken);
                    requestPostUserData.Content = new StringContent(userDataFD, Encoding.UTF8, "application/json");
                    HttpClient client = new HttpClient();
                    HttpResponseMessage responsePostUserData = new HttpResponseMessage();
                    responsePostUserData = await client.SendAsync(requestPostUserData);
                    if (responsePostUserData.IsSuccessStatusCode)
                    {
                        counter++;
                    }
                }
                if (counter == 5)
                    return HttpStatusCode.OK;

                return HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                return HttpStatusCode.BadRequest;
            }
        }

        //Gets the saved field mappings 
        [HttpGet]
        [Route("api/FDCC/GetFDMappings")]
        public async Task<Dictionary<string, object>> GetFDMappings(string accesstoken)
        {
            try
            {
                string path = "https://api.getcloudcherry.com/api/UserData/" + ConfigurationManager.AppSettings["FDKey"].ToString();
                //POST /api/UserData/{key}
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, path);
                req.Headers.Add("Authorization", "Bearer " + accesstoken);
                HttpResponseMessage res = new HttpResponseMessage();
                HttpClient client = new HttpClient();

                res = await client.SendAsync(req);
                string responseBody = await res.Content.ReadAsStringAsync();
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

        //Raises a ticket at FD
        [HttpPost]
        [Route("api/FDCC/AddATicket")]
        public async Task<HttpResponseMessage> AddATicket(NotificationData ccData)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            string FDAPIKey = "";
            string FDURL = "";
            string CCApikey = "";
            string reqMobile = "";
            string reqName = "";
            string reqEmail = "";
            string ticketDescription = "";
            string ticketSubject = "";
            string ccUserName = ccData.answer.user;
            string objTicketData = "";
            string surveydata = "";
            HttpResponseMessage msg = new HttpResponseMessage();
            try
            {
                if (ccData.answer != null)
                {
                    //Gets the CC SURVEY data from the users of CC

                    //Logs the cc user's survey response 
                    surveydata = JsonConvert.SerializeObject(ccData.answer);
                    //await sentry.LogTheResponseRecord("Logging whenever data received from CC", "Log", surveydata, DateTime.Now.ToString(), "ResponseLog", ccData.answer.id, ccUserName, integrationType);

                    //Gets the CC credentials from the FD settings JSON
                    Dictionary<string, object> Userdata = cloudCherryController.GetUserCredentials(ccUserName, IntegrationType.freshdesk);
                    if (Userdata == null)
                    {
                        if (await sentry.LogTheFailedRecord("User not authenticated.", "Token", "", ExceptionType.General, ccData.answer.id, ccUserName, IntegrationType.freshdesk))
                        {
                             msg.StatusCode = HttpStatusCode.OK;
                            return msg;
                        }
                    }
                    CCApikey = Userdata["ccapikey"].ToString();
                    char[] splitstr = { '|' };
                    string[] integrationdetails = Userdata["integrationDetails"].ToString().Split(splitstr);
                    FDAPIKey = integrationdetails[1];
                    FDURL = integrationdetails[0];

                    //If the FDAPIKey is missing in the DB, logs the survey response 
                    if (FDAPIKey == "")
                    {
                        if (await sentry.LogTheFailedRecord("FD API KEY is not found for this user.", "Token", "", ExceptionType.General, ccData.answer.id, ccUserName, IntegrationType.freshdesk))
                        {
                            msg.StatusCode = HttpStatusCode.OK;
                            return msg;
                        }
                    }

                    //If the FDURL is missing in the DB, logs the survey response 
                    if (FDURL == "")
                    {
                        if (await sentry.LogTheFailedRecord("FD URL is not found for this user.", "Token", "", ExceptionType.General, ccData.answer.id, ccUserName, IntegrationType.freshdesk))
                        {
                            msg.StatusCode = HttpStatusCode.OK;
                            return msg;
                        }   
                    }

                    //If the CC APIKEY is missing in the DB, logs the survey response 
                    if (CCApikey == "")
                    {
                        if (await sentry.LogTheFailedRecord("CC API Key is not found for this user.", "Token", "", ExceptionType.General, ccData.answer.id, ccUserName, IntegrationType.freshdesk))
                          {
                            msg.StatusCode = HttpStatusCode.OK;
                            return msg;
                        }
                    }

                    string ccAccessToken = await cloudCherryController.getCCAccessToken(ccUserName, CCApikey);

                    //Gets the user's mapping details from the cc
                    string urlPath = "https://api.getcloudcherry.com/api/UserData/" + ConfigurationManager.AppSettings[IntegrationType.freshdesk.ToString()].ToString();
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, urlPath);
                    request.Headers.Add("Authorization", "Bearer " + ccAccessToken);

                    HttpResponseMessage response = new HttpResponseMessage();
                    HttpClient client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(100);
                    response = await client.SendAsync(request);

                    string responseBody = await response.Content.ReadAsStringAsync();
                    if (responseBody == null)
                    {
                        msg.StatusCode = HttpStatusCode.NoContent;
                        return msg;
                    }

                    responseBody = responseBody.Replace("\\", "");
                    responseBody = responseBody.Replace("\"", "'");
                    responseBody = responseBody.Replace("'{", "{");
                    responseBody = responseBody.Replace("}'", "}");
                    responseBody = responseBody.Replace("'", "\"");
                    List<string> defaultTags = new List<string>() { "NAME", "EMAIL", "MOBILE", "SUBJECT", "DESCRIPTION" };
                    if (responseBody == "null")
                    {
                        string strTags = "";
                        foreach (string tag in defaultTags)
                        {
                            strTags = strTags + tag + " ,";
                        }
                        await sentry.LogTheFailedRecord("Default mappings are not found for this user.", "Token", "", ExceptionType.General, ccData.answer.id, ccUserName, IntegrationType.freshdesk);

                       msg.StatusCode = HttpStatusCode.OK;
                        return msg;
                    }
                    var Integrationdata = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);

                    List<Dictionary<string, object>> objMappings = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(Integrationdata["mappings"].ToString());
                    Dictionary<string, object> jsonmap = new Dictionary<string, object>();

                    List<string> ExistingTaglist = await cloudCherryController.GetQuestionsTags(ccAccessToken);
                    List<string> tagListInCaps = new List<string>();

                    foreach (string tag in ExistingTaglist)
                    {
                        tagListInCaps.Add(tag.ToUpper());
                    }

                    Dictionary<string, object> IntegrationData = cloudCherryController.GetUserCredentials(ccData.answer.user.ToString(), IntegrationType.freshdesk);
                    CCApikey = IntegrationData["ccapikey"].ToString();
                    //string accessToken = await cloudCherryController.getCCAccessToken(ccData.answer.user.ToString(), CCApikey);                      
                    List<Question> questionList = await cloudCherryController.GetQuestions(ccAccessToken).ConfigureAwait(false);


                    if (objMappings != null)
                    {
                        foreach (var map in objMappings)
                        {
                            string Field = map["Field"].ToString();
                            string Qid = map["QtnID"].ToString();
                            bool isRequesterField = false;

                            switch (map["Tag"].ToString().ToUpper())
                            {
                                case "EMAIL":
                                    {
                                        if (tagListInCaps.Contains(map["Tag"].ToString().ToUpper()))
                                            defaultTags.Remove("EMAIL");
                                        isRequesterField = true;
                                        reqEmail = getValueByQId(ccData, Qid, questionList).ToString();
                                        break;
                                    }
                                case "MOBILE":
                                    {
                                        if (tagListInCaps.Contains(map["Tag"].ToString().ToUpper()))
                                            defaultTags.Remove("MOBILE");
                                        isRequesterField = true;
                                        reqMobile = getValueByQId(ccData, Qid, questionList).ToString();
                                        break;
                                    }
                                case "NAME":
                                    {
                                        if (tagListInCaps.Contains(map["Tag"].ToString().ToUpper()))
                                            defaultTags.Remove("NAME");
                                        isRequesterField = true;
                                        reqName = getValueByQId(ccData, Qid, questionList, "NAME").ToString();
                                        break;
                                    }
                                case "SUBJECT":
                                    {
                                        if (tagListInCaps.Contains(map["Tag"].ToString().ToUpper()))
                                            defaultTags.Remove("SUBJECT");
                                        isRequesterField = true;
                                        ticketSubject = getValueByQId(ccData, Qid, questionList).ToString();
                                        break;
                                    }
                                case "DESCRIPTION":
                                    {
                                        if (tagListInCaps.Contains(map["Tag"].ToString().ToUpper()))
                                            defaultTags.Remove("DESCRIPTION");
                                        isRequesterField = true;
                                        ticketDescription = getValueByQId(ccData, Qid, questionList).ToString();
                                        break;
                                    }

                            }

                            if (map["disabled"].ToString().ToLower() == "true")
                                continue;

                            if (!isRequesterField)
                                jsonmap.Add(Field, getValueByQId(ccData, Qid, questionList, map["Tag"].ToString().ToUpper()));
                        }

                        if (defaultTags.Count != 0)
                        {
                            string strTags = "";
                            foreach (string tag in defaultTags)
                            {
                                strTags = strTags + tag + " ,";
                            }
                            if (await sentry.LogTheFailedRecord("No Default tags were provided", "While creation of ticket", "", ExceptionType.General, ccData.answer.id, ccUserName, IntegrationType.freshdesk)) ;

                            msg.StatusCode = HttpStatusCode.OK;
                            return msg;
                        }

                        string nps = "";
                        string errorMessage = "";
                        if (questionList != null)
                        {
                            Question question = cloudCherryController.GetFirstQuestionofTag("NPS", ccAccessToken, questionList, ref errorMessage);
                            if (question != null)
                            {
                                foreach (Response res in ccData.answer.responses)
                                {
                                    if (res.QuestionId == question.Id)
                                    {
                                        nps = res.NumberInput.ToString();
                                    }
                                }
                            }
                        }

                        ticketDescription = ticketDescription == "" || ticketDescription == "0" ? FormDescriptionStringByAnswer(ccData) : ticketDescription;
                        string[] locationSplits = ccData.notification.ToString().Split(new string[] { ":" }, StringSplitOptions.None);
                        string notificationname = "";
                        if (locationSplits != null && locationSplits.Length>0 )
                        {
                            notificationname = locationSplits[locationSplits.Length-1];
                        }
                        string tempvariable = notificationname.ToLower().Replace("matched", "").Trim();
                        notificationname = notificationname.Substring(0, tempvariable.Length);
                        ticketSubject = ticketSubject == "" || ticketSubject == "0" ? ConfigurationManager.AppSettings["FDDefaultSubject"].ToString() + " '" + notificationname + "' (" + ccData.answer.id + ")" : ticketSubject;
                        List<string> tags = new List<string>();
                        tags.Add("TId:" + ccData.answer.id);
                        tags.Add("NPS:" + nps);

                        Dictionary<string, object> objTicket = new Dictionary<string, object>();
                        objTicket.Add("description", ticketDescription);
                        objTicket.Add("status", 2);
                        objTicket.Add("phone", reqMobile);
                        objTicket.Add("subject", ticketSubject);
                        objTicket.Add("source", 2);
                        objTicket.Add("name", reqName);
                        objTicket.Add("custom_fields", jsonmap);
                        objTicket.Add("priority", 2);
                        objTicket.Add("email", reqEmail);
                        objTicket.Add("tags", tags);

                        objTicketData = JsonConvert.SerializeObject(objTicket);     //Deserializes the fd ticket object                    

                        string fdapiKey = FDAPIKey + ":X";
                        string fdAllTicketsAPI = "/api/v2/tickets";

                        HttpRequestMessage requestFD = new HttpRequestMessage(HttpMethod.Post, FDURL + fdAllTicketsAPI);
                        requestFD.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(fdapiKey)));
                        requestFD.Content = new StringContent(objTicketData, Encoding.UTF8, "application/json");

                        HttpResponseMessage responseFD = new HttpResponseMessage();
                        HttpClient clientFD = new HttpClient();

                        clientFD.Timeout = TimeSpan.FromSeconds(100);
                        responseFD = await clientFD.SendAsync(requestFD);   //Sends the request to FD API

                        string responseFDMsg = await responseFD.Content.ReadAsStringAsync();
                        if (responseFDMsg == null)
                            responseFDMsg = "";

                        //Checks the response code and logs accordinglhy
                        if ((responseFD.StatusCode != HttpStatusCode.OK) && (responseFD.StatusCode != HttpStatusCode.Created))
                        {
                            if (responseFD.StatusCode == HttpStatusCode.BadRequest)
                            {
                             bool result=   await RemoveErrorFieldsAndCreateTicket(responseFDMsg, jsonmap, objTicket, FDURL , fdAllTicketsAPI, fdapiKey,CCApikey, ccData.answer.id, ccUserName);
                                if (result == false)
                                    msg.StatusCode = HttpStatusCode.BadRequest;
                                else
                                    msg.StatusCode = HttpStatusCode.OK;
                                
                               
                                return msg;
                            }
                            else
                            {
                                if (await sentry.LogTheFailedRecord(responseFDMsg, "While creation of ticket", "", ExceptionType.Create, ccData.answer.id, ccUserName, IntegrationType.freshdesk))
                                {
                                    msg.StatusCode = HttpStatusCode.BadRequest;
                                    return msg;
                                }
                            }

                        }
                        else if (responseFD.StatusCode == HttpStatusCode.Created)
                        {
                            string FDTicketId = GetFDTicketNumberByResponseMsg(responseFDMsg);
                          
                            await CreatenoteOnTicketCreation(FDURL, FDTicketId, ccUserName, CCApikey, ccData.answer.id);
                        }

                      msg.StatusCode = HttpStatusCode.OK;
                        return msg;
                    }
                }
                msg.StatusCode = HttpStatusCode.OK;
                return msg;
            }
            catch (TimeoutException ex)
            {
                ExceptionRaised = "Timeout";
                ExceptionRaisedMessage = ex.Message;
              
            }
            catch (Exception ex)
            {
                ExceptionRaised = "Timeout";
                ExceptionRaisedMessage = ex.Message;
             
            }
            if (ExceptionRaised == "Timeout")
            {
                await sentry.LogTheFailedRecord(ExceptionRaisedMessage, "Timed Out Exception", "", ExceptionType.Create, ccData.answer.id, ccUserName, IntegrationType.freshdesk);

                msg.StatusCode = HttpStatusCode.OK;
                return msg;

            }
            if (ExceptionRaised == "Unknown")
            {
                await sentry.LogTheFailedRecord(ExceptionRaisedMessage, "Unknown Exception : ", "", ExceptionType.General, ccData.answer.id, ccUserName, IntegrationType.freshdesk);

                msg.StatusCode = HttpStatusCode.BadRequest;
                return msg;
            }
            msg.StatusCode = HttpStatusCode.BadRequest;
            return msg;
        }

        [HttpGet,HttpPost]
        public string FormDescriptionStringByAnswer(NotificationData notificationData)
        {
            StringBuilder descriptionString = new StringBuilder();
            try
            {
                string locationName = string.Empty;
               
               locationName = "All Locations";
               
                 locationName = notificationData.answer.locationId == null ? "All Locations" : notificationData.answer.locationId;

                //Response response in notificationData.answer.responses)
                for (int counter = 0; counter < notificationData.answer.responses.Count; counter++)
                {
                    string ansValue = (notificationData.answer.responses[counter].TextInput == null) || (notificationData.answer.responses[counter].TextInput.Trim() == "") ? notificationData.answer.responses[counter].NumberInput.ToString() : notificationData.answer.responses[counter].TextInput;

                    if (counter == 0)
                        descriptionString.Append(notificationData.answer.responses[counter].QuestionText + "  :  " + ansValue);
                    else
                        descriptionString.Append("  ,  " + notificationData.answer.responses[counter].QuestionText + "  :  " + ansValue);
                }
                return "Location Name : " + locationName + ", "+descriptionString.ToString() ;

            }
            catch (Exception ex)
            {
                return descriptionString.ToString();
            }
        }

        public string GetFDTicketNumberByResponseMsg(string responseFDMsg)
        {
            Dictionary<string, object> obj = new Dictionary<string, object>();
            obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseFDMsg);
            string FDTicketId = "";
            foreach (var item in obj)
            {
                if (item.Key.ToString().ToUpper() == "ID")
                {
                    FDTicketId = item.Value.ToString();
                }
            }
            return FDTicketId;
        }


        public async Task<bool> RemoveErrorFieldsAndCreateTicket(string responseFDMsg, Dictionary<string, object> jsonmap, Dictionary<string, object> objTicket, string FDURL,string Ticketapi, string FDapiKey, string CCapiKey,string ansID, string userName)
        {
            string Exception = "";
            List<string> errFieldList = new List<string>();
            try
            {
                Dictionary<string, object> errMsgDeserialize = new Dictionary<string, object>();
                errMsgDeserialize = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseFDMsg);
                List<Dictionary<string, object>> errorFields = new List<Dictionary<string, object>>();
                if (errMsgDeserialize.Keys.Contains("errors"))
                {
                    errorFields = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(errMsgDeserialize["errors"].ToString());

                    if (errorFields != null)
                    {
                        foreach (Dictionary<string, object> errObj in errorFields)
                        {
                            if (errObj.Keys.Contains("field"))
                            {
                                errFieldList.Add(errObj.Where(keyName => keyName.Key == "field").Select(value => value.Value.ToString()).FirstOrDefault());
                            }
                        }
                    }
                }

                //jsonmap
                Dictionary<string, object> removeCustomErrFields = new Dictionary<string, object>();
                string jsonCustomFieldsSerialize = JsonConvert.SerializeObject(jsonmap);
                removeCustomErrFields = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonCustomFieldsSerialize);//jsonmap;


                Dictionary<string, object> removeErrFields = new Dictionary<string, object>();
                string jsonSerialize = JsonConvert.SerializeObject(objTicket);
                removeErrFields = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonSerialize); // objTicket;
                removeErrFields.Remove("custom_fields");

                string jsonTmpErrFields = JsonConvert.SerializeObject(errFieldList);
                List<string> tempErrFields = new List<string>();
                tempErrFields = JsonConvert.DeserializeObject<List<string>>(jsonTmpErrFields);

                Dictionary<string, object> removedFields = new Dictionary<string, object>();

                if (errFieldList != null)
                {
                    if (errFieldList.Count > 0)
                    {
                        foreach (var item in objTicket)
                        {

                            if (errFieldList.Select(x => x.ToString().ToUpper() == item.Key.ToString().ToUpper()).FirstOrDefault())
                            {
                                removeErrFields.Remove(item.Key.ToString());
                                removedFields.Add(item.Key, item.Value);
                                tempErrFields.Remove(item.Key.ToString());
                            }
                        }

                        foreach (var item in jsonmap)
                        {

                            if (tempErrFields.Select(x => x.ToString().ToUpper() == item.Key.ToString().ToUpper()).FirstOrDefault())
                            {
                                removeCustomErrFields.Remove(item.Key.ToString());
                                removedFields.Add(item.Key, item.Value);
                            }
                        }
                    }
                }

                string responseFDRemoveErrFieldsMsg = "";
                if (removeErrFields != null)
                {
                    if (removeErrFields.Count > 0)
                    {
                        removeErrFields.Add("custom_fields", removeCustomErrFields);


                        string jsonSerializeDate = JsonConvert.SerializeObject(removeErrFields);

                        HttpRequestMessage requestFDRemoveErrField = new HttpRequestMessage(HttpMethod.Post, FDURL+Ticketapi);
                        requestFDRemoveErrField.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(FDapiKey)));
                        requestFDRemoveErrField.Content = new StringContent(jsonSerializeDate, Encoding.UTF8, "application/json");

                        HttpResponseMessage responseFDRemoveErrField = new HttpResponseMessage();
                        HttpClient clientFD = new HttpClient();

                        clientFD.Timeout = TimeSpan.FromSeconds(100);
                        responseFDRemoveErrField = await clientFD.SendAsync(requestFDRemoveErrField);   //Sends the request to FD API

                        responseFDRemoveErrFieldsMsg = await responseFDRemoveErrField.Content.ReadAsStringAsync();
                        if (responseFDRemoveErrField.IsSuccessStatusCode)
                        {
                            string fields = "";
                            foreach (var errField in removedFields)
                            {
                                fields = fields + "  " + errField.Key;
                            }
                            string FDTicketId = GetFDTicketNumberByResponseMsg(responseFDRemoveErrFieldsMsg);
                          await  CreatenoteOnTicketCreation(FDURL, FDTicketId, userName, CCapiKey, ansID);
                            bool flag = await sentry.LogThePartialSuccessRecord(ansID, DateTime.Now.ToString(), fields, JsonConvert.SerializeObject(removedFields), responseFDRemoveErrFieldsMsg, FDTicketId, ExceptionType.FieldLevel, userName, IntegrationType.freshdesk);
                            if (flag)
                                return true;
                        }
                        else
                        {
                            bool flag = await sentry.LogTheFailedRecord(responseFDRemoveErrFieldsMsg, "FieldLevel", "", ExceptionType.Create, ansID, userName, IntegrationType.freshdesk);

                            if (flag)
                                return false;
                        }
                    }
                    else
                    {
                        bool flag = await sentry.LogTheFailedRecord(responseFDRemoveErrFieldsMsg, "FieldLevel", "", ExceptionType.Create, ansID, userName, IntegrationType.freshdesk);


                        if (flag)
                            return false;
                    }
                }
            }
            catch (JsonSerializationException jsonEx)
            {
                Exception = "FieldLevel";
              

               
            }
                
            catch (Exception ex) { }
            if( Exception == "FieldLevel")  {
                await sentry.LogTheFailedRecord(responseFDMsg, "FieldLevel", "", ExceptionType.Create, ansID, userName, IntegrationType.freshdesk);
                return false;
                }
            return false;
        }
        async Task<HttpResponseMessage> CreatenoteOnTicketCreation(string FDURL, string FDTicketId, string ccUserName, string CCApikey, string Answerid)
        {
            try
            {
                MessageNotes notes = new MessageNotes();
                notes.note = ConfigurationManager.AppSettings["FDTicketCreation"].ToString() + " " + FDURL + @"/helpdesk/tickets/" + FDTicketId;
                notes.noteTime = System.DateTime.Now.ToString();
                return await cloudCherryController.UpdateNote(notes, Answerid, ccUserName, CCApikey);
            }
            catch {
                return new HttpResponseMessage();
            }
        }
        //Adds a NOTE in CC whenever update happens in FD Ticket
        [HttpPost]
        [Route("api/FDCC/AddANoteAtCC")]
        public async Task<IHttpActionResult> AddANoteAtCC()
        {
            string UserName = "";
            var serializeNote = ""; string CCTicketId = "";
            string Exceptionmsg="";
            string addNoteRespContent = "";
            MessageNotes notes = new MessageNotes();
            try
            {             
                string response = "";
                HttpClient client = new HttpClient();

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

                string CCApikey = "";
                
                Dictionary<string, object> caseDeserialize = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);              

                if (caseDeserialize == null)
                    return Ok();


                UserName = caseDeserialize["CCUserName"].ToString();  //Gets the cc api key 
                CCTicketId = caseDeserialize["CCTicketID"].ToString();    //Get cc ticket id 
                notes.note = ConfigurationManager.AppSettings["FDTicketUpdation"].ToString() + " " + caseDeserialize["CCNoteLink"].ToString();
                notes.noteTime = System.DateTime.Now.ToString();

                string FDAPIKey = "";
                string FDURL = "";
                // string CCApikey = "";

                serializeNote = JsonConvert.SerializeObject(notes);
                Dictionary<string, object> Userdata = cloudCherryController.GetUserCredentials(UserName, IntegrationType.freshdesk);
                CCApikey = Userdata["ccapikey"].ToString();
                char[] splitstr = { '|' };
                string[] integrationdetails = Userdata["integrationDetails"].ToString().Split(splitstr);
                FDAPIKey = integrationdetails[1];
                FDURL = integrationdetails[0];
                //Dictionary<string, object> doc = cloudCherryController.GetUserCredentials("ccapikey", ccapikey, ref UserName);
                //freshDesk.SetUserCredentials(doc, ref  FDAPIKey, ref FDURL, ref ccapikey);

                //Specifying the add Note URL at CC
                string ccURLforAddNote = "https://api.getcloudcherry.com/api/Answers/Note/" + CCTicketId;
                HttpRequestMessage addNoteRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(ccURLforAddNote));

                //Get AccessToken from CC
                string ccAccessToken = await cloudCherryController.getCCAccessToken(UserName, CCApikey);
                addNoteRequest.Headers.Add("Authorization", "Bearer " + ccAccessToken);
                addNoteRequest.Content = new StringContent(serializeNote, Encoding.UTF8, "application/json");

                //Get Response from the CC API
                HttpResponseMessage addNoteResponse = await client.SendAsync(addNoteRequest);
                 addNoteRespContent = await addNoteResponse.Content.ReadAsStringAsync();

                 if (addNoteResponse.StatusCode != HttpStatusCode.OK)
                 {
                     await sentry.LogTheFailedRecord(addNoteRespContent, "Unknown Exception", notes.note, ExceptionType.Update, CCTicketId, UserName, IntegrationType.freshdesk);
                     return BadRequest();
                 }
                
               
                return Ok();
            }
            catch (Exception ex)
            {
               Exceptionmsg="unknown";
            }

            if (Exceptionmsg == "unknown")
            {
                await sentry.LogTheFailedRecord("Unknown Exception raised at CC Into API while requesting CC API To add NOTE", "Unknown Exception", notes.note, ExceptionType.Update, CCTicketId, UserName, IntegrationType.freshdesk);

                return BadRequest();
            }
            return BadRequest();
        }



     

        [HttpGet]
        [Route("api/FDCC/GetTagonEdit")]
        public async Task<List<string>> GetTagonEdit(string Questionid, string Username)
        {
            string CCApikey = "";
            Dictionary<string, object> Integrationdata = cloudCherryController.GetUserCredentials(Username, IntegrationType.freshdesk);
            List<string> defaultTags = new List<string>() { "NAME", "EMAIL", "MOBILE", "SUBJECT", "DESCRIPTION", "NPS" };
            CCApikey = Integrationdata["ccapikey"].ToString();
            //  freshDesk.SetUserCredentials(doc, ref  FDAPIKey, ref FDURL, ref  CCApikey);
            string accessToken = await cloudCherryController.getCCAccessToken(Username, CCApikey);
            List<string> ExistingTags = await cloudCherryController.GetTagonEditbyQuestion(Questionid, accessToken);
            List<string> FinalList = new List<string>();
            foreach (string singletag in ExistingTags)
            {
                if (!defaultTags.Contains(singletag.ToUpper()))
                {
                    FinalList.Add(singletag.ToLower());
                }
            }

            return FinalList;

        }

        //Gets the input value by the question
        public object getValueByQId(NotificationData ccData, string Qid, List<Question> questionList, string tagName = "")
        {
            object ans = new object();
            try
            {
                Dictionary<string, object> jsonmap = new Dictionary<string, object>();
                foreach (var res in ccData.answer.responses)
                {
                    if (Qid.Contains(res.QuestionId))
                    {
                        ans = res.TextInput == "" || res.TextInput == null ? res.NumberInput.ToString() : res.TextInput.ToString();

                        if (tagName.ToUpper() == "NAME")
                        {
                            ans = res.TextInput;
                        }
                        else if (tagName != "")
                        {

                            Question singleQuestiondata = new Question();
                            foreach (Question SingleQuestion in questionList)
                            {
                                if (SingleQuestion.Id == Qid)
                                {
                                    singleQuestiondata = SingleQuestion;
                                    break;
                                }
                            }
                            string dType = singleQuestiondata.DisplayType;

                            if ((dType.ToUpper() == "NUMBER") || (dType.ToUpper() == "STAR-5") || (dType.ToUpper() == "SMILE-5") || (dType.ToUpper() == "SCALE"))
                            {
                                try
                                {
                                    ans = (object)Convert.ToInt32(ans);
                                }
                                catch(Exception ex)
                                {
                                    ans = 0;
                                }
                            }
                            else if (dType.ToUpper() == "DATE")
                            {
                                ans = ans.ToString().Insert(4, "-");
                                ans = ans.ToString().Insert(7, "-");
                            }
                        }
                        return ans;
                    }
                }
            }
            catch (Exception ex)
            {
                return ans;
            }

            return "";
        }

        //Gets the FD ticket fields from FD
        [HttpGet]
        [Route("api/FDCC/getFDTicketFields")]
        public async Task<Dictionary<string, object>> getFDTicketFields(string FDkey, string FDurl)
        {
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                string fdAllTicketsAPI = "/api/v2/ticket_fields";
                string apiKey = FDkey + ":X";
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, FDurl + fdAllTicketsAPI);
                request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(apiKey)));
                HttpResponseMessage response = new HttpResponseMessage();
                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(100);
                response = await client.SendAsync(request);
                string responseMsg = await response.Content.ReadAsStringAsync();
                Dictionary<string, object> fdFields = new Dictionary<string, object>();
                List<Dictionary<string, object>> responseFields = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(responseMsg);
                if (responseFields != null)
                {
                    foreach (Dictionary<string, object> dictionaryObj in responseFields)
                    {
                        string key = "";
                        string value = "";
                        foreach (var item in dictionaryObj)
                        {
                            if (item.Key == "name")
                            {
                                key = item.Value.ToString();
                            }
                            else if (item.Key == "type")
                            {
                                value = item.Value.ToString();
                            }
                        }
                        fdFields.Add(key, value + "::" + key);

                    }
                }
                fdFields.Remove("ccticket");
                fdFields.Remove("subject");
                fdFields.Remove("description");
                return fdFields;
            }
            catch (Exception ex)
            { return null; }
        }

    }
}
