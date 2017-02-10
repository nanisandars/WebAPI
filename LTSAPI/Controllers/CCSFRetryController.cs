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



namespace LTSAPI.Controllers
{
    public class CCSFRetryController : ApiController
    {
        CloudCherryController cloudCherryController = new CloudCherryController();
        Connect2SFController connect2sf = new Connect2SFController();
        SentryController sentry = new SentryController();

        Cherry.HelperClasses.SalesForce salesforce = new Cherry.HelperClasses.SalesForce();
        IntegrationType integrationType = IntegrationType.salesforce;

        #region Reconnect

        //A New Sales force is created for the given  cc username
        [HttpGet]
        [Route("api/CCSFRetry/ReConnectToSF")]
        public async Task<HttpResponseMessage> getSFNewRefreshToken(string CCUsername)
        {
            try
            {
                Dictionary<string, object> Exceptiondata = cloudCherryController.getClientsettingsByKey("username", CCUsername, IntegrationType.salesforce);

                string clientId = Exceptiondata["clientid"].ToString();
                string secretKey = Exceptiondata["secret"].ToString();
                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                string authEndPoint = "https://login.salesforce.com/services/oauth2/authorize";
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                                    var postCrendentials = new[] {
                    new KeyValuePair<string,string>("response_type","code"),
                    new KeyValuePair<string,string>("client_id", clientId),    
                    new KeyValuePair<string,string>("client_secret", secretKey),  
                    new KeyValuePair<string,string>("redirect_uri", ConfigurationManager.AppSettings["SFCallbackURL"])
                    };

                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, new Uri(authEndPoint));
                req.Content = new FormUrlEncodedContent(postCrendentials);

                HttpResponseMessage response = await client.SendAsync(req);
                string responseAsString = await response.Content.ReadAsStringAsync();
                var response1 = Request.CreateResponse(HttpStatusCode.Moved);
                response1.Headers.Location = new Uri(response.RequestMessage.RequestUri.AbsoluteUri);
                return response1;
            }
            catch (Exception ex)
            {
                return null;
            }

        }

        #endregion

        //GetCCNoteInsertRetryRecords
        #region SF CC Inserting RetryRecords posted from SF to CC for Notes

        [HttpPost, HttpGet]
        [Route("api/CCSFRetry/GetCCNotesInsertRetryRecords")]
        public async Task<IHttpActionResult> getInsertNoteRetryRecords()
        {


            string Temporaryvar = "";
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
                string Error = "";

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
                Dictionary<string, object> userdata = cloudCherryController.getClientsettingsByKey("apikey", apiKey, IntegrationType.salesforce);
                ccusername = userdata["username"].ToString();

                Dictionary<string, object> NewExceptiondata = new Dictionary<string, object>();
                NewExceptiondata.Add("_Id", ExceptionType.Create + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + DateTime.Now.Hour + DateTime.Now.Minute + DateTime.Now.Second + DateTime.Now.Millisecond);

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
                sentry.LogTheFailedRecord(ex.Message, "Unknown Exception", "", ExceptionType.Update, "", ccusername, IntegrationType.salesforce);
                return BadRequest();
            }
        }

        #endregion

        //Retry manually from settings screen
        #region RetryManually

        //Insert the records manually fired from the SCREEN at SF END
        [HttpPost]
        [Route("api/CCSFRetry/retryManuallyFailedRecordsSFInsert")]
        public async Task<HttpResponseMessage> retryManuallyFailedRecordsSFInsert(string userName, string exceptionType, string startDate, string endDate)
        {
            HttpResponseMessage msg = new HttpResponseMessage();
            try
            {
                //deserialising the data  posted to this method
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
                    msg.StatusCode = HttpStatusCode.BadRequest;
                    return msg;
                }

                List<Dictionary<string, object>> retryRecords = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(httpResponse); //Converting the response into dictionary collection 

                //Checks whether the reponse has been converted or not
                if (retryRecords == null || retryRecords.Count == 0)
                {


                    msg.StatusCode = HttpStatusCode.BadRequest;
                    return msg;


                }

                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                //Specifying the INSERT API at SF
                string serviceEndPoint = "/services/apexrest/CCService/insertCCTicket";
                Dictionary<string, object> exceptiondata = cloudCherryController.getClientsettingsByKey("username", userName, IntegrationType.salesforce);
                if (exceptiondata == null)
                {
                    msg.StatusCode = HttpStatusCode.BadRequest;
                    return msg;
                }
                string Clientid = exceptiondata["clientid"].ToString();
                string Secret = exceptiondata["secret"].ToString();
                string Rtoken = exceptiondata["rtoken"].ToString();
                string InstanceUrl = exceptiondata["instanceurl"].ToString();
                Connect2SFController connect2SF = new Connect2SFController();

                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                //Get the token details of the salesforce
                List<string> tokendetails = await connect2SF.ConnectSF(Clientid, Secret, Rtoken);
                if (tokendetails[0] == "invalid_grant")
                {
                    if (await sentry.LogTheFailedRecord("Refresh Token has expired.", "TokenExpired", "", ExceptionType.General, "", userName, IntegrationType.salesforce))
                    {

                        msg.StatusCode = HttpStatusCode.BadRequest;
                        return msg;

                    }
                }

                Dictionary<string, object> Integrationdata = await sentry.GetIntegrationData(userName, integrationType);
                msg.StatusCode = HttpStatusCode.OK;

                if (!Integrationdata.Keys.Contains(exceptionType))
                    return msg; //new List<Dictionary<string, object>>();
                List<Dictionary<string, object>> Exceptiondata = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(Integrationdata[exceptionType].ToString());

                Dictionary<string, object> doc = cloudCherryController.GetUserCredentials(userName, IntegrationType.salesforce);
                string CCApikey = doc["apikey"].ToString();

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
                            IHttpActionResult code = await connect2sf.getDataFromAPI(notificationData);
                            if (code.ToString() == "System.Web.Http.Results.OkResult")
                                isexist = true;
                            else
                            {
                                isexist = true;
                                record["ExceptionRaisedOn"] = DateTime.Now;
                                Exceptiondatacopy.Add(record);
                            }

                        }
                    }
                    //collecting the non selected records
                    if (!isexist)
                        Exceptiondatacopy.Add(record);
                }
                //updating "InsertCasesRetry" category with non selected records so the records sent to sf  doesnot exist in "InsertCasesRetry" category

                await sentry.AddCompleteException(userName, integrationType, (ExceptionType)Enum.Parse(typeof(ExceptionType), exceptionType, true), Exceptiondatacopy);


                msg.StatusCode = HttpStatusCode.OK;
                return msg;//Returning the updated list of records
            }
            catch (InvalidOperationException ioe) { return msg; }

            catch (Exception ex)
            {
                return msg;
            }
        }


        //Insert the records manually fired from the SCREEN at CC END
        [HttpPost]
        [Route("api/CCSFRetry/ManualTriggerNotesInserts")]
        public async Task<HttpResponseMessage> retryManuallyFailedRecordsCCInsert(string userName, string exceptionType, string startDate, string endDate)
        {
            HttpResponseMessage responsemessage = new HttpResponseMessage();
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
                    responsemessage.StatusCode = HttpStatusCode.BadRequest;
                    return responsemessage;
                }

                List<Dictionary<string, object>> retryRecords = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(httpResponse); //Converting the response into dictionary collection 

                //Checks whether the reponse has been converted or not
                if (retryRecords == null)
                {

                    responsemessage.StatusCode = HttpStatusCode.BadRequest;
                    return responsemessage;
                }
                if (retryRecords.Count == 0)
                {
                    responsemessage.StatusCode = HttpStatusCode.BadRequest;
                    return responsemessage;
                }

                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                //Specifying the INSERT API at SF
                string serviceEndPoint = "https://api.getcloudcherry.com/api/Answers/Note/";



                Dictionary<string, object> exceptiondata = cloudCherryController.getClientsettingsByKey("username", userName, IntegrationType.salesforce);
                if (exceptiondata == null)
                    return null;
                string Clientid = exceptiondata["clientid"].ToString();
                string Secret = exceptiondata["secret"].ToString();
                string Rtoken = exceptiondata["rtoken"].ToString();
                string apikey = exceptiondata["apikey"].ToString();
                Connect2SFController connect2SF = new Connect2SFController();



                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                //Get the token details of the salesforce
                List<string> tokendetails = await connect2SF.ConnectSF(Clientid, Secret, Rtoken);
                if (tokendetails[0] == "invalid_grant")
                {
                    if (await sentry.LogTheFailedRecord("Refresh Token has expired.", "TokenExpired", "", ExceptionType.General, "", userName, IntegrationType.salesforce))
                        return null;
                }

                Dictionary<string, object> Integrationdata = await sentry.GetIntegrationData(userName, integrationType);

                if (!Integrationdata.Keys.Contains(exceptionType))
                    return null; ;

                List<Dictionary<string, object>> Exceptiondata = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(Integrationdata[exceptionType].ToString());


                MessageNotes notes = new MessageNotes();
                List<Dictionary<string, object>> Exceptiondatacopy = new List<Dictionary<string, object>>();
                foreach (var record in Exceptiondata)
                {
                    bool isexist = false;
                    foreach (Dictionary<string, object> retryRecord in retryRecords)
                    {
                        if (record["_Id"].ToString() == retryRecord["id"].ToString())
                        {
                            try
                            {
                                //sending the mathced record to CC

                                //Converting the value of the current key into dictionary
                                Dictionary<string, object> ticketDocument = JsonConvert.DeserializeObject<Dictionary<string, object>>(record["FailedRecord"].ToString());
                              
                                if (ticketDocument == null||ticketDocument.Count == 0)
                                {
                                    responsemessage.StatusCode = HttpStatusCode.BadRequest;
                                    return responsemessage;
                                }
                                string CCTicketId = "";

                                //Declaring the notes object
                                StringBuilder noteStructure = new StringBuilder();

                                //Loop through the key values in the dictionary of the failed record
                                foreach (var res in ticketDocument)
                                {
                                    StringBuilder propertiesNote = new StringBuilder();
                                    if (res.Key == "CCTicket__c")
                                    {
                                        CCTicketId = Convert.ToString(res.Value);
                                    }

                                    else
                                    {
                                        if ((!(res.Key == "attributes")) && (!(res.Key == "apiKey")))
                                        {
                                            propertiesNote.Append(res.Key + ":" + res.Value + "      ");
                                        }
                                    }
                                    noteStructure.Append(propertiesNote);
                                }
                                notes.noteTime = DateTime.Now.ToString();
                                notes.note = noteStructure.ToString();
                                var serializeNote = JsonConvert.SerializeObject(notes);
                                serviceEndPoint = serviceEndPoint + CCTicketId; //Appendin the ticket/answer id with the service
                                HttpRequestMessage reqInsertNote = new HttpRequestMessage(HttpMethod.Post, new Uri(serviceEndPoint));
                                reqInsertNote.Content = new StringContent(serializeNote, Encoding.UTF8, "application/json");
                                string ccAccessToken = await cloudCherryController.getCCAccessToken(userName, apikey);
                                reqInsertNote.Headers.Add("Authorization", "Bearer " + ccAccessToken);
                                HttpResponseMessage insertCaseResponse = await client.SendAsync(reqInsertNote); //Sending request to CC in order to insert NOTE                          

                                string responseCase = await insertCaseResponse.Content.ReadAsStringAsync();
                                if (insertCaseResponse.StatusCode != HttpStatusCode.OK)
                                {
                                    // var isDeleted = failedCollection.DeleteOne(record); //Deleting the record from DB.

                                    Exceptiondatacopy.Add(sentry.RenewthisRecord(record["FailedRecord"].ToString() + "      " + responseCase, DateTime.Now.ToString(), "SFCC", record["FailedRecord"].ToString(), ExceptionType.Update, CCTicketId, userName));
                                    isexist = true;
                                }
                                if (insertCaseResponse.StatusCode == HttpStatusCode.OK) //Successful insertion of NOTE at CC happens
                                {
                                    isexist = true;
                                }
                            }
                            catch { }

                        }
                    }
                    //collecting the records  the records  that not sent to CC
                    if (!isexist)
                        Exceptiondatacopy.Add(record);
                }
                //Upatin the "InsertNotesRetry" category with non selected records so the records that are sent to  CC are not in "InsertNotesRetry" category
                await sentry.AddCompleteException(userName, integrationType, (ExceptionType)Enum.Parse(typeof(ExceptionType), exceptionType, true), Exceptiondatacopy);
                Task.Delay(3000);

                responsemessage.StatusCode = HttpStatusCode.OK;
                return responsemessage;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        #endregion

        //Retry from scheduler
        #region SchedularServiceToRetry

        //Retries the failed records while inserting a CASE in SF and Deletes from the LOG Table after successful inserion at SF as a CASE.
        [HttpGet]
        [Route("api/CCSFRetry/AutoRetrySFFailedRecords")]
        public async Task retryFailedRecordsSFInsert()
        {
            List<string> Usernamelist = new List<string>();
            Dictionary<string, object> repeatedUsernamelist = new Dictionary<string, object>();
            List<Dictionary<string, object>> Alluserdata = await sentry.GetAllUserData(integrationType);
            bool Isusersrepeated = false;
            foreach (Dictionary<string, object> singeluserdata in Alluserdata)
            {
                Dictionary<string, object> metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(singeluserdata["metadata"].ToString());

                string responseBody = metadata["value"].ToString();
                //{"sanlexicion":{"Salesforce":{"Resp


                int firstindex = responseBody.IndexOf(":"); ;
                responseBody = responseBody.Substring(0, firstindex);
                responseBody = responseBody.Replace("{", "");
                responseBody = responseBody.Replace("\"", "");

                string username = "|" + responseBody + "|";
                if (Usernamelist.Contains(username))
                {
                    Isusersrepeated = true;
                    List<string> Issuelist = (List<string>)repeatedUsernamelist[responseBody];
                    Issuelist.Add(singeluserdata["id"].ToString());
                    repeatedUsernamelist.Remove(responseBody);
                    repeatedUsernamelist.Add(responseBody, Issuelist);
                }
                else
                {

                    List<string> Issuelist = new List<string>();
                    Issuelist.Add(singeluserdata["id"].ToString());
                    repeatedUsernamelist.Add(responseBody, Issuelist);
                    Usernamelist.Add(username);
                }

                await retryFailedRecordsSFInsert(responseBody, integrationType);
                Task.Delay(3000);
                await retryFailedRecordsCCInsert(responseBody, integrationType.ToString());
                Task.Delay(3000);
                await getAPILimitInsertNoteRetryRecords(responseBody);
                Task.Delay(3000);
            }
            if (Isusersrepeated)
            {
                await MergeUserissues(repeatedUsernamelist);
            }
        }

        //Verifying if a username is repeated in sentry, if found multiple times then the data is  merged.
        [HttpGet]
        [Route("api/CCSFRetry/FindRepeatedusers")]
        public async Task FindRepeatedusers()
        {
            Task.Delay(3000);
         //   Thread.Sleep(3000);
            List<string> Usernamelist = new List<string>();
            Dictionary<string, object> repeatedUsernamelist = new Dictionary<string, object>();
            List<Dictionary<string, object>> Alluserdata = await sentry.GetAllUserData(integrationType);
            bool Isusersrepeated = false;
            foreach (Dictionary<string, object> singeluserdata in Alluserdata)
            {
                Dictionary<string, object> metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(singeluserdata["metadata"].ToString());

                string responseBody = metadata["value"].ToString();
                //{"sanlexicion":{"Salesforce":{"Resp


                int firstindex = responseBody.IndexOf(":"); ;
                responseBody = responseBody.Substring(0, firstindex);
                responseBody = responseBody.Replace("{", "");
                responseBody = responseBody.Replace("\"", "");

                string username = "|" + responseBody + "|";
                if (Usernamelist.Contains(username))
                {
                    Isusersrepeated = true;
                    List<string> Issuelist = (List<string>)repeatedUsernamelist[responseBody];
                    Issuelist.Add(singeluserdata["id"].ToString());
                    repeatedUsernamelist.Remove(responseBody);
                    repeatedUsernamelist.Add(responseBody, Issuelist);
                }
                else
                {

                    List<string> Issuelist = new List<string>();
                    Issuelist.Add(singeluserdata["id"].ToString());
                    repeatedUsernamelist.Add(responseBody, Issuelist);
                    Usernamelist.Add(username);
                }

                // await retryFailedRecordsSFInsert(responseBody, "Salesforce");
                Task.Delay(3000);
                // await retryFailedRecordsCCInsert(responseBody, "Salesforce");
                Task.Delay(3000);
            }
            if (Isusersrepeated)
            {
                await MergeUserissues(repeatedUsernamelist);
            }
        }

        //Merging the user issues when a user has more then one issue.
        public async Task MergeUserissues(Dictionary<string, object> repeatedUsernamelist)
        {
            foreach (KeyValuePair<string, object> Kpusername in repeatedUsernamelist)
            {
                List<Dictionary<string, object>> FinalExistingExceptionData = new List<Dictionary<string, object>>();
                Dictionary<string, object> FinalCompleteExceptionData = new Dictionary<string, object>();
                Dictionary<string, object> FinalIntegrationdata = new Dictionary<string, object>();
                List<string> Issuelist = (List<string>)Kpusername.Value;
                Dictionary<string, object> Finalsingleuserdata = new Dictionary<string, object>();
                foreach (string issueid in Issuelist)
                {
                    Dictionary<string, object> singleuserdata = await sentry.RetreiveanIssue(issueid);
                    Task.Delay(3000);
                    await sentry.Updateissue(issueid);
                    if (!singleuserdata.Keys.Contains(Kpusername.Key))
                        continue;
                    string Encryptdata = EncrytionService.Decrypt(singleuserdata[Kpusername.Key].ToString(), true);
                    Dictionary<string, object> Integrationdata = JsonConvert.DeserializeObject<Dictionary<string, object>>(Encryptdata);
                    if (Finalsingleuserdata.Keys.Contains(Kpusername.Key))
                    {
                        Encryptdata = EncrytionService.Decrypt(Finalsingleuserdata[Kpusername.Key].ToString(), true);
                        FinalIntegrationdata = JsonConvert.DeserializeObject<Dictionary<string, object>>(Encryptdata);
                        Finalsingleuserdata.Remove(Kpusername.Key);
                    }

                    foreach (KeyValuePair<string, object> KPintegraion in Integrationdata)
                    {
                        Dictionary<string, object> CompleteExceptionData = JsonConvert.DeserializeObject<Dictionary<string, object>>(Integrationdata[KPintegraion.Key].ToString());
                        if (FinalIntegrationdata.Keys.Contains(KPintegraion.Key))
                        {
                            FinalCompleteExceptionData = JsonConvert.DeserializeObject<Dictionary<string, object>>(FinalIntegrationdata[KPintegraion.Key].ToString());
                            FinalIntegrationdata.Remove(KPintegraion.Key);
                        }

                        foreach (KeyValuePair<string, object> KPexception in CompleteExceptionData)
                        {
                            FinalExistingExceptionData = new List<Dictionary<string, object>>();
                            List<Dictionary<string, object>> ExistingExceptionData = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(CompleteExceptionData[KPexception.Key].ToString());
                            if (FinalCompleteExceptionData.Keys.Contains(KPexception.Key))
                            {
                                FinalExistingExceptionData = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(FinalCompleteExceptionData[KPexception.Key].ToString());
                                FinalCompleteExceptionData.Remove(KPexception.Key);
                            }

                            foreach (var singlExceptiondata in ExistingExceptionData)
                            {
                                FinalExistingExceptionData.Add(singlExceptiondata);
                            }

                            FinalCompleteExceptionData.Add(KPexception.Key, FinalExistingExceptionData);


                        }
                        FinalIntegrationdata.Add(KPintegraion.Key, FinalCompleteExceptionData);

                    }

                    string Encrypteddata = EncrytionService.Encrypt(JsonConvert.SerializeObject(FinalIntegrationdata), true);
                    Finalsingleuserdata.Add(Kpusername.Key, Encrypteddata);

                    Task.Delay(3000);
                }

                await sentry.InsertAnIssue(Finalsingleuserdata, integrationType);
                Task.Delay(3000);

            }//  foreach (KeyValuePair<string, object> KPusername

        }

        //Retying to send records to  SF, done by scheduler
        public async Task<IHttpActionResult> retryFailedRecordsSFInsert(string userName, IntegrationType Integration)
        {
            try
            {
                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                string serviceEndPoint = "/services/apexrest/CCService/insertCCTicket";
                Dictionary<string, object> exceptiondata = cloudCherryController.getClientsettingsByKey("username", userName, IntegrationType.salesforce);
                if (exceptiondata == null)
                    return null;
                string Clientid = exceptiondata["clientid"].ToString();
                string Secret = exceptiondata["secret"].ToString();
                string Rtoken = exceptiondata["rtoken"].ToString();
                string apikey = exceptiondata["apikey"].ToString();
                List<string> tokendetails = await connect2sf.ConnectSF(Clientid, Secret, Rtoken);

                Dictionary<string, object> Integrationdata = await sentry.GetIntegrationData(userName, Integration);

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
                            string ansID = record["AnswerId"].ToString();
                            string answerURL = "https://api.getcloudcherry.com/api/Answer/";
                            //Get AnswerByID from CC
                            HttpRequestMessage reqAnswerFromCC = new HttpRequestMessage(HttpMethod.Get, new Uri(answerURL + ansID));
                            string ccAccessToken = await cloudCherryController.getCCAccessToken(userName, apikey);
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
                            IHttpActionResult code = await connect2sf.getDataFromAPI(notificationData);
                            if (code.ToString() == "System.Web.Http.Results.OkResult")
                                isexist = true;
                            else
                            {
                                isexist = true;
                                record["ExceptionRaisedOn"] = DateTime.Now;
                                Exceptiondatacopy.Add(record);
                            }
                        }

                        if (!isexist)
                            Exceptiondatacopy.Add(record);
                    }

                    await sentry.AddCompleteException(userName, integrationType, ExceptionType.Create, Exceptiondatacopy);
                    Task.Delay(3000);
                }
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest();
            }
        }

        //Retries the failed records while inserting a CASE in SF and Deletes from the LOG Table after successful inserion at SF as a CASE.
        [HttpGet]
        [Route("api/CCSFRetry/AutoRetryCCFailedRecords")]
        public async Task<IHttpActionResult> retryFailedRecordsCCInsert(string userName, string Integration)
        {
            try
            {
                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                MessageNotes notes = new MessageNotes();
                string serviceEndPoint = "https://api.getcloudcherry.com/api/Answers/Note/";

                //Connectiong to CC-Replica Database

                Dictionary<string, object> exceptiondata = cloudCherryController.getClientsettingsByKey("username", userName, IntegrationType.salesforce);
                if (exceptiondata == null)
                    return null;
                string Clientid = exceptiondata["clientid"].ToString();
                string Secret = exceptiondata["secret"].ToString();
                string Rtoken = exceptiondata["rtoken"].ToString();
                string apikey = exceptiondata["apikey"].ToString();
                Dictionary<string, object> Integrationdata = await sentry.GetIntegrationData(userName, (IntegrationType)Enum.Parse(typeof(IntegrationType), Integration, true));

                List<Dictionary<string, object>> Exceptiondata = new List<Dictionary<string, object>>();

                Connect2SFController connect = new Connect2SFController();
                string ccAccessToken = await connect.getCCAccessToken(userName, apikey);

                foreach (KeyValuePair<string, object> kpdata in Integrationdata)
                {
                    string[] exceptiontypes = new string[] { ExceptionType.Update.ToString() };
                    if (!exceptiontypes.Contains(kpdata.Key))
                        continue;
                    Exceptiondata = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(Integrationdata[kpdata.Key].ToString());
                    List<Dictionary<string, object>> Exceptiondatacopy = new List<Dictionary<string, object>>();
                    foreach (var record in Exceptiondata)
                    {
                        bool isexist = false;

                        if (record.Keys.Contains("FailedRecord"))
                        {
                            Dictionary<string, object> ticketDocument = JsonConvert.DeserializeObject<Dictionary<string, object>>(record["FailedRecord"].ToString());
                            StringBuilder propertiesNote = new StringBuilder();
                            string CCTicketId = "";
                            StringBuilder noteStructure = new StringBuilder();
                            foreach (var res in ticketDocument)
                            {
                                if (res.Key == "CCTicket__c")
                                {
                                    CCTicketId = Convert.ToString(res.Value);
                                }

                                else
                                {
                                    if ((!(res.Key == "attributes")) && (!(res.Key == "apiKey")))
                                    {
                                        propertiesNote.Append(res.Key + ":" + res.Value + "      ");
                                    }
                                }
                                noteStructure.Append(propertiesNote);
                            }
                            notes.noteTime = DateTime.Now.ToString();
                            notes.note = noteStructure.ToString();
                            var serializeNote = JsonConvert.SerializeObject(notes);
                            //  string ticketId = ticketDocument.Where(x => x.Key == "CCTicket__c").Select(x => x.Value.ToString()).FirstOrDefault();
                            serviceEndPoint = serviceEndPoint + CCTicketId; ;
                            HttpRequestMessage reqInsertNote = new HttpRequestMessage(HttpMethod.Post, new Uri(serviceEndPoint));
                            reqInsertNote.Headers.Add("Authorization", "Bearer " + ccAccessToken);
                            reqInsertNote.Content = new StringContent(serializeNote, Encoding.UTF8, "application/json");
                            HttpResponseMessage insertCaseResponse = await client.SendAsync(reqInsertNote);
                            string responseCase = await insertCaseResponse.Content.ReadAsStringAsync();
                            if (insertCaseResponse.StatusCode != HttpStatusCode.OK)
                            {
                                // var isDeleted = failedCollection.DeleteOne(record); //Deleting the record from DB.

                                Exceptiondatacopy.Add(sentry.RenewthisRecord(record["FailedRecord"].ToString() + "      " + responseCase, DateTime.Now.ToString(), "SFCC", record["FailedRecord"].ToString(), ExceptionType.Update, "", userName));
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

        #region Get Failed Note Insert Records from SF
        [HttpGet]
        [Route("api/CCSFRetry/GetCCNotesInsertRetryRecordsAPILimit")]
        public async Task<IHttpActionResult> getAPILimitInsertNoteRetryRecords(string CCUserName)
        {
            try
            {
                string Clientid = "";
                string RefreshToken = "";
                string Secret = "";
                string apiKey = "";
                string InstanceUrl = "";

                Dictionary<string, object> SFDetails = cloudCherryController.getClientsettingsByKey("username", CCUserName, IntegrationType.salesforce);

                if (SFDetails == null)
                {
                    return BadRequest();
                }

                Clientid = SFDetails["clientid"].ToString();
                Secret = SFDetails["secret"].ToString();
                RefreshToken = SFDetails["rtoken"].ToString();
                InstanceUrl = SFDetails["instanceurl"].ToString();
                apiKey = SFDetails["apikey"].ToString();


                List<string> tokendetails = await connect2sf.ConnectSF(Clientid, Secret, RefreshToken);

                if (tokendetails == null)
                    return BadRequest();

                string updateFailRecordsQuery = "/services/data/v20.0/query/?q=SELECT FailedRecord__c,ExceptionDescription__c,ExceptionRaisedAt__c,ExceptionRaisedOn__c,ExceptionType__c,apiKey__c  FROM CC_Update_Fail_Log__c";

                HttpClient httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                HttpRequestMessage requestUpdateFailedRecords = new HttpRequestMessage(HttpMethod.Get, new Uri(InstanceUrl + updateFailRecordsQuery));
                requestUpdateFailedRecords.Headers.Add("Authorization", "Bearer " + tokendetails[0]);

                HttpResponseMessage responseUpdateFailedRecords = new HttpResponseMessage();
                responseUpdateFailedRecords = await httpClient.SendAsync(requestUpdateFailedRecords);

                string respUpdateFailedRecords = await responseUpdateFailedRecords.Content.ReadAsStringAsync();

                Dictionary<string, object> qryResponse = new Dictionary<string, object>();
                qryResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(respUpdateFailedRecords);

                if (!qryResponse.Keys.Contains("records"))
                    return BadRequest();

                List<Dictionary<string, object>> recordsUpdatFailed = new List<Dictionary<string, object>>();
                recordsUpdatFailed = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(qryResponse["records"].ToString());
                if (recordsUpdatFailed != null)
                {
                    foreach (Dictionary<string, object> record in recordsUpdatFailed)
                    {
                        if (await sentry.LogTheFailedRecord("Unknown Exception raised at CC Into API while requesting CC API To add NOTE", "Unknown Exception", record["FailedRecord__c"].ToString(), ExceptionType.Update, "", CCUserName, IntegrationType.salesforce))
                        {

                            Dictionary<string, object> recordId = new Dictionary<string, object>();
                            recordId = JsonConvert.DeserializeObject<Dictionary<string, object>>(record["attributes"].ToString());

                            string recordURL = recordId["url"].ToString();
                            HttpRequestMessage requestDeleteRecord = new HttpRequestMessage(HttpMethod.Delete, new Uri(InstanceUrl + recordURL));
                            requestDeleteRecord.Headers.Add("Authorization", "Bearer " + tokendetails[0]);
                            HttpResponseMessage responseDeleteRecord = new HttpResponseMessage();
                            responseDeleteRecord = await httpClient.SendAsync(requestDeleteRecord);
                            string respDeleteRecord = await responseDeleteRecord.Content.ReadAsStringAsync();
                        }

                    }
                }
                return Ok();
            }
            catch (Exception ex)
            {
                return Ok();
            }
        }

         #endregion
     
    }
}
