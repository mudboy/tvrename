using System.Xml.Serialization;

namespace TvRename.Core.Settings.Serialized {
    public class MyShowStatusTVWColors {
        [XmlAttribute]
        public bool IsMeta { get; set; }

        [XmlAttribute]
        public bool IsShowLevel { get; set; }

        [XmlAttribute]
        public string ShowStatus { get; set; }

        [XmlAttribute]
        public string Color { get; set; }
    }
}