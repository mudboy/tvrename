using System;
using System.Windows.Forms.VisualStyles;
using System.Xml.Serialization;
using TvRename.TheTVDB;

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

        public string Text {
            get {
                if (IsShowLevel && IsMeta) {
                    return string.Format("Show Seasons Status: {0}", StatusTextForDisplay);
                }
                if (!IsShowLevel && IsMeta) {
                    return string.Format("Season Status: {0}", StatusTextForDisplay);
                }
                if (IsShowLevel && !IsMeta) {
                    return string.Format("Show Status: {0}", StatusTextForDisplay);
                }
                return "";
            }
        }

        private string StatusTextForDisplay {
            get {
                if (!IsMeta) {
                    return ShowStatus;
                }
                if (IsShowLevel) {
                    ShowAirStatus status =
                        (ShowAirStatus) Enum.Parse(typeof (ShowAirStatus), ShowStatus);
                    switch (status) {
                        case ShowAirStatus.Aired:
                            return "All aired";
                        case ShowAirStatus.NoEpisodesOrSeasons:
                            return "No Seasons or Episodes in Seasons";
                        case ShowAirStatus.NoneAired:
                            return "None aired";
                        case ShowAirStatus.PartiallyAired:
                            return "Partially aired";
                        default:
                            return ShowStatus;
                    }
                } else {
                    Season.SeasonStatus status =
                        (Season.SeasonStatus) Enum.Parse(typeof (Season.SeasonStatus), ShowStatus);
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
                            return ShowStatus;
                    }
                }
            }
        }
    }
}