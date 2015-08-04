using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using TvRename.Core.Settings;

//using TvRename.Core.Settings;

namespace TestConsole {
    internal class Program {
        public static void Main(string[] args) {
            Write();
            Read();
        }

        private static void Read() {
            var x = new XmlSerializer(typeof (SettingsWrapper));
            using (var fileStream = new FileStream("c:\\temp\\test3.xml", FileMode.Open, FileAccess.Read)) {
                SettingsWrapper oo = (SettingsWrapper) x.Deserialize(fileStream);
            }           
        }

        private static void Write() {
            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            var x = new XmlSerializer(typeof (SettingsWrapper));
            var settings2 = new SettingsWrapper {
                Version = "2.1",
                Settings = new Settings {
                    TheSearchers =
                        new TheSearchers {
                            Current = "bob", Choices = new List<TheSearchers.Choice> {
                                new TheSearchers.Choice {Name = "bob", URL2 = "http://www.newzleech.com/usenet/?group=&amp;minage=&amp;age=&amp;min=min&amp;max=max&amp;q={ShowName}+{Season}+{Episode}&amp;mode=usenet&amp;adv="},
                                new TheSearchers.Choice {Name = "bob", URL2 = "http://www.newzleech.com/usenet/?group=&amp;minage=&amp;age=&amp;min=min&amp;max=max&amp;q={ShowName}+{Season}+{Episode}&amp;mode=usenet&amp;adv="},
                                new TheSearchers.Choice {Name = "bob", URL2 = "http://www.newzleech.com/usenet/?group=&amp;minage=&amp;age=&amp;min=min&amp;max=max&amp;q={ShowName}+{Season}+{Episode}&amp;mode=usenet&amp;adv="},
                            }
                        },
                    Replacements = new List<MyReplacement> {new MyReplacement {This = "-", That = " ", CaseInsensitive = false}}
                }
            };
            XmlTextWriter w = new MyXmlWriter(new FileStream("c:\\temp\\test.xml", FileMode.Truncate, FileAccess.Write), Encoding.UTF8);
            using (w) {
                x.Serialize(w, settings2, ns);
            }
        }
    }

    class MyXmlWriter : XmlTextWriter {
        public MyXmlWriter(Stream w, Encoding encoding) : base(w, encoding) {
            Formatting = Formatting.Indented;
            Indentation = 2;
        }
    }
}