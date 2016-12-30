using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LTSAPI.CC_Classes
{
    public class SurveyToken
    {
        public string note { get; set; }
        public string id { get; set; }
    }

    public class Mondodb
    {
        public string _id  {get; set; }
        public string TokenId { get; set; }
        public string QuestionId { get; set; }
        public string QuestionText { get; set; }
        public string Case { get; set; }

    }
   
}