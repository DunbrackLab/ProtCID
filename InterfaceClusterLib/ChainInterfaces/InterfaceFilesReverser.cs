using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Settings;
using DbLib;
using AuxFuncLib;
using PfamLib.PfamArch;
using InterfaceClusterLib.InterfaceProcess;
using InterfaceClusterLib.InterfaceComp;

namespace InterfaceClusterLib.ChainInterfaces
{
    public class InterfaceFilesReverser 
    {
        #region member variables
        private CrystEntryInterfaceComp interfaceComp = new CrystEntryInterfaceComp();
        private CrystInterfaceProcessor interfaceGenerator = new CrystInterfaceProcessor();
        private InterfaceRetriever interfaceRetriever = new InterfaceRetriever();
        private PfamArchitecture pfamArch = new PfamArchitecture(); 
        private DbQuery dbQuery = new DbQuery();
        private DbInsert dbInsert = new DbInsert();
        private DataTable symInterfaceTable = null;
        #endregion
        

        /// <summary>
        /// 
        /// </summary>
        public string ReverseClusterInterfaceFiles()
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }
            if (AppSettings.parameters == null)
            {
                AppSettings.LoadParameters();
            }
      //      string reverseInterfaceFile = "ReverseInterfacesInCluster.txt";

            string querystring = "Select PdbID, InterfaceID From CrystEntryInterfaces Where IsSymmetry = '1';";
            symInterfaceTable = ProtCidSettings.protcidQuery.Query( querystring);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Find Non-symmetry interface files in the clusters.");
            string nonSymInterfaceFile = GetClusterNonSymmetryInterfaces();
            
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare Non symmetry interface files to the first interface in the cluster (in alphabet order)");
            string nonSymInterfaceCompFile = interfaceComp.CompareClusterNonSymInterfaces(nonSymInterfaceFile);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Reverse interface files in pdb format.");
            string reverseInterfaceFile = GetInterfacesToReversed(nonSymInterfaceCompFile);
            interfaceGenerator.ReverseInterfaceChains(reverseInterfaceFile);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("The current working directory: " + System.Environment.CurrentDirectory);
            ProtCidSettings.logWriter.WriteLine("Reverse interface files in pdb format.");
            ProtCidSettings.logWriter.WriteLine("The current working directory: " + System.Environment.CurrentDirectory);
           
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Reverse hetero interface files in reversed pfam arch order");
            ReverseInterfaceFilesInDifPfamOrder();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("The current working directory: " + System.Environment.CurrentDirectory);
            ProtCidSettings.logWriter.WriteLine("Reverse hetero interface files in reversed pfam arch order");
            ProtCidSettings.logWriter.WriteLine("The current working directory: " + System.Environment.CurrentDirectory);

            return reverseInterfaceFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateGroups"></param>
  //      public string ReverseClusterInterfaceFiles(int[] updateGroups)
        public string ReverseClusterInterfaceFiles (Dictionary<int, Dictionary<int, string[]>> updateSuperGroupHash)
        {
            List<int> updateSuperGroupList = new List<int> (updateSuperGroupHash.Keys);

            string updateReverseInterfaceFile = ReverseClusterInterfaceFiles(updateSuperGroupList.ToArray ());
            
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Reverse hetero interface files in reversed pfam arch order");
            List<string> updateEntryList = new List<string> ();
            foreach (int superGroupId in updateSuperGroupHash.Keys)
            {
                foreach (int groupId in updateSuperGroupHash[superGroupId].Keys)
                {
                    foreach (string entry in updateSuperGroupHash[superGroupId][groupId])
                    {
                        if (!updateEntryList.Contains(entry))
                        {
                            updateEntryList.Add(entry);
                        }
                    }
                }
            }

            ReverseInterfaceFilesInDifPfamOrder(updateEntryList.ToArray ());

            return updateReverseInterfaceFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateGroups"></param>
         public string ReverseClusterInterfaceFiles(int[] updateGroups)
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }
            if (AppSettings.parameters == null)
            {
                AppSettings.LoadParameters();
            }
            string updateReverseInterfaceFile = Path.Combine (ProtCidSettings.applicationStartPath, "ReverseInterfacesInCluster.txt");

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Non-symmetry interface files in the clusters.");
            string updateNonSymInterfaceFile = UpdateClusterNonSymmetryInterfaces(updateGroups);
       //     string updateNonSymInterfaceFile = "UpdateClusterNonSymInterfaces.txt";

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare Non symmetry interface files to the first interface in the cluster (in alphabet order)");
            string updateNonSymInterfaceCompFile = interfaceComp.CompareClusterNonSymInterfaces(updateNonSymInterfaceFile);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Reverse interface files in pdb format.");
            updateReverseInterfaceFile = GetInterfacesToReversed(updateNonSymInterfaceCompFile);

            interfaceGenerator.ReverseInterfaceChains(updateReverseInterfaceFile);

            return updateReverseInterfaceFile;
        }

        #region non symmetry interface files
        /// <summary>
        /// 
        /// </summary>
        public string GetClusterNonSymmetryInterfaces ()
        {
            string queryString = "Select Distinct SuperGroupSeqID From PfamSuperClusterEntryInterfaces;";
            DataTable groupTable = ProtCidSettings.protcidQuery.Query( queryString);
            string nonSymInterfaceFile = "ClusterNonSymInterfaces.txt";
            StreamWriter dataWriter = new StreamWriter(nonSymInterfaceFile);
            int[] groupIds = new int[groupTable.Rows.Count];
            int count = 0;
            foreach (DataRow groupRow in groupTable.Rows)
            {
                groupIds[count] = Convert.ToInt32(groupRow["SuperGroupSeqID"].ToString ());
                count++;
            }
            GetNonSymmetryInterfaces(groupIds, dataWriter);
            dataWriter.Close();
            return nonSymInterfaceFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groups"></param>
        /// <returns></returns>
        public string UpdateClusterNonSymmetryInterfaces (int[] updateGroups)
        {
            string nonSymInterfaceFile = "UpdateClusterNonSymInterfaces.txt";
            StreamWriter dataWriter = new StreamWriter(nonSymInterfaceFile);
            GetNonSymmetryInterfaces(updateGroups, dataWriter);
            dataWriter.Close();
            return nonSymInterfaceFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groups"></param>
        /// <param name="dataWriter"></param>
        private void GetNonSymmetryInterfaces(int[] groups, StreamWriter dataWriter)
        {
            string dataLine = "";
            foreach (int superGroupId in groups)
            {
                if (!IsSuperGroupSamePfamArch(superGroupId))
                {
                    continue;
                }
                int[] clusterIds = GetGroupClusterIds(superGroupId);
                foreach (int clusterId in clusterIds)
                {
                    try
                    {
                        string[] clusterInterfaces = GetClusterInterfaces(superGroupId, clusterId);
                //        string firstInterface = GetRepInterfaceInAlphabetOrder(superGroupId, clusterId);
                        string firstInterface = clusterInterfaces[0];
                        string[] nonSymmetryInterfaces = GetNonSymmetryDimers(clusterInterfaces);
                        if (nonSymmetryInterfaces.Length == 0)
                        {
                            dataWriter.WriteLine(superGroupId.ToString() + "  " + clusterId.ToString() +
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
                        dataWriter.WriteLine(dataLine);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupId.ToString () + 
                            clusterId.ToString () + " errors: " + ex.Message);
                    }
                }
            }
            dataWriter.Flush();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private bool IsSuperGroupSamePfamArch(int superGroupId)
        {
            string queryString = string.Format("Select ChainRelPfamArch From PfamSuperGroups " +
                " Where SuperGroupSeqID = {0};", superGroupId);
            DataTable familyStringTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (familyStringTable.Rows.Count > 0)
            {
                string familyString = familyStringTable.Rows[0]["ChainRelPfamArch"].ToString();
                if (familyString.IndexOf(";") > -1)
                {
                    return false;
                }
            }
            return true;
        }

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
            List<string> nonSymmetryInterfaceList = new List<string> ();
            bool isSymmetry = false;
            foreach (string clusterInterface in clusterInterfaces)
            {
                pdbId = clusterInterface.Substring(0, 4);
                interfaceId = Convert.ToInt32(clusterInterface.Substring(4, clusterInterface.Length - 4));
                isSymmetry = IsInterfaceSymmetry(pdbId, interfaceId);
                if (! isSymmetry)
                {
                    nonSymmetryInterfaceList.Add(clusterInterface);
                }
/*
                int[] contacts = GetNumOfContactsFromFile(pdbId, interfaceId);
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
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private bool IsInterfaceSymmetry(string pdbId, int interfaceId)
        {
            if (symInterfaceTable != null)
            {
                DataRow[] symInterfaceRows = symInterfaceTable.Select(string.Format ("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, interfaceId));
                if (symInterfaceRows.Length > 0)
                {
                    return true;
                }
            }
            else
            {
                string queryString = string.Format("Select IsSymmetry From CrystEntryInterfaces Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
                DataTable isSymmetryTable = ProtCidSettings.protcidQuery.Query( queryString);
                if (isSymmetryTable.Rows.Count > 0)
                {
                    string isSymString = isSymmetryTable.Rows[0]["IsSymmetry"].ToString();
                    if (isSymString == "1")
                    {
                        return true;
                    }
                }              
            }
            return false;
        }

        /// <summary>
        /// SgInterfaceResidues table is obsolete
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        public int[] ReadDimerCommonResidueContacts(string pdbId, int interfaceId)
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
        /// <param name="crystInterface"></param>
        /// <returns></returns>
        private int[] GetNumOfContactsFromFile(string pdbId, int interfaceId)
        {
            int[] interfaceIds = new int[1];
            interfaceIds[0] = interfaceId;
            InterfaceChains[] interfaceChains = interfaceRetriever.GetCrystInterfaces(pdbId, interfaceIds, "cryst");

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

        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        public string[] GetClusterInterfaces(int superGroupId, int clusterId)
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
        #endregion

        #region the list of interface files to be reversed
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterNonSymInterfaceCompFile"></param>
        /// <returns></returns>
        public string GetInterfacesToReversed(string clusterNonSymInterfaceCompFile)
        {
            //    StreamReader dataReader = new StreamReader("ClusterHomoDimerQscores.txt");
            StreamReader dataReader = new StreamReader(clusterNonSymInterfaceCompFile);
            string line = "";
            Dictionary<string, List<string>> clusterReverseInterfaceHash = new Dictionary<string,List<string>> ();
            string cluster = "";
            List<string> reverseInterfaceList = null;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("No alignments for") > -1)
                {
                    continue;
                }
                string[] fields = line.Split('\t');
                if (fields.Length == 1)
                {
                    if (cluster != "")
                    {
                        if (reverseInterfaceList.Count > 0)
                        {
                            clusterReverseInterfaceHash.Add(cluster, reverseInterfaceList);
                        }
                    }
                    cluster = line;
                    reverseInterfaceList = new List<string> ();
                }
                if (fields.Length == 5)
                {
                    if (fields[4] == "1")
                    {
                        if (!reverseInterfaceList.Contains(fields[1]))
                        {
                            reverseInterfaceList.Add(fields[1]);
                        }
                    }
                }
            }
            dataReader.Close();
            clusterReverseInterfaceHash.Add(cluster, reverseInterfaceList);
            string reverseInterfacesInClusterFile = "ReverseInterfacesInCluster.txt";
            StreamWriter dataWriter = new StreamWriter(reverseInterfacesInClusterFile);
            string dataLine = "";
            foreach (string reverseCluster in clusterReverseInterfaceHash.Keys)
            {
                dataLine = reverseCluster + ":";
                List<string> clusterReverseInterfaceList = clusterReverseInterfaceHash[reverseCluster];
                foreach (string reverseInterface in clusterReverseInterfaceList)
                {
                    dataLine += (reverseInterface + ",");
                }
                dataLine = dataLine.TrimEnd(',');
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();
            return reverseInterfacesInClusterFile;
        }
        #endregion

        #region heterodimer in pfam arch order
        /// <summary>
        /// 
        /// </summary>
        public void ReverseInterfaceFilesInDifPfamOrder()
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Find hetero interface files in different pfam arch order");
            string updateHeteroInterfaceListFile = FindEntryInterfacesInDifPfamOrder();

       //     string updateHeteroInterfaceListFile = "UpdateHeteroInterfaces.txt";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Regenerate interface files in the pfam arch order");
            interfaceGenerator.GenerateDifPfamArchEntryInterfaceFiles(updateHeteroInterfaceListFile);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void ReverseInterfaceFilesInDifPfamOrder(string[] updateEntries)
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Find hetero interface files in different pfam arch order");
            string updateHeteroInterfaceListFile = FindEntryInterfaceInDifPfamOrder(updateEntries);

         //   string updateHeteroInterfaceListFile = "UpdateHeteroInterfaces.txt";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Regenerate interface files in the pfam arch order");
            interfaceGenerator.GenerateDifPfamArchEntryInterfaceFiles(updateHeteroInterfaceListFile);
        }
        /// <summary>
        /// 
        /// </summary>
        public string FindEntryInterfacesInDifPfamOrder()
        {
            string updateHetereInterfaceListFile = "UpdateHeteroInterfaces.txt";
            StreamWriter updateInterfaceWriter = new StreamWriter(updateHetereInterfaceListFile);
            string queryString = "Select Distinct PdbID From CrystEntryInterfaces WHere EntityID1 <> EntityID2;";
            DataTable heterInterfaceEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            foreach (DataRow entryRow in heterInterfaceEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
             
                FindEntryInterfacesInDifPfamOrder(pdbId, updateInterfaceWriter);
            }
            updateInterfaceWriter.Close();
            return updateHetereInterfaceListFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        public string FindEntryInterfaceInDifPfamOrder(string[] updateEntries)
        {
            string updateHetereInterfaceListFile = "UpdateHeteroInterfaces.txt";
            StreamWriter updateInterfaceWriter = new StreamWriter(updateHetereInterfaceListFile);

            foreach (string pdbId in updateEntries )
            {
                FindEntryInterfacesInDifPfamOrder(pdbId, updateInterfaceWriter);
            }
            updateInterfaceWriter.Close();
            return updateHetereInterfaceListFile;
        }
        /// <summary>
        /// 
        /// </summary>
        private void FindEntryInterfacesInDifPfamOrder(string pdbId, StreamWriter updateInterfaceWriter)
        {
            Dictionary<int, string> entityPfamArchHash = null;
            string queryString = string.Format("Select * From CrystEntryInterfaces " +
                " Where PdbID = '{0}' AND EntityID1 <> EntityID2;", pdbId);
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            interfaceTable.TableName = "CrystEntryInterfaces";
            if (interfaceTable.Rows.Count > 0)
            {
                entityPfamArchHash = GetEntryEntityPfamArchHash(pdbId);
            }
            string dataLine = "";
            foreach (DataRow interfaceRow in interfaceTable.Rows)
            {
                if (!IsInterfaceInRightPfamOrder(interfaceRow, entityPfamArchHash))
                {
                //    dataWriter.WriteLine(ParseHelper.FormatDataRow (interfaceRow));
                    dataLine += (interfaceRow["InterfaceID"].ToString() + "\t");

                    ReverseInterfaceRowInDb(interfaceRow);
                }
            }
            if (dataLine != "")
            {
                dataLine = pdbId + "\t" + dataLine.TrimEnd('\t');
                updateInterfaceWriter.WriteLine(dataLine);
            }
        //    dataWriter.Flush();
            updateInterfaceWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceRow"></param>
        /// <param name="chainPfamArchHash"></param>
        /// <returns></returns>
        private bool IsInterfaceInRightPfamOrder(DataRow interfaceRow, Dictionary<int, string> entityPfamArchHash)
        {
            int entityId1 = Convert.ToInt32 (interfaceRow["EntityID1"].ToString());
            int entityId2 = Convert.ToInt32 (interfaceRow["EntityID2"].ToString());
            string chainPfamArchString1 = entityPfamArchHash[entityId1];
            string chainPfamArchString2 = entityPfamArchHash[entityId2];
            if (string.Compare(chainPfamArchString1, chainPfamArchString2) > 0)
            {
                return false;
            }
            return true;
        }

        private void ReverseInterfaceRowInDb(DataRow interfaceRow)
        {
            object tempObj = interfaceRow["AsymChain1"];
            interfaceRow["AsymChain1"] = interfaceRow["AsymChain2"];
            interfaceRow["AsymChain2"] = tempObj;

            tempObj = interfaceRow["AuthChain1"];
            interfaceRow["AuthChain1"] = interfaceRow["AuthChain2"];
            interfaceRow["AuthChain2"] = tempObj;

            tempObj = interfaceRow["EntityID1"];
            interfaceRow["EntityID1"] = interfaceRow["EntityID2"];
            interfaceRow["EntityID2"] = tempObj;

            tempObj = interfaceRow["SymmetryString1"];
            interfaceRow["SymmetryString1"] = interfaceRow["SymmetryString2"];
            interfaceRow["SymmetryString2"] = tempObj;

            tempObj = interfaceRow["FullSymmetryString1"];
            interfaceRow["FullSymmetryString1"] = interfaceRow["FullSymmetryString2"];
            interfaceRow["FullSymmetryString2"] = tempObj;

            DeleteEntryInterface(interfaceRow["PdbID"].ToString(), Convert.ToInt32(interfaceRow["InterfaceID"].ToString()));
            dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, interfaceRow);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        private void DeleteEntryInterface(string pdbId, int interfaceId)
        {
            string deleteString = string.Format("Delete From CrystEntryInterfaces Where PdbId = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            ProtCidSettings.protcidQuery.Query( deleteString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<int, string> GetEntryEntityPfamArchHash(string pdbId)
        {
            string queryString = string.Format("Select * From PfamEntityPfamArch Where PdbID = '{0}' ORDER BY EntityID;", pdbId);
            DataTable pfamArchTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            Dictionary<int, string> entryChainPfamHash = new Dictionary<int,string> ();
            int entityId = -1;
            string chainPfamArchString = "";
            foreach (DataRow pfamArchRow in pfamArchTable.Rows)
            {
                entityId = Convert.ToInt32 (pfamArchRow["EntityID"].ToString());
                chainPfamArchString = pfamArch.GetEntityGroupPfamArch (pdbId, entityId);
                entryChainPfamHash.Add(entityId, chainPfamArchString);
            }
            return entryChainPfamHash;
        }
        #endregion  

        #region reverse an interface file
        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceFile"></param>
        public void ReverseInterfaceFile(string interfaceFile)
        {
            StreamReader interfaceFileReader = new StreamReader(interfaceFile);
            string line = "";
            string dataLine = "";
            string interfaceChainA = "";
            string interfaceChainB = "";
            string chainAAtomLines = "";
            string chainBAtomLines = "";
            string fileChain = "";
            while ((line = interfaceFileReader.ReadLine()) != null)
            {
                if (line.IndexOf("Interface Chain A") > -1)
                {
                    interfaceChainA = line;
                    line = interfaceFileReader.ReadLine();
                    interfaceChainB = line;
                    dataLine += (interfaceChainB + "\r\n");
                    dataLine += (interfaceChainA + "\r\n");
                }
                else if (line.IndexOf("ATOM  ") > -1)
                {
                    fileChain = GetInterfaceFileChain(line);
                    if (fileChain == "A")
                    {
                        chainAAtomLines += (line + "\r\n");
                    }
                    else if (fileChain == "B")
                    {
                        chainBAtomLines += (line + "\r\n");
                    }
                }
                else
                {
                    dataLine += (line + "\r\n");
                }
            }
            dataLine += chainBAtomLines;
            dataLine += chainAAtomLines;
            dataLine += "END";
            interfaceFileReader.Close();

            StreamWriter reverseInterfaceWriter = new StreamWriter(interfaceFile);
            reverseInterfaceWriter.Write(dataLine);
            reverseInterfaceWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="atomLine"></param>
        /// <returns></returns>
        private string GetInterfaceFileChain(string atomLine)
        {
            string[] fields = ParseHelper.ParsePdbAtomLine(atomLine);
            return fields[4];
        }
        #endregion

        #region update issymmetry 
        DbUpdate dbUpdate = new DbUpdate();
        private DataTable crystInterfaceTable = null;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaces"></param>
        /// <returns></returns>
        public void UpdateIsSymmetry ()
        {
            string queryString = "Select Distinct PdbId, InterfaceID From PfamSuperClusterEntryInterfaces";
            DataTable clusterInterfaceTable = dbQuery.Query (ProtCidSettings.protcidDbConnection, queryString);

            queryString = "Select PdbID, InterfaceID, EntityID1, EntityID2 From CrystEntryInterfaces;";
            crystInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            string pdbId = "";
            int interfaceId = -1;
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString ();
                interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());

                if (! IsInterfaceSameEntity (pdbId, interfaceId))
                {
                    continue;
                }
                try
                {
                    int[] contacts = GetNumOfContactsFromFile(pdbId, interfaceId);

                    int numOfCommonContacts = contacts[0];
                    int numOfContacts = contacts[1];
                    double percent = (double)numOfCommonContacts / (double)numOfContacts;
                    if (percent < 0.9)
                    {
                        UpdateInterfaceIsSymmetry(pdbId, interfaceId, false);
                    }
                    else
                    {
                        UpdateInterfaceIsSymmetry(pdbId, interfaceId, true);
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine(pdbId + interfaceId.ToString () + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }           
        } 
 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private bool IsInterfaceSameEntity (string pdbId, int interfaceId)
        {
            if (crystInterfaceTable != null)
            {
                DataRow[] interfaceRows = crystInterfaceTable.Select(string.Format("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, interfaceId));
                if (interfaceRows.Length > 0)
                {
                    if (interfaceRows[0]["EntityID1"].ToString () == interfaceRows[0]["EntityID2"].ToString ())
                    {
                        return true;
                    }
                }
            }
            else
            {
                string queryString = string.Format("Select EntityID1, EntityID2 From CrystEntryInterfaces Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
                DataTable entityTable = ProtCidSettings.protcidQuery.Query( queryString);

                if (entityTable.Rows.Count > 0)
                {
                    if (entityTable.Rows[0]["EntityID1"].ToString () == entityTable.Rows[0]["EntityID2"].ToString ())
                    {
                        return true;
                    }
                }
            }           
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="isSymmetry"></param>
        private void UpdateInterfaceIsSymmetry (string pdbId, int interfaceId, bool isSymmetry)
        {
            string updateString = "";
            if (isSymmetry)
            {
                updateString = string.Format("Update CrystEntryInterfaces Set IsSymmetry = '1' Where PdbID = '{0}' AND InterfaceID = {1};", pdbId,  interfaceId);
            }
            else
            {
                updateString = string.Format("Update CrystEntryInterfaces Set IsSymmetry = '0' Where PdbID = '{0}' AND InterfaceID= {1};", pdbId, interfaceId);
            }
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }
        #endregion
    }
}
