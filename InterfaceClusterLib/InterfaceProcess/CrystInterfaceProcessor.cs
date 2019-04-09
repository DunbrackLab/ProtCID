using System;
using System.IO;
using System.Collections.Generic;
using System.Data;
using AuxFuncLib;
using DbLib;
using CrystalInterfaceLib.ProtInterfaces;
using ProtCidSettingsLib;
using CrystalInterfaceLib.StructureComp;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Crystal;
using InterfaceClusterLib.DataTables;
using BuCompLib.BuInterfaces;

namespace InterfaceClusterLib.InterfaceProcess
{
	/// <summary>
	/// print crystal interfaces occurs at least two crystal forms
	/// </summary>
	public class CrystInterfaceProcessor
	{
		#region member variables
		private InterfaceFileWriter interfaceWriter = new InterfaceFileWriter ();
	//	private ContactsFinder contactsFinder = new ContactsFinder ();
		private DbUpdate dbUpdate = new DbUpdate ();
		private DbInsert dbInsert = new DbInsert ();
		private DbQuery dbQuery = new DbQuery ();
	//	private DataTable contactsTable = null;
        private AsuInterfaces asuInterfaces = new AsuInterfaces();
		#endregion

		public CrystInterfaceProcessor()
		{
		}

		#region process cryst interface
		/// <summary>
		/// print all the crystal interfaces occur in at leat two different space groups
		/// from database into pdb formatted files
		/// </summary>
		public void ProcessCrystInterfacesFromDb ()
		{
			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();

           int[] groups = null;
           /*  int startGroupId = 3801;
               int endGroupId = 3800;

              string crystInterfaceQuery = string.Format("Select * From {0} Where SurfaceArea = -1.0 AND " + 
                   " GroupSeqID >= {1};",
                   GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], startGroupId);
               Hashtable groupSgNumHash = GetGroupSgEntryHash(startGroupId, endGroupId);
               Hashtable groupSgNumHash = GetGroupSgEntryHash(startGroupId);
        */
           string crystInterfaceQuery = string.Format ("Select * From {0} Where SurfaceArea = -1.0 ;",
                GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces]);

			// get the number of space groups in a group
           Dictionary<int, List<string>> groupSgNumHash = GetGroupSgEntryHash(groups);

			// get the interface table needed to be processed
			DataTable homoInterfaceTable = dbQuery.Query (ProtCidSettings.protcidDbConnection, crystInterfaceQuery);
			Dictionary<int, List<string>> groupEntryNumHash = GetGroupEntryNumHash (homoInterfaceTable);

			ProtCidSettings.progressInfo.currentOperationLabel = "Process Cryst Interfaces";
			ProtCidSettings.progressInfo.totalOperationNum = groupEntryNumHash.Count;
			ProtCidSettings.progressInfo.totalStepNum = groupEntryNumHash.Count;
		
			List<int> groupList = new List<int> (groupEntryNumHash.Keys);
			groupList.Sort ();
			foreach (int groupId in groupList)
			{
				ProtCidSettings.progressInfo.currentFileName = groupId.ToString ();
				ProtCidSettings.progressInfo.currentStepNum ++;
				ProtCidSettings.progressInfo.currentOperationNum ++;
                int totalSgNum = groupSgNumHash[groupId].Count;
				ProcessGroupInterfaces (groupId, homoInterfaceTable, totalSgNum);
			}
            // for those not in the sginterfaces table
            GenerateMissingCrystInterfaces ();
		}

		#region update
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateGroupHash"></param>
        public void UpdateCrystInterfaces(Dictionary<int, string[]> updateGroupHash)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            ProtCidSettings.progressInfo.currentOperationLabel = "Update Cryst Interfaces";
            List<string> updateEntryList = new List<string> ();
            foreach (int updateGroup in updateGroupHash.Keys)
            {
                updateEntryList.AddRange((string[])updateGroupHash[updateGroup]);
            }

            GenerateEntryInterfaceFiles(updateEntryList.ToArray ());
        }
		#endregion

		#region generating common interface files from user-defined groups

		/// <summary>
		/// generate all common interfaces with PDB formatted
		/// </summary>
		/// <param name="groupId">sudo group seqID</param>
		/// <param name="pdbList">the entry list provided by the user</param>
		public void GenerateCrystInterfacesFromUser (int groupId, string[] pdbList)
		{
			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
	  
			// get the number of space groups in a group
			int totalSgNum = GetGroupSgNumHash (groupId);

			string crystInterfaceQuery = string.Format ("Select * From {0} Where GroupSeqID = {1} AND NumOfSg > 1", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], groupId);			
			// get the interface table needed to be processed
            DataTable homoInterfaceTable = ProtCidSettings.protcidQuery.Query( crystInterfaceQuery);

			ProtCidSettings.progressInfo.currentOperationLabel = "Generating Cryst Interfaces";
				
			ProcessGroupInterfaces (groupId, homoInterfaceTable, totalSgNum);
		}

		/// <summary>
		/// get the number of space groups for each group
		/// </summary>
		/// <returns>key: group id, value: space groups list</returns>
		private int GetGroupSgNumHash (int groupId)
		{
			string queryString = string.Format ("Select Distinct GroupSeqID, PdbID From {0} Where GroupSeqID = {1};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], groupId);

            DataTable groupSgNumTable = ProtCidSettings.protcidQuery.Query( queryString);

			List<string> pdbList = new List<string> ();
			string pdbId = "";
			foreach (DataRow dRow in groupSgNumTable.Rows)
			{
				pdbId = dRow["PdbID"].ToString ();	
				pdbList.Add (pdbId);
			}
			return pdbList.Count;
		}
		#endregion

		/// <summary>
		/// process interfaces existing at least two crystal forms in a group
		/// </summary>
		/// <param name="groupId"></param>
		/// <param name="homoInterfaceTable"></param>
		/// <param name="totalSgNum"></param>
		private void ProcessGroupInterfaces (int groupId, DataTable homoInterfaceTable, int totalSgNum)
		{
			string residueQuery = "";
			DataTable residueTable = null;
			string currentPdbId = "";
			string prePdbId = "";
			int interfaceId = -1;
			List<ProtInterfaceInfo> interfaceList = new List<ProtInterfaceInfo> ();

			// get interface definition rows 
			DataRow[] groupInterfaceRows = homoInterfaceTable.Select ("GroupSeqID = " + groupId, "PdbID ASC");
			List<string> pdbList = new List<string>  ();
			//		ArrayList updatePdbList = (ArrayList)groupUpdateEntryHash[groupId];
			foreach (DataRow interfaceRow in groupInterfaceRows)
			{
				if (pdbList.Contains (interfaceRow["PdbID"].ToString ()))
				{
					continue;
				}					
				pdbList.Add (interfaceRow["PdbID"].ToString ());	
			}
			string interfaceDefQueryString = string.Format ("Select * From CrystEntryInterfaces Where PdbID In ({0});", 
				ParseHelper.FormatSqlListString (pdbList.ToArray ()));
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query( interfaceDefQueryString);
			foreach (DataRow interfaceRow in groupInterfaceRows)
			{					
				currentPdbId = interfaceRow["PdbID"].ToString ();
				interfaceId = Convert.ToInt32 (interfaceRow["InterfaceID"].ToString ());
				
				if (currentPdbId != prePdbId)
				{					
					if (prePdbId != "")
					{
						ProtCidSettings.progressInfo.currentFileName = groupId.ToString () + "_" + prePdbId;
						if (interfaceList.Count > 0)
						{
							ProtInterfaceInfo[] interfaces = new ProtInterfaceInfo [interfaceList.Count];
							interfaceList.CopyTo (interfaces);
							ProcessInterfaces (groupId, prePdbId, interfaces);
						}
					}
					interfaceList = new List<ProtInterfaceInfo> ();
				}
				DataRow[] interfaceDefRows = interfaceTable.Select 
					(string.Format ("PdbID = '{0}' AND InterfaceID = '{1}'", currentPdbId, interfaceId));
				if (interfaceDefRows.Length == 0)
				{
					ProtCidSettings.progressInfo.progStrQueue.Enqueue 
						("Error: No interface definition available for Entry: " + currentPdbId + " Interface: " + interfaceId);
                    ProtCidSettings.logWriter.WriteLine
                        ("Error: No interface definition available for Entry: " + currentPdbId + " Interface: " + interfaceId);
					continue;
				}

				//  if the interface file already exist and surface area already computed, skip
				double asa = Convert.ToDouble (interfaceRow["SurfaceArea"].ToString ());
				if (asa > 0 && IsInterfaceFileExist (currentPdbId, interfaceId))
				{
					continue;
				}

				// query residues and contacts if necessary
				if (currentPdbId != prePdbId)
				{
					residueQuery = string.Format ("SELECT * FROM SgInterfaceResidues WHERE PDBID = '{0}';", currentPdbId);
                    residueTable = ProtCidSettings.protcidQuery.Query( residueQuery);
				}
				// if file not exist, compute surface area again
				if (! File.Exists (Path.Combine (ProtCidSettings.dirSettings.interfaceFilePath, "cryst\\" + currentPdbId + "_" + interfaceId + ".cryst.gz")))
				{
					interfaceRow["SurfaceArea"] = -1;
				}

				ProtInterfaceInfo interfaceInfo = GetInterfaceInfo 
					(interfaceId, interfaceRow, interfaceDefRows[0], residueTable, totalSgNum);
				interfaceList.Add (interfaceInfo);

				prePdbId = currentPdbId;	
			}
			if (interfaceList.Count > 0)
			{
				ProtInterfaceInfo[] lastInterfaces = new ProtInterfaceInfo [interfaceList.Count];
				interfaceList.CopyTo (lastInterfaces);
				ProtCidSettings.progressInfo.currentFileName = groupId.ToString () + "_" + currentPdbId;
				ProcessInterfaces (groupId, currentPdbId, lastInterfaces);
			}
		}

		/// <summary>
		/// check if the specific interface file already exist
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="interfaceId"></param>
		/// <returns></returns>
		private bool IsInterfaceFileExist (string pdbId, int interfaceId)
		{
			string filePath = ProtCidSettings.dirSettings.interfaceFilePath + "\\cryst\\";
			filePath += pdbId.Substring (1, 2);
			string interfaceFile = Path.Combine (filePath, pdbId + "_" + interfaceId + ".cryst.gz");
			if (File.Exists (interfaceFile))
			{
				return true;
			}
			return false;
		}
		#endregion

		#region Accessor functions
		/// <summary>
		/// get interface from db
		/// </summary>
		/// <param name="interfaceId"></param>
		/// <param name="interfaceRow"></param>
		/// <param name="interfaceDefRow"></param>
		/// <param name="residueTable"></param>
		/// <returns></returns>
		private ProtInterfaceInfo GetInterfaceInfo (int interfaceId, DataRow interfaceRow, DataRow interfaceDefRow, 
			DataTable residueTable, int totalSgNum)
		{
			ProtInterfaceInfo interfaceInfo = new ProtInterfaceInfo ();
			interfaceInfo.InterfaceId = interfaceId;
			interfaceInfo.ASA = Convert.ToDouble (interfaceRow["SurfaceArea"].ToString ());
			interfaceInfo.Chain1 = interfaceDefRow["AsymChain1"].ToString ().Trim ();
			interfaceInfo.Chain2 = interfaceDefRow["AsymChain2"].ToString ().Trim ();
			interfaceInfo.SymmetryString1 = interfaceDefRow["SymmetryString1"].ToString ().Trim ();
			interfaceInfo.SymmetryString2 = interfaceDefRow["SymmetryString2"].ToString ().Trim ();
			interfaceInfo.Remark = FormatRemark (interfaceDefRow, 
				Convert.ToInt32 (interfaceRow["NumOfSg"].ToString ()), totalSgNum);
			Dictionary<string, string>[] residueSeqHashes = GetResidueSeqs (residueTable, interfaceId);
			interfaceInfo.ResiduesInChain1 = residueSeqHashes[0];
			interfaceInfo.ResiduesInChain2 = residueSeqHashes[1];
			interfaceInfo.Remark += interfaceInfo.FormatResidues ("A");
			interfaceInfo.Remark += interfaceInfo.FormatResidues ("B");
			return interfaceInfo;
		}

		/// <summary>
		/// get interface from db
		/// </summary>
		/// <param name="interfaceId"></param>
		/// <param name="interfaceRow"></param>
		/// <param name="interfaceDefRow"></param>
		/// <param name="residueTable"></param>
		/// <returns></returns>
		public ProtInterfaceInfo GetInterfaceInfo (int interfaceId, DataRow interfaceDefRow)
		{
			ProtInterfaceInfo interfaceInfo = new ProtInterfaceInfo ();
			interfaceInfo.InterfaceId = interfaceId;
			interfaceInfo.ASA = Convert.ToDouble (interfaceDefRow["SurfaceArea"].ToString ());
			interfaceInfo.Chain1 = interfaceDefRow["AsymChain1"].ToString ().Trim ();
			interfaceInfo.Chain2 = interfaceDefRow["AsymChain2"].ToString ().Trim ();
			interfaceInfo.SymmetryString1 = interfaceDefRow["SymmetryString1"].ToString ().Trim ();
			interfaceInfo.SymmetryString2 = interfaceDefRow["SymmetryString2"].ToString ().Trim ();
			interfaceInfo.Remark = FormatRemark (interfaceDefRow);
			return interfaceInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="theInterface"></param>
        /// <returns></returns>
        public ProtInterfaceInfo GetInterfaceInfo(InterfaceChains theInterface, DataTable asuTable)
        {
            ProtInterfaceInfo interfaceInfo = new ProtInterfaceInfo();
            interfaceInfo.InterfaceId = theInterface.interfaceId;
            interfaceInfo.ASA = -1.0;
            string authorChain1 = "";
            string authorChain2 = "";
            string[] chainSymOpStrings = GetAsymChainAndSymOpString(theInterface.firstSymOpString);
            interfaceInfo.Chain1 = chainSymOpStrings[0];
            authorChain1 = GetAuthorChain(interfaceInfo.Chain1, asuTable);
            interfaceInfo.SymmetryString1 = chainSymOpStrings[1];
            chainSymOpStrings = GetAsymChainAndSymOpString(theInterface.secondSymOpString);
            interfaceInfo.Chain2 = chainSymOpStrings[0];
            if (interfaceInfo.Chain1 == interfaceInfo.Chain2)
            {
                authorChain2 = authorChain1;
            }
            else
            {
                authorChain2 = GetAuthorChain(interfaceInfo.Chain2, asuTable);
            }
            interfaceInfo.SymmetryString2 = chainSymOpStrings[1];
            interfaceInfo.Remark = "Remark 300 Interface Chain A For Asymmetric Chain " +
                interfaceInfo.Chain1 + " Author Chain " + authorChain1 + " " +
                " Entity  " + theInterface.entityId1.ToString () + " " +
                " Symmetry Operator    " + interfaceInfo.SymmetryString1 + "\r\n" +
                "Remark 300 Interface Chain B For Asymmetric Chain " +
                interfaceInfo.Chain2 + " Author Chain " + authorChain2 + " " + 
                " Entity  " + theInterface.entityId2.ToString () + " " +
                " Symmetry Operator    " + interfaceInfo.SymmetryString2 + "\r\n";
            return interfaceInfo; 
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymId"></param>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        public string GetAuthorChain(string asymId, DataTable asuTable)
        {
            DataRow[] authChainRows = asuTable.Select(string.Format ("AsymID = '{0}' AND PolymerType = 'polypeptide'", asymId));
            if (authChainRows.Length > 0)
            {
                return authChainRows[0]["AuthorChain"].ToString().TrimEnd();
            }
            return "-";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="symOpString"></param>
        /// <returns></returns>
        public string[] GetAsymChainAndSymOpString(string symOpString)
        {
            string[] chainSymOpStrings = new string[2];
            string[] symOpFields = symOpString.Split('_');
            if (symOpFields.Length == 1)
            {
                chainSymOpStrings[0] = symOpFields[0];
                chainSymOpStrings[1] = "1_555";
            }
            else if (symOpFields.Length == 2)
            {
                chainSymOpStrings[0] = symOpFields[0];
                chainSymOpStrings[1] = symOpFields[1];
            }
            else if (symOpFields.Length == 3)
            {
                chainSymOpStrings[0] = symOpFields[0];
                chainSymOpStrings[1] = symOpFields[1] + "_" + symOpFields[2];
            }
            return chainSymOpStrings;
        }

        #region representative entries in a group
        /// <summary>
		/// get the entries in a group
		/// </summary>
		/// <param name="homoInterfaceTable"></param>
		/// <returns></returns>
		private Dictionary<int, List<string>> GetGroupEntryNumHash (DataTable homoInterfaceTable)
		{
            Dictionary<int, List<string>> groupEntryNumHash = new Dictionary<int, List<string>>();
			foreach (DataRow dRow in homoInterfaceTable.Rows)
			{
				int groupId = Convert.ToInt32 (dRow["GroupSeqID"].ToString ());
				string pdbId = dRow["PdbID"].ToString ();
				if (! groupEntryNumHash.ContainsKey (groupId))
				{
					List<string> pdbList = new List<string> ();					
					pdbList.Add (pdbId);
					groupEntryNumHash.Add (groupId, pdbList);
				}
				else
				{
                    if (!groupEntryNumHash[groupId].Contains(pdbId))
					{
                        groupEntryNumHash[groupId].Add(pdbId);
					}
				}
			}
			return groupEntryNumHash;
		}
		
		/// <summary>
		/// get the entries in a group
		/// </summary>
		/// <param name="homoInterfaceTable"></param>
		/// <returns></returns>
        private Dictionary<int, List<string>> GetGroupSgEntryHash(DataTable homoInterfaceTable, Dictionary<int, List<string>> updateGroupHash)
		{
            Dictionary<int, List<string>> groupSgEntryHash = new Dictionary<int, List<string>> ();
			foreach (DataRow dRow in homoInterfaceTable.Rows)
			{
				int groupId = Convert.ToInt32 (dRow["GroupSeqID"].ToString ());
				string pdbId = dRow["PdbID"].ToString ();
				List<string> groupUpdatePdbList = updateGroupHash[groupId];
				if (! groupUpdatePdbList.Contains (pdbId))
				{
					continue;
				}
                if (!groupSgEntryHash.ContainsKey(groupId))
				{
					List<string> pdbList = new List<string> ();					
					pdbList.Add (pdbId);
                    groupSgEntryHash.Add(groupId, pdbList);
				}
				else
				{
					if (! groupSgEntryHash[groupId].Contains (pdbId))
					{
						groupSgEntryHash[groupId].Add(pdbId);
					}
				}
			}
			return groupSgEntryHash;
		}
		/// <summary>
		/// get the number of space groups for each group
		/// </summary>
		/// <returns>key: group id, value: a list of entries representating the list of space groups in the group</returns>
		private Dictionary<int, List<string>> GetGroupSgEntryHash (int[] groups)
		{
			string queryString = "";
			if (groups == null)
			{
				queryString = string.Format ("Select Distinct GroupSeqId, PdbID From {0};", 
					GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces]);
			}
			else
			{
				if (groups.Length  > 0)
				{
					queryString = string.Format ("Select Distinct GroupSeqID, PdbID From {0} Where GroupSeqID IN ({1});", 
						GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], ParseHelper.FormatSqlListString (groups));
				}
			}
            DataTable groupSgEntryTable = ProtCidSettings.protcidQuery.Query( queryString);

            Dictionary<int, List<string>> groupSgEntryHash = GetGroupSgEntryHash(groupSgEntryTable);

            return groupSgEntryHash;
		}

        /// <summary>
        /// get the number of space groups for each group
        /// </summary>
        /// <returns>key: group id, value: a list of entries which representing a list of space groups in the group </returns>
        private Dictionary<int, List<string>> GetGroupSgEntryHash(int startGroupId)
        {
            string queryString = string.Format("Select Distinct GroupSeqID, PdbID From {0} Where GroupSeqID >= {1};",
                        GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], startGroupId);

            DataTable groupSgEntryTable = ProtCidSettings.protcidQuery.Query( queryString);

            Dictionary<int, List<string>> groupSgEntryHash = GetGroupSgEntryHash(groupSgEntryTable);

            return groupSgEntryHash;
        }

        /// <summary>
        /// get the number of space groups for each group
        /// </summary>
        /// <returns>key: group id, value: a list of entries which representing a list of space groups in the group </returns>
        private Dictionary<int, List<string>> GetGroupSgEntryHash(int startGroupId, int endGroupId)
        {
            string queryString = string.Format("Select Distinct GroupSeqID, PdbID From {0} " + 
                " Where GroupSeqID >= {1} AND GroupSeqID <= {2};",
                        GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], startGroupId, endGroupId);

            DataTable groupSgEntryTable = ProtCidSettings.protcidQuery.Query( queryString);

            Dictionary<int, List<string>> groupSgEntryHash = GetGroupSgEntryHash(groupSgEntryTable);

            return groupSgEntryHash;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupSgEntryTable"></param>
        /// <returns>key: group id, value: a list of entries which representing a list of space groups in the group</returns>
        private Dictionary<int, List<string>> GetGroupSgEntryHash(DataTable groupSgEntryTable)
        {
            Dictionary<int, List<string>> groupSgEntryHash = new Dictionary<int, List<string>>();
            foreach (DataRow dRow in groupSgEntryTable.Rows)
            {
                int groupId = Convert.ToInt32(dRow["GroupSeqID"].ToString());
                string pdbId = dRow["PdbID"].ToString();
                if (!groupSgEntryHash.ContainsKey(groupId))
                {
                    List<string> pdbList = new List<string> ();
                    pdbList.Add(pdbId);
                    groupSgEntryHash.Add(groupId, pdbList);
                }
                else
                {
                    if (!groupSgEntryHash[groupId].Contains(pdbId))
                    {
                        groupSgEntryHash[groupId].Add(pdbId);
                    }
                }
            }
            return groupSgEntryHash;
        }
        #endregion

        /// <summary>
		/// process all the interfaces for the crystal of the entry
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="interfaces"></param>
		private void ProcessInterfaces (int groupId, string  pdbId, ProtInterfaceInfo[] interfacesInfo)
		{
			try
			{
				bool needAsaUpdated = false;
				ProtInterface[] crystInterfaces = interfaceWriter.GenerateInterfaceFiles 
					(pdbId, interfacesInfo, "cryst", out needAsaUpdated);
				if (needAsaUpdated)
				{
					UpdateSurfaceAreaInDb (pdbId, interfacesInfo);
				}
			}
			catch (Exception ex)
			{
				ProtCidSettings.progressInfo.progStrQueue.Enqueue 
					("Processing " + groupId + "_" + pdbId + " interfaces error:" + ex.Message);
                ProtCidSettings.logWriter.WriteLine("Processing " + groupId + "_" + pdbId + " interfaces error:" + ex.Message);
			}
		}

		/// <summary>
		/// update surface area field for interfaces in database
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="interfaceInfoList"></param>
		private void UpdateSurfaceAreaInDb (string pdbId, ProtInterfaceInfo[] interfaceInfoList)
		{
			string tableName = ProtCidSettings.dataType + "SgInterfaces";
			string updateString = "";
			foreach (ProtInterfaceInfo protInterface in interfaceInfoList)
			{				
				updateString = string.Format ("Update {0} SET SurfaceArea = {1} " + 
					" Where PdbID = '{2}' AND InterfaceID = {3};", 
					tableName, protInterface.ASA, pdbId, protInterface.InterfaceId);
                dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
				updateString = string.Format ("Update CrystEntryInterfaces SET SurfaceArea = {0} " + 
					" Where PdbID = '{1}' AND InterfaceID = {2};", 
					protInterface.ASA, pdbId, protInterface.InterfaceId);
                dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
			}
		}
		#endregion

		#region format cryst interface
		/// <summary>
		/// the resiudes and seq ids for the interface
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="interfaceId"></param>
		/// <returns></returns>
		private Dictionary<string, string> GetResidueSeqs (DataTable residueTable, int interfaceId, int chain)
		{
            Dictionary<string, string> residueSeqHash = new Dictionary<string, string>();
			DataRow[] residueRows = 
				residueTable.Select (string.Format (" InterfaceID = '{0}' AND ChainNum = '{1}'", interfaceId, chain));
			foreach (DataRow residueRow in residueRows)
			{
				if (! residueSeqHash.ContainsKey (residueRow["SeqID"].ToString ().Trim ()))
				{
					residueSeqHash.Add (residueRow["SeqID"].ToString ().Trim (), residueRow["Residue"].ToString ().Trim ());
				}
			}
			return residueSeqHash;
		}

		/// <summary>
		/// the resiudes and seq ids for the interface
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="interfaceId"></param>
		/// <returns></returns>
        private Dictionary<string, string>[] GetResidueSeqs(DataTable residueTable, int interfaceId)
		{
            Dictionary<string, string> residueSeqHash1 = new Dictionary<string, string>();
            Dictionary<string, string> residueSeqHash2 = new Dictionary<string, string>();
			DataRow[] residueRows = 
				residueTable.Select (string.Format (" InterfaceID = '{0}'", interfaceId));
			foreach (DataRow residueRow in residueRows)
			{
				if (! residueSeqHash1.ContainsKey (residueRow["SeqID1"].ToString ().Trim ()))
				{
					residueSeqHash1.Add (residueRow["SeqID1"].ToString ().Trim (), residueRow["Residue1"].ToString ().Trim ());
				}
				if (! residueSeqHash2.ContainsKey (residueRow["SeqID2"].ToString ().Trim ()))
				{
					residueSeqHash2.Add (residueRow["SeqID2"].ToString ().Trim (), residueRow["Residue2"].ToString ().Trim ());
				}
			}
            Dictionary<string, string>[] residueSeqHashes = new Dictionary<string, string>[2];
			residueSeqHashes[0] = residueSeqHash1;
			residueSeqHashes[1] = residueSeqHash2;
			return residueSeqHashes;
		}

		/// <summary>
		/// format the remark
		/// </summary>
		/// <param name="interfaceRow"></param>
		/// <param name="existNumOfSg"></param>
		/// <param name="totalNumOfSg"></param>
		/// <returns></returns>
		private string FormatRemark (DataRow interfaceRow, int existNumOfSg, int totalNumOfSg)
		{
			string remarkLine = "";
			// chain A
			string symmetryString1 = interfaceRow["FullSymmetryString1"].ToString ().Trim ();
			string symmetryString2 = interfaceRow["FullSymmetryString2"].ToString ().Trim ();
			
			remarkLine += "Remark 300 Interface Chain A For ";			
			remarkLine += ("Asymmetric Chain " + interfaceRow["AsymChain1"].ToString ().Trim () + " ");
			remarkLine += ("Author Chain " + interfaceRow["AuthChain1"].ToString ().Trim () + " ");
			remarkLine += ("Entity " + interfaceRow["EntityID1"].ToString () + " ");		
			remarkLine += ("Symmetry Operator    " + symmetryString1);
			remarkLine += "\r\n";
			// chain B
			remarkLine += "Remark 300 Interface Chain B For ";
			remarkLine += ("Asymmetric Chain " + interfaceRow["AsymChain2"].ToString ().Trim () + " ");
			remarkLine += ("Author Chain " +  interfaceRow["AuthChain2"].ToString ().Trim () + " ");
			remarkLine += ("Entity " + interfaceRow["EntityID2"].ToString () + " ");
			remarkLine += ("Symmetry Operator    " + symmetryString2);
			remarkLine += "\r\n";
			// number of copies
			remarkLine += ("Remark 300 Number of entries with this interface in the group: " + existNumOfSg + "/" + totalNumOfSg);
			remarkLine += "\r\n"; 

			return remarkLine;
		}

		/// <summary>
		/// format the remark
		/// </summary>
		/// <param name="interfaceRow"></param>
		/// <param name="existNumOfSg"></param>
		/// <param name="totalNumOfSg"></param>
		/// <returns></returns>
		private string FormatRemark (DataRow interfaceRow)
		{
			string remarkLine = "";
			// chain A
			remarkLine += "Remark 300 Interface Chain A For ";			
			remarkLine += ("Asymmetric Chain " + interfaceRow["AsymChain1"].ToString ().Trim () + " ");
			remarkLine += ("Author Chain " + interfaceRow["AuthChain1"].ToString ().Trim () + " ");
			remarkLine += ("Entity " + interfaceRow["EntityID1"].ToString () + " ");
            remarkLine += ("Symmetry Operator    " + interfaceRow["SymmetryString1"].ToString().TrimEnd() + " ");
            remarkLine += ("Full Symmetry Operator    " + interfaceRow["FullSymmetryString1"].ToString().TrimEnd() + " ");
                
			remarkLine += "\r\n";
			// chain B
			remarkLine += "Remark 300 Interface Chain B For ";
			remarkLine += ("Asymmetric Chain " + interfaceRow["AsymChain2"].ToString ().Trim () + " ");
			remarkLine += ("Author Chain " +  interfaceRow["AuthChain2"].ToString ().Trim () + " ");
			remarkLine += ("Entity " + interfaceRow["EntityID2"].ToString () + " ");
			remarkLine += ("Symmetry Operator    " + interfaceRow["SymmetryString2"].ToString ().TrimEnd () + " ");
            remarkLine += ("Full Symmetry Operator    " + interfaceRow["FullSymmetryString2"].ToString().TrimEnd());
			remarkLine += "\r\n";

			return remarkLine;
		}
		#endregion

		#region generate interface files for a given pdb list

		#region representative entries
		public void GenerateRepEntryInterfaceFiles ()
		{
			string queryString = string.Format ("Select Distinct PdbID From {0} ORDER BY GroupSeqID;", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces]);
            DataTable repEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
			string[] repPdbs = new string [repEntryTable.Rows.Count];
			int i = 0;
			foreach (DataRow dRow in repEntryTable.Rows)
			{
				repPdbs[i] = dRow["PdbID"].ToString ();
				i ++;
			}
			GenerateRepEntryInterfaceFiles (repPdbs);
		}
		/// <summary>
		/// print all the crystal interfaces occur in at leat two different space groups
		/// from database into pdb formatted files
		/// </summary>
		public void GenerateRepEntryInterfaceFiles (string[] pdbList)
		{	
			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
			ProtCidSettings.progressInfo.currentOperationLabel = "Generate Rep Interface Files";
			ProtCidSettings.progressInfo.totalStepNum = pdbList.Length;
			ProtCidSettings.progressInfo.totalOperationNum = pdbList.Length;
			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Generate representative entry interface files.");

			StreamWriter asaWriter = new StreamWriter ("RepEntryInterfaceAsa.txt");

            List<ProtInterfaceInfo> interfaceList = new List<ProtInterfaceInfo> ();
	//		int totalSgNum = 0;
	//		Hashtable groupSgNumHash = new Hashtable ();
			int groupId = -1;
			foreach (string pdbId in pdbList)
			{
				ProtCidSettings.progressInfo.currentOperationNum ++;
				ProtCidSettings.progressInfo.currentStepNum ++;
				ProtCidSettings.progressInfo.currentFileName = pdbId;

				string interfaceDefQueryString = string.Format ("Select * From CrystEntryInterfaces " +
					" Where PdbID = '{0}' ORDER BY InterfaceID;", pdbId);
                DataTable interfaceTable = ProtCidSettings.protcidQuery.Query( interfaceDefQueryString);
		//		string sgInterfaceQueryString = string.Format ("Select * From {0} WHERE PdbID = '{1}' AND SurfaceArea > 0;", 
				string sgInterfaceQueryString = string.Format ("Select * From {0} WHERE PdbID = '{1}';", 
					GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], pdbId);
                DataTable sgInterfaceTable = ProtCidSettings.protcidQuery.Query( sgInterfaceQueryString);
				if (sgInterfaceTable.Rows.Count == 0)
				{
					continue;
				}
				groupId = Convert.ToInt32 (sgInterfaceTable.Rows[0]["GroupSeqID"].ToString ());
		//		totalSgNum = GetTotalSgNum (groupId, ref groupSgNumHash);
				interfaceList.Clear ();
				
				string residueQuery = string.Format ("SELECT * FROM SgInterfaceResidues WHERE PDBID = '{0}';", pdbId);
                DataTable residueTable = ProtCidSettings.protcidQuery.Query( residueQuery);

				foreach (DataRow interfaceRow in interfaceTable.Rows)
				{					
					int interfaceId = Convert.ToInt32 (interfaceRow["InterfaceID"].ToString ());
					DataRow[] sgInterfaceRows = sgInterfaceTable.Select (string.Format ("InterfaceID = {0}", interfaceId));
					if (sgInterfaceRows.Length > 0)
					{
						asaWriter.WriteLine (pdbId + "	" + interfaceId.ToString () + "	" + 
							string.Format ("{0:0.00}", Convert.ToDouble (sgInterfaceRows[0]["SurfaceArea"].ToString ())));
						continue;
					}
					ProtInterfaceInfo interfaceInfo = GetInterfaceInfo 
						(interfaceId, interfaceRow, residueTable, -1);
					interfaceList.Add (interfaceInfo);	
				}
				if (interfaceList.Count > 0)
				{
					ProtInterfaceInfo[] interfacesInfo = new ProtInterfaceInfo [interfaceList.Count];
					interfaceList.CopyTo (interfacesInfo);
					bool needAsaUpdate = false;
					try
					{
						ProtInterface[] crystInterfaces = interfaceWriter.GenerateInterfaceFiles 
							(pdbId, interfacesInfo, "cryst", out needAsaUpdate);
					}
					catch (Exception ex)
					{
						ProtCidSettings.progressInfo.progStrQueue.Enqueue (ex.Message);
                        ProtCidSettings.logWriter.WriteLine(ex.Message);
						continue;
					}
					foreach (ProtInterfaceInfo interfaceInfo in interfacesInfo)
					{
						asaWriter.WriteLine (pdbId + "	" + interfaceInfo.InterfaceId + "	" + interfaceInfo.ASA);
					}
				}	
				asaWriter.Flush ();
			}
		    asaWriter.Close ();
			ProtCidSettings.progressInfo.progStrQueue.Enqueue  ("Done!");
			ProtCidSettings.progressInfo.threadFinished = true;
		}

		/// <summary>
		/// total number of crystal forms for this group
		/// </summary>
		/// <param name="groupId"></param>
		/// <param name="groupSgNumHash"></param>
		/// <returns></returns>
		private int GetTotalSgNum (int groupId, ref Dictionary<int, int> groupSgNumHash)
		{
			if (groupSgNumHash.ContainsKey (groupId))
			{
				return groupSgNumHash[groupId];
			}
			string queryString = string.Format ("Select Distinct PdbID From {0} Where GroupSeqID = {1};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.SgInterfaces], groupId);
            DataTable crystFormTable = ProtCidSettings.protcidQuery.Query( queryString);
			groupSgNumHash.Add (groupId, crystFormTable.Rows.Count);
			return crystFormTable.Rows.Count;
		}

		/// <summary>
		/// get interface from db
		/// </summary>
		/// <param name="interfaceId"></param>
		/// <param name="interfaceRow"></param>
		/// <param name="interfaceDefRow"></param>
		/// <param name="residueTable"></param>
		/// <returns></returns>
		private ProtInterfaceInfo GetInterfaceInfo (int interfaceId, DataRow interfaceDefRow, 
			DataTable residueTable, int totalSgNum)
		{
			ProtInterfaceInfo interfaceInfo = new ProtInterfaceInfo ();
			interfaceInfo.InterfaceId = interfaceId;
			interfaceInfo.ASA = -1.0;
			interfaceInfo.Chain1 = interfaceDefRow["AsymChain1"].ToString ().Trim ();
			interfaceInfo.Chain2 = interfaceDefRow["AsymChain2"].ToString ().Trim ();
			interfaceInfo.SymmetryString1 = interfaceDefRow["SymmetryString1"].ToString ().Trim ();
			interfaceInfo.SymmetryString2 = interfaceDefRow["SymmetryString2"].ToString ().Trim ();
			/*		interfaceInfo.Remark = FormatRemark (interfaceDefRow, 
						Convert.ToInt32 (interfaceRow["NumOfSg"].ToString ()), totalSgNum);*/
			Dictionary<string, string>[] residueSeqHashes = GetResidueSeqs (residueTable, interfaceId);
			interfaceInfo.ResiduesInChain1 = residueSeqHashes[0];
			interfaceInfo.ResiduesInChain2 = residueSeqHashes[1];
			interfaceInfo.Remark += interfaceInfo.FormatResidues ("A");
			interfaceInfo.Remark += interfaceInfo.FormatResidues ("B");
			return interfaceInfo;
		}
		#endregion

		#region regular entries
		/// <summary>
		/// print all crystal interfaces and update surface areas
		/// </summary>
		public void GenerateEntryInterfaceFiles (string[] pdbList)
		{	
			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
			ProtCidSettings.progressInfo.currentOperationLabel = "Generate Entry Interface Files";
			ProtCidSettings.progressInfo.totalStepNum = pdbList.Length;
			ProtCidSettings.progressInfo.totalOperationNum = pdbList.Length;
			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Generate entry interface files.");

			foreach (string pdbId in pdbList)
			{
				ProtCidSettings.progressInfo.currentOperationNum ++;
				ProtCidSettings.progressInfo.currentStepNum ++;
				ProtCidSettings.progressInfo.currentFileName = pdbId;
              
                DeleteObsInterfaceFiles(pdbId);
                GenerateEntryInterfaceFiles(pdbId);
			}
			ProtCidSettings.progressInfo.progStrQueue.Enqueue  ("Done!");
        }

        /// <summary>
        /// print all crystal interfaces and update surface areas
        /// </summary>
        public void GenerateMissingEntryInterfaceFiles(string[] pdbList)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Generate Entry Interface Files";
            ProtCidSettings.progressInfo.totalStepNum = pdbList.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pdbList.Length;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate entry interface files.");

            foreach (string pdbId in pdbList)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

         //       DeleteObsInterfaceFiles(pdbId);
                GenerateMissingEntryInterfaceFiles(pdbId);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
		/// update surface area field for interfaces in database
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="interfaceInfoList"></param>
		private void UpdateSurfaceAreaInInterfaceDef (string pdbId, ProtInterfaceInfo[] interfaceInfoList)
		{
			string updateString = "";
			foreach (ProtInterfaceInfo protInterface in interfaceInfoList)
			{				
				updateString = string.Format ("Update CrystEntryInterfaces SET SurfaceArea = {0} " + 
					" Where PdbID = '{1}' AND InterfaceID = {2};", 
					protInterface.ASA, pdbId, protInterface.InterfaceId);
                dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
			}
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbList"></param>
        public int[] GenerateEntryInterfaceFiles(string pdbId, bool needLigandInfo, string interfaceFileDir)
        {
            List<int> interfaceEntityList = new List<int> ();
            List<ProtInterfaceInfo> interfaceList = new List<ProtInterfaceInfo> ();
            string interfaceDefQueryString = string.Format("Select * From CrystEntryInterfaces " +
                //		" Where PdbID = '{0}' AND SurfaceArea < 0 ORDER BY InterfaceID;", pdbId);
                " Where PdbID = '{0}' ORDER BY InterfaceID;", pdbId);
            DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( interfaceDefQueryString);

            int interfaceId = 0;
            int entityId1 = 0;
            int entityId2 = 0;
            foreach (DataRow interfaceDefRow in interfaceDefTable.Rows)
            {
                interfaceId = Convert.ToInt32(interfaceDefRow["InterfaceID"].ToString());
                ProtInterfaceInfo interfaceInfo = GetInterfaceInfo(interfaceId, interfaceDefRow);
                interfaceList.Add(interfaceInfo);

                entityId1 = Convert.ToInt32(interfaceDefRow["EntityID1"].ToString ());
                entityId2 = Convert.ToInt32(interfaceDefRow["EntityID2"].ToString ());
                if (!interfaceEntityList.Contains(entityId1))
                {
                    interfaceEntityList.Add(entityId1);
                }
                if (!interfaceEntityList.Contains(entityId2))
                {
                    interfaceEntityList.Add(entityId2);
                }
            }
            if (interfaceList.Count > 0)
            {
                ProtInterfaceInfo[] interfacesInfos = new ProtInterfaceInfo[interfaceList.Count];
                interfaceList.CopyTo(interfacesInfos);
                bool isAsuInterface = false;
                try
                {
                    ProtInterface[] crystInterfaces = interfaceWriter.GenerateInterfaceFiles
                        (pdbId, interfacesInfos, "cryst", needLigandInfo, interfaceFileDir, isAsuInterface);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message + ": " + pdbId);
                    ProtCidSettings.logWriter.WriteLine(ex.Message + ": " + pdbId);
                }
            }
            int[] interfaceEntities = new int[interfaceEntityList.Count];
            interfaceEntityList.CopyTo(interfaceEntities);
            return interfaceEntities;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbList"></param>
        public void GenerateEntryInterfaceFiles(string pdbId)
        {
            List<ProtInterfaceInfo> interfaceList = new List<ProtInterfaceInfo> ();
            string interfaceDefQueryString = string.Format("Select * From CrystEntryInterfaces " +
                	//	" Where PdbID = '{0}' AND SurfaceArea < 0 ORDER BY InterfaceID;", pdbId);
                " Where PdbID = '{0}' ORDER BY InterfaceID;", pdbId);
            DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( interfaceDefQueryString);

            foreach (DataRow interfaceDefRow in interfaceDefTable.Rows)
            {
                int interfaceId = Convert.ToInt32(interfaceDefRow["InterfaceID"].ToString());

                ProtInterfaceInfo interfaceInfo = GetInterfaceInfo(interfaceId, interfaceDefRow);
                interfaceList.Add(interfaceInfo);
            }
            if (interfaceList.Count > 0)
            {
                ProtInterfaceInfo[] interfacesInfos = new ProtInterfaceInfo[interfaceList.Count];
                interfaceList.CopyTo(interfacesInfos);
                bool needAsaUpdate = false;
                try
                {
                    ProtInterface[] crystInterfaces = interfaceWriter.GenerateInterfaceFiles
                        (pdbId, interfacesInfos, "cryst", out needAsaUpdate);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message + ": " + pdbId);
                    ProtCidSettings.logWriter.WriteLine(ex.Message + ": " + pdbId);
                }
                if (needAsaUpdate)
                {
                    UpdateSurfaceAreaInInterfaceDef(pdbId, interfacesInfos);
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbList"></param>
        public void GenerateEntryInterfaceFiles(string pdbId, int[] interfaceIds, string interfaceFileDir)
        {
            List<ProtInterfaceInfo> interfaceList = new List<ProtInterfaceInfo> ();
            string interfaceDefQueryString = string.Format("Select * From CrystEntryInterfaces " +
                " Where PdbID = '{0}' AND InterfaceID IN ({1});", pdbId, ParseHelper.FormatSqlListString (interfaceIds));
            DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( interfaceDefQueryString);

            foreach (DataRow interfaceDefRow in interfaceDefTable.Rows)
            {
                int interfaceId = Convert.ToInt32(interfaceDefRow["InterfaceID"].ToString());

                ProtInterfaceInfo interfaceInfo = GetInterfaceInfo(interfaceId, interfaceDefRow);
                interfaceList.Add(interfaceInfo);
            }
            if (interfaceList.Count > 0)
            {
                ProtInterfaceInfo[] interfacesInfos = new ProtInterfaceInfo[interfaceList.Count];
                interfaceList.CopyTo(interfacesInfos);
                bool needAsaUpdate = false;
                try                 
                {
                    ProtInterface[] crystInterfaces = interfaceWriter.GenerateInterfaceFiles(pdbId, interfacesInfos, "cryst", true, interfaceFileDir, false);
             /*       ProtInterface[] crystInterfaces = interfaceWriter.GenerateInterfaceFiles
                        (pdbId, interfacesInfos, "cryst", out needAsaUpdate);*/
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message + ": " + pdbId);
                    ProtCidSettings.logWriter.WriteLine(ex.Message + ": " + pdbId);
                }
                if (needAsaUpdate)
                {
                    UpdateSurfaceAreaInInterfaceDef(pdbId, interfacesInfos);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbList"></param>
        public void GenerateMissingEntryInterfaceFiles(string pdbId)
        {
            List<ProtInterfaceInfo> interfaceList = new List<ProtInterfaceInfo> ();
            string interfaceDefQueryString = string.Format("Select * From CrystEntryInterfaces " +
                        " Where PdbID = '{0}' AND SurfaceArea = -1 ORDER BY InterfaceID;", pdbId);
            //  " Where PdbID = '{0}' ORDER BY InterfaceID;", pdbId);
            DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( interfaceDefQueryString);

            foreach (DataRow interfaceDefRow in interfaceDefTable.Rows)
            {
                int interfaceId = Convert.ToInt32(interfaceDefRow["InterfaceID"].ToString());

                ProtInterfaceInfo interfaceInfo = GetInterfaceInfo(interfaceId, interfaceDefRow);
                interfaceList.Add(interfaceInfo);
            }
            if (interfaceList.Count > 0)
            {
                ProtInterfaceInfo[] interfacesInfos = new ProtInterfaceInfo[interfaceList.Count];
                interfaceList.CopyTo(interfacesInfos);
                bool needAsaUpdate = false;
                try
                {
                    ProtInterface[] crystInterfaces = interfaceWriter.GenerateInterfaceFiles
                        (pdbId, interfacesInfos, "cryst", out needAsaUpdate);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message + ": " + pdbId);
                    ProtCidSettings.logWriter.WriteLine(ex.Message + ": " + pdbId);
                }
                if (needAsaUpdate)
                {
                    UpdateSurfaceAreaInInterfaceDef(pdbId, interfacesInfos);
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbList"></param>
        public void GenerateMissingEntryInterfaceFiles(Dictionary<string, int[]> entryInterfaceIdHash)
        {
            List<ProtInterfaceInfo> interfaceList = new List<ProtInterfaceInfo> ();
            bool needAsaUpdate = false;
            foreach (string pdbId in entryInterfaceIdHash.Keys)
            {
                DataTable interfaceDefTable = GetEntryInterfaceDefTable(pdbId);

         //       ArrayList interfaceIdList = (ArrayList)entryInterfaceIdHash[pdbId];
                int[] interfaceIds = (int[])entryInterfaceIdHash[pdbId];
                interfaceList.Clear();
                foreach (int interfaceId in interfaceIds)
                {
                    DataRow[] interfaceRows = interfaceDefTable.Select("InterfaceID = " + interfaceId);
                    ProtInterfaceInfo interfaceInfo = GetInterfaceInfo(interfaceId, interfaceRows[0]);
                    interfaceList.Add(interfaceInfo);
                }
                if (interfaceList.Count > 0)
                {
                    ProtInterfaceInfo[] interfacesInfos = new ProtInterfaceInfo[interfaceList.Count];
                    interfaceList.CopyTo(interfacesInfos);
                    needAsaUpdate = false;
                    try
                    {
                        ProtInterface[] crystInterfaces = interfaceWriter.GenerateInterfaceFiles
                            (pdbId, interfacesInfos, "cryst", out needAsaUpdate);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message + ": " + pdbId);
                        ProtCidSettings.logWriter.WriteLine(ex.Message + ": " + pdbId);
                    }
                    if (needAsaUpdate)
                    {
                        UpdateSurfaceAreaInInterfaceDef(pdbId, interfacesInfos);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryInterfaceDefTable(string pdbId)
        {
            string queryString = string.Format("Select * From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( queryString);
            return interfaceDefTable;
        }
		#endregion

        #region interface files in asu between small peptides and specific peptides in asu
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="needLigandInfo"></param>
        /// <param name="interfaceFileDir"></param>
        public void GenerateCrystAndAsuInterfaceFiles(string pdbId, int[] pfamEntities, bool needLigandInfo, string interfaceFileDir)
        {
            int[] interfaceEntities = GenerateEntryInterfaceFiles(pdbId, needLigandInfo, interfaceFileDir);

            InterfaceChains[] leftHeteroAsuInterfaces = GetLeftHeteroInteractionsInAsu(pdbId, interfaceEntities, pfamEntities);
            if (leftHeteroAsuInterfaces.Length == 0)
            {
                return;
            }
            ProtInterfaceInfo[] protInterfaceInfos = new ProtInterfaceInfo[leftHeteroAsuInterfaces.Length];
            int count = 0;
            DataTable asuTable = GetEntryAsuTable(pdbId);
            foreach (InterfaceChains heteroInterface in leftHeteroAsuInterfaces)
            {
                protInterfaceInfos[count] = GetInterfaceInfo (heteroInterface, asuTable);
                count++;
            }
            bool isAsuInterface = true;
            try
            {
                ProtInterface[] crystInterfaces = interfaceWriter.GenerateInterfaceFiles
                    (pdbId, protInterfaceInfos, "cryst", needLigandInfo, interfaceFileDir, isAsuInterface);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message + ": " + pdbId);
                ProtCidSettings.logWriter.WriteLine(ex.Message + ": " + pdbId);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public DataTable GetEntryAsuTable(string pdbId)
        {
            string queryString = string.Format("Select * From AsymUnit Where PdbID = '{0}';", pdbId);
            DataTable asuTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return asuTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entryEntities"></param>
        /// <param name="existEntities"></param>
        public InterfaceChains[] GetLeftHeteroInteractionsInAsu(string pdbId, int[] existEntities,
            int[] pkinaseEntities)
        {
            string asuXmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string asuFile = ParseHelper.UnZipFile(asuXmlFile, ProtCidSettings.tempDir);
            Dictionary<string, AtomInfo[]> asuHash = asuInterfaces.GetAsuFromXml(asuFile);
            InterfaceChains[] asuInteractions = asuInterfaces.GetAllInteractionsInAsu(pdbId, asuHash);
            Dictionary<string, int> asymChainEntityHash = GetAsymChainEntityHash(pdbId);
            string asymChain1 = "";
            string asymChain2 = "";
            int entityId1 = 0;
            int entityId2 = 0;
            List<InterfaceChains> leftInteractionList = new List<InterfaceChains> ();
       //     ArrayList protInterfaceInfoList = new ArrayList();
            foreach (InterfaceChains asuInteraction in asuInteractions)
            {
                asymChain1 = GetAsymChainFromSymOpString(asuInteraction.firstSymOpString);
                asymChain2 = GetAsymChainFromSymOpString(asuInteraction.secondSymOpString);
                if (asymChain1 == asymChain2)
                {
                    continue;
                }
                entityId1 = (int)asymChainEntityHash[asymChain1];
                entityId2 = (int)asymChainEntityHash[asymChain2];
                if (entityId1 == entityId2)
                {
                    continue;
                }
                if (Array.IndexOf(existEntities, entityId1) > -1 &&
                    Array.IndexOf(existEntities, entityId2) > -1)
                {
                    continue;
                }
                if ((Array.IndexOf(pkinaseEntities, entityId1) > -1 &&
                    Array.IndexOf(pkinaseEntities, entityId2) < 0) ||
                    (Array.IndexOf(pkinaseEntities, entityId1) < 0 &&
                    Array.IndexOf(pkinaseEntities, entityId2) > -1))
                {
                    asuInteraction.entityId1 = entityId1;
                    asuInteraction.entityId2 = entityId2;
                    leftInteractionList.Add(asuInteraction);
                }
            }
            return leftInteractionList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symOpString"></param>
        /// <returns></returns>
        private string GetAsymChainFromSymOpString(string symOpString)
        {
            string[] fields = symOpString.Split('_');
            return fields[0];
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<string, int> GetAsymChainEntityHash(string pdbId)
        {
            string queryString = string.Format("Select EntityID, AsymID From AsymUnit " +
                " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable entityChainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            Dictionary<string, int> asymChainEntityHash = new Dictionary<string, int>();
            int entityId = 0;
            string asymChain = "";
            foreach (DataRow chainRow in entityChainTable.Rows)
            {
                entityId = Convert.ToInt32(chainRow["EntityID"].ToString());
                asymChain = chainRow["AsymID"].ToString().TrimEnd();
                asymChainEntityHash.Add(asymChain, entityId);
            }
            return asymChainEntityHash;
        }
        #endregion
		#endregion 

        #region hetero-dimer with different pfam architectures
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateHeteroInterfaceListFile"></param>
        public void GenerateDifPfamArchEntryInterfaceFiles(string updateHeteroInterfaceListFile)
        {
            Dictionary<string, string[]> updateEntryInterfaceHash = ReadUpdateDifPfamArchEntryInterfaces(updateHeteroInterfaceListFile);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Generate Entry Interface Files";
            ProtCidSettings.progressInfo.totalStepNum = updateEntryInterfaceHash.Count;
            ProtCidSettings.progressInfo.totalOperationNum = updateEntryInterfaceHash.Count;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate entry interface files.");

            List<ProtInterfaceInfo> interfaceList = new List<ProtInterfaceInfo> ();

            foreach (string pdbId in updateEntryInterfaceHash.Keys)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                string[] updateInterfaceIds = (string[])updateEntryInterfaceHash[pdbId];
                string interfaceDefQueryString = string.Format("Select * From CrystEntryInterfaces " +
                    " Where PdbID = '{0}' AND InterfaceID in ({1});", pdbId, ParseHelper.FormatSqlListString(updateInterfaceIds));
                DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( interfaceDefQueryString);

                interfaceList.Clear();

                foreach (DataRow interfaceDefRow in interfaceDefTable.Rows)
                {
                    int interfaceId = Convert.ToInt32(interfaceDefRow["InterfaceID"].ToString());

                    ProtInterfaceInfo interfaceInfo = GetInterfaceInfo(interfaceId, interfaceDefRow);
                    interfaceList.Add(interfaceInfo);
                }
                if (interfaceList.Count > 0)
                {
                    ProtInterfaceInfo[] interfacesInfo = new ProtInterfaceInfo[interfaceList.Count];
                    interfaceList.CopyTo(interfacesInfo);
                    bool needAsaUpdate = false;
                    try
                    {
                        ProtInterface[] crystInterfaces = interfaceWriter.GenerateInterfaceFiles
                            (pdbId, interfacesInfo, "cryst", out needAsaUpdate);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message + ": " + pdbId);
                        ProtCidSettings.logWriter.WriteLine(ex.Message + ": " + pdbId);
                        continue;
                    }
                    if (needAsaUpdate)
                    {
                        UpdateSurfaceAreaInInterfaceDef(pdbId, interfacesInfo);
                    }
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, string[]> ReadUpdateDifPfamArchEntryInterfaces(string updateHeteroInterfaceListFile)
        {
       //     StreamReader dataReader = new StreamReader("UpdateHeteroInterfaces.txt");
            StreamReader dataReader = new StreamReader(updateHeteroInterfaceListFile);
            Dictionary<string, string[]> updateInterfaceHash = new Dictionary<string, string[]>();
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                string[] interfaceIds = new string[fields.Length - 1];
                Array.Copy(fields, 1, interfaceIds, 0, fields.Length - 1);
                updateInterfaceHash.Add(fields[0], interfaceIds);
            }
            dataReader.Close();
            return updateInterfaceHash;
        }
        #endregion

        #region reverse interface chains
        /// <summary>
        /// 
        /// </summary>
        public void ReverseInterfaceChains(string reverseInterfaceFile)
        {
            Dictionary<string, List<int>> reverseEntryInterfaceHash = ReadReverseEntryInterfaces(reverseInterfaceFile);
            ProtCidSettings.dirSettings.interfaceFilePath = ProtCidSettings.dirSettings.interfaceFilePath + "\\ReversedInterfaces";

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Generate Entry Interface Files";
            ProtCidSettings.progressInfo.totalStepNum = reverseEntryInterfaceHash.Count;
            ProtCidSettings.progressInfo.totalOperationNum = reverseEntryInterfaceHash.Count;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate reversed entry interface files.");

            List<ProtInterfaceInfo> interfaceList = new List<ProtInterfaceInfo> ();
            string interfaceFile = "";
            string hashDir = "";

            foreach (string pdbId in reverseEntryInterfaceHash.Keys)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                string interfaceDefQueryString = string.Format("Select * From CrystEntryInterfaces " +
                    " Where PdbID = '{0}' AND InterfaceID in ({1});", pdbId, ParseHelper.FormatSqlListString(reverseEntryInterfaceHash[pdbId].ToArray ()));
                DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( interfaceDefQueryString);

                interfaceList.Clear();

                foreach (DataRow interfaceDefRow in interfaceDefTable.Rows)
                {
                    int interfaceId = Convert.ToInt32(interfaceDefRow["InterfaceID"].ToString());
                    hashDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst\\" + pdbId.Substring(1, 2));
                    interfaceFile = Path.Combine(hashDir, pdbId + "_" + interfaceId.ToString () + ".cryst.gz");
                    if (File.Exists(interfaceFile))
                    {
                        continue;
                    }
                    ReverseInterfaceDefRow(interfaceDefRow);

                    ProtInterfaceInfo interfaceInfo = GetInterfaceInfo(interfaceId, interfaceDefRow);
                    interfaceList.Add(interfaceInfo);
                }
                if (interfaceList.Count > 0)
                {
                    ProtInterfaceInfo[] interfacesInfo = new ProtInterfaceInfo[interfaceList.Count];
                    interfaceList.CopyTo(interfacesInfo);
                    bool needAsaUpdate = false;
                    try
                    {
                        ProtInterface[] crystInterfaces = interfaceWriter.GenerateInterfaceFiles
                            (pdbId, interfacesInfo, "cryst", out needAsaUpdate);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message + ": " + pdbId);
                        ProtCidSettings.logWriter.WriteLine(ex.Message + ": " + pdbId);
                        continue;
                    }
                    if (needAsaUpdate)
                    {
                        UpdateSurfaceAreaInInterfaceDef(pdbId, interfacesInfo);
                    }
                }
            }
     //       ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            int reversedInterfaceDirIndex = ProtCidSettings.dirSettings.interfaceFilePath.IndexOf("\\ReversedInterfaces");
            ProtCidSettings.dirSettings.interfaceFilePath =
                ProtCidSettings.dirSettings.interfaceFilePath.Remove(reversedInterfaceDirIndex, "ReversedInterfaces".Length + 1);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceDefRow"></param>
        private void ReverseInterfaceDefRow(DataRow interfaceDefRow)
        {
            object temp = interfaceDefRow["AsymChain1"];
            interfaceDefRow["AsymChain1"] = interfaceDefRow["AsymChain2"];
            interfaceDefRow["AsymChain2"] = temp;
            temp = interfaceDefRow["AuthChain1"];
            interfaceDefRow["AuthChain1"] = interfaceDefRow["AuthChain2"];
            interfaceDefRow["AuthChain2"] = temp;
            temp = interfaceDefRow["EntityID1"];
            interfaceDefRow["EntityID1"] = interfaceDefRow["EntityID2"];
            interfaceDefRow["EntityID2"] = temp;
            temp = interfaceDefRow["SymmetryString1"];
            interfaceDefRow["SymmetryString1"] = interfaceDefRow["SymmetryString2"];
            interfaceDefRow["SymmetryString2"] = temp;
            temp = interfaceDefRow["FullSymmetryString1"];
            interfaceDefRow["FullSymmetryString1"] = interfaceDefRow["FullSymmetryString2"];
            interfaceDefRow["FullSymmetryString2"] = temp;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, List<int>> ReadReverseEntryInterfaces(string reverseInterfacesFile)
        {
       //     StreamReader dataReader = new StreamReader("ReverseInterfacesInCluster.txt");
            StreamReader dataReader = new StreamReader(reverseInterfacesFile);
            string line = "";
            Dictionary<string, List<int>> entryInterfaceHash = new Dictionary<string, List<int>>();
            string pdbId = "";
            int interfaceId = -1;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split(':');
                string[] reverseInterfaces = fields[1].Split(',');
                foreach (string reverseInterface in reverseInterfaces)
                {
                    if (reverseInterface == "")
                    {
                        continue;
                    }
                    pdbId = reverseInterface.Substring(0, 4);
                    interfaceId = Convert.ToInt32 (reverseInterface.Substring (4, reverseInterface.Length - 4));
                    if (entryInterfaceHash.ContainsKey(pdbId))
                    {
                        if (!entryInterfaceHash[pdbId].Contains(interfaceId))
                        {
                            entryInterfaceHash[pdbId].Add(interfaceId);
                        }
                    }
                    else
                    {
                        List<int> interfaceList = new List<int> ();
                        interfaceList.Add(interfaceId);
                        entryInterfaceHash.Add(pdbId, interfaceList);
                    }
                }
            }
            dataReader.Close();
            return entryInterfaceHash;
        }
        #endregion

        #region delete obs files
        /// <summary>
		/// delete obsolete files
		/// </summary>
		/// <param name="entryList"></param>
		private void DeleteObsInterfaceFiles (string[] entryList)
		{
			foreach (string entry in entryList)
			{
                DeleteObsInterfaceFiles(entry);
			}
		}

        private void DeleteObsInterfaceFiles(string pdbId)
        {
            string[] entryInterfaceFiles =
                                Directory.GetFiles(Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst"), pdbId + "*.cryst.gz");
            foreach (string interfaceFile in entryInterfaceFiles)
            {
                File.Delete(interfaceFile);
            }
        }
		#endregion

        #region for debug only

        public void GenerateMissingCrystInterfaces()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            ProtCidSettings.progressInfo.currentOperationLabel = "Update Cryst Interfaces";
        //    string[] updateEntries = GetMissingInterfaceEntries();
       //     Hashtable entryInterfaceHash = ReadEntryInterfacesFromLog ();
       /*     Hashtable entryInterfaceHash = new Hashtable();
            ArrayList interfaceList = new ArrayList ();
            interfaceList.Add (14);
            entryInterfaceHash.Add("2e67", interfaceList);*/
            Dictionary<string, int[]> entryInterfaceHash = GetMessEntryInterfaces();
            GenerateMissingEntryInterfaceFiles(entryInterfaceHash);
      //      GenerateMissingEntryInterfaceFiles (updateEntries);
        }

        private Dictionary<string, int[]> GetMessEntryInterfaces()
        {
            StreamWriter missingInterfaceFileWriter = new StreamWriter("missingInterfaceFiles.txt");
            string interfaceFileDir = @"D:\DbProjectData\InterfaceFiles_update\cryst";
            string queryString = "Select Distinct PdbID From CrystEntryInterfaces;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            Dictionary<string, int[]> entryInterfaceHash = new Dictionary<string, int[]>();
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                int[] notExistFileInterfaceIds = GetEntryInterfaceFilesNotExist(pdbId, interfaceFileDir);
                if (notExistFileInterfaceIds.Length > 0)
                {
                    entryInterfaceHash.Add(pdbId, notExistFileInterfaceIds);
                    missingInterfaceFileWriter.WriteLine(pdbId + "," + FormatInterfaceIds (notExistFileInterfaceIds));
                    missingInterfaceFileWriter.Flush();
                }
            }
            missingInterfaceFileWriter.Close();

       /*    DateTime dtCutoff = new DateTime(2013, 5, 6);
            string[] hashFolders = Directory.GetDirectories(interfaceFileDir);
            
           
            foreach (string hashFolder in hashFolders)
            {
                DateTime changeTime = Directory.GetLastWriteTime(hashFolder);
                if (DateTime.Compare(changeTime, dtCutoff) > 0)
                {
                    string[] interfaceFiles = Directory.GetFiles(hashFolder);
                    foreach (string interfaceFile in interfaceFiles)
                    {
                        FileInfo fileInfo = new FileInfo(interfaceFile);
                        if (DateTime.Compare(fileInfo.LastWriteTime, dtCutoff) > 0)
                        {
                            interfaceFileName = fileInfo.Name;
                            interfaceFileName = interfaceFileName.Substring (0, interfaceFileName.IndexOf (".cryst"));
                            pdbId = interfaceFileName.Substring(0, 4);
                            interfaceId = Convert.ToInt32(interfaceFileName.Substring(5, interfaceFileName.Length - 5));
                            if (entryInterfaceHash.ContainsKey(pdbId))
                            {
                                ArrayList interfaceIdList = (ArrayList)entryInterfaceHash[pdbId];
                                interfaceIdList.Add(interfaceId);
                            }
                            else
                            {
                                ArrayList interfaceIdList = new ArrayList();
                                interfaceIdList.Add(interfaceId);
                                entryInterfaceHash.Add(pdbId, interfaceIdList);
                            }
                        }
                    }
                }
            }*/
            return entryInterfaceHash;
        }

        private string FormatInterfaceIds(int[] interfaceIds)
        {
            string interfaceIdString = "";
            foreach (int interfaceId in interfaceIds)
            {
                interfaceIdString += (interfaceId.ToString() + ",");
            }
            interfaceIdString = interfaceIdString.TrimEnd(',');
            return interfaceIdString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceIds"></param>
        /// <param name="interfaceFileDir"></param>
        /// <returns></returns>
        private int[] GetEntryInterfaceFilesNotExist(string pdbId, string interfaceFileDir)
        {
            int[] interfaceIds = GetEntryInterfaceIds(pdbId);
            string hashFolder = Path.Combine(interfaceFileDir, pdbId.Substring(1, 2));
            string[] interfaceFiles = Directory.GetFiles(hashFolder, pdbId + "*");
            string interfaceName = "";
            List<int> notExistInterfaceIdList = new List<int> (interfaceIds);
            int fileInterfaceId = 0;
            foreach (string interfaceFile in interfaceFiles)
            {
                FileInfo fileInfo = new FileInfo(interfaceFile);
                interfaceName = fileInfo.Name.Replace(".gz", "");
                interfaceName = interfaceName.Replace(".cryst", "");
                string[] fields = interfaceName.Split('_');
                if (fields.Length == 2)
                {
                    fileInterfaceId = Convert.ToInt32(fields[1]);
                    notExistInterfaceIdList.Remove(fileInterfaceId);
                }
            }
            int[] notExistInterfaceIds = new int[notExistInterfaceIdList.Count];
            notExistInterfaceIdList.CopyTo(notExistInterfaceIds);
            return notExistInterfaceIds;
       }

        private int[] GetEntryInterfaceIds(string pdbId)
        {
            string queryString = string.Format("Select Distinct InterfaceID From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable interfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] interfaceIds = new int[interfaceIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow interfaceIdRow in interfaceIdTable.Rows)
            {
                interfaceIds[count] = Convert.ToInt32(interfaceIdRow["InterfaceId"].ToString ());
                count++;
            }
            return interfaceIds;
        }
        private Dictionary<string, List<int>> ReadEntryInterfacesFromLog()
        {
            Dictionary<string, List<int>> entryInterfaceHash = new Dictionary<string, List<int>>();
            StreamReader dataReader = new StreamReader("missCrystInterfaces.txt");
            string line = "";
            int fileIndex = 0;
            int exeIndex = 0;
            string interfaceFileName = "";
            string pdbId = "";
            int interfaceId = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                fileIndex = line.LastIndexOf("\\");
                if (fileIndex > -1)
                {
                    exeIndex = line.IndexOf(".cryst.gz");
                    interfaceFileName = line.Substring(fileIndex + 1, exeIndex - fileIndex - 1);
                    pdbId = interfaceFileName.Substring(0, 4);
                    interfaceId = Convert.ToInt32(interfaceFileName.Substring (5, interfaceFileName.Length - 5));
                    if (entryInterfaceHash.ContainsKey(pdbId))
                    {
                        if (!entryInterfaceHash[pdbId].Contains(interfaceId))
                        {
                            entryInterfaceHash[pdbId].Add(interfaceId);
                        }
                    }
                    else
                    {
                        List<int> interfaceIdList = new List<int> ();
                        interfaceIdList.Add(interfaceId);
                        entryInterfaceHash.Add(pdbId, interfaceIdList);
                    }
                }
                
            }
            dataReader.Close();
            return entryInterfaceHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetMissingInterfaceEntries ()
        {
 /*           StreamReader dataReader = new StreamReader("interfaceGenLog.txt");
            ArrayList entryList = new ArrayList();
            string line = "";
            int groupId = 0;
            string pdbId = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("Processing ") > -1)
                {
                    string[] fields = line.Split(' ');
                    string[] groupEntryFields = fields[1].Split('_');
                    groupId = Convert.ToInt32(groupEntryFields[0]);
                    pdbId = groupEntryFields[1];
                    if (!entryList.Contains(pdbId))
                    {
                        entryList.Add(pdbId);
                    }
                }
            }
            dataReader.Close();
            string[] updateEntries = new string[entryList.Count];
            entryList.CopyTo(updateEntries);
            return updateEntries;*/
            string queryString = "Select Distinct PdbID From CrystEntryInterfaces Where SurfaceArea = -1.0;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] updateEntries = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                updateEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return updateEntries;
        }
        #endregion
    }
}
