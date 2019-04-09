using System;
using System.Data;
using System.Collections.Generic;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;
using InterfaceClusterLib.DataTables;

namespace InterfaceClusterLib.Alignments
{
	/// <summary>
	/// Retrieve alignment info from precalculated alignments tables
	/// </summary>
	public class HomoEntryAlignInfo
	{
		#region member variables
		private DbQuery dbQuery = new DbQuery ();
		#endregion
		
		public HomoEntryAlignInfo()
		{
		}

		#region alignment info between representative entry and its homologies
		/// <summary>
		/// 
		/// </summary>
		/// <param name="groupId"></param>
		/// <param name="repPdbId"></param>
		/// <returns></returns>
		public DataTable GetHomoEntryAlignInfo (int groupId, string repPdbId)
		{
			string queryString = string.Format ("Select * From {0} Where GroupSeqID = {1} AND PdbID1 = '{2}';", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoRepEntryAlign], groupId, repPdbId);
			DataTable homoEntryAlignTable = dbQuery.Query (ProtCidSettings.protcidDbConnection, queryString);
			GetHomoEntryAlignAsymInfo (groupId, repPdbId, ref homoEntryAlignTable);

			return homoEntryAlignTable;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="repPdbId"></param>
        /// <returns></returns>
		public DataTable GetHomoEntryAlignInfo (string repPdbId)
		{
			int groupId = 0;

			string queryString = string.Format ("Select * From {0} Where PdbID1 = '{1}';", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoRepEntryAlign], repPdbId);
			DataTable homoEntryAlignTable = dbQuery.Query (ProtCidSettings.protcidDbConnection, queryString);
			if (homoEntryAlignTable.Rows.Count > 0)
			{
				groupId = Convert.ToInt32 (homoEntryAlignTable.Rows[0]["GroupSeqID"].ToString ());
			}
			GetHomoEntryAlignAsymInfo (groupId, repPdbId, ref homoEntryAlignTable);

			return homoEntryAlignTable;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="repPdbId"></param>
        /// <param name="homoEntry"></param>
        /// <returns></returns>
		public DataTable GetHomoEntryAlignInfo (string repPdbId, string homoEntry)
		{
			int groupId = 0;

			string queryString = string.Format ("Select * From {0} Where PdbID1 = '{1}' AND PdbID2 = '{2}';", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoRepEntryAlign], repPdbId, homoEntry);
            DataTable homoEntryAlignTable = ProtCidSettings.protcidQuery.Query( queryString);
			if (homoEntryAlignTable.Rows.Count > 0)
			{
				groupId = Convert.ToInt32 (homoEntryAlignTable.Rows[0]["GroupSeqID"].ToString ());
			}
			GetHomoEntryAlignAsymInfo (groupId, repPdbId, ref homoEntryAlignTable);

			return homoEntryAlignTable;
		}

		/// <summary>
		/// Add asymmetric chains into alignment table
		/// </summary>
		/// <param name="homoEntryList"></param>
		/// <param name="homoEntryAlignTable"></param>
		public void GetHomoEntryAlignInfo (string[] homoEntryList, ref DataTable homoEntryAlignTable)
		{
			string selectString = "";
			DataTable entityTable = GetEntityTable (homoEntryList);

			homoEntryAlignTable.Columns.Add (new DataColumn ("AsymChainList1"));
			homoEntryAlignTable.Columns.Add (new DataColumn ("AsymChainList2"));
			DataRow[] asymChainRows = null;
			string asymChainsString = "";
			foreach (DataRow dRow in homoEntryAlignTable.Rows)
			{
				asymChainsString = "";
				selectString = string.Format ("PdbID = '{0}'", dRow["QueryEntry"].ToString ().Trim ());
				asymChainRows = entityTable.Select (selectString);
				foreach (DataRow asymChainRow in asymChainRows)
				{
					asymChainsString += asymChainRow["AsymID"].ToString ().Trim ();
					asymChainsString += " ";
				}
				dRow["AsymChainList1"] = asymChainsString.Trim ();
				
				asymChainsString = "";
				selectString = string.Format ("PdbID = '{0}'", dRow["HitEntry"].ToString ().Trim ());
				asymChainRows = entityTable.Select (selectString);
				foreach (DataRow asymChainRow in asymChainRows)
				{
					asymChainsString += asymChainRow["AsymID"].ToString ().Trim ();
					asymChainsString += " ";
				}
				dRow["AsymChainList2"] = asymChainsString.Trim ();
				homoEntryAlignTable.AcceptChanges ();
			}
		}
		/// <summary>
		/// get the asymmetric chains for the aligned entities
		/// </summary>
		/// <param name="homoEntryList"></param>
		/// <param name="homoEntryAlignTable"></param>
		private void GetHomoEntryAlignAsymInfo (int groupId, string repPdbId, ref DataTable homoEntryAlignTable)
		{
			// add the same entry alignment info
			List<int> repEntityList = new List<int> ();
			string crystQueryString = string.Format ("Select Distinct EntityID From AsymUnit Where PdbID = '{0}' " + 
				" AND PolymerType = 'polypeptide';", repPdbId);
            DataTable repEntityTable = ProtCidSettings.pdbfamQuery.Query( crystQueryString);
			foreach (DataRow dRow in repEntityTable.Rows)
			{	
				repEntityList.Add (Convert.ToInt32 (dRow["EntityID"].ToString ()));
			}
			foreach (int entityId in repEntityList)
			{
				DataRow repPdbRow = homoEntryAlignTable.NewRow ();
				repPdbRow["GroupSeqID"] = groupId;
				repPdbRow["PdbID1"] = repPdbId;
				repPdbRow["PdbID2"] = repPdbId;
				repPdbRow["EntityID1"] = entityId;
				repPdbRow["EntityID2"] = entityId;
				repPdbRow["Identity"] = 100;
				repPdbRow["QueryStart"] = 0;
				repPdbRow["QueryEnd"] = 0;
				repPdbRow["HitStart"] = 0;
				repPdbRow["HitEnd"] = 0;
				repPdbRow["QuerySequence"] = "-";
				repPdbRow["HitSequence"] = "-";
				homoEntryAlignTable.Rows.Add (repPdbRow);
			}
			// get the homo sequence entries 
			List<string> homoEntryList = new List<string> ();
			homoEntryList.Add (repPdbId);
			foreach (DataRow entryRow in homoEntryAlignTable.Rows)
			{
				if (! homoEntryList.Contains (entryRow["PdbID2"].ToString ()))
				{
					homoEntryList.Add (entryRow["PdbID2"].ToString ());
				}
			}

			string selectString = "";
			DataTable entityTable = GetEntityTable (homoEntryList.ToArray ());

			homoEntryAlignTable.Columns.Add (new DataColumn ("AsymChainList1"));
			homoEntryAlignTable.Columns.Add (new DataColumn ("AsymChainList2"));
			DataRow[] asymChainRows = null;
			string asymChainsString = "";
			foreach (DataRow dRow in homoEntryAlignTable.Rows)
			{
				asymChainsString = "";
				selectString = string.Format ("PdbID = '{0}' AND EntityID = '{1}'", 
					dRow["PdbID1"].ToString ().Trim (), dRow["EntityID1"].ToString ());
				asymChainRows = entityTable.Select (selectString);
				foreach (DataRow asymChainRow in asymChainRows)
				{
					asymChainsString += asymChainRow["AsymID"].ToString ().Trim ();
					asymChainsString += " ";
				}
				dRow["AsymChainList1"] = asymChainsString.Trim ();
				
				asymChainsString = "";
				selectString = string.Format ("PdbID = '{0}' AND EntityID = '{1}'", 
					dRow["PdbID2"].ToString ().Trim (), dRow["EntityID2"].ToString ());
				asymChainRows = entityTable.Select (selectString);
				foreach (DataRow asymChainRow in asymChainRows)
				{
					asymChainsString += asymChainRow["AsymID"].ToString ().Trim ();
					asymChainsString += " ";
				}
				dRow["AsymChainList2"] = asymChainsString.Trim ();
				homoEntryAlignTable.AcceptChanges ();
			}
		}

		/// <summary>
		/// get the asymmetric chains for the aligned entities
		/// </summary>
		/// <param name="homoEntryList"></param>
		/// <param name="homoEntryAlignTable"></param>
		private void GetHomoEntryAlignAsymInfo (ref DataTable homoEntryAlignTable)
		{
            if (!homoEntryAlignTable.Columns.Contains("AsymChainList1"))
            {
                homoEntryAlignTable.Columns.Add(new DataColumn("AsymChainList1"));
                homoEntryAlignTable.Columns.Add(new DataColumn("AsymChainList2"));
            }

            if (homoEntryAlignTable.Rows.Count == 0)
            {
                return;
            }
			// get the homo sequence entries 
			List<string> homoEntryList = new List<string> ();
			foreach (DataRow entryRow in homoEntryAlignTable.Rows)
			{
				if (! homoEntryList.Contains (entryRow["PdbID1"].ToString ()))
				{
					homoEntryList.Add (entryRow["PdbID1"].ToString ());
				}
				if (! homoEntryList.Contains (entryRow["PdbID2"].ToString ()))
				{
					homoEntryList.Add (entryRow["PdbID2"].ToString ());
				}
			}

			string selectString = "";
		
			DataTable entityTable = GetEntityTable (homoEntryList.ToArray ());

           
			DataRow[] asymChainRows = null;
			string asymChainsString = "";
			foreach (DataRow dRow in homoEntryAlignTable.Rows)
			{
				asymChainsString = "";
				selectString = string.Format ("PdbID = '{0}' AND EntityID = '{1}'", 
					dRow["PdbID1"].ToString ().Trim (), dRow["EntityID1"].ToString ());
				asymChainRows = entityTable.Select (selectString);
				foreach (DataRow asymChainRow in asymChainRows)
				{
					asymChainsString += asymChainRow["AsymID"].ToString ().Trim ();
					asymChainsString += " ";
				}
				dRow["AsymChainList1"] = asymChainsString.Trim ();
				
				asymChainsString = "";
				selectString = string.Format ("PdbID = '{0}' AND EntityID = '{1}'", 
					dRow["PdbID2"].ToString ().Trim (), dRow["EntityID2"].ToString ());
				asymChainRows = entityTable.Select (selectString);
				foreach (DataRow asymChainRow in asymChainRows)
				{
					asymChainsString += asymChainRow["AsymID"].ToString ().Trim ();
					asymChainsString += " ";
				}
				dRow["AsymChainList2"] = asymChainsString.Trim ();
				homoEntryAlignTable.AcceptChanges ();
			}
		}
		/// <summary>
		/// get a subset of the table 
		/// selected by PDBs
		/// </summary>
		/// <param name="psiblastInfoTable"></param>
		/// <param name="pdbId1"></param>
		/// <param name="pdbId2"></param>
		/// <returns></returns>
		public DataTable SelectAlignInfoTable (DataTable alignmentInfoTable, string repPdb, string homoPdb)
		{
            DataTable subTable = alignmentInfoTable.Clone();
			string selectString = string.Format ("PdbID1 = '{0}' AND PdbID2 = '{1}'", repPdb, homoPdb);
            DataRow[] alignRows = alignmentInfoTable.Select(selectString);
			foreach (DataRow dRow in alignRows)
			{
				DataRow newRow = subTable.NewRow ();
				newRow.ItemArray = dRow.ItemArray;
				subTable.Rows.Add (newRow);
			}		
			return subTable;
		}
		#endregion

		#region Alignment Info for representative entries in a group		
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupRepEntries"></param>
        /// <param name="groupId"></param>
        /// <returns></returns>
        public DataTable GetGroupRepAlignInfoTable(string[] groupRepEntries, int groupId)
        {
            string selectString = "";
            // get alignment information from database
            string queryString = string.Format("Select * From {0} Where GroupSeqID = {1};",
                GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoGroupEntryAlign], groupId);
            DataTable alignmentInfoTable = ProtCidSettings.protcidQuery.Query( queryString);

            DataTable entityTable = GetEntityTable(groupRepEntries);

            alignmentInfoTable.Columns.Add(new DataColumn("AsymChainList1"));
            alignmentInfoTable.Columns.Add(new DataColumn("AsymChainList2"));
            DataRow[] asymChainRows = null;
            string asymChainsString = "";
            foreach (DataRow dRow in alignmentInfoTable.Rows)
            {
                asymChainsString = "";
                selectString = string.Format("PdbID = '{0}' AND EntityID = '{1}'",
                    dRow["PdbID1"].ToString().Trim(), dRow["EntityID1"].ToString());
                asymChainRows = entityTable.Select(selectString);
                foreach (DataRow asymChainRow in asymChainRows)
                {
                    asymChainsString += asymChainRow["AsymID"].ToString().Trim();
                    asymChainsString += " ";
                }
                dRow["AsymChainList1"] = asymChainsString.Trim();

                asymChainsString = "";
                selectString = string.Format("PdbID = '{0}' AND EntityID = '{1}'",
                    dRow["PdbID2"].ToString().Trim(), dRow["EntityID2"].ToString());
                asymChainRows = entityTable.Select(selectString);
                foreach (DataRow asymChainRow in asymChainRows)
                {
                    asymChainsString += asymChainRow["AsymID"].ToString().Trim();
                    asymChainsString += " ";
                }
                dRow["AsymChainList2"] = asymChainsString.Trim();
                alignmentInfoTable.AcceptChanges();
            }
            return alignmentInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupRepEntries"></param>
        /// <param name="groupId"></param>
        /// <returns></returns>
        public DataTable GetGroupRepAlignInfoTable(int groupId, string pdbId1, string pdbId2)
        {
            string selectString = "";
            // get alignment information from database
            string queryString = string.Format("Select * From {0} Where GroupSeqID = {1} " +
                " AND ((PdbID1 = '{2}' AND PdbID2 = '{3}') OR (PdbID1 = '{3}' AND PdbID2 = '{2}'));",
                GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoGroupEntryAlign], groupId, pdbId1, pdbId2);
            DataTable alignmentInfoTable = ProtCidSettings.protcidQuery.Query( queryString);

            string[] groupRepEntries = new string[2];
            groupRepEntries[0] = pdbId1;
            groupRepEntries[1] = pdbId2;
            DataTable entityTable = GetEntityTable(groupRepEntries);

            alignmentInfoTable.Columns.Add(new DataColumn("AsymChainList1"));
            alignmentInfoTable.Columns.Add(new DataColumn("AsymChainList2"));
            DataRow[] asymChainRows = null;
            string asymChainsString = "";
            foreach (DataRow dRow in alignmentInfoTable.Rows)
            {
                asymChainsString = "";
                selectString = string.Format("PdbID = '{0}' AND EntityID = '{1}'",
                    dRow["PdbID1"].ToString().Trim(), dRow["EntityID1"].ToString());
                asymChainRows = entityTable.Select(selectString);
                foreach (DataRow asymChainRow in asymChainRows)
                {
                    asymChainsString += asymChainRow["AsymID"].ToString().Trim();
                    asymChainsString += " ";
                }
                dRow["AsymChainList1"] = asymChainsString.Trim();

                asymChainsString = "";
                selectString = string.Format("PdbID = '{0}' AND EntityID = '{1}'",
                    dRow["PdbID2"].ToString().Trim(), dRow["EntityID2"].ToString());
                asymChainRows = entityTable.Select(selectString);
                foreach (DataRow asymChainRow in asymChainRows)
                {
                    asymChainsString += asymChainRow["AsymID"].ToString().Trim();
                    asymChainsString += " ";
                }
                dRow["AsymChainList2"] = asymChainsString.Trim();
                alignmentInfoTable.AcceptChanges();
            }
            return alignmentInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <returns></returns>
        private DataTable GetEntityTable(string[] pdbIds)
        {
            DataTable entityTable = null;
            foreach (string pdbId in pdbIds)
            {
                DataTable entryEntityTable = GetEntryEntityTable(pdbId);   
                if (entityTable == null)
                {
                    entityTable = entryEntityTable.Copy();
                }
                else
                {
                    foreach (DataRow entryEntityRow in entryEntityTable.Rows)
                    {
                        DataRow entityRow = entityTable.NewRow();
                        entityRow.ItemArray = entryEntityRow.ItemArray;
                        entityTable.Rows.Add(entityRow);
                    }
                }
            }
            return entityTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryEntityTable(string pdbId)
        {
            string queryString = string.Format("Select PdbID, AsymID, EntityID From AsymUnit Where PdbID = '{0}' " +
                   " AND PolymerType = 'polypeptide';", pdbId);
            DataTable entryEntityTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return entryEntityTable;
        }
       
		/// <summary>
		/// get a subset of the table 
		/// selected by PDBs
		/// </summary>
		/// <param name="psiblastInfoTable"></param>
		/// <param name="pdbId1"></param>
		/// <param name="pdbId2"></param>
		/// <returns></returns>
		public DataTable SelectSubTable (DataTable alignmentInfoTable, string pdbId1, string pdbId2)
		{
            DataTable subTable = alignmentInfoTable.Clone();
			string selectString = string.Format ("PdbID1 = '{0}' AND PdbID2 = '{1}'", pdbId1, pdbId2);
            DataRow[] alignRows = alignmentInfoTable.Select(selectString);
			foreach (DataRow dRow in alignRows)
			{
				DataRow newRow = subTable.NewRow ();
				newRow.ItemArray = dRow.ItemArray;
				subTable.Rows.Add (newRow);
			}
			if (alignRows.Length == 0)
			{
				selectString = string.Format ("PdbID1 = '{0}' AND PdbID2 = '{1}'", pdbId2, pdbId1);
                alignRows = alignmentInfoTable.Select(selectString);
				// reverse query entry and hit entry
				foreach (DataRow dRow in alignRows)
				{
					DataRow newRow = subTable.NewRow ();
					newRow["GroupSeqID"] = dRow["GroupSeqID"];
					newRow["PdbID1"] = dRow["PdbID2"];
					newRow["EntityID1"] = dRow["EntityID2"];
					newRow["AsymChainList1"] = dRow["AsymChainList2"];
					newRow["QueryStart"] = dRow["HitStart"];
					newRow["QueryEnd"] = dRow["HitEnd"];
					newRow["QuerySequence"] = dRow["HitSequence"];
					newRow["Identity"] = dRow["Identity"];
					newRow["PdbID2"] = dRow["PdbID1"];
					newRow["EntityID2"] = dRow["EntityID1"];
					newRow["AsymChainList2"] = dRow["AsymChainList1"];
					newRow["HitStart"] = dRow["QueryStart"];
					newRow["HitEnd"] = dRow["QueryEnd"];
					newRow["HitSequence"] = dRow["QuerySequence"];
					subTable.Rows.Add (newRow);
				}
			}		
			return subTable;
		}

      
		#endregion

		#region alignment info between two arbitrary entries from cehit/psiblasthit
		/// <summary>
		/// alignment info for any two entries
		/// </summary>
		/// <param name="pdbId1"></param>
		/// <param name="pdbId2"></param>
		/// <returns></returns>
		public DataTable GetAlignmentInfo (string pdbId1, string pdbId2)
		{
			DataTable alignInfoTable = GetEntryAlignmentInfoFromExistTables (pdbId1, pdbId2);
            if (alignInfoTable.Rows.Count > 0)
            {
                GetHomoEntryAlignAsymInfo(ref alignInfoTable);
            }
            else
            {
				alignInfoTable  = GetEntryAlignmentInfoFromAlignmentsTables (pdbId1, pdbId2);
			}
			return alignInfoTable;
		}

		#region alignment data from exist tables
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
		private DataTable GetEntryAlignmentInfoFromExistTables (string pdbId1, string pdbId2)
		{
			DataTable alignInfoTable = GetEntryAlignmentInfoFromTable (pdbId1, pdbId2, 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoRepEntryAlign]);
			if (alignInfoTable.Rows.Count == 0)
			{
				alignInfoTable = GetEntryAlignmentInfoFromTable (pdbId1, pdbId2, 
					GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoGroupEntryAlign]);
			}
			return alignInfoTable;
		}
		
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
		private DataTable GetEntryAlignmentInfoFromTable (string pdbId1, string pdbId2, string tableName)
		{
			string queryString = string.Format ("Select * From {0} Where PdbID1 = '{1}' And PdbID2 = '{2}';",
				tableName, pdbId1, pdbId2);
            DataTable entryAlignTable = ProtCidSettings.protcidQuery.Query( queryString);
			if (entryAlignTable.Rows.Count == 0)
			{
				queryString = string.Format ("Select * From {0} Where PdbID1 = '{1}' And PdbID2 = '{2}';",
					tableName, pdbId2, pdbId1);
                entryAlignTable = ProtCidSettings.protcidQuery.Query( queryString);
				if (entryAlignTable.Rows.Count > 0)
				{
					entryAlignTable.Columns.Remove ("GroupSeqID");
					entryAlignTable = ReverseEntryAlignInfo (entryAlignTable);
				}
			}
			else
			{
				entryAlignTable.Columns.Remove ("GroupSeqID");
			}
			return entryAlignTable;
		}
		#endregion

		#region alignment info from alignments tables
        private EntryAlignment entryAlignment = new EntryAlignment();
		/// <summary>
		/// 
		/// </summary>
		/// <param name="pdbId1"></param>
		/// <param name="pdbId2"></param>
		public DataTable GetEntryAlignmentInfoFromAlignmentsTables (string pdbId1, string pdbId2)
		{
            if (HomoGroupTables.homoGroupTables == null)
            {
                HomoGroupTables.InitializeTables ();
            }
            HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign].Clear();

            string[] homoEntries = new string[1];
            homoEntries[0] = pdbId2;
   
			entryAlignment.RetrieveRepEntryAlignment (pdbId1, homoEntries, -1);

            GetHomoEntryAlignAsymInfo(ref HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign]);

			return HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign];
     //       return alignInfoTable;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        public DataTable GetEntryAlignments (string pdbId1, string pdbId2)
        {
            EntryAlignment entryAlignment = new EntryAlignment();
            DataTable alignInfoTable = entryAlignment.RetrieveEntryAlignments (pdbId1, pdbId2);

            return alignInfoTable;
        }
		#endregion

		public DataTable ReverseEntryAlignInfo (DataTable alignInfoTable)
		{
			DataTable reversedAlignInfoTable = alignInfoTable.Clone ();
			foreach (DataRow dRow in alignInfoTable.Rows)
			{
				DataRow reversedRow = reversedAlignInfoTable.NewRow ();
				reversedRow.ItemArray = dRow.ItemArray;
				reversedRow["PdbID1"] = dRow["PdbID2"];
				reversedRow["EntityID1"] = dRow["EntityID2"];
				reversedRow["PdbID2"] = dRow["PdbID1"];
				reversedRow["EntityID2"] = dRow["EntityID1"];
				reversedRow["QueryStart"] = dRow["HitStart"];
				reversedRow["QueryEnd"] = dRow["HitEnd"];
				reversedRow["HitStart"] = dRow["QueryStart"];
				reversedRow ["HitEnd"] = dRow["QueryEnd"];
				reversedRow["QuerySequence"] = dRow["HitSequence"];
				reversedRow["hitSequence"] = dRow["QuerySequence"];
				reversedAlignInfoTable.Rows.Add (reversedRow);
			}
			return reversedAlignInfoTable;
		}
		#endregion
	}
}
