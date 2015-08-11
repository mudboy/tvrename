using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using TvRename.TheTVDB;

namespace TvRename.Core.Settings.Serialized {
    public class TvSettings {
        public enum FolderJpgIsType {
            [XmlEnum("0")] Banner,
            [XmlEnum("1")] Poster,
            [XmlEnum("2")] FanArt
        }

        public enum WTWDoubleClickAction {
            [XmlEnum("0")] Search,
            [XmlEnum("1")] Scan
        }

        public TvSettings() {
            AutoSelectShowInMyShows = true;
            ExportWTWRSSTo = "";
            ExportWTWXMLTo = "";
            ExportMissingXMLTo = "";
            ExportMissingCSVTo = "";
            ExportRenamingXMLTo = "";
            ExportFOXMLTo = "";
            StartupTab2 = "";
            VideoExtensions = "";
            OtherExtensions = "";
            SpecialsFolderName = "";
            uTorrentPath = "";
            ResumeDatPath = "";
            SABAPIKey = "";
            SABHostPort = "";
            PreferredLanguage = "en";
            FNPRegexs = DefaultFNPList();
            FolderJpgIs = FolderJpgIsType.Poster;
            IgnoreSamples = true;
            KeepTogether = true;
            MissingCheck = true;
            NamingStyle = new CustomName();
            RssUrls = DefaultRSSURLList();
            RenameCheck = true;
            Replacements = DefaultListRE();
            SearchLocally = true;
            ShowEpisodePictures = true;
            ShowInTaskbar = true;
            TheSearchers = new TheSearchers();
            WTWRecentDays = 7;
            SampleFileMaxSizeMB = 50;

            // todo colors
            SetToDefaults();
        }

        private static List<string> DefaultRSSURLList() {
            return new List<String> {
                "http://tvrss.net/feed/eztv"
            };
        }

        private static List<MyReplacement> DefaultListRE() {
            return new List<MyReplacement> {
                MyReplacement.Create("*", "#", false),
                MyReplacement.Create("?", "", false),
                MyReplacement.Create(">", "", false),
                MyReplacement.Create("<", "", false),
                MyReplacement.Create(":", "-", false),
                MyReplacement.Create("/", "-", false),
                MyReplacement.Create("\\", "-", false),
                MyReplacement.Create("|", "-", false),
                MyReplacement.Create("\"", "'", false)
            };
        }

        public void SetToDefaults() {
            // defaults that aren't handled with default initialisers
            VideoExtensions = ".avi;.mpg;.mpeg;.mkv;.mp4;.wmv;.divx;.ogm;.qt;.rm";
            OtherExtensions = ".srt;.nfo;.txt;.tbn";

            // have a guess at utorrent's path
            var guesses = new string[3];
            guesses[0] = System.Windows.Forms.Application.StartupPath + "\\..\\uTorrent\\uTorrent.exe";
            guesses[1] = "c:\\Program Files\\uTorrent\\uTorrent.exe";
            guesses[2] = "c:\\Program Files (x86)\\uTorrent\\uTorrent.exe";
            uTorrentPath = "";
            foreach (string g in guesses) {
                FileInfo f = new FileInfo(g);
                if (f.Exists) {
                    uTorrentPath = f.FullName;
                    break;
                }
            }

            // ResumeDatPath
            FileInfo f2 =
                new FileInfo(System.Windows.Forms.Application.UserAppDataPath + "\\..\\..\\..\\uTorrent\\resume.dat");
            ResumeDatPath = f2.Exists ? f2.FullName : "";
        }

        public static List<FilenameProcessorRegEx> DefaultFNPList() {
            // Default list of filename processors
            return new List<FilenameProcessorRegEx> {
                FilenameProcessorRegEx.Create(true, "(^|[^a-z])s?(?<s>[0-9]+)[ex](?<e>[0-9]{2,})(e[0-9]{2,})*[^a-z]", false, "3x23 s3x23 3e23 s3e23 s04e01e02e03"),
                FilenameProcessorRegEx.Create(false, "(^|[^a-z])s?(?<s>[0-9]+)(?<e>[0-9]{2,})[^a-z]", false, "323 or s323 for season 3, episode 23. 2004 for season 20, episode 4."),
                FilenameProcessorRegEx.Create(false, "(^|[^a-z])s(?<s>[0-9]+)--e(?<e>[0-9]{2,})[^a-z]", false, "S02--E03"),
                FilenameProcessorRegEx.Create(false, "(^|[^a-z])s(?<s>[0-9]+) e(?<e>[0-9]{2,})[^a-z]", false, "'S02.E04' and 'S02 E04'"),
                FilenameProcessorRegEx.Create(false, "^(?<s>[0-9]+) (?<e>[0-9]{2,})", false, "filenames starting with '1.23' for season 1, episode 23"),
                FilenameProcessorRegEx.Create(true, "(^|[^a-z])(?<s>[0-9])(?<e>[0-9]{2,})[^a-z]", false, "Show - 323 - Foo"),
                FilenameProcessorRegEx.Create(true, "(^|[^a-z])se(?<s>[0-9]+)([ex]|ep|xep)?(?<e>[0-9]+)[^a-z]", false, "se3e23 se323 se1ep1 se01xep01..."),
                FilenameProcessorRegEx.Create(true, "(^|[^a-z])(?<s>[0-9]+)-(?<e>[0-9]{2,})[^a-z]", false, "3-23 EpName"),
                FilenameProcessorRegEx.Create(true, "(^|[^a-z])s(?<s>[0-9]+) +- +e(?<e>[0-9]{2,})[^a-z]", false, "ShowName - S01 - E01"),
                FilenameProcessorRegEx.Create(true, "\\b(?<e>[0-9]{2,}) ?- ?.* ?- ?(?<s>[0-9]+)", false, "like '13 - Showname - 2 - Episode Title.avi'"),
                FilenameProcessorRegEx.Create(true, "\\b(episode|ep|e) ?(?<e>[0-9]{2,}) ?- ?(series|season) ?(?<s>[0-9]+)", false, "episode 3 - season 4"),
                FilenameProcessorRegEx.Create(true, "season (?<s>[0-9]+)\\\\e?(?<e>[0-9]{1,3}) ?-", true, "Show Season 3\\E23 - Epname"),
                FilenameProcessorRegEx.Create(false, "season (?<s>[0-9]+)\\\\episode (?<e>[0-9]{1,3})", true, "Season 3\\Episode 23")
            };
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
        public int StartupTab { get; set; }
        public bool ShouldMonitorFolders { get; set; }

        [XmlIgnore]
        public CustomName NamingStyle { get; set; }

        [XmlElement("NamingStyle")]
        public string NamingStyleString {
            get { return NamingStyle.StyleString; }
            set { NamingStyle.StyleString = value; }
        }

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
        public string SABAPIKey { get; set; }
        public bool CheckSABnzbd { get; set; }
        public string SABHostPort { get; set; }
        public string PreferredLanguage { get; set; }
        public WTWDoubleClickAction WTWDoubleClick { get; set; }
        public int SampleFileMaxSizeMB { get; set; }

        [XmlArray("FNPRegexs")]
        public List<FilenameProcessorRegEx> FNPRegexs { get; set; }

        [XmlArray("RSSURLs")]
        [XmlArrayItem("URL")]
        public List<string> RssUrls { get; set; }

        [XmlArray("ShowStatusTVWColors")]
        [XmlArrayItem("ShowStatusTVWColor")]
        public List<MyShowStatusTVWColors> Colors { get; set; }

        [XmlIgnore]
        public string[] OtherExtensionsArray {
            get { return OtherExtensions.Split(';'); }
        }

        [XmlIgnore]
        public string[] VideoExtensionsArray {
            get { return VideoExtensions.Split(';'); }
        }

        public bool UsefulExtension(string ext, bool otherExtensionsToo) {
            if (VideoExtensionsArray.Any(s => ext.ToLower() == s)) {
                return true;
            }
            if (otherExtensionsToo) {
                return OtherExtensionsArray.Any(s => ext.ToLower() == s);
            }
            return false;
        }

        public string BTSearchURL(ProcessedEpisode epi) {
            if (epi == null) {
                return "";
            }
            SeriesInfo s = epi.TheSeries;
            if (s == null) {
                return "";
            }
            String url = String.IsNullOrEmpty(epi.ShowItem.CustomSearchURL) ? TheSearchers.CurrentSearchURL() : epi.ShowItem.CustomSearchURL;
            return CustomName.NameForNoExt(epi, url, true);
        }

        public string FilenameFriendly(string fn) {
            foreach (var replacement in Replacements) {
                if (replacement.CaseInsensitive) {
                    fn = Regex.Replace(fn, Regex.Escape(replacement.This), Regex.Escape(replacement.That), RegexOptions.IgnoreCase);
                } else {
                    fn = fn.Replace(replacement.This, replacement.That);
                }
            }
            if (ForceLowercaseFilenames) {
                fn = fn.ToLower();
            }
            return fn;
        }

        public string ItemForFolderJpg() {
            switch (FolderJpgIs) {
                case FolderJpgIsType.Banner:
                    return "banner";
                case FolderJpgIsType.FanArt:
                    return "fanart";
                default:
                    return "poster";
            }
        }
    }
}