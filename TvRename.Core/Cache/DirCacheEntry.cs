// 
// Main website for TVRename is http://tvrename.com
// 
// Source code available at http://code.google.com/p/tvrename/
// 
// This code is released under GPLv3 http://www.gnu.org/licenses/gpl.html
// 

using System;
using System.IO;
using TvRename.Core.Settings.Serialized;
using TvRename.Utils;

namespace TvRename.Core.Cache
{
    public class DirCacheEntry
    {
        public bool HasUsefulExtension_NotOthersToo;
        public bool HasUsefulExtension_OthersToo;
        public Int64 Length;
        public string LowerName;
        public string SimplifiedFullName;
        public FileInfo TheFile;

        public DirCacheEntry(FileInfo f, TvSettings theSettings)
        {
            TheFile = f;
            SimplifiedFullName = Helpers.SimplifyName(f.FullName);
            LowerName = f.Name.ToLower();
            Length = f.Length;

            if (theSettings == null)
                return;

            HasUsefulExtension_NotOthersToo = theSettings.UsefulExtension(f.Extension, false);
            HasUsefulExtension_OthersToo = HasUsefulExtension_NotOthersToo | theSettings.UsefulExtension(f.Extension, true);
        }
    }
}