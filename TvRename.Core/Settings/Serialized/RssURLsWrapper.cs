using System.Collections.Generic;
using System.Xml.Serialization;

namespace TvRename.Core.Settings.Serialized {
    public class RssURLsWrapper {
        [XmlElement("URL")]
        public List<string> Urls { get; set; }
    }
}