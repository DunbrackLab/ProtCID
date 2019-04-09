using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamLigand
{
    /// <summary>
    /// this class is used to calculate the jaccard index between two ligands in a Pfam
    /// </summary>
    public class LigandComPfamHmm
    {
        #region member variables
        private string tableName = PfamLigandTableNames.pfamLigandComHmmTableName;
        private DataTable ligandComHmmTable = null;
        DbQuery dbQuery = new DbQuery();
        DbInsert dbInsert = new DbInsert();
        DbUpdate dbUpdate = new DbUpdate();
        #endregion

        #region new
        /// <summary>
        /// 
        /// </summary>
        public void CalculateLigandsComPfamHmmPos ()
        {
            bool isUpdate = true;
            CreateTables(isUpdate);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculate common HMM positions interacting Ligands");

            string[] ligandPfams = GetLigandInteractingPfams();

            ProtCidSettings.progressInfo.totalOperationNum = ligandPfams.Length;
            ProtCidSettings.progressInfo.totalStepNum = ligandPfams.Length;

            foreach (string pfamId in ligandPfams)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pfamId;

                ProtCidSettings.logWriter.WriteLine(ProtCidSettings.progressInfo.currentOperationNum.ToString() + "  " + pfamId);
                try
                {
                    CalculatePfamLigandComHmmPos(pfamId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculating common HMM positions interacting Ligands done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetLigandInteractingPfams()
        {
            string queryString = "Select Distinct PfamID From PfamLigands;";
            DataTable ligandPfamsTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] pfamIds = new string[ligandPfamsTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamRow in ligandPfamsTable.Rows)
            {
                pfamIds[count] = pfamRow["PfamID"].ToString().TrimEnd();
                count++;
            }
            return pfamIds;
        }
        #endregion

        #region update
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public Dictionary<string, List<string>> UpdateLigandsComPfamHmmPos (string[] updateEntries)
        {
            bool isUpdate = true;
            CreateTables(isUpdate);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Ligand common Pfam sites");
            ProtCidSettings.logWriter.WriteLine("Calculate common Pfam sites of ligands");

            Dictionary<string, List<string>> updatePfamEntryDict = GetUpdatePfams(updateEntries);
            List<string> updatePfamList = updatePfamEntryDict.Keys.ToList();
            updatePfamList.Sort();

            StreamWriter updateDataWriter = new StreamWriter("UpdatePfamEntries.txt");
            string dataLine = "";          
            foreach (string pfamId in updatePfamList)
            {
                dataLine = pfamId;
                List<string> updateEntryList = (List<string>) updatePfamEntryDict[pfamId];
                foreach (string pdbId in updateEntryList)
                {
                    dataLine += (" " + pdbId);
                }
                updateDataWriter.WriteLine(dataLine);
            }
            updateDataWriter.Close();

            // delete the data rows related with update entries
            DeleteObsoleteData(updateEntries);

            ProtCidSettings.progressInfo.totalOperationNum = updatePfamList.Count;
            ProtCidSettings.progressInfo.totalStepNum = updatePfamList.Count;

            foreach (string pfamId in updatePfamList)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pfamId;

                ProtCidSettings.logWriter.WriteLine(ProtCidSettings.progressInfo.currentOperationNum.ToString() + "  " + pfamId);
                try
                {
                    List<string> pfamUpdateEntryList = (List<string>) updatePfamEntryDict[pfamId];
                    UpdatePfamLigandComHmmPos(pfamId, pfamUpdateEntryList);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Clustering ligands done!");
            ProtCidSettings.logWriter.WriteLine("Update ligand common Pfam sites done!");
            ProtCidSettings.logWriter.Flush();
            return updatePfamEntryDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        public void UpdatePfamLigandComHmmPos(string pfamId, List<string> updateEntryList)
        {
            string queryString = string.Format("Select PdbID, ChainDomainID, AsymChain, LigandChain, LigandSeqID, SeqID, HmmSeqID From PfamLigands" +
                " Where PfamID = '{0}' Order By PdbID, ChainDomainID, LigandChain;", pfamId);
            DataTable ligandInteractSeqTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> entryList = new List<string>();
            string pdbId = "";
            foreach (DataRow seqRow in ligandInteractSeqTable.Rows)
            {
                pdbId = seqRow["PdbID"].ToString().TrimEnd();
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            int numOfComHmmPos = 0;
            double jScore = 0;
            for (int i = 0; i < entryList.Count; i++)
            {
                Dictionary<int, List<string>> chainDomainLigandsDictI = GetChainDomainLigandList(entryList[i], ligandInteractSeqTable);
                for (int j = i + 1; j < entryList.Count; j++)
                {
                    if ((! updateEntryList.Contains (entryList[i])) && (! updateEntryList.Contains (entryList[j]))) 
                    {
                        continue; // both are not update entries, then continue;
                    }
                    Dictionary<int, List<string>> chainDomainLigandsDictJ = GetChainDomainLigandList(entryList[j], ligandInteractSeqTable);
                    foreach (int chainDomainIdI in chainDomainLigandsDictI.Keys)
                    {
                        List<string> ligandChainListI = chainDomainLigandsDictI[chainDomainIdI];
                        foreach (int chainDomainIdJ in chainDomainLigandsDictJ.Keys)
                        {
                            List<string> ligandChainListJ = chainDomainLigandsDictJ[chainDomainIdJ];
                            foreach (string ligandChainI in ligandChainListI)
                            {
                                List<int> hmmPosListI = GetLigandInteractingPfamHmmList(entryList[i], chainDomainIdI, ligandChainI, ligandInteractSeqTable);
                                foreach (string ligandChainJ in ligandChainListJ)
                                {
                                    List<int> hmmPosListJ = GetLigandInteractingPfamHmmList(entryList[j], chainDomainIdJ, ligandChainJ, ligandInteractSeqTable);
                                    jScore = CalculateJaccardScore(hmmPosListI, hmmPosListJ, out numOfComHmmPos);
                                    if (numOfComHmmPos > 0)
                                    {
                                        DataRow comHmmRow = ligandComHmmTable.NewRow();
                                        comHmmRow["PfamId"] = pfamId;
                                        comHmmRow["PdbID1"] = entryList[i];
                                        comHmmRow["ChainDomainID1"] = chainDomainIdI;
                                        comHmmRow["LigandChain1"] = ligandChainI;
                                        comHmmRow["PdbID2"] = entryList[j];
                                        comHmmRow["ChainDomainID2"] = chainDomainIdJ;
                                        comHmmRow["LigandChain2"] = ligandChainJ;
                                        comHmmRow["NumOfHmmSites1"] = hmmPosListI.Count;
                                        comHmmRow["NumOfHmmSites2"] = hmmPosListJ.Count;
                                        comHmmRow["NumOfComHmmSites"] = numOfComHmmPos;
                                        comHmmRow["Jscore"] = jScore;
                                        ligandComHmmTable.Rows.Add(comHmmRow);
                                    } // if there is shared interacting Pfam HMM positions
                                }// end of ligandChainJ
                            } // end of ligandChainI
                        } // end of chainDomainJ
                    } // end of chainDomainI
                } // end of entrylist J
                dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, ligandComHmmTable);
                ligandComHmmTable.Clear();
            } // end of entryList I                    
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        public Dictionary<string, List<string>> GetUpdatePfams(string[] updateEntries)
        {
            string[] subUpdateEntries = null;
            string queryString = "";
            Dictionary<string, List<string>> pfamUpdateEntryDict = new Dictionary<string, List<string>>();
            string pfamId = "";
            string pdbId = "";
            for (int i = 0; i < updateEntries.Length; i += 300)
            {
                subUpdateEntries = ParseHelper.GetSubArray(updateEntries, i, 300);
                queryString = string.Format("Select Distinct Pfam_ID, PdbID From  PdbPfam Where PdbID IN ({0});", ParseHelper.FormatSqlListString(subUpdateEntries));
                DataTable pfamIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                foreach (DataRow pfamIdRow in pfamIdTable.Rows)
                {
                    pfamId = pfamIdRow["Pfam_ID"].ToString().TrimEnd();
                    pdbId = pfamIdRow["PdbID"].ToString ();
                    if (pfamUpdateEntryDict.ContainsKey (pfamId))
                    {
                        List<string> entryList = (List<string>) pfamUpdateEntryDict[pfamId];
                        if (! entryList.Contains (pdbId))
                        {
                            entryList.Add(pdbId);
                        }
                    }
                    else
                    {
                        List<string> entryList = new List<string>();
                        entryList.Add(pdbId);
                        pfamUpdateEntryDict.Add(pfamId, entryList);
                    }
                }
            }
            return pfamUpdateEntryDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        private void DeleteObsoleteData (string[] updateEntries)
        {
            string deleteString = "";
            for (int i = 0; i < updateEntries.Length; i += 300)
            {
                string[] subEntries = ParseHelper.GetSubArray(updateEntries, i, 300);
                deleteString = string.Format("Delete From {0} Where PdbID1 IN ({1}) OR PdbID2 IN ({1});", tableName, ParseHelper.FormatSqlListString (subEntries));
                dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
            }
        }
        #endregion

        #region calculate common hmm positions 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        public void CalculatePfamLigandComHmmPos (string pfamId)
        {
            if (ligandComHmmTable == null)
            {
                InitializeComHmmTable ();
            }
            string queryString = string.Format("Select PdbID, ChainDomainID, AsymChain, LigandChain, LigandSeqID, SeqID, HmmSeqID From PfamLigands" + 
                " Where PfamID = '{0}' Order By PdbID, ChainDomainID, LigandChain;", pfamId);
            DataTable ligandInteractSeqTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> entryList = new List<string>();
            string pdbId = "";
            foreach (DataRow seqRow in ligandInteractSeqTable.Rows)
            {
                pdbId = seqRow["PdbID"].ToString().TrimEnd();
                if (! entryList.Contains (pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            int numOfComHmmPos = 0;
            double jScore = 0;
            for (int i = 0; i < entryList.Count; i++)
            {
                Dictionary<int, List<string>> chainDomainLigandsDictI = GetChainDomainLigandList(entryList[i], ligandInteractSeqTable);
                for (int j = i + 1; j < entryList.Count; j++)
                {
                    Dictionary<int, List<string>> chainDomainLigandsDictJ = GetChainDomainLigandList(entryList[j], ligandInteractSeqTable);
                    foreach (int chainDomainIdI in chainDomainLigandsDictI.Keys)
                    {
                        List<string> ligandChainListI = chainDomainLigandsDictI[chainDomainIdI];
                        foreach (int chainDomainIdJ in chainDomainLigandsDictJ.Keys)
                        {
                            List<string> ligandChainListJ = chainDomainLigandsDictJ[chainDomainIdJ];
                            foreach (string ligandChainI in ligandChainListI)
                            {
                                List<int> hmmPosListI = GetLigandInteractingPfamHmmList(entryList[i], chainDomainIdI, ligandChainI, ligandInteractSeqTable);
                                foreach (string ligandChainJ in ligandChainListJ)
                                {
                                    List<int> hmmPosListJ = GetLigandInteractingPfamHmmList(entryList[j], chainDomainIdJ, ligandChainJ, ligandInteractSeqTable);
                                    jScore = CalculateJaccardScore(hmmPosListI, hmmPosListJ, out numOfComHmmPos);
                                    if (numOfComHmmPos > 0)
                                    {
                                        DataRow comHmmRow = ligandComHmmTable.NewRow();
                                        comHmmRow["PfamId"] = pfamId;
                                        comHmmRow["PdbID1"] = entryList[i];
                                        comHmmRow["ChainDomainID1"] = chainDomainIdI;
                                        comHmmRow["LigandChain1"] = ligandChainI;
                                        comHmmRow["PdbID2"] = entryList[j];
                                        comHmmRow["ChainDomainID2"] = chainDomainIdJ;
                                        comHmmRow["LigandChain2"] = ligandChainJ;
                                        comHmmRow["NumOfHmmSites1"] = hmmPosListI.Count;
                                        comHmmRow["NumOfHmmSites2"] = hmmPosListJ.Count;
                                        comHmmRow["NumOfComHmmSites"] = numOfComHmmPos;
                                        comHmmRow["Jscore"] = jScore;
                                        ligandComHmmTable.Rows.Add(comHmmRow);
                                    } // if there is shared interacting Pfam HMM positions
                                }// end of ligandChainJ
                            } // end of ligandChainI
                        } // end of chainDomainJ
                    } // end of chainDomainI
                } // end of entrylist J
                dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, ligandComHmmTable);
                ligandComHmmTable.Clear();
            } // end of entryList I                    
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <param name="ligandChain"></param>
        /// <param name="ligandInteractSeqTable"></param>
        /// <returns></returns>
        private List<int> GetLigandInteractingPfamHmmList (string pdbId, int chainDomainId, string ligandChain, DataTable ligandInteractSeqTable)
        {
            DataRow[] interactHmmSeqRows = ligandInteractSeqTable.Select(string.Format ("PdbID = '{0}' AND ChainDomainID = '{1}' AND LigandChain = '{2}'", 
                pdbId, chainDomainId, ligandChain));
            List<int> hmmList = new List<int>();
            int hmmSeqId = 0;
            foreach (DataRow seqRow in interactHmmSeqRows)
            {
                hmmSeqId = Convert.ToInt32(seqRow["HmmSeqID"].ToString ());
                if (! hmmList.Contains (hmmSeqId))
                {
                    hmmList.Add(hmmSeqId);
                }
            }
            return hmmList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="ligandInteractSeqTable"></param>
        /// <returns></returns>
        private Dictionary<int, List<string>> GetChainDomainLigandList(string pdbId, DataTable ligandInteractSeqTable)
        {
            Dictionary<int, List<string>> chainDomainLigandDict = new Dictionary<int, List<string>>();
            DataRow[] entryInfoRows = ligandInteractSeqTable.Select(string.Format("PdbID= '{0}'", pdbId));
            int chainDomainId = 0;
            string ligandChain = "";
            foreach (DataRow seqRow in entryInfoRows)
            {
                chainDomainId = Convert.ToInt32(seqRow["ChainDomainID"].ToString());
                ligandChain = seqRow["LigandChain"].ToString().TrimEnd();
                if (chainDomainLigandDict.ContainsKey(chainDomainId))
                {
                    List<string> ligandList = (List<string>)chainDomainLigandDict[chainDomainId];
                    if (!ligandList.Contains(ligandChain))
                    {
                        ligandList.Add(ligandChain);
                    }
                }
                else
                {
                    List<string> ligandList = new List<string>();
                    ligandList.Add(ligandChain);
                    chainDomainLigandDict.Add(chainDomainId, ligandList);
                }
            }
            return chainDomainLigandDict;
        }
        #endregion

        #region jaccard index (score)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSeqList1"></param>
        /// <param name="hmmSeqList2"></param>
        /// <returns></returns>
        private int GetNumOfCommonPfamHmmPos (List<int> hmmSeqList1, List<int> hmmSeqList2)
        {
            List<int> comHmmList = GetCommonPfamHmmPos(hmmSeqList1, hmmSeqList2);
            return comHmmList.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hmmSeqList1"></param>
        /// <param name="hmmSeqList2"></param>
        /// <returns></returns>
        private List<int> GetCommonPfamHmmPos(List<int> hmmSeqList1, List<int> hmmSeqList2)
        {
            List<int> comHmmList = new List<int>();
            foreach (int seqId1 in hmmSeqList1)
            {
                foreach (int seqId2 in hmmSeqList2)
                {
                    if (seqId1 == seqId2)
                    {
                        if (!comHmmList.Contains(seqId1))
                        {
                            comHmmList.Add(seqId1);
                        }
                    }
                }
            }
            return comHmmList;
        }

        /// <summary>
        /// Jaccard index
        /// </summary>
        /// <param name="hmmSeqList1"></param>
        /// <param name="hmmSeqList2"></param>
        /// <returns></returns>
        private double CalculateJaccardScore (List<int> hmmSeqList1, List<int> hmmSeqList2, out int numOfComHmmPos)
        {
            numOfComHmmPos = GetNumOfCommonPfamHmmPos(hmmSeqList1, hmmSeqList2);
            List<int> allHmmList = new List<int>();
            allHmmList.AddRange(hmmSeqList1);
            foreach (int seqId2 in hmmSeqList2)
            {
                if (! allHmmList.Contains (seqId2))
                {
                    allHmmList.Add(seqId2);
                }
            }
            double jScore = (double)numOfComHmmPos / (double)allHmmList.Count;
            return jScore;
        }
        #endregion

        #region create tables
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        private void CreateTables (bool isUpdate)
        {
            InitializeComHmmTable();

            if (! isUpdate)
            {
                DbCreator dbCreate = new DbCreator ();
                 string dbCreateTableString = "Create Table " + tableName + " ( " +
                    "PfamID Varchar(40) Not Null, " +
                    "PdbID1 char(4) Not Null, " +
                    "ChainDomainID1 Integer Not Null, " +
                    "LigandChain1 char(3) Not Null, " +
                    "PdbID2 char(4) Not Null, " +
                    "ChainDomainID2 Integer Not Null, " +                  
                    "LigandChain2 char(3) Not Null, " +
                    "NumOfHmmSites1 Integer Not Null, " +
                    "NumOfHmmSites2 Integer Not Null, " +
                    "NumOfComHmmSites Integer Not Null, " +
                    "Jscore FLOAT Not Null);";
                dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, dbCreateTableString, tableName);
                string indexString = "Create Index PfamLigandComHmm_pfam on " + tableName + " (PfamID)";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, tableName);
                indexString = "Create Index PfamLigandComHmm_pdb on " + tableName + "(PdbID1, PdbID2)";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, tableName);
                indexString = "Create Index PfamLigandComHmm_pdb1 on " + tableName + "(PdbID1, ChainDomainID1)";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, tableName);
                indexString = "Create Index PfamLigandComHmm_pdb2 on " + tableName + "(PdbID2, ChainDomainID2)";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, tableName);          
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void InitializeComHmmTable ()
        {
            string[] dataColumns = {"PfamID", "PdbID1", "ChainDomainID1", "LigandChain1", "PdbID2", "ChainDomainID2", "LigandChain2", 
                                       "NumOfHmmSites1", "NumOfHmmSites2", "NumOfComHmmSites", "Jscore"};
            ligandComHmmTable = new DataTable(PfamLigandTableNames.pfamLigandComHmmTableName);
            foreach (string col in dataColumns)
            {
                ligandComHmmTable.Columns.Add(new DataColumn(col));
            }
        }
        #endregion
    }
}
