using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TvRename.Core.Settings.Serialized {
    public class TheSearchers {
        public class Choice {
            [XmlAttribute]
            public String Name { get; set; }

            [XmlAttribute]
            public String URL2 { get; set; }

            public static Choice Create(string name, string url) {
                return new Choice {
                    Name = name,
                    URL2 = url
                };
            }
        }

        public String Current { get; set; }

        [XmlElement(ElementName = "Choice")]
        public List<Choice> Choices { get; set; }

        public static TheSearchers Create() {
            return new TheSearchers {
                Current = "",
                Choices = new List<Choice> {
                    Choice.Create("Area07", "http://www.area07.net/browse.php?search={ShowName}+{Season}+{Episode}&cat=4"),
                    Choice.Create("BitMeTV", "http://www.bitmetv.org/browse.php?search={ShowName}+{Season}+{Episode}"),
                    Choice.Create("BushTorrents", "http://www.bushtorrent.com/torrents.php?search=&words={ShowName}+{Season}+{Episode}"),
                    Choice.Create("BT Junkie", "http://btjunkie.org/search?q={ShowName}+{Season}+{Episode}"),
                    Choice.Create("diwana.org", "http://diwana.org/browse.php?search={ShowName}+{Season}+{Episode}&cat=0"),
                    Choice.Create("IP Torrents", "http://iptorrents.com/browse.php?incldead=0&search={ShowName}+{Season}+{Episode}&cat=0"),
                    Choice.Create("ISO Hunt", "http://isohunt.com/torrents/?ihq={ShowName}+{Season}+{Episode}"),
                    Choice.Create("Mininova", "http://www.mininova.org/search/?search={ShowName}+{Season}+{Episode}/8"),
                    Choice.Create("Pirate Bay", "http://thepiratebay.org/search.php?q={ShowName}+{Season}+{Episode}"),
                    Choice.Create("torrentz.com", "http://www.torrentz.com/search?q={ShowName}+{Season}+{Episode}"),
                    Choice.Create("NewzLeech", "http://www.newzleech.com/usenet/?group=&minage=&age=&min=min&max=max&q={ShowName}+{Season}+{Episode}&mode=usenet&adv="),
                    Choice.Create("nzbs.org", "http://nzbs.org/index.php?action=search&q={ShowName}+{Season}+{Episode}"),
                    Choice.Create("binsearch", "http://binsearch.net/?q={ShowName}+s{Season:2}e{Episode2}&max=25&adv_age=365&server=")
                }
            };
        }

        public void SetToNumber(int n) {
            Current = Choices[n].Name;
        }

        public int CurrentSearchNum() {
            return NumForName(Current);
        }

        public int NumForName(string srch) {
            for (int i = 0; i < Choices.Count; i++) {
                if (Choices[i].Name == srch) {
                    return i;
                }
            }
            return 0;
        }

        public string CurrentSearchURL() {
            if (Choices.Count == 0) {
                return "";
            }
            return Choices[CurrentSearchNum()].URL2;
        }

        public void Clear() {
            Choices.Clear();
        }

        public void Add(string name, string url) {
            Choices.Add(new Choice {Name = name, URL2 = url});
        }

        public int Count() {
            return Choices.Count;
        }

        public string Name(int n) {
            if (n >= Choices.Count) {
                n = Choices.Count - 1;
            } else {
                if (n < 0) {
                    n = 0;
                }
            }
            return Choices[n].Name;
        }

        public string URL(int n) {
            if (n >= Choices.Count) {
                n = Choices.Count - 1;
            } else {
                if (n < 0) {
                    n = 0;
                }
            }
            return Choices[n].URL2;
        }
    }
}