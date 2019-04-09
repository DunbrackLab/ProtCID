using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using ProtCidSettingsLib;
using DbLib;
using InterfaceClusterLib.Clustering;
using InterfaceClusterLib.DataTables;
using CrystalInterfaceLib.Settings;
using AuxFuncLib;
using System.IO;

namespace InterfaceClusterLib.DomainInterfaces
{
    public class DomainInterfaceCluster : InterfaceCluster
    {
        #region member variables
        public string tableName = "";
        public string domainInterfaceCompTable = "PfamDomainInterfaceComp";
        // to check if two domain interfaces of one entry is similar or not
        private double entrySimInterfaceQCutoff = 0.75;
        private DbUpdate dbUpdate = new DbUpdate();
        #endregion

        public DomainInterfaceCluster()
        {
            InitializeTable();
            isDomain = true;
            hierarchicalCluster.MergeQCutoff = 0.20;
            GroupDbTableNames.SetGroupDbTableNames(ProtCidSettings.dataType);

            AppSettings.parameters.simInteractParam.interfaceSimCutoff = 0.20;
            surfaceArea = 100.0;
        }

        #region cluster domain interfaces for relations
        /// <summary>
        /// 
        /// </summary>
        public void ClusterDomainInterfaces()
        {
            InitializeDbTable();  // create new table in the db

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Clustering domain interfaces";

            string queryString = string.Format("SELECT Distinct RelSeqID FROM {0}DomainInterfaceComp;",
                                ProtCidSettings.dataType);
            DataTable relationTable = ProtCidSettings.protcidQuery.Query( queryString);

            ProtCidSettings.progressInfo.totalOperationNum = relationTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = relationTable.Rows.Count;

            int relSeqId = -1;
            foreach (DataRow relSeqIdRow in relationTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString());

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();

                ClusterDomainInterfaces(relSeqId);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqIds"></param>
        public void UpdateDomainInterfaceClusters (int[] relSeqIds)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Clustering domain interfaces";

            ProtCidSettings.progressInfo.totalOperationNum = relSeqIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = relSeqIds.Length;

            foreach (int relSeqId in relSeqIds)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();

            /*    if (relSeqId < 17908)
                {
                    continue;
                }*/

                DeleteDomainInterfaceClusters(relSeqId);
                ClusterDomainInterfaces(relSeqId);

                ProtCidSettings.logWriter.WriteLine(relSeqId.ToString());
                ProtCidSettings.logWriter.Flush ();
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        private void DeleteDomainInterfaceClusters(int relSeqId)
        {
            string deleteString = string.Format("Delete From PfamDomainInterfaceCluster Where RelSeqID = {0};", relSeqId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relationEntryTable"></param>
        private void ClusterDomainInterfaces(int relSeqId)
        {
            string[] relRepEntries = GetRelationRepEntries(relSeqId);
            if (relRepEntries == null)
            {
                return;
            }
            if (relRepEntries.Length < 2)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString () + 
                    ": don't have >=2 representative entries: " + relRepEntries.Length.ToString ());
                ProtCidSettings.logWriter.WriteLine(relSeqId.ToString() +
                    ": don't have >=2 representative entries: " + relRepEntries.Length.ToString());
                ProtCidSettings.logWriter.Flush();
                return;
            }
            if (relRepEntries.Length > 400)
            {
                ClusterBigGroupDomainInterfaces(relSeqId, relRepEntries);
            }
            else
            {
                ClusterGroupDomainInterfaces(relSeqId, relRepEntries);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        private void OutputRelationClusterInfoToFile(int relSeqId)
        {
            StreamWriter datawriter = new StreamWriter(relSeqId.ToString() + "_clusters.txt");
            string queryString = string.Format("Select * From PfamDomainInterfaceCluster Where RelSeqID = {0};", relSeqId);
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            datawriter.WriteLine(ParseHelper.FormatDataRows(clusterTable.Select()));
            datawriter.Close();
        }
        #endregion

        #region input entries
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateDomainInterfaceClusters(string[] updateEntries)
        {
            InitializeTable();
            //    InitializeDbTable();
            isDomain = true;
            GroupDbTableNames.SetGroupDbTableNames(ProtCidSettings.dataType);

            AppSettings.parameters.simInteractParam.interfaceSimCutoff = 0.20;

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Update Clustering domain interfaces";

            int[] relSeqIDs = GetRelSeqIDsForEntries(updateEntries);
            ProtCidSettings.progressInfo.totalOperationNum = relSeqIDs.Length;
            ProtCidSettings.progressInfo.totalStepNum = relSeqIDs.Length;

            foreach (int relSeqId in relSeqIDs)
            {
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                DeleteObsRelationClusterData(relSeqId);
                ClusterDomainInterfaces(relSeqId);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }
        /// <summary>
        /// the relation SeqIDs for input entries
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        private int[] GetRelSeqIDsForEntries(string[] entries)
        {
            List<int> relSeqIdList = new List<int> ();

            string queryString = "";
            foreach (string pdbId in entries)
            {
                queryString = string.Format("Select RelSeqID From {0}DomainInterfaces " +
                     " Where PdbID = '{1}';", ProtCidSettings.dataType, pdbId);
                DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);

                int relSeqId = -1;
                foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
                {
                    relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString());
                    if (!relSeqIdList.Contains(relSeqId))
                    {
                        relSeqIdList.Add(relSeqId);
                    }
                }
            }
            return relSeqIdList.ToArray();
        }
        #endregion

        #region cluster one relation
        /// <summary>
        /// cluster interface for this group
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="interfaceCompTable"></param>
        /// <param name="interfaceOfSgCompTable"></param>
        public void ClusterGroupDomainInterfaces(int relSeqId, string[] relEntries)
        {
            try
            {
                DataTable domainInterfaceTable = GetRelationDomainInterfaceTable(relSeqId, relEntries);
                DataTable groupInterfaceCompTable = GetRelationDifEntryInterfaceComp (relSeqId, domainInterfaceTable);
                DataTable sameEntryInterfaceCompTable = GetRelationEntryInterfaceComp (relSeqId, relEntries, domainInterfaceTable);
             /*   DataTable groupInterfaceCompTable = GetRelationDifEntryInterfaceComp(relSeqId);
                DataTable sameEntryInterfaceCompTable = GetRelationEntryInterfaceComp(relSeqId);*/
                
                Dictionary<string, int> interfaceIndexHash = GetInterfacesIndexes(groupInterfaceCompTable);
                GetPdbInterfacesFromIndex(interfaceIndexHash);

                bool canClustered = true;
                double[,] distMatrix = CreateDistMatrix(sameEntryInterfaceCompTable, groupInterfaceCompTable, 
                    interfaceIndexHash, out canClustered);
                if (canClustered)
                {
                    Dictionary<int, int[]> clusterHash = ClusterThisGroupInterfaces(distMatrix, interfaceIndexHash);
                    if (clusterHash.Count > 0)
                    {
                        Dictionary<string, int[]> entryCfGroupHash = GetEntryCfGroups(relEntries);
                        Dictionary<string, string> entrySgAsuHash = GetEntrySgAsu(relEntries);
                        AssignDataToTable(relSeqId, clusterHash, entryCfGroupHash, entrySgAsuHash, sameEntryInterfaceCompTable);
                        InsertDataToDb();
                    }
                }
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Clustering group " + relSeqId + " error: " + ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        public string[] GetRelationRepEntries(int relSeqId)
        {
            List<string> relationRepEntryList = new List<string> ();
            string pdbId = "";
            string queryString = string.Format("Select Distinct PdbID From {0}DomainInterfaces " +
                " Where RelSeqID = {1};", ProtCidSettings.dataType, relSeqId);
            DataTable relationEntryTable = ProtCidSettings.protcidQuery.Query( queryString);

            foreach (DataRow entryRow in relationEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (!IsEntryRepresentativeEntry(pdbId))
                {
                    continue;
                }
                relationRepEntryList.Add(pdbId);
            }
            if (relationRepEntryList.Count == 0)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("No representative entries in " + relSeqId.ToString());
                return null;
            }

            return relationRepEntryList.ToArray ();
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsEntryRepresentativeEntry(string pdbId)
        {
            string queryString = string.Format("Select * From {0}HomoSeqInfo Where PdbID = '{1}';",
                ProtCidSettings.dataType, pdbId);
            DataTable repEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (repEntryTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region cluster one big relation: distance matrix can be more than 1,000,000
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        public void ClusterDomainInterfacesInBigRelation(int relSeqId)
        {
            InitializeTable();
            isDomain = true;
            GroupDbTableNames.SetGroupDbTableNames(ProtCidSettings.dataType);

            string[] relRepEntries = GetRelationRepEntries(relSeqId);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString() +
                    ": # of representative entries: " + relRepEntries.Length.ToString());

            ClusterBigGroupDomainInterfaces(relSeqId, relRepEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="relEntries"></param>
        private void ClusterBigGroupDomainInterfaces(int relSeqId, string[] relEntries)
        {
            try
            {
                DataTable relDomainInterfaceTable = GetRelationDomainInterfaceTable(relSeqId, relEntries);
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve interface comp for entries in the raltion" + relSeqId.ToString () + " #entries: " + relEntries.Length.ToString ());
                ProtCidSettings.logWriter.WriteLine ("Retrieve interface comp for entries in the raltion" + relSeqId.ToString() + " #entries: " + relEntries.Length.ToString());
                
                DataTable sameEntryInterfaceCompTable = GetRelationEntryInterfaceComp(relSeqId, relEntries, relDomainInterfaceTable);
                
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve interface comp for dif entries in the relation " + relSeqId.ToString() + " #entries: " + relEntries.Length.ToString());
                ProtCidSettings.logWriter.WriteLine("Retrieve interface comp for dif entries in the relation " + relSeqId.ToString() + " #entries: " + relEntries.Length.ToString());

                DataTable relGroupInterfaceCompTable = GetBigRelationDifEntryInterfaceComp(relSeqId, relEntries, sameEntryInterfaceCompTable, entrySimInterfaceQCutoff);
                DataTable groupInterfaceCompTable = GetDifEntryDomainInterfaceComp(relGroupInterfaceCompTable, relDomainInterfaceTable);

                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Interface index hash");
                Dictionary<string, int> interfaceIndexHash = GetInterfacesIndexes(groupInterfaceCompTable);
                GetPdbInterfacesFromIndex(interfaceIndexHash);

                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Create distance matrix with matrix size: " + interfaceIndexHash.Count);
                bool canClustered = true;
                double[,] distMatrix = CreateDistMatrix(sameEntryInterfaceCompTable, groupInterfaceCompTable,
                    interfaceIndexHash, out canClustered);
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Clustering ...");
                if (canClustered)
                {
                    Dictionary<int, int[]> clusterHash = ClusterThisBigGroupInterfaces(distMatrix);
                    if (clusterHash.Count > 0)
                    {
                        Dictionary<string, int[]> entryCfGroupHash = GetEntryCfGroups(relEntries);
                        Dictionary<string, string> entrySgAsuHash = GetEntrySgAsu(relEntries);
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue("Add data into table");
                        AssignDataToTable(relSeqId, clusterHash, entryCfGroupHash, entrySgAsuHash, sameEntryInterfaceCompTable, entrySimInterfaceQCutoff);
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue("Insert data into db");
                        InsertDataToDb();
                    }
                }
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Clustering group " + relSeqId + " error: " + ex.Message);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Cluster " + relSeqId.ToString () + " done!");
        }


        /// <summary>
        /// interface comparison between two different entries
        /// </summary>
        /// <param name="entryList"></param>
        /// <returns></returns>
        public DataTable GetBigRelationDifEntryInterfaceComp(int relSeqId, string[] relEntries, DataTable entryInterfaceCompTable, double entrySimInterfaceQCutoff)
        {
      /*      ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = relEntries.Length;
            ProtCidSettings.progressInfo.totalOperationNum = relEntries.Length;
           */
            DataTable relationInterfaceCompTable = null;
            for (int i = 0; i < relEntries.Length; i++)
            {
                ProtCidSettings.progressInfo.currentFileName = relEntries[i];
   //             ProtCidSettings.progressInfo.currentOperationNum++;
  //              ProtCidSettings.progressInfo.currentStepNum++;

                for (int j = i + 1; j < relEntries.Length; j++)
                {
                    DataTable domainInterfaceCompTable = GetRelationInterfaceCompTable(relSeqId, relEntries[i], relEntries[j]);
                    if (domainInterfaceCompTable.Rows.Count == 0)
                    {
                        continue;
                    }
                    DataTable uniqueDomainInterfaceCompTable = ReduceDomainInterfaceCompTable(domainInterfaceCompTable, entryInterfaceCompTable, entrySimInterfaceQCutoff);
                    if (relationInterfaceCompTable == null)
                    {
                        relationInterfaceCompTable = uniqueDomainInterfaceCompTable.Copy();
                    }
                    else
                    {
                        foreach (DataRow compRow in uniqueDomainInterfaceCompTable.Rows)
                        {
                            DataRow newCompRow = relationInterfaceCompTable.NewRow();
                            newCompRow.ItemArray = compRow.ItemArray;
                            relationInterfaceCompTable.Rows.Add(newCompRow);
                        }
                    }
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            return relationInterfaceCompTable;
        }

        /// <summary>
        /// Reduce those domain interfaces similar to other domain interfaces in the same entry
        /// so that reduce the domain interface comp in the relation interface comp.
        /// </summary>
        /// <param name="domainInterfaceCompTable"></param>
        /// <param name="entryInterfaceCompTable"></param>
        /// <param name="entrySimInterfaceQCutoff"></param>
        private DataTable ReduceDomainInterfaceCompTable(DataTable domainInterfaceCompTable, DataTable entryInterfaceCompTable, double entrySimInterfaceQCutoff)
        {
            List<int> domainInterfaceList1 = new List<int> ();
            List<int> domainInterfaceList2 = new List<int> ();
            int domainInterfaceId1 = 0;
            int domainInterfaceId2 = 0;
            string pdbId1 = domainInterfaceCompTable.Rows[0]["PdbID1"].ToString ();
            string pdbId2 = domainInterfaceCompTable.Rows[0]["PdbID2"].ToString ();;
            foreach (DataRow compRow in domainInterfaceCompTable.Rows)
            {
                domainInterfaceId1 = Convert.ToInt32(compRow["InterfaceID1"].ToString ());
                domainInterfaceId2 = Convert.ToInt32(compRow["InterfaceID2"].ToString ());
                if (!domainInterfaceList1.Contains(domainInterfaceId1))
                {
                    domainInterfaceList1.Add(domainInterfaceId1);
                }
                if (!domainInterfaceList2.Contains(domainInterfaceId2))
                {
                    domainInterfaceList2.Add(domainInterfaceId2);
                }
            }
            domainInterfaceList1.Sort();
            int[] uniqueDomainInterfaces1 = GetUniqueDomainInterfaces(pdbId1, domainInterfaceList1.ToArray (), entryInterfaceCompTable, entrySimInterfaceQCutoff);
            domainInterfaceList2.Sort();
            int[] uniqueDomainInterfaces2 = GetUniqueDomainInterfaces(pdbId2, domainInterfaceList2.ToArray (), entryInterfaceCompTable, entrySimInterfaceQCutoff);

            DataTable uniqueDomainInterfaceCompTable = domainInterfaceCompTable.Clone();
            foreach (int uniqueDomainInterfaceId1 in uniqueDomainInterfaces1)
            {
                foreach (int uniqueDomainInterfaceId2 in uniqueDomainInterfaces2)
                {
                    DataRow[] interfaceCompRows = domainInterfaceCompTable.Select(string.Format ("InterfaceID1 = '{0}' AND InterfaceID2 = '{1}'", 
                        uniqueDomainInterfaceId1, uniqueDomainInterfaceId2));
                    foreach (DataRow interfaceCompRow in interfaceCompRows)
                    {
                        DataRow newInterfaceCompRow = uniqueDomainInterfaceCompTable.NewRow();
                        newInterfaceCompRow.ItemArray = interfaceCompRow.ItemArray;
                        uniqueDomainInterfaceCompTable.Rows.Add(newInterfaceCompRow);
                    }
                }
            }
            return uniqueDomainInterfaceCompTable;
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="compDomainInterfaces"></param>
        /// <param name="entryInterfaceCompTable"></param>
        /// <param name="entrySimInterfaceQCutoff"></param>
        /// <returns></returns>
        private int[] GetUniqueDomainInterfaces(string pdbId, int[] compDomainInterfaces, DataTable entryInterfaceCompTable, double entrySimInterfaceQCutoff)
        {
            List<int> leftDomainInterfaceList = new List<int> (compDomainInterfaces);
            for (int i = 0; i < compDomainInterfaces.Length; i++)
            {
                if (!leftDomainInterfaceList.Contains(compDomainInterfaces[i]))
                {
                    continue;
                }
                for (int j = i + 1; j < compDomainInterfaces.Length; j++)
                {
                    if (!leftDomainInterfaceList.Contains(compDomainInterfaces[j]))
                    {
                        continue;
                    }
                    if (AreEntryDomainInterfaceSim(pdbId, compDomainInterfaces[i], compDomainInterfaces[j], entryInterfaceCompTable, entrySimInterfaceQCutoff))
                    {
                        leftDomainInterfaceList.Remove(compDomainInterfaces[j]);
                    }
                }
            }
            return leftDomainInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterface1"></param>
        /// <param name="domainInterface2"></param>
        /// <param name="entryInterfaceCompTable"></param>
        /// <param name="entrySimInterfaceQCutoff"></param>
        /// <returns></returns>
        private bool AreEntryDomainInterfaceSim(string pdbId, int domainInterface1, int domainInterface2, DataTable entryInterfaceCompTable, double entrySimInterfaceQCutoff)
        {
            DataRow[] interfaceCompRows = entryInterfaceCompTable.Select(string.Format ("PdbID = '{0}' AND InterfaceID1 = '{1}' AND InterfaceID2 = '{2}'", 
                pdbId, domainInterface1, domainInterface2));
            if (interfaceCompRows.Length == 0)
            {
                interfaceCompRows = entryInterfaceCompTable.Select(string.Format ("PdbID = '{0}' AND InterfaceID1 = '{1}' AND InterfaceID2 = '{2}'",
                pdbId, domainInterface2, domainInterface1));
            }
            if (interfaceCompRows.Length > 0)
            {
                double qScore = Convert.ToDouble(interfaceCompRows[0]["Qscore"].ToString ());
                if (qScore >= entrySimInterfaceQCutoff)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region data tables for clutering
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        protected Dictionary<string, int[]> GetEntryCfGroups(string[] entries)
        {
            Dictionary<string, int[]> entryCfHash = new Dictionary<string,int[]> ();
            int[] cfGroup = null;
            foreach (string entry in entries)
            {
                cfGroup = GetEntryCfGroup(entry);
                entryCfHash.Add(entry, cfGroup);
            }
            return entryCfHash;
        }

        /// <summary>
        /// the crystal form group ID for this entry
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        private int[] GetEntryCfGroup(string entry)
        {
            string queryString = string.Format("Select * From {0} Where PdbID = '{1}';",
                    GroupDbTableNames.dbTableNames[GroupDbTableNames.NonredundantCfGroups], entry);
            DataTable cfGroupTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] cfGroup = new int[2];
            cfGroup[0] = -1;
            cfGroup[1] = -1;
            if (cfGroupTable.Rows.Count > 0)
            {
                cfGroup[0] = Convert.ToInt32 (cfGroupTable.Rows[0]["GroupSeqID"].ToString ());
                cfGroup[1] = Convert.ToInt32 (cfGroupTable.Rows[0]["CfGroupID"].ToString());
            }
            return cfGroup;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        public DataTable GetRelationDifEntryInterfaceComp(int relSeqId)
        {
            string queryString = string.Format("Select RelSeqId, PdbID1, DomainInterfaceID1 As InterfaceID1, " +
                " PdbID2, DomainInterfaceID2 As InterfaceID2, QScore From {0} Where RelSeqID = {1} ;",
                domainInterfaceCompTable, relSeqId);
            DataTable relationInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return relationInterfaceCompTable;
        }
        /// <summary>
        /// interface comparison between two different entries
        /// </summary>
        /// <param name="entryList"></param>
        /// <returns></returns>
        public DataTable GetRelationDifEntryInterfaceComp(int relSeqId, string[] relEntries)
        {
            // in order to use functions from InterfaceCluster class
            string queryString = string.Format("Select RelSeqId, PdbID1, DomainInterfaceID1 As InterfaceID1, " + 
                " PdbID2, DomainInterfaceID2 As InterfaceID2, QScore From {0} " +
                " Where RelSeqID = {1} ;",  domainInterfaceCompTable, relSeqId);
            DataTable interfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);

            DataTable relationInterfaceCompTable = interfaceCompTable.Clone ();
            string pdbId1 = "";
            string pdbId2 = "";
            foreach (DataRow interfaceCompRow in interfaceCompTable.Rows)
            {
                pdbId1 = interfaceCompRow["PdbID1"].ToString();
                pdbId2 = interfaceCompRow["PdbID2"].ToString();

                if (Array.IndexOf(relEntries, pdbId1) > -1 && Array.IndexOf(relEntries, pdbId2) > -1)
                {
                    DataRow dataRow = relationInterfaceCompTable.NewRow();
                    dataRow.ItemArray = interfaceCompRow.ItemArray;
                    relationInterfaceCompTable.Rows.Add(dataRow);
                }
            }
            return relationInterfaceCompTable;
        }

        /// <summary>
        /// interface comparison between two different entries
        /// </summary>
        /// <param name="entryList"></param>
        /// <returns></returns>
        public DataTable GetRelationDifEntryInterfaceComp(int relSeqId, DataTable relDomainInterfaceTable)
        {
            // in order to use functions from InterfaceCluster class
            string queryString = string.Format("Select RelSeqId, PdbID1, DomainInterfaceID1 As InterfaceID1, " +
                " PdbID2, DomainInterfaceID2 As InterfaceID2, QScore From {0} " +
                " Where RelSeqID = {1} ;", domainInterfaceCompTable, relSeqId);
            DataTable interfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);

            DataTable relInterfaceCompTable = GetDifEntryDomainInterfaceComp(interfaceCompTable, relDomainInterfaceTable);
            return relInterfaceCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceCompTable"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private DataTable GetDifEntryDomainInterfaceComp(DataTable interfaceCompTable, DataTable domainInterfaceTable)
        {
            DataTable relationInterfaceCompTable = interfaceCompTable.Clone();
            string pdbId1 = "";
            int interfaceId1 = 0;
            string pdbId2 = "";
            int interfaceId2 = 0;
            foreach (DataRow interfaceCompRow in interfaceCompTable.Rows)
            {
                pdbId1 = interfaceCompRow["PdbID1"].ToString();
                interfaceId1 = Convert.ToInt32(interfaceCompRow["InterfaceID1"].ToString());
                pdbId2 = interfaceCompRow["PdbID2"].ToString();
                interfaceId2 = Convert.ToInt32(interfaceCompRow["InterfaceID2"].ToString());

                if (IsDomainInterfaceDefined(pdbId1, interfaceId1, domainInterfaceTable) && IsDomainInterfaceDefined(pdbId2, interfaceId2, domainInterfaceTable))
                {
                    DataRow dataRow = relationInterfaceCompTable.NewRow();
                    dataRow.ItemArray = interfaceCompRow.ItemArray;
                    relationInterfaceCompTable.Rows.Add(dataRow);
                }
            }
            return relationInterfaceCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="interfaceTable"></param>
        /// <returns></returns>
        private bool IsDomainInterfaceDefined(string pdbId, int interfaceId, DataTable interfaceTable)
        {
            DataRow[] interfaceRows = interfaceTable.Select(string.Format ("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, interfaceId));
            if (interfaceRows.Length > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
        public DataTable GetRelationInterfaceCompTable(int relSeqId, string pdbId1, string pdbId2)
        {
            string queryString = string.Format("Select RelSeqId, PdbID1, DomainInterfaceID1 As InterfaceID1, " +
               " PdbID2, DomainInterfaceID2 As InterfaceID2, QScore From {0} " +
               " Where RelSeqID = {1} AND ((PdbID1 = '{2}' AND PdbID2 = '{3}') OR (PdbID1 = '{3}' AND PdbID2 = '{2}'));",
               domainInterfaceCompTable, relSeqId, pdbId1, pdbId2);
            DataTable entryInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return entryInterfaceCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        public DataTable GetRelationDomainInterfaceTable(int relSeqId)
        {
            string queryString = string.Format("Select RelSeqID, PdbID, DomainInterfaceID As InterfaceID" + 
                " From PfamDomainInterfaces Where RelSeqID = {0} AND SurfaceArea >= {1};", relSeqId, surfaceArea);
            DataTable relDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return relDomainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="relEntries"></param>
        /// <returns></returns>
        public DataTable GetRelationDomainInterfaceTable(int relSeqId, string[] relEntries)
        {
            DataTable relDomainInterfaceTable = GetRelationDomainInterfaceTable(relSeqId);
            DataTable domainInterfaceTable = relDomainInterfaceTable.Clone();
            string pdbId = "";
            foreach (DataRow domainInterfaceRow in relDomainInterfaceTable.Rows)
            {
                pdbId = domainInterfaceRow["PdbID"].ToString();
                if (Array.IndexOf(relEntries, pdbId) > -1)
                {
                    DataRow newDomainInterfaceRow = domainInterfaceTable.NewRow();
                    newDomainInterfaceRow.ItemArray = domainInterfaceRow.ItemArray;
                    domainInterfaceTable.Rows.Add(newDomainInterfaceRow);
                }
            }
            return domainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        public DataTable GetRelationEntryInterfaceComp(int relSeqId)
        {
            string queryString = string.Format("Select RelSeqID, PdbID, " +
               " DomainInterfaceID1 As InterfaceID1, DomainInterfaceID2 As InterfaceID2, QScore " +
               " From {0}EntryDomainInterfaceComp Where RelSeqID = {1};", ProtCidSettings.dataType, relSeqId);
            DataTable entryInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return entryInterfaceCompTable;
        }

        /// <summary>
        /// interface comparison for each entry in the relation
        /// </summary>
        /// <param name="entryList"></param>
        /// <returns></returns>
        private DataTable GetRelationEntryInterfaceComp(int relSeqId, string[] relEntries, DataTable domainInterfaceTable)
        {
            DataTable relationEntryInterfaceCompTable = null;

            int interfaceId1 = 0;
            int interfaceId2 = 0;
            foreach (string pdbId in relEntries)
            {
                int[] domainInterfaceIds = GetEntryDomainInterfaceIds(pdbId, domainInterfaceTable);
                DataTable entryInterfaceCompTable = GetRelationEntryInterfaceComp(relSeqId, pdbId);
                if (relationEntryInterfaceCompTable == null)
                {
                    relationEntryInterfaceCompTable = entryInterfaceCompTable.Clone();
                }

                foreach (DataRow entryInterfaceCompRow in entryInterfaceCompTable.Rows)
                {
                    interfaceId1 = Convert.ToInt32(entryInterfaceCompRow ["InterfaceID1"].ToString ());
                    interfaceId2 = Convert.ToInt32(entryInterfaceCompRow["InterfaceID2"].ToString ());
                    if (Array.IndexOf(domainInterfaceIds, interfaceId1) > -1 &&
                        Array.IndexOf(domainInterfaceIds, interfaceId2) > -1)
                    {
                        DataRow newCompRow = relationEntryInterfaceCompTable.NewRow();
                        newCompRow.ItemArray = entryInterfaceCompRow.ItemArray;
                        relationEntryInterfaceCompTable.Rows.Add(newCompRow);
                    }
                }
            }
            return relationEntryInterfaceCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private int[] GetEntryDomainInterfaceIds(string pdbId, DataTable domainInterfaceTable)
        {
            DataRow[] domainInterfaceRows = domainInterfaceTable.Select(string.Format ("PdbID = '{0}'", pdbId));
            int[] domainInterfaceIds = new int[domainInterfaceRows.Length];
            int count = 0;
            int domainInterfaceId = 0;
            foreach (DataRow domainInterfaceRow in domainInterfaceRows)
            {
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["InterfaceID"].ToString ());
                domainInterfaceIds[count] = domainInterfaceId;
                count++;
            }
            return domainInterfaceIds;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetRelationEntryInterfaceComp(int relSeqId, string pdbId)
        {
            string queryString = string.Format("Select RelSeqID, PdbID, " +
               " DomainInterfaceID1 As InterfaceID1, DomainInterfaceID2 As InterfaceID2, QScore " +
               " From {0}EntryDomainInterfaceComp Where RelSeqID = {1} AND PdbID = '{2}';",
              ProtCidSettings.dataType, relSeqId, pdbId);
            DataTable entryInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return entryInterfaceCompTable;
        }
        #endregion

        #region assign data to table
        /// <summary>
        /// assign cluster data into table
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="clusterHash"></param>
        /// <param name="interfaceIndexHash"></param>
        /// <param name="interfaceOfSgCompTable"></param>
        protected void AssignDataToTable(int relSeqId, Dictionary<int, int[]> clusterHash, Dictionary<string, int[]> entryCfGroupHash, Dictionary<string, string> entrySgAsuHash, DataTable entryDomainInterfaceCompTable)
        {
            List<int> clusterIdList = new List<int> (clusterHash.Keys);
            clusterIdList.Sort();
            string[] sgAsuFields = null;
            string[] pdbInterfaceFields = null;
            Dictionary<string, List<int>> entryClusterInterfaceHash = new Dictionary<string,List<int>> ();
            foreach (int clusterId in clusterIdList)
            {
                entryClusterInterfaceHash.Clear();

                foreach (int interfaceIndex in clusterHash[clusterId])
                {
                    try
                    {
                        string pdbInterfaceString = pdbInterfaces[interfaceIndex];
                        pdbInterfaceFields = pdbInterfaceString.Split('_');
                        DataRow clusterRow = clusterTable.NewRow();
                        if (ProtCidSettings.dataType == "clan")
                        {
                            clusterRow["ClanSeqID"] = relSeqId;
                        }
                        else
                        {
                            clusterRow["RelSeqID"] = relSeqId;
                        }
                        clusterRow["ClusterID"] = clusterId;
                        int[] cfGroup = entryCfGroupHash[pdbInterfaceFields[0]];
                        clusterRow["RelCfGroupID"] = GetRelationCfGroupId(relSeqId, cfGroup[0], cfGroup[1]);
                        clusterRow["PdbID"] = pdbInterfaceFields[0];
                        clusterRow["DomainInterfaceID"] = pdbInterfaceFields[1];
                        sgAsuFields = entrySgAsuHash[pdbInterfaceFields[0]].ToString().Split('_');
                        clusterRow["SpaceGroup"] = sgAsuFields[0];
                        clusterRow["ASU"] = sgAsuFields[1];
                        clusterTable.Rows.Add(clusterRow);
                        //       clusterInterfaceList.Add(pdbInterfaceString);
                        if (entryClusterInterfaceHash.ContainsKey (pdbInterfaceFields[0]))
                        {
                            entryClusterInterfaceHash[pdbInterfaceFields[0]].Add(Convert.ToInt32(pdbInterfaceFields[1]));
                        }
                        else
                        {
                            List<int> clusterInterfaceList = new List<int> ();
                            clusterInterfaceList.Add(Convert.ToInt32(pdbInterfaceFields[1]));
                            entryClusterInterfaceHash.Add(pdbInterfaceFields[0], clusterInterfaceList);
                        }
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue("Assign data to table error: " + ex.Message);
                    }
                }
               
                // add same domain interfaces which are not used to be compared to other entries
                // and with Qscore >= 0.90
                foreach (string clusterEntry in entryClusterInterfaceHash.Keys)
                {
                    int[] entrySimInterfaces = GetSimDomainInterfacesNotInCluster(clusterEntry, entryClusterInterfaceHash[clusterEntry].ToArray (), entryDomainInterfaceCompTable);
                    foreach (int simInterfaceId in entrySimInterfaces)
                    {
                        DataRow homoClusterRow = clusterTable.NewRow();
                        if (ProtCidSettings.dataType == "clan")
                        {
                            homoClusterRow["ClanSeqID"] = relSeqId;
                        }
                        else
                        {
                            homoClusterRow["RelSeqID"] = relSeqId;
                        }
                        homoClusterRow["ClusterID"] = clusterId;
                        int[] cfGroup = (int[])entryCfGroupHash[clusterEntry];
                     //   homoClusterRow["GroupSeqID"] = cfGroup[0];
                     //   homoClusterRow["CfGroupID"] = cfGroup[1];
                        homoClusterRow["RelCfGroupId"] = GetRelationCfGroupId(relSeqId, cfGroup[0], cfGroup[1]);
                        homoClusterRow["PdbID"] = clusterEntry;
                        homoClusterRow["DomainInterfaceID"] = simInterfaceId;
                        sgAsuFields = entrySgAsuHash[clusterEntry].ToString().Split('_');
                        homoClusterRow["SpaceGroup"] = sgAsuFields[0];
                        homoClusterRow["ASU"] = sgAsuFields[1];
                        clusterTable.Rows.Add(homoClusterRow);
                    }
                }
            }
        }

        /// <summary>
        /// assign cluster data into table
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="clusterHash"></param>
        /// <param name="interfaceIndexHash"></param>
        /// <param name="interfaceOfSgCompTable"></param>
        protected void AssignDataToTable(int relSeqId, Dictionary<int, int[]> clusterHash, Dictionary<string, int[]> entryCfGroupHash, Dictionary<string, string> entrySgAsuHash, 
            DataTable entryDomainInterfaceCompTable, double entrySimInterfaceQCutoff)
        {
            List<int> clusterIdList = new List<int> (clusterHash.Keys);
            clusterIdList.Sort();
            string[] sgAsuFields = null;
            string[] pdbInterfaceFields = null;
            Dictionary<string, List<int>> entryClusterInterfaceHash = new Dictionary<string,List<int>> ();
            foreach (int clusterId in clusterIdList)
            {
                entryClusterInterfaceHash.Clear();

                foreach (int interfaceIndex in clusterHash[clusterId])
                {
                    try
                    {
                        string pdbInterfaceString = pdbInterfaces[interfaceIndex];
                        pdbInterfaceFields = pdbInterfaceString.Split('_');
                        DataRow clusterRow = clusterTable.NewRow();
                        if (ProtCidSettings.dataType == "clan")
                        {
                            clusterRow["ClanSeqID"] = relSeqId;
                        }
                        else
                        {
                            clusterRow["RelSeqID"] = relSeqId;
                        }
                        clusterRow["ClusterID"] = clusterId;
                        int[] cfGroup = (int[])entryCfGroupHash[pdbInterfaceFields[0]];
                  //      clusterRow["GroupSeqID"] = cfGroup[0];
                 //       clusterRow["CfGroupID"] = cfGroup[1];
                        clusterRow["RelCfGroupId"] = GetRelationCfGroupId(relSeqId, cfGroup[0], cfGroup[1]);
                        clusterRow["PdbID"] = pdbInterfaceFields[0];
                        clusterRow["DomainInterfaceID"] = pdbInterfaceFields[1];
                        sgAsuFields = entrySgAsuHash[pdbInterfaceFields[0]].ToString().Split('_');
                        clusterRow["SpaceGroup"] = sgAsuFields[0];
                        clusterRow["ASU"] = sgAsuFields[1];
                        clusterTable.Rows.Add(clusterRow);
                        if (entryClusterInterfaceHash.ContainsKey(pdbInterfaceFields[0]))
                        {
                            entryClusterInterfaceHash[pdbInterfaceFields[0]].Add(Convert.ToInt32(pdbInterfaceFields[1]));
                        }
                        else
                        {
                            List<int> clusterInterfaceList = new List<int> ();
                            clusterInterfaceList.Add(Convert.ToInt32(pdbInterfaceFields[1]));
                            entryClusterInterfaceHash.Add(pdbInterfaceFields[0], clusterInterfaceList);
                        }
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue("Assign data to table error: " + ex.Message);
                    }
                }

                // add same domain interfaces which are not used to be compared to other entries
                // and with Qscore >= entrySimInterfaceQCutoff
                foreach (string clusterEntry in entryClusterInterfaceHash.Keys)
                {
                    int[] entrySimInterfaces = GetSimDomainInterfacesNotInCluster(clusterEntry, entryClusterInterfaceHash[clusterEntry].ToArray (), entryDomainInterfaceCompTable, entrySimInterfaceQCutoff);
                    foreach (int simInterfaceId in entrySimInterfaces)
                    {
                        DataRow homoClusterRow = clusterTable.NewRow();
                        if (ProtCidSettings.dataType == "clan")
                        {
                            homoClusterRow["ClanSeqID"] = relSeqId;
                        }
                        else
                        {
                            homoClusterRow["RelSeqID"] = relSeqId;
                        }
                        homoClusterRow["ClusterID"] = clusterId;
                        int[] cfGroup = (int[])entryCfGroupHash[clusterEntry];
                        homoClusterRow["RelCfGroupId"] = GetRelationCfGroupId(relSeqId, cfGroup[0], cfGroup[1]);
                        homoClusterRow["PdbID"] = clusterEntry;
                        homoClusterRow["DomainInterfaceID"] = simInterfaceId;
                        sgAsuFields = entrySgAsuHash[clusterEntry].ToString().Split('_');
                        homoClusterRow["SpaceGroup"] = sgAsuFields[0];
                        homoClusterRow["ASU"] = sgAsuFields[1];
                        clusterTable.Rows.Add(homoClusterRow);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryInterface"></param>
        /// <param name="entryInterfaceCompTable"></param>
        /// <param name="clusterInterfaceList"></param>
        /// <returns></returns>
        private int[] GetSimDomainInterfacesNotInCluster(string entry, int[] clusterInterfaceList, DataTable entryInterfaceCompTable)
        {
            // use the unique interface Q score cutoff for most of the relations
            int[] entrySimInterfaces = GetSimDomainInterfacesNotInCluster(entry, clusterInterfaceList, entryInterfaceCompTable,
                                 AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff);
            return entrySimInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="clusterInterfaceList"></param>
        /// <param name="entryInterfaceCompTable"></param>
        /// <param name="entrySimInterfaceQ">The Q score cutoff for domain interfaces in an entry, user-defined</param>
        /// <returns></returns>
        private int[] GetSimDomainInterfacesNotInCluster(string entry, int[] clusterInterfaceList, DataTable entryInterfaceCompTable, double entrySimInterfaceQ)
        {
            List<int> entrySimInterfaceList = new List<int> ();
            foreach (int clusterInterfaceId in clusterInterfaceList)
            {
                int[] simInterfaces = GetSimInterfacesNotInCluster(entry, clusterInterfaceId, entryInterfaceCompTable, entrySimInterfaceQ);
                foreach (int simInterfaceId in simInterfaces)
                {
                    if (!entrySimInterfaceList.Contains(simInterfaceId) &&
                        Array.IndexOf(clusterInterfaceList, simInterfaceId) < 0)
                    {
                        entrySimInterfaceList.Add(simInterfaceId);
                    }
                }
            }
            return entrySimInterfaceList.ToArray ();
        }

        /// <summary>
        /// very similar domain interfaces to this domain interfaces
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="interfaceId"></param>
        /// <param name="entryInterfaceCompTable"></param>
        /// <returns></returns>
        private int[] GetSimInterfacesNotInCluster(string entry, int interfaceId, DataTable entryInterfaceCompTable)
        {
            int[] simInterfaces = GetSimInterfacesNotInCluster(entry, interfaceId, entryInterfaceCompTable, 
                AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff);
            return simInterfaces;
        }

        /// <summary>
        /// very similar domain interfaces to this domain interfaces
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="interfaceId"></param>
        /// <param name="entryInterfaceCompTable"></param>
        /// <returns></returns>
        private int[] GetSimInterfacesNotInCluster(string entry, int interfaceId, DataTable entryInterfaceCompTable, double entrySimInterfaceQ)
        {
            DataRow[] simInterfaceRows = entryInterfaceCompTable.Select (string.Format("(PdbID = '{0}' AND InterfaceID1 = '{1}' AND QScore >= '{2}')",
                entry, interfaceId, entrySimInterfaceQ));
            List<int> simInterfaceList = new List<int> ();
            int simInterfaceId = -1;
            foreach (DataRow simInterfaceRow in simInterfaceRows)
            {
                simInterfaceId = Convert.ToInt16(simInterfaceRow["InterfaceID2"].ToString());
                if (!simInterfaceList.Contains(simInterfaceId))
                {
                    simInterfaceList.Add(simInterfaceId);
                }
            }
            return simInterfaceList.ToArray ();
        }
        /// <summary>
        /// space group and ASU info for each entry
        /// </summary>
        /// <param name="interfaceIndexHash"></param>
        /// <param name="cfRepHomoInterfaceHash"></param>
        /// <returns></returns>
        protected Dictionary<string, string> GetEntrySgAsu(string[] relEntries)
        {
            Dictionary<string, string> entrySgAsuHash = new Dictionary<string,string> ();
            string sgQueryString = "";
            foreach (string pdbId in relEntries)
            {
                sgQueryString = string.Format("Select Distinct PdbID, SpaceGroup, ASU From {0}" +
                    " Where PdbID = '{1}';",
                    GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo], pdbId);
                DataTable sgTable = ProtCidSettings.protcidQuery.Query( sgQueryString);
                entrySgAsuHash.Add(pdbId, sgTable.Rows[0]["SpaceGroup"].ToString().Trim() + "_" + sgTable.Rows[0]["ASU"].ToString().Trim());
            }
            return entrySgAsuHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="groupId"></param>
        /// <param name="cfGroupId"></param>
        /// <returns></returns>
        private int GetRelationCfGroupId(int relSeqId, int groupId, int cfGroupId)
        {
            string queryString = string.Format("Select RelCfGroupID From PfamDomainCfGroups Where RelSeqID = {0} AND GroupSeqID = {1} AND CfGroupId = {2};",
                relSeqId, groupId, cfGroupId);
            DataTable relCfGroupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relCfGroupId = -1;
            if (relCfGroupIdTable.Rows.Count > 0)
            {
                relCfGroupId = Convert.ToInt32(relCfGroupIdTable.Rows[0]["RelCfGroupID"].ToString());
            }
            return relCfGroupId;
        }
        #endregion

        #region delete data
        /// <summary>
        /// delete obsolete cluster data for the input RelSeqIDs
        /// </summary>
        /// <param name="relSeqIDs"></param>
        private void DeleteObsClusterData(int[] relSeqIDs)
        {
            foreach (int relSeqId in relSeqIDs)
            {
                DeleteObsRelationClusterData(relSeqId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        private void DeleteObsRelationClusterData(int relSeqId)
        {
            string deleteString = string.Format("Delete From {0}DomainInterfaceCluster " +
                " Where RelSeqID = {1};", ProtCidSettings.dataType, relSeqId);
            ProtCidSettings.protcidQuery.Query( deleteString);
        }
        #endregion

        #region initialize table
        /// <summary>
        /// 
        /// </summary>
        public void InitializeTable()
        {
            tableName = ProtCidSettings.dataType + "DomainInterfaceCluster";
            string[] clusterCols = { "RelSeqID",  "ClusterID", "RelCfGroupId", "SpaceGroup", "ASU", "PdbID", "DomainInterfaceID" };
            clusterTable = new DataTable(tableName);
            foreach (string clusterCol in clusterCols)
            {
                clusterTable.Columns.Add(new DataColumn(clusterCol));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void InitializeDbTable()
        {
            string createTableString = string.Format("CREATE Table {0} (" +
                " RelSeqID INTEGER NOT NULL, ClusterID INTEGER NOT NULL, " +
          //      " GroupSeqID INTEGER NOT NULL, " +
          //      " CFGroupID INTEGER NOT NULL, " +
                " RelCfGroupID INTEGER NOT NULL, " +
                " SpaceGroup VARCHAR(30) NOT NULL, " +
                " ASU BLOB SUB_TYPE 1 SEGMENT SIZE 80 NOT NULL, " +
                " PDBID CHAR(4) NOT NULL, " + 
                " DomainInterfaceID INTEGER NOT NULL);",
                ProtCidSettings.dataType + "DomainInterfaceCluster");
            DbCreator dbCreate = new DbCreator();
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName, true);

            string indexString = string.Format("Create INDEX {0}_Idx1 ON {0} (RelSeqID, PdbID, DomainInterfaceID);",
               tableName);
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, tableName);
        }
        #endregion

        #region for debug

        /// <summary>
        /// 
        /// </summary>
        public void ClusterLeftRelations()
        {
            int[] relSeqIds = { 2986, 3175, 17915 };

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Clustering domain interfaces";
            ProtCidSettings.progressInfo.totalOperationNum = relSeqIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = relSeqIds.Length;
            foreach (int relSeqId in relSeqIds)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();


                ClusterDomainInterfaces(relSeqId);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        public void UpdateClusterInterfaces()
        {
            string queryString = "Select RelSeqID, ClusterID, ClusterInterface From PFAMDOMAINCLUSTERSUMINFO;";
            DataTable clusterInfoTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = 0;
            int clusterId = 0;
            string clusterInterface = "";
            string newClusterInterface = "";
            string updateString = "";
            foreach (DataRow clusterInfoRow in clusterInfoTable.Rows)
            {
                relSeqId = Convert.ToInt32(clusterInfoRow["RelSeqID"].ToString ());
                clusterId = Convert.ToInt32(clusterInfoRow["ClusterID"].ToString ());
                clusterInterface = clusterInfoRow["ClusterInterface"].ToString().TrimEnd();
                newClusterInterface = clusterInterface.Replace("_", "_d");
                updateString = string.Format("Update PFAMDOMAINCLUSTERSUMINFO Set ClusterInterface = '{0}' " +
                    " Where RelSeqID = {1} AND ClusterID = {2};", newClusterInterface, relSeqId, clusterId);
                dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
            }
        }
        public void ReClusterDomainInterfaces()
        {
            int[] udpateRelSeqIds = GetUpdateRelSeqIDs();
            UpdateDomainInterfaceClusters(udpateRelSeqIds);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int[] GetUpdateRelSeqIDs()
        {
            StreamWriter dataWriter = new StreamWriter("ErrorClusterRelations.txt");
            StreamReader dataReader = new StreamReader("ErrorRelationClusterEntries.txt");
            string line = "";
            List<int> updateRelSeqIdList = new List<int> ();
            int orgRelSeqId = 0;
            string pdbId = "";
            int domainInterfaceId = 0;
            int relSeqId = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                orgRelSeqId = Convert.ToInt32(fields[0]);
                pdbId = fields[2];
                domainInterfaceId = Convert.ToInt32(fields[3]);
                relSeqId = GetDomainInterfaceRelSeqId(pdbId, domainInterfaceId);
                if (!updateRelSeqIdList.Contains(orgRelSeqId))
                {
                    updateRelSeqIdList.Add(orgRelSeqId);
                    dataWriter.WriteLine(orgRelSeqId.ToString ());
                }
                if (relSeqId > -1)
                {
                    if (! updateRelSeqIdList.Contains(relSeqId))
                    {
                        updateRelSeqIdList.Add(relSeqId);
                        dataWriter.WriteLine(relSeqId.ToString ());
                    }
                }
            }
            dataReader.Close();
            dataWriter.Close();
            int[] updateRelSeqIds = new int[updateRelSeqIdList.Count];
            updateRelSeqIdList.CopyTo(updateRelSeqIds);
            return updateRelSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private int GetDomainInterfaceRelSeqId(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select RelSeqID From PfamDomainInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (relSeqIdTable.Rows.Count > 0)
            {
                return Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString ());
            }
            return -1;
        }
        #endregion

    }
}
