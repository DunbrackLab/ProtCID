using System;
using System.Collections.Generic;
using System.Data;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;
using InterfaceClusterLib.DataTables;
using PfamLib.PfamArch;
using CrystalInterfaceLib.Settings;

namespace InterfaceClusterLib.Clustering
{
	/// <summary>
	/// Clustering interfaces in a homologous group
	/// </summary>
	public class InterfaceCluster
	{
		#region member variables
		public DataTable clusterTable = null;
		public DbInsert dbInsert = new DbInsert ();
		public DbQuery dbQuery = new DbQuery ();
		public double surfaceArea = 200.0;
		public const double leastQCutoff = 0.2;
		public string[] pdbInterfaces = null;
        public bool isDomain = false;
        public PfamArchitecture pfamArch = new PfamArchitecture ();
        public Clustering hierarchicalCluster = new Clustering();
        public string groupRelType = "same";
        public InterfaceCompDbQuery interfaceCompQuery = new InterfaceCompDbQuery();
		#endregion

		public InterfaceCluster()
		{
            if (AppSettings.parameters == null)
            {
                AppSettings.LoadParameters();
            }
            AppSettings.parameters.contactParams.minQScore = 0.1;
            AppSettings.parameters.simInteractParam.interfaceSimCutoff = 0.20;
		}

		/// <summary>
		/// cluster interfaces in a homologous group
		/// </summary>
		public void ClusterInterfaces ()
		{
			InitializeTable ();
			InitializeDbTable ();
            isDomain = false;

			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
			ProtCidSettings.progressInfo.currentOperationLabel = "Clustering interfaces";

			string queryString = string.Format ("SELECT DISTINCT GroupSeqID FROM {0};", 
         //   string queryString = string.Format("SELECT DISTINCT GroupSeqID, PdbID FROM {0} WHERE GroupSeqID = 795;",
				GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces]);
            DataTable groupTable = ProtCidSettings.protcidQuery.Query( queryString);

			ProtCidSettings.progressInfo.totalOperationNum = groupTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = groupTable.Rows.Count;
            int groupSeqID = 0;
			foreach (DataRow groupRow in groupTable.Rows)
			{
                groupSeqID = Convert.ToInt32 (groupRow["GroupSeqID"].ToString());
				ProtCidSettings.progressInfo.currentOperationNum ++;
				ProtCidSettings.progressInfo.currentStepNum ++;
				ProtCidSettings.progressInfo.currentFileName = groupSeqID.ToString ();

                try
                {
                    ClusterGroupInterfaces(groupSeqID);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                    ProtCidSettings.logWriter.WriteLine(ex.Message);
                }
                InsertDataToDb(); 
			}
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="GroupSeqID"></param>
        public void ClusterGroupInterfaces(int groupSeqId)
        {
            GroupDbTableNames.SetGroupDbTableNames("pfam");
            string queryString = string.Format("Select Distinct PdbID From {0} Where GroupSeqID = {1};",
                     GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], groupSeqId);
            DataTable groupEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> groupEntryList = new List<string> ();
            foreach (DataRow entryRow in groupEntryTable.Rows)
            {
                groupEntryList.Add (entryRow["PdbID"].ToString());
            }
           
            ClusterGroupInterfaces(groupSeqId, groupEntryList);  
        }

        #region interface comp info
		/// <summary>
		/// interface comparison between two different entries
		/// </summary>
		/// <param name="entryList"></param>
		/// <returns></returns>
        protected DataTable GetEntriesInterfaceComp(Dictionary<string, int[]> cfRepInterfaceHash)
		{
            if (cfRepInterfaceHash.Count == 0)
            {
                return null;
            }
            List<string> entryList = new List<string> (cfRepInterfaceHash.Keys);
		/*	string queryString = string.Format ("Select * From DifEntryInterfaceComp " + 
				" Where PdbID1 IN ({0}) AND PdbID2 IN ({0}) AND QScore > {1};", 
				ParseHelper.FormatSqlListString (entryList), leastQCutoff);
			DataTable difEntryInterfaceCompTable = dbQuery.Query (queryString);*/
            DataTable difEntryInterfaceCompTable = interfaceCompQuery.GetDifEntryInterfaceCompTable(entryList, leastQCutoff);

			DataTable interfaceCompTable = difEntryInterfaceCompTable.Clone ();
			foreach (DataRow compRow in difEntryInterfaceCompTable.Rows)
			{
				int[] interfaceList1 = (int[])cfRepInterfaceHash[compRow["PdbID1"].ToString ()];
				int[] interfaceList2 = (int[])cfRepInterfaceHash[compRow["PdbID2"].ToString ()];
				if (Array.IndexOf (interfaceList1, Convert.ToInt32 (compRow["InterfaceID1"].ToString ())) > -1 && 
					Array.IndexOf (interfaceList2, Convert.ToInt32 (compRow["InterfaceID2"].ToString ())) > -1)
				{
					DataRow newRow = interfaceCompTable.NewRow ();
					newRow.ItemArray = compRow.ItemArray;
					interfaceCompTable.Rows.Add (newRow);
				}
			}
			return interfaceCompTable;
		}

     
		/// <summary>
		/// interface comparison of one entry
		/// </summary>
		/// <param name="entryList"></param>
		/// <returns></returns>
		protected DataTable GetEntryInterfaceComp (List<string> entryList)
		{
            if (entryList.Count == 0)
            {
                return null;
            }
			string queryString = string.Format ("Select * From EntryInterfaceComp"  + 
				" Where PdbID IN ({0});", ParseHelper.FormatSqlListString (entryList.ToArray ()));
            DataTable entryInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
			return entryInterfaceCompTable;
		}

		/// <summary>
		/// interface comparison of one entry
		/// </summary>
		/// <param name="entryList"></param>
		/// <returns></returns>
        protected DataTable GetEntryInterfaceComp(Dictionary<string, int[]> cfRepInterfaceHash)
		{
            if (cfRepInterfaceHash.Count == 0)
            {
                return null;
            }
			List<string> entryList = new List<string> (cfRepInterfaceHash.Keys);
            string[] entries = entryList.ToArray();
            string queryString = "";
            DataTable entryInterfaceCompTable = null;
            for (int i = 0; i < entries.Length; i += 300)
            {
                string[] subEntryList = ParseHelper.GetSubArray (entries, i, 300);
                queryString = string.Format("Select * From EntryInterfaceComp Where PdbID IN ({0});", ParseHelper.FormatSqlListString(subEntryList));
                DataTable subEntryInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
                ParseHelper.AddNewTableToExistTable(subEntryInterfaceCompTable, ref entryInterfaceCompTable);
            }           
			DataTable repEntryInterfaceCompTable = entryInterfaceCompTable.Clone ();
			foreach (DataRow dRow in entryInterfaceCompTable.Rows)
			{
				int[] interfaceList = cfRepInterfaceHash[dRow["PdbID"].ToString ()];
				if (Array.IndexOf (interfaceList, Convert.ToInt32 (dRow["InterfaceID1"].ToString ())) > -1 && 
					Array.IndexOf (interfaceList, Convert.ToInt32 (dRow["InterfaceID2"].ToString ())) > -1)
				{
					DataRow newRow = repEntryInterfaceCompTable.NewRow ();
					newRow.ItemArray = dRow.ItemArray;
					repEntryInterfaceCompTable.Rows.Add (newRow);
				}
			}
			return repEntryInterfaceCompTable;
        }
        #endregion

        #region interface pfam arch
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cfRepInterfaceHash"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetCfRepEntryInterfacePfamArchHash(Dictionary<string, int[]> cfRepInterfaceHash)
        {
            if (cfRepInterfaceHash.Count == 0)
            {
                return null;
            }
            Dictionary<string, string> cfRepInterfaceFamilyArchHash = new Dictionary<string,string> ();
            foreach (string pdbId in cfRepInterfaceHash.Keys)
            {
                int[] cfRepInterfaces = cfRepInterfaceHash[pdbId];
                if (cfRepInterfaces.Length > 0)
                {
                    SetEntryInterfacePfamArchHash(pdbId, cfRepInterfaces, ref cfRepInterfaceFamilyArchHash);
                }
            }

            return cfRepInterfaceFamilyArchHash;
        }
        #endregion

        /// <summary>
		/// update interface clusters
		/// </summary>
		/// <param name="groupUpdateEntryHash"></param>
		public void UpdateInterfaceClusters (int[] updateGroups)
		{
            GroupDbTableNames.SetGroupDbTableNames("pfam");
			InitializeTable ();

			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
			ProtCidSettings.progressInfo.currentOperationLabel = "Clustering interfaces";

			ProtCidSettings.progressInfo.totalOperationNum = updateGroups.Length;
			ProtCidSettings.progressInfo.totalStepNum = updateGroups.Length;

		/*	string queryString = string.Format ("SELECT DISTINCT GroupSeqID, PdbID FROM {0} " + 
				" WHERE GroupSeqId IN ({1});", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces],
				ParseHelper.FormatSqlListString (new ArrayList (updateGroups)));
			DataTable groupTable = dbQuery.Query (queryString);
            */
			Dictionary<int, List<string>> updateGroupEntryHash = new Dictionary<int,List<string>> ();
		//	foreach (DataRow entryRow in groupTable.Rows)
            foreach (int updateGroup in updateGroups)
			{
                string[] groupEntries = GetGroupEntries(updateGroup);
                updateGroupEntryHash.Add(updateGroup, new List<string> (groupEntries));
			}

            List<int> groupList = new List<int> (updateGroupEntryHash.Keys);
			groupList.Sort ();
			foreach (int GroupSeqID in groupList)
			{
				ProtCidSettings.progressInfo.currentOperationNum ++;
				ProtCidSettings.progressInfo.currentStepNum ++;
				ProtCidSettings.progressInfo.currentFileName = GroupSeqID.ToString ();
				DeleteObsDataInDb (GroupSeqID);
                try
                {
                    ClusterGroupInterfaces(GroupSeqID, updateGroupEntryHash[GroupSeqID]);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                    ProtCidSettings.logWriter.WriteLine(ex.Message);
                }
                InsertDataToDb(); 
			}
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private string[] GetGroupEntries(int groupId)
        {
            string queryString = string.Format("SELECT DISTINCT PdbID FROM {0} " +
                " WHERE GroupSeqId = {1};",
                GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], groupId);
            DataTable groupEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] groupEntries = new string[groupEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in groupEntryTable.Rows)
            {
                groupEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return groupEntries;
        }
		/// <summary>
		/// cluster interface for this group
		/// </summary>
		/// <param name="GroupSeqID"></param>
		/// <param name="interfaceCompTable"></param>
		/// <param name="interfaceOfSgCompTable"></param>
		private void ClusterGroupInterfaces (int groupSeqId, List<string> entryList)
		{
            try
            {
                Dictionary<string, int> entryCfGroupHash = GetNonReduntCfGroups(groupSeqId);
                Dictionary<string,  int[]> cfRepInterfaceHash = new Dictionary<string,int[]> ();
                Dictionary<string, List<string>> cfRepHomoInterfaceHash = new Dictionary<string,List<string>> ();
                Dictionary<string, string> cfRepInterfaceFamilyArchHash = new Dictionary<string,string> ();

                GetCfRepEntryAndInterfaces(groupSeqId, entryList, entryCfGroupHash, ref cfRepInterfaceHash, ref cfRepHomoInterfaceHash, ref cfRepInterfaceFamilyArchHash);

                if (cfRepInterfaceHash.Count == 0)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("No rep interfaces in group " + groupSeqId.ToString ());
                    ProtCidSettings.logWriter.WriteLine("No rep interfaces in group " + groupSeqId.ToString ());
                    return;
                }
                DataTable groupInterfaceCompTable = GetEntriesInterfaceComp(cfRepInterfaceHash);
                DataTable sameEntryInterfaceCompTable = GetEntryInterfaceComp(cfRepInterfaceHash);
                Dictionary<string, int> interfaceIndexHash = GetInterfacesIndexes(groupInterfaceCompTable);
                if (interfaceIndexHash.Count == 0)
                {
                    interfaceIndexHash = GetInterfaceIndexes(cfRepInterfaceHash);
                }
                GetPdbInterfacesFromIndex(interfaceIndexHash);

                bool canClustered = true;
                double[,] distMatrix = CreateDistMatrix(sameEntryInterfaceCompTable, groupInterfaceCompTable,
                    interfaceIndexHash, out canClustered);
              
                Dictionary<int, int[]> clusterHash = ClusterThisGroupInterfaces(distMatrix, interfaceIndexHash, cfRepInterfaceFamilyArchHash);
                if (clusterHash.Count > 0)
                {
                    AssignDataToTable(groupSeqId, clusterHash, interfaceIndexHash, entryCfGroupHash, cfRepHomoInterfaceHash);
                }
            }
            catch (Exception ex)
            {
          //      ProtCidSettings.progressInfo.progStrQueue.Enqueue("Clustering group " + groupSeqId + " error: " + ex.Message);
          //      ProtCidSettings.logWriter.WriteLine("Clustering group " + groupSeqId + " error: " + ex.Message);
                throw new Exception("Cluster group " + groupSeqId + " error: " + ex.Message);
            }
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="GroupSeqID"></param>
        /// <returns></returns>
		public Dictionary<string, int> GetNonReduntCfGroups (int GroupSeqID)
		{
			string queryString = string.Format ("Select * From {0} Where GroupSeqID = {1};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.NonredundantCfGroups], GroupSeqID);
            DataTable nonRedunCfGroupTable = ProtCidSettings.protcidQuery.Query( queryString);
			Dictionary<string,int> entryCfHash = new Dictionary<string,int> ();
			foreach (DataRow dRow in nonRedunCfGroupTable.Rows)
			{
				entryCfHash.Add (dRow["PdbID"].ToString (), Convert.ToInt32 (dRow["CfGroupID"].ToString ()));
			}
			return entryCfHash;
		}

		#region representative entry and interfaces in CF
		/// <summary>
		/// 
		/// </summary>
		/// <param name="GroupSeqID"></param>
		/// <param name="entryList"></param>
		/// <param name="entryCfGroupHash"></param>
		/// <param name="cfRepInterfaceHash"></param>
		/// <param name="cfRepHomoInterfaceHash"></param>
		protected void GetCfRepEntryAndInterfaces (int groupSeqId, List<string> entryList, Dictionary<string, int> entryCfGroupHash,
            ref Dictionary<string,  int[]> cfRepInterfaceHash, ref Dictionary<string, List<string>> cfRepHomoInterfaceHash, ref Dictionary<string, string> cfRepInterfaceFamilyArchHash)
		{
            Dictionary<int, List<string>> cfEntryHash = GetCfEntriesFromEntryCfHash(entryCfGroupHash);

			string cfRepEntry = "";
			foreach (int cfGroupId in cfEntryHash.Keys)
			{
                List<string> cfEntryList = cfEntryHash[cfGroupId];
                cfRepEntry = GetEntryWithBestResolution(cfEntryList);
                if (cfRepEntry == "None")
                {
                    ProtCidSettings.logWriter.WriteLine(groupSeqId.ToString () + ":" + cfGroupId.ToString () + " no cf rep entry");
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }
                if (cfRepEntry == "NMR")
                {
                    foreach (string nmrEntry in cfEntryList)
                    {
                        int[] interfaces = GetEntryInterfaces(nmrEntry);
                        cfRepInterfaceHash.Add(nmrEntry, interfaces);
                        SetEntryInterfacePfamArchHash(cfRepEntry, interfaces, ref cfRepInterfaceFamilyArchHash);
                    }
                }
                else
                {
                    cfEntryList.Remove(cfRepEntry);
                    int[] commonInterfaces = GetCommonInterfacesInCF(cfRepEntry, cfEntryList);
                    cfRepInterfaceHash.Add(cfRepEntry, commonInterfaces);

                    SetEntryInterfacePfamArchHash(cfRepEntry, commonInterfaces, ref cfRepInterfaceFamilyArchHash);
                     
                    if (cfEntryList.Count > 0)
                    {
                        GetSimilarInterfacesFromOtherCfEntries(cfRepEntry, commonInterfaces, cfEntryList, ref cfRepHomoInterfaceHash, cfRepInterfaceFamilyArchHash);
                    }
                }
			}
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryCfHash"></param>
        /// <returns></returns>
        protected Dictionary<int, List<string>> GetCfEntriesFromEntryCfHash(Dictionary<string, int> entryCfHash)
        {
            Dictionary<int, List<string>> cfEntryHash = new Dictionary<int,List<string>> ();
            foreach (string entry in entryCfHash.Keys)
            {
                if (cfEntryHash.ContainsKey(entryCfHash[entry]))
                {
                    cfEntryHash[entryCfHash[entry]].Add(entry);
                }
                else
                {
                    List<string> cfEntryList = new List<string> ();
                    cfEntryList.Add(entry);
                    cfEntryHash.Add (entryCfHash[entry], cfEntryList);
                }
            }
            return cfEntryHash;
        }
        /// <summary>
        /// the entry with best resolution for this crystal form
        /// </summary>
        /// <param name="cfEntryList"></param>
        /// <returns></returns>
		protected string GetEntryWithBestResolution (List<string> cfEntryList)
		{
			string queryString = string.Format ("Select PdbID, Resolution, Method From PdbEntry " + 
				" WHere PdbID IN ({0});", ParseHelper.FormatSqlListString (cfEntryList.ToArray ()));
            DataTable resolutionTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (resolutionTable.Rows.Count == 0)
            {
                return "None";
            }
			if (resolutionTable.Rows[0]["Method"].ToString ().TrimEnd ().IndexOf ("NMR") > -1)
			{
				return "NMR";  // treat each nmr entry as one cf
				//			return resolutionTable.Rows[0]["PdbID"].ToString ();
			}
			double resolution = 0.0;
			double bestResolution = 100.0;
			string entryWithBestResolution = "";
			foreach (DataRow dRow in resolutionTable.Rows)
			{
				resolution = Convert.ToDouble (dRow["Resolution"].ToString ());
				if (bestResolution > resolution)
				{
					bestResolution = resolution;
					entryWithBestResolution = dRow["PdbID"].ToString ();
				}
			}
			return entryWithBestResolution;
		}

        /// <summary>
        /// the interfaces for this entry
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
		private int[] GetEntryInterfaces (string entry)
		{
			string queryString = string.Format ("Select Distinct InterfaceID From CrystEntryInterfaces " + 
				" Where PdbID = '{0}' AND SurfaceArea > {1};", entry, surfaceArea);
            DataTable entryInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
			int[] interfaces = new int [entryInterfaceTable.Rows.Count];
			int i = 0;
			foreach (DataRow dRow in entryInterfaceTable.Rows)
			{
				interfaces[i] = Convert.ToInt32 (dRow["InterfaceID"].ToString ());
                i++;
			}
			return interfaces;
        }

        /// <summary>
        /// common interfaces for the crystal form
        /// </summary>
        /// <param name="cfRepEntry"></param>
        /// <param name="homoEntryList"></param>
        /// <returns></returns>
		private int[] GetCommonInterfacesInCF (string cfRepEntry, List<string> homoEntryList)
		{
			string  queryString = string.Format ("Select Distinct InterfaceID From CrystEntryInterfaces " + 
				" Where PdbID = '{0}' AND SurfaceArea > '{1}';", cfRepEntry, surfaceArea);
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
			List<int> commonInterfaceList = new List<int> ();
			foreach (DataRow interfaceRow in interfaceTable.Rows)
			{
				commonInterfaceList.Add (Convert.ToInt32 (interfaceRow["InterfaceID"].ToString ()));
			}
			double qScore = 0.0;
			int interfaceId = 0;
			foreach (string homoEntry in homoEntryList)
			{
				List<int> tempComInterfaceList = new List<int> ();
				DataTable interfaceCompTable = GetInterfaceCompTable (cfRepEntry, homoEntry);
				foreach (DataRow compRow in interfaceCompTable.Rows)
				{
					interfaceId = Convert.ToInt32 (compRow["InterfaceID1"].ToString ());
					qScore = Convert.ToDouble (compRow["QScore"].ToString ());
					if (qScore > AppSettings.parameters.contactParams.minQScore)
             //       if (qScore > AppSettings.parameters.simInteractParam.interfaceSimCutoff)
					{
						if (! tempComInterfaceList.Contains (interfaceId))
						{
							if (commonInterfaceList.Contains (interfaceId))
							{
								tempComInterfaceList.Add (interfaceId);
							}
						}
					}
				}
				commonInterfaceList = tempComInterfaceList;
			}
            return commonInterfaceList.ToArray ();
		}

		/// <summary>
		/// the comparison between two entries
		/// </summary>
		/// <param name="pdbId1"></param>
		/// <param name="pdbId2"></param>
		/// <returns></returns>
        protected DataTable GetInterfaceCompTable (string pdbId1, string pdbId2)
		{
			string queryString = string.Format ("Select * From DifEntryInterfaceComp " + 
				" Where PdbID1 = '{0}' AND PdbID2 = '{1}';", pdbId1, pdbId2);
            DataTable interfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
			if (interfaceCompTable.Rows.Count ==  0)
			{
				queryString = string.Format ("Select * From DifEntryInterfaceComp " + 
					" Where PdbID2 = '{0}' AND PdbID1 = '{1}';", pdbId1, pdbId2);
                DataTable tempCompTable = ProtCidSettings.protcidQuery.Query( queryString);
				ReverseInterfaceCompTable (tempCompTable, ref interfaceCompTable);
			}
			return interfaceCompTable;
		}

		/// <summary>
		/// the similar interfaces from the homologous entries as the interface of pdbId
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="interfaceId"></param>
		/// <param name="homoEntryList"></param>
		/// <returns></returns>
        protected DataTable GetSimilarInterfaceCompTable (string pdbId, int interfaceId, List<string> homoEntryList)
		{
            string queryString = string.Format("Select * From DifEntryInterfaceComp " +
                " Where PdbID1 = '{0}' AND InterfaceID1 = '{1}' AND PdbID2 IN ({2})" +
                " AND QScore > {3};", pdbId, interfaceId, ParseHelper.FormatSqlListString(homoEntryList.ToArray ()),
                leastQCutoff);
            DataTable interfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);

            queryString = string.Format("Select * From DifEntryInterfaceComp " +
                " Where PdbID2 = '{0}' AND InterfaceID2 = '{1}' AND PdbID1 IN ({2}) " +
               " AND QScore > {3};", pdbId, interfaceId, ParseHelper.FormatSqlListString(homoEntryList.ToArray ()),
                //	AppSettings.parameters.simInteractParam.interfaceSimCutoff);
                leastQCutoff);
            DataTable tempCompTable = ProtCidSettings.protcidQuery.Query( queryString);
			ReverseInterfaceCompTable (tempCompTable, ref interfaceCompTable);

			return interfaceCompTable;
		}

        /// <summary>
        /// Reverse the Q score table if two entries are reversed
        /// </summary>
        /// <param name="tempCompTable"></param>
        /// <param name="interfaceCompTable"></param>
		protected void ReverseInterfaceCompTable (DataTable tempCompTable, ref DataTable interfaceCompTable)
		{
			foreach (DataRow compRow in tempCompTable.Rows)
			{
				DataRow newRow = interfaceCompTable.NewRow ();
				newRow.ItemArray = compRow.ItemArray;
				newRow["PdbID1"] = compRow["PdbID2"];
				newRow["InterfaceID1"] = compRow["InterfaceID2"];
				newRow["PdbID2"] = compRow["PdbID1"];
				newRow["InterfaceID2"] = compRow["InterfaceID1"];
				interfaceCompTable.Rows.Add (newRow);
			}
		}

        /// <summary>
        /// find the similar interfaces as the interfaces of the representative entry in the crystal form group
        /// which contain the same PFAM architecture
        /// </summary>
        /// <param name="cfRepEntry"></param>
        /// <param name="commonInterfaces"></param>
        /// <param name="homoEntryList"></param>
        /// <param name="cfRepHomoInterfaceHash"></param>
		private void GetSimilarInterfacesFromOtherCfEntries (string cfRepEntry, int[] commonInterfaces, List<string> homoEntryList,
            ref Dictionary<string, List<string>> cfRepHomoInterfaceHash, Dictionary<string, string> cfRepInterfaceFamilyArchHash)
		{
			string repInterfaceString = "";
            string homoInterfaceString = "";
            string cfRepInterfaceArch = "";
            string cfHomoInterfaceArch = "";
            Dictionary<string, string> homoEntryInterfaceFamilyArchHash = new Dictionary<string,string> ();
            foreach (string homoEntry in homoEntryList)
            {
                SetEntryInterfacePfamArchHash(homoEntry, null, ref homoEntryInterfaceFamilyArchHash);
            }
			foreach (int interfaceId in commonInterfaces)
			{
				repInterfaceString = cfRepEntry + "_" + interfaceId.ToString ();
                cfRepInterfaceArch = cfRepInterfaceFamilyArchHash[repInterfaceString];
				DataTable interfaceCompTable = GetSimilarInterfaceCompTable (cfRepEntry, interfaceId, homoEntryList);
				foreach (DataRow dRow in interfaceCompTable.Rows)
				{	
                    homoInterfaceString = dRow["PdbID2"].ToString () + "_" + dRow["InterfaceID2"].ToString ();
                    cfHomoInterfaceArch = homoEntryInterfaceFamilyArchHash[homoInterfaceString];
                    if (cfRepInterfaceArch == cfHomoInterfaceArch)
                    {
                        if (cfRepHomoInterfaceHash.ContainsKey(repInterfaceString))
                        {
                            cfRepHomoInterfaceHash[repInterfaceString].Add(homoInterfaceString);
                        }
                        else
                        {
                            List<string> simInterfaceList = new List<string> ();
                            simInterfaceList.Add(homoInterfaceString);
                            cfRepHomoInterfaceHash.Add(repInterfaceString, simInterfaceList);
                        }
                    }
				}
			}
		}
		#endregion

		#region indexing interfaces
		/// <summary>
		/// index each interfaces in a homologous group
		/// </summary>
		/// <param name="sgInterfaceCompRows">all space group interfaces</param>
		/// <returns></returns>
        private Dictionary<string, int> GetInterfacesIndexes(DataTable groupInterfaceCompTable, DataTable sgInterfaceTable)
		{
			Dictionary<string, int> interfaceIndexHash = new Dictionary<string,int> ();
			int indexNum = 0;
			string pdbId = "";
			int interfaceId = -1;
			// index every interfaces in the homologous group, starting index is 0
			foreach (DataRow sgInterfaceCompRow in groupInterfaceCompTable.Rows)
			{
				pdbId = sgInterfaceCompRow["PdbID1"].ToString ();
				interfaceId = Convert.ToInt32 (sgInterfaceCompRow["InterfaceID1"].ToString ());
				DataRow[] interfaceRows = sgInterfaceTable.Select 
					(string.Format ("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, interfaceId));
				if (interfaceRows.Length > 0)
				{
					AddInterfaceToIndex (pdbId, interfaceId, ref interfaceIndexHash, ref indexNum);
				}
	
				pdbId = sgInterfaceCompRow["PdbID2"].ToString ();
				interfaceId = Convert.ToInt32 (sgInterfaceCompRow["InterfaceID2"].ToString ());
				interfaceRows = sgInterfaceTable.Select 
					(string.Format ("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, interfaceId));
				if (interfaceRows.Length > 0)
				{
					AddInterfaceToIndex (pdbId, interfaceId, ref interfaceIndexHash, ref indexNum);
				}
			}
			return interfaceIndexHash;
		}

		/// <summary>
		/// index each interfaces in a homologous group
		/// </summary>
		/// <param name="sgInterfaceCompRows">all space group interfaces</param>
		/// <returns></returns>
		public Dictionary<string, int> GetInterfacesIndexes (DataTable groupInterfaceCompTable)
		{
			Dictionary<string, int> interfaceIndexHash = new Dictionary<string,int> ();
			int indexNum = 0;
			string pdbId = "";
			int interfaceId = -1;
			// index every interfaces in the homologous group, starting index is 0
			foreach (DataRow sgInterfaceCompRow in groupInterfaceCompTable.Rows)
			{
				pdbId = sgInterfaceCompRow["PdbID1"].ToString ();
				interfaceId = Convert.ToInt32 (sgInterfaceCompRow["InterfaceID1"].ToString ());
				AddInterfaceToIndex (pdbId, interfaceId, ref interfaceIndexHash, ref indexNum);
				
				pdbId = sgInterfaceCompRow["PdbID2"].ToString ();
				interfaceId = Convert.ToInt32 (sgInterfaceCompRow["InterfaceID2"].ToString ());
				AddInterfaceToIndex (pdbId, interfaceId, ref interfaceIndexHash, ref indexNum);
			}
			return interfaceIndexHash;
		}

     
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, int> GetInterfaceIndexes(Dictionary<string, int[]> cfRepInterfaceHash)
        {
            Dictionary<string, int> interfaceIndexHash = new Dictionary<string,int> ();
            int indexNum = 0;
            foreach (string repEntry in cfRepInterfaceHash.Keys)
            {
                int[] interfaceList = cfRepInterfaceHash[repEntry];
                foreach (int interfaceId in interfaceList)
                {
                    AddInterfaceToIndex(repEntry, interfaceId, ref interfaceIndexHash, ref indexNum);
                }
            }
            return interfaceIndexHash;
        }
		/// <summary>
		/// add interface to index hash
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="interfaceId"></param>
		/// <param name="contactNumTable"></param>
		/// <param name="interfaceIndexHash"></param>
		/// <param name="indexNum"></param>
		private void AddInterfaceToIndex (string pdbId, int interfaceId, ref Dictionary<string, int> interfaceIndexHash, ref int indexNum)
		{
			string pdbInterfaceString = pdbId + "_" + interfaceId;
			if (! interfaceIndexHash.ContainsKey (pdbInterfaceString))
			{
				interfaceIndexHash.Add (pdbInterfaceString, indexNum);
				indexNum ++;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="interfaceIndexHash"></param>
		public void GetPdbInterfacesFromIndex (Dictionary<string, int> interfaceIndexHash)
		{
			pdbInterfaces = new string [interfaceIndexHash.Count];
			foreach (string pdbInterface in interfaceIndexHash.Keys)
			{
				pdbInterfaces[(int)interfaceIndexHash[pdbInterface]] = pdbInterface;
			}
		}
		#endregion

		#region distance matrix
		/// <summary>
		/// set the distance matrix
		/// </summary>
		/// <param name="sgInterfaceCompRows"></param>
		/// <param name="groupInterfaceCompRows"></param>
		/// <param name="interfaceIndexHash"></param>
		/// <param name="canClustered">any pair of interfaces are similar</param>
		/// <returns></returns>
		public double[,] CreateDistMatrix (DataTable sgInterfaceCompTable, DataTable groupInterfaceCompTable, Dictionary<string, int> interfaceIndexHash, out bool canClustered)
		{
			// if no similar interfaces exist, don't need to apply clustering
			canClustered = false;
			double[,] distMatrix = new double [interfaceIndexHash.Count, interfaceIndexHash.Count];
			int interfaceIndex1 = -1;
			int interfaceIndex2 = -1;
            if (sgInterfaceCompTable != null)
            {
                foreach (DataRow interfaceCompRow in sgInterfaceCompTable.Rows)
                {
                    double qScore = Convert.ToDouble(interfaceCompRow["QScore"].ToString());

                    string pdbInterface1 = interfaceCompRow["PdbID"].ToString()
                        + "_" + interfaceCompRow["InterfaceID1"].ToString();
                    string pdbInterface2 = interfaceCompRow["PdbID"].ToString()
                        + "_" + interfaceCompRow["InterfaceID2"].ToString();
                    if (interfaceIndexHash.ContainsKey(pdbInterface1) &&
                        interfaceIndexHash.ContainsKey(pdbInterface2))
                    {
                        interfaceIndex1 = (int)interfaceIndexHash[pdbInterface1];
                        interfaceIndex2 = (int)interfaceIndexHash[pdbInterface2];
                        distMatrix[interfaceIndex1, interfaceIndex2] = qScore;
                        distMatrix[interfaceIndex2, interfaceIndex1] = qScore;
                        if (qScore >= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
                        {
                            canClustered = true;
                        }
                    }
                }
            }
            if (groupInterfaceCompTable != null)
            {
                foreach (DataRow interfaceCompRow in groupInterfaceCompTable.Rows)
                {
                    double qScore = Convert.ToDouble(interfaceCompRow["QScore"].ToString());

                    string pdbInterface1 = interfaceCompRow["PdbID1"].ToString()
                        + "_" + interfaceCompRow["InterfaceID1"].ToString();
                    string pdbInterface2 = interfaceCompRow["PdbID2"].ToString()
                        + "_" + interfaceCompRow["InterfaceID2"].ToString();
                    if (interfaceIndexHash.ContainsKey(pdbInterface1) &&
                        interfaceIndexHash.ContainsKey(pdbInterface2))
                    {
                        interfaceIndex1 = (int)interfaceIndexHash[pdbInterface1];
                        interfaceIndex2 = (int)interfaceIndexHash[pdbInterface2];
                        distMatrix[interfaceIndex1, interfaceIndex2] = qScore;
                        distMatrix[interfaceIndex2, interfaceIndex1] = qScore;
                        if (qScore >= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
                        {
                            canClustered = true;
                        }
                    }
                }
            }
			return distMatrix;
		}

		/// <summary>
		/// Initialize a matrix
		/// </summary>
		/// <param name="matrix"></param>
		private void InitializeMatrix (ref double[,] matrix)
		{
			for (int i = 0; i <= matrix.GetUpperBound (0); i ++)
			{
				for (int j= 0; j <= matrix.GetUpperBound (1); j ++)
				{
					if (i == j)
					{
						matrix[i, j] = 1;
					}
					else
					{
						matrix[i, j] = 0;
					}
				}
			}
		}
		#endregion

		#region clustering 
		/// <summary>
		/// clustering interfaces based on distance matrix
        /// Modified on June 2, 2010 so that clusters can contain one crystal form group and/or one entry
		/// </summary>
		/// <param name="distMatrix"></param>
		/// <param name="interfaceIndexHash"></param>
		public Dictionary<int, int[]> ClusterThisGroupInterfaces (double[,] distMatrix, Dictionary<string, int> interfaceIndexHash)
		{
			Dictionary<int, int[]> clusterHash = new Dictionary<int,int[]> ();
			int clusterId = 1;
			List<int> interfaceNumsInGroup = new List<int>  ();
			List<string> entryInGroup = new List<string> ();
			List<int> leftInterfaceNumList = new List<int> ();
			List<string> simEntryList = new List<string> ();
			for (int count = 0; count < interfaceIndexHash.Count; count ++)
			{
				leftInterfaceNumList.Add (count);
			}			
			//	leftInterfaceNumList.Sort ();
			// set the starting interface to be the interface with maximum number of similar interfaces
			// so that the first cluster can have the largest number of interfaces
			int[] sortedInterfaceIndexes = SortInterfaceIdxBySimCount (distMatrix);
			
			for (int i = 0; i < sortedInterfaceIndexes.Length - 1; i ++ )
			{
				entryInGroup.Clear ();
				if (! leftInterfaceNumList.Contains (sortedInterfaceIndexes[i]))
				{
					continue;
				}
				interfaceNumsInGroup.Add (sortedInterfaceIndexes[i]);
				entryInGroup.Add (pdbInterfaces[sortedInterfaceIndexes[i]].Substring (0, 4));
				leftInterfaceNumList.Remove (sortedInterfaceIndexes[i]);

				for (int j = i + 1; j < sortedInterfaceIndexes.Length; j ++)
				{
					simEntryList.Clear ();
					foreach (int interfaceIndex in interfaceNumsInGroup)
					{
						if (distMatrix[interfaceIndex, sortedInterfaceIndexes[j]] >= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
						{
							if (! simEntryList.Contains (pdbInterfaces[interfaceIndex].Substring (0, 4)))
							{
								simEntryList.Add (pdbInterfaces[interfaceIndex].Substring (0, 4));
							}
						}		
					}
				//	if (simEntryList.Count >= (int)(Math.Ceiling (entryInGroup.Count / 2.0)) )
                    if (simEntryList.Count >= 1)
					{
						if (leftInterfaceNumList.Contains (sortedInterfaceIndexes[j]))
						{
							interfaceNumsInGroup.Add (sortedInterfaceIndexes[j]);
							if (! entryInGroup.Contains (pdbInterfaces[sortedInterfaceIndexes[j]].Substring (0, 4)))
							{
								entryInGroup.Add (pdbInterfaces[sortedInterfaceIndexes[j]].Substring (0, 4));
							}
							leftInterfaceNumList.Remove (sortedInterfaceIndexes[j]);
						}
					}
				}
				//			if (CanBeACluster (interfaceNumsInGroup, pdbInterfaces, entryCfGroupHash))
				//				{
				clusterHash.Add (clusterId, interfaceNumsInGroup.ToArray ());
				clusterId ++;
				//				}
				interfaceNumsInGroup = new List<int> ();
			}
			return clusterHash;
		}

        /// <summary>
		/// clustering interfaces based on distance matrix
		/// </summary>
		/// <param name="distMatrix"></param>
		/// <param name="interfaceIndexHash"></param> 
        public Dictionary<int, int[]> ClusterThisGroupInterfaces(double[,] distMatrix, Dictionary<string, int> interfaceIndexHash, Dictionary<string, string> interfaceFamilyArchHash)
        {
            Dictionary<int, string> interfaceIndexPfamArchHash = ChangePfamArchHashKeyToIndex(interfaceIndexHash, interfaceFamilyArchHash);
            hierarchicalCluster.InterfacePfamArchHash = interfaceIndexPfamArchHash;
            hierarchicalCluster.PdbInterfaces = pdbInterfaces;
            List<List<int>> clusterList = hierarchicalCluster.Cluster(distMatrix);
            int clusterId = 1;
            Dictionary<int, int[]> clusterHash = new Dictionary<int,int[]> ();
            foreach (List<int> cluster in clusterList)
            {
                clusterHash.Add(clusterId, cluster.ToArray ());
                clusterId++;
            }
            return clusterHash;
        }       

        /// <summary>
        /// clustering interfaces based on distance matrix
        /// </summary>
        /// <param name="distMatrix"></param>
        /// <param name="interfaceIndexHash"></param> 
        public Dictionary<int, int[]> ClusterThisGroupInterfaces(double[,] distMatrix)
        {
            hierarchicalCluster.PdbInterfaces = pdbInterfaces;
            List<List<int>> clusterList = hierarchicalCluster.Cluster(distMatrix);
            int clusterId = 1;
            Dictionary<int, int[]> clusterHash = new Dictionary<int, int[]>();
            foreach (List<int> cluster in clusterList)
            {
                clusterHash.Add(clusterId, cluster.ToArray ());
                clusterId++;
            }
            return clusterHash;
        }

        /// <summary>
        /// clustering interfaces based on distance matrix
        /// </summary>
        /// <param name="distMatrix"></param>
        /// <param name="interfaceIndexHash"></param> 
        public Dictionary<int, int[]> ClusterThisBigGroupInterfaces(double[,] distMatrix)
        {
            hierarchicalCluster.PdbInterfaces = pdbInterfaces;
            List<List<int>> clusterList = hierarchicalCluster.ClusterInBig(distMatrix);
            int clusterId = 1;
            Dictionary<int, int[]> clusterHash = new Dictionary<int, int[]>();
            foreach (List<int> cluster in clusterList)
            {
                clusterHash.Add(clusterId, cluster.ToArray ());
                clusterId++;
            }
            return clusterHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceIndexHash"></param>
        /// <param name="interfaceFamilyArchHash"></param>
        /// <returns></returns>
        private Dictionary<int, string> ChangePfamArchHashKeyToIndex(Dictionary<string, int> interfaceIndexHash, Dictionary<string, string> interfaceFamilyArchHash)
        {
            Dictionary<int, string> interfaceIndexPfamArchHash = new Dictionary<int,string> ();
            foreach (string entryInterface in interfaceFamilyArchHash.Keys)
            {
                if (interfaceIndexHash.ContainsKey(entryInterface))
                {
                    int interfaceIndex = interfaceIndexHash[entryInterface];
                    interfaceIndexPfamArchHash.Add(interfaceIndex, interfaceFamilyArchHash[entryInterface]);
                }
            }
            return interfaceIndexPfamArchHash;
        }


             /*     public Hashtable ClusterThisGroupInterfaces(double[,] distMatrix, Hashtable interfaceIndexHash, Hashtable interfaceFamilyArchHash)
                  {
                      Hashtable clusterHash = new Hashtable();
                      int clusterId = 1;
                      ArrayList interfaceNumsInGroup = new ArrayList();
                      ArrayList entryInGroup = new ArrayList();
                      ArrayList leftInterfaceNumList = new ArrayList();
                      ArrayList simEntryList = new ArrayList();
                      for (int count = 0; count < interfaceIndexHash.Count; count++)
                      {
                          leftInterfaceNumList.Add(count);
                      }
                      //	leftInterfaceNumList.Sort ();
                      // set the starting interface to be the interface with maximum number of similar interfaces
                      // so that the first cluster can have the largest number of interfaces
                      int[] sortedInterfaceIndexes = SortInterfaceIdxBySimCount(distMatrix);

                      for (int i = 0; i < sortedInterfaceIndexes.Length; i++)
                      {
                          entryInGroup.Clear();
                          if (!leftInterfaceNumList.Contains(sortedInterfaceIndexes[i]))
                          {
                              continue;
                          }
                          interfaceNumsInGroup.Add(sortedInterfaceIndexes[i]);
                          entryInGroup.Add(pdbInterfaces[sortedInterfaceIndexes[i]].Substring(0, 4));
                          leftInterfaceNumList.Remove(sortedInterfaceIndexes[i]);

                          for (int j = i + 1; j < sortedInterfaceIndexes.Length; j++)
                          {
                              simEntryList.Clear();
                              foreach (int interfaceIndex in interfaceNumsInGroup)
                              {
                                  if (distMatrix[interfaceIndex, sortedInterfaceIndexes[j]] >= AppSettings.parameters.simInteractParam.interfaceSimCutoff
                                      && AreTwoInterfacesWithSameFamilyArch(pdbInterfaces[interfaceIndex], pdbInterfaces[sortedInterfaceIndexes[j]], interfaceFamilyArchHash))
                                  {
                                      if (!simEntryList.Contains(pdbInterfaces[interfaceIndex].Substring(0, 4)))
                                      {
                                          simEntryList.Add(pdbInterfaces[interfaceIndex].Substring(0, 4));
                                      }
                                  }
                              }
                              // similar to interfaces of at least 2 entries if there are at least two entries in the group
                              //  Modified on July 14, 2010

                              if ((entryInGroup.Count == 1 && simEntryList.Count >= 1) ||
                                  simEntryList.Count >= 2)
                              //    if (simEntryList.Count >= 1)
                              {
                                  if (leftInterfaceNumList.Contains(sortedInterfaceIndexes[j]))
                                  {
                                      interfaceNumsInGroup.Add(sortedInterfaceIndexes[j]);
                                      if (!entryInGroup.Contains(pdbInterfaces[sortedInterfaceIndexes[j]].Substring(0, 4)))
                                      {
                                          entryInGroup.Add(pdbInterfaces[sortedInterfaceIndexes[j]].Substring(0, 4));
                                      }
                                      leftInterfaceNumList.Remove(sortedInterfaceIndexes[j]);
                                  }
                              }
                          }
                          //      if (CanBeACluster(interfaceNumsInGroup, pdbInterfaces))
                          //      {
                          clusterHash.Add(clusterId, interfaceNumsInGroup);
                          clusterId++;
                          //      }
                          interfaceNumsInGroup = new ArrayList();
                      }
                      return clusterHash;
                  }
                      */
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryInterface1"></param>
        /// <param name="entryInterface2"></param>
        /// <param name="entryInterfaceFamilyArchHash"></param>
        /// <returns></returns>
        private bool AreTwoInterfacesWithSameFamilyArch(string entryInterface1, string entryInterface2, Dictionary<string, string> entryInterfaceFamilyArchHash)
        {
            if ((string)entryInterfaceFamilyArchHash[entryInterface1] == (string)entryInterfaceFamilyArchHash[entryInterface2])
            {
                return true;
            }
            return false;
        }

		#region deal with redundant crystal forms
		/// <summary>
		/// if the interface group has >= 2 different crystal forms, it is a cluster
		/// otherwise, not
		/// </summary>
		/// <param name="interfaceNumsInGroup"></param>
		/// <param name="interfaceIndexHash"></param>
		/// <returns></returns>
		private bool CanBeACluster (List<int> interfaceNumsInGroup, string[] pdbInterfaces, Dictionary<string, int> entryCfGroupHash)
		{
			List<int> clusterCfList = new List<int> ();
			string pdbId = "";
			foreach (int interfaceIdx in interfaceNumsInGroup)
			{
				pdbId = pdbInterfaces[interfaceIdx].Substring (0, 4);
				if (! clusterCfList.Contains (entryCfGroupHash[pdbId]))	
				{
					clusterCfList.Add (entryCfGroupHash[pdbId]);
				}
			}
			if (clusterCfList.Count < 2) // only one CF in the cluster
			{
				return false;
			}
			return true;
		}

        /// <summary>
        /// if a cluster has more than one entry, then it is a cluster
        /// otherwise, not
        /// </summary>
        /// <param name="interfaceNumsInGroup"></param>
        /// <param name="interfaceIndexHash"></param>
        /// <returns></returns>
        private bool CanBeACluster(List<int> interfaceNumsInGroup, string[] pdbInterfaces)
        {
            List<string> clusterEntryList = new List<string> ();
            string pdbId = "";
            foreach (int interfaceIdx in interfaceNumsInGroup)
            {
                pdbId = pdbInterfaces[interfaceIdx].Substring(0, 4);
                if (!clusterEntryList.Contains(pdbId))
                {
                    clusterEntryList.Add(pdbId);
                }
            }
            if (clusterEntryList.Count < 2) // only one entry in the cluster
            {
                return false;
            }
            return true;
        }
		#endregion

		/// <summary>
		/// sort interfaces by the number of similar interfaces
		/// the interface with most similar interfaces added into the cluster first,
		/// then those interfaces with less similar interfaces
		/// </summary>
		/// <param name="distMatrix"></param>
		/// <returns></returns>
		private int[] SortInterfaceIdxBySimCount (double[,] distMatrix)
		{
			List<string> simCountInterfaceStringList = new List<string> ();
			List<string> simEntryList = new List<string> ();
			string pdbId = "";
            for (int i = 0; i <= distMatrix.GetUpperBound(0); i++)
            {
                simEntryList.Clear();
                for (int j = 0; j <= distMatrix.GetUpperBound(1); j++)
                {
                    if (i == j || distMatrix[i, j] >= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
                    {
                        pdbId = pdbInterfaces[j].Substring(0, 4);
                        if (!simEntryList.Contains(pdbId))
                        {
                            simEntryList.Add(pdbId);
                        }
                    }
                }
                //		if (simEntryList.Count > 1)
                //		{
                simCountInterfaceStringList.Add(simEntryList.Count.ToString().PadLeft(5, '0') + "_" + i.ToString().PadLeft(4, '0'));
                //		}
            }
			simCountInterfaceStringList.Sort ();
			int[] interfaceIdxList = new int [simCountInterfaceStringList.Count];
			int count = 0;
			for (int i = simCountInterfaceStringList.Count - 1; i >= 0; i --)
			{
				string[] fields = simCountInterfaceStringList[i].ToString ().Split ('_');
				interfaceIdxList[count] = Convert.ToInt32 (fields[1]);
				count ++;
			}
			return interfaceIdxList;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="distMatrix"></param>
        /// <param name="entryCfGroupHash"></param>
        /// <returns></returns>
		private int[] SortInterfaceIdxByCfCount (double[,] distMatrix, Dictionary<string, int> entryCfGroupHash)
		{
			List<string> simCountInterfaceStringList = new List<string> ();
			//		int simCount = 0;
			List<int> simCfList = new List<int> ();
			string pdbId = "";
			for (int i = 0; i <= distMatrix.GetUpperBound (0); i ++)
			{
				//		simCount = 0;
				simCfList.Clear ();
				for (int j = 0; j <= distMatrix.GetUpperBound (1); j ++)
				{
					if (i == j || distMatrix[i, j] >= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
					{
						//		simCount ++;
						pdbId = pdbInterfaces[j].Substring (0, 4);
						if (! simCfList.Contains ((int)entryCfGroupHash[pdbId]))
						{
							simCfList.Add ((int)entryCfGroupHash[pdbId]);
						}
					}
				}
				if (simCfList.Count > 1)
				{
					simCountInterfaceStringList.Add (simCfList.Count.ToString ().PadLeft (5, '0') + "_" + i.ToString ().PadLeft (4, '0'));
				}
			}
			simCountInterfaceStringList.Sort ();
			int[] interfaceIdxList = new int [simCountInterfaceStringList.Count];
			int count = 0;
			for (int i = simCountInterfaceStringList.Count - 1; i >= 0; i --)
			{
				string[] fields = simCountInterfaceStringList[i].ToString ().Split ('_');
				interfaceIdxList[count] = Convert.ToInt32 (fields[1]);
				count ++;
			}
			return interfaceIdxList;
		}
		#endregion

		#region assign data to table
		/// <summary>
		/// assign cluster data into table
		/// </summary>
		/// <param name="GroupSeqID"></param>
		/// <param name="clusterHash"></param>
		/// <param name="interfaceIndexHash"></param>
		/// <param name="interfaceOfSgCompTable"></param>
		private void AssignDataToTable (int groupSeqID, Dictionary<int, int[]> clusterHash, Dictionary<string, int> interfaceIndexHash)
		{
			List<string> pdbList = new List<string> ();
			foreach (string pdbInterface in interfaceIndexHash.Keys)
			{
				if (! pdbList.Contains (pdbInterface.Substring (0, 4)))
				{
					pdbList.Add (pdbInterface.Substring (0, 4));
				}
			}
			string sgQueryString = string.Format ("Select Distinct PdbID, SpaceGroup, ASU From {0}" + 
				" Where PdbID IN ({1})", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], 
				ParseHelper.FormatSqlListString (pdbList.ToArray ()));
            DataTable sgTable = ProtCidSettings.protcidQuery.Query( sgQueryString);
			List<int> clusterIdList = new List<int> (clusterHash.Keys);
			clusterIdList.Sort ();
			foreach (int clusterId in clusterIdList)
			{
				foreach (int interfaceIndex in clusterHash[clusterId])
				{
					string pdbInterfaceString = pdbInterfaces[interfaceIndex];
					string[] pdbInterfaceFields = pdbInterfaceString.Split ('_');
					DataRow clusterRow = clusterTable.NewRow ();
					clusterRow["GroupSeqID"] = groupSeqID;
					clusterRow["ClusterID"] = clusterId;
					clusterRow["PdbID"] = pdbInterfaceFields[0];
					clusterRow["InterfaceID"] = pdbInterfaceFields[1];
					DataRow[] spaceGroupRows = sgTable.Select (string.Format ("PdbID = '{0}'", pdbInterfaceFields[0]));
					clusterRow["SpaceGroup"] = spaceGroupRows[0]["SpaceGroup"];
					clusterRow["ASU"] = spaceGroupRows[0]["ASU"];
					clusterTable.Rows.Add (clusterRow);
				}
			}
		}

		/// <summary>
		/// assign cluster data into table
		/// </summary>
		/// <param name="GroupSeqID"></param>
		/// <param name="clusterHash"></param>
		/// <param name="interfaceIndexHash"></param>
		/// <param name="interfaceOfSgCompTable"></param>
        protected void AssignDataToTable(int GroupSeqID, Dictionary<int, int[]> clusterHash, Dictionary<string, int> interfaceIndexHash, Dictionary<string, string> entryCfGroupHash,
            Dictionary<string, List<string>> cfRepHomoInterfaceHash)
        {
            Dictionary<string, string> entrySgAsuHash = GetEntrySgAsu(interfaceIndexHash, cfRepHomoInterfaceHash);
            List<int> clusterIdList = new List<int> (clusterHash.Keys);
            clusterIdList.Sort();
            string[] sgAsuFields = null;
            List<string> clusterHomoInterfaceList = new List<string> ();
            string[] pdbInterfaceFields = null;
            foreach (int clusterId in clusterIdList)
            {
                int[] interfaceList = clusterHash[clusterId];
                clusterHomoInterfaceList.Clear();
                foreach (int interfaceIndex in interfaceList)
                {
                    string pdbInterfaceString = pdbInterfaces[interfaceIndex];
                    pdbInterfaceFields = pdbInterfaceString.Split('_');
                    DataRow clusterRow = clusterTable.NewRow();
                    string[] cfGroupIdFields = entryCfGroupHash[pdbInterfaceFields[0]].Split('_');
                    clusterRow["SuperGroupSeqID"] = GroupSeqID;
                    clusterRow["GroupSeqId"] = cfGroupIdFields[0];
                    clusterRow["CfGroupID"] = cfGroupIdFields[1];
                    clusterRow["ClusterID"] = clusterId;
                    clusterRow["PdbID"] = pdbInterfaceFields[0];
                    clusterRow["InterfaceID"] = pdbInterfaceFields[1];
                    sgAsuFields = entrySgAsuHash[pdbInterfaceFields[0]].ToString().Split('_');
                    clusterRow["SpaceGroup"] = sgAsuFields[0];
                    clusterRow["ASU"] = sgAsuFields[1];
                    clusterTable.Rows.Add(clusterRow);
                    if (cfRepHomoInterfaceHash.ContainsKey(pdbInterfaceString))
                    {

                        foreach (string homoInterface in cfRepHomoInterfaceHash[pdbInterfaceString])
                        {
                            if (!clusterHomoInterfaceList.Contains(homoInterface))
                            {
                                clusterHomoInterfaceList.Add(homoInterface);
                            }
                        }
                    }
                }
                foreach (string homoInterface in clusterHomoInterfaceList)
                {
                    pdbInterfaceFields = homoInterface.Split('_');
                    DataRow homoClusterRow = clusterTable.NewRow();
                    string[] cfGroupIdFields = entryCfGroupHash[pdbInterfaceFields[0]].Split('_');
                    homoClusterRow["SuperGroupSeqID"] = GroupSeqID;
                    homoClusterRow["GroupSeqID"] = cfGroupIdFields[0];
                    homoClusterRow["CfGroupID"] = cfGroupIdFields[1];
                    homoClusterRow["ClusterID"] = clusterId;
                    homoClusterRow["PdbID"] = pdbInterfaceFields[0];
                    homoClusterRow["InterfaceID"] = pdbInterfaceFields[1];
                    sgAsuFields = entrySgAsuHash[pdbInterfaceFields[0]].ToString().Split('_');
                    homoClusterRow["SpaceGroup"] = sgAsuFields[0];
                    homoClusterRow["ASU"] = sgAsuFields[1];
                    clusterTable.Rows.Add(homoClusterRow);
                }
            }
        }

        /// <summary>
        /// assign cluster data into table
        /// </summary>
        /// <param name="GroupSeqID"></param>
        /// <param name="clusterHash"></param>
        /// <param name="interfaceIndexHash"></param>
        /// <param name="interfaceOfSgCompTable"></param>
        protected void AssignDataToTable(int GroupSeqID, Dictionary<int, int[]> clusterHash, Dictionary<string, int> interfaceIndexHash, Dictionary<string, int> entryCfGroupHash, Dictionary<string, List<string>> cfRepHomoInterfaceHash)
        {
            Dictionary<string, string> entrySgAsuHash = GetEntrySgAsu(interfaceIndexHash, cfRepHomoInterfaceHash);
            List<int> clusterIdList = new List<int>(clusterHash.Keys);
            clusterIdList.Sort();
            string[] sgAsuFields = null;
            List<string> clusterHomoInterfaceList = new List<string> ();
            string[] pdbInterfaceFields = null;
            foreach (int clusterId in clusterIdList)
            {
                int[] interfaceList = clusterHash[clusterId];
                clusterHomoInterfaceList.Clear();
                foreach (int interfaceIndex in interfaceList)
                {
                    string pdbInterfaceString = pdbInterfaces[interfaceIndex];
                    pdbInterfaceFields = pdbInterfaceString.Split('_');
                    DataRow clusterRow = clusterTable.NewRow();
                    clusterRow["GroupSeqID"] = GroupSeqID;
                    clusterRow["CfGroupID"] = entryCfGroupHash[pdbInterfaceFields[0]];
                    clusterRow["ClusterID"] = clusterId;
                    clusterRow["PdbID"] = pdbInterfaceFields[0];
                    clusterRow["InterfaceID"] = pdbInterfaceFields[1];
                    sgAsuFields = entrySgAsuHash[pdbInterfaceFields[0]].ToString().Split('_');
                    clusterRow["SpaceGroup"] = sgAsuFields[0];
                    clusterRow["ASU"] = sgAsuFields[1];
                    clusterTable.Rows.Add(clusterRow);
                    if (cfRepHomoInterfaceHash.ContainsKey(pdbInterfaceString))
                    {

                        foreach (string homoInterface in cfRepHomoInterfaceHash[pdbInterfaceString])
                        {
                            if (!clusterHomoInterfaceList.Contains(homoInterface))
                            {
                                clusterHomoInterfaceList.Add(homoInterface);
                            }
                        }
                    }
                }
                foreach (string homoInterface in clusterHomoInterfaceList)
                {
                    pdbInterfaceFields = homoInterface.Split('_');
                    DataRow homoClusterRow = clusterTable.NewRow();
                    homoClusterRow["GroupSeqID"] = GroupSeqID;
                    homoClusterRow["CfGroupID"] = entryCfGroupHash[pdbInterfaceFields[0]];
                    homoClusterRow["ClusterID"] = clusterId;
                    homoClusterRow["PdbID"] = pdbInterfaceFields[0];
                    homoClusterRow["InterfaceID"] = pdbInterfaceFields[1];
                    sgAsuFields = entrySgAsuHash[pdbInterfaceFields[0]].ToString().Split('_');
                    homoClusterRow["SpaceGroup"] = sgAsuFields[0];
                    homoClusterRow["ASU"] = sgAsuFields[1];
                    clusterTable.Rows.Add(homoClusterRow);
                }
            }
        }

		/// <summary>
		/// space group and ASU info for each entry
		/// </summary>
		/// <param name="interfaceIndexHash"></param>
		/// <param name="cfRepHomoInterfaceHash"></param>
		/// <returns></returns>
		protected Dictionary<string, string> GetEntrySgAsu (Dictionary<string, int> interfaceIndexHash, Dictionary<string, List<string>> cfRepHomoInterfaceHash)
		{
			List<string> pdbList = new List<string> ();
			string[] entryInterfaceFields = null;
			string sgQueryString  = "";
			foreach (string pdbInterface in interfaceIndexHash.Keys)
			{
				if (! pdbList.Contains (pdbInterface.Substring (0, 4)))
				{
					pdbList.Add (pdbInterface.Substring (0, 4));
				}
			}
			foreach (string cfRepInterface in cfRepHomoInterfaceHash.Keys)
			{
                foreach (string homoInterface in cfRepHomoInterfaceHash[cfRepInterface])
				{
					entryInterfaceFields = homoInterface.Split ('_');
					if (! pdbList.Contains (entryInterfaceFields[0]))
					{
						pdbList.Add (entryInterfaceFields[0]);
					}
				}
			}
			Dictionary<string, string> entrySgAsuHash = new Dictionary<string,string> ();
            string entrySgAsu = "";
			foreach (string pdbId in pdbList)
			{
				sgQueryString = string.Format ("Select Distinct PdbID, SpaceGroup, ASU From {0}" + 
					" Where PdbID = '{1}';", 
					GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo], pdbId);
                DataTable sgTable = ProtCidSettings.protcidQuery.Query( sgQueryString);
                if (sgTable.Rows.Count == 0)
                {
                    entrySgAsu = GetEntrySgAsu(pdbId);
                }
                else
                {
                    entrySgAsu =  sgTable.Rows[0]["SpaceGroup"].ToString ().Trim () + "_" + 
                        sgTable.Rows[0]["ASU"].ToString ().Trim ();
                }
				entrySgAsuHash.Add (pdbId, entrySgAsu);
			}
			return entrySgAsuHash;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        protected string GetEntrySgAsu(string pdbId)
        {
            string queryString = string.Format("Select * From PfamNonRedundantCfgroups Where PdbId = '{0}';", pdbId);
            DataTable cfgTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (cfgTable.Rows.Count > 0)
            {
                return cfgTable.Rows[0]["SpaceGroup"].ToString().TrimEnd() + "_" + cfgTable.Rows[0]["Asu"].ToString().TrimEnd();
            }
            return "";
        }
		/// <summary>
		/// insert data to database
		/// </summary>
		protected void InsertDataToDb ()
		{
            if (clusterTable.Rows.Count == 0)
            {
                return;
            }
            // clear those clusters with only one entry, added on July 21, 2010
            ClearOneEntryClusters(clusterTable);
            // rename clusterID so that it is in the order of #CFGs in the cluster
            UpdateClusterIDs(clusterTable);

            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, clusterTable);
			clusterTable.Clear ();
		}

        /// <summary>
        /// rename clusterID based on the #CFGs in the cluster
        /// </summary>
        /// <param name="clusterTable"></param>
        private void UpdateClusterIDs(DataTable clusterTable)
        {
            Dictionary<int, List<string>> clusterCfgsHash = new Dictionary<int,List<string>> ();
            Dictionary<int, List<string>> clusterEntryHash = new Dictionary<int,List<string>> ();
            Dictionary<int, List<string>> clusterInterfaceHash = new Dictionary<int,List<string>> ();
            int clusterId = 0;
            string groupCfgId = "";
            string pdbId = "";
            string entryInterface = "";
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString ());
                if (isDomain)
                {
                    groupCfgId = clusterRow["RelSeqID"].ToString() + "_" + clusterRow["RelCfGroupID"].ToString();
                }
                else
                {
                    groupCfgId = clusterRow["GroupSeqID"].ToString() + "_" + clusterRow["CfGroupID"].ToString();
                }
                pdbId = clusterRow["PdbID"].ToString ();
                if (isDomain)
                {
                    entryInterface = pdbId + clusterRow["DomainInterfaceID"].ToString();
                }
                else
                {
                    entryInterface = pdbId + clusterRow["InterfaceID"].ToString();
                }
                if (clusterCfgsHash.ContainsKey(clusterId))
                {
                    if (!clusterCfgsHash[clusterId].Contains(groupCfgId))
                    {
                        clusterCfgsHash[clusterId].Add(groupCfgId);
                    }
                }
                else
                {
                    List<string> cfgList = new List<string> ();
                    cfgList.Add(groupCfgId);
                    clusterCfgsHash.Add(clusterId, cfgList);
                }
                if (clusterEntryHash.ContainsKey(clusterId))
                {
                    if (!clusterEntryHash[clusterId].Contains(pdbId))
                    {
                        clusterEntryHash[clusterId].Add(pdbId);
                    }
                }
                else
                {
                    List<string> entryList = new List<string> ();
                    entryList.Add(pdbId);
                    clusterEntryHash.Add(clusterId, entryList);
                }
                if (clusterInterfaceHash.ContainsKey(clusterId))
                {
                    clusterInterfaceHash[clusterId].Add(entryInterface);
                }
                else
                {
                    List<string> interfaceList = new List<string> ();
                    interfaceList.Add(entryInterface);
                    clusterInterfaceHash.Add(clusterId, interfaceList);
                }
            }
            // bubble sort cluster ID by the #CFGs and #Entries in each cluster
            List<int> clusterList = new List<int> (clusterCfgsHash.Keys);
            clusterList.Sort ();
            // store the original cluster rows
            Dictionary<int, DataRow[]> origClusterRowsHash = new Dictionary<int,DataRow[]> ();
            foreach (int lsClusterId in clusterList)
            {
                DataRow[] clusterRows = clusterTable.Select(string.Format ("ClusterID = '{0}'", lsClusterId));
                origClusterRowsHash.Add(lsClusterId, clusterRows);
            }
            int numOfCfg1 = 0;
            int numOfCfg2 = 0;
            int numOfEntries1 = 0;
            int numOfEntries2 = 0;
            int numOfInterfaces1 = 0;
            int numOfInterfaces2 = 0;
            for (int i = 0; i < clusterList.Count; i++)
            {
                for (int j = i + 1; j < clusterList.Count; j++)
                {
                    numOfCfg1 = clusterCfgsHash[clusterList[i]].Count;
                    numOfCfg2 = clusterCfgsHash[clusterList[j]].Count;
                    if (numOfCfg1 < numOfCfg2)
                    {
                        int temp = (int)clusterList[i];
                        clusterList[i] = clusterList[j];
                        clusterList[j] = temp;
                    }
                    else if (numOfCfg1 == numOfCfg2)
                    {
                        numOfEntries1 = clusterEntryHash[clusterList[i]].Count;
                        numOfEntries2 = clusterEntryHash[clusterList[j]].Count;
                        if (numOfEntries1 < numOfEntries2)
                        {
                            int temp = (int)clusterList[i];
                            clusterList[i] = clusterList[j];
                            clusterList[j] = temp;
                        }
                        else if (numOfEntries1 == numOfEntries2)
                        {
                            numOfInterfaces1 = clusterInterfaceHash[clusterList[i]].Count;
                            numOfInterfaces2 = clusterInterfaceHash[clusterList[j]].Count;
                            if (numOfInterfaces1 < numOfInterfaces2)
                            {
                                int temp = (int)clusterList[i];
                                clusterList[i] = clusterList[j];
                                clusterList[j] = temp;
                            }
                        }
                    }
                }
            }
            int newClusterId = 0;
            for (int i = 0; i < clusterList.Count; i++)
            {
                newClusterId++;

                if ((int)clusterList[i] == newClusterId)
                {
                    continue;
                }
                DataRow[] origClusterRows = (DataRow[])origClusterRowsHash[clusterList[i]];
                foreach (DataRow origClusterRow in origClusterRows)
                {
                    origClusterRow["ClusterID"] = newClusterId;
                }
            }
            clusterTable.AcceptChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterTable"></param>
        private void ClearOneEntryClusters(DataTable clusterTable)
        {
            Dictionary<int, List<string>> clusterEntryHash = new Dictionary<int,List<string>> ();
            int clusterId = -1;
            string pdbId = "";
            foreach (DataRow dataRow in clusterTable.Rows)
            {
                clusterId = Convert.ToInt32 (dataRow["ClusterID"].ToString());
                pdbId = dataRow["PdbID"].ToString ();
                if (clusterEntryHash.ContainsKey(clusterId))
                {
                    if (!clusterEntryHash[clusterId].Contains(pdbId))
                    {
                        clusterEntryHash[clusterId].Add(pdbId);
                    }
                }
                else
                {
                    List<string> entryList = new List<string> ();
                    entryList.Add(pdbId);
                    clusterEntryHash.Add(clusterId, entryList);
                }
            }
            // remove those clusters with only one entry
            foreach (int lsClusterId in clusterEntryHash.Keys)
            {
                if (clusterEntryHash[lsClusterId].Count == 1)
                {
                    DataRow[] clusterRows = clusterTable.Select(string.Format ("ClusterID = '{0}'", lsClusterId));
                    foreach (DataRow clusterRow in clusterRows)
                    {
                        clusterTable.Rows.Remove(clusterRow);
                    }
                }
            }
            clusterTable.AcceptChanges();
        }
		#endregion

		#region initialize table
		private void InitializeTable ()
		{
			string[] clusterCols = {"GroupSeqID", "ClusterID", "CfGroupID", "SpaceGroup", "ASU", "PdbID", "InterfaceID"};
			clusterTable = new DataTable (GroupDbTableNames.dbTableNames[GroupDbTableNames.InterfaceClusters]);
			foreach (string clusterCol in clusterCols)
			{
				clusterTable.Columns.Add (new DataColumn (clusterCol));
			}
		}

        private void InitializeDbTable()
        {
            DbCreator dbCreate = new DbCreator();
            string createTableString = string.Format("CREATE Table {0} (" +
                " GroupSeqID INTEGER NOT NULL, ClusterID INTEGER NOT NULL, " +
                " CFGroupID INTEGER NOT NULL, " +
                " SpaceGroup VARCHAR(50) NOT NULL, ASU BLOB Sub_Type TEXT NOT NULL,  " +
                " PDBID CHAR(4) NOT NULL, InterfaceID INTEGER NOT NULL);",
                GroupDbTableNames.dbTableNames[GroupDbTableNames.InterfaceClusters]);

            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString,
                GroupDbTableNames.dbTableNames[GroupDbTableNames.InterfaceClusters], true);

            string indexString = string.Format("Create INDEX {0}_IdxPdbID ON {0} (GroupSeqID, PdbID, InterfaceID);",
                GroupDbTableNames.dbTableNames[GroupDbTableNames.InterfaceClusters]);
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString,
                GroupDbTableNames.dbTableNames[GroupDbTableNames.InterfaceClusters]);
        }

		/// <summary>
		/// delete obsolete  data from db
		/// </summary>
		/// <param name="GroupSeqID"></param>
		private void DeleteObsDataInDb (int GroupSeqID)
		{
			string deleteString = string.Format ("Delete FROM {0} Where GroupSeqID = {1};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.InterfaceClusters], GroupSeqID);
            ProtCidSettings.protcidQuery.Query( deleteString);
		}
		#endregion

        #region user group
        /// <summary>
        /// update interface clusters
        /// </summary>
        /// <param name="groupUpdateEntryHash"></param>
        public void UpdateUserGroupInterfaceClusters(int updateGroup, string[] entryChains)
        {
            GroupDbTableNames.SetGroupDbTableNames("pfam");
            InitializeTable();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Clustering interfaces";
            ProtCidSettings.progressInfo.currentFileName = updateGroup.ToString();

            DeleteObsDataInDb(updateGroup);
            try
            {
                ClusterGroupInterfaces(updateGroup, entryChains);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                ProtCidSettings.logWriter.WriteLine(ex.Message);
            }
            InsertDataToDb();
        }

        /// <summary>
        /// cluster interface for this group
        /// </summary>
        /// <param name="GroupSeqID"></param>
        /// <param name="interfaceCompTable"></param>
        /// <param name="interfaceOfSgCompTable"></param>
        private void ClusterGroupInterfaces(int groupSeqId, string[] entryChains)
        {
            try
            {
                Dictionary<string, int> entryCfGroupHash = GetNonReduntCfGroups(groupSeqId);
                Dictionary<string, int[]> cfRepInterfaceHash = new Dictionary<string,int[]> ();
                Dictionary<string, List<string>> cfRepHomoInterfaceHash = new Dictionary<string,List<string>> ();
                Dictionary<string, string> cfRepInterfaceFamilyArchHash = new Dictionary<string,string> ();
                Dictionary<string, List<string>> entryChainsHash = new Dictionary<string,List<string>> ();
                string entry = "";
                string authChain = "";
                foreach (string entryChain in entryChains)
                {
                    entry = entryChain.Substring(0, 4);
                    authChain = entryChain.Substring(4, entryChain.Length - 4);
                    if (entryChainsHash.ContainsKey(entry))
                    {
                        entryChainsHash[entry].Add(authChain);
                    }
                    else
                    {
                        List<string> chainList = new List<string> ();
                        chainList.Add(authChain);
                        entryChainsHash.Add(entry, chainList);
                    }
                }
                List<string> entryList = new List<string> (entryChainsHash.Keys);
                GetCfRepEntryAndInterfaces(groupSeqId, entryList, entryCfGroupHash,
                    ref cfRepInterfaceHash, ref cfRepHomoInterfaceHash, ref cfRepInterfaceFamilyArchHash);
                RemoveNonChainInterfaces(entryChainsHash, cfRepInterfaceHash, cfRepHomoInterfaceHash);

                if (cfRepInterfaceHash.Count == 0)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("No rep interfaces in group " + groupSeqId.ToString());
                    ProtCidSettings.logWriter.WriteLine("No rep interfaces in group " + groupSeqId.ToString());
                    return;
                }
                DataTable groupInterfaceCompTable = GetEntriesInterfaceComp(cfRepInterfaceHash);
                DataTable sameEntryInterfaceCompTable = GetEntryInterfaceComp(cfRepInterfaceHash);
                Dictionary<string, int> interfaceIndexHash = GetInterfacesIndexes(groupInterfaceCompTable);
                if (interfaceIndexHash.Count == 0)
                {
                    interfaceIndexHash = GetInterfaceIndexes(cfRepInterfaceHash);
                }
                GetPdbInterfacesFromIndex(interfaceIndexHash);

                bool canClustered = true;
                double[,] distMatrix = CreateDistMatrix(sameEntryInterfaceCompTable, groupInterfaceCompTable,
                    interfaceIndexHash, out canClustered);

                Dictionary<int, int[]> clusterHash = ClusterThisGroupInterfaces(distMatrix, interfaceIndexHash, cfRepInterfaceFamilyArchHash);
                if (clusterHash.Count > 0)
                {
                    AssignDataToTable(groupSeqId, clusterHash, interfaceIndexHash, entryCfGroupHash, cfRepHomoInterfaceHash);
                }
            }
            catch (Exception ex)
            {
                //      ProtCidSettings.progressInfo.progStrQueue.Enqueue("Clustering group " + groupSeqId + " error: " + ex.Message);
                //      ProtCidSettings.logWriter.WriteLine("Clustering group " + groupSeqId + " error: " + ex.Message);
                throw new Exception("Cluster group " + groupSeqId + " error: " + ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryChains"></param>
        /// <param name="entryInterfaceHash"></param>
        private void RemoveNonChainInterfaces(Dictionary<string, List<string>> entryChainsHash, Dictionary<string, int[]> cfRepInterfaceHash, Dictionary<string, List<string>> cfRepHomoInterfaceHash)
        {
            int interfaceId = -1;
            foreach (string entry in cfRepInterfaceHash.Keys)
            {
                int[] interfaceIds = (int[])cfRepInterfaceHash[entry];
                string[] authChains = entryChainsHash[entry].ToArray(); 
                int[] chainInterfaceIds = GetInterfacesWithSpecificChains(entry, authChains, interfaceIds);
                cfRepInterfaceHash[entry] = chainInterfaceIds;
                foreach (string repEntity in cfRepHomoInterfaceHash.Keys)
                {
                    if (repEntity.Substring(0, 4) != entry)
                    {
                        continue;
                    }
                    string[] fields = repEntity.Split('_');
                    interfaceId = Convert.ToInt32(fields[1]);
                    if (Array.IndexOf(chainInterfaceIds, interfaceId) < 0)
                    {
                        cfRepHomoInterfaceHash.Remove(repEntity);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chains"></param>
        /// <param name="interfaceIds"></param>
        /// <returns></returns>
        private int[] GetInterfacesWithSpecificChains(string pdbId, string[] chains, int[] interfaceIds)
        {
            string queryString = string.Format("Select * From CrystEntryInterfaces " + 
                " Where PdbID = '{0}' AND InterfaceID IN ({1});", pdbId, ParseHelper.FormatSqlListString (interfaceIds));
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string authChain1 = "";
            string authChain2 = "";
            List<int> chainInterfaceList = new List<int> ();
            foreach (DataRow interfaceRow in interfaceTable.Rows)
            {
                authChain1 = interfaceRow["AuthChain1"].ToString().TrimEnd();
                authChain2 = interfaceRow["AuthChain2"].ToString().TrimEnd();
                if (Array.IndexOf(chains, authChain1) > -1 &&
                    Array.IndexOf(chains, authChain2) > -1)
                {
                    chainInterfaceList.Add(Convert.ToInt32 (interfaceRow["InterfaceID"].ToString ()));
                }
            }
            return chainInterfaceList.ToArray ();
        }
        #endregion

        #region pfamarch for interfaces
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="cfRepInterfaces"></param>
        /// <param name="cfRepInterfaceFamilyArchHash"></param>
        public void SetEntryInterfacePfamArchHash(string pdbId, int[] entryInterfaces, ref Dictionary<string, string> entryInterfacePfamArchHash)
        {
            //     Hashtable entityFamilyArchHash = GetEntityFamilyArchString(pdbId);
            Dictionary<int, string> entityPfamArchHash = pfamArch.GetEntryEntityGroupPfamArchHash (pdbId);
            string queryString = "";
            if (entryInterfaces == null)
            {
                queryString = string.Format("Select InterfaceID, EntityID1, EntityID2 From CrystEntryInterfaces " +
                " Where PdbID = '{0}';", pdbId);
            }
            else
            {
                if (entryInterfaces.Length == 0)
                {
                    return;
                }
                queryString = string.Format("Select InterfaceID, EntityID1, EntityID2 From CrystEntryInterfaces " +
                " Where PdbID = '{0}';", pdbId);
            }
            DataTable interfaceInfoTable = ProtCidSettings.protcidQuery.Query( queryString);
            int entityId1 = 0;
            int entityId2 = 0;
            string entityFamilyArch1 = "";
            string entityFamilyArch2 = "";
            int interfaceId = -1;
            foreach (DataRow interfaceRow in interfaceInfoTable.Rows)
            {
                interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                if (entryInterfaces != null && Array.IndexOf(entryInterfaces, interfaceId) < 0)
                {
                    continue;
                }
                entityId1 = Convert.ToInt32(interfaceRow["EntityID1"].ToString());
                entityId2 = Convert.ToInt32(interfaceRow["EntityID2"].ToString());
                entityFamilyArch1 = (string)entityPfamArchHash[entityId1];
                entityFamilyArch2 = (string)entityPfamArchHash[entityId2];
                if (string.Compare(entityFamilyArch1, entityFamilyArch2) > 0)
                {
                    string temp = entityFamilyArch1;
                    entityFamilyArch1 = entityFamilyArch2;
                    entityFamilyArch2 = temp;
                }
                entryInterfacePfamArchHash.Add(pdbId + "_" + interfaceRow["InterfaceID"].ToString(),
                    entityFamilyArch1 + ";" + entityFamilyArch2);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entryInterfaces"></param>
        /// <param name="chainPfamArchRel"></param>
        /// <param name="entryInterfacePfamArchHash"></param>
        public void SetEntryInterfacePfamArchHash(string pdbId, int[] entryInterfaces, string[] chainPfamArchRel, ref Dictionary<string, string> entryInterfacePfamArchHash)
        {
            //     Hashtable entityFamilyArchHash = GetEntityFamilyArchString(pdbId);
            Dictionary<int, string> entityPfamArchHash = pfamArch.GetEntryEntityGroupPfamArchHash(pdbId);
            string queryString = "";
            if (entryInterfaces == null)
            {
                queryString = string.Format("Select InterfaceID, EntityID1, EntityID2 From CrystEntryInterfaces " +
                " Where PdbID = '{0}';", pdbId);
            }
            else
            {
                if (entryInterfaces.Length == 0)
                {
                    return;
                }
                queryString = string.Format("Select InterfaceID, EntityID1, EntityID2 From CrystEntryInterfaces " +
                " Where PdbID = '{0}';", pdbId);
            }
            DataTable interfaceInfoTable = ProtCidSettings.protcidQuery.Query( queryString);
            int interfaceId = -1;
            string interfacePfamArch = "";
            // chainPfamArchRel includes two chain-pfam-arch, for user-defined ones, may be one
            if (chainPfamArchRel.Length == 1)
            {
                interfacePfamArch = chainPfamArchRel[0] + ";" + chainPfamArchRel[0];
            }
            else if (chainPfamArchRel.Length == 2)
            {
                // in alphabet order
                if (string.Compare(chainPfamArchRel[0], chainPfamArchRel[1]) > 0)
                {
                    string temp = chainPfamArchRel[0];
                    chainPfamArchRel[0] = chainPfamArchRel[1];
                    chainPfamArchRel[1] = temp;
                }
                
                interfacePfamArch = chainPfamArchRel[0] + ";" + chainPfamArchRel[1];
            }
            int entityId1 = 0;
            int entityId2 = 0;
            string entityPfamArch1 = "";
            string entityPfamArch2 = "";
            foreach (DataRow interfaceRow in interfaceInfoTable.Rows)
            {
                interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                if (entryInterfaces != null && Array.IndexOf(entryInterfaces, interfaceId) < 0)
                {
                    continue;
                }
                if (entryInterfaces == null)
                {
                    entityId1 = Convert.ToInt32(interfaceRow["EntityID1"].ToString());
                    entityId2 = Convert.ToInt32(interfaceRow["EntityID2"].ToString());
                    if (entityPfamArchHash.ContainsKey(entityId1) && entityPfamArchHash.ContainsKey(entityId2))
                    {
                        entityPfamArch1 = entityPfamArchHash[entityId1];
                        entityPfamArch2 = entityPfamArchHash[entityId2];

                        if (groupRelType == "contain")
                        {
                            if (IsInterfacePfamArchContainGroupChainRel(entityPfamArch1, entityPfamArch2, chainPfamArchRel))
                            {
                                entryInterfacePfamArchHash.Add(pdbId + "_" + interfaceRow["InterfaceID"].ToString(),
                                                interfacePfamArch);
                            }
                        }
                        else
                        {
                            if (IsInterfacePfamArchSameAsGroupChainRel(entityPfamArch1, entityPfamArch2, chainPfamArchRel))
                            {
                                entryInterfacePfamArchHash.Add(pdbId + "_" + interfaceRow["InterfaceID"].ToString(),
                                                interfacePfamArch);
                            }
                        }
                    }
                }
                else
                {
                    entryInterfacePfamArchHash.Add(pdbId + "_" + interfaceRow["InterfaceID"].ToString(),
                        interfacePfamArch);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceEntityPfamArch1"></param>
        /// <param name="interfaceEntityPfamArch2"></param>
        /// <param name="groupChainRel"></param>
        /// <returns></returns>
        private bool IsInterfacePfamArchContainGroupChainRel(string interfaceEntityPfamArch1,
            string interfaceEntityPfamArch2, string[] groupChainRel)
        {
            if (groupChainRel.Length == 1)
            {
                if (interfaceEntityPfamArch1.IndexOf(groupChainRel[0]) > -1 &&
                    interfaceEntityPfamArch2.IndexOf(groupChainRel[0]) > -1)
                {
                    return true;
                }
            }
            else if (groupChainRel.Length == 2)
            {
                if ((interfaceEntityPfamArch1.IndexOf(groupChainRel[0]) > -1 &&
                    interfaceEntityPfamArch2.IndexOf(groupChainRel[1]) > -1) ||
                    (interfaceEntityPfamArch1.IndexOf(groupChainRel[1]) > -1 &&
                    interfaceEntityPfamArch2.IndexOf(groupChainRel[0]) > -1))
                {
                    return true;
                }
            }
            return false;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceEntityPfamArch1"></param>
        /// <param name="interfaceEntityPfamArch2"></param>
        /// <param name="groupChainRel"></param>
        /// <returns></returns>
        private bool IsInterfacePfamArchSameAsGroupChainRel(string interfaceEntityPfamArch1,
            string interfaceEntityPfamArch2, string[] groupChainRel)
        {
            if (groupChainRel.Length == 1)
            {
                if (interfaceEntityPfamArch1 == groupChainRel[0] &&
                    interfaceEntityPfamArch2 == groupChainRel[0])
                {
                    return true;
                }
            }
            else if (groupChainRel.Length == 2)
            {
                if ((interfaceEntityPfamArch1 == groupChainRel[0] &&
                    interfaceEntityPfamArch2 == groupChainRel[1]) ||
                    (interfaceEntityPfamArch1 == groupChainRel[1] &&
                    interfaceEntityPfamArch2 == groupChainRel[0]))
                {
                    return true;
                }
            }
            return false;
        }
        #endregion
    }
}
