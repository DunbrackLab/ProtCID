using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using ProtCidSettingsLib;
using DbLib;
using AuxFuncLib;

namespace DataCollectorLib.FatcatAlignment
{
    public class AlignmentsRetriever
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery ();
        private string fatcatDir = "/home/qifang/Fatcat/";
        private string exeFatcatDir = "/home/qifang/Fatcat/FATCAT/FATCATMain/";
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="nonAlignedPairsFile"></param>
        public string GetFatcatAlignmentsOnLinux(string nonAlignedPairsFile)
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }
            if (ProtCidSettings.alignmentDbConnection == null)
            {
                ProtCidSettings.alignmentDbConnection = new DbLib.DbConnect();
                ProtCidSettings.alignmentDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;" +
                    "PWD=fbmonkeyox;DATABASE=" + ProtCidSettings.dirSettings.alignmentDbPath;
            }

            string repChainPairsFileName = "NonAlignRepEntryPairs.txt";
            WriteRepChainPairsToFile(nonAlignedPairsFile, repChainPairsFileName);

            CmdOperations linuxOperator = new CmdOperations();

            linuxOperator.CopyWindowsDataToLinux(repChainPairsFileName, exeFatcatDir + repChainPairsFileName);

            string cmdLineFile = WriteFatcatCommandFile(repChainPairsFileName);
            linuxOperator.RunPlink(cmdLineFile);

            string alignFileInLinux = fatcatDir + repChainPairsFileName.Replace(".txt", ".aln");
            string alignFileInWindows = Path.Combine(ProtCidSettings.dirSettings.fatcatPath, repChainPairsFileName.Replace(".txt", ".aln"));
            linuxOperator.CopyLinuxDataToWindows(alignFileInLinux, alignFileInWindows);
            return alignFileInWindows;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignedPairListFile"></param>
        /// <returns></returns>
        private string WriteFatcatCommandFile(string alignedPairListFile)
        {
            string fatcatCmdFile = "fatcatCmdLine";
            StreamWriter dataWriter = new StreamWriter(fatcatCmdFile);
            string fatcatCmdLine = exeFatcatDir + "FATCATQue.pl " + exeFatcatDir + alignedPairListFile +
                " -q > " + fatcatDir + alignedPairListFile.Replace (".txt", ".aln");
            dataWriter.WriteLine(fatcatCmdLine);
            dataWriter.Close();
            return fatcatCmdFile;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="nonAlignedPairsFile"></param>
        public void WriteRepChainPairsToFile (string nonAlignedPairsFile, string repEntryPairFile)
        {
            StreamReader dataReader = new StreamReader(nonAlignedPairsFile);
            StreamWriter dataWriter = new StreamWriter(repEntryPairFile);
            string line = "";
            string repChain1 = "";
            string repChain2 = "";
            Dictionary<string, string> chainRepChainHash = new Dictionary<string,string> ();
            List<string> repChainPairList = new List<string> ();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                repChain1 = GetRepAsymChain(fields[0].Substring(0, 4), fields[0].Substring(4, fields[0].Length - 4), ref chainRepChainHash);
                repChain2 = GetRepAsymChain(fields[1].Substring(0, 4), fields[1].Substring(4, fields[1].Length - 4), ref chainRepChainHash);
                if (repChain1 == repChain2)
                {
                    continue;
                }
                if (!repChainPairList.Contains(repChain1 + "   " + repChain2))
                {
                    repChainPairList.Add(repChain1 + "   " + repChain2);
                    dataWriter.WriteLine(repChain1 + "   " + repChain2);
                }
            }
            dataReader.Close();
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <param name="chainRepChainHash"></param>
        /// <returns></returns>
        private string GetRepAsymChain(string pdbId, string asymChain, ref Dictionary<string, string> chainRepChainHash)
        {
            if (chainRepChainHash.ContainsKey(pdbId + asymChain))
            {
                return chainRepChainHash[pdbId + asymChain];
            }
            else
            {
                string repChain = "";
                string queryString = string.Format("Select crc From PdbCrcMap Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymChain);
                DataTable crcTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
                string crc = "";
                if (crcTable.Rows.Count > 0)
                {
                    crc = crcTable.Rows[0]["crc"].ToString();
                    queryString = string.Format("Select PdbID, AsymID From PdbCrcMap Where crc = '{0}' AND IsRep = '1';", crc);
                    DataTable repChainTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
                    if (repChainTable.Rows.Count > 0)
                    {
                        repChain = repChainTable.Rows[0]["PdbID1"].ToString() + repChainTable.Rows[0]["AsymChainID1"].ToString().TrimEnd();
                    }
                }                              
                if (repChain == "")
                {
                    repChain = pdbId + asymChain;
                }
                chainRepChainHash.Add(pdbId + asymChain, repChain);
                return repChain;
            }
        }
    }
}
