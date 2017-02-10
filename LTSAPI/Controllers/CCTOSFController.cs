using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Configuration;
using System.Text;
using System.Web;
using System.IO;
using Cherry.HelperClasses;

namespace LTSAPI.Controllers
{
    public class CCTOSFController : ApiController
    {
        string Clientid = "";
        string Secret = "";
        string Rtoken = "";
        string InstanceUrl = "";
        string apiKey = "";
        string Error = "";

        CloudCherryController cloudCherryController = new CloudCherryController();
        SalesForce salesForce = new SalesForce();

        /**
        *  Checks if Salesforce Refresh  Token Exists or Not, if found Returns Refresh Token to UI
        * 
        * Else  Salesforce authentication Screen Details is sent to UI, which is then used for Salesforce authentication 
       
        **/

        [HttpGet]
        [Route("api/CCTOSF/IsRtokenexist")]
        public async Task<List<Dictionary<string, object>>> IsRefreshtokenExist(string userName)
        {
            List<Dictionary<string, object>> Finaltoken = new List<Dictionary<string, object>>();
            Dictionary<string, object> rtoken = new Dictionary<string, object>();
            try
            {

                Dictionary<string, object> Exceptiondata = cloudCherryController.getClientsettingsByKey("username", userName, IntegrationType.salesforce);
                if (Exceptiondata == null)
                {
                    rtoken = new Dictionary<string, object>();
                    rtoken.Add("Refreshtoken", "");
                    rtoken.Add("Error", "");
                    Finaltoken.Add(rtoken);
                    return Finaltoken;
                }
                Clientid = Exceptiondata["clientid"].ToString();
                Secret = Exceptiondata["secret"].ToString();
                Rtoken = Exceptiondata["rtoken"].ToString();
                InstanceUrl = Exceptiondata["instanceurl"].ToString();
                apiKey = Exceptiondata["apikey"].ToString();
                Error = Exceptiondata["error"].ToString();

                if (Exceptiondata != null)
                {
                    rtoken.Add("Refreshtoken", Rtoken);
                }
                if (Exceptiondata == null)
                {
                    rtoken.Add("Refreshtoken", "");
                    rtoken.Add("Error", "");
                }

                else if (Error != "")
                {
                    rtoken.Add("Error", Error);
                }
                else if (Error == "" && (Rtoken == ""))
                {
                    rtoken.Add("Error", "Mismatched cloud cherry life time API key");
                }
                else
                {
                    List<string> sfdet = await salesForce.ConnectSF(Clientid, Secret, Rtoken);
                    if (sfdet[0] == null)
                    {
                        rtoken = new Dictionary<string, object>();
                        rtoken.Add("Refreshtoken", "");
                        rtoken.Add("Error", "Refresh Token Expired or Invalid Grant");
                    }

                }
                Finaltoken.Add(rtoken);
                return Finaltoken;
            }
            catch (Exception ex) { salesForce.LogThisError(ex.Message); }
            finally { }
            rtoken = new Dictionary<string, object>();
            rtoken.Add("Refreshtoken", "");
            rtoken.Add("Error", "Unknown Error occured");
            return Finaltoken;
        }

    }
}
