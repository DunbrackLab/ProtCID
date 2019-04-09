using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using DbLib;
using XtalLib.Settings;

namespace DataCollectorLib.rpsblast
{
    public class PdbSequenceReader
    {
        #region member variables
        private string chkDir = @"E:\DbProjectData\rpsblastDB\chk";
        private string seqFileDir = @"E:\DbProjectData\rpsblastDB\seq";
        private string lsFileDir = @"E:\DbProjectData\rpsblastDB\mtx";
        DbQuery dbQuery = new DbQuery();
        #endregion

        #region generate sequence files
        /// <summary>
        /// 
        /// </summary>
        public void GenerateSequenceFiles()
        {
            if (DbBuilder.dbConnect.ConnectString == "")
            {
                if (AppSettings.dirSettings == null)
                {
                    AppSettings.LoadDirSettings();
                }
                DbBuilder.dbConnect.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=masterkey;DATABASE=" +
                     AppSettings.dirSettings.dbPath;
                AppSettings.alignmentDbConnection = new DbConnect();
                AppSettings.alignmentDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=masterkey;DATABASE=" +
                     AppSettings.dirSettings.alignmentDbPath;
            }

            StreamWriter chkLsFileWriter = new StreamWriter(Path.Combine(lsFileDir, "nrpdb.pn"), true);
            StreamWriter seqLsFileWriter = new StreamWriter(Path.Combine(lsFileDir, "nrpdb.sn"), true);
            string[] chkFiles = Directory.GetFiles(chkDir, "*.chk");
            string pdbId = "";
            string chainId = "";
            string entryChain = "";
            string sequence = "";
            string seqFileName = "";
            StreamWriter seqWriter = null;
            foreach (string chkFile in chkFiles)
            {
                entryChain = GetPdbEntryChain(chkFile);
               
                if (entryChain.Length < 5)
                {
                    continue;
                }

                pdbId = entryChain.Substring(0, 4).ToLower ();
                chainId = entryChain.Substring(4, entryChain.Length - 4);

                seqFileName = Path.Combine(seqFileDir, entryChain + ".csq");
         
                sequence = GetChainSequence(pdbId, chainId);
                if (sequence != "")
                {
                    chkLsFileWriter.WriteLine(Path.Combine (chkDir, entryChain + ".chk"));
                    seqLsFileWriter.WriteLine(Path.Combine (seqFileDir, entryChain + ".csq"));

                    seqWriter = new StreamWriter(seqFileName);
                    seqWriter.WriteLine(sequence);
                    seqWriter.Close();
                }
            }
            chkLsFileWriter.Close();
            seqLsFileWriter.Close();
        }
        #endregion

        #region update sequence files
        /// <summary>
        /// generate the sequence files for the new/updated pdb entries
        /// </summary>
        public void UpdateSequenceFiles()
        {
            if (DbBuilder.dbConnect.ConnectString == "")
            {
                if (AppSettings.dirSettings == null)
                {
                    AppSettings.LoadDirSettings();
                }
                DbBuilder.dbConnect.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=masterkey;DATABASE=" +
                     AppSettings.dirSettings.dbPath;
                AppSettings.alignmentDbConnection = new DbConnect();
                AppSettings.alignmentDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=masterkey;DATABASE=" +
                     AppSettings.dirSettings.alignmentDbPath;
            }

            DateTime lastCollectTime = new DateTime(2009, 4, 9);

            string[] updateEntries = GetUpdateChkFiles(lastCollectTime);
            string pdbId = "";
            string chainId = "";
            string sequence = "";
            string seqFileName = "";
            StreamWriter seqWriter = null;

            foreach (string entryChain in updateEntries)
            {
                if (entryChain.Length < 5)
                {
                    continue;
                }

                pdbId = entryChain.Substring(0, 4).ToLower();
                chainId = entryChain.Substring(4, entryChain.Length - 4);

                seqFileName = Path.Combine(seqFileDir, entryChain + ".csq");

                sequence = GetChainSequence(pdbId, chainId);

                if (sequence != "")
                {
                    seqWriter = new StreamWriter(seqFileName);
                    seqWriter.WriteLine(sequence);
                    seqWriter.Close();
                }
            }

            StreamWriter chkLsFileWriter = new StreamWriter(Path.Combine(lsFileDir, "nrpdb.pn"), true);
            StreamWriter seqLsFileWriter = new StreamWriter(Path.Combine(lsFileDir, "nrpdb.sn"), true);
            string[] chkFiles = Directory.GetFiles(chkDir, "*.chk");
            
            foreach (string chkFile in chkFiles)
            {
                string entryChain = GetPdbEntryChain(chkFile);
                if (IsEntryChainExistInDb(entryChain.Substring(0, 4), entryChain.Substring(4, 1)))
                {
                    string[] csqFiles = Directory.GetFiles(seqFileDir, entryChain + "*");
                    if (csqFiles.Length == 1)
                    {
                        chkLsFileWriter.WriteLine(chkFile);
                        seqLsFileWriter.WriteLine(csqFiles[0]);
                    }
                }
                else
                {
                    File.Delete(chkFile);
                    File.Delete(Path.Combine(seqFileDir, entryChain + ".csq"));
                }
            }
            chkLsFileWriter.Close();
            seqLsFileWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lastCollectTime"></param>
        /// <returns></returns>
        private string[] GetUpdateChkFiles(DateTime lastCollectTime)
        {
            string[] chkFiles = Directory.GetFiles(chkDir, "*.chk");
            ArrayList updateEntryList = new ArrayList();
            foreach (string chkFile in chkFiles)
            {
                FileInfo fileInfo = new FileInfo(chkFile);
                if (DateTime.Compare(fileInfo.LastWriteTime, lastCollectTime) > 0)
                {
                    updateEntryList.Add(GetPdbEntryChain(chkFile));
                }
            }
            string[] updateEntries = new string[updateEntryList.Count];
            updateEntryList.CopyTo(updateEntries);
            return updateEntries;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authorChain"></param>
        /// <returns></returns>
        private bool IsEntryChainExistInDb(string pdbId, string authorChain)
        {
            string queryString = string.Format("Select * From AsymUnit " + 
                " Where PdbID = '{0}' AND AuthorChain = '{1}';", pdbId, authorChain);
            DataTable entryChainTable = dbQuery.Query(queryString);
            if (entryChainTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region accessory functions
        private string GetPdbEntryChain (string chkFile)
        {
            int fileNameIdx = chkFile.LastIndexOf("\\");
            int exeNameIdx = chkFile.IndexOf(".");
            string entryChain = chkFile.Substring(fileNameIdx + 1, exeNameIdx - fileNameIdx - 1);
            return entryChain;
        }

        private string GetChainSequence(string pdbId, string chainId)
        {
            string queryString = string.Format("Select Sequence From AsymUnit " +
                " Where PdbID = '{0}' AND AuthorChain = '{1}' AND PolymerType = 'polypeptide';", pdbId, chainId);
            DataTable sequenceTable = dbQuery.Query(queryString);
            if (sequenceTable.Rows.Count == 0)
            {
                if (chainId == "A")
                {
                    chainId = "_";
                    queryString = string.Format("Select Sequence From AsymUnit " +
                     " Where PdbID = '{0}' AND AuthorChain = '{1}' AND PolymerType = 'polypeptide';", pdbId, chainId);
                    sequenceTable = dbQuery.Query(queryString);
                }
            }
            if (sequenceTable.Rows.Count > 0)
            {
                return sequenceTable.Rows[0]["Sequence"].ToString().Trim();
            }
            return "";
        }

        private bool IsRepresentativeChain(string pdbId, string chainId)
        {
            string queryString = string.Format("Select * From RedundantPdbChains " + 
                " Where PdbID1 = '{0}' AND ChainID1 = '{1}';", pdbId, chainId);
            DataTable repChainTable = dbQuery.Query(AppSettings.alignmentDbConnection, queryString);
            if (repChainTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion
    }
}
