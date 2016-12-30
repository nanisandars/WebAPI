using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net.Http;
using System.Web.Http;
using System.Threading.Tasks;
using System.Net;
using System.Configuration;
using Newtonsoft.Json;
using System.IO;

namespace Cherry.HelperClasses
{
    public class Cherry
    {

        string EncryptKey = "";
        TDesEncryption tDesEncryption = new TDesEncryption();
        public Cherry()
        {
            EncryptKey = ConfigurationManager.AppSettings["EncryptKey"];
        }

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

        public string getFDClientsettingsByCCAPI(string Key,string Value,ref string UserName)
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
                    if ((objkey.Key.ToString().ToUpper() == Key.ToUpper() ) && ( objkey.Value.ToString() == Value))
                    {
                        UserName = item.Key.ToString();
                        return item.Value.ToString();
                    }
                }

                //if (item.Key.ToString() == Value)
                //{
                //    UserName = item.Key;
                //    return item.Value.ToString();
                //}
                //if (item.Key.ToString() == Value)
                //{
                //    return item.Value.ToString();
                //}
            }

            //foreach (Dictionary<string, object> setting in ccfduser)
            //{
            //    if (Key == "Username" && setting.UserName == Value)
            //    {
            //        return setting;
            //    }
            //}
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

            //foreach (Dictionary<string, object> setting in ccfduser)
            //{
            //    if (Key == "Username" && setting.UserName == Value)
            //    {
            //        return setting;
            //    }
            //}
            return null;
        }



        public CCSFSettings getClientsettingsByKey(string Key, string Value)
        {
            string settingsPath = HttpContext.Current.Server.MapPath("~/" + ConfigurationManager.AppSettings["SettingsPath"]);
            List<CCSFSettings> ccsfSettings = getClientsettings(settingsPath);
            if (ccsfSettings == null)
                return null;

            foreach (CCSFSettings setting in ccsfSettings)
            {
                if (Key == "Apikey" && setting.settings.Apikey.Trim() == Value.Trim())
                {
                    return setting;

                }
                if (Key == "Username" && setting.settings.Username == Value)
                {
                    return setting;

                }
                if (Key == "Refreshtoken" && setting.settings.Refreshtoken == Value)
                {
                    return setting;

                }


            }
            return null;
        }
    }

    public class settings
    {
        public string Username { get; set; }
        public string Apikey { get; set; }
        public string Refreshtoken { get; set; }
        public string SFClientid { get; set; }
        public string SFSecret { get; set; }
        public string SFInstanceurl { get; set; }
        public string Errorstring { get; set; }
    }

    public class CCSFSettings
    {
        public string Username { get; set; }
        public settings settings = new settings();
    }

    public class CCFDUser
    {
        public string UserName { get; set; }
        public CCFDSettings fdSettings { get; set; }
    }

    public class CCFDSettings
    {
        public string FDKey { get; set; }
        public string FDURL { get; set; }
        public string CCApikey { get; set; }
    }

}
