// 
// Main website for TVRename is http://tvrename.com
// 
// Source code available at http://code.google.com/p/tvrename/
// 
// This code is released under GPLv3 http://www.gnu.org/licenses/gpl.html
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using TvRename.Core;
using TvRename.Core.Actions;
using TvRename.Core.Items;
using TvRename.Core.Settings;
using TvRename.Core.Settings.Serialized;
using TvRename.TheTVDB;
using TvRename.Utils;
using TVRename.App;
using TVRename.Custom_Controls;
using Action = TvRename.Core.Actions.Action;
using Version = TvRename.Utils.Version;

namespace TVRename.Forms {
    // right click commands
    public enum RightClickCommands {
        None = 0,
        kEpisodeGuideForShow = 1,
        kVisitTVDBEpisode,
        kVisitTVDBSeason,
        kVisitTVDBSeries,
        kScanSpecificSeries,
        kWhenToWatchSeries,
        kForceRefreshSeries,
        kBTSearchFor,
        kActionIgnore,
        kActionBrowseForFile,
        kActionAction,
        kActionDelete,
        kActionIgnoreSeason,
        kEditShow,
        kEditSeason,
        kDeleteShow,
        kWatchBase = 1000,
        kOpenFolderBase = 2000
    }

    /// <summary>
    /// Summary for UI
    ///
    /// WARNING: If you change the name of this class, you will need to change the
    ///          'Resource File Name' property for the managed resource compiler tool
    ///          associated with all .resx files this class depends on.  Otherwise,
    ///          the designers will not be able to interact properly with localized
    ///          resources associated with this form.
    /// </summary>
    public partial class UI : Form {
        #region Delegates

        public delegate void IPCDelegate();

        public delegate void AutoFolderMonitorDelegate();

        #endregion

        protected int Busy;
        public IPCDelegate IPCBringToForeground;
        public IPCDelegate IPCDoAll;
        public IPCDelegate IPCQuit;
        public IPCDelegate IPCScan;
        protected bool InternalCheckChange;
        private int LastDLRemaining;
        public AutoFolderMonitorDelegate AFMScan;
        public AutoFolderMonitorDelegate AFMDoAll;
        public SetProgressDelegate SetProgress;
        private MyListView lvAction;
        protected TVDoc mDoc;
        protected List<string> mFoldersToOpen;
        protected int mInternalChange;
        protected List<FileInfo> mLastFL;
        protected Point mLastNonMaximizedLocation;
        protected Size mLastNonMaximizedSize;
        protected AutoFolderMonitor mAutoFolderMonitor;
        private bool treeExpandCollapseToggle = true;
        protected IList<Item> mLastActionsClicked;
        protected ProcessedEpisode mLastEpClicked;
        protected string mLastFolderClicked;
        protected Season mLastSeasonClicked;
        protected List<MyShowItem> mLastShowsClicked;

        public UI(TVDoc doc) {
            mDoc = doc;
            Busy = 0;
            mLastEpClicked = null;
            mLastFolderClicked = null;
            mLastSeasonClicked = null;
            mLastShowsClicked = null;
            mLastActionsClicked = null;
            mInternalChange = 0;
            mFoldersToOpen = new List<String>();
            InternalCheckChange = false;
            InitializeComponent();
            SetupIPC();
            try {
                LoadLayoutXML();
            } catch {
                // silently fail, doesn't matter too much
            }
            SetProgress += SetProgressActual;
            lvWhenToWatch.ListViewItemSorter = new DateSorterWTW();
            if (mDoc.Args.Hide) {
                WindowState = FormWindowState.Minimized;
                Visible = false;
                Hide();
            }
            Text = Text + " " + Version.DisplayVersionString();
            FillMyShows();
            UpdateSearchButton();
            SetGuideHTMLbody("");
            mDoc.DoWhenToWatch(true);
            FillWhenToWatchList();
            mDoc.WriteUpcomingRSSandXML();
            ShowHideNotificationIcon();
            int t = mDoc.Settings.StartupTab;
            if (t < tabControl1.TabCount) {
                tabControl1.SelectedIndex = mDoc.Settings.StartupTab;
            }
            tabControl1_SelectedIndexChanged(null, null);
            mAutoFolderMonitor = new AutoFolderMonitor(mDoc, this);
            if (mDoc.Settings.ShouldMonitorFolders) {
                mAutoFolderMonitor.StartMonitor();
            }
        }

        public static int BGDLLongInterval() {
            return 1000*60*60; // one hour
        }

        protected void MoreBusy() {
            Interlocked.Increment(ref Busy);
        }

        protected void LessBusy() {
            Interlocked.Decrement(ref Busy);
        }

        private void SetupIPC() {
            IPCBringToForeground += ShowYourself;
            IPCScan += ScanAll;
            IPCDoAll += ActionAll;
            IPCQuit += Close;
            AFMScan += ScanAll;
            AFMDoAll += ActionAll;
            int retries = 2;
            while (retries > 0) {
                try {
                    //Instantiate our server channel.
                    var channel = new IpcServerChannel("TVRenameChannel");

                    //Register the server channel.
                    ChannelServices.RegisterChannel(channel, true);

                    //Register this service type.
                    RemotingConfiguration.RegisterWellKnownServiceType(typeof (IPCMethods), "IPCMethods",
                                                                       WellKnownObjectMode.Singleton);
                    IPCMethods.Setup(this, mDoc);
                    break; // got this far, all is good, exit retry loop
                } catch {
                    // Maybe there is a half-dead TVRename process?  Try to kill it off.
                    String pn = Process.GetCurrentProcess().ProcessName;
                    Process[] procs = Process.GetProcessesByName(pn);
                    foreach (Process proc in procs) {
                        if (proc.Id != Process.GetCurrentProcess().Id) {
                            try {
                                proc.Kill();
                            } catch {}
                        }
                    }
                }
                retries--;
            } // retry loop
        }

        public void SetProgressActual(int p) {
            if (p < 0) {
                p = 0;
            } else {
                if (p > 100) {
                    p = 100;
                }
            }
            pbProgressBarx.Value = p;
            pbProgressBarx.Update();
        }

        public void ProcessArgs() {
            // TODO: Unify command line handling between here and in Program.cs
            if (mDoc.Args.Scan || mDoc.Args.DoAll) // doall implies scan
            {
                ScanAll();
            }
            if (mDoc.Args.DoAll) {
                ActionAll();
            }
            if (mDoc.Args.Quit || mDoc.Args.Hide) {
                Close();
            }
        }

        ~UI() {
            //		mDoc->StopBGDownloadThread();  TODO
            mDoc = null;
        }

        public void UpdateSearchButton() {
            string name = mDoc.GetSearchers().Name(mDoc.Settings.TheSearchers.CurrentSearchNum());
            bool customWTW = false;
            foreach (ListViewItem lvi in lvWhenToWatch.SelectedItems) {
                ProcessedEpisode pe = lvi.Tag as ProcessedEpisode;
                if (pe != null && !String.IsNullOrEmpty(pe.SI.CustomSearchURL)) {
                    customWTW = true;
                    break;
                }
            }
            bool customAction = false;
            foreach (ListViewItem lvi in lvAction.SelectedItems) {
                ProcessedEpisode pe = lvi.Tag as ProcessedEpisode;
                if (pe != null && !String.IsNullOrEmpty(pe.SI.CustomSearchURL)) {
                    customAction = true;
                    break;
                }
            }
            bnWTWBTSearch.Text = customWTW ? "Search" : name;
            bnActionBTSearch.Text = customAction ? "Search" : name;
            FillEpGuideHTML();
        }

        private void exitToolStripMenuItem_Click(object sender, System.EventArgs e) {
            Close();
        }

        private void visitWebsiteToolStripMenuItem_Click(object sender, System.EventArgs e) {
            TVDoc.SysOpen("http://tvrename.com");
        }

        private void UI_Load(object sender, System.EventArgs e) {
            ShowInTaskbar = mDoc.Settings.ShowInTaskbar && !mDoc.Args.Hide;
            foreach (TabPage tp in tabControl1.TabPages) // grr! TODO: why does it go white?
            {
                tp.BackColor = System.Drawing.SystemColors.Control;
            }
            Show();
            UI_LocationChanged(null, null);
            UI_SizeChanged(null, null);
            backgroundDownloadToolStripMenuItem.Checked = mDoc.Settings.BGDownload;
            offlineOperationToolStripMenuItem.Checked = mDoc.Settings.OfflineMode;
            BGDownloadTimer.Interval = 10000; // first time
            if (mDoc.Settings.BGDownload) {
                BGDownloadTimer.Start();
            }
            quickTimer.Start();
        }

        private ListView ListViewByName(string name) {
            if (name == "WhenToWatch") {
                return lvWhenToWatch;
            }
            if (name == "AllInOne") {
                return lvAction;
            }
            return null;
        }

        private void flushCacheToolStripMenuItem_Click(object sender, System.EventArgs e) {
            DialogResult res = MessageBox.Show("Are you sure you want to remove all " + "locally stored TheTVDB information?  This information will have to be downloaded again.  You " + "can force the refresh of a single show by holding down the \"Control\" key while clicking on " + "the \"Refresh\" button in the \"My Shows\" tab.",
                                               "Force Refresh All", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (res == DialogResult.Yes) {
                mDoc.GetTVDB(false, "").ForgetEverything();
                FillMyShows();
                FillEpGuideHTML();
                FillWhenToWatchList();
            }
        }

        private bool LoadWidths(XmlReader xml) {
            string forwho = xml.GetAttribute("For");
            ListView lv = ListViewByName(forwho);
            if (lv == null) {
                xml.ReadOuterXml();
                return true;
            }
            xml.Read();
            int c = 0;
            while (xml.Name == "Width") {
                if (c >= lv.Columns.Count) {
                    return false;
                }
                lv.Columns[c++].Width = xml.ReadElementContentAsInt();
            }
            xml.Read();
            return true;
        }

        private bool LoadLayoutXML() {
            if (mDoc.Args.Hide) {
                return true;
            }
            bool ok = true;
            XmlReaderSettings settings = new XmlReaderSettings {
                IgnoreComments = true,
                IgnoreWhitespace = true
            };
            string fn = PathManager.UILayoutFile.FullName;
            if (!File.Exists(fn)) {
                return true;
            }
            XmlReader reader = XmlReader.Create(fn, settings);
            reader.Read();
            if (reader.Name != "xml") {
                return false;
            }
            reader.Read();
            if (reader.Name != "TVRename") {
                return false;
            }
            if (reader.GetAttribute("Version") != "2.1") {
                return false;
            }
            reader.Read();
            if (reader.Name != "Layout") {
                return false;
            }
            reader.Read();
            while (reader.Name != "Layout") {
                if (reader.Name == "Window") {
                    reader.Read();
                    while (reader.Name != "Window") {
                        if (reader.Name == "Size") {
                            int x = int.Parse(reader.GetAttribute("Width"));
                            int y = int.Parse(reader.GetAttribute("Height"));
                            Size = new System.Drawing.Size(x, y);
                            reader.Read();
                        } else {
                            if (reader.Name == "Location") {
                                int x = int.Parse(reader.GetAttribute("X"));
                                int y = int.Parse(reader.GetAttribute("Y"));
                                Location = new Point(x, y);
                                reader.Read();
                            } else {
                                if (reader.Name == "Maximized") {
                                    WindowState = (reader.ReadElementContentAsBoolean() ? FormWindowState.Maximized : FormWindowState.Normal);
                                } else {
                                    reader.ReadOuterXml();
                                }
                            }
                        }
                    }
                    reader.Read();
                } // window
                else {
                    if (reader.Name == "ColumnWidths") {
                        ok = LoadWidths(reader) && ok;
                    } else {
                        if (reader.Name == "Splitter") {
                            splitContainer1.SplitterDistance = int.Parse(reader.GetAttribute("Distance"));
                            splitContainer1.Panel2Collapsed = bool.Parse(reader.GetAttribute("HTMLCollapsed"));
                            if (splitContainer1.Panel2Collapsed) {
                                bnHideHTMLPanel.ImageKey = "FillLeft.bmp";
                            }
                            reader.Read();
                        } else {
                            reader.ReadOuterXml();
                        }
                    }
                }
            } // while
            reader.Close();
            return ok;
        }

        private bool SaveLayoutXML() {
            if (mDoc.Args.Hide) {
                return true;
            }
            XmlWriterSettings settings = new XmlWriterSettings {
                Indent = true,
                NewLineOnAttributes = true
            };
            using (XmlWriter writer = XmlWriter.Create(PathManager.UILayoutFile.FullName, settings)) {
                writer.WriteStartDocument();
                writer.WriteStartElement("TVRename");
                writer.WriteStartAttribute("Version");
                writer.WriteValue("2.1");
                writer.WriteEndAttribute(); // version
                writer.WriteStartElement("Layout");
                writer.WriteStartElement("Window");
                writer.WriteStartElement("Size");
                writer.WriteStartAttribute("Width");
                writer.WriteValue(mLastNonMaximizedSize.Width);
                writer.WriteEndAttribute();
                writer.WriteStartAttribute("Height");
                writer.WriteValue(mLastNonMaximizedSize.Height);
                writer.WriteEndAttribute();
                writer.WriteEndElement(); // size
                writer.WriteStartElement("Location");
                writer.WriteStartAttribute("X");
                writer.WriteValue(mLastNonMaximizedLocation.X);
                writer.WriteEndAttribute();
                writer.WriteStartAttribute("Y");
                writer.WriteValue(mLastNonMaximizedLocation.Y);
                writer.WriteEndAttribute();
                writer.WriteEndElement(); // Location
                writer.WriteStartElement("Maximized");
                writer.WriteValue(WindowState == FormWindowState.Maximized);
                writer.WriteEndElement(); // maximized
                writer.WriteEndElement(); // window
                WriteColWidthsXML("WhenToWatch", writer);
                WriteColWidthsXML("AllInOne", writer);
                writer.WriteStartElement("Splitter");
                writer.WriteStartAttribute("Distance");
                writer.WriteValue(splitContainer1.SplitterDistance);
                writer.WriteEndAttribute();
                writer.WriteStartAttribute("HTMLCollapsed");
                writer.WriteValue(splitContainer1.Panel2Collapsed);
                writer.WriteEndAttribute();
                writer.WriteEndElement(); // splitter
                writer.WriteEndElement(); // Layout
                writer.WriteEndElement(); // tvrename
                writer.WriteEndDocument();
                writer.Close();
            }
            return true;
        }

        private void WriteColWidthsXML(string thingName, XmlWriter writer) {
            ListView lv = ListViewByName(thingName);
            if (lv == null) {
                return;
            }
            writer.WriteStartElement("ColumnWidths");
            writer.WriteStartAttribute("For");
            writer.WriteValue(thingName);
            writer.WriteEndAttribute();
            foreach (ColumnHeader lvc in lv.Columns) {
                writer.WriteStartElement("Width");
                writer.WriteValue(lvc.Width);
                writer.WriteEndElement();
            }
            writer.WriteEndElement(); // columnwidths
        }

        private void UI_FormClosing(object sender, FormClosingEventArgs e) {
            try {
                if (mDoc.Dirty()) {
                    DialogResult res = MessageBox.Show("Your changes have not been saved.  Do you wish to save before quitting?", "Unsaved data", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                    if (res == DialogResult.Yes) {
                        //mDoc.WriteXMLSettings(); todo fix saving settings
                        return;
                    } else {
                        if (res == DialogResult.Cancel) {
                            e.Cancel = true;
                        } else {
                            if (res == DialogResult.No) {}
                        }
                    }
                }
                if (!e.Cancel) {
                    SaveLayoutXML();
                    mDoc.TidyTVDB();
                    mDoc.Closing();
                }
            } catch (System.Exception ex) {
                MessageBox.Show(this, ex.Message + "\r\n\r\n" + ex.StackTrace, "Form Closing Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private ContextMenuStrip BuildSearchMenu() {
            menuSearchSites.Items.Clear();
            for (int i = 0; i < mDoc.GetSearchers().Count(); i++) {
                ToolStripMenuItem tsi = new ToolStripMenuItem(mDoc.GetSearchers().Name(i));
                tsi.Tag = i;
                menuSearchSites.Items.Add(tsi);
            }
            return menuSearchSites;
        }

        private void ChooseSiteMenu(int n) {
            ContextMenuStrip sm = BuildSearchMenu();
            if (n == 1) {
                sm.Show(bnWTWChooseSite, new Point(0, 0));
            } else {
                if (n == 0) {
                    sm.Show(bnActionWhichSearch, new Point(0, 0));
                }
            }
        }

        private void bnWTWChooseSite_Click(object sender, System.EventArgs e) {
            ChooseSiteMenu(1);
        }

        private void FillMyShows() {
            Season currentSeas = TreeNodeToSeason(MyShowTree.SelectedNode);
            var currentSI = TreeNodeToShowItem(MyShowTree.SelectedNode);
            var expanded = new List<MyShowItem>();
            foreach (TreeNode n in MyShowTree.Nodes) {
                if (n.IsExpanded) {
                    expanded.Add(TreeNodeToShowItem(n));
                }
            }
            MyShowTree.BeginUpdate();
            MyShowTree.Nodes.Clear();
            var sil = mDoc.GetShowItems(true);
            foreach (var si in sil) {
                TreeNode tvn = AddShowItemToTree(si);
                if (expanded.Contains(si)) {
                    tvn.Expand();
                }
            }
            mDoc.UnlockShowItems();
            foreach (var si in expanded) {
                foreach (TreeNode n in MyShowTree.Nodes) {
                    if (TreeNodeToShowItem(n) == si) {
                        n.Expand();
                    }
                }
            }
            if (currentSeas != null) {
                SelectSeason(currentSeas);
            } else {
                if (currentSI != null) {
                    SelectShow(currentSI);
                }
            }
            MyShowTree.EndUpdate();
        }

        private static string QuickStartGuide() {
            return "http://tvrename.com/quickstart-2.2.html";
        }

        private void ShowQuickStartGuide() {
            tabControl1.SelectTab(tbMyShows);
            epGuideHTML.Navigate(QuickStartGuide());
        }

        private void FillEpGuideHTML() {
            if (MyShowTree.Nodes.Count == 0) {
                ShowQuickStartGuide();
            } else {
                TreeNode n = MyShowTree.SelectedNode;
                FillEpGuideHTML(n);
            }
        }

        private MyShowItem TreeNodeToShowItem(TreeNode n) {
            if (n == null) {
                return null;
            }
            MyShowItem si = n.Tag as MyShowItem;
            if (si != null) {
                return si;
            }
            ProcessedEpisode pe = n.Tag as ProcessedEpisode;
            if (pe != null) {
                return pe.SI;
            }
            Season seas = n.Tag as Season;
            if (seas != null) {
                if (seas.Episodes.Count > 0) {
                    int TVDBID = seas.TheSeries.TVDBCode;
                    foreach (MyShowItem si2 in mDoc.GetShowItems(true)) {
                        if (si2.TVDBID == TVDBID) {
                            mDoc.UnlockShowItems();
                            return si2;
                        }
                    }
                    mDoc.UnlockShowItems();
                }
            }
            return null;
        }

        private static Season TreeNodeToSeason(TreeNode n) {
            if (n == null) {
                return null;
            }
            Season seas = n.Tag as Season;
            return seas;
        }

        private void FillEpGuideHTML(TreeNode n) {
            if (n == null) {
                FillEpGuideHTML(null, -1);
                return;
            }
            ProcessedEpisode pe = n.Tag as ProcessedEpisode;
            if (pe != null) {
                FillEpGuideHTML(pe.SI, pe.SeasonNumber);
                return;
            }
            Season seas = TreeNodeToSeason(n);
            if (seas != null) {
                // we have a TVDB season, but need to find the equiavlent one in our local processed episode collection
                if (seas.Episodes.Count > 0) {
                    int TVDBID = seas.TheSeries.TVDBCode;
                    foreach (MyShowItem si in mDoc.GetShowItems(true)) {
                        if (si.TVDBID == TVDBID) {
                            mDoc.UnlockShowItems();
                            FillEpGuideHTML(si, seas.SeasonNumber);
                            return;
                        }
                    }
                    mDoc.UnlockShowItems();
                }
                FillEpGuideHTML(null, -1);
                return;
            }
            FillEpGuideHTML(TreeNodeToShowItem(n), -1);
        }

        private void FillEpGuideHTML(MyShowItem si, int snum) {
            if (tabControl1.SelectedTab != tbMyShows) {
                return;
            }
            if (si == null) {
                SetGuideHTMLbody("");
                return;
            }
            TheTVDB db = mDoc.GetTVDB(true, "FillEpGuideHTML");
            SeriesInfo ser = db.GetSeries(si.TVDBID);
            if (ser == null) {
                SetGuideHTMLbody("Not downloaded, or not available");
                return;
            }
            string body = "";
            List<string> skip = new List<String> {
                "Actors",
                "banner",
                "Overview",
                "Airs_Time",
                "Airs_DayOfWeek",
                "fanart",
                "poster",
                "zap2it_id"
            };
            if ((snum >= 0) && (ser.Seasons.ContainsKey(snum))) {
                if (!string.IsNullOrEmpty(ser.GetItem("banner")) && !string.IsNullOrEmpty(db.BannerMirror)) {
                    body += "<img width=758 height=140 src=\"" + db.BannerMirror + "/banners/" + ser.GetItem("banner") + "\"><br/>";
                }
                Season s = ser.Seasons[snum];
                List<ProcessedEpisode> eis = null;
                // int snum = s.SeasonNumber;
                if (si.SeasonEpisodes.ContainsKey(snum)) {
                    eis = si.SeasonEpisodes[snum]; // use processed episodes if they are available
                } else {
                    eis = s.Episodes.Select(e => new ProcessedEpisode(e, si)).ToList();
                }
                string seasText = snum == 0 ? "Specials" : ("Season " + snum);
                if ((eis.Count > 0) && (eis[0].SeasonID > 0)) {
                    seasText = " - <A HREF=\"" + db.WebsiteURL(si.TVDBID, eis[0].SeasonID, false) + "\">" + seasText + "</a>";
                } else {
                    seasText = " - " + seasText;
                }
                body += "<h1><A HREF=\"" + db.WebsiteURL(si.TVDBID, -1, true) + "\">" + si.ShowName + "</A>" + seasText + "</h1>";
                foreach (ProcessedEpisode ei in eis) {
                    string epl = ei.NumsAsString();

                    // http://www.thetvdb.com/?tab=episode&seriesid=73141&seasonid=5356&id=108303&lid=7
                    string episodeURL = "http://www.thetvdb.com/?tab=episode&seriesid=" + ei.SeriesID + "&seasonid=" + ei.SeasonID + "&id=" + ei.EpisodeID;
                    body += "<A href=\"" + episodeURL + "\" name=\"ep" + epl + "\">"; // anchor
                    body += "<b>" + CustomName.NameForNoExt(ei, CustomName.OldNStyle(6)) + "</b>";
                    body += "</A>"; // anchor
                    if (si.UseSequentialMatch && (ei.OverallNumber != -1)) {
                        body += " (#" + ei.OverallNumber + ")";
                    }
                    body += " <A HREF=\"" + mDoc.Settings.BTSearchURL(ei) + "\" class=\"search\">Search</A>";
                    List<FileInfo> fl = mDoc.FindEpOnDisk(ei);
                    if (fl != null) {
                        foreach (FileInfo fi in fl) {
                            body += " <A HREF=\"file://" + fi.FullName + "\" class=\"search\">Watch</A>";
                        }
                    }
                    DateTime? dt = ei.GetAirDateDT(true);
                    if ((dt != null) && (dt.Value.CompareTo(DateTime.MaxValue) != 0)) {
                        body += "<p>" + dt.Value.ToShortDateString() + " (" + ei.HowLong() + ")";
                    }
                    body += "<p><p>";
                    if (mDoc.Settings.ShowEpisodePictures) {
                        body += "<table><tr>";
                        body += "<td width=100% valign=top>" + ei.Overview + "</td><td width=300 height=225>";
                        // 300x168 / 300x225
                        if (!string.IsNullOrEmpty(ei.GetItem("filename"))) {
                            body += "<img src=" + db.BannerMirror + "/banners/_cache/" + ei.GetItem("filename") + ">";
                        }
                        body += "</td></tr></table>";
                    } else {
                        body += ei.Overview;
                    }
                    body += "<p><hr><p>";
                } // for each episode in this season
            } else {
                // no epnum specified, just show an overview
                if ((!string.IsNullOrEmpty(ser.GetItem("banner"))) && (!string.IsNullOrEmpty(db.BannerMirror))) {
                    body += "<img width=758 height=140 src=\"" + db.BannerMirror + "/banners/" + ser.GetItem("banner") + "\"><br/>";
                }
                body += "<h1><A HREF=\"" + db.WebsiteURL(si.TVDBID, -1, true) + "\">" + si.ShowName + "</A> " + "</h1>";
                body += "<h2>Overview</h2>" + ser.GetItem("Overview");
                string actors = ser.GetItem("Actors");
                if (!string.IsNullOrEmpty(actors)) {
                    bool first = true;
                    foreach (string aa in actors.Split('|')) {
                        if (!string.IsNullOrEmpty(aa)) {
                            if (!first) {
                                body += ", ";
                            } else {
                                body += "<h2>Actors</h2>";
                            }
                            body += "<A HREF=\"http://www.imdb.com/find?s=nm&q=" + aa + "\">" + aa + "</a>";
                            first = false;
                        }
                    }
                }
                string airsTime = ser.GetItem("Airs_Time");
                string airsDay = ser.GetItem("Airs_DayOfWeek");
                if ((!string.IsNullOrEmpty(airsTime)) && (!string.IsNullOrEmpty(airsDay))) {
                    body += "<h2>Airs</h2> " + airsTime + " " + airsDay;
                    string net = ser.GetItem("Network");
                    if (!string.IsNullOrEmpty(net)) {
                        skip.Add("Network");
                        body += ", " + net;
                    }
                }
                bool firstInfo = true;
                foreach (KeyValuePair<string, string> kvp in ser.Items) {
                    if (firstInfo) {
                        body += "<h2>Information<table border=0>";
                        firstInfo = false;
                    }
                    if (!skip.Contains(kvp.Key)) {
                        if (kvp.Key == "SeriesID") {
                            body += "<tr><td width=120px>tv.com</td><td><A HREF=\"http://www.tv.com/show/" + kvp.Value + "/summary.html\">Visit</a></td></tr>";
                        } else {
                            if (kvp.Key == "IMDB_ID") {
                                body += "<tr><td width=120px>imdb.com</td><td><A HREF=\"http://www.imdb.com/title/" + kvp.Value + "\">Visit</a></td></tr>";
                            } else {
                                body += "<tr><td width=120px>" + kvp.Key + "</td><td>" + kvp.Value + "</td></tr>";
                            }
                        }
                    }
                }
                if (!firstInfo) {
                    body += "</table>";
                }
            }
            db.Unlock("FillEpGuideHTML");
            SetGuideHTMLbody(body);
        }

        // FillEpGuideHTML
        public static string EpGuidePath() {
            string tp = Path.GetTempPath();
            return tp + "tvrenameepguide.html";
        }

        public static string EpGuideURLBase() {
            return "file://" + EpGuidePath();
        }

        public void SetGuideHTMLbody(string body) {
            System.Drawing.Color col = System.Drawing.Color.FromName("ButtonFace");
            string css = "* { font-family: Tahoma, Arial; font-size 10pt; } " + "a:link { color: black } " + "a:visited { color:black } " + "a:hover { color:#000080 } " + "a:active { color:black } " + "a.search:link { color: #800000 } " + "a.search:visited { color:#800000 } " + "a.search:hover { color:#000080 } " + "a.search:active { color:#800000 } " + "* {background-color: #" + col.R.ToString("X2") + col.G.ToString("X2") + col.B.ToString("X2") + "}" + "* { color: black }";
            string html = "<html><head><STYLE type=\"text/css\">" + css + "</style>";
            html += "</head><body>";
            html += body;
            html += "</body></html>";
            epGuideHTML.Navigate("about:blank"); // make it close any file it might have open
            string path = EpGuidePath();
            BinaryWriter bw = new BinaryWriter(new FileStream(path, FileMode.Create));
            bw.Write(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(html));
            bw.Close();
            epGuideHTML.Navigate(EpGuideURLBase());
        }

        public void TVDBFor(ProcessedEpisode e) {
            if (e == null) {
                return;
            }
            TVDoc.SysOpen(mDoc.GetTVDB(false, "").WebsiteURL(e.SI.TVDBID, e.SeasonID, false));
        }

        public void TVDBFor(Season seas) {
            if (seas == null) {
                return;
            }
            TVDoc.SysOpen(mDoc.GetTVDB(false, "").WebsiteURL(seas.TheSeries.TVDBCode, -1, false));
        }

        public void TVDBFor(MyShowItem si) {
            if (si == null) {
                return;
            }
            TVDoc.SysOpen(mDoc.GetTVDB(false, "").WebsiteURL(si.TVDBID, -1, false));
        }

        public void menuSearchSites_ItemClicked(object sender, ToolStripItemClickedEventArgs e) {
            mDoc.SetSearcher((int) (e.ClickedItem.Tag));
            UpdateSearchButton();
        }

        public void bnWhenToWatchCheck_Click(object sender, System.EventArgs e) {
            RefreshWTW(true);
        }

        public void FillWhenToWatchList() {
            mInternalChange++;
            lvWhenToWatch.BeginUpdate();
            int dd = mDoc.Settings.WTWRecentDays;
            lvWhenToWatch.Groups[0].Header = "Aired in the last " + dd + " day" + ((dd == 1) ? "" : "s");

            // try to maintain selections if we can
            List<ProcessedEpisode> selections = new List<ProcessedEpisode>();
            foreach (ListViewItem lvi in lvWhenToWatch.SelectedItems) {
                selections.Add((ProcessedEpisode) (lvi.Tag));
            }
            Season currentSeas = TreeNodeToSeason(MyShowTree.SelectedNode);
            var currentSI = TreeNodeToShowItem(MyShowTree.SelectedNode);
            lvWhenToWatch.Items.Clear();
            var bolded = new List<DateTime>();
            foreach (var si in mDoc.GetShowItems(true)) {
                if (!si.ShowNextAirdate) {
                    continue;
                }
                foreach (var kvp in si.SeasonEpisodes) {
                    if (si.IgnoreSeasons.Contains(kvp.Key)) {
                        continue; // ignore this season
                    }
                    var eis = kvp.Value;
                    bool nextToAirFound = false;
                    foreach (ProcessedEpisode ei in eis) {
                        var dt = ei.GetAirDateDT(true);
                        if ((dt != null) && (dt.Value.CompareTo(DateTime.MaxValue) != 0)) {
                            TimeSpan ts = dt.Value.Subtract(DateTime.Now);
                            if (ts.TotalHours >= (-24*dd)) // in the future (or fairly recent)
                            {
                                bolded.Add(dt.Value);
                                if ((ts.TotalHours >= 0) && (!nextToAirFound)) {
                                    nextToAirFound = true;
                                    ei.NextToAir = true;
                                } else {
                                    ei.NextToAir = false;
                                }
                                ListViewItem lvi = new ListViewItem {Text = ""};
                                for (int i = 0; i < 7; i++) {
                                    lvi.SubItems.Add("");
                                }
                                UpdateWTW(ei, lvi);
                                lvWhenToWatch.Items.Add(lvi);
                                foreach (ProcessedEpisode pe in selections) {
                                    if (pe.SameAs(ei)) {
                                        lvi.Selected = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            mDoc.UnlockShowItems();
            lvWhenToWatch.Sort();
            lvWhenToWatch.EndUpdate();
            calCalendar.BoldedDates = bolded.ToArray();
            if (currentSeas != null) {
                SelectSeason(currentSeas);
            } else {
                if (currentSI != null) {
                    SelectShow(currentSI);
                }
            }
            UpdateToolstripWTW();
            mInternalChange--;
        }

        public void lvWhenToWatch_ColumnClick(object sender, ColumnClickEventArgs e) {
            int col = e.Column;
            // 3 4, or 6 = do date sort on 3
            // 1 or 2 = number sort
            // 5 = day sort
            // all others, text sort
            if (col == 6) // straight sort by date
            {
                lvWhenToWatch.ListViewItemSorter = new DateSorterWTW();
                lvWhenToWatch.ShowGroups = false;
            } else {
                if ((col == 3) || (col == 4)) {
                    lvWhenToWatch.ListViewItemSorter = new DateSorterWTW();
                    lvWhenToWatch.ShowGroups = true;
                } else {
                    lvWhenToWatch.ShowGroups = false;
                    if ((col == 1) || (col == 2)) {
                        lvWhenToWatch.ListViewItemSorter = new NumberAsTextSorter(col);
                    } else {
                        if (col == 5) {
                            lvWhenToWatch.ListViewItemSorter = new DaySorter(col);
                        } else {
                            lvWhenToWatch.ListViewItemSorter = new TextSorter(col);
                        }
                    }
                }
            }
            lvWhenToWatch.Sort();
        }

        public void lvWhenToWatch_Click(object sender, System.EventArgs e) {
            UpdateSearchButton();
            if (lvWhenToWatch.SelectedIndices.Count == 0) {
                txtWhenToWatchSynopsis.Text = "";
                return;
            }
            int n = lvWhenToWatch.SelectedIndices[0];
            ProcessedEpisode ei = (ProcessedEpisode) (lvWhenToWatch.Items[n].Tag);
            txtWhenToWatchSynopsis.Text = ei.Overview;
            mInternalChange++;
            DateTime? dt = ei.GetAirDateDT(true);
            if (dt != null) {
                calCalendar.SelectionStart = (DateTime) dt;
                calCalendar.SelectionEnd = (DateTime) dt;
            }
            mInternalChange--;
            if (mDoc.Settings.AutoSelectShowInMyShows) {
                GotoEpguideFor(ei, false);
            }
        }

        public void lvWhenToWatch_DoubleClick(object sender, System.EventArgs e) {
            if (lvWhenToWatch.SelectedItems.Count == 0) {
                return;
            }
            ProcessedEpisode ei = (ProcessedEpisode) (lvWhenToWatch.SelectedItems[0].Tag);
            List<FileInfo> fl = mDoc.FindEpOnDisk(ei);
            if ((fl != null) && (fl.Count > 0)) {
                TVDoc.SysOpen(fl[0].FullName);
                return;
            }

            // Don't have the episode.  Scan or search?
            switch (mDoc.Settings.WTWDoubleClick) {
                case TvSettings.WTWDoubleClickAction.Search:
                default:
                    bnWTWBTSearch_Click(null, null);
                    break;
                case TvSettings.WTWDoubleClickAction.Scan:
                    Scan(new List<MyShowItem> {ei.SI});
                    tabControl1.SelectTab(tbAllInOne);
                    break;
            }
        }

        public void calCalendar_DateSelected(object sender, DateRangeEventArgs e) {
            if (mInternalChange != 0) {
                return;
            }
            DateTime dt = calCalendar.SelectionStart;
            for (int i = 0; i < lvWhenToWatch.Items.Count; i++) {
                lvWhenToWatch.Items[i].Selected = false;
            }
            bool first = true;
            for (int i = 0; i < lvWhenToWatch.Items.Count; i++) {
                ListViewItem lvi = lvWhenToWatch.Items[i];
                ProcessedEpisode ei = (ProcessedEpisode) (lvi.Tag);
                DateTime? dt2 = ei.GetAirDateDT(true);
                if (dt2 != null) {
                    double h = dt2.Value.Subtract(dt).TotalHours;
                    if ((h >= 0) && (h < 24.0)) {
                        lvi.Selected = true;
                        if (first) {
                            lvi.EnsureVisible();
                            first = false;
                        }
                    }
                }
            }
            lvWhenToWatch.Focus();
        }

        public void bnEpGuideRefresh_Click(object sender, System.EventArgs e) {
            bnWhenToWatchCheck_Click(null, null); // close enough!
            FillMyShows();
        }

        public void RefreshWTW(bool doDownloads) {
            if (doDownloads) {
                if (!mDoc.DoDownloadsFG()) {
                    return;
                }
            }
            mInternalChange++;
            mDoc.DoWhenToWatch(true);
            FillMyShows();
            FillWhenToWatchList();
            mInternalChange--;
            mDoc.WriteUpcomingRSSandXML();
        }

        public void refreshWTWTimer_Tick(object sender, System.EventArgs e) {
            if (Busy == 0) {
                RefreshWTW(false);
            }
        }

        public void UpdateToolstripWTW() {
            // update toolstrip text too
            List<ProcessedEpisode> next1 = mDoc.NextNShows(1, 0, 36500);
            tsNextShowTxt.Text = "Next airing: ";
            if ((next1 != null) && (next1.Count >= 1)) {
                ProcessedEpisode ei = next1[0];
                tsNextShowTxt.Text += CustomName.NameForNoExt(ei, CustomName.OldNStyle(1)) + ", " + ei.HowLong() + " (" + ei.DayOfWeek() + ", " + ei.TimeOfDay() + ")";
            } else {
                tsNextShowTxt.Text += "---";
            }
        }

        public void bnWTWBTSearch_Click(object sender, System.EventArgs e) {
            foreach (ListViewItem lvi in lvWhenToWatch.SelectedItems) {
                mDoc.DoBTSearch((ProcessedEpisode) (lvi.Tag));
            }
        }

        public void epGuideHTML_Navigating(object sender, WebBrowserNavigatingEventArgs e) {
            string url = e.Url.AbsoluteUri;
            if (url.Contains("tvrenameepguide.html#ep")) {
                return; // don't intercept
            }
            if (url.EndsWith("tvrenameepguide.html")) {
                return; // don't intercept
            }
            if (url.CompareTo("about:blank") == 0) {
                return; // don't intercept about:blank
            }
            if (url == QuickStartGuide()) {
                return; // let the quickstartguide be shown
            }
            if ((url.Substring(0, 7).CompareTo("http://") == 0) || (url.Substring(0, 7).CompareTo("file://") == 0)) {
                e.Cancel = true;
                TVDoc.SysOpen(e.Url.AbsoluteUri);
            }
        }

        public void notifyIcon1_Click(object sender, MouseEventArgs e) {
            // double-click of notification icon causes a click then doubleclick event, 
            // so we need to do a timeout before showing the single click's popup
            tmrShowUpcomingPopup.Start();
        }

        public void tmrShowUpcomingPopup_Tick(object sender, System.EventArgs e) {
            tmrShowUpcomingPopup.Stop();
            UpcomingPopup UP = new UpcomingPopup(mDoc);
            UP.Show();
        }

        public void ShowYourself() {
            if (!mDoc.Settings.ShowInTaskbar) {
                Show();
            }
            if (WindowState == FormWindowState.Minimized) {
                WindowState = FormWindowState.Normal;
            }
            Activate();
        }

        public void notifyIcon1_DoubleClick(object sender, MouseEventArgs e) {
            tmrShowUpcomingPopup.Stop();
            ShowYourself();
        }

        public void buyMeADrinkToolStripMenuItem_Click(object sender, System.EventArgs e) {
            BuyMeADrink bmad = new BuyMeADrink();
            bmad.ShowDialog();
        }

        public void GotoEpguideFor(MyShowItem si, bool changeTab) {
            if (changeTab) {
                tabControl1.SelectTab(tbMyShows);
            }
            FillEpGuideHTML(si, -1);
        }

        public void GotoEpguideFor(Episode ep, bool changeTab) {
            if (changeTab) {
                tabControl1.SelectTab(tbMyShows);
            }
            SelectSeason(ep.TheSeason);
        }

        public void RightClickOnMyShows(MyShowItem si, Point pt) {
            mLastShowsClicked = new List<MyShowItem>() {si};
            mLastEpClicked = null;
            mLastSeasonClicked = null;
            mLastActionsClicked = null;
            BuildRightClickMenu(pt);
        }

        public void RightClickOnMyShows(Season seas, Point pt) {
            mLastShowsClicked = new List<MyShowItem> {mDoc.GetShowItem(seas.TheSeries.TVDBCode)};
            mLastEpClicked = null;
            mLastSeasonClicked = seas;
            mLastActionsClicked = null;
            BuildRightClickMenu(pt);
        }

        public void WTWRightClickOnShow(List<ProcessedEpisode> eps, Point pt) {
            if (eps.Count == 0) {
                return;
            }
            ProcessedEpisode ep = eps[0];
            var sis = eps.Select(e => e.SI).ToList();
            mLastEpClicked = ep;
            mLastShowsClicked = sis;
            mLastSeasonClicked = ep != null ? ep.TheSeason : null;
            mLastActionsClicked = null;
            BuildRightClickMenu(pt);
        }

        public void MenuGuideAndTVDB(bool addSep) {
            if (mLastShowsClicked == null || mLastShowsClicked.Count != 1) {
                return; // nothing or multiple selected
            }
            var si = (mLastShowsClicked != null) && (mLastShowsClicked.Count > 0) ? mLastShowsClicked[0] : null;
            Season seas = mLastSeasonClicked;
            ProcessedEpisode ep = mLastEpClicked;
            ToolStripMenuItem tsi;
            if (si != null) {
                if (addSep) {
                    showRightClickMenu.Items.Add(new ToolStripSeparator());
                    addSep = false;
                }
                tsi = new ToolStripMenuItem("Episode Guide");
                tsi.Tag = (int) RightClickCommands.kEpisodeGuideForShow;
                showRightClickMenu.Items.Add(tsi);
            }
            if (ep != null) {
                if (addSep) {
                    showRightClickMenu.Items.Add(new ToolStripSeparator());
                    addSep = false;
                }
                tsi = new ToolStripMenuItem("Visit thetvdb.com");
                tsi.Tag = (int) RightClickCommands.kVisitTVDBEpisode;
                showRightClickMenu.Items.Add(tsi);
            } else {
                if (seas != null) {
                    if (addSep) {
                        showRightClickMenu.Items.Add(new ToolStripSeparator());
                        addSep = false;
                    }
                    tsi = new ToolStripMenuItem("Visit thetvdb.com");
                    tsi.Tag = (int) RightClickCommands.kVisitTVDBSeason;
                    showRightClickMenu.Items.Add(tsi);
                } else {
                    if (si != null) {
                        if (addSep) {
                            showRightClickMenu.Items.Add(new ToolStripSeparator());
                            addSep = false;
                        }
                        tsi = new ToolStripMenuItem("Visit thetvdb.com");
                        tsi.Tag = (int) RightClickCommands.kVisitTVDBSeries;
                        showRightClickMenu.Items.Add(tsi);
                    }
                }
            }
        }

        public void MenuShowAndEpisodes() {
            var si = (mLastShowsClicked != null) && (mLastShowsClicked.Count > 0) ? mLastShowsClicked[0] : null;
            Season seas = mLastSeasonClicked;
            ProcessedEpisode ep = mLastEpClicked;
            ToolStripMenuItem tsi;
            if (si != null) {
                tsi = new ToolStripMenuItem("Force Refresh");
                tsi.Tag = (int) RightClickCommands.kForceRefreshSeries;
                showRightClickMenu.Items.Add(tsi);
                ToolStripSeparator tss = new ToolStripSeparator();
                showRightClickMenu.Items.Add(tss);
                String scanText = mLastShowsClicked.Count > 1 ? "Scan Multiple Shows" : "Scan \"" + si.ShowName + "\"";
                tsi = new ToolStripMenuItem(scanText);
                tsi.Tag = (int) RightClickCommands.kScanSpecificSeries;
                showRightClickMenu.Items.Add(tsi);
                if (mLastShowsClicked != null && mLastShowsClicked.Count == 1) {
                    tsi = new ToolStripMenuItem("When to Watch");
                    tsi.Tag = (int) RightClickCommands.kWhenToWatchSeries;
                    showRightClickMenu.Items.Add(tsi);
                    tsi = new ToolStripMenuItem("Edit Show");
                    tsi.Tag = (int) RightClickCommands.kEditShow;
                    showRightClickMenu.Items.Add(tsi);
                    tsi = new ToolStripMenuItem("Delete Show");
                    tsi.Tag = (int) RightClickCommands.kDeleteShow;
                    showRightClickMenu.Items.Add(tsi);
                }
            }
            if (seas != null && mLastShowsClicked != null && mLastShowsClicked.Count == 1) {
                tsi = new ToolStripMenuItem("Edit " + (seas.SeasonNumber == 0 ? "Specials" : "Season " + seas.SeasonNumber));
                tsi.Tag = (int) RightClickCommands.kEditSeason;
                showRightClickMenu.Items.Add(tsi);
            }
            if (ep != null && mLastShowsClicked != null && mLastShowsClicked.Count == 1) {
                List<FileInfo> fl = mDoc.FindEpOnDisk(ep);
                if (fl != null) {
                    if (fl.Count > 0) {
                        ToolStripSeparator tss = new ToolStripSeparator();
                        showRightClickMenu.Items.Add(tss);
                        int n = mLastFL.Count;
                        foreach (FileInfo fi in fl) {
                            mLastFL.Add(fi);
                            tsi = new ToolStripMenuItem("Watch: " + fi.FullName);
                            tsi.Tag = (int) RightClickCommands.kWatchBase + n;
                            showRightClickMenu.Items.Add(tsi);
                        }
                    }
                }
            } else {
                if (seas != null && si != null && mLastShowsClicked != null && mLastShowsClicked.Count == 1) {
                    // for each episode in season, find it on disk
                    bool first = true;
                    foreach (ProcessedEpisode epds in si.SeasonEpisodes[seas.SeasonNumber]) {
                        List<FileInfo> fl = mDoc.FindEpOnDisk(epds);
                        if ((fl != null) && (fl.Count > 0)) {
                            if (first) {
                                ToolStripSeparator tss = new ToolStripSeparator();
                                showRightClickMenu.Items.Add(tss);
                                first = false;
                            }
                            int n = mLastFL.Count;
                            foreach (FileInfo fi in fl) {
                                mLastFL.Add(fi);
                                tsi = new ToolStripMenuItem("Watch: " + fi.FullName);
                                tsi.Tag = (int) RightClickCommands.kWatchBase + n;
                                showRightClickMenu.Items.Add(tsi);
                            }
                        }
                    }
                }
            }
        }

        public void MenuFolders(LVResults lvr) {
            if (mLastShowsClicked == null || mLastShowsClicked.Count != 1) {
                return;
            }
            var si = (mLastShowsClicked != null) && (mLastShowsClicked.Count > 0) ? mLastShowsClicked[0] : null;
            Season seas = mLastSeasonClicked;
            ProcessedEpisode ep = mLastEpClicked;
            ToolStripMenuItem tsi;
            List<string> added = new List<String>();
            if (ep != null) {
                if (ep.SI.AllFolderLocations(mDoc.Settings).ContainsKey(ep.SeasonNumber)) {
                    int n = mFoldersToOpen.Count;
                    bool first = true;
                    foreach (string folder in ep.SI.AllFolderLocations(mDoc.Settings)[ep.SeasonNumber]) {
                        if ((!string.IsNullOrEmpty(folder)) && Directory.Exists(folder)) {
                            if (first) {
                                ToolStripSeparator tss = new ToolStripSeparator();
                                showRightClickMenu.Items.Add(tss);
                                first = false;
                            }
                            tsi = new ToolStripMenuItem("Open: " + folder);
                            added.Add(folder);
                            mFoldersToOpen.Add(folder);
                            tsi.Tag = (int) RightClickCommands.kOpenFolderBase + n;
                            n++;
                            showRightClickMenu.Items.Add(tsi);
                        }
                    }
                }
            } else {
                if ((seas != null) && (si != null) && (si.AllFolderLocations(mDoc.Settings).ContainsKey(seas.SeasonNumber))) {
                    int n = mFoldersToOpen.Count;
                    bool first = true;
                    foreach (string folder in si.AllFolderLocations(mDoc.Settings)[seas.SeasonNumber]) {
                        if ((!string.IsNullOrEmpty(folder)) && Directory.Exists(folder) && !added.Contains(folder)) {
                            added.Add(folder); // don't show the same folder more than once
                            if (first) {
                                ToolStripSeparator tss = new ToolStripSeparator();
                                showRightClickMenu.Items.Add(tss);
                                first = false;
                            }
                            tsi = new ToolStripMenuItem("Open: " + folder);
                            mFoldersToOpen.Add(folder);
                            tsi.Tag = (int) RightClickCommands.kOpenFolderBase + n;
                            n++;
                            showRightClickMenu.Items.Add(tsi);
                        }
                    }
                } else {
                    if (si != null) {
                        int n = mFoldersToOpen.Count;
                        bool first = true;
                        foreach (KeyValuePair<int, List<string>> kvp in si.AllFolderLocations(mDoc.Settings)) {
                            foreach (string folder in kvp.Value) {
                                if ((!string.IsNullOrEmpty(folder)) && Directory.Exists(folder) && !added.Contains(folder)) {
                                    added.Add(folder); // don't show the same folder more than once
                                    if (first) {
                                        ToolStripSeparator tss = new ToolStripSeparator();
                                        showRightClickMenu.Items.Add(tss);
                                        first = false;
                                    }
                                    tsi = new ToolStripMenuItem("Open: " + folder);
                                    mFoldersToOpen.Add(folder);
                                    tsi.Tag = (int) RightClickCommands.kOpenFolderBase + n;
                                    n++;
                                    showRightClickMenu.Items.Add(tsi);
                                }
                            }
                        }
                    }
                }
            }
            if (lvr != null) // add folders for selected Scan items
            {
                int n = mFoldersToOpen.Count;
                bool first = true;
                foreach (ScanListItem sli in lvr.FlatList) {
                    string folder = sli.TargetFolder;
                    if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder) || added.Contains(folder)) {
                        continue;
                    }
                    added.Add(folder); // don't show the same folder more than once
                    if (first) {
                        ToolStripSeparator tss = new ToolStripSeparator();
                        showRightClickMenu.Items.Add(tss);
                        first = false;
                    }
                    tsi = new ToolStripMenuItem("Open: " + folder);
                    mFoldersToOpen.Add(folder);
                    tsi.Tag = (int) RightClickCommands.kOpenFolderBase + n;
                    n++;
                    showRightClickMenu.Items.Add(tsi);
                }
            }
        }

        public void BuildRightClickMenu(Point pt) {
            showRightClickMenu.Items.Clear();
            mFoldersToOpen = new List<String>();
            mLastFL = new List<FileInfo>();
            MenuGuideAndTVDB(false);
            MenuShowAndEpisodes();
            MenuFolders(null);
            showRightClickMenu.Show(pt);
        }

        public void showRightClickMenu_ItemClicked(object sender, ToolStripItemClickedEventArgs e) {
            showRightClickMenu.Close();
            RightClickCommands n = (RightClickCommands) e.ClickedItem.Tag;
            var si = (mLastShowsClicked != null) && (mLastShowsClicked.Count > 0)
                         ? mLastShowsClicked[0]
                         : null;
            switch (n) {
                case RightClickCommands.kEpisodeGuideForShow: // epguide
                    if (mLastEpClicked != null) {
                        GotoEpguideFor(mLastEpClicked, true);
                    } else {
                        if (si != null) {
                            GotoEpguideFor(si, true);
                        }
                    }
                    break;
                case RightClickCommands.kVisitTVDBEpisode: // thetvdb.com
                {
                    TVDBFor(mLastEpClicked);
                    break;
                }
                case RightClickCommands.kVisitTVDBSeason:
                {
                    TVDBFor(mLastSeasonClicked);
                    break;
                }
                case RightClickCommands.kVisitTVDBSeries:
                {
                    if (si != null) {
                        TVDBFor(si);
                    }
                    break;
                }
                case RightClickCommands.kScanSpecificSeries:
                {
                    if (mLastShowsClicked != null) {
                        Scan(mLastShowsClicked);
                        tabControl1.SelectTab(tbAllInOne);
                    }
                    break;
                }
                case RightClickCommands.kWhenToWatchSeries: // when to watch
                {
                    int code = -1;
                    if (mLastEpClicked != null) {
                        code = mLastEpClicked.TheSeries.TVDBCode;
                    }
                    if (si != null) {
                        code = si.TVDBID;
                    }
                    if (code != -1) {
                        tabControl1.SelectTab(tbWTW);
                        for (int i = 0; i < lvWhenToWatch.Items.Count; i++) {
                            lvWhenToWatch.Items[i].Selected = false;
                        }
                        for (int i = 0; i < lvWhenToWatch.Items.Count; i++) {
                            ListViewItem lvi = lvWhenToWatch.Items[i];
                            ProcessedEpisode ei = (ProcessedEpisode) (lvi.Tag);
                            if ((ei != null) && (ei.TheSeries.TVDBCode == code)) {
                                lvi.Selected = true;
                            }
                        }
                        lvWhenToWatch.Focus();
                    }
                    break;
                }
                case RightClickCommands.kForceRefreshSeries:
                    if (si != null) {
                        ForceRefresh(mLastShowsClicked);
                    }
                    break;
                case RightClickCommands.kEditShow:
                    if (si != null) {
                        EditShow(si);
                    }
                    break;
                case RightClickCommands.kDeleteShow:
                    if (si != null) {
                        DeleteShow(si);
                    }
                    break;
                case RightClickCommands.kEditSeason:
                    if (si != null) {
                        EditSeason(si, mLastSeasonClicked.SeasonNumber);
                    }
                    break;
                case RightClickCommands.kBTSearchFor:
                {
                    foreach (ListViewItem lvi in lvAction.SelectedItems) {
                        ItemMissing m = (ItemMissing) (lvi.Tag);
                        if (m != null) {
                            mDoc.DoBTSearch(m.Episode);
                        }
                    }
                }
                    break;
                case RightClickCommands.kActionAction:
                    ActionAction(false);
                    break;
                case RightClickCommands.kActionBrowseForFile:
                {
                    if ((mLastActionsClicked != null) && (mLastActionsClicked.Count > 0)) {
                        ItemMissing mi = (ItemMissing) mLastActionsClicked[0];
                        if (mi != null) {
                            // browse for mLastActionClicked
                            openFile.Filter = "Video Files|" +
                                              mDoc.Settings.VideoExtensions.Replace(".", "*.") +
                                              "|All Files (*.*)|*.*";
                            if (openFile.ShowDialog() == DialogResult.OK) {
                                // make new Item for copying/moving to specified location
                                FileInfo from = new FileInfo(openFile.FileName);
                                mDoc.TheActionList.Add(
                                    new ActionCopyMoveRename(
                                        mDoc.Settings.LeaveOriginals
                                            ? ActionCopyMoveRename.Op.Copy
                                            : ActionCopyMoveRename.Op.Move, from,
                                        new FileInfo(mi.TheFileNoExt + from.Extension), mi.Episode, new TVRenameStats()));
                                // and remove old Missing item
                                mDoc.TheActionList.Remove(mi);
                            }
                        }
                        mLastActionsClicked = null;
                        FillActionList();
                    }
                    break;
                }
                case RightClickCommands.kActionIgnore:
                    IgnoreSelected();
                    break;
                case RightClickCommands.kActionIgnoreSeason:
                {
                    // add season to ignore list for each show selected
                    if ((mLastActionsClicked != null) && (mLastActionsClicked.Count > 0)) {
                        foreach (Item ai in mLastActionsClicked) {
                            ScanListItem er = ai as ScanListItem;
                            if ((er == null) || (er.Episode == null)) {
                                continue;
                            }
                            int snum = er.Episode.SeasonNumber;
                            if (!er.Episode.SI.IgnoreSeasons.Contains(snum)) {
                                er.Episode.SI.IgnoreSeasons.Add(snum);
                            }

                            // remove all other episodes of this season from the Action list
                            var remove = new List<Item>();
                            foreach (Item action in mDoc.TheActionList) {
                                ScanListItem er2 = action as ScanListItem;
                                if ((er2 != null) && (er2.Episode != null) && (er2.Episode.SeasonNumber == snum)) {
                                    remove.Add(action);
                                }
                            }
                            foreach (Item action in remove) {
                                mDoc.TheActionList.Remove(action);
                            }
                            if (remove.Count > 0) {
                                mDoc.SetDirty();
                            }
                        }
                        FillMyShows();
                    }
                    mLastActionsClicked = null;
                    FillActionList();
                    break;
                }
                case RightClickCommands.kActionDelete:
                    ActionDeleteSelected();
                    break;
                default:
                {
                    if ((n >= RightClickCommands.kWatchBase) && (n < RightClickCommands.kOpenFolderBase)) {
                        int wn = n - RightClickCommands.kWatchBase;
                        if ((mLastFL != null) && (wn >= 0) && (wn < mLastFL.Count)) {
                            TVDoc.SysOpen(mLastFL[wn].FullName);
                        }
                    } else {
                        if (n >= RightClickCommands.kOpenFolderBase) {
                            int fnum = n - RightClickCommands.kOpenFolderBase;
                            if (fnum < mFoldersToOpen.Count) {
                                string folder = mFoldersToOpen[fnum];
                                if (Directory.Exists(folder)) {
                                    TVDoc.SysOpen(folder);
                                }
                            }
                            return;
                        } else {
                            System.Diagnostics.Debug.Fail("Unknown right-click action " + n);
                        }
                    }
                    break;
                }
            }
            mLastEpClicked = null;
        }

        public void tabControl1_DoubleClick(object sender, System.EventArgs e) {
            if (tabControl1.SelectedTab == tbMyShows) {
                bnMyShowsRefresh_Click(null, null);
            } else {
                if (tabControl1.SelectedTab == tbWTW) {
                    bnWhenToWatchCheck_Click(null, null);
                } else {
                    if (tabControl1.SelectedTab == tbAllInOne) {
                        bnActionRecentCheck_Click(null, null);
                    }
                }
            }
        }

        public void folderRightClickMenu_ItemClicked(object sender, ToolStripItemClickedEventArgs e) {
            switch ((int) (e.ClickedItem.Tag)) {
                case 0: // open folder
                    TVDoc.SysOpen(mLastFolderClicked);
                    break;
                default:
                    break;
            }
        }

        public void RightClickOnFolder(string folderPath, Point pt) {
            mLastFolderClicked = folderPath;
            folderRightClickMenu.Items.Clear();
            int n = 0;
            ToolStripMenuItem tsi = new ToolStripMenuItem("Open: " + folderPath);
            tsi.Tag = n++;
            folderRightClickMenu.Items.Add(tsi);
            folderRightClickMenu.Show(pt);
        }

        public void lvWhenToWatch_MouseClick(object sender, MouseEventArgs e) {
            if (e.Button != MouseButtons.Right) {
                return;
            }
            if (lvWhenToWatch.SelectedItems.Count == 0) {
                return;
            }
            Point pt = lvWhenToWatch.PointToScreen(new Point(e.X, e.Y));
            var eis = (from ListViewItem lvi in lvWhenToWatch.SelectedItems select lvi.Tag as ProcessedEpisode).ToList();
            WTWRightClickOnShow(eis, pt);
        }

        public void preferencesToolStripMenuItem_Click(object sender, System.EventArgs e) {
            DoPrefs(false);
        }

        public void DoPrefs(bool scanOptions) {
            MoreBusy(); // no background download while preferences are open!
            Preferences pref = new Preferences(mDoc, scanOptions);
            if (pref.ShowDialog() == DialogResult.OK) {
                mDoc.SetDirty();
                mDoc.UpdateTVDBLanguage();
                ShowHideNotificationIcon();
                FillWhenToWatchList();
                ShowInTaskbar = mDoc.Settings.ShowInTaskbar;
                FillEpGuideHTML();
                mAutoFolderMonitor.SettingsChanged(mDoc.Settings.ShouldMonitorFolders);
                ForceRefresh(null);
            }
            LessBusy();
        }

        public void saveToolStripMenuItem_Click(object sender, System.EventArgs e) {
            try {
                //mDoc.WriteXMLSettings(); todo fix saving settings
                mDoc.GetTVDB(false, "").SaveCache();
                SaveLayoutXML();
            } catch (Exception ex) {
                Exception e2 = ex;
                while (e2.InnerException != null) {
                    e2 = e2.InnerException;
                }
                String m2 = e2.Message;
                MessageBox.Show(this,
                                ex.Message + "\r\n\r\n" +
                                m2 + "\r\n\r\n" +
                                ex.StackTrace,
                                "Save Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void UI_SizeChanged(object sender, System.EventArgs e) {
            if (WindowState == FormWindowState.Normal) {
                mLastNonMaximizedSize = Size;
            }
            if ((WindowState == FormWindowState.Minimized) && (!mDoc.Settings.ShowInTaskbar)) {
                Hide();
            }
        }

        public void UI_LocationChanged(object sender, System.EventArgs e) {
            if (WindowState == FormWindowState.Normal) {
                mLastNonMaximizedLocation = Location;
            }
        }

        public void statusTimer_Tick(object sender, System.EventArgs e) {
            int n = mDoc.DownloadDone ? 0 : mDoc.DownloadsRemaining;
            txtDLStatusLabel.Visible = (n != 0 || mDoc.Settings.BGDownload);
            if (n != 0) {
                txtDLStatusLabel.Text = "Background download: " + mDoc.GetTVDB(false, "").CurrentDLTask;
                backgroundDownloadNowToolStripMenuItem.Enabled = false;
            } else {
                txtDLStatusLabel.Text = "Background download: Idle";
            }
            if (Busy == 0) {
                if ((n == 0) && (LastDLRemaining > 0)) {
                    // we've just finished a bunch of background downloads
                    mDoc.GetTVDB(false, "").SaveCache();
                    RefreshWTW(false);
                    backgroundDownloadNowToolStripMenuItem.Enabled = true;
                }
                LastDLRemaining = n;
            }
        }

        public void backgroundDownloadToolStripMenuItem_Click(object sender, System.EventArgs e) {
            mDoc.Settings.BGDownload = !mDoc.Settings.BGDownload;
            backgroundDownloadToolStripMenuItem.Checked = mDoc.Settings.BGDownload;
            statusTimer_Tick(null, null);
            mDoc.SetDirty();
            if (mDoc.Settings.BGDownload) {
                BGDownloadTimer.Start();
            } else {
                BGDownloadTimer.Stop();
            }
        }

        public void BGDownloadTimer_Tick(object sender, System.EventArgs e) {
            if (Busy != 0) {
                BGDownloadTimer.Interval = 10000; // come back in 10 seconds
                BGDownloadTimer.Start();
                return;
            }
            BGDownloadTimer.Interval = BGDLLongInterval(); // after first time (10 seconds), put up to 60 minutes
            BGDownloadTimer.Start();
            if (mDoc.Settings.BGDownload && mDoc.DownloadDone) // only do auto-download if don't have stuff to do already
            {
                mDoc.StartBGDownloadThread(false);
                statusTimer_Tick(null, null);
            }
        }

        public void backgroundDownloadNowToolStripMenuItem_Click(object sender, System.EventArgs e) {
            if (mDoc.Settings.OfflineMode) {
                DialogResult res = MessageBox.Show("Ignore offline mode and download anyway?", "Background Download", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (res != DialogResult.Yes) {
                    return;
                }
            }
            BGDownloadTimer.Stop();
            BGDownloadTimer.Start();
            mDoc.StartBGDownloadThread(false);
            statusTimer_Tick(null, null);
        }

        public void offlineOperationToolStripMenuItem_Click(object sender, System.EventArgs e) {
            if (!mDoc.Settings.OfflineMode) {
                if (MessageBox.Show("Are you sure you wish to go offline?", "TVRename", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No) {
                    return;
                }
            }
            mDoc.Settings.OfflineMode = !mDoc.Settings.OfflineMode;
            offlineOperationToolStripMenuItem.Checked = mDoc.Settings.OfflineMode;
            mDoc.SetDirty();
        }

        public void tabControl1_SelectedIndexChanged(object sender, System.EventArgs e) {
            if (tabControl1.SelectedTab == tbMyShows) {
                FillEpGuideHTML();
            }
            exportToolStripMenuItem.Enabled = false; //( (tabControl1->SelectedTab == tbMissing) ||
            //														  (tabControl1->SelectedTab == tbFnO) ||
            //														  (tabControl1->SelectedTab == tbRenaming) );
        }

        public void bugReportToolStripMenuItem_Click(object sender, System.EventArgs e) {
            BugReport br = new BugReport(mDoc);
            br.ShowDialog();
        }

        public void exportToolStripMenuItem_Click(object sender, System.EventArgs e) {
            // TODO: Export to CSV/XML

            //                
            //				if (tabControl1->SelectedTab == tbMissing)
            //				{
            //				if (!MissingListHasStuff())
            //				return;
            //
            //				saveFile->Filter = "CSV Files (*.csv)|*.csv|XML Files (*.xml)|*.xml";
            //				if (saveFile->ShowDialog() != ::DialogResult::OK)
            //				return;
            //
            //				if (saveFile->FilterIndex == 1) // counts from 1
            //				mDoc->ExportMissingCSV(saveFile->FileName);
            //				else if (saveFile->FilterIndex == 2)
            //				mDoc->ExportMissingXML(saveFile->FileName);
            //				}
            //				else if (tabControl1->SelectedTab == tbFnO)
            //				{
            //				saveFile->Filter = "XML Files|*.xml";
            //				if (saveFile->ShowDialog() != ::DialogResult::OK)
            //				return;
            //				mDoc->ExportFOXML(saveFile->FileName);
            //				}
            //				else if (tabControl1->SelectedTab == tbRenaming)
            //				{
            //				saveFile->Filter = "XML Files|*.xml";
            //				if (saveFile->ShowDialog() != ::DialogResult::OK)
            //				return;
            //				mDoc->ExportRenamingXML(saveFile->FileName);
            //				}
            //				
        }

        public void ShowHideNotificationIcon() {
            notifyIcon1.Visible = mDoc.Settings.NotificationAreaIcon && !mDoc.Args.Hide;
        }

        public void statisticsToolStripMenuItem_Click(object sender, System.EventArgs e) {
            StatsWindow sw = new StatsWindow(mDoc.Stats());
            sw.ShowDialog();
        }

        ////// //////// ////// //////// ////// //////// ////// //////// ////// //////// ////// //////// ////// //////// 
        public TreeNode AddShowItemToTree(MyShowItem si) {
            TheTVDB db = mDoc.GetTVDB(true, "AddShowItemToTree");
            string name = si.ShowName;
            SeriesInfo ser = db.GetSeries(si.TVDBID);
            if (string.IsNullOrEmpty(name)) {
                if (ser != null) {
                    name = ser.Name;
                } else {
                    name = "-- Unknown : " + si.TVDBID + " --";
                }
            }
            TreeNode n = new TreeNode(name);
            n.Tag = si;
            if (ser != null) {
/* todo when fixing color
                if (mDoc.Settings.ShowStatusColors != null)
                {
                    if (mDoc.Settings.ShowStatusColors.IsShowStatusDefined(si.ShowStatus))
                    {
                        n.ForeColor = mDoc.Settings.ShowStatusColors.GetEntry(false, true, si.ShowStatus);
                    }
                    else
                    {
                        Color nodeColor = mDoc.Settings.ShowStatusColors.GetEntry(true, true, si.SeasonsAirStatus.ToString());
                        if (!nodeColor.IsEmpty)
                            n.ForeColor = nodeColor;
                    }
                }
*/
                List<int> theKeys = new List<int>(ser.Seasons.Keys);
                // now, go through and number them all sequentially
                //foreach (int snum in ser.Seasons.Keys)
                //    theKeys.Add(snum);
                theKeys.Sort();
                foreach (int snum in theKeys) {
                    string nodeTitle = snum == 0 ? "Specials" : "Season " + snum;
                    TreeNode n2 = new TreeNode(nodeTitle);
                    if (si.IgnoreSeasons.Contains(snum)) {
                        n2.ForeColor = Color.Gray;
                    } else {
/* todo when fixing color
                        if (mDoc.Settings.ShowStatusColors != null)
                        {
                            Color nodeColor = mDoc.Settings.ShowStatusColors.GetEntry(true, false, ser.Seasons[snum].Status.ToString());
                            if (!nodeColor.IsEmpty)
                                n2.ForeColor = nodeColor;
                        }
*/
                    }
                    n2.Tag = ser.Seasons[snum];
                    n.Nodes.Add(n2);
                }
            }
            MyShowTree.Nodes.Add(n);
            db.Unlock("AddShowItemToTree");
            return n;
        }

        public void UpdateWTW(ProcessedEpisode pe, ListViewItem lvi) {
            lvi.Tag = pe;

            // group 0 = just missed
            //       1 = this week
            //       2 = future / unknown
            DateTime? airdt = pe.GetAirDateDT(true);
            if (airdt == null) {
                // TODO: something!
                return;
            }
            DateTime dt = (DateTime) airdt;
            double ttn = (dt.Subtract(DateTime.Now)).TotalHours;
            if (ttn < 0) {
                lvi.Group = lvWhenToWatch.Groups[0];
            } else {
                if (ttn < (7*24)) {
                    lvi.Group = lvWhenToWatch.Groups[1];
                } else {
                    if (!pe.NextToAir) {
                        lvi.Group = lvWhenToWatch.Groups[3];
                    } else {
                        lvi.Group = lvWhenToWatch.Groups[2];
                    }
                }
            }
            int n = 1;
            lvi.Text = pe.SI.ShowName;
            lvi.SubItems[n++].Text = (pe.SeasonNumber != 0) ? pe.SeasonNumber.ToString() : "Special";
            string estr = (pe.EpNum > 0) ? pe.EpNum.ToString() : "";
            if ((pe.EpNum > 0) && (pe.EpNum2 != pe.EpNum) && (pe.EpNum2 > 0)) {
                estr += "-" + pe.EpNum2;
            }
            lvi.SubItems[n++].Text = estr;
            lvi.SubItems[n++].Text = dt.ToShortDateString();
            lvi.SubItems[n++].Text = dt.ToString("t");
            lvi.SubItems[n++].Text = dt.ToString("ddd");
            lvi.SubItems[n++].Text = pe.HowLong();
            lvi.SubItems[n++].Text = pe.Name;

            // icon..
            if (airdt.Value.CompareTo(DateTime.Now) < 0) // has aired
            {
                List<FileInfo> fl = mDoc.FindEpOnDisk(pe);
                if ((fl != null) && (fl.Count > 0)) {
                    lvi.ImageIndex = 0;
                } else {
                    if (pe.SI.DoMissingCheck) {
                        lvi.ImageIndex = 1;
                    }
                }
            }
        }

        public void SelectSeason(Season seas) {
            foreach (TreeNode n in MyShowTree.Nodes) {
                foreach (TreeNode n2 in n.Nodes) {
                    if (TreeNodeToSeason(n2) == seas) {
                        n2.EnsureVisible();
                        MyShowTree.SelectedNode = n2;
                        return;
                    }
                }
            }
            FillEpGuideHTML(null);
        }

        public void SelectShow(MyShowItem si) {
            foreach (TreeNode n in MyShowTree.Nodes) {
                if (TreeNodeToShowItem(n) == si) {
                    n.EnsureVisible();
                    MyShowTree.SelectedNode = n;
                    //FillEpGuideHTML();
                    return;
                }
            }
            FillEpGuideHTML(null);
        }

        private void bnMyShowsAdd_Click(object sender, System.EventArgs e) {
            MoreBusy();
            var si = new MyShowItem();
            TheTVDB db = mDoc.GetTVDB(true, "AddShow");
            AddEditShow aes = new AddEditShow(si, db);
            DialogResult dr = aes.ShowDialog();
            db.Unlock("AddShow");
            if (dr == DialogResult.OK) {
                mDoc.GetShowItems(true).Add(si);
                mDoc.UnlockShowItems();
                SeriesInfo ser = db.GetSeries(si.TVDBID);
                if (ser != null) {
                    ser.ShowTimeZone = aes.ShowTimeZone;
                }
                ShowAddedOrEdited(true);
                SelectShow(si);
            }
            LessBusy();
        }

        private void ShowAddedOrEdited(bool download) {
            mDoc.SetDirty();
            RefreshWTW(download);
            FillMyShows();
        }

        private void bnMyShowsDelete_Click(object sender, System.EventArgs e) {
            TreeNode n = MyShowTree.SelectedNode;
            var si = TreeNodeToShowItem(n);
            if (si == null) {
                return;
            }
            DeleteShow(si);
        }

        private void DeleteShow(MyShowItem si) {
            DialogResult res = MessageBox.Show("Remove show \"" + si.ShowName + "\".  Are you sure?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (res != DialogResult.Yes) {
                return;
            }
            mDoc.GetShowItems(true).Remove(si);
            mDoc.UnlockShowItems();
            ShowAddedOrEdited(false);
        }

        private void bnMyShowsEdit_Click(object sender, System.EventArgs e) {
            TreeNode n = MyShowTree.SelectedNode;
            if (n == null) {
                return;
            }
            Season seas = TreeNodeToSeason(n);
            if (seas != null) {
                var si = TreeNodeToShowItem(n);
                if (si != null) {
                    EditSeason(si, seas.SeasonNumber);
                }
                return;
            }
            var si2 = TreeNodeToShowItem(n);
            if (si2 != null) {
                EditShow(si2);
                return;
            }
        }

        private void EditSeason(MyShowItem si, int seasnum) {
            MoreBusy();
            TheTVDB db = mDoc.GetTVDB(true, "EditSeason");
            SeriesInfo ser = db.GetSeries(si.TVDBID);
            List<ProcessedEpisode> pel = TVDoc.GenerateEpisodes(si, ser, seasnum, false);
            EditRules er = new EditRules(si, pel, seasnum, mDoc.Settings.NamingStyle);
            DialogResult dr = er.ShowDialog();
            db.Unlock("EditSeason");
            if (dr == DialogResult.OK) {
                ShowAddedOrEdited(false);
                if (ser != null) {
                    SelectSeason(ser.Seasons[seasnum]);
                }
            }
            LessBusy();
        }

        private void EditShow(MyShowItem si) {
            MoreBusy();
            TheTVDB db = mDoc.GetTVDB(true, "EditShow");
            SeriesInfo ser = db.GetSeries(si.TVDBID);
            int oldCode = si.TVDBID;
            AddEditShow aes = new AddEditShow(si, db);
            DialogResult dr = aes.ShowDialog();
            db.Unlock("EditShow");
            if (dr == DialogResult.OK) {
                if (ser != null) {
                    ser.ShowTimeZone = aes.ShowTimeZone; // TODO: move into AddEditShow
                }
                ShowAddedOrEdited(si.TVDBID != oldCode);
                SelectShow(si);
            }
            LessBusy();
        }

        private void ForceRefresh(List<MyShowItem> sis) {
            if (sis != null) {
                foreach (var si in sis) {
                    mDoc.GetTVDB(false, "").ForgetShow(si.TVDBID, true);
                }
            }
            mDoc.DoDownloadsFG();
            FillMyShows();
            FillEpGuideHTML();
            RefreshWTW(false);
        }

        private void bnMyShowsRefresh_Click(object sender, System.EventArgs e) {
            if (Control.ModifierKeys == Keys.Control) {
                // nuke currently selected show to force getting it fresh
                TreeNode n = MyShowTree.SelectedNode;
                var si = TreeNodeToShowItem(n);
                ForceRefresh(new List<MyShowItem>() {si});
            } else {
                ForceRefresh(null);
            }
        }

        private void MyShowTree_AfterSelect(object sender, TreeViewEventArgs e) {
            FillEpGuideHTML(e.Node);
        }

        private void bnMyShowsVisitTVDB_Click(object sender, System.EventArgs e) {
            TreeNode n = MyShowTree.SelectedNode;
            var si = TreeNodeToShowItem(n);
            if (si == null) {
                return;
            }
            Season seas = TreeNodeToSeason(n);
            int sid = -1;
            if (seas != null) {
                sid = seas.SeasonID;
            }
            TVDoc.SysOpen(mDoc.GetTVDB(false, "").WebsiteURL(si.TVDBID, sid, false));
        }

        private void bnMyShowsOpenFolder_Click(object sender, System.EventArgs e) {
            TreeNode n = MyShowTree.SelectedNode;
            MyShowItem si = TreeNodeToShowItem(n);
            if (si == null) {
                return;
            }
            Season seas = TreeNodeToSeason(n);
            Dictionary<int, List<string>> afl = si.AllFolderLocations(mDoc.Settings);
            int[] keys = new int[afl.Count];
            afl.Keys.CopyTo(keys, 0);
            if ((seas == null) && (keys.Length > 0)) {
                string f = si.AutoAdd_FolderBase;
                if (string.IsNullOrEmpty(f)) {
                    int n2 = keys[0];
                    if (afl[n2].Count > 0) {
                        f = afl[n2][0];
                    }
                }
                if (!string.IsNullOrEmpty(f)) {
                    try {
                        TVDoc.SysOpen(f);
                        return;
                    } catch {}
                }
            }
            if ((seas != null) && (afl.ContainsKey(seas.SeasonNumber))) {
                foreach (string folder in afl[seas.SeasonNumber]) {
                    if (Directory.Exists(folder)) {
                        TVDoc.SysOpen(folder);
                        return;
                    }
                }
            }
            try {
                if (!string.IsNullOrEmpty(si.AutoAdd_FolderBase) && (Directory.Exists(si.AutoAdd_FolderBase))) {
                    TVDoc.SysOpen(si.AutoAdd_FolderBase);
                }
            } catch {}
        }

        private void MyShowTree_MouseClick(object sender, MouseEventArgs e) {
            if (e.Button != MouseButtons.Right) {
                return;
            }
            MyShowTree.SelectedNode = MyShowTree.GetNodeAt(e.X, e.Y);
            Point pt = MyShowTree.PointToScreen(new Point(e.X, e.Y));
            TreeNode n = MyShowTree.SelectedNode;
            if (n == null) {
                return;
            }
            MyShowItem si = TreeNodeToShowItem(n);
            Season seas = TreeNodeToSeason(n);
            if (seas != null) {
                RightClickOnMyShows(seas, pt);
            } else {
                if (si != null) {
                    RightClickOnMyShows(si, pt);
                }
            }
        }

        private void quickstartGuideToolStripMenuItem_Click(object sender, System.EventArgs e) {
            ShowQuickStartGuide();
        }

        private List<ProcessedEpisode> CurrentlySelectedPEL() {
            Season currentSeas = TreeNodeToSeason(MyShowTree.SelectedNode);
            MyShowItem currentSI = TreeNodeToShowItem(MyShowTree.SelectedNode);
            int snum = (currentSeas != null) ? currentSeas.SeasonNumber : 1;
            List<ProcessedEpisode> pel = null;
            if ((currentSI != null) && (currentSI.SeasonEpisodes.ContainsKey(snum))) {
                pel = currentSI.SeasonEpisodes[snum];
            } else {
                foreach (var si in mDoc.GetShowItems(true)) {
                    foreach (var kvp in si.SeasonEpisodes) {
                        pel = kvp.Value;
                        break;
                    }
                    if (pel != null) {
                        break;
                    }
                }
                mDoc.UnlockShowItems();
            }
            return pel;
        }

        private void filenameTemplateEditorToolStripMenuItem_Click(object sender, System.EventArgs e) {
            CustomName cn = new CustomName(mDoc.Settings.NamingStyle.StyleString);
            CustomNameDesigner cne = new CustomNameDesigner(CurrentlySelectedPEL(), cn, mDoc);
            DialogResult dr = cne.ShowDialog();
            if (dr == DialogResult.OK) {
                mDoc.Settings.NamingStyle = cn;
                mDoc.SetDirty();
            }
        }

        private void searchEnginesToolStripMenuItem_Click(object sender, System.EventArgs e) {
            List<ProcessedEpisode> pel = CurrentlySelectedPEL();
            AddEditSearchEngine aese = new AddEditSearchEngine(mDoc.GetSearchers(), ((pel != null) && (pel.Count > 0)) ? pel[0] : null);
            DialogResult dr = aese.ShowDialog();
            if (dr == DialogResult.OK) {
                mDoc.SetDirty();
                UpdateSearchButton();
            }
        }

        private void filenameProcessorsToolStripMenuItem_Click(object sender, System.EventArgs e) {
            //Season ^currentSeas = TreeNodeToSeason(MyShowTree->SelectedNode);
            MyShowItem currentSI = TreeNodeToShowItem(MyShowTree.SelectedNode);
            string theFolder = "";
            if (currentSI != null) {
                foreach (KeyValuePair<int, List<string>> kvp in currentSI.AllFolderLocations(mDoc.Settings)) {
                    foreach (string folder in kvp.Value) {
                        if ((!string.IsNullOrEmpty(folder)) && Directory.Exists(folder)) {
                            theFolder = folder;
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(theFolder)) {
                        break;
                    }
                }
            }
            AddEditSeasEpFinders d = new AddEditSeasEpFinders(mDoc, currentSI, theFolder);
            mDoc.UnlockShowItems();
            DialogResult dr = d.ShowDialog();
            if (dr == DialogResult.OK) {
                mDoc.SetDirty();
            }
        }

        private void actorsToolStripMenuItem_Click(object sender, System.EventArgs e) {
            new ActorsGrid(mDoc).ShowDialog();
        }

        private void quickTimer_Tick(object sender, System.EventArgs e) {
            quickTimer.Stop();
            ProcessArgs();
        }

        private void uTorrentToolStripMenuItem_Click(object sender, System.EventArgs e) {
            uTorrent ut = new uTorrent(mDoc, SetProgress);
            ut.ShowDialog();
            tabControl1.SelectedIndex = 1; // go to all-in-one tab
        }

        private void bnMyShowsCollapse_Click(object sender, System.EventArgs e) {
            MyShowTree.BeginUpdate();
            treeExpandCollapseToggle = !treeExpandCollapseToggle;
            if (treeExpandCollapseToggle) {
                MyShowTree.CollapseAll();
            } else {
                MyShowTree.ExpandAll();
            }
            if (MyShowTree.SelectedNode != null) {
                MyShowTree.SelectedNode.EnsureVisible();
            }
            MyShowTree.EndUpdate();
        }

        private void UI_KeyDown(object sender, KeyEventArgs e) {
            int t = -1;
            if (e.Control && (e.KeyCode == Keys.D1)) {
                t = 0;
            } else {
                if (e.Control && (e.KeyCode == Keys.D2)) {
                    t = 1;
                } else {
                    if (e.Control && (e.KeyCode == Keys.D3)) {
                        t = 2;
                    } else {
                        if (e.Control && (e.KeyCode == Keys.D4)) {
                            t = 3;
                        } else {
                            if (e.Control && (e.KeyCode == Keys.D5)) {
                                t = 4;
                            } else {
                                if (e.Control && (e.KeyCode == Keys.D6)) {
                                    t = 5;
                                } else {
                                    if (e.Control && (e.KeyCode == Keys.D7)) {
                                        t = 6;
                                    } else {
                                        if (e.Control && (e.KeyCode == Keys.D8)) {
                                            t = 7;
                                        } else {
                                            if (e.Control && (e.KeyCode == Keys.D9)) {
                                                t = 8;
                                            } else {
                                                if (e.Control && (e.KeyCode == Keys.D0)) {
                                                    t = 9;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if ((t >= 0) && (t < tabControl1.TabCount)) {
                tabControl1.SelectedIndex = t;
                e.Handled = true;
            }
        }

        private void bnActionCheck_Click(object sender, System.EventArgs e) {
            ScanAll();
            mDoc.ExportMissingXML(); //Save missing shows to XML
        }

        private void ScanAll() {
            tabControl1.SelectedTab = tbAllInOne;
            Scan(null);
        }

        private void ScanRecent() {
            // only scan "recent" shows
            var shows = new List<MyShowItem>();
            int dd = mDoc.Settings.WTWRecentDays;

            // for each show, see if any episodes were aired in "recent" days...
            foreach (var si in mDoc.GetShowItems(true)) {
                bool added = false;
                foreach (var kvp in si.SeasonEpisodes) {
                    if (added) {
                        break;
                    }
                    if (si.IgnoreSeasons.Contains(kvp.Key)) {
                        continue; // ignore this season
                    }
                    var eis = kvp.Value;
                    foreach (ProcessedEpisode ei in eis) {
                        DateTime? dt = ei.GetAirDateDT(true);
                        if ((dt != null) && (dt.Value.CompareTo(DateTime.MaxValue) != 0)) {
                            TimeSpan ts = dt.Value.Subtract(DateTime.Now);
                            if ((ts.TotalHours >= (-24*dd)) && (ts.TotalHours <= 0)) // fairly recent?
                            {
                                shows.Add(si);
                                added = true;
                                break;
                            }
                        }
                    }
                }
            }
            Scan(shows);
        }

        private void Scan(List<MyShowItem> shows) {
            MoreBusy();
            mDoc.ActionGo(shows);
            LessBusy();
            FillMyShows(); // scanning can download more info to be displayed in my shows
            FillActionList();
        }

        private static string GBMB(long size) {
            long gb1 = (1024*1024*1024);
            long gb = ((gb1/2) + size)/gb1;
            if (gb > 1) {
                return gb + " GB";
            } else {
                long mb1 = 1024*1024;
                long mb = ((mb1/2) + size)/mb1;
                return mb + " MB";
            }
        }

        private static string itemitems(int n) {
            return n == 1 ? "Item" : "Items";
        }

        private ListViewItem LVIForItem(Item item) {
            ScanListItem sli = item as ScanListItem;
            if (sli == null) {
                return new ListViewItem();
            }
            ListViewItem lvi = sli.ScanListViewItem;
            lvi.Group = lvAction.Groups[sli.ScanListViewGroup];
            if (sli.IconNumber != -1) {
                lvi.ImageIndex = sli.IconNumber;
            }
            lvi.Checked = true;
            lvi.Tag = sli;
            const int kErrCol = 8;
            System.Diagnostics.Debug.Assert(lvi.SubItems.Count <= kErrCol);
            while (lvi.SubItems.Count < kErrCol) {
                lvi.SubItems.Add(""); // pad our way to the error column
            }
            var act = item as Action;
            if ((act != null) && act.Error) {
                lvi.BackColor = Helpers.WarningColor();
                lvi.SubItems.Add(act.ErrorText); // error text
            } else {
                lvi.SubItems.Add("");
            }
            if (!(item is Action)) {
                lvi.Checked = false;
            }
            System.Diagnostics.Debug.Assert(lvi.SubItems.Count == lvAction.Columns.Count);
            return lvi;
        }

        private void lvAction_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e) {
            Item item = mDoc.TheActionList[e.ItemIndex];
            e.Item = LVIForItem(item);
        }

        private void FillActionList() {
            InternalCheckChange = true;
            if (lvAction.VirtualMode) {
                lvAction.VirtualListSize = mDoc.TheActionList.Count;
            } else {
                lvAction.BeginUpdate();
                lvAction.Items.Clear();
                foreach (Item item in mDoc.TheActionList) {
                    ListViewItem lvi = LVIForItem(item);
                    lvAction.Items.Add(lvi);
                }
                lvAction.EndUpdate();
            }

            // do nice totals for each group
            int missingCount = 0;
            int renameCount = 0;
            int copyCount = 0;
            long copySize = 0;
            int moveCount = 0;
            long moveSize = 0;
            int rssCount = 0;
            int downloadCount = 0;
            int nfoCount = 0;
            int metaCount = 0;
            int dlCount = 0;
            foreach (Item Action in mDoc.TheActionList) {
                if (Action is ItemMissing) {
                    missingCount++;
                } else {
                    if (Action is ActionCopyMoveRename) {
                        ActionCopyMoveRename cmr = (ActionCopyMoveRename) (Action);
                        ActionCopyMoveRename.Op op = cmr.Operation;
                        if (op == ActionCopyMoveRename.Op.Copy) {
                            copyCount++;
                            if (cmr.From.Exists) {
                                copySize += cmr.From.Length;
                            }
                        } else {
                            if (op == ActionCopyMoveRename.Op.Move) {
                                moveCount++;
                                if (cmr.From.Exists) {
                                    moveSize += cmr.From.Length;
                                }
                            } else {
                                if (op == ActionCopyMoveRename.Op.Rename) {
                                    renameCount++;
                                }
                            }
                        }
                    } else {
                        if (Action is ActionDownload) {
                            downloadCount++;
                        } else {
                            if (Action is ActionRSS) {
                                rssCount++;
                            } else {
                                if (Action is ActionNFO) {
                                    nfoCount++;
                                } else {
                                    if (Action is ActionPyTivoMeta) {
                                        metaCount++;
                                    } else {
                                        if (Action is ItemuTorrenting || Action is ItemSABnzbd) {
                                            dlCount++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            lvAction.Groups[0].Header = "Missing (" + missingCount + " " + itemitems(missingCount) + ")";
            lvAction.Groups[1].Header = "Rename (" + renameCount + " " + itemitems(renameCount) + ")";
            lvAction.Groups[2].Header = "Copy (" + copyCount + " " + itemitems(copyCount) + ", " + GBMB(copySize) + ")";
            lvAction.Groups[3].Header = "Move (" + moveCount + " " + itemitems(moveCount) + ", " + GBMB(moveSize) + ")";
            lvAction.Groups[4].Header = "Download RSS (" + rssCount + " " + itemitems(rssCount) + ")";
            lvAction.Groups[5].Header = "Download (" + downloadCount + " " + itemitems(downloadCount) + ")";
            lvAction.Groups[6].Header = "NFO File (" + nfoCount + " " + itemitems(nfoCount) + ")";
            lvAction.Groups[7].Header = "pyTiovo Meta File (" + metaCount + " " + itemitems(metaCount) + ")";
            lvAction.Groups[8].Header = "Downloading (" + dlCount + " " + itemitems(dlCount) + ")";
            InternalCheckChange = false;
            UpdateActionCheckboxes();
        }

        private void bnActionAction_Click(object sender, System.EventArgs e) {
            ActionAction(true);
        }

        private void ActionAll() {
            ActionAction(true);
        }

        private void ActionAction(bool checkedNotSelected) {
            LVResults lvr = new LVResults(lvAction, checkedNotSelected);
            mDoc.DoActions(lvr.FlatList);
            // remove items from master list, unless it had an error
            foreach (Item i2 in (new LVResults(lvAction, checkedNotSelected)).FlatList) {
                ScanListItem sli = i2 as ScanListItem;
                if ((sli != null) && (!lvr.FlatList.Contains(sli))) {
                    mDoc.TheActionList.Remove(i2);
                }
            }
            FillActionList();
            RefreshWTW(false);
        }

        private void folderMonitorToolStripMenuItem_Click(object sender, System.EventArgs e) {
            FolderMonitor fm = new FolderMonitor(mDoc);
            fm.ShowDialog();
            FillMyShows();
        }

        private void torrentMatchToolStripMenuItem_Click(object sender, System.EventArgs e) {
            TorrentMatch tm = new TorrentMatch(mDoc, SetProgress);
            tm.ShowDialog();
            FillActionList();
        }

        private void bnActionWhichSearch_Click(object sender, System.EventArgs e) {
            ChooseSiteMenu(0);
        }

        private void lvAction_MouseClick(object sender, MouseEventArgs e) {
            if (e.Button != MouseButtons.Right) {
                return;
            }

            // build the right click menu for the _selected_ items, and types of items
            LVResults lvr = new LVResults(lvAction, false);
            if (lvr.Count == 0) {
                return; // nothing selected
            }
            Point pt = lvAction.PointToScreen(new Point(e.X, e.Y));
            showRightClickMenu.Items.Clear();

            // Action related items
            ToolStripMenuItem tsi;
            if (lvr.Count > lvr.Missing.Count) // not just missing selected
            {
                tsi = new ToolStripMenuItem("Action Selected");
                tsi.Tag = (int) RightClickCommands.kActionAction;
                showRightClickMenu.Items.Add(tsi);
            }
            tsi = new ToolStripMenuItem("Ignore Selected");
            tsi.Tag = (int) RightClickCommands.kActionIgnore;
            showRightClickMenu.Items.Add(tsi);
            tsi = new ToolStripMenuItem("Ignore Entire Season");
            tsi.Tag = (int) RightClickCommands.kActionIgnoreSeason;
            showRightClickMenu.Items.Add(tsi);
            tsi = new ToolStripMenuItem("Remove Selected");
            tsi.Tag = (int) RightClickCommands.kActionDelete;
            showRightClickMenu.Items.Add(tsi);
            if (lvr.Count == lvr.Missing.Count) // only missing items selected?
            {
                showRightClickMenu.Items.Add(new ToolStripSeparator());
                tsi = new ToolStripMenuItem("Search");
                tsi.Tag = (int) RightClickCommands.kBTSearchFor;
                showRightClickMenu.Items.Add(tsi);
                if (lvr.Count == 1) // only one selected
                {
                    tsi = new ToolStripMenuItem("Browse For...");
                    tsi.Tag = (int) RightClickCommands.kActionBrowseForFile;
                    showRightClickMenu.Items.Add(tsi);
                }
            }
            MenuGuideAndTVDB(true);
            MenuFolders(lvr);
            showRightClickMenu.Show(pt);
        }

        private void lvAction_SelectedIndexChanged(object sender, System.EventArgs e) {
            UpdateSearchButton();
            LVResults lvr = new LVResults(lvAction, false);
            if (lvr.Count == 0) {
                // disable everything
                bnActionBTSearch.Enabled = false;
                return;
            }
            bnActionBTSearch.Enabled = lvr.Download.Count <= 0;
            mLastShowsClicked = null;
            mLastEpClicked = null;
            mLastSeasonClicked = null;
            mLastActionsClicked = null;
            showRightClickMenu.Items.Clear();
            mFoldersToOpen = new List<String>();
            mLastFL = new List<FileInfo>();
            mLastActionsClicked = new List<Item>();
            foreach (Item ai in lvr.FlatList) {
                mLastActionsClicked.Add(ai);
            }
            if ((lvr.Count == 1) && (lvAction.FocusedItem != null) && (lvAction.FocusedItem.Tag != null)) {
                ScanListItem action = lvAction.FocusedItem.Tag as ScanListItem;
                if (action != null) {
                    mLastEpClicked = action.Episode;
                    if (action.Episode != null) {
                        mLastSeasonClicked = action.Episode.TheSeason;
                        mLastShowsClicked = new List<MyShowItem> {action.Episode.SI};
                    } else {
                        mLastSeasonClicked = null;
                        mLastShowsClicked = null;
                    }
                    if ((mLastEpClicked != null) && (mDoc.Settings.AutoSelectShowInMyShows)) {
                        GotoEpguideFor(mLastEpClicked, false);
                    }
                }
            }
        }

        private void ActionDeleteSelected() {
            ListView.SelectedListViewItemCollection sel = lvAction.SelectedItems;
            foreach (ListViewItem lvi in sel) {
                mDoc.TheActionList.Remove((Item) (lvi.Tag));
            }
            FillActionList();
        }

        private void lvAction_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Delete) {
                ActionDeleteSelected();
            }
        }

        private void cbActionIgnore_Click(object sender, System.EventArgs e) {
            IgnoreSelected();
        }

        private void UpdateActionCheckboxes() {
            if (InternalCheckChange) {
                return;
            }
            LVResults all = new LVResults(lvAction, LVResults.WhichResults.All);
            LVResults chk = new LVResults(lvAction, LVResults.WhichResults.Checked);
            if (chk.Rename.Count == 0) {
                cbRename.CheckState = CheckState.Unchecked;
            } else {
                cbRename.CheckState = (chk.Rename.Count == all.Rename.Count) ? CheckState.Checked : CheckState.Indeterminate;
            }
            if (chk.CopyMove.Count == 0) {
                cbCopyMove.CheckState = CheckState.Unchecked;
            } else {
                cbCopyMove.CheckState = (chk.CopyMove.Count == all.CopyMove.Count) ? CheckState.Checked : CheckState.Indeterminate;
            }
            if (chk.RSS.Count == 0) {
                cbRSS.CheckState = CheckState.Unchecked;
            } else {
                cbRSS.CheckState = (chk.RSS.Count == all.RSS.Count) ? CheckState.Checked : CheckState.Indeterminate;
            }
            if (chk.Download.Count == 0) {
                cbDownload.CheckState = CheckState.Unchecked;
            } else {
                cbDownload.CheckState = (chk.Download.Count == all.Download.Count) ? CheckState.Checked : CheckState.Indeterminate;
            }
            if (chk.NFO.Count == 0) {
                cbNFO.CheckState = CheckState.Unchecked;
            } else {
                cbNFO.CheckState = (chk.NFO.Count == all.NFO.Count) ? CheckState.Checked : CheckState.Indeterminate;
            }
            if (chk.PyTivoMeta.Count == 0) {
                cbMeta.CheckState = CheckState.Unchecked;
            } else {
                cbMeta.CheckState = (chk.PyTivoMeta.Count == all.PyTivoMeta.Count) ? CheckState.Checked : CheckState.Indeterminate;
            }
            int total1 = all.Rename.Count + all.CopyMove.Count + all.RSS.Count + all.Download.Count + all.NFO.Count + all.PyTivoMeta.Count;
            int total2 = chk.Rename.Count + chk.CopyMove.Count + chk.RSS.Count + chk.Download.Count + chk.NFO.Count + chk.PyTivoMeta.Count;
            if (total2 == 0) {
                cbAll.CheckState = CheckState.Unchecked;
            } else {
                cbAll.CheckState = (total2 == total1) ? CheckState.Checked : CheckState.Indeterminate;
            }
        }

        private void cbActionAllNone_Click(object sender, System.EventArgs e) {
            CheckState cs = cbAll.CheckState;
            if (cs == CheckState.Indeterminate) {
                cbAll.CheckState = CheckState.Unchecked;
                cs = CheckState.Unchecked;
            }
            InternalCheckChange = true;
            foreach (ListViewItem lvi in lvAction.Items) {
                lvi.Checked = cs == CheckState.Checked;
            }
            InternalCheckChange = false;
            UpdateActionCheckboxes();
        }

        private void cbActionRename_Click(object sender, System.EventArgs e) {
            CheckState cs = cbRename.CheckState;
            if (cs == CheckState.Indeterminate) {
                cbRename.CheckState = CheckState.Unchecked;
                cs = CheckState.Unchecked;
            }
            InternalCheckChange = true;
            foreach (ListViewItem lvi in lvAction.Items) {
                Item i = (Item) (lvi.Tag);
                if ((i != null) && (i is ActionCopyMoveRename) && (((ActionCopyMoveRename) i).Operation == ActionCopyMoveRename.Op.Rename)) {
                    lvi.Checked = cs == CheckState.Checked;
                }
            }
            InternalCheckChange = false;
            UpdateActionCheckboxes();
        }

        private void cbActionCopyMove_Click(object sender, System.EventArgs e) {
            CheckState cs = cbCopyMove.CheckState;
            if (cs == CheckState.Indeterminate) {
                cbCopyMove.CheckState = CheckState.Unchecked;
                cs = CheckState.Unchecked;
            }
            InternalCheckChange = true;
            foreach (ListViewItem lvi in lvAction.Items) {
                Item i = (Item) (lvi.Tag);
                if ((i != null) && (i is ActionCopyMoveRename) && (((ActionCopyMoveRename) i).Operation != ActionCopyMoveRename.Op.Rename)) {
                    lvi.Checked = cs == CheckState.Checked;
                }
            }
            InternalCheckChange = false;
            UpdateActionCheckboxes();
        }

        private void cbActionNFO_Click(object sender, System.EventArgs e) {
            CheckState cs = cbNFO.CheckState;
            if (cs == CheckState.Indeterminate) {
                cbNFO.CheckState = CheckState.Unchecked;
                cs = CheckState.Unchecked;
            }
            InternalCheckChange = true;
            foreach (ListViewItem lvi in lvAction.Items) {
                Item i = (Item) (lvi.Tag);
                if ((i != null) && (i is ActionNFO)) {
                    lvi.Checked = cs == CheckState.Checked;
                }
            }
            InternalCheckChange = false;
            UpdateActionCheckboxes();
        }

        private void cbActionPyTivoMeta_Click(object sender, System.EventArgs e) {
            CheckState cs = cbMeta.CheckState;
            if (cs == CheckState.Indeterminate) {
                cbMeta.CheckState = CheckState.Unchecked;
                cs = CheckState.Unchecked;
            }
            InternalCheckChange = true;
            foreach (ListViewItem lvi in lvAction.Items) {
                Item i = (Item) (lvi.Tag);
                if ((i != null) && (i is ActionPyTivoMeta)) {
                    lvi.Checked = cs == CheckState.Checked;
                }
            }
            InternalCheckChange = false;
            UpdateActionCheckboxes();
        }

        private void cbActionRSS_Click(object sender, System.EventArgs e) {
            CheckState cs = cbRSS.CheckState;
            if (cs == CheckState.Indeterminate) {
                cbRSS.CheckState = CheckState.Unchecked;
                cs = CheckState.Unchecked;
            }
            InternalCheckChange = true;
            foreach (ListViewItem lvi in lvAction.Items) {
                Item i = (Item) (lvi.Tag);
                if ((i != null) && (i is ActionRSS)) {
                    lvi.Checked = cs == CheckState.Checked;
                }
            }
            InternalCheckChange = false;
            UpdateActionCheckboxes();
        }

        private void cbActionDownloads_Click(object sender, System.EventArgs e) {
            CheckState cs = cbDownload.CheckState;
            if (cs == CheckState.Indeterminate) {
                cbDownload.CheckState = CheckState.Unchecked;
                cs = CheckState.Unchecked;
            }
            InternalCheckChange = true;
            foreach (ListViewItem lvi in lvAction.Items) {
                Item i = (Item) (lvi.Tag);
                if ((i != null) && (i is ActionDownload)) {
                    lvi.Checked = cs == CheckState.Checked;
                }
            }
            InternalCheckChange = false;
            UpdateActionCheckboxes();
        }

        private void lvAction_ItemCheck(object sender, ItemCheckEventArgs e) {
            if ((e.Index < 0) || (e.Index > lvAction.Items.Count)) {
                return;
            }
            Item Action = (Item) (lvAction.Items[e.Index].Tag);
            if ((Action != null) && ((Action is ItemMissing) || (Action is ItemuTorrenting) || (Action is ItemSABnzbd))) {
                e.NewValue = CheckState.Unchecked;
            }
        }

        private void bnActionOptions_Click(object sender, System.EventArgs e) {
            DoPrefs(true);
        }

        private void lvAction_MouseDoubleClick(object sender, MouseEventArgs e) {
            // double-click on an item will search for missing, do nothing (for now) for anything else
            foreach (ItemMissing miss in new LVResults(lvAction, false).Missing) {
                if (miss.Episode != null) {
                    mDoc.DoBTSearch(miss.Episode);
                }
            }
        }

        private void bnActionBTSearch_Click(object sender, System.EventArgs e) {
            LVResults lvr = new LVResults(lvAction, false);
            if (lvr.Count == 0) {
                return;
            }
            foreach (Item i in lvr.FlatList) {
                ScanListItem sli = i as ScanListItem;
                if ((sli != null) && (sli.Episode != null)) {
                    mDoc.DoBTSearch(sli.Episode);
                }
            }
        }

        private void bnRemoveSel_Click(object sender, System.EventArgs e) {
            ActionDeleteSelected();
        }

        private void IgnoreSelected() {
            LVResults lvr = new LVResults(lvAction, false);
            bool added = false;
            foreach (ScanListItem Action in lvr.FlatList) {
                IgnoreItem ii = Action.Ignore;
                if (ii != null) {
                    mDoc.Ignore.Add(ii);
                    added = true;
                }
            }
            if (added) {
                mDoc.SetDirty();
                mDoc.RemoveIgnored();
                FillActionList();
            }
        }

        private void ignoreListToolStripMenuItem_Click(object sender, System.EventArgs e) {
            IgnoreEdit ie = new IgnoreEdit(mDoc);
            ie.ShowDialog();
        }

        private void lvAction_ItemChecked(object sender, ItemCheckedEventArgs e) {
            UpdateActionCheckboxes();
        }

        private void bnHideHTMLPanel_Click(object sender, EventArgs e) {
            if (splitContainer1.Panel2Collapsed) {
                splitContainer1.Panel2Collapsed = false;
                bnHideHTMLPanel.ImageKey = "FillRight.bmp";
            } else {
                splitContainer1.Panel2Collapsed = true;
                bnHideHTMLPanel.ImageKey = "FillLeft.bmp";
            }
        }

        private void bnActionRecentCheck_Click(object sender, EventArgs e) {
            ScanRecent();
        }
    }
}