using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace ProtCIDPaperDataLib.paper
{
    public class PaperLigandDataInfo : PaperDataInfo
    {
        private string ligandDataDir = "";

        public PaperLigandDataInfo ()
        {
            ligandDataDir = Path.Combine(dataDir, "PfamLigands");
        }

        #region add unpid and residue numbers to PfamLigands
        public void AddUnpSeqInfoToPfamLigands()
        {
            StreamWriter logWriter = new StreamWriter("AddUnpToPfamLigandsLog.txt", true);
            logWriter.WriteLine(DateTime.Today.ToShortDateString());
            string queryString = "Select Distinct PdbID From PfamLigands;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString().TrimEnd();
                try
                {
                    AddUnpSeqInfoToPfamLigandsTable(pdbId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(pdbId + ": " + ex.Message);
                    logWriter.WriteLine(pdbId + ": " + ex.Message);
                    logWriter.Flush();
                }
            }
            logWriter.Close();
        }

        private void AddUnpSeqInfoToPfamLigandsTable(string pdbId)
        {
            string asymChain = "";
            string seqId = "";
            string queryString = string.Format("Select PdbID, AsymChain, SeqID From PfamLigands Where PdbID = '{0}';", pdbId);
            DataTable pdbChainSeqTable = ProtCidSettings.protcidQuery.Query(queryString);
            DataTable ligandChainUnpSeqTable = pdbChainSeqTable.Clone();
            ligandChainUnpSeqTable.Columns.Add(new DataColumn("UnpCode"));
            ligandChainUnpSeqTable.Columns.Add(new DataColumn("UnpSeqID"));
            Dictionary<string, Dictionary<string, string[][]>> entryChainUnpSeqMapDict = GetEntryChainSeqMapDict(pdbId);

            foreach (DataRow seqRow in pdbChainSeqTable.Rows)
            {
                asymChain = seqRow["AsymChain"].ToString().TrimEnd();
                seqId = seqRow["SeqID"].ToString();
                string[] unpSeq = GetUnpSeqId(asymChain, seqId, entryChainUnpSeqMapDict);
                if (unpSeq[0] != "" && unpSeq[1] != "")
                {
                    AddUnpSeqInfo(pdbId, asymChain, seqId, unpSeq[0], unpSeq[1]);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="seqId"></param>
        /// <param name="unpCode"></param>
        /// <param name="unpSeqId"></param>
        private void AddUnpSeqInfo(string pdbId, string asymChain, string seqId, string unpCode, string unpSeqId)
        {
            string updateString = string.Format("Update PfamLigands Set UnpCode = '{0}', UnpSeqId = {1} " +
                " Where PdbId = '{2}' AND AsymChain = '{3}' AND SeqID = {4};", unpCode, unpSeqId, pdbId, asymChain, seqId);
            dbUpdate.Update(updateString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<string, string[][]>> GetEntryChainSeqMapDict(string pdbId)
        {
            string asymChain = "";
            string unpCode = "";
            string seqNumbers = "";
            string dbSeqNumbers = "";
            int orgSeqNumberLen = 0;
            string queryString = string.Format("Select PdbDbRefSifts.PdbID, DbCode, AsymID, SeqNumbers, DbSeqNumbers " +
                " From PdbDbRefSifts, PdbDbRefSeqAlignSifts " +
                " Where PdbDbRefSifts.PdbID = '{0}' AND DbName = 'UNP' AND PdbDbRefSifts.PdbID = PDbDbRefSeqAlignSifts.PdbID AND " +
                " PdbDbRefSifts.RefID = PdbDbRefSeqAlignSifts.RefID;", pdbId);
            DataTable pdbDbSeqAlignTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            Dictionary<string, Dictionary<string, string[][]>> entryChainSeqMapDict = new Dictionary<string, Dictionary<string, string[][]>>();
            foreach (DataRow seqAlignRow in pdbDbSeqAlignTable.Rows)
            {
                asymChain = seqAlignRow["AsymID"].ToString().TrimEnd();
                unpCode = seqAlignRow["DbCode"].ToString().TrimEnd();
                seqNumbers = seqAlignRow["SeqNumbers"].ToString();
                dbSeqNumbers = seqAlignRow["DbSeqNumbers"].ToString();
                string[][] seqDbSeqNumbers = new string[2][];
                seqDbSeqNumbers[0] = seqNumbers.Split(',');
                seqDbSeqNumbers[1] = dbSeqNumbers.Split(',');
                if (entryChainSeqMapDict.ContainsKey(asymChain))
                {
                    Dictionary<string, string[][]> chainSeqMapDict = entryChainSeqMapDict[asymChain];
                    if (chainSeqMapDict.ContainsKey(unpCode))
                    {
                        string[][] chainSeqNumbers = chainSeqMapDict[unpCode];
                        orgSeqNumberLen = chainSeqNumbers[0].Length;
                        Array.Resize(ref chainSeqNumbers[0], orgSeqNumberLen + seqDbSeqNumbers[0].Length);
                        Array.Copy(seqDbSeqNumbers[0], 0, chainSeqNumbers[0], orgSeqNumberLen, seqDbSeqNumbers[0].Length);
                        orgSeqNumberLen = chainSeqNumbers[1].Length;
                        Array.Resize(ref chainSeqNumbers[1], chainSeqNumbers[1].Length + seqDbSeqNumbers[1].Length);
                        Array.Copy(seqDbSeqNumbers[1], 0, chainSeqNumbers[1], orgSeqNumberLen, seqDbSeqNumbers[1].Length);
                    }
                    else
                    {
                        chainSeqMapDict.Add(unpCode, seqDbSeqNumbers);
                    }
                }
                else
                {
                    Dictionary<string, string[][]> chainSeqMapDict = new Dictionary<string, string[][]>();
                    chainSeqMapDict.Add(unpCode, seqDbSeqNumbers);
                    entryChainSeqMapDict.Add(asymChain, chainSeqMapDict);
                }
            }
            return entryChainSeqMapDict;
        }

        private string[] GetUnpSeqId(string asymChain, string seqId, Dictionary<string, Dictionary<string, string[][]>> entryChainUnpSeqMapDict)
        {
            int seqIndex = -1;
            string dbSeqId = "";
            int intDbSeqId = 0;
            string chainUnpCode = "";
            if (entryChainUnpSeqMapDict.ContainsKey(asymChain))
            {
                Dictionary<string, string[][]> chainSeqMapDict = entryChainUnpSeqMapDict[asymChain];
                foreach (string unpCode in chainSeqMapDict.Keys)
                {
                    string[][] chainDbSeqNumbers = chainSeqMapDict[unpCode];
                    seqIndex = Array.IndexOf(chainDbSeqNumbers[0], seqId);
                    if (seqIndex > -1)
                    {
                        dbSeqId = chainDbSeqNumbers[1][seqIndex];
                        if (int.TryParse(dbSeqId, out intDbSeqId))
                        {
                            chainUnpCode = unpCode;
                            break;
                        }
                    }
                }
            }
            string[] unpSeq = new string[2];
            unpSeq[0] = chainUnpCode;
            unpSeq[1] = dbSeqId;
            return unpSeq;
        }
        #endregion

        #region can be added pdb/proteins by pfams
        /// <summary>
        /// 
        /// </summary>
        public void PrintPfamLigandPepSumInfo()
        {
            StreamWriter dataWriter = new StreamWriter(Path.Combine(ligandDataDir, "PfamLigandsPdbUnpNumbers.txt"), true);
            dataWriter.WriteLine("PfamID\tIPDB\tPDBs\tIUniProts\tUniProts\tIHumanUnps\tHumanUnps");
            string queryString = "Select Distinct PfamID From PfamLigands;";
            DataTable pfamTable = ProtCidSettings.protcidQuery.Query(queryString);
            /*          foreach (DataRow pfamRow in pfamTable.Rows)
                      {
                          pfamId = pfamRow["PfamID"].ToString().TrimEnd();
                          int[] pdbNumbers = GetPfamPdbLigandsInfo(pfamId);
                          int[] unpNumbers = GetPfamProteinLigandsInfo(pfamId);
                          int[] humanNumbers = GetPfamHumanProteinLigandsInfo(pfamId);
                          dataWriter.WriteLine(pfamId + "\t" + pdbNumbers[0] + "\t" + pdbNumbers[1] + "\t" +
                              unpNumbers[0] + "\t" + unpNumbers[1] + "\t" + humanNumbers[0] + "\t" + humanNumbers[1]); 
                      }*/
            int[] ligandPdbOrNot = GetNumOfEntriesCanBeAddedByPfam(pfamTable);
            int[] ligandUnpOrNot = GetNumOfProteinsCanBeAddedByPfam(pfamTable);
            int[] ligandHumanOrNot = GetNumOfHumanCanBeAddedByPfam(pfamTable);
            /*        dataWriter.WriteLine("Total\t" + ligandPdbOrNot[0] + "\t" + ligandPdbOrNot[1] + "\t" +
                        ligandUnpOrNot[0] + "\t" + ligandUnpOrNot[1] + "\t" + ligandHumanOrNot[0] + "\t" + ligandHumanOrNot[1]);*/
            dataWriter.WriteLine("Total_distinct\t" + ligandPdbOrNot[0] + "\t" + ligandPdbOrNot[1] + "\t" +
                ligandUnpOrNot[0] + "\t" + ligandUnpOrNot[1] + "\t" + ligandHumanOrNot[0] + "\t" + ligandHumanOrNot[1]);
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamTable"></param>
        /// <returns></returns>
        private int[] GetNumOfEntriesCanBeAddedByPfam(DataTable pfamTable)
        {
            Dictionary<string, List<string>> pfamEntryListDict = GetPfamPdbList();
            string queryString = "Select Distinct PdbID From PfamLigands;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> ligandPdbList = new List<string>();
            foreach (DataRow entryRow in entryTable.Rows)
            {
                ligandPdbList.Add(entryRow["PdbID"].ToString());
            }
            List<string> canBeAddedEntryList = new List<string>();
            string pfamId = "";
            foreach (DataRow pfamRow in pfamTable.Rows)
            {
                pfamId = pfamRow["PfamID"].ToString();
                if (pfamEntryListDict.ContainsKey(pfamId))
                {
                    foreach (string pdbId in pfamEntryListDict[pfamId])
                    {
                        if (!ligandPdbList.Contains(pdbId))
                        {
                            if (!canBeAddedEntryList.Contains(pdbId))
                            {
                                canBeAddedEntryList.Add(pdbId);
                            }
                        }
                    }
                }
            }
            int[] pdbNumbers = new int[2];
            pdbNumbers[0] = entryTable.Rows.Count;
            pdbNumbers[1] = canBeAddedEntryList.Count;
            return pdbNumbers;
        }

        private Dictionary<string, List<string>> GetPfamPdbList()
        {
            string queryString = "Select Distinct Pfam_ID, PdbID From PdbPfam;";
            DataTable pfamEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pfamId = "";
            string pdbId = "";
            Dictionary<string, List<string>> pfamEntryListDict = new Dictionary<string, List<string>>();
            foreach (DataRow entryRow in pfamEntryTable.Rows)
            {
                pfamId = entryRow["Pfam_ID"].ToString().TrimEnd();
                pdbId = entryRow["PdbID"].ToString();
                if (pfamEntryListDict.ContainsKey(pfamId))
                {
                    if (!pfamEntryListDict[pfamId].Contains(pdbId))
                    {
                        pfamEntryListDict[pfamId].Add(pdbId);
                    }
                }
                else
                {
                    List<string> entryList = new List<string>();
                    entryList.Add(pdbId);
                    pfamEntryListDict.Add(pfamId, entryList);
                }
            }
            return pfamEntryListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamTable"></param>
        /// <returns></returns>
        private int[] GetNumOfProteinsCanBeAddedByPfam(DataTable pfamTable)
        {
            Dictionary<string, List<string>> pfamEntryListDict = GetPfamUnpList();
            string queryString = "Select Distinct UnpCode From PfamLigands;";
            DataTable unpTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> ligandUnpList = new List<string>();
            foreach (DataRow entryRow in unpTable.Rows)
            {
                ligandUnpList.Add(entryRow["UnpCode"].ToString());
            }
            List<string> canBeAddedUnpList = new List<string>();
            string pfamId = "";
            foreach (DataRow pfamRow in pfamTable.Rows)
            {
                pfamId = pfamRow["PfamID"].ToString();
                if (pfamEntryListDict.ContainsKey(pfamId))
                {
                    foreach (string unpCode in pfamEntryListDict[pfamId])
                    {
                        if (!ligandUnpList.Contains(unpCode))
                        {
                            if (!canBeAddedUnpList.Contains(unpCode))
                            {
                                canBeAddedUnpList.Add(unpCode);
                            }
                        }
                    }
                }
            }
            int[] unpNumbers = new int[2];
            unpNumbers[0] = unpTable.Rows.Count;
            unpNumbers[1] = canBeAddedUnpList.Count;
            return unpNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetPfamUnpList()
        {
            string queryString = "Select Distinct Pfam_ID, UnpCode From UnpPfam;";
            DataTable pfamUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string pfamId = "";
            string unpCode = "";
            Dictionary<string, List<string>> pfamEntryListDict = new Dictionary<string, List<string>>();
            foreach (DataRow entryRow in pfamUnpTable.Rows)
            {
                pfamId = entryRow["Pfam_ID"].ToString().TrimEnd();
                unpCode = entryRow["UnpCode"].ToString();
                if (pfamEntryListDict.ContainsKey(pfamId))
                {
                    if (!pfamEntryListDict[pfamId].Contains(unpCode))
                    {
                        pfamEntryListDict[pfamId].Add(unpCode);
                    }
                }
                else
                {
                    List<string> entryList = new List<string>();
                    entryList.Add(unpCode);
                    pfamEntryListDict.Add(pfamId, entryList);
                }
            }
            queryString = "Select Distinct Pfam_ID, UnpCode From HumanPfam;";
            DataTable pfamHumanTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            foreach (DataRow entryRow in pfamHumanTable.Rows)
            {
                pfamId = entryRow["Pfam_ID"].ToString().TrimEnd();
                unpCode = entryRow["UnpCode"].ToString();
                if (pfamEntryListDict.ContainsKey(pfamId))
                {
                    if (!pfamEntryListDict[pfamId].Contains(unpCode))
                    {
                        pfamEntryListDict[pfamId].Add(unpCode);
                    }
                }
                else
                {
                    List<string> entryList = new List<string>();
                    entryList.Add(unpCode);
                    pfamEntryListDict.Add(pfamId, entryList);
                }
            }
            return pfamEntryListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamTable"></param>
        /// <returns></returns>
        private int[] GetNumOfHumanCanBeAddedByPfam(DataTable pfamTable)
        {
            Dictionary<string, List<string>> pfamHumanListDict = GetPfamHumanList();
            string queryString = "Select Distinct UnpCode From PfamLigands Where UnpCode Like '%_HUMAN';";
            DataTable unpTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> ligandUnpList = new List<string>();
            string unpCode = "";
            foreach (DataRow entryRow in unpTable.Rows)
            {
                unpCode = entryRow["UnpCode"].ToString().TrimEnd();
                ligandUnpList.Add(unpCode);
            }
            List<string> canBeAddedUnpList = new List<string>();
            string pfamId = "";
            foreach (DataRow pfamRow in pfamTable.Rows)
            {
                pfamId = pfamRow["PfamID"].ToString();
                if (pfamHumanListDict.ContainsKey(pfamId))
                {
                    foreach (string humanUnp in pfamHumanListDict[pfamId])
                    {
                        if (!ligandUnpList.Contains(humanUnp))
                        {
                            if (!canBeAddedUnpList.Contains(humanUnp))
                            {
                                canBeAddedUnpList.Add(humanUnp);
                            }
                        }
                    }
                }
            }
            int[] humanNumbers = new int[2];
            humanNumbers[0] = unpTable.Rows.Count;
            humanNumbers[1] = canBeAddedUnpList.Count;
            return humanNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetPfamHumanList()
        {
            string pfamId = "";
            string unpCode = "";
            Dictionary<string, List<string>> pfamUnpListDict = new Dictionary<string, List<string>>();
            string queryString = "Select Distinct Pfam_ID, UnpCode From HumanPfam;";
            DataTable pfamHumanTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            foreach (DataRow entryRow in pfamHumanTable.Rows)
            {
                pfamId = entryRow["Pfam_ID"].ToString().TrimEnd();
                unpCode = entryRow["UnpCode"].ToString();
                if (pfamUnpListDict.ContainsKey(pfamId))
                {
                    if (!(pfamUnpListDict[pfamId]).Contains(unpCode))
                    {
                        pfamUnpListDict[pfamId].Add(unpCode);
                    }
                }
                else
                {
                    List<string> entryList = new List<string>();
                    entryList.Add(unpCode);
                    pfamUnpListDict.Add(pfamId, entryList);
                }
            }
            return pfamUnpListDict;
        }
        #endregion

        #region pfam ligand cluster sum info - paper table
        /// <summary>
        /// 
        /// </summary>
        public void PrintPfamLigandClusterSumInfo()
        {
            StreamWriter dataWriter = new StreamWriter(Path.Combine(ligandDataDir, "PfamLigandClusterSumInfo_pdb.txt"));
            string queryString = "";
            int[] numEntryCutoffs = { 2, 5, 10, 20 };
            queryString = "Select PfamId, ClusterId, Count(distinct PdbID) As EntryCount From PfamLigandClustersHmm Group By PfamID, ClusterID;";
            DataTable pfamLigandClusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = "Select Distinct PfamID From PfamLigands;";
            DataTable ligandPfamTable = ProtCidSettings.protcidQuery.Query(queryString);
            dataWriter.WriteLine("Total #Pfams interacting with and ligands in PDB: " + ligandPfamTable.Rows.Count);
            queryString = "Select Distinct PdbID From PfamLigands;";
            DataTable ligandEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            dataWriter.WriteLine("Total #entries interacting with any ligands in PDB: " + ligandEntryTable.Rows.Count);
            foreach (int numEntryCutoff in numEntryCutoffs)
            {
                DataRow[] clusterRows = pfamLigandClusterTable.Select(string.Format("EntryCount >= " + numEntryCutoff));
                GetPfamClusterSumInfo(numEntryCutoff, dataWriter, clusterRows);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void PrintPfamLigandClusterSumInfoUnp ()
        {
            StreamWriter dataWriter = new StreamWriter(Path.Combine(ligandDataDir, "PfamLigandClusterSumInfo_unp_pdb.txt"));
            string queryString = "";
            int[] numEntryCutoffs = { 2, 5, 10, 20 };
 //           queryString = "Select PfamId, ClusterId, Count(distinct PdbID) As EntryCount From PfamLigandClustersHmm Group By PfamID, ClusterID;";
            queryString = "Select PfamLigandClustersHmm.PfamId, ClusterId, Count(Distinct UnpCode) As UnpCount " + 
                " From PfamLigandClustersHmm, PfamLigands " + 
                "Where PfamLigandClustersHmm.PdbID = PfamLigands.PdbID AND " + 
                " PfamLigandClustersHmm.ChainDomainID = PfamLigands.ChainDomainID AND " + 
                " PfamLigandClustersHmm.LigandChain = PfamLigands.LigandChain Group By PfamLigandClustersHmm.PfamId, ClusterId;";
            DataTable pfamLigandClusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = "Select Distinct PfamID From PfamLigands;";
            DataTable ligandPfamTable = ProtCidSettings.protcidQuery.Query(queryString);
            dataWriter.WriteLine("Total #Pfams interacting with and ligands in PDB: " + ligandPfamTable.Rows.Count);
            queryString = "Select Distinct UnpCode From PfamLigands;";
            DataTable ligandUnpTable = ProtCidSettings.protcidQuery.Query(queryString);
            dataWriter.WriteLine("Total #UNPs interacting with any ligands in PDB: " + ligandUnpTable.Rows.Count);
            queryString = "Select Distinct PdbID From PfamLigands;";
            DataTable ligandEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            dataWriter.WriteLine("Total #Entries interacting with any ligands in PDB: " + ligandEntryTable.Rows.Count);
            foreach (int numEntryCutoff in numEntryCutoffs)
            {
                DataRow[] clusterRows = pfamLigandClusterTable.Select(string.Format("UnpCount >= " + numEntryCutoff));
                GetPfamClusterSumInfoUnp (numEntryCutoff, dataWriter, clusterRows);
            }
            dataWriter.Close();
        }

        private int[] GetPfamProteinLigandsInfo(string pfamId)
        {
            string queryString = string.Format("Select Distinct UnpCode From PfamLigands Where PfamID = '{0}';", pfamId);
            DataTable unpCodeTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = string.Format("Select Distinct UnpCode From UnpPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamProtTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            queryString = string.Format("Select Distinct UnpCode From HumanPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamHumanTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            int[] pfamUnpNumbers = new int[2];
            pfamUnpNumbers[0] = unpCodeTable.Rows.Count;
            pfamUnpNumbers[1] = pfamProtTable.Rows.Count + pfamHumanTable.Rows.Count;
            return pfamUnpNumbers;
        }

        private int[] GetPfamHumanProteinLigandsInfo(string pfamId)
        {
            string queryString = string.Format("Select Distinct UnpCode From PfamLigands Where PfamID = '{0}' AND UnpCode Like '%_HUMAN';", pfamId);
            DataTable unpCodeTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = string.Format("Select Distinct UnpCode From HumanPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamHumanTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            int[] pfamUnpNumbers = new int[2];
            pfamUnpNumbers[0] = unpCodeTable.Rows.Count;
            pfamUnpNumbers[1] = pfamHumanTable.Rows.Count;
            return pfamUnpNumbers;
        }

        private string GetPfamProteins(string pfamId)
        {
            string queryString = string.Format("Select Distinct UnpCode, DomainID, SeqStart, SeqEnd " +
                " From HumanPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable humanPfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            queryString = string.Format("Select Distinct PdbID, EntityID, DomainID, SeqStart, SeqEnd " +
                " From PdbPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable pdbPfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        public void PrintPfamLigandInfo()
        {
            StreamWriter dataWriter = new StreamWriter(Path.Combine(ligandDataDir, "ligandPfamEntryInfo.txt"));
            dataWriter.WriteLine("Ligand\tPfamID\tNumEntry\tNumIEntry\tNumEntryPfam");
            string queryString = "Select Ligand, PfamID, NumEntry, NumIEntry From PfamLigandsPairSumInfo;";
            DataTable pfamTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pfamId = "";
            int entryInPfam = 0;
            string dataLine = "";
            foreach (DataRow pfamRow in pfamTable.Rows)
            {
                pfamId = pfamRow["PfamID"].ToString().TrimEnd();
                entryInPfam = GetNumEntriesOfPfam(pfamId);
                dataLine = ParseHelper.FormatDataRow(pfamRow) + "\t" + entryInPfam.ToString();
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
        }

        public void PrintLigandInteractionsMoreEntries()
        {
            StreamReader dataReader = new StreamReader(Path.Combine(ligandDataDir, "ligandPfamEntryInfo.txt"));
            StreamWriter dataWriter = new StreamWriter(Path.Combine(ligandDataDir, "ligandMoreEntries.txt"));
            string line = dataReader.ReadLine();  // header line
            string dataLine = "";
            DataTable ligandPfamInfoTable = new DataTable("LigandPfamInfo");
            string[] tableColumns = { "Ligand", "PfamID", "NumEntry", "NumIEntry", "NumEntryPfam" };
            foreach (string col in tableColumns)
            {
                /*      if (col.Substring(0, 3) == "Num")
                      {
                          ligandPfamInfoTable.Columns.Add(new DataColumn(col, System.Type.GetType ("System.Int32")));
                      }
                      else
                      {*/
                ligandPfamInfoTable.Columns.Add(new DataColumn(col));
                //          }
            }
            List<string> ligandList = new List<string>();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                if (!ligandList.Contains(fields[0]))
                {
                    ligandList.Add(fields[0]);
                }
                DataRow dataRow = ligandPfamInfoTable.NewRow();
                dataRow.ItemArray = fields;
                ligandPfamInfoTable.Rows.Add(dataRow);
            }
            dataReader.Close();
            int difNumEntries = 0;
            dataWriter.WriteLine("Ligand\tNumPfams\tNumEntries\tNumIEntries\tNumDifPfamEntries");
            foreach (string ligand in ligandList)
            {
                DataRow[] ligandsRows = ligandPfamInfoTable.Select(string.Format("Ligand = '{0}'", ligand));
                int[] ligandNumbers = GetLigandNumbers(ligandsRows);
                difNumEntries = ligandNumbers[2] - ligandNumbers[1];
                dataLine = ligandsRows[0]["Ligand"].ToString() + "\t" + ligandsRows.Length + "\t" +
                    ligandNumbers[0] + "\t" + ligandNumbers[1] + "\t" + difNumEntries.ToString();
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandRows"></param>
        /// <returns></returns>
        private int[] GetLigandNumbers(DataRow[] ligandRows)
        {
            int numEntries = 0;
            int numIEntries = 0;
            int numPfamEntries = 0;
            foreach (DataRow dataRow in ligandRows)
            {
                numEntries += Convert.ToInt32(dataRow["NumEntry"].ToString());
                numIEntries += Convert.ToInt32(dataRow["NumIEntry"].ToString());
                numPfamEntries += Convert.ToInt32(dataRow["NumEntryPfam"].ToString());
            }
            int[] ligandNumbers = new int[3];
            ligandNumbers[0] = numEntries;
            ligandNumbers[1] = numIEntries;
            ligandNumbers[2] = numPfamEntries;
            return ligandNumbers;
        }       

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetPfamEntryChainTable(string pfamId)
        {
            string queryString = string.Format("Select Distinct PdbID, AsymChain From PdbPfam, PdbPfamChain " +
                " Where PdbPfam.Pfam_ID = '{0}' AND PdbPfam.PdbID = PdbPfamChain.PdbID AND " +
                " PdbPfam.DomainID = PdbPfamChain.DomainID AND PdbPfam.EntityID = PdbPfamChain.EntityID;", pfamId);
            DataTable pfamChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            return pfamChainTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="numEntryCutoff"></param>
        /// <param name="dataWriter"></param>
        /// <param name="clusterRows"></param>
        private void GetPfamClusterSumInfo(int numEntryCutoff, StreamWriter dataWriter, DataRow[] clusterRows)
        {
            List<string> entryList = new List<string>();
            List<string> pfamList = new List<string>();
            int clusterCount = clusterRows.Length;
            string pfamId = "";
            int clusterId = 0;
            foreach (DataRow clusterRow in clusterRows)
            {
                pfamId = clusterRow["PfamID"].ToString();
                if (!pfamList.Contains(pfamId))
                {
                    pfamList.Add(pfamId);
                }
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString());
                string[] clusterEntries = GetPfamLigandClusterEntries(pfamId, clusterId);
                foreach (string pdbId in clusterEntries)
                {
                    if (!entryList.Contains(pdbId))
                    {
                        entryList.Add(pdbId);
                    }
                }
            }
            dataWriter.WriteLine("#Entry >= " + numEntryCutoff.ToString());
            dataWriter.WriteLine("#Pfams = " + pfamList.Count.ToString());
            dataWriter.WriteLine("#Clusters = " + clusterRows.Length.ToString());
            dataWriter.WriteLine("#Entries = " + entryList.Count.ToString());
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string[] GetPfamLigandClusterEntries(string pfamId, int clusterId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamLigandClustersHmm Where PfamID = '{0}' AND ClusterID = {1};", pfamId, clusterId);
            DataTable clusterEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] clusterEntries = new string[clusterEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in clusterEntryTable.Rows)
            {
                clusterEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return clusterEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="numEntryCutoff"></param>
        /// <param name="dataWriter"></param>
        /// <param name="clusterRows"></param>
        private void GetPfamClusterSumInfoUnp(int numEntryCutoff, StreamWriter dataWriter, DataRow[] clusterRows)
        {
            List<string> unpList = new List<string>();
            List<string> pfamList = new List<string>();
            List<string> pdbList = new List<string>();
            int clusterCount = clusterRows.Length;
            string pfamId = "";
            int clusterId = 0;
            foreach (DataRow clusterRow in clusterRows)
            {
                pfamId = clusterRow["PfamID"].ToString();
                if (!pfamList.Contains(pfamId))
                {
                    pfamList.Add(pfamId);
                }
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString());
                string[] clusterUnps = GetPfamLigandClusterUnps(pfamId, clusterId);
                string[] clusterPdbs = GetPfamLigandClusterEntries(pfamId, clusterId);
                foreach (string unpId in clusterUnps)
                {
                    if (!unpList.Contains(unpId))
                    {
                        unpList.Add(unpId);
                    }
                }

                foreach (string pdbId in clusterPdbs)
                {
                    if (!pdbList.Contains(pdbId))
                    {
                        pdbList.Add(pdbId);
                    }
                }
            }
            dataWriter.WriteLine("#UNPs >= " + numEntryCutoff.ToString());
            dataWriter.WriteLine("#Pfams = " + pfamList.Count.ToString());
            dataWriter.WriteLine("#Clusters = " + clusterRows.Length.ToString());
            dataWriter.WriteLine("#Unps = " + unpList.Count.ToString());
            dataWriter.WriteLine("#Entriess = " + pdbList.Count.ToString());
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string[] GetPfamLigandClusterUnps(string pfamId, int clusterId)
        {
            string queryString = string.Format("Select Distinct UnpCode From PfamLigandClustersHmm, PfamLigands " +
                " Where PfamLigandClustersHmm.PfamID = '{0}' AND ClusterID = {1} AND " +
                " PfamLigandClustersHmm.PdbID = PfamLigands.PdbID AND " +
                " PfamLigandClustersHmm.ChainDomainID = PfamLigands.ChainDomainID AND " +
                " PfamLigandClustersHmm.LigandChain = PfamLigands.LigandChain;", pfamId, clusterId);
            DataTable clusterUnpTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] clusterUnps = new string[clusterUnpTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in clusterUnpTable.Rows)
            {
                clusterUnps[count] = entryRow["UnpCode"].ToString();
                count++;
            }
            return clusterUnps;
        }
        #endregion

        #region  Pfam-ligands summary info
        /// <summary>
        /// 
        /// </summary>
        public void GetAllPfamLigandsSumInfo()
        {
            string pfamLigandFile = Path.Combine(ligandDataDir, "PfamligandsSumInfo.txt");
            StreamWriter dataWriter = new StreamWriter(pfamLigandFile);
            dataWriter.WriteLine("PfamID\t#entries\t#entrychains\t#uniprots\t#ligands\t#entries in pfam\t#entrychains in pfam\t#Uniprots in pfam");
            string queryString = "Select Distinct PfamID From PfamLigands;";
            DataTable pfamTable = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow pfamRow in pfamTable.Rows)
            {
                PrintPfamLigandsSumInfo(pfamRow["PfamID"].ToString().TrimEnd(), dataWriter);
            }

            dataWriter.Close();
        }               

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="dataWriter"></param>
        public void PrintPfamLigandsSumInfo(string pfamId, StreamWriter dataWriter)
        {
            //        string pfamId = "PDZ";
            string queryString = string.Format("Select Distinct PfamLigands.PdbID, PfamLigands.AsymChain, Ligand, UnpCode From PfamLigands, PdbLigands " +
                " WHere PfamID = '{0}' AND PfamLigands.PdbID = PdbLigands.PdbID AND PfamLigands.LigandChain = PdbLigands.AsymChain" +
                " AND  PfamLigands.LigandSeqID = PdbLigands.SeqID;", pfamId);
            DataTable pfamLigandInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = string.Format("Select Distinct PdbPfamChain.PdbID, AsymChain From PdbPfam, PdbPfamChain Where Pfam_ID = '{0}' AND " +
                "PdbPfam.PdbID = PdbPfamChain.PdbID AND PdbPfam.EntityID = PdbPfamChain.EntityID AND PdbPfam.DomainID = PdbPfamChain.DomainID;", pfamId);
            DataTable pfamEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> ligandInteractingEntryList = new List<string>();
            List<string> ligandInteractingChainList = new List<string>();
            List<string> ligandInteractingUnpList = new List<string>();
            List<string> ligandList = new List<string>();
            List<string> pfamEntryList = new List<string>();

            string pdbId = "";
            string entryChain = "";
            string unpCode = "";
            string ligand = "";
            foreach (DataRow pfamLigandRow in pfamLigandInfoTable.Rows)
            {
                pdbId = pfamLigandRow["PdbID"].ToString();
                entryChain = pdbId + pfamLigandRow["AsymChain"].ToString().TrimEnd();
                unpCode = pfamLigandRow["UnpCode"].ToString().TrimEnd();
                ligand = pfamLigandRow["Ligand"].ToString().TrimEnd();
                if (!ligandInteractingEntryList.Contains(pdbId))
                {
                    ligandInteractingEntryList.Add(pdbId);
                }
                if (!ligandInteractingChainList.Contains(entryChain))
                {
                    ligandInteractingChainList.Add(entryChain);
                }
                if (!ligandInteractingUnpList.Contains(unpCode))
                {
                    ligandInteractingUnpList.Add(unpCode);
                }
                if (!ligandList.Contains(ligand))
                {
                    ligandList.Add(ligand);
                }
            }
            foreach (DataRow pfamChainRow in pfamEntryTable.Rows)
            {
                pdbId = pfamChainRow["PdbID"].ToString();
                if (!pfamEntryList.Contains(pdbId))
                {
                    pfamEntryList.Add(pdbId);
                }
            }
            string[] pfamUnps = GetPfamUnpCodes(pfamId);
            dataWriter.WriteLine(pfamId + "\t" + ligandInteractingEntryList.Count + "\t" + ligandInteractingChainList.Count + "\t" +
                ligandInteractingUnpList.Count + "\t" + ligandList.Count + "\t" +
                pfamEntryList.Count + "\t" + pfamEntryTable.Rows.Count + "\t" + pfamUnps.Length);
            dataWriter.Flush();
        }
        #endregion
    }
}
