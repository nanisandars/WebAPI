using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace Cherry.HelperClasses
{
    public class SalesForce
    {
    
        private string _sfClientID = "";
        private string _sfSecretKey = "";
        private string _ccUserName = "";
        private string _ccApiKey = "";
        private string _sfRefreshToken = "";   
        TDesEncryption tDesEncryption = new TDesEncryption();

        public SalesForce() { } // Used in Retry Controller

        public SalesForce(string sfClientID, string sfSecret, string ccUserName, string ccApiKey, string sfRefreshToken)
        {
            _sfClientID = sfClientID;
            _sfSecretKey = sfSecret;
            _ccUserName = ccUserName;
            _ccApiKey = ccApiKey;

            if (sfRefreshToken != "")
            {
                _sfRefreshToken = sfRefreshToken;               
            }
        }

        internal async Task<HttpResponseMessage> setSFRefreshToken()
        {
            HttpResponseMessage response = null;
            if (_sfRefreshToken == "")
            {
                var postCrendentials = new[] 
                {
                    new KeyValuePair<string,string>("response_type","code"),
                    new KeyValuePair<string,string>("client_id", _sfClientID),    
                    new KeyValuePair<string,string>("client_secret", _sfSecretKey),  
                    new KeyValuePair<string,string>("redirect_uri", ConfigurationManager.AppSettings["SFCallbackURL"])
                };

                Cherry cherryObj = new Cherry();
                response = await cherryObj.callApiEndPoint(postCrendentials, ConfigurationManager.AppSettings["SFEndPoint"]);                
            }
            return response;
        }

        /*
         *  Assigns the values of the given user credentials
         *
         */
        public void SetUserCredentials(CCSFSettings doc,  ref  string Clientid, ref string Secret, ref  string Rtoken, ref  string InstanceUrl, ref  string ccUserName, ref string apiKey, ref  string Error)
        {
            Clientid = doc.settings.SFClientid;
            Secret =  doc.settings.SFSecret;
            Rtoken =  doc.settings.Refreshtoken;
            InstanceUrl =  doc.settings.SFInstanceurl;
            ccUserName =  doc.settings.Username;
            apiKey =  doc.settings.Apikey;
            Error =  doc.settings.Errorstring;           
        }

        public async Task<List<string>> ConnectSF(string clientid, string secret, string refreshtoken)
        {
            try
            {
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

        public void LogThisError(string Errormessage)
        {
            string logPath = System.Configuration.ConfigurationManager.AppSettings["SettingsPath"];
            logPath = HttpContext.Current.Server.MapPath("~/" + logPath);

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(logPath, true))
            {
                file.WriteLine("Message :" + Errormessage + "<br/>" + Environment.NewLine + "Date :" + DateTime.Now.ToString());
                file.WriteLine(Environment.NewLine + "-----------------------------------------------------------------------------" + Environment.NewLine);
            }
        }

    }
}
