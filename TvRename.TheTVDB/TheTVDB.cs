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
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using Ionic.Zip;
using TvRename.Utils;

// Talk to the TheTVDB web API, and get tv series info

// Hierarchy is:
//   TheTVDB -> Series (class SeriesInfo) -> Seasons (class Season) -> Episodes (class Episode)

namespace TvRename.TheTVDB
{
    [Flags] 
    public enum typeMaskBits // defined by thetvdb for mirror types
    {
        tmMainSite = 0,
        tmXML = 1,
        tmBanner = 2,
        tmZIP = 4
    }

    public class TVDBException : System.Exception
    {
        // Thrown if an error occurs in the XML when reading TheTVDB.xml
        public TVDBException(String message)
            : base(message)
        {
        }
    }

    public class TheTVDB
    {
        public string BannerMirror;
        private FileInfo CacheFile;
        public bool Connected;
        public string CurrentDLTask;
        private List<ExtraEp> ExtraEpisodes; // IDs of extra episodes to grab and merge in on next update
        private List<int> ForceReloadOn;
        public Dictionary<string, string> LanguageList;
        public string LastError;
        public string LoadErr;
        public bool LoadOK;
        private long New_Srv_Time;
        private Dictionary<int, SeriesInfo> Series; // TODO: make this private or a property. have online/offline state that controls auto downloading of needed info.
        private long Srv_Time; // only update this after a 100% successful download
        // private List<String> WhoHasLock;
        public string XMLMirror;
        public string ZIPMirror;

        public String RequestLanguage = "en"; // Set and updated by TVDoc

        private CommandLineArgs Args;

        public TheTVDB(FileInfo loadFrom, FileInfo cacheFile, CommandLineArgs args)
        {
            Args = args;

            System.Diagnostics.Debug.Assert(cacheFile != null);
            CacheFile = cacheFile;

            LastError = "";
            // WhoHasLock = new List<String>();
            Connected = false;
            ExtraEpisodes = new List<ExtraEp>();

            LanguageList = new Dictionary<string, string>();
            LanguageList["en"] = "English";

            XMLMirror = "http://thetvdb.com";
            BannerMirror = "http://thetvdb.com";
            ZIPMirror = "http://thetvdb.com";

            Series = new Dictionary<int, SeriesInfo>();
            New_Srv_Time = Srv_Time = 0;

            LoadOK = (loadFrom == null) || LoadCache(loadFrom);

            ForceReloadOn = new List<int>();
        }

        private void LockEE()
        {
            Monitor.Enter(ExtraEpisodes);
        }

        private void UnlockEE()
        {
            Monitor.Exit(ExtraEpisodes);
        }

        public bool HasSeries(int id)
        {
            return Series.ContainsKey(id);
        }

        public SeriesInfo GetSeries(int id)
        {
            if (!HasSeries(id))
                return null;

            return Series[id];
        }

        public Dictionary<int, SeriesInfo> GetSeriesDict()
        {
            return Series;
        }

        public bool GetLock(string whoFor)
        {
            System.Diagnostics.Debug.Print("Lock Series for " + whoFor);
            bool ok = Monitor.TryEnter(Series, 10000);
            System.Diagnostics.Debug.Assert(ok);
            return ok;
            //            WhoHasLock->Add(whoFor);
        }

        public void Unlock(string whoFor)
        {
            //return;

            //            int n = WhoHasLock->Count - 1;
            //            String ^whoHad = WhoHasLock[n];
            //#if defined(DEBUG)
            //            System.Diagnostics::Debug::Assert(whoFor == whoHad);
            //#endif
            System.Diagnostics.Debug.Print("Unlock series (" + whoFor + ")");
                        // WhoHasLock->RemoveAt(n);
            //
                        Monitor.Exit(Series);
        }

        private void Say(string s)
        {
            CurrentDLTask = s;
        }

        public bool LoadCache(FileInfo loadFrom)
        {
            if ((loadFrom == null) || !loadFrom.Exists)
                return true; // that's ok

            FileStream fs = null;
            try
            {
                fs = loadFrom.Open(FileMode.Open);
                bool r = ProcessTVDBResponse(fs, null);
                fs.Close();
                fs = null;
                if (r)
                    UpdatesDoneOK();
                return r;
            }
            catch (Exception e)
            {
                LoadErr = loadFrom.Name + " : " + e.Message;

                if (fs != null)
                    fs.Close();

                fs = null;

                return false;
            }
        }

        public void UpdatesDoneOK()
        {
            // call when all downloading and updating is done.  updates local Srv_Time with the tentative
            // new_srv_time value.
            Srv_Time = New_Srv_Time;
        }

        public void SaveCache()
        {
            if (!GetLock("SaveCache"))
                return;

            if (CacheFile.Exists)
            {
                double hours = 999.9;
                if (File.Exists(CacheFile.FullName + ".0"))
                {
                    // see when the last rotate was, and only rotate if its been at least an hour since the last save
                    DateTime dt = File.GetLastWriteTime(CacheFile.FullName + ".0");
                    hours = DateTime.Now.Subtract(dt).TotalHours;
                }
                if (hours >= 24.0) // rotate the save file daily
                {
                    for (int i = 8; i >= 0; i--)
                    {
                        string fn = CacheFile.FullName + "." + i;
                        if (File.Exists(fn))
                        {
                            string fn2 = CacheFile.FullName + "." + (i + 1);
                            if (File.Exists(fn2))
                                File.Delete(fn2);
                            File.Move(fn, fn2);
                        }
                    }

                    File.Copy(CacheFile.FullName, CacheFile.FullName + ".0");
                }
            }

            // write ourselves to disc for next time.  use same structure as thetvdb.com (limited fields, though)
            // to make loading easy
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                NewLineOnAttributes = true
            };
            using (XmlWriter writer = XmlWriter.Create(CacheFile.FullName, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Data");
                writer.WriteStartAttribute("time");
                writer.WriteValue(Srv_Time);
                writer.WriteEndAttribute();

                foreach (KeyValuePair<int, SeriesInfo> kvp in Series)
                {
                    if (kvp.Value.Srv_LastUpdated != 0)
                    {
                        kvp.Value.WriteXml(writer);
                        foreach (KeyValuePair<int, Season> kvp2 in kvp.Value.Seasons)
                        {
                            Season seas = kvp2.Value;
                            foreach (Episode e in seas.Episodes)
                                e.WriteXml(writer);
                        }
                    }
                }

                writer.WriteEndElement(); // data

                writer.WriteEndDocument();
                writer.Close();
            }
            Unlock("SaveCache");
        }

        public SeriesInfo FindSeriesForName(string showName)
        {
            Search(showName);
            
            if (string.IsNullOrEmpty(showName))
                return null;

            showName = showName.ToLower();

            foreach (KeyValuePair<int, SeriesInfo> ser in Series)
            {
                if (ser.Value.Name.ToLower() == showName)
                    return ser.Value;
            }

            return null;
        }


        public Episode FindEpisodeByID(int id)
        {
            if (!GetLock("FindEpisodeByID"))
                return null;

            foreach (KeyValuePair<int, SeriesInfo> kvp in Series)
            {
                foreach (KeyValuePair<int, Season> kvp2 in kvp.Value.Seasons)
                {
                    Season seas = kvp2.Value;
                    foreach (Episode e in seas.Episodes)
                    {
                        if (e.EpisodeID == id)
                        {
                            Unlock("FindEpisodeByID");
                            return e;
                        }
                    }
                }
            }
            Unlock("FindEpisodeByID");
            return null;
        }

        public bool Connect()
        {
            Connected = GetMirrors() && GetLanguages();
            return Connected;
        }

        public static string BuildURL(bool withHttpAndKey, bool episodesToo, int code, string lang)
        {
            string r = withHttpAndKey ? "http://thetvdb.com/api/" + APIKey() + "/" : "";
            r += episodesToo ? "series/" + code + "/all/" + lang + ".zip" : "series/" + code + "/" + lang + ".xml";
            return r;
        }

        private byte[] GetPageZIP(string url, string extractFile, bool useKey, bool forceReload)
        {
            byte[] zipped = GetPage(url, useKey, typeMaskBits.tmZIP, forceReload);

            if (zipped == null)
                return null;

            MemoryStream ms = new MemoryStream(zipped);
            MemoryStream theFile = new MemoryStream();
            //try 
            //{
            using (ZipFile zip = ZipFile.Read(ms))
            {
                ZipEntry e = zip[extractFile];
                e.Extract(theFile);
            }

            System.Diagnostics.Debug.Print("Downloaded " + url + ", " + ms.Length + " bytes became " + theFile.Length);
            //}
            //catch (Exception ^e)
            //{
            //    LastError = CurrentDLTask + " : " + e->Message;
            //    return nullptr;
            //}

            // ZipFile allocates more buffer than is needed, so we need to resize the array before returning it
            byte[] r = theFile.GetBuffer();
            Array.Resize(ref r, (int) theFile.Length);

            return r;
        }

        private static string APIKey()
        {
            return "5FEC454623154441"; // tvrename's API key on thetvdb.com
        }

        public byte[] GetPage(string url, bool useKey, typeMaskBits mirrorType, bool forceReload)
        {
            string mirr = "";
            switch (mirrorType)
            {
                case typeMaskBits.tmXML:
                    mirr = XMLMirror;
                    break;
                case typeMaskBits.tmBanner:
                    mirr = BannerMirror;
                    break;
                case typeMaskBits.tmZIP:
                    mirr = ZIPMirror;
                    break;
                default:
                case typeMaskBits.tmMainSite:
                    mirr = "http://www.thetvdb.com";
                    break;
            }
            if (url.StartsWith("/"))
                url = url.Substring(1);

            if (!mirr.EndsWith("/"))
                mirr += "/";

            string theURL = mirr;
            if (mirrorType != typeMaskBits.tmBanner)
                theURL += "api/";
            else
                theURL += "banners/";
            if (useKey)
                theURL += APIKey() + "/";
            theURL += url;

            //HttpWebRequest ^wr = dynamic_cast<HttpWebRequest ^>(HttpWebRequest::Create(theURL));
            //wr->Timeout = 10000; // 10 seconds
            //wr->Method = "GET";
            //wr->KeepAlive = false;

            System.Net.WebClient wc = new System.Net.WebClient();
            
            if (forceReload)
                wc.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Reload);

            try
            {
                byte[] r = wc.DownloadData(theURL);
                //HttpWebResponse ^wres = dynamic_cast<HttpWebResponse ^>(wr->GetResponse());
                //Stream ^str = wres->GetResponseStream();
                //array<unsigned char> ^r = gcnew array<unsigned char>((int)str->Length);
                //str->Read(r, 0, (int)str->Length);

                if (!url.EndsWith(".zip"))
                    System.Diagnostics.Debug.Print("Downloaded " + url + ", " + r.Length + " bytes");

                return r;
            }
            catch (WebException e)
            {
                LastError = CurrentDLTask + " : " + e.Message;
                return null;
            }
        }

        public void ForgetEverything()
        {
            if (!GetLock("ForgetEverything"))
                return;

            Series.Clear();
            Connected = false;
            SaveCache();
            Unlock("ForgetEverything");
        }

        public void ForgetShow(int id, bool makePlaceholder)
        {
            if (!GetLock("ForgetShow"))
                return;

            if (Series.ContainsKey(id))
            {
                string name = Series[id].Name;
                Series.Remove(id);
                if (makePlaceholder)
                {
                    MakePlaceholderSeries(id, name);
                    ForceReloadOn.Add(id);
                }
            }
            Unlock("ForgetShow");
        }

        public bool GetLanguages()
        {
            Say("TheTVDB Languages");

            byte[] p = GetPage("languages.xml", true, typeMaskBits.tmMainSite, false);
            if (p == null)
                return false;
            
            MemoryStream ms = new MemoryStream(p);

            XmlReaderSettings settings = new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true
            };
            XmlReader reader = XmlReader.Create(ms, settings);
            reader.Read();

            if (reader.Name != "xml")
                return false;

            reader.Read();

            if (reader.Name != "Languages")
                return false;

            reader.Read(); // move forward one

            LanguageList.Clear();

            while (!reader.EOF)
            {
                if (reader.Name == "Languages" && !reader.IsStartElement())
                    break; // end of mirror whatsit

                if (reader.Name != "Language")
                    return false;

                XmlReader r = reader.ReadSubtree();
                r.Read(); // puts us on "Language"
                int ID = -1;
                string name = "";
                string abbrev = "";

                r.Read(); // get onto the first thingy

                while (!r.EOF)
                {
                    if (r.Name == "Language" && !r.IsStartElement())
                    {
                        if ((ID != -1) && (!string.IsNullOrEmpty(name)) && (!string.IsNullOrEmpty(abbrev)))
                            LanguageList[abbrev] = name;
                        break; // end of language whatsit
                    }

                    if (r.Name == "id")
                        ID = r.ReadElementContentAsInt();
                    else if (r.Name == "name")
                        name = r.ReadElementContentAsString();
                    else if (r.Name == "abbreviation")
                        abbrev = r.ReadElementContentAsString();
                    else
                        r.ReadOuterXml(); // skip unknown element
                }
                reader.Read(); // move forward one
            }
            return true;
        }

        public bool GetMirrors()
        {
            // get mirror list
            Say("TheTVDB Mirrors");

            List<string> XMLMirrorList = new List<String>();
            List<string> BannerMirrorList = new List<String>();
            List<string> ZIPMirrorList = new List<String>();

            byte[] p = GetPage("mirrors.xml", true, typeMaskBits.tmMainSite, false);
            if (p == null)
                return false;
            
            MemoryStream ms = new MemoryStream(p);

            XmlReaderSettings settings = new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true
            };
            XmlReader reader = XmlReader.Create(ms, settings);
            reader.Read();

            if (reader.Name != "xml")
                return false;

            reader.Read();

            if (reader.Name != "Mirrors")
                return false;

            reader.Read(); // move forward one

            while (!reader.EOF)
            {
                if (reader.Name == "Mirrors" && !reader.IsStartElement())
                    break; // end of mirror whatsit

                if (reader.Name != "Mirror")
                    return false;

                XmlReader r = reader.ReadSubtree();
                r.Read(); // puts us on "Mirror"
                int ID = -1;
                string mirrorPath = "";
                int typeMask = -1;

                r.Read(); // get onto the first thingy

                while (!r.EOF)
                {
                    if (r.Name == "Mirror" && !r.IsStartElement())
                    {
                        if ((ID != -1) && (!string.IsNullOrEmpty(mirrorPath)) && (typeMask != -1))
                        {
                            if ((typeMask & (int) typeMaskBits.tmXML) != 0)
                                XMLMirrorList.Add(mirrorPath);
                            if ((typeMask & (int) typeMaskBits.tmBanner) != 0)
                                BannerMirrorList.Add(mirrorPath);
                            if ((typeMask & (int) typeMaskBits.tmZIP) != 0)
                                ZIPMirrorList.Add(mirrorPath);
                        }
                        break; // end of mirror whatsit
                    }

                    if (r.Name == "id")
                        ID = r.ReadElementContentAsInt();
                    else if (r.Name == "mirrorpath")
                        mirrorPath = r.ReadElementContentAsString();
                    else if (r.Name == "typemask")
                        typeMask = r.ReadElementContentAsInt();
                    else
                        r.ReadOuterXml(); // skip unknown element
                }
                reader.Read(); // move forward one
            }

            // choose a random mirror to use
            int c = 0;
            Random ra = new Random((int) DateTime.Now.Ticks);
            c = ZIPMirrorList.Count;
            if (c != 0)
                ZIPMirror = ZIPMirrorList[ra.Next(0, c - 1)];
            c = XMLMirrorList.Count;
            if (c != 0)
                XMLMirror = XMLMirrorList[ra.Next(0, c - 1)];
            c = BannerMirrorList.Count;
            if (c != 0)
                BannerMirror = BannerMirrorList[ra.Next(0, c - 1)];

            return true;
        }

        public bool GetUpdates()
        {
            Say("Updates list");

            if (!Connected && !Connect())
            {
                Say("");
                return false;
            }

            long theTime = Srv_Time;

            if (theTime == 0)
            {
                // we can use the oldest thing we have locally.  It isn't safe to use the newest thing.
                // This will only happen the first time we do an update, so a false _all update isn't too bad.
                foreach (KeyValuePair<int, SeriesInfo> kvp in Series)
                {
                    SeriesInfo ser = kvp.Value;
                    if ((theTime == 0) || ((ser.Srv_LastUpdated != 0) && (ser.Srv_LastUpdated < theTime)))
                        theTime = ser.Srv_LastUpdated;
                    foreach (KeyValuePair<int, Season> kvp2 in kvp.Value.Seasons)
                    {
                        Season seas = kvp2.Value;

                        foreach (Episode e in seas.Episodes)
                        {
                            if ((theTime == 0) || ((e.Srv_LastUpdated != 0) && (e.Srv_LastUpdated < theTime)))
                                theTime = e.Srv_LastUpdated;
                        }
                    }
                }
            }

            // anything with a srv_lastupdated of 0 should be marked as dirty
            // typically, this'll be placeholder series
            foreach (KeyValuePair<int, SeriesInfo> kvp in Series)
            {
                SeriesInfo ser = kvp.Value;
                if ((ser.Srv_LastUpdated == 0) || (ser.Seasons.Count == 0))
                    ser.Dirty = true;
                foreach (KeyValuePair<int, Season> kvp2 in kvp.Value.Seasons)
                {
                    foreach (Episode ep in kvp2.Value.Episodes)
                    {
                        if (ep.Srv_LastUpdated == 0)
                            ep.Dirty = true;
                    }
                }
            }

            if (theTime == 0)
            {
                Say("");
                return true; // that's it for now
            }

            long seconds = Utils.TimeZone.Epoch() - theTime;
            if (seconds < 3540) // 59 minutes
            {
                Say("");
                return true;
            }

            string timePeriod = "";

            int howLongDays = (int) (seconds / (60 * 60 * 24));

            if ((howLongDays < 1) || (Series.Count == 0))
                timePeriod = "day";
            else if ((howLongDays >= 1) && (howLongDays < 7))
                timePeriod = "week";
            else if ((howLongDays >= 7) && (howLongDays < 28))
                timePeriod = "month";
            else
                timePeriod = "all";

            if (timePeriod != "all")
                Say("Updates list for the " + timePeriod);
            else
                Say("Updates list for everything");

            // http://thetvdb.com/api/5FEC454623154441/updates/updates_day.xml
            // day, week, month, all

            string udf = "updates_" + timePeriod;
            byte[] p = GetPageZIP("updates/" + udf + ".zip", udf + ".xml", true, false);
            if (p == null)
            {
                Say("");
                return false;
            }
            //BinaryWriter ^fs = gcnew BinaryWriter(gcnew FileStream("c:\\temp\\ud.xml", FileMode::Create));
            //fs->Write(p, 0, p->Length);
            //fs->Close();

            MemoryStream ms = new MemoryStream(p);
            Say("");

            return ProcessUpdateList(ms);
        }

        public bool ProcessUpdateList(Stream str)
        {
            // if updatetime > localtime for item, then remove it, so it will be downloaded later

            XmlReaderSettings settings = new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true
            };
            XmlReader reader = XmlReader.Create(str, settings);
            reader.Read();

            if (reader.Name != "xml")
                return false;

            reader.Read();

            if ((reader.Name != "Data") || (reader.AttributeCount != 1))
                return false;

            New_Srv_Time = int.Parse(reader.GetAttribute("time"));

            // what follows is the last update time for a bunch of zero or more series, episodes, and banners

            while (!reader.EOF)
            {
                reader.Read();
                if (reader.Name == "Series")
                {
                    //<Series>
                    // <id>70761</id>
                    // <time>1221383086</time>
                    //</Series>
                    int ID = -1;
                    int time = -1;
                    XmlReader r = reader.ReadSubtree();
                    r.Read(); // puts us on "Series"
                    r.Read(); // get onto first thingy
                    while (!r.EOF)
                    {
                        if (r.Name == "Series" && !r.IsStartElement())
                        {
                            if ((ID != -1) && (time != -1))
                            {
                                if (Series.ContainsKey(ID)) // this is a series we have
                                {
                                    if (time > Series[ID].Srv_LastUpdated) // newer version on the server
                                        Series[ID].Dirty = true; // mark as dirty, so it'll be fetched again later
                                }
                            }
                            break;
                        }

                        if (r.Name == "id")
                            ID = r.ReadElementContentAsInt();
                        else if (r.Name == "time")
                            time = r.ReadElementContentAsInt();
                        else
                            r.ReadOuterXml(); // skip
                    }
                } // series
                else if (reader.Name == "Episode")
                {
                    //<Episode>
                    //<id>73175</id>
                    //<Series>72102</Series>
                    //<time>1221387596</time>
                    //</Episode>
                    int serID = -1;
                    int time = -1;
                    int epID = -1;
                    XmlReader r = reader.ReadSubtree();
                    r.Read(); // puts us on "Series"
                    r.Read(); // get onto first thingy
                    while (!r.EOF)
                    {
                        if (r.Name == "Episode" && !r.IsStartElement())
                        {
                            if ((serID != -1) && (time != -1) && (epID != -1))
                            {
                                if (Series.ContainsKey(serID))
                                {
                                    bool found = false;
                                    foreach (KeyValuePair<int, Season> kvp2 in Series[serID].Seasons)
                                    {
                                        Season seas = kvp2.Value;

                                        foreach (Episode ep in seas.Episodes)
                                        {
                                            if (ep.EpisodeID == epID)
                                            {
                                                if (ep.Srv_LastUpdated < time)
                                                    ep.Dirty = true; // mark episode as dirty.
                                                found = true;
                                                break;
                                            }
                                        }
                                    }
                                    if (!found)
                                    {
                                        // must be a new episode
                                        LockEE();
                                        ExtraEpisodes.Add(new ExtraEp(serID, epID));
                                        UnlockEE();
                                    }
                                }
                            }
                            break;
                        }

                        if (r.Name == "id")
                            epID = r.ReadElementContentAsInt();
                        else if (r.Name == "time")
                            time = r.ReadElementContentAsInt();
                        else if (r.Name == "Series")
                            serID = r.ReadElementContentAsInt();
                        else
                            r.ReadOuterXml(); // skip
                    }
                }
                else
                    reader.ReadOuterXml(); // skip
            } // reader EOF

            // if more than 10% of a show's episodes are marked as dirty, just download the entire show again
            foreach (KeyValuePair<int, SeriesInfo> kvp in Series)
            {
                int totaleps = 0;
                int totaldirty = 0;
                foreach (KeyValuePair<int, Season> kvp2 in kvp.Value.Seasons)
                {
                    foreach (Episode ep in kvp2.Value.Episodes)
                    {
                        if (ep.Dirty)
                            totaldirty++;
                        totaleps++;
                    }
                }
                if (totaldirty >= (totaleps / 10)) // 10%
                {
                    kvp.Value.Dirty = true;
                    kvp.Value.Seasons.Clear();
                }
            }

            return true;
        }

        public bool ProcessTVDBResponse(Stream str, int? codeHint)
        {
            // Will have one or more series, and episodes
            // all wrapped in <Data> </Data>

            // e.g.: 
            //<Data>
            // <Series>
            //  <id>...</id>
            //  etc.
            // </Series>
            // <Episode>
            //  <id>...</id>
            //  blah blah
            // </Episode>
            // <Episode>
            //  <id>...</id>
            //  blah blah
            // </Episode>
            // ...
            //</Data>

            if (!GetLock("ProcessTVDBResponse"))
                return false;

            try
            {
                XmlReaderSettings settings = new XmlReaderSettings
                {
                    IgnoreComments = true,
                    IgnoreWhitespace = true
                };
                XmlReader r = XmlReader.Create(str, settings);

                r.Read();

                while (!r.EOF)
                {
                    if ((r.Name == "Data") && !r.IsStartElement())
                        break; // that's it.
                    if (r.Name == "Series")
                    {
                        // The <series> returned by GetSeries have
                        // less info than other results from
                        // thetvdb.com, so we need to smartly merge
                        // in a <Series> if we already have some/all
                        // info on it (depending on which one came
                        // first).

                        SeriesInfo si = new SeriesInfo(r.ReadSubtree());
                        if (Series.ContainsKey(si.TVDBCode))
                            Series[si.TVDBCode].Merge(si, RequestLanguage);
                        else
                            Series[si.TVDBCode] = si;
                        r.Read();
                    }
                    else if (r.Name == "Episode")
                    {
                        Episode e = new Episode(null, null, r.ReadSubtree(), Args.Unattended);
                        if (e.OK())
                        {
                            if (!Series.ContainsKey(e.SeriesID))
                                throw new TVDBException("Can't find the series to add the episode to (TheTVDB).");
                            SeriesInfo ser = Series[e.SeriesID];
                            Season seas = ser.GetOrAddSeason(e.ReadSeasonNum, e.SeasonID);

                            bool added = false;
                            for (int i = 0; i < seas.Episodes.Count; i++)
                            {
                                Episode ep = seas.Episodes[i];
                                if (ep.EpisodeID == e.EpisodeID)
                                {
                                    seas.Episodes[i] = e;
                                    added = true;
                                    break;
                                }
                            }
                            if (!added)
                                seas.Episodes.Add(e);
                            e.SetSeriesSeason(ser, seas);
                        }
                        r.Read();
                    }
                    else if (r.Name == "xml")
                        r.Read();
                    else if (r.Name == "Data")
                    {
                        string time = r.GetAttribute("time");
                        if (time != null)
                            New_Srv_Time = int.Parse(time);
                        r.Read();
                    }
                    else
                        r.ReadOuterXml();
                }
            }
            catch (XmlException e)
            {
                if (!Args.Unattended)
                {
                    string message = "Error processing data from TheTVDB (top level).";
                    message += "\r\n" + e.Message;
                    String name = "";
                    if (codeHint.HasValue && Series.ContainsKey(codeHint.Value))
                    {
                        name += "Show \"" + Series[codeHint.Value].Name + "\" ";
                    }
                    if (codeHint.HasValue)
                    {
                        name += "ID #" + codeHint.Value + " ";
                    }
                    MessageBox.Show(name + message, "TVRename", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    // throw new TVDBException(e.Message);
                }
                return false;
            }
            finally
            {
                Unlock("ProcessTVDBResponse");
            }
            return true;
        }

        public bool DoWeForceReloadFor(int code)
        {
            return ForceReloadOn.Contains(code) || !Series.ContainsKey(code);
        }

        public SeriesInfo DownloadSeriesNow(int code, bool episodesToo)
        {
            bool forceReload = ForceReloadOn.Contains(code);
            string txt = "";
            if (Series.ContainsKey(code))
                txt = Series[code].Name;
            else
                txt = "Code " + code;
            if (episodesToo)
                txt += " (Everything)";
            else
                txt += " Overview";
            Say(txt);

            string lang = RequestLanguage;
            string url = BuildURL(false, episodesToo, code, lang);
            byte[] p = episodesToo ? GetPageZIP(url, lang + ".xml", true, forceReload) : GetPage(url, true, typeMaskBits.tmXML, forceReload);
            if (p == null)
                return null;

            MemoryStream ms = new MemoryStream(p);

            ProcessTVDBResponse(ms, code);

            ForceReloadOn.Remove(code);

            return (Series.ContainsKey(code)) ? Series[code] : null;
        }

        public bool DownloadEpisodeNow(int seriesID, int episodeID)
        {
            bool forceReload = ForceReloadOn.Contains(seriesID);

            string txt = "";
            if (Series.ContainsKey(seriesID))
            {
                Episode ep = FindEpisodeByID(episodeID);
                string eptxt = "New Episode";
                if ((ep != null) && (ep.TheSeason != null))
                    eptxt = string.Format("S{0:00}E{1:00}", ep.TheSeason.SeasonNumber, ep.EpNum);

                txt = Series[seriesID].Name + " (" + eptxt + ")";
            }
            else
                return false; // shouldn't happen
            Say(txt);

            string url = "episodes/" + episodeID + "/" + RequestLanguage + ".xml";

            byte[] p = GetPage(url, true, typeMaskBits.tmXML, forceReload);

            if (p == null)
                return false;

            MemoryStream ms = new MemoryStream(p);

            return ProcessTVDBResponse(ms, seriesID);
        }

        public SeriesInfo MakePlaceholderSeries(int code, string name)
        {
            if (string.IsNullOrEmpty(name))
                name = "";
            Series[code] = new SeriesInfo(name, code);
            Series[code].Dirty = true;
            return Series[code];
        }

        public bool EnsureUpdated(int code)
        {
            if (!Series.ContainsKey(code) || (Series[code].Seasons.Count == 0))
                return DownloadSeriesNow(code, true) != null; // the whole lot!

            bool ok = true;

            if (Series[code].Dirty)
                ok = (DownloadSeriesNow(code, false) != null) && ok;

            foreach (KeyValuePair<int, Season> kvp in Series[code].Seasons)
            {
                Season seas = kvp.Value;
                foreach (Episode e in seas.Episodes)
                {
                    if (e.Dirty)
                    {
                        LockEE();
                        ExtraEpisodes.Add(new ExtraEp(e.SeriesID, e.EpisodeID));
                        UnlockEE();
                    }
                }
            }

            LockEE();
            foreach (ExtraEp ee in ExtraEpisodes)
            {
                if ((ee.SeriesID == code) && (!ee.Done))
                {
                    ok = DownloadEpisodeNow(ee.SeriesID, ee.EpisodeID) && ok;
                    ee.Done = true;
                }
            }
            UnlockEE();

            ForceReloadOn.Remove(code);

            return ok;
        }

        public void Search(string text)
        {
            text = Helpers.RemoveDiacritics(text); // API doesn't like accented characters

            // http://www.thetvdb.com/api/GetSeries.php?seriesname=prison
            // by default, english only.  add &language=all

            bool isNumber = Regex.Match(text, "^[0-9]+$").Success;
            if (isNumber)
                DownloadSeriesNow(int.Parse(text), false);

            // but, the number could also be a name, so continue searching as usual
            text = text.Replace(".", " ");

            byte[] p = GetPage("GetSeries.php?seriesname=" + text + "&language=all", false, typeMaskBits.tmXML, true);

            if (p == null)
                return;

            MemoryStream ms = new MemoryStream(p);
            
            ProcessTVDBResponse(ms, null);
        }

        public string WebsiteURL(int code, int seasid, bool summaryPage)
        {
            // Summary: http://www.thetvdb.com/?tab=series&id=75340&lid=7
            // Season 3: http://www.thetvdb.com/?tab=season&seriesid=75340&seasonid=28289&lid=7

            if (summaryPage || (seasid <= 0) || !Series.ContainsKey(code))
                return "http://www.thetvdb.com/?tab=series&id=" + code;
            else
                return "http://www.thetvdb.com/?tab=season&seriesid=" + code + "&seasonid=" + seasid;
        }

        // Next episode to air of a given show		
        public Episode NextAiring(int code)
        {
            if (!Series.ContainsKey(code) || (Series[code].Seasons.Count == 0))
                return null; // DownloadSeries(code, true);

            Episode next = null;
            DateTime today = DateTime.Now;
            DateTime mostSoonAfterToday = new DateTime(0);

            SeriesInfo ser = Series[code];
            foreach (KeyValuePair<int, Season> kvp2 in ser.Seasons)
            {
                Season s = kvp2.Value;

                foreach (Episode e in s.Episodes)
                {
                    DateTime? adt = e.GetAirDateDT(true);
                    if (adt != null)
                    {
                        DateTime dt = (DateTime) adt;
                        if ((dt.CompareTo(today) > 0) && ((mostSoonAfterToday.CompareTo(new DateTime(0)) == 0) || (dt.CompareTo(mostSoonAfterToday) < 0)))
                        {
                            mostSoonAfterToday = dt;
                            next = e;
                        }
                    }
                }
            }

            return next;
        }
    }
}