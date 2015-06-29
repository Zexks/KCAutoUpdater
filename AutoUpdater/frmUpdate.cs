using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;
using System.Windows.Forms;
using System.Diagnostics;

namespace AutoUpdater
{
    public partial class frmUpdate : Form
    {
        public static BackgroundWorker bgUpdateWorker = new BackgroundWorker();
        public static Dictionary<Guid, string> TitlesToBeUpdated = new Dictionary<Guid, string>();

        public frmUpdate()
        {
            InitializeComponent();

            this.Show();
        }

        public void frmUpdate_Load(object sender, EventArgs e)
        {
            PopList();

            bgUpdateWorker.WorkerReportsProgress = true;
            bgUpdateWorker.WorkerSupportsCancellation = true;

            bgUpdateWorker.DoWork += new DoWorkEventHandler(bw_DoWork);
            bgUpdateWorker.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
            bgUpdateWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkCompleted);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            btnAdd.Enabled = false;
            btnCancel.Enabled = false;
            lstTitles.Enabled = false;

            bgUpdateWorker.RunWorkerAsync();
        }

        public void PopList()
        {
            Guid guid1 = new Guid("02FBD952-B2B0-4DAD-9558-B83448DF6229");
            Guid guid2 = new Guid("B9CF3D6B-F5BE-4299-B401-2B271E34522D");
            TitlesToBeUpdated.Add(guid1, "Base Title: Financial Statement Audits v4");
            TitlesToBeUpdated.Add(guid2, "Knowledge-Based Audits of Commercial Entities v5");

            foreach (string titles in TitlesToBeUpdated.Values)
            {
                lstTitles.Items.Add(titles);
            }
        }

        public void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker localWorker = (BackgroundWorker)sender;
            
            try
            {
                #region InProgress
                // Logic for comparing XML to installed titles to allow automatic download
                //Dictionary<Guid, TitleData> toBeUpdated = new Dictionary<Guid, TitleData>();
                //Dictionary<Guid, TitleData> localTitles = Program.GetStuffsDB();
                //Dictionary<Guid, TitleData> awsTitles = Program.GetStuffsXML();
                //List<TitleData> thingy = awsTitles.Values.SelectMany(lt => localTitles.Values.Where(et => lt.ProductID == et.ProductID && lt.Year > et.Year)).ToList();
                //foreach (TitleData title in awsTitles.Values.SelectMany(lt => localTitles.Values.Where(et => lt.ProductID == et.ProductID && lt.Year > et.Year)))
                //    if (title.Year > localTitles.Values.Aggregate((a, b) => a.Year > b.Year ? a : b).Year)
                //        toBeUpdated.Add(localTitles.FirstOrDefault(x => title.Equals(x.Value)).Key, title);
#endregion
                foreach (string name in lstTitles.CheckedItems)
                {
                    Guid titleGuid = TitlesToBeUpdated.FirstOrDefault(x => x.Value == name).Key;
                    Program.Report(Program.LogHeader, true);
                    string title = TitlesToBeUpdated.FirstOrDefault(x => x.Key == titleGuid).Value;
                    Program.Report("Downloading " + title);

                    using (WebClient Client = new WebClient())
                    {
                        string AWSURL = Program.PullPackage(titleGuid.ToString());
                        FileInfo fileKCP = new FileInfo(Path.GetTempPath() + Regex.Split(AWSURL, @"\/*.*\/").ToList()[1].Replace("\n", string.Empty));
                        Program.Report("Update name : " + fileKCP.Name);
                        localWorker.ReportProgress(75, "Downloading title package...");
                        if (AWSURL != string.Empty) Client.DownloadFile(AWSURL, fileKCP.FullName);
                    }
                    break;
                }
            }
            catch (Exception)
            { Program.Report("Automatic content download failed."); }

            localWorker.CancelAsync();
        }

        public void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

        }

        public void bw_RunWorkCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.Close();
            if (frmSplash.pfxStart.Exists) Process.Start(frmSplash.pfxStart.FullName);
            else Program.Report("pfxStart.exe Not Found");
        }
    }
}
