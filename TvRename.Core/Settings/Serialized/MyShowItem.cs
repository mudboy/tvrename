using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using TvRename.TheTVDB;

namespace TvRename.Core.Settings.Serialized {
    public class MyShowItem {
        public MyShowItem() {
            SetDefaults();
        }

        public MyShowItem(int tvDbCode) {
            SetDefaults();
            TVDBID = tvDbCode;
        }

        private void SetDefaults() {
            ManualFolderLocations = new Dictionary<int, List<string>>();
            SeasonEpisodes = new Dictionary<int, List<ProcessedEpisode>>();
            IgnoreSeasons = new List<int>();
            UseCustomShowName = false;
            CustomShowName = "";
            UseSequentialMatch = false;
            SeasonRules = new Dictionary<int, List<ShowRule>>();
            SeasonEpisodes = new Dictionary<int, List<ProcessedEpisode>>();
            ShowNextAirdate = true;
            TVDBID = -1;
            AutoAddNewSeasons = true;
            PadSeasonToTwoDigits = false;
            AutoAdd_FolderBase = "";
            AutoAdd_FolderPerSeason = true;
            AutoAdd_SeasonFolderName = "Season ";
            DoRename = true;
            DoMissingCheck = true;
            CountSpecials = false;
            DVDOrder = false;
            CustomSearchURL = "";
            ForceCheckNoAirdate = false;
            ForceCheckFuture = false;
        }

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

        [XmlIgnore] public Dictionary<int, List<ProcessedEpisode>> SeasonEpisodes; // built up by applying rules.
        [XmlIgnore] public Dictionary<int, List<String>> ManualFolderLocations;
        [XmlIgnore] public Dictionary<int, List<ShowRule>> SeasonRules;
        [XmlIgnore] public string AutoAdd_FolderBase; // TODO: use magical renaming tokens here
        [XmlIgnore] public bool AutoAdd_FolderPerSeason;
        [XmlIgnore] public string AutoAdd_SeasonFolderName; // TODO: use magical renaming tokens here

        public static int CompareShowItemNames(MyShowItem one, MyShowItem two) {
            string ones = one.ShowName; // + " " +one->SeasonNumber.ToString("D3");
            string twos = two.ShowName; // + " " +two->SeasonNumber.ToString("D3");
            return ones.CompareTo(twos);
        }

        private SeriesInfo _seriesInfo;

        public void SetSeriesInfo(SeriesInfo series) {
            _seriesInfo = series;
        }

        public SeriesInfo TheSeries() {
            return _seriesInfo;
        }

        public string ShowName {
            get {
                if (UseCustomShowName) {
                    return CustomShowName;
                }
                SeriesInfo ser = TheSeries();
                if (ser != null) {
                    return ser.Name;
                }
                return "<" + TVDBID + " not downloaded>";
            }
        }

        public string ShowStatus {
            get {
                SeriesInfo ser = TheSeries();
                if (ser != null && ser.Items != null && ser.Items.ContainsKey("Status")) {
                    return ser.Items["Status"];
                }
                return "Unknown";
            }
        }

        public Dictionary<int, List<string>> AllFolderLocations(TvSettings settings) {
            return AllFolderLocations(settings, true);
        }

        public Dictionary<int, List<string>> AllFolderLocations(TvSettings settings, bool manualToo) {
            var fld = new Dictionary<int, List<string>>();
            if (manualToo) {
                foreach (var kvp in ManualFolderLocations) {
                    if (!fld.ContainsKey(kvp.Key)) {
                        fld[kvp.Key] = new List<String>();
                    }
                    foreach (string s in kvp.Value) {
                        fld[kvp.Key].Add(TTS(s));
                    }
                }
            }
            if (AutoAddNewSeasons && (!string.IsNullOrEmpty(AutoAdd_FolderBase))) {
                int highestThereIs = -1;
                foreach (var kvp in SeasonEpisodes) {
                    if (kvp.Key > highestThereIs) {
                        highestThereIs = kvp.Key;
                    }
                }
                foreach (int i in SeasonEpisodes.Keys) {
                    if (IgnoreSeasons.Contains(i)) {
                        continue;
                    }
                    string newName = AutoFolderNameForSeason(i, settings);
                    if ((!string.IsNullOrEmpty(newName)) && (Directory.Exists(newName))) {
                        if (!fld.ContainsKey(i)) {
                            fld[i] = new List<String>();
                        }
                        if (!fld[i].Contains(newName)) {
                            fld[i].Add(TTS(newName));
                        }
                    }
                }
            }
            return fld;
        }

        public string AutoFolderNameForSeason(int n, TvSettings settings) {
            bool leadingZero = settings.LeadingZeroOnSeason || PadSeasonToTwoDigits;
            string r = AutoAdd_FolderBase;
            if (string.IsNullOrEmpty(r)) {
                return "";
            }
            if (!r.EndsWith(Path.DirectorySeparatorChar.ToString())) {
                r += Path.DirectorySeparatorChar.ToString();
            }
            if (AutoAdd_FolderPerSeason) {
                if (n == 0) {
                    r += settings.SpecialsFolderName;
                } else {
                    r += AutoAdd_SeasonFolderName;
                    if ((n < 10) && leadingZero) {
                        r += "0";
                    }
                    r += n.ToString();
                }
            }
            return r;
        }

        public static string TTS(string s) // trim trailing slash
        {
            return s.TrimEnd(System.IO.Path.DirectorySeparatorChar);
        }

        public int MaxSeason() {
            return SeasonEpisodes.Select(kvp => kvp.Key).Concat(new[] {0}).Max();
        }

        public List<ShowRule> RulesForSeason(int n) {
            if (SeasonRules.ContainsKey(n)) {
                return SeasonRules[n];
            }
            return null;
        }
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