// 
// Main website for TVRename is http://tvrename.com
// 
// Source code available at http://code.google.com/p/tvrename/
// 
// This code is released under GPLv3 http://www.gnu.org/licenses/gpl.html
// 

using System.Windows.Forms;

namespace TVRename.Forms
{
    /// <summary>
    /// Summary for ScanProgress
    ///
    /// WARNING: If you change the name of this class, you will need to change the
    ///          'Resource File Name' property for the managed resource compiler tool
    ///          associated with all .resx files this class depends on.  Otherwise,
    ///          the designers will not be able to interact properly with localized
    ///          resources associated with this form.
    /// </summary>
    public partial class ScanProgress : Form
    {
        public bool Finished;
        public bool Ready;

        private int _pctLocalSearch;
        private int _pctMediaLib;
        private int _pctRss;
        private int _pctuTorrent;

        public ScanProgress(bool mediaLib, bool searchLocal, bool downloading, bool rss)
        {
            Ready = false;
            Finished = false;
            InitializeComponent();

            lbMediaLibrary.Enabled = mediaLib;
            lbSearchLocally.Enabled = searchLocal;
            lbCheckDownloading.Enabled = downloading;
            lbSearchRSS.Enabled = rss;
        }

        private void UpdateProg()
        {
            pbMediaLib.Value = ((_pctMediaLib < 0) ? 0 : ((_pctMediaLib > 100) ? 100 : _pctMediaLib));
            pbMediaLib.Update();
            pbLocalSearch.Value = ((_pctLocalSearch < 0) ? 0 : ((_pctLocalSearch > 100) ? 100 : _pctLocalSearch));
            pbLocalSearch.Update();
            pbRSS.Value = ((_pctRss < 0) ? 0 : ((_pctRss > 100) ? 100 : _pctRss));
            pbRSS.Update();
            pbDownloading.Value = ((_pctuTorrent < 0) ? 0 : ((_pctuTorrent > 100) ? 100 : _pctuTorrent));
            pbDownloading.Update();
        }

        public void MediaLibProg(int p)
        {
            _pctMediaLib = p;
        }

        public void LocalSearchProg(int p)
        {
            _pctLocalSearch = p;
        }

        public void RSSProg(int p)
        {
            _pctRss = p;
        }

        public void DownloadingProg(int p)
        {
            _pctuTorrent = p;
        }

        private void ScanProgress_Load(object sender, System.EventArgs e)
        {
            Ready = true;
            timer1.Start();
        }

        private void timer1_Tick(object sender, System.EventArgs e)
        {
            UpdateProg();
            timer1.Start();
            if (Finished)
                Close();
        }

        public void Done()
        {
            Finished = true;
        }
    }
}