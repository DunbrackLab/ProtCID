using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using DbLib;
using AuxFuncLib;
using DataCollectorLib.HHAlignment;
using ProtCidSettingsLib;
using PfamLib.ExtDataProcess;

namespace InterfaceClusterLib.Alignments
{
    /* this class is to retrieve hh alignments from PISCES mysql database
     *  
     * */
    public class HHAlignments
    {
        #region member variables   
        private double seqIdCutoff = 20; // it was 30, changed it to 20 on July 10, 2018
        private double coverageCutoff = 0.80;
        private string[] alignColumns = {"QueryEntry", "QueryChain", "QueryEntity", "HitEntry", "HitChain", "HitEntity", 
                                         "QueryLength", "QueryStart", "QueryEnd", "QuerySequence", 
                                         "HitLength", "HitStart", "HitEnd", "HitSequence", "Identity"};
        private static Dictionary<string, List<string>> crcPdbChainDict = new Dictionary<string,List<string>> ();
        private string hhAlignTableName = "PdbCrcHHAlignments";
        private string pdbCrcMapTableName = "PdbCrcMap";
        public DataTable pdbCrcMapTable = null;
        private string hhrFileDir = "";
        private string linuxDir = "/extra/pisces/hh/crcpdbHHout/crcHhrGlobal";

        public string HHAlignTableName
        {
            get
            {
                return hhAlignTableName;
            }
            set
            {
                hhAlignTableName = value;
            }
        }

        public HHAlignments ()
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
                ProtCidSettings.alignmentDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                ProtCidSettings.dirSettings.alignmentDbPath);
                ProtCidSettings.alignmentQuery = new DbQuery(ProtCidSettings.alignmentDbConnection);

                ProtCidSettings.pdbfamDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                ProtCidSettings.dirSettings.pdbfamDbPath);
                ProtCidSettings.pdbfamQuery = new DbQuery(ProtCidSettings.pdbfamDbConnection);
            }
            hhrFileDir = Path.Combine(ProtCidSettings.dirSettings.hhPath, "CrcPdbHhAlign\\crcHhrGlobal");
        }
        #endregion

        #region parse hh alignments
        public void ParseHhrFiles ()
        {
            HhrFileParser hhrParser = new HhrFileParser();
            hhrParser.BuildHhAlignments (hhrFileDir);
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdataParsingHhrFiles ()
        {
            // delete crcs not in db
 //           DeleteCrcsNotInCrcPdbMapTable(hhrFileDir);

            HhrFileParser hhrParser = new HhrFileParser();
            hhrParser.HHAlignTableName = hhAlignTableName;
            string[] newObsLsFiles = DownloadHhrFileFromFourpaws();

            // comment out due to the inconsistency between protcid and pisces
            // delete obsolete hh alignments from db.            
    //        hhrParser.DeleteObsoleteHhAlignData(newObsLsFiles[1]);

            string newLsFile = newObsLsFiles[0];
      //      string newLsFile = @"D:\Pfam\HH\CrcPdbHhAlign\crcnewls-seq.txt";
            hhrParser.UpdateHhAlignments(newLsFile, hhrFileDir);
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateMissingHhrFiles ()
        {
            string updateListFile = Path.Combine (hhrFileDir, "missingCrcList.txt");
  /*          StreamWriter lsCrcWriter = new StreamWriter(updateListFile);
            string queryString = "Select Distinct Crc From PdbCrcMap;";
            DataTable dbCrcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string crc = "";
            foreach (DataRow crcRow in dbCrcTable.Rows)
            {
                crc = crcRow["Crc"].ToString().TrimEnd();
                queryString = string.Format ("Select Distinct Query From PdbCrcHHAlignments Where Query = '{0}';", crc);
                DataTable alignCrcTable = ProtCidSettings.alignmentQuery.Query(queryString);
                if (alignCrcTable.Rows.Count > 0)
                {
                    continue;
                }
                else if (File.Exists (Path.Combine (hhrFileDir, crc + ".hhr.gz")))
                {
                    lsCrcWriter.WriteLine(crc);
                }
            }
            lsCrcWriter.Close();*/
            HhrFileParser hhrParser = new HhrFileParser();
            hhrParser.HHAlignTableName = hhAlignTableName;
            hhrParser.UpdateHhAlignments(updateListFile, hhrFileDir);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] DownloadHhrFileFromFourpaws ()
        {
            HhPsiblastDownload hhrDownload = new HhPsiblastDownload();
            string dataType = "crc";
            string fileType = "hhr.gz";
            string winDir = hhrFileDir; 
            bool isSubFolder = false;
            // return two files: new/update list file and those files are not in linux folder (up-to-date)
            string[] newObsLsFiles = hhrDownload.SynchronizeFilesFromLinux(winDir, linuxDir, dataType, fileType, isSubFolder);

            // due to the inconsistence between pisces and protcid, some crcs may not be deleted
 /*           string[] obsSeqCodes = ReadSequenceCodes (newObsLsFiles[1]);
            hhrDownload.DeleteObsoleteFiles(winDir, obsSeqCodes, isSubFolder);*/

            return newObsLsFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obsLsFile"></param>
        /// <returns></returns>
        private string[] ReadSequenceCodes (string obsLsFile)
        {
            List<string> seqCodeList = new List<string> ();
            StreamReader dataReader = new StreamReader(obsLsFile);
            string line = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                seqCodeList.Add(line);
            }
            dataReader.Close();
           
            return seqCodeList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crcHhFileDir"></param>
        private void DeleteCrcsNotInCrcPdbMapTable (string crcHhFileDir)
        {
            string[] crcsNotInDb = GetCrcsNotInDb(crcHhFileDir);
            string hhFile = "";
            foreach (string crc in crcsNotInDb)
            {
                hhFile = Path.Combine(crcHhFileDir, crc + ".hhr.gz");
                File.Delete(hhFile);
            }
            DeleteCrcHhAlignments(crcsNotInDb);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obsCrcs"></param>
        private void DeleteCrcHhAlignments (string[] obsCrcs)
        {
            DbUpdate dbDelete = new DbUpdate();
            string deleteString = "";
            for (int i = 0; i < obsCrcs.Length; i += 50)
            {
                string[] subCrcs = ParseHelper.GetSubArray(obsCrcs, i, 50);
                deleteString = string.Format("Delete From {0} Where Query IN ({1}) OR Hit IN ({1});", 
                    hhAlignTableName, ParseHelper.FormatSqlListString (subCrcs));
                dbDelete.Delete(ProtCidSettings.alignmentDbConnection, deleteString);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crcHhFileDir"></param>
        /// <returns></returns>
        private string[] GetCrcsNotInDb (string crcHhFileDir)
        {
            string queryString = "Select Distinct Crc From PdbCrcMap;";
            DataTable dbCrcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> crcList = new List<string>();
            foreach (DataRow crcRow in dbCrcTable.Rows)
            {
                crcList.Add(crcRow["Crc"].ToString().TrimEnd());
            }
            crcList.Sort();
            string[] hhAlignFiles = Directory.GetFiles(crcHhFileDir, "*.hhr*");
            string crc = "";
            List<string> crcNotInDbList = new List<string>();
            StreamWriter crcNotInDbWriter = new StreamWriter("CrcsNotInDb" + DateTime.Today.ToString("yyyyMMdd") + ".txt");
            foreach (string hhAlignFile in hhAlignFiles)
            {
                FileInfo fileInfo = new FileInfo (hhAlignFile);
                crc = fileInfo.Name.Substring(0, fileInfo.Name.IndexOf("."));
                if (crcList.BinarySearch(crc) > -1)
                {
                    continue;
                }
                crcNotInDbList.Add(crc);
                crcNotInDbWriter.WriteLine(crc);
            }
            crcNotInDbWriter.Close();
            return crcNotInDbList.ToArray();
        }
        #endregion

        #region query hh alignments
        /// <summary>
        /// retrieve hh alignments which are greater than the sequence identity cutoff and coverage cutoff
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
        public DataTable RetrieveHHAlignments(string pdbId1, string pdbId2)
        {
            crcPdbChainDict.Clear();
            string[] crcs1 = GetPdbCrcs(pdbId1, ref crcPdbChainDict);
            string[] crcs2 = GetPdbCrcs(pdbId2, ref crcPdbChainDict);
            string crc1 = "";
            string crc2 = "";
            double coverage = 0;
            string[] pdbChainEntity1 = null;
            string[] pdbChainEntity2 = null;
            DataTable hhAlignTable = InitializeHHAlignTable();

            if (crcs1.Length == 0 || crcs2.Length == 0)
            {
                ProtCidSettings.logWriter.WriteLine(pdbId1 + " #crc=" + crcs1.Length.ToString());
                ProtCidSettings.logWriter.WriteLine(pdbId2 + " #crc=" + crcs2.Length.ToString());
                ProtCidSettings.logWriter.Flush();
                return hhAlignTable;
            }

            string queryString = string.Format ("SELECT HitNo, Query, Hit, AlignLength, QueryLength, QueryStart, QueryEnd, QueryAlignment, " + 
                " HitLength, HitStart, HitEnd, HitAlignment, Identity " + 
                "FROM {0} Where Query In ({1}) AND Hit In ({2}) AND Identity > {3};",
                hhAlignTableName, ParseHelper.FormatSqlListString(crcs1), ParseHelper.FormatSqlListString(crcs2), seqIdCutoff);
            DataTable crcAlignTable = ProtCidSettings.alignmentQuery.Query(queryString);

            queryString = string.Format("SELECT HitNo, Query, Hit, AlignLength, QueryLength, QueryStart, QueryEnd, QueryAlignment, " +
                " HitLength, HitStart, HitEnd, HitAlignment, Identity " + 
               "FROM {0} Where Query In ({1}) AND Hit In ({2}) AND Identity > {3};", 
               hhAlignTableName, ParseHelper.FormatSqlListString(crcs2), ParseHelper.FormatSqlListString(crcs1), seqIdCutoff);
            DataTable reverseCrcAlignTable = ProtCidSettings.alignmentQuery.Query(queryString);

            foreach (DataRow reverseRow in reverseCrcAlignTable.Rows)
            {
                crc1 = reverseRow["Query"].ToString ();
                crc2 = reverseRow["Hit"].ToString ();
                DataRow[] crcAlignRows = crcAlignTable.Select (string.Format ("Query = '{0}' AND Hit = '{1}'", crc2, crc1));
                // alignment exist in crcAlignTable
                if (crcAlignRows.Length > 0)
                {
                    continue;
                }
                DataRow crcAlignRow = crcAlignTable.NewRow ();
                crcAlignRow.ItemArray = reverseRow.ItemArray;
                ReverseQueryHitAlignInfo (crcAlignRow);
                crcAlignTable.Rows.Add (crcAlignRow);
            }

            // replace crc by pdb codes
            List<string> addedPdbChainPairList = new List<string> ();
            string entityChainPair = "";
            foreach (DataRow crcAlignRow in crcAlignTable.Rows)
            {
                crc1 = crcAlignRow["Query"].ToString();
                crc2 = crcAlignRow["Hit"].ToString();

                coverage = GetMaximumCoverage(Convert.ToDouble(crcAlignRow["AlignLength"]), Convert.ToDouble(crcAlignRow["QueryLength"]), Convert.ToDouble(crcAlignRow["HitLength"]));
                // coverage > coverage cutoff
                if (coverage < coverageCutoff)
                {
                    continue;
                }

                addedPdbChainPairList.Clear();

                List<string> pdbChainList1 = crcPdbChainDict[crc1];
                List<string> pdbChainList2 = crcPdbChainDict[crc2];
                foreach (string pdbChain1 in pdbChainList1)
                {
                    pdbChainEntity1 = pdbChain1.Split(',');
                    foreach (string pdbChain2 in pdbChainList2)
                    {
                        if (pdbChain1 == pdbChain2)
                        {
                            continue;
                        }
                        pdbChainEntity2 = pdbChain2.Split(',');
                        entityChainPair = pdbChain1 + "_" + pdbChain2;
                        if (string.Compare (pdbChain1, pdbChain2) > 0)
                        {
                            entityChainPair = pdbChain2 + "_" + pdbChain1;
                        }
                        if (addedPdbChainPairList.Contains (entityChainPair))
                        {
                            continue;
                        }
                        addedPdbChainPairList.Add(entityChainPair);
                        if (pdbChainEntity1[0] == pdbId1 && pdbChainEntity2[0] == pdbId2)
                        {
                            DataRow hhalignRow = hhAlignTable.NewRow();
                            hhalignRow["QueryEntry"] = pdbChainEntity1[0];
                            hhalignRow["QueryChain"] = pdbChainEntity1[1];
                            hhalignRow["QueryEntity"] = pdbChainEntity1[2];
                            hhalignRow["HitEntry"] = pdbChainEntity2[0];
                            hhalignRow["HitChain"] = pdbChainEntity2[1];
                            hhalignRow["HitEntity"] = pdbChainEntity2[2];
                            hhalignRow["QueryLength"] = crcAlignRow["QueryLength"];
                            hhalignRow["QueryStart"] = crcAlignRow["QueryStart"];
                            hhalignRow["QueryEnd"] = crcAlignRow["QueryEnd"];
                            hhalignRow["QuerySequence"] = crcAlignRow["QueryAlignment"];
                            hhalignRow["HitLength"] = crcAlignRow["HitLength"];
                            hhalignRow["HitStart"] = crcAlignRow["HitStart"];
                            hhalignRow["HitEnd"] = crcAlignRow["HitEnd"];
                            hhalignRow["HitSequence"] = crcAlignRow["HitAlignment"];
                            hhalignRow["Identity"] = crcAlignRow["Identity"];
                            hhAlignTable.Rows.Add(hhalignRow);
                        }
                        else if (pdbChainEntity1[0] == pdbId2 && pdbChainEntity2[0] == pdbId1)
                        {
                            DataRow hhalignRow = hhAlignTable.NewRow();
                            hhalignRow["QueryEntry"] = pdbChainEntity2[0];
                            hhalignRow["QueryChain"] = pdbChainEntity2[1];
                            hhalignRow["QueryEntity"] = pdbChainEntity2[2];
                            hhalignRow["HitEntry"] = pdbChainEntity1[0];
                            hhalignRow["HitChain"] = pdbChainEntity1[1];
                            hhalignRow["HitEntity"] = pdbChainEntity1[2];
                            hhalignRow["QueryLength"] = crcAlignRow["HitLength"];
                            hhalignRow["QueryStart"] = crcAlignRow["HitStart"];
                            hhalignRow["QueryEnd"] = crcAlignRow["HitEnd"];
                            hhalignRow["QuerySequence"] = crcAlignRow["HitAlignment"];
                            hhalignRow["HitLength"] = crcAlignRow["QueryLength"];
                            hhalignRow["HitStart"] = crcAlignRow["QueryStart"];
                            hhalignRow["HitEnd"] = crcAlignRow["QueryEnd"];
                            hhalignRow["HitSequence"] = crcAlignRow["QueryAlignment"];
                            hhalignRow["Identity"] = crcAlignRow["Identity"];
                            hhAlignTable.Rows.Add(hhalignRow);
                        }
                    }
                }
            }

            return hhAlignTable;
        }

        /// <summary>
        /// retrieve hh alignments which are greater than the sequence identity cutoff and coverage cutoff
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
        public DataTable RetrieveHHAlignments(string pdbId1, string  chain1, string pdbId2, string chain2)
        {
            int entityId1 = -1;
            int entityId2 = -1;
            string crc1 = GetPdbChainCrc (pdbId1, chain1, out entityId1);
            string crc2 = GetPdbChainCrc (pdbId2, chain2, out entityId2);
            double coverage = 0;
            DataTable hhAlignTable = InitializeHHAlignTable();

            string queryString = string.Format("SELECT HitNo, Query, Hit, AlignLength, QueryLength, QueryStart, QueryEnd, QueryAlignment, " + 
                " HitLength, HitStart, HitEnd, HitAlignment, Identity " +
                "FROM {0} Where query = '{1}' AND Hit = '{2}' AND identity > {3};", hhAlignTableName, crc1, crc2, seqIdCutoff);
            DataTable crcAlignTable = ProtCidSettings.alignmentQuery.Query(queryString);

            if (crcAlignTable.Rows.Count == 0)
            {
                queryString = string.Format("SELECT HitNo, Query, Hit, AlignLength, QueryLength, QueryStart, QueryEnd, QueryAlignment, " +
                " HitLength, HitStart, HitEnd, HitAlignment, Identity " +
                "FROM {0} Where query = '{1}' AND Hit = '{2}' AND identity > {3};", hhAlignTableName, crc2, crc1, seqIdCutoff);
                DataTable reverseCrcAlignTable = ProtCidSettings.alignmentQuery.Query(queryString);

                foreach (DataRow reverseRow in reverseCrcAlignTable.Rows)
                {
                    crc1 = reverseRow["Query"].ToString();
                    crc2 = reverseRow["Hit"].ToString();
                    DataRow[] crcAlignRows = crcAlignTable.Select(string.Format("Query = '{0}' AND Hit = '{1}'", crc2, crc1));
                    // alignment exist in crcAlignTable
                    if (crcAlignRows.Length > 0)
                    {
                        continue;
                    }
                    DataRow crcAlignRow = crcAlignTable.NewRow();
                    crcAlignRow.ItemArray = reverseRow.ItemArray;
                    ReverseQueryHitAlignInfo(crcAlignRow);
                    crcAlignTable.Rows.Add(crcAlignRow);
                }
            }

            // replace crc by pdb codes
            foreach (DataRow crcAlignRow in crcAlignTable.Rows)
            {
                crc1 = crcAlignRow["Query"].ToString();
                crc2 = crcAlignRow["Hit"].ToString();

                coverage = GetMaximumCoverage(Convert.ToDouble(crcAlignRow["AlignLength"]), Convert.ToDouble(crcAlignRow["QueryLength"]), Convert.ToDouble(crcAlignRow["HitLength"]));
                // coverage > coverage cutoff
                if (coverage < coverageCutoff)
                {
                    continue;
                }

                DataRow hhalignRow = hhAlignTable.NewRow();
                hhalignRow["QueryEntry"] = pdbId1;
                hhalignRow["QueryChain"] = chain1;
                hhalignRow["QueryEntity"] = entityId1;
                hhalignRow["HitEntry"] = pdbId2;
                hhalignRow["HitChain"] = chain2;
                hhalignRow["HitEntity"] = entityId2;
                hhalignRow["QueryLength"] = crcAlignRow["QueryLength"];
                hhalignRow["QueryStart"] = crcAlignRow["QueryStart"];
                hhalignRow["QueryEnd"] = crcAlignRow["QueryEnd"];
                hhalignRow["QuerySequence"] = crcAlignRow["QueryAlignment"];
                hhalignRow["HitLength"] = crcAlignRow["HitLength"];
                hhalignRow["HitStart"] = crcAlignRow["HitStart"];
                hhalignRow["HitEnd"] = crcAlignRow["HitEnd"];
                hhalignRow["HitSequence"] = crcAlignRow["HitAlignment"];
                hhalignRow["Identity"] = crcAlignRow["Identity"];
                hhAlignTable.Rows.Add(hhalignRow);
            }

            return hhAlignTable;
        }

        /// <summary>
        /// retrieve all HH alignments in pdbcrchhalignments table
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
        public DataTable RetrieveAllHHAlignments(string pdbId1, string pdbId2)
        {
            crcPdbChainDict.Clear();
            string[] crcs1 = GetPdbCrcs(pdbId1, ref crcPdbChainDict);
            string[] crcs2 = GetPdbCrcs(pdbId2, ref crcPdbChainDict);
            string crc1 = "";
            string crc2 = "";
            string[] pdbChainEntity1 = null;
            string[] pdbChainEntity2 = null;
            DataTable hhAlignTable = InitializeHHAlignTable();

            if (crcs1.Length == 0 || crcs2.Length == 0)
            {
                ProtCidSettings.logWriter.WriteLine(pdbId1 + " #crc=" + crcs1.Length.ToString());
                ProtCidSettings.logWriter.WriteLine(pdbId2 + " #crc=" + crcs2.Length.ToString());
                ProtCidSettings.logWriter.Flush();
                return hhAlignTable;
            }

            string queryString = string.Format("SELECT HitNo, Query, Hit, AlignLength, QueryLength, QueryStart, QueryEnd, QueryAlignment, " +
                " HitLength, HitStart, HitEnd, HitAlignment, Identity " +
                "FROM {0} Where Query In ({1}) AND Hit In ({2});",
                hhAlignTableName, ParseHelper.FormatSqlListString(crcs1), ParseHelper.FormatSqlListString(crcs2));
            DataTable crcAlignTable = ProtCidSettings.alignmentQuery.Query(queryString);

            queryString = string.Format("SELECT HitNo, Query, Hit, AlignLength, QueryLength, QueryStart, QueryEnd, QueryAlignment, " +
                " HitLength, HitStart, HitEnd, HitAlignment, Identity " +
               "FROM {0} Where Query In ({1}) AND Hit In ({2});",
               hhAlignTableName, ParseHelper.FormatSqlListString(crcs2), ParseHelper.FormatSqlListString(crcs1));
            DataTable reverseCrcAlignTable = ProtCidSettings.alignmentQuery.Query(queryString);

            foreach (DataRow reverseRow in reverseCrcAlignTable.Rows)
            {
                crc1 = reverseRow["Query"].ToString();
                crc2 = reverseRow["Hit"].ToString();
                DataRow[] crcAlignRows = crcAlignTable.Select(string.Format("Query = '{0}' AND Hit = '{1}'", crc2, crc1));
                // alignment exist in crcAlignTable
                if (crcAlignRows.Length > 0)
                {
                    continue;
                }
                DataRow crcAlignRow = crcAlignTable.NewRow();
                crcAlignRow.ItemArray = reverseRow.ItemArray;
                ReverseQueryHitAlignInfo(crcAlignRow);
                crcAlignTable.Rows.Add(crcAlignRow);
            }

            // replace crc by pdb codes
            List<string> addedPdbChainPairList = new List<string> ();
            string entityChainPair = "";
            foreach (DataRow crcAlignRow in crcAlignTable.Rows)
            {
                crc1 = crcAlignRow["Query"].ToString();
                crc2 = crcAlignRow["Hit"].ToString();
             
                addedPdbChainPairList.Clear();

                List<string> pdbChainList1 = crcPdbChainDict[crc1];
                List<string> pdbChainList2 = crcPdbChainDict[crc2];
                foreach (string pdbChain1 in pdbChainList1)
                {
                    pdbChainEntity1 = pdbChain1.Split(',');
                    foreach (string pdbChain2 in pdbChainList2)
                    {
                        if (pdbChain1 == pdbChain2)
                        {
                            continue;
                        }
                        pdbChainEntity2 = pdbChain2.Split(',');
                        entityChainPair = pdbChain1 + "_" + pdbChain2;
                        if (string.Compare(pdbChain1, pdbChain2) > 0)
                        {
                            entityChainPair = pdbChain2 + "_" + pdbChain1;
                        }
                        if (addedPdbChainPairList.Contains(entityChainPair))
                        {
                            continue;
                        }
                        addedPdbChainPairList.Add(entityChainPair);
                        if (pdbChainEntity1[0] == pdbId1 && pdbChainEntity2[0] == pdbId2)
                        {
                            DataRow hhalignRow = hhAlignTable.NewRow();
                            hhalignRow["QueryEntry"] = pdbChainEntity1[0];
                            hhalignRow["QueryChain"] = pdbChainEntity1[1]; // authorchain
                            hhalignRow["QueryEntity"] = pdbChainEntity1[2];
                            hhalignRow["HitEntry"] = pdbChainEntity2[0];
                            hhalignRow["HitChain"] = pdbChainEntity2[1];
                            hhalignRow["HitEntity"] = pdbChainEntity2[2];
                            hhalignRow["QueryLength"] = crcAlignRow["QueryLength"];
                            hhalignRow["QueryStart"] = crcAlignRow["QueryStart"];
                            hhalignRow["QueryEnd"] = crcAlignRow["QueryEnd"];
                            hhalignRow["QuerySequence"] = crcAlignRow["QueryAlignment"];
                            hhalignRow["HitLength"] = crcAlignRow["HitLength"];
                            hhalignRow["HitStart"] = crcAlignRow["HitStart"];
                            hhalignRow["HitEnd"] = crcAlignRow["HitEnd"];
                            hhalignRow["HitSequence"] = crcAlignRow["HitAlignment"];
                            hhalignRow["Identity"] = crcAlignRow["Identity"];
                            hhAlignTable.Rows.Add(hhalignRow);
                        }
                        else if (pdbChainEntity1[0] == pdbId2 && pdbChainEntity2[0] == pdbId1)
                        {
                            DataRow hhalignRow = hhAlignTable.NewRow();
                            hhalignRow["QueryEntry"] = pdbChainEntity2[0];
                            hhalignRow["QueryChain"] = pdbChainEntity2[1];
                            hhalignRow["QueryEntity"] = pdbChainEntity2[2];
                            hhalignRow["HitEntry"] = pdbChainEntity1[0];
                            hhalignRow["HitChain"] = pdbChainEntity1[1];
                            hhalignRow["HitEntity"] = pdbChainEntity1[2];
                            hhalignRow["QueryLength"] = crcAlignRow["HitLength"];
                            hhalignRow["QueryStart"] = crcAlignRow["HitStart"];
                            hhalignRow["QueryEnd"] = crcAlignRow["HitEnd"];
                            hhalignRow["QuerySequence"] = crcAlignRow["HitAlignment"];
                            hhalignRow["HitLength"] = crcAlignRow["QueryLength"];
                            hhalignRow["HitStart"] = crcAlignRow["QueryStart"];
                            hhalignRow["HitEnd"] = crcAlignRow["QueryEnd"];
                            hhalignRow["HitSequence"] = crcAlignRow["QueryAlignment"];
                            hhalignRow["Identity"] = crcAlignRow["Identity"];
                            hhAlignTable.Rows.Add(hhalignRow);
                        }
                    }
                }
            }

            return hhAlignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
        public DataTable RetrieveAllHHAlignments(string pdbId1, string chain1, string pdbId2, string chain2)
        {
            int entityId1 = -1;
            int entityId2 = -1;
            string crc1 = GetPdbChainCrc(pdbId1, chain1, out entityId1);
            string crc2 = GetPdbChainCrc(pdbId2, chain2, out entityId2);
            DataTable hhAlignTable = InitializeHHAlignTable();

            string queryString = string.Format("SELECT HitNo, Query, Hit, AlignLength, QueryLength, QueryStart, QueryEnd, QueryAlignment, " +
                " HitLength, HitStart, HitEnd, HitAlignment, Identity " +
                "FROM {0} Where query = '{1}' AND Hit = '{2}';", hhAlignTableName, crc1, crc2);
            DataTable crcAlignTable = ProtCidSettings.alignmentQuery.Query(queryString);

            if (crcAlignTable.Rows.Count == 0)
            {
                queryString = string.Format("SELECT HitNo, Query, Hit, AlignLength, QueryLength, QueryStart, QueryEnd, QueryAlignment, " +
                " HitLength, HitStart, HitEnd, HitAlignment, Identity " +
                "FROM {0} Where query = '{1}' AND Hit = '{2}';", hhAlignTableName, crc2, crc1);
                DataTable reverseCrcAlignTable = ProtCidSettings.alignmentQuery.Query(queryString);

                foreach (DataRow reverseRow in reverseCrcAlignTable.Rows)
                {
                    crc1 = reverseRow["Query"].ToString();
                    crc2 = reverseRow["Hit"].ToString();
                    DataRow[] crcAlignRows = crcAlignTable.Select(string.Format("Query = '{0}' AND Hit = '{1}'", crc2, crc1));
                    // alignment exist in crcAlignTable
                    if (crcAlignRows.Length > 0)
                    {
                        continue;
                    }
                    DataRow crcAlignRow = crcAlignTable.NewRow();
                    crcAlignRow.ItemArray = reverseRow.ItemArray;
                    ReverseQueryHitAlignInfo(crcAlignRow);
                    crcAlignTable.Rows.Add(crcAlignRow);
                }
            }

            // replace crc by pdb codes
            foreach (DataRow crcAlignRow in crcAlignTable.Rows)
            {
                crc1 = crcAlignRow["Query"].ToString();
                crc2 = crcAlignRow["Hit"].ToString();

                DataRow hhalignRow = hhAlignTable.NewRow();
                hhalignRow["QueryEntry"] = pdbId1;
                hhalignRow["QueryChain"] = chain1;
                hhalignRow["QueryEntity"] = entityId1;
                hhalignRow["HitEntry"] = pdbId2;
                hhalignRow["HitChain"] = chain2;
                hhalignRow["HitEntity"] = entityId2;
                hhalignRow["QueryLength"] = crcAlignRow["QueryLength"];
                hhalignRow["QueryStart"] = crcAlignRow["QueryStart"];
                hhalignRow["QueryEnd"] = crcAlignRow["QueryEnd"];
                hhalignRow["QuerySequence"] = crcAlignRow["QueryAlignment"];
                hhalignRow["HitLength"] = crcAlignRow["HitLength"];
                hhalignRow["HitStart"] = crcAlignRow["HitStart"];
                hhalignRow["HitEnd"] = crcAlignRow["HitEnd"];
                hhalignRow["HitSequence"] = crcAlignRow["HitAlignment"];
                hhalignRow["Identity"] = crcAlignRow["Identity"];
                hhAlignTable.Rows.Add(hhalignRow);
            }

            return hhAlignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
        public DataTable RetrieveAllHHAlignments(string pdbId1, int entityId1, string pdbId2, int entityId2)
        {
            string chain1 = "";
            string chain2 = "";
            string crc1 = GetPdbChainCrc(pdbId1, entityId1, out chain1);
            string crc2 = GetPdbChainCrc(pdbId2, entityId2, out chain2);
            DataTable hhAlignTable = InitializeHHAlignTable();

            string queryString = string.Format("SELECT HitNo, Query, Hit, AlignLength, QueryLength, QueryStart, QueryEnd, QueryAlignment, " +
                " HitLength, HitStart, HitEnd, HitAlignment, Identity " +
                "FROM {0} Where query = '{1}' AND Hit = '{2}';", hhAlignTableName, crc1, crc2);
            DataTable crcAlignTable = ProtCidSettings.alignmentQuery.Query(queryString);

            if (crcAlignTable.Rows.Count == 0)
            {
                queryString = string.Format("SELECT HitNo, Query, Hit, AlignLength, QueryLength, QueryStart, QueryEnd, QueryAlignment, " +
                " HitLength, HitStart, HitEnd, HitAlignment, Identity " +
                "FROM {0} Where query = '{1}' AND Hit = '{2}';", hhAlignTableName, crc2, crc1);
                DataTable reverseCrcAlignTable = ProtCidSettings.alignmentQuery.Query(queryString);

                foreach (DataRow reverseRow in reverseCrcAlignTable.Rows)
                {
                    crc1 = reverseRow["Query"].ToString();
                    crc2 = reverseRow["Hit"].ToString();
                    DataRow[] crcAlignRows = crcAlignTable.Select(string.Format("Query = '{0}' AND Hit = '{1}'", crc2, crc1));
                    // alignment exist in crcAlignTable
                    if (crcAlignRows.Length > 0)
                    {
                        continue;
                    }
                    DataRow crcAlignRow = crcAlignTable.NewRow();
                    crcAlignRow.ItemArray = reverseRow.ItemArray;
                    ReverseQueryHitAlignInfo(crcAlignRow);
                    crcAlignTable.Rows.Add(crcAlignRow);
                }
            }

            // replace crc by pdb codes
            foreach (DataRow crcAlignRow in crcAlignTable.Rows)
            {
                crc1 = crcAlignRow["Query"].ToString();
                crc2 = crcAlignRow["Hit"].ToString();

                DataRow hhalignRow = hhAlignTable.NewRow();
                hhalignRow["QueryEntry"] = pdbId1;
                hhalignRow["QueryChain"] = chain1;
                hhalignRow["QueryEntity"] = entityId1;
                hhalignRow["HitEntry"] = pdbId2;
                hhalignRow["HitChain"] = chain2;
                hhalignRow["HitEntity"] = entityId2;
                hhalignRow["QueryLength"] = crcAlignRow["QueryLength"];
                hhalignRow["QueryStart"] = crcAlignRow["QueryStart"];
                hhalignRow["QueryEnd"] = crcAlignRow["QueryEnd"];
                hhalignRow["QuerySequence"] = crcAlignRow["QueryAlignment"];
                hhalignRow["HitLength"] = crcAlignRow["HitLength"];
                hhalignRow["HitStart"] = crcAlignRow["HitStart"];
                hhalignRow["HitEnd"] = crcAlignRow["HitEnd"];
                hhalignRow["HitSequence"] = crcAlignRow["HitAlignment"];
                hhalignRow["Identity"] = crcAlignRow["Identity"];
                hhAlignTable.Rows.Add(hhalignRow);
            }

            return hhAlignTable;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable InitializeHHAlignTable()
        {
            DataTable hhalignTable = new DataTable();
            foreach (string tableCol in alignColumns)
            {
                hhalignTable.Columns.Add(new DataColumn (tableCol));
            }
            return hhalignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hhalignRow"></param>
        private void ReverseQueryHitAlignInfo(DataRow hhalignRow)
        {
            object temp = null;
            temp = hhalignRow["Query"];
            hhalignRow["Query"] = hhalignRow["Hit"];
            hhalignRow["Hit"] = temp;

            temp = hhalignRow["QueryLength"];
            hhalignRow["QueryLength"] = hhalignRow["HitLength"];
            hhalignRow["HitLength"] = temp;

            temp = hhalignRow["QueryStart"];
            hhalignRow["QueryStart"] = hhalignRow["HitStart"];
            hhalignRow["HitStart"] = temp;

            temp = hhalignRow["QueryEnd"];
            hhalignRow["QueryEnd"] = hhalignRow["HitEnd"];
            hhalignRow["HitEnd"] = temp;

            temp = hhalignRow["QueryAlignment"];
            hhalignRow["QueryAlignment"] = hhalignRow["HitAlignment"];
            hhalignRow["HitAlignment"] = temp;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignLen"></param>
        /// <param name="queryLen"></param>
        /// <param name="hitLen"></param>
        /// <returns></returns>
        private double GetMaximumCoverage(int alignLen, int queryLen, int hitLen)
        {
            double qcov = (double)alignLen / (double)queryLen;
            double hcov = (double)alignLen / (double)hitLen;
            return Math.Max(qcov, hcov);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignLen"></param>
        /// <param name="queryLen"></param>
        /// <param name="hitLen"></param>
        /// <returns></returns>
        private double GetMaximumCoverage(double alignLen, double queryLen, double hitLen)
        {
            double qcov = alignLen / queryLen;
            double hcov = alignLen / hitLen;
            return Math.Max(qcov, hcov);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetPdbCrcs(string pdbId, ref Dictionary<string, List<string>> crcPdbchainDict)
        {
            DataRow[] crcRows = GetPdbEntryCrcRows(pdbId);
            if (crcRows.Length == 0)
            {
                crcRows = CalculatePdbSeqCrcCodes(pdbId); // compute crc64 for the entry
            }
            List<string> crcCodeList = new List<string> ();
            string crc = "";
            string entryChain = "";
            foreach (DataRow crcRow in crcRows)
            {
                crc = crcRow["crc"].ToString();
                if (!crcCodeList.Contains(crc))
                {
                    crcCodeList.Add(crc);
                }
                entryChain = crcRow["PdbID"].ToString().TrimEnd() + "," + crcRow["AuthorChain"].ToString().TrimEnd() + "," + crcRow["EntityID"].ToString().TrimEnd();
                if (crcPdbchainDict.ContainsKey(crc))
                {
                    List<string> chainList = crcPdbchainDict[crc];
                    chainList.Add(entryChain);
                }
                else
                {
                    List<string> chainList = new List<string> ();
                    chainList.Add(entryChain);
                    crcPdbchainDict.Add(crc, chainList);
                }
            }

            return crcCodeList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetPdbEntryCrcs (string pdbId)
        {
            DataRow[] crcRows = null;
            if (pdbCrcMapTable != null)
            {
                crcRows = pdbCrcMapTable.Select(string.Format("PdbId = '{0}'", pdbId));
            }
            else
            {
                string queryString = string.Format("Select Distinct Crc From {0} Where PdbID = '{1}' ;", pdbCrcMapTableName, pdbId);
                DataTable crcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                crcRows = crcTable.Select();
            }         
            string[] entryCrcs = new string[crcRows.Length];
            int count = 0;
            foreach (DataRow crcRow in crcRows)
            {
                entryCrcs[count] = crcRow["crc"].ToString ().TrimEnd ();
                count++;
            }
            return entryCrcs;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataRow[] GetPdbEntryCrcRows (string pdbId)
        {
            DataRow[] crcRows = null;
            if (pdbCrcMapTable != null)
            {
                crcRows = pdbCrcMapTable.Select(string.Format("PdbId = '{0}'", pdbId));
            }
            else
            {
                string queryString = string.Format("Select Distinct Crc, PdbId, AuthorChain, EntityID From {0} Where PdbID = '{1}' ;", pdbCrcMapTableName, pdbId);
                DataTable crcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                crcRows = crcTable.Select();              
            }

            return crcRows;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authorChain"></param>
        /// <returns></returns>
        public string GetPdbChainCrc(string pdbId, string authorChain)
        {
            string crc = "";
            if (pdbCrcMapTable != null)
            {
                DataRow[] crcRows = pdbCrcMapTable.Select(string.Format("PdbID = '{0}' AND AuthorCHain = '{1}'", pdbId, authorChain));
                if (crcRows.Length > 0)
                {
                    crc = crcRows[0]["crc"].ToString().Trim();
                }
            }
            else
            {
                string queryString = string.Format("Select Crc, EntityID From {0} Where PdbID = '{1}' AND AuthorCHain = '{2}';", pdbCrcMapTableName, pdbId, authorChain);
                DataTable crcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                if (crcTable.Rows.Count > 0)
                {
                    crc = crcTable.Rows[0]["crc"].ToString().Trim();
                }

            }
            if (crc == "")
            {
                crc = CalculatePdbChainSeqCrcCode(pdbId, authorChain);
            }
            return crc;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authorChain"></param>
        /// <returns></returns>
        public string GetPdbChainCrc(string pdbId, string authorChain, out int entityId)
        {
            string crc = "";
            entityId = -1;
            if (pdbCrcMapTable != null)
            {
                DataRow[] crcRows = pdbCrcMapTable.Select(string.Format("PdbID = '{0}' AND AuthorCHain = '{1}'", pdbId, authorChain));
                if (crcRows.Length > 0)
                {
                    crc = crcRows[0]["crc"].ToString().Trim();
                    entityId = Convert.ToInt32(crcRows[0]["EntityId"].ToString());
                }
            }
            else
            {
                string queryString = string.Format("Select Crc, EntityID From {0} Where PdbID = '{1}' AND AuthorCHain = '{2}';", pdbCrcMapTableName, pdbId, authorChain);
                DataTable crcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                
                if (crcTable.Rows.Count > 0)
                {
                    crc = crcTable.Rows[0]["crc"].ToString().Trim();
                    entityId = Convert.ToInt32(crcTable.Rows[0]["EntityID"].ToString());
                }
            }
            if (crc == "")
            {
                crc = CalculatePdbChainSeqCrcCode(pdbId, authorChain, out entityId);
            }
            return crc;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authorChain"></param>
        /// <returns></returns>
        public string GetPdbChainCrc(string pdbId, int entityId, out string authorChain)
        {
            authorChain = "";
            string crc = "";
            if (pdbCrcMapTable != null)
            {
                DataRow[] crcRows = pdbCrcMapTable.Select(string.Format("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId));
                if (crcRows.Length > 0)
                {
                    crc = crcRows[0]["crc"].ToString().Trim();
                    authorChain = crcRows[0]["AuthorChain"].ToString();
                }
            }
            else
            {
                string queryString = string.Format("Select Crc, AuthorChain From {0} Where PdbID = '{1}' AND EntityID = '{2}';", pdbCrcMapTableName, pdbId, entityId);
                DataTable crcTable = ProtCidSettings.pdbfamQuery.Query(queryString);

                if (crcTable.Rows.Count > 0)
                {
                    crc = crcTable.Rows[0]["crc"].ToString().Trim();
                    authorChain = crcTable.Rows[0]["AuthorChain"].ToString();
                }
            }
            if (crc == "")
            {
                crc = CalculatePdbChainSeqCrcCode(pdbId, entityId, out authorChain);
            }
            return crc;
        }
        #endregion

        #region crc alignments
        /// <summary>
        /// 
        /// </summary>
        /// <param name="crcs"></param>
        /// <returns></returns>
        public DataTable GetCrcAlignTable (string[] crcs)
        {
            DataTable hhAlignTable = InitializeHHAlignTable();
            string crcSqlListString = ParseHelper.FormatSqlListString(crcs);
            string queryString = string.Format("SELECT HitNo, Query, Hit, AlignLength, QueryLength, QueryStart, QueryEnd, QueryAlignment, " +
                " HitLength, HitStart, HitEnd, HitAlignment, Identity " +
                "FROM {0} Where query IN ({1}) AND Hit IN ({2});", hhAlignTableName, crcSqlListString, crcSqlListString);
            DataTable crcAlignTable = ProtCidSettings.alignmentQuery.Query(queryString);
            return crcAlignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crcs"></param>
        /// <returns></returns>
        public DataTable GetCrcAlignTable(string[] crcs1, string[] crcs2)
        {
            DataTable hhAlignTable = InitializeHHAlignTable();
            string queryString = string.Format("SELECT HitNo, Query, Hit, AlignLength, QueryLength, QueryStart, QueryEnd, QueryAlignment, " +
                " HitLength, HitStart, HitEnd, HitAlignment, Identity " +
                "FROM {0} Where query IN ({1}) AND Hit IN ({2});", hhAlignTableName, 
                ParseHelper.FormatSqlListString (crcs1), ParseHelper.FormatSqlListString (crcs2));
            DataTable crcAlignTable = ProtCidSettings.alignmentQuery.Query(queryString);
            return crcAlignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crc"></param>
        /// <param name="entityCrcMapTable"></param>
        /// <returns></returns>
        private string[] GetCrcPdbEntities(string crc, DataTable entityCrcMapTable)
        {
            DataRow[] entityRows = entityCrcMapTable.Select(string.Format ("crc = '{0}'", crc));
            string[] crcEntities = new string[entityRows.Length];
            int count = 0;
            foreach (DataRow entityRow in entityRows )
            {
                crcEntities[count] = entityRow["PdbID"].ToString() + entityRow["EntityID"].ToString();
                count++;
            }
            return crcEntities;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crcHHAlignTable"></param>
        /// <param name="entityCrcMapTable"></param>
        public DataTable ChangeCrcAlignToEntryAlign(DataTable crcHHAlignTable, DataTable entityCrcMapTable)
        {
            DataTable hhAlignTable = InitializeHHAlignTable();
            string crc1 = "";
            string crc2 = "";
            foreach (DataRow crcAlignRow in crcHHAlignTable.Rows)
            {
                crc1 = crcAlignRow["Query"].ToString();
                crc2 = crcAlignRow["Hit"].ToString();
                DataRow[] entityRows1 = entityCrcMapTable.Select(string.Format("crc = '{0}'", crc1));
                DataRow[] entityRows2 = entityCrcMapTable.Select(string.Format("crc = '{0}'", crc2));

               foreach (DataRow entityRow1 in entityRows1)
               {
                   foreach (DataRow entityRow2 in entityRows2)
                   {
                       if (entityRow1["PdbID"] == entityRow2["PdbID"])
                       {
                           continue;
                       }
                        DataRow hhalignRow = hhAlignTable.NewRow();
                        hhalignRow["QueryEntry"] = entityRow1["PdbID"];
                        hhalignRow["QueryChain"] = entityRow1["AuthorChain"];
                        hhalignRow["QueryEntity"] = entityRow1["EntityID"];
                        hhalignRow["HitEntry"] = entityRow2["PdbID"];
                        hhalignRow["HitChain"] = entityRow2["AuthorChain"];
                        hhalignRow["HitEntity"] = entityRow2["EntityID"];
                        hhalignRow["QueryLength"] = crcAlignRow["QueryLength"];
                        hhalignRow["QueryStart"] = crcAlignRow["QueryStart"];
                        hhalignRow["QueryEnd"] = crcAlignRow["QueryEnd"];
                        hhalignRow["QuerySequence"] = crcAlignRow["QueryAlignment"];
                        hhalignRow["HitLength"] = crcAlignRow["HitLength"];
                        hhalignRow["HitStart"] = crcAlignRow["HitStart"];
                        hhalignRow["HitEnd"] = crcAlignRow["HitEnd"];
                        hhalignRow["HitSequence"] = crcAlignRow["HitAlignment"];
                        hhalignRow["Identity"] = crcAlignRow["Identity"];
                        hhAlignTable.Rows.Add(hhalignRow);
                    }
                }
            }
            return hhAlignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crcHHAlignTable"></param>
        /// <param name="entityCrcMapTable"></param>
        public DataTable ChangeCrcAlignToEntryAlign(DataTable crcHHAlignTable, string[] entryPairsToBeAligned, DataTable entityCrcMapTable)
        {
            DataTable hhAlignTable = InitializeHHAlignTable();
            string crc1 = "";
            string crc2 = "";
            string entryPair = "";
            string reversedEntryPair = "";
            foreach (DataRow crcAlignRow in crcHHAlignTable.Rows)
            {
                crc1 = crcAlignRow["Query"].ToString();
                crc2 = crcAlignRow["Hit"].ToString();
                DataRow[] entityRows1 = entityCrcMapTable.Select(string.Format("crc = '{0}'", crc1));
                DataRow[] entityRows2 = entityCrcMapTable.Select(string.Format("crc = '{0}'", crc2));

                foreach (DataRow entityRow1 in entityRows1)
                {
                    foreach (DataRow entityRow2 in entityRows2)
                    {
                        if (entityRow1["PdbID"] == entityRow2["PdbID"])
                        {
                            continue;
                        }
                        entryPair = entityRow1["PdbID"].ToString() + entityRow2["PdbID"].ToString();
                        reversedEntryPair = entityRow2["PdbID"].ToString() + entityRow1["PdbID"].ToString();
                        if (Array.IndexOf (entryPairsToBeAligned, entryPair) > -1)
                        {
                            DataRow hhalignRow = hhAlignTable.NewRow();
                            hhalignRow["QueryEntry"] = entityRow1["PdbID"];
                            hhalignRow["QueryChain"] = entityRow1["AuthorChain"];
                            hhalignRow["QueryEntity"] = entityRow1["EntityID"];
                            hhalignRow["HitEntry"] = entityRow2["PdbID"];
                            hhalignRow["HitChain"] = entityRow2["AuthorChain"];
                            hhalignRow["HitEntity"] = entityRow2["EntityID"];
                            hhalignRow["QueryLength"] = crcAlignRow["QueryLength"];
                            hhalignRow["QueryStart"] = crcAlignRow["QueryStart"];
                            hhalignRow["QueryEnd"] = crcAlignRow["QueryEnd"];
                            hhalignRow["QuerySequence"] = crcAlignRow["QueryAlignment"];
                            hhalignRow["HitLength"] = crcAlignRow["HitLength"];
                            hhalignRow["HitStart"] = crcAlignRow["HitStart"];
                            hhalignRow["HitEnd"] = crcAlignRow["HitEnd"];
                            hhalignRow["HitSequence"] = crcAlignRow["HitAlignment"];
                            hhalignRow["Identity"] = crcAlignRow["Identity"];
                            hhAlignTable.Rows.Add(hhalignRow);
                        }
                        else if (Array.IndexOf (entryPairsToBeAligned, reversedEntryPair) > -1)
                        {
                            DataRow hhalignRow = hhAlignTable.NewRow();
                            hhalignRow["QueryEntry"] = entityRow2["PdbID"];
                            hhalignRow["QueryChain"] = entityRow2["AuthorChain"];
                            hhalignRow["QueryEntity"] = entityRow2["EntityID"];
                            hhalignRow["HitEntry"] = entityRow1["PdbID"];
                            hhalignRow["HitChain"] = entityRow1["AuthorChain"];
                            hhalignRow["HitEntity"] = entityRow1["EntityID"];
                            hhalignRow["QueryLength"] = crcAlignRow["QueryLength"];
                            hhalignRow["QueryStart"] = crcAlignRow["QueryStart"];
                            hhalignRow["QueryEnd"] = crcAlignRow["QueryEnd"];
                            hhalignRow["QuerySequence"] = crcAlignRow["QueryAlignment"];
                            hhalignRow["HitLength"] = crcAlignRow["HitLength"];
                            hhalignRow["HitStart"] = crcAlignRow["HitStart"];
                            hhalignRow["HitEnd"] = crcAlignRow["HitEnd"];
                            hhalignRow["HitSequence"] = crcAlignRow["HitAlignment"];
                            hhalignRow["Identity"] = crcAlignRow["Identity"];
                            hhAlignTable.Rows.Add(hhalignRow);
                        }
                    }
                }
            }
            return hhAlignTable;
        }
        #endregion

        #region calcluate crc codes for those missing crc pdb sequences
        private SeqCrc seqCrc = new SeqCrc();
        public DataRow[] CalculatePdbSeqCrcCodes(string pdbId)
        {
            DataTable tmpPdbCrcMapTable = null;
            string queryString = "";
            if (pdbCrcMapTable == null)
            {
                queryString = "Select First 1 * From PdbCrcMap;";
                tmpPdbCrcMapTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                tmpPdbCrcMapTable.Clear();
            }
            else
            {
                tmpPdbCrcMapTable = pdbCrcMapTable.Clone();
            }
            queryString = string.Format("Select AsymID, AuthorChain, EntityID, Sequence From AsymUnit" + 
                " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable entrySeqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            Dictionary<int,string> entityCrcHash = new Dictionary<int,string> ();
            int entityId = 0;
            string sequence = "";
            string crc64 = "";
            List<DataRow> crcRowList = new List<DataRow> ();
            foreach (DataRow chainRow in entrySeqTable.Rows)
            {
                entityId = Convert.ToInt32(chainRow["EntityID"].ToString ());
                if (entityCrcHash.ContainsKey (entityId))
                {
                    crc64 = (string)entityCrcHash[entityId];
                }
                else
                {
                    sequence = chainRow["Sequence"].ToString();
                    crc64 = seqCrc.GetCrc64(sequence);
                    entityCrcHash.Add(entityId, crc64);
                }
               
                DataRow newPdbCrcRow = tmpPdbCrcMapTable.NewRow();
                newPdbCrcRow["PdbID"] = pdbId;
                newPdbCrcRow["EntityID"] = entityId;
                newPdbCrcRow["AuthorChain"] = chainRow["AuthorChain"];
                newPdbCrcRow["AsymID"] = chainRow["AsymID"];
                newPdbCrcRow["crc"] = crc64;
                newPdbCrcRow["IsRep"] = '0';
                tmpPdbCrcMapTable.Rows.Add(newPdbCrcRow);
                crcRowList.Add(newPdbCrcRow);
            }
            DataRow[] crcRows = new DataRow[crcRowList.Count];
            crcRowList.CopyTo(crcRows);
            return crcRows;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authorChain"></param>
        /// <returns></returns>
        public string CalculatePdbChainSeqCrcCode(string pdbId, string authorChain)
        {
            string queryString = string.Format("Select AsymId, entityId, Sequence From AsymUnit" +
                " Where PdbID = '{0}' AND AuthorChain = '{1}' AND PolymerType = 'polypeptide';", pdbId, authorChain);
            DataTable chainSeqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string sequence = "";
            string crc64 = "";
            if (chainSeqTable.Rows.Count > 0)
            {
                sequence = chainSeqTable.Rows[0]["Sequence"].ToString();
                crc64 = seqCrc.GetCrc64(sequence);
                DataRow newPdbCrcRow = pdbCrcMapTable.NewRow();
                newPdbCrcRow["PdbID"] = pdbId;
                newPdbCrcRow["EntityID"] = chainSeqTable.Rows[0]["entityId"];
                newPdbCrcRow["AuthorChain"] = authorChain;
                newPdbCrcRow["AsymID"] = chainSeqTable.Rows[0]["AsymID"];
                newPdbCrcRow["crc"] = crc64;
                newPdbCrcRow["IsRep"] = '0';
                pdbCrcMapTable.Rows.Add(newPdbCrcRow);
            }
            return crc64;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authorChain"></param>
        /// <returns></returns>
        public string CalculatePdbChainSeqCrcCode(string pdbId, int entityId, out string authorChain)
        {
            string queryString = string.Format("Select AsymId, AuthorChain, Sequence From AsymUnit" +
                " Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable chainSeqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string sequence = "";
            string crc64 = "";
            authorChain = "";
            if (chainSeqTable.Rows.Count > 0)
            {
                sequence = chainSeqTable.Rows[0]["Sequence"].ToString();
                crc64 = seqCrc.GetCrc64(sequence);
                authorChain = chainSeqTable.Rows[0]["AuthorChain"].ToString().TrimEnd();
                DataRow newPdbCrcRow = pdbCrcMapTable.NewRow();
                newPdbCrcRow["PdbID"] = pdbId;
                newPdbCrcRow["EntityID"] = entityId;
                newPdbCrcRow["AuthorChain"] = authorChain;
                newPdbCrcRow["AsymID"] = chainSeqTable.Rows[0]["AsymID"];
                newPdbCrcRow["crc"] = crc64;
                newPdbCrcRow["IsRep"] = '0';
                pdbCrcMapTable.Rows.Add(newPdbCrcRow);
            }
            return crc64;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authorChain"></param>
        /// <returns></returns>
        public string CalculatePdbChainSeqCrcCode(string pdbId, string authorChain, out int entityId)
        {
            string queryString = string.Format("Select AsymID, entityId, Sequence From AsymUnit" +
                " Where PdbID = '{0}' AND AuthorChain = '{1}' AND PolymerType = 'polypeptide';", pdbId, authorChain);
            DataTable chainSeqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string sequence = "";
            string crc64 = "";
            entityId = -1;
            if (chainSeqTable.Rows.Count > 0)
            {
                sequence = chainSeqTable.Rows[0]["Sequence"].ToString();
                crc64 = seqCrc.GetCrc64(sequence);
                DataRow newPdbCrcRow = pdbCrcMapTable.NewRow();
                newPdbCrcRow["PdbID"] = pdbId;
                newPdbCrcRow["EntityID"] = chainSeqTable.Rows[0]["EntityID"];
                newPdbCrcRow["AuthorChain"] = authorChain;
                newPdbCrcRow["AsymID"] = chainSeqTable.Rows[0]["AsymID"];
                newPdbCrcRow["crc"] = crc64;
                newPdbCrcRow["IsRep"] = '0';
                pdbCrcMapTable.Rows.Add(newPdbCrcRow);
                entityId = Convert.ToInt32(chainSeqTable.Rows[0]["EntityID"].ToString ());
            }
            return crc64;
        }
        #endregion
    }
}
