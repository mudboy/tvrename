// 
// Main website for TVRename is http://tvrename.com
// 
// Source code available at http://code.google.com/p/tvrename/
// 
// This code is released under GPLv3 http://www.gnu.org/licenses/gpl.html
// 

using System;
using System.IO;
using System.Windows.Forms;
using TvRename.Core.Settings;
using TvRename.Core.Settings.Serialized;
using TvRename.TheTVDB;
using TvRename.Utils;

namespace TvRename.Core.Actions
{
    public class ActionDownload : Item, Action, ScanListItem
    {
        private readonly string BannerPath;
        private readonly TheTVDB.TheTVDB _tvdb;
        private readonly FileInfo Destination;
        private readonly MyShowItem SI;

        public ActionDownload(MyShowItem si, ProcessedEpisode pe, FileInfo dest, string bannerPath, TheTVDB.TheTVDB tvdb)
        {
            Episode = pe;
            SI = si;
            Destination = dest;
            BannerPath = bannerPath;
            _tvdb = tvdb;
        }

        #region Action Members

        public bool Done { get; set; }
        public bool Error { get; set; }
        public string ErrorText { get; set; }

        public string Name
        {
            get { return "Download"; }
        }

        public string ProgressText
        {
            get { return Destination.Name; }
        }

        public double PercentDone
        {
            get { return Done ? 100 : 0; }
        }

        // 0 to 100
        public long SizeOfWork
        {
            get { return 1000000; }
        }

        public bool Go(ref bool pause)
        {
            var theData = _tvdb.GetPage(BannerPath, false, typeMaskBits.tmBanner, false);
            if ((theData == null) || (theData.Length == 0))
            {
                ErrorText = "Unable to download " + BannerPath;
                Error = true;
                Done = true;
                return false;
            }

            try
            {
                FileStream fs = new FileStream(Destination.FullName, FileMode.Create);
                fs.Write(theData, 0, theData.Length);
                fs.Close();
            }
            catch (Exception e)
            {
                ErrorText = e.Message;
                Error = true;
                Done = true;
                return false;
            }
                

            Done = true;
            return true;
        }

        #endregion

        #region Item Members

        public bool SameAs(Item o)
        {
            return (o is ActionDownload) && ((o as ActionDownload).Destination == Destination);
        }

        public int Compare(Item o)
        {
            ActionDownload dl = o as ActionDownload;
            return dl == null ? 0 : Destination.FullName.CompareTo(dl.Destination.FullName);
        }

        #endregion

        #region ScanListItem Members

        public int IconNumber
        {
            get { return 5; }
        }

        public ProcessedEpisode Episode { get; set; }

        public IgnoreItem Ignore
        {
            get
            {
                if (Destination == null)
                    return null;
                return new IgnoreItem(Destination.FullName);
            }
        }

        public ListViewItem ScanListViewItem
        {
            get
            {
                ListViewItem lvi = new ListViewItem {
                                                        Text = (Episode != null) ? Episode.ShowItem.ShowName : ((SI != null) ? SI.ShowName : "")
                                                    };

                lvi.SubItems.Add(Episode != null ? Episode.SeasonNumber.ToString() : "");
                lvi.SubItems.Add(Episode != null ? Episode.NumsAsString() : "");

                if (Episode != null)
                {
                    DateTime? dt = Episode.GetAirDateDT(true);
                    if ((dt != null) && (dt.Value.CompareTo(DateTime.MaxValue) != 0))
                        lvi.SubItems.Add(dt.Value.ToShortDateString());
                    else
                        lvi.SubItems.Add("");
                }
                else
                    lvi.SubItems.Add("");

                lvi.SubItems.Add(Destination.DirectoryName);
                lvi.SubItems.Add(BannerPath);

                if (string.IsNullOrEmpty(BannerPath))
                    lvi.BackColor = Helpers.WarningColor();

                lvi.SubItems.Add(Destination.Name);

                lvi.Tag = this;

                return lvi;
            }
        }

        public int ScanListViewGroup
        {
            get { return 5; }
        }

        public string TargetFolder
        {
            get
            {
                if (Destination == null)
                    return null;
                return Destination.DirectoryName;
            }
        }

        #endregion
    }
}