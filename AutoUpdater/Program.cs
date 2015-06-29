using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using System.Threading;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using Microsoft.Win32;
using System.Web;
using System.Net;
using System.Diagnostics;
using System.Windows.Forms;
using System.ComponentModel;

namespace AutoUpdater
{
    class Program
    {
        public static RegHive REGINFO = new RegHive().Invoke();
        public static string AWSXML = "http://pfx_engagement.s3.amazonaws.com/KC/KCtitles.xml";

        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmSplash());
        }

        private static string Hook
        {
            get 
            {
                return new SqlConnectionStringBuilder
                {
                    DataSource = GetLocalDBMachine() + "\\ProFXEngagement",
                    InitialCatalog = "master",
                    UserID = "sa",
                    Password = "banglapur"
                }.ConnectionString;
            }
        }

        public static string LogHeader
        { get { return string.Format("{0} {1} {2}", "-----------------------------------------------------------------\r\nAutomatic Update Log Start Time : ", DateTime.Now.ToString(), "\r\n"); } }

        private static string GetLocalDBMachine()
        {
            try
            {
                string machine = PullRegEntry("LocalDBServer");
                return (machine.Equals(string.Empty)) ? Environment.MachineName : machine;
            }
            catch (Exception)
            {
                Report("GetLocalDBMachine failed.");
                return Environment.MachineName; 
            }
        }

        public static string GetAppPath()
        {
            try
            { return PullRegEntry("AppPath"); }
            catch (Exception)
            {
                Report("GetAppPath failed.");
                return string.Empty; 
            }
        }

        public static string GetLogPath()
        {
            return GetAppPath().Replace("WM", "Common");
        }

        public static Dictionary<Guid, TitleData> GetStuffsDB()
        {
            try
            {
                return PullSQL("SELECT Year, Name, TitleVersionID, SUBSTRING(ItemXML, CHARINDEX('productid=\"', ItemXML) + 11, 6) AS ProductID FROM [KC].[Ecosystem].[TitleVersion]")
                       .Select().ToDictionary(title => title.Field<Guid>("TitleVersionID"),
                                              title => new TitleData() { Name = title.Field<string>("Name"), 
                                                                         Year = title.Field<Int16>("Year") ,
                                                                         ProductID = Convert.ToInt32(title.Field<string>("ProductID")) });
            }
            catch(Exception)
            {
                Report("GetStuffsDB failed.");
                return new Dictionary<Guid, TitleData>();
            }
        }

        public static Dictionary<Guid, TitleData> GetStuffsXML()
        {
            try
            {
                return XDocument.Load(AWSXML).Root.Element("Titles").Elements("Title")
                                .ToDictionary(title => new Guid(title.Element("guid").Value),
                                              title => new TitleData() { Name = title.Element("name").Value, 
                                                                         Year = Convert.ToInt32(title.Element("year").Value), 
                                                                         ProductID = Convert.ToInt32(title.Element("productid").Value) });
            }
            catch (Exception)
            {
                Report("GetStuffsXML failed.");
                return new Dictionary<Guid, TitleData>(); 
            }
        }

        public static Dictionary<Guid, string> GetLocalTitles()
        {
            try
            {
                return PullSQL("SELECT Year, Name, TitleVersionID FROM [KC].[EcoSystem].[TitleVersion]")
                       .Select().ToDictionary(title => title.Field<Guid>("TitleVersionID"),
                                              title => string.Format("{0} {1}", title.Field<Int16>("Year"), title.Field<string>("Name")));
            }
            catch (Exception)
            {
                Report("Get Local Titles failed.");
                return new Dictionary<Guid, string>();
            }
        }

        public static Dictionary<Guid, string> GetKCMaster()
        {
            try
            {
                return XDocument.Load(AWSXML).Root.Element("Titles").Elements("Title")
                                .ToDictionary(title => new Guid(title.Element("guid").Value),
                                              title => string.Format("{0} {1}", title.Element("year").Value, title.Element("name").Value));
            }
            catch (Exception)
            {
                Report("Get KC master title list failed.");
                return new Dictionary<Guid, string>();
            }
        }

        public static string PullLatest()
        {
            try
            { return XDocument.Load(AWSXML).Root.Element("LatestPatch").Value.ToString(); }
            catch (Exception)
            {
                Report("PullLatest failed.");
                return string.Empty; 
            }
        }

        public static string PullPackage(string titleID)
        {
            try
            {
                string urlKCP = String.Empty;
                XDocument kcXML = XDocument.Load(AWSXML);
                IEnumerable<XElement> titleList = kcXML.Root.Element("Titles").Elements("Title");

                foreach (XElement title in titleList)
                {
                    bool found = false;
                    IEnumerable<XElement> attributeList = title.Elements();
                    foreach (XElement attribute in attributeList) if (attribute.Name == "guid" && attribute.Value == titleID.ToUpper()) found = true;

                    if (found)
                    {
                        urlKCP = title.Element("url").Value.ToString();
                        break;
                    }
                }
                return urlKCP;
            }
            catch (Exception)
            {
                Report("Pull Package failed");
                return string.Empty;
            }
        }

        private static string PullRegEntry(string key)
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, REGINFO.View))
                using (var sub1 = hklm.OpenSubKey(REGINFO.Root))
                    foreach (string val in sub1.GetValueNames())
                        if (val.Equals(key, StringComparison.InvariantCultureIgnoreCase))
                            return sub1.GetValue(val).ToString();
                return string.Empty;
            }
            catch(Exception)
            {
                Report("GetRegEntry failed: " + key);
                return string.Empty;
            }
        }

        private static DataTable PullSQL(string query)
        {
            DataTable table = new DataTable();
            using (SqlConnection conn = new SqlConnection() { ConnectionString = Hook })
            using (SqlDataAdapter adapter = new SqlDataAdapter(new SqlCommand(query, conn)))
            { conn.Open(); adapter.Fill(table); conn.Close(); }

            return table;
        }

        public static void Report(string msg)
        {
            Report(msg, false);
        }

        public static void Report(string msg, bool header)
        {
            using (StreamWriter writer = new StreamWriter(frmSplash.LOG.FullName, true))
            {
                writer.WriteLine((header) ? string.Format("{0}", msg) : string.Format("{0} {1}", DateTime.Now.ToString(), msg));
                writer.Close();
            }
        }

    }
    
    public class RegHive
    {
        public RegistryView View { get; private set; }
        public string Root { get; private set; }

        public RegHive Invoke()
        {

            if (System.Environment.Is64BitOperatingSystem)
            {
                Root = @"SOFTWARE\Wow6432Node\ProFxENGAGEMENT30\WM\";
                View = RegistryView.Registry64;
            }
            else
            {
                Root = @"SOFTWARE\ProFxENGAGEMENT30\WM\";
                View = RegistryView.Registry32;
            }
            return this;
        }
    }

    public class TitleData
    {
        public int ProductID { get; set; }
        public int Year { get; set; }
        public string Name { get; set; }

        public override bool Equals(object obj)
        {
            TitleData tmp = (TitleData)obj;
            if (tmp.ProductID == ProductID &&
               tmp.Year == Year) return true;
            return false;
        }

        public bool SameProduct(object obj)
        {
            TitleData tmp = (TitleData)obj;
            if (tmp.ProductID == ProductID) return true;
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
