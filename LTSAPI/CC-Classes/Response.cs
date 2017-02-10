using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LTSAPI.CC_Classes
{
    public class Response
    {
        public int NumberInput { get; set; }
        public string QuestionId { get; set; }
        public string QuestionText { get; set; }
        public string TextInput { get; set; }
    }

    public class ClientCredentials
    {
        public string CCUserName { get; set; }
      
        public string SFClientID { get; set; }
        public string SFSecretKey { get; set; }
        public string APIKey { get; set; }
        public string SFRefreshToken { get; set; }
    }
}