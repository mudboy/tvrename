// 
// Main website for TVRename is http://tvrename.com
// 
// Source code available at http://code.google.com/p/tvrename/
// 
// This code is released under GPLv3 http://www.gnu.org/licenses/gpl.html
// 
 // "Doc" is short for "Document", from the "Document" and "View" model way of thinking of things.
// All the processing and work should be done in here, nothing in UI.cs
// Means we can run TVRename and do useful stuff, without showing any UI. (i.e. text mode / console app)

//todo remove all ui related code

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using TvRename.Core.Actions;
using TvRename.Core.BT;
using TvRename.Core.Cache;
using TvRename.Core.Items;
using TvRename.Core.Settings;
using TvRename.TheTVDB;
using TvRename.Utils;
using Action = TvRename.Core.Actions.Action;
using RSSItem = TvRename.Core.RSS.RSSItem;
using RSSItemList = TvRename.Core.RSS.RSSItemList;
using TimeZone = TvRename.Utils.TimeZone;
using TvRename.Core.Settings.Serialized;

namespace TvRename.Core
{
    public class TVDoc
    {
        public TvSettings Settings;
        public List<ShowItem> ShowItems;
        public List<string> MonitorFolders;
        public List<string> IgnoreFolders;
        public List<string> SearchFolders;
        public List<IgnoreItem> Ignore;

        public bool ActionCancel;
        public bool ActionPause;
        private Thread ActionProcessorThread;
        private Semaphore[] ActionSemaphores;
        private bool ActionStarting;
        private List<Thread> ActionWorkers;
        public FolderMonitorEntryList AddItems;
        public CommandLineArgs Args;

        public bool DownloadDone;
        private bool DownloadOK;
        public int DownloadPct;
        public bool DownloadStopOnError;
        public int DownloadsRemaining;
        public string LoadErr;
        //public bool LoadOK;
        public RSSItemList RSSList;
        //todo public ScanProgress ScanProgDlg;
        public IList<Item> TheActionList;
        public Semaphore WorkerSemaphore;
        public List<Thread> Workers;
        private bool mDirty;
        private Thread mDownloaderThread;
        private TVRenameStats mStats;
        private TheTVDB.TheTVDB mTVDB;

        public TVDoc(TvSettings z, TheTVDB.TheTVDB tvdb, CommandLineArgs args)
        {
            mTVDB = tvdb;
            Args = args;

            Ignore = new List<IgnoreItem>();

            Workers = null;
            WorkerSemaphore = null;

            mStats = new TVRenameStats();
            mDirty = false;
            TheActionList = new List<Item>();

            Settings = z;// = new TVSettings();

            MonitorFolders = new List<String>();
            IgnoreFolders = new List<String>();
            SearchFolders = new List<String>();
            
            ShowItems = new List<ShowItem>();
            AddItems = new FolderMonitorEntryList();

            DownloadDone = true;
            DownloadOK = true;

            ActionCancel = false;
            //todo ScanProgDlg = null;

            //LoadOK = ((settingsFile == null) || LoadXMLSettings(settingsFile)) && mTVDB.LoadOK;

            UpdateTVDBLanguage();

            //    StartServer();
        }

        public void UpdateTVDBLanguage()
        {
            mTVDB.RequestLanguage = Settings.PreferredLanguage;
        }

        public TheTVDB.TheTVDB GetTVDB(bool lockDB, string whoFor)
        {
            if (lockDB)
            {
                System.Diagnostics.Debug.Assert(!String.IsNullOrEmpty(whoFor));
                if (string.IsNullOrEmpty(whoFor))
                    whoFor = "unknown";

                mTVDB.GetLock("GetTVDB : " + whoFor);
            }
            return mTVDB;
        }

        
        ~TVDoc()
        {
            StopBGDownloadThread();
        }

        private void LockShowItems()
        {
            return;
            /*#if DEBUG
                             System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(1);
                             System.Diagnostics.StackFrame sf = st.GetFrame(0);
                             string msg = sf.GetMethod().DeclaringType.FullName + "::" + sf.GetMethod().Name;
                             System.Diagnostics.Debug.Print("LockShowItems " + msg);
            #endif
                             Monitor.Enter(ShowItems);
                    */
        }

        public void UnlockShowItems()
        {
            return;
            /*
    #if DEBUG
                    System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(1);
                    System.Diagnostics.StackFrame sf = st.GetFrame(0);
                    string msg = sf.GetMethod().DeclaringType.FullName + "::" + sf.GetMethod().Name;
                    System.Diagnostics.Debug.Print("UnlockShowItems " + msg);
    #endif

                    Monitor.Exit(ShowItems);
             */
        }

        public TVRenameStats Stats()
        {
            mStats.NS_NumberOfShows = ShowItems.Count;
            mStats.NS_NumberOfSeasons = 0;
            mStats.NS_NumberOfEpisodesExpected = 0;

            LockShowItems();
            foreach (ShowItem si in ShowItems)
            {
                foreach (var episodes in si.SeasonEpisodes.Values)
                    mStats.NS_NumberOfEpisodesExpected += episodes.Count;
                mStats.NS_NumberOfSeasons += si.SeasonEpisodes.Count;
            }
            UnlockShowItems();

            return mStats;
        }

        public void SetDirty()
        {
            mDirty = true;
        }

        public bool Dirty()
        {
            return mDirty;
        }

        public List<ShowItem> GetShowItems(bool lockThem)
        {
            if (lockThem)
                LockShowItems();

            ShowItems.Sort(ShowItem.CompareShowItemNames);
            return ShowItems;
        }

        public ShowItem GetShowItem(int id)
        {
            LockShowItems();
            foreach (ShowItem si in ShowItems)
            {
                if (si.TVDBCode == id)
                {
                    UnlockShowItems();
                    return si;
                }
            }
            UnlockShowItems();
            return null;
        }

        public void SetSearcher(int n)
        {
            Settings.TheSearchers.SetToNumber(n);
            SetDirty();
        }

        public bool FolderIsSubfolderOf(string thisOne, string ofThat)
        {
            // need terminating slash, otherwise "c:\abc def" will match "c:\abc"
            thisOne += Path.DirectorySeparatorChar.ToString();
            ofThat += Path.DirectorySeparatorChar.ToString();
            int l = ofThat.Length;
            return ((thisOne.Length >= l) && (thisOne.Substring(0, l).ToLower() == ofThat.ToLower()));
        }

        readonly string[] _seasonWords = { "Season", // EN
                                           "Saison", // FR, DE
                                           "temporada" // ES
                                         }; // TODO: move into settings, and allow user to edit these

        public bool MonitorFolderHasSeasonFolders(DirectoryInfo di, out string folderName)
        {
            try
            {
                // keep in sync with ProcessAddItems, etc.
                foreach (string sw in _seasonWords)
                {
                    var di2 = di.GetDirectories("*" + sw + " *");
                    if (di2.Length == 0)
                        continue;

                    folderName = sw;
                    return true;
                }
            }
            catch (UnauthorizedAccessException)
            {
                // e.g. recycle bin, system volume information
            }
            folderName = null;
            return false;
        }

        public bool MonitorAddSingleFolder(DirectoryInfo di2, bool andGuess)
        {
            // ..and not already a folder for one of our shows
            string theFolder = di2.FullName.ToLower();
            bool alreadyHaveIt = false;
            foreach (ShowItem si in ShowItems)
            {
                if (si.AutoAddNewSeasons && !string.IsNullOrEmpty(si.AutoAdd_FolderBase) && FolderIsSubfolderOf(theFolder, si.AutoAdd_FolderBase))
                {
                    // we're looking at a folder that is a subfolder of an existing show
                    alreadyHaveIt = true;
                    break;
                }

                var afl = si.AllFolderLocations(Settings);
                if (afl.Any(kvp => kvp.Value.Any(folder => theFolder.ToLower() == folder.ToLower()))) {
                    alreadyHaveIt = true;
                    break;
                }
            } // for each showitem

            bool hasSeasonFolders = false;
            try
            {
                string folderName;
                hasSeasonFolders = MonitorFolderHasSeasonFolders(di2, out folderName);
                bool hasSubFolders = di2.GetDirectories().Length > 0;
                if (!alreadyHaveIt && (!hasSubFolders || hasSeasonFolders))
                {
                    // ....its good!
                    FolderMonitorEntry ai = new FolderMonitorEntry(di2.FullName, hasSeasonFolders, folderName);
                    AddItems.Add(ai);
                    if (andGuess)
                        MonitorGuessShowItem(ai);
                }

            }
            catch (UnauthorizedAccessException)
            {
                alreadyHaveIt = true;
            }

            return hasSeasonFolders || alreadyHaveIt;
        }


        public void MonitorCheckFolderRecursive(DirectoryInfo di, ref bool stop)
        {
            // is it on the folder monitor ignore list?
            if (IgnoreFolders.Contains(di.FullName.ToLower()))
                return;

            if (MonitorAddSingleFolder(di, false))
                return; // done.

            // recursively check a monitored folder for new shows

            foreach (DirectoryInfo di2 in di.GetDirectories())
            {
                if (stop)
                    return;

                MonitorCheckFolderRecursive(di2, ref stop); // not a season folder.. recurse!
            } // for each directory
        }

        public void MonitorAddAllToMyShows()
        {
             LockShowItems();

            foreach (FolderMonitorEntry ai in AddItems)
            {
                if (ai.CodeUnknown)
                    continue; // skip

                // see if there is a matching show item
                ShowItem found = ShowItemForCode(ai.TVDBCode);
                if (found == null)
                {
                    // need to add a new showitem
                    found = new ShowItem(mTVDB, ai.TVDBCode);
                    ShowItems.Add(found);
                }

                found.AutoAdd_FolderBase = ai.Folder;
                found.AutoAdd_FolderPerSeason = ai.HasSeasonFoldersGuess;

                found.AutoAdd_SeasonFolderName = ai.SeasonFolderName + " ";
                Stats().AutoAddedShows++;
            }

            GenDict();
            Dirty();
            AddItems.Clear();
            UnlockShowItems();
        }

        public void MonitorGuessShowItem(FolderMonitorEntry ai)
        {
            string showName = GuessShowName(ai);

            if (string.IsNullOrEmpty(showName))
                return;

            TheTVDB.TheTVDB db = GetTVDB(true, "MonitorGuessShowItem");

            SeriesInfo ser = db.FindSeriesForName(showName);
            if (ser != null)
               ai.TVDBCode = ser.TVDBCode;

            db.Unlock("MonitorGuessShowItem");
        }

        public void MonitorCheckFolders(ref bool stop, ref int percentDone)
        {
            // Check the monitored folder list, and build up a new "AddItems" list.
            // guessing what the shows actually are isn't done here.  That is done by
            // calls to "MonitorGuessShowItem"

            AddItems = new FolderMonitorEntryList();

            int c = MonitorFolders.Count;

            LockShowItems();
            int c2 = 0;
            foreach (string folder in MonitorFolders)
            {
                percentDone = 100 * c2++ / c;
                DirectoryInfo di = new DirectoryInfo(folder);
                if (!di.Exists)
                    continue;

                MonitorCheckFolderRecursive(di, ref stop);

                if (stop)
                    break;
            }

            UnlockShowItems();
        }

        public bool RenameFilesToMatchTorrent(string torrent, string folder, TreeView tvTree, SetProgressDelegate prog, bool copyNotMove, string copyDest, CommandLineArgs args)
        {
            if (string.IsNullOrEmpty(folder))
                return false;
            if (string.IsNullOrEmpty(torrent))
                return false;

            if (copyNotMove)
            {
                if (string.IsNullOrEmpty(copyDest))
                    return false;
                if (!Directory.Exists(copyDest))
                    return false;
            }

            Stats().TorrentsMatched++;

            BTFileRenamer btp = new BTFileRenamer(prog);
            IList<Item> newList = new List<Item>();
            bool r = btp.RenameFilesOnDiskToMatchTorrent(torrent, folder, tvTree, newList, copyNotMove, copyDest, args);

            foreach (Item i in newList)
                TheActionList.Add(i);

            return r;
        }

        // consider each of the files, see if it is suitable for series "ser" and episode "epi"
        // if so, add a rcitem for copy to "fi"
        public bool FindMissingEp(DirCache dirCache, ItemMissing me, IList<Item> addTo, ActionCopyMoveRename.Op whichOp)
        {
            string showname = me.Episode.SI.ShowName;
            int season = me.Episode.SeasonNumber;

            int epnum = me.Episode.EpNum;

            // TODO: find a 'best match', or use first ?

            showname = Helpers.SimplifyName(showname);

            foreach (DirCacheEntry dce in dirCache)
            {
                if (ActionCancel)
                    return true;

                bool matched = false;

                try
                {
                    if (!dce.HasUsefulExtension_NotOthersToo) // not a usefile file extension
                        continue;
                    if (Settings.IgnoreSamples && dce.LowerName.Contains("sample") && ((dce.Length / (1024 * 1024)) < Settings.SampleFileMaxSizeMB))
                        continue;

                    matched = Regex.Match(dce.SimplifiedFullName, "\\b" + showname + "\\b", RegexOptions.IgnoreCase).Success;

                    // if we don't match the main name, then test the aliases
                    if (!matched)
                    {
                        foreach (string alias in me.Episode.SI.AliasNames)
                        {
                            string aliasName = Helpers.SimplifyName(alias);
                            matched = Regex.Match(dce.SimplifiedFullName, "\\b" + aliasName + "\\b", RegexOptions.IgnoreCase).Success;
                            if (matched)
                                break;
                        }
                    }

                    if (matched)
                    {
                        int seasF;
                        int epF;
                        // String ^fn = file->Name;

                        if ((FindSeasEp(dce.TheFile, out seasF, out epF, me.Episode.SI) && (seasF == season) && (epF == epnum)) || (me.Episode.SI.UseSequentialMatch && MatchesSequentialNumber(dce.TheFile.Name, ref seasF, ref epF, me.Episode) && (seasF == season) && (epF == epnum)))
                        {
                            FileInfo fi = new FileInfo(me.TheFileNoExt + dce.TheFile.Extension);
                            addTo.Add(new ActionCopyMoveRename(whichOp, dce.TheFile, fi, me.Episode));

                            // if we're copying/moving a file across, we might also want to make a thumbnail or NFO for it
                            ThumbnailAndNFOCheck(me.Episode, fi, addTo);

                            return true;
                        }
                    }
                }
                catch (PathTooLongException e)
                {
                    string t = "Path too long. " + dce.TheFile.FullName + ", " + e.Message;
                    t += ".  Try to display more info?";
                    DialogResult dr = MessageBox.Show(t, "Path too long", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
                    if (dr == DialogResult.Yes)
                    {
                        t = "DirectoryName " + dce.TheFile.DirectoryName + ", File name: " + dce.TheFile.Name;
                        t += matched ? ", matched.  " : ", no match.  ";
                        if (matched)
                        {
                            t += "Show: " + me.Episode.TheSeries.Name + ", Season " + season + ", Ep " + epnum + ".  ";
                            t += "To: " + me.TheFileNoExt;
                        }

                        MessageBox.Show(t, "Path too long", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                }
            }

            return false;
        }

        public void KeepTogether(IList<Item> Actionlist)
        {
            // for each of the items in rcl, do the same copy/move if for other items with the same
            // base name, but different extensions

            IList<Item> extras = new List<Item>();

            foreach (Item Action1 in Actionlist)
            {
                if (!(Action1 is ActionCopyMoveRename))
                    continue;

                ActionCopyMoveRename Action = (ActionCopyMoveRename) (Action1);

                try
                {
                    DirectoryInfo sfdi = Action.From.Directory;
                    string basename = Action.From.Name;
                    int l = basename.Length;
                    basename = basename.Substring(0, l - Action.From.Extension.Length);

                    string toname = Action.To.Name;
                    int l2 = toname.Length;
                    toname = toname.Substring(0, l2 - Action.To.Extension.Length);

                    FileInfo[] flist = sfdi.GetFiles(basename + ".*");
                    foreach (FileInfo fi in flist)
                    {
                        // do case insensitive replace
                        string n = fi.Name;
                        int p = n.ToUpper().IndexOf(basename.ToUpper());
                        string newName = n.Substring(0, p) + toname + n.Substring(p + basename.Length);
                        if ((Settings.RenameTxtToSub) && (newName.EndsWith(".txt")))
                            newName = newName.Substring(0, newName.Length - 4) + ".sub";

                        ActionCopyMoveRename newitem = new ActionCopyMoveRename(Action.Operation, fi, Helpers.FileInFolder(Action.To.Directory, newName), Action.Episode);

                        // check this item isn't already in our to-do list
                        bool doNotAdd = false;
                        foreach (Item ai2 in Actionlist)
                        {
                            if (!(ai2 is ActionCopyMoveRename))
                                continue;

                            if (((ActionCopyMoveRename) (ai2)).SameSource(newitem))
                            {
                                doNotAdd = true;
                                break;
                            }
                        }

                        if (!doNotAdd)
                        {
                            if (!newitem.SameAs(Action)) // don't re-add ourself
                                extras.Add(newitem);
                        }
                    }
                }
                catch (PathTooLongException e)
                {
                    string t = "Path or filename too long. " + Action.From.FullName + ", " + e.Message;
                    MessageBox.Show(t, "Path or filename too long", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }

            foreach (Item action in extras)
            {
                // check we don't already have this in our list and, if we don't add it!
                bool have = false;
                foreach (Item action2 in Actionlist)
                {
                    if (action2.SameAs(action))
                    {
                        have = true;
                        break;
                    }
                }

                if (!have)
                    Actionlist.Add(action);
            }
        }

        public void LookForMissingEps(SetProgressDelegate prog)
        {
            // for each ep we have noticed as being missing
            // look through the monitored folders for it

            Stats().FindAndOrganisesDone++;

            prog.Invoke(0);

            var newList = new List<Item>();
            IList<Item> toRemove = new List<Item>();

            int fileCount = 0;
            foreach (string s in SearchFolders)
                fileCount += DirCache.CountFiles(s, true);

            int c = 0;

            DirCache dirCache = new DirCache();
            foreach (String s in SearchFolders)
            {
                if (ActionCancel)
                    return;

                c = dirCache.AddFolder(prog, c, fileCount, s, true, Settings);
            }

            c = 0;
            int totalN = TheActionList.Count;
            foreach (Item action1 in TheActionList)
            {
                if (ActionCancel)
                    return;

                prog.Invoke(50 + 50 * (++c) / (totalN + 1)); // second 50% of progress bar

                if (action1 is ItemMissing)
                {
                    if (FindMissingEp(dirCache, (ItemMissing) (action1), newList, ActionCopyMoveRename.Op.Copy))
                        toRemove.Add(action1);
                }
            }

            if (Settings.KeepTogether)
                KeepTogether(newList);

            prog.Invoke(100);

            if (!Settings.LeaveOriginals)
            {
                // go through and change last of each operation on a given source file to a 'Move'
                // ideally do that move within same filesystem

                // sort based on source file, and destination drive, putting last if destdrive == sourcedrive
                newList.Sort(new ActionItemSorter());

                // sort puts all the CopyMoveRenames together				

                // then set the last of each source file to be a move
                for (int i = 0; i < newList.Count; i++)
                {
                    ActionCopyMoveRename cmr1 = newList[i] as ActionCopyMoveRename;
                    bool ok1 = cmr1 != null;

                    if (!ok1)
                        continue;

                    bool last = i == (newList.Count - 1);
                    ActionCopyMoveRename cmr2 = !last ? newList[i + 1] as ActionCopyMoveRename : null;
                    bool ok2 = cmr2 != null;

                    if (ok2)
                    {
                        ActionCopyMoveRename a1 = cmr1;
                        ActionCopyMoveRename a2 = cmr2;
                        if (!Helpers.Same(a1.From, a2.From))
                            a1.Operation = ActionCopyMoveRename.Op.Move;
                    }
                    else
                    {
                        // last item, or last copymoverename item in the list
                        ActionCopyMoveRename a1 = cmr1;
                        a1.Operation = ActionCopyMoveRename.Op.Move;
                    }
                }
            }

            foreach (Item i in toRemove)
                TheActionList.Remove(i);

            foreach (Item i in newList)
                TheActionList.Add(i);

            //                 if (Settings->ExportFOXML)
            //				ExportFOXML(Settings->ExportFOXMLTo);
        }

        // -----------------------------------------------------------------------------

        public void GetThread(Object codeIn)
        {
            System.Diagnostics.Debug.Assert(WorkerSemaphore != null);

            WorkerSemaphore.WaitOne(); // don't start until we're allowed to

            int code = (int) (codeIn);

            System.Diagnostics.Debug.Print("  Downloading " + code);
            bool r = GetTVDB(false, "").EnsureUpdated(code);
            System.Diagnostics.Debug.Print("  Finished " + code);
            if (!r)
            {
                DownloadOK = false;
                if (DownloadStopOnError)
                    DownloadDone = true;
            }
            WorkerSemaphore.Release(1);
        }

        public void WaitForAllThreadsAndTidyUp()
        {
            if (Workers != null)
            {
                foreach (Thread t in Workers)
                {
                    if (t.IsAlive)
                        t.Join();
                }
            }

            Workers = null;
            WorkerSemaphore = null;
        }

        public void Downloader()
        {
            // do background downloads of webpages

            try
            {
                if (ShowItems.Count == 0)
                {
                    DownloadDone = true;
                    DownloadOK = true;
                    return;
                }

                if (!GetTVDB(false, "").GetUpdates())
                {
                    DownloadDone = true;
                    DownloadOK = false;
                    return;
                }

                // for eachs of the ShowItems, make sure we've got downloaded data for it

                int n2 = ShowItems.Count;
                int n = 0;
                List<int> codes = new List<int>();
                LockShowItems();
                foreach (ShowItem si in ShowItems)
                    codes.Add(si.TVDBCode);
                UnlockShowItems();

                int numWorkers = Settings.ParallelDownloads;
                Workers = new List<Thread>();

                WorkerSemaphore = new Semaphore(numWorkers, numWorkers); // allow up to numWorkers working at once

                foreach (int code in codes)
                {
                    DownloadPct = 100 * (n + 1) / (n2 + 1);
                    DownloadsRemaining = n2 - n;
                    n++;

                    WorkerSemaphore.WaitOne(); // blocks until there is an available slot
                    Thread t = new Thread(GetThread);
                    Workers.Add(t);
                    t.Name = "GetThread:" + code;
                    t.Start(code); // will grab the semaphore as soon as we make it available
                    int nfr = WorkerSemaphore.Release(1); // release our hold on the semaphore, so that worker can grab it
                    System.Diagnostics.Debug.Print("Started " + code + " pool has " + nfr + " free");
                    Thread.Sleep(1); // allow the other thread a chance to run and grab

                    // tidy up any finished workers
                    for (int i = Workers.Count - 1; i >= 0; i--)
                    {
                        if (!Workers[i].IsAlive)
                            Workers.RemoveAt(i); // remove dead worker
                    }

                    if (DownloadDone)
                        break;
                }

                WaitForAllThreadsAndTidyUp();

                GetTVDB(false, "").UpdatesDoneOK();
                DownloadDone = true;
                DownloadOK = true;
                return;
            }
            catch (ThreadAbortException)
            {
                DownloadDone = true;
                DownloadOK = false;
                return;
            }
            finally
            {
                Workers = null;
                WorkerSemaphore = null;
            }
        }

        public void StartBGDownloadThread(bool stopOnError)
        {
            if (!DownloadDone)
                return;
            DownloadStopOnError = stopOnError;
            DownloadPct = 0;
            DownloadDone = false;
            DownloadOK = true;
            mDownloaderThread = new Thread(Downloader);
            mDownloaderThread.Name = "Downloader";
            mDownloaderThread.Start();
        }

        public void WaitForBGDownloadDone()
        {
            if ((mDownloaderThread != null) && (mDownloaderThread.IsAlive))
                mDownloaderThread.Join();
            mDownloaderThread = null;
        }

        public void StopBGDownloadThread()
        {
            if (mDownloaderThread != null)
            {
                DownloadDone = true;
                mDownloaderThread.Join();

                /*if (Workers != null)
                {
                    foreach (Thread t in Workers)
                        t.Interrupt();
                }

                WaitForAllThreadsAndTidyUp();
                if (mDownloaderThread.IsAlive)
                {
                    mDownloaderThread.Interrupt();
                    mDownloaderThread = null;
                }
                */
                mDownloaderThread = null;
            }
        }

        public bool DoDownloadsFG()
        {
            if (Settings.OfflineMode)
                return true; // don't do internet in offline mode!

            StartBGDownloadThread(true);

            const int delayStep = 100;
            int count = 1000 / delayStep; // one second
            while ((count-- > 0) && (!DownloadDone))
                System.Threading.Thread.Sleep(delayStep);

            if (!DownloadDone && !Args.Hide) // downloading still going on, so time to show the dialog if we're not in /hide mode
            {
//todo                DownloadProgress dp = new DownloadProgress(this);
//                dp.ShowDialog();
//                dp.Update();
            }

            WaitForBGDownloadDone();

            GetTVDB(false, "").SaveCache();

            GenDict();

            if (!DownloadOK)
            {
                if (!Args.Unattended)
                    MessageBox.Show(mTVDB.LastError, "Error while downloading", MessageBoxButtons.OK, MessageBoxIcon.Error);
                mTVDB.LastError = "";
            }

            return DownloadOK;
        }

        public bool GenDict()
        {
            bool res = true;
            LockShowItems();
            foreach (ShowItem si in ShowItems)
            {
                if (!GenerateEpisodeDict(si))
                    res = false;
            }
            UnlockShowItems();
            return res;
        }

        public TheSearchers GetSearchers()
        {
            return Settings.TheSearchers;
        }

        public void TidyTVDB()
        {
            // remove any shows from thetvdb that aren't in My Shows
            TheTVDB.TheTVDB db = GetTVDB(true, "TidyTVDB");
            List<int> removeList = new List<int>();

            foreach (KeyValuePair<int, SeriesInfo> kvp in mTVDB.GetSeriesDict())
            {
                bool found = false;
                foreach (ShowItem si in ShowItems)
                {
                    if (si.TVDBCode == kvp.Key)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    removeList.Add(kvp.Key);
            }

            foreach (int i in removeList)
                db.ForgetShow(i, false);

            db.Unlock("TheTVDB");
            db.SaveCache();
        }

        public void Closing()
        {
            StopBGDownloadThread();
            Stats().Save();
        }

        public void DoBTSearch(ProcessedEpisode ep)
        {
            if (ep == null)
                return;
            SysOpen(Settings.BTSearchURL(ep));
        }

        public void DoWhenToWatch(bool cachedOnly)
        {
            if (!cachedOnly && !DoDownloadsFG())
                return;
            if (cachedOnly)
                GenDict();
        }

        public List<FileInfo> FindEpOnDisk(ProcessedEpisode pe)
        {
            return FindEpOnDisk(pe.SI, pe);
        }

        public List<FileInfo> FindEpOnDisk(ShowItem si, Episode epi)
        {
            List<FileInfo> ret = new List<FileInfo>();

            int seasWanted = epi.TheSeason.SeasonNumber;
            int epWanted = epi.EpNum;

            int snum = epi.TheSeason.SeasonNumber;

            if (!si.AllFolderLocations(Settings).ContainsKey(snum))
                return ret;

            foreach (string folder in si.AllFolderLocations(Settings)[snum])
            {
                DirectoryInfo di;
                try
                {
                    di = new DirectoryInfo(folder);
                }
                catch
                {
                    return ret;
                }
                if (!di.Exists)
                    return ret;

                FileInfo[] files = di.GetFiles();
                foreach (FileInfo fiTemp in files)
                {
                    int seasFound;
                    int epFound;

                    if (!Settings.UsefulExtension(fiTemp.Extension, false))
                        continue; // move on

                    if (FindSeasEp(fiTemp, out seasFound, out epFound, si))
                    {
                        if (seasFound == -1)
                            seasFound = seasWanted;
                        if ((seasFound == seasWanted) && (epFound == epWanted))
                            ret.Add(fiTemp);
                    }
                }
            }

            return ret;
        }

        public bool HasAnyAirdates(ShowItem si, int snum)
        {
            bool r = false;
            TheTVDB.TheTVDB db = GetTVDB(false, "");
            SeriesInfo ser = db.GetSeries(si.TVDBCode);
            if ((ser != null) && (ser.Seasons.ContainsKey(snum)))
            {
                foreach (Episode e in ser.Seasons[snum].Episodes)
                {
                    if (e.FirstAired != null)
                    {
                        r = true;
                        break;
                    }
                }
            }
            return r;
        }

        public void TVShowNFOCheck(ShowItem si)
        {
            // check there is a TVShow.nfo file in the root folder for the show
            if (string.IsNullOrEmpty(si.AutoAdd_FolderBase)) // no base folder defined
                return;

            if (si.AllFolderLocations(Settings).Count == 0) // no seasons enabled
                return;

            FileInfo tvshownfo = Helpers.FileInFolder(si.AutoAdd_FolderBase, "tvshow.nfo");

            bool needUpdate = !tvshownfo.Exists || (si.TheSeries().Srv_LastUpdated > TimeZone.Epoch(tvshownfo.LastWriteTime));
            // was it written before we fixed the bug in <episodeguideurl> ?
            needUpdate = needUpdate || (tvshownfo.LastWriteTime.ToUniversalTime().CompareTo(new DateTime(2009, 9, 13, 7, 30, 0, 0, DateTimeKind.Utc)) < 0);
            if (needUpdate)
                TheActionList.Add(new ActionNFO(tvshownfo, si));
        }

        public bool GenerateEpisodeDict(ShowItem si)
        {
            si.SeasonEpisodes.Clear();

            // copy data from tvdb
            // process as per rules
            // done!

            TheTVDB.TheTVDB db = GetTVDB(true, "GenerateEpisodeDict");

            SeriesInfo ser = db.GetSeries(si.TVDBCode);

            if (ser == null)
            {
                db.Unlock("GenerateEpisodeDict");
                return false; // TODO: warn user
            }

            bool r = true;
            foreach (KeyValuePair<int, Season> kvp in ser.Seasons)
            {
                List<ProcessedEpisode> pel = GenerateEpisodes(si, ser, kvp.Key, true);
                si.SeasonEpisodes[kvp.Key] = pel;
                if (pel == null)
                    r = false;
            }

            List<int> theKeys = new List<int>();
            // now, go through and number them all sequentially
            foreach (int snum in ser.Seasons.Keys)
                theKeys.Add(snum);

            theKeys.Sort();

            int overallCount = 1;
            foreach (int snum in theKeys)
            {
                if (snum != 0)
                {
                    foreach (ProcessedEpisode pe in si.SeasonEpisodes[snum])
                    {
                        pe.OverallNumber = overallCount;
                        overallCount += 1 + pe.EpNum2 - pe.EpNum;
                    }
                }
            }

            db.Unlock("GenerateEpisodeDict");

            return r;
        }

        public static List<ProcessedEpisode> GenerateEpisodes(ShowItem si, SeriesInfo ser, int snum, bool applyRules)
        {
            List<ProcessedEpisode> eis = new List<ProcessedEpisode>();

            if ((ser == null) || !ser.Seasons.ContainsKey(snum))
                return null; // todo.. something?

            Season seas = ser.Seasons[snum];

            if (seas == null)
                return null; // TODO: warn user

            foreach (Episode e in seas.Episodes)
                eis.Add(new ProcessedEpisode(e, si)); // add a copy

            if (si.DVDOrder)
            {
                eis.Sort(new System.Comparison<ProcessedEpisode>(ProcessedEpisode.DVDOrderSorter));
                Renumber(eis);
            }
            else
                eis.Sort(new System.Comparison<ProcessedEpisode>(ProcessedEpisode.EPNumberSorter));

            if (si.CountSpecials && ser.Seasons.ContainsKey(0))
            {
                // merge specials in
                foreach (Episode ep in ser.Seasons[0].Episodes)
                {
                    if (ep.Items.ContainsKey("airsbefore_season") && ep.Items.ContainsKey("airsbefore_episode"))
                    {
                        string seasstr = ep.Items["airsbefore_season"];
                        string epstr = ep.Items["airsbefore_episode"];
                        if ((string.IsNullOrEmpty(seasstr)) || (string.IsNullOrEmpty(epstr)))
                            continue;
                        int sease = int.Parse(seasstr);
                        if (sease != snum)
                            continue;
                        int epnum = int.Parse(epstr);
                        for (int i = 0; i < eis.Count; i++)
                        {
                            if ((eis[i].SeasonNumber == sease) && (eis[i].EpNum == epnum))
                            {
                                ProcessedEpisode pe = new ProcessedEpisode(ep, si)
                                                          {
                                                              TheSeason = eis[i].TheSeason,
                                                              SeasonID = eis[i].SeasonID
                                                          };
                                eis.Insert(i, pe);
                                break;
                            }
                        }
                    }
                }
                // renumber to allow for specials
                int epnumr = 1;
                for (int j = 0; j < eis.Count; j++)
                {
                    eis[j].EpNum2 = epnumr + (eis[j].EpNum2 - eis[j].EpNum);
                    eis[j].EpNum = epnumr;
                    epnumr++;
                }
            }

            if (applyRules)
            {
                List<ShowRule> rules = si.RulesForSeason(snum);
                if (rules != null)
                    ApplyRules(eis, rules, si);
            }

            return eis;
        }

        public static void ApplyRules(List<ProcessedEpisode> eis, List<ShowRule> rules, ShowItem si)
        {
            foreach (ShowRule sr in rules)
            {
                int nn1 = sr.First;
                int nn2 = sr.Second;

                int n1 = -1;
                int n2 = -1;
                // turn nn1 and nn2 from ep number into position in array
                for (int i = 0; i < eis.Count; i++)
                {
                    if (eis[i].EpNum == nn1)
                    {
                        n1 = i;
                        break;
                    }
                }
                for (int i = 0; i < eis.Count; i++)
                {
                    if (eis[i].EpNum == nn2)
                    {
                        n2 = i;
                        break;
                    }
                }

                if (sr.DoWhatNow == RuleAction.kInsert)
                {
                    // this only applies for inserting an episode, at the end of the list
                    if (nn1 == eis[eis.Count-1].EpNum+1) // after the last episode
                        n1 = eis.Count;
                }

                string txt = sr.UserSuppliedText;
                int ec = eis.Count;

                switch (sr.DoWhatNow)
                {
                    case RuleAction.kRename:
                        {
                            if ((n1 < ec) && (n1 >= 0))
                                eis[n1].Name = txt;
                            break;
                        }
                    case RuleAction.kRemove:
                        {
                            if ((n1 < ec) && (n1 >= 0) && (n2 < ec) && (n2 >= 0))
                                eis.RemoveRange(n1, 1 + n2 - n1);
                            else if ((n1 < ec) && (n1 >= 0) && (n2 == -1))
                                eis.RemoveAt(n1);
                            break;
                        }
                    case RuleAction.kIgnoreEp:
                        {
                            if (n2 == -1)
                                n2 = n1;
                            for (int i = n1; i <= n2; i++)
                            {
                                if ((i < ec) && (i >= 0))
                                    eis[i].Ignore = true;
                            }
                            break;
                        }
                    case RuleAction.kSplit:
                        {
                            // split one episode into a multi-parter
                            if ((n1 < ec) && (n1 >= 0))
                            {
                                ProcessedEpisode ei = eis[n1];
                                string nameBase = ei.Name;
                                eis.RemoveAt(n1); // remove old one
                                for (int i = 0; i < nn2; i++) // make n2 new parts
                                {
                                    ProcessedEpisode pe2 = new ProcessedEpisode(ei, si)
                                                               {
                                                                   Name = nameBase + " (Part " + (i + 1) + ")",
                                                                   EpNum = -2,
                                                                   EpNum2 = -2
                                                               };
                                    eis.Insert(n1 + i, pe2);
                                }
                            }
                            break;
                        }
                    case RuleAction.kMerge:
                    case RuleAction.kCollapse:
                        {
                            if ((n1 != -1) && (n2 != -1) && (n1 < ec) && (n2 < ec) && (n1 < n2))
                            {
                                ProcessedEpisode oldFirstEI = eis[n1];
                                string combinedName = eis[n1].Name + " + ";
                                string combinedSummary = eis[n1].Overview + "<br/><br/>";
                                //int firstNum = eis[n1]->TVcomEpCount();
                                for (int i = n1 + 1; i <= n2; i++)
                                {
                                    combinedName += eis[i].Name;
                                    combinedSummary += eis[i].Overview;
                                    if (i != n2)
                                    {
                                        combinedName += " + ";
                                        combinedSummary += "<br/><br/>";
                                    }
                                }

                                eis.RemoveRange(n1, n2 - n1);

                                eis.RemoveAt(n1);

                                ProcessedEpisode pe2 = new ProcessedEpisode(oldFirstEI, si)
                                                           {
                                                               Name = ((string.IsNullOrEmpty(txt)) ? combinedName : txt),
                                                               EpNum = -2
                                                           };
                                if (sr.DoWhatNow == RuleAction.kMerge)
                                    pe2.EpNum2 = -2 + n2 - n1;
                                else
                                    pe2.EpNum2 = -2;

                                pe2.Overview = combinedSummary;
                                eis.Insert(n1, pe2);
                            }
                            break;
                        }
                    case RuleAction.kSwap:
                        {
                            if ((n1 != -1) && (n2 != -1) && (n1 < ec) && (n2 < ec))
                            {
                                ProcessedEpisode t = eis[n1];
                                eis[n1] = eis[n2];
                                eis[n2] = t;
                            }
                            break;
                        }
                    case RuleAction.kInsert:
                        {
                            if ((n1 < ec) && (n1 >= 0))
                            {
                                ProcessedEpisode t = eis[n1];
                                ProcessedEpisode n = new ProcessedEpisode(t.TheSeries, t.TheSeason, si)
                                                         {
                                                             Name = txt,
                                                             EpNum = -2,
                                                             EpNum2 = -2
                                                         };
                                eis.Insert(n1, n);
                            }
                            else if (n1 == eis.Count)
                            {
                                ProcessedEpisode t = eis[n1-1];
                                ProcessedEpisode n = new ProcessedEpisode(t.TheSeries, t.TheSeason, si)
                                {
                                    Name = txt,
                                    EpNum = -2,
                                    EpNum2 = -2
                                };
                                eis.Add(n);
                            }
                            break;
                        }
                } // switch DoWhatNow

                Renumber(eis);
            } // for each rule
            // now, go through and remove the ignored ones (but don't renumber!!)
            for (int i = eis.Count - 1; i >= 0; i--)
            {
                if (eis[i].Ignore)
                    eis.RemoveAt(i);
            }
        }

        public static void Renumber(List<ProcessedEpisode> eis)
        {
            if (eis.Count == 0)
                return; // nothing to do

            // renumber 
            // pay attention to specials etc.
            int n = (eis[0].EpNum == 0) ? 0 : 1;

            for (int i = 0; i < eis.Count; i++)
            {
                if (eis[i].EpNum != -1) // is -1 if its a special or other ignored ep
                {
                    int num = eis[i].EpNum2 - eis[i].EpNum;
                    eis[i].EpNum = n;
                    eis[i].EpNum2 = n + num;
                    n += num + 1;
                }
            }
        }

        public string GuessShowName(FolderMonitorEntry ai)
        {
            // see if we can guess a season number and show name, too
            // Assume is blah\blah\blah\show\season X
            string showName = ai.Folder;

            foreach (string seasonWord in _seasonWords)
            {
                string seasonFinder = ".*" + seasonWord + "[ _\\.]+([0-9]+).*"; // todo: don't look for just one season word
                if (Regex.Matches(showName, seasonFinder, RegexOptions.IgnoreCase).Count == 0)
                    continue;

                try
                {
                    // remove season folder from end of the path
                    showName = Regex.Replace(showName, "(.*)\\\\" + seasonFinder, "$1", RegexOptions.IgnoreCase);
                    break;
                }
                catch (ArgumentException)
                {
                }
            }
            // assume last folder element is the show name
            showName = showName.Substring(showName.LastIndexOf(Path.DirectorySeparatorChar.ToString()) + 1);

            return showName;
        }

        public List<ProcessedEpisode> NextNShows(int nShows, int nDaysPast, int nDaysFuture)
        {
            DateTime notBefore = DateTime.Now.AddDays(-nDaysPast);
            List<ProcessedEpisode> found = new List<ProcessedEpisode>();

            LockShowItems();
            for (int i = 0; i < nShows; i++)
            {
                ProcessedEpisode nextAfterThat = null;
                TimeSpan howClose = TimeSpan.MaxValue;
                foreach (ShowItem si in GetShowItems(false))
                {
                    if (!si.ShowNextAirdate)
                        continue;
                    foreach (KeyValuePair<int, List<ProcessedEpisode>> v in si.SeasonEpisodes)
                    {
                        if (si.IgnoreSeasons.Contains(v.Key))
                            continue; // ignore this season

                        foreach (ProcessedEpisode ei in v.Value)
                        {
                            if (found.Contains(ei))
                                continue;

                            DateTime? airdt = ei.GetAirDateDT(true);

                            if ((airdt == null) || (airdt == DateTime.MaxValue))
                                continue;
                            DateTime dt = (DateTime) airdt;

                            TimeSpan ts = dt.Subtract(notBefore);
                            TimeSpan timeUntil = dt.Subtract(DateTime.Now);
                            if (((howClose == TimeSpan.MaxValue) || (ts.CompareTo(howClose) <= 0) && (ts.TotalHours >= 0)) && (ts.TotalHours >= 0) && (timeUntil.TotalDays <= nDaysFuture))
                            {
                                howClose = ts;
                                nextAfterThat = ei;
                            }
                        }
                    }
                }
                if (nextAfterThat == null)
                    return found;

                DateTime? nextdt = nextAfterThat.GetAirDateDT(true);
                if (nextdt.HasValue)
                {
                    notBefore = nextdt.Value;
                    found.Add(nextAfterThat);
                }
            }
            UnlockShowItems();

            return found;
        }

        public static void WriteStringsToXml(List<string> strings, XmlWriter writer, string elementName, string stringName)
        {
            writer.WriteStartElement(elementName);
            foreach (string ss in strings)
            {
                writer.WriteStartElement(stringName);
                writer.WriteValue(ss);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        public static void Rotate(string filenameBase)
        {
            if (File.Exists(filenameBase))
            {
                for (int i = 8; i >= 0; i--)
                {
                    string fn = filenameBase + "." + i;
                    if (File.Exists(fn))
                    {
                        string fn2 = filenameBase + "." + (i + 1);
                        if (File.Exists(fn2))
                            File.Delete(fn2);
                        File.Move(fn, fn2);
                    }
                }

                File.Copy(filenameBase, filenameBase + ".0");
            }
        }

//        public void WriteXMLSettings()
//        {
//            // backup old settings before writing new ones
//
//            Rotate(PathManager.TVDocSettingsFile.FullName);
//
//            XmlWriterSettings settings = new XmlWriterSettings
//            {
//                Indent = true,
//                NewLineOnAttributes = true
//            };
//            using (XmlWriter writer = XmlWriter.Create(PathManager.TVDocSettingsFile.FullName, settings))
//            {
//
//                writer.WriteStartDocument();
//                writer.WriteStartElement("TVRename");
//                writer.WriteStartAttribute("Version");
//                writer.WriteValue("2.1");
//                writer.WriteEndAttribute(); // version
//
//                Settings.WriteXML(writer); // <Settings>
//
//                writer.WriteStartElement("MyShows");
//                foreach (ShowItem si in ShowItems)
//                    si.WriteXMLSettings(writer);
//                writer.WriteEndElement(); // myshows
//
//                WriteStringsToXml(MonitorFolders, writer, "MonitorFolders", "Folder");
//                WriteStringsToXml(IgnoreFolders, writer, "IgnoreFolders", "Folder");
//                WriteStringsToXml(SearchFolders, writer, "FinderSearchFolders", "Folder");
//
//                writer.WriteStartElement("IgnoreItems");
//                foreach (IgnoreItem ii in Ignore)
//                    ii.Write(writer);
//                writer.WriteEndElement(); // IgnoreItems
//
//                writer.WriteEndElement(); // tvrename
//                writer.WriteEndDocument();
//                writer.Close();
//            }
//
//            mDirty = false;
//
//            Stats().Save();
//        }

        public static List<string> ReadStringsFromXml(XmlReader reader, string elementName, string stringName)
        {
            List<string> r = new List<String>();

            if (reader.Name != elementName)
                return r; // uhoh

            if (!reader.IsEmptyElement)
            {
                reader.Read();
                while (!reader.EOF)
                {
                    if ((reader.Name == elementName) && !reader.IsStartElement())
                        break;
                    if (reader.Name == stringName)
                        r.Add(reader.ReadElementContentAsString());
                    else
                        reader.ReadOuterXml();
                }
            }
            reader.Read();
            return r;
        }

//        public bool LoadXMLSettings(FileInfo from)
//        {
//            if (from == null)
//                return true;
//
//            try
//            {
//                XmlReaderSettings settings = new XmlReaderSettings
//                {
//                    IgnoreComments = true,
//                    IgnoreWhitespace = true
//                };
//
//                if (!from.Exists)
//                {
//                    //LoadErr = from->Name + " : File does not exist";
//                    //return false;
//                    return true; // that's ok
//                }
//
//                XmlReader reader = XmlReader.Create(from.FullName, settings);
//
//                reader.Read();
//                if (reader.Name != "xml")
//                {
//                    LoadErr = from.Name + " : Not a valid XML file";
//                    return false;
//                }
//
//                reader.Read();
//
//                if (reader.Name != "TVRename")
//                {
//                    LoadErr = from.Name + " : Not a TVRename settings file";
//                    return false;
//                }
//
//                if (reader.GetAttribute("Version") != "2.1")
//                {
//                    LoadErr = from.Name + " : Incompatible version";
//                    return false;
//                }
//
//                reader.Read(); // move forward one
//
//                while (!reader.EOF)
//                {
//                    if (reader.Name == "TVRename" && !reader.IsStartElement())
//                        break; // end of it all
//
//                    if (reader.Name == "Settings")
//                    {
//                        Settings = new TVSettings(reader.ReadSubtree());
//                        reader.Read();
//                    }
//                    else if (reader.Name == "MyShows")
//                    {
//                        XmlReader r2 = reader.ReadSubtree();
//                        r2.Read();
//                        r2.Read();
//                        while (!r2.EOF)
//                        {
//                            if ((r2.Name == "MyShows") && (!r2.IsStartElement()))
//                                break;
//                            if (r2.Name == "ShowItem")
//                            {
//                                ShowItem si = new ShowItem(mTVDB, r2.ReadSubtree(), Settings);
//
//                                if (si.UseCustomShowName) // see if custom show name is actually the real show name
//                                {
//                                    SeriesInfo ser = si.TheSeries();
//                                    if ((ser != null) && (si.CustomShowName == ser.Name))
//                                    {
//                                        // then, turn it off
//                                        si.CustomShowName = "";
//                                        si.UseCustomShowName = false;
//                                    }
//                                }
//                                ShowItems.Add(si);
//
//                                r2.Read();
//                            }
//                            else
//                                r2.ReadOuterXml();
//                        }
//                        reader.Read();
//                    }
//                    else if (reader.Name == "MonitorFolders")
//                        MonitorFolders = ReadStringsFromXml(reader, "MonitorFolders", "Folder");
//                    else if (reader.Name == "IgnoreFolders")
//                        IgnoreFolders = ReadStringsFromXml(reader, "IgnoreFolders", "Folder");
//                    else if (reader.Name == "FinderSearchFolders")
//                        SearchFolders = ReadStringsFromXml(reader, "FinderSearchFolders", "Folder");
//                    else if (reader.Name == "IgnoreItems")
//                    {
//                        XmlReader r2 = reader.ReadSubtree();
//                        r2.Read();
//                        r2.Read();
//                        while (r2.Name == "Ignore")
//                            Ignore.Add(new IgnoreItem(r2));
//                        reader.Read();
//                    }
//                    else
//                        reader.ReadOuterXml();
//                }
//
//                reader.Close();
//                reader = null;
//            }
//            catch (Exception e)
//            {
//                LoadErr = from.Name + " : " + e.Message;
//                return false;
//            }
//
//            try
//            {
//                mStats = TVRenameStats.Load();
//            }
//            catch (Exception)
//            {
//                // not worried if stats loading fails
//            }
//            return true;
//        }

        public static bool SysOpen(string what)
        {
            try
            {
                System.Diagnostics.Process.Start(what);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void ExportMissingXML() 
        {
            if (Settings.ExportMissingXML)
            {
                XmlWriterSettings settings = new XmlWriterSettings();
                //XmlWriterSettings settings = gcnew XmlWriterSettings();
                settings.Indent = true;
                settings.NewLineOnAttributes = true;
                using (XmlWriter writer = XmlWriter.Create(Settings.ExportMissingXMLTo, settings))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("TVRename");
                    writer.WriteStartAttribute("Version");
                    writer.WriteValue("2.1");
                    writer.WriteEndAttribute(); // version
                    writer.WriteStartElement("MissingItems");

                    foreach (Item Action in TheActionList)
                    {
                        if (Action is ItemMissing)
                        {
                            ItemMissing Missing = (ItemMissing) (Action);
                            writer.WriteStartElement("MissingItem");
                            writer.WriteStartElement("id");
                            writer.WriteValue(Missing.Episode.SI.TVDBCode);
                            writer.WriteEndElement();
                            writer.WriteStartElement("title");
                            writer.WriteValue(Missing.Episode.TheSeries.Name);
                            writer.WriteEndElement();
                            writer.WriteStartElement("season");

                            if (Missing.Episode.SeasonNumber.ToString().Length > 1)
                            {
                                writer.WriteValue(Missing.Episode.SeasonNumber);
                            }
                            else
                            {
                                writer.WriteValue("0" + Missing.Episode.SeasonNumber);
                            }

                            writer.WriteEndElement();
                            writer.WriteStartElement("episode");

                            if (Missing.Episode.EpNum.ToString().Length > 1)
                            {
                                writer.WriteValue(Missing.Episode.EpNum);
                            }
                            else
                            {
                                writer.WriteValue("0" + Missing.Episode.EpNum);
                            }
                            writer.WriteEndElement();
                            writer.WriteStartElement("episodeName");
                            writer.WriteValue(Missing.Episode.Name);
                            writer.WriteEndElement();
                            writer.WriteStartElement("description");
                            writer.WriteValue(Missing.Episode.Overview);
                            writer.WriteEndElement();
                            writer.WriteStartElement("pubDate");

                            DateTime? dt = Missing.Episode.GetAirDateDT(true);
                            if (dt != null)
                                writer.WriteValue(dt.Value.ToString("F"));
                            writer.WriteEndElement();
                            writer.WriteEndElement(); // MissingItem
                        }
                    }
                    writer.WriteEndElement(); // MissingItems
                    writer.WriteEndElement(); // tvrename
                    writer.WriteEndDocument();
                    writer.Close();
                }
            }
        }

        public bool GenerateUpcomingRSS(Stream str, List<ProcessedEpisode> elist)
        {
            if (elist == null)
                return false;

            try
            {
                XmlWriterSettings settings = new XmlWriterSettings
                                                 {
                                                     Indent = true,
                                                     NewLineOnAttributes = true,
                                                     Encoding = System.Text.Encoding.ASCII
                                                 };
                using (XmlWriter writer = XmlWriter.Create(str, settings))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("rss");
                    writer.WriteStartAttribute("version");
                    writer.WriteValue("2.0");
                    writer.WriteEndAttribute();
                    writer.WriteStartElement("channel");
                    writer.WriteStartElement("title");
                    writer.WriteValue("Upcoming Shows");
                    writer.WriteEndElement();
                    writer.WriteStartElement("title");
                    writer.WriteValue("http://tvrename.com");
                    writer.WriteEndElement();
                    writer.WriteStartElement("description");
                    writer.WriteValue("Upcoming shows, exported by TVRename");
                    writer.WriteEndElement();

                    foreach (ProcessedEpisode ei in elist)
                    {
                        string niceName = Settings.NamingStyle.NameForExt(ei, null, 0);

                        writer.WriteStartElement("item");
                        writer.WriteStartElement("title");
                        writer.WriteValue(ei.HowLong() + " " + ei.DayOfWeek() + " " + ei.TimeOfDay() + " " + niceName);
                        writer.WriteEndElement();
                        writer.WriteStartElement("link");
                        writer.WriteValue(GetTVDB(false, "").WebsiteURL(ei.TheSeries.TVDBCode, ei.SeasonID, false));
                        writer.WriteEndElement();
                        writer.WriteStartElement("description");
                        writer.WriteValue(niceName + "<br/>" + ei.Overview);
                        writer.WriteEndElement();
                        writer.WriteStartElement("pubDate");
                        DateTime? dt = ei.GetAirDateDT(true);
                        if (dt != null)
                            writer.WriteValue(dt.Value.ToString("r"));
                        writer.WriteEndElement();
                        writer.WriteEndElement(); // item
                    }
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                    writer.Close();
                }
                return true;
            } // try
            catch
            {
                return false;
            }
        } 

        public bool GenerateUpcomingXML(Stream str, List<ProcessedEpisode> elist)
        {
            if (elist == null)
                return false;

            try
            {
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                settings.NewLineOnAttributes = true;
                settings.Encoding = System.Text.Encoding.ASCII;
                using (XmlWriter writer = XmlWriter.Create(str, settings))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("WhenToWatch");

                    foreach (ProcessedEpisode ei in elist)
                    {
                        string niceName = Settings.NamingStyle.NameForExt(ei, null, 0);

                        writer.WriteStartElement("item");
                        writer.WriteStartElement("id");
                        writer.WriteValue(ei.TheSeries.TVDBCode);
                        writer.WriteEndElement();
                        writer.WriteStartElement("SeriesName");
                        writer.WriteValue(ei.TheSeries.Name);
                        writer.WriteEndElement();
                        writer.WriteStartElement("SeasonNumber");

                        if (ei.SeasonNumber.ToString().Length > 1)
                        {
                            writer.WriteValue(ei.SeasonNumber);
                        }
                        else
                        {
                            writer.WriteValue("0" + ei.SeasonNumber);
                        }
                        writer.WriteEndElement();

                        writer.WriteStartElement("EpisodeNumber");
                        if (ei.EpNum.ToString().Length > 1)
                        {
                            writer.WriteValue(ei.EpNum);
                        }
                        else
                        {
                            writer.WriteValue("0" + ei.EpNum);
                        }
                        writer.WriteEndElement();

                        writer.WriteStartElement("EpisodeName");
                        writer.WriteValue(ei.Name);
                        writer.WriteEndElement();

                        writer.WriteStartElement("available");
                        DateTime? airdt = ei.GetAirDateDT(true);

                        if (airdt.Value.CompareTo(DateTime.Now) < 0) // has aired
                        {
                            List<FileInfo> fl = FindEpOnDisk(ei);
                            if ((fl != null) && (fl.Count > 0))
                                writer.WriteValue("true");
                            else if (ei.SI.DoMissingCheck)
                                writer.WriteValue("false");
                        }

                        writer.WriteEndElement();
                        writer.WriteStartElement("Overview");
                        writer.WriteValue(ei.Overview);
                        writer.WriteEndElement();

                        writer.WriteStartElement("FirstAired");
                        DateTime? dt = ei.GetAirDateDT(true);
                        if (dt != null)
                            writer.WriteValue(dt.Value.ToString("F"));
                        writer.WriteEndElement();
                        writer.WriteStartElement("Rating");
                        writer.WriteValue(ei.EpisodeRating);
                        writer.WriteEndElement();
                        writer.WriteStartElement("filename");
                        writer.WriteValue(ei.GetItem("filename"));
                        writer.WriteEndElement();

                        writer.WriteEndElement(); // item
                    }
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                    writer.Close();
                }
                return true;
            } // try
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                return false;
            }
        } // wtwXML Export

        public void WriteUpcomingRSSandXML()
        {
            if (!Settings.ExportWTWRSS && !Settings.ExportWTWXML) //Additional check added for the wtwXML export.
                return;

            try
            {
                // dirty try/catch to "fix" the problem that a share can disappear during a sleep/resume, and
                // when windows restarts, the share isn't "back" before this timer times out and fires
                // windows explorer tends to lose explorer windows on shares when slept/resumed, too, so its not
                // just me :P
                if (Settings.ExportWTWRSS)
                {
                    MemoryStream RSS = new MemoryStream(); //duplicated the IF statement one for RSS and one for XML so that both can be generated.
                    if (GenerateUpcomingRSS(RSS, NextNShows(Settings.ExportRSSMaxShows,Settings.ExportRSSDaysPast, Settings.ExportRSSMaxDays)))
                    {
                        StreamWriter sRSS = new StreamWriter(Settings.ExportWTWRSSTo);
                        sRSS.Write(System.Text.Encoding.UTF8.GetString(RSS.ToArray()));
                        sRSS.Close();
                    }
                }
                if (Settings.ExportWTWXML)
                {
                    MemoryStream ms = new MemoryStream();
                    if (GenerateUpcomingXML(ms, NextNShows(Settings.ExportRSSMaxShows, Settings.ExportRSSDaysPast, Settings.ExportRSSMaxDays)))
                    {
                        StreamWriter sw = new StreamWriter(Settings.ExportWTWXMLTo);
                        sw.Write(System.Text.Encoding.UTF8.GetString(ms.ToArray()));
                        sw.Close();
                    }
                }
            }
            catch
            {
            }
        }

        // see if showname is somewhere in filename
        public bool SimplifyAndCheckFilename(string filename, string showname, bool simplifyfilename, bool simplifyshowname)
        {
            return Regex.Match(simplifyfilename ? Helpers.SimplifyName(filename) : filename, "\\b" + (simplifyshowname ? Helpers.SimplifyName(showname) : showname) + "\\b", RegexOptions.IgnoreCase).Success;
        }

        public void CheckAgainstSABnzbd(SetProgressDelegate prog, int startpct, int totPct)
        {
            if (String.IsNullOrEmpty(Settings.SABAPIKey) || String.IsNullOrEmpty(Settings.SABHostPort))
            {
                prog.Invoke(startpct + totPct);
                return;
            }

            // get list of files being downloaded by SABnzbd

            // Something like:
            // http://localhost:8080/sabnzbd/api?mode=queue&apikey=xxx&start=0&limit=8888&output=xml
            String theURL = "http://" + Settings.SABHostPort +
                            "/sabnzbd/api?mode=queue&start=0&limit=8888&output=xml&apikey=" + Settings.SABAPIKey;

            WebClient wc = new WebClient();
            byte[] r = null;
            try
            {
                r = wc.DownloadData(theURL);
            }
            catch (WebException)
            {
            }
           
            if (r == null)
            {
                prog.Invoke(startpct + totPct);
                return;
            }

            try
            {
                result res = result.Deserialize(r);
                if (res.status == "False")
                {
                    MessageBox.Show(res.error, "SABnzbd Queue Check", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    prog.Invoke(startpct + totPct);
                    return;
                }
            }
            catch
            {
                // wasn't a result/error combo.  this is good!
            }

            queue sq = null;
            try
            {
                sq = queue.Deserialize(r);
            }
            catch (Exception)
            {
                MessageBox.Show("Error processing data from SABnzbd", "SABnzbd Queue Check", MessageBoxButtons.OK, MessageBoxIcon.Error);
                prog.Invoke(startpct + totPct);
                return;
            }

            System.Diagnostics.Debug.Assert(sq != null); // shouldn't happen
            if (sq == null || sq.slots==null || sq.slots.Length == 0) // empty queue
                return;

            var newList = new List<Item>();
            var toRemove = new List<Item>();
            int c = TheActionList.Count + 2;
            int n = 1;

            foreach (Item Action1 in TheActionList)
            {
                if (ActionCancel)
                    return;

                n++;
                prog.Invoke(startpct + totPct * n / c);

                if (!(Action1 is ItemMissing))
                    continue;

                ItemMissing Action = (ItemMissing) (Action1);

                string showname = Helpers.SimplifyName(Action.Episode.SI.ShowName);

                foreach (queueSlotsSlot te  in sq.slots)
                {
                    //foreach (queueSlotsSlot te in qs)
                    {
                        FileInfo file = new FileInfo(te.filename);
                        //if (!Settings.UsefulExtension(file.Extension, false)) // not a usefile file extension
                        //    continue;

                        if (SimplifyAndCheckFilename(file.FullName, showname, true, false))
                            // if (Regex::Match(simplifiedfname,"\\b"+showname+"\\b",RegexOptions::IgnoreCase)->Success)
                        {
                            int seasF;
                            int epF;
                            if (FindSeasEp(file, out seasF, out epF, Action.Episode.SI) &&
                                (seasF == Action.Episode.SeasonNumber) && (epF == Action.Episode.EpNum))
                            {
                                toRemove.Add(Action1);
                                newList.Add(new ItemSABnzbd(te, Action.Episode, Action.TheFileNoExt));
                                break;
                            }
                        }
                    }
                }
            }

            foreach (Item i in toRemove)
                TheActionList.Remove(i);

            foreach (Item Action in newList)
                TheActionList.Add(Action);

            prog.Invoke(startpct+totPct);
        }


        public void CheckAgainstuTorrent(SetProgressDelegate prog, int startpct, int totPct)
        {
            // get list of files being downloaded by uTorrent
            string resDatFile = Settings.ResumeDatPath;
            if (string.IsNullOrEmpty(resDatFile) || !File.Exists(resDatFile))
                return;

            BTResume btr = new BTResume(prog, resDatFile);
            if (!btr.LoadResumeDat(Args))
                return;

            List<TorrentEntry> downloading = btr.AllFilesBeingDownloaded(Settings, Args);

            var newList = new List<Item>();
            var toRemove = new List<Item>();
            int c = TheActionList.Count + 2;
            int n = 1;
            prog.Invoke(startpct + totPct * n / c);
            foreach (Item Action1 in TheActionList)
            {
                if (ActionCancel)
                    return;

                n++;
                prog.Invoke(100 * n / c);

                if (!(Action1 is ItemMissing))
                    continue;

                ItemMissing Action = (ItemMissing) (Action1);

                string showname = Helpers.SimplifyName(Action.Episode.SI.ShowName);

                foreach (TorrentEntry te in downloading)
                {
                    FileInfo file = new FileInfo(te.DownloadingTo);
                    if (!Settings.UsefulExtension(file.Extension, false)) // not a usefile file extension
                        continue;

                    if (SimplifyAndCheckFilename(file.FullName, showname, true, false)) // if (Regex::Match(simplifiedfname,"\\b"+showname+"\\b",RegexOptions::IgnoreCase)->Success)
                    {
                        int seasF;
                        int epF;
                        if (FindSeasEp(file, out seasF, out epF, Action.Episode.SI) && (seasF == Action.Episode.SeasonNumber) && (epF == Action.Episode.EpNum))
                        {
                            toRemove.Add(Action1);
                            newList.Add(new ItemuTorrenting(te, Action.Episode, Action.TheFileNoExt));
                            break;
                        }
                    }
                }
            }

            foreach (Item i in toRemove)
                TheActionList.Remove(i);

            foreach (Item Action in newList)
                TheActionList.Add(Action);

            prog.Invoke(startpct + totPct);
        }

        public void RSSSearch(SetProgressDelegate prog)
        {
            int c = TheActionList.Count + 2;
            int n = 1;
            prog.Invoke(100 * n / c);
            RSSList = new RSSItemList();
            foreach (string s in Settings.RssUrls)
                RSSList.DownloadRSS(s, Settings.FNPRegexs);

            var newItems = new List<Item>();
            var toRemove = new List<Item>();

            foreach (Item Action1 in TheActionList)
            {
                if (ActionCancel)
                    return;

                n++;
                prog.Invoke(100 * n / c);

                if (!(Action1 is ItemMissing))
                    continue;

                ItemMissing Action = (ItemMissing) (Action1);

                ProcessedEpisode pe = Action.Episode;
                string simpleShowName = Helpers.SimplifyName(pe.SI.ShowName);
                string simpleSeriesName = Helpers.SimplifyName(pe.TheSeries.Name);

                foreach (RSSItem rss in RSSList)
                {
                    if ((SimplifyAndCheckFilename(rss.ShowName, simpleShowName, true, false) || (string.IsNullOrEmpty(rss.ShowName) && SimplifyAndCheckFilename(rss.Title, simpleSeriesName, true, false))) && (rss.Season == pe.SeasonNumber) && (rss.Episode == pe.EpNum))
                    {
                        newItems.Add(new ActionRSS(rss, Action.TheFileNoExt, pe));
                        toRemove.Add(Action1);
                    }
                }
            }
            foreach (Item i in toRemove)
                TheActionList.Remove(i);
            foreach (Item Action in newItems)
                TheActionList.Add(Action);

            prog.Invoke(100);
        }

        public void ProcessSingleAction(Object infoIn)
        {
            ProcessActionInfo info = infoIn as ProcessActionInfo;
            if (info == null)
                return;

            ActionSemaphores[info.SemaphoreNumber].WaitOne(); // don't start until we're allowed to
            ActionStarting = false; // let our creator know we're started ok

            Action action = info.TheAction;
            if (action != null)
                action.Go(Settings, ref ActionPause, mStats);

            ActionSemaphores[info.SemaphoreNumber].Release(1);
        }

        public ActionQueue[] ActionProcessorMakeQueues(ScanListItemList theList)
        {
            // Take a single list
            // Return an array of "ActionQueue" items.
            // Each individual queue is processed sequentially, but all the queues run in parallel
            // The lists:
            //     - #0 all the cross filesystem moves, and all copies
            //     - #1 all quick "local" moves
            //     - #2 NFO Generator list
            //     - #3 Downloads (rss torrent, thumbnail, folder.jpg) across Settings.ParallelDownloads lists
            // We can discard any non-action items, as there is nothing to do for them

            ActionQueue[] queues = new ActionQueue[4];
            queues[0] = new ActionQueue("Move/Copy", 1); // cross-filesystem moves (slow ones)
            queues[1] = new ActionQueue("Move", 2); // local rename/moves
            queues[2] = new ActionQueue("Write NFO/pyTivo Meta", 4); // writing XBMC NFO files
            queues[3] = new ActionQueue("Download", Settings.ParallelDownloads); // downloading torrents, banners, thumbnails

            foreach (ScanListItem sli in theList)
            {
                Action action = sli as Action;

                if (action == null)
                    continue; // skip non-actions

                if (action is ActionNFO || action is ActionPyTivoMeta)
                    queues[2].Actions.Add(action);
                else if ((action is ActionDownload) || (action is ActionRSS))
                    queues[3].Actions.Add(action);
                else if (action is ActionCopyMoveRename)
                    queues[(action as ActionCopyMoveRename).QuickOperation() ? 1 : 0].Actions.Add(action);
            }
            return queues;
        }

        public void ActionProcessor(Object queuesIn)
        {
#if DEBUG
            System.Diagnostics.Debug.Assert(queuesIn is ActionQueue[]);
#endif
            ActionQueue[] queues = queuesIn as ActionQueue[];
            if (queues == null)
                return;

            int N = queues.Length;

            ActionWorkers = new List<Thread>();
            ActionSemaphores = new Semaphore[N];

            for (int i = 0; i < N; i++)
                ActionSemaphores[i] = new Semaphore(queues[i].ParallelLimit, queues[i].ParallelLimit); // allow up to numWorkers working at once

            try
            {
                for (;;)
                {
                    while (ActionPause)
                        Thread.Sleep(100);

                    // look through the list of semaphores to see if there is one waiting for some work to do
                    bool allDone = true;
                    int which = -1;
                    for (int i = 0; i < N; i++)
                    {
                        // something to do in this queue, and semaphore is available
                        if (queues[i].ActionPosition < queues[i].Actions.Count)
                        {
                            allDone = false;
                            if (ActionSemaphores[i].WaitOne(20, false))
                            {
                                which = i;
                                break;
                            }
                        }
                    }
                    if ((which == -1) && (allDone))
                        break; // all done!

                    if (which == -1)
                        continue; // no semaphores available yet, try again for one

                    ActionQueue Q = queues[which];
                    Action act = Q.Actions[Q.ActionPosition++];

                    if (act == null)
                        continue;

                    if (!act.Done)
                    {
                        Thread t = new Thread(ProcessSingleAction) {
                                                                            Name = "ProcessSingleAction(" + act.Name + ":" + act.ProgressText + ")"
                                                                        };
                        ActionWorkers.Add(t);
                        ActionStarting = true; // set to false in thread after it has the semaphore
                        t.Start(new ProcessActionInfo(which, act));

                        int nfr = ActionSemaphores[which].Release(1); // release our hold on the semaphore, so that worker can grab it
                        System.Diagnostics.Debug.Print("ActionProcessor[" + which + "] pool has " + nfr + " free");
                    }

                    while (ActionStarting) // wait for thread to get the semaphore
                        Thread.Sleep(10); // allow the other thread a chance to run and grab

                    // tidy up any finished workers
                    for (int i = ActionWorkers.Count - 1; i >= 0; i--)
                    {
                        if (!ActionWorkers[i].IsAlive)
                            ActionWorkers.RemoveAt(i); // remove dead worker
                    }
                }
                WaitForAllActionThreadsAndTidyUp();
            }
            catch (ThreadAbortException)
            {
                foreach (Thread t in ActionWorkers)
                    t.Abort();
                WaitForAllActionThreadsAndTidyUp();
            }
        }

        private void WaitForAllActionThreadsAndTidyUp()
        {
            if (ActionWorkers != null)
            {
                foreach (Thread t in ActionWorkers)
                {
                    if (t.IsAlive)
                        t.Join();
                }
            }

            ActionWorkers = null;
            ActionSemaphores = null;
        }

        public void DoActions(ScanListItemList theList)
        {
            if (theList == null)
                return;

            // Run tasks in parallel (as much as is sensible)

            ActionQueue[] queues = ActionProcessorMakeQueues(theList);
            ActionPause = false;

            // If not /hide, show CopyMoveProgress dialog

/* todo
            CopyMoveProgress cmp = null;
            if (!Args.Hide)
                cmp = new CopyMoveProgress(this, queues);

            ActionProcessorThread = new Thread(ActionProcessor) {
                                                                              Name = "ActionProcessorThread"
                                                                          };

            ActionProcessorThread.Start(queues);

            if ((cmp != null) && (cmp.ShowDialog() == DialogResult.Cancel))
                ActionProcessorThread.Abort();

            ActionProcessorThread.Join();

            theList.RemoveAll(x => (x is Action) && (x as Action).Done && !(x as Action).Error);
*/
        }

        public bool ListHasMissingItems(IList<Item> l)
        {
            foreach (Item i in l)
            {
                if (i is ItemMissing)
                    return true;
            }
            return false;
        }

        public void ActionGo(List<ShowItem> shows)
        {
            if (Settings.MissingCheck && !CheckAllFoldersExist(shows)) // only check for folders existing for missing check
                return;

            if (!DoDownloadsFG())
                return;

            Thread ActionWork = new Thread(ScanWorker);
            ActionWork.Name = "ActionWork";

            ActionCancel = false;

/* todo 
            if (!Args.Hide)
            {
                ScanProgDlg = new ScanProgress(Settings.RenameCheck || Settings.MissingCheck,
                                                    Settings.MissingCheck && Settings.SearchLocally,
                                                    Settings.MissingCheck && (Settings.CheckuTorrent || Settings.CheckSABnzbd),
                                                    Settings.MissingCheck && Settings.SearchRSS);
            }
            else
                ScanProgDlg = null;

            ActionWork.Start(shows);

            if ((ScanProgDlg != null) && (ScanProgDlg.ShowDialog() == DialogResult.Cancel))
            {
                ActionCancel = true;
                ActionWork.Interrupt();
            }
            else
                ActionWork.Join();

            ScanProgDlg = null;
*/
        }

        public bool CheckAllFoldersExist(List<ShowItem> showlist)
        {
            // show MissingFolderAction for any folders that are missing
            // return false if user cancels

            LockShowItems();

            if (showlist == null) // nothing specified?
                showlist = ShowItems; // everything

            foreach (ShowItem si in showlist)
            {
                if (!si.DoMissingCheck && !si.DoRename)
                    continue; // skip

                Dictionary<int, List<string>> flocs = si.AllFolderLocations(Settings);

                int[] numbers = new int[si.SeasonEpisodes.Keys.Count];
                si.SeasonEpisodes.Keys.CopyTo(numbers, 0);
                foreach (int snum in numbers)
                {
                    if (si.IgnoreSeasons.Contains(snum))
                        continue; // ignore this season

                    //int snum = kvp->Key;
                    if ((snum == 0) && (si.CountSpecials))
                        continue; // no specials season, they're merged into the seasons themselves

                    List<string> folders = new List<String>();

                    if (flocs.ContainsKey(snum))
                        folders = flocs[snum];

                    if ((folders.Count == 0) && (!si.AutoAddNewSeasons))
                        continue; // no folders defined or found, autoadd off, so onto the next

                    if (folders.Count == 0)
                    {
                        // no folders defined for this season, and autoadd didn't find any, so suggest the autoadd folder name instead
                        folders.Add(si.AutoFolderNameForSeason(snum, Settings));
                    }

                    foreach (string folderFE in folders)
                    {
                        String folder = folderFE;

                        // generate new filename info
                        bool goAgain = false;
                        DirectoryInfo di = null;
                        do
                        {
                            goAgain = false;
                            if (!string.IsNullOrEmpty(folder))
                            {
                                try
                                {
                                    di = new DirectoryInfo(folder);
                                }
                                catch
                                {
                                    goAgain = false;
                                    break;
                                }
                            }
                            if ((di == null) || (!di.Exists))
                            {
                                string sn = si.ShowName;
                                string text = snum + " of " + si.MaxSeason();
                                string theFolder = folder;
                                string otherFolder = null;

/* todo
                                FAResult whatToDo = FAResult.kfaNotSet;

                                if (Args.MissingFolder == CommandLineArgs.MissingFolderBehaviour.Create)
                                    whatToDo = FAResult.kfaCreate;
                                else if (Args.MissingFolder == CommandLineArgs.MissingFolderBehaviour.Ignore)
                                    whatToDo = FAResult.kfaIgnoreOnce;

                                if (Args.Hide && (whatToDo == FAResult.kfaNotSet))
                                    whatToDo = FAResult.kfaIgnoreOnce; // default in /hide mode is to ignore

                                if (whatToDo == FAResult.kfaNotSet)
                                {
                                    // no command line guidance, so ask the user
                                    // 									MissingFolderAction ^mfa = gcnew MissingFolderAction(sn, text, theFolder);
                                    // 									mfa->ShowDialog();
                                    // 									whatToDo = mfa->Result;
                                    // 									otherFolder = mfa->FolderName;

                                    MissingFolderAction mfa = new MissingFolderAction(sn, text, theFolder);
                                    mfa.ShowDialog();
                                    whatToDo = mfa.Result;
                                    otherFolder = mfa.FolderName;
                                }

                                if (whatToDo == FAResult.kfaCancel)
                                {
                                    UnlockShowItems();
                                    return false;
                                }
                                else if (whatToDo == FAResult.kfaCreate)
                                {
                                    Directory.CreateDirectory(folder);
                                    goAgain = true;
                                }
                                else if (whatToDo == FAResult.kfaIgnoreAlways)
                                {
                                    si.IgnoreSeasons.Add(snum);
                                    SetDirty();
                                    break;
                                }
                                else if (whatToDo == FAResult.kfaIgnoreOnce)
                                    break;
                                else if (whatToDo == FAResult.kfaRetry)
                                    goAgain = true;
                                else if (whatToDo == FAResult.kfaDifferentFolder)
                                {
                                    folder = otherFolder;
                                    di = new DirectoryInfo(folder);
                                    goAgain = !di.Exists;
                                    if (di.Exists && (si.AutoFolderNameForSeason(snum, Settings).ToLower() != folder.ToLower()))
                                    {
                                        if (!si.ManualFolderLocations.ContainsKey(snum))
                                            si.ManualFolderLocations[snum] = new List<String>();
                                        si.ManualFolderLocations[snum].Add(folder);
                                        SetDirty();
                                    }
                                }
*/
                            }
                        }
                        while (goAgain);
                    } // for each folder
                } // for each snum
            } // for each show
            UnlockShowItems();

            return true;
        }

        // CheckAllFoldersExist

        public void RemoveIgnored()
        {
            var toRemove = new List<Item>();
            foreach (Item item in TheActionList)
            {
                ScanListItem act = item as ScanListItem;
                foreach (IgnoreItem ii in Ignore)
                {
                    if (ii.SameFileAs(act.Ignore))
                    {
                        toRemove.Add(item);
                        break;
                    }
                }
            }
            foreach (Item Action in toRemove)
                TheActionList.Remove(Action);
        }

        public void RenameAndMissingCheck(SetProgressDelegate prog, List<ShowItem> showList)
        {
            TheActionList = new List<Item>();

            //int totalEps = 0;

            LockShowItems();

            if (showList == null)
                showList = ShowItems;

            //foreach (ShowItem si in showlist)
            //  if (si.DoRename)
            //    totalEps += si.SeasonEpisodes.Count;

            if (Settings.RenameCheck)
                Stats().RenameChecksDone++;

            if (Settings.MissingCheck)
                Stats().MissingChecksDone++;

            prog.Invoke(0);

            if (showList == null) // only do episode count if we're doing all shows and seasons
                mStats.NS_NumberOfEpisodes = 0;

            int c = 0;
            foreach (ShowItem si in showList)
            {
                if (ActionCancel)
                    return;

                System.Diagnostics.Debug.Print(DateTime.Now.ToLongTimeString()+ " Rename and missing check: " + si.ShowName);
                c++;

                prog.Invoke(100 * c / showList.Count);

                if (si.AllFolderLocations(Settings).Count == 0) // no folders defined for this show
                    continue; // so, nothing to do.

                // for each tv show, optionally write a tvshow.nfo file

                if (Settings.NFOs && !string.IsNullOrEmpty(si.AutoAdd_FolderBase) && (si.AllFolderLocations(Settings).Count > 0))
                {
                    FileInfo tvshownfo = Helpers.FileInFolder(si.AutoAdd_FolderBase, "tvshow.nfo");

                    bool needUpdate = !tvshownfo.Exists || (si.TheSeries().Srv_LastUpdated > TimeZone.Epoch(tvshownfo.LastWriteTime));
                    // was it written before we fixed the bug in <episodeguideurl> ?
                    needUpdate = needUpdate || (tvshownfo.LastWriteTime.ToUniversalTime().CompareTo(new DateTime(2009, 9, 13, 7, 30, 0, 0, DateTimeKind.Utc)) < 0);
                    if (needUpdate)
                        TheActionList.Add(new ActionNFO(tvshownfo, si));
                }

                // process each folder for each season...

                int[] numbers = new int[si.SeasonEpisodes.Keys.Count];
                si.SeasonEpisodes.Keys.CopyTo(numbers, 0);
                Dictionary<int, List<string>> allFolders = si.AllFolderLocations(Settings);

                int lastSeason = 0;
                foreach (int n in numbers)
                    if (n > lastSeason)
                        lastSeason = n;

                foreach (int snum in numbers)
                {
                    if (ActionCancel)
                        return;

                    if ((si.IgnoreSeasons.Contains(snum)) || (!allFolders.ContainsKey(snum)))
                        continue; // ignore/skip this season

                    if ((snum == 0) && (si.CountSpecials))
                        continue; // don't process the specials season, as they're merged into the seasons themselves

                    // all the folders for this particular season
                    List<string> folders = allFolders[snum];

                    bool folderNotDefined = (folders.Count == 0);
                    if (folderNotDefined && (Settings.MissingCheck && !si.AutoAddNewSeasons))
                        continue; // folder for the season is not defined, and we're not auto-adding it

                    List<ProcessedEpisode> eps = si.SeasonEpisodes[snum];
                    int maxEpisodeNumber = 0;
                    foreach (ProcessedEpisode episode in eps)
                    {
                        if (episode.EpNum > maxEpisodeNumber)
                            maxEpisodeNumber = episode.EpNum;
                    }

                    List<string> doneFolderJPG = new List<String>();
                    if (Settings.FolderJpg)
                    {
                        // main image for the folder itself

                        // base folder:
                        if (!string.IsNullOrEmpty(si.AutoAdd_FolderBase) && (si.AllFolderLocations(Settings, false).Count > 0))
                        {
                            FileInfo fi = Helpers.FileInFolder(si.AutoAdd_FolderBase, "folder.jpg");
                            if (!fi.Exists)
                            {
                                string bannerPath = si.TheSeries().GetItem(Settings.ItemForFolderJpg());
                                if (!string.IsNullOrEmpty(bannerPath))
                                    TheActionList.Add(new ActionDownload(si, null, fi, bannerPath));
                            }
                            doneFolderJPG.Add(si.AutoAdd_FolderBase);
                        }
                    }

                    foreach (string folder in folders)
                    {
                        if (ActionCancel)
                            return;

                        // generate new filename info
                        DirectoryInfo di;
                        try
                        {
                            di = new DirectoryInfo(folder);
                        }
                        catch
                        {
                            continue;
                        }

                        bool renCheck = Settings.RenameCheck && si.DoRename && di.Exists; // renaming check needs the folder to exist
                        bool missCheck = Settings.MissingCheck && si.DoMissingCheck;

                        if (Settings.FolderJpg)
                        {
                            // season folders JPGs

                            if (!doneFolderJPG.Contains(folder)) // some folders may come up multiple times
                            {
                                doneFolderJPG.Add(folder);

                                FileInfo fi = Helpers.FileInFolder(folder, "folder.jpg");
                                if (!fi.Exists)
                                {
                                    string bannerPath = si.TheSeries().GetItem(Settings.ItemForFolderJpg());
                                    if (!string.IsNullOrEmpty(bannerPath))
                                        TheActionList.Add(new ActionDownload(si, null, fi, bannerPath));
                                }
                            }
                        }

                        FileInfo[] files = di.GetFiles(); // all the files in the folder
                        FileInfo[] localEps = new FileInfo[maxEpisodeNumber + 1];

                        int maxEpNumFound = 0;
                        if (!renCheck && !missCheck)
                            continue;

                        foreach (FileInfo fi in files)
                        {
                            if (ActionCancel)
                                return;

                            int seasNum;
                            int epNum;

                            if (!FindSeasEp(fi, out seasNum, out epNum, si))
                                continue; // can't find season & episode, so this file is of no interest to us

                            if (seasNum == -1)
                                seasNum = snum;

#if !NOLAMBDA
                            int epIdx = eps.FindIndex(x => ((x.EpNum == epNum) && (x.SeasonNumber == seasNum)));
                            if (epIdx == -1)
                                continue; // season+episode number don't correspond to any episode we know of from thetvdb
                            ProcessedEpisode ep = eps[epIdx];
#else
    // equivalent of the 4 lines above, if compiling on MonoDevelop on Windows which, for 
    // some reason, doesn't seem to support lambda functions (the => thing)
                            
                            ProcessedEpisode ep = null;
                            
                            foreach (ProcessedEpisode x in eps)
                            {
                                if (((x.EpNum == epNum) && (x.SeasonNumber == seasNum)))
                                {
                                    ep = x;
                                    break;
                                }
                            }
                            if (ep == null)
                              continue;
                            // season+episode number don't correspond to any episode we know of from thetvdb
#endif

                            FileInfo actualFile = fi;

                            if (renCheck && Settings.UsefulExtension(fi.Extension, true)) // == RENAMING CHECK ==
                            {
                                string newname = Settings.FilenameFriendly(Settings.NamingStyle.NameForExt(ep, fi.Extension, folder.Length));

                                if (newname != actualFile.Name)
                                {
                                    actualFile = Helpers.FileInFolder(folder, newname); // rename updates the filename
                                      TheActionList.Add(new ActionCopyMoveRename(ActionCopyMoveRename.Op.Rename, fi, actualFile, ep));
                                }
                            }
                            if (missCheck && Settings.UsefulExtension(fi.Extension, false)) // == MISSING CHECK part 1/2 ==
                            {
                                // first pass of missing check is to tally up the episodes we do have
                                localEps[epNum] = actualFile;
                                if (epNum > maxEpNumFound)
                                    maxEpNumFound = epNum;
                            }
                        } // foreach file in folder

                        if (missCheck) // == MISSING CHECK part 2/2 (includes NFO and Thumbnails) ==
                        {
                            // second part of missing check is to see what is missing!

                            // look at the offical list of episodes, and look to see if we have any gaps

                            DateTime today = DateTime.Now;
                            TheTVDB.TheTVDB db = GetTVDB(true, "UpToDateCheck");
                            foreach (ProcessedEpisode dbep in eps)
                            {
                                if ((dbep.EpNum > maxEpNumFound) || (localEps[dbep.EpNum] == null)) // not here locally
                                {
                                    DateTime? dt = dbep.GetAirDateDT(true);
                                    bool dtOK = dt != null;

                                    bool notFuture = (dtOK && (dt.Value.CompareTo(today) < 0)); // isn't an episode yet to be aired

                                    bool noAirdatesUntilNow = true;
                                    // for specials "season", see if any season has any airdates
                                    // otherwise, check only up to the season we are considering
                                    for (int i = 1; i <= ((snum == 0) ? lastSeason : snum); i++)
                                    {
                                        if (HasAnyAirdates(si, i))
                                        {
                                            noAirdatesUntilNow = false;
                                            break;
                                        }
                                    }

                                    // only add to the missing list if, either:
                                    // - force check is on
                                    // - there are no airdates at all, for up to and including this season
                                    // - there is an airdate, and it isn't in the future
                                    if (noAirdatesUntilNow ||
                                        ((si.ForceCheckFuture || notFuture) && dtOK) ||
                                        (si.ForceCheckNoAirdate && !dtOK))
                                    {
                                        // then add it as officially missing
                                        TheActionList.Add(new ItemMissing(dbep, folder + Path.DirectorySeparatorChar + Settings.FilenameFriendly(Settings.NamingStyle.NameForExt(dbep, null, folder.Length))));
                                    }
                                }
                                else
                                {
                                    // the file is here
                                    if (showList == null)
                                        mStats.NS_NumberOfEpisodes++;

                                    // do NFO and thumbnail checks if required
                                    FileInfo filo = localEps[dbep.EpNum]; // filename (or future filename) of the file

                                    ThumbnailAndNFOCheck(dbep, filo, TheActionList);
                                }
                            } // up to date check, for each episode in thetvdb
                            db.Unlock("UpToDateCheck");
                        } // if doing missing check
                    } // for each folder for this season of this show
                } // for each season of this show
            } // for each show

            UnlockShowItems();
            RemoveIgnored();
        }

        private void ThumbnailAndNFOCheck(ProcessedEpisode dbep, FileInfo filo, IList<Item> addTo)
        {
            if (Settings.EpImgs)
            {
                string ban = dbep.GetItem("filename");
                if (!string.IsNullOrEmpty(ban))
                {
                    string fn = filo.Name;
                    fn = fn.Substring(0, fn.Length - filo.Extension.Length);
                    fn += ".tbn";
                    FileInfo img = Helpers.FileInFolder(filo.Directory, fn);
                    if (!img.Exists)
                        addTo.Add(new ActionDownload(dbep.SI, dbep, img, ban));
                }
            }
            if (Settings.NFOs)
            {
                string fn = filo.Name;
                fn = fn.Substring(0, fn.Length - filo.Extension.Length);
                fn += ".nfo";
                FileInfo nfo = Helpers.FileInFolder(filo.Directory, fn);

                if (!nfo.Exists || (dbep.Srv_LastUpdated > TimeZone.Epoch(nfo.LastWriteTime)))
                    addTo.Add(new ActionNFO(nfo, dbep));
            }
            if (Settings.pyTivoMeta)
            {
                string fn = filo.Name;
                fn += ".txt";
                string folder = filo.DirectoryName;
                if (Settings.pyTivoMetaSubFolder)
                    folder += "\\.meta";
                FileInfo meta = Helpers.FileInFolder(folder, fn);

                if (!meta.Exists || (dbep.Srv_LastUpdated > TimeZone.Epoch(meta.LastWriteTime)))
                    addTo.Add(new ActionPyTivoMeta(meta, dbep));
            }
        }

        public void NoProgress(int pct)
        {
        }

        public void ScanWorker(Object o)
        {
/* todo
            List<ShowItem> specific = (List<ShowItem>)(o);

            while (!Args.Hide && ((ScanProgDlg == null) || (!ScanProgDlg.Ready)))
                Thread.Sleep(10); // wait for thread to create the dialog

            TheActionList = new List<Item>();
            SetProgressDelegate noProgress = NoProgress;

            if (Settings.RenameCheck || Settings.MissingCheck)
                RenameAndMissingCheck(ScanProgDlg == null ? noProgress : ScanProgDlg.MediaLibProg, specific);

            if (Settings.MissingCheck)
            {
                if (ActionCancel)
                    return;

                // have a look around for any missing episodes

                if (Settings.SearchLocally && ListHasMissingItems(TheActionList))
                {
                    LookForMissingEps(ScanProgDlg == null ? noProgress : ScanProgDlg.LocalSearchProg);
                    RemoveIgnored();
                }

                if (ActionCancel)
                    return;


                bool ut = Settings.CheckuTorrent;
                bool sab = Settings.CheckSABnzbd;
                if (ut && ListHasMissingItems(TheActionList))
                {
                    CheckAgainstuTorrent(ScanProgDlg == null ? noProgress : ScanProgDlg.DownloadingProg, 0, sab ? 50:100);
                    RemoveIgnored();
                }

                if (sab && ListHasMissingItems(TheActionList))
                {
                    CheckAgainstSABnzbd(ScanProgDlg == null ? noProgress : ScanProgDlg.DownloadingProg, ut?50:0, ut?50:100);
                    RemoveIgnored();
                }

                if (ActionCancel)
                    return;

                if (Settings.SearchRSS && ListHasMissingItems(TheActionList))
                {
                    RSSSearch(ScanProgDlg == null ? noProgress : ScanProgDlg.RSSProg);
                    RemoveIgnored();
                }
            }
            if (ActionCancel)
                return;

            // sort Action list by type
            TheActionList.Sort(new ActionItemSorter()); // was new ActionSorter()

            if (ScanProgDlg != null)
                ScanProgDlg.Done();
*/
        }

        public bool MatchesSequentialNumber(string filename, ref int seas, ref int ep, ProcessedEpisode pe)
        {
            if (pe.OverallNumber == -1)
                return false;

            string num = pe.OverallNumber.ToString();

            bool found = Regex.Match("X" + filename + "X", "[^0-9]0*" + num + "[^0-9]").Success; // need to pad to let it match non-numbers at start and end
            if (found)
            {
                seas = pe.SeasonNumber;
                ep = pe.EpNum;
            }
            return found;
        }

        public static string SEFinderSimplifyFilename(string filename, string showNameHint)
        {
            // Look at showNameHint and try to remove the first occurance of it from filename
            // This is very helpful if the showname has a >= 4 digit number in it, as that
            // would trigger the 1302 -> 13,02 matcher
            // Also, shows like "24" can cause confusion

            filename = filename.Replace(".", " "); // turn dots into spaces

            if ((showNameHint == null) || (string.IsNullOrEmpty(showNameHint)))
                return filename;

            bool nameIsNumber = (Regex.Match(showNameHint, "^[0-9]+$").Success);

            int p = filename.IndexOf(showNameHint);

            if (p == 0)
            {
                filename = filename.Remove(0, showNameHint.Length);
                return filename;
            }

            if (nameIsNumber) // e.g. "24", or easy exact match of show name at start of filename
                return filename;

            foreach (Match m in Regex.Matches(showNameHint, "(?:^|[^a-z]|\\b)([0-9]{3,})")) // find >= 3 digit numbers in show name
            {
                if (m.Groups.Count > 1) // just in case
                {
                    string number = m.Groups[1].Value;
                    filename = Regex.Replace(filename, "(^|\\W)" + number + "\\b", ""); // remove any occurances of that number in the filename
                }
            }

            return filename;
        }

        private static bool FindSeasEpDateCheck(FileInfo fi, out int seas, out int ep, ShowItem si)
        {
            if (fi == null)
            {
                seas = -1;
                ep = -1;
                return false;
            }

            // look for a valid airdate in the filename
            // check for YMD, DMY, and MDY
            // only check against airdates we expect for the given show
            SeriesInfo ser = si.TVDB.GetSeries(si.TVDBCode);
            string[] dateFormats = new[] { "yyyy-MM-dd", "dd-MM-yyyy", "MM-dd-yyyy", "yy-MM-dd", "dd-MM-yy", "MM-dd-yy" };
            string filename = fi.Name;
            // force possible date separators to a dash
            filename = filename.Replace("/", "-");
            filename = filename.Replace(".", "-");
            filename = filename.Replace(",", "-");
            filename = filename.Replace(" ", "-");

            ep = -1;
            seas = -1;

            foreach (KeyValuePair<int, Season> kvp in ser.Seasons)
            {
                if (si.IgnoreSeasons.Contains(kvp.Value.SeasonNumber))
                    continue;

                foreach (Episode epi in kvp.Value.Episodes)
                {
                    DateTime? dt = epi.GetAirDateDT(false); // file will have local timezone date, not ours
                    if ((dt == null) || (!dt.HasValue))
                        continue;

                    TimeSpan closestDate = TimeSpan.MaxValue;

                    foreach (string dateFormat in dateFormats)
                    {
                        string datestr = dt.Value.ToString(dateFormat);
                        DateTime dtInFilename;
                        if (filename.Contains(datestr) && DateTime.TryParseExact(datestr, dateFormat, new CultureInfo("en-GB"), DateTimeStyles.None, out dtInFilename))
                        {
                            TimeSpan timeAgo = DateTime.Now.Subtract(dtInFilename);
                            if (timeAgo < closestDate)
                            {
                                seas = epi.SeasonNumber;
                                ep = epi.EpNum;
                                closestDate = timeAgo;
                            }
                        }
                    }
                }
            }

            return ((ep != -1) && (seas != -1));
        }

        public bool FindSeasEp(FileInfo fi, out int seas, out int ep, ShowItem si)
        {
            return TVDoc.FindSeasEp(fi, out seas, out ep, si, Settings.FNPRegexs, Settings.LookForDateInFilename);
        }

        public static bool FindSeasEp(FileInfo fi, out int seas, out int ep, ShowItem si, List<FilenameProcessorRegEx> rexps, bool doDateCheck)
        {
            if (fi == null)
            {
                seas = -1;
                ep = -1;
                return false;
            }

            if (doDateCheck && FindSeasEpDateCheck(fi, out seas, out ep, si))
                return true;

            string filename = fi.Name;
            int l = filename.Length;
            int le = fi.Extension.Length;
            filename = filename.Substring(0, l - le);
            return FindSeasEp(fi.Directory.FullName, filename, out seas, out ep, si, rexps);
        }

        public static bool FindSeasEp(string directory, string filename, out int seas, out int ep, ShowItem si, List<FilenameProcessorRegEx> rexps)
        {
            string showNameHint = (si != null) ? si.ShowName : "";
                
            seas = ep = -1;

            filename = SEFinderSimplifyFilename(filename, showNameHint);

            string fullPath = directory + Path.DirectorySeparatorChar + filename; // construct full path with sanitised filename

            if ((filename.Length > 256) || (fullPath.Length > 256))
                return false;

            int leftMostPos = filename.Length;

            filename = filename.ToLower() + " ";
            fullPath = fullPath.ToLower() + " ";

            foreach (FilenameProcessorRegEx re in rexps)
            {
                if (!re.Enabled)
                    continue;
                try
                {
                    Match m = Regex.Match(re.UseFullPath ? fullPath : filename, re.RE, RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        int adj = re.UseFullPath ? (fullPath.Length - filename.Length) : 0;

                        int p = Math.Min(m.Groups["s"].Index, m.Groups["e"].Index) - adj;
                        if (p >= leftMostPos)
                            continue;

                        if (!int.TryParse(m.Groups["s"].ToString(), out seas))
                            seas = -1;
                        if (!int.TryParse(m.Groups["e"].ToString(), out ep))
                            ep = -1;

                        leftMostPos = p;
                    }
                }
                catch (FormatException)
                {
                }
            }

            return ((seas != -1) || (ep != -1));
        }

        #region Nested type: ProcessActionInfo

        private class ProcessActionInfo
        {
            public readonly int SemaphoreNumber;
            public readonly Action TheAction;

            public ProcessActionInfo(int n, Action a)
            {
                SemaphoreNumber = n;
                TheAction = a;
            }
        } ;

        #endregion

        private ShowItem ShowItemForCode(int code)
        {
            foreach (ShowItem si in ShowItems)
            {
                if (si.TVDBCode == code)
                    return si;
            }
            return null;
        }
    }
}
