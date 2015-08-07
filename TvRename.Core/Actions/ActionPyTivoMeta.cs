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

namespace TvRename.Core.Actions
{
    public class ActionPyTivoMeta : Item, Action, ScanListItem
    {
        public FileInfo Where;

        public ActionPyTivoMeta(FileInfo nfo, ProcessedEpisode pe)
        {
            Episode = pe;
            Where = nfo;
        }

        #region Action Members

        public string Name
        {
            get { return "Write pyTivo Meta"; }
        }

        public bool Done { get; private set; }
        public bool Error { get; private set; }
        public string ErrorText { get; set; }

        public string ProgressText
        {
            get { return Where.Name; }
        }

        public double PercentDone
        {
            get { return Done ? 100 : 0; }
        }

        public long SizeOfWork
        {
            get { return 10000; }
        }

        public bool Go(ref bool pause)
        {
            // "try" and silently fail.  eg. when file is use by other...
            StreamWriter writer;
            try
            {
                // create folder if it does not exist. (Only really applies when .meta\ folder is being used.)
                if (!Where.Directory.Exists)
                    Where.Directory.Create();
                writer = new StreamWriter(Where.FullName);
            }
            catch (Exception)
            {
                Done = true;
                return true;
            }

            // See: http://pytivo.sourceforge.net/wiki/index.php/Metadata
            writer.WriteLine("title : {0}", Episode.SI.ShowName);
            writer.WriteLine("seriesTitle : {0}", Episode.SI.ShowName);
            writer.WriteLine("episodeTitle : {0}", Episode.Name);
            writer.WriteLine("episodeNumber : {0}{1:0#}", Episode.SeasonNumber, Episode.EpNum);
            writer.WriteLine("isEpisode : true");
            writer.WriteLine("description : {0}", Episode.Overview);
            if (Episode.FirstAired != null)
                writer.WriteLine("originalAirDate : {0:yyyy-MM-dd}T00:00:00Z", Episode.FirstAired.Value);
            writer.WriteLine("callsign : {0}", Episode.SI.TheSeries().GetItem("Network"));

            WriteEntries(writer, "vDirector", Episode.EpisodeDirector);
            WriteEntries(writer, "vWriter", Episode.Writer);
            WriteEntries(writer, "vActor", Episode.SI.TheSeries().GetItem("Actors"));
            WriteEntries(writer, "vGuestStar", Episode.EpisodeGuestStars); // not worring about actors being repeated
            WriteEntries(writer, "vProgramGenre", Episode.SI.TheSeries().GetItem("Genre"));

            writer.Close();
            Done = true;
            return true;
        }

        private void WriteEntries(StreamWriter writer, string Heading, string Entries)
        {
            if (string.IsNullOrEmpty(Entries))
                return;
            if (!Entries.Contains("|"))
                writer.WriteLine(string.Format("{0} : {1}", Heading, Entries));
            else
            {
                foreach (string entry in Entries.Split('|'))
                    if (!string.IsNullOrEmpty(entry))
                        writer.WriteLine(string.Format("{0} : {1}", Heading, entry));
            }
        }

        #endregion

        #region Item Members

        public bool SameAs(Item o)
        {
            return (o is ActionPyTivoMeta) && ((o as ActionPyTivoMeta).Where == Where);
        }

        public int Compare(Item o)
        {
            ActionPyTivoMeta nfo = o as ActionPyTivoMeta;

            if (Episode == null)
                return 1;
            if (nfo ==null || nfo.Episode == null)
                return -1;
            return (Where.FullName + Episode.Name).CompareTo(nfo.Where.FullName + nfo.Episode.Name);
        }

        #endregion

        #region ScanListItem Members

        public IgnoreItem Ignore
        {
            get
            {
                if (Where == null)
                    return null;
                return new IgnoreItem(Where.FullName);
            }
        }

        public ListViewItem ScanListViewItem
        {
            get
            {
                ListViewItem lvi = new ListViewItem();

                lvi.Text = Episode.SI.ShowName;
                lvi.SubItems.Add(Episode.SeasonNumber.ToString());
                lvi.SubItems.Add(Episode.NumsAsString());
                DateTime? dt = Episode.GetAirDateDT(true);
                if ((dt != null) && (dt.Value.CompareTo(DateTime.MaxValue)) != 0)
                    lvi.SubItems.Add(dt.Value.ToShortDateString());
                else
                    lvi.SubItems.Add("");

                lvi.SubItems.Add(Where.DirectoryName);
                lvi.SubItems.Add(Where.Name);

                lvi.Tag = this;

                //lv->Items->Add(lvi);
                return lvi;
            }
        }

        string ScanListItem.TargetFolder
        {
            get
            {
                if (Where == null)
                    return null;
                return Where.DirectoryName;
            }
        }

        public int ScanListViewGroup
        {
            get { return 7; }
        }

        public int IconNumber
        {
            get { return 7; }
        }

        public ProcessedEpisode Episode { get; private set; }

        #endregion

    }
}