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
using System.Xml;
using TvRename.Core.Settings;
using TvRename.Core.Settings.Serialized;

namespace TvRename.Core.Actions
{
    public class ActionNFO : Item, Action, ScanListItem
    {
        public MyShowItem SI; // if for an entire show, rather than specific episode
        public FileInfo Where;
        private readonly TheTVDB.TheTVDB _tvdb;

        public ActionNFO(TheTVDB.TheTVDB tvdb, FileInfo nfo, ProcessedEpisode pe)
        {
            SI = null;
            Episode = pe;
            Where = nfo;
            _tvdb = tvdb;
        }

        public ActionNFO(FileInfo nfo, MyShowItem si, TheTVDB.TheTVDB tvdb)
        {
            SI = si;
            _tvdb = tvdb;
            Episode = null;
            Where = nfo;
        }

        #region Action Members

        public string Name
        {
            get { return "Write NFO"; }
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
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                NewLineOnAttributes = true
            };
            // "try" and silently fail.  eg. when file is use by other...
            XmlWriter writer;
            try
            {
                //                XmlWriter writer = XmlWriter.Create(Where.FullName, settings);
                writer = XmlWriter.Create(Where.FullName, settings);
                if (writer == null)
                    return false;
            }
            catch (Exception)
            {
                Done = true;
                return true;
            }

            if (Episode != null) // specific episode
            {
                // See: http://xbmc.org/wiki/?title=Import_-_Export_Library#TV_Episodes
                // See: http://kodi.wiki/view/NFO_files/tvepisodes todo serialize this
                writer.WriteStartElement("episodedetails");
                writer.WriteStartElement("title");
                writer.WriteValue(Episode.Name);
                writer.WriteEndElement();
                writer.WriteStartElement("rating");
                writer.WriteValue(Episode.EpisodeRating);
                writer.WriteEndElement();
                writer.WriteStartElement("season");
                writer.WriteValue(Episode.SeasonNumber);
                writer.WriteEndElement();
                writer.WriteStartElement("episode");
                writer.WriteValue(Episode.EpNum);
                writer.WriteEndElement();
                writer.WriteStartElement("plot");
                writer.WriteValue(Episode.Overview);
                writer.WriteEndElement();
                writer.WriteStartElement("aired");
                if (Episode.FirstAired != null)
                    writer.WriteValue(Episode.FirstAired.Value.ToString("yyyy-MM-dd"));
                writer.WriteEndElement();

                if (Episode.SI != null)
                {
                    WriteInfo(writer, Episode.SI, "ContentRating", "mpaa");
                }

                //Director(s)
                if (!String.IsNullOrEmpty(Episode.EpisodeDirector))
                {
                    string EpDirector = Episode.EpisodeDirector;
                    if (!string.IsNullOrEmpty(EpDirector))
                    {
                        foreach (string Daa in EpDirector.Split('|'))
                        {
                            if (string.IsNullOrEmpty(Daa))
                                continue;

                            writer.WriteStartElement("director");
                            writer.WriteValue(Daa);
                            writer.WriteEndElement();
                        }
                    }
                }

                //Writers(s)
                if (!String.IsNullOrEmpty(Episode.Writer))
                {
                    string EpWriter = Episode.Writer;
                    if (!string.IsNullOrEmpty(EpWriter))
                    {
                        writer.WriteStartElement("credits");
                        writer.WriteValue(EpWriter);
                        writer.WriteEndElement();
                    }
                }

                // Guest Stars...
                if (!String.IsNullOrEmpty(Episode.EpisodeGuestStars))
                {
                    string RecurringActors = "";

                    if (Episode.SI != null)
                    {
                        RecurringActors = Episode.SI.TheSeries().GetItem("Actors");
                    }

                    string GuestActors = Episode.EpisodeGuestStars;
                    if (!string.IsNullOrEmpty(GuestActors))
                    {
                        foreach (string Gaa in GuestActors.Split('|'))
                        {
                            if (string.IsNullOrEmpty(Gaa))
                                continue;

                            // Skip if the guest actor is also in the overal recurring list
                            if (!string.IsNullOrEmpty(RecurringActors) && RecurringActors.Contains(Gaa))
                            {
                                continue;
                            }

                            writer.WriteStartElement("actor");
                            writer.WriteStartElement("name");
                            writer.WriteValue(Gaa);
                            writer.WriteEndElement(); // name
                            writer.WriteEndElement(); // actor
                        }
                    }
                }

                // actors...
                if (Episode.SI != null)
                {
                    string actors = Episode.SI.TheSeries().GetItem("Actors");
                    if (!string.IsNullOrEmpty(actors))
                    {
                        foreach (string aa in actors.Split('|'))
                        {
                            if (string.IsNullOrEmpty(aa))
                                continue;

                            writer.WriteStartElement("actor");
                            writer.WriteStartElement("name");
                            writer.WriteValue(aa);
                            writer.WriteEndElement(); // name
                            writer.WriteEndElement(); // actor
                        }
                    }
                }

                writer.WriteEndElement(); // episodedetails
            }
            else if (SI != null) // show overview (tvshow.nfo)
            {
                // http://www.xbmc.org/wiki/?title=Import_-_Export_Library#TV_Shows

                writer.WriteStartElement("tvshow");

                writer.WriteStartElement("title");
                writer.WriteValue(SI.ShowName);
                writer.WriteEndElement();

                writer.WriteStartElement("episodeguideurl");
                writer.WriteValue(TheTVDB.TheTVDB.BuildURL(true, true, SI.TVDBID, _tvdb.RequestLanguage));
                writer.WriteEndElement();

                WriteInfo(writer, SI, "Overview", "plot");

                string genre = SI.TheSeries().GetItem("Genre");
                if (!string.IsNullOrEmpty(genre))
                {
                    genre = genre.Trim('|');
                    genre = genre.Replace("|", " / ");
                    writer.WriteStartElement("genre");
                    writer.WriteValue(genre);
                    writer.WriteEndElement();
                }

                WriteInfo(writer, SI, "FirstAired", "premiered");
                WriteInfo(writer, SI, "Year", "year");
                WriteInfo(writer, SI, "Rating", "rating");
                WriteInfo(writer, SI, "Status", "status");

                // actors...
                string actors = SI.TheSeries().GetItem("Actors");
                if (!string.IsNullOrEmpty(actors))
                {
                    foreach (string aa in actors.Split('|'))
                    {
                        if (string.IsNullOrEmpty(aa))
                            continue;

                        writer.WriteStartElement("actor");
                        writer.WriteStartElement("name");
                        writer.WriteValue(aa);
                        writer.WriteEndElement(); // name
                        writer.WriteEndElement(); // actor
                    }
                }

                WriteInfo(writer, SI, "ContentRating", "mpaa");
                WriteInfo(writer, SI, "IMDB_ID", "id", "moviedb","imdb");

                writer.WriteStartElement("tvdbid");
                writer.WriteValue(SI.TheSeries().TVDBCode);
                writer.WriteEndElement();

                string rt = SI.TheSeries().GetItem("Runtime");
                if (!string.IsNullOrEmpty(rt))
                {
                    writer.WriteStartElement("runtime");
                    writer.WriteValue(rt + " minutes");
                    writer.WriteEndElement();
                }

                writer.WriteEndElement(); // tvshow
            }

            writer.Close();
            Done = true;
            return true;
        }

        #endregion

        #region Item Members

        public bool SameAs(Item o)
        {
            return (o is ActionNFO) && ((o as ActionNFO).Where == Where);
        }

        public int Compare(Item o)
        {
            ActionNFO nfo = o as ActionNFO;

            if (Episode == null)
                return 1;
            if (nfo == null || nfo.Episode == null)
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

                if (Episode != null)
                {
                    lvi.Text = Episode.SI.ShowName;
                    lvi.SubItems.Add(Episode.SeasonNumber.ToString());
                    lvi.SubItems.Add(Episode.NumsAsString());
                    DateTime? dt = Episode.GetAirDateDT(true);
                    if ((dt != null) && (dt.Value.CompareTo(DateTime.MaxValue)) != 0)
                        lvi.SubItems.Add(dt.Value.ToShortDateString());
                    else
                        lvi.SubItems.Add("");
                }
                else
                {
                    lvi.Text = SI.ShowName;
                    lvi.SubItems.Add("");
                    lvi.SubItems.Add("");
                    lvi.SubItems.Add("");
                }

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
            get { return 6; }
        }

        public int IconNumber
        {
            get { return 7; }
        }

        public ProcessedEpisode Episode { get; private set; }

        #endregion

        private static void WriteInfo(XmlWriter writer, MyShowItem si, string whichItem, string elemName)
        {
            WriteInfo(writer, si, whichItem, elemName, null, null);
        }

        private static void WriteInfo(XmlWriter writer, MyShowItem si, string whichItem, string elemName, string attribute, string attributeVal)
        {
            string t = si.TheSeries().GetItem(whichItem);
            if (!string.IsNullOrEmpty(t))
            {
                writer.WriteStartElement(elemName);
                if (!String.IsNullOrEmpty(attribute) && !String.IsNullOrEmpty(attributeVal))
                {
                    writer.WriteAttributeString(attribute, attributeVal);
                }
                writer.WriteValue(t);
                writer.WriteEndElement();
            }
        }
    }
}