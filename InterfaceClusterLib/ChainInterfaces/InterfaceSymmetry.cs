using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using DbLib;
using CrystalInterfaceLib.StructureComp.HomoEntryComp;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Settings;
using CrystalInterfaceLib.ProtInterfaces;
using InterfaceClusterLib.Alignments;
using AuxFuncLib;
using ProtCidSettingsLib;
using InterfaceClusterLib.InterfaceProcess;

namespace InterfaceClusterLib.ChainInterfaces
{
    public class InterfaceSymmetry
    {
        #region member variables
        public HomoEntryAlignInfo homoEntryAlignInfo = new HomoEntryAlignInfo();
        private SupInterfacesComp interfaceComp = new SupInterfacesComp();
        private InterfaceRetriever interfaceReader = new InterfaceRetriever();
        private InterfaceSymmetryIndex symmetryIndex = new InterfaceSymmetryIndex();
        private DbUpdate protcidUpdate = new DbUpdate (ProtCidSettings.protcidDbConnection);
        private double symmetryPercentCutoff = 0.9;
        private double symmetryJindexCutoff = 0.9;
        private Dictionary<string, string> entityCrcDict = null;
        private Dictionary<Tuple<string, string>, CrcSeqAlignInfo> crcPairAlignInfoDict = null;
        #endregion

        #region superpose homo-dimers
        /// <summary>
        /// 
        /// </summary>
        public void SortHomoDimerInterfacesInCluster()
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }
            if (AppSettings.parameters == null)
            {
                AppSettings.LoadParameters();
            }
            string queryString = "Select Distinct SuperGroupSeqID From PfamSuperClusterEntryInterfaces;";
            DataTable groupTable = ProtCidSettings.protcidQuery.Query( queryString);
            int superGroupId = -1;
            StreamWriter logWriter = new StreamWriter("ClusterHomoDimersSortLog.txt");
            string dataLine = "";
            foreach (DataRow groupRow in groupTable.Rows)
            {
                superGroupId = Convert.ToInt32(groupRow["SuperGroupSeqID"].ToString());

                if (!IsSuperGroupSamePfamArch(superGroupId))
                {
                    continue;
                }
                int[] clusterIds = GetGroupClusterIds(superGroupId);
                foreach (int clusterId in clusterIds)
                {
                    string[] clusterInterfaces = GetClusterInterfaces(superGroupId, clusterId);
                    string firstInterface = GetRepInterfaceInAlphabetOrder(superGroupId, clusterId);
                    string[] nonSymmetryInterfaces = GetNonSymmetryDimers(clusterInterfaces);
                    if (nonSymmetryInterfaces.Length == 0)
                    {
                        logWriter.WriteLine(superGroupId.ToString() + "  " + clusterId.ToString() +
                            " no non-symmetric homo-dimers ");
                        continue;
                    }
                    dataLine = superGroupId.ToString() + "   " + clusterId.ToString() + "   " + firstInterface;
                    foreach (string nonSymmetryInterface in nonSymmetryInterfaces)
                    {
                        if (firstInterface == nonSymmetryInterface)
                        {
                            continue;
                        }
                        dataLine += ("," + nonSymmetryInterface);
                    }
                    logWriter.WriteLine(dataLine);
                }
                logWriter.Flush();
            }
            logWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private bool IsSuperGroupSamePfamArch(int superGroupId)
        {
            string queryString = string.Format("Select FamilyString From PfamSuperGroups " +
                " Where SuperGroupSeqID = {0};", superGroupId);
            DataTable familyStringTable = ProtCidSettings.protcidQuery.Query( queryString);
            string familyString = familyStringTable.Rows[0]["FamilyString"].ToString();
            if (familyString.IndexOf(";") > -1)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private int[] GetGroupClusterIds(int superGroupId)
        {
            string queryString = string.Format("Select Distinct ClusterID From PfamSuperInterfaceClusters " +
                " Where SuperGroupSeqID = {0};", superGroupId);
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] clusterIds = new int[clusterTable.Rows.Count];
            int count = 0;
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                clusterIds[count] = Convert.ToInt32(clusterRow["ClusterID"].ToString());
                count++;
            }
            return clusterIds;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string GetRepInterfaceInAlphabetOrder(int superGroupId, int clusterId)
        {
            string queryString = string.Format("Select First 1 * From PfamSuperInterfaceClusters " +
                " Where SuperGroupSeqID = {0} AND ClusterID = {1} Order By PdbId, InterfaceID;", superGroupId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string firstInterface = clusterInterfaceTable.Rows[0]["PdbID"].ToString() +
                clusterInterfaceTable.Rows[0]["InterfaceID"].ToString();
            return firstInterface;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaces"></param>
        /// <returns></returns>
        private string[] GetNonSymmetryDimers(string[] clusterInterfaces)
        {
            string pdbId = "";
            int interfaceId = -1;
            List<string> nonSymmetryInterfaceList = new List<string>();
            bool isInterfaceSym = false;
            foreach (string clusterInterface in clusterInterfaces)
            {
                pdbId = clusterInterface.Substring(0, 4);
                interfaceId = Convert.ToInt32(clusterInterface.Substring(4, clusterInterface.Length - 4));
                isInterfaceSym = IsInterfaceSymmetry(pdbId, interfaceId);
                if (! isInterfaceSym)
                {
                    nonSymmetryInterfaceList.Add(clusterInterface);
                }
       /*         int[] contacts = ReadDimerCommonResidueContacts(pdbId, interfaceId);
                if (contacts == null)
                {
                    contacts = GetNumOfContactsFromFile(pdbId, interfaceId);
                }
                int numOfCommonContacts = contacts[0];
                int numOfContacts = contacts[1];
                // including those the contacts info may not in the db
                if (numOfCommonContacts == 0)
                {
                    nonSymmetryInterfaceList.Add(clusterInterface);
                }
                else
                {
                    double percent = (double)numOfCommonContacts / (double)numOfContacts;
                    if (percent < 0.9)
                    {
                        nonSymmetryInterfaceList.Add(clusterInterface);
                    }
                }*/
            }
            return nonSymmetryInterfaceList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="crystInterface"></param>
        /// <returns></returns>
        private int[] GetNumOfContactsFromFile(string pdbId, int interfaceId)
        {
            int[] interfaceIds = new int[1];
            interfaceIds[0] = interfaceId;
            InterfaceChains[] interfaceChains = interfaceReader.GetCrystInterfaces(pdbId, interfaceIds, "cryst");

            List<string> chainAContactList = new List<string>();
            List<string> chainBContactList = new List<string>();
            foreach (string seqPair in interfaceChains[0].seqDistHash.Keys)
            {
                string[] seqIds = seqPair.Split('_');
                chainAContactList.Add(seqIds[0] + "_" + seqIds[1]);
                chainBContactList.Add(seqIds[1] + "_" + seqIds[0]);
            }
            int numOfCommonContacts = GetNumOfCommonContacts(chainAContactList, chainBContactList);
            int totalContacts = interfaceChains[0].seqDistHash.Count;
            int[] contacts = new int[2];
            contacts[0] = numOfCommonContacts;
            contacts[1] = totalContacts;

            return contacts;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private bool IsInterfaceSymmetry (string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select SymmetryIndex From CrystEntryInterfaces Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable jindexTable = ProtCidSettings.protcidQuery.Query(queryString);
            double symJindex = -1.0;
            if (jindexTable.Rows.Count > 0)
            {
                symJindex = Convert.ToDouble(jindexTable.Rows[0]["SymmetrIndex"].ToString ());
                if (symJindex >= symmetryJindexCutoff)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region add symmetry jaccard index
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainInterface"></param>
        /// <returns></returns>
        public double CalculateInterfaceSymmetryJindex (InterfaceChains chainInterface)
        {
            double symJindex = symmetryIndex.CalculateHomoInterfaceSymmetry(chainInterface);
            return symJindex;
        }
        /// <summary>
        /// 
        /// </summary>
        public void CalculateInterfaceSymmetryJindex ()
        {
            ProtCidSettings.logWriter.WriteLine("Calculate symmetry jaccard indexes. ");
            string queryString = "Select Distinct SuperGroupSeqId From PfamSuperGroups Where ChainRelPfamArch NOT Like '%);(%';";
            DataTable chainGroupTable = ProtCidSettings.protcidQuery.Query (queryString);
            int chainGroupId = 0;
            List<string> parsedEntryList = new List<string>();
            entityCrcDict = new Dictionary<string, string>();
            crcPairAlignInfoDict = new Dictionary<Tuple<string, string>, CrcSeqAlignInfo>();
            
            foreach (DataRow groupIdRow in chainGroupTable.Rows)
            {
                entityCrcDict.Clear();
                crcPairAlignInfoDict.Clear();
                
                chainGroupId = Convert.ToInt32(groupIdRow["SuperGroupSeqID"].ToString ());

                if (chainGroupId < 3114)
                {
                    continue;
                }

                ProtCidSettings.logWriter.WriteLine(chainGroupId);
                try
                {
                    CalculateGroupHomoInterfaceSymmetryIndexes(chainGroupId, ref parsedEntryList);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine(chainGroupId + " calculating symmetry jaccard index error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainGroupId"></param>
        /// <param name="parsedEntryList"></param>
        /// <returns></returns>
        public void CalculateGroupHomoInterfaceSymmetryIndexes (int chainGroupId, ref List<string> parsedEntryList)
        {
            string[] groupEntries = GetChainGroupEntries (chainGroupId, parsedEntryList);
            if (groupEntries.Length == 0)
            {
                return;
            }
            Dictionary<string, List<int>> entryHomoInterfaceListDict = GetHomoChainInterfaces(groupEntries);
            double symmetryJindex = 0;
            foreach (string pdbId in entryHomoInterfaceListDict.Keys)
            {               
                parsedEntryList.Add(pdbId);
                int[] interfaceIds = entryHomoInterfaceListDict[pdbId].ToArray ();
                InterfaceChains[] homoInterfaces = interfaceReader.GetCrystInterfaces(pdbId, interfaceIds, "cryst");
                if (homoInterfaces == null)
                {
                    ProtCidSettings.logWriter.WriteLine(pdbId + " no homo-dimer interfaces.");
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }
                foreach (InterfaceChains homoInterface in homoInterfaces)
                {
                    try
                    {
                        symmetryJindex = symmetryIndex.CalculateInterfaceSymmetry(homoInterface, entityCrcDict, crcPairAlignInfoDict);
                        UpdateSymmetryJaccardIndex(pdbId, homoInterface.interfaceId, symmetryJindex);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.logWriter.WriteLine(pdbId + homoInterface.interfaceId + " calculating symmetry jaccard index error: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                }
            }           
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        private Dictionary<string, List<int>> GetHomoChainInterfaces (string[] entries)
        {
            Dictionary<string, bool> entryEntityPairListDict = new Dictionary<string, bool>();
            DataTable interfaceTable = null;
            string queryString = "";
            for (int i = 0; i < entries.Length; i +=300)
            {
                string[] subEntries = ParseHelper.GetSubArray(entries, i, 300);
                queryString = string.Format("Select PdbID, InterfaceID, EntityID1, EntityID2 From CrystEntryInterfaces Where PdbID IN ({0}) AND SymmetryIndex < 0;", ParseHelper.FormatSqlListString (subEntries));
                DataTable subInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(subInterfaceTable, ref interfaceTable);
            }
            Dictionary<string, List<int>> entryHomoInterfaceListDict = new Dictionary<string, List<int>>();
            string pdbId = "";
            int entityId1 = 0;
            int entityId2 = 0;
            int interfaceId = 0;
            foreach (DataRow interfaceRow in interfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                entityId1 = Convert.ToInt32(interfaceRow["EntityID1"].ToString ());
                entityId2 = Convert.ToInt32(interfaceRow["EntityID2"].ToString ());                
                if (AreEntitySamSequence (pdbId, entityId1, entityId2, entryEntityPairListDict))
                {
                    interfaceId = Convert.ToInt32 (interfaceRow["InterfaceID"].ToString ());
                    if (entryHomoInterfaceListDict.ContainsKey (pdbId))
                    {
                        entryHomoInterfaceListDict[pdbId].Add(interfaceId);
                    }
                    else
                    {
                        List<int> interfaceIdList = new List<int>();
                        interfaceIdList.Add(interfaceId);
                        entryHomoInterfaceListDict.Add(pdbId, interfaceIdList);
                    }
                }
            }
            return entryHomoInterfaceListDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId1"></param>
        /// <param name="entityId2"></param>
        /// <returns></returns>
        private bool AreEntitySamSequence (string pdbId, int entityId1, int entityId2, Dictionary<string, bool> entryEntityPairListDict)
        {
            if (entityId1 == entityId2)
            {
                return true;
            }
            else
            {
                string entityPair = pdbId + "_" + entityId1 + "_" + entityId2;
                if (entityId1 > entityId2)
                {
                    entityPair = pdbId + "_" + entityId2 + "_" + entityId1;
                }
                if (entryEntityPairListDict.ContainsKey(entityPair))
                {
                    return entryEntityPairListDict[entityPair];
                }
                else
                {
                    bool homoSeq = symmetryIndex.AreSameSequence(pdbId, entityId1, entityId2);
                    entryEntityPairListDict.Add(entityPair, homoSeq);
                    return homoSeq;
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainGroupId"></param>
        /// <returns></returns>
        private string[] GetChainGroupEntries (int chainGroupId, List<string> parsedEntryList )
        {
            List<string> groupEntryList = new List<string>();
            string queryString = string.Format("Select Distinct pdbId From PfamHomoSeqInfo, PfamSuperGroups " +
                " Where SuperGroupSeqId = {0} and PfamHomoSeqInfo.GroupSeqId = PfamSuperGroups.GroupSeqId;", chainGroupId);
            DataTable repEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId = "";
            foreach (DataRow entryRow in repEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (! parsedEntryList.Contains(pdbId))
                {
                    groupEntryList.Add(pdbId);
         //           parsedEntryList.Add(pdbId);
                }
            }
            queryString = string.Format("Select Distinct pdbId2 As PdbID From PfamHomoRepEntryAlign, PfamSuperGroups " + 
                " Where SuperGroupSeqId = {0} and PfamHomoRepEntryAlign.GroupSeqId = PfamSuperGroups.GroupSeqId;", chainGroupId);
            DataTable homoEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow entryRow in homoEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (!parsedEntryList.Contains(pdbId))
                {
                    groupEntryList.Add(pdbId);
       //             parsedEntryList.Add(pdbId);
                }
            }
            return groupEntryList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="symmetryJindex"></param>
        private void UpdateSymmetryJaccardIndex (string pdbId, int interfaceId, double symmetryJindex)
        {
            if (symmetryJindex > -1)
            {
                string updateString = string.Format("Update CrystEntryInterfaces Set SymmetryIndex = {0} Where PdbID = '{1}' AND InterfaceID = {2};", symmetryJindex, pdbId, interfaceId);
                protcidUpdate.Update(updateString);
            }
        }
        #endregion

        #region common contacts
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string[] GetClusterInterfaces(int superGroupId, int clusterId)
        {
            string queryString = string.Format("Select PdbId, InterfaceID From PfamSuperClusterEntryInterfaces" +
                " Where SuperGroupSeqID = {0} AND ClusterID = {1} Order By PdbID, InterfaceID;", superGroupId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> repInterfaceList = new List<string>();
            List<string> addedEntryList = new List<string>();
            string pdbId = "";
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                if (addedEntryList.Contains(pdbId))
                {
                    continue;
                }
                repInterfaceList.Add(pdbId + interfaceRow["InterfaceId"].ToString());
                addedEntryList.Add(pdbId);
            }
            return repInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private int[] ReadDimerCommonResidueContacts(string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select SeqID1, SeqID2 From SgInterfaceResidues " +
                " Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable contactsTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (contactsTable.Rows.Count == 0)
            {
                return null;
            }

            List<string> chainAContactList = new List<string>();
            List<string> chainBContactList = new List<string>();
            string seqA = "";
            string seqB = "";
            foreach (DataRow contactRow in contactsTable.Rows)
            {
                seqA = contactRow["SeqID1"].ToString().TrimEnd();
                seqB = contactRow["SeqID2"].ToString().TrimEnd();
                chainAContactList.Add(seqA + "_" + seqB);
                chainBContactList.Add(seqB + "_" + seqA);
            }
            int numOfCommonContacts = GetNumOfCommonContacts(chainAContactList, chainBContactList);
            int[] numOfContacts = new int[3];
            numOfContacts[0] = numOfCommonContacts;
            numOfContacts[1] = chainAContactList.Count;
            numOfContacts[2] = chainBContactList.Count;
            return numOfContacts;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainAContactList"></param>
        /// <param name="chainBContactList"></param>
        /// <returns></returns>
        private int GetNumOfCommonContacts(List<string> chainAContactList, List<string> chainBContactList)
        {
            int numOfCommonContacts = 0;
            foreach (string seqPairA in chainAContactList)
            {
                if (chainBContactList.Contains(seqPairA))
                {
                    numOfCommonContacts++;
                }
            }
            return numOfCommonContacts;
        }
        #endregion

        #region interface symmetry
        /// <summary>
        /// check symmetry when two chains with same sequence
        /// </summary>
        /// <param name="chainInterface"></param>
        /// <returns></returns>
        public bool IsInterfaceSymmetry(InterfaceChains chainInterface)
        {
            if (chainInterface.entityId1 != chainInterface.entityId2)
            {
                return false;
            }
            List<string> chainAContactList = new List<string>();
            List<string> chainBContactList = new List<string>();
            foreach (string seqPair in chainInterface.seqDistHash.Keys)
            {
                string[] seqIds = seqPair.Split('_');
                chainAContactList.Add(seqIds[0] + "_" + seqIds[1]);
                chainBContactList.Add(seqIds[1] + "_" + seqIds[0]);
            }

            int numOfCommonContacts = GetNumOfCommonContacts(chainAContactList, chainBContactList);
            int totalContacts = chainInterface.seqDistHash.Count;
            int[] contacts = new int[2];
            contacts[0] = numOfCommonContacts;
            contacts[1] = totalContacts;

            double percent = (double)numOfCommonContacts / (double)totalContacts;
            if (percent < symmetryPercentCutoff)
            {
                return false;
            }
            return true;
        }

        #endregion
    }
}
