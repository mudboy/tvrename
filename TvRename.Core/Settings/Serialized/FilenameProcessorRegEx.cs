using System.Xml.Serialization;

namespace TvRename.Core.Settings.Serialized {
    [XmlType("Regex")]
    public class FilenameProcessorRegEx {
        // A regular expression to find the season and episode number in a filename
        [XmlAttribute]
        public bool Enabled { get; set; }

        [XmlAttribute]
        public string Notes { get; set; }

        [XmlAttribute]
        public string RE { get; set; }

        [XmlAttribute]
        public bool UseFullPath { get; set; }

        public static FilenameProcessorRegEx Create(bool enabled, string re, bool useFullPath, string notes) {
            return new FilenameProcessorRegEx {
                Enabled = enabled,
                RE = re,
                UseFullPath = useFullPath,
                Notes = notes
            };
        }
    }
}