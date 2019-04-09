using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using DbLib;
using InterfaceClusterLib.Clustering;
using ProtCidSettingsLib;
using AuxFuncLib;
using InterfaceClusterLib.DataTables;
using CrystalInterfaceLib.Settings;

namespace InterfaceClusterLib.ChainInterfaces
{
    public class ChainInterfaceCluster : InterfaceCluster
    {
        private DataTable pfamSuperGroupTable = null;
        #region public interfaces to cluster super groups
        /// <summary>
        /// 
        /// </summary>
        public void ClusterChainGroupInterfaces()
        {
       //     InitializeDbTable();
            InitializeTable();         
            string superGroupString = "Select SuperGroupSeqID, GroupSeqID From pfamSuperGroups;";
            pfamSuperGroupTable = ProtCidSettings.protcidQuery.Query( superGroupString);

            AppSettings.parameters.simInteractParam.interfaceSimCutoff = 0.20;

            string queryString = string.Format("Select Distinct SuperGroupSeqID, ChainRelPfamArch From {0}SuperGroups Where SuperGroupSeqID > 991;",
                ProtCidSettings.dataType);
            DataTable superGroupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int superGroupId = -1;
            string chainRelPfamArch = "";

            // the relation of the group is same as the chain relation
            groupRelType = "same";

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Cluster interfaces for superset groups");
            ProtCidSettings.progressInfo.currentOperationLabel = "Clustering interfaces";
            ProtCidSettings.progressInfo.totalStepNum = superGroupIdTable.Rows.Count;
            ProtCidSettings.progressInfo.totalOperationNum = superGroupIdTable.Rows.Count;

            ProtCidSettings.logWriter.WriteLine("Cluster interfaces for superset groups.");

            foreach (DataRow superGroupRow in superGroupIdTable.Rows)
            {
                superGroupId = Convert.ToInt32(superGroupRow["SuperGroupSeqID"].ToString());
                chainRelPfamArch = superGroupRow["ChainRelPfamArch"].ToString().TrimEnd();
                ProtCidSettings.progressInfo.currentFileName = superGroupId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

  /*              if (superGroupId == 744) // exclude antibody group (V-set)_(C1-set)
                {
                    continue;
                }*/
                try
                {
                    ClusterChainGroupInterfaces(superGroupId);
                    // update CfGroupID to CfGroupID in SuperGroup
                    ChangeCfGroupIDsInSuperGroup(superGroupId);
                    // insert data into db, and clear memory table
                    InsertDataToDb();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupId.ToString () + ": " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(superGroupId.ToString () + ": " + ex.Message);
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        public void ClusterChainGroupInterfaces(int superGroupId)
        {
            int[] groupIds = null;
            int count = 0;
            if (pfamSuperGroupTable != null)
            {
                DataRow[] groupRows = pfamSuperGroupTable.Select(string.Format ("SuperGroupSeqID = '{0}'", superGroupId));
                groupIds = new int[groupRows.Length];
                foreach (DataRow groupSeqRow in groupRows)
                {
                    groupIds[count] = Convert.ToInt32(groupSeqRow["GroupSeqID"].ToString());
                    count++;
                }
            }
            else
            {
                string queryString = string.Format("Select Distinct GroupSeqID From {0}SuperGroups Where SuperGroupSeqID = {1};", ProtCidSettings.dataType, superGroupId);
                DataTable groupSeqTable = ProtCidSettings.protcidQuery.Query( queryString);
                groupIds = new int[groupSeqTable.Rows.Count];
               
                foreach (DataRow groupSeqRow in groupSeqTable.Rows)
                {
                    groupIds[count] = Convert.ToInt32(groupSeqRow["GroupSeqID"].ToString());
                    count++;
                }
            }
           
            if (groupIds.Length == 0)
            {
                return;
            }
            ClusterChainGroupInterfaces(superGroupId, groupIds);

            ProtCidSettings.logWriter.Flush();
        }
        #endregion

        #region cluster pfam super groups
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        private void AddSuperGroupIdToTable(int superGroupId)
        {
            foreach (DataRow dataRow in clusterTable.Rows)
            {
                dataRow["SuperGroupSeqID"] = superGroupId;
            }
            clusterTable.AcceptChanges();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupString"></param>
        /// <param name="groupIds"></param>
        private void ClusterChainGroupInterfaces(int superGroupId, int[] groupIds)
        {
            try
            {
                string[] superGroupFamilyArchFields = GetSuperGroupFamilyArchFields(superGroupId);

                Dictionary<string, int[]> superGroupCfRepInterfaceHash = new Dictionary<string,int[]> ();
                Dictionary<string, List<string>> superGroupCfRepHomoInterfaceHash = new Dictionary<string,List<string>> ();
                Dictionary<string, string> superGroupEntryCfGroupHash = new Dictionary<string,string> ();
                Dictionary<string, string> superGroupCfRepInterfaceFamilyArchHash = new Dictionary<string,string> ();

                GetSuperGroupCfRepEntryAndInterfaces(superGroupFamilyArchFields, groupIds, ref superGroupEntryCfGroupHash,
                    ref superGroupCfRepInterfaceHash, ref superGroupCfRepHomoInterfaceHash, ref superGroupCfRepInterfaceFamilyArchHash);
                ClearDuplicateHomoInterfaceHash(ref superGroupCfRepHomoInterfaceHash);

                DataTable groupInterfaceCompTable = GetEntriesInterfaceComp(superGroupCfRepInterfaceHash);
                if (groupInterfaceCompTable == null)
                {
                    return;
                }
                DataTable sameEntryInterfaceCompTable = GetEntryInterfaceComp(superGroupCfRepInterfaceHash);
                Dictionary<string, int> interfaceIndexHash = GetInterfacesIndexes(groupInterfaceCompTable);
                if (interfaceIndexHash.Count == 0)
                {
                    interfaceIndexHash = GetInterfaceIndexes(superGroupCfRepInterfaceHash);
                }
                if (interfaceIndexHash.Count < 2)
                {
                    return;
                }
                GetPdbInterfacesFromIndex(interfaceIndexHash);

                bool canClustered = true;
                double[,] distMatrix = CreateDistMatrix(sameEntryInterfaceCompTable, groupInterfaceCompTable,
                    interfaceIndexHash, out canClustered);

                Dictionary<int, int[]> clusterHash = ClusterThisGroupInterfaces(distMatrix, interfaceIndexHash, superGroupCfRepInterfaceFamilyArchHash);
                if (clusterHash.Count > 0)
                {
                    AssignDataToTable(superGroupId, clusterHash, interfaceIndexHash, superGroupEntryCfGroupHash, superGroupCfRepHomoInterfaceHash);
                }
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Clustering supergroup " + superGroupId + " error: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine("Clustering supergroup " + superGroupId + " error: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupIds"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetSuperGroupNonreduntCfGroups(int[] groupIds)
        {
            Dictionary<string, string> superGroupCfGroupHash = new Dictionary<string,string> ();
            foreach (int GroupSeqID in groupIds)
            {
                Dictionary<string, int> entryCfGroupHash = GetNonReduntCfGroups(GroupSeqID);
                foreach (string entry in entryCfGroupHash.Keys)
                {
                    superGroupCfGroupHash.Add(entry, GroupSeqID.ToString() + "_" + entryCfGroupHash[entry].ToString());
                }
            }
            return superGroupCfGroupHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupString"></param>
        /// <param name="groupIds"></param>
        /// <param name="entryCfGroupHash"></param>
        /// <param name="superGroupCfRepInterfaceHash"></param>
        /// <param name="superGroupCfRepHomoInterfaceHash"></param>
        private void GetSuperGroupCfRepEntryAndInterfaces(string[] familyArchFields, int[] groupIds, ref Dictionary<string, string> superGroupEntryCfGroupHash, ref Dictionary<string, int[]> superGroupCfRepInterfaceHash,
            ref Dictionary<string, List<string>> superGroupCfRepHomoInterfaceHash, ref Dictionary<string, string> superGroupCfRepInterfaceFamilyArchHash)
        {
            Dictionary<string, int[]> cfRepInterfaceHash = new Dictionary<string,int[]> ();
            Dictionary<string, List<string>> cfRepHomoInterfaceHash = new Dictionary<string,List<string>> ();
            Dictionary<string, string> cfRepInterfaceFamilyArchHash = new Dictionary<string,string> ();
            foreach (int groupSeqId in groupIds)
            {
                cfRepInterfaceHash.Clear();
                cfRepInterfaceFamilyArchHash.Clear();
                cfRepHomoInterfaceHash.Clear();
                Dictionary<string, int> entryCfGroupHash = GetNonReduntCfGroups(groupSeqId);

                try
                {
                    GetCfRepEntryAndInterfaces(groupSeqId, entryCfGroupHash, ref cfRepInterfaceHash, ref cfRepHomoInterfaceHash, familyArchFields, ref cfRepInterfaceFamilyArchHash);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine(groupSeqId + " " + ex.Message);
                }
                    foreach (string entry in entryCfGroupHash.Keys)
                {
                    if (!superGroupEntryCfGroupHash.ContainsKey(entry))
                    {
                        superGroupEntryCfGroupHash.Add(entry, groupSeqId.ToString() + "_" + entryCfGroupHash[entry].ToString());
                    }
                    else
                    {
                        ProtCidSettings.logWriter.WriteLine(entry + " in " + groupSeqId.ToString() + " duplicated.");
                        ProtCidSettings.logWriter.Flush();
                    }
                }
                foreach (string repEntry in cfRepInterfaceHash.Keys)
                {
                    if (!superGroupCfRepInterfaceHash.ContainsKey(repEntry))
                    {
                        superGroupCfRepInterfaceHash.Add(repEntry, (int[])cfRepInterfaceHash[repEntry]);
                    }
                }
                foreach (string cfEntry in cfRepHomoInterfaceHash.Keys)
                {
                    if (!superGroupCfRepHomoInterfaceHash.ContainsKey(cfEntry))
                    {
                        superGroupCfRepHomoInterfaceHash.Add(cfEntry, cfRepHomoInterfaceHash[cfEntry]);
                    }
                }
                foreach (string cfEntry in cfRepInterfaceFamilyArchHash.Keys)
                {
                    if (!superGroupCfRepInterfaceFamilyArchHash.ContainsKey(cfEntry))
                    {
                        superGroupCfRepInterfaceFamilyArchHash.Add(cfEntry, (string)cfRepInterfaceFamilyArchHash[cfEntry]);
                    }
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param> 
        /// <returns></returns>
        public string[] GetSuperGroupFamilyArchFields(int superGroupId)
        {
            string queryString = string.Format("Select ChainRelPfamArch From {0}SuperGroups " +
                "Where SuperGroupSeqID = {1};", ProtCidSettings.dataType, superGroupId);
            DataTable chainRelTable = ProtCidSettings.protcidQuery.Query( queryString);
            string familyArchString = chainRelTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();

            return SplitEntityFamilyArchString(familyArchString);
        }
        #endregion

        #region remove duplicate homo interfaces

        /// <summary>
        /// An interface of a homo entry may be similar to several interfaces of representative entry 
        /// in different clusters with q scores >= cutoff, so the interface might be added into different clusters.
        /// It is supposed to be added into a cluster with best qscore.
        /// </summary>
        /// <param name="superGroupCfRepHomoInterfaceHash"></param>
        private void ClearDuplicateHomoInterfaceHash(ref Dictionary<string, List<string>> superGroupCfRepHomoInterfaceHash)
        {
            Dictionary<string, List<string>> homoRepInterfaceHash = GetHomoRepInterfaceHash(superGroupCfRepHomoInterfaceHash);
            string repInterfaceWithBestQ = "";
            foreach (string homoInterface in homoRepInterfaceHash.Keys)
            {
                if (homoRepInterfaceHash[homoInterface].Count > 1) // the homo interface similar to several rep interfaces
                {
                    string[] repInterfaces = homoRepInterfaceHash[homoInterface].ToArray(); 
                    repInterfaceWithBestQ = GetBestRepInterface(homoInterface, repInterfaces);
                    // remove the homo interface from the list of interfaces similar to the rep interface
                    foreach (string repInterface in repInterfaces)
                    {
                        if (repInterface != repInterfaceWithBestQ)
                        {
                            superGroupCfRepHomoInterfaceHash[repInterface].Remove(homoInterface);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="homoInterface"></param>
        /// <param name="repInterfaces"></param>
        /// <returns></returns>
        private string GetBestRepInterface(string homoInterface, string[] repInterfaces)
        {
            string homoPdbId = homoInterface.Substring(0, 4);
            int homoInterfaceId = Convert.ToInt32(homoInterface.Substring(5, homoInterface.Length - 5));
            string repPdbId = "";
            int repInterfaceId = 0;
            double qscore = 0;
            double maxQscore = -1;
            string repInterfaceWithBestQ = "";
            foreach (string repInterface in repInterfaces)
            {
                repPdbId = repInterface.Substring(0, 4);
                repInterfaceId = Convert.ToInt32(repInterface.Substring(5, repInterface.Length - 5));
                qscore = GetInterfaceQscore(repPdbId, repInterfaceId, homoPdbId, homoInterfaceId);
                if (maxQscore < qscore)
                {
                    maxQscore = qscore;
                    repInterfaceWithBestQ = repInterface;
                }
            }
            return repInterfaceWithBestQ;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="interfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="interfaceId2"></param>
        /// <returns></returns>
        private double GetInterfaceQscore(string pdbId1, int interfaceId1, string pdbId2, int interfaceId2)
        {
            string queryString = string.Format("Select * From DifEntryInterfaceComp Where " +
                " (PdbID1 = '{0}' AND InterfaceID1 = {1} AND PdbID2 = '{2}' AND InterfaceID2 = {3}) OR " +
                " (PdbID1 = '{2}' AND InterfaceID1 = {3} AND PdbID2 = '{0}' AND InterfaceID2 = {1});",
                pdbId1, interfaceId1, pdbId2, interfaceId2);
            DataTable interfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            double qscore = -1;
            if (interfaceCompTable.Rows.Count > 0)
            {
                qscore = Convert.ToDouble(interfaceCompTable.Rows[0]["Qscore"].ToString());
            }
            return qscore;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupCfRepHomoInterfaceHash"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetHomoRepInterfaceHash(Dictionary<string, List<string>> superGroupCfRepHomoInterfaceHash)
        {
            Dictionary<string, List<string>> homoRepInterfaceHash = new Dictionary<string, List<string>>();
            foreach (string repInterface in superGroupCfRepHomoInterfaceHash.Keys)
            {
                foreach (string homoInterface in superGroupCfRepHomoInterfaceHash[repInterface])
                {
                    if (homoRepInterfaceHash.ContainsKey(homoInterface))
                    {
                        homoRepInterfaceHash[homoInterface].Add(repInterface);
                    }
                    else
                    {
                        List<string> repInterfaceList = new List<string> ();
                        repInterfaceList.Add(repInterface);
                        homoRepInterfaceHash.Add(homoInterface, repInterfaceList);
                    }
                }
            }
            return homoRepInterfaceHash;
        }
        #endregion

        #region interfaces contain the family content
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <param name="entryCfGroupHash"></param>
        /// <param name="cfRepInterfaceHash"></param>
        /// <param name="cfRepHomoInterfaceHash"></param>
        /// <param name="familyArchFields"></param>
        /// <param name="entryInterfaceFamilyArchHash"></param>
        private void GetCfRepEntryAndInterfaces(int groupSeqId, Dictionary<string, int> entryCfGroupHash, ref Dictionary<string, int[]> cfRepInterfaceHash,
            ref Dictionary<string, List<string>> cfRepHomoInterfaceHash, string[] chainPfamArchRel, ref Dictionary<string, string> entryInterfaceFamilyArchHash)
        {
            Dictionary<int, List<string>> cfEntryHash = GetCfEntriesFromEntryCfHash(entryCfGroupHash);

            string cfRepEntry = "";
            foreach (int cfGroupId in cfEntryHash.Keys)
            {
                List<string> cfEntryList = cfEntryHash[cfGroupId];
                cfRepEntry = GetEntryWithBestResolution(cfEntryList);
                if (cfRepEntry == "None")
                {
                    ProtCidSettings.logWriter.WriteLine (groupSeqId.ToString () + ":" + cfGroupId.ToString () + " no cf rep entry.");
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }
                if (cfRepEntry == "NMR")
                {
                    foreach (string nmrEntry in cfEntryList)
                    {
                        int[] interfaces = GetEntryInterfaces(nmrEntry, chainPfamArchRel);
                        cfRepInterfaceHash.Add(nmrEntry, interfaces);
                        SetEntryInterfacePfamArchHash(nmrEntry, interfaces, chainPfamArchRel, ref entryInterfaceFamilyArchHash);
                    }
                }
                else
                {
                    cfEntryList.Remove(cfRepEntry);
                    Dictionary<string, int[]> entryFamilyArchInterfacesHash = new Dictionary<string,int[]> ();
                    int[] commonInterfaces = GetCommonInterfacesInCF(cfRepEntry, cfEntryList, chainPfamArchRel, ref entryFamilyArchInterfacesHash);
                    cfRepInterfaceHash.Add(cfRepEntry, commonInterfaces);
                    SetEntryInterfacePfamArchHash(cfRepEntry, commonInterfaces, chainPfamArchRel, ref entryInterfaceFamilyArchHash);

                    if (cfEntryList.Count > 0)
                    {
                        try
                        {
                            GetSimilarInterfacesFromOtherCfEntries(cfRepEntry, commonInterfaces, chainPfamArchRel, cfEntryList, ref cfRepHomoInterfaceHash,
                                entryFamilyArchInterfacesHash, entryInterfaceFamilyArchHash);
                        }
                        catch (Exception ex)
                        {
                            ProtCidSettings.logWriter.WriteLine(groupSeqId + " "+ cfGroupId.ToString () + " " + ex.Message);
                            ProtCidSettings.logWriter.Flush();
                        }
                    }
                }
            }
        }

        #region interfaces contain the family contents
        /// <summary>
        /// the interfaces for this entry
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        public int[] GetEntryInterfaces(string entry, string[] pfamArchFields)
        {
            //    Hashtable entryAsymChainFamilyArchHash = GetEntryChainPfamArchHash(entry);
            Dictionary<int, string> entryEntityPfamArchHash = pfamArch.GetEntryEntityGroupPfamArchHash (entry);

            string queryString = string.Format("Select * From CrystEntryInterfaces " +
                " Where PdbID = '{0}' AND SurfaceArea > {1};", entry, surfaceArea);
            DataTable entryInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<int> interfaceList = new List<int> ();
            int[] interfaceEntityIds = new int[2];
            foreach (DataRow dRow in entryInterfaceTable.Rows)
            {
                interfaceEntityIds[0] = Convert.ToInt32(dRow["EntityID1"].ToString());
                interfaceEntityIds[1] = Convert.ToInt32(dRow["EntityID2"].ToString());
                //      if (DoesInterfaceContainFamilyArch(entry, interfaceAsymChains, familyArchFields, entryAsymChainFamilyArchHash))
                if (groupRelType == "contain")
                {
                    if (IsInterfaceContainPfamArch(entry, interfaceEntityIds, pfamArchFields, entryEntityPfamArchHash))
                    {
                        interfaceList.Add(Convert.ToInt32(dRow["InterfaceID"].ToString()));
                    }
                }
                else
                {
                    if (IsInterfaceSamePfamArch(entry, interfaceEntityIds, pfamArchFields, entryEntityPfamArchHash))
                    {
                        interfaceList.Add(Convert.ToInt32(dRow["InterfaceID"].ToString()));
                    }
                }
            }
            return interfaceList.ToArray ();
        }

        /// <summary>
        /// all the interfaces with the same PFAM architectures for this entry
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        private int[] GetAllEntryInterfaces(string pdbId, string[] pfamArchFields)
        {
            //   Hashtable entryAsymChainFamilyArchHash = GetEntryChainPfamArchHash(entry);
            Dictionary<int, string> entryEntityPfamArchHash = pfamArch.GetEntryEntityGroupPfamArchHash(pdbId);

            string queryString = string.Format("Select * From CrystEntryInterfaces " +
                " Where PdbID = '{0}';", pdbId);
            DataTable entryInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<int> interfaceList = new List<int> ();
            int[] interfaceEntityIds = new int[2];
            foreach (DataRow dRow in entryInterfaceTable.Rows)
            {
                interfaceEntityIds[0] = Convert.ToInt32(dRow["EntityID1"].ToString());
                interfaceEntityIds[1] = Convert.ToInt32(dRow["EntityID2"].ToString());
                if (groupRelType == "contain")
                {
                    if (IsInterfaceContainPfamArch(pdbId, interfaceEntityIds, pfamArchFields, entryEntityPfamArchHash))
                    {
                        interfaceList.Add(Convert.ToInt32(dRow["InterfaceID"].ToString()));
                    }
                }
                else
                {
                    if (IsInterfaceSamePfamArch(pdbId, interfaceEntityIds, pfamArchFields, entryEntityPfamArchHash))
                    {
                        interfaceList.Add(Convert.ToInt32(dRow["InterfaceID"].ToString()));
                    }
                }
            }
            return interfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChains"></param>
        /// <param name="familyArchFields"></param>
        /// <param name="entryAsymChainFamilyArchHash"></param>
        /// <returns></returns>
        private bool IsInterfaceSamePfamArch(string pdbId, int[] entityIds, string[] pfamArchFields, Dictionary<int, string> entryEntityPfamArchHash)
        {
            if (entityIds.Length == 1 && pfamArchFields.Length == 2) // homodimer verse hetero pfam arch group
            {
                return false;
            }
            string chainPfamArchString = "";
            foreach (int entityId in entityIds)
            {
                if (! entryEntityPfamArchHash.ContainsKey (entityId))
                {
                    return false;
                }
                chainPfamArchString = entryEntityPfamArchHash[entityId];
                if (Array.IndexOf(pfamArchFields, chainPfamArchString) < 0)
                {
                    return false;
                }
            }
            // something wrong, interface pfam arch is not matched to the group pfam arch
            if (entityIds.Length == 2 && pfamArchFields.Length == 2)
            {
                string chainPfamArchString1 = entryEntityPfamArchHash[entityIds[0]];
                string chainPfamArchString2 = entryEntityPfamArchHash[entityIds[1]];
                if ((chainPfamArchString1 == chainPfamArchString2 && pfamArchFields[0] != pfamArchFields[1]) ||
                    (chainPfamArchString1 != chainPfamArchString2 && pfamArchFields[0] == pfamArchFields[1])) 
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChains"></param>
        /// <param name="familyArchFields"></param>
        /// <param name="entryAsymChainFamilyArchHash"></param>
        /// <returns></returns>
        private bool IsInterfaceContainPfamArch(string pdbId, int[] entityIds, string[] groupPfamArchFields, Dictionary<int, string> entryEntityPfamArchHash)
        {
            string chainPfamArchString = "";
            if (groupPfamArchFields.Length == 1)
            {
                string groupPfamArch = groupPfamArchFields[0].ToLower();
                foreach (int entityId in entityIds)
                {
                    if (! entryEntityPfamArchHash.ContainsKey(entityId))
                    {
                        return false;   
                    }
                    chainPfamArchString = entryEntityPfamArchHash[entityId].ToLower();
                    if (chainPfamArchString.IndexOf(groupPfamArch) < 0)
                    {
                        return false;
                    }
                }
                return true;
            }
            else if (groupPfamArchFields.Length == 2)
            {
                if (entityIds.Length == 2)
                {
                    string groupPfamArchField1 = groupPfamArchFields[0].ToLower();
                    string groupPfamArchField2 = groupPfamArchFields[1].ToLower();
                    if (entryEntityPfamArchHash.ContainsKey(entityIds[0]) &&
                        entryEntityPfamArchHash.ContainsKey(entityIds[1]))
                    {
                        string chainPfamArchString1 = ((string)entryEntityPfamArchHash[entityIds[0]]).ToLower();
                        string chainPfamArchString2 = ((string)entryEntityPfamArchHash[entityIds[1]]).ToLower();

                        if ((chainPfamArchString1.IndexOf(groupPfamArchField1) > -1 && chainPfamArchString2.IndexOf(groupPfamArchField2) > -1)
                            || (chainPfamArchString1.IndexOf(groupPfamArchField2) > -1 && chainPfamArchString2.IndexOf(groupPfamArchField1) > -1))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="familyArch"></param>
        /// <returns></returns>
        public string[] SplitEntityFamilyArchString(string familyArchString)
        {
            string[] familyArchFields = familyArchString.Split(';');
            string[] familyArchComponent = new string[familyArchFields.Length];
            int count = 0;
            foreach (string familyArchField in familyArchFields)
            {
                //   familyArchComponent[count] = familyArchField.Trim("()".ToCharArray());
                familyArchComponent[count] = familyArchField;
                count++;
            }
            return familyArchComponent;
        }
        #endregion

        #region commmon interfaces in the CF group
        /// <summary>
        /// common interfaces for the crystal form
        /// </summary>
        /// <param name="cfRepEntry"></param>
        /// <param name="homoEntryList"></param>
        /// <returns></returns>
        private int[] GetCommonInterfacesInCF(string cfRepEntry, List<string> cfEntryList, string[] familyArchFields, ref Dictionary<string, int[]> entryFamilyArchInterfaceHash)
        {
            int[] repEntryInterfaces = GetEntryInterfaces(cfRepEntry, familyArchFields);
            List<int> commonInterfaceList = new List<int> (repEntryInterfaces);
            entryFamilyArchInterfaceHash.Add(cfRepEntry, repEntryInterfaces);

            double qScore = 0.0;
            int interfaceId = 0;
            foreach (string cfEntry in cfEntryList)
            {
                List<int> tempComInterfaceList = new List<int> ();
                DataTable interfaceCompTable = GetInterfaceCompTable(cfRepEntry, cfEntry, familyArchFields, ref entryFamilyArchInterfaceHash);
                foreach (DataRow compRow in interfaceCompTable.Rows)
                {
                    interfaceId = Convert.ToInt32(compRow["InterfaceID1"].ToString());
                    qScore = Convert.ToDouble(compRow["QScore"].ToString());
                    //		if (qScore > AppSettings.parameters.contactParams.minQScore)
                    if (qScore > AppSettings.parameters.simInteractParam.interfaceSimCutoff)
                    {
                        if (!tempComInterfaceList.Contains(interfaceId))
                        {
                            if (commonInterfaceList.Contains(interfaceId))
                            {
                                tempComInterfaceList.Add(interfaceId);
                            }
                        }
                    }
                }
                commonInterfaceList = tempComInterfaceList;
            }
            return commonInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="repEntry"></param>
        /// <param name="homoEntry"></param>
        /// <param name="familyArchFields"></param>
        /// <param name="entryFamilyArchInterfaceHash"></param>
        /// <returns></returns>
        private DataTable GetInterfaceCompTable(string repEntry, string homoEntry, string[] familyArchFields, ref Dictionary<string, int[]> entryFamilyArchInterfaceHash)
        {
            int[] repEntryInterfaceIds = entryFamilyArchInterfaceHash[repEntry];
            int[] interfacesWithSamePfamArch = null;
            if (entryFamilyArchInterfaceHash.ContainsKey(homoEntry))
            {
                interfacesWithSamePfamArch = entryFamilyArchInterfaceHash[homoEntry];
            }
            else
            {
                interfacesWithSamePfamArch = GetAllEntryInterfaces(homoEntry, familyArchFields);
                entryFamilyArchInterfaceHash.Add(homoEntry, interfacesWithSamePfamArch);
            }

            DataTable interfaceCompTable = GetInterfaceCompTable(repEntry, homoEntry);
            DataTable interfaceCompFamilyArchTable = interfaceCompTable.Clone();
            int interfaceId1 = 0;
            int interfaceId2 = 0;
            foreach (DataRow interfaceCompRow in interfaceCompTable.Rows)
            {
                interfaceId1 = Convert.ToInt32(interfaceCompRow["InterfaceID1"].ToString());
                interfaceId2 = Convert.ToInt32(interfaceCompRow["InterfaceID2"].ToString());
                if (Array.IndexOf(repEntryInterfaceIds, interfaceId1) > -1 &&
                    Array.IndexOf(interfacesWithSamePfamArch, interfaceId2) > -1)
                {
                    DataRow dataRow = interfaceCompFamilyArchTable.NewRow();
                    dataRow.ItemArray = interfaceCompRow.ItemArray;
                    interfaceCompFamilyArchTable.Rows.Add(dataRow);
                }
            }
            return interfaceCompFamilyArchTable;
        }

        /// <summary>
        /// find the similar interfaces as the interfaces of the representative entry in the crystal form group
        /// </summary>
        /// <param name="cfRepEntry"></param>
        /// <param name="commonInterfaces"></param>
        /// <param name="homoEntryList"></param>
        /// <param name="cfRepHomoInterfaceHash"></param>
        private void GetSimilarInterfacesFromOtherCfEntries(string cfRepEntry, int[] commonInterfaces, string[] chainPfamArchRel, List<string> homoEntryList,
            ref Dictionary<string, List<string>> cfRepHomoInterfaceHash, Dictionary<string, int[]> entryPfamArchInterfaceHash, Dictionary<string, string> cfRepInterfaceFamilyArchHash)
        {
            string repInterfaceString = "";
            string homoInterfaceString = "";
            string cfRepInterfaceArch = "";
            string cfHomoInterfaceArch = "";
            Dictionary<string, string> homoEntryInterfaceFamilyArchHash = new Dictionary<string,string> ();
            foreach (string homoEntry in homoEntryList)
            {
                SetEntryInterfacePfamArchHash(homoEntry, null, chainPfamArchRel, ref homoEntryInterfaceFamilyArchHash);
            }
            foreach (int interfaceId in commonInterfaces)
            {
                repInterfaceString = cfRepEntry + "_" + interfaceId.ToString();
                cfRepInterfaceArch = "";
                if (cfRepInterfaceFamilyArchHash.ContainsKey(repInterfaceString))
                {
                    cfRepInterfaceArch = cfRepInterfaceFamilyArchHash[repInterfaceString];
                }            

                DataTable interfaceCompTable = GetSimilarInterfaceCompTable(cfRepEntry, interfaceId, homoEntryList, entryPfamArchInterfaceHash);
                foreach (DataRow dRow in interfaceCompTable.Rows)
                {
                    homoInterfaceString = dRow["PdbID2"].ToString() + "_" + dRow["InterfaceID2"].ToString();
                    cfHomoInterfaceArch = "";
                    if (homoEntryInterfaceFamilyArchHash.ContainsKey (homoInterfaceString))
                    {
                        cfHomoInterfaceArch = homoEntryInterfaceFamilyArchHash[homoInterfaceString];
                    }
                    
                    if (cfRepInterfaceArch == cfHomoInterfaceArch)
                    {
                        if (cfRepHomoInterfaceHash.ContainsKey(repInterfaceString))
                        {
                            cfRepHomoInterfaceHash[repInterfaceString].Add(dRow["PdbID2"].ToString() + "_" + dRow["InterfaceID2"].ToString());
                        }
                        else
                        {
                            List<string> simInterfaceList = new List<string> ();
                            simInterfaceList.Add(dRow["PdbID2"].ToString() + "_" + dRow["InterfaceID2"].ToString());
                            cfRepHomoInterfaceHash.Add(repInterfaceString, simInterfaceList);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// the similar interfaces from the ohter CF entries as the interface of rep entry
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="homoEntryList"></param>
        /// <returns></returns>
        protected DataTable GetSimilarInterfaceCompTable(string repEntry, int interfaceId,
            List<string> cfEntryList, Dictionary<string, int[]> entryPfamArchInterfaceHash)
        {
            DataTable simInterfaceCompTable = null;
            int[] cfEntryInterfaces = null;
            string queryString = "";
            // these two variables for these cases with large number of interfaces
            int numOfQueryInterfaces = 500;
            int numOfLoops = 0;
            int[] queryCfEntryInterfaces = null;
            foreach (string cfEntry in cfEntryList)
            {
                if (entryPfamArchInterfaceHash.ContainsKey(cfEntry))
                {
                    cfEntryInterfaces = (int[])entryPfamArchInterfaceHash[cfEntry];
                    numOfLoops = GetNumOfLoops(cfEntryInterfaces.Length, numOfQueryInterfaces);
                    for (int i = 0; i < numOfLoops; i++)
                    {
                        if ((i + 1) * numOfQueryInterfaces > cfEntryInterfaces.Length)
                        {
                            queryCfEntryInterfaces = new int[cfEntryInterfaces.Length - i * numOfQueryInterfaces];
                        }
                        else
                        {
                            queryCfEntryInterfaces = new int[numOfQueryInterfaces];
                        }
                        Array.Copy(cfEntryInterfaces, i * numOfQueryInterfaces, queryCfEntryInterfaces,
                               0, queryCfEntryInterfaces.Length);

                        queryString = string.Format("Select * From DifEntryInterfaceComp " +
                            " Where PdbID1 = '{0}' AND InterfaceID1 = '{1}' AND " +
                            " PdbID2 = '{2}' AND InterfaceID2 IN ({3})" +
                            " AND QScore > {4};", repEntry, interfaceId, cfEntry,
                            ParseHelper.FormatSqlListString(queryCfEntryInterfaces),
                            AppSettings.parameters.simInteractParam.interfaceSimCutoff);
                        DataTable interfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
                        if (interfaceCompTable.Rows.Count == 0)
                        {
                            queryString = string.Format("Select * From DifEntryInterfaceComp " +
                                " Where PdbID2 = '{0}' AND InterfaceID2 = '{1}' AND " +
                                " PdbID1 = '{2}' AND InterfaceID1 IN ({3}) " +
                                " AND QScore > {4};", repEntry, interfaceId, cfEntry,
                                ParseHelper.FormatSqlListString(queryCfEntryInterfaces),
                                AppSettings.parameters.simInteractParam.interfaceSimCutoff);
                            DataTable tempCompTable = ProtCidSettings.protcidQuery.Query( queryString);
                            ReverseInterfaceCompTable(tempCompTable, ref interfaceCompTable);
                        }
                        if (simInterfaceCompTable == null)
                        {
                            simInterfaceCompTable = interfaceCompTable.Clone();
                        }
                        foreach (DataRow interfaceCompRow in interfaceCompTable.Rows)
                        {
                            DataRow dataRow = simInterfaceCompTable.NewRow();
                            dataRow.ItemArray = interfaceCompRow.ItemArray;
                            simInterfaceCompTable.Rows.Add(dataRow);
                        }
                    }
                }
            }
            return simInterfaceCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="numOfInterfaces"></param>
        /// <param name="numOfQueryInterface"></param>
        /// <returns></returns>
        private int GetNumOfLoops(int numOfInterfaces, int numOfQueryInterface)
        {
            int numOfLoops = (int)Math.Ceiling((double)numOfInterfaces / (double)numOfQueryInterface);
            return numOfLoops;
        }
        #endregion
        #endregion

        #region initialize table
        private void InitializeTable()
        {
            string[] clusterCols = { "SuperGroupSeqID", "ClusterID", "GroupSeqID", "CfGroupID", "SpaceGroup", "ASU", "PdbID", "InterfaceID" };
            clusterTable = new DataTable(GroupDbTableNames.dbTableNames[GroupDbTableNames.SuperInterfaceClusters]);
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
            DbCreator dbCreate = new DbCreator();
            string createTableString = string.Format("CREATE Table {0} (" +
                " SuperGroupSeqID INTEGER NOT NULL, " +
                " ClusterID INTEGER NOT NULL, " +
                " GroupSeqID INTEGER NOT NULL, " +
                " CFGroupID INTEGER NOT NULL, " +
                " SpaceGroup VARCHAR(40) NOT NULL, ASU BLOB Sub_Type TEXT NOT NULL,  " +
                " PDBID CHAR(4) NOT NULL, InterfaceID INTEGER NOT NULL);",
                GroupDbTableNames.dbTableNames[GroupDbTableNames.SuperInterfaceClusters]);

            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString,
                GroupDbTableNames.dbTableNames[GroupDbTableNames.SuperInterfaceClusters], true);

            string indexString = string.Format("Create INDEX PfamSuperGroupClusters_Idx1 ON {0} (SuperGroupSeqID, PdbID, InterfaceID);",
                GroupDbTableNames.dbTableNames[GroupDbTableNames.SuperInterfaceClusters]);
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString,
                GroupDbTableNames.dbTableNames[GroupDbTableNames.SuperInterfaceClusters]);
        }

        /// <summary>
        /// delete obsolete  data from db
        /// </summary>
        /// <param name="GroupSeqID"></param>
        private void DeleteObsDataInDb(int GroupSeqID)
        {
            string deleteString = string.Format("Delete FROM {0} Where SuperGroupSeqID = {1};",
                GroupDbTableNames.dbTableNames[GroupDbTableNames.SuperInterfaceClusters], GroupSeqID);
            ProtCidSettings.protcidQuery.Query( deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        private void ChangeCfGroupIDsInSuperGroup(int superGroupId)
        {
            string cfGroupString = "";
            Dictionary<string, List<DataRow>> groupCfGroupRowsHash = new Dictionary<string,List<DataRow>> ();
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                cfGroupString = clusterRow["GroupSeqID"].ToString() + "_" + clusterRow["CfGroupID"].ToString();
                if (groupCfGroupRowsHash.ContainsKey(cfGroupString))
                {
                    groupCfGroupRowsHash[cfGroupString].Add(clusterRow);
                }
                else
                {
                    List<DataRow> cfGroupRowList = new List<DataRow> ();
                    cfGroupRowList.Add(clusterRow);
                    groupCfGroupRowsHash.Add(cfGroupString, cfGroupRowList);
                }
            }
            int groupId = -1;
            int cfGroupId = -1;
            int superCfGroupId = -1;
            foreach (string groupCfGroup in groupCfGroupRowsHash.Keys)
            {
                string[] cfGroupFields = groupCfGroup.Split('_');
                groupId = Convert.ToInt32(cfGroupFields[0]);
                cfGroupId = Convert.ToInt32(cfGroupFields[1]);
                superCfGroupId = GetSuperCfGroupID(superGroupId, groupId, cfGroupId);

                foreach (DataRow cfGroupRow in groupCfGroupRowsHash[groupCfGroup])
                {
                    cfGroupRow["CfGroupID"] = superCfGroupId;
                }
            }
            clusterTable.AcceptChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="groupId"></param>
        /// <param name="cfGroupId"></param>
        /// <returns></returns>
        private int GetSuperCfGroupID(int superGroupId, int groupId, int cfGroupId)
        {
            string queryString = string.Format("Select SuperCfGroupID From PfamSuperCfGroups " +
                " Where SuperGroupSeqID = {0} AND GroupSeqID = {1} AND CfGroupID = {2};", superGroupId, groupId, cfGroupId);
            DataTable superCfGroupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int superCfGroupId = -1;
            if (superCfGroupIdTable.Rows.Count > 0)
            {
                superCfGroupId = Convert.ToInt32(superCfGroupIdTable.Rows[0]["SuperCfGroupID"].ToString());
            }
            return superCfGroupId;
        }
        #endregion

        #region for update
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateSuperGroups"></param>
        public void UpdateSuperGroupClusters(int[] updateSuperGroups)
        {
            InitializeTable();

            AppSettings.parameters.simInteractParam.interfaceSimCutoff = 0.20;
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Clustering interfaces";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update clustering interfaces in superset groups");
            ProtCidSettings.logWriter.WriteLine("Update clustering chain interfaces.");
            ProtCidSettings.progressInfo.totalStepNum = updateSuperGroups.Length;
            ProtCidSettings.progressInfo.totalOperationNum = updateSuperGroups.Length;
            foreach (int superGroupId in updateSuperGroups)
            {
                ProtCidSettings.progressInfo.currentFileName = superGroupId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    ClusterChainGroupInterfaces(superGroupId);
                    ChangeCfGroupIDsInSuperGroup(superGroupId);

                    DeleteObsDataInDb(superGroupId);
                    InsertDataToDb();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupId.ToString() + ": " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(superGroupId.ToString() + ": " + ex.Message);
                }
                ProtCidSettings.logWriter.WriteLine(superGroupId.ToString());
                ProtCidSettings.logWriter.Flush();
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Update clustering done!");
            ProtCidSettings.logWriter.Flush();
        }
        #endregion

        #region for user-defined groups
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupIds"></param>
        public void ClusterUserDefinedGroupInterfaces(int[] updateSuperGroups)
        {
            InitializeTable();

            AppSettings.parameters.simInteractParam.interfaceSimCutoff = 0.20;
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Clustering interfaces";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update clustering interfaces in superset groups");
            ProtCidSettings.progressInfo.totalStepNum = updateSuperGroups.Length;
            ProtCidSettings.progressInfo.totalOperationNum = updateSuperGroups.Length;

            // the group relation is part of the chain relation
            // that is, one of PFAM domain in the chain
            groupRelType = "contain";

            foreach (int superGroupId in updateSuperGroups)
            {
                ProtCidSettings.progressInfo.currentFileName = superGroupId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                ClusterChainGroupInterfaces(superGroupId);
                ChangeCfGroupIDsInSuperGroup(superGroupId);

                DeleteObsDataInDb(superGroupId);
                InsertDataToDb();
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.Flush(); 
        }
        #endregion

        #region for debug
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateSuperGroups"></param>
        public void UpdateSuperGroupClusters()
        {
            StreamWriter dataWriter = new StreamWriter("UpdateSuperGroups.txt", true);
     //      int[] missingClusterSuperGroups = GetMissingClusterSuperGroups ();
            int[] updateSuperGroups = GetSuperGroupsWithDuplicateInterfaces();
            foreach (int updateSuperGroup in updateSuperGroups)
            {
                dataWriter.WriteLine(updateSuperGroup.ToString ());
            }
            dataWriter.Close();

            UpdateSuperGroupClusters (updateSuperGroups);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int[] GetSuperGroupsWithDuplicateInterfaces()
        {
            string querystring = "Select SuperGroupSeqID, PdbID, InterfaceID, Count(Distinct ClusterID) As ClusterCount " + 
                " From PfamSuperInterfaceClusters Group By SuperGroupSeqID, PdbID, InterfaceID;";
            DataTable interfaceClusterCountTable = ProtCidSettings.protcidQuery.Query( querystring);
            List<int> superGroupIdList = new List<int> ();
            int superGroupId = 0;
            int clusterCount = 0;
            foreach (DataRow clusterCountRow in interfaceClusterCountTable.Rows)
            {
                clusterCount = Convert.ToInt32(clusterCountRow["ClusterCount"].ToString ());
                if (clusterCount > 1)
                {
                    superGroupId = Convert.ToInt32(clusterCountRow["SuperGroupSeqID"].ToString ());
                    if (!superGroupIdList.Contains(superGroupId))
                    {
                        superGroupIdList.Add(superGroupId);
                    }
                }
            }
            return superGroupIdList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int[] GetMissingClusterSuperGroups()
        {
            int groupSeqId = 0;
            List<int> superGroupIdList = new List<int> ();
            string listFileName = "MissClusterSuperGroups.txt";
            if (File.Exists(listFileName))
            {
                StreamReader dataReader = new StreamReader(listFileName);
                string line = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    superGroupIdList.Add(Convert.ToInt32 (line));
                }
                dataReader.Close();
            }
            else
            {
                string queryString = "Select Distinct GroupSeqID From PfamInterfaceClusters;";
                DataTable groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);

                foreach (DataRow groupRow in groupIdTable.Rows)
                {
                    groupSeqId = Convert.ToInt32(groupRow["GroupSeqID"].ToString());
                    if (!AreGroupClustersInSuperGroup(groupSeqId))
                    {
                        int[] superGroupIds = GetMissingSuperGroupSeqIDsForGroup(groupSeqId);
                        foreach (int superGroupId in superGroupIds)
                        {
                            if (!superGroupIdList.Contains(superGroupId))
                            {
                                superGroupIdList.Add(superGroupId);
                            }
                        }
                    }
                }
                StreamWriter dataWriter = new StreamWriter(listFileName);
                foreach (int superGroupId in superGroupIdList)
                {
                    dataWriter.WriteLine(superGroupId.ToString());
                }
                dataWriter.Close();
            }
            return superGroupIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <returns></returns>
        private bool AreGroupClustersInSuperGroup(int groupSeqId)
        {
            string queryString = string.Format("Select * From PfamSuperInterfaceClusters Where GroupSeqID = {0};",
                groupSeqId);
            DataTable superClusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (superClusterTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        private int[] GetMissingSuperGroupSeqIDsForGroup(int groupSeqId)
        {
            string queryString = string.Format("Select SuperGroupSeqID From PfamSuperGroups Where GroupSeqID = {0};",
                groupSeqId);
            DataTable superGroupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<int> missClusterSuperGroupList = new List<int> ();
            int superGroupId = 0;
            foreach (DataRow superGroupRow in superGroupIdTable.Rows)
            {
                superGroupId = Convert.ToInt32(superGroupRow["SuperGroupSeqID"].ToString ());
                if (!AreSuperGroupClustersExist(superGroupId))
                {
                    missClusterSuperGroupList.Add(superGroupId);
                }
            }
            return missClusterSuperGroupList.ToArray ();
        }

        private bool AreSuperGroupClustersExist(int superGroupId)
        {
            string queryString = string.Format("Select * From PfamSuperInterfaceClusters Where SuperGroupSeqID = {0};", superGroupId);
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (clusterTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int[] GetSuperGroupIDs()
        {
            System.IO.StreamReader dataReader = new System.IO.StreamReader("ClusterLog.txt");
            List<int> groupList = new List<int> ();
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split(' ');
                if (fields.Length > 3)
                {
                    groupList.Add(Convert.ToInt32(fields[2]));
                }
            }
            dataReader.Close();
            return groupList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int[] GetNewSuperGroups()
        {
            System.IO.StreamReader dataReader = new System.IO.StreamReader("NewSuperGroups.txt");
            string line = "";
            List<int> newsuperGroupList = new List<int> ();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                newsuperGroupList.Add(Convert.ToInt32(fields[0]));
            }
            dataReader.Close();
            return newsuperGroupList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int[] GetMessedSuperGroupIDs()
        {
            /*       string queryString = "Select Distinct SuperGroupSeqId From PfamSuperInterfaceClusters " +
                       " Where InterfaceID = 0;";
                   DataTable superGroupIdTable = dbQuery.Query(queryString);
                   int[] superGroupIds = new int[superGroupIdTable.Rows.Count];
                   int count = 0;
                   foreach (DataRow groupIdRow in superGroupIdTable.Rows)
                   {
                       superGroupIds[count] = Convert.ToInt32(groupIdRow["SuperGroupSeqID"].ToString ());
                       count++;
                   }*/
            System.IO.StreamReader dataReader = new System.IO.StreamReader("InterfaceDbLog0.txt");
            string line = "";
            string preLine = "";
            int superGroupId = -1;
            List<int> superGroupIdList =new List<int> ();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split(' ');
                if (fields.Length == 1)
                {
                    preLine = line;
                    continue;
                }

                //        superGroupIdList.Add(Convert.ToInt32 (fields[2]));
                try
                {
                    superGroupId = Convert.ToInt32(preLine);
                }
                catch
                {
                    continue;
                }
                superGroupIdList.Add(superGroupId);
                preLine = line;
            }
            dataReader.Close();
            return superGroupIdList.ToArray ();
        }
        #endregion
    }
}
