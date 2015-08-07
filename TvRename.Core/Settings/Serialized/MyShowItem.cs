using System.Collections.Generic;
using System.Xml.Serialization;

namespace TvRename.Core.Settings.Serialized {
    public class MyShowItem {
        public bool UseCustomShowName { get; set; }
        public string CustomShowName { get; set; }
        public bool ShowNextAirdate { get; set; }
        public int TVDBID { get; set; }
        public bool AutoAddNewSeasons { get; set; }
        public string FolderBase { get; set; }
        public bool FolderPerSeason { get; set; }
        public string SeasonFolderName { get; set; }
        public bool DoRename { get; set; }
        public bool DoMissingCheck { get; set; }
        public bool CountSpecials { get; set; }
        public bool DVDOrder { get; set; }
        public bool ForceCheckNoAirdate { get; set; }
        public bool ForceCheckFuture { get; set; }
        public bool UseSequentialMatch { get; set; }
        public bool PadSeasonToTwoDigits { get; set; }

        [XmlArrayItem("Ignore")]
        public List<int> IgnoreSeasons { get; set; }

        [XmlArrayItem("Alias")]
        public List<string> AliasNames { get; set; }

        public string CustomSearchURL { get; set; }

        public RulesWrapper Rules { get; set; }

        [XmlElement("SeasonFolders")]
        public List<FolderWrapper> SeasonFolders { get; set; }
    }

        public class FolderWrapper {
        [XmlAttribute]
        public string SeasonNumber { get; set; }

        [XmlElement]
        public List<Folder> Folder { get; set; }
    }

    public class Folder {
        [XmlAttribute]
        public string Location { get; set; }
    }

    public class RulesWrapper {
        [XmlAttribute]
        public string SeasonNumber { get; set; }

        [XmlElement("Rule")]
        public List<MyRule> Rules { get; set; }
    }

    public class MyRule {
        public enum RuleAction {
            [XmlEnum("0")] kRemove,
            [XmlEnum("1")] kSwap,
            [XmlEnum("2")] kMerge,
            [XmlEnum("3")] kInsert,
            [XmlEnum("4")] kIgnoreEp,
            [XmlEnum("5")] kRename,
            [XmlEnum("6")] kSplit,
            [XmlEnum("7")] kCollapse
        }

        public RuleAction DoWhatNow { get; set; }
        public int First { get; set; }
        public int Second { get; set; }
        public string UseUserSuppliedText { get; set; }
    }

}