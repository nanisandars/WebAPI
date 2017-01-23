using LTSAPI.CC_Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Newtonsoft.Json;
using System.Configuration;
using System.IO;

namespace LTSAPI.Controllers
{
    public class ZendeskCCController : ApiController
    {
        CloudCherryController cloudCherryController = new CloudCherryController();
        SentryController sentry = new SentryController();
        string ExceptionRaised = "";
        string ExceptionRaisedMessage = "";

        //Raises a ticket at ZD
        [HttpPost]
        [Route("api/ZendeskCC/AddAZendeskTicket")]
        public async Task<HttpResponseMessage> AddAZendeskTicket(NotificationData ccData)
        {

            string surveydata = "";
            string ccUserName = ccData.answer.user;
            string CCApikey = "";
            string ZDAPIKey = "";
            string ZDURL = "";
            string ZDEmail = "";
            string reqMobile = "";
            string reqName = "";
            string reqEmail = "";
            string ticketDescription = "";
            string ticketSubject = "";

            HttpResponseMessage msg = new HttpResponseMessage();
            try
            {
                if (ccData.answer != null)
                {

                    //Logs the cc user's survey response 
                    surveydata = JsonConvert.SerializeObject(ccData.answer);

                    //Gets the CC credentials from the ZD settings JSON
                    Dictionary<string, object> Userdata = cloudCherryController.GetUserCredentials(ccUserName, IntegrationType.zendesk);
                    if (Userdata == null)
                    {
                        if (await sentry.LogTheFailedRecord("User not authenticated.", "Token", "", ExceptionType.General, ccData.answer.id, ccUserName, IntegrationType.zendesk))
                        {
                            msg.StatusCode = HttpStatusCode.OK;
                            return msg;
                        }
                    }

                    CCApikey = Userdata["ccapikey"].ToString();
                    char[] splitstr = { '|' };
                    string[] integrationdetails = Userdata["integrationDetails"].ToString().Split(splitstr);
                    ZDURL = integrationdetails[0];
                    ZDAPIKey = integrationdetails[1];
                    ZDEmail = integrationdetails[2];

                    //If the ZDAPIKey is missing in the DB, logs the survey response 
                    if (ZDAPIKey == "")
                    {
                        if (await sentry.LogTheFailedRecord("ZD API KEY is not found for this user.", "Token", "", ExceptionType.General, ccData.answer.id, ccUserName, IntegrationType.zendesk))
                        {
                            msg.StatusCode = HttpStatusCode.OK;
                            return msg;
                        }
                    }

                    //If the ZDURL is missing in the DB, logs the survey response 
                    if (ZDURL == "")
                    {
                        if (await sentry.LogTheFailedRecord("ZD URL is not found for this user.", "Token", "", ExceptionType.General, ccData.answer.id, ccUserName, IntegrationType.zendesk))
                        {
                            msg.StatusCode = HttpStatusCode.OK;
                            return msg;
                        }
                    }

                    //If the ZDAdminEmail is missing in the DB, logs the survey response 
                    if (ZDEmail == "")
                    {
                        if (await sentry.LogTheFailedRecord("ZD Admin Email is not found for this user.", "Token", "", ExceptionType.General, ccData.answer.id, ccUserName, IntegrationType.zendesk))
                        {
                            msg.StatusCode = HttpStatusCode.OK;
                            return msg;
                        }
                    }

                    //If the CC APIKEY is missing in the DB, logs the survey response 
                    if (CCApikey == "")
                    {
                        if (await sentry.LogTheFailedRecord("CC API Key is not found for this user.", "Token", "", ExceptionType.General, ccData.answer.id, ccUserName, IntegrationType.zendesk))
                        {
                            msg.StatusCode = HttpStatusCode.OK;
                            return msg;
                        }
                    }


                    string zenTicketFieldsURL = "/api/v2/tickets.json";

                    List<string> defaultTags = new List<string>() { "NAME", "EMAIL", "MOBILE", "SUBJECT", "DESCRIPTION" };

                    string ccAccessToken = await cloudCherryController.getCCAccessToken(ccUserName, CCApikey);

                    List<Question> questionList = await cloudCherryController.GetQuestions(ccAccessToken).ConfigureAwait(false);

                    Dictionary<string, object> ccMappings = await cloudCherryController.GetCCMappings(ccAccessToken, ConfigurationManager.AppSettings[IntegrationType.zendesk.ToString()]);

                    if (ccMappings["mappings"] == null)
                    {
                        string strTags = "";
                        foreach (string tag in defaultTags)
                        {
                            strTags = strTags + tag + " ,";
                        }
                        await sentry.LogTheFailedRecord("Default mappings are not found for this user.", "Token", "", ExceptionType.General, ccData.answer.id, ccUserName, IntegrationType.zendesk);

                        msg.StatusCode = HttpStatusCode.OK;
                        return msg;
                    }

                    List<Dictionary<string, object>> objMappings = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(ccMappings["mappings"].ToString());

                    Dictionary<string, object> jsonmap = new Dictionary<string, object>();

                    List<string> ExistingTaglist = await cloudCherryController.GetQuestionsTags(ccAccessToken);

                    List<string> tagListInCaps = new List<string>();

                    foreach (string tag in ExistingTaglist)
                    {
                        tagListInCaps.Add(tag.ToUpper());
                    }


                    List<string> drpFieldCheck = new List<string>();

                    Dictionary<string, object> ticketZendeskTicketProperties = new Dictionary<string, object>();

                    List<Dictionary<string, object>> ticketFieldList = await getZendeskTicketFields(ZDURL, ZDAPIKey, ZDEmail);

                    Dictionary<string, object> customFieldsZendesk = new Dictionary<string, object>();

                    List<Dictionary<string, object>> listFields = new List<Dictionary<string, object>>();

                    bool flagCCTicket = false; bool flagNPSScore = false;

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

                    if (objMappings != null)
                    {
                        foreach (var map in objMappings)
                        {
                            string Field = map["Field"].ToString();
                            string Qid = map["QtnID"].ToString();
                            bool isRequesterField = false;

                            if (map["disabled"].ToString().ToLower() == "true")
                                continue;

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
                                        reqMobile = (reqMobile.Trim().ToString() == "0") || (reqMobile.Trim().ToString() == "") ? "" : reqMobile.ToString();
                                        break;
                                    }
                                case "NAME":
                                    {
                                        if (tagListInCaps.Contains(map["Tag"].ToString().ToUpper()))
                                            defaultTags.Remove("NAME");
                                        isRequesterField = true;
                                        reqName = getValueByQId(ccData, Qid, questionList, "NAME").ToString();
                                        reqName = (reqName.Trim().ToString() == "0") || (reqName.Trim().ToString() == "") ? "" : reqName.ToString();
                                        break;
                                    }
                                case "SUBJECT":
                                    {
                                        if (tagListInCaps.Contains(map["Tag"].ToString().ToUpper()))
                                            defaultTags.Remove("SUBJECT");
                                        isRequesterField = true;
                                        ticketSubject = getValueByQId(ccData, Qid, questionList).ToString();
                                        ticketSubject = ticketSubject.Trim().ToString() == "0" ? "" : ticketSubject.Trim().ToString();
                                        break;
                                    }
                                case "DESCRIPTION":
                                    {
                                        if (tagListInCaps.Contains(map["Tag"].ToString().ToUpper()))
                                            defaultTags.Remove("DESCRIPTION");
                                        isRequesterField = true;
                                        ticketDescription = getValueByQId(ccData, Qid, questionList).ToString();
                                        ticketDescription = ticketDescription.Trim().ToString() == "0" ? "" : ticketDescription.Trim().ToString();
                                        break;
                                    }

                            }


                            foreach (var fieldsList in ticketFieldList)
                            {
                                Dictionary<string, object> customField = new Dictionary<string, object>();
                                if (!isRequesterField)
                                {
                                    if (fieldsList["title"].ToString().ToUpper() == Field.ToUpper())
                                    {
                                        customField.Add("id", fieldsList["id"].ToString());
                                        object valueOfField = getValueByQId(ccData, Qid, questionList, map["Tag"].ToString().ToUpper());
                                        customField.Add("value", valueOfField);
                                        listFields.Add(customField);

                                        if (fieldsList["type"].ToString().ToLower() == "tagger")
                                        {
                                            drpFieldCheck.Add(fieldsList["id"].ToString() + "|||" + valueOfField + "|||" + fieldsList["title"].ToString());
                                        }

                                        break;
                                    }
                                }
                                else if (fieldsList["title"].ToString().ToUpper() == "CCTICKET")
                                {
                                    if (!flagCCTicket)
                                    {
                                        customField.Add("id", fieldsList["id"].ToString());
                                        customField.Add("value", ccData.answer.id);
                                        listFields.Add(customField);
                                        flagCCTicket = true;
                                        break;
                                    }
                                }
                                else if (fieldsList["title"].ToString().ToUpper() == "NPSSCORE")
                                {
                                    if (!flagNPSScore)
                                    {
                                        customField.Add("id", fieldsList["id"].ToString());
                                        customField.Add("value", nps);
                                        listFields.Add(customField);
                                        flagNPSScore = true;
                                        break;
                                    }
                                }
                            }
                        }

                        ticketDescription = ticketDescription == "" || ticketDescription == "0" ? FormDescriptionStringByAnswer(ccData) : ticketDescription;
                        string[] locationSplits = ccData.notification.ToString().Split(new string[] { ":" }, StringSplitOptions.None);
                        string notificationname = "";
                        if (locationSplits != null && locationSplits.Length > 0)
                        {
                            notificationname = locationSplits[locationSplits.Length - 1].Trim();
                        }
                        string tempvariable = notificationname.ToLower().Replace("matched", "").Trim();
                        notificationname = notificationname.Substring(0, tempvariable.Length);
                        ticketSubject = ticketSubject == "" || ticketSubject == "0" ? ConfigurationManager.AppSettings["ZDDefaultSubject"].ToString() + " '" + notificationname + "' (" + ccData.answer.id + ")" : ticketSubject;



                        Dictionary<string, object> requesterFieldsZendesk = new Dictionary<string, object>();
                        requesterFieldsZendesk.Add("name", reqName);
                        requesterFieldsZendesk.Add("email", reqEmail);
                        requesterFieldsZendesk.Add("phone", reqMobile);

                        ticketZendeskTicketProperties.Add("subject", ticketSubject);
                        ticketZendeskTicketProperties.Add("description", ticketDescription);
                        ticketZendeskTicketProperties.Add("requester", requesterFieldsZendesk);
                        ticketZendeskTicketProperties.Add("custom_fields", listFields);

                        Dictionary<string, object> zendeskTicket = new Dictionary<string, object>();
                        zendeskTicket.Add("ticket", ticketZendeskTicketProperties);

                        string jsonZenTicket = JsonConvert.SerializeObject(zendeskTicket);

                        HttpClient httpClient = new HttpClient();

                        HttpRequestMessage zenReqCreateTicket = new HttpRequestMessage(HttpMethod.Post, ZDURL + zenTicketFieldsURL);
                        zenReqCreateTicket.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(ZDEmail + "/token:" + ZDAPIKey)));
                        zenReqCreateTicket.Content = new StringContent(jsonZenTicket, Encoding.UTF8, "application/json");

                        HttpResponseMessage zenResCreateTicket = new HttpResponseMessage();
                        zenResCreateTicket = await httpClient.SendAsync(zenReqCreateTicket);

                        string responseZenCreateTicket = await zenResCreateTicket.Content.ReadAsStringAsync();

                        //Checks the response code and logs accordinglhy
                        if ((zenResCreateTicket.StatusCode != HttpStatusCode.OK) && (zenResCreateTicket.StatusCode != HttpStatusCode.Created))
                        {
                            if (zenResCreateTicket.StatusCode == (HttpStatusCode)422)
                            {
                                bool result = await RemoveErrorFieldsAndCreateTicket(responseZenCreateTicket, requesterFieldsZendesk, listFields, zendeskTicket, ZDURL, zenTicketFieldsURL, ZDEmail + "/token:" + ZDAPIKey, ccData.answer.id, ccUserName, ticketFieldList, CCApikey);
                                if (result == false)
                                    msg.StatusCode = HttpStatusCode.BadRequest;
                                else
                                    msg.StatusCode = HttpStatusCode.OK;

                                return msg;
                            }
                            else
                            {
                                if (await sentry.LogTheFailedRecord(responseZenCreateTicket, "While creation of ticket", "", ExceptionType.Create, ccData.answer.id, ccUserName, IntegrationType.zendesk))
                                {
                                    msg.StatusCode = HttpStatusCode.BadRequest;
                                    return msg;
                                }
                            }
                        }
                        else if (zenResCreateTicket.StatusCode == HttpStatusCode.Created)
                        {
                            string ZDTicketId = GetZDTicketNumberByResponseMsg(responseZenCreateTicket);
                            await CreatenoteOnTicketCreation(ZDURL, ZDTicketId, ccUserName, CCApikey, ccData.answer.id);
                            if (drpFieldCheck != null)
                            {
                                if (drpFieldCheck.Count > 0)
                                {
                                    await checkDropdownValueFieldsOnCreationTicket(drpFieldCheck, responseZenCreateTicket, ccData.answer.id, ZDTicketId, ccUserName);
                                }
                            }
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
                await sentry.LogTheFailedRecord(ExceptionRaisedMessage, "Timed Out Exception", "", ExceptionType.Create, ccData.answer.id, ccUserName, IntegrationType.zendesk);

                msg.StatusCode = HttpStatusCode.OK;
                return msg;

            }
            if (ExceptionRaised == "Unknown")
            {
                await sentry.LogTheFailedRecord(ExceptionRaisedMessage, "Unknown Exception : ", "", ExceptionType.General, ccData.answer.id, ccUserName, IntegrationType.zendesk);

                msg.StatusCode = HttpStatusCode.BadRequest;
                return msg;
            }
            msg.StatusCode = HttpStatusCode.BadRequest;
            return msg;
        }

        public async Task<bool> checkDropdownValueFieldsOnCreationTicket(List<string> taggerFeilds, string responseString, string AnswerID, string ZDTicketNumber, string UserName)
        {
            Dictionary<string, object> skippedFields = new Dictionary<string, object>();
            try
            {
                Dictionary<string, object> ticketResponse = new Dictionary<string, object>();
                ticketResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseString);
                Dictionary<string, object> responseDictionary = new Dictionary<string, object>();
                responseDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(ticketResponse["ticket"].ToString());
                if (responseDictionary != null)
                {
                    List<Dictionary<string, object>> customFields = new List<Dictionary<string, object>>();
                    customFields = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(responseDictionary["custom_fields"].ToString());

                    if (customFields != null)
                    {
                        if (customFields.Count > 0)
                        {
                            foreach (string chkDrpField in taggerFeilds)
                            {
                                string[] drpFieldSplits = chkDrpField.Split(new string[] { "|||" }, StringSplitOptions.None);

                                if (drpFieldSplits != null)
                                {
                                    if (drpFieldSplits.Length > 1)
                                    {
                                        foreach (Dictionary<string, object> dicObj in customFields)
                                        {
                                            if (drpFieldSplits[0].ToString() == dicObj["id"].ToString())
                                            {
                                                if (drpFieldSplits[1].ToString().ToLower() != (dicObj["value"] == null ? "" : dicObj["value"].ToString().ToLower()))
                                                {
                                                    if (!(skippedFields.Keys.Contains(drpFieldSplits[2].ToString())))
                                                        skippedFields.Add(drpFieldSplits[2].ToString(), drpFieldSplits[1].ToString());
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (skippedFields != null)
                {
                    if (skippedFields.Count > 0)
                    {
                        string serializeSkippedFields = JsonConvert.SerializeObject(skippedFields);
                        bool flag = await sentry.LogThePartialSuccessRecord(AnswerID, DateTime.Now.ToString(), serializeSkippedFields, serializeSkippedFields, serializeSkippedFields, ZDTicketNumber, ExceptionType.FieldLevel, UserName, IntegrationType.zendesk);
                        if (flag)
                            return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        [HttpGet, HttpPost]
        public string FormDescriptionStringByAnswer(NotificationData notificationData)
        {
            StringBuilder descriptionString = new StringBuilder();
            try
            {
                string locationName = string.Empty;

                locationName = "All Locations";

                locationName = notificationData.answer.locationId == null ? "All Locations" : notificationData.answer.locationId;


                for (int counter = 0; counter < notificationData.answer.responses.Count; counter++)
                {
                    string ansValue = (notificationData.answer.responses[counter].TextInput == null) || (notificationData.answer.responses[counter].TextInput.Trim() == "") ? notificationData.answer.responses[counter].NumberInput.ToString() : notificationData.answer.responses[counter].TextInput;

                    if (counter == 0)
                        descriptionString.Append(notificationData.answer.responses[counter].QuestionText + "  :  " + ansValue);
                    else
                        descriptionString.Append("  ,  " + notificationData.answer.responses[counter].QuestionText + "  :  " + ansValue);
                }
                return "Location Name : " + locationName + ", " + descriptionString.ToString();

            }
            catch (Exception ex)
            {
                return descriptionString.ToString();
            }
        }

        //Adds a NOTE in CC whenever update happens in ZD Ticket
        [HttpPost]
        [Route("api/ZendeskCC/AddANoteAtCCFromZD")]
        public async Task<IHttpActionResult> AddANoteAtCCFromZD()
        {
            string UserName = "";
            string Exception = "";
            string ccapikey = "";
            string ZDURL = "";
            string ZDEmail = "";
            string ZDAPIKey = "";
            var serializeNote = ""; string CCTicketId = "";
            try
            {
                MessageNotes notes = new MessageNotes();
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

                Dictionary<string, object> caseDeserialize = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);

                ccapikey = caseDeserialize["CCAPIKey"].ToString();  //Gets the cc api key 
                CCTicketId = caseDeserialize["CCTicket"].ToString();    //Get cc ticket id 
                UserName = caseDeserialize["CCUserName"].ToString();  //Gets the cc username



                if (caseDeserialize == null)
                    return Ok();


                Dictionary<string, object> Userdata = cloudCherryController.GetUserCredentials(UserName, IntegrationType.zendesk);
                if (Userdata == null)
                {
                    return Ok();
                }

                char[] splitstr = { '|' };
                string[] integrationdetails = Userdata["integrationDetails"].ToString().Split(splitstr);
                ZDURL = integrationdetails[0];
                ZDAPIKey = integrationdetails[1];
                ZDEmail = integrationdetails[2];

                StringBuilder sbNotes = new StringBuilder();

                Dictionary<string, object> dicTicketAudit = new Dictionary<string, object>();

                dicTicketAudit = JsonConvert.DeserializeObject<Dictionary<string, object>>(caseDeserialize["TicketAudit"].ToString());    //Ticket Audit

                if (dicTicketAudit == null)
                {
                    await sentry.LogTheFailedRecord("Ticket Audit Data not found..", "Unknown Exception", response, ExceptionType.Update, CCTicketId, UserName, IntegrationType.zendesk);
                    return BadRequest();
                }

                List<Dictionary<string, object>> listAudits = new List<Dictionary<string, object>>();

                listAudits = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(dicTicketAudit["audits"].ToString());    //Ticket Audit

                if (listAudits == null)
                {
                    await sentry.LogTheFailedRecord("Ticket Audit Data not found..", "Unknown Exception", response, ExceptionType.Update, CCTicketId, UserName, IntegrationType.zendesk);
                    return BadRequest();
                }

                List<Dictionary<string, object>> listEvents = new List<Dictionary<string, object>>();

                //foreach (Dictionary<string, object> objAudit in listAudits)
                //{
                Dictionary<string, object> updateEvent = new Dictionary<string, object>();
                if (listAudits != null)
                {
                    if (listAudits.Count >= 1)
                    {
                        updateEvent = listAudits[listAudits.Count - 1];
                    }
                }

                if (listAudits.Count >= 1)
                {
                    listEvents = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(updateEvent["events"].ToString());    //Ticket Events

                    if (listEvents != null)
                    {
                        if (listEvents.Count >= 1)
                        {
                            foreach (Dictionary<string, object> objEvent in listEvents)
                            {
                                if (objEvent["type"].ToString() == "Change")
                                {
                                    string fieldName = "";
                                    string value = "";
                                    string previousVlaue = "";

                                    fieldName = objEvent["field_name"].ToString();
                                    value = objEvent["value"] == null ? "" : objEvent["value"].ToString();
                                    previousVlaue = objEvent["previous_value"] == null ? "" : objEvent["previous_value"].ToString();

                                    int zdFieldID;
                                    if (Int32.TryParse(fieldName, out zdFieldID))
                                    {
                                        fieldName = await getZDFieldNameByID(fieldName, ZDURL, ZDAPIKey, ZDEmail);
                                    }

                                    if (fieldName.ToLower() != "ccticket")
                                    {
                                        sbNotes.Append(fieldName + "  :  " + value + "      \\n      ");
                                    }

                                }
                            }
                        }
                    }
                }
                //}               


                notes.note = sbNotes.ToString();
                notes.noteTime = System.DateTime.Now.ToString();

                serializeNote = JsonConvert.SerializeObject(notes);


                //Specifying the add Note URL at CC
                string ccURLforAddNote = "https://api.getcloudcherry.com/api/Answers/Note/" + CCTicketId;
                HttpRequestMessage addNoteRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(ccURLforAddNote));

                //Get AccessToken from CC
                string ccAccessToken = await cloudCherryController.getCCAccessToken(UserName, ccapikey);
                addNoteRequest.Headers.Add("Authorization", "Bearer " + ccAccessToken);
                addNoteRequest.Content = new StringContent(serializeNote, Encoding.UTF8, "application/json");
                HttpResponseMessage addNoteResponse = new HttpResponseMessage();
                if (sbNotes.ToString() != string.Empty)
                {
                    //Get Response from the CC API
                    addNoteResponse = await client.SendAsync(addNoteRequest);
                }

                string addNoteRespContent = await addNoteResponse.Content.ReadAsStringAsync();

                if (addNoteResponse.StatusCode != HttpStatusCode.OK)
                {
                    await sentry.LogTheFailedRecord(addNoteRespContent, "Unknown Exception", serializeNote, ExceptionType.Update, CCTicketId, UserName, IntegrationType.zendesk);
                    return BadRequest();
                }
                //Need to handle exceptions
                return Ok();
            }
            catch (Exception ex)
            {
                Exception = "Unknown";

            }
            if (Exception == "Unknown")
            {
                await sentry.LogTheFailedRecord("Unknown Exception raised at CC Into API while requesting CC API To add NOTE", "Unknown Exception", serializeNote, ExceptionType.Update, CCTicketId, UserName, IntegrationType.zendesk);

                return BadRequest();
            }
            return BadRequest();
        }

        public async Task<string> getZDFieldNameByID(string zdFieldID, string zdURL, string zdAPIKey, string zdEmail)
        {
            try
            {
                List<Dictionary<string, object>> ticketFields = await getZendeskTicketFields(zdURL, zdAPIKey, zdEmail);

                foreach (var field in ticketFields)
                {
                    if (field["id"].ToString() == zdFieldID)
                    {
                        return field["title"].ToString();
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }

        public async Task<bool> RemoveErrorFieldsAndCreateTicket(string responseZDMsg, Dictionary<string, object> requesterFieldsZendesk, List<Dictionary<string, object>> jsonmap, Dictionary<string, object> objTicket, string ZDURL, string insertZDTicketURL, string apiKey, string ansID, string userName, List<Dictionary<string, object>> ticketFieldList, string CCapikey)
        {
            List<Dictionary<string, object>> dicRemovedFields = new List<Dictionary<string, object>>();
            string Exception_Type = "";
            try
            {
                List<Dictionary<string, object>> removeCustomErrFields = new List<Dictionary<string, object>>();

                List<string> fieldsRequester = new List<string>();

                Dictionary<string, object> removeFieldsRequester = new Dictionary<string, object>();
                Dictionary<string, object> removedRequesterFeild = new Dictionary<string, object>();
                Dictionary<string, object> zdTicketCreate = new Dictionary<string, object>();

                Dictionary<string, object> errMsgDeserialize = new Dictionary<string, object>();
                errMsgDeserialize = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseZDMsg);
                Dictionary<string, object> details = new Dictionary<string, object>();
                List<Dictionary<string, object>> errorFields = new List<Dictionary<string, object>>();
                if (errMsgDeserialize.Keys.Contains("error"))
                {
                    details = JsonConvert.DeserializeObject<Dictionary<string, object>>(errMsgDeserialize["details"].ToString());

                    foreach (var item in details)
                    {
                        if (item.Key.ToString().ToLower() == "base")
                        {
                            List<Dictionary<string, object>> baseFields = new List<Dictionary<string, object>>();
                            baseFields = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(item.Value.ToString());
                            List<string> ticketFieldIDs = new List<string>();
                            foreach (var baseField in baseFields)
                            {
                                ticketFieldIDs.Add(baseField["ticket_field_id"].ToString());
                            }

                            foreach (var removeFieldDictionary in jsonmap)
                            {
                                foreach (var kPair in removeFieldDictionary)
                                {
                                    if (kPair.Key.ToString().ToLower() == "id")
                                    {
                                        var match = ticketFieldIDs.FirstOrDefault(stringToCheck => stringToCheck.Contains(kPair.Value.ToString()));

                                        if (match == null)
                                        {
                                            removeCustomErrFields.Add(removeFieldDictionary);
                                        }
                                        else
                                        {
                                            Dictionary<string, object> objRemoveCustomFields = new Dictionary<string, object>();
                                            foreach (var fieldsList in ticketFieldList)
                                            {
                                                foreach (var fields in fieldsList)
                                                {
                                                    if (fields.Key.ToString().ToLower() == "id")
                                                    {
                                                        if (kPair.Value.ToString().ToLower() == fields.Value.ToString().ToLower())
                                                        {
                                                            var objFields = fieldsList["title"];
                                                            Dictionary<string, object> objTemp = new Dictionary<string, object>();
                                                            objTemp.Add(objFields.ToString(), removeFieldDictionary["value"].ToString());
                                                            dicRemovedFields.Add(objTemp);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else if (item.Key.ToString().ToLower() == "requester")
                        {
                            List<Dictionary<string, object>> requesterFields = new List<Dictionary<string, object>>();
                            requesterFields = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(item.Value.ToString());
                            if (requesterFields.Count == 1)
                            {
                                Dictionary<string, object> requesterRecord = requesterFields[0];
                                string descriptionFields = requesterRecord["description"].ToString();
                                string[] listFields = descriptionFields.Split(':');
                                fieldsRequester.Add(listFields[1]);
                            }


                            foreach (var requesterFieldKey in requesterFieldsZendesk.Keys.ToList())
                            {
                                foreach (string field in fieldsRequester)
                                {
                                    if (requesterFieldKey.ToString().Trim().ToLower() != field.Trim().ToLower())
                                    {
                                        removeFieldsRequester.Add(requesterFieldKey.ToString(), requesterFieldsZendesk[requesterFieldKey].ToString());
                                    }
                                    else
                                    {
                                        removedRequesterFeild.Add(requesterFieldKey.ToString(), requesterFieldsZendesk[requesterFieldKey].ToString());
                                        dicRemovedFields.Add(removedRequesterFeild);
                                    }
                                }
                            }
                        }
                    }

                    string subject = "";
                    string description = "";

                    List<Dictionary<string, object>> objTicketReqNCustomers = new List<Dictionary<string, object>>();
                    foreach (var obj in objTicket)
                    {
                        if (obj.Key.ToString().ToLower() == "ticket")
                        {

                            foreach (var objDic in (Dictionary<string, object>)obj.Value)
                            {
                                if (objDic.Key.ToString().ToLower() == "subject")
                                {
                                    subject = objDic.Value.ToString();
                                }
                                else if (objDic.Key.ToString().ToLower() == "description")
                                {
                                    description = objDic.Value.ToString();
                                }
                            }
                        }
                    }

                    zdTicketCreate.Add("subject", subject);
                    zdTicketCreate.Add("description", description);
                    if (removeCustomErrFields != null)
                    {
                        if (removeCustomErrFields.Count == 0)
                        {
                            removeCustomErrFields = jsonmap;
                        }
                    }
                    if (removeFieldsRequester != null)
                    {
                        if (removeFieldsRequester.Count == 0)
                        {
                            removeFieldsRequester = requesterFieldsZendesk;
                        }
                    }
                    zdTicketCreate.Add("requester", removeFieldsRequester);
                    zdTicketCreate.Add("custom_fields", removeCustomErrFields);

                }

                Dictionary<string, object> zdFinalTicket = new Dictionary<string, object>();
                zdFinalTicket.Add("ticket", zdTicketCreate);

                string jsonZenTicket = JsonConvert.SerializeObject(zdFinalTicket);

                HttpClient httpClient = new HttpClient();

                HttpRequestMessage zenReqCreateTicket = new HttpRequestMessage(HttpMethod.Post, ZDURL + insertZDTicketURL);
                zenReqCreateTicket.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(apiKey)));
                zenReqCreateTicket.Content = new StringContent(jsonZenTicket, Encoding.UTF8, "application/json");

                HttpResponseMessage zenResCreateTicket = new HttpResponseMessage();
                zenResCreateTicket = await httpClient.SendAsync(zenReqCreateTicket);

                string responseZenCreateTicket = await zenResCreateTicket.Content.ReadAsStringAsync();

                if (zenResCreateTicket.IsSuccessStatusCode)
                {
                    string ZDTicketId = GetZDTicketNumberByResponseMsg(responseZenCreateTicket);
                    await CreatenoteOnTicketCreation(ZDURL, ZDTicketId, userName, CCapikey, ansID);
                    string fields = "";
                    foreach (var errField in dicRemovedFields)
                    {
                        foreach (var item in errField.Keys)
                        {
                            fields = fields + "  " + item + ":" + errField[item].ToString();
                        }
                    }

                    bool flag = await sentry.LogThePartialSuccessRecord(ansID, DateTime.Now.ToString(), fields, fields, fields, "", ExceptionType.FieldLevel, userName, IntegrationType.zendesk);
                    if (flag)
                        return true;
                }
                else
                {
                    bool flag = await sentry.LogTheFailedRecord(responseZenCreateTicket, "FieldLevel", "", ExceptionType.Create, ansID, userName, IntegrationType.zendesk);

                    if (flag)
                        return false;
                }

            }
            catch (JsonSerializationException jsonEx)
            {
                Exception_Type = "FieldLevel";

            }
            catch (Exception ex) { }
            if (Exception_Type == "FieldLevel")
            {
                await sentry.LogTheFailedRecord(responseZDMsg, "FieldLevel", "", ExceptionType.Create, ansID, userName, IntegrationType.zendesk);
            }
            return false;
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
                                ans = (object)Convert.ToInt32(ans);
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



        //Get All ticket fields from the Zendesk
        [HttpGet]
        [Route("api/ZendeskCC/getZendeskTicketFields")]
        public async Task<List<Dictionary<string, object>>> getZendeskTicketFields(string ZDURL, string ZDKey, string ZDEmail)
        {
            try
            {
                string zenTicketFieldsURL = "/api/v2/ticket_fields.json";
                HttpClient httpClient = new HttpClient();
                HttpRequestMessage zenReqCreateTicket = new HttpRequestMessage(HttpMethod.Get, ZDURL + zenTicketFieldsURL);
                zenReqCreateTicket.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(ZDEmail + "/token:" + ZDKey)));

                HttpResponseMessage zenResCreateTicket = new HttpResponseMessage();
                zenResCreateTicket = await httpClient.SendAsync(zenReqCreateTicket);

                string responseZenCreateTicket = await zenResCreateTicket.Content.ReadAsStringAsync();

                Dictionary<string, object> ticketFieldsResponse = new Dictionary<string, object>();
                ticketFieldsResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseZenCreateTicket);

                List<Dictionary<string, object>> ticketFields = new List<Dictionary<string, object>>();
                ticketFields = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(ticketFieldsResponse["ticket_fields"].ToString());

                List<Dictionary<string, object>> activeTicketFields = new List<Dictionary<string, object>>();

                foreach (Dictionary<string, object> ticketObject in ticketFields)
                {
                    foreach (KeyValuePair<string, object> checkActive in ticketObject)
                    {
                        if (checkActive.Key.ToString().ToLower() == "active" && checkActive.Value.ToString().ToLower() == "true" && (!(ticketObject["type"].ToString() == "regexp")))
                        {
                            activeTicketFields.Add(ticketObject);
                        }
                    }
                }
                return activeTicketFields;

            }
            catch (Exception ex)
            {
                return new List<Dictionary<string, object>>();
            }
        }

        async Task<HttpResponseMessage> CreatenoteOnTicketCreation(string ZDURL, string ZDTicketId, string ccUserName, string CCApikey, string Answerid)
        {
            try
            {
                MessageNotes notes = new MessageNotes();
                notes.note = ConfigurationManager.AppSettings["ZDTicketCreation"].ToString() + " " + ZDURL + @"agent/tickets/" + ZDTicketId;
                notes.noteTime = System.DateTime.Now.ToString();
                return await cloudCherryController.UpdateNote(notes, Answerid, ccUserName, CCApikey);
            }
            catch
            {
                return new HttpResponseMessage();
            }
        }

        public string GetZDTicketNumberByResponseMsg(string responseFDMsg)
        {
            try
            {
                Dictionary<string, object> obj = new Dictionary<string, object>();
                obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseFDMsg);
                string ZDTicketId = "";
                Dictionary<string, object> ticketObject = new Dictionary<string, object>();
                ticketObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(obj["ticket"].ToString());
                ZDTicketId = ticketObject["id"].ToString();
                return ZDTicketId;
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        [HttpGet]
        [Route("api/ZendeskCC/getZendeskcredentials")]
        public async Task<Dictionary<string, object>> getZendeskcredentials(string userName, string integrationtype)
        {
            Dictionary<string, object> Credentials = new Dictionary<string, object>();

            try
            {

                Dictionary<string, object> Userdata = cloudCherryController.GetUserCredentials(userName, (IntegrationType)Enum.Parse(typeof(IntegrationType), integrationtype, true));
                string CCApikey = Userdata["ccapikey"].ToString();
                char[] splitstr = { '|' };
                string[] integrationdetails = Userdata["integrationDetails"].ToString().Split(splitstr);
                Userdata.Add("ZDURL", integrationdetails[0]);
                Userdata.Add("ZDAPIKey", integrationdetails[1]);
                Userdata.Add("ZDEmail", integrationdetails[2]);
                return Userdata;
            }
            catch { }
            return new Dictionary<string, object>();
        }
    }
}
