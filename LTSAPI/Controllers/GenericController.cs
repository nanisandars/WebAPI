using LTSAPI.CC_Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Configuration;
using System.Threading.Tasks;
using Newtonsoft.Json;
using LTSAPI.HelperClasses;
using Cherry.HelperClasses;
using System.Text;
using System.IO;

namespace LTSAPI.Controllers
{
    public class GenericController : ApiController
    {
        SentryController sentry = new SentryController();
        CloudCherryController cloudCherryController = new CloudCherryController();
       

        [HttpPost]
        [Route("api/Generic/AddATicket")]
        public async Task<List<Dictionary<string, object>>> connectToSF(string UriPathKey, string ccUserName, string ccApiKey, string sfClientId, string sfSecret, string sfRefreshToken)
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


        [HttpGet]
        [Route("api/Generic/IsIntegrationUserAuthenticated")]
        public Dictionary<string, object> IsIntegrationUserAuthenticated(string integrationType, string ccUsername)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            try
            {
                IntegrationType iType = (IntegrationType)Enum.Parse(typeof(IntegrationType), integrationType.Replace("integrations.", ""), true);
                Dictionary<string, object> FDlogindata = cloudCherryController.GetUserCredentials(ccUsername, iType);
                if (FDlogindata != null)
                {
                    result.Add("Message", "authenticated");
                }
                else
                {
                    result.Add("Message", "notauthenticated");
                }
                return result;
            }
            catch { result.Add("Message", "something went wrong!."); return result; }

        }

        [Route("api/Generic/saveUserCredentials")]
        [HttpGet]
        public bool saveUserCredentials(string FDKEY, string FDURL, string CCAPIkey, string ccUserName, string integrationType)
        {
            Dictionary<string, object> returnValue = new Dictionary<string, object>();
            try
            { 
                   IntegrationType iType = (IntegrationType)Enum.Parse(typeof(IntegrationType), integrationType.Replace("integrations.", ""), true);
                   Dictionary<string, Object> userData = cloudCherryController.GetUserCredentials(ccUserName, iType);
                    if (userData == null)
                    {
                        Dictionary<string, object> loginData = new Dictionary<string, object>();
                        loginData.Add("integrationDetails", FDURL + "|" + FDKEY);
                        loginData.Add("ccapikey", CCAPIkey);
                        cloudCherryController.AddNewUserCredentials(ccUserName, iType, loginData);
                        
                    }             
                
            }
            catch (Exception e)
            {
                
            }
            return true;
        }

        [Route("api/Generic/saveDefaultMappings")]
        [HttpPost]
        public async Task<Dictionary<string, object>> saveDefaultMappings(string integrationType)
        {
            Dictionary<string, object> returnValue = new Dictionary<string, object>();
            try
            {
                string httpResponse = "";
                using (var contentStream = await this.Request.Content.ReadAsStreamAsync())
                {
                    contentStream.Seek(0, SeekOrigin.Begin);
                    using (var sr = new StreamReader(contentStream))
                    {
                        httpResponse = sr.ReadToEnd();
                    }
                }
                               
                if (httpResponse!="")
                {
                    Dictionary<string, Object> integrationDetails = JsonConvert.DeserializeObject<Dictionary<string, Object>>(httpResponse);
                    Dictionary<string, Object> integrationMappings = new Dictionary<string, object>();
                    integrationMappings.Add("integrationdetails", integrationDetails["integrationdetails"]);
                    integrationMappings.Add("mappings", integrationDetails["mappings"]);
                    integrationMappings.Add("mappingsbackup", integrationDetails["mappingsbackup"]);

                    await cloudCherryController.createIntegrationList(integrationDetails["username"].ToString(), integrationDetails["ccapikey"].ToString(), integrationType);
                    await cloudCherryController.PostIntegrationData(integrationDetails["username"].ToString(), integrationDetails["ccapikey"].ToString(), integrationType, integrationMappings);
                    IntegrationType iType = (IntegrationType)Enum.Parse(typeof(IntegrationType), integrationType.Replace("integrations.", ""), true);
                    Dictionary<string, Object> userData = cloudCherryController.GetUserCredentials(integrationDetails["username"].ToString(), iType);
                    if (userData == null)
                    {
                        Dictionary<string, object> loginData = new Dictionary<string, object>();
                        loginData.Add("integrationDetails", integrationDetails["integrationdetails"].ToString());
                        loginData.Add("ccapikey", integrationDetails["ccapikey"].ToString());
                        cloudCherryController.AddNewUserCredentials(integrationDetails["username"].ToString(), iType, loginData);
                    }
                    returnValue.Add("Message", "success");
                }
                return returnValue;
            }
            catch (Exception e)
            {
                returnValue.Add("Message", "not successful"); return returnValue;
            }
           
        }

        public async Task<bool> ConnectBasic(string UriPathKey, string FDkey, string FDurl)
        {
            try
            {
                Dictionary<string, object> CCFDdata = new Dictionary<string, object>();
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                string fdAllTicketsAPI = getKeyPath(UriPathKey);
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
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            { return false; }
        }

        public string getKeyPath(string key)
        {
            return ConfigurationManager.AppSettings[key].ToString();
        }

    }
}
