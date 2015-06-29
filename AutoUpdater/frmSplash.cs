using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using System.Data.Sql;
using System.Data.SqlClient;
using Microsoft.Win32;
using System.Web;
using System.Net;
using System.Diagnostics;

namespace AutoUpdater
{
    public partial class frmSplash : Form
    {
        public static BackgroundWorker bgWorker = new BackgroundWorker();
        public static FileInfo pfxStart = new FileInfo(Program.GetAppPath() + "\\pfxStart.exe"),
                               LOG = new FileInfo(Program.GetLogPath() + "\\ContentInstallationPatch.log");

        public frmSplash()
        {
            InitializeComponent();
            
            this.Show();
            bgWorker.RunWorkerAsync();
            //Application.Run(new frmUpdate());
        }

        public void frmSpash_Load(object sender, EventArgs e)
        {
            bgWorker.WorkerReportsProgress = true;
            bgWorker.WorkerSupportsCancellation = true;

            bgWorker.DoWork += new DoWorkEventHandler(bw_DoWork);
            bgWorker.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
            bgWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkCompleted);
        }

        public void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker localWorker = (BackgroundWorker)sender;

            localWorker.ReportProgress(0, "Initializing...");

            try
            {
                if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1) return;

                localWorker.ReportProgress(15, "Fetching local titles...");
                Dictionary<Guid, string> localTitles = Program.GetLocalTitles();

                localWorker.ReportProgress(15, "Fetching available titles...");
                Dictionary<Guid, string> awsTitles = Program.GetKCMaster();
                
                localWorker.ReportProgress(45, "Checking for title updates...");
                foreach (Guid titleGuid in localTitles.Keys)
                    if (!awsTitles.First(title => title.Key == titleGuid).Value.Equals(localTitles[titleGuid].Trim(), StringComparison.InvariantCultureIgnoreCase))
                    {
                        Program.Report(Program.LogHeader, true);
                        Program.Report(localTitles[titleGuid] + " requires an update.");

                        using (WebClient Client = new WebClient())
                        {
                            string AWSURL = Program.PullLatest();
                            FileInfo update = new FileInfo(Path.GetTempPath() + Regex.Split(AWSURL, @"\/*.*\/").ToList()[1].Replace("\n", string.Empty));
                            Program.Report("Update name : " + update.Name);
                            localWorker.ReportProgress(75, "Downloading title update...");
                            if (AWSURL != string.Empty) Client.DownloadFile(AWSURL, update.FullName);
                            {
                                localWorker.ReportProgress(75, "Installing title update...");
                                using (Process p = Process.Start(update.FullName))
                                    p.WaitForExit();
                            }
                        }
                        break;
                    }
            }
            catch (Exception)
            { Program.Report("Automatic content update failed."); }

            localWorker.CancelAsync();
        }

        public void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            int progress = e.ProgressPercentage; //Could be used for progress bar later.
            lblStep.Text = (string)e.UserState;
        }

        public void bw_RunWorkCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (frmSplash.pfxStart.Exists) Process.Start(frmSplash.pfxStart.FullName);
            else Program.Report("pfxStart.exe Not Found");
            this.Close();
        }
    }
}
