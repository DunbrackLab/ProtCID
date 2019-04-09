using System;
using System.IO;
using System.Xml.Serialization;
using ProgressLib;
using DbLib;

namespace ProtCidSettingsLib
{
	/// <summary>
	/// Summary description for AppSettings.
	/// </summary>
	public class ProtCidSettings
	{
		public static ProgressInfo progressInfo = new ProgressInfo ();
		// should set up when the application started, initialized in the constructor of the main form
		public static string applicationStartPath = "";
        public static string paramFile = Path.Combine(applicationStartPath, "Settings\\parameters.xml");
        public static string dirFile = Path.Combine(applicationStartPath, "Settings\\dirSettings.xml");
        public static string symOpsFile = Path.Combine(applicationStartPath, "Settings\\symOps.xml");
        public static string crystMethodFile = Path.Combine(applicationStartPath, "Settings\\CrystMethods.txt");
        public static string tempDir = Path.Combine (applicationStartPath, "xtal_temp");

		public static string dataType = "pfam"; // the default family is pfam
		public static bool buCompDone = false;
        public static DbConnect alignmentDbConnection = null;
        public static DbConnect buCompConnection = null;
        public static DbConnect protcidDbConnection = null;
        public static DbConnect pdbfamDbConnection = null;
        public static DbQuery alignmentQuery = null;
        public static DbQuery buCompQuery = null;
        public static DbQuery protcidQuery = null;
        public static DbQuery pdbfamQuery = null;
        // standard cutoffs for good Psiblast alignments
        public static double psiblastIdentityCutoff = 50;
        public static double psiblastCoverageCutoff = 0.80;
        public static string logFile = "ProtcidDbBuildLog.txt";
        public static StreamWriter logWriter = null;

        public static string chainLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        // this number is added to the original pdb sequence numbers so that 
        // the multi-chain domain can have distinct sequence numbers
        // when calculate Q scores. Entity ID * sudoSeqNumber + sequence number
        // use in DomainInterfaceRetriever and DomainAlignment class
        public static int sudoSeqNumber = 10000;

        // use for PDB/PISA/PQS BUs
        public static string[] buTypes = { "PDB", "PISA"};
        public enum BuType
        {
            PDB, PISA
        }

        // for pfam-peptide
        public static int peptideLengthCutoff = 30;

		public static DirectorySettings dirSettings = null;

        public ProtCidSettings ()
		{
		}

		#region setting files provided by parameters
		// can be called outside of application
		/// <summary>
		/// load data file directories
		/// </summary>
		public static void LoadDirSettings (string dirFile)
		{
			if (! File.Exists (dirFile))
			{
				throw new Exception ("Directory setting file not exists.");
			}
			dirSettings = new DirectorySettings ();
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(DirectorySettings));
			FileStream settingStream = new FileStream (dirFile, FileMode.Open);
			dirSettings = (DirectorySettings) xmlSerializer.Deserialize (settingStream);
			settingStream.Close ();

            logWriter = new StreamWriter(logFile, true);
		}
		#endregion

		#region Setting files provided in application
		/// <summary>
		/// load data file directories
		/// </summary>
		public static void LoadDirSettings ()
		{
			if (! File.Exists (dirFile))
			{
				throw new Exception ("Directory setting file not exists.");
			}
			dirSettings = new DirectorySettings ();
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(DirectorySettings));
			FileStream settingStream = new FileStream (dirFile, FileMode.Open);
			dirSettings = (DirectorySettings) xmlSerializer.Deserialize (settingStream);
			settingStream.Close ();

            logWriter = new StreamWriter(logFile, true);
		}		
		#endregion

        #region initialize database connections
        /// <summary>
        /// 
        /// </summary>
        public static void InitializeDbSettings ()
        {
            if (dirSettings == null)
            {
                LoadDirSettings();
            }
            pdbfamDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                dirSettings.pdbfamDbPath);
            protcidDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                dirSettings.protcidDbPath);
            alignmentDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                dirSettings.alignmentDbPath);
            buCompConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                dirSettings.baInterfaceDbPath);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dirFile"></param>
        public static void InitializeDbSettings(string dirFile)
        {
            LoadDirSettings(dirFile);

            pdbfamDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                dirSettings.pdbfamDbPath);
            protcidDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                dirSettings.protcidDbPath);
            alignmentDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                dirSettings.alignmentDbPath);
            buCompConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                dirSettings.baInterfaceDbPath);
        }
        #endregion
    }
}
