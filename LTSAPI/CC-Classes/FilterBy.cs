using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LTSAPI.CC_Classes
{
    public class FilterBy
    {
        public DateTime? afterdate { get; set; }
        public DateTime? aftertime { get; set; }
        public bool archived { get; set; }
        public DateTime? beforedate { get; set; }
        public DateTime? beforetime { get; set; }
        public List<DayOfWeek> days { get; set; }
        public List<FilterByQuestions> filterquestions { get; set; }
        public List<string> location { get; set; }
        public bool onlyWithAttachments { get; set; }
        public bool onlyWithNotes { get; set; }
    }
}