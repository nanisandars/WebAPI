using Cherry.HelperClasses;
using LTSAPI.CC_Classes;
using LTSAPI.HelperClasses;
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
using LTSAPI.Controllers;

/* This is used to retry all the exceptions both manual and auto */
namespace LTSAPI.Controllers
{
    public class CCFDRetryController : ApiController
    {
        FreshDesk freshDesk = new FreshDesk();
        CloudCherryController cloudCherryController = new CloudCherryController();
        SentryController sentry = new SentryController();


        IntegrationType integrationType = IntegrationType.freshdesk;
        string FDSettingsPath = ConfigurationManager.AppSettings["FDSettingsPath"];

        //GetCCNoteInsertRetryRecords
        #region FD CC Inserting RetryRecords posted from FD to CC for Notes

        [HttpPost, HttpGet]
        [Route("api/CCFDRetry/GetCCNotesInsertRetryRecords")]
        public async Task<IHttpActionResult> getInsertNoteRetryRecords()
        {
            string ccusername = "";
            string apiKey = "";
            string httpResponse = "";
            try
            {
                HttpClient httpClient = new HttpClient();

                //Read response from the requset
                using (var contentStream = await this.Request.Content.ReadAsStreamAsync())
                {
                    contentStream.Seek(0, SeekOrigin.Begin);
                    using (var sr = new StreamReader(contentStream))
                    {
                        httpResponse = sr.ReadToEnd();
                    }
                }

                //Checking whether the response is empty or not
                if (httpResponse == "")
                {
                    //If the response is empty returning badrequest
                    return BadRequest();
                }

                //Declaring the variables to store the values of the response fields in order to generate required document
                string caseLabel = "";
                string exceptionDescription = "";
                string exceptionRaisedAt = "";
                string exceptonRaisedOn = "";
                string failedRecord = "";
                string exceptionType = "";

                Dictionary<string, object> retryRecords = JsonConvert.DeserializeObject<Dictionary<string, object>>(httpResponse);

                //Checking whether the response has the records or not
                if (retryRecords == null)
                {
                    if (retryRecords.Count == 0)
                    {
                        return BadRequest();
                    }
                    return BadRequest();
                }

                foreach (var item in retryRecords)
                {
                    caseLabel = item.Key.ToString();
                    switch (caseLabel)
                    {
                        case "ExceptionDescription":
                            {
                                exceptionDescription = item.Value.ToString();
                                break;
                            }
                        case "ExceptionRaisedAt":
                            {
                                exceptionRaisedAt = item.Value.ToString();
                                break;
                            }
                        case "ExceptionRaisedOn":
                            {
                                exceptonRaisedOn = item.Value.ToString();
                                break;
                            }
                        case "FailedRecord":
                            {
                                failedRecord = item.Value.ToString();
                                break;
                            }
                        case "ExceptionType":
                            {
                                exceptionType = item.Value.ToString();
                                break;
                            }
                        case "apiKey":
                            {
                                apiKey = item.Value.ToString();
                                break;
                            }
                    }
                }

                Dictionary<string, object> Exceptiondata = cloudCherryController.getClientsettingsByKey("apiKey", apiKey, IntegrationType.freshdesk);


                ccusername = Exceptiondata["username"].ToString();

                Dictionary<string, object> NewExceptiondata = new Dictionary<string, object>();
                NewExceptiondata.Add("_Id", "InsertFDTicketsRetry" + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + DateTime.Now.Hour + DateTime.Now.Minute + DateTime.Now.Second + DateTime.Now.Millisecond);
                NewExceptiondata.Add("ExceptionType", exceptionType);
                NewExceptiondata.Add("ExceptionDescription", exceptionDescription);
                NewExceptiondata.Add("ExceptionRaisedAt", exceptionRaisedAt);
                NewExceptiondata.Add("ExceptionRaisedOn", exceptonRaisedOn);
                NewExceptiondata.Add("FailedRecord", failedRecord);
                NewExceptiondata.Add("ccusername", ccusername);

                await sentry.AddNewExceptionData(ccusername, integrationType, ExceptionType.Update, NewExceptiondata);
                return Ok();
            }
            catch (Exception ex)
            {
                sentry.LogTheFailedRecord(ex.Message, "Unknown Exception", "", ExceptionType.Create, "", ccusername, IntegrationType.freshdesk);
                return BadRequest();
            }
        }

        #endregion

        #region RetryManually

        //Insert the records manually fired from the SCREEN at FD END
        [HttpPost]
        [Route("api/CCFDRetry/ManualTriggerFDTicketInserts")]
        public async Task<List<Dictionary<string, object>>> retryManuallyFailedRecordsFDInsert(string userName, string startDate, string endDate)
        {
            string FDAPIKey = "";
            string FDURL = "";
            string CCApikey = "";

            try
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
                    return null;
                }

                List<Dictionary<string, object>> retryRecords = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(httpResponse); //Converting the response into dictionary collection 

                //Checks whether the reponse has been converted or not
                if (retryRecords == null)
                {
                    if (retryRecords.Count == 0)
                    {
                        return null;
                    }
                    return null;
                }

                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                //Specifying the INSERT API at SF
                Dictionary<string, object> doc = cloudCherryController.GetUserCredentials(userName, IntegrationType.freshdesk);
                CCApikey = doc["ccapikey"].ToString();
                char[] splitstr = { '|' };
                string[] integrationdetails = doc["integrationDetails"].ToString().Split(splitstr);
                FDAPIKey = integrationdetails[1];
                FDURL = integrationdetails[0];
                //string serviceEndPoint = FDURL + "/api/v2/tickets";
                //string apiKey = FDAPIKey + ":X";

                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                //Get the token details of the salesforce              

                FDCCController fdcc = new FDCCController();

                Dictionary<string, object> Integrationdata = await sentry.GetIntegrationData(userName, integrationType);

                List<Dictionary<string, object>> Exceptiondata = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(Integrationdata[ExceptionType.Create.ToString()].ToString());

                List<Dictionary<string, object>> Exceptiondatacopy = new List<Dictionary<string, object>>();
                foreach (var record in Exceptiondata)
                {
                    bool isexist = false;
                    foreach (Dictionary<string, object> retryRecord in retryRecords)
                    {
                        if (record["_Id"].ToString() == retryRecord["id"].ToString())
                        {
                            string ansID = record["AnswerId"].ToString();
                            string answerURL = "https://api.getcloudcherry.com/api/Answer/";
                            //Get AnswerByID from CC
                            HttpRequestMessage reqAnswerFromCC = new HttpRequestMessage(HttpMethod.Get, new Uri(answerURL + ansID));
                            string ccAccessToken = await cloudCherryController.getCCAccessToken(userName, CCApikey);
                            reqAnswerFromCC.Headers.Add("Authorization", "Bearer " + ccAccessToken);
                            HttpResponseMessage resAnswerFromCC = new HttpResponseMessage();
                            resAnswerFromCC = await client.SendAsync(reqAnswerFromCC);
                            string responseAnswer = await resAnswerFromCC.Content.ReadAsStringAsync();
                            PostSurvey answer = new PostSurvey();
                            answer = JsonConvert.DeserializeObject<PostSurvey>(responseAnswer);
                            NotificationData notificationData = new NotificationData();
                            notificationData.answer = answer;
                            notificationData.notification = userName;

                            HttpStatusCode httpCode = new HttpStatusCode();
                            httpCode = await fdcc.AddATicket(notificationData);

                            if (httpCode == HttpStatusCode.Created)
                            {
                                isexist = true;
                            }
                        }
                        isexist = true;
                    }

                    if (!isexist)
                        Exceptiondatacopy.Add(record);
                }
                await sentry.AddCompleteException(userName, integrationType, ExceptionType.Create, Exceptiondatacopy);
                Task.Delay(3000);
                return new List<Dictionary<string, object>>();
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        //Insert the records manually fired from the SCREEN at CC END
        [HttpPost]
        [Route("api/CCFDRetry/ManualTriggerNotesInserts")]
        public async Task<List<Dictionary<string, object>>> retryManuallyFailedRecordsCCInsert(string userName, string startDate, string endDate)
        {
            string FDAPIKey = "";
            string FDURL = "";
            string CCApikey = "";
            string ccticketid = "";
            try
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
                    return null;
                }

                List<Dictionary<string, object>> retryRecords = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(httpResponse); //Converting the response into dictionary collection 

                //Checks whether the reponse has been converted or not
                if (retryRecords == null)
                {
                    if (retryRecords.Count == 0)
                    {
                        return null;
                    }
                    return null;
                }

                FreshDesk frshDesk = new FreshDesk();

                Dictionary<string, object> Userdata = cloudCherryController.GetUserCredentials(userName, IntegrationType.freshdesk);
                CCApikey = Userdata["ccapikey"].ToString();
                char[] splitstr = { '|' };
                string[] integrationdetails = Userdata["integrationDetails"].ToString().Split(splitstr);
                FDAPIKey = integrationdetails[1];
                FDURL = integrationdetails[0];

                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                //Specifying the INSERT API at SF
                string serviceEndPoint = "https://api.getcloudcherry.com/api/Answers/Note/";

                Dictionary<string, object> Integrationdata = await sentry.GetIntegrationData(userName, integrationType);

                List<Dictionary<string, object>> Exceptiondata = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(Integrationdata[ExceptionType.Update.ToString()].ToString());

                MessageNotes notes = new MessageNotes();
                List<Dictionary<string, object>> Exceptiondatacopy = new List<Dictionary<string, object>>();
                foreach (var record in Exceptiondata)
                {
                    bool isexist = false;
                    foreach (Dictionary<string, object> retryRecord in retryRecords)
                    {
                        if (record["_Id"].ToString() == retryRecord["id"].ToString())
                        {
                            string[] arrSplit = record["FailedRecord"].ToString().Split(new string[] { "||||" }, StringSplitOptions.None);
                            ccticketid = arrSplit[1];

                            Dictionary<string, object> ticketDocument = JsonConvert.DeserializeObject<Dictionary<string, object>>(arrSplit[0]);

                            if (ticketDocument == null)
                            {
                                if (ticketDocument.Count == 0)
                                {
                                    return null;
                                }
                                return null;
                            }

                            string CCTicketId = "";

                            //Declaring the notes object
                            StringBuilder noteStructure = new StringBuilder();

                            //Loop through the key values in the dictionary of the failed record
                            foreach (var res in ticketDocument)
                            {
                                StringBuilder propertiesNote = new StringBuilder();
                                if ((!(res.Key == "attributes")) && (!(res.Key == "apiKey")))
                                {
                                    propertiesNote.Append(res.Key + ":" + res.Value + "      ");
                                }
                                noteStructure.Append(propertiesNote);
                            }

                            notes.noteTime = DateTime.Now.ToString();
                            notes.note = noteStructure.ToString();
                            var serializeNote = JsonConvert.SerializeObject(notes);
                            serviceEndPoint = serviceEndPoint + ccticketid; //Appendin the ticket/answer id with the service
                            HttpRequestMessage reqInsertNote = new HttpRequestMessage(HttpMethod.Post, new Uri(serviceEndPoint));
                            reqInsertNote.Content = new StringContent(serializeNote, Encoding.UTF8, "application/json");

                            string ccAccessToken = await cloudCherryController.getCCAccessToken(userName, CCApikey);
                            reqInsertNote.Headers.Add("Authorization", "Bearer " + ccAccessToken);
                            HttpResponseMessage insertNoteResponse = await client.SendAsync(reqInsertNote); //Sending request to CC in order to insert NOTE
                            string stringResponse = await insertNoteResponse.Content.ReadAsStringAsync();
                            isexist = true;
                        }
                    }

                    if (!isexist)
                        Exceptiondatacopy.Add(record);
                }

                await sentry.AddCompleteException(userName, integrationType, ExceptionType.Update, Exceptiondatacopy);
                Task.Delay(3000);
                return await sentry.GetGivenExceptionData(userName, integrationType.ToString(), ExceptionType.Update.ToString(), startDate, endDate);

            }
            catch (Exception ex)
            {
                return null;
            }

        }

        #endregion

        #region SchedularServiceToRetry

        //Retries the failed records while inserting a CASE in FD and Deletes from the LOG Table after successful inserion at FD as a CASE.
        [HttpGet]
        [Route("api/CCFDRetry/AutoRetryFDFailedRecords")]
        public async Task<IHttpActionResult> retryFailedRecordsFDInsert(string userName, string Integration)
        {
            string CCApikey = "";
            string FDAPIKey = "";
            string FDURL = "";
            try
            {
                HttpClient client = new HttpClient();
                FreshDesk freshDesk = new FreshDesk();
                client.Timeout = TimeSpan.FromSeconds(10);
                Dictionary<string, object> Userdata = cloudCherryController.GetUserCredentials(userName, IntegrationType.freshdesk);
                CCApikey = Userdata["ccapikey"].ToString();
                char[] splitstr = { '|' };
                string[] integrationdetails = Userdata["integrationDetails"].ToString().Split(splitstr);
                FDAPIKey = integrationdetails[1];
                FDURL = integrationdetails[0];
                string serviceEndPoint = FDURL + "/api/v2/tickets";
                string apiKey = FDAPIKey + ":X";

                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                Dictionary<string, object> Integrationdata = await sentry.GetIntegrationData(userName, integrationType);

                List<Dictionary<string, object>> Exceptiondata = new List<Dictionary<string, object>>();

                foreach (KeyValuePair<string, object> kpdata in Integrationdata)
                {
                    string[] exceptiontypes = new string[] { ExceptionType.Create.ToString() };
                    if (!exceptiontypes.Contains(kpdata.Key))
                        continue;
                    Exceptiondata = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(Integrationdata[kpdata.Key].ToString());

                    MessageNotes notes = new MessageNotes();
                    List<Dictionary<string, object>> Exceptiondatacopy = new List<Dictionary<string, object>>();
                    foreach (var record in Exceptiondata)
                    {
                        bool isexist = false;

                        if (record.Keys.Contains("FailedRecord"))
                        {
                            string ansId = "";
                            Dictionary<string, object> failRecs = JsonConvert.DeserializeObject<Dictionary<string, object>>(record["FailedRecord"].ToString());
                            Dictionary<string, object> failRecord = new Dictionary<string, object>();

                            HttpRequestMessage reqInsertCase = new HttpRequestMessage(HttpMethod.Post, new Uri(serviceEndPoint));
                            reqInsertCase.Content = new StringContent(record["FailedRecord"].ToString(), Encoding.UTF8, "application/json");
                            reqInsertCase.Headers.Add("Authorization", "Bearer " + Convert.ToBase64String(Encoding.Default.GetBytes(apiKey)));
                            HttpResponseMessage inserCaseResponse = await client.SendAsync(reqInsertCase);
                            string respString = await inserCaseResponse.Content.ReadAsStringAsync();
                            if (inserCaseResponse.StatusCode != HttpStatusCode.OK)
                            {
                                await sentry.LogTheFailedRecord(record["FailedRecord"].ToString() + "<br/>" + respString, "Token", "", ExceptionType.General, ansId, userName, IntegrationType.freshdesk);

                            }
                        }

                        if (!isexist)
                            Exceptiondatacopy.Add(record);
                    }

                    await sentry.AddCompleteException(userName, integrationType, ExceptionType.Create, Exceptiondatacopy);// cloudCherryController.AddCompleteException(userName, kpdata.Key, Exceptiondatacopy);
                    Task.Delay(3000);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest();
            }

        }

        [HttpGet]
        [Route("api/CCFDRetry/AutoRetryFDFailedRecords")]
        public async Task retryFailedRecordsFDInsert()
        {
            List<string> Usernamelist = new List<string>();
            Dictionary<string, object> repeatedUsernamelist = new Dictionary<string, object>();
            List<Dictionary<string, object>> Alluserdata = await sentry.GetAllUserData(integrationType);
            bool Isusersrepeated = false;
            foreach (Dictionary<string, object> singeluserdata in Alluserdata)
            {
                Dictionary<string, object> metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(singeluserdata["metadata"].ToString());

                string responseBody = metadata["value"].ToString();
                int firstindex = responseBody.IndexOf(":"); ;
                responseBody = responseBody.Substring(0, firstindex);
                responseBody = responseBody.Replace("{", "");
                responseBody = responseBody.Replace("\"", "");
                string username = responseBody;
                await retryFailedRecordsFDInsert(username, integrationType.ToString());
                Task.Delay(3000);
                await retryFailedRecordsCCInsert(username, integrationType.ToString(), DateTime.Now.AddDays(-30).ToString(), DateTime.Now.ToString());
                Task.Delay(3000);
            }

        }


        //Retries the failed records while inserting a ticket in FD and Deletes from the LOG Table after successful inserion at FD as a CASE.
        [HttpGet]
        [Route("api/CCSFRetry/AutoRetryCCFailedRecords")]
        public async Task<IHttpActionResult> retryFailedRecordsCCInsert(string userName, string Integration, string startDate, string endDate)
        {
            string FDAPIKey = "";
            string FDURL = "";
            string CCApikey = "";
            string CCTicketId = "";
            try
            {
                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                MessageNotes notes = new MessageNotes();
                string serviceEndPoint = "https://api.getcloudcherry.com/api/Answers/Note/";

                Dictionary<string, object> Userdata = cloudCherryController.GetUserCredentials(userName, IntegrationType.freshdesk);
                CCApikey = Userdata["ccapikey"].ToString();
                char[] splitstr = { '|' };
                string[] integrationdetails = Userdata["integrationDetails"].ToString().Split(splitstr);
                FDAPIKey = integrationdetails[1];
                FDURL = integrationdetails[0];

                Dictionary<string, object> Integrationdata = await sentry.GetIntegrationData(userName, (IntegrationType)Enum.Parse(typeof(IntegrationType), Integration, true));

                List<Dictionary<string, object>> Exceptiondata = new List<Dictionary<string, object>>();

                string ccAccessToken = await cloudCherryController.getCCAccessToken(userName, CCApikey);

                foreach (KeyValuePair<string, object> kpdata in Integrationdata)
                {
                    string[] exceptiontypes = new string[] { "InsertNotesRetry" };
                    if (!exceptiontypes.Contains(kpdata.Key))
                        continue;
                    Exceptiondata = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(Integrationdata[kpdata.Key].ToString());
                    List<Dictionary<string, object>> Exceptiondatacopy = new List<Dictionary<string, object>>();
                    foreach (var record in Exceptiondata)
                    {
                        bool isexist = false;

                        if (record.Keys.Contains("FailedRecord"))
                        {
                            StringBuilder propertiesNote = new StringBuilder();
                            string[] arrSplits = record["FailedRecord"].ToString().Split(new string[] { "||||" }, StringSplitOptions.None);
                            CCTicketId = arrSplits[1];
                            Dictionary<string, object> ticketDocument = JsonConvert.DeserializeObject<Dictionary<string, object>>(arrSplits[0]);
                            StringBuilder noteStructure = new StringBuilder();
                            foreach (var res in ticketDocument)
                            {
                                noteStructure.Append(res.Key + ":" + res.Value + "      ");
                            }
                            notes.noteTime = DateTime.Now.ToString();
                            notes.note = noteStructure.ToString();
                            var serializeNote = JsonConvert.SerializeObject(notes);
                            serviceEndPoint = serviceEndPoint + CCTicketId; ;
                            HttpRequestMessage reqInsertNote = new HttpRequestMessage(HttpMethod.Post, new Uri(serviceEndPoint));
                            reqInsertNote.Headers.Add("Authorization", "Bearer " + ccAccessToken);
                            reqInsertNote.Content = new StringContent(serializeNote, Encoding.UTF8, "application/json");
                            HttpResponseMessage insertCaseResponse = await client.SendAsync(reqInsertNote);
                            string responseCase = await insertCaseResponse.Content.ReadAsStringAsync();

                            if (insertCaseResponse.StatusCode != HttpStatusCode.OK)
                            {
                                Exceptiondatacopy.Add(sentry.RenewthisRecord(record["FailedRecord"].ToString() + "      " + responseCase, DateTime.Now.ToString(), "FDCC", record["FailedRecord"].ToString(), ExceptionType.Update, "", userName));
                                isexist = true;
                            }

                            if (insertCaseResponse.StatusCode == HttpStatusCode.OK) //Successful insertion of NOTE at CC happens
                            {
                                isexist = true;
                            }

                        }
                        if (!isexist)
                            Exceptiondatacopy.Add(record);
                    }

                    await sentry.AddCompleteException(userName, integrationType, ExceptionType.Update, Exceptiondatacopy);
                    Task.Delay(3000);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest();
            }

        }
        #endregion
    }
}
