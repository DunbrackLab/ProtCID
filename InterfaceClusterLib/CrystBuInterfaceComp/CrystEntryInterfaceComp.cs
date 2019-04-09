using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Data;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.StructureComp;
using CrystalInterfaceLib.StructureComp.HomoEntryComp;
using CrystalInterfaceLib.Settings;
using InterfaceClusterLib.DataTables;
using DbLib;
using AuxFuncLib;
using InterfaceClusterLib.InterfaceProcess;


namespace InterfaceClusterLib.InterfaceComp
{
    public class CrystEntryInterfaceComp
    {
        #region member variables
        private InterfaceRetriever interfaceReader = new InterfaceRetriever();
        private DbQuery dbQuery = new DbQuery();
        private DbInsert dbInsert = new DbInsert();
        public Alignments.HomoEntryAlignInfo homoEntryAlignInfo = new Alignments.HomoEntryAlignInfo();
        private SupInterfacesComp interfaceComp = new SupInterfacesComp();
        private StreamWriter compDataWriter = null;
        public StreamWriter nonAlignPairWriter = null;
        #endregion

        public CrystEntryInterfaceComp()
        {
            CrystInterfaceTables.InitializeTables();
        }

        #region compare cryst interfaces for two entries
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryPairs"></param>
        public void CompareCrystInterfaces(string[] entryPairs)
        {
            nonAlignPairWriter = new StreamWriter("EntryPairsCompLog.txt", true);

            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Comparing Interfaces.";
            ProtCidSettings.progressInfo.totalStepNum = entryPairs.Length;
            ProtCidSettings.progressInfo.totalOperationNum = entryPairs.Length;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Comparing interfaces.");

            nonAlignPairWriter.WriteLine("Comparing interfaces. Total# =" + entryPairs.Length);

            string errorMsg = "";
            bool deleteOld = false;
            bool compareNonAlignedPair = true;
          //  string entryPair = "";
           // for (int i = entryPairs.Length - 1; i >= 0; i--)
            foreach (string entryPair in entryPairs)
            {
           //     entryPair = entryPairs[i];
                ProtCidSettings.progressInfo.currentFileName = entryPair;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                nonAlignPairWriter.WriteLine(entryPair + " " + ProtCidSettings.progressInfo.currentOperationNum);

                string[] fields = ParseHelper.SplitPlus(entryPair, ' ');
                try
                {
                    errorMsg = CompareCrystInterfaces(fields[0], fields[1], deleteOld, compareNonAlignedPair);
                }
                catch (Exception ex)
                {
                    nonAlignPairWriter.WriteLine(entryPair + ": " + ex.Message);
                    nonAlignPairWriter.Flush();
                }
                if (errorMsg != "")
                {
                    nonAlignPairWriter.WriteLine(errorMsg);
                    nonAlignPairWriter.Flush();
                }
            }
            try
            {
                Directory.Delete(ProtCidSettings.tempDir, true);
            }
            catch { }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            nonAlignPairWriter.WriteLine("Done!");
            nonAlignPairWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryPairs"></param>
        public void CompareCrystInterfacesWithErrors(string pdbId)
        {
            CrystInterfaceTables.InitializeTables();
            interfaceReader = new InterfaceRetriever();
            nonAlignPairWriter = new StreamWriter("EntryPairsCompLog.txt");

            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Computing Interfaces.";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Computing interfaces from crystal structures.");

            string queryString = string.Format("Select Distinct PdbID1, PdbID2 From DifEntryInterfaceComp " +
                " Where PdbID1 = '{0}' OR PdbID2 = '{0}';", pdbId);
            DataTable entryPairTable = ProtCidSettings.protcidQuery.Query( queryString);

            ProtCidSettings.progressInfo.totalStepNum = entryPairTable.Rows.Count;
            ProtCidSettings.progressInfo.totalOperationNum = entryPairTable.Rows.Count;

            string pdbId1 = "";
            string pdbId2 = "";
            bool deleteOld = true;
            bool compareNonAlignedPair = false;
            foreach (DataRow entryPairRow in entryPairTable.Rows)
            {
                pdbId1 = entryPairRow["PdbID1"].ToString();
                pdbId2 = entryPairRow["PdbID2"].ToString();

                ProtCidSettings.progressInfo.currentFileName = pdbId1 + "  " + pdbId2;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    CompareCrystInterfaces(pdbId1, pdbId2, deleteOld, compareNonAlignedPair);
                }
                catch (Exception ex)
                {
                    nonAlignPairWriter.WriteLine(pdbId1 + "  " + pdbId2 + ": " + ex.Message);
                    nonAlignPairWriter.Flush();
                }
            }
            Directory.Delete(ProtCidSettings.tempDir, true);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            nonAlignPairWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        public string CompareCrystInterfaces(string pdbId1, string pdbId2, bool deleteOld, bool compareNonAlignedPair)
        {
            if (string.Compare(pdbId1, pdbId2) > 0)
            {
                string temp = pdbId1;
                pdbId1 = pdbId2;
                pdbId2 = temp;
            }
            string errorMsg = "";

            bool difEntryCompExist = IsDifEntryCompExist(pdbId1, pdbId2);
            if (difEntryCompExist && (! deleteOld))
            {
                return "";
            }

            DataTable alignInfoTable = homoEntryAlignInfo.GetEntryAlignmentInfoFromAlignmentsTables(pdbId1, pdbId2);
            //           DataTable alignInfoTable = homoEntryAlignInfo.GetHomoEntryAlignInfo(pdbId1, pdbId2);
            DataTable subAlignInfoTable = homoEntryAlignInfo.SelectSubTable(alignInfoTable, pdbId1, pdbId2);

            bool alignmentExist = AreTwoEntryAlignmentsExist(subAlignInfoTable);

            if (!alignmentExist)
            {
                errorMsg = pdbId1 + "   " + pdbId2 + ": not aligned pair.";
            }
            if (difEntryCompExist)
            {
                if (deleteOld && alignmentExist)
                {
                    DeleteEntryInterfaceCompData(pdbId1, pdbId2);
                }
                else
                {
                    return errorMsg;
                }
            }

            int[] interfaceIds1 = GetInterfacesWithOrigAsymIDs(pdbId1);
            InterfaceChains[] crystInterfaces1 = interfaceReader.GetCrystInterfaces(pdbId1, interfaceIds1, "cryst");
            int[] interfaceIds2 = GetInterfacesWithOrigAsymIDs(pdbId2);
            InterfaceChains[] crystInterfaces2 = interfaceReader.GetCrystInterfaces(pdbId2, interfaceIds2, "cryst");
            if (crystInterfaces1 == null || crystInterfaces2 == null)
            {
                if (crystInterfaces1 == null)
                {
                    errorMsg = pdbId1 + "   " + pdbId2 + ": " + pdbId1 + " no cryst interfaces.";
                }
                else
                {
                    errorMsg = pdbId1 + "   " + pdbId2 + ": " + pdbId2 + " no cryst interfaces.";
                }
                return errorMsg;
            }

            if (alignmentExist || compareNonAlignedPair)
            {
                InterfacePairInfo[] compInfos = interfaceComp.CompareSupStructures(crystInterfaces1, crystInterfaces2, subAlignInfoTable);

                foreach (InterfacePairInfo compInfo in compInfos)
                {
                    if (compInfo.qScore >= AppSettings.parameters.contactParams.minQScore)
                    {
                        DataRow dataRow = CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].NewRow();
                        dataRow["PdbID1"] = pdbId1;
                        dataRow["InterfaceID1"] = compInfo.interfaceInfo1.interfaceId;
                        dataRow["PdbID2"] = pdbId2;
                        dataRow["InterfaceID2"] = compInfo.interfaceInfo2.interfaceId;
                        dataRow["Qscore"] = compInfo.qScore;
                        dataRow["Identity"] = compInfo.identity;
                        if (compInfo.isInterface2Reversed)
                        {
                            dataRow["IsReversed"] = 1;
                        }
                        else
                        {
                            dataRow["IsReversed"] = 0;
                        }
                        CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].Rows.Add(dataRow);
                    }
                }
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, 
                    CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp]);
                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].Clear();
            }
            
            return errorMsg;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="entryInterfaceHash"></param>
        /// <returns></returns>
        public string CompareCrystInterfaces(string pdbId1, string pdbId2, ref Dictionary<string, InterfaceChains[]> entryInterfaceHash)
        {
            if (IsDifEntryCompExist(pdbId1, pdbId2))
            {
            //    DeleteEntryInterfaceCompData(pdbId1, pdbId2);
                return "";
            }
            if (string.Compare(pdbId1, pdbId2) > 0)
            {
                string temp = pdbId1;
                pdbId1 = pdbId2;
                pdbId2 = temp;
            }
            string errorMsg = "";
            InterfaceChains[] crystInterfaces1 = null;
            if (entryInterfaceHash.ContainsKey(pdbId1))
            {
                crystInterfaces1 = (InterfaceChains[])entryInterfaceHash[pdbId1];
            }
            else
            {
                int[] interfaceIds1 = GetInterfacesWithOrigAsymIDs(pdbId1);
                crystInterfaces1 = interfaceReader.GetCrystInterfaces(pdbId1, interfaceIds1, "cryst");
                entryInterfaceHash.Add(pdbId1, crystInterfaces1);
            }
            InterfaceChains[] crystInterfaces2 = null;
            if (entryInterfaceHash.ContainsKey(pdbId2))
            {
                crystInterfaces2 = (InterfaceChains[])entryInterfaceHash[pdbId2];
            }
            else
            {
                int[] interfaceIds2 = GetInterfacesWithOrigAsymIDs(pdbId2);
                crystInterfaces2 = interfaceReader.GetCrystInterfaces(pdbId2, interfaceIds2, "cryst");
                entryInterfaceHash.Add(pdbId2, crystInterfaces2);
            }
            if (crystInterfaces1 == null || crystInterfaces2 == null)
            {
                if (crystInterfaces1 == null)
                {
                    errorMsg = pdbId1 + "   " + pdbId2 + ": " + pdbId1 + " no cryst interfaces.";
                }
                else
                {
                    errorMsg = pdbId1 + "   " + pdbId2 + ": " + pdbId2 + " no cryst interfaces.";
                }
                return errorMsg;
            }

            DataTable alignInfoTable = homoEntryAlignInfo.GetEntryAlignmentInfoFromAlignmentsTables(pdbId1, pdbId2);
            //           DataTable alignInfoTable = homoEntryAlignInfo.GetHomoEntryAlignInfo(pdbId1, pdbId2);
            DataTable subAlignInfoTable = homoEntryAlignInfo.SelectSubTable(alignInfoTable, pdbId1, pdbId2);

            bool alignmentExist = AreTwoEntryAlignmentsExist(subAlignInfoTable);
         //   if (subAlignInfoTable.Rows.Count > 0)
            if (alignmentExist)
            {
                //    subAlignInfoTable.Clear();
                InterfacePairInfo[] compInfos = interfaceComp.CompareSupStructures(crystInterfaces1, crystInterfaces2, subAlignInfoTable);

                foreach (InterfacePairInfo compInfo in compInfos)
                {
                    if (compInfo.qScore >= AppSettings.parameters.contactParams.minQScore)
                    {
                        DataRow dataRow = CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].NewRow();
                        dataRow["PdbID1"] = pdbId1;
                        dataRow["InterfaceID1"] = compInfo.interfaceInfo1.interfaceId;
                        dataRow["PdbID2"] = pdbId2;
                        dataRow["InterfaceID2"] = compInfo.interfaceInfo2.interfaceId;
                        dataRow["Qscore"] = compInfo.qScore;
                        dataRow["Identity"] = compInfo.identity;
                        if (compInfo.isInterface2Reversed)
                        {
                            dataRow["IsReversed"] = 1;
                        }
                        else
                        {
                            dataRow["IsReversed"] = 0;
                        }
                        CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].Rows.Add(dataRow);
                    }
                }

                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, 
                    CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp]);
                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].Clear();
            }
            else
            {
               errorMsg = pdbId1 + "   " + pdbId2 + ": not aligned pair.";
            }
            return errorMsg;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetInterfacesWithOrigAsymIDs(string pdbId)
        {
            string queryString = string.Format("Select InterfaceID, AsymChain1, AsymChain2 From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable entryInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<int> interfaceIdList = new List<int> ();
            foreach (DataRow interfaceRow in entryInterfaceTable.Rows)
            {
                if (IsOrigAsymChain(interfaceRow["AsymChain1"].ToString()) &&
                    IsOrigAsymChain(interfaceRow["AsymChain2"].ToString()))
                {
                    interfaceIdList.Add(Convert.ToInt32(interfaceRow["InterfaceID"].ToString()));
                }
            }
            return interfaceIdList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private bool IsOrigAsymChain(string asymChain)
        {
            foreach (char ch in asymChain)
            {
                if (char.IsDigit(ch))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignInfoTable"></param>
        /// <returns></returns>
        private bool AreTwoEntryAlignmentsExist(DataTable alignInfoTable)
        {
            if (alignInfoTable.Rows.Count == 0)
            {
                return false;
            }
            int queryStart = 0;
            foreach (DataRow alignInfoRow in alignInfoTable.Rows)
            {
                queryStart = Convert.ToInt32(alignInfoRow["QueryStart"].ToString ());
                // at least one entity with alignments, most likely just because other entities cannot be aligned
                if (queryStart > -1) 
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
        public bool IsDifEntryCompExist(string pdbId1, string pdbId2)
        {
            string queryString = string.Format("Select * From DifEntryInterfaceComp " + 
                " Where (PdbID1 = '{0}' AND PdbID2 = '{1}') OR (PdbID1 = '{1}' AND PdbID2 = '{0}');", pdbId1, pdbId2);
            DataTable difEntryCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (difEntryCompTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        private void DeleteEntryInterfaceCompData(string pdbId1, string pdbId2)
        {
            string deleteString = string.Format("Delete From DifEntryInterfaceComp " + 
                " Where (PdbID1 = '{0}' AND PdbID2 = '{1}') OR " + 
                " (PdbID1 = '{1}' AND PdbID2 = '{0}');", pdbId1, pdbId2);
            ProtCidSettings.protcidQuery.Query( deleteString);
        }
        #endregion

        #region interface pairs in previous DB
        /// <summary>
        /// interfaces comparison info between two pdb entries
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="tableNum"></param>
        public InterfacePairInfo[] GetInterfaceCompPairsInfoFromDb(string pdbId1, string pdbId2, int tableNum)
        {
            bool isReversed = false;
            string tableName = CrystInterfaceTables.crystInterfaceTables[tableNum].TableName;

            string queryString = string.Format("SELECT * From CrystEntryInterfaces WHERE PDBID = '{0}'", pdbId2);
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (interfaceTable.Rows.Count == 0) // if pdbid2 does not have interfaces
            {
                queryString = string.Format("DELETE FROM {0} WHERE PdbID1 = '{1}' AND PdbID2 = '{2}';",
                    tableName, pdbId1, pdbId2);
                ProtCidSettings.protcidQuery.Query( queryString);

                return null;
            }

            queryString = string.Format("SELECT * FROM {0} WHERE PdbID1 = '{1}' AND PdbID2 = '{2}';",
                tableName, pdbId1, pdbId2);
            DataTable dbInterfacePairTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (dbInterfacePairTable.Rows.Count == 0)
            {
                queryString = string.Format("SELECT * FROM {0} WHERE PdbID1 = '{1}' AND PdbID2 = '{2}';",
                    tableName, pdbId2, pdbId1);
                dbInterfacePairTable = ProtCidSettings.protcidQuery.Query( queryString);
                if (dbInterfacePairTable.Rows.Count == 0)
                {
                    return null;
                }
                isReversed = true;
            }
            InterfacePairInfo[] pairInfoList = new InterfacePairInfo[dbInterfacePairTable.Rows.Count];
            int count = 0;
            foreach (DataRow pairInfoRow in dbInterfacePairTable.Rows)
            {
                InterfacePairInfo pairInfo = new InterfacePairInfo();
                if (isReversed)
                {
                    pairInfo.interfaceInfo1.interfaceId =
                        Convert.ToInt32(pairInfoRow["InterfaceID2"].ToString());
                    pairInfo.interfaceInfo2.interfaceId =
                        Convert.ToInt32(pairInfoRow["InterfaceID1"].ToString());
                }
                else
                {
                    pairInfo.interfaceInfo1.interfaceId =
                        Convert.ToInt32(pairInfoRow["InterfaceID1"].ToString());
                    pairInfo.interfaceInfo2.interfaceId =
                        Convert.ToInt32(pairInfoRow["InterfaceID2"].ToString());
                }
                pairInfo.qScore = Convert.ToDouble(pairInfoRow["Qscore"].ToString());
                pairInfo.identity = Convert.ToDouble(pairInfoRow["Identity"].ToString());
                if (pairInfoRow["IsReversed"].ToString () == "1")
                {
                    pairInfo.isInterface2Reversed = true;
                }
                else
                {
                    pairInfo.isInterface2Reversed = false;
                }
                pairInfoList[count] = pairInfo;
                count++;
            }
            // if the entries in the db reversed, reverse them
            if (isReversed)
            {
                string deleteString = string.Format("DELETE FROM {0} WHERE PdbID1 = '{1}' AND PdbID2 = '{2}';",
                    tableName, pdbId1, pdbId2);
                ProtCidSettings.protcidQuery.Query( deleteString);
                AssignInterfacePairToTable(pairInfoList, pdbId1, pdbId2, tableNum);
            }
            return pairInfoList;
        }
        #endregion

        #region Compare dif entries in groups
        /// <summary>
        /// 
        /// </summary>
        public void CompareGroupDifEntryInterfaces()
        {
            /* [1, 19]: 7237
             * [20, 29]: 147
             * [30, 49]: 114
             * [50, 89]: 73
             * [90, 200]: 35
             * [205, 300]: 7
             * [300, 400]: 1
             * 
             * */
            int minEntryCount = 250;
            int maxEntryCount = 300;

            CrystInterfaceTables.InitializeTables();
            interfaceReader = new InterfaceRetriever();
            nonAlignPairWriter = new StreamWriter("NonAlignedEntryPairs_" + minEntryCount.ToString () + "Less" + maxEntryCount.ToString () + ".txt", true);
           
            compDataWriter = new StreamWriter("DifEntryInterfaceComp_" + minEntryCount.ToString () + "Less" + maxEntryCount.ToString () + ".txt", true);

            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Computing Interfaces.";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Computing interfaces from crystal structures.");

            string queryString = string.Format("Select GroupSeqID, count(distinct PdbID) As EntryCount From {0} Group By GroupSeqID;",
                DataTables.GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo]);
            DataTable groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int groupId = -1;
            int entryCount = 0;

            foreach (DataRow groupIdRow in groupIdTable.Rows)
            {
                groupId = Convert.ToInt32(groupIdRow["GroupSeqID"].ToString ());
                entryCount = Convert.ToInt32(groupIdRow["EntryCount"].ToString ());
                if (entryCount >= minEntryCount && entryCount < maxEntryCount)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(groupId.ToString());
                   
                    CompareEntryInterfacesInGroup(groupId);
                }
            }
            Directory.Delete(ProtCidSettings.tempDir, true);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            nonAlignPairWriter.Close();
            compDataWriter.Close();

            ProtCidSettings.progressInfo.threadFinished = true;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        public void CompareEntryInterfacesInGroup(int groupId)
        {
            string[] groupRepEntries = GetGroupRepEntries(groupId);
            Dictionary<string, List<string>> groupRepHomoEntryHash = GetGroupHomoEntries(groupId);
            if (groupRepEntries.Length == 0)
            {
                return;
            }

            ProtCidSettings.progressInfo.totalStepNum = groupRepEntries.Length;
            ProtCidSettings.progressInfo.totalOperationNum = groupRepEntries.Length;
            ProtCidSettings.progressInfo.currentOperationNum = 0;
            ProtCidSettings.progressInfo.currentStepNum = 0;

            Dictionary<string, InterfaceChains[]> entryInterfacesHash = new Dictionary<string,InterfaceChains[]> ();
            string repEntry = "";
            string[] entriesToBeCompared = null;
            // the alignments between representative entries in the group
            DataTable repEntryAlignmentInfoTable = homoEntryAlignInfo.GetGroupRepAlignInfoTable(groupRepEntries, groupId);
            for (int i = 0; i < groupRepEntries.Length; i++)
            {
                repEntry = groupRepEntries[i];
               
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentFileName = repEntry;
                
                if (i < groupRepEntries.Length - 1)
                {
                    entriesToBeCompared = new string[groupRepEntries.Length - i - 1];
                    Array.Copy(groupRepEntries, i + 1, entriesToBeCompared, 0, groupRepEntries.Length - i - 1);
                    CompareEntryInterfaces(groupId, repEntry, entriesToBeCompared, entryInterfacesHash, repEntryAlignmentInfoTable);
                }

                if (groupRepHomoEntryHash.ContainsKey(repEntry))
                {
                    string[] homoEntries = groupRepHomoEntryHash[repEntry].ToArray();
                    CompareRepHomoEntryInterfacesInGroup(groupId, repEntry, homoEntries, entryInterfacesHash);
                }
            }
        }
     
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private string[] GetGroupRepEntries(int groupId)
        {
            string queryString = string.Format("Select * From {0} Where GroupSeqID = {1};",
                DataTables.GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo], groupId);
            DataTable groupEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> groupRepEntryList = new List<string> ();
            string spaceGroup = "";
            string asuString = "";
            string asu = "";
            foreach (DataRow repEntryRow in groupEntryTable.Rows)
            {
                spaceGroup = repEntryRow["SpaceGroup"].ToString().TrimEnd();
                if (spaceGroup == "NMR")
                {
                    asuString = repEntryRow["ASU"].ToString().TrimEnd();
                    asu = GetRealAsu(asuString);
                    if (asu == "A")
                    {
                        continue;
                    }
                }
                groupRepEntryList.Add(repEntryRow["PdbID"].ToString ());
            }
            groupRepEntryList.Sort();
            string[] groupRepEntries = new string[groupRepEntryList.Count];
            groupRepEntryList.CopyTo(groupRepEntries);
            return groupRepEntries;
        }

        private string GetRealAsu(string asuString)
        {
            string asu = "";
            int asuIndex = asuString.IndexOf("(");
            if (asuIndex > 0)
            {
                asu = asuString.Substring(0, asuIndex);
            }
            else
            {
                asu = asuString;
            }
            return asu;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetGroupHomoEntries(int groupId)
        {
            string queryString = string.Format("Select Distinct PdbID1, PdbID2 From {0} Where GroupSeqID = {1};",
                DataTables.GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoRepEntryAlign], groupId);
            DataTable repHomoEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            Dictionary<string, List<string>> repHomoEntryHash = new Dictionary<string,List<string>> ();
            string repEntry = "";
            foreach (DataRow repHomoEntryRow in repHomoEntryTable.Rows)
            {
                repEntry = repHomoEntryRow["PdbID1"].ToString();
                if (repHomoEntryHash.ContainsKey(repEntry))
                {
                    repHomoEntryHash[repEntry].Add(repHomoEntryRow["PdbID2"].ToString());
                }
                else
                {
                    List<string> homoEntryList = new List<string> ();
                    homoEntryList.Add(repHomoEntryRow["PdbID2"].ToString ());
                    repHomoEntryHash.Add(repEntry, homoEntryList);
                }
            }
            return repHomoEntryHash;
        }
        #endregion

        #region compare one entry with other entries
    /*    public void CompareRepHomoEntryInterfaces(string repEntry, string[] homoEntries, bool isUpdate)
        {
            InterfaceChains[] repInterfaces = interfaceReader.GetCrystInterfaces(repEntry, "cryst");
            foreach (string homoEntry in homoEntries)
            {
                InterfaceChains[] homoInterfaces = interfaceReader.GetCrystInterfaces(homoEntry, "cryst");
            }
        }*/
        /// <summary>
        /// Compare interfaces between two entries
        /// </summary>
        /// <param name="sgInterfacesHash"></param>
        /// <returns></returns>
        public void CompareRepHomoEntryInterfacesInGroup(string repEntry, string[] homologyList, Dictionary<string,  InterfaceChains[]> entryInterfacesHash)
        {
            string queryString = string.Format("Select * FROM {0} WHERE PdbID = '{1}';",
                DataTables.GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo], repEntry);
            DataTable groupTable = ProtCidSettings.protcidQuery.Query( queryString);
            int groupId = Convert.ToInt32(groupTable.Rows[0]["GroupSeqID"].ToString());
            // get alignment information from database	
            DataTable homoEntryAlignTable = homoEntryAlignInfo.GetHomoEntryAlignInfo(groupId, repEntry);

            CompareEntryInterfaces(groupId, repEntry, homologyList, entryInterfacesHash, homoEntryAlignTable);
        }

        /// <summary>
        /// Compare interfaces between two entries
        /// </summary>
        /// <param name="sgInterfacesHash"></param>
        /// <returns></returns>
        public void CompareRepHomoEntryInterfacesInGroup(int groupId, string repEntry, string[] homologyList, Dictionary<string, InterfaceChains[]> entryInterfacesHash)
        {
            // get alignment information from database	
            DataTable homoEntryAlignTable = homoEntryAlignInfo.GetHomoEntryAlignInfo(groupId, repEntry);

            CompareEntryInterfaces(groupId, repEntry, homologyList, entryInterfacesHash, homoEntryAlignTable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="repEntry"></param>
        /// <param name="entriesToBeCompared"></param>
        /// <param name="entryInterfacesHash"></param>
        /// <param name="alignmentInfoTable"></param>
        private void CompareEntryInterfaces(int groupId, string repEntry, string[] entriesToBeCompared, Dictionary<string, InterfaceChains[]> entryInterfacesHash, DataTable alignmentInfoTable)
        {
            if (entriesToBeCompared.Length == 0)
            {
                return;
            }
            if (!entryInterfacesHash.ContainsKey(repEntry))
            {
                InterfaceChains[] crystInterfaces = interfaceReader.GetCrystInterfaces(repEntry, "cryst");
                entryInterfacesHash.Add(repEntry, crystInterfaces);
            }
            InterfaceChains[] repInterfaces = (InterfaceChains[])entryInterfacesHash[repEntry];
            if (repInterfaces == null)
            {
                return;
            }
            foreach (string pdbId in entriesToBeCompared)
            {
                ProtCidSettings.progressInfo.currentFileName = repEntry + "_" + pdbId;

                if (!entryInterfacesHash.ContainsKey(pdbId))
                {
                    InterfaceChains[] crystInterfaces = interfaceReader.GetCrystInterfaces(pdbId, "cryst");
                    entryInterfacesHash.Add(pdbId, crystInterfaces);
                }
                InterfaceChains[] thisEntryInterfaceList = (InterfaceChains[])entryInterfacesHash[pdbId];
                if (thisEntryInterfaceList == null)
                {
                    continue;
                }
          //      DataTable entryPairAlignmentInfoTable = homoEntryAlignInfo.SelectAlignInfoTable(alignmentInfoTable, repEntry, pdbId);
                DataTable entryPairAlignmentInfoTable = homoEntryAlignInfo.SelectSubTable (alignmentInfoTable, repEntry, pdbId);
                if (entryPairAlignmentInfoTable.Rows.Count == 0)
                {
                    nonAlignPairWriter.WriteLine(repEntry + "   " + pdbId);
                }
                // compare interfaces in two structures
                InterfacePairInfo[] interfacePairs = null;
                try
                {
                    interfacePairs = interfaceComp.CompareSupStructures
                        (repInterfaces, thisEntryInterfaceList, entryPairAlignmentInfoTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue
                        ("Comparing " + repEntry + " and " + pdbId + " interfaces errors: " + ex.Message);
#if DEBUG
                    ProtCidSettings.logWriter.WriteLine("Comparing " + repEntry + " and " + pdbId + " interfaces errors: " + ex.Message);
#endif
                    continue;
                }
                // store the comparison between repsentative and homologous entries into table
                AssignInterfacePairToTable(interfacePairs, repEntry, pdbId, CrystInterfaceTables.DifEntryInterfaceComp);

                if (compDataWriter != null)
                {
                    WriterDifEntryInterfaceCompDataIntoFile();
                }

                /*       string deleteString = string.Format("DELETE FROM DifEntryInterfaceComp WHERE PdbID1 = '{0}' AND PdbID2 = '{1}';",
                           repEntry, pdbId);
                       dbQuery.Query(deleteString);*/

                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp]);
                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].Clear();
            }
        }
        #endregion

        #region dif entry interface comparison
        /// <summary>
        /// assign interface pairs of an entry into datatables
        /// </summary>
        /// <param name="interfacePairs">interface pairs between 2 entries</param>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        public void AssignInterfacePairToTable(InterfacePairInfo[] interfacePairs, string pdbId1, string pdbId2, int tableNum)
        {
            foreach (InterfacePairInfo thisPairInfo in interfacePairs)
            {
                if (thisPairInfo.qScore > AppSettings.parameters.contactParams.minQScore)
                {
                    // interative chains info
                    DataRow interfaceRow = CrystInterfaceTables.crystInterfaceTables[tableNum].NewRow();
                    interfaceRow["PDBID1"] = pdbId1;
                    interfaceRow["PDBID2"] = pdbId2;
                    interfaceRow["InterfaceID1"] = thisPairInfo.interfaceInfo1.interfaceId;
                    interfaceRow["InterfaceID2"] = thisPairInfo.interfaceInfo2.interfaceId;
                    interfaceRow["QScore"] = thisPairInfo.qScore;
                    interfaceRow["Identity"] = thisPairInfo.identity;
                    if (thisPairInfo.isInterface2Reversed)
                    {
                        interfaceRow["IsReversed"] = 1;
                    }
                    else
                    {
                        interfaceRow["IsReversed"] = 0;
                    }
                    CrystInterfaceTables.crystInterfaceTables[tableNum].Rows.Add(interfaceRow);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void WriterDifEntryInterfaceCompDataIntoFile()
        {
            string dataLine = "";
            foreach (DataRow dataRow in CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].Rows)
            {
                dataLine = "";
                foreach (object item in dataRow.ItemArray)
                {
                    dataLine += (item.ToString() + "\t");
                }
                compDataWriter.WriteLine(dataLine.TrimEnd ('\t'));
            }
            compDataWriter.Flush();
        }
        #endregion

        #region interface comp for input entries
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryPairs"></param>
        public void UpdateCrystInterfacesComp(string[] entries)
        {
            nonAlignPairWriter = new StreamWriter("EntryPairsCompLog.txt", true);

            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Computing Interfaces.";

            int totalNumOfEntryPairs = 0;
            Dictionary<string, List<string>> entryToBeCompHash = GetEntriesToBeCompared(entries, out totalNumOfEntryPairs);
            ProtCidSettings.progressInfo.totalStepNum = totalNumOfEntryPairs;
            ProtCidSettings.progressInfo.totalOperationNum = totalNumOfEntryPairs;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Computing interfaces from crystal structures.");

            foreach (string entry in entries)
            {
                List<string> entryToBeComparedList = entryToBeCompHash[entry];
                string[] entriesToBeComp = new string[entryToBeComparedList.Count];
                entryToBeComparedList.CopyTo (entriesToBeComp);
                CompareCrystInterfaces(entry, entriesToBeComp);
            }
            Directory.Delete(ProtCidSettings.tempDir, true);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            nonAlignPairWriter.Close();

            ProtCidSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="entriesToBeComp"></param>
        public void CompareCrystInterfaces(string entry, string[] entriesToBeComp)
        {
            string errorMsg = "";
            string entryPair = "";

            int[] interfaceIds = GetInterfacesWithOrigAsymIDs(entry);
            InterfaceChains[] crystInterfaces = interfaceReader.GetCrystInterfaces(entry, interfaceIds, "cryst");

            foreach (string compEntry in entriesToBeComp)
            {
                entryPair = entry + "_" + compEntry;
                ProtCidSettings.progressInfo.currentFileName = entryPair;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    errorMsg = CompareCrystInterfaces(entry, compEntry, crystInterfaces, true);
                }
                catch (Exception ex)
                {
                    nonAlignPairWriter.WriteLine(entryPair + ": " + ex.Message);
                    nonAlignPairWriter.Flush();
                }
                if (errorMsg != "")
                {
                    nonAlignPairWriter.WriteLine(errorMsg);
                    nonAlignPairWriter.Flush();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="crystInterfaces1"></param>
        /// <param name="deleteOld"></param>
        /// <returns></returns>
        public string CompareCrystInterfaces(string pdbId1, string pdbId2, 
            InterfaceChains[] crystInterfaces1, bool deleteOld)
        {
            if (IsDifEntryCompExist(pdbId1, pdbId2))
            {
                if (deleteOld)
                {
                    DeleteEntryInterfaceCompData(pdbId1, pdbId2);
                }
                else
                {
                    return "";
                }
            }
            int[] interfaceIds2 = GetInterfacesWithOrigAsymIDs(pdbId2);
            InterfaceChains[] crystInterfaces2 = interfaceReader.GetCrystInterfaces(pdbId2, interfaceIds2, "cryst");
            if (string.Compare(pdbId1, pdbId2) > 0)
            {
                string temp = pdbId1;
                pdbId1 = pdbId2;
                pdbId2 = temp;
                InterfaceChains[] tempInterfaces = crystInterfaces1;
                crystInterfaces1 = crystInterfaces2;
                crystInterfaces2 = tempInterfaces;
            }
            string errorMsg = "";
            if (crystInterfaces1 == null || crystInterfaces2 == null)
            {
                if (crystInterfaces1 == null)
                {
                    errorMsg = pdbId1 + "   " + pdbId2 + ": " + pdbId1 + " no cryst interfaces.";
                }
                else
                {
                    errorMsg = pdbId1 + "   " + pdbId2 + ": " + pdbId2 + " no cryst interfaces.";
                }
                return errorMsg;
            }

            DataTable alignInfoTable = homoEntryAlignInfo.GetEntryAlignmentInfoFromAlignmentsTables(pdbId1, pdbId2);
            //           DataTable alignInfoTable = homoEntryAlignInfo.GetHomoEntryAlignInfo(pdbId1, pdbId2);
            DataTable subAlignInfoTable = homoEntryAlignInfo.SelectSubTable(alignInfoTable, pdbId1, pdbId2);

            bool alignmentExist = AreTwoEntryAlignmentsExist(subAlignInfoTable);
            if (alignmentExist)
            //        if (subAlignInfoTable.Rows.Count > 0)
            {
                //    subAlignInfoTable.Clear();
                InterfacePairInfo[] compInfos = interfaceComp.CompareSupStructures(crystInterfaces1, crystInterfaces2, subAlignInfoTable);

                foreach (InterfacePairInfo compInfo in compInfos)
                {
                    if (compInfo.qScore >= AppSettings.parameters.contactParams.minQScore)
                    {
                        DataRow dataRow = CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].NewRow();
                        dataRow["PdbID1"] = pdbId1;
                        dataRow["InterfaceID1"] = compInfo.interfaceInfo1.interfaceId;
                        dataRow["PdbID2"] = pdbId2;
                        dataRow["InterfaceID2"] = compInfo.interfaceInfo2.interfaceId;
                        dataRow["Qscore"] = compInfo.qScore;
                        dataRow["Identity"] = compInfo.identity;
                        if (compInfo.isInterface2Reversed)
                        {
                            dataRow["IsReversed"] = 1;
                        }
                        else
                        {
                            dataRow["IsReversed"] = 0;
                        }
                        CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].Rows.Add(dataRow);
                    }
                }
                /*     string deleteString = string.Format("Delete From {0} Where (PdbID1 = '{1}' AND PdbID2 = '{2}') " +
                         " OR (PdbID1 = '{2}' AND PdbID2 = '{1}');",
                         CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].TableName,
                         pdbId1, pdbId2);
                     dbQuery.Query(deleteString);*/

                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp]);
                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].Clear();
            }
            else
            {
                errorMsg = pdbId1 + "   " + pdbId2 + ": not aligned pair.";
            }
            return errorMsg;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <param name="totalEntryPair"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetEntriesToBeCompared(string[] entries, out int totalEntryPair)
        {
            string queryString = "";
            Dictionary<string, List<string>> entryToBeCompHash = new Dictionary<string, List<string>>();
            totalEntryPair = 0;
            string compEntry = "";
            foreach (string entry in entries)
            {
                List<string> entryToBeComparedList = new List<string> ();

                queryString = string.Format("Select Distinct PdbID2 From DifEntryInterfaceComp " +
                    " Where PdbID1 = '{0}';", entry);
                DataTable entryCompTable = ProtCidSettings.protcidQuery.Query( queryString);
                foreach (DataRow entryRow in entryCompTable.Rows)
                {
                    compEntry = entryRow["PdbID2"].ToString();
                    if (string.Compare(entry, compEntry) <= 0)
                    {
                        continue;
                    }
                    entryToBeComparedList.Add(compEntry);
                }

                queryString = string.Format("Select Distinct PdbID1 From DifEntryInterfaceComp " +
                    " Where PdbID2 = '{0}';", entry);
                entryCompTable = ProtCidSettings.protcidQuery.Query( queryString);
                foreach (DataRow entryRow in entryCompTable.Rows)
                {
                    compEntry = entryRow["PdbID1"].ToString();
                    if (string.Compare(entry, compEntry) <= 0)
                    {
                        continue;
                    }
                    entryToBeComparedList.Add(compEntry);
                }
                totalEntryPair += entryToBeComparedList.Count;

                entryToBeCompHash.Add(entry, entryToBeComparedList);
            }
            return entryToBeCompHash;
        }
        #endregion

        #region compare interface qscores for the chain order
   /*     public void TestInterfaceComp()
        {
            string interface1 = "1ybd11";
            string interface2 = "3glf3";

            string pdbId1 = interface1.Substring(0, 4);
            int[] interfaceIds1 = new int[1];
            interfaceIds1[0] = Convert.ToInt32(interface1.Substring (4, interface1.Length - 4));
            InterfaceChains[] interfaceChains1 = interfaceReader.GetCrystInterfaces(pdbId1, interfaceIds1, "cryst");
            string pdbId2 = interface2.Substring(0, 4);
            int[] interfaceIds2 = new int[1];
            interfaceIds2[0] = Convert.ToInt32(interface2.Substring (4, interface2.Length - 4));
            InterfaceChains[] interfaceChains2 = interfaceReader.GetCrystInterfaces(pdbId2, interfaceIds2, "cryst");
            bool alignmentExist = true; 
            DataTable alignTable = GetAlignmentTable (pdbId1, pdbId2, out alignmentExist);
            InterfacePairInfo compInfo = interfaceComp.CompareSupStructures
                                            (interfaceChains1[0], interfaceChains2[0], alignTable);
        }*/

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterNonSymInterfaceFile"></param>
        /// <returns></returns>
        public string CompareClusterNonSymInterfaces(string clusterNonSymInterfaceFile)
        {
       //     StreamWriter dataWriter = new StreamWriter("ClusterHomoDimerQscores.txt");
            string clusterNonSymInterfaceCompFile = "ClusterNonSymInterfaceComp.txt";
            StreamWriter dataWriter = new StreamWriter(clusterNonSymInterfaceCompFile);
            List<string> lineList = ReadClusterLines(clusterNonSymInterfaceFile);
            string repInterface = "";
            string repPdb = "";
            string homoPdb = "";
            bool alignmentExist = false;
            string dataLine = "";
            InterfacePairInfo compInfo = null;
            DataTable alignTable = null;
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = lineList.Count;
            ProtCidSettings.progressInfo.totalStepNum = lineList.Count;

            foreach (string line in lineList)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                string[] fields = ParseHelper.SplitPlus(line, ' ');
                ProtCidSettings.progressInfo.currentFileName = fields[0] + "_" + fields[1];

                string[] clusterInterfaces = fields[2].Split(',');
                if (clusterInterfaces.Length < 2)
                {
                    continue;
                }
                dataWriter.WriteLine(fields[0] + "  " + fields[1]);
                repInterface = clusterInterfaces[0];
                repPdb = repInterface.Substring(0, 4);
                int[] repInterfaceIds = new int[1];
                repInterfaceIds[0] = Convert.ToInt32 (repInterface.Substring (4, repInterface.Length - 4));
                InterfaceChains[] repInterfaceChains = interfaceReader.GetCrystInterfaces (repPdb,  repInterfaceIds, "cryst");
                foreach (string clusterInterface in clusterInterfaces)
                {
                    if (repInterface == clusterInterface)
                    {
                        continue;
                    }
                    homoPdb = clusterInterface.Substring(0, 4);
                    int[] homoInterfaceIds = new int[1];
                    homoInterfaceIds[0] = Convert.ToInt32(clusterInterface.Substring(4, clusterInterface.Length - 4));
                    InterfaceChains[] homoInterfaceChains = interfaceReader.GetCrystInterfaces(homoPdb, homoInterfaceIds, "cryst");
                    try
                    {
                        alignTable = GetAlignmentTable(repPdb, homoPdb, out alignmentExist);
                    }
                    catch (Exception ex)
                    {
                        dataWriter.WriteLine("Retrieve alignment error: " + ex.Message);
                        continue;
                    }

                    try
                    {
                        compInfo = interfaceComp.CompareSupStructures(repInterfaceChains[0],
                            homoInterfaceChains[0], alignTable);
                    }
                    catch (Exception ex)
                    {
                        dataWriter.WriteLine("Compare " + repInterface + " " + clusterInterface + " error: " + ex.Message);
                        continue;
                    }
                    UpdateInterfaceCompRow(repPdb, repInterfaceIds[0], homoPdb, homoInterfaceIds[0], compInfo.isInterface2Reversed);
                    dataLine = repInterface + "\t" + clusterInterface + "\t" +
                        compInfo.qScore.ToString() + "\t" + compInfo.identity.ToString() + "\t";
                    if (compInfo.isInterface2Reversed)
                    {
                        dataLine += "1";
                    }
                    else
                    {
                        dataLine += "0";
                    }
                    dataWriter.WriteLine(dataLine);
                    if (!alignmentExist)
                    {
                        dataWriter.WriteLine("No alignments for " + repPdb + "  " + homoPdb);
                    }
                }
                dataWriter.Flush();
            }
            dataWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            return clusterNonSymInterfaceCompFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="interfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="interfaceId2"></param>
        /// <param name="isReversed"></param>
        private void UpdateInterfaceCompRow(string pdbId1, int interfaceId1, string pdbId2, int interfaceId2, bool isReversed)
        {
            string updateString = "";
            if (isReversed)
            {
                updateString = string.Format("Update DifEntryInterfaceComp Set IsReversed = 1 " +
                    " Where (PdbId1 = '{0}' AND InterfaceID1 = {1} AND PdbID2 = '{2}' AND InterfaceID2 = {3}) " +
                    " OR (PdbId1 = '{2}' AND InterfaceID1 = {3} AND PdbID2 = '{0}' AND InterfaceID2 = {1});",
                    pdbId1, interfaceId1, pdbId2, interfaceId2);
            }
            else
            {
                updateString = string.Format("Update DifEntryInterfaceComp Set IsReversed = 0 " +
                    " Where (PdbId1 = '{0}' AND InterfaceID1 = {1} AND PdbID2 = '{2}' AND InterfaceID2 = {3}) " +
                    " OR (PdbId1 = '{2}' AND InterfaceID1 = {3} AND PdbID2 = '{0}' AND InterfaceID2 = {1});",
                    pdbId1, interfaceId1, pdbId2, interfaceId2);
            }
            ProtCidSettings.protcidQuery.Query( updateString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="repInterface"></param>
        /// <param name="interfaceFile2"></param>
        public InterfacePairInfo CompareInterfaceFiles(string repInterface, string clusterInterface)
        {
            InterfacePairInfo compInfo = null;
            string repPdb = repInterface.Substring(0, 4);
            int[] repInterfaceIds = new int[1];
            DataTable alignTable = null;
            bool alignmentExist = false;
            repInterfaceIds[0] = Convert.ToInt32(repInterface.Substring(4, repInterface.Length - 4));
            InterfaceChains[] repInterfaceChains = interfaceReader.GetCrystInterfaces(repPdb, repInterfaceIds, "cryst");

            if (repInterface == clusterInterface)
            {
                return null;
            }
            string homoPdb = clusterInterface.Substring(0, 4);
            int[] homoInterfaceIds = new int[1];
            homoInterfaceIds[0] = Convert.ToInt32(clusterInterface.Substring(4, clusterInterface.Length - 4));
            InterfaceChains[] homoInterfaceChains = interfaceReader.GetCrystInterfaces(homoPdb, homoInterfaceIds, "crsyt");
            try
            {
                alignTable = GetAlignmentTable(repPdb, homoPdb, out alignmentExist);
            }
            catch (Exception ex)
            {
                throw new Exception ("Retrieve alignment error: " + ex.Message);
            }

            try
            {
                compInfo = interfaceComp.CompareSupStructures(repInterfaceChains[0],
                    homoInterfaceChains[0], alignTable);
            }
            catch (Exception ex)
            {
                throw new Exception ("Compare " + repInterface + " " + clusterInterface + " error: " + ex.Message);
            }

            if (!alignmentExist)
            {
                throw new Exception("No alignments for " + repPdb + "  " + homoPdb);
            }
            return compInfo;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="alignmentExist"></param>
        /// <returns></returns>
        private DataTable GetAlignmentTable(string pdbId1, string pdbId2, out bool alignmentExist)
        {
            DataTable alignInfoTable = homoEntryAlignInfo.GetEntryAlignmentInfoFromAlignmentsTables(pdbId1, pdbId2);
            DataTable subAlignInfoTable = homoEntryAlignInfo.SelectSubTable(alignInfoTable, pdbId1, pdbId2);

            alignmentExist = AreTwoEntryAlignmentsExist(subAlignInfoTable);

            return subAlignInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private List<string> ReadClusterLines(string clusterNonSymInterfaceFile)
        {
            string line = "";
        //    StreamReader dataReader = new StreamReader("ClusterHomoDimersSortLog.txt");
            List<string> lineToBeComparedList = new List<string> ();
            StreamReader dataReader = new StreamReader(clusterNonSymInterfaceFile);
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("no non-symmetric homo-dimers") > -1)
                {
                    continue;
                }
                lineToBeComparedList.Add(line);
            }
            dataReader.Close();
            return lineToBeComparedList;
        }
        #endregion

        #region fix prob of weight of Q score
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryPairs"></param>
        public void CompareCrystInterfacesAfterWeightFixed (string[] entryPairs)
        {
            nonAlignPairWriter = new StreamWriter("EntryPairsCompLog", true);
            StreamWriter compDataWriter = new StreamWriter("EntryPairsQscoresAfterWeigtFixed1.txt", true);

            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Comparing Interfaces.";
            ProtCidSettings.progressInfo.totalStepNum = entryPairs.Length;
            ProtCidSettings.progressInfo.totalOperationNum = entryPairs.Length;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Comparing interfaces.");

            string errorMsg = "";
            string prePdbId1 = "";
            foreach (string entryPair in entryPairs)
            {
                //     entryPair = entryPairs[i];
                ProtCidSettings.progressInfo.currentFileName = entryPair;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                string[] fields = ParseHelper.SplitPlus(entryPair, ' ');

                if (prePdbId1 == fields[0])
                {
                    continue;
                }
                prePdbId1 = fields[0];
                try
                {
                    errorMsg = CompareCrystInterfacesAfterWeightFixed(fields[0], fields[1], compDataWriter);
                }
                catch (Exception ex)
                {
                    nonAlignPairWriter.WriteLine(entryPair + ": " + ex.Message);
                    nonAlignPairWriter.Flush();
                }
                if (errorMsg != "")
                {
                    nonAlignPairWriter.WriteLine(errorMsg);
                    nonAlignPairWriter.Flush();
                }
            }
            try
            {
                Directory.Delete(ProtCidSettings.tempDir, true);
            }
            catch { }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            nonAlignPairWriter.Close();
            compDataWriter.Close();
        }
        public string CompareTwoEntriesCrystInterfaces(string pdbId1, string pdbId2, bool deleteOld, bool compareNonAlignedPair)
        {          
            string errorMsg = "";

            bool difEntryCompExist = IsDifEntryCompExist(pdbId1, pdbId2);
            if (difEntryCompExist && (!deleteOld))
            {
                return "";
            }

            DataTable alignInfoTable = homoEntryAlignInfo.GetEntryAlignmentInfoFromAlignmentsTables(pdbId1, pdbId2);
            //           DataTable alignInfoTable = homoEntryAlignInfo.GetHomoEntryAlignInfo(pdbId1, pdbId2);
            DataTable subAlignInfoTable = homoEntryAlignInfo.SelectSubTable(alignInfoTable, pdbId1, pdbId2);

            bool alignmentExist = AreTwoEntryAlignmentsExist(subAlignInfoTable);

            if (!alignmentExist)
            {
                errorMsg = pdbId1 + "   " + pdbId2 + ": not aligned pair.";
            }
            if (difEntryCompExist)
            {
                if (deleteOld && alignmentExist)
                {
                    DeleteEntryInterfaceCompData(pdbId1, pdbId2);
                }
                else
                {
                    return errorMsg;
                }
            }

            int[] interfaceIds1 = GetInterfacesWithOrigAsymIDs(pdbId1);
            InterfaceChains[] crystInterfaces1 = interfaceReader.GetCrystInterfaces(pdbId1, interfaceIds1, "cryst");
            int[] interfaceIds2 = GetInterfacesWithOrigAsymIDs(pdbId2);
            InterfaceChains[] crystInterfaces2 = interfaceReader.GetCrystInterfaces(pdbId2, interfaceIds2, "cryst");
            if (crystInterfaces1 == null || crystInterfaces2 == null)
            {
                if (crystInterfaces1 == null)
                {
                    errorMsg = pdbId1 + "   " + pdbId2 + ": " + pdbId1 + " no cryst interfaces.";
                }
                else
                {
                    errorMsg = pdbId1 + "   " + pdbId2 + ": " + pdbId2 + " no cryst interfaces.";
                }
                return errorMsg;
            }

            if (alignmentExist || compareNonAlignedPair)
            {
                InterfacePairInfo[] compInfos = interfaceComp.CompareSupStructures(crystInterfaces1, crystInterfaces2, subAlignInfoTable);

                foreach (InterfacePairInfo compInfo in compInfos)
                {
                    if (compInfo.qScore >= AppSettings.parameters.contactParams.minQScore)
                    {
                        DataRow dataRow = CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].NewRow();
                        dataRow["PdbID1"] = pdbId1;
                        dataRow["InterfaceID1"] = compInfo.interfaceInfo1.interfaceId;
                        dataRow["PdbID2"] = pdbId2;
                        dataRow["InterfaceID2"] = compInfo.interfaceInfo2.interfaceId;
                        dataRow["Qscore"] = compInfo.qScore;
                        dataRow["Identity"] = compInfo.identity;
                        if (compInfo.isInterface2Reversed)
                        {
                            dataRow["IsReversed"] = 1;
                        }
                        else
                        {
                            dataRow["IsReversed"] = 0;
                        }
                        CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].Rows.Add(dataRow);
                    }
                }
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection,
                    CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp]);
                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].Clear();
            }

            return errorMsg;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        public string CompareCrystInterfacesAfterWeightFixed(string pdbId1, string pdbId2, StreamWriter dataWriter)
        {
            if (string.Compare(pdbId1, pdbId2) > 0)
            {
                string temp = pdbId1;
                pdbId1 = pdbId2;
                pdbId2 = temp;
            }
            string errorMsg = "";

            DataTable alignInfoTable = homoEntryAlignInfo.GetEntryAlignmentInfoFromAlignmentsTables(pdbId1, pdbId2);
            //           DataTable alignInfoTable = homoEntryAlignInfo.GetHomoEntryAlignInfo(pdbId1, pdbId2);
            DataTable subAlignInfoTable = homoEntryAlignInfo.SelectSubTable(alignInfoTable, pdbId1, pdbId2);

            bool alignmentExist = AreTwoEntryAlignmentsExist(subAlignInfoTable);

            if (!alignmentExist)
            {
                errorMsg = pdbId1 + "   " + pdbId2 + ": not aligned pair.";
            }

            int[] interfaceIds1 = GetInterfacesWithOrigAsymIDs(pdbId1);
            InterfaceChains[] crystInterfaces1 = interfaceReader.GetCrystInterfaces(pdbId1, interfaceIds1, "cryst");
            int[] interfaceIds2 = GetInterfacesWithOrigAsymIDs(pdbId2);
            InterfaceChains[] crystInterfaces2 = interfaceReader.GetCrystInterfaces(pdbId2, interfaceIds2, "cryst");
            if (crystInterfaces1 == null || crystInterfaces2 == null)
            {
                if (crystInterfaces1 == null)
                {
                    errorMsg = pdbId1 + "   " + pdbId2 + ": " + pdbId1 + " no cryst interfaces.";
                }
                else
                {
                    errorMsg = pdbId1 + "   " + pdbId2 + ": " + pdbId2 + " no cryst interfaces.";
                }
                return errorMsg;
            }

            InterfacePairInfo[] compInfos = interfaceComp.CompareSupStructures(crystInterfaces1, crystInterfaces2, subAlignInfoTable);

            foreach (InterfacePairInfo compInfo in compInfos)
            {
                if (compInfo.qScore >= AppSettings.parameters.contactParams.minQScore)
                {
                    DataRow dataRow = CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].NewRow();
                    dataRow["PdbID1"] = pdbId1;
                    dataRow["InterfaceID1"] = compInfo.interfaceInfo1.interfaceId;
                    dataRow["PdbID2"] = pdbId2;
                    dataRow["InterfaceID2"] = compInfo.interfaceInfo2.interfaceId;
                    dataRow["Qscore"] = compInfo.qScore;
                    dataRow["Identity"] = compInfo.identity;
                    if (compInfo.isInterface2Reversed)
                    {
                        dataRow["IsReversed"] = 1;
                    }
                    else
                    {
                        dataRow["IsReversed"] = 0;
                    }
                    CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].Rows.Add(dataRow);
                }
            }
  /*          DeleteEntryInterfaceCompData(pdbId1, pdbId2);
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection,
                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp]);*/
            dataWriter.WriteLine(ParseHelper.FormatDataRows(CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].Select()));
            dataWriter.Flush();
            CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].Clear();

            return errorMsg;
        }
        #endregion
    }
}
