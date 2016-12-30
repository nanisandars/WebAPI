using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LTSAPI.CC_Classes
{
    public class AltDisplay
    {
        public string Audio { get; set; }
        public string DisclaimerText { get; set; }
        public string EndOfSurveyAudio { get; set; }
        public string EndOfSurveyMessage { get; set; }
        public List<string> MultiSelect { get; set; }
        public List<string> MultiSelectAudio { get; set; }
        public string Text { get; set; }
        public string ValidationRegex { get; set; }
    }
}