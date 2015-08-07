// 
// Main website for TVRename is http://tvrename.com
// 
// Source code available at http://code.google.com/p/tvrename/
// 
// This code is released under GPLv3 http://www.gnu.org/licenses/gpl.html
// 

using System.Collections.Generic;
using System.Xml;

// Things like bittorrent search engines, etc.  Manages a URL template that is fed through
// CustomName.cs to generate a final URL.

namespace TvRename.Core.Settings
{
    public class Searchers
    {
        public class Choice
        {
            public string Name;
            public string URL2;
        }

        public string CurrentSearch;
        private List<Choice> Choices = new List<Choice>();

        public Searchers()
        {
            CurrentSearch = "";

            Add("Area07", "http://www.area07.net/browse.php?search={ShowName}+{Season}+{Episode}&cat=4");
            Add("BitMeTV", "http://www.bitmetv.org/browse.php?search={ShowName}+{Season}+{Episode}");
            Add("BushTorrents", "http://www.bushtorrent.com/torrents.php?search=&words={ShowName}+{Season}+{Episode}");
            Add("BT Junkie", "http://btjunkie.org/search?q={ShowName}+{Season}+{Episode}");
            Add("diwana.org", "http://diwana.org/browse.php?search={ShowName}+{Season}+{Episode}&cat=0");
            Add("IP Torrents", "http://iptorrents.com/browse.php?incldead=0&search={ShowName}+{Season}+{Episode}&cat=0");
            Add("ISO Hunt", "http://isohunt.com/torrents/?ihq={ShowName}+{Season}+{Episode}");
            Add("Mininova", "http://www.mininova.org/search/?search={ShowName}+{Season}+{Episode}/8"); // "/8" for tv shows only
            Add("Pirate Bay", "http://thepiratebay.org/search.php?q={ShowName}+{Season}+{Episode}");
            Add("torrentz.com", "http://www.torrentz.com/search?q={ShowName}+{Season}+{Episode}");
            Add("NewzLeech", "http://www.newzleech.com/usenet/?group=&minage=&age=&min=min&max=max&q={ShowName}+{Season}+{Episode}&mode=usenet&adv=");
            Add("nzbs.org", "http://nzbs.org/index.php?action=search&q={ShowName}+{Season}+{Episode}");
            Add("binsearch", "http://binsearch.net/?q={ShowName}+s{Season:2}e{Episode2}&max=25&adv_age=365&server=");

            CurrentSearch = "Mininova";
        }
        
        public Searchers(XmlReader reader)
        {
            Choices = new List<Choice>();
            CurrentSearch = "";

            reader.Read();
            if (reader.Name != "TheSearchers")
                return; // bail out

            reader.Read();
            while (!reader.EOF)
            {
                if ((reader.Name == "TheSearchers") && !reader.IsStartElement())
                    break; // all done

                if (reader.Name == "Current")
                    CurrentSearch = reader.ReadElementContentAsString();
                else if (reader.Name == "Choice")
                {
                    string url = reader.GetAttribute("URL");
                    if (url == null)
                        url = reader.GetAttribute("URL2");
                    else
                    {
                        // old-style URL, replace "!" with "{ShowName}+{Season}+{Episode}"
                        url = url.Replace("!", "{ShowName}+{Season}+{Episode}");
                    }
                    Add(reader.GetAttribute("Name"), url);
                    reader.ReadElementContentAsString();
                }
                else
                    reader.ReadOuterXml();
            }
        }

        public void SetToNumber(int n)
        {
            CurrentSearch = Choices[n].Name;
        }

        public int CurrentSearchNum()
        {
            return NumForName(CurrentSearch);
        }

        public int NumForName(string srch)
        {
            for (int i = 0; i < Choices.Count; i++)
            {
                if (Choices[i].Name == srch)
                    return i;
            }
            return 0;
        }

        public string CurrentSearchURL()
        {
            if (Choices.Count == 0)
                return "";
            return Choices[CurrentSearchNum()].URL2;
        }
        public void WriteXML(XmlWriter writer)
        {
            writer.WriteStartElement("TheSearchers");
            writer.WriteStartElement("Current");
            writer.WriteValue(CurrentSearch);
            writer.WriteEndElement();

            for (int i = 0; i < Count(); i++)
            {
                writer.WriteStartElement("Choice");
                writer.WriteStartAttribute("Name");
                writer.WriteValue(Choices[i].Name);
                writer.WriteEndAttribute();
                writer.WriteStartAttribute("URL2");
                writer.WriteValue(Choices[i].URL2);
                writer.WriteEndAttribute();
                writer.WriteEndElement();
            }
            writer.WriteEndElement(); // TheSearchers
        }
        public void Clear()
        {
            Choices.Clear();
        }

        public void Add(string name, string url)
        {

            Choices.Add(new Choice { Name = name, URL2 = url });
        }

        public int Count()
        {
            return Choices.Count;
        }

        public string Name(int n)
        {
            if (n >= Choices.Count)
                n = Choices.Count - 1;
            else if (n < 0)
                n = 0;
            return Choices[n].Name;
        }

        public string URL(int n)
        {
            if (n >= Choices.Count)
                n = Choices.Count - 1;
            else if (n < 0)
                n = 0;
            return Choices[n].URL2;
        }
    }
}