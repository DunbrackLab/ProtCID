using System;
using System.Collections.Generic;
using System.IO;
using System.Data;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;
using BuQueryLib;
using InterfaceClusterLib.DataTables;
using InterfaceClusterLib.Clustering;
using CrystalInterfaceLib.Settings;

/* revised on July 31, 2009, 
         * remove PQS info
         * */

namespace InterfaceClusterLib.stat
{
	/// <summary>
	/// Statistical summary of interface clusters
	/// </summary>
	public class ClusterStat
	{
		public struct ClusterMinIdentityQ 
		{
			public double minIdentity;
			public double qScore;
		}

//		public DbQuery dbQuery = new DbQuery ();
        public DbQuery protcidQuery = new DbQuery(ProtCidSettings.protcidDbConnection);
        public DbQuery pdbfamQuery = new DbQuery(ProtCidSettings.pdbfamDbConnection);
        public DbQuery bucompQuery = new DbQuery(ProtCidSettings.buCompConnection);
        public DbQuery alignQuery = new DbQuery(ProtCidSettings.alignmentDbConnection);
        public DbUpdate protcidUpdate = new DbUpdate(ProtCidSettings.protcidDbConnection);
		public DbInsert dbInsert = new DbInsert ();
        public DbUpdate dbDelete = new DbUpdate();
		public BiolUnitQuery buQuery = new BiolUnitQuery ();
		public InterfaceStatData interfaceStatData = null;
        protected Dictionary<string, int> sgInterfaceNumHash = new Dictionary<string,int> ();
		// key: groupid_spaceGroup_asu, value: a list of PISA BUs
		protected Dictionary<string, Dictionary<string, List<string>>> groupPisaBusHash = new Dictionary<string,Dictionary<string,List<string>>> ();
		// key: groupid_spaceGroup_asu, value: a list of PDB BUs
        protected Dictionary<string, Dictionary<string, List<string>>> groupPdbBusHash = new Dictionary<string, Dictionary<string, List<string>>>();
        // used to store the ABC format of PDB BUs of entries in the group
        protected Dictionary<string, Dictionary<string, string>> pdbGroupEntryBuAbcFormatHash = new Dictionary<string, Dictionary<string, string>>();
        // used to store the ABC format of PISA BUs of entries in the group
        protected Dictionary<string, Dictionary<string, string>> pisaGroupEntryBuAbcFormatHash = new Dictionary<string, Dictionary<string, string>>();
        //used to 
        protected Dictionary<string, string> groupEntryEntityChainNameHash = new Dictionary<string,string> ();
        public AsuInfoFinder asuInfoFinder = new AsuInfoFinder();

		public const string chainNames = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";	

#if DEBUG
        protected StreamWriter logWriter = null;
#endif
			
		public ClusterStat()
		{
            AppSettings.parameters.simInteractParam.interfaceSimCutoff = 0.2;
            if (!Directory.Exists(ProtCidSettings.applicationStartPath + "\\HomoSeq"))
            {
                Directory.CreateDirectory(ProtCidSettings.applicationStartPath + "\\HomoSeq");
            }

#if DEBUG       
            if (logWriter == null)
            {
                logWriter = new StreamWriter(ProtCidSettings.applicationStartPath + "\\HomoSeq\\ClusterStatLog.txt", true);
            }
#endif
        }

        public void Dispose ()
        {
            protcidQuery.Dispose();
            pdbfamQuery.Dispose();
            bucompQuery.Dispose();
            alignQuery.Dispose();
        }
		
		#region public interface -- print interface clusters
		/// <summary>
		/// print cryst interface statistical info to files 
		/// </summary>
		public void PrintCrystInterfaceClusters (string type)
		{
			interfaceStatData = new InterfaceStatData (type);
//			interfaceStatData.InitializeSumInfoTablesInDb (type);

			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
			string resultDir = Path.Combine (ProtCidSettings.applicationStartPath,  "HomoSeq\\result_chain_" + DateTime.Today.ToString ("yyyyMMdd"));
			if (! Directory.Exists (resultDir))
			{
				Directory.CreateDirectory (resultDir);
			}
            StreamWriter clusterWriter = new StreamWriter(Path.Combine(resultDir, type + "ChainInterfaceClusterInfo.txt"), true);
			StreamWriter clusterSumWriter = new StreamWriter (Path.Combine (resultDir, type + "ChainInterfaceClusterSumInfo.txt"), true);
			string queryString = string.Format ("Select Distinct groupSeqId From {0};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.InterfaceClusters]);
            DataTable clusterTable = protcidQuery.Query(queryString);
			
			List<int> groupList = new List<int> ();
			foreach (DataRow dRow in clusterTable.Rows)
			{
				if (! groupList.Contains (Convert.ToInt32 (dRow["groupSeqId"])))
				{
					groupList.Add (Convert.ToInt32 (dRow["groupSeqId"].ToString ()));
				}
			}
			ProtCidSettings.progressInfo.currentOperationLabel = "Retrieving Cluster Stat Info";
			ProtCidSettings.progressInfo.totalOperationNum = groupList.Count;
			ProtCidSettings.progressInfo.totalStepNum = groupList.Count;
			groupList.Sort ();
			foreach (int groupSeqId in groupList)
			{
				sgInterfaceNumHash.Clear ();
                
				ProtCidSettings.progressInfo.currentOperationNum ++;
				ProtCidSettings.progressInfo.currentStepNum ++;
				ProtCidSettings.progressInfo.currentFileName = groupSeqId.ToString ();

				PrintGroupClusterStatInfo (groupSeqId, clusterWriter, clusterSumWriter, type);
                clusterSumWriter.Flush();
                clusterWriter.Flush();
			}
			//	DbBuilder.dbConnect.DisconnectFromDatabase ();
			clusterWriter.Close ();
			clusterSumWriter.Close ();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Divide the cluster info file into smaller files.");
            DivideChainInterfaceResultOutputFile(Path.Combine(resultDir, type + "ChainInterfaceClusterInfo.txt"));

            ParseHelper.ZipPdbFile (Path.Combine (resultDir, type + "ChainInterfaceClusterInfo.txt"));
            ParseHelper.ZipPdbFile (Path.Combine (resultDir, type + "ChainInterfaceClusterSumInfo.txt"));
#if DEBUG
			logWriter.Close ();
#endif

		}

		/// <summary>
		/// print cryst interface statistical info to files for a specific list of groups
		/// </summary>
		public void PrintCrystInterfaceClusters (int[] groupList, string type)
		{
			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
			string resultDir = Path.Combine (ProtCidSettings.applicationStartPath,  "HomoSeq\\result_chain_" + DateTime.Today.ToString ("yyyyMMdd"));
			if (! Directory.Exists (resultDir))
			{
				Directory.CreateDirectory (resultDir);
			}
			StreamWriter clusterWriter = new StreamWriter (Path.Combine (resultDir, "InterfaceClusters_" + type + ".txt"));
			StreamWriter clusterSumWriter = new StreamWriter (Path.Combine (resultDir, "SummaryOfClusters_" + type + ".txt"));
			
			ProtCidSettings.progressInfo.currentOperationLabel = "Retrieving Cluster Stat Info";
			ProtCidSettings.progressInfo.totalOperationNum = groupList.Length;
			ProtCidSettings.progressInfo.totalStepNum = groupList.Length;
			Array.Sort (groupList);
			foreach (int GroupSeqID in groupList)
			{
				sgInterfaceNumHash.Clear ();
				ProtCidSettings.progressInfo.currentOperationNum ++;
				ProtCidSettings.progressInfo.currentStepNum ++;
				ProtCidSettings.progressInfo.currentFileName = GroupSeqID.ToString ();

				PrintGroupClusterStatInfo (GroupSeqID, clusterWriter, clusterSumWriter, type);
			}
			clusterWriter.Close ();
			clusterSumWriter.Close ();
		}

		/// <summary>
		/// print cryst interface statistical info to files for a specific list of groups
		/// </summary>
        public void PrintCrystInterfaceClusters(int groupSeqId, string type)
        {
            int[] groupList = new int[1];
            groupList[0] = groupSeqId;
            interfaceStatData = new InterfaceStatData(type);
            PrintCrystInterfaceClusters(groupList, type);
        }

		/// <summary>
		/// print cryst interface statistical info to files 
		/// </summary>
		public void PrintUpdateCrystInterfaceClusters (int[] updateGroups, string type)
		{
            interfaceStatData = new InterfaceStatData(type);
			PrintCrystInterfaceClusters (updateGroups, type);
		}
		#endregion

		#region print one group stat info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <param name="clusterWriter"></param>
        /// <param name="clusterSumWriter"></param>
        /// <param name="type"></param>
        private void PrintGroupClusterStatInfo(int groupSeqId, StreamWriter clusterWriter, StreamWriter clusterSumWriter, string type)
        {
            DataTable clusterTable = GetGroupClusterInfo(groupSeqId);
            List<string> pdbList = new List<string> ();
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                if (!pdbList.Contains(clusterRow["PdbID"].ToString()))
                {
                    pdbList.Add(clusterRow["PdbID"].ToString());
                }
            }
            if (pdbList.Count == 0)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("No pdb entries in " + groupSeqId);
                return;
            }
            // cluster info	
            DataTable interfaceTable = GetInterfaceTable(groupSeqId, pdbList);
            DataTable entityInfoTable = GetEntityInfoTable(pdbList);
            Dictionary<string, int> entryCfGroupHash = GetNonReduntCfGroups(groupSeqId);

            pdbGroupEntryBuAbcFormatHash.Clear();
            pisaGroupEntryBuAbcFormatHash.Clear();
            groupEntryEntityChainNameHash.Clear();

            Dictionary<string,string> groupFamilyArchChainHash = GetGroupFamilyArchChainHash(groupSeqId);
            string[] repEntries = new string[pdbList.Count];
            pdbList.CopyTo(repEntries);
            SetGroupEntryEntityChainNameHash(repEntries, groupFamilyArchChainHash);
            string[] homoEntries = GetGroupHomoEntries(groupSeqId, type);
            SetGroupEntryEntityChainNameHash(homoEntries, groupFamilyArchChainHash);

            FormatGroupClusterInfoIntoTable(groupSeqId, clusterTable, interfaceTable, entityInfoTable, entryCfGroupHash);
            // interfaces in BUs of homologous entries in a space group
            FormatRepInterfacesInHomoBUs(groupSeqId, clusterTable, entryCfGroupHash);

            // interface qscore in a cluster
            DataTable groupInterfaceCompTable = GetGroupInterfaceCompTable(pdbList);
            DataTable sgInterfaceCompTable = GetInterfaceOfSgCompTable(pdbList);
            DataTable groupRepAlignTable = GetGroupRepAlignTable(groupSeqId, pdbList);

            FormatClusterSumInfoIntoTable (groupSeqId, clusterTable, groupInterfaceCompTable,
                sgInterfaceCompTable, groupRepAlignTable, entryCfGroupHash);

            string familyCode = GetFamilyString(groupSeqId);
            WriterStatDataToFile(clusterWriter, clusterSumWriter, familyCode);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <returns></returns>
        protected Dictionary<string, string> GetGroupFamilyArchChainHash(int groupSeqId)
        {
            string familyString = GetFamilyString (groupSeqId);
            string[] familyArchFields = familyString.Split(';');
            Dictionary<string, string> familyArchChainHash = new Dictionary<string, string>();
            for (int i = 0; i < familyArchFields.Length; i++)
            {
                if (!familyArchChainHash.ContainsKey(familyArchFields[i]))
                {
                    familyArchChainHash.Add(familyArchFields[i], chainNames[i].ToString ());
                }
            }
            return familyArchChainHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <returns></returns>
		private DataTable GetGroupClusterInfo (int groupSeqId)
		{
			string queryString = string.Format ("Select * From {0} Where groupSeqId = {1};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.InterfaceClusters], groupSeqId);
            DataTable clusterInfoTable = protcidQuery.Query(queryString);
			return clusterInfoTable;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public string[] GetGroupHomoEntries(int groupSeqId, string type)
        {
            string queryString = string.Format("Select Distinct PdbID2 From " + type + "HomoRepEntryAlign " + 
                " Where groupSeqId = {0};", groupSeqId);
            DataTable homoEntryTable = protcidQuery.Query(queryString);
            string[] homoEntries = new string[homoEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow homoEntryRow in homoEntryTable.Rows)
            {
                homoEntries[count] = homoEntryRow["PdbID2"].ToString();
                count++;
            }
            return homoEntries;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <returns></returns>
		public Dictionary<string, int> GetNonReduntCfGroups (int groupSeqId)
		{
			string queryString = string.Format ("Select * From {0} Where groupSeqId = {1};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.NonredundantCfGroups], groupSeqId);
            DataTable nonRedunCfGroupTable = protcidQuery.Query(queryString);
            Dictionary<string, int> entryCfHash = new Dictionary<string, int>();
			foreach (DataRow dRow in nonRedunCfGroupTable.Rows)
			{
				entryCfHash.Add (dRow["PdbID"].ToString (), Convert.ToInt32 (dRow["CfGroupID"].ToString ()));
			}
			return entryCfHash;
		}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <returns></returns>
		public string GetFamilyString (int groupSeqId)
		{
			string queryString = string.Format ("Select EntryPfamArch From {0} Where groupSeqId = {1};",
				GroupDbTableNames.dbTableNames[GroupDbTableNames.FamilyGroups], groupSeqId);
            DataTable entryPfamArchTable = protcidQuery.Query(queryString);
            if (entryPfamArchTable.Rows.Count > 0)
            {
                return entryPfamArchTable.Rows[0]["EntryPfamArch"].ToString().TrimEnd();
            }
            return "";
		}
		#endregion

		#region Print Representative Interfaces In BUs of Homologous Entries
		/// <summary>
		/// 
		/// </summary>
		protected void FormatRepInterfacesInHomoBUs (int GroupSeqID, DataTable clusterTable, Dictionary<string, int> entryCfGroupHash)
		{
		//	DataRow[] clusterRows = clusterTable.Select (string.Format ("GroupSeqID = '{0}'", GroupSeqID));
			List<int> clusterList = new List<int> ();
			List<string> repPdbList = new List<string> ();
			foreach (DataRow clusterRow in clusterTable.Rows)
			{
				if (! clusterList.Contains (Convert.ToInt32 (clusterRow["ClusterID"].ToString ())))
				{
					clusterList.Add (Convert.ToInt32 (clusterRow["ClusterID"].ToString ()));
				}
				if (! repPdbList.Contains (clusterRow["PdbID"].ToString ()))
				{
					repPdbList.Add (clusterRow["PdbID"].ToString ());
				}
			}
			clusterList.Sort ();
			DataTable repHomoTable = GetRepHomoEntryTable (GroupSeqID, repPdbList);
			List<string> pdbList = new List<string> ();
			Dictionary<string, List<string>> repHomoEntryHash = new Dictionary<string,List<string>> ();
			foreach (string repPdb in repPdbList)
			{
				DataRow[] homoRows = repHomoTable.Select 
					(string.Format ("GroupSeqId = '{0}' AND PdbID1 = '{1}'", GroupSeqID, repPdb));
				List<string> homoEntryList = new List<string> ();
				foreach (DataRow homoRow in homoRows)
				{
					if (! pdbList.Contains (homoRow["PdbID2"].ToString ()))
					{
						pdbList.Add (homoRow["PdbID2"].ToString ());
					}
					if (! homoEntryList.Contains (homoRow["PdbID2"].ToString ()))
					{
						homoEntryList.Add (homoRow["PdbID2"].ToString ());
					}
				}
				if (homoEntryList.Count > 0)
				{
					repHomoEntryHash.Add (repPdb, homoEntryList);
				}
			}
			// no homologous entries
			if (repHomoEntryHash.Count == 0)
			{
				return;
			}
			DataTable entityInfoTable = GetEntityInfoTable (pdbList);

			foreach (int clusterId in clusterList)
			{
                ProtCidSettings.progressInfo.currentFileName = GroupSeqID.ToString() + "_" + clusterId.ToString();

				DataRow[] clusterInterfaceRows = clusterTable.Select 
					(string.Format ("GroupSeqID = '{0}' AND ClusterID = '{1}'", GroupSeqID, clusterId), "SpaceGroup, PdbID ASC");
                string clusterFamilyArchString = GetClusterDimerFamilyArchAbcFormat(clusterInterfaceRows[0]);
				FormatHomoInterfacesIntoTable (repHomoEntryHash, clusterInterfaceRows, entityInfoTable, entryCfGroupHash, clusterFamilyArchString);
			}
		}


        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaceRow"></param>
        /// <returns></returns>
        private string GetClusterDimerFamilyArchAbcFormat(DataRow clusterInterfaceRow)
        {
            string pdbId = clusterInterfaceRow["PdbID"].ToString();
            int interfaceId = Convert.ToInt32(clusterInterfaceRow["InterfaceID"].ToString());
            string interfaceFamilyArch = GetInterfaceEntityFamilyArch (pdbId, interfaceId);
            return interfaceFamilyArch;
        }
        
		/// <summary>
		/// 
		/// </summary>
		/// <param name="clusterInterfaceRows"></param>
		/// <param name="crystBuCompTable"></param>
		/// <param name="buTable"></param>
		/// <param name="clusterHomoWriter"></param>
		private void FormatHomoInterfacesIntoTable (Dictionary<string, List<string>> repHomoEntryHash, DataRow[] clusterInterfaceRows, 
				DataTable entityInfoTable, Dictionary<string, int> entryCfGroupHash, string clusterFamilyArchString)
		{
			int GroupSeqID = Convert.ToInt32 (clusterInterfaceRows[0]["GroupSeqID"].ToString ());
			string pdbId = "";
			int interfaceId = -1;
			int clusterId = Convert.ToInt32 (clusterInterfaceRows[0]["ClusterID"].ToString ());
			string spaceGroup = "";
			int cfGroupId = -1;
			string crystForm = "";

            Dictionary<string, List<int>> clusterEntryInterfaceHash = new Dictionary<string,List<int>> ();
            Dictionary<string, string> entryCrystFormHash = new Dictionary<string,string> ();
            foreach (DataRow interfaceRow in clusterInterfaceRows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString ());
                spaceGroup = interfaceRow["SpaceGroup"].ToString().Trim();
                crystForm = interfaceRow["ASU"].ToString().Trim();

                if (clusterEntryInterfaceHash.ContainsKey(pdbId))
                {
                    clusterEntryInterfaceHash[pdbId].Add(interfaceId);
                }
                else
                {
                    List<int> interfaceList = new List<int>();
                    interfaceList.Add(interfaceId);
                    clusterEntryInterfaceHash.Add(pdbId, interfaceList);
                }
                if (! entryCrystFormHash.ContainsKey(pdbId))
                {
                    entryCrystFormHash.Add(pdbId, spaceGroup + "_" + crystForm);
                }
            }

            foreach (string repEntry in clusterEntryInterfaceHash.Keys)
            {
                cfGroupId = (int)entryCfGroupHash[repEntry];
                List<int> clusterInterfaceList = clusterEntryInterfaceHash[repEntry];
                int[] interfacesInCluster = clusterInterfaceList.ToArray();

                string sgCrystFormString = (string)entryCrystFormHash[repEntry];
                string[] spaceGroupCrystForm = sgCrystFormString.Split('_');

                int numOfInterfacesInSg = GetNumOfInterfacesInSg(GroupSeqID, spaceGroupCrystForm[0], spaceGroupCrystForm[1]); ;

                if (! repHomoEntryHash.ContainsKey (repEntry))
                {
                    continue;
                }
                List<string> homoEntryList = repHomoEntryHash[repEntry];
                homoEntryList.Remove(pdbId);

                foreach (string homoEntry in homoEntryList)
                {
                    DataRow[] speciesRows = entityInfoTable.Select(string.Format("PdbId = '{0}'", homoEntry));
                    int[] homoInterfacesInCluster = GetHomoInterfacesInCluster(repEntry, homoEntry,
                        interfacesInCluster, clusterFamilyArchString);

                    if (homoInterfacesInCluster.Length == 0)
                    {
                        continue;
                    }
                    DataTable entryInterfaceTable = GetEntryInterfaceTable(homoEntry);
                    foreach (int homoInterface in homoInterfacesInCluster)
                    {
                        DataRow homoInterfaceRow = interfaceStatData.interfaceDataTable.NewRow();
                        homoInterfaceRow["GroupSeqID"] = GroupSeqID;
                        homoInterfaceRow["CfGroupID"] = cfGroupId;
                        homoInterfaceRow["ClusterID"] = clusterId;
                        homoInterfaceRow["SpaceGroup"] = spaceGroupCrystForm[0];
                        homoInterfaceRow["CrystForm"] = spaceGroupCrystForm[1];
                        homoInterfaceRow["PdbID"] = homoEntry;
                        homoInterfaceRow["Species"] = GetInterfaceSpecies (homoEntry, homoInterface, entryInterfaceTable, entityInfoTable);
                        homoInterfaceRow["Name"] = GetInterfaceName(homoEntry, homoInterface, entryInterfaceTable, entityInfoTable);
                        homoInterfaceRow["UnpCode"] = GetInterfaceUnpCode(homoEntry, homoInterface, entryInterfaceTable, entityInfoTable);
                        homoInterfaceRow["InterfaceID"] = homoInterface;
                        homoInterfaceRow["SurfaceArea"] = GetInterfaceSurfaceArea(homoEntry, homoInterface, entryInterfaceTable);
                        homoInterfaceRow["InterfaceUnit"] = GetCrystInterfaceAbcFormat(homoEntry, homoInterface);
                        if (IsInterfaceInAsu(homoEntry, homoInterface))
                        {
                            homoInterfaceRow["InAsu"] = 1;
                        }
                        else
                        {
                            homoInterfaceRow["InAsu"] = 0;
                        }
                        homoInterfaceRow["ASU"] = GetAsuStringFromCrystFormString(spaceGroupCrystForm[1]);
                        homoInterfaceRow["NumOfInterfaces"] = numOfInterfacesInSg;

                        FormatBuAndCrystBuCompInfo(ref homoInterfaceRow);

                        interfaceStatData.interfaceDataTable.Rows.Add(homoInterfaceRow);
                        AddPdbPisaBuToHash(GroupSeqID.ToString() + "_" + sgCrystFormString, homoInterfaceRow);
                    }
                }
            }	
		}

        /// <summary>
        /// the interfaces for the homologous entry which are similar to 
        /// those interfaces from the representative entry in the cluster
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="homoEntry"></param>
        /// <param name="interfacesInCluster"></param>
        /// <param name="homoEntryInterfaceTable"></param>
        /// <returns></returns>
        private int[] GetHomoInterfacesInCluster(string pdbId, string homoEntry, int[] interfacesInCluster, string clusterFamilyArch)
        {
            string queryString = string.Format("Select * From DifEntryInterfaceComp " +
                " Where PdbID1 = '{0}' AND PdbID2 = '{1}' AND InterfaceID1 IN ({2}) AND QScore >= {3};",
                pdbId, homoEntry, ParseHelper.FormatSqlListString(interfacesInCluster),
                AppSettings.parameters.simInteractParam.interfaceSimCutoff);
            DataTable clusterHomoInterfaceTable = protcidQuery.Query(queryString);

            List<int> homoInterfaceInClusterList = new List<int> ();
            int homoInterfaceId = -1;
            string homoInterfaceFamilyArch = "";
            if (clusterHomoInterfaceTable.Rows.Count == 0)
            {
                queryString = string.Format("Select * From DifEntryInterfaceComp " +
                " Where PdbID2 = '{0}' AND PdbID1 = '{1}' AND InterfaceID2 IN ({2}) AND QScore >= {3};",
                                    pdbId, homoEntry, ParseHelper.FormatSqlListString(interfacesInCluster),
                                    AppSettings.parameters.simInteractParam.interfaceSimCutoff);
                clusterHomoInterfaceTable = protcidQuery.Query(queryString);
                ReverseDifInterfaceCompTable(clusterHomoInterfaceTable);
            }
            foreach (DataRow homoInterfaceRow in clusterHomoInterfaceTable.Rows)
            {
                homoInterfaceId = Convert.ToInt32(homoInterfaceRow["InterfaceID2"].ToString());

                if (!homoInterfaceInClusterList.Contains(homoInterfaceId))
                {
                    homoInterfaceFamilyArch = GetInterfaceEntityFamilyArch(homoEntry, homoInterfaceId);
                    if (homoInterfaceFamilyArch != "" && homoInterfaceFamilyArch == clusterFamilyArch)
                    {
                        homoInterfaceInClusterList.Add(homoInterfaceId);
                    }
                }
            }

            int[] homoInterfacesInCluster_all = new int[homoInterfaceInClusterList.Count];
            homoInterfaceInClusterList.CopyTo(homoInterfacesInCluster_all);

            int[] homoInterfacesInCluster = GetHomoInterfacesInCluster(pdbId, homoEntry, homoInterfacesInCluster_all, interfacesInCluster);

            return homoInterfacesInCluster;
        }

        /// <summary>
        /// this function is remove those interfaces of homo entry 
        /// which have Q scores >= cutoff with the interfaces of its rep entry from different clusters
        /// So, only assign the interfaces of homo entry to the clusters with highest Q scores
        /// This is to fix the bug where an interface of homo entry is assigned to different clusters
        /// The possible problem is that, we may miss some interfaces for homo entry, 
        /// if the interfaces with highest Q from rep entry don't belong to any clusters
        /// </summary>
        /// <param name="repEntry"></param>
        /// <param name="homoEntry"></param>
        /// <param name="simHomoInterfaces"></param>
        /// <param name="clusterRepInterfaces"></param>
        /// <returns></returns>
        private int[] GetHomoInterfacesInCluster(string repEntry, string homoEntry, int[] simHomoInterfaces, int[] clusterRepInterfaces)
        {
            DataTable repHomoInterfaceCompTable = GetRepHomoInterfaceCompTable(repEntry, homoEntry);
            List<int> homoInterfaceInClusterList = new List<int> ();
            int repInterfaceId = 0;
            foreach (int homoInterfaceId in simHomoInterfaces)
            {
                // highest Q score first
                DataRow[] interfaceCompRows = repHomoInterfaceCompTable.Select
                    (string.Format ("PdbID2 = '{0}' AND InterfaceID2 = {1}", homoEntry, homoInterfaceId), "QScore DESC");
                if (interfaceCompRows.Length > 0)
                {
                    repInterfaceId = Convert.ToInt32(interfaceCompRows[0]["InterfaceID1"].ToString());
                    if (Array.IndexOf(clusterRepInterfaces, repInterfaceId) > -1)
                    {
                        homoInterfaceInClusterList.Add(homoInterfaceId);
                    }
                }
                else
                {
                    ProtCidSettings.logWriter.WriteLine("Warning: " + repEntry + " and " + homoEntry + " representative and homo entry don't have interface comp rows.\n");
                    ProtCidSettings.logWriter.Flush();
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Warning: "  + repEntry + " and " + homoEntry + " representative and homo entry don't have interface comp rows.");
                }
            }
            return homoInterfaceInClusterList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="repEntry"></param>
        /// <param name="homoEntry"></param>
        /// <returns></returns>
        private DataTable GetRepHomoInterfaceCompTable(string repEntry, string homoEntry)
        {
            string queryString = string.Format("Select * From DifEntryInterfaceComp " +
                " Where PdbID1 = '{0}' AND PdbID2 = '{1}' AND QScore >= {2};",
                repEntry, homoEntry, AppSettings.parameters.simInteractParam.interfaceSimCutoff);
            DataTable repHomoInterfaceCompTable = protcidQuery.Query(queryString);
            if (repHomoInterfaceCompTable.Rows.Count == 0)
            {
                queryString = string.Format("Select * From DifEntryInterfaceComp " +
                            " Where PdbID1 = '{0}' AND PdbID2 = '{1}' AND QScore >= {2};",
                           homoEntry, repEntry, AppSettings.parameters.simInteractParam.interfaceSimCutoff);
                repHomoInterfaceCompTable = protcidQuery.Query(queryString);
                ReverseDifInterfaceCompTable(repHomoInterfaceCompTable);
            }
            return repHomoInterfaceCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="difEntryInterfaceCompTable"></param>
        public void ReverseDifInterfaceCompTable(DataTable difEntryInterfaceCompTable)
        {
            for (int i = 0; i < difEntryInterfaceCompTable.Rows.Count; i++)
            {
                DataRow interfaceCompRow = difEntryInterfaceCompTable.Rows[i];
                ReverseDifInterfaceCompRow(interfaceCompRow);
            }
            difEntryInterfaceCompTable.AcceptChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceCompRow"></param>
        private void ReverseDifInterfaceCompRow(DataRow interfaceCompRow)
        {
            object temp = interfaceCompRow["PdbID1"];
            interfaceCompRow["PdbID1"] = interfaceCompRow["PdbID2"];
            interfaceCompRow["PdbID2"] = temp;

            temp = interfaceCompRow["InterfaceID1"];
            interfaceCompRow["InterfaceID1"] = interfaceCompRow["InterfaceID2"];
            interfaceCompRow["InterfaceID2"] = temp;
        }

		/// <summary>
		/// BUs distribution in each crystal form
		/// </summary>
		/// <param name="groupSgAsu"></param>
		/// <param name="homoInterfaceRow"></param>
		public void AddPdbPisaBuToHash (string groupSgAsu, DataRow homoInterfaceRow)
		{			
			if (homoInterfaceRow["PdbBu"].ToString () != "-" )
			{
				if (groupPdbBusHash.ContainsKey (groupSgAsu))
				{
					Dictionary<string,List<string>> pdbBuEntryHash = groupPdbBusHash[groupSgAsu];
					if (pdbBuEntryHash.ContainsKey (homoInterfaceRow["PdbBu"].ToString ()))
					{
						List<string> entryList = pdbBuEntryHash[homoInterfaceRow["PdbBu"].ToString ()];
						if (! entryList.Contains (homoInterfaceRow["PdbID"].ToString ()))
						{
							entryList.Add (homoInterfaceRow["PdbID"].ToString ());
							pdbBuEntryHash[homoInterfaceRow["PdbBu"].ToString ()] = entryList;
						}
					}
					else
					{
						List<string> entryList = new List<string> ();
						entryList.Add (homoInterfaceRow["PdbID"].ToString ());
						pdbBuEntryHash.Add (homoInterfaceRow["PdbBu"].ToString (), entryList);
					}
					groupPdbBusHash[groupSgAsu] = pdbBuEntryHash;
				}
				else
				{
					List<string> entryList = new List<string> ();
					entryList.Add (homoInterfaceRow["PdbID"].ToString ());
					Dictionary<string, List<string>> pdbBuEntryHash = new Dictionary<string,List<string>> ();
					pdbBuEntryHash.Add (homoInterfaceRow["PdbBU"].ToString (), entryList);
					groupPdbBusHash.Add (groupSgAsu, pdbBuEntryHash);
				}
			}
			// change Pqs to Pisa on July 31, 2009
			if (homoInterfaceRow["PisaBu"].ToString () != "-" )
			{
				if (groupPisaBusHash.ContainsKey (groupSgAsu))
				{
					Dictionary<string, List<string>> pisaBuEntryHash = groupPisaBusHash[groupSgAsu];
					if (pisaBuEntryHash.ContainsKey (homoInterfaceRow["PisaBu"].ToString ()))
					{
                        List<string> entryList = pisaBuEntryHash[homoInterfaceRow["PisaBu"].ToString ()];
						if (! entryList.Contains (homoInterfaceRow["PdbID"].ToString ()))
						{
							entryList.Add (homoInterfaceRow["PdbID"].ToString ());
                            pisaBuEntryHash[homoInterfaceRow["PisaBu"].ToString ()] = entryList;
						}
					}
					else
					{
						List<string> entryList = new List<string> ();
						entryList.Add (homoInterfaceRow["PdbID"].ToString ());
                        pisaBuEntryHash.Add(homoInterfaceRow["PisaBu"].ToString (), entryList);
					}
					groupPisaBusHash[groupSgAsu] = pisaBuEntryHash;
				}
				else
				{
					List<string> entryList = new List<string> ();
					entryList.Add (homoInterfaceRow["PdbID"].ToString ());
					Dictionary<string, List<string>> pisaBuEntryHash = new Dictionary<string,List<string>> ();
					pisaBuEntryHash.Add (homoInterfaceRow["PisaBU"].ToString (), entryList);
					groupPisaBusHash.Add (groupSgAsu, pisaBuEntryHash);
				}
			}
		}
		#endregion

		#region Retrieve interfaces info
        #region entity info: name, species, unp codes
        /// <summary>
        /// the species for the input Pdb entries
        /// </summary>
        /// <param name="pdbList"></param>
        /// <returns></returns>
		public DataTable GetEntityInfoTable (List<string> pdbList)
		{
            DataTable entityInfoTable = null;
            string queryString = "";
            foreach (string pdbId in pdbList)
            {
                queryString = string.Format("Select Distinct PdbID, EntityID, Name, Species " +
                    " From AsymUnit WHere PdbId = '{0}' AND PolymerType = 'polypeptide';", pdbId);
                DataTable entryEntityInfoTable = pdbfamQuery.Query(queryString);
                if (entityInfoTable == null)
                {
                    entityInfoTable = entryEntityInfoTable.Copy();
                }
                else
                {
                    foreach (DataRow dataRow in entryEntityInfoTable.Rows)
                    {
                        DataRow newRow = entityInfoTable.NewRow();
                        newRow.ItemArray = dataRow.ItemArray;
                        entityInfoTable.Rows.Add(newRow);
                    }
                }
            }
            entityInfoTable.Columns.Add(new DataColumn("UnpCode"));
            string entry = "";
            int entityId = -1;
            string entityUnpCode = "";
            for (int i = 0; i < entityInfoTable.Rows.Count; i++)
            {
                entry = entityInfoTable.Rows[i]["PdbID"].ToString();
                entityId = Convert.ToInt32(entityInfoTable.Rows[i]["EntityID"].ToString());
                entityUnpCode = GetEntityUnpCode(entry, entityId);
                entityInfoTable.Rows[i]["UnpCode"] = entityUnpCode;
            }
            entityInfoTable.AcceptChanges();
            return entityInfoTable;
        }
        #region unp codes
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string GetEntityUnpCode(string pdbId, int entityId)
        {
            string queryString = string.Format("Select * From PdbDbRefSifts " + 
                " Where PdbID = '{0}' AND EntityID = {1} AND DBName = 'UNP'", pdbId, entityId);
            DataTable unpCodeTable = pdbfamQuery.Query(queryString);
            // if not in SIFTs due to inconsistent update, get the unp code from PDB xml file
            // added on August 27, 2010
            if (unpCodeTable.Rows.Count == 0)
            {
                queryString = string.Format("Select * From PdbDbRefXml " +
                    " Where PdbID = '{0}' AND EntityID = {1} AND DbName = 'UNP';", pdbId, entityId);
                unpCodeTable = pdbfamQuery.Query(queryString);
            }
            if (unpCodeTable.Rows.Count == 1)
            {
                return unpCodeTable.Rows[0]["DbCode"].ToString().TrimEnd();
            }
            string asymChain = GetAsymChainForEntity(pdbId, entityId);
            int[] refIdsInSeqOrder = GetRefIDInSequenceOrder(pdbId, asymChain);
            string unpCodeString = "";
            string unpCode = "";
            List<string> entityUnpCodeList = new List<string>();
            foreach (int refId in refIdsInSeqOrder)
            {
                DataRow[] unpCodeRows = unpCodeTable.Select(string.Format ("RefID = {0}", refId));
                if (unpCodeRows.Length > 0)
                {
                    unpCode = unpCodeRows[0]["DbCode"].ToString().TrimEnd();
                    if (! entityUnpCodeList.Contains(unpCode))
                    {
                        entityUnpCodeList.Add(unpCode);
                        unpCodeString += "(" + unpCode + ")";
                        unpCodeString += "_";
                    }
                }
            }
            return unpCodeString.TrimEnd ('_');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string GetAsymChainForEntity(string pdbId, int entityId)
        {
            string queryString = string.Format("Select AsymID From AsymUnit Where PdbID = '{0}' AND EntityID = {1};",
                pdbId, entityId);
            DataTable asymChainTable = pdbfamQuery.Query(queryString);
            if (asymChainTable.Rows.Count > 0)
            {
                return asymChainTable.Rows[0]["AsymID"].ToString().TrimEnd();
            }
            return "-";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private int[] GetRefIDInSequenceOrder(string pdbId, string asymChain)
        {
            string queryString = string.Format("Select * From PdbDbRefSeqSifts " + 
                " Where PdbID = '{0}' AND AsymID = '{1}' ORDER BY SeqAlignBeg;", pdbId, asymChain);
            DataTable refIdTable = pdbfamQuery.Query(queryString);
            int[] refIdsInSeqOrder = new int[refIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow refIdRow in refIdTable.Rows)
            {
                refIdsInSeqOrder[count] = Convert.ToInt32(refIdRow["RefID"].ToString ());
                count++;
            }
            return refIdsInSeqOrder;
        }
        #endregion
        #endregion

        #region representative entries and their homologous entries in a group
        /// <summary>
        /// the representative entries and their homologous entries in the group
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <param name="pdbList"></param>
        /// <returns></returns>
		private DataTable GetRepHomoEntryTable (int groupSeqId, List<string> pdbList)
		{
            string queryString = "";
            DataTable repHomoEntryTable = null;
            foreach (string pdbId in pdbList)
            {
                queryString = string.Format("Select groupSeqId, PdbID1, PdbID2 From {0} " +
                     " Where groupSeqId = {1} AND PdbID1 IN ({2});",
                     GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoRepEntryAlign],
                     groupSeqId, ParseHelper.FormatSqlListString(pdbList.ToArray ()));
                DataTable entryRepHomoEntryTable = protcidQuery.Query(queryString);
                if (repHomoEntryTable == null)
                {
                    repHomoEntryTable = entryRepHomoEntryTable.Copy();
                }
                else
                {
                    foreach (DataRow homoEntryRow in entryRepHomoEntryTable.Rows)
                    {
                        DataRow dataRow = repHomoEntryTable.NewRow();
                        dataRow.ItemArray = homoEntryRow.ItemArray;
                        repHomoEntryTable.Rows.Add(dataRow);
                    }
                }
            }
			return repHomoEntryTable;
        }
        #endregion

        #region interface comparison data
        /// <summary>
		/// similar interfaces of homologous entries 
		/// </summary>
		/// <param name="repPdb"></param>
		/// <param name="homoInterfaceCompTable"></param>
		/// <returns></returns>
		private DataTable GetHomoInterfaceCompTable (string repPdb)
		{
			string queryString = string.Format ("SELECT Distinct PdbID2 FROM {0} WHERE PdbID1 = '{1}';", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoRepEntryAlign], repPdb);
			DataTable homoEntryTable = protcidQuery.Query(queryString);
			List<string> homoEntryList = new List<string> ();

			foreach (DataRow homoEntryRow in homoEntryTable.Rows)
			{
				homoEntryList.Add (homoEntryRow["PdbID2"].ToString ());
			}
			if (homoEntryList.Count > 0)
			{
				queryString = string.Format ("SELECT * FROM DifEntryInterfaceComp " + 
                    " WHERE PdbId1 = '{0}' AND QScore > {1} AND PdbID2 IN ({2});", 
					repPdb, AppSettings.parameters.simInteractParam.interfaceSimCutoff, 
					ParseHelper.FormatSqlListString (homoEntryList.ToArray ()));
                DataTable homoInterfaceCompTable = protcidQuery.Query(queryString);
				return homoInterfaceCompTable;
			}
			return null;
		}
		/// <summary>
		/// retrieve the comparison between two space groups
		/// </summary>
		/// <param name="groupSeqId"></param>
		/// <param name="pdbList"></param>
		/// <returns></returns>
		public DataTable GetGroupInterfaceCompTable (List<string> pdbList)
		{
			string groupInterfaceCompString = string.Format 
				(string.Format ("Select * From DifEntryInterfaceComp " + 
			//	" Where groupSeqId = {0} AND PdbID1 IN ({1}) AND PdbID2 IN ({1})", 
				" Where PdbID1 IN ({0}) AND PdbID2 IN ({0})",
				ParseHelper.FormatSqlListString (pdbList.ToArray ())));
            DataTable groupInterfaceCompTable = protcidQuery.Query(groupInterfaceCompString);
			return groupInterfaceCompTable;
		}

		/// <summary>
		/// retrieve the comparison between two interface in a space group
		/// </summary>
		/// <param name="groupSeqId"></param>
		/// <param name="pdbList"></param>
		/// <returns></returns>
		public DataTable GetInterfaceOfSgCompTable (List<string> pdbList)
		{
			string sgInterfaceCompString = string.Format 
				(string.Format ("Select * From EntryInterfaceComp " + 
				" Where PdbID IN ({0})", ParseHelper.FormatSqlListString (pdbList.ToArray ())));
            DataTable sgInterfaceCompTable = protcidQuery.Query(sgInterfaceCompString);
			return sgInterfaceCompTable;
        }
        #endregion

        #region alignments
        /// <summary>
		/// the sequence alignment between two representative entries in a group
		/// </summary>
		/// <param name="groupSeqId"></param>
		/// <param name="pdbList"></param>
		/// <returns></returns>
		public DataTable GetGroupRepAlignTable (int groupSeqId, List<string> pdbList)
		{
			string groupRepAlignString = string.Format 
				("Select * From {0} WHERE groupSeqId = {1} AND PdbID1 IN ({2}) AND PdbID2 IN ({2})", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoGroupEntryAlign],
				groupSeqId, ParseHelper.FormatSqlListString (pdbList.ToArray ()));
            return protcidQuery.Query(groupRepAlignString);
        }
        #endregion

        #region cfg 
        /// <summary>
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <returns></returns>
		private DataTable GetGroupReduntCrystForms (int groupSeqId)
		{
			string queryString = string.Format ("Select * From {0} Where groupSeqId = {1};", 
				ProtCidSettings.dataType + "ReduntCrystForms", groupSeqId);
			DataTable reduntCrystFormsTable = protcidQuery.Query(queryString);

			DataTable copyTable = reduntCrystFormsTable.Copy ();
			reduntCrystFormsTable.Clear ();
			double simPercent1 = 0.0;
			double simPercent2 = 0.0;
			foreach (DataRow dRow in copyTable.Rows)
			{
				simPercent1 = Convert.ToDouble (dRow["NumOfSimInterfaces1"].ToString ()) / 
					Convert.ToDouble (dRow["NumOfSgInterfaces1"].ToString ());
				simPercent2 = Convert.ToDouble (dRow["NumOfSimInterfaces2"].ToString ()) / 
					Convert.ToDouble (dRow["NumOfSgInterfaces2"].ToString ());
				if ((int)simPercent1 == 1 || (int)simPercent2 == 1 )
			//		|| (simPercent1 >= simPercent &&  simPercent2 >= simPercent))
				{
					DataRow newRow = reduntCrystFormsTable.NewRow ();
					newRow.ItemArray = dRow.ItemArray;
					reduntCrystFormsTable.Rows.Add (newRow);
				}
			}
			return reduntCrystFormsTable;
        }
        #endregion

        #region data for the interfaces

        /// <summary>
        /// retrieve the info about interfaces of PDB list in a group
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <param name="clusterTable"></param>
        /// <returns></returns>
        public DataTable GetInterfaceTable(int groupSeqId, List<string> pdbList)
        {
            string interfaceQuery = "";
            DataTable interfaceTable = null;
            foreach (string pdbId in pdbList)
            {
                interfaceQuery = string.Format("Select groupSeqId, {0}.PdbId, SpaceGroup, ASU, " +
                     " InterfaceID, EntityID1, AsymChain1, SymmetryString1, " +
                     " EntityID2, AsymChain2, SymmetryString2, SurfaceArea " +
                     " From CrystEntryInterfaces, {0} " +
                     " WHERE {0}.groupSeqId = {1} AND " +
                     " CrystEntryInterfaces.PdbID = '{2}' AND {0}.pdbID = '{2}' AND " +
                     " CrystEntryInterfaces.PdbID = {0}.PdbID;",
                     GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo],
                     groupSeqId, pdbId);
                DataTable entryInterfaceTable = protcidQuery.Query(interfaceQuery);
                if (interfaceTable == null)
                {
                    interfaceTable = entryInterfaceTable.Copy();
                }
                else
                {
                    foreach (DataRow interfaceRow in entryInterfaceTable.Rows)
                    {
                        DataRow dataRow = interfaceTable.NewRow();
                        dataRow.ItemArray = interfaceRow.ItemArray;
                        interfaceTable.Rows.Add(dataRow);
                    }
                }
            }
            return interfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryInterfaceTable(string pdbId)
        {
            string queryString = string.Format("Select * From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable interfaceTable = protcidQuery.Query(queryString);
            return interfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private double GetInterfaceSurfaceArea(string pdbId, int interfaceId, DataTable entryInterfaceTable)
        {
            DataRow[] interfaceRows = entryInterfaceTable.Select(string.Format("InterfaceID = '{0}'", interfaceId));
            if (interfaceRows.Length > 0)
            {
                return Convert.ToDouble(interfaceRows[0]["SurfaceArea"].ToString());
            }
            return -1.0;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="entityInfoTable"></param>
        /// <returns></returns>
        private string GetInterfaceSpecies(string pdbId, int interfaceId, DataTable entryInterfaceTable, DataTable entityInfoTable)
        {
            DataRow[] interfaceRows = entryInterfaceTable.Select
                (string.Format("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, interfaceId));
            string species2 = "";
            string species = "";
            if (interfaceRows.Length > 0)
            {
                string entityId1 = interfaceRows[0]["EntityID1"].ToString();
                string entityId2 = interfaceRows[0]["EntityID2"].ToString();
                DataRow[] speciesRows = entityInfoTable.Select(string.Format("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId1));
                if (speciesRows.Length > 0)
                {
                    species = speciesRows[0]["Species"].ToString().TrimEnd();
                }
                if (entityId1 != entityId2)
                {
                    speciesRows = entityInfoTable.Select(string.Format("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId2));
                    if (speciesRows.Length > 0)
                    {
                        species2 = speciesRows[0]["Species"].ToString().TrimEnd();
                        if (species2 != species)
                        {
                            species = species + ";" + species2;
                        }
                    }
                }
            }
            if (species == "")
            {
                species = "-";
            }
            return species;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="entryInterfaceTable"></param>
        /// <param name="nameTable"></param>
        /// <returns></returns>
        private string GetInterfaceName(string pdbId, int interfaceId, DataTable entryInterfaceTable, DataTable nameTable)
        {
            DataRow[] interfaceRows = entryInterfaceTable.Select
                (string.Format("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, interfaceId));
            string name2 = "";
            string name = "";
            if (interfaceRows.Length > 0)
            {
                string entityId1 = interfaceRows[0]["EntityID1"].ToString();
                string entityId2 = interfaceRows[0]["EntityID2"].ToString();
                DataRow[] nameRows = nameTable.Select(string.Format("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId1));
                if (nameRows.Length > 0)
                {
                    name = nameRows[0]["Name"].ToString().TrimEnd();
                }
                if (entityId1 != entityId2)
                {
                    nameRows = nameTable.Select(string.Format("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId2));
                    if (nameRows.Length > 0)
                    {
                        name2 = nameRows[0]["Name"].ToString().TrimEnd();
                        if (name2 != name)
                        {
                            name = name + ";" + name2;
                        }
                    }
                }
            }
            if (name == "")
            {
                name = "-";
            }
            return name;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="entryInterfaceTable"></param>
        /// <param name="nameTable"></param>
        private string GetInterfaceUnpCode(string pdbId, int interfaceId, DataTable entryInterfaceTable, DataTable unpCodeTable)
        {
            DataRow[] interfaceRows = entryInterfaceTable.Select
               (string.Format("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, interfaceId));
            string unpCode2 = "";
            string unpCode = "";
            if (interfaceRows.Length > 0)
            {
                string entityId1 = interfaceRows[0]["EntityID1"].ToString();
                string entityId2 = interfaceRows[0]["EntityID2"].ToString();
                DataRow[] unpCodeRows = unpCodeTable.Select(string.Format("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId1));
                if (unpCodeRows.Length > 0)
                {
                    unpCode = unpCodeRows[0]["UnpCode"].ToString().TrimEnd();
                }
                if (entityId1 != entityId2)
                {
                    unpCodeRows = unpCodeTable.Select(string.Format("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId2));
                    if (unpCodeRows.Length > 0)
                    {
                        unpCode2 = unpCodeRows[0]["UnpCode"].ToString().TrimEnd();
                        if (unpCode2 != unpCode)
                        {
                            unpCode = unpCode + ";" + unpCode2;
                        }
                    }
                }
            }
            if (unpCode == "")
            {
                unpCode = "-";
            }
            return unpCode;
        }
        #endregion
		#endregion

		#region format clusters info into table
		/// <summary>
		/// writer all clusters of a group into a file
		/// </summary>
		/// <param name="clusterRows"></param>
		/// <param name="interfaceTable"></param>
		protected void FormatGroupClusterInfoIntoTable (int groupSeqId, DataTable clusterTable, DataTable interfaceTable, DataTable entityInfoTable, Dictionary<string, int> entryCfGroupHash)
		{
			List<int> clusterList = new List<int> ();
			foreach (DataRow clusterRow in clusterTable.Rows)
			{
				if (! clusterList.Contains (Convert.ToInt32 (clusterRow["ClusterID"].ToString ())))
				{
					clusterList.Add (Convert.ToInt32 (clusterRow["ClusterID"].ToString ()));
				}
			}
			clusterList.Sort ();
			foreach (int clusterId in clusterList)
			{
                ProtCidSettings.progressInfo.currentFileName = groupSeqId.ToString() + "_" + clusterId.ToString();
				DataRow[] clusterInterfaceRows = clusterTable.Select 
					(string.Format ("GroupSeqId = '{0}' AND ClusterID = '{1}'", groupSeqId, clusterId), "SpaceGroup, PdbID ASC");
             //       (string.Format(" ClusterID = '{0}'", clusterId), "GroupSeqID, SpaceGroup, PdbID ASC");
				FormatGroupClustersIntoTable (clusterInterfaceRows, interfaceTable, entityInfoTable, entryCfGroupHash);
			}
		}

		/// <summary>
		/// Writer all clusters in a group into the file
		/// </summary>
		/// <param name="clusterInterfaceRows"></param>
		/// <param name="interfaceTable"></param>
		/// <param name="crystBuCompTable"></param>
		/// <param name="clusterWriter"></param>
		private void FormatGroupClustersIntoTable (DataRow[] clusterInterfaceRows, DataTable interfaceTable, DataTable entityInfoTable, Dictionary<string, int> entryCfGroupHash)
		{
			int groupSeqId = -1;
			int cfGroupId = -1;
			string pdbId = "";
			int interfaceId = -1;
			int clusterId = -1;
			string spaceGroup = "";
			int numOfInterfacesInSg = -1;
			string asu = "";
            string cfgString = "";
			foreach (DataRow clusterRow in clusterInterfaceRows)
			{
				groupSeqId = Convert.ToInt32 (clusterRow["GroupSeqId"].ToString ());
				pdbId = clusterRow["PdbID"].ToString ();
				cfGroupId = (int)entryCfGroupHash[pdbId];
				clusterId = Convert.ToInt32 (clusterRow["ClusterID"].ToString ());
				interfaceId = Convert.ToInt32 (clusterRow["InterfaceID"].ToString ());
				spaceGroup = clusterRow["SpaceGroup"].ToString ().Trim ();
				asu = clusterRow["ASU"].ToString ().Trim ();
                cfgString = spaceGroup + "_" + asu;
				if (sgInterfaceNumHash.ContainsKey (cfgString))
				{
					numOfInterfacesInSg = (int) sgInterfaceNumHash[cfgString];
				}
				else
				{
					numOfInterfacesInSg =  GetNumOfInterfacesInSg (interfaceTable, groupSeqId, spaceGroup, asu);
					sgInterfaceNumHash.Add (cfgString, numOfInterfacesInSg);
				}
				DataRow[] interfaceRows = interfaceTable.Select 
					(string.Format ("groupSeqId = '{0}' AND PdbID = '{1}' AND InterfaceID = '{2}'", 
					groupSeqId, pdbId, interfaceId));
				if (interfaceRows.Length == 0)
				{
					ProtCidSettings.progressInfo.progStrQueue.Enqueue ("No interface defined for groupSeqId: " + groupSeqId.ToString () + 
						" Entry: " + pdbId + " Interface ID: " + interfaceId);
					continue;
				}

			//	DataRow[] speciesRows = entityInfoTable.Select (string.Format ("PdbID = '{0}'", pdbId));
				// must have only one row
				DataRow interfaceStatRow = interfaceStatData.interfaceDataTable.NewRow ();
				interfaceStatRow["Species"] = GetInterfaceSpecies (pdbId, interfaceId, interfaceTable, entityInfoTable);
                interfaceStatRow["Name"] = GetInterfaceName (pdbId, interfaceId, interfaceTable, entityInfoTable);
                interfaceStatRow["UnpCode"] = GetInterfaceUnpCode(pdbId, interfaceId, interfaceTable, entityInfoTable);

				FomatClusterInterface (ref interfaceStatRow, clusterId, cfGroupId, interfaceRows[0], numOfInterfacesInSg);
                FormatBuAndCrystBuCompInfo(ref interfaceStatRow);
				interfaceStatData.interfaceDataTable.Rows.Add (interfaceStatRow);

				AddPdbPisaBuToHash (cfgString, interfaceStatRow);
			}
		}

		/// <summary>
		/// the number of interfaces in a space group
		/// which are common interfaces for all entries in the space group
		/// </summary>
		/// <param name="interfaceTable"></param>
		/// <param name="groupSeqId"></param>
		/// <param name="spaceGroup"></param>
		/// <returns></returns>
		private int GetNumOfInterfacesInSg (DataTable interfaceTable, int groupSeqId, string spaceGroup, string asu)
		{
			DataRow[] interfaceRows = interfaceTable.Select 
				(string.Format ("groupSeqId = '{0}' AND SpaceGroup = '{1}' AND ASU = '{2}'", 
				groupSeqId, spaceGroup, asu));
			return interfaceRows.Length;
		}

        private int GetNumOfInterfacesInSg(int groupSeqId, string spaceGroup, string crystForm)
        {
            string queryString = string.Format("Select * From {0}SgInterfaces " + 
                " Where groupSeqId = {1} AND SpaceGroup = '{2}' AND ASU = '{3}';", 
                ProtCidSettings.dataType, groupSeqId, spaceGroup, crystForm);
            DataTable sgInterfaceTable = protcidQuery.Query(queryString);
            return sgInterfaceTable.Rows.Count;
        }
		#region format cluster info
		/// <summary>
		/// format an interface info into a line
		/// </summary>
		/// <param name="clusterId"></param>
		/// <param name="interfaceRow"></param>
		/// <returns></returns>
        private void FomatClusterInterface(ref DataRow interfaceStatRow, int clusterId, int cfGroupId,
            DataRow interfaceRow, int numOfInterfacesInSg)
        {
            interfaceStatRow["groupSeqId"] = interfaceRow["groupSeqId"];
            interfaceStatRow["ClusterID"] = clusterId;
            interfaceStatRow["CfGroupID"] = cfGroupId;
            interfaceStatRow["SpaceGroup"] = interfaceRow["SpaceGroup"].ToString().Trim();
            interfaceStatRow["CrystForm"] = interfaceRow["ASU"].ToString().Trim();
            interfaceStatRow["PdbID"] = interfaceRow["PdbID"];
            interfaceStatRow["InterfaceID"] = interfaceRow["InterfaceID"];
            interfaceStatRow["InterfaceUnit"] = GetCrystInterfaceAbcFormat(interfaceRow["PdbID"].ToString(),
                Convert.ToInt32(interfaceRow["InterfaceID"].ToString()));
            interfaceStatRow["NumOfInterfaces"] = numOfInterfacesInSg;
            interfaceStatRow["SurfaceArea"] = interfaceRow["SurfaceArea"];
            // check if the interface is in ASU or not
            if (interfaceRow["SymmetryString1"].ToString().Trim() == "1_555" &&
                interfaceRow["SymmetryString2"].ToString().Trim() == "1_555")
            {
                interfaceStatRow["InASU"] = 1;
            }
            else
            {
                interfaceStatRow["InASU"] = 0;
            }
            interfaceStatRow["ASU"] = GetAsuStringFromCrystFormString(interfaceStatRow["CrystForm"].ToString());
        }

        /// <summary>
        /// the CrystForm is make up of two parts: ASU(X)
        /// X: the number of different crystal structures with same space group, ASU
        /// and similar unit cell
        /// </summary>
        /// <param name="crystFormString"></param>
        /// <returns></returns>
        private string GetAsuStringFromCrystFormString (string crystFormString)
        {
            int parenthesisIndex = crystFormString.IndexOf("(");
            string asu = "";
            if (parenthesisIndex > -1)
            {
                asu = crystFormString.Substring(0, parenthesisIndex);
            }
            else
            {
                asu = crystFormString;
            }
            return asu;
        }

        /// <summary>
        /// Add PDB/PISA BU, cryst and BU comp, BU comp info into the row
        /// </summary>
        /// <param name="interfaceStatRow"></param>
        public void FormatBuAndCrystBuCompInfo(ref DataRow interfaceStatRow)
        {
            string pdbId = interfaceStatRow["PdbID"].ToString();
            int crystInterfaceId = Convert.ToInt32(interfaceStatRow["InterfaceID"].ToString ());

            string pdbBuId = "";
            string pisaBuId = "";

            GetPdbPisaBuContainingCrystInterface(pdbId, crystInterfaceId, out pdbBuId, out pisaBuId);

            if (pdbBuId == "-1")
            {
                interfaceStatRow["InPDB"] = 0;
        //        pdbBuId = "1";
            }
            else
            {
                interfaceStatRow["InPDB"] = 1;
            }
            interfaceStatRow["PdbBU"] = GetBuAbcFormat(pdbId, ref pdbBuId, "pdb", ref pdbGroupEntryBuAbcFormatHash);
            interfaceStatRow["PdbBUID"] = pdbBuId;

            if (pisaBuId == "-1")
            {
                interfaceStatRow["InPisa"] = 0;
          //      pisaBuId = "1";
            }
            else
            {
                interfaceStatRow["InPisa"] = 1;
            }
            interfaceStatRow["PisaBU"] = GetBuAbcFormat(pdbId, ref pisaBuId, "pisa", ref pisaGroupEntryBuAbcFormatHash);
            interfaceStatRow["PisaBuID"] = pisaBuId;

            string buComp = GetBuCompInfo(interfaceStatRow);
            interfaceStatRow["PdbPisa"] = buComp;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="crystInterfaceId"></param>
        /// <param name="pdbBu"></param>
        /// <param name="pisaBu"></param>
        private void GetPdbPisaBuContainingCrystInterface(string pdbId, int crystInterfaceId, 
            out string pdbBu, out string pisaBu)
        {
            pdbBu = "";
            pisaBu = "";
            string[] pdbBuIDs = GetBUsContainingCrystInterface(pdbId, crystInterfaceId, "pdb");
            string[] pisaBuIDs = GetBUsContainingCrystInterface(pdbId, crystInterfaceId, "pisa");

            if (pdbBuIDs.Length > 0 && pisaBuIDs.Length > 0)
            {
                pdbBu = pdbBuIDs[0];
                pisaBu = pisaBuIDs[0];
            }
            else if (pdbBuIDs.Length > 0 && pisaBuIDs.Length == 0)
            {
                Dictionary<string, List<string>> pdbPisaSameBuHash = GetPisaSameBUsAsPdbBUs(pdbId, pdbBuIDs);
                if (pdbPisaSameBuHash.Count > 0)
                {
                    List<string> keyPdbBuList = new List<string>(pdbPisaSameBuHash.Keys);
                    keyPdbBuList.Sort();
                    pdbBu = keyPdbBuList[0].ToString();
                    List<string> samePisaBuList = pdbPisaSameBuHash[keyPdbBuList[0]];
                    samePisaBuList.Sort();
                    pisaBu = samePisaBuList[0].ToString();
                }
                else
                {
                    pdbBu = pdbBuIDs[0];
                    pisaBu = "-1";
                }
            }
            else if (pdbBuIDs.Length == 0 && pisaBuIDs.Length > 0)
            {
                pdbBu = "-1";
                pisaBu = pisaBuIDs[0];
            }
            else
            {
                pdbBu = "-1";
                pisaBu = "-1";
            }
        }
        /// <summary>
        /// the comp between two BUs from PDB and PISA
        /// </summary>
        /// <param name="interfaceStatRow"></param>
        /// <returns></returns>
        private string GetBuCompInfo(DataRow interfaceStatRow)
        {
            string queryString = string.Format("Select * From PdbPisaBuComp " + 
                " Where PdbID = '{0}' AND BuID1 = '{1}' AND BuID2 = '{2}';",
                interfaceStatRow["PdbId"], interfaceStatRow["PdbBuID"], interfaceStatRow["PisaBuID"]);
            DataTable buCompTable = bucompQuery.Query(queryString);

            if (buCompTable.Rows.Count > 0)
            {
                if (buCompTable.Rows[0]["IsSame"].ToString() == "1")
                {
                    if (Convert.ToInt32(buCompTable.Rows[0]["InterfaceNum1"].ToString()) ==
                        Convert.ToInt32(buCompTable.Rows[0]["InterfaceNum2"].ToString()))
                    {
                        return "same";
                    }
                    else
                    {
                        return "substruct";
                    }
                }
                else
                {
                    return "dif";
                }
            }
            else
            {
                string pdbBu = interfaceStatRow["PdbBu"].ToString();
                string pisaBu = interfaceStatRow["PisaBu"].ToString();
                if (pdbBu == pisaBu)
                {
                    return "same";
                }
                else if (pdbBu == "A" || pisaBu == "A")
                {
                    return "substruct";
                }
                else
                {
                    return "dif";
                }
            }
        }

        /// <summary>
        /// the first BU which contains this cryst interface
        /// otherwise -1
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private string[] GetBUsContainingCrystInterface(string pdbId, int interfaceId, string buType)
        {
            string queryString = string.Format("Select Distinct BuID From Cryst{0}BuInterfaceComp " +
                " Where PdbID = '{1}' AND InterfaceID = {2} AND QScore >= {3} ORDER BY BuID;",
                buType, pdbId, interfaceId, AppSettings.parameters.simInteractParam.interfaceSimCutoff);
            DataTable crystBuCompTable = protcidQuery.Query(queryString);
            //        string buId = "-1"; // no BU in the table
            string[] withCrystInterfaceBUs = new string[crystBuCompTable.Rows.Count];
            int count = 0;
            foreach (DataRow buRow in crystBuCompTable.Rows)
            {
                withCrystInterfaceBUs[count] = crystBuCompTable.Rows[0]["BuID"].ToString().TrimEnd();
                count++;
            }
            return withCrystInterfaceBUs;
        }

        /// <summary>
        /// since cryst and PISA BU interface comp info are not in the database 
        /// if PISA BU is same as PDB BU, so check if the any PISA BU same as 
        /// the PDB BUs containing the cryst interface.
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pdbBUs"></param>
        /// <returns>key: pdb bu, value: the list of same pisa BUs</returns>
        private Dictionary<string, List<string>> GetPisaSameBUsAsPdbBUs(string pdbId, string[] pdbBUs)
        {
            string queryString = "";
            Dictionary<string, List<string>> pdbPisaSameBuHash = new Dictionary<string, List<string>>();
            foreach (string pdbBu in pdbBUs)
            {
                queryString = string.Format("Select * From PdbPisaBuComp " +
                 " Where PdbID = '{0}' AND BuID1 = '{1}';", pdbId, pdbBu);
                DataTable buCompTable = bucompQuery.Query(queryString);
                foreach (DataRow buCompRow in buCompTable.Rows)
                {
                    if (ArePdbPisaBuSame(buCompRow))
                    {
                        if (pdbPisaSameBuHash.ContainsKey(pdbBu))
                        {
                            pdbPisaSameBuHash[pdbBu].Add(buCompRow["BuID2"].ToString().TrimEnd());
                        }
                        else
                        {
                            List<string> samePisaBuList = new List<string> ();
                            samePisaBuList.Add(buCompRow["BuID2"].ToString ().TrimEnd ());
                            pdbPisaSameBuHash.Add(pdbBu, samePisaBuList);
                        }
                    }
                }
            }
            return pdbPisaSameBuHash;
        }

        /// <summary>
        /// check if PDB BU and PISA BU same or not from the data row
        /// </summary>
        /// <param name="buCompRow"></param>
        /// <returns></returns>
        private bool ArePdbPisaBuSame(DataRow buCompRow)
        {
            if (buCompRow["IsSame"].ToString() == "1" &&
                buCompRow["InterfaceNum1"].ToString() == buCompRow["InterfaceNum2"].ToString())
            {
                return true;
            }
            return false;
        }
		#endregion

		#endregion

		#region format cluster Q scores 
		/// <summary>
		/// Q scores for interface comparison between space groups
		/// retrieve Q score for minimum identity in a cluster
		/// </summary>
		/// <param name="clusterInterfaceRows"></param>
		/// <param name="clusterWriter"></param>
		protected void FormatClusterSumInfoIntoTable (int groupSeqId, DataTable clusterTable, DataTable groupInterfaceCompTable, 
			DataTable sgInterfaceCompTable, DataTable groupRepAlignTable, Dictionary<string, int> entryCfGroupHash)
		{
			// get the number of space group in the family
            string queryString = string.Format("Select distinct PdbID, spaceGroup, ASU From {0} Where groupSeqId = {1};",
                 //	GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], groupSeqId);
                   GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo], groupSeqId);
            DataTable sgInterfaceTable = protcidQuery.Query(queryString);

			int numOfEntryInFamily = GetNumOfEntriesInFamily (groupSeqId, sgInterfaceTable);
		
			int numOfCFGsInFamily = GetNumOfCFGsInFamily (sgInterfaceTable, entryCfGroupHash);

			queryString = string.Format ("Select * From {0} Where groupSeqId = {1};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoGroupEntryAlign], groupSeqId);
            DataTable groupAlignTable = protcidQuery.Query(queryString);

			List<string> groupEntryList = new List<string> ();
			foreach (DataRow dRow in sgInterfaceTable.Rows)
			{
				groupEntryList.Add (dRow["PdbID"].ToString ());
			}

			DataRow[] clusterRows = clusterTable.Select (string.Format ("groupSeqId = '{0}'", groupSeqId), "ClusterID, SpaceGroup, ASU ASC");
			Dictionary<int, List<string>> clusterPdbInterfaceHash = new Dictionary<int,List<string>> ();
			Dictionary<int, List<int>> clusterCfHash = new Dictionary<int,List<int>> ();
			
			foreach (DataRow clusterRow in clusterRows)
			{
				int clusterId = Convert.ToInt32 (clusterRow["ClusterId"].ToString ());
				string pdbInterface = clusterRow["PdbID"].ToString () + "_" + clusterRow["InterfaceID"].ToString ();
				if (clusterPdbInterfaceHash.ContainsKey (clusterId))
				{
                    if (!clusterPdbInterfaceHash[clusterId].Contains(pdbInterface))
					{
                        clusterPdbInterfaceHash[clusterId].Add(pdbInterface);
					}
				}
				else
				{
					List<string> pdbInterfaceList = new List<string> ();
					pdbInterfaceList.Add (pdbInterface);
					clusterPdbInterfaceHash.Add (clusterId, pdbInterfaceList);
				}
		//		string sg_asu = clusterRow["SpaceGroup"].ToString ().Trim () + "_" + clusterRow["ASU"].ToString ().Trim ();
				string cfEntry = clusterRow["PdbID"].ToString ();
				if (clusterCfHash.ContainsKey (clusterId))
				{
                    if (!clusterCfHash[clusterId].Contains(entryCfGroupHash[cfEntry]))
					{
                        clusterCfHash[clusterId].Add(entryCfGroupHash[cfEntry]);
					}
				}
				else
				{
					List<int> crsytFormList = new List<int> ();
					crsytFormList.Add (entryCfGroupHash[cfEntry]);
					clusterCfHash.Add (clusterId, crsytFormList);
				}
			}
			List<int> clusterList = new List<int> (clusterPdbInterfaceHash.Keys);
			clusterList.Sort ();
			double minIdentity = 1000.0;
            double identity = 0;
            double qScore = 0;
			double qMinIdentity = -1.0;
			double outMaxIdentity = -1.0;
            string spaceGroup1 = "";
            string spaceGroup2 = "";
            string clusterInterface = "";
            double mediumSurfaceArea = 0;
			foreach (int clusterId in clusterList)
			{
				minIdentity = 1000.0;
				qMinIdentity = -1.0;
				outMaxIdentity = -1.0;

				List<string> pdbInterfaceList = clusterPdbInterfaceHash[clusterId];
				for (int i = 0; i < pdbInterfaceList.Count - 1; i ++)
				{
					string[] pdbInterface1 = pdbInterfaceList[i].ToString ().Split ('_');
					DataRow[] sgRows = clusterTable.Select 
						(string.Format ("groupSeqId = '{0}' AND PdbID = '{1}' AND InterfaceID = '{2}'", 
						groupSeqId, pdbInterface1[0], pdbInterface1[1]));
					spaceGroup1 = sgRows[0]["SpaceGroup"].ToString ().Trim ();
					for (int j = i + 1; j < pdbInterfaceList.Count; j ++)
					{
						string[] pdbInterface2 = pdbInterfaceList[j].ToString ().Split ('_');
						sgRows = clusterTable.Select (string.Format ("groupSeqId = '{0}' AND PdbID = '{1}' AND InterfaceID = '{2}'", 
							groupSeqId, pdbInterface2[0], pdbInterface2[1]));
						spaceGroup2 = sgRows[0]["SpaceGroup"].ToString ().Trim ();
						qScore = GetQScore (pdbInterface1[0], Convert.ToInt32 (pdbInterface1[1]), pdbInterface2[0], Convert.ToInt32 (pdbInterface2[1]), sgInterfaceCompTable, groupInterfaceCompTable, out identity);
		//				identity = GetIdentity (pdbInterface1[0], pdbInterface2[0], groupRepAlignTable);
						DataRow qScoreRow = interfaceStatData.clusterInterfaceCompTable.NewRow ();
						qScoreRow["groupSeqId"] = groupSeqId;
						qScoreRow["ClusterID"] = clusterId;
						qScoreRow["SpaceGroup1"] = spaceGroup1;
						qScoreRow["PdbID1"] = pdbInterface1[0];
						qScoreRow["InterfaceID1"] = pdbInterface1[1];
						qScoreRow["SpaceGroup2"] = spaceGroup2;
						qScoreRow["PdbID2"] = pdbInterface2[0];
						qScoreRow["InterfaceID2"] = pdbInterface2[1];
						qScoreRow["QScore"] = qScore;
						qScoreRow["Identity"] = identity;
						interfaceStatData.clusterInterfaceCompTable.Rows.Add (qScoreRow);
						if (identity > 0 && minIdentity > identity)
						{
							minIdentity = identity;
							qMinIdentity = qScore;
#if DEBUG
							if (qScore < AppSettings.parameters.contactParams.minQScore)
							{
								string logLine = groupSeqId.ToString () + "	" + clusterId.ToString () + "	" +
									pdbInterface1[0] + "	" + pdbInterface1[1] + "	" + 
									pdbInterface2[0] + "	" + pdbInterface2[1]  + "	" + 
									qScore.ToString () + "	" + identity.ToString ();
								logWriter.WriteLine (logLine);
							}
#endif
						}

					}
				}
				 
				DataRow[] clusterEntryRows = clusterTable.Select (string.Format ("groupSeqId = '{0}' AND ClusterID = '{1}'", 
					groupSeqId, clusterId));
				// find the maximum sequence identity between this cluster and entries outside of this cluster
				List<string> clusterEntryList = new List<string> ();
				foreach (DataRow dRow in clusterEntryRows)
				{
					if (! clusterEntryList.Contains (dRow["PdbID"].ToString ()))
					{
						clusterEntryList.Add (dRow["PdbID"].ToString ());
					}
				}
				if (clusterEntryList.Count == groupEntryList.Count)
				{
					outMaxIdentity = -1.0;
				}
				else
				{
					List<string> outClusterEntryList = new List<string> ();
					foreach (string entry in groupEntryList)
					{
						if (clusterEntryList.Contains (entry))
						{
							continue;
						}
						outClusterEntryList.Add (entry);
					}
					outMaxIdentity = GetOutMaxIdentity (clusterEntryList, outClusterEntryList, groupAlignTable);
				}
				// for cluster summary info
				DataRow clusterStatRow = interfaceStatData.clusterDataTable.NewRow ();
				clusterStatRow["groupSeqId"] = groupSeqId;
				clusterStatRow["ClusterID"] = clusterId;
				clusterStatRow["#CFG/Cluster"] = clusterCfHash[clusterId].Count;
                clusterStatRow["#CFG/Family"] = numOfCFGsInFamily;
				clusterStatRow["#Entry/Family"] = numOfEntryInFamily;
				clusterStatRow["MinSeqIdentity"] = minIdentity;
				clusterStatRow["Q(MinIdentity)"] = qMinIdentity;
				clusterStatRow["OutMaxSeqIdentity"] = outMaxIdentity;
                clusterStatRow["InterfaceType"] = GetInterfaceTypeInCluster(clusterId);
                clusterInterface = GetClusterInterface(groupSeqId, clusterId, clusterTable, out mediumSurfaceArea);
                clusterStatRow["ClusterInterface"] = clusterInterface;
                clusterStatRow["MediumSurfaceArea"] = mediumSurfaceArea;
				interfaceStatData.clusterDataTable.Rows.Add (clusterStatRow);
			}
        }

        /// <summary>
        /// the cluster interface is the first interface of a representative entry in the cluster
        /// with medium surface area
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="mediumSurfaceArea"></param>
        /// <returns></returns>
        private string GetClusterInterface(int groupSeqId, int clusterId, DataTable clusterTable, out double mediumSurfaceArea)
        {
            DataRow[] clusterRows = clusterTable.Select(string.Format ("ClusterID = {0}", clusterId), "PdbID, InterfaceID ASC");
            List<string> entryList = new List<string> ();
            string pdbId = "";
            int interfaceId = 0;
            DataTable clusterRepInterfaceInfoTable = interfaceStatData.interfaceDataTable.Clone();
            foreach (DataRow clusterRow in clusterRows)
            {
                pdbId = clusterRow["PdbID"].ToString();
                if (entryList.Contains(pdbId))
                {
                    continue;
                }
                entryList.Add(pdbId);
                interfaceId = Convert.ToInt32(clusterRow["InterfaceID"].ToString ());
                DataRow[] interfaceRows = interfaceStatData.interfaceDataTable.Select
                    (string.Format ("PdbID = '{0}' AND InterfaceID= '{1}'", pdbId, interfaceId));
                if (interfaceRows.Length > 0)
                {
                    DataRow newRow = clusterRepInterfaceInfoTable.NewRow();
                    newRow.ItemArray = interfaceRows[0].ItemArray;
                    clusterRepInterfaceInfoTable.Rows.Add(newRow);
                }
            }

            DataRow[] clusterInterfaceRows = clusterRepInterfaceInfoTable.Select ("", "SurfaceArea ASC");
            int mediumIndex = (int)(clusterInterfaceRows.Length / 2);
            string clusterInterface = clusterInterfaceRows[mediumIndex]["PdbId"].ToString() + "_" +
                clusterInterfaceRows[mediumIndex]["InterfaceID"].ToString();
            mediumSurfaceArea = Convert.ToDouble(clusterInterfaceRows[mediumIndex]["SurfaceArea"].ToString());
            /*      string clusterInterface = "";
                  double surfaceArea = 0;
                  mediumSurfaceArea = 0;
                  foreach (DataRow interfaceRow in clusterInterfaceRows)
                  {
                      surfaceArea = Convert.ToDouble(interfaceRow["SurfaceArea"].ToString ());
                      if (maxSurfaceArea < surfaceArea)
                      {
                          maxSurfaceArea = surfaceArea;
                          clusterInterface = interfaceRow["PdbID"].ToString() + "_" + interfaceRow["InterfaceID"].ToString();
                      }
                  }*/
            return clusterInterface;
        }

        /// <summary>
        /// S: same sequence (homodimers)
        /// D: different sequence (heterodimers)
        /// </summary>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        protected string GetInterfaceTypeInCluster(int clusterId)
        {
            DataRow[] clusterRows = interfaceStatData.interfaceDataTable.Select
                (string.Format("ClusterID = '{0}'", clusterId));
            bool isHetero = false;
            bool isHomo = false;
            string interfaceUnit = "";
            foreach (DataRow interfaceRow in clusterRows)
            {
                interfaceUnit = interfaceRow["InterfaceUnit"].ToString();
                if (interfaceUnit.Length < 2)
                {
#if DEUBG
                    logWriter.WriteLine(FormatDataRow (interfaceRow));
                    logWriter.Flush ();
#endif
                    continue;
                }
                if (interfaceUnit.IndexOf("'") > -1)
                {
                    isHetero = true;
                }
                else if (interfaceUnit[0] != interfaceUnit[1])
                {
                    if (char.IsDigit(interfaceUnit[1])) // for A2, B2
                    {
                        isHomo = true;
                    }
                    else
                    {
                        isHetero = true;
                    }
                }
                else
                {
                    isHomo = true;
                }
            }
            string interfaceType = "";
            if (isHomo)
            {
                interfaceType = "S"; // S: same sequence (homodimers)
            }
            if (isHetero)
            {
                if (interfaceType == "")
                {
                    interfaceType = "D";
                }
                else
                {
                    interfaceType += ",D"; // D: different sequence (heterodimers)
                }
            }
            return interfaceType;
        }
        #region the number of entries and number of CFGs in a group
        /// <summary>
        /// the number of entries in the family excluding those NMR monomers
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <param name="sgEntryTable"></param>
        /// <returns></returns>
        protected int GetNumOfEntriesInFamily(int groupSeqId, DataTable sgEntryTable)
        {
            int numOfEntriesInFamily = 0;
            string pdbId = "";
            string spaceGroup = "";
            string asuString = "";
            string realAsu = "";
            int numOfHomoEntries = 0;
            foreach (DataRow sgEntryRow in sgEntryTable.Rows)
            {
                pdbId = sgEntryRow["PdbID"].ToString();
                spaceGroup = sgEntryRow["SpaceGroup"].ToString().TrimEnd();
                if (spaceGroup == "NMR")
                {
                    asuString = sgEntryRow["ASU"].ToString().TrimEnd();
                    realAsu = GetRealAsymmetricUnitString(asuString);
                    if (realAsu == "A")
                    {
                        continue;
                    }
                }
                numOfEntriesInFamily++;
                numOfHomoEntries = GetNumOfHomoEntries (groupSeqId, pdbId);
                numOfEntriesInFamily += numOfHomoEntries;
            }
            return numOfEntriesInFamily;
        }

        /// <summary>
        /// the number of homologous entries for this representative entry
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <param name="repPdbId"></param>
        /// <returns></returns>
        private int GetNumOfHomoEntries(int groupSeqId, string repPdbId)
        {
            string queryString = string.Format("Select Distinct PdbID2 From {0} " + 
                " Where groupSeqId = {1} AND PdbID1 = '{2}';", 
                GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoRepEntryAlign], groupSeqId, repPdbId);
            DataTable homoEntryTable = protcidQuery.Query(queryString);
            return homoEntryTable.Rows.Count;
        }
        /// <summary>
        /// real ASU string for the entry
        /// </summary>
        /// <param name="asuString"></param>
        /// <returns></returns>
        private string GetRealAsymmetricUnitString(string asuString)
        {
            int leftParenthesisIndex = asuString.IndexOf("(");
            if (leftParenthesisIndex == -1)
            {
                return asuString;
            }
            return asuString.Substring(0, leftParenthesisIndex);
        }

        /// <summary>
        /// all NMR entries count as one space group
        /// </summary>
        /// <param name="sgInterfaceTable"></param>
        /// <returns></returns>
        protected int GetNumOfCFGsInFamily(DataTable sgInterfaceTable, Dictionary<string, int> entryCfGroupHash)
        {
            List<int> crystFormList = new List<int> ();
            string pdbId = "";
            foreach (DataRow dRow in sgInterfaceTable.Rows)
            {
                pdbId = dRow["PdbID"].ToString();
                if (entryCfGroupHash.ContainsKey(pdbId))
                {
                    if (!crystFormList.Contains(entryCfGroupHash[pdbId]))
                    {
                        crystFormList.Add(entryCfGroupHash[pdbId]);
                    }
                }
            }
            return crystFormList.Count;
        }
        #endregion
		/// <summary>
		/// get the maximum sequence identity
		/// between entries outside of the cluster 
		/// and entries in the cluster
		/// </summary>
		/// <param name="inEntryList"></param>
		/// <param name="groupRepAlignTable"></param>
		/// <returns></returns>
		private double GetOutMaxIdentity (List<string> inClusterEntryList, List<string> outClusterEntryList, DataTable groupEntryAlignTable)
		{
			double outMaxIdentity = -1.0;
			foreach (DataRow dRow in groupEntryAlignTable.Rows)
			{
				// both within cluster
				if (
					(inClusterEntryList.Contains (dRow["PdbID1"].ToString ())&& 
					outClusterEntryList.Contains (dRow["PdbID2"].ToString ())) 
					|| 
					(inClusterEntryList.Contains (dRow["PdbID2"].ToString ()) &&
					outClusterEntryList.Contains (dRow["PdbID1"].ToString ()))
					)
				{
					
					double identity = Convert.ToDouble (dRow["Identity"].ToString ());
					if (outMaxIdentity < identity)
					{
						outMaxIdentity = identity;
					}
				}
			}
			return outMaxIdentity;
		}
		/// <summary>
		/// Get the QScore for two interfaces from InterfaceOfSgComp table
		/// </summary>
		/// <param name="pdbId1"></param>
		/// <param name="interfaceId1"></param>
		/// <param name="pdbId2"></param>
		/// <param name="interfaceId2"></param>
		/// <param name="sgInterfaceCompTable"></param>
		/// <returns></returns>
		private double GetQScore (string pdbId1, int interfaceId1, string pdbId2, int interfaceId2, DataTable sgInterfaceCompTable, DataTable groupInterfaceCompTable, out double identity)
		{
			DataRow[] interfaceCompRows = null;
            identity = -1.0;
			if (pdbId1 == pdbId2)
			{
				
				interfaceCompRows = sgInterfaceCompTable.Select 
					(string.Format ("PdbID = '{0}' AND InterfaceID1 = '{1}' AND InterfaceID2 = '{2}'", 
					pdbId1, interfaceId1, interfaceId2));
				if (interfaceCompRows.Length == 0)
				{
					interfaceCompRows = sgInterfaceCompTable.Select 
						(string.Format ("PdbID = '{0}' AND InterfaceID1 = '{1}' AND InterfaceID2 = '{2}'", 
						pdbId1, interfaceId2, interfaceId1));
				}
                identity = 100.0;
			}
			else
			{
				interfaceCompRows = groupInterfaceCompTable.Select 
					(string.Format ("PdbID1 = '{0}' AND InterfaceID1 = '{1}' AND PdbID2 = '{2}' AND InterfaceID2 = '{3}'", 
					pdbId1, interfaceId1, pdbId2, interfaceId2));
				if (interfaceCompRows.Length == 0)
				{
					interfaceCompRows = groupInterfaceCompTable.Select 
						(string.Format ("PdbID1 = '{0}' AND InterfaceID1 = '{1}' AND PdbID2 = '{2}' AND InterfaceID2 = '{3}'", 
						pdbId2, interfaceId2, pdbId1, interfaceId1));
				}
                if (interfaceCompRows.Length > 0) // in difentryinterfacecomp, identity is in [0, 1]
                {
                    identity = Convert.ToDouble(interfaceCompRows[0]["Identity"].ToString()) * 100.0;
                }
			}
			double qScore = 0.0;
			// must have at least one row
			if (interfaceCompRows.Length > 0)
			{
				qScore = Convert.ToDouble (interfaceCompRows[0]["QScore"].ToString ());
			}
			return qScore;
		}   
		
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private bool IsInterfaceInAsu(string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select * From CrystEntryInterfaces " + 
                " Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable interfaceDefTable = protcidQuery.Query(queryString);
            if (interfaceDefTable.Rows.Count > 0)
            {
                if (interfaceDefTable.Rows[0]["SymmetryString1"].ToString().TrimEnd() == "1_555" &&
                    interfaceDefTable.Rows[0]["SymmetryString2"].ToString().TrimEnd() == "1_555")
                {
                    return true;
                }
            }
            return false;
        }
		#endregion

		#region write data into file
		protected string[] headerNames = null;
		/// <summary>
		/// write interface info into a file
		/// </summary>
		/// <param name="clusterWriter"></param>
		public void WriterStatDataToFile (StreamWriter clusterWriter, StreamWriter clusterSumWriter, string familyCode)
		{
			if (headerNames == null)
			{
				headerNames = GetHeaderNames ();
			}

			// write data to file
			clusterWriter.WriteLine (FormatHeaderString ());
			List<int> clusterList = new List<int> ();
			
			foreach (DataRow dRow in interfaceStatData.clusterDataTable.Rows)
			{
				clusterList.Add (Convert.ToInt32 (dRow["ClusterID"].ToString ()));
			}

			clusterList.Sort ();
			string dataStream = "";
			string modifiedColName = "";

			foreach (int clusterId in clusterList)
			{
				DataRow sumInfoRow = interfaceStatData.clusterSumInfoTable.NewRow ();
				dataStream = "";
				DataRow[] interfaceRows = interfaceStatData.interfaceDataTable.Select (string.Format ("ClusterId = '{0}'", clusterId), "SpaceGroup, CrystForm, PdbID ASC");
				if (interfaceRows.Length == 0)
				{
					continue;
				}
                List<string> clusterDistEntryList = new List<string>();
                List<string> clusterNmrEntryList = new List<string>();
				string line = "";
				double sumSurfaceArea = 0.0;
				int[] maxAsuNums = null;
				int[] maxPdbBuNums = null;
				int[] maxPisaBuNums = null;

                List<string> existAsuEntryList = new List<string>();
                List<string> existPdbEntryList = new List<string>();
                List<string> existPisaEntryList = new List<string>();

				int asaCount = 0;
				foreach (DataRow interfaceRow in interfaceRows)
				{
					if (interfaceRow["SpaceGroup"].ToString ().Trim () == "NMR")
					{
						if (! clusterNmrEntryList.Contains (interfaceRow["PdbID"].ToString ()))
						{
							clusterNmrEntryList.Add (interfaceRow["PdbID"].ToString ());
						}
					}
					line = "";
					if (! clusterDistEntryList.Contains (interfaceRow["PdbID"].ToString ()))
					{
						clusterDistEntryList.Add (interfaceRow["PdbID"].ToString ());
					}
                    if (interfaceRow["SurfaceArea"].ToString() != "" && interfaceRow["SurfaceArea"].ToString() != "-1")
					{
						sumSurfaceArea += Convert.ToDouble (interfaceRow["SurfaceArea"].ToString ());
						asaCount ++;
					}
					GetMaxCopyNumFromAsuBu (interfaceRow["ASU"].ToString (), ref maxAsuNums);
					GetMaxCopyNumFromAsuBu (interfaceRow["PdbBu"].ToString (), ref maxPdbBuNums);
					GetMaxCopyNumFromAsuBu (interfaceRow["PisaBu"].ToString (), ref maxPisaBuNums);

					// entries where the interface exists
					if (interfaceRow["InASU"].ToString () == "1")
					{
						if (! existAsuEntryList.Contains (interfaceRow["PdbID"].ToString ()))
						{
							existAsuEntryList.Add (interfaceRow["PdbID"].ToString ());
						}
					}
					if (interfaceRow["InPdb"].ToString () == "1")
					{
						if (! existPdbEntryList.Contains (interfaceRow["PdbID"].ToString ()))
						{
							existPdbEntryList.Add (interfaceRow["PdbID"].ToString ());
						}
					}
		
					if (interfaceRow["InPisa"].ToString () == "1")
					{
						if (! existPisaEntryList.Contains (interfaceRow["PdbID"].ToString ()))
						{
							existPisaEntryList.Add (interfaceRow["PdbID"].ToString ());
						}
					}
					foreach (string colName in headerNames)
					{
						if (interfaceStatData.interfaceDataTable.Columns.Contains (colName))
						{
							if (colName.ToUpper () == "groupSeqId")
							{
								line += familyCode;
							}
							else
							{
								line += interfaceRow[colName].ToString ();
							}
							line += "	";
						}
						else
						{
							line += "";
							line += "	";
						}
					}
					dataStream += line.TrimEnd ('	');
					dataStream += "\r\n";
				}

				double avgSurfaceArea = sumSurfaceArea / (double)(asaCount);
				DataRow[] clusterSummaryRows = interfaceStatData.clusterDataTable.Select 
					(string.Format ("ClusterID = '{0}'", clusterId));
				line = "";
				string sumLine = familyCode + "	";
				foreach (string colName in headerNames)
				{
					if (interfaceStatData.clusterDataTable.Columns.Contains (colName))
					{
						line += clusterSummaryRows[0][colName].ToString ();
						line += "	";
						sumLine += clusterSummaryRows[0][colName].ToString ();
                        if (colName.IndexOf("#") > -1 || colName.IndexOf("/") > -1 || colName.IndexOf ("(") > -1)
                        {
                            modifiedColName = colName.Replace("#", "NumOf");
                            modifiedColName = modifiedColName.Replace("/", "");
                            modifiedColName = modifiedColName.Replace("(", "_");
                            modifiedColName = modifiedColName.Replace(")", "");
                        }
                        else
                        {
                            modifiedColName = colName;
                        }
						sumInfoRow[modifiedColName] = clusterSummaryRows[0][colName];
						sumLine += "	";
					}
					else
					{
						switch (colName.ToUpper ())
						{
							case "SURFACEAREA":
								line += string.Format ("{0:0.##}", avgSurfaceArea);
								line += "	";
								sumLine += string.Format ("{0:0.##}", avgSurfaceArea);
								sumInfoRow["SurfaceArea"] = avgSurfaceArea;
								sumLine += "	";
								break;

							case "INASU":
								line += existAsuEntryList.Count;
								line += "	";
								sumLine += existAsuEntryList.Count;
								sumInfoRow["InASU"] = existAsuEntryList.Count;
								sumLine += "	";
								break;
							case "INPDB":
								line += existPdbEntryList.Count;
								line += "	";
								sumLine += existPdbEntryList.Count;
								sumInfoRow["InPDB"] = existPdbEntryList.Count;
								sumLine += "	";
								break;
				
							case "INPISA":
								line += existPisaEntryList.Count;
								line += "	";
								sumLine += existPisaEntryList.Count;
								sumInfoRow["InPISA"] = existPisaEntryList.Count;
								sumLine += "	";
								break;
							case "ASU":
								line += FormatMaxAsuBuString (maxAsuNums);
								sumLine += FormatMaxAsuBuString (maxAsuNums);
								sumInfoRow["MaxASU"] = FormatMaxAsuBuString (maxAsuNums);
								line += "	";
								sumLine += "	";
								break;
							case "PDBBU":
								line += FormatMaxAsuBuString (maxPdbBuNums);
								sumLine += FormatMaxAsuBuString (maxPdbBuNums);
								sumInfoRow["MaxPDBBU"] = FormatMaxAsuBuString (maxPdbBuNums);
								line += "	";
								sumLine += "	";
								break;
				
							case "PISABU":
								line += FormatMaxAsuBuString (maxPisaBuNums);
								sumLine += FormatMaxAsuBuString (maxPisaBuNums);
								sumInfoRow["MaxPISABU"] = FormatMaxAsuBuString (maxPisaBuNums);
								line += "	";
								sumLine += "	";
								break;
							case "#ENTRY/CLUSTER":
								line += clusterDistEntryList.Count;
								line += "	";
								sumLine += clusterDistEntryList.Count;
								sumInfoRow["NumOfEntryCluster"] = clusterDistEntryList.Count;
								sumLine += "	";
								break;
							default:
								line += "";
								line += "	";
								break;
						}						
					}					
				}// finish one cluster
				dataStream = line.TrimEnd ('	') + "\r\n" + dataStream;
				clusterWriter.WriteLine (dataStream);
				// add summary data into line
				// ratio #SG/Cluster and #SG/Family
				sumLine += string.Format ("{0:0.###}", Convert.ToDouble (clusterSummaryRows[0]["#CFG/Cluster"].ToString ()) /  
					Convert.ToDouble (clusterSummaryRows[0]["#CFG/Family"].ToString ()));
				sumLine += "	";

				// ratio #ASU/Cluster and #Entry/Cluster
				sumLine += string.Format ("{0:0.###}", (double) (existAsuEntryList.Count - clusterNmrEntryList.Count) /  
					(double) (clusterDistEntryList.Count - clusterNmrEntryList.Count));
				sumLine += "	";
				// ratio #PDBBU/Cluster and #Entry/Cluster
				sumLine += string.Format ("{0:0.###}", (double) (existPdbEntryList.Count - clusterNmrEntryList.Count) /  
					(double) (clusterDistEntryList.Count - clusterNmrEntryList.Count));
				sumLine += "	";
				// ratio #PISABU/Cluster and #Entry/Cluster
				sumLine += string.Format ("{0:0.###}", (double) (existPisaEntryList.Count) /  
					(double) (clusterDistEntryList.Count - clusterNmrEntryList.Count));
				sumLine += "	";
				// #NMR entries/Cluster
				sumLine += clusterNmrEntryList.Count.ToString ();
				sumInfoRow["NumOfNmr"] = clusterNmrEntryList.Count;
				clusterSumWriter.WriteLine (sumLine);

				// add the summary info row into the table
				interfaceStatData.clusterSumInfoTable.Rows.Add (sumInfoRow);
			}
			// insert data into database
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, interfaceStatData.clusterSumInfoTable);
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, interfaceStatData.interfaceDataTable);
			// then clear for next group
			interfaceStatData.Clear ();
        }

        #region format header line
        /// <summary>
		/// format header line
		/// </summary>
		/// <returns></returns>
		protected string FormatHeaderString ()
		{
			string headerLine = "";
			foreach (string header in headerNames)
			{
				headerLine += header;
				headerLine += "	";
			}
			return headerLine.TrimEnd ('	');
		}
		/// <summary>
		/// get header names
		/// </summary>
		/// <returns></returns>
		protected string[] GetHeaderNames ()
		{
            List<string> headerList = new List<string>();
			foreach (DataColumn dCol in interfaceStatData.interfaceDataTable.Columns)
			{
				if ( dCol.ColumnName.IndexOf ("Name") < 0 && dCol.ColumnName.IndexOf ("Species") < 0)
				{
					headerList.Add (dCol.ColumnName);
				}
			}
			foreach (DataColumn dCol in interfaceStatData.clusterDataTable.Columns)
			{
				if (dCol.ColumnName.IndexOf ("GroupSeqID") < 0 && dCol.ColumnName.IndexOf ("ClusterID") < 0)
				{
					headerList.Add (dCol.ColumnName);
				}
			}
			headerList.Add ("Name");
			headerList.Add ("Species");
			int idxClusterEntryNum = headerList.IndexOf ("#CFG/Cluster");
			headerList.Insert (idxClusterEntryNum + 1, "#Entry/Cluster");

            return headerList.ToArray();
        }
        #endregion
        #endregion

        #region  Write DataRow
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataRow"></param>
        /// <returns></returns>
        private string FormatDataRow(DataRow dataRow)
        {
            string dataLine = "";
            foreach (object item in dataRow.ItemArray)
            {
                dataLine += (item.ToString() + "\t");
            }
            return dataLine.TrimEnd('\t');
        }
        #endregion

        #region # of chains and maximum ABC format
        /// <summary>
        /// for monomer
        /// </summary>
        /// <param name="asuBuString"></param>
        /// <returns></returns>
        public int[] GetCopyNumFromAsuBu(string asuBuString)
        {
            string digitString = "";
            List<int> digitList = new List<int> ();
            foreach (char ch in asuBuString)
            {
                if (!char.IsDigit(ch))
                {
                    if (digitString != "")
                    {
                        digitList.Add(Convert.ToInt32(digitString));
                        digitString = "";
                    }
                    continue;
                }
                digitString += ch.ToString();
            }
            if (digitString != "")
            {
                digitList.Add(Convert.ToInt32(digitString));
            }
            if (digitString == "")
            {
                digitList.Add(1);
            }

            return digitList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asuBuString"></param>
        /// <param name="maxAsuNum"></param>
        public void GetMaxCopyNumFromAsuBu(string asuBuString, ref int[] maxNums)
        {
            if (asuBuString == "-")
            {
                return;
            }
            string digitString = "";
            List<int> digitList = new List<int> ();
            for (int i = 0; i < asuBuString.Length; i++)
            {
                if (char.IsLetter(asuBuString[i]) || asuBuString[i] == '(' || asuBuString[i] == ')')
                {
                    if (digitString != "")
                    {
                        digitList.Add(Convert.ToInt32(digitString));
                        digitString = "";
                    }
                    if (i + 1 < asuBuString.Length && char.IsLetter(asuBuString[i + 1]))
                    {
                        digitList.Add(1);
                    }
                }
                else
                {
                    digitString += asuBuString[i].ToString();
                }
            }
            if (digitString != "")
            {
                digitList.Add(Convert.ToInt32(digitString));
            }
            if (digitString == "")
            {
                digitList.Add(1);
            }
            if (maxNums == null)
            {
                maxNums = new int[digitList.Count];
                digitList.CopyTo(maxNums);
            }
            else
            {
                List<int> maxNumList = new List<int> (maxNums);
                for (int i = 0; i < maxNums.Length; i++)
                {
                    if (i < digitList.Count)
                    {
                        if (maxNums[i] < (int)digitList[i])
                        {
                            maxNumList[i] = digitList[i];
                        }
                    }
                }
                if (maxNums.Length < digitList.Count)
                {
                    int count = maxNums.Length;
                    while (count < digitList.Count)
                    {
                        maxNumList.Add(digitList[count]);
                        count++;
                    }
                }
                maxNums = new int[maxNumList.Count];
                maxNumList.CopyTo(maxNums);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="maxNums"></param>
        /// <returns></returns>
        public string FormatMaxAsuBuString(int[] maxNums)
        {
            if (maxNums == null)
            {
                return "-";
            }
            string maxAsuBuString = "";
            int chainCount = 0;
            for (int i = 0; i < maxNums.Length; i++)
            {
                if (i % chainNames.Length == 0)
                {
                    chainCount = 0;
                }
                if (maxNums[i] > 1)
                {
                    maxAsuBuString += (chainNames[chainCount] + maxNums[i].ToString());
                }
                else
                {
                    maxAsuBuString += chainNames[chainCount];
                }
                chainCount++;
            }
            return maxAsuBuString;
        }
        #endregion

        #region BU and Interface ABC format        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
   /*     public string GetCrystInterfaceAbcFormat(string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select * From CrystEntryInterfaces " +
                " Where PdbId = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable interfaceDefTable = dbQuery.Query(queryString);
            int entityId1 = -1;
            int entityId2 = -1;
            if (interfaceDefTable.Rows.Count > 0)
            {
                entityId1 = Convert.ToInt32(interfaceDefTable.Rows[0]["EntityID1"].ToString());
                entityId2 = Convert.ToInt32(interfaceDefTable.Rows[0]["EntityID2"].ToString());
                if (entityId1 == entityId2)
                {
                    return "A2"; // homodimer
                }
                else
                {
                    return "AB"; // heterodimer
                }
            }
            return "";
        }*/

       /// <summary>
       /// 
       /// </summary>
       /// <param name="pdbId"></param>
       /// <param name="interfaceId"></param>
       /// <param name="groupPfamArchChainHash"></param>
       /// <returns></returns>
        public string GetCrystInterfaceAbcFormat(string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select EntityID1, EntityID2 From CrystEntryInterfaces " +
                " Where PdbId = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable interfaceDefTable = protcidQuery.Query(queryString);
            int entityId1 = -1;
            int entityId2 = -1;
            if (interfaceDefTable.Rows.Count > 0)
            {
                entityId1 = Convert.ToInt32(interfaceDefTable.Rows[0]["EntityID1"].ToString());
                entityId2 = Convert.ToInt32(interfaceDefTable.Rows[0]["EntityID2"].ToString());
                if (entityId1 == entityId2)
                {
                   string chainName = (string)groupEntryEntityChainNameHash[pdbId + "_" + entityId1.ToString ()];
                   return chainName + "2";
                }
                else
                {
                    string chainName1 = (string)groupEntryEntityChainNameHash[pdbId + "_" + entityId1.ToString ()];
                    string chainName2 = (string)groupEntryEntityChainNameHash[pdbId + "_" + entityId2.ToString ()];
                    if (string.Compare(chainName1, chainName2) > 0)
                    {
                        return chainName2 + chainName1;
                    }
                    else if (chainName1 == chainName2) // heterodimer with same PFAM archtechture
                    {
                        return chainName1 + chainName2 + "'";
                    }
                    else
                    {
                        return chainName1 + chainName2;
                    }
                }
            }
            return "";
        }

        /// <summary>
        /// the family arch as chain names
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        public string GetInterfaceEntityFamilyArch(string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select EntityID1, EntityID2 From CrystEntryInterfaces " +
                " Where PdbId = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable interfaceDefTable = protcidQuery.Query(queryString);
        
            if (interfaceDefTable.Rows.Count > 0)
            {
                string entityId1 = interfaceDefTable.Rows[0]["EntityID1"].ToString();
                string entityId2 = interfaceDefTable.Rows[0]["EntityID2"].ToString();
                if (groupEntryEntityChainNameHash.ContainsKey(pdbId + "_" + entityId1) && groupEntryEntityChainNameHash.ContainsKey(pdbId + "_" + entityId2))
                {
                    string chainName1 = groupEntryEntityChainNameHash[pdbId + "_" + entityId1];
                    string chainName2 = groupEntryEntityChainNameHash[pdbId + "_" + entityId2];
                    if (string.Compare(chainName1, chainName2) > 0)
                    {
                        return chainName2 + chainName1;
                    }
                    else
                    {
                        return chainName1 + chainName2;
                    }
                }
            }
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupPdbList"></param>
        public void SetGroupEntryEntityChainNameHash(string[] groupPdbList, Dictionary<string, string> groupFamilyArchChainHash)
        {
            foreach (string pdbId in groupPdbList)
            {
                GetEntryEntityFamilyChainNames(pdbId, groupFamilyArchChainHash); 
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entryEntityChainNameHash"></param>
        public void GetEntryEntityFamilyChainNames(string pdbId, Dictionary<string, string> groupFamilyArchChainHash)
        {
            string queryString = string.Format("Select EntityID, AsymID From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide' ORDER BY EntityID, AsymID;", pdbId);
            DataTable asymChainTable = pdbfamQuery.Query(queryString);
            int entityId = -1;
            string asymChain = "";
            string entityFamilyArch = "";
            Dictionary<int, string> entityAsymChainHash = new Dictionary<int,string> ();
            foreach (DataRow chainRow in asymChainTable.Rows)
            {
                entityId = Convert.ToInt32 (chainRow["EntityID"].ToString());
                if (!entityAsymChainHash.ContainsKey(entityId))
                {
                    entityAsymChainHash.Add(entityId, chainRow["AsymID"].ToString ().TrimEnd ());
                }
            }
    //        ArrayList entityFamilyStringList = new ArrayList();
            foreach (int keyEntityId in entityAsymChainHash.Keys)
            {
                asymChain = entityAsymChainHash[keyEntityId];
                entityFamilyArch = GetChainPfamArchForAsymChain(pdbId, asymChain);
                if (groupFamilyArchChainHash.ContainsKey(entityFamilyArch))
                {
                    if (! groupEntryEntityChainNameHash.ContainsKey (pdbId + "_" + keyEntityId.ToString ()))
                    {
                        if (groupFamilyArchChainHash.ContainsKey(entityFamilyArch))
                        {
                            groupEntryEntityChainNameHash.Add(pdbId + "_" + keyEntityId.ToString(), groupFamilyArchChainHash[entityFamilyArch]);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 
        /// </summary
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
   //     private string GetFamilyStringForAsymChain (string pdbId, string asymChain)
        private string GetChainPfamArchForAsymChain  (string pdbId, string asymChain)
        {
            int entityId = asuInfoFinder.GetEntityIdForAsymChain(pdbId, asymChain);
            string queryString = string.Format("Select SupPfamArch, SupPfamArchE3, SupPfamArchE5 From PfamEntityPfamArch Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable pfamArchTable = pdbfamQuery.Query(queryString);
            string chainPfamArch = "";
            if (pfamArchTable.Rows.Count > 0)
            {
                chainPfamArch = pfamArchTable.Rows[0]["SupPfamArchE5"].ToString().TrimEnd();
                if (chainPfamArch == "")
                {
                    chainPfamArch = pfamArchTable.Rows[0]["SupPfamArchE3"].ToString().TrimEnd();
                }
                if (chainPfamArch == "")
                {
                    chainPfamArch = pfamArchTable.Rows[0]["SupPfamArch"].ToString().TrimEnd();
                }
            }
            return chainPfamArch;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        public string GetBuAbcFormat(string pdbId, ref string buId, string buType, ref Dictionary<string, Dictionary<string, string>>  entryBuAbcFormatHash)
        {
            Dictionary<string, string> buAbcFormatHash = null;
            if (entryBuAbcFormatHash.ContainsKey(pdbId))
            {
                buAbcFormatHash = entryBuAbcFormatHash[pdbId];
            }
            else
            {
                buAbcFormatHash = buQuery.GetEntryBUFormats(pdbId, buType);
                entryBuAbcFormatHash.Add(pdbId, buAbcFormatHash);
            }
            if (buAbcFormatHash.ContainsKey(buId))
            {
                return buAbcFormatHash[buId];
            }
            else if (buId == "-1" && buAbcFormatHash.Count > 0)
            {
                List<string> buList = new List<string> (buAbcFormatHash.Keys);
                buList.Sort();
                buId = buList[0];
                return buAbcFormatHash[buId];
            }
            return "-";
        }
        #endregion

        #region accessory functions 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryList1"></param>
        /// <param name="entryList2"></param>
        /// <returns></returns>
        public int GetDistinctEntryList(List<string> entryList1, List<string> entryList2)
        {
            List<string> entryList = new List<string> (entryList1);
            foreach (string entry in entryList2)
            {
                if (!entryList.Contains(entry))
                {
                    entryList.Add(entry);
                }
            }
            return entryList.Count;
        }
        #endregion

        #region for results output file
        /// <summary>
        /// divide the result file into several small files in order to display in the Excel.
        /// </summary>
        public void DivideChainInterfaceResultOutputFile(string resultFile)
        {
            int fileNum = 0;
            int familyLineCount = 0;
            int fileLineCount = 0;
            int fileLineCountTotal = 65000;
            StreamReader dataReader = new StreamReader(resultFile);
            StreamWriter dataWriter = new StreamWriter(resultFile.Replace (".txt", fileNum.ToString () + ".txt"));
            string line = "";
            string groupString = "";
            string preGroupString = "";
            string familyLines = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    familyLines += (line + "\r\n");
                    familyLineCount++;
                    continue;
                }
                string[] fields = line.Split('\t');
                groupString = fields[0];

                if ((groupString == "groupSeqId" || groupString == "SuperGroupSeqID") && preGroupString == "") // for first header line
                {
                    familyLines += (line + "\r\n");
                    familyLineCount++;
                    continue;
                }

                if (groupString == "groupSeqId" || groupString == "SuperGroupSeqID") // for new family
                {
                    if (fileLineCount + familyLineCount <= fileLineCountTotal)
                    {
                        dataWriter.Write(familyLines);
                        fileLineCount += familyLineCount;
                    }
                    else
                    {
                        dataWriter.Close();
                        fileNum++;
                        fileLineCount = familyLineCount;
                        dataWriter = new StreamWriter(resultFile.Replace (".txt", fileNum.ToString () + ".txt"));
                        dataWriter.Write(familyLines);
                    }
                    familyLines = "";
                    familyLineCount = 0;
                }

                familyLines += (line + "\r\n");
                familyLineCount++;
                preGroupString = groupString;
            }
            dataReader.Close();
            dataWriter.Close();
        }
        #endregion

        #region print heterodimer or larger from db
        /// <summary>
        /// 
        /// </summary>
        public void PrintHeteroInterfaceClustersFromDb(string type)
        {
            string resultDir = Path.Combine(ProtCidSettings.applicationStartPath, "HomoSeq\\result_chain_" + DateTime.Today.ToString("yyyyMMdd"));
            if (!Directory.Exists(resultDir))
            {
                Directory.CreateDirectory(resultDir);
            }
            interfaceStatData = new InterfaceStatData(type);

            StreamWriter clusterWriter = new StreamWriter(Path.Combine(resultDir, type + "HeteroChainInterfaceClusters_all.txt"));
            StreamWriter clusterSumInfoWriter = new StreamWriter(Path.Combine(resultDir, type + "HeteroChainInterfaceClusterSumInfo_all.txt"));
            string sumInfoHeaderLine = GetClusterSumInfoHeaderLine();
            clusterSumInfoWriter.WriteLine(sumInfoHeaderLine);
            string queryString = "Select Distinct groupSeqId From PfamClusterEntryInterfaces;";
            DataTable groupIdTable = protcidQuery.Query(queryString);
            int groupSeqId = -1;
            string groupFamilyString = "";

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = groupIdTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = groupIdTable.Rows.Count;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving Hetero groups cluter info.");
            ProtCidSettings.progressInfo.currentOperationLabel = "Cluster Info";

            foreach (DataRow groupIdRow in groupIdTable.Rows)
            {
                ProtCidSettings.progressInfo.currentFileName = groupSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                groupSeqId = Convert.ToInt32(groupIdRow["groupSeqId"].ToString ());
                groupFamilyString = GetGroupFamilyString(groupSeqId);
                if (IsGroupWithHeteroProteins(groupFamilyString))
                {
                    //     if (IsGroupWithSingleDomainProteins(groupFamilyString))
                    //     {
                    PrintGroupInterfaceClustersFromDb(groupSeqId, groupFamilyString, clusterWriter, clusterSumInfoWriter);
                    clusterWriter.Flush();
                    clusterSumInfoWriter.Flush();
                    //     }
                }
            }
            clusterWriter.Close();
            clusterSumInfoWriter.Close();
        }

        public void PrintGroupInterfaceClustersFromDb (int groupSeqId, string groupFamilyString, StreamWriter clusterWriter, StreamWriter clusterSumInfoWriter)
        {
            if (headerNames == null)
            {
                headerNames = GetHeaderNames();
            }

            // write data to file
            clusterWriter.WriteLine(FormatHeaderString());
            int[] clusterIdList = GetClusterIdList(groupSeqId);

            string dataStream = "";
            string modifiedColName = "";
            string line = "";
            foreach (int clusterId in clusterIdList)
            {
                DataTable clusterInterfaceTable = GetClusterInterfaceInfoFromDb(groupSeqId, clusterId);
                DataTable clusterSumInfoTable = GetClusterSumInfoFromDb(groupSeqId, clusterId);
                dataStream = "";

                foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
                {
                    line = "";
                    foreach (string colName in headerNames)
                    {
                        if (clusterInterfaceTable.Columns.Contains(colName))
                        {
                            if (colName.ToUpper() == "groupSeqId")
                            {
                                line += groupFamilyString;
                            }
                            else
                            {
                                line += interfaceRow[colName].ToString();
                            }
                            line += "\t";
                        }
                        else
                        {
                            line += "\t";
                        }
                    }
                    dataStream += line.TrimEnd('	');
                    dataStream += "\r\n";
                }

                string sumLine = groupFamilyString + "\t" ;
                line = "";
                foreach (string colName in headerNames)
                {
                    if (colName.IndexOf("#") > -1 || colName.IndexOf("/") > -1 || colName.IndexOf("(") > -1)
                    {
                        modifiedColName = colName.Replace("#", "NumOf");
                        modifiedColName = modifiedColName.Replace("/", "");
                        modifiedColName = modifiedColName.Replace("(", "_");
                        modifiedColName = modifiedColName.Replace(")", "");
                    }
                    else if (colName.ToUpper () == "ASU" || colName.ToUpper () == "PDBBU" || colName.ToUpper () == "PISABU")
                    {
                        modifiedColName = "Max" + colName;
                    }
                    else
                    {
                        modifiedColName = colName;
                    }

                    if (clusterSumInfoTable.Columns.Contains(modifiedColName))
                    {
                        line += clusterSumInfoTable.Rows[0][modifiedColName].ToString();
                        line += "\t";
                        sumLine += clusterSumInfoTable.Rows[0][modifiedColName].ToString();
                        sumLine += "\t";
                    }
                    else
                    {
                        line += "";
                        line += "	";
                    }
                }// finish one cluster
                // finish one cluster
                dataStream = line.TrimEnd('	') + "\r\n" + dataStream;
                clusterWriter.WriteLine(dataStream);
                // add summary data into line
                // ratio #SG/Cluster and #SG/Family
                sumLine += string.Format("{0:0.###}", Convert.ToDouble(clusterSumInfoTable.Rows[0]["NumOfCfgCluster"].ToString()) /
                    Convert.ToDouble(clusterSumInfoTable.Rows[0]["NumOfCFGFamily"].ToString()));
                sumLine += "\t";

                // ratio #ASU/Cluster and #Entry/Cluster
                int numOfAsu = Convert.ToInt32(clusterSumInfoTable.Rows[0]["InASU"].ToString());
                int numOfPdbBu = Convert.ToInt32(clusterSumInfoTable.Rows[0]["InPdb"].ToString ());
                int numOfPisaBu = Convert.ToInt32(clusterSumInfoTable.Rows[0]["InPisa"].ToString ());
                int numOfNmr = Convert.ToInt32 (clusterSumInfoTable.Rows[0]["NumOfNmr"].ToString());
                int numOfEntryCluster = Convert.ToInt32(clusterSumInfoTable.Rows[0]["NumOfEntryCluster"].ToString ());

                sumLine += string.Format("{0:0.###}", (double)(numOfAsu - numOfNmr) / (double)(numOfEntryCluster - numOfNmr));
                sumLine += "\t";
                // ratio #PDBBU/Cluster and #Entry/Cluster
                sumLine += string.Format("{0:0.###}", (double)(numOfPdbBu - numOfNmr) / (double)(numOfEntryCluster - numOfNmr));
                sumLine += "\t";
                // ratio #PISABU/Cluster and #Entry/Cluster
                sumLine += string.Format("{0:0.###}", (double)(numOfPisaBu) / (double)(numOfEntryCluster - numOfNmr));
                sumLine += "\t";
                sumLine += clusterSumInfoTable.Rows[0]["NumOfNmr"].ToString();
                clusterSumInfoWriter.WriteLine(sumLine);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string GetClusterSumInfoHeaderLine()
        {
            string queryString = "Select First 1 * From PfamClusterSumInfo;";
            DataTable clusterSumInfoTable = protcidQuery.Query(queryString);
            string headerLine = "PfamArch\t";
            foreach (DataColumn dCol in clusterSumInfoTable.Columns)
            {
                if (dCol.ColumnName.ToUpper() == "NUMOFNMR")
                {
                    continue;
                }
                headerLine += (dCol.ColumnName + "\t");
            }
            headerLine += "#CFGs-RATIO\t#ASU-RATIO\t#PDBBU-RATIO\t#PISABU-RATIO\tNUMOFNMR";
            return headerLine.TrimEnd('\t');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <returns></returns>
        private int[] GetClusterIdList(int groupSeqId)
        {
            string queryString = string.Format("Select Distinct ClusterID From PfamClusterEntryInterfaces Where groupSeqId = {0} ORDER BY ClusterID;", groupSeqId);
            DataTable clusterIdTable = protcidQuery.Query(queryString);
            int[] clusterIdList = new int[clusterIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow clusterIdRow in clusterIdTable.Rows)
            {
                clusterIdList[count] = Convert.ToInt32(clusterIdRow["ClusterID"].ToString ());
                count++;
            }
            return clusterIdList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private DataTable GetClusterInterfaceInfoFromDb(int groupSeqId, int clusterId)
        {
            string queryString = string.Format("Select * From PfamClusterEntryInterfaces Where groupSeqId = {0} AND ClusterID = {1};", groupSeqId, clusterId);
            DataTable clusterInterfaceInfoTable = protcidQuery.Query(queryString);
            return clusterInterfaceInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private DataTable GetClusterSumInfoFromDb(int groupSeqId, int clusterId)
        {
            string queryString = string.Format("Select * From PfamClusterSumInfo Where groupSeqId = {0} AND ClusterID = {1};", groupSeqId, clusterId);
            DataTable clusterSumInfoTable = protcidQuery.Query(queryString);
            return clusterSumInfoTable;
        }
        #region hetero
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <returns></returns>
        private string GetGroupFamilyString(int groupSeqId)
        {
            string queryString = string.Format("Select * From PfamGroups where groupSeqId = {0};", groupSeqId);
            DataTable familyStringTable = protcidQuery.Query(queryString);
            string groupFamilyString = familyStringTable.Rows[0]["EntryPfamArch"].ToString().TrimEnd();
            return groupFamilyString;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupFamilyString"></param>
        /// <returns></returns>
        private bool IsGroupWithHeteroProteins(string groupFamilyString)
        {
            string[] entityFamilies = SplitEntityFamilyString(groupFamilyString);
            if (entityFamilies.Length > 1)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSeqId"></param>
        /// <returns></returns>
        private bool IsGroupWithSingleDomainProteins (string groupFamilyString)
        {
            string[] entityFamilies = SplitEntityFamilyString(groupFamilyString);
            foreach (string entityFamily in entityFamilies)
            {
                if (IsEntityMultiDomains(entityFamily))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="familyString"></param>
        /// <returns></returns>
        private string[] SplitEntityFamilyString(string familyString)
        {
            string[] entityFamilies = familyString.Split(';');
            return entityFamilies;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityFamily"></param>
        /// <returns></returns>
        private bool IsEntityMultiDomains(string entityFamily)
        {
            if (entityFamily.IndexOf("_") > -1)
            {
                return true;
            }
            return false;
        }
        #endregion
        #endregion

        #region print interface files for specific chains
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryChains"></param>
        public void CopyInterfaceFilesWithSpecificChains(string[] entryChains)
        {
            string destFileDir = @"C:\ProtBuDProject\xtal\XtalInterfaceProject\BuXtal\bin\Debug\HomoSeq\result_chain_20110224";
            string interfaceFileSrcDir = Path.Combine (ProtCidSettings.dirSettings.interfaceFilePath, "cryst");
            string interfaceFile = "";
            string destInterfaceFile = "";
            string interfaceFileSrc = "";
             Dictionary<string, List<string>> entryChainsHash = new Dictionary<string,List<string>> ();
            string pdbId = "";
            string authChain = "";
            foreach (string entryChain in entryChains)
            {
                pdbId = entryChain.Substring(0, 4);
                authChain = entryChain.Substring(4, entryChain.Length - 4);
                if (entryChainsHash.ContainsKey(pdbId))
                {
                    entryChainsHash[pdbId].Add(authChain);
                }
                else
                {
                    List<string> chainList = new List<string> ();
                    chainList.Add(authChain);
                    entryChainsHash.Add(pdbId, chainList);
                }
            }
            foreach (string entry in entryChainsHash.Keys)
            {
                interfaceFileSrc = Path.Combine(interfaceFileSrcDir, entry.Substring(1, 2));

                string[] authChains = entryChainsHash[entry].ToArray (); 
                int[] interfaceIds = GetInterfacesWithChains(entry, authChains);
                foreach (int interfaceId in interfaceIds)
                {
                    interfaceFile = Path.Combine(interfaceFileSrc, entry + "_" + interfaceId.ToString () + ".cryst.gz");
                    destInterfaceFile = Path.Combine(destFileDir, entry + "_" + interfaceId.ToString() + ".cryst.gz");
                    File.Copy(interfaceFile, destInterfaceFile);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChains"></param>
        /// <returns></returns>
        private int[] GetInterfacesWithChains (string pdbId, string[] authChains)
        {
            string queryString = string.Format("Select *  From CrystEntryInterfaces " +
                " Where PdbId = '{0}';", pdbId);
            DataTable entryCrystInterfaceTable = protcidQuery.Query(queryString);
            string authChain1 = "";
            string authChain2 = "";
            List<int> interfaceList = new List<int> ();
            foreach (DataRow interfaceRow in entryCrystInterfaceTable.Rows)
            {
                authChain1 = interfaceRow["AuthChain1"].ToString().TrimEnd();
                authChain2 = interfaceRow["AuthChain2"].ToString().TrimEnd();
                if (Array.IndexOf(authChains, authChain1) > -1 &&
                    Array.IndexOf(authChains, authChain2) > -1)
                {
                    interfaceList.Add(Convert.ToInt32(interfaceRow["InterfaceID"].ToString()));
                }
            }
            int[] interfaceIds = new int[interfaceList.Count];
            interfaceList.CopyTo(interfaceIds);
            return interfaceIds;
        }
        #endregion

        #region for debug minseqidentity
        /// <summary>
        /// 
        /// </summary>
        public void UpdateMinSeqIdentities()
        {
            string queryString = "Select Distinct SuperGroupSeqID, ClusterID From PfamSuperClusterSumInfo Where MinSeqIdentity <= 0;";
            DataTable superClusterTable = protcidQuery.Query(queryString);
            int superGroupId = 0;
            int clusterId = 0;
            foreach (DataRow clusterRow in superClusterTable.Rows)
            {
                superGroupId = Convert.ToInt32(clusterRow["SuperGroupSeqID"].ToString ());
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString ());

            }
        }
        #endregion
    }
}
