using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LTSAPI.CC_Classes
{
    public class LeadingOption
    {
        public string Audio { get; set; }
        public FilterBy filter { get; set; }
        public List<string> MultiSelect { get; set; }
        public string MultiSelectAudio { get; set; }
        public string Text { get; set; }
    }
}