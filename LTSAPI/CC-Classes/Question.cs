using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LTSAPI.CC_Classes
{
    public class Question
    {
        //public string AnalyticsTag { get; set; }
        //public bool ApiFill { get; set; }
        //public string Audio { get; set; }
        //public string BackgroundURL { get; set; }
        //public double CDMWeight { get; set; }
        //public List<string> ConditionalAnswerCheck { get; set; }
        //public FilterBy ConditionalFilter { get; set; }
        //public int ConditionalNumber { get; set; }
        //public string ConditionalToQuestion { get; set; }
        //public string DisclaimerText { get; set; }
        public List<string> DisplayLocation { get; set; }
        public string DisplayStyle { get; set; }
        public string DisplayType { get; set; }
        //public bool EndOfSurvey { get; set; }
        //public string EndOfSurveyMessage { get; set; }
        //public DateTime? GoodAfter { get; set; }
        //public DateTime? GoodBefore { get; set; }
        public string Id { get; set; }
        //public string InteractiveLiveAPIPreFillUrl { get; set; }
        //public bool IsRequired { get; set; }
        //public bool IsRetired { get; set; }
        //public List<LeadingOption> LeadingDisplayTexts { get; set; }
        //public string LocationSpecific { get; set; }
        //public List<string> MultiSelect { get; set; }
        //public List<string> MultiSelectAudio { get; set; }
        //public List<string> MultiSelectChoiceTag { get; set; }
        //public string Note { get; set; }
        //public string PresentationMode { get; set; }
        public List<string> QuestionTags { get; set; }
        //public int Sequence { get; set; }
        //public string SetName { get; set; }
        //public bool StaffFill { get; set; }
        public string Text { get; set; }
        //public int TimeLimit { get; set; }
        //public DateTime? TimeOfDayAfter { get; set; }
        //public DateTime? TimeOfDayBefore { get; set; }
        //public List<string> TopicTags { get; set; }
        //public Dictionary<string, AltDisplay> Translated { get; set; }
        //public string User { get; set; }
        //public double UserWeight { get; set; }
        //public string ValidationHint { get; set; }
        //public string ValidationRegex { get; set; }
    }
}