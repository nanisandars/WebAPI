using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LTSAPI.CC_Classes
{
    public class FilterByQuestions
    {
        public List<string> answerCheck { get; set; }
        public string GroupBy { get; set; }
        public int Number { get; set; }
        public string questionId { get; set; }
    }
}