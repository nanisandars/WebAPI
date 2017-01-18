using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LTSAPI.CC_Classes
{
    public class Ticket
    {
        public string status { get; set; }
        public string assignedBy { get; set; }
        public string action { get; set; }
        public string department { get; set; }
        public string language { get; set; }
        public string orginalRoutedTo { get; set; }
        public string created { get; set; }
        public string after { get; set; }
        public int sequence { get; set; }
        public string nextEscalationIn { get; set; }
        public string nextEscalationUser { get; set; }
        public string closed { get; set; }
        public int rating { get; set; }
        public List<string> routeHistory { get; set; }
        public string currentAssignedTo { get; set; }
        public List<string> cCedTo { get; set; }
        public int priority { get; set; }
        public bool isEscalated { get; set; }
        public string description { get; set; }
        public List<string> diagnosticSurveyResponses { get; set; }
        public bool isShowcased { get; set; }
        public bool isGlobalShowcased { get; set; }
        public string showcaseTitle { get; set; }
        public string showcasedBy { get; set; }
        public string comments { get; set; }
    }
}