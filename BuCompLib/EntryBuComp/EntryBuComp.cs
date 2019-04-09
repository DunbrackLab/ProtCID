using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.IO;
using DbLib;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.StructureComp;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Settings;

namespace BuCompLib.EntryBuComp
{
    public class EntryBuComp
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        private DbInsert dbInsert = new DbInsert();
        private BiolUnitRetriever buRetriever = new BiolUnitRetriever();
        private BuInterfaceRetriever buInterfaceRetriever = new BuInterfaceRetriever();
        private InterfacesComp interfaceComp = new InterfacesComp();
#if DEBUG
        StreamWriter logWriter = new StreamWriter("EntryBuCompLog.txt");
#endif

        public enum BuType 
        {
            PDB, PISA
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void CompareEntryBUs()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Compare Entry BUs";

            string queryString = "Select Distinct PdbID From AsymUNit Where PolymerType = 'polypeptide';";
            DataTable protEntryTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

            EntryBuCompTables.InitializeTables();
            EntryBuCompTables.InitializeDbTables();

            ProtCidSettings.progressInfo.totalOperationNum = protEntryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = protEntryTable.Rows.Count;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare Entry BUs");

            string pdbId = "";
            foreach (DataRow entryRow in protEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                try
                {
                    CompareEntryBUs(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare " + pdbId + " entry BUs errors: " + ex.Message);
#if DEBUG
                    logWriter.WriteLine("Compare " + pdbId + " entry BUs errors: " + ex.Message);
#endif
                }
            }
#if DEBUG
            logWriter.Close();
#endif
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            
        }

        /// <summary>
        /// 
        /// </summary>
        public void CompareMissingEntryBUs()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Compare Entry BUs";

            string[] missingEntries = GetMissingEntries();

            EntryBuCompTables.InitializeTables();

            ProtCidSettings.progressInfo.totalOperationNum = missingEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = missingEntries.Length;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare Entry BUs");

            foreach (string pdbId in missingEntries)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                try
                {
                    CompareEntryBUs(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare " + pdbId + " entry BUs errors: " + ex.Message);
#if DEBUG
                    logWriter.WriteLine("Compare " + pdbId + " entry BUs errors: " + ex.Message);
#endif
                }
            }
#if DEBUG
            logWriter.Close();
#endif
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

      
        /// <summary>
        /// 
        /// </summary>
        public void UpdateComparingEntryBUs(string[] updatedEntries)
        {
            EntryBuCompTables.InitializeTables();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Compare Entry BUs";
            ProtCidSettings.progressInfo.totalOperationNum = updatedEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = updatedEntries.Length;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Comparing Entry BUs");

            foreach (string pdbId in updatedEntries)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                try
                {
                    DeleteObsDataInDb(pdbId);

                    CompareEntryBUs(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare " + pdbId + " entry BUs errors: " + ex.Message);
#if DEBUG
                    logWriter.WriteLine("Compare " + pdbId + " entry BUs errors: " + ex.Message);
#endif
                }
            }
#if DEBUG
            logWriter.Close();
#endif
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
       //     ProtCidSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        public void CompareEntryBUs (string pdbId)
        {
            Dictionary<string, InterfaceChains[]>[] entryBuInterfaceHashes = new Dictionary<string,InterfaceChains[]> [2];
            try
            {
                entryBuInterfaceHashes[(int)BuType.PDB] = buInterfaceRetriever.GetEntryBuInterfaces(pdbId, "pdb");
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving " + pdbId + " PDB BU interfaces errors: " + ex.Message);
#if DEBUG
                logWriter.WriteLine("Retrieving " + pdbId + " PDB BU interfaces errors: " + ex.Message);
#endif
            }
            try
            {
                entryBuInterfaceHashes[(int)BuType.PISA] = buInterfaceRetriever.GetEntryBuInterfaces(pdbId, "pisa");
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving " + pdbId + " PISA BU interfaces errors: " + ex.Message);
#if DEBUG
                logWriter.WriteLine("Retrieving " + pdbId + " PISA BU interfaces errors: " + ex.Message);
#endif
            }

            ComapreEntryBuInterfaces(pdbId, entryBuInterfaceHashes);
        }

        #region compare BU interfaces
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryBuInterfaceHashes"></param>
        public void ComapreEntryBuInterfaces(string pdbId, Dictionary<string, InterfaceChains[]>[] entryBuInterfaceHashes)
        {
            Dictionary<string, InterfacePairInfo[]> pdbPisaBuInterfaceCompHash = CompareEntryBuInterfaces(entryBuInterfaceHashes[(int)BuType.PDB], entryBuInterfaceHashes[(int)BuType.PISA]);
            InsertDataIntoTables(pdbId, pdbPisaBuInterfaceCompHash, "pdbpisa");

            dbInsert.InsertDataIntoDBtables(ProtCidSettings.buCompConnection, EntryBuCompTables.entryBuCompTables);
            EntryBuCompTables.ClearTables();
        }

        /// <summary>
        /// compare interfaces from BUs of the entry in different sources
        /// </summary>
        /// <param name="entryBuInterfaceHash1"></param>
        /// <param name="entryBuInterfaceHash2"></param>
        /// <returns></returns>
        public Dictionary<string, InterfacePairInfo[]> CompareEntryBuInterfaces(Dictionary<string, InterfaceChains[]> entryBuInterfaceHash1, Dictionary<string, InterfaceChains[]> entryBuInterfaceHash2)
        {
            Dictionary<string,  InterfacePairInfo[]> buInterfaceCompHash = new Dictionary<string,InterfacePairInfo[]> ();
            if (entryBuInterfaceHash1 != null && entryBuInterfaceHash2 != null)
            {
                foreach (string buId1 in entryBuInterfaceHash1.Keys)
                {
                    InterfaceChains[] buInterfaces1 = (InterfaceChains[])entryBuInterfaceHash1[buId1];
                    foreach (string buId2 in entryBuInterfaceHash2.Keys)
                    {
                        InterfaceChains[] buInterfaces2 = (InterfaceChains[])entryBuInterfaceHash2[buId2];
                        InterfacePairInfo[] compResult = CompareBuInterfaces(buInterfaces1, buInterfaces2);
                        if (compResult != null && compResult.Length > 0)
                        {
                            buInterfaceCompHash.Add(buId1 + "_" + buId2, compResult);
                        }
                    }
                }
            }
            return buInterfaceCompHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buInterfaces1"></param>
        /// <param name="buInterfaces2"></param>
        /// <returns></returns>
        private InterfacePairInfo[] CompareBuInterfaces(InterfaceChains[] buInterfaces1, InterfaceChains[] buInterfaces2)
        {
            InterfacePairInfo[] compResult = interfaceComp.CompareInterfacesBetweenCrystals(buInterfaces1, buInterfaces2);
            return compResult;
        }
        #endregion

        #region insert data into tables
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buInterfaceCompHash"></param>
        /// <param name="compType"></param>
        private void InsertDataIntoTables(string pdbId, Dictionary<string, InterfacePairInfo[]> buInterfaceCompHash, string compType)
        {
            int buCompTableNum = -1;
            int buInterfaceCompTableNum = -1;

            switch (compType)
            {
                case "pdbpisa":
                    buCompTableNum = EntryBuCompTables.PdbPisaBuComp;
                    buInterfaceCompTableNum = EntryBuCompTables.PdbPisaBuInterfaceComp;
                    break; 

                default:
                    break;
            }
            InsertDataIntoInterfaceCompTable(pdbId, buInterfaceCompHash, buInterfaceCompTableNum);
            InsertDataIntoBuCompTable(pdbId, buInterfaceCompHash, buCompTableNum, compType);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buInterfaceCompHash"></param>
        /// <param name="buInterfaceCompTableNum"></param>
        private void InsertDataIntoInterfaceCompTable(string pdbId, Dictionary<string, InterfacePairInfo[]> buInterfaceCompHash, int buInterfaceCompTableNum)
        {
            foreach (string buPair in buInterfaceCompHash.Keys)
            {
                string[] buIds = buPair.Split('_');
                InterfacePairInfo[] compPairs = (InterfacePairInfo[])buInterfaceCompHash[buPair];
                foreach (InterfacePairInfo compPair in compPairs)
                {
                    if (compPair.qScore >= AppSettings.parameters.contactParams.minQScore)
                    {
                        DataRow dataRow = EntryBuCompTables.entryBuCompTables[buInterfaceCompTableNum].NewRow();
                        dataRow["PdbID"] = pdbId;
                        dataRow["BuID1"] = buIds[0];
                        dataRow["BuID2"] = buIds[1];
                        dataRow["InterfaceID1"] = compPair.interfaceInfo1.interfaceId;
                        dataRow["InterfaceID2"] = compPair.interfaceInfo2.interfaceId;
                        dataRow["QScore"] = compPair.qScore;
                        EntryBuCompTables.entryBuCompTables[buInterfaceCompTableNum].Rows.Add(dataRow);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buInterfaceCompHash"></param>
        /// <param name="buCompTableNum"></param>
        /// <param name="compType"></param>
        private void InsertDataIntoBuCompTable(string pdbId, Dictionary<string, InterfacePairInfo[]> buInterfaceCompHash, int buCompTableNum, string compType)
        {
            string buType1 = "";
            string buType2 = "";

            switch (compType)
            {
                case "pdbpisa":
                    buType1 = "pdb";
                    buType2 = "pisa";
                    break;

                default:
                    break;
            }

            int numOfInterfaces1 = -1;
            int numOfInterfaces2 = -1;
            int[] interfaceIds1 = null;
            int[] interfaceIds2 = null;
            InterfacePairInfo[] compResults = null;
            int isSame = 0;
            foreach (string buPair in buInterfaceCompHash.Keys)
            {
                string[] buIds = buPair.Split ('_');
                interfaceIds1 = GetBuInterfaceIds(pdbId, buIds[0], buType1, out numOfInterfaces1);
                interfaceIds2 = GetBuInterfaceIds(pdbId, buIds[1], buType2, out numOfInterfaces2);
                compResults = (InterfacePairInfo[]) buInterfaceCompHash[buPair];
                isSame = 0;
                if (AreBUsSame(compResults, interfaceIds1, interfaceIds2, numOfInterfaces1, numOfInterfaces2))
                {
                    isSame = 1;
                }
                DataRow dataRow = EntryBuCompTables.entryBuCompTables[buCompTableNum].NewRow ();
                dataRow["PdbID"] = pdbId;
                dataRow["BuID1"] = buIds[0];
                dataRow["BuID2"] = buIds[1];
                dataRow["InterfaceNum1"] = numOfInterfaces1;
                dataRow["InterfaceNum2"] = numOfInterfaces2;
                dataRow["IsSame"] = isSame;
                EntryBuCompTables.entryBuCompTables[buCompTableNum].Rows.Add(dataRow);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private int[] GetBuInterfaceIds(string pdbId, string buId, string buType, out int numOfInterfaces)
        {
            string queryString = string.Format("Select * From {0}BuInterfaces " + 
                " Where PdbID = '{1}' AND BuID = '{2}';", buType, pdbId, buId);
            DataTable interfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            int[] buInterfaceIds = new int[interfaceTable.Rows.Count];
            int count = 0;
            numOfInterfaces = 0;
            foreach (DataRow interfaceRow in interfaceTable.Rows)
            {
                buInterfaceIds[count] = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                count++;
                numOfInterfaces += Convert.ToInt32(interfaceRow["NumOfCopy"].ToString ());
            }
            return buInterfaceIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buCompInfo"></param>
        /// <param name="interfaceIds1"></param>
        /// <param name="interfaceIds2"></param>
        /// <returns></returns>
        private bool AreBUsSame(InterfacePairInfo[] buCompInfo, int[] interfaceIds1, int[] interfaceIds2, 
            int numOfInterfaces1, int numOfInterfaces2)
        {
            List<int> simInterfaceList1 = new List<int> ();
            List<int> simInterfaceList2 = new List<int> ();

            foreach (InterfacePairInfo compPair in buCompInfo)
            {
                if (compPair.qScore >= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
                {
                    if (! simInterfaceList1.Contains(compPair.interfaceInfo1.interfaceId))
                    {
                        simInterfaceList1.Add(compPair.interfaceInfo1.interfaceId);
                    }
                    if (! simInterfaceList2.Contains(compPair.interfaceInfo2.interfaceId))
                    {
                        simInterfaceList2.Add(compPair.interfaceInfo2.interfaceId);
                    }
                }
            }

            if (numOfInterfaces1 == numOfInterfaces2)
            {
                // same BUs
                if (simInterfaceList1.Count == interfaceIds1.Length &&
                    simInterfaceList2.Count == interfaceIds2.Length)
                {
                    return true;
                }
            }
            else
            {
                // substructure
                if (Math.Min(simInterfaceList1.Count, simInterfaceList2.Count) ==
                    Math.Min(interfaceIds1.Length, interfaceIds2.Length))
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region get entries to be updated
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetMissingEntries()
        {
            string queryString = "Select Distinct PdbID From AsymUNit Where PolymerType = 'polypeptide';";
            DataTable protEntryTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

            List<string> entryNotInDbList = new List<string> ();
            string pdbId = "";
            foreach (DataRow entryRow in protEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (!IsEntryCompared(pdbId, "PdbPisaBuComp"))
                {
                    entryNotInDbList.Add(pdbId);
                }
            }
            string[] entriesNotInDb = new string[entryNotInDbList.Count];
            entryNotInDbList.CopyTo(entriesNotInDb);
            return entriesNotInDb;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private bool IsEntryCompared(string pdbId, string tableName)
        {
            string queryString = string.Format("Select * From {0} Where PdbID = '{1}';", tableName, pdbId);
            DataTable buCompTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            if (buCompTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region delete obsolete data
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteObsDataInDb(string pdbId)
        {
            string deleteString = "";
            foreach (DataTable dataTable in EntryBuCompTables.entryBuCompTables)
            {
                deleteString = string.Format("Delete From {0} Where PdbID = '{1}';", dataTable.TableName, pdbId);
                dbQuery.Query(ProtCidSettings.buCompConnection, deleteString);
            }
        }
        #endregion
    }
}
