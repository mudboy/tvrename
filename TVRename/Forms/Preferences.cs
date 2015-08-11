// 
// Main website for TVRename is http://tvrename.com
// 
// Source code available at http://code.google.com/p/tvrename/
// 
// This code is released under GPLv3 http://www.gnu.org/licenses/gpl.html
// 

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using TvRename.Core;
using TvRename.Core.Settings;
using TvRename.Core.Settings.Serialized;
using TvRename.TheTVDB;
using ColumnHeader = SourceGrid.Cells.ColumnHeader;

namespace TVRename.Forms {
    /// <summary>
    /// Summary for Preferences
    ///
    /// WARNING: If you change the name of this class, you will need to change the
    ///          'Resource File Name' property for the managed resource compiler tool
    ///          associated with all .resx files this class depends on.  Otherwise,
    ///          the designers will not be able to interact properly with localized
    ///          resources associated with this form.
    /// </summary>
    public partial class Preferences : Form {
        private delegate void LoadLanguageDoneDel();

        private TVDoc mDoc;
        private Thread LoadLanguageThread;
        private String EnterPreferredLanguage; // hold here until background language download task is done
        private LoadLanguageDoneDel LoadLanguageDone;

        public Preferences(TVDoc doc, bool goToScanOpts) {
            InitializeComponent();
            LoadLanguageDone += LoadLanguageDoneFunc;
            SetupRSSGrid();
            SetupReplacementsGrid();
            mDoc = doc;
            if (goToScanOpts) {
                tabControl1.SelectedTab = tpScanOptions;
            }
        }

        public static bool OKExtensionsString(string s) {
            if (string.IsNullOrEmpty(s)) {
                return true;
            }
            string[] t = s.Split(';');
            foreach (string s2 in t) {
                if ((string.IsNullOrEmpty(s2)) || (!s2.StartsWith("."))) {
                    return false;
                }
            }
            return true;
        }

        private void OKButton_Click(object sender, EventArgs e) {
            if (!OKExtensionsString(txtVideoExtensions.Text)) {
                MessageBox.Show("Extensions list must be separated by semicolons, and each extension must start with a dot.", "Preferences", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tabControl1.SelectedIndex = 1;
                txtVideoExtensions.Focus();
                return;
            }
            if (!OKExtensionsString(txtOtherExtensions.Text)) {
                MessageBox.Show("Extensions list must be separated by semicolons, and each extension must start with a dot.", "Preferences", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tabControl1.SelectedIndex = 1;
                txtOtherExtensions.Focus();
                return;
            }
            TvSettings settings = mDoc.Settings;
            settings.Replacements.Clear();
            for (int i = 1; i < ReplacementsGrid.RowsCount; i++) {
                string from = (string) (ReplacementsGrid[i, 0].Value);
                string to = (string) (ReplacementsGrid[i, 1].Value);
                bool ins = (bool) (ReplacementsGrid[i, 2].Value);
                if (!string.IsNullOrEmpty(from)) {
                    settings.Replacements.Add(MyReplacement.Create(from, to, ins));
                }
            }
            settings.ExportWTWRSS = cbWTWRSS.Checked;
            settings.ExportWTWRSSTo = txtWTWRSS.Text;
            settings.ExportWTWXML = cbWTWXML.Checked;
            settings.ExportWTWXMLTo = txtWTWXML.Text;
            settings.ExportMissingXML = cbMissingXML.Checked;
            settings.ExportMissingXMLTo = txtMissingXML.Text;
            settings.ExportMissingCSV = cbMissingCSV.Checked;
            settings.ExportMissingCSVTo = txtMissingCSV.Text;
            settings.ExportRenamingXML = cbRenamingXML.Checked;
            settings.ExportRenamingXMLTo = txtRenamingXML.Text;
            settings.ExportFOXML = cbFOXML.Checked;
            settings.ExportFOXMLTo = txtFOXML.Text;
            settings.WTWRecentDays = Convert.ToInt32(txtWTWDays.Text);
            settings.StartupTab = cbStartupTab.SelectedIndex;
            settings.NotificationAreaIcon = cbNotificationIcon.Checked;
            settings.VideoExtensions = txtVideoExtensions.Text;
            settings.OtherExtensions = txtOtherExtensions.Text;
            settings.ExportRSSMaxDays = Convert.ToInt32(txtExportRSSMaxDays.Text);
            settings.ExportRSSMaxShows = Convert.ToInt32(txtExportRSSMaxShows.Text);
            settings.ExportRSSDaysPast = Convert.ToInt32(txtExportRSSDaysPast.Text);
            settings.KeepTogether = cbKeepTogether.Checked;
            settings.LeadingZeroOnSeason = cbLeadingZero.Checked;
            settings.ShowInTaskbar = chkShowInTaskbar.Checked;
            settings.RenameTxtToSub = cbTxtToSub.Checked;
            settings.ShowEpisodePictures = cbShowEpisodePictures.Checked;
            settings.AutoSelectShowInMyShows = cbAutoSelInMyShows.Checked;
            settings.SpecialsFolderName = txtSpecialsFolderName.Text;
            settings.ForceLowercaseFilenames = cbForceLower.Checked;
            settings.IgnoreSamples = cbIgnoreSamples.Checked;
            settings.uTorrentPath = txtRSSuTorrentPath.Text;
            settings.ResumeDatPath = txtUTResumeDatPath.Text;
            settings.SABHostPort = txtSABHostPort.Text;
            settings.SABAPIKey = txtSABAPIKey.Text;
            settings.CheckSABnzbd = cbCheckSABnzbd.Checked;
            settings.SearchRSS = cbSearchRSS.Checked;
            settings.EpImgs = cbEpImgs.Checked;
            settings.NFOs = cbNFOs.Checked;
            settings.pyTivoMeta = cbMeta.Checked;
            settings.pyTivoMetaSubFolder = cbMetaSubfolder.Checked;
            settings.FolderJpg = cbFolderJpg.Checked;
            settings.RenameCheck = cbRenameCheck.Checked;
            settings.MissingCheck = cbMissing.Checked;
            settings.SearchLocally = cbSearchLocally.Checked;
            settings.LeaveOriginals = cbLeaveOriginals.Checked;
            settings.CheckuTorrent = cbCheckuTorrent.Checked;
            settings.LookForDateInFilename = cbLookForAirdate.Checked;
            settings.ShouldMonitorFolders = cbMonitorFolder.Checked;
            if (rbFolderFanArt.Checked) {
                settings.FolderJpgIs = TvSettings.FolderJpgIsType.FanArt;
            } else {
                if (rbFolderBanner.Checked) {
                    settings.FolderJpgIs = TvSettings.FolderJpgIsType.Banner;
                } else {
                    settings.FolderJpgIs = TvSettings.FolderJpgIsType.Poster;
                }
            }
            TheTVDB db = mDoc.GetTVDB(true, "Preferences-OK");
            foreach (var kvp in db.LanguageList) {
                if (kvp.Value == cbLanguages.Text) {
                    settings.PreferredLanguage = kvp.Key;
                    break;
                }
            }
            if (rbWTWScan.Checked) {
                settings.WTWDoubleClick = TvSettings.WTWDoubleClickAction.Scan;
            } else {
                settings.WTWDoubleClick = TvSettings.WTWDoubleClickAction.Search;
            }
            db.SaveCache();
            db.Unlock("Preferences-OK");
            try {
                settings.SampleFileMaxSizeMB = int.Parse(txtMaxSampleSize.Text);
            } catch {
                settings.SampleFileMaxSizeMB = 50;
            }
            try {
                settings.ParallelDownloads = int.Parse(txtParallelDownloads.Text);
            } catch {
                settings.ParallelDownloads = 4;
            }
            if (settings.ParallelDownloads < 1) {
                settings.ParallelDownloads = 1;
            } else {
                if (settings.ParallelDownloads > 8) {
                    settings.ParallelDownloads = 8;
                }
            }

            // RSS URLs
            settings.RssUrls.Clear();
            for (int i = 1; i < RSSGrid.RowsCount; i++) {
                string url = (string) (RSSGrid[i, 0].Value);
                if (!string.IsNullOrEmpty(url)) {
                    settings.RssUrls.Add(url);
                }
            }

            settings.Colors = new List<MyShowStatusTVWColors>();
            foreach (ListViewItem item in lvwDefinedColors.Items)
            {
                if (item.SubItems.Count > 1 && !string.IsNullOrEmpty(item.SubItems[1].Text) && item.Tag != null && item.Tag is MyShowStatusTVWColors) {
                    var x = item.Tag as MyShowStatusTVWColors;
                    x.Color = item.SubItems[1].Text;
                    settings.Colors.Add(x);
                }
            }
            mDoc.SetDirty();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Preferences_Load(object sender, EventArgs e) {
            SetupLanguages();
            TvSettings S = mDoc.Settings;
            int r = 1;
            foreach (MyReplacement R in S.Replacements) {
                AddNewReplacementRow(R.This, R.That, R.CaseInsensitive);
                r++;
            }
            txtMaxSampleSize.Text = S.SampleFileMaxSizeMB.ToString();
            cbWTWRSS.Checked = S.ExportWTWRSS;
            txtWTWRSS.Text = S.ExportWTWRSSTo;
            txtWTWDays.Text = S.WTWRecentDays.ToString();
            cbWTWXML.Checked = S.ExportWTWXML;
            txtWTWXML.Text = S.ExportWTWXMLTo;
            txtExportRSSMaxDays.Text = S.ExportRSSMaxDays.ToString();
            txtExportRSSMaxShows.Text = S.ExportRSSMaxShows.ToString();
            txtExportRSSDaysPast.Text = S.ExportRSSDaysPast.ToString();
            cbMissingXML.Checked = S.ExportMissingXML;
            txtMissingXML.Text = S.ExportMissingXMLTo;
            cbMissingCSV.Checked = S.ExportMissingCSV;
            txtMissingCSV.Text = S.ExportMissingCSVTo;
            cbRenamingXML.Checked = S.ExportRenamingXML;
            txtRenamingXML.Text = S.ExportRenamingXMLTo;
            cbFOXML.Checked = S.ExportFOXML;
            txtFOXML.Text = S.ExportFOXMLTo;
            cbStartupTab.SelectedIndex = S.StartupTab;
            cbNotificationIcon.Checked = S.NotificationAreaIcon;
            txtVideoExtensions.Text = S.VideoExtensions;
            txtOtherExtensions.Text = S.OtherExtensions;
            cbKeepTogether.Checked = S.KeepTogether;
            cbKeepTogether_CheckedChanged(null, null);
            cbLeadingZero.Checked = S.LeadingZeroOnSeason;
            chkShowInTaskbar.Checked = S.ShowInTaskbar;
            cbTxtToSub.Checked = S.RenameTxtToSub;
            cbShowEpisodePictures.Checked = S.ShowEpisodePictures;
            cbAutoSelInMyShows.Checked = S.AutoSelectShowInMyShows;
            txtSpecialsFolderName.Text = S.SpecialsFolderName;
            cbForceLower.Checked = S.ForceLowercaseFilenames;
            cbIgnoreSamples.Checked = S.IgnoreSamples;
            txtRSSuTorrentPath.Text = S.uTorrentPath;
            txtUTResumeDatPath.Text = S.ResumeDatPath;
            txtSABHostPort.Text = S.SABHostPort;
            txtSABAPIKey.Text = S.SABAPIKey;
            cbCheckSABnzbd.Checked = S.CheckSABnzbd;
            txtParallelDownloads.Text = S.ParallelDownloads.ToString();
            cbSearchRSS.Checked = S.SearchRSS;
            cbEpImgs.Checked = S.EpImgs;
            cbNFOs.Checked = S.NFOs;
            cbMeta.Checked = S.pyTivoMeta;
            cbMetaSubfolder.Checked = S.pyTivoMetaSubFolder;
            cbFolderJpg.Checked = S.FolderJpg;
            cbRenameCheck.Checked = S.RenameCheck;
            cbCheckuTorrent.Checked = S.CheckuTorrent;
            cbLookForAirdate.Checked = S.LookForDateInFilename;
            cbMonitorFolder.Checked = S.ShouldMonitorFolders;
            cbMissing.Checked = S.MissingCheck;
            cbSearchLocally.Checked = S.SearchLocally;
            cbLeaveOriginals.Checked = S.LeaveOriginals;
            EnterPreferredLanguage = S.PreferredLanguage;
            switch (S.WTWDoubleClick) {
                case TvSettings.WTWDoubleClickAction.Search:
                default:
                    rbWTWSearch.Checked = true;
                    break;
                case TvSettings.WTWDoubleClickAction.Scan:
                    rbWTWScan.Checked = true;
                    break;
            }
            EnableDisable(null, null);
            ScanOptEnableDisable();
            FillSearchFolderList();
            foreach (string s in S.RssUrls) {
                AddNewRSSRow(s);
            }
            switch (S.FolderJpgIs) {
                case TvSettings.FolderJpgIsType.FanArt:
                    rbFolderFanArt.Checked = true;
                    break;
                case TvSettings.FolderJpgIsType.Banner:
                    rbFolderBanner.Checked = true;
                    break;
                default:
                    rbFolderPoster.Checked = true;
                    break;
            }

            if (S.Colors != null)
            {
                foreach (var showStatusColor in S.Colors)
                {
                    ListViewItem item = new ListViewItem {
                        Text = showStatusColor.Text, Tag = showStatusColor
                    };
                    item.SubItems.Add(showStatusColor.Color);
                    item.ForeColor = ColorTranslator.FromHtml(showStatusColor.Color);
                    lvwDefinedColors.Items.Add(item);
                }
            }
            FillTreeViewColoringShowStatusTypeCombobox();
        }

        private void FillTreeViewColoringShowStatusTypeCombobox() {
            // Shows
            foreach (string status in Enum.GetNames(typeof (ShowAirStatus))) {
                cboShowStatus.Items.Add(new MyShowStatusTVWColors {IsMeta = true, IsShowLevel = true, ShowStatus = status});
            }
            var showStatusList = new List<string>();
            var shows = mDoc.GetShowItems(false);
            foreach (var show in shows) {
                if (!showStatusList.Contains(show.ShowStatus)) {
                    showStatusList.Add(show.ShowStatus);
                }
            }
            foreach (string status in showStatusList) {
                cboShowStatus.Items.Add(new MyShowStatusTVWColors {IsMeta = false, IsShowLevel = true, ShowStatus = status});
            }
            // Seasons
            foreach (string status in Enum.GetNames(typeof (Season.SeasonStatus))) {
                cboShowStatus.Items.Add(new MyShowStatusTVWColors {IsMeta = true, IsShowLevel = false, ShowStatus = status});
            }
            cboShowStatus.DisplayMember = "Text";
        }

        private void Browse(TextBox txt) {
            saveFile.FileName = txt.Text;
            if (saveFile.ShowDialog() == DialogResult.OK) {
                txt.Text = saveFile.FileName;
            }
        }

        private void bnBrowseWTWRSS_Click(object sender, EventArgs e) {
            Browse(txtWTWRSS);
        }

        private void bnBrowseWTWXML_Click(object sender, EventArgs e) {
            Browse(txtWTWXML);
        }

        private void txtNumberOnlyKeyPress(object sender, KeyPressEventArgs e) {
            // digits only
            if ((e.KeyChar >= 32) && (!Char.IsDigit(e.KeyChar))) {
                e.Handled = true;
            }
        }

        private void CancelButton_Click(object sender, EventArgs e) {
            Close();
        }

        private void cbNotificationIcon_CheckedChanged(object sender, EventArgs e) {
            if (!cbNotificationIcon.Checked) {
                chkShowInTaskbar.Checked = true;
            }
        }

        private void chkShowInTaskbar_CheckedChanged(object sender, EventArgs e) {
            if (!chkShowInTaskbar.Checked) {
                cbNotificationIcon.Checked = true;
            }
        }

        private void cbKeepTogether_CheckedChanged(object sender, EventArgs e) {
            cbTxtToSub.Enabled = cbKeepTogether.Checked;
        }

        private void bnBrowseMissingCSV_Click(object sender, EventArgs e) {
            Browse(txtMissingCSV);
        }

        private void bnBrowseMissingXML_Click(object sender, EventArgs e) {
            Browse(txtMissingXML);
        }

        private void bnBrowseRenamingXML_Click(object sender, EventArgs e) {
            Browse(txtRenamingXML);
        }

        private void bnBrowseFOXML_Click(object sender, EventArgs e) {
            Browse(txtFOXML);
        }

        private void EnableDisable(object sender, EventArgs e) {
            txtWTWRSS.Enabled = cbWTWRSS.Checked;
            bnBrowseWTWRSS.Enabled = cbWTWRSS.Checked;
            txtWTWXML.Enabled = cbWTWXML.Checked;
            bnBrowseWTWXML.Enabled = cbWTWXML.Checked;
            bool wtw;
            if ((cbWTWRSS.Checked) || (cbWTWXML.Checked)) {
                wtw = true;
            } else {
                wtw = false;
            }
            label4.Enabled = wtw;
            label15.Enabled = wtw;
            label16.Enabled = wtw;
            label17.Enabled = wtw;
            txtExportRSSMaxDays.Enabled = wtw;
            txtExportRSSMaxShows.Enabled = wtw;
            txtExportRSSDaysPast.Enabled = wtw;
            bool fo = cbFOXML.Checked;
            txtFOXML.Enabled = fo;
            bnBrowseFOXML.Enabled = fo;
            bool ren = cbRenamingXML.Checked;
            txtRenamingXML.Enabled = ren;
            bnBrowseRenamingXML.Enabled = ren;
            bool misx = cbMissingXML.Checked;
            txtMissingXML.Enabled = misx;
            bnBrowseMissingXML.Enabled = misx;
            bool misc = cbMissingCSV.Checked;
            txtMissingCSV.Enabled = misc;
            bnBrowseMissingCSV.Enabled = misc;
        }

        private void bnAddSearchFolder_Click(object sender, EventArgs e) {
            int n = lbSearchFolders.SelectedIndex;
            folderBrowser.SelectedPath = n != -1 ? mDoc.SearchFolders[n] : "";
            if (folderBrowser.ShowDialog() == DialogResult.OK) {
                mDoc.SearchFolders.Add(folderBrowser.SelectedPath);
                mDoc.SetDirty();
            }
            FillSearchFolderList();
        }

        private void bnRemoveSearchFolder_Click(object sender, EventArgs e) {
            int n = lbSearchFolders.SelectedIndex;
            if (n == -1) {
                return;
            }
            mDoc.SearchFolders.RemoveAt(n);
            mDoc.SetDirty();
            FillSearchFolderList();
        }

        private void bnOpenSearchFolder_Click(object sender, EventArgs e) {
            int n = lbSearchFolders.SelectedIndex;
            if (n == -1) {
                return;
            }
            TVDoc.SysOpen(mDoc.SearchFolders[n]);
        }

        private void FillSearchFolderList() {
            lbSearchFolders.Items.Clear();
            mDoc.SearchFolders.Sort();
            foreach (string efi in mDoc.SearchFolders) {
                lbSearchFolders.Items.Add(efi);
            }
        }

        private void lbSearchFolders_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Delete) {
                bnRemoveSearchFolder_Click(null, null);
            }
        }

        private void lbSearchFolders_DragOver(object sender, DragEventArgs e) {
            e.Effect = !e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.None : DragDropEffects.Copy;
        }

        private void lbSearchFolders_DragDrop(object sender, DragEventArgs e) {
            string[] files = (string[]) (e.Data.GetData(DataFormats.FileDrop));
            for (int i = 0; i < files.Length; i++) {
                string path = files[i];
                try {
                    DirectoryInfo di = new DirectoryInfo(path);
                    if (di.Exists) {
                        mDoc.SearchFolders.Add(path.ToLower());
                    }
                } catch {}
            }
            mDoc.SetDirty();
            FillSearchFolderList();
        }

        private void bnRSSBrowseuTorrent_Click(object sender, EventArgs e) {
            openFile.FileName = txtRSSuTorrentPath.Text;
            openFile.Filter = "utorrent.exe|utorrent.exe|All Files (*.*)|*.*";
            if (openFile.ShowDialog() == DialogResult.OK) {
                txtRSSuTorrentPath.Text = openFile.FileName;
            }
        }

        private void bnUTBrowseResumeDat_Click(object sender, EventArgs e) {
            openFile.FileName = txtUTResumeDatPath.Text;
            openFile.Filter = "resume.dat|resume.dat|All Files (*.*)|*.*";
            if (openFile.ShowDialog() == DialogResult.OK) {
                txtUTResumeDatPath.Text = openFile.FileName;
            }
        }

        private void SetupReplacementsGrid() {
            SourceGrid.Cells.Views.Cell titleModel = new SourceGrid.Cells.Views.Cell {
                BackColor = Color.SteelBlue,
                ForeColor = Color.White,
                TextAlignment = DevAge.Drawing.ContentAlignment.MiddleLeft
            };
            ReplacementsGrid.Columns.Clear();
            ReplacementsGrid.Rows.Clear();
            ReplacementsGrid.RowsCount = 1;
            ReplacementsGrid.ColumnsCount = 3;
            ReplacementsGrid.FixedRows = 1;
            ReplacementsGrid.FixedColumns = 0;
            ReplacementsGrid.Selection.EnableMultiSelection = false;
            ReplacementsGrid.Columns[0].AutoSizeMode = SourceGrid.AutoSizeMode.EnableStretch | SourceGrid.AutoSizeMode.EnableAutoSize;
            ReplacementsGrid.Columns[1].AutoSizeMode = SourceGrid.AutoSizeMode.EnableStretch | SourceGrid.AutoSizeMode.EnableAutoSize;
            ReplacementsGrid.Columns[2].AutoSizeMode = SourceGrid.AutoSizeMode.EnableAutoSize;
            ReplacementsGrid.Columns[2].Width = 80;
            ReplacementsGrid.AutoStretchColumnsToFitWidth = true;
            ReplacementsGrid.Columns.StretchToFit();
            ReplacementsGrid.Columns[0].Width = ReplacementsGrid.Columns[0].Width - 8; // allow for scrollbar
            ReplacementsGrid.Columns[1].Width = ReplacementsGrid.Columns[1].Width - 8;

            //////////////////////////////////////////////////////////////////////
            // header row
            var h = new ColumnHeader("Search") {AutomaticSortEnabled = false};
            ReplacementsGrid[0, 0] = h;
            ReplacementsGrid[0, 0].View = titleModel;
            h = new ColumnHeader("Replace") {AutomaticSortEnabled = false};
            ReplacementsGrid[0, 1] = h;
            ReplacementsGrid[0, 1].View = titleModel;
            h = new ColumnHeader("Case Ins.") {AutomaticSortEnabled = false};
            ReplacementsGrid[0, 2] = h;
            ReplacementsGrid[0, 2].View = titleModel;
        }

        public static string CompulsoryReplacements() {
            return "*?<>:/\\|\""; // invalid filename characters, must be in the list!
        }

        private void AddNewReplacementRow(string from, string to, bool ins) {
            SourceGrid.Cells.Views.Cell roModel = new SourceGrid.Cells.Views.Cell {ForeColor = Color.Gray};
            int r = ReplacementsGrid.RowsCount;
            ReplacementsGrid.RowsCount = r + 1;
            ReplacementsGrid[r, 0] = new SourceGrid.Cells.Cell(from, typeof (string));
            ReplacementsGrid[r, 1] = new SourceGrid.Cells.Cell(to, typeof (string));
            ReplacementsGrid[r, 2] = new SourceGrid.Cells.CheckBox(null, ins);
            if (!string.IsNullOrEmpty(from) && (CompulsoryReplacements().IndexOf(from) != -1)) {
                ReplacementsGrid[r, 0].Editor.EnableEdit = false;
                ReplacementsGrid[r, 0].View = roModel;
            }
        }

        private void SetupRSSGrid() {
            SourceGrid.Cells.Views.Cell titleModel = new SourceGrid.Cells.Views.Cell {
                BackColor = Color.SteelBlue,
                ForeColor = Color.White,
                TextAlignment = DevAge.Drawing.ContentAlignment.MiddleLeft
            };
            RSSGrid.Columns.Clear();
            RSSGrid.Rows.Clear();
            RSSGrid.RowsCount = 1;
            RSSGrid.ColumnsCount = 1;
            RSSGrid.FixedRows = 1;
            RSSGrid.FixedColumns = 0;
            RSSGrid.Selection.EnableMultiSelection = false;
            RSSGrid.Columns[0].AutoSizeMode = SourceGrid.AutoSizeMode.EnableAutoSize | SourceGrid.AutoSizeMode.EnableStretch;
            RSSGrid.AutoStretchColumnsToFitWidth = true;
            RSSGrid.Columns.StretchToFit();

            //////////////////////////////////////////////////////////////////////
            // header row
            ColumnHeader h = new SourceGrid.Cells.ColumnHeader("URL");
            h.AutomaticSortEnabled = false;
            RSSGrid[0, 0] = h;
            RSSGrid[0, 0].View = titleModel;
        }

        private void AddNewRSSRow(string text) {
            int r = RSSGrid.RowsCount;
            RSSGrid.RowsCount = r + 1;
            RSSGrid[r, 0] = new SourceGrid.Cells.Cell(text, typeof (string));
        }

        private void bnRSSAdd_Click(object sender, EventArgs e) {
            AddNewRSSRow(null);
        }

        private void bnRSSRemove_Click(object sender, EventArgs e) {
            // multiselection is off, so we can cheat...
            int[] rowsIndex = RSSGrid.Selection.GetSelectionRegion().GetRowsIndex();
            if (rowsIndex.Length > 0) {
                RSSGrid.Rows.Remove(rowsIndex[0]);
            }
        }

        private void bnRSSGo_Click(object sender, EventArgs e) {
            // multiselection is off, so we can cheat...
            int[] rowsIndex = RSSGrid.Selection.GetSelectionRegion().GetRowsIndex();
            if (rowsIndex.Length > 0) {
                TVDoc.SysOpen((string) (RSSGrid[rowsIndex[0], 0].Value));
            }
        }

        private void SetupLanguages() {
            cbLanguages.Items.Clear();
            cbLanguages.Items.Add("Please wait...");
            cbLanguages.SelectedIndex = 0;
            cbLanguages.Update();
            cbLanguages.Enabled = false;
            LoadLanguageThread = new Thread(LoadLanguage);
            LoadLanguageThread.Start();
        }

        private void LoadLanguage() {
            TheTVDB db = mDoc.GetTVDB(true, "Preferences-LoadLanguages");
            bool aborted = false;
            try {
                if (!db.Connected) {
                    db.Connect();
                }
            } catch (ThreadAbortException) {
                aborted = true;
            }
            db.Unlock("Preferences-LoadLanguages");
            if (!aborted) {
                BeginInvoke(LoadLanguageDone);
            }
        }

        private void LoadLanguageDoneFunc() {
            FillLanguageList();
        }

        private void FillLanguageList() {
            TheTVDB db = mDoc.GetTVDB(true, "Preferences-FLL");
            cbLanguages.BeginUpdate();
            cbLanguages.Items.Clear();
            String pref = "";
            foreach (var kvp in db.LanguageList) {
                String name = kvp.Value;
                cbLanguages.Items.Add(name);
                if (EnterPreferredLanguage == kvp.Key) {
                    pref = kvp.Value;
                }
            }
            cbLanguages.EndUpdate();
            cbLanguages.Text = pref;
            cbLanguages.Enabled = true;
            db.Unlock("Preferences-FLL");
        }

        /*
        private void bnLangDown_Click(object sender, EventArgs e)
        {
            int n = lbLangs.SelectedIndex;
            if (n == -1)
                return;

            if (n < (LangList.Count - 1))
            {
                LangList.Reverse(n, 2);
                FillLanguageList();
                lbLangs.SelectedIndex = n + 1;
            }
        }

        private void bnLangUp_Click(object sender, EventArgs e)
        {
            int n = lbLangs.SelectedIndex;
            if (n == -1)
                return;
            if (n > 0)
            {
                LangList.Reverse(n - 1, 2);
                FillLanguageList();
                lbLangs.SelectedIndex = n - 1;
            }
        }*/

        private void cbMissing_CheckedChanged(object sender, EventArgs e) {
            ScanOptEnableDisable();
        }

        private void ScanOptEnableDisable() {
            bool e = cbMissing.Checked;
            cbSearchRSS.Enabled = e;
            cbSearchLocally.Enabled = e;
            cbEpImgs.Enabled = e;
            cbNFOs.Enabled = e;
            cbMeta.Enabled = e;
            cbCheckuTorrent.Enabled = e;
            bool e2 = cbSearchLocally.Checked;
            cbLeaveOriginals.Enabled = e && e2;
            bool e3 = cbMeta.Checked;
            cbMetaSubfolder.Enabled = e && e3;
        }

        private void cbSearchLocally_CheckedChanged(object sender, EventArgs e) {
            ScanOptEnableDisable();
        }

        private void cbMeta_CheckedChanged(object sender, EventArgs e) {
            ScanOptEnableDisable();
        }

        private void bnReplaceAdd_Click(object sender, EventArgs e) {
            AddNewReplacementRow(null, null, false);
        }

        private void bnReplaceRemove_Click(object sender, EventArgs e) {
            // multiselection is off, so we can cheat...
            int[] rowsIndex = ReplacementsGrid.Selection.GetSelectionRegion().GetRowsIndex();
            if (rowsIndex.Length > 0) {
                // don't delete compulsory items
                int n = rowsIndex[0];
                string from = (string) (ReplacementsGrid[n, 0].Value);
                if (string.IsNullOrEmpty(from) || (CompulsoryReplacements().IndexOf(from) == -1)) {
                    ReplacementsGrid.Rows.Remove(n);
                }
            }
        }

        private void btnAddShowStatusColoring_Click(object sender, EventArgs e) {
            if (cboShowStatus.SelectedItem != null && !string.IsNullOrEmpty(txtShowStatusColor.Text)) {
                try {
                    MyShowStatusTVWColors ssct = cboShowStatus.SelectedItem as MyShowStatusTVWColors;
                    if (!ColorTranslator.FromHtml(txtShowStatusColor.Text).IsEmpty && ssct != null) {
                        ListViewItem item;
                        item = lvwDefinedColors.FindItemWithText(ssct.Text);
                        if (item == null) {
                            item = new ListViewItem();
                            item.SubItems.Add(txtShowStatusColor.Text);
                            lvwDefinedColors.Items.Add(item);
                        }
                        item.Text = ssct.Text;
                        item.SubItems[1].Text = txtShowStatusColor.Text;
                        item.ForeColor = ColorTranslator.FromHtml(txtShowStatusColor.Text);
                        item.Tag = ssct;
                        txtShowStatusColor.Text = string.Empty;
                        txtShowStatusColor.ForeColor = Color.Black;
                    }
                } catch {}
            }
        }

        private void btnSelectColor_Click(object sender, EventArgs e) {
            try {
                colorDialog.Color = ColorTranslator.FromHtml(txtShowStatusColor.Text);
            } catch {
                colorDialog.Color = Color.Black;
            }
            if (colorDialog.ShowDialog(this) == DialogResult.OK) {
                txtShowStatusColor.Text = TranslateColorToHtml(colorDialog.Color);
                txtShowStatusColor.ForeColor = colorDialog.Color;
            }
        }

        private string TranslateColorToHtml(Color c) {
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }

        private void lvwDefinedColors_DoubleClick(object sender, EventArgs e) {
            RemoveSelectedDefinedColor();
        }

        private void bnRemoveDefinedColor_Click(object sender, EventArgs e) {
            RemoveSelectedDefinedColor();
        }

        private void lvwDefinedColors_SelectedIndexChanged(object sender, EventArgs e) {
            bnRemoveDefinedColor.Enabled = lvwDefinedColors.SelectedItems.Count == 1;
        }

        private void RemoveSelectedDefinedColor() {
            if (lvwDefinedColors.SelectedItems.Count == 1) {
                lvwDefinedColors.Items.Remove(lvwDefinedColors.SelectedItems[0]);
            }
        }

        private void txtShowStatusColor_TextChanged(object sender, EventArgs e) {
            try {
                txtShowStatusColor.ForeColor = ColorTranslator.FromHtml(txtShowStatusColor.Text);
            } catch {
                txtShowStatusColor.ForeColor = Color.Black;
            }
        }

        private void Preferences_FormClosing(object sender, FormClosingEventArgs e) {
            if (LoadLanguageThread != null && LoadLanguageThread.IsAlive) {
                LoadLanguageThread.Abort();
                LoadLanguageThread.Join(500); // milliseconds timeout
            }
        }
    }
}