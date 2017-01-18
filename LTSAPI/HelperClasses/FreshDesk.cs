using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Cherry.HelperClasses;
using Newtonsoft.Json;
namespace LTSAPI.HelperClasses
{
    public class FreshDesk
    {
        public void SetUserCredentials(string doc, ref string FDAPIKey, ref  string FDURL, ref string CCApikey)
        {
            Dictionary<string,object> objDoc = JsonConvert.DeserializeObject<Dictionary<string,object>>(doc);
            foreach (var item in objDoc)
            {
                string key = item.Key;
                switch (key)
                {
                    case "FDURL":
                        {
                            FDURL = item.Value.ToString();
                            break;
                        }
                    case "FDKey":
                        {
                            FDAPIKey = item.Value.ToString();
                            break;
                        }
                    case "CCApikey":
                        {
                            CCApikey = item.Value.ToString();
                            break;
                        }
                }
            }
        }
    }
}
