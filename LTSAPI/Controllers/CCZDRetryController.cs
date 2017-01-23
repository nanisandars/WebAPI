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
    public class CCZDRetryController : ApiController
    {

        CloudCherryController cloudCherryController = new CloudCherryController();
        SentryController sentry = new SentryController();


        IntegrationType integrationType = IntegrationType.zendesk;


        //GetCCNoteInsertRetryRecords(ZD CC Inserting RetryRecords posted from ZD to CC for Notes)
        #region
        [HttpPost, HttpGet]
        [Route("api/CCZDRetry/GetCCNotesInsertRetryRecords")]
        public async Task<IHttpActionResult> getInsertNoteRetryRecords()
        {
            string Exceptionmsgtype = "";
            string Exceptionmessage = "";
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

                Dictionary<string, object> Exceptiondata = cloudCherryController.getClientsettingsByKey("apiKey", apiKey, IntegrationType.zendesk);


                ccusername = Exceptiondata["username"].ToString();

                Dictionary<string, object> NewExceptiondata = new Dictionary<string, object>();
                NewExceptiondata.Add("_Id", "InsertZDTicketsRetry" + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + DateTime.Now.Hour + DateTime.Now.Minute + DateTime.Now.Second + DateTime.Now.Millisecond);
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
                Exceptionmessage = ex.Message;
                Exceptionmsgtype = "Unknown Exception";

            }
            if (Exceptionmsgtype == "Unknown Exception")
            {
                await sentry.LogTheFailedRecord(Exceptionmessage, "Unknown Exception", "", ExceptionType.Create, "", ccusername, IntegrationType.zendesk);
                return BadRequest();
            }
            return BadRequest();
        }

        #endregion

        #region RetryManually

        //Insert the records manually fired from the SCREEN at ZD END
        [HttpPost]
        [Route("api/CCZDRetry/ManualTriggerZDTicketInserts")]
        public async Task<HttpResponseMessage> retryManuallyFailedRecordsZDInsert(string userName)
        {
            string ZDEmail = "";
            string ZDAPIKey = "";
            string ZDURL = "";
            string CCApikey = "";
            HttpResponseMessage resMsg = new HttpResponseMessage();
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
                    resMsg.StatusCode = HttpStatusCode.OK;
                    return resMsg;
                }

                List<Dictionary<string, object>> retryRecords = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(httpResponse); //Converting the response into dictionary collection 

                //Checks whether the reponse has been converted or not
                if (retryRecords == null)
                {
                    if (retryRecords.Count == 0)
                    {
                        resMsg.StatusCode = HttpStatusCode.OK;
                        return resMsg;
                    }
                    resMsg.StatusCode = HttpStatusCode.OK;
                    return resMsg;
                }

                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                //Specifying the INSERT API at SF
                Dictionary<string, object> doc = cloudCherryController.GetUserCredentials(userName, IntegrationType.zendesk);
                CCApikey = doc["ccapikey"].ToString();
                char[] splitstr = { '|' };
                string[] integrationdetails = doc["integrationDetails"].ToString().Split(splitstr);
                ZDURL = integrationdetails[0];
                ZDAPIKey = integrationdetails[1];
                ZDEmail = integrationdetails[0];



                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                //Get the token details of the salesforce              

                ZendeskCCController zdcc = new ZendeskCCController();

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

                            HttpResponseMessage httpCode = new HttpResponseMessage();
                            httpCode = await zdcc.AddAZendeskTicket(notificationData);
                            if (httpCode.StatusCode == HttpStatusCode.OK)
                                isexist = true;
                            else
                            {

                                isexist = true;
                                Exceptiondatacopy.Add(sentry.RenewthisRecord(record["ExceptionDescription"].ToString(), DateTime.Now.ToString(), record["ExceptionRaisedAt"].ToString(), record["FailedRecord"].ToString(), ExceptionType.Create, ansID, userName));
                            }

                        }

                    }

                    if (!isexist)
                        Exceptiondatacopy.Add(record);
                }
                await sentry.AddCompleteException(userName, integrationType, ExceptionType.Create, Exceptiondatacopy);
                Task.Delay(3000);
                resMsg.StatusCode = HttpStatusCode.OK;
                return resMsg;
            }
            catch (Exception ex)
            {
                resMsg.StatusCode = HttpStatusCode.OK;
                return resMsg;
            }
        }

        //Insert the records manually fired from the SCREEN at CC END
        [HttpPost]
        [Route("api/CCZDRetry/ManualTriggerCCNotesInserts")]
        public async Task<HttpResponseMessage> retryManuallyFailedRecordsCCInsert(string userName)
        {
            string ZDAPIKey = "";
            string ZDURL = "";
            string CCApikey = "";
            string ccticketid = "";
            HttpResponseMessage msg = new HttpResponseMessage();
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
                    msg.StatusCode = HttpStatusCode.OK;
                    return msg;
                }

                List<Dictionary<string, object>> retryRecords = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(httpResponse); //Converting the response into dictionary collection 

                //Checks whether the reponse has been converted or not
                if (retryRecords == null)
                {
                    if (retryRecords.Count == 0)
                    {
                        msg.StatusCode = HttpStatusCode.OK;
                        return msg;
                    }
                    msg.StatusCode = HttpStatusCode.OK;
                    return msg;
                }



                Dictionary<string, object> Userdata = cloudCherryController.GetUserCredentials(userName, IntegrationType.zendesk);
                CCApikey = Userdata["ccapikey"].ToString();
                char[] splitstr = { '|' };
                string[] integrationdetails = Userdata["integrationDetails"].ToString().Split(splitstr);
                ZDAPIKey = integrationdetails[1];
                ZDURL = integrationdetails[0];

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
                            ccticketid = record["AnswerId"].ToString();

                            //Declaring the notes object
                            StringBuilder noteStructure = new StringBuilder();


                            serviceEndPoint = serviceEndPoint + ccticketid; //Appendin the ticket/answer id with the service
                            HttpRequestMessage reqInsertNote = new HttpRequestMessage(HttpMethod.Post, new Uri(serviceEndPoint));
                            reqInsertNote.Content = new StringContent(arrSplit[0].ToString(), Encoding.UTF8, "application/json");

                            string ccAccessToken = await cloudCherryController.getCCAccessToken(userName, CCApikey);
                            reqInsertNote.Headers.Add("Authorization", "Bearer " + ccAccessToken);
                            HttpResponseMessage insertNoteResponse = await client.SendAsync(reqInsertNote); //Sending request to CC in order to insert NOTE
                            string stringResponse = await insertNoteResponse.Content.ReadAsStringAsync();
                            if (insertNoteResponse.IsSuccessStatusCode)
                                isexist = true;
                            else
                            {
                                record["ExceptionRaisedOn"] = DateTime.Now;
                                Exceptiondatacopy.Add(record);
                                isexist = true;
                            }
                        }
                    }

                    if (!isexist)
                        Exceptiondatacopy.Add(record);
                }

                await sentry.AddCompleteException(userName, integrationType, ExceptionType.Update, Exceptiondatacopy);
                Task.Delay(3000);


            }
            catch (Exception ex)
            {
                msg.StatusCode = HttpStatusCode.BadRequest;
                return msg;
            }
            msg.StatusCode = HttpStatusCode.BadRequest;
            return msg;
        }

        #endregion

        #region SchedularServiceToRetry
        //Retries the failed records while inserting a CASE in ZD and Deletes from the LOG Table after successful inserion at ZD as a CASE.
        [HttpGet]
        [Route("api/CCZDRetry/AutoRetryZDFailedRecords")]
        public async Task<IHttpActionResult> retryFailedRecordsZDInsert(string userName, string Integration)
        {
            string CCApikey = "";
            string ZDAPIKey = "";
            string ZDURL = "";
            string ZDEmail = "";
            try
            {
                HttpClient client = new HttpClient();

                client.Timeout = TimeSpan.FromSeconds(10);
                Dictionary<string, object> Userdata = cloudCherryController.GetUserCredentials(userName, IntegrationType.zendesk);
                CCApikey = Userdata["ccapikey"].ToString();
                char[] splitstr = { '|' };
                string[] integrationdetails = Userdata["integrationDetails"].ToString().Split(splitstr);
                ZDURL = integrationdetails[0];
                ZDAPIKey = integrationdetails[1];
                ZDEmail = integrationdetails[2];
                string serviceEndPoint = ZDURL + "/api/v2/tickets.json";

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
                            reqInsertCase.Headers.Add("Authorization", "Bearer " + Convert.ToBase64String(Encoding.Default.GetBytes(ZDEmail + "/token:" + ZDAPIKey)));
                            HttpResponseMessage inserCaseResponse = await client.SendAsync(reqInsertCase);
                            string respString = await inserCaseResponse.Content.ReadAsStringAsync();
                            if (inserCaseResponse.StatusCode != HttpStatusCode.OK)
                            {
                                await sentry.LogTheFailedRecord(respString, "Token", "", ExceptionType.General, ansId, userName, IntegrationType.zendesk);

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
        [Route("api/CCZDRetry/AutoRetryZDFailedRecords")]
        public async Task retryFailedRecordsZDInsert()
        {
            List<string> Usernamelist = new List<string>();
            Dictionary<string, object> repeatedUsernamelist = new Dictionary<string, object>();
            List<Dictionary<string, object>> Alluserdata = await sentry.GetAllUserData(integrationType);

            foreach (Dictionary<string, object> singeluserdata in Alluserdata)
            {
                Dictionary<string, object> metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(singeluserdata["metadata"].ToString());

                string responseBody = metadata["value"].ToString();
                int firstindex = responseBody.IndexOf(":"); ;
                responseBody = responseBody.Substring(0, firstindex);
                responseBody = responseBody.Replace("{", "");
                responseBody = responseBody.Replace("\"", "");
                string username = responseBody;
                await retryFailedRecordsZDInsert(username, integrationType.ToString());
                Task.Delay(3000);
                await retryFailedRecordsCCInsert(username, integrationType.ToString(), DateTime.Now.AddDays(-30).ToString(), DateTime.Now.ToString());
                Task.Delay(3000);
            }

        }


        //Retries the failed records while inserting a ticket in ZD and Deletes from the LOG Table after successful inserion at ZD as a CASE.
        [HttpGet]
        [Route("api/CCSFRetry/AutoRetryCCFailedRecords")]
        public async Task<IHttpActionResult> retryFailedRecordsCCInsert(string userName, string Integration, string startDate, string endDate)
        {
            string ZDAPIKey = "";
            string ZDURL = "";
            string CCApikey = "";
            string CCTicketId = "";
            try
            {
                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                MessageNotes notes = new MessageNotes();
                string serviceEndPoint = "https://api.getcloudcherry.com/api/Answers/Note/";

                Dictionary<string, object> Userdata = cloudCherryController.GetUserCredentials(userName, IntegrationType.zendesk);
                CCApikey = Userdata["ccapikey"].ToString();
                char[] splitstr = { '|' };
                string[] integrationdetails = Userdata["integrationDetails"].ToString().Split(splitstr);
                ZDAPIKey = integrationdetails[1];
                ZDURL = integrationdetails[0];

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
                                Exceptiondatacopy.Add(sentry.RenewthisRecord(record["FailedRecord"].ToString() + "      " + responseCase, DateTime.Now.ToString(), "ZDCC", record["FailedRecord"].ToString(), ExceptionType.Update, "", userName));
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
