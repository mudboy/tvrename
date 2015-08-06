// 
// Main website for TVRename is http://tvrename.com
// 
// Source code available at http://code.google.com/p/tvrename/
// 
// This code is released under GPLv3 http://www.gnu.org/licenses/gpl.html
// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using TvRename.TheTVDB;
using TvRename.Utils;

// Settings for TVRename.  All of this stuff is through Options->Preferences in the app.

namespace TvRename.Core.Settings {
    public class Replacement {
        // used for invalid (and general) character (and string) replacements in filenames
        public bool CaseInsensitive;
        public string That;
        public string This;

        public Replacement(string a, string b, bool insens) {
            if (b == null) {
                b = "";
            }
            This = a;
            That = b;
            CaseInsensitive = insens;
        }
    }

    public class FilenameProcessorRE {
        // A regular expression to find the season and episode number in a filename
        public bool Enabled;
        public string Notes;
        public string RE;
        public bool UseFullPath;

        public FilenameProcessorRE(bool enabled, string re, bool useFullPath, string notes) {
            Enabled = enabled;
            RE = re;
            UseFullPath = useFullPath;
            Notes = notes;
        }
    }

    public class ShowStatusColoringTypeList : Dictionary<ShowStatusColoringType, System.Drawing.Color> {
        public bool IsShowStatusDefined(string showStatus) {
            foreach (var e in this) {
                if (!e.Key.IsMetaType && e.Key.IsShowLevel && e.Key.Status.Equals(showStatus, StringComparison.CurrentCultureIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }

        public System.Drawing.Color GetEntry(bool meta, bool showLevel, string status) {
            foreach (var e in this) {
                if (e.Key.IsMetaType == meta && e.Key.IsShowLevel == showLevel && e.Key.Status.Equals(status, StringComparison.CurrentCultureIgnoreCase)) {
                    return e.Value;
                }
            }
            return System.Drawing.Color.Empty;
        }
    }

    public class ShowStatusColoringType {
        public ShowStatusColoringType(bool isMetaType, bool isShowLevel, string status) {
            IsMetaType = isMetaType;
            IsShowLevel = isShowLevel;
            Status = status;
        }

        public bool IsMetaType;
        public bool IsShowLevel;
        public string Status;

        public string Text {
            get {
                if (IsShowLevel && IsMetaType) {
                    return string.Format("Show Seasons Status: {0}", StatusTextForDisplay);
                }
                if (!IsShowLevel && IsMetaType) {
                    return string.Format("Season Status: {0}", StatusTextForDisplay);
                }
                if (IsShowLevel && !IsMetaType) {
                    return string.Format("Show Status: {0}", StatusTextForDisplay);
                }
                return "";
            }
        }

        private string StatusTextForDisplay {
            get {
                if (!IsMetaType) {
                    return Status;
                }
                if (IsShowLevel) {
                    ShowItem.ShowAirStatus status =
                        (ShowItem.ShowAirStatus) Enum.Parse(typeof (ShowItem.ShowAirStatus), Status);
                    switch (status) {
                        case ShowItem.ShowAirStatus.Aired:
                            return "All aired";
                        case ShowItem.ShowAirStatus.NoEpisodesOrSeasons:
                            return "No Seasons or Episodes in Seasons";
                        case ShowItem.ShowAirStatus.NoneAired:
                            return "None aired";
                        case ShowItem.ShowAirStatus.PartiallyAired:
                            return "Partially aired";
                        default:
                            return Status;
                    }
                } else {
                    Season.SeasonStatus status =
                        (Season.SeasonStatus) Enum.Parse(typeof (Season.SeasonStatus), Status);
                    switch (status) {
                        case Season.SeasonStatus.Aired:
                            return "All aired";
                        case Season.SeasonStatus.NoEpisodes:
                            return "No Episodes";
                        case Season.SeasonStatus.NoneAired:
                            return "None aired";
                        case Season.SeasonStatus.PartiallyAired:
                            return "Partially aired";
                        default:
                            return Status;
                    }
                }
            }
        }
    }

    public class TVSettings {
        #region FolderJpgIsType enum

        public enum FolderJpgIsType {
            Banner,
            Poster,
            FanArt
        }

        public enum WTWDoubleClickAction {
            Search,
            Scan
        }

        #endregion

        public bool AutoSelectShowInMyShows = true;
        public bool BGDownload;
        public bool CheckuTorrent;
        public bool EpImgs;
        public bool ExportFOXML;
        public string ExportFOXMLTo = "";
        public bool ExportMissingCSV;
        public string ExportMissingCSVTo = "";
        public bool ExportMissingXML;
        public string ExportMissingXMLTo = "";
        public int ExportRSSMaxDays = 7;
        public int ExportRSSMaxShows = 10;
        public int ExportRSSDaysPast = 0;
        public bool ExportRenamingXML;
        public string ExportRenamingXMLTo = "";
        public bool ExportWTWRSS;
        public string ExportWTWRSSTo = "";
        public bool ExportWTWXML;
        public string ExportWTWXMLTo = "";
        public List<FilenameProcessorRE> FNPRegexs = DefaultFNPList();
        public bool FolderJpg;
        public FolderJpgIsType FolderJpgIs = FolderJpgIsType.Poster;
        public bool ForceLowercaseFilenames;
        public bool IgnoreSamples = true;
        public bool KeepTogether = true;
        public bool LeadingZeroOnSeason;
        public bool LeaveOriginals;
        public bool LookForDateInFilename;
        public bool MissingCheck = true;
        public bool NFOs;
        public bool pyTivoMeta;
        public bool pyTivoMetaSubFolder;
        public CustomName NamingStyle = new CustomName();
        public bool NotificationAreaIcon;
        public bool OfflineMode;
        public string OtherExtensionsString = "";

        public string[] OtherExtensionsArray {
            get { return OtherExtensionsString.Split(';'); }
        }

        public int ParallelDownloads = 4;
        public List<string> RSSURLs = DefaultRSSURLList();
        public bool RenameCheck = true;
        public bool RenameTxtToSub;
        public List<Replacement> Replacements = DefaultListRE();
        public string ResumeDatPath = "";
        public int SampleFileMaxSizeMB = 50; // sample file must be smaller than this to be ignored
        public bool SearchLocally = true;
        public bool SearchRSS;
        public bool ShowEpisodePictures = true;
        public bool ShowInTaskbar = true;
        public string SpecialsFolderName = "Specials";
        public int StartupTab;
        public Searchers TheSearchers = new Searchers();

        public string[] VideoExtensionsArray {
            get { return VideoExtensionsString.Split(';'); }
        }

        public string VideoExtensionsString = "";
        public int WTWRecentDays = 7;
        public string uTorrentPath = "";
        public bool ShouldMonitorFolders;
        public ShowStatusColoringTypeList ShowStatusColors = new ShowStatusColoringTypeList();
        public String SABHostPort = "";
        public String SABAPIKey = "";
        public bool CheckSABnzbd;
        public String PreferredLanguage = "en";
        public WTWDoubleClickAction WTWDoubleClick;

        public TVSettings() {
            SetToDefaults();
        }

        public TVSettings(XmlReader reader) {
            SetToDefaults();
            reader.Read();
            if (reader.Name != "Settings") {
                return; // bail out
            }
            reader.Read();
            while (!reader.EOF) {
                if ((reader.Name == "Settings") && !reader.IsStartElement()) {
                    break; // all done
                }
                if (reader.Name == "Searcher") {
                    string srch = reader.ReadElementContentAsString(); // and match it based on name...
                    TheSearchers.CurrentSearch = srch;
                } else {
                    if (reader.Name == "TheSearchers") {
                        TheSearchers = new Searchers(reader.ReadSubtree());
                        reader.Read();
                    } else {
                        if (reader.Name == "BGDownload") {
                            BGDownload = reader.ReadElementContentAsBoolean();
                        } else {
                            if (reader.Name == "OfflineMode") {
                                OfflineMode = reader.ReadElementContentAsBoolean();
                            } else {
                                if (reader.Name == "Replacements") {
                                    Replacements.Clear();
                                    reader.Read();
                                    while (!reader.EOF) {
                                        if ((reader.Name == "Replacements") && (!reader.IsStartElement())) {
                                            break;
                                        }
                                        if (reader.Name == "Replace") {
                                            Replacements.Add(new Replacement(reader.GetAttribute("This"), reader.GetAttribute("That"), reader.GetAttribute("CaseInsensitive") == "Y"));
                                            reader.Read();
                                        } else {
                                            reader.ReadOuterXml();
                                        }
                                    }
                                    reader.Read();
                                } else {
                                    if (reader.Name == "ExportWTWRSS") {
                                        ExportWTWRSS = reader.ReadElementContentAsBoolean();
                                    } else {
                                        if (reader.Name == "ExportWTWRSSTo") {
                                            ExportWTWRSSTo = reader.ReadElementContentAsString();
                                        } else {
                                            if (reader.Name == "ExportWTWXML") {
                                                ExportWTWXML = reader.ReadElementContentAsBoolean();
                                            } else {
                                                if (reader.Name == "ExportWTWXMLTo") {
                                                    ExportWTWXMLTo = reader.ReadElementContentAsString();
                                                } else {
                                                    if (reader.Name == "WTWRecentDays") {
                                                        WTWRecentDays = reader.ReadElementContentAsInt();
                                                    } else {
                                                        if (reader.Name == "StartupTab") {
                                                            int n = reader.ReadElementContentAsInt();
                                                            if (n == 6) {
                                                                StartupTab = 2; // WTW is moved
                                                            } else {
                                                                if ((n >= 1) && (n <= 3)) // any of the three scans
                                                                {
                                                                    StartupTab = 1;
                                                                } else {
                                                                    StartupTab = 0; // otherwise, My Shows
                                                                }
                                                            }
                                                        } else {
                                                            if (reader.Name == "StartupTab2") {
                                                                StartupTab = TabNumberFromName(reader.ReadElementContentAsString());
                                                            } else {
                                                                if (reader.Name == "DefaultNamingStyle") // old naming style
                                                                {
                                                                    NamingStyle.StyleString = CustomName.OldNStyle(reader.ReadElementContentAsInt());
                                                                } else {
                                                                    if (reader.Name == "NamingStyle") {
                                                                        NamingStyle.StyleString = reader.ReadElementContentAsString();
                                                                    } else {
                                                                        if (reader.Name == "NotificationAreaIcon") {
                                                                            NotificationAreaIcon = reader.ReadElementContentAsBoolean();
                                                                        } else {
                                                                            if ((reader.Name == "GoodExtensions") || (reader.Name == "VideoExtensions")) {
                                                                                VideoExtensionsString = reader.ReadElementContentAsString();
                                                                            } else {
                                                                                if (reader.Name == "OtherExtensions") {
                                                                                    OtherExtensionsString = reader.ReadElementContentAsString();
                                                                                } else {
                                                                                    if (reader.Name == "ExportRSSMaxDays") {
                                                                                        ExportRSSMaxDays = reader.ReadElementContentAsInt();
                                                                                    } else {
                                                                                        if (reader.Name == "ExportRSSMaxShows") {
                                                                                            ExportRSSMaxShows = reader.ReadElementContentAsInt();
                                                                                        } else {
                                                                                            if (reader.Name == "ExportRSSDaysPast") {
                                                                                                ExportRSSDaysPast = reader.ReadElementContentAsInt();
                                                                                            } else {
                                                                                                if (reader.Name == "KeepTogether") {
                                                                                                    KeepTogether = reader.ReadElementContentAsBoolean();
                                                                                                } else {
                                                                                                    if (reader.Name == "LeadingZeroOnSeason") {
                                                                                                        LeadingZeroOnSeason = reader.ReadElementContentAsBoolean();
                                                                                                    } else {
                                                                                                        if (reader.Name == "ShowInTaskbar") {
                                                                                                            ShowInTaskbar = reader.ReadElementContentAsBoolean();
                                                                                                        } else {
                                                                                                            if (reader.Name == "RenameTxtToSub") {
                                                                                                                RenameTxtToSub = reader.ReadElementContentAsBoolean();
                                                                                                            } else {
                                                                                                                if (reader.Name == "ShowEpisodePictures") {
                                                                                                                    ShowEpisodePictures = reader.ReadElementContentAsBoolean();
                                                                                                                } else {
                                                                                                                    if (reader.Name == "AutoSelectShowInMyShows") {
                                                                                                                        AutoSelectShowInMyShows = reader.ReadElementContentAsBoolean();
                                                                                                                    } else {
                                                                                                                        if (reader.Name == "SpecialsFolderName") {
                                                                                                                            SpecialsFolderName = reader.ReadElementContentAsString();
                                                                                                                        } else {
                                                                                                                            if (reader.Name == "SABAPIKey") {
                                                                                                                                SABAPIKey = reader.ReadElementContentAsString();
                                                                                                                            } else {
                                                                                                                                if (reader.Name == "CheckSABnzbd") {
                                                                                                                                    CheckSABnzbd = reader.ReadElementContentAsBoolean();
                                                                                                                                } else {
                                                                                                                                    if (reader.Name == "SABHostPort") {
                                                                                                                                        SABHostPort = reader.ReadElementContentAsString();
                                                                                                                                    } else {
                                                                                                                                        if (reader.Name == "PreferredLanguage") {
                                                                                                                                            PreferredLanguage = reader.ReadElementContentAsString();
                                                                                                                                        } else {
                                                                                                                                            if (reader.Name == "WTWDoubleClick") {
                                                                                                                                                WTWDoubleClick = (WTWDoubleClickAction) reader.ReadElementContentAsInt();
                                                                                                                                            } else {
                                                                                                                                                if (reader.Name == "ExportMissingXML") {
                                                                                                                                                    ExportMissingXML = reader.ReadElementContentAsBoolean();
                                                                                                                                                } else {
                                                                                                                                                    if (reader.Name == "ExportMissingXMLTo") {
                                                                                                                                                        ExportMissingXMLTo = reader.ReadElementContentAsString();
                                                                                                                                                    } else {
                                                                                                                                                        if (reader.Name == "ExportMissingCSV") {
                                                                                                                                                            ExportMissingCSV = reader.ReadElementContentAsBoolean();
                                                                                                                                                        } else {
                                                                                                                                                            if (reader.Name == "ExportMissingCSVTo") {
                                                                                                                                                                ExportMissingCSVTo = reader.ReadElementContentAsString();
                                                                                                                                                            } else {
                                                                                                                                                                if (reader.Name == "ExportRenamingXML") {
                                                                                                                                                                    ExportRenamingXML = reader.ReadElementContentAsBoolean();
                                                                                                                                                                } else {
                                                                                                                                                                    if (reader.Name == "ExportRenamingXMLTo") {
                                                                                                                                                                        ExportRenamingXMLTo = reader.ReadElementContentAsString();
                                                                                                                                                                    } else {
                                                                                                                                                                        if (reader.Name == "ExportFOXML") {
                                                                                                                                                                            ExportFOXML = reader.ReadElementContentAsBoolean();
                                                                                                                                                                        } else {
                                                                                                                                                                            if (reader.Name == "ExportFOXMLTo") {
                                                                                                                                                                                ExportFOXMLTo = reader.ReadElementContentAsString();
                                                                                                                                                                            } else {
                                                                                                                                                                                if (reader.Name == "ForceLowercaseFilenames") {
                                                                                                                                                                                    ForceLowercaseFilenames = reader.ReadElementContentAsBoolean();
                                                                                                                                                                                } else {
                                                                                                                                                                                    if (reader.Name == "IgnoreSamples") {
                                                                                                                                                                                        IgnoreSamples = reader.ReadElementContentAsBoolean();
                                                                                                                                                                                    } else {
                                                                                                                                                                                        if (reader.Name == "SampleFileMaxSizeMB") {
                                                                                                                                                                                            SampleFileMaxSizeMB = reader.ReadElementContentAsInt();
                                                                                                                                                                                        } else {
                                                                                                                                                                                            if (reader.Name == "ParallelDownloads") {
                                                                                                                                                                                                ParallelDownloads = reader.ReadElementContentAsInt();
                                                                                                                                                                                            } else {
                                                                                                                                                                                                if (reader.Name == "uTorrentPath") {
                                                                                                                                                                                                    uTorrentPath = reader.ReadElementContentAsString();
                                                                                                                                                                                                } else {
                                                                                                                                                                                                    if (reader.Name == "ResumeDatPath") {
                                                                                                                                                                                                        ResumeDatPath = reader.ReadElementContentAsString();
                                                                                                                                                                                                    } else {
                                                                                                                                                                                                        if (reader.Name == "SearchRSS") {
                                                                                                                                                                                                            SearchRSS = reader.ReadElementContentAsBoolean();
                                                                                                                                                                                                        } else {
                                                                                                                                                                                                            if (reader.Name == "EpImgs") {
                                                                                                                                                                                                                EpImgs = reader.ReadElementContentAsBoolean();
                                                                                                                                                                                                            } else {
                                                                                                                                                                                                                if (reader.Name == "NFOs") {
                                                                                                                                                                                                                    NFOs = reader.ReadElementContentAsBoolean();
                                                                                                                                                                                                                } else {
                                                                                                                                                                                                                    if (reader.Name == "pyTivoMeta") {
                                                                                                                                                                                                                        pyTivoMeta = reader.ReadElementContentAsBoolean();
                                                                                                                                                                                                                    } else {
                                                                                                                                                                                                                        if (reader.Name == "pyTivoMetaSubFolder") {
                                                                                                                                                                                                                            pyTivoMetaSubFolder = reader.ReadElementContentAsBoolean();
                                                                                                                                                                                                                        } else {
                                                                                                                                                                                                                            if (reader.Name == "FolderJpg") {
                                                                                                                                                                                                                                FolderJpg = reader.ReadElementContentAsBoolean();
                                                                                                                                                                                                                            } else {
                                                                                                                                                                                                                                if (reader.Name == "FolderJpgIs") {
                                                                                                                                                                                                                                    FolderJpgIs = (FolderJpgIsType) reader.ReadElementContentAsInt();
                                                                                                                                                                                                                                } else {
                                                                                                                                                                                                                                    if (reader.Name == "RenameCheck") {
                                                                                                                                                                                                                                        RenameCheck = reader.ReadElementContentAsBoolean();
                                                                                                                                                                                                                                    } else {
                                                                                                                                                                                                                                        if (reader.Name == "CheckuTorrent") {
                                                                                                                                                                                                                                            CheckuTorrent = reader.ReadElementContentAsBoolean();
                                                                                                                                                                                                                                        } else {
                                                                                                                                                                                                                                            if (reader.Name == "MissingCheck") {
                                                                                                                                                                                                                                                MissingCheck = reader.ReadElementContentAsBoolean();
                                                                                                                                                                                                                                            } else {
                                                                                                                                                                                                                                                if (reader.Name == "SearchLocally") {
                                                                                                                                                                                                                                                    SearchLocally = reader.ReadElementContentAsBoolean();
                                                                                                                                                                                                                                                } else {
                                                                                                                                                                                                                                                    if (reader.Name == "LeaveOriginals") {
                                                                                                                                                                                                                                                        LeaveOriginals = reader.ReadElementContentAsBoolean();
                                                                                                                                                                                                                                                    } else {
                                                                                                                                                                                                                                                        if (reader.Name == "LookForDateInFilename") {
                                                                                                                                                                                                                                                            LookForDateInFilename = reader.ReadElementContentAsBoolean();
                                                                                                                                                                                                                                                        } else {
                                                                                                                                                                                                                                                            if (reader.Name == "MonitorFolders") {
                                                                                                                                                                                                                                                                ShouldMonitorFolders = reader.ReadElementContentAsBoolean();
                                                                                                                                                                                                                                                            } else {
                                                                                                                                                                                                                                                                if (reader.Name == "FNPRegexs") {
                                                                                                                                                                                                                                                                    FNPRegexs.Clear();
                                                                                                                                                                                                                                                                    reader.Read();
                                                                                                                                                                                                                                                                    while (!reader.EOF) {
                                                                                                                                                                                                                                                                        if ((reader.Name == "FNPRegexs") && (!reader.IsStartElement())) {
                                                                                                                                                                                                                                                                            break;
                                                                                                                                                                                                                                                                        }
                                                                                                                                                                                                                                                                        if (reader.Name == "Regex") {
                                                                                                                                                                                                                                                                            string s = reader.GetAttribute("Enabled");
                                                                                                                                                                                                                                                                            bool en = s == null || bool.Parse(s);
                                                                                                                                                                                                                                                                            FNPRegexs.Add(new FilenameProcessorRE(en, reader.GetAttribute("RE"), bool.Parse(reader.GetAttribute("UseFullPath")), reader.GetAttribute("Notes")));
                                                                                                                                                                                                                                                                            reader.Read();
                                                                                                                                                                                                                                                                        } else {
                                                                                                                                                                                                                                                                            reader.ReadOuterXml();
                                                                                                                                                                                                                                                                        }
                                                                                                                                                                                                                                                                    }
                                                                                                                                                                                                                                                                    reader.Read();
                                                                                                                                                                                                                                                                } else {
                                                                                                                                                                                                                                                                    if (reader.Name == "RSSURLs") {
                                                                                                                                                                                                                                                                        RSSURLs.Clear();
                                                                                                                                                                                                                                                                        reader.Read();
                                                                                                                                                                                                                                                                        while (!reader.EOF) {
                                                                                                                                                                                                                                                                            if ((reader.Name == "RSSURLs") && (!reader.IsStartElement())) {
                                                                                                                                                                                                                                                                                break;
                                                                                                                                                                                                                                                                            }
                                                                                                                                                                                                                                                                            if (reader.Name == "URL") {
                                                                                                                                                                                                                                                                                RSSURLs.Add(reader.ReadElementContentAsString());
                                                                                                                                                                                                                                                                            } else {
                                                                                                                                                                                                                                                                                reader.ReadOuterXml();
                                                                                                                                                                                                                                                                            }
                                                                                                                                                                                                                                                                        }
                                                                                                                                                                                                                                                                        reader.Read();
                                                                                                                                                                                                                                                                    } else {
                                                                                                                                                                                                                                                                        if (reader.Name == "ShowStatusTVWColors") {
                                                                                                                                                                                                                                                                            ShowStatusColors = new ShowStatusColoringTypeList();
                                                                                                                                                                                                                                                                            reader.Read();
                                                                                                                                                                                                                                                                            while (!reader.EOF) {
                                                                                                                                                                                                                                                                                if ((reader.Name == "ShowStatusTVWColors") && (!reader.IsStartElement())) {
                                                                                                                                                                                                                                                                                    break;
                                                                                                                                                                                                                                                                                }
                                                                                                                                                                                                                                                                                if (reader.Name == "ShowStatusTVWColor") {
                                                                                                                                                                                                                                                                                    ShowStatusColoringType type = null;
                                                                                                                                                                                                                                                                                    try {
                                                                                                                                                                                                                                                                                        string showStatus = reader.GetAttribute("ShowStatus");
                                                                                                                                                                                                                                                                                        bool isMeta = bool.Parse(reader.GetAttribute("IsMeta"));
                                                                                                                                                                                                                                                                                        bool isShowLevel = bool.Parse(reader.GetAttribute("IsShowLevel"));
                                                                                                                                                                                                                                                                                        type = new ShowStatusColoringType(isMeta, isShowLevel, showStatus);
                                                                                                                                                                                                                                                                                    } catch {}
                                                                                                                                                                                                                                                                                    string color = reader.GetAttribute("Color");
                                                                                                                                                                                                                                                                                    if (type != null && !string.IsNullOrEmpty(color)) {
                                                                                                                                                                                                                                                                                        try {
                                                                                                                                                                                                                                                                                            System.Drawing.Color c = System.Drawing.ColorTranslator.FromHtml(color);
                                                                                                                                                                                                                                                                                            ShowStatusColors.Add(type, c);
                                                                                                                                                                                                                                                                                        } catch {}
                                                                                                                                                                                                                                                                                    }
                                                                                                                                                                                                                                                                                    reader.Read();
                                                                                                                                                                                                                                                                                } else {
                                                                                                                                                                                                                                                                                    reader.ReadOuterXml();
                                                                                                                                                                                                                                                                                }
                                                                                                                                                                                                                                                                            }
                                                                                                                                                                                                                                                                            reader.Read();
                                                                                                                                                                                                                                                                        } else {
                                                                                                                                                                                                                                                                            reader.ReadOuterXml();
                                                                                                                                                                                                                                                                        }
                                                                                                                                                                                                                                                                    }
                                                                                                                                                                                                                                                                }
                                                                                                                                                                                                                                                            }
                                                                                                                                                                                                                                                        }
                                                                                                                                                                                                                                                    }
                                                                                                                                                                                                                                                }
                                                                                                                                                                                                                                            }
                                                                                                                                                                                                                                        }
                                                                                                                                                                                                                                    }
                                                                                                                                                                                                                                }
                                                                                                                                                                                                                            }
                                                                                                                                                                                                                        }
                                                                                                                                                                                                                    }
                                                                                                                                                                                                                }
                                                                                                                                                                                                            }
                                                                                                                                                                                                        }
                                                                                                                                                                                                    }
                                                                                                                                                                                                }
                                                                                                                                                                                            }
                                                                                                                                                                                        }
                                                                                                                                                                                    }
                                                                                                                                                                                }
                                                                                                                                                                            }
                                                                                                                                                                        }
                                                                                                                                                                    }
                                                                                                                                                                }
                                                                                                                                                            }
                                                                                                                                                        }
                                                                                                                                                    }
                                                                                                                                                }
                                                                                                                                            }
                                                                                                                                        }
                                                                                                                                    }
                                                                                                                                }
                                                                                                                            }
                                                                                                                        }
                                                                                                                    }
                                                                                                                }
                                                                                                            }
                                                                                                        }
                                                                                                    }
                                                                                                }
                                                                                            }
                                                                                        }
                                                                                    }
                                                                                }
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void SetToDefaults() {
            // defaults that aren't handled with default initialisers
            VideoExtensionsString = ".avi;.mpg;.mpeg;.mkv;.mp4;.wmv;.divx;.ogm;.qt;.rm";
            OtherExtensionsString = ".srt;.nfo;.txt;.tbn";

            // have a guess at utorrent's path
            string[] guesses = new string[3];
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

        public void WriteXML(XmlWriter writer) {
            writer.WriteStartElement("Settings");
            TheSearchers.WriteXML(writer);
            writer.WriteStartElement("BGDownload");
            writer.WriteValue(BGDownload);
            writer.WriteEndElement();
            writer.WriteStartElement("OfflineMode");
            writer.WriteValue(OfflineMode);
            writer.WriteEndElement();
            writer.WriteStartElement("Replacements");
            foreach (Replacement R in Replacements) {
                writer.WriteStartElement("Replace");
                writer.WriteStartAttribute("This");
                writer.WriteValue(R.This);
                writer.WriteEndAttribute();
                writer.WriteStartAttribute("That");
                writer.WriteValue(R.That);
                writer.WriteEndAttribute();
                writer.WriteStartAttribute("CaseInsensitive");
                writer.WriteValue(R.CaseInsensitive ? "Y" : "N");
                writer.WriteEndAttribute();
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteStartElement("ExportWTWRSS");
            writer.WriteValue(ExportWTWRSS);
            writer.WriteEndElement();
            writer.WriteStartElement("ExportWTWRSSTo");
            writer.WriteValue(ExportWTWRSSTo);
            writer.WriteEndElement();
            writer.WriteStartElement("ExportWTWXML");
            writer.WriteValue(ExportWTWXML);
            writer.WriteEndElement();
            writer.WriteStartElement("ExportWTWXMLTo");
            writer.WriteValue(ExportWTWXMLTo);
            writer.WriteEndElement();
            writer.WriteStartElement("WTWRecentDays");
            writer.WriteValue(WTWRecentDays);
            writer.WriteEndElement();
            writer.WriteStartElement("ExportMissingXML");
            writer.WriteValue(ExportMissingXML);
            writer.WriteEndElement();
            writer.WriteStartElement("ExportMissingXMLTo");
            writer.WriteValue(ExportMissingXMLTo);
            writer.WriteEndElement();
            writer.WriteStartElement("ExportMissingCSV");
            writer.WriteValue(ExportMissingCSV);
            writer.WriteEndElement();
            writer.WriteStartElement("ExportMissingCSVTo");
            writer.WriteValue(ExportMissingCSVTo);
            writer.WriteEndElement();
            writer.WriteStartElement("ExportRenamingXML");
            writer.WriteValue(ExportRenamingXML);
            writer.WriteEndElement();
            writer.WriteStartElement("ExportRenamingXMLTo");
            writer.WriteValue(ExportRenamingXMLTo);
            writer.WriteEndElement();
            writer.WriteStartElement("ExportFOXML");
            writer.WriteValue(ExportFOXML);
            writer.WriteEndElement();
            writer.WriteStartElement("ExportFOXMLTo");
            writer.WriteValue(ExportFOXMLTo);
            writer.WriteEndElement();
            writer.WriteStartElement("StartupTab2");
            writer.WriteValue(TabNameForNumber(StartupTab));
            writer.WriteEndElement();
            writer.WriteStartElement("NamingStyle");
            writer.WriteValue(NamingStyle.StyleString);
            writer.WriteEndElement();
            writer.WriteStartElement("NotificationAreaIcon");
            writer.WriteValue(NotificationAreaIcon);
            writer.WriteEndElement();
            writer.WriteStartElement("VideoExtensions");
            writer.WriteValue(VideoExtensionsString);
            writer.WriteEndElement();
            writer.WriteStartElement("OtherExtensions");
            writer.WriteValue(OtherExtensionsString);
            writer.WriteEndElement();
            writer.WriteStartElement("ExportRSSMaxDays");
            writer.WriteValue(ExportRSSMaxDays);
            writer.WriteEndElement();
            writer.WriteStartElement("ExportRSSMaxShows");
            writer.WriteValue(ExportRSSMaxShows);
            writer.WriteEndElement();
            writer.WriteStartElement("ExportRSSDaysPast");
            writer.WriteValue(ExportRSSDaysPast);
            writer.WriteEndElement();
            writer.WriteStartElement("KeepTogether");
            writer.WriteValue(KeepTogether);
            writer.WriteEndElement();
            writer.WriteStartElement("LeadingZeroOnSeason");
            writer.WriteValue(LeadingZeroOnSeason);
            writer.WriteEndElement();
            writer.WriteStartElement("ShowInTaskbar");
            writer.WriteValue(ShowInTaskbar);
            writer.WriteEndElement();
            writer.WriteStartElement("IgnoreSamples");
            writer.WriteValue(IgnoreSamples);
            writer.WriteEndElement();
            writer.WriteStartElement("ForceLowercaseFilenames");
            writer.WriteValue(ForceLowercaseFilenames);
            writer.WriteEndElement();
            writer.WriteStartElement("RenameTxtToSub");
            writer.WriteValue(RenameTxtToSub);
            writer.WriteEndElement();
            writer.WriteStartElement("ParallelDownloads");
            writer.WriteValue(ParallelDownloads);
            writer.WriteEndElement();
            writer.WriteStartElement("AutoSelectShowInMyShows");
            writer.WriteValue(AutoSelectShowInMyShows);
            writer.WriteEndElement();
            writer.WriteStartElement("ShowEpisodePictures");
            writer.WriteValue(ShowEpisodePictures);
            writer.WriteEndElement();
            writer.WriteStartElement("SpecialsFolderName");
            writer.WriteValue(SpecialsFolderName);
            writer.WriteEndElement();
            writer.WriteStartElement("uTorrentPath");
            writer.WriteValue(uTorrentPath);
            writer.WriteEndElement();
            writer.WriteStartElement("ResumeDatPath");
            writer.WriteValue(ResumeDatPath);
            writer.WriteEndElement();
            writer.WriteStartElement("SearchRSS");
            writer.WriteValue(SearchRSS);
            writer.WriteEndElement();
            writer.WriteStartElement("EpImgs");
            writer.WriteValue(EpImgs);
            writer.WriteEndElement();
            writer.WriteStartElement("NFOs");
            writer.WriteValue(NFOs);
            writer.WriteEndElement();
            writer.WriteStartElement("pyTivoMeta");
            writer.WriteValue(pyTivoMeta);
            writer.WriteEndElement();
            writer.WriteStartElement("pyTivoMetaSubFolder");
            writer.WriteValue(pyTivoMetaSubFolder);
            writer.WriteEndElement();
            writer.WriteStartElement("FolderJpg");
            writer.WriteValue(FolderJpg);
            writer.WriteEndElement();
            writer.WriteStartElement("FolderJpgIs");
            writer.WriteValue((int) FolderJpgIs);
            writer.WriteEndElement();
            writer.WriteStartElement("CheckuTorrent");
            writer.WriteValue(CheckuTorrent);
            writer.WriteEndElement();
            writer.WriteStartElement("RenameCheck");
            writer.WriteValue(RenameCheck);
            writer.WriteEndElement();
            writer.WriteStartElement("MissingCheck");
            writer.WriteValue(MissingCheck);
            writer.WriteEndElement();
            writer.WriteStartElement("SearchLocally");
            writer.WriteValue(SearchLocally);
            writer.WriteEndElement();
            writer.WriteStartElement("LeaveOriginals");
            writer.WriteValue(LeaveOriginals);
            writer.WriteEndElement();
            writer.WriteStartElement("LookForDateInFilename");
            writer.WriteValue(LookForDateInFilename);
            writer.WriteEndElement();
            writer.WriteStartElement("MonitorFolders");
            writer.WriteValue(ShouldMonitorFolders);
            writer.WriteEndElement();
            writer.WriteStartElement("SABAPIKey");
            writer.WriteValue(SABAPIKey);
            writer.WriteEndElement();
            writer.WriteStartElement("CheckSABnzbd");
            writer.WriteValue(CheckSABnzbd);
            writer.WriteEndElement();
            writer.WriteStartElement("SABHostPort");
            writer.WriteValue(SABHostPort);
            writer.WriteEndElement();
            writer.WriteStartElement("PreferredLanguage");
            writer.WriteValue(PreferredLanguage);
            writer.WriteEndElement();
            writer.WriteStartElement("WTWDoubleClick");
            writer.WriteValue((int) WTWDoubleClick);
            writer.WriteEndElement();
            writer.WriteStartElement("FNPRegexs");
            foreach (FilenameProcessorRE re in FNPRegexs) {
                writer.WriteStartElement("Regex");
                writer.WriteStartAttribute("Enabled");
                writer.WriteValue(re.Enabled);
                writer.WriteEndAttribute();
                writer.WriteStartAttribute("RE");
                writer.WriteValue(re.RE);
                writer.WriteEndAttribute();
                writer.WriteStartAttribute("UseFullPath");
                writer.WriteValue(re.UseFullPath);
                writer.WriteEndAttribute();
                writer.WriteStartAttribute("Notes");
                writer.WriteValue(re.Notes);
                writer.WriteEndAttribute();
                writer.WriteEndElement(); // Regex
            }
            writer.WriteEndElement(); // FNPRegexs
            writer.WriteStartElement("RSSURLs");
            foreach (string s in RSSURLs) {
                writer.WriteStartElement("URL");
                writer.WriteValue(s);
                writer.WriteEndElement();
            }
            writer.WriteEndElement(); // RSSURL
            if (ShowStatusColors != null) {
                writer.WriteStartElement("ShowStatusTVWColors");
                foreach (KeyValuePair<ShowStatusColoringType, System.Drawing.Color> e in ShowStatusColors) {
                    writer.WriteStartElement("ShowStatusTVWColor");
                    // TODO ... Write Meta Flags
                    writer.WriteStartAttribute("IsMeta");
                    writer.WriteValue(e.Key.IsMetaType);
                    writer.WriteEndAttribute();
                    writer.WriteStartAttribute("IsShowLevel");
                    writer.WriteValue(e.Key.IsShowLevel);
                    writer.WriteEndAttribute();
                    writer.WriteStartAttribute("ShowStatus");
                    writer.WriteValue(e.Key.Status);
                    writer.WriteEndAttribute();
                    writer.WriteStartAttribute("Color");
                    writer.WriteValue(Helpers.TranslateColorToHtml(e.Value));
                    writer.WriteEndAttribute();
                    writer.WriteEndElement();
                }
                writer.WriteEndElement(); // ShowStatusTVWColors
            }
            writer.WriteEndElement(); // settings
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

        public string GetVideoExtensionsString() {
            return VideoExtensionsString;
        }

        public string GetOtherExtensionsString() {
            return OtherExtensionsString;
        }

        public static bool OKExtensionsString(string s) {
            if (string.IsNullOrEmpty(s)) {
                return true;
            }
            string[] t = s.Split(';');
            foreach (string s2 in t) {
                if ((string.IsNullOrEmpty(s2)) || (!s2.StartsWith("."))) {
                    return false;
                }
            }
            return true;
        }

        public static string CompulsoryReplacements() {
            return "*?<>:/\\|\""; // invalid filename characters, must be in the list!
        }

        public static List<FilenameProcessorRE> DefaultFNPList() {
            // Default list of filename processors
            List<FilenameProcessorRE> l = new List<FilenameProcessorRE> {
                new FilenameProcessorRE(true, "(^|[^a-z])s?(?<s>[0-9]+)[ex](?<e>[0-9]{2,})(e[0-9]{2,})*[^a-z]", false, "3x23 s3x23 3e23 s3e23 s04e01e02e03"),
                new FilenameProcessorRE(false, "(^|[^a-z])s?(?<s>[0-9]+)(?<e>[0-9]{2,})[^a-z]", false, "323 or s323 for season 3, episode 23. 2004 for season 20, episode 4."),
                new FilenameProcessorRE(false, "(^|[^a-z])s(?<s>[0-9]+)--e(?<e>[0-9]{2,})[^a-z]", false, "S02--E03"),
                new FilenameProcessorRE(false, "(^|[^a-z])s(?<s>[0-9]+) e(?<e>[0-9]{2,})[^a-z]", false, "'S02.E04' and 'S02 E04'"),
                new FilenameProcessorRE(false, "^(?<s>[0-9]+) (?<e>[0-9]{2,})", false, "filenames starting with '1.23' for season 1, episode 23"),
                new FilenameProcessorRE(true, "(^|[^a-z])(?<s>[0-9])(?<e>[0-9]{2,})[^a-z]", false, "Show - 323 - Foo"),
                new FilenameProcessorRE(true, "(^|[^a-z])se(?<s>[0-9]+)([ex]|ep|xep)?(?<e>[0-9]+)[^a-z]", false, "se3e23 se323 se1ep1 se01xep01..."),
                new FilenameProcessorRE(true, "(^|[^a-z])(?<s>[0-9]+)-(?<e>[0-9]{2,})[^a-z]", false, "3-23 EpName"),
                new FilenameProcessorRE(true, "(^|[^a-z])s(?<s>[0-9]+) +- +e(?<e>[0-9]{2,})[^a-z]", false, "ShowName - S01 - E01"),
                new FilenameProcessorRE(true, "\\b(?<e>[0-9]{2,}) ?- ?.* ?- ?(?<s>[0-9]+)", false, "like '13 - Showname - 2 - Episode Title.avi'"),
                new FilenameProcessorRE(true, "\\b(episode|ep|e) ?(?<e>[0-9]{2,}) ?- ?(series|season) ?(?<s>[0-9]+)", false, "episode 3 - season 4"),
                new FilenameProcessorRE(true, "season (?<s>[0-9]+)\\\\e?(?<e>[0-9]{1,3}) ?-", true, "Show Season 3\\E23 - Epname"),
                new FilenameProcessorRE(false, "season (?<s>[0-9]+)\\\\episode (?<e>[0-9]{1,3})", true, "Season 3\\Episode 23")
            };
            return l;
        }

        private static List<Replacement> DefaultListRE() {
            return new List<Replacement> {
                new Replacement("*", "#", false),
                new Replacement("?", "", false),
                new Replacement(">", "", false),
                new Replacement("<", "", false),
                new Replacement(":", "-", false),
                new Replacement("/", "-", false),
                new Replacement("\\", "-", false),
                new Replacement("|", "-", false),
                new Replacement("\"", "'", false)
            };
        }

        private static List<string> DefaultRSSURLList() {
            List<string> sl = new List<String> {
                "http://tvrss.net/feed/eztv"
            };
            return sl;
        }

        public static string[] TabNames() {
            return new[] {"MyShows", "Scan", "WTW"};
        }

        public static string TabNameForNumber(int n) {
            if ((n >= 0) && (n < TabNames().Length)) {
                return TabNames()[n];
            }
            return "";
        }

        public static int TabNumberFromName(string n) {
            int r = 0;
            if (!string.IsNullOrEmpty(n)) {
                r = Array.IndexOf(TabNames(), n);
            }
            if (r < 0) {
                r = 0;
            }
            return r;
        }

        public bool UsefulExtension(string sn, bool otherExtensionsToo) {
            foreach (string s in VideoExtensionsArray) {
                if (sn.ToLower() == s) {
                    return true;
                }
            }
            if (otherExtensionsToo) {
                foreach (string s in OtherExtensionsArray) {
                    if (sn.ToLower() == s) {
                        return true;
                    }
                }
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
            String url = String.IsNullOrEmpty(epi.SI.CustomSearchURL) ? TheSearchers.CurrentSearchURL() : epi.SI.CustomSearchURL;
            return CustomName.NameForNoExt(epi, url, true);
        }

        public string FilenameFriendly(string fn) {
            foreach (Replacement R in Replacements) {
                if (R.CaseInsensitive) {
                    fn = Regex.Replace(fn, Regex.Escape(R.This), Regex.Escape(R.That), RegexOptions.IgnoreCase);
                } else {
                    fn = fn.Replace(R.This, R.That);
                }
            }
            if (ForceLowercaseFilenames) {
                fn = fn.ToLower();
            }
            return fn;
        }

/*
        public static bool LoadXMLSettings(FileInfo settingsFile) {
            if (settingsFile == null || !settingsFile.Exists) {
                return true;
            }
            try {
                var settings = new XmlReaderSettings {
                    IgnoreComments = true,
                    IgnoreWhitespace = true
                };

                using (var reader = XmlReader.Create(settingsFile.FullName, settings)) {
                    ValidateSettingsFile(settingsFile, reader);

                    while (!reader.EOF) {
                        if (reader.Name == "TVRename" && !reader.IsStartElement()) {
                            break; // end of it all
                        }
                        if (reader.Name == "Settings") {
                            Settings = new TVSettings(reader.ReadSubtree());
                            reader.Read();
                        } else {
                            if (reader.Name == "MyShows") {
                                XmlReader r2 = reader.ReadSubtree();
                                r2.Read();
                                r2.Read();
                                while (!r2.EOF) {
                                    if ((r2.Name == "MyShows") && (!r2.IsStartElement())) {
                                        break;
                                    }
                                    if (r2.Name == "ShowItem") {
                                        ShowItem si = new ShowItem(mTVDB, r2.ReadSubtree(), Settings);
                                        if (si.UseCustomShowName) // see if custom show name is actually the real show name
                                        {
                                            SeriesInfo ser = si.TheSeries();
                                            if ((ser != null) && (si.CustomShowName == ser.Name)) {
                                                // then, turn it off
                                                si.CustomShowName = "";
                                                si.UseCustomShowName = false;
                                            }
                                        }
                                        ShowItems.Add(si);
                                        r2.Read();
                                    } else {
                                        r2.ReadOuterXml();
                                    }
                                }
                                reader.Read();
                            } else {
                                switch (reader.Name) {
                                    case "MonitorFolders":
                                        MonitorFolders = ReadStringsFromXml(reader, "MonitorFolders", "Folder");
                                        break;
                                    case "IgnoreFolders":
                                        IgnoreFolders = ReadStringsFromXml(reader, "IgnoreFolders", "Folder");
                                        break;
                                    case "FinderSearchFolders":
                                        SearchFolders = ReadStringsFromXml(reader, "FinderSearchFolders", "Folder");
                                        break;
                                    case "IgnoreItems":
                                        XmlReader r2 = reader.ReadSubtree();
                                        r2.Read();
                                        r2.Read();
                                        while (r2.Name == "Ignore") {
                                            Ignore.Add(new IgnoreItem(r2));
                                        }
                                        reader.Read();
                                        break;
                                    default:
                                        reader.ReadOuterXml();
                                        break;
                                }
                            }
                        }
                    }
                }
            } catch (Exception e) {
                throw new Exception(settingsFile.Name + " : " + e.Message, e);
            }
            try {
                mStats = TVRenameStats.Load();
            } catch (Exception) {
                // not worried if stats loading fails
            }
            return true;
        }

*/
        private static void ValidateSettingsFile(FileInfo file, XmlReader reader) {
            reader.Read();
            if (reader.Name != "xml") {
                throw new Exception(file.Name + " : Not a valid XML file");
            }
            reader.Read();
            if (reader.Name != "TVRename") {
                throw new Exception(file.Name + " : Not a TVRename settings file");
            }
            if (reader.GetAttribute("Version") != "2.1") {
                throw new Exception(file.Name + " : Incompatible version");
            }
            reader.Read(); // move forward one
        }

        public static List<string> ReadStringsFromXml(XmlReader reader, string elementName, string stringName) {
            List<string> r = new List<String>();
            if (reader.Name != elementName) {
                return r; // uhoh
            }
            if (!reader.IsEmptyElement) {
                reader.Read();
                while (!reader.EOF) {
                    if ((reader.Name == elementName) && !reader.IsStartElement()) {
                        break;
                    }
                    if (reader.Name == stringName) {
                        r.Add(reader.ReadElementContentAsString());
                    } else {
                        reader.ReadOuterXml();
                    }
                }
            }
            reader.Read();
            return r;
        }
    }
}
