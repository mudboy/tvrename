using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace TvRename.Core.Settings {
    [XmlRoot(ElementName = "TVRename", Namespace = "")]
    public class SettingsWrapper {
        [XmlAttribute]
        public String Version { get; set; }

        public Settings Settings { get; set; }

        [XmlArrayItem("ShowItem")]
        public List<MyShowItem> MyShows { get; set; }
    }

    public class Settings {
        public enum FolderJpgIsType {
            [XmlEnum("0")] Banner,
            [XmlEnum("1")] Poster,
            [XmlEnum("2")] FanArt
        }

        public enum WTWDoubleClickAction {
            [XmlEnum("0")] Search,
            [XmlEnum("1")] Scan
        }

        public Settings() {
            ExportWTWRSSTo = "";
            ExportWTWXMLTo = "";
            ExportMissingXMLTo = "";
            ExportMissingCSVTo = "";
            ExportRenamingXMLTo = "";
            ExportFOXMLTo = "";
            StartupTab2 = "";
            NamingStyle = "";
            VideoExtensions = "";
            OtherExtensions = "";
            SpecialsFolderName = "";
            uTorrentPath = "";
            ResumeDatPath = "";
            SABApiKey = "";
            SABHostPort = "";
            PreferredLanguage = "en";
        }

        public TheSearchers TheSearchers { get; set; }
        public bool BGDownload { get; set; }
        public bool OfflineMode { get; set; }

        [XmlArray("Replacements")]
        public List<MyReplacement> Replacements { get; set; }

        // if you need to rename these then add an xmlelement attribute with the old name
        public bool ExportWTWRSS { get; set; }
        public string ExportWTWRSSTo { get; set; }
        public bool ExportWTWXML { get; set; }
        public string ExportWTWXMLTo { get; set; }
        public int WTWRecentDays { get; set; }
        public bool ExportMissingXML { get; set; }
        public string ExportMissingXMLTo { get; set; }
        public bool ExportMissingCSV { get; set; }
        public string ExportMissingCSVTo { get; set; }
        public bool ExportRenamingXML { get; set; }
        public string ExportRenamingXMLTo { get; set; }
        public bool ExportFOXML { get; set; }
        public string ExportFOXMLTo { get; set; }
        public string StartupTab2 { get; set; }
        public string NamingStyle { get; set; }
        public bool NotificationAreaIcon { get; set; }
        public string VideoExtensions { get; set; }
        public string OtherExtensions { get; set; }
        public int ExportRSSMaxDays { get; set; }
        public int ExportRSSMaxShows { get; set; }
        public int ExportRSSDaysPast { get; set; }
        public bool KeepTogether { get; set; }
        public bool LeadingZeroOnSeason { get; set; }
        public bool ShowInTaskbar { get; set; }
        public bool IgnoreSamples { get; set; }
        public bool ForceLowercaseFilenames { get; set; }
        public bool RenameTxtToSub { get; set; }
        public int ParallelDownloads { get; set; }
        public bool AutoSelectShowInMyShows { get; set; }
        public bool ShowEpisodePictures { get; set; }
        public string SpecialsFolderName { get; set; }
        public string uTorrentPath { get; set; }
        public string ResumeDatPath { get; set; }
        public bool SearchRSS { get; set; }
        public bool EpImgs { get; set; }
        public bool NFOs { get; set; }
        public bool pyTivoMeta { get; set; }
        public bool pyTivoMetaSubFolder { get; set; }
        public bool FolderJpg { get; set; }
        public FolderJpgIsType FolderJpgIs { get; set; }
        public bool CheckuTorrent { get; set; }
        public bool RenameCheck { get; set; }
        public bool MissingCheck { get; set; }
        public bool SearchLocally { get; set; }
        public bool LeaveOriginals { get; set; }
        public bool LookForDateInFilename { get; set; }
        public bool MonitorFolders { get; set; }
        public string SABApiKey { get; set; }
        public bool CheckSABnzbd { get; set; }
        public string SABHostPort { get; set; }
        public string PreferredLanguage { get; set; }
        public WTWDoubleClickAction WTWDoubleClick { get; set; }

        [XmlArray("FNPRegexs")]
        public List<FilenameProcessorRegEx> FNPRegexs { get; set; }

        [XmlArray("RSSURLs")]
        [XmlArrayItem("URL")]
        public List<string> RssUrls { get; set; }

        [XmlArray("ShowStatusTVWColors")]
        [XmlArrayItem("ShowStatusTVWColor")]
        public List<MyShowStatusTVWColors> Colors { get; set; }
    }

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

    [XmlType("Replace")]
    public class MyReplacement {
        [XmlAttribute]
        public string This { get; set; }

        [XmlAttribute]
        public string That { get; set; }

        [XmlIgnore]
        public bool CaseInsensitive { get; set; }

        [XmlAttribute("CaseInsensitive")]
        public string CaseInsensitiveString {
            get { return CaseInsensitive ? "Y" : "N"; }
            set { CaseInsensitive = value == "Y"; }
        }
    }

    public class TheSearchers {
        public class Choice {
            [XmlAttribute]
            public String Name { get; set; }

            [XmlAttribute]
            public String URL2 { get; set; }
        }

        public String Current { get; set; }

        [XmlElement(ElementName = "Choice")]
        public List<Choice> Choices { get; set; }
    }

    public class RssURLsWrapper {
        [XmlElement("URL")]
        public List<string> Urls { get; set; }
    }

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
    }

    public class MyShows {
        [XmlElement("ShowItem")]
        public List<MyShowItem> Items { get; set; }
    }

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