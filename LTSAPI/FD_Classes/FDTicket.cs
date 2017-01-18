using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LTSAPI.FD_Classes
{
    public class FDTicket
    {
        public string name { get; set; }
        public string requester_id { get; set; }
        public string email { get; set; }
        public string facebook_id { get; set; }
        public string phone { get; set; }
        public string twitter_id { get; set; }
        public string subject { get; set; }
        public string type { get; set; }
        public string status { get; set; }
        public string priority { get; set; }
        public string description { get; set; }
        public string responder_id { get; set; }
        public List<object> attachments { get; set; }
        public List<string> cc_emails { get; set; }
        public Dictionary<string, object> custom_fields { get; set; }
        public DateTime due_by { get; set; }
        public string email_config_id { get; set; }
        public DateTime fr_due_by { get; set; }
        public string group_id { get; set; }
        public string product_id { get; set; }
        public string source { get; set; }
        public List<string> tags { get; set; }

    }
}