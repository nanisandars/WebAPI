using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LTSAPI.CC_Classes
{
    public class SurveyByToken
    {
        public string logoURL { get; set; }
        public List<Question> questions { get; set; }
    }
}