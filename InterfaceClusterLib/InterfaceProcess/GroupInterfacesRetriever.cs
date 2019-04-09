using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.ProtInterfaces;
using CrystalInterfaceLib.StructureComp;
using CrystalInterfaceLib.StructureComp.HomoEntryComp;
using CrystalInterfaceLib.Settings;
using InterfaceClusterLib.DataTables;
using DbLib;
using AuxFuncLib;
using BuCompLib.BuInterfaces;


namespace InterfaceClusterLib.InterfaceProcess
{
	/// <summary>
	/// Summary description for GroupInterfacesDB.
	/// </summary>
	public class GroupInterfacesRetriever : CrystInterfaceRetriever
	{
		#region member variables	
		private InterfaceRetriever interfaceReader = null;
		private SupInterfacesComp interfaceComp = new SupInterfacesComp ();
        private InterfaceCompDbQuery interfaceCompDbQuery = new InterfaceCompDbQuery();
		#endregion
#if DEBUG
		private StreamWriter fileWriter = new StreamWriter ("mononer.txt", false);
#endif

		public GroupInterfacesRetriever ()
		{
		}

		#region interfaces to compute whole groups
		/// <summary>
		/// find common interfaces in a homologous group
		/// </summary>
		/// <param name="crystFileHash">key: groupId, value: a list of pdb entries</param>
		public void DetectGroupInterfacesCompDb (Dictionary<int, Dictionary<string, List<string>>> crystFileHash)
		{
            // [1, 10], [11, 100], [101, 300], [301, 1000] 
            // for those big group, may need change code to avoid outofmemory problem
            int minNumOfEntries = 1001;
            int maxNumOfEntries = 10000;
            int numOfEntriesCutoff = 2000;
#if DEBUG
            ProtCidSettings.logWriter.WriteLine(DateTime.Today.ToShortTimeString());
            ProtCidSettings.logWriter.WriteLine("GroupSeqID between " + minNumOfEntries.ToString () + "-" +
                maxNumOfEntries.ToString ());
#endif

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("GroupSeqID between " + minNumOfEntries.ToString () + "-" +
                maxNumOfEntries.ToString ());
			try
			{			
	//			int[] groupList = InitializeSgInterfaceThread (crystFileHash);
                int[] groupList = InitializeSgInterfaceThread(crystFileHash, minNumOfEntries, maxNumOfEntries);
          
				Dictionary<string, List<string>> crystSgEntryHash = null; // pdb entries for a space group in a group
                foreach (int groupId in groupList)
                {                    
                    crystSgEntryHash = null;
                    // clear all data tables
                    CrystInterfaceTables.ClearTables();
                    if (crystFileHash.ContainsKey(groupId))
                    {
                        crystSgEntryHash = crystFileHash[groupId];
                    }
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("\r\nGroup ID: " + groupId.ToString());
#if DEBUG
                    ProtCidSettings.logWriter.WriteLine("Group ID: " + groupId.ToString());
#endif
                    // the group is too big to fit into memory        
                    if (minNumOfEntries >= numOfEntriesCutoff)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(groupId.ToString () + " a big group");
                        ProtCidSettings.logWriter.WriteLine(groupId.ToString () + " a big group");

                        int numOfEntriesGroup = GetNumOfEntriesInGroup(crystSgEntryHash);

                        ProtCidSettings.progressInfo.totalOperationNum = numOfEntriesGroup * numOfEntriesGroup;
                        ProtCidSettings.progressInfo.totalStepNum = numOfEntriesGroup * numOfEntriesGroup;
                        
                        DetectBigGroupCommonInterfaces(groupId, crystSgEntryHash);
                    }
                    else
                    {
                        DetectGroupCommonInterfaces(groupId, crystSgEntryHash);
                    }
#if DEBUG
                    ProtCidSettings.logWriter.Flush();
#endif
                }			
			}
			catch (Exception ex)
			{
				ProtCidSettings.progressInfo.progStrQueue.Enqueue (ex.Message);
#if DEBUG
                ProtCidSettings.logWriter.WriteLine(ex.Message);
#endif
			}
			finally
			{
				try
				{
					Directory.Delete (ProtCidSettings.tempDir, true);
				}
				catch {}
#if DEBUG
				fileWriter.Close ();
                ProtCidSettings.logWriter.WriteLine("Done!");
				ProtCidSettings.logWriter.Close ();
#endif
			}
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private int GetGroupNumOfEntries(int groupId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamHomoSeqInfo Where GroupSeqID = {0};", groupId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            return entryTable.Rows.Count;
        }
		/// <summary>
		/// detect common interfaces in this family
		/// </summary>
		/// <param name="groupId"></param>
		/// <param name="crystSgFileHash"></param>
        private void DetectGroupCommonInterfaces(int groupId, Dictionary<string, List<string>> crystSgEntryHash)
		{
			Dictionary<string, InterfaceChains[]> groupInterfaceChainsHash = null;
			try
			{
                GetGroupInterfacesCompDb(crystSgEntryHash, groupId, ref groupInterfaceChainsHash);
			}
			catch (Exception ex)
			{
				ProtCidSettings.progressInfo.progStrQueue.Enqueue 
					("Retrieving " + groupId + " Error: " + ex.Message);
#if DEBUG
				ProtCidSettings.logWriter.Write ("Retrieving " + groupId + " Error: " + ex.Message);
#endif
				return;
			}
			if (groupInterfaceChainsHash == null || groupInterfaceChainsHash.Count == 0)
			{
				ProtCidSettings.progressInfo.progStrQueue.Enqueue ("No interfaces exist in group " + groupId);
#if DEBUG
                ProtCidSettings.logWriter.WriteLine("No interfaces exist in group " + groupId);
#endif
				return;
			}
			ProtCidSettings.progressInfo.currentOperationLabel = "Comparing Interfaces";
			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Comparing interfaces in homologous groups ...");
			// compare interfaces between cryst forms
			try
			{
				CompareHomoSeqInterfacesCompDb (groupId, groupInterfaceChainsHash);
			}
			catch (Exception ex)
			{
				ProtCidSettings.progressInfo.progStrQueue.Enqueue 
					("Compareing interfaces  between space groups in Group " + groupId + " Error: " + ex.Message);
#if DEBUG
				ProtCidSettings.logWriter.WriteLine 
					("Compareing interfaces  between space groups in Group " + groupId + " Error: " + ex.Message);
#endif
				return;
			}
			// find the number of common interfaces for interfaces
			//  based on Q scores and identity-weighted Q scores
			try
			{
				ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Updating number of common interfaces. Please Wait...");
				FindGroupCommonInterfaces (groupId);
				ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Done.");
			}
			catch (Exception ex)
			{
				ProtCidSettings.progressInfo.progStrQueue.Enqueue (ex.Message);
#if DEBUG
                ProtCidSettings.logWriter.WriteLine("Updating number of common interfaces errors: " + ex.Message);
#endif
			}
		}    
		#endregion

        #region for big groups with entries >= 2000
        /// <summary>
        /// detect common interfaces in this family
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="crystSgFileHash"></param>
        private void DetectBigGroupCommonInterfaces(int groupId,  Dictionary<string, List<string>> crystSgEntryHash)
        {
            string[] sgAsuList = new string[crystSgEntryHash.Count];
            crystSgEntryHash.Keys.CopyTo(sgAsuList, 0);
            string repEntryI = "";
            string repEntryJ = "";
            InterfaceChains[] repInterfacesI = null;
            InterfaceChains[] repInterfacesJ = null;
            for (int i = 0; i < sgAsuList.Length; i++)
            {
                string[] sgEntriesI = crystSgEntryHash[sgAsuList[i]].ToArray(); 
                repEntryI = sgEntriesI[0];
                repInterfacesI = GetCrystFormRepInterafaces(groupId, sgAsuList[i], sgEntriesI);
                for (int j = i + 1; j < sgAsuList.Length; j++)
                {
                    ProtCidSettings.progressInfo.currentFileName = repEntryI + " " + repEntryJ;

                    string[] sgEntriesJ = crystSgEntryHash[sgAsuList[j]].ToArray(); 
                    repEntryJ = sgEntriesJ[0];
                    try
                    {
                        repInterfacesJ = GetCrystFormRepInterafaces(groupId, sgAsuList[j], sgEntriesJ);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue
                            ("Retrieving " + groupId + " Error: " + ex.Message);
                        ProtCidSettings.logWriter.Write("Retrieving " + groupId + " Error: " + ex.Message);
                    }
                    // compare interfaces between cryst forms
                    try
                    {
                        CompareTwoEntryInterfaces(groupId, repEntryI, repEntryJ, repInterfacesI, repInterfacesJ);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue
                            ("Compareing interfaces  between space groups in Group " + groupId + " Error: " + ex.Message);
#if DEBUG
                        ProtCidSettings.logWriter.WriteLine
                            ("Compareing interfaces  between space groups in Group " + groupId + " Error: " + ex.Message);
#endif
                    }
                }
            }

            try
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating number of common interfaces. Please Wait...");
                FindGroupCommonInterfaces(groupId);
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done.");
                ProtCidSettings.logWriter.WriteLine(groupId.ToString () + " done!");
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
#if DEBUG
                ProtCidSettings.logWriter.WriteLine(groupId.ToString () + " Updating number of common interfaces errors: " + ex.Message);
                ProtCidSettings.logWriter.Flush();
#endif
            }
        }
        #endregion

        #region functions to update interface tables
        /// <summary>
		/// find common interfaces in a homologous group
		/// </summary>
		/// <param name="crystFileHash">key: groupId, value: a list of pdb entries</param>
		public void UpdateGroupInterfacesCompDb (Dictionary<int, Dictionary<string, List<string>>> crystFileHash, Dictionary<int, string[]> updateGroupHash)
		{
#if DEBUG
            ProtCidSettings.logWriter.WriteLine(DateTime.Today.ToShortTimeString ());
#endif
	
			try
			{
		//		int[] groupList = InitializeSgInterfaceThread (crystFileHash, updateGroupHash);
                int[] groupList = InitializeSgInterfaceThread(crystFileHash);

				foreach (int groupId in groupList)
                {     
          /*      int groupId = 0;
                // reversed order, to speed up the update
                for (int i = groupList.Length - 1; i >= 0; i  -- )
                {
                    groupId = groupList[i]; */
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("\r\nGroup ID: " + groupId.ToString());
#if DEBUG
                    ProtCidSettings.logWriter.WriteLine("Group ID: " + groupId.ToString());
#endif
                    Dictionary<string, List<string>> crystSgFileHash = crystFileHash[groupId];
                    // clear all data tables
                    CrystInterfaceTables.ClearTables();
                   
                    string[] updateEntries = updateGroupHash[groupId];

                    // delete all data related with those update entries
                    PrepareCommonInterfaceUpdate(groupId, updateEntries);
                    // calculate common interfaces in a group
                    DetectGroupCommonInterfaces(groupId, crystSgFileHash);

#if DEBUG
                    ProtCidSettings.logWriter.Flush();
#endif
                }			
			}
			catch (Exception ex)
			{
				ProtCidSettings.progressInfo.progStrQueue.Enqueue (ex.Message);
#if DEBUG
                ProtCidSettings.logWriter.WriteLine(ex.Message);
#endif
			}
			finally
			{
				try
				{
					Directory.Delete (ProtCidSettings.tempDir, true);
				}
				catch {}
#if DEBUG
				fileWriter.Close ();
		//		AppSettings.BuCompBuilder.logWriter.Close ();
#endif
			}
		}
		/// <summary>
		/// prepare updating interface tables 
		/// delete existing data
		/// </summary>
		/// <param name="groupId"></param>
		/// <param name="crystSgFileHash"></param>
		/// <param name="updateEntryList"></param>
		/// <returns></returns>
	/*	private bool PrepareInterfaceUpdate (int groupId, Hashtable crystSgFileHash, ArrayList updateEntryList)
		{
			bool groupDeleted = false;
			if (AreGroupRepEntryUpdated (crystSgFileHash, updateEntryList))
			{
				DeleteObsData (groupId);	
				groupDeleted = true;
			}
			string[] updateEntries = new string [updateEntryList.Count];
			updateEntryList.CopyTo (updateEntries);			
			RemoveObsDataFromInterfaceTables (updateEntries);
			return groupDeleted;
		}*/
        private void PrepareCommonInterfaceUpdate(int groupId, string[] updateEntries)
        {
            DeleteEntryGroupObsData (groupId);
   //         RemoveObsDataFromInterfaceTables(updateEntries);
        }

		/// <summary>
		/// check if representative entries need to be updated
		/// </summary>
		/// <param name="crystSgFileHash"></param>
		/// <param name="updateEntryList"></param>
		/// <returns></returns>
		private bool AreGroupRepEntryUpdated (Dictionary<string, List<string>> crystSgFileHash, List<string> updateEntryList)
		{
			foreach (string repEntry in crystSgFileHash.Keys)
			{
                if (updateEntryList.Contains(crystSgFileHash[repEntry][0]))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// no representative entries  need to be udpated
		/// so just update those cryst form groups with updating homologous entries 
		/// </summary>
		/// <param name="groupId"></param>
		/// <param name="crystSgFileHash"></param>
		/// <param name="updateEntryList"></param>
		private void UpdateGroupCommonInterfaces (int groupId, Dictionary<string, List<string>> crystSgFileHash, List<string> updateEntryList)
		{
			foreach (string sg_asu in crystSgFileHash.Keys )
			{
                string[] updateCfEntries = GetUpdateCrystFormEntries(crystSgFileHash[sg_asu], updateEntryList);
				if (updateCfEntries.Length == 0)
				{
					continue;
				}
				// for those only homologous entries changed or added
				// compute interfaces and interface comparison
				// between representative entry and homologous entries 
				GetCrystFormGroupInterafaces (groupId, sg_asu, crystSgFileHash);
			}
		}

		/// <summary>
		/// the updating entries in the group
		/// </summary>
		/// <param name="crystFormEntryList"></param>
		/// <param name="groupUpdateEntryList"></param>
		/// <returns></returns>
		private string[] GetUpdateCrystFormEntries (List<string> crystFormEntryList, List<string> groupUpdateEntryList)
		{
			List<string> cfUpdateEntryList = new List<string> ();
			foreach (string cfEntry in crystFormEntryList)
			{
				if (groupUpdateEntryList.Contains (cfEntry))
				{
					cfUpdateEntryList.Add (cfEntry);
				}
			}
            return cfUpdateEntryList.ToArray ();
		}
		#endregion

		#region intialize progress info
		private int[] InitializeSgInterfaceThread (Dictionary<int, Dictionary<string, List<string>>> crystFileHash)
		{
			asuInterfacesNonCryst = new AsuInterfaces ();
			interfaceReader = new InterfaceRetriever ();

			CrystInterfaceTables.InitializeTables ();

			if (! Directory.Exists (ProtCidSettings.tempDir))
			{
				Directory.CreateDirectory (ProtCidSettings.tempDir);
			}
			int totalOpNum = 0;
			List<int> groupList = new List<int> ();
			foreach (int groupId in crystFileHash.Keys)
			{
                foreach (string sg_Asu in crystFileHash[groupId].Keys)
				{
                    totalOpNum += crystFileHash[groupId][sg_Asu].Count;
				}
				groupList.Add (groupId);
			}
			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
			ProtCidSettings.progressInfo.totalOperationNum = totalOpNum;
			ProtCidSettings.progressInfo.totalStepNum = totalOpNum;
			ProtCidSettings.progressInfo.currentOperationLabel = "Computing Interfaces";
			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Computing interfaces in homologous groups ...");
			groupList.Sort ();
            return groupList.ToArray ();
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crystFileHash"></param>
        /// <param name="minNumOfEntries"></param>
        /// <param name="maxNumOfEntries"></param>
        /// <returns></returns>
        private int[] InitializeSgInterfaceThread(Dictionary<int, Dictionary<string, List<string>>> crystFileHash,  int minNumOfEntries, int maxNumOfEntries)
        {
            asuInterfacesNonCryst = new AsuInterfaces();
            interfaceReader = new InterfaceRetriever();

            CrystInterfaceTables.InitializeTables();

            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            int totalOpNum = 0;
            int numOfGroupEntry = 0;
            List<int> groupList = new List<int> ();
            foreach (int groupId in crystFileHash.Keys)
            {
                Dictionary<string, List<string>> sgFileHash = crystFileHash[groupId];
                numOfGroupEntry = 0;
                foreach (string sg_Asu in sgFileHash.Keys)
                {
                    numOfGroupEntry += sgFileHash[sg_Asu].Count;
                }
                if (numOfGroupEntry <= maxNumOfEntries && numOfGroupEntry >= minNumOfEntries)
                {
                    totalOpNum += numOfGroupEntry;
                    groupList.Add(groupId);
                }
            }
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = totalOpNum;
            ProtCidSettings.progressInfo.totalStepNum = totalOpNum;
            ProtCidSettings.progressInfo.currentOperationLabel = "Computing Interfaces";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Computing interfaces in homologous groups ...");            

            return groupList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sgFileHash"></param>
        /// <returns></returns>
        private int GetNumOfEntriesInGroup(Dictionary<string, List<string>> sgFileHash)
        {
            int numOfGroupEntry = 0;
            foreach (string sg_Asu in sgFileHash.Keys)
            {
                numOfGroupEntry +=  sgFileHash[sg_Asu].Count;
            }
            return numOfGroupEntry;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crystFileHash"></param>
        /// <param name="updateGroupHash"></param>
        /// <returns></returns>
		private int[] InitializeSgInterfaceThread (Dictionary<int, Dictionary<string, List<string>>> crystFileHash, Dictionary<int, List<string>> updateGroupHash)
		{
			asuInterfacesNonCryst = new AsuInterfaces ();
			interfaceReader = new InterfaceRetriever ();

			CrystInterfaceTables.InitializeTables ();

			if (! Directory.Exists (ProtCidSettings.tempDir))
			{
				Directory.CreateDirectory (ProtCidSettings.tempDir);
			}
			int totalOpNum = 0;
			List<int> groupList = new List<int> ();
			foreach (int groupNum in crystFileHash.Keys)
			{
				int groupId = Convert.ToInt32 (groupNum.ToString ());
				Dictionary<string, List<string>> sgFileHash = crystFileHash[groupNum];
                if (AreGroupRepEntryUpdated(sgFileHash, updateGroupHash[groupNum]))
				{
					foreach (string sg_asu in sgFileHash.Keys)
					{
						totalOpNum += sgFileHash[sg_asu].Count;
					}
				}
				else
				{
					foreach (string sg_asu in sgFileHash.Keys)
					{
                        string[] updateEntries = GetUpdateCrystFormEntries(sgFileHash[sg_asu], updateGroupHash[groupNum]);
						if (updateEntries.Length == 0)
						{
							continue;
						}
						totalOpNum += sgFileHash[sg_asu].Count;
					}
				}
				groupList.Add (groupNum);
			}
			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
			ProtCidSettings.progressInfo.totalOperationNum = totalOpNum;
			ProtCidSettings.progressInfo.totalStepNum = totalOpNum;
			ProtCidSettings.progressInfo.currentOperationLabel = "Computing Interfaces";
			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Computing interfaces in homologous groups ...");
			int[] groups = new int [groupList.Count];
			groupList.Sort ();
			groupList.CopyTo (groups);
			return groups;
		}

		#endregion

		#region common interfaces in a crystal form group
		#region get interfaces for a homologous group
		/// <summary>
		///  get interfaces for each x-ray entry in a spacegroup + asu group
		/// </summary>
		/// <param name="sgFileListHash"></param>
		/// <param name="groupId"></param>
		/// <param name="groupInterfaceChainsHash"></param>
		private void GetGroupInterfacesCompDb (Dictionary<string, List<string>> sgEntryListHash, int groupId, ref Dictionary<string, InterfaceChains[]> groupInterfaceChainsHash)
		{
			groupInterfaceChainsHash = new Dictionary<string,InterfaceChains[]> ();

            foreach (string sg_asu in sgEntryListHash.Keys)
			{
                string[] sgEntries = sgEntryListHash[sg_asu].ToArray ();
                GetCrystFormGroupInterafaces(groupId, sg_asu, sgEntries, ref groupInterfaceChainsHash);
			}
		}

		/// <summary>
		/// the common interfaces in a crystal form
		/// data are inserted into db
		/// </summary>
		/// <param name="groupId"></param>
		/// <param name="sg_asu"></param>
		/// <param name="sgFileListHash"></param>
		/// <param name="groupInterfaceChainsHash"></param>
		private void GetCrystFormGroupInterafaces (int groupId, string sg_asu, string[] sgEntries, ref Dictionary<string, InterfaceChains[]> groupInterfaceChainsHash)
		{
            // for each sg_asu crystal form group, the first one is always the representative entry          
			InterfaceChains[] uniqueInterfaceChainList = null;
			try
			{
                string[] sgAsuFields = sg_asu.Split('_');
                string abbrevMethod = ParseHelper.GetNonXrayAbbrevMethod (sgAsuFields[0]);
			//	if (sg_asu.IndexOf ("NMR") > -1)
                if (Array.IndexOf(AppSettings.abbrevNonXrayCrystMethods, abbrevMethod) > -1)  // for non-XRAY structures
				{
                    uniqueInterfaceChainList = GetNmrGroupUniqueInterfaces(sgEntries, groupId, sg_asu);
				}
				else
				{
					// get interfaces exist in every entry in a space group
                    uniqueInterfaceChainList = GetSpaceGroupUniqueInterfaces(sgEntries, sg_asu, groupId);					
				}
				// insert entry interfaces into database, clear table to free memory
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.CrystEntryInterfaces]);
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp]);
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.EntryInterfaceComp]);				
                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.CrystEntryInterfaces].Clear ();
				CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].Clear ();
                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.EntryInterfaceComp].Clear();
			}
			catch (Exception ex)
			{
				ProtCidSettings.progressInfo.progStrQueue.Enqueue 
					("Retrieving interfaces of " + groupId + "_" + sg_asu  + " errors: " + ex.Message);
#if DEBUG
				ProtCidSettings.logWriter.WriteLine 
					("Retrieving interfaces of " + groupId + "_" + sg_asu  + " errors: " + ex.Message);
#endif
			}
			if (uniqueInterfaceChainList != null && uniqueInterfaceChainList.Length > 0)
			{
                // assign sg interfaces to tables,
                // and also interface residues or contacts
                AssignSgInterfacesToTable(uniqueInterfaceChainList, sgEntries[0], sg_asu, groupId);
				// insert data into database
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.SgInterfaces]);  
				CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.SgInterfaces].Clear ();

                groupInterfaceChainsHash.Add(sgEntries[0], uniqueInterfaceChainList);
			}
#if DEBUG
			else
			{
				ProtCidSettings.logWriter.WriteLine ("No space group interfaces exist in " + groupId + "_" + sg_asu);
			}
#endif
		}

        /// <summary>
        /// the common interfaces in a crystal form
        /// data are inserted into db
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="sg_asu"></param>
        /// <param name="sgFileListHash"></param>
        /// <param name="groupInterfaceChainsHash"></param>
        private InterfaceChains[] GetCrystFormRepInterafaces(int groupId, string sg_asu, string[] sgEntries)
        {
            // for each sg_asu crystal form group, the first one is always the representative entry          
            InterfaceChains[] uniqueInterfaceChainList = null;
            try
            {
                string[] sgAsuFields = sg_asu.Split('_');
                string abbrevMethod = ParseHelper.GetNonXrayAbbrevMethod(sgAsuFields[0]);
                //	if (sg_asu.IndexOf ("NMR") > -1)
                if (Array.IndexOf(AppSettings.abbrevNonXrayCrystMethods, abbrevMethod) > -1)  // for non-XRAY structures
                {
                    uniqueInterfaceChainList = GetNmrGroupUniqueInterfaces(sgEntries, groupId, sg_asu);
                }
                else
                {
                    // get interfaces exist in every entry in a space group
                    uniqueInterfaceChainList = GetSpaceGroupUniqueInterfaces(sgEntries, sg_asu, groupId);
                }
                // insert entry interfaces into database, clear table to free memory
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.CrystEntryInterfaces]);
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp]);
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.EntryInterfaceComp]);
                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.CrystEntryInterfaces].Clear();
                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].Clear();
                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.EntryInterfaceComp].Clear();
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue
                    ("Retrieving interfaces of " + groupId + "_" + sg_asu + " errors: " + ex.Message);
#if DEBUG
                ProtCidSettings.logWriter.WriteLine
                    ("Retrieving interfaces of " + groupId + "_" + sg_asu + " errors: " + ex.Message);
#endif
            }
            if (uniqueInterfaceChainList != null && uniqueInterfaceChainList.Length > 0)
            {
                // assign sg interfaces to tables,
                // and also interface residues or contacts
                AssignSgInterfacesToTable(uniqueInterfaceChainList, sgEntries[0].ToString(), sg_asu, groupId);
                // insert data into database
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.SgInterfaces]);
                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.SgInterfaces].Clear();               
            }
#if DEBUG
            else
            {
                ProtCidSettings.logWriter.WriteLine("No space group interfaces exist in " + groupId + "_" + sg_asu);
            }
#endif
            return uniqueInterfaceChainList;
        }

		/// <summary>
		/// for those representative entries are not updated
		/// 1. compute crystal interfaces of representative entry and homologous entries
		/// 2. compute the comparison between representative entry and updated entries
		/// 3. insert data into db
		/// The sginterface, sginterfaceresidues and sginterfacecontacts tables are not changed
		/// </summary>
		/// <param name="groupId"></param>
		/// <param name="sg_asu"></param>
		/// <param name="sgFileListHash"></param>
		private void GetCrystFormGroupInterafaces (int groupId, string sg_asu, Dictionary<string, List<string>> sgFileListHash)
		{
            string[] sgEntries = sgFileListHash[sg_asu].ToArray(); 
			InterfaceChains[] uniqueInterfaceChainList = null;
			try
			{
                string[] sgAsuFields = sg_asu.Split ('_');
                string abbrevMethod = ParseHelper.GetNonXrayAbbrevMethod (sgAsuFields[0]);
		//		if (sg_asu.IndexOf ("NMR") > -1 || sg_asu.IndexOf ("MICROSCOPY") > -1)
                if (Array.IndexOf(AppSettings.abbrevNonXrayCrystMethods, abbrevMethod) > -1) // for non-xray structure
				{
                    uniqueInterfaceChainList = GetNmrGroupUniqueInterfaces(sgEntries, groupId, sg_asu);
				}
				else
				{
					// get interfaces exist in every entry in a crystal form
                    uniqueInterfaceChainList = GetSpaceGroupUniqueInterfaces(sgEntries, sg_asu, groupId);					
				}
				// insert entry interfaces into database, clear table to free memory
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.CrystEntryInterfaces]);
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp]);
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.EntryInterfaceComp]);				               
                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.CrystEntryInterfaces].Clear ();
				CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].Clear ();
                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.EntryInterfaceComp].Clear();
			}
			catch (Exception ex)
			{
				ProtCidSettings.progressInfo.progStrQueue.Enqueue 
					("Retrieving interfaces of " + groupId + "_" + sg_asu  + " errors: " + ex.Message);
#if DEBUG
				ProtCidSettings.logWriter.WriteLine 
					("Retrieving interfaces of " + groupId + "_" + sg_asu  + " errors: " + ex.Message);
#endif
			}
		}
		#endregion

		#region common interfaces for a spacegroup + asu subgroup
		/// <summary>
		/// check the representative interfaces exist in the NMR group
		/// </summary>
		/// <param name="noCrystFileList"></param>
		/// <param name="groupId"></param>
		/// <returns></returns>
		public InterfaceChains[] GetNmrGroupUniqueInterfaces (string[] sgEntries, int groupId, string sg_asu)
		{
            Dictionary<string, InterfaceChains[]> sgEntryInterfacesHash = new Dictionary<string, InterfaceChains[]>();
            bool isNmrInDb = false;
			foreach (string pdbId in sgEntries)
			{
				ProtCidSettings.progressInfo.currentOperationNum ++;
				ProtCidSettings.progressInfo.currentStepNum ++;
				ProtCidSettings.progressInfo.currentFileName = pdbId;
			
				try
				{
                    InterfaceChains[] asuInterfaces = null;
                    isNmrInDb = AreNmrInterfacesInDb(pdbId);
                    if (isNmrInDb)
                    {
                        asuInterfaces = interfaceReader.GetInterfacesFromFiles(pdbId, "cryst");
                    }
                    if (asuInterfaces == null || asuInterfaces.Length == 0)
                    {
                        string noCrystFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
                        // unzip xml file
                        if (!File.Exists(noCrystFile))
                        {
                            ProtCidSettings.progressInfo.progStrQueue.Enqueue(string.Format("{0} file not exist. ", noCrystFile));
#if DEBUG
                            ProtCidSettings.logWriter.WriteLine(string.Format("{0} file not exist. ", noCrystFile));
#endif
                            continue;
                        }
                        asuInterfaces = asuInterfacesNonCryst.GetAsuInterfacesFromXml(noCrystFile);
                        AddEntityIDsToInterfaces(pdbId, ref asuInterfaces);
                        isNmrInDb = false;
                    }

					if (asuInterfaces != null && asuInterfaces.Length > 0)
					{
						sgEntryInterfacesHash.Add (pdbId, asuInterfaces);

                        if (!isNmrInDb)
						{
							AssignEntryInterfacesToTable (asuInterfaces, pdbId, sg_asu);
                            AssignEntryInterfaceCompToTable(pdbId, asuInterfaces);  // compare interfaces of this entry
						}
					}
				}
				catch (Exception ex)
				{
					ProtCidSettings.progressInfo.progStrQueue.Enqueue (ex.Message);
#if DEBUG
                    ProtCidSettings.logWriter.WriteLine("Retrieving " + pdbId + " NMR interfaces errors: " + ex.Message);
#endif
				}
			}
			if (sgEntryInterfacesHash.Count > 0)
			{
				// compare the interfaces between NMR entries for future analysis
				//	FindUniqueInterfaceInSpaceGroup (sgEntryInterfacesHash, groupId, noCrystPdbList[0].ToString ()/*, entryIdentityHash*/);
				// return the interfaces in the representative NMR entry
				if (sgEntryInterfacesHash.ContainsKey (sgEntries[0]))
				{
					return (InterfaceChains[]) sgEntryInterfacesHash[sgEntries[0]];
				}
			}
			return null;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
		private bool AreNmrInterfacesInDb (string pdbId)
		{
			string queryString = string.Format ("SELECT * FROM CrystEntryInterfaces WHERE PdbID = '{0}';", pdbId);
			DataTable interfaceTable = dbQuery.Query (ProtCidSettings.protcidDbConnection, queryString);
			if (interfaceTable.Rows.Count > 0)
			{
				return true;
			}
			return false;
		}
		/// <summary>
		/// find unique interfaces in homologous entries of a space group
		/// </summary>
		/// <param name="pdbList"></param>
		/// <param name="spaceGroup"></param>
		/// <returns></returns>
		private InterfaceChains[] GetSpaceGroupUniqueInterfaces (string[] sgEntries, string sg_asu, int groupId)
		{
			Dictionary<string, InterfaceChains[]> sgEntryInterfacesHash = new Dictionary<string,InterfaceChains[]> ();
			Dictionary<string, InterfacePairInfo[]> interfacePairHash = new Dictionary<string,InterfacePairInfo[]> ();
			InterfacePairInfo[] pairInfoList = null;
			string xmlFile = "";
            for (int i = 0; i < sgEntries.Length; i++)
            {
                string pdbId = sgEntries[i].ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                /* the first one is the representative entry, 
                 * to find the common interfaces in sg_asu group, 
                 * only calculate Q scores between representative entry and its homology entries
                 * */
                if (i > 0)
                {
                    pairInfoList = crystInterfaceComp.GetInterfaceCompPairsInfoFromDb(sgEntries[0].ToString(), sgEntries[i].ToString(), CrystInterfaceTables.DifEntryInterfaceComp);
                    if (pairInfoList != null)
                    {
                        interfacePairHash.Add(pdbId, pairInfoList);
                        continue;
                    }
                }

                InterfaceChains[] uniqueInterfaces = null;

                // read interfaces from interface files
                uniqueInterfaces = interfaceReader.GetInterfacesFromFiles(pdbId, "cryst");

                // no interface files
                if (uniqueInterfaces == null || uniqueInterfaces.Length == 0)
                {
                    xmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");

                    // unzip xml file
                    if (!File.Exists(xmlFile))
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(string.Format("{0} file not exist. ", xmlFile));
#if DEBUG
                        ProtCidSettings.logWriter.WriteLine(string.Format("{0} file not exist. ", xmlFile));
#endif
                        continue;
                    }


                    try
                    {
                        FindUniqueInterfaces(xmlFile, sg_asu, ref uniqueInterfaces);
                        if (uniqueInterfaces == null)
                        {
#if DEBUG
                            fileWriter.WriteLine(pdbId);
#endif
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ":" + ex.Message);
#if DEBUG
                        ProtCidSettings.logWriter.WriteLine("Computing " + pdbId + " unique interfaces errors: " + ex.Message);
#endif
                    }
                }
                sgEntryInterfacesHash.Add(pdbId, uniqueInterfaces);
            }
            if (sgEntryInterfacesHash.Count > 0 || interfacePairHash.Count > 0)
			{
                return FindUniqueInterfaceInSpaceGroup(groupId, sgEntries[0].ToString(), sgEntryInterfacesHash, interfacePairHash);
			}
			else
			{
				return null;
			}
		}
		/// <summary>
		/// find the list of relevant interfaces 
		/// which exist in every entry in space group
		/// </summary>
		/// <param name="sgInterfacesHash"></param>
		/// <returns></returns>
		private InterfaceChains[] FindUniqueInterfaceInSpaceGroup (int groupId, string repEntry, 
			Dictionary<string, InterfaceChains[]> sgEntryInterfacesHash, Dictionary<string, InterfacePairInfo[]> interfacePairHash)
		{
			// get alignment information from database	
			DataTable homoEntryAlignTable = homoEntryAlignInfo.GetHomoEntryAlignInfo (groupId, repEntry);

			InterfaceChains[] repInterfaces = (InterfaceChains[])sgEntryInterfacesHash[repEntry];
			List<InterfaceChains> sgComInterfaceList = new List<InterfaceChains> (repInterfaces);
			List<string> sgEntryList = new List<string> (sgEntryInterfacesHash.Keys);
			sgEntryList.Remove (repEntry);
			foreach (string pdbId in sgEntryList)
			{
				InterfaceChains[] thisEntryInterfaceList = (InterfaceChains[])sgEntryInterfacesHash[pdbId];
				DataTable homoEntryPairAlignTable = homoEntryAlignInfo.SelectAlignInfoTable (homoEntryAlignTable, repEntry, pdbId);
				// compare interfaces in two structures
				InterfacePairInfo[] interfacePairs = null;
				try
				{
					interfacePairs = interfaceComp.CompareSupStructures
						(repInterfaces, thisEntryInterfaceList, homoEntryPairAlignTable);
				}
				catch (Exception ex)
				{
					ProtCidSettings.progressInfo.progStrQueue.Enqueue 
						("Comparing " + repEntry + " and " + pdbId + " interfaces errors: " + ex.Message);
					continue;
				}
				// store the comparison between repsentative and homologous entries into table
				crystInterfaceComp.AssignInterfacePairToTable (interfacePairs, repEntry, pdbId, 
					CrystInterfaceTables.DifEntryInterfaceComp);
				GetCommonInterfacesInSgAsu (ref sgComInterfaceList, interfacePairs);				
			}
			foreach (string dbEntry in interfacePairHash.Keys)
			{
				InterfacePairInfo[] interfacePairs = (InterfacePairInfo[])interfacePairHash[dbEntry];
			/*	AssignInterfacePairToTable (interfacePairs, repEntry, dbEntry, groupId, 
					CrystInterfaceTables.CrystEntryInterfaceComp);*/
				GetCommonInterfacesInSgAsu (ref sgComInterfaceList, interfacePairs);
			}
			InterfaceChains[] sgInterfaces = new InterfaceChains [sgComInterfaceList.Count];
			sgComInterfaceList.CopyTo (sgInterfaces);

			return sgInterfaces;
		}

		/// <summary>
		/// common interfaces from this homologous entry
		/// </summary>
		/// <param name="sgComInterfaceList"></param>
		/// <param name="interfacePairs"></param>
		private void GetCommonInterfacesInSgAsu (ref List<InterfaceChains> sgComInterfaceList, InterfacePairInfo[] interfacePairs)
		{
			bool interfaceExistOther = true;
			List<InterfaceChains> sgInterfaceList = new List<InterfaceChains> (sgComInterfaceList);
			sgComInterfaceList.Clear ();
			foreach (InterfaceChains interfaceChains in sgInterfaceList)
			{
				interfaceExistOther = false;
				foreach (InterfacePairInfo pairInfo in interfacePairs)
				{
					if (pairInfo.interfaceInfo1.interfaceId == interfaceChains.interfaceId && 
						pairInfo.qScore > AppSettings.parameters.contactParams.minQScore )
					{
						interfaceExistOther = true;
						break;
					}
				}
				if (interfaceExistOther)
				{
					sgComInterfaceList.Add (interfaceChains);
				}
			}
		}
		#endregion
		#endregion

		#region common interfaces between crystal forms in a group
		/// <summary>
		/// Compare two interfaces with homologous sequence
		/// sequence aligned by psiblast
		/// </summary>
		/// <param name="groupInterfacesHash"></param>
		/// <param name="groupId"></param>
		private void CompareHomoSeqInterfacesCompDb (int groupId, Dictionary<string, InterfaceChains[]> groupInterfaceChainsHash)
		{
			List<string> hashPdbList = new List<string> (groupInterfaceChainsHash.Keys);
			hashPdbList.Sort ();
            string[] groupEntries = hashPdbList.ToArray();
#if DEBUG
			string errorMsg = "";
#endif
			try
			{
                // retrieve the alignments at one time, may cause huge memory usage
			//	DataTable alignInfoTable =  homoEntryAlignInfo.GetGroupRepAlignInfoTable (hashPdbList, groupId);
				InterfacePairInfo[] interfacePairs = null;
				SupInterfacesComp interfaceComp = new SupInterfacesComp ();
                string pdbId1 = "";
                string pdbId2 = "";
                for (int i = 0; i < groupEntries.Length - 1; i++)
				{
                    pdbId1 = groupEntries[i];
					if (((InterfaceChains[])groupInterfaceChainsHash[pdbId1]).Length == 0)
					{
						continue;
					}
					//	ProtCidSettings.progressInfo.currentFileName = hashPdbList[i].ToString (); 
					for (int j = i + 1; j < groupEntries.Length; j ++)
					{
                        pdbId2 = groupEntries[j];
						ProtCidSettings.progressInfo.currentFileName = pdbId1+ "_" + pdbId2;
						
						if (((InterfaceChains[])groupInterfaceChainsHash[pdbId2]).Length == 0)
						{
							continue;
						}
						interfacePairs = crystInterfaceComp.GetInterfaceCompPairsInfoFromDb (pdbId1, pdbId2, CrystInterfaceTables.DifEntryInterfaceComp);
						if (interfacePairs != null)
						{
							continue;
						}
#if DEBUG
						errorMsg = "group ID: " + groupId.ToString () + "     " + pdbId1 + "_" + pdbId2;
#endif
						try
						{
                            // retrieve the alignment only for this pair
                            DataTable alignInfoTable = homoEntryAlignInfo.GetGroupRepAlignInfoTable(groupId, pdbId1, pdbId2);
							DataTable entryAlignInfoTable  = homoEntryAlignInfo.SelectSubTable (alignInfoTable, pdbId1, pdbId2);
							interfacePairs = interfaceComp.CompareSupStructures (
								(InterfaceChains[])groupInterfaceChainsHash[pdbId1], 
								(InterfaceChains[])groupInterfaceChainsHash[pdbId2], entryAlignInfoTable);
							crystInterfaceComp.AssignInterfacePairToTable (interfacePairs, pdbId1, pdbId2, CrystInterfaceTables.DifEntryInterfaceComp);
						}
						catch (Exception ex)
						{
#if DEBUG							
							ProtCidSettings.logWriter.WriteLine (errorMsg);
							ProtCidSettings.logWriter.WriteLine (ex.Message);
                            ProtCidSettings.progressInfo.progStrQueue.Enqueue (errorMsg);
#endif
                            ProtCidSettings.progressInfo.progStrQueue.Enqueue (ex.Message);
							continue;
						}
					}
					// clear the interfaces definition for entry hashPdbList[i]
					// to release memory
                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp]);
					CrystInterfaceTables.crystInterfaceTables [CrystInterfaceTables.DifEntryInterfaceComp].Clear ();
					groupInterfaceChainsHash[pdbId1] = null;
				}
		//		ProtCidSettings.progressInfo.currentFileName = hashPdbList[i].ToString ()+ ".xml"; 
				ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Done!");
			}
			catch (Exception ex)
			{
				ProtCidSettings.progressInfo.progStrQueue.Enqueue (ex.Message);
#if DEBUG
				ProtCidSettings.logWriter.WriteLine (ex.Message);
#endif
			}
		}
        
        /// <summary>
        /// Compare two list of interfaces of two entries of two sg-asu group
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="groupInterfaceChainsHash"></param>
        private void CompareTwoEntryInterfaces(int groupId, string pdbId1, string pdbId2, InterfaceChains[] interfaces1, InterfaceChains[] interfaces2)
        {
            InterfacePairInfo[] interfacePairs = null;
            SupInterfacesComp interfaceComp = new SupInterfacesComp();

            if (interfaces1 == null || interfaces1.Length == 0)
            {
                return;
            }

            if (interfaces2 == null || interfaces2.Length == 0)
            {
                return;
            }
            interfacePairs = crystInterfaceComp.GetInterfaceCompPairsInfoFromDb(pdbId1, pdbId2, CrystInterfaceTables.DifEntryInterfaceComp);
            // already in the db
            if (interfacePairs != null)
            {
                return;
            }

            try
            {
                // retrieve the alignment only for this pair
                DataTable alignInfoTable = homoEntryAlignInfo.GetGroupRepAlignInfoTable(groupId, pdbId1, pdbId2);
                DataTable entryAlignInfoTable = homoEntryAlignInfo.SelectSubTable(alignInfoTable, pdbId1, pdbId2);
                interfacePairs = interfaceComp.CompareSupStructures(interfaces1, interfaces2, entryAlignInfoTable);
                crystInterfaceComp.AssignInterfacePairToTable(interfacePairs, pdbId1, pdbId2, CrystInterfaceTables.DifEntryInterfaceComp);

                // clear the interfaces definition for entry hashPdbList[i]
                // to release memory
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp]);
                CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.DifEntryInterfaceComp].Clear();
            }
            catch (Exception ex)
            {
#if DEBUG
                ProtCidSettings.logWriter.WriteLine("group ID: " + groupId.ToString() + "     " + pdbId1 + "_" + pdbId2);
                ProtCidSettings.logWriter.WriteLine(ex.Message);
                ProtCidSettings.logWriter.Flush();
#endif
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("group ID: " + groupId.ToString() + "     " + pdbId1 + "_" + pdbId2);
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
            }
        }
		#endregion

		#region group common interfaces
		/// <summary>
		/// find common interfaces in a group based on identity-weighted Q score
		/// </summary>
		protected void FindGroupCommonInterfaces(int groupId)
		{
			// for Q score
			Dictionary<string, List<string>> commonInterfaceHash = new Dictionary<string,List<string>> ();
			List<string> distinctEntryList = new List<string> ();
			// for Q_Identity
			Dictionary<string, List<string>> commonInterfaceIdentityHash = new Dictionary<string,List<string>> ();
			List<string> distinctEntryIdentityList = new List<string> ();
			DataTable sgInterfaceCompTable = GetSgInterfaceCompTable (groupId);
			if (sgInterfaceCompTable.Rows.Count == 0)	
			{
				return;
			}
			foreach (DataRow dRow in sgInterfaceCompTable.Rows)	
			{
				string pdbId1 = dRow["PdbId1"].ToString ();
				string pdbId2 = dRow["PdbId2"].ToString ();
				if (Convert.ToDouble (dRow["QScore"].ToString ()) >= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
				{
					if (! distinctEntryList.Contains (pdbId1))
					{
						distinctEntryList.Add (pdbId1);
					}
					if (! distinctEntryList.Contains (pdbId2))
					{
						distinctEntryList.Add (pdbId2);
					}
					if (commonInterfaceHash.ContainsKey (pdbId1 + "_" + dRow["InterfaceId1"].ToString ()))
					{
						List<string> entryList = commonInterfaceHash[pdbId1 + "_" + dRow["InterfaceId1"].ToString ()];
						if (! entryList.Contains (pdbId2))
						{
							entryList.Add (pdbId2);
						}
					}
					else
					{
						List<string> entryList = new List<string> ();
						entryList.Add (pdbId2);
						commonInterfaceHash.Add (pdbId1 + "_" + dRow["InterfaceId1"].ToString (), entryList);
					}
					if (commonInterfaceHash.ContainsKey (pdbId2 + "_" + dRow["InterfaceId2"].ToString ()))
					{
						List<string> entryList = commonInterfaceHash[pdbId2 + "_" + dRow["InterfaceId2"].ToString ()];
						if (! entryList.Contains (pdbId1))
						{
							entryList.Add (pdbId1);
						}
					}
					else
					{
						List<string> entryList = new List<string> ();
						entryList.Add (pdbId1);
						commonInterfaceHash.Add (pdbId2 + "_" + dRow["InterfaceId2"].ToString (), entryList);
					}
				}
				if (! distinctEntryIdentityList.Contains (pdbId1))
				{
					distinctEntryIdentityList.Add (pdbId1);
				}
				if (! distinctEntryIdentityList.Contains (pdbId2))
				{
					distinctEntryIdentityList.Add (pdbId2);
				}
				if (commonInterfaceIdentityHash.ContainsKey (pdbId1 + "_" + dRow["InterfaceId1"].ToString ()))
				{
					List<string> entryList = commonInterfaceIdentityHash[pdbId1 + "_" + dRow["InterfaceId1"].ToString ()];
					if (! entryList.Contains (pdbId2))
					{
						entryList.Add (pdbId2);
					}
				}
				else
				{
					List<string> entryList = new List<string> ();
					entryList.Add (pdbId2);
					commonInterfaceIdentityHash.Add (pdbId1 + "_" + dRow["InterfaceId1"].ToString (), entryList);
				}
				if (commonInterfaceIdentityHash.ContainsKey (pdbId2 + "_" + dRow["InterfaceId2"].ToString ()))
				{
					List<string> entryList = commonInterfaceIdentityHash[pdbId2 + "_" + dRow["InterfaceId2"].ToString ()];
					if (! entryList.Contains (pdbId1))
					{
						entryList.Add (pdbId1);
					}
				}
				else
				{
					List<string> entryList = new List<string> ();
					entryList.Add (pdbId1);
					commonInterfaceIdentityHash.Add (pdbId2 + "_" + dRow["InterfaceId2"].ToString (), entryList);
				}
			}
			string conditionString = "";
			string updateString = "";
			int numOfSg_identity = 1;
			int numOfSg = 1;
			foreach (string pdbInterface in commonInterfaceIdentityHash.Keys)
			{
				List<string> entryList_identity = commonInterfaceIdentityHash[pdbInterface];
				numOfSg_identity = entryList_identity.Count + 1;
				if (commonInterfaceHash.ContainsKey (pdbInterface))
				{
					List<string> entryList = commonInterfaceHash[pdbInterface];
					numOfSg = entryList.Count + 1;
				}
				else
				{
					numOfSg = 1;
				}
				string[] fields = pdbInterface.Split ('_');
				conditionString = string.Format ("PdbID = '{0}' AND InterfaceID = '{1}'", fields[0], fields[1]);
				updateString = string.Format ("Update {0} Set NumOfSg = {1}, NumOfSg_Identity = {2} " + 
					" Where {3};", GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], numOfSg, numOfSg_identity, conditionString);
                dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
			}
		}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
		private DataTable GetSgInterfaceCompTable (int groupId)
		{
            string queryString = string.Format ("SELECT Distinct PdbID FROM {0} WHere GroupSeqId = {1};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], groupId);
            DataTable sgEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> entryList = new List<string>();
			foreach (DataRow dRow in sgEntryTable.Rows)
			{
                entryList.Add(dRow["PdbID"].ToString ());
			}

            DataTable interfaceCompTable = interfaceCompDbQuery.GetDifEntryInterfaceCompTable(entryList,
                AppSettings.parameters.simInteractParam.interfaceSimCutoff);
            
            return interfaceCompTable;
		}
		#endregion

		#region organize data to table		
	
		#region representative entry interfaces
		/// <summary>
		/// assign unique interface in a space group into the table
		/// </summary>
		/// <param name="interfaceChains"></param>
		/// <param name="repPdbId"></param>
		/// <param name="spaceGroup"></param>
		/// <param name="groupId"></param>
		protected void AssignSgInterfacesToTable (InterfaceChains[] interfaceChains, string repPdbId, string sg_asu, int groupId)
		{
            if (AreSgInterfacesInDb (groupId, sg_asu, repPdbId))
            {
                return;
            }
			string[] sgAsu = sg_asu.Split ('_');
			double asa = -1.0;
			foreach (InterfaceChains thisInterface in interfaceChains)
			{
				// interative chains info
				DataRow interfaceRow = CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.SgInterfaces].NewRow ();
				interfaceRow["GroupSeqID"] = groupId;
				interfaceRow["PDBID"] = repPdbId;
				interfaceRow["SpaceGroup"] = sgAsu[0];
				interfaceRow["ASU"] = sgAsu[1];
				interfaceRow["InterfaceID"] = thisInterface.interfaceId;
				interfaceRow["NumOfSg"] = 1;
				interfaceRow["NumOfSg_Identity"] = 1;
				// find asa from crystentryinterfaces
				asa = GetSurfaceAreaFromDb (repPdbId, thisInterface.interfaceId);					
				interfaceRow["SurfaceArea"] = asa;
	

				CrystInterfaceTables.crystInterfaceTables[CrystInterfaceTables.SgInterfaces].Rows.Add (interfaceRow);
                #region remove contacts: can be calculated from the coordinate interface file
                // modified on Nov. 15, 2016
                // add residues to table
                /*				ArrayList seqList1 = new ArrayList ();
                                ArrayList seqList2 = new ArrayList ();
                                // a residue of one chain may has distance <= 12A 
                                // with several residues in the other chain
                                if (! IsEntryInterfaceResiduesExistInDb (repPdbId, thisInterface.interfaceId))
                                {
					
                                    foreach (string seqStr in thisInterface.seqDistHash.Keys )
                                    {
                                        string[] seqIds = seqStr.Split ('_');
                                        DataRow residueRow = CrystInterfaceTables.crystInterfaceTables [CrystInterfaceTables.SgInterfaceResidues].NewRow ();
                                        //	residueRow["GroupSeqId"] = groupId;
                                        residueRow["PdbID"] = repPdbId;
                                        residueRow["InterfaceID"] = thisInterface.interfaceId;
                                        residueRow["Residue1"] = thisInterface.GetAtom (seqIds[0], 1).residue;					
                                        residueRow["SeqID1"] = seqIds[0];
                                        residueRow["Residue2"] = thisInterface.GetAtom (seqIds[1], 2).residue;					
                                        residueRow["SeqID2"] = seqIds[1];
                                        residueRow["Distance"] = (double)thisInterface.seqDistHash[seqStr];
                                        CrystInterfaceTables.crystInterfaceTables [CrystInterfaceTables.SgInterfaceResidues].Rows.Add (residueRow);
                                    }
                                }
                                if (! IsEntryInterfaceContactsExistInDb (repPdbId, thisInterface.interfaceId))
                                {					
                                    foreach (string seqStr in thisInterface.seqContactHash.Keys )
                                    {
					
                                        string[] seqIds = seqStr.Split ('_');
                                        Contact contact = (Contact) thisInterface.seqContactHash[seqStr];
                                        DataRow contactRow = CrystInterfaceTables.crystInterfaceTables [CrystInterfaceTables.SgInterfaceContacts].NewRow ();
                                        //		contactRow["GroupSeqId"] = groupId;
                                        contactRow["PdbID"] = repPdbId;
                                        contactRow["InterfaceID"] = thisInterface.interfaceId;
                                        contactRow["Residue1"] = contact.Residue1;					
                                        contactRow["SeqID1"] = contact.SeqID1;
                                        contactRow["Residue2"] = contact.Residue2;					
                                        contactRow["SeqID2"] = contact.SeqID2;
                                        contactRow["Distance"] = contact.Distance;
                                        CrystInterfaceTables.crystInterfaceTables [CrystInterfaceTables.SgInterfaceContacts].Rows.Add (contactRow);
                                    }
                                }*/
                #endregion
            }
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="sgAsu"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool AreSgInterfacesInDb (int groupId, string sgAsu, string pdbId)
        {
            string[] sgAsuFields = sgAsu.Split('_');
            string queryString = string.Format("Select PdbID From {0} Where GroupSeqID = {1} AND SpaceGroup = '{2}' AND ASU = '{3}' AND PdbID = '{4}';",
                GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], groupId, sgAsuFields[0], sgAsuFields[1], pdbId);
            DataTable sgRepInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (sgRepInterfaceTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
		private double GetSurfaceAreaFromDb (string pdbId, int interfaceId)
		{
			string queryString = string.Format ("SELECT SurfaceArea FROM CrystEntryInterfaces " + 
				" WHERE PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable asaTable = ProtCidSettings.protcidQuery.Query( queryString);
			if (asaTable.Rows.Count > 0)
			{
				return Convert.ToDouble (asaTable.Rows[0]["SurfaceArea"].ToString ());
			}
			return -1.0;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
		private bool IsEntryInterfaceResiduesExistInDb (string pdbId, int interfaceId)
		{
			string queryString = string.Format ("Select * FROM SgInterfaceResidues " + 
				" WHERE PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable residueTable = ProtCidSettings.protcidQuery.Query( queryString);
			if (residueTable.Rows.Count > 0)
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Are contacts of this interfac
		/// </summary>
		/// <param name="pdbId"></param>
		/// <returns></returns>
		private bool IsEntryInterfaceContactsExistInDb (string pdbId, int interfaceId)
		{
			string queryString = string.Format ("Select * FROM SgInterfaceContacts " + 
				" WHERE PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable contactTable = ProtCidSettings.protcidQuery.Query( queryString);
			if (contactTable.Rows.Count > 0)
			{
				return true;
			}
			else
			{
				return false;
			}
		}
		#endregion

		
		#endregion

		#region delete obsolete data
		/// <summary>
		/// delete obsolete data from db
		/// </summary>
		/// <param name="groupId"></param>
		private void DeleteEntryGroupObsData (int groupId)
		{
			string deleteString = string.Format ("DELETE FROM {0} WHERE GroupSeqID = {1};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], groupId);
            ProtCidSettings.protcidQuery.Query( deleteString);
	/*		deleteString = string.Format ("DELETE FROM SgInterfaceResidues WHERE GroupSeqID = {0};", groupId);
			dbQuery.Query (deleteString);
			deleteString = string.Format ("DELETE FROM SgInterfaceContacts WHERE GroupSeqID = {0};", groupId);
			dbQuery.Query (deleteString);*/
		}

		private void RemoveObsRepDataFromSgInterfaceTables (string updateRepPdb)
		{			
			string deleteString = "";	
			deleteString = string.Format ("DELETE FROM {0} WHERE PdbID = '{1}';", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], updateRepPdb);
            ProtCidSettings.protcidQuery.Query( deleteString);
	/*		deleteString = string.Format ("DELETE FROM SgInterfaceResidues WHERE PdbID = '{0}';", updateRepPdb);
            ProtCidSettings.protcidQuery.Query( deleteString);
			deleteString = string.Format ("DELETE FROM SgInterfaceContacts WHERE PdbID = '{0}';", updateRepPdb);
            ProtCidSettings.protcidQuery.Query( deleteString);*/
		}

		private void RemoveObsRepDataFromAllInterfaceTables (string updatePdb)
		{			
			string deleteString = "";	
			deleteString = string.Format ("DELETE FROM {0} WHERE PdbID = {1};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], updatePdb);
            ProtCidSettings.protcidQuery.Query( deleteString);
	/*		deleteString = string.Format ("DELETE FROM SgInterfaceResidues WHERE PdbID = '{0}';", updatePdb);
            ProtCidSettings.protcidQuery.Query( deleteString);
			deleteString = string.Format ("DELETE FROM SgInterfaceContacts WHERE PdbID = '{0}';", updatePdb);
            ProtCidSettings.protcidQuery.Query( deleteString);*/

			deleteString = string.Format ("DELETE FROM CrystEntryInterfaces WHERE PdbID = '{0}';", updatePdb);
            ProtCidSettings.protcidQuery.Query( deleteString);
			deleteString = string.Format ("DELETE FROM EntryInterfaceComp WHERE PdbID = '{0}';", updatePdb);
            ProtCidSettings.protcidQuery.Query( deleteString);
			deleteString = string.Format ("DELETE FROM DifEntryInterfaceComp " + 
				" WHERE PdbID1 = '{0}' OR PdbID2 = '{0}';", updatePdb);
            ProtCidSettings.protcidQuery.Query( deleteString);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="updateEntries"></param>
        private void RemoveObsDataFromInterfaceTables(string[] updateEntries)
        {
            string deleteString = "";
            foreach (string updatePdb in updateEntries)
            {
                deleteString = string.Format("DELETE FROM CrystEntryInterfaces WHERE PdbID = '{0}';", updatePdb);
                ProtCidSettings.protcidQuery.Query( deleteString);

                deleteString = string.Format("DELETE FROM EntryInterfaceComp WHERE PdbID = '{0}';", updatePdb);
                ProtCidSettings.protcidQuery.Query( deleteString);

                deleteString = string.Format("DELETE FROM DifEntryInterfaceComp " +
                    " WHERE PdbID1 = '{0}' OR PdbID2 = '{0}';", updatePdb);
                ProtCidSettings.protcidQuery.Query( deleteString);

         /*       deleteString = string.Format("DELETE FROM SgInterfaceResidues WHERE PdbID = '{0}';", updatePdb);
                ProtCidSettings.protcidQuery.Query( deleteString);

                deleteString = string.Format("DELETE FROM SgInterfaceContacts WHERE PdbID = '{0}';", updatePdb);
                ProtCidSettings.protcidQuery.Query( deleteString);*/
            }
        }

		private void RemoveObsHomoDataFromInterfaceTables (string updatePdb)
		{			
			string deleteString = "";	
			deleteString = string.Format ("DELETE FROM CrystEntryInterfaces WHERE PdbID = '{0}';", updatePdb);
            ProtCidSettings.protcidQuery.Query( deleteString);
			deleteString = string.Format ("DELETE FROM EntryInterfaceComp WHERE PdbID = '{0}';", updatePdb);
            ProtCidSettings.protcidQuery.Query( deleteString);
			deleteString = string.Format ("DELETE FROM DifEntryInterfaceComp " + 
				" WHERE PdbID1 = '{0}' OR PdbID2 = '{0}';", updatePdb);
            ProtCidSettings.protcidQuery.Query( deleteString);
		}
		#endregion
    }
}
