using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LTSAPI.CC_Classes
{
    public class Answer
    {
        public string Id { get; set; }
        public string user { get; set; }
        public string locationId { get; set; }
        public DateTime responseDateTime { get; set; }
        public int responseDuration { get; set; }
        public string surveyClient { get; set; }
        public List<Response> responses { get; set; }
        public bool archived { get; set; }
        public MessageNotes notes { get; set; }
        public Ticket openTicket { get; set; }
    }
}