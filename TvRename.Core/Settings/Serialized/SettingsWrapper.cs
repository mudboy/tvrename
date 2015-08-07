using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace TvRename.Core.Settings.Serialized {
    [XmlRoot(ElementName = "TVRename", Namespace = "")]
    public class SettingsWrapper {
        [XmlAttribute]
        public String Version { get; set; }

        public TvSettings Settings { get; set; }

        [XmlArrayItem("ShowItem")]
        public List<MyShowItem> MyShows { get; set; }

        [XmlArrayItem("Folder")]
        public List<string> MonitorFolders { get; set; }

        [XmlArrayItem("Folder")]
        public List<string> IgnoreFolders { get; set; }

        [XmlArrayItem("Folder")]
        public List<string> FinderSearchFolders { get; set; }

        [XmlArrayItem("Ignore")]
        public List<string> IgnoreItems { get; set; }

        public static SettingsWrapper Load() {
            FileInfo settingsFile = PathManager.TVDocSettingsFile;
            var x = new XmlSerializer(typeof (SettingsWrapper));
            using (var fileStream = new FileStream(settingsFile.FullName, FileMode.Open, FileAccess.Read)) {
                return (SettingsWrapper) x.Deserialize(fileStream);
            }
        }

        public static void Save(SettingsWrapper wrapper) {
            FileInfo settingsFile = PathManager.TVDocSettingsFile;
            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            var x = new XmlSerializer(typeof (SettingsWrapper));
            using (XmlTextWriter w = new XmlTextWriter(new FileStream(settingsFile.FullName, FileMode.Truncate, FileAccess.Write), Encoding.UTF8) {Indentation = 2, Formatting = Formatting.Indented}) {
                x.Serialize(w, wrapper, ns);
            }
        }
    }

    public class MyShows {
        [XmlElement("ShowItem")]
        public List<MyShowItem> Items { get; set; }
    }
}