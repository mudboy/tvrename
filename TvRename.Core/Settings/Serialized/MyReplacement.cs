using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace TvRename.Core.Settings.Serialized {
    [XmlType("Replace")]
    public class MyReplacement {
        [XmlAttribute]
        public string This { get; set; }

        [XmlAttribute]
        public string That { get; set; }

        [XmlIgnore]
        public bool CaseInsensitive { get; set; }

        [XmlAttribute("CaseInsensitive")]
        public string CaseInsensitiveString {
            get { return CaseInsensitive ? "Y" : "N"; }
            set { CaseInsensitive = value == "Y"; }
        }

        public static MyReplacement Create(string a, string b, bool insesns) {
            return new MyReplacement {
                This = a,
                That = b,
                CaseInsensitive = insesns
            };
        }
    }
}