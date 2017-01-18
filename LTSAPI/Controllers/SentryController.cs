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
using SharpRaven;

namespace LTSAPI.Controllers
{
    /**
    * SentryController is a webapi controller which is used to log exceptions generically irrespective of integration type. This class has following methods
     
    * InsertAnIssue
    * GetIntegrationData
    * GetGivenExceptionData
    * RetreiveanIssue
    * GetSingleUser
    * GetSingleUserIssue
    * GetAllUserData
    * RenewthisRecord
    * AddNewExceptionData
    * AddCompleteException
    * MoveRecord
    * ArchiveExceptions
    * Updateissue
    * LogTheFailedRecord
    * LogTheSuccessRecord
    * LogThePartialSuccessRecord
    * RetreiveProjects
    * CreateProject
    * RetreiveTeams
    * CreateTeam
    * RetreiveOrganizations
    * CreateOrganization
    * RetreiveClientKey
    * IsObjectExist
    * CreateObject
    **/

    //ExceptionType = Generic enumeration keys for Exceptions (Create, Update, Archive, FieldLevel, General) for handling different types of integrations
    public enum ExceptionType { Create, Update, Archive, FieldLevel, General };

    //IntegrationType = Generic enumeration keys for Integration Type (salesforce, freshdesk, zendesk) for handling different types of integrations
    public enum IntegrationType { salesforce, freshdesk, zendesk };

    public class SentryController : ApiController
    {

        string previousissueid = "";
        string dsnUrl = "";
        
        //Following variables are used for authentication and these needs to be configured in web.config file
        string sentrybyte = ConfigurationManager.AppSettings["SentryByte"]; 
        string organization = ConfigurationManager.AppSettings["Organization"]; 
        string teamname = ConfigurationManager.AppSettings["Team"];
        string existCheck = ConfigurationManager.AppSettings["ExistCheck"];
        public ExceptionType ExceptionType { get; set; }
        public IntegrationType IntegrationType { get; set; }


        /**
        * 
        * Api GetGivenExceptionData/ is used to retrieve all the exceptions that or logged in to sentry for a
        * particular (user+integrationdata). These exceptions are filterd by startdat, enddate that are paseed as parameters to this method
        * 
        **/

        [HttpGet]
        [Route("api/Sentry/GetGivenExceptionData")]
        //Retreives  all the records from the given exception type and given user and integration type
        public async Task<List<Dictionary<string, object>>> GetGivenExceptionData(string Username, string integrationType, string Exceptiontype, string startDate, string endDate)
        {
            try
            {
                DateTime dt = Convert.ToDateTime(startDate);
                DateTime dt1 = Convert.ToDateTime(endDate);

                //Retreives the all the exception data from given username and integration type
                Dictionary<string, object> CompleteIntegrationdata = await GetIntegrationData(Username, (IntegrationType)Enum.Parse(typeof(IntegrationType), integrationType, true));


                if (CompleteIntegrationdata.Keys.Contains(Exceptiontype))
                {
                    // Filtering the  all the exceptions list and retreiving  the given exception type category records
                    List<Dictionary<string, object>> RecordList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(CompleteIntegrationdata[Exceptiontype].ToString());

                    List<Dictionary<string, object>> FilteredList = new List<Dictionary<string, object>>();

                    foreach (Dictionary<string, object> singlerecord in RecordList)
                    {
                        DateTime Recorddatetime = Convert.ToDateTime(singlerecord["ExceptionRaisedOn"].ToString());

                        if (Convert.ToDateTime(Recorddatetime) >= Convert.ToDateTime(startDate) && Convert.ToDateTime(Recorddatetime) <= Convert.ToDateTime(endDate).AddDays(1))
                            FilteredList.Add(singlerecord);
                    }

                    return FilteredList;

                }

            }
            catch { }
            return new List<Dictionary<string, object>>();
        }

        /**
        * 
        * Api RetreiveanIssue/ is used to retrieve single exceptions that or logged in to sentry
        * 
        **/

        [HttpGet]
        [Route("api/Sentry/RetreiveanIssue")]
        public async Task<Dictionary<string, object>> RetreiveanIssue(string Issueid)
        {
            try
            {
                string path = "https://sentry.io/api/0/issues/" + Issueid + @"/events/";
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, path);
                req.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(sentrybyte)));
                HttpResponseMessage res = new HttpResponseMessage();
                HttpClient client = new HttpClient();
                res = await client.SendAsync(req);
                string resMsg = await res.Content.ReadAsStringAsync();
                List<Dictionary<string, object>> Integrationdata = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(resMsg);
                string responseBody = Integrationdata[0]["message"].ToString();
                Dictionary<string, object> metadatavalue = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);
                return metadatavalue;
            }
            catch { }
            return new Dictionary<string, object>();
        }

        /**
        * 
        * Api RetreiveanIssue/ is used to retrieve single exception that or logged in to sentry which is filterd  by single issueid
        * 
        **/

        [HttpGet]
        [Route("api/Sentry/GetSingleUserdata")]
        public async Task<Dictionary<string, object>> GetSingleUser(string Username, string Project)
        {
            try
            {

                Dictionary<string, object> SingleUserIssue = await GetSingleUserIssue(Username, (IntegrationType)Enum.Parse(typeof(IntegrationType), Project, true));
                string Issueid = SingleUserIssue["id"].ToString();
                Dictionary<string, object> SingleIssue = await RetreiveanIssue(Issueid);
                Dictionary<string, object> UserdataWithIssue = new Dictionary<string, object>();
                UserdataWithIssue.Add("issueid", Issueid);
                UserdataWithIssue.Add("singleuserdata", SingleIssue);
                return UserdataWithIssue;

            }
            catch { }
            return null;

        }

        /**
        * 
        * Api ArchiveExceptions/ is used to categorize the exceptions in to different types this method makes an exception to be archive.
        * 
        **/
        [HttpPost]
        [Route("api/Sentry/ArchiveExceptions")]
        public async Task<HttpResponseMessage> ArchiveExceptions(string userName, string sourceExceptionType, string DestinationExceptionType, string integrationType)
        {
            HttpResponseMessage msg = new HttpResponseMessage();

            try
            {//reteiving the list of ids to archive from token exception list

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

                List<Dictionary<string, object>> archiveSFRecords = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(httpResponse); //Converting the response into dictionary collection 
                //Checks whether the reponse has been converted or not
                if (archiveSFRecords == null)
                {
                    if (archiveSFRecords.Count == 0)
                    {
                        msg.StatusCode = HttpStatusCode.OK;                        
                        return msg;
                    }
                    return null;
                }
                await MoveRecord((ExceptionType)Enum.Parse(typeof(ExceptionType), sourceExceptionType, true), (ExceptionType)Enum.Parse(typeof(ExceptionType), DestinationExceptionType, true), userName, (IntegrationType)Enum.Parse(typeof(IntegrationType), integrationType, true), archiveSFRecords);
                msg.StatusCode = HttpStatusCode.OK;                
                return msg;
           }
            catch (InvalidOperationException ed)
            { 
                msg.StatusCode = HttpStatusCode.OK;                
                return msg;
            }
            catch (Exception ex)
            {
                msg.StatusCode = HttpStatusCode.OK;                
                return msg;
            }
        }

        /**
        * 
        * Api Updateissue/ is used to updated issue at sentry whichcalls sentry api
        * 
        **/
        [HttpGet]
        [Route("api/Sentry/Updateissue")]
        //updates record of the given issueid to resolved
        public async Task Updateissue(string Issueid)
        {
            try
            {
                Dictionary<string, string> status = new Dictionary<string, string>();
                status.Add("status", "resolved");

                string ccURLforAddNote = "https://sentry.io/api/0/issues/" + Issueid + "/";
                HttpRequestMessage addNoteRequest = new HttpRequestMessage(HttpMethod.Put, new Uri(ccURLforAddNote));
                var serializeNote = JsonConvert.SerializeObject(status);

                addNoteRequest.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(sentrybyte)));
                addNoteRequest.Content = new StringContent(serializeNote, Encoding.UTF8, "application/json");
                HttpClient client = new HttpClient();
                HttpResponseMessage addNoteResponse = await client.SendAsync(addNoteRequest);
                string addNoteRespContent = await addNoteResponse.Content.ReadAsStringAsync();
            }
            catch { }
        }

        /**
        * 
        * Api RetreiveProjects/ is used to get different types of projects that are there in sentry
        * 
        **/
        [HttpGet]
        [Route("api/Sentry/RetreiveProjects")]
        public async Task<List<Dictionary<string, object>>> RetreiveProjects()
        {
            try
            {
                string path = "https://sentry.io/api/0/organizations/" + organization + "/projects/";
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, path);
                req.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(sentrybyte)));
                HttpResponseMessage res = new HttpResponseMessage();
                HttpClient client = new HttpClient();
                res = await client.SendAsync(req);
                string resMsg = await res.Content.ReadAsStringAsync();
                List<Dictionary<string, object>> Integrationdata = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(resMsg);
                return Integrationdata;
            }
            catch { }
            return new List<Dictionary<string, object>>();
        }

        /**
        * 
        * Api CreateProject/ is used to create a project at sentry by checking if the projectis there or not for particular interation type.
        * If a project does not exist with particular integrationtype that will be created using this api.
        * 
        **/
        [HttpGet]
        [Route("api/Sentry/CreateProject")]      
        public async Task CreateProject(string Projectname)
        {
            try
            {
                Dictionary<string, string> status = new Dictionary<string, string>();
                status.Add("name", Projectname);
                status.Add("slug", Projectname.ToLower());
                string ccURLforAddNote = "https://sentry.io/api/0/teams/" + organization + "/" + teamname + "/projects/";
                HttpRequestMessage addNoteRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(ccURLforAddNote));
                var serializeNote = JsonConvert.SerializeObject(status);
                addNoteRequest.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(sentrybyte)));
                addNoteRequest.Content = new StringContent(serializeNote, Encoding.UTF8, "application/json");
                HttpClient client = new HttpClient();
                HttpResponseMessage addNoteResponse = await client.SendAsync(addNoteRequest);
                string addNoteRespContent = await addNoteResponse.Content.ReadAsStringAsync();
            }
            catch { }
        }

        /**
        * 
        * Api RetreiveTeams/ retrieving all the teams that are there in sentry.         * 
        * 
        **/
        [HttpGet]
        [Route("api/Sentry/RetreiveTeams")]
        public async Task<List<Dictionary<string, object>>> RetreiveTeams()
        {
            try
            {
                string path = "https://sentry.io/api/0/organizations/" + organization + "/teams/";
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, path);
                req.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(sentrybyte)));
                HttpResponseMessage res = new HttpResponseMessage();
                HttpClient client = new HttpClient();
                res = await client.SendAsync(req);
                string resMsg = await res.Content.ReadAsStringAsync();
                List<Dictionary<string, object>> Integrationdata = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(resMsg);
                return Integrationdata;
            }
            catch { }
            return new List<Dictionary<string, object>>();
        }

        /**
        * 
        * Api CreateTeam/ creating team/project at sentry using sentry api
        * 
        **/
        [HttpGet]
        [Route("api/Sentry/CreateTeam")]       
        public async Task CreateTeam()
        {
            try
            {
                Dictionary<string, string> status = new Dictionary<string, string>();
                status.Add("name", teamname);
                status.Add("slug", teamname.ToLower());
                string ccURLforAddNote = "https://sentry.io/api/0/organizations/" + organization + "/teams/";
                HttpRequestMessage addNoteRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(ccURLforAddNote));
                var serializeNote = JsonConvert.SerializeObject(status);
                addNoteRequest.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(sentrybyte)));
                addNoteRequest.Content = new StringContent(serializeNote, Encoding.UTF8, "application/json");
                HttpClient client = new HttpClient();
                HttpResponseMessage addNoteResponse = await client.SendAsync(addNoteRequest);
                string addNoteRespContent = await addNoteResponse.Content.ReadAsStringAsync();
            }
            catch { }
        }

        /**
        * 
        * Api RetreiveOrganizations/ creating Organizations at sentry using sentry api
        * 
        **/
        [HttpGet]
        [Route("api/Sentry/RetreiveOrganizations")]
        public async Task<List<Dictionary<string, object>>> RetreiveOrganizations()
        {
            try
            {
                string path = "https://sentry.io/api/0/organizations/";
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, path);
                req.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(sentrybyte)));
                HttpResponseMessage res = new HttpResponseMessage();
                HttpClient client = new HttpClient();
                res = await client.SendAsync(req);
                string resMsg = await res.Content.ReadAsStringAsync();
                List<Dictionary<string, object>> Integrationdata = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(resMsg);
                return Integrationdata;
            }
            catch { }
            return new List<Dictionary<string, object>>();
        }

        /**
        * 
        * Api CreateOrganization/ is used create organization at sentry using sentry api if it is not there
        * 
        **/
        [HttpGet]
        [Route("api/Sentry/CreateOrganization")]
        public async Task CreateOrganization()
        {
            try
            {
                Dictionary<string, string> status = new Dictionary<string, string>();
                status.Add("name", organization);
                status.Add("slug", organization.ToLower());
                string ccURLforAddNote = "https://sentry.io/api/0/organizations/";
                HttpRequestMessage addNoteRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(ccURLforAddNote));
                var serializeNote = JsonConvert.SerializeObject(status);
                addNoteRequest.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(sentrybyte)));
                addNoteRequest.Content = new StringContent(serializeNote, Encoding.UTF8, "application/json");
                HttpClient client = new HttpClient();
                HttpResponseMessage addNoteResponse = await client.SendAsync(addNoteRequest);
                string addNoteRespContent = await addNoteResponse.Content.ReadAsStringAsync();
            }
            catch { }
        }


        /**
        * InsertAnIssue inserts an exception in to sentry which acceapts whole exception as input, integrationType as input
        * IntegrationType is to used to segregate all the exceptions across different integration types by creating a new project in sentry
        * 
        * */
        public async Task InsertAnIssue(Dictionary<string, object> Singleuserdata, IntegrationType IntegrationType)
        {
            try
            {
                if (existCheck == "1")
                {
                    if (!await IsObjectExist("organization", organization))
                        await CreateObject("organization", organization);
                    if (!await IsObjectExist("team", teamname))
                        await CreateObject("team", teamname);
                }
                if (!await IsObjectExist("project", IntegrationType.ToString()))
                    await CreateObject("project", IntegrationType.ToString());
                dsnUrl = await RetreiveClientKey(IntegrationType.ToString());

                Exception exc = new Exception(JsonConvert.SerializeObject(Singleuserdata));
                var client = new RavenClient(dsnUrl);
                string s = client.CaptureException(exc);
            }
            catch { }
        }

        /**
        * GetIntegrationData is used for getting all the exceptions data that match with (user+integrationType)
        * This method uses GetSingleUser, GetSingleUserIssue, GetAllUserData
        * */
        public async Task<Dictionary<string, object>> GetIntegrationData(string Username, IntegrationType Integration)
        {
            try
            {
                Dictionary<string, object> Issuedata = await GetSingleUser(Username, Integration.ToString());
                Dictionary<string, object> singleuserdata = new Dictionary<string, object>();
                Dictionary<string, object> Integrationdata = new Dictionary<string, object>();
                Dictionary<string, object> CompleteExceptionData = new Dictionary<string, object>();

                if (Issuedata != null)
                {
                    singleuserdata = (Dictionary<string, object>)Issuedata["singleuserdata"];
                    Integrationdata = JsonConvert.DeserializeObject<Dictionary<string, object>>(EncrytionService.Decrypt(singleuserdata[Username].ToString(), true));
                    CompleteExceptionData = JsonConvert.DeserializeObject<Dictionary<string, object>>(Integrationdata[Integration.ToString()].ToString());
                }

                return CompleteExceptionData;
            }
            catch (Exception ex) { }
            return null;
        }

        public async Task<Dictionary<string, object>> GetSingleUserIssue(string Username, IntegrationType Project)
        {
            List<Dictionary<string, object>> AllUserdata = await GetAllUserData(Project);
            try
            {
                for (int usercount = 0; usercount < AllUserdata.Count; usercount++)
                {
                    Dictionary<string, object> metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(AllUserdata[usercount]["metadata"].ToString());
                    string responseBody = metadata["value"].ToString();
                    int firstindex = responseBody.IndexOf(":"); ;
                    responseBody = responseBody.Substring(0, firstindex);
                    responseBody = responseBody.Replace("{", "");
                    responseBody = responseBody.Replace("\"", "");

                    if (responseBody != Username)
                        continue;

                    return AllUserdata[usercount];

                }
            }
            catch { }
            return null;

        }

        public async Task<List<Dictionary<string, object>>> GetAllUserData(IntegrationType project)
        {
            try
            {
                string path = "https://sentry.io/api/0/projects/" + organization + "/" + project.ToString() + "/issues/?statsPeriod=";
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, path);
                req.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(sentrybyte)));
                HttpResponseMessage res = new HttpResponseMessage();
                HttpClient client = new HttpClient();

                res = await client.SendAsync(req);
                string resMsg = await res.Content.ReadAsStringAsync();
                List<Dictionary<string, object>> Integrationdata = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(resMsg);

                return Integrationdata;
            }
            catch { return null; }
        }


        /**
        * Api RenewthisRecord/ This method name is used to update the record with the new date time in order tostore it in to sentry
        * 
        **/

        public Dictionary<string, object> RenewthisRecord(string exceptionDescription, string createddate, string exceptionRaisedAt, string failedRecord, ExceptionType exceptionType, string AnswerId, string ccUserName)
        {
            try
            {
                Dictionary<string, object> NewExceptionData = new Dictionary<string, object>();
                NewExceptionData.Add("_Id", exceptionType + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + DateTime.Now.Hour + DateTime.Now.Minute + DateTime.Now.Second + DateTime.Now.Millisecond);
                NewExceptionData.Add("ExceptionDescription", exceptionDescription);
                NewExceptionData.Add("ExceptionType", exceptionType.ToString());
                NewExceptionData.Add("ExceptionRaisedOn", createddate);
                NewExceptionData.Add("ExceptionRaisedAt", exceptionRaisedAt);
                NewExceptionData.Add("FailedRecord", failedRecord);
                NewExceptionData.Add("AnswerId", AnswerId);
                NewExceptionData.Add("ccUserName", ccUserName);

                return NewExceptionData;
            }
            catch { }
            return new Dictionary<string, object>();
        }

        /**
        * Api AddNewExceptionData/ Adds the given record into given exception category of given integration type and given username
        * 
        **/
        public async Task<bool> AddNewExceptionData(string Username, IntegrationType Integrationtype, ExceptionType Exceptiontype, Dictionary<string, object> NewExceptionData)
        {
            Task.Delay(4000); //Delay has been used due to async calls

            try
            {
                string Issueid = "";
                Dictionary<string, object> singleuserdata = new Dictionary<string, object>();
                Dictionary<string, object> Integrationdata = new Dictionary<string, object>();
                Dictionary<string, object> CompleteExceptionData = new Dictionary<string, object>();
                //Retreiving complete user data from sentry
                Dictionary<string, object> Issuedata = await GetSingleUser(Username, Integrationtype.ToString());
                if (Issuedata != null)
                {
                    Issueid = Issuedata["issueid"].ToString();
                    if (previousissueid == Issueid)
                    {
                        Issuedata = await GetSingleUser(Username, Integrationtype.ToString());
                        Issueid = Issuedata["issueid"].ToString();
                    }
                    previousissueid = Issueid;
                    singleuserdata = (Dictionary<string, object>)Issuedata["singleuserdata"];

                    //Retreiving all the integrations data  for the given user
                    Integrationdata = JsonConvert.DeserializeObject<Dictionary<string, object>>(EncrytionService.Decrypt(singleuserdata[Username].ToString(), true));

                    //Retreiving all the  Exceptions data for given integration type
                    if (!Integrationdata.Keys.Contains(Integrationtype.ToString()))
                        return false;
                    CompleteExceptionData = JsonConvert.DeserializeObject<Dictionary<string, object>>(Integrationdata[Integrationtype.ToString()].ToString());
                }

                List<Dictionary<string, object>> ExistingExceptionData = new List<Dictionary<string, object>>();

                if (CompleteExceptionData.Keys.Contains(Exceptiontype.ToString()))
                {
                    ExistingExceptionData = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(CompleteExceptionData[Exceptiontype.ToString()].ToString());
                }
                ExistingExceptionData.Add(NewExceptionData);
                if (CompleteExceptionData.Keys.Contains(Exceptiontype.ToString()))
                {
                    CompleteExceptionData.Remove(Exceptiontype.ToString());
                }

                CompleteExceptionData.Add(Exceptiontype.ToString(), ExistingExceptionData);
                if (Integrationdata.Keys.Contains(Integrationtype.ToString()))
                    Integrationdata.Remove(Integrationtype.ToString());
                Integrationdata.Add(Integrationtype.ToString(), CompleteExceptionData);

                if (singleuserdata.Keys.Contains(Username))
                    singleuserdata.Remove(Username);

                string Encrypteddata = EncrytionService.Encrypt(JsonConvert.SerializeObject(Integrationdata), true);

                singleuserdata.Add(Username, Encrypteddata);

                await InsertAnIssue(singleuserdata, Integrationtype);
                return true;
            }
            catch { }
            return false;

        }

        /**
        * Api AddCompleteException/ Adds the given record into given exception category of given integration type and given username
        * 
        **/
        public async Task<bool> AddCompleteException(string Username, IntegrationType Integrationtype, ExceptionType Exceptiontype, List<Dictionary<string, object>> ExceptionData)
        {
            try
            {
                string Issueid = "";
                Dictionary<string, object> singleuserdata = new Dictionary<string, object>();
                Dictionary<string, object> Integrationdata = new Dictionary<string, object>();
                Dictionary<string, object> CompleteExceptionData = new Dictionary<string, object>();
                Dictionary<string, object> Issuedata = await GetSingleUser(Username, Integrationtype.ToString());
                if (Issuedata != null)
                {
                    Issueid = Issuedata["issueid"].ToString();
                    if (previousissueid == Issueid)
                    {
                        Issuedata = await GetSingleUser(Username, Integrationtype.ToString());
                        Issueid = Issuedata["issueid"].ToString();
                    }
                    previousissueid = Issueid;
                    singleuserdata = (Dictionary<string, object>)Issuedata["singleuserdata"];

                    Integrationdata = JsonConvert.DeserializeObject<Dictionary<string, object>>(EncrytionService.Decrypt(singleuserdata[Username].ToString(), true));

                    CompleteExceptionData = JsonConvert.DeserializeObject<Dictionary<string, object>>(Integrationdata[Integrationtype.ToString()].ToString());
                }

                if (CompleteExceptionData.Keys.Contains(Exceptiontype.ToString()))
                {
                    CompleteExceptionData.Remove(Exceptiontype.ToString());
                }

                CompleteExceptionData.Add(Exceptiontype.ToString(), ExceptionData);
                if (Integrationdata.Keys.Contains(Integrationtype.ToString()))
                    Integrationdata.Remove(Integrationtype.ToString());
                Integrationdata.Add(Integrationtype.ToString(), CompleteExceptionData);

                if (singleuserdata.Keys.Contains(Username))
                    singleuserdata.Remove(Username);

                string Encrypteddata = EncrytionService.Encrypt(JsonConvert.SerializeObject(Integrationdata), true);

                singleuserdata.Add(Username, Encrypteddata);

                await InsertAnIssue(singleuserdata, Integrationtype);
                return true;
            }
            catch { }
            return false;

        }


        /**
        * Moves record  from  one given category to another category  for the given user and integration type, Example record moving from General tab to archive tab
        * list of  selected ids retreived from  UI and respective records are retreived  from sentry and changing of records take place.
        * 
        **/
        public async Task<bool> MoveRecord(ExceptionType sourceExceptiontype, ExceptionType destinationExceptionType, string userName, IntegrationType integrationType, List<Dictionary<string, object>> Recordids)
        {
            try
            {
                //Retreiving  all the records from  sentry for the given username and given intergration type
                Dictionary<string, object> Integrationdata = await GetIntegrationData(userName, integrationType);
                if (!Integrationdata.Keys.Contains(sourceExceptiontype.ToString()))
                    return false;

                //All  the records from the  source exception type is retreived so selected records will be moved to destination folder
                List<Dictionary<string, object>> Exceptiondata = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(Integrationdata[sourceExceptiontype.ToString()].ToString());

                //variable that holds  the list of records that are not selected
                List<Dictionary<string, object>> unSelectedRecords = new List<Dictionary<string, object>>();
                foreach (var record in Exceptiondata)
                {
                    bool isexist = false;
                    foreach (Dictionary<string, object> retryRecord in Recordids)
                    {
                        if (!record.Keys.Contains("_Id") || !retryRecord.Keys.Contains("id"))
                        {
                            return false;
                        }
                        if (record["_Id"].ToString() == retryRecord["id"].ToString())
                        {
                            //matched ids are pushed to  ArchiveTokenExceptions category in sentry
                            if (await AddNewExceptionData(userName, integrationType, destinationExceptionType, record))
                            {
                                isexist = true;
                            }
                            await Task.Delay(3000); //Delay has been used due to async calls
                        }
                    }
                    // Listing  the records  that are not selected 
                    if (!isexist)
                        unSelectedRecords.Add(record);
                }
                //Updating the  source exception type category with  unselected records so the selected records are removed from source exception type
                await AddCompleteException(userName, integrationType, sourceExceptiontype, unSelectedRecords);
            }
            catch { }
            return false;
        }

        #region Logging

        public async Task<bool> LogTheFailedRecord(string exceptionDescription, string exceptionRaisedAt, string failedRecord, ExceptionType Exceptiontype, string AnswerId, string ccUserName, IntegrationType integrationType)
        {
            try
            {
                Dictionary<string, object> NewExceptionData = new Dictionary<string, object>();
                NewExceptionData.Add("_Id", Exceptiontype + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + DateTime.Now.Hour + DateTime.Now.Minute + DateTime.Now.Second + DateTime.Now.Millisecond);
                NewExceptionData.Add("ExceptionDescription", exceptionDescription);
                NewExceptionData.Add("ExceptionRaisedOn", DateTime.Now);
                NewExceptionData.Add("ExceptionRaisedAt", exceptionRaisedAt);
                NewExceptionData.Add("FailedRecord", failedRecord);
                NewExceptionData.Add("AnswerId", AnswerId);
                NewExceptionData.Add("ccUserName", ccUserName);
                return await AddNewExceptionData(ccUserName, integrationType, Exceptiontype, NewExceptionData);

            }
            catch { }
            return false;
        }

        /**
        * Two methods LogTheSuccessRecord, LogThePartialSuccessRecord these are the methods used in from other controllers
        * to log the data in to sentry when ever an exception arises in the process of inserting, updating tickets between 
        * cloudcherry and third party integration systems
        *  
        **/
        public async Task<bool> LogTheSuccessRecord(string caseNumber, string ansId, string insertedOn, ExceptionType Exceptiontype, string ccUserName, IntegrationType integrationtype)
        {

            try
            {
                Dictionary<string, object> NewExceptionData = new Dictionary<string, object>();
                NewExceptionData.Add("_Id", Exceptiontype + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + DateTime.Now.Hour + DateTime.Now.Minute + DateTime.Now.Second + DateTime.Now.Millisecond);
                NewExceptionData.Add("NewTicketId", caseNumber);
                NewExceptionData.Add("ExistedAnwserId", ansId);
                NewExceptionData.Add("InsertedOn", insertedOn);
                NewExceptionData.Add("ccUserName", ccUserName);

                return await AddNewExceptionData(ccUserName, integrationtype, Exceptiontype, NewExceptionData);

            }
            catch { }
            return false;
        }

        public async Task<bool> LogThePartialSuccessRecord(string AnswerID, string LogDateTime, string ExceptionFields, string ExceptionFieldsVsValues, string ExceptionMsg, string CaseNumber, ExceptionType collectionName, string ccUserName, IntegrationType integrationtype)
        {
            Dictionary<string, object> NewExceptionData = new Dictionary<string, object>();
            NewExceptionData.Add("_Id", collectionName + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + DateTime.Now.Hour + DateTime.Now.Minute + DateTime.Now.Second + DateTime.Now.Millisecond);
            NewExceptionData.Add("AnswerId", AnswerID);
            NewExceptionData.Add("ExceptionRaisedOn", LogDateTime);
            NewExceptionData.Add("ExceptionFieldsVsValues", ExceptionFieldsVsValues);
            NewExceptionData.Add("ExceptionDescription", ExceptionMsg);
            NewExceptionData.Add("CaseNumber", CaseNumber);
            NewExceptionData.Add("ccUserName", ccUserName);
            await AddNewExceptionData(ccUserName, integrationtype, collectionName, NewExceptionData);
            return true;

        }

        #endregion

        /**
* 
* RetreiveClientKey is used to retrieve project's key from sentry
* 
**/
        public async Task<string> RetreiveClientKey(string project)
        {
            try
            {
                string path = "https://sentry.io/api/0/projects/" + organization + "/" + project + "/keys/";
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, path);
                req.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(sentrybyte)));
                HttpResponseMessage res = new HttpResponseMessage();
                HttpClient client = new HttpClient();
                res = await client.SendAsync(req);
                string resMsg = await res.Content.ReadAsStringAsync();
                List<Dictionary<string, object>> Integrationdata = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(resMsg);
                Dictionary<string, object> dsn = JsonConvert.DeserializeObject<Dictionary<string, object>>(Integrationdata[0]["dsn"].ToString());
                return dsn["secret"].ToString();
            }
            catch { }
            return "";
        }


        /**
        * 
        * IsObjectExist is method used to check whether an organization, team, slug exist in the respective lists, if exists returns true else false.
        * Basing on this return value organization, team, slug will be created.
        * 
        **/
        public async Task<bool> IsObjectExist(string Objecttype, string objectname)
        {
            try
            {
                List<Dictionary<string, object>> objectList;
                if (Objecttype.ToLower() == "organization")
                    objectList = await RetreiveOrganizations();
                else if (Objecttype.ToLower() == "team")
                    objectList = await RetreiveTeams();
                else
                    objectList = await RetreiveProjects();

                foreach (Dictionary<string, object> li in objectList)
                {
                    if (li["slug"].ToString() == objectname)
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /**
        * 
        * CreateObject is method used to create an organization, team, slug respectively 
        * basing on the Objecttype, objectname parameters
        * 
        **/
        public async Task<bool> CreateObject(string Objecttype, string objectname)
        {
            try
            {
                if (Objecttype.ToLower() == "organization")
                    await CreateOrganization();
                else if (Objecttype.ToLower() == "team")
                    await CreateTeam();
                else
                    await CreateProject(objectname);

                return true;
            }
            catch
            {
                return false;
            }
        }


    }
}
