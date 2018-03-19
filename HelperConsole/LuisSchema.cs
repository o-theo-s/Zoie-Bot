using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelperConsole
{
    public class LuisSchema
    {
        public string luis_schema_version { get; set; }
        public string versionId { get; set; }
        public string name { get; set; }
        public string desc { get; set; }
        public string culture { get; set; }
        public List<Intent> intents { get; set; }
        public List<Entity> entities { get; set; }
        public List<object> composites { get; set; }
        public List<ClosedList> closedLists { get; set; }
        public List<string> bing_entities { get; set; }
        public List<ModelFeature> model_features { get; set; }
        public List<object> regex_features { get; set; }
        public List<Utterance> utterances { get; set; }
    }

    public class Intent
    {
        public string name { get; set; }
    }

    public class Entity
    {
        public string name { get; set; }
        public List<string> children { get; set; }
    }

    public class SubList
    {
        public string canonicalForm { get; set; }
        public List<object> list { get; set; }
    }

    public class ClosedList
    {
        public string name { get; set; }
        public List<SubList> subLists { get; set; }
    }

    public class ModelFeature
    {
        public string name { get; set; }
        public bool mode { get; set; }
        public string words { get; set; }
        public bool activated { get; set; }
    }

    public class Utterance
    {
        public string text { get; set; }
        public string intent { get; set; }
        public List<object> entities { get; set; }
    }
}
