using System;
using System.IO;
using System.Data;
using System.Collections.Generic;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;
using InterfaceClusterLib.DataTables;
using CrystalInterfaceLib.Settings;
using PfamLib.ExtDataProcess;

namespace InterfaceClusterLib.Alignments
{
	/// <summary>
	/// Align two entries
	/// </summary>
	public class EntryAlignment
	{
		#region member variables	
		public DataTable entryEntityFamilyTable = new DataTable ();
        public AlignmentBlosumScore blosumScore = new AlignmentBlosumScore();
        public HHAlignments hhAlignments = new HHAlignments();
//       PdbCrcMap pdbCrcMap = new PdbCrcMap();
 //       public DataTable pdbCrcTable = null;
        private DbInsert dbInsert = new DbInsert();
        private DbUpdate dbDelete = new DbUpdate();
        private DataTable reduntChainTable = null;
#if DEBUG
		public static StreamWriter nonAlignedDataWriter = null;
		public static StreamWriter logWriter = null;
#endif
		#endregion

        public EntryAlignment()
        {
            string[] colNames = { "PdbID", "EntityID", "FamilyList" };
            foreach (string col in colNames)
            {
                entryEntityFamilyTable.Columns.Add(new DataColumn(col));
            }
            if (ProtCidSettings.alignmentDbConnection == null)
            {
                ProtCidSettings.alignmentDbConnection = new DbConnect();
            }
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }
            if (ProtCidSettings.alignmentDbConnection.ConnectString == "")
            {
                ProtCidSettings.alignmentDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                ProtCidSettings.dirSettings.alignmentDbPath;
            }
#if DEBUG
            if (nonAlignedDataWriter == null)
            {
                nonAlignedDataWriter = new StreamWriter("NonAlignedEntryPairs.txt", true);
            }

            if (logWriter == null)
            {
                logWriter = new StreamWriter ("alignLog.txt", true);
            }
#endif        
            reduntChainTable = InitializeReduntTable();
        }
/*
        public void InitializePdbCrcMapTable (DataTable inPdbCrcTable)
        {
           pdbCrcTable = inPdbCrcTable;
            hhAlignments.pdbCrcMapTable = inPdbCrcTable;
        }
        */
		#region Alignment in a group
		/// <summary>
		/// get the pairwise sequence alignmentfor the input list
		/// </summary>
		/// <param name="pdbList"></param>
		/// <param name="groupNum"></param>
		public void GetEntryPairAlignment (string[] pdbIds, int groupNum)
		{
            if (pdbIds.Length <= 1)
            {
                return;
            }
            // the redundant chains info from redundant pdb chains table
            DataTable reduntChainTable = GetRepChainTable(pdbIds);
            // entity info for each entry in pdbList
            DataTable entityTable = GetEntityTable(pdbIds);

			string pdbId = "";
            for (int i = 0; i < pdbIds.Length - 1; i++)
			{
				pdbId = pdbIds[i].ToString ();
				string[] alignPdbIds = ParseHelper.GetSubArray (pdbIds, i + 1, pdbIds.Length - i - 1);
				RetrieveRepEntryAlignment (pdbId, alignPdbIds, groupNum, HomoGroupTables.HomoGroupEntryAlign, reduntChainTable, entityTable);				
			}
		}

        /// <summary>
        /// get the pairwise sequence alignmentfor the input list
        /// </summary>
        /// <param name="pdbList"></param>
        /// <param name="groupNum"></param>
        public void GetEntryPairAlignmentInBigGroup (string[] pdbIds, int groupNum, string alignTableName)
        {
            string deleteString = "";
            // the redundant chains info from redundant pdb chains table
            DataTable reduntChainTable = GetRepChainTable(pdbIds);
            // entity info for each entry in pdbList
            DataTable entityTable = GetEntityTable(pdbIds);

            string pdbId = "";
            for (int i = 0; i < pdbIds.Length - 1; i++)
            {
                pdbId = pdbIds[i];
                string[] alignPdbIds = ParseHelper.GetSubArray(pdbIds, i + 1, pdbIds.Length - i - 1);
                RetrieveRepEntryAlignment(pdbId, alignPdbIds, groupNum, HomoGroupTables.HomoGroupEntryAlign, reduntChainTable, entityTable);

                deleteString = string.Format("Delete From {0} Where GroupSeqID = '{1}' AND PdbID1 = '{2}';", alignTableName, groupNum, pdbId);
                dbDelete.Delete (ProtCidSettings.protcidDbConnection, deleteString);

                dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign]);
                HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign].Clear();
            }
        }

		/// <summary>
		/// alignment between representative and its homologous entries
		/// </summary>
		/// <param name="repPdbId"></param>
		/// <param name="pdbList"></param>
		/// <param name="groupNum"></param>
		public void RetrieveRepEntryAlignment (string repPdbId, string[] alignPdbIds, int groupNum)
		{
            if (alignPdbIds.Length == 0)
            {
                return;
            }
            string[] theWholeList = new string[alignPdbIds.Length + 1];
            theWholeList[0] = repPdbId;
            Array.Copy(alignPdbIds, 0, theWholeList, 1, alignPdbIds.Length);
            // the redundant chains info from redundant pdb chains table
            DataTable reduntChainTable = GetRepChainTable(theWholeList);
            // entity info for each entry in pdbList
            DataTable entityTable = GetEntityTable(theWholeList);

            RetrieveRepEntryAlignment(repPdbId, alignPdbIds, groupNum, HomoGroupTables.HomoRepEntryAlign, reduntChainTable, entityTable);
		}
			
		/// <summary>
		/// sequence alignment between repsentative entry and 
		/// its homologous entries
		/// </summary>
		/// <param name="repPdbId"></param>
		/// <param name="pdbList"></param>
		/// <param name="groupNum"></param>
        private void RetrieveRepEntryAlignment(string repPdbId, string[] alignPdbIds, int groupNum, int tableNum, DataTable reduntChainTable, DataTable entityTable)
		{
			List<int> leftRepEntityList = null;
			List<int> homoEntityList = null;
			List<int> leftHomoEntityList = null;
           
			// entity list of repPdbId
		 	List<int> repEntityList = GetEntityList (repPdbId, entityTable);
            // entity pairs are added
            List<string> addedEntityPairList = new List<string> ();

            foreach (string pdbId in alignPdbIds)
			{				
                addedEntityPairList.Clear();

                // add redundant chains to table which have identity = 100%
                AddReduntEntryAlignInfoToTable (groupNum, tableNum, repPdbId, pdbId, reduntChainTable, ref addedEntityPairList);

                leftRepEntityList = new List<int> (repEntityList);
                // entity list for pdbId
                homoEntityList = GetEntityList (pdbId, entityTable);
                leftHomoEntityList = new List<int> (homoEntityList);
                GetLeftEntityList(addedEntityPairList, ref leftRepEntityList, ref leftHomoEntityList);

				if (leftRepEntityList.Count == 0 && leftHomoEntityList.Count == 0)
				{
					continue;
				}

                try
                {
                    GetAlignments(groupNum, tableNum, repPdbId, pdbId, reduntChainTable, entityTable,
                        ref leftRepEntityList, ref leftHomoEntityList, ref addedEntityPairList);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving alignments between " + 
                        repPdbId + " and " + pdbId + " errors: " + ex.Message);
                    continue;
                }

                GetLeftEntityList(addedEntityPairList, ref leftRepEntityList, ref leftHomoEntityList);
			
#if DEBUG
				WriterNonAlignedEntityPairsIntoLogFile (repPdbId, pdbId, leftRepEntityList, repEntityList, 
					leftHomoEntityList, homoEntityList, entityTable);
#endif
				// somehow, no alignment info, set to identical
				try
				{
					for (int i = 0; i < leftRepEntityList.Count; i ++)
					{
						DataRow entryRow = HomoGroupTables.homoGroupTables[tableNum].NewRow ();
						entryRow["GroupSeqID"] = groupNum;
						entryRow["PdbID1"] = repPdbId;
						entryRow["EntityID1"] = leftRepEntityList[i];
						entryRow["PdbID2"] = pdbId;
						if (i > leftHomoEntityList.Count - 1)
						{
							break;
						}
						entryRow["EntityID2"] = leftHomoEntityList[i];
						entryRow["Identity"] = 100;
						entryRow["QueryStart"] = -1;
						entryRow["QueryEnd"] = -1;
						entryRow["HitStart"] = -1;
						entryRow["HitEnd"] = -1;
						entryRow["QuerySequence"] = "-";
						entryRow["HitSequence"] = "-";
						HomoGroupTables.homoGroupTables[tableNum].Rows.Add (entryRow);	
					}
				}
				catch (Exception ex)
				{
#if DEBUG
					logWriter.WriteLine (ex.Message);
					logWriter.WriteLine (groupNum.ToString () + ":" + repPdbId + " " + pdbId);
                    logWriter.Flush();
#endif
				}
			}
#if DEBUG
			nonAlignedDataWriter.Flush ();
#endif
		}
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="addedEntityPairList"></param>
        /// <param name="leftRepEntityList"></param>
        /// <param name="leftHomoEntityList"></param>
        private void GetLeftEntityList(List<string> addedEntityPairList, ref List<int> leftRepEntityList, ref List<int> leftHomoEntityList)
        {
            foreach (string entityPair in addedEntityPairList)
            {
                string[] entities = entityPair.Split('_');
                leftRepEntityList.Remove(Convert.ToInt32(entities[0]));
                leftHomoEntityList.Remove(Convert.ToInt32 (entities[1]));
            }
        }
		#endregion

		#region write nonaligned entities into a log file
		/// <summary>
		/// Write non-aligned entities into the log file
		/// then do CE or Fatcat alignment, filled out those missing alignments
        /// changed authorchain into asymchain on May 14, 2010
		/// </summary>
		/// <param name="leftRepEntityList"></param>
		/// <param name="leftHomoEntityList"></param>
		/// <param name="entityTable"></param>
		private void WriterNonAlignedEntityPairsIntoLogFile (string repPdbId, string homoPdbId, List<int> leftRepEntityList, List<int> repEntityList, 
			List<int> leftHomoEntityList, List<int> homoEntityList, DataTable entityTable)
		{
			string repChain = "";
			string homoChain = "";

			foreach (int repEntity in leftRepEntityList)
			{
				DataRow[] repChainRows = entityTable.Select 
					(string.Format ("PdbID = '{0}' AND EntityID = '{1}'", repPdbId, repEntity), "AsymID ASC");
				if (repChainRows.Length > 0)
				{
			/*		repChain = repChainRows[0]["AuthorChain"].ToString ().Trim ();
					if (repChain == "_")
					{
						repChain = "A";
					}*/
                    repChain = repChainRows[0]["AsymID"].ToString().TrimEnd();
					foreach (int homoEntity in homoEntityList)
					{
						DataRow[] homoChainRows = entityTable.Select 
							(string.Format ("PdbID = '{0}' AND EntityID = '{1}'", homoPdbId, homoEntity), "AuthorChain ASC");
						if (homoChainRows.Length > 0)
						{
						/*	homoChain = homoChainRows[0]["AuthorChain"].ToString ().Trim ();
							if (homoChain  == "_")
							{
								homoChain = "A";
							}*/
                            homoChain = homoChainRows[0]["AsymID"].ToString().TrimEnd();
							string line = repPdbId +  repChain + "	" + homoPdbId + homoChain;
#if DEBUG
							nonAlignedDataWriter.WriteLine (line);
#endif
						}
					}
				}
			}
			foreach (int homoEntity in leftHomoEntityList)
			{
				DataRow[] homoChainRows = entityTable.Select 
					(string.Format ("PdbID = '{0}' AND EntityID = '{1}'", homoPdbId, homoEntity), "AuthorChain ASC");	
				if (homoChainRows.Length > 0)
				{
					homoChain = homoChainRows[0]["AsymID"].ToString ().Trim ();
					
					foreach (int repEntity in repEntityList)
					{
						if (leftRepEntityList.Contains (repEntity))
						{
							continue;
						}
						DataRow[] repChainRows = entityTable.Select 
							(string.Format ("PdbID = '{0}' AND EntityID = '{1}'", repPdbId, repEntity), "AuthorChain ASC");
						if (repChainRows.Length > 0)
						{
							repChain = repChainRows[0]["AsymID"].ToString ().Trim ();
							
							string line = repPdbId + repChain + "	" + homoPdbId + homoChain;
#if DEBUG
							nonAlignedDataWriter.WriteLine (line);
#endif
						}
					}
				}
			}
		}
		#endregion

		#region Alignments from PSIBLAST/CE/FATCAT
        /// <summary>
        /// 
        /// </summary>
        /// <param name="repPdbId"></param>
        /// <param name="homoPdbId"></param>
        /// <param name="reduntEntryTable"></param>
        private void GetAlignments(int groupNum, int tableNum, string repPdbId, string pdbId, DataTable reduntChainTable, 
            DataTable entityTable, ref List<int> leftRepEntityList, ref List<int> leftHomoEntityList, ref List<string> addedEntityPairList )
        {
            // select the alignment info from crc-hh and FATCAT 
            // between reppdbid and pdbid          

            // replace psiblast table by hh alignments

            DataTable hhAlignTable = hhAlignments.RetrieveHHAlignments(repPdbId, pdbId);
            AssignHitEntryAlignInfoToTable(groupNum, tableNum, repPdbId, pdbId, hhAlignTable, ref addedEntityPairList);
            GetLeftEntityList(addedEntityPairList, ref leftRepEntityList, ref leftHomoEntityList);

            if (leftRepEntityList.Count == 0 && leftHomoEntityList.Count == 0)
            {
                return;
            }
            
            DataTable fatcatHitTable = GetAlignTable(repPdbId, pdbId, reduntChainTable, entityTable, "FatcatAlignments");
            if (fatcatHitTable.Rows.Count > 0)
            {
                AssignHitEntryAlignInfoToTable(groupNum, tableNum, repPdbId, pdbId, fatcatHitTable, ref addedEntityPairList);
            }      
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="repPdbId"></param>
        /// <param name="homoPdbId"></param>
        /// <param name="reduntEntryTable"></param>
        private void GetEntryAllAlignments(int groupNum, int tableNum, string repPdbId, string pdbId, DataTable reduntChainTable,
            DataTable entityTable, ref List<int> leftRepEntityList, ref List<int> leftHomoEntityList, ref List<string> addedEntityPairList)
        {
            // select the alignment info from crc-hh and FATCAT 
            // between reppdbid and pdbid          

            // replace psiblast table by hh alignments

            DataTable hhAlignTable = hhAlignments.RetrieveAllHHAlignments (repPdbId, pdbId);
            AssignHitEntryAlignInfoToTable(groupNum, tableNum, repPdbId, pdbId, hhAlignTable, ref addedEntityPairList);
            GetLeftEntityList(addedEntityPairList, ref leftRepEntityList, ref leftHomoEntityList);

            if (leftRepEntityList.Count == 0 && leftHomoEntityList.Count == 0)
            {
                return;
            }

            DataTable fatcatHitTable = GetAlignTable(repPdbId, pdbId, reduntChainTable, entityTable, "FatcatAlignments");
            if (fatcatHitTable.Rows.Count > 0)
            {
                AssignHitEntryAlignInfoToTable(groupNum, tableNum, repPdbId, pdbId, fatcatHitTable, ref addedEntityPairList);
            }
        }       
        #region alignment table
        /// <summary>
		/// alignment info between two entries
        /// all alignments in the database
        /// theoretically, alignments are only between representative chains
        /// if the input entries are not representatives, has to use representatives to find alignments,
        /// then replace by input entries
		/// </summary>
		/// <param name="repPdbId"></param>
		/// <param name="homoPdbId"></param>
		/// <param name="reduntEntryTable"></param>
		/// <param name="tableName"></param>
		/// <returns></returns>
		private DataTable GetAlignTable (string repPdbId, string homoPdbId, DataTable reduntEntryTable,	
			DataTable entityTable, string tableName)
		{
			List<string> repReduntRepEntries = new List<string> (GetRedundantRepEntries (repPdbId, reduntEntryTable));
			List<string> homoReduntRepEntries = new List<string> (GetRedundantRepEntries (homoPdbId, reduntEntryTable));

            DataTable alignmentTable = null;

			string queryString = "";
			// find all the alignment between these two entries
			foreach (string repReduntRepEntry in repReduntRepEntries)
			{
                foreach (string homoReduntRepEntry in homoReduntRepEntries)
                {
                    queryString = string.Format("SELECT QueryEntry, QueryChain, HitEntry, HitChain, " + 
                        " QueryLength, QueryStart, QueryEnd, QuerySequence, " +
                        " HitLength, HitStart, HitEnd, HitSequence, Identity" +
                        " FROM {0} WHERE " +
                        " QueryEntry = '{1}' AND HitEntry = '{2}';",
                        tableName, repReduntRepEntry, homoReduntRepEntry);
                    DataTable hitTable = ProtCidSettings.alignmentQuery.Query(queryString);

                    queryString = string.Format("SELECT QueryEntry, QueryChain, HitEntry, HitChain, " +
                        " QueryLength, QueryStart, QueryEnd, QuerySequence, " +
                        " HitLength, HitStart, HitEnd, HitSequence, Identity" +
                        " FROM {0} WHERE " +
                       " QueryEntry = '{1}' AND HitEntry = '{2}';",
                       tableName, homoReduntRepEntry, repReduntRepEntry);
                    DataTable thisHitTable = ProtCidSettings.alignmentQuery.Query(queryString);
                    foreach (DataRow thisHitRow in thisHitTable.Rows)
                    {
                        // reverse the alignment
                        ReverseAlignmentRow(thisHitRow);
                        DataRow newRow = hitTable.NewRow();
                        newRow.ItemArray = thisHitRow.ItemArray;
                        hitTable.Rows.Add(newRow);
                    }
                    UpdateAlignmentTable(repPdbId, homoPdbId, 
                        repReduntRepEntry, homoReduntRepEntry, hitTable, reduntEntryTable);
                    if (alignmentTable == null)
                    {
                        alignmentTable = hitTable.Copy();
                    }
                    else
                    {
                        foreach (DataRow hitRow in hitTable.Rows)
                        {
                            DataRow newRow = alignmentTable.NewRow();
                            newRow.ItemArray = hitRow.ItemArray;
                            alignmentTable.Rows.Add(newRow);
                        }
                    }
                }
			}
            AddEntityIDToHitTable(ref alignmentTable, entityTable);
            return alignmentTable;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="repPdbId"></param>
        /// <param name="homoPdbId"></param>
        /// <param name="repReduntRepEntry"></param>
        /// <param name="homoReduntRepEntry"></param>
        /// <param name="alignTable"></param>
        /// <param name="reduntChainTable"></param>
        private void UpdateAlignmentTable(string repPdbId, string homoPdbId, string repReduntRepEntry, string homoReduntRepEntry, DataTable alignTable, DataTable reduntChainTable)
        {
            // the repPdbId is not a representative entry in the redundantPdbChains 
            if (repReduntRepEntry != repPdbId)
            {
                string queryEntry = "";
                string queryChain = "";
                List<DataRow> removeRowList = new List<DataRow> ();
                foreach (DataRow alignRow in alignTable.Rows)
                {
                    queryEntry = alignRow["QueryEntry"].ToString ();
                    queryChain = alignRow["QueryChain"].ToString ().TrimEnd ();
                    DataRow reduntChainRow = FindReduntChainRow(queryEntry, queryChain, repPdbId, reduntChainTable);
                    if (reduntChainRow != null)
                    {
                        // replace the representative chain by the repPdbId chain
                        alignRow["QueryEntry"] = repPdbId;
                        alignRow["QueryChain"] = reduntChainRow["ChainID2"];
                    }
                    else
                    {
                        removeRowList.Add(alignRow);
                    }
                }
                foreach (DataRow removeRow in removeRowList)
                {
                    alignTable.Rows.Remove(removeRow);
                }
            }
            // if the homoPdbId is not representative entry in the redundantpdbchains
            if (homoReduntRepEntry != homoPdbId)
            {
                string hitEntry = "";
                string hitChain = "";
                List<DataRow> removeRowList = new List<DataRow> ();
                foreach (DataRow alignRow in alignTable.Rows)
                {
                    hitEntry = alignRow["HitEntry"].ToString();
                    hitChain = alignRow["HitChain"].ToString().TrimEnd();
                    DataRow reduntChainRow = FindReduntChainRow(hitEntry, hitChain, homoPdbId, reduntChainTable);
                    if (reduntChainRow != null)
                    {
                        // replace the representative entry by the homoPdbId itself
                        alignRow["HitEntry"] = homoPdbId;
                        alignRow["HitChain"] = reduntChainRow["ChainID2"];
                    }
                    else
                    {
                        removeRowList.Add(alignRow);
                    }
                }
                foreach (DataRow removeRow in removeRowList)
                {
                    alignTable.Rows.Remove(removeRow);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <param name="reduntPdbId"></param>
        /// <param name="reduntChainTable"></param>
        /// <returns></returns>
        private DataRow FindReduntChainRow(string pdbId, string authChain, string reduntPdbId, DataTable reduntChainTable)
        {
            DataRow[] reduntChainRows = reduntChainTable.Select (string.Format ("PdbID1 = '{0}' AND ChainID1 = '{1}' AND PdbID2 = '{2}'", 
                pdbId, authChain, reduntPdbId));
            if (reduntChainRows.Length == 0)
            {
                reduntChainRows = reduntChainTable.Select (string.Format("PdbID2 = '{0}' AND ChainID2 = '{1}'", pdbId, authChain));
                if (reduntChainRows.Length > 0)
                {
                    string repPdb = reduntChainRows[0]["PdbID1"].ToString();
                    string repAuthChain = reduntChainRows[0]["ChainID1"].ToString();
                    reduntChainRows = reduntChainTable.Select(string.Format("PdbID1 = '{0}' AND ChainID1 = '{1}' AND PdbID2 = '{2}'",
                                    repPdb, repAuthChain, reduntPdbId));
                }
            }
            if (reduntChainRows.Length > 0)
            {
                return reduntChainRows[0];
            }
            return null;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="reduntChainTable"></param>
        /// <returns></returns>
		private string[] GetReduntEntries (string pdbId, DataTable reduntChainTable)
		{
			List<string> reduntEntryList = new List<string> ();
			DataRow[] reduntRows = reduntChainTable.Select (string.Format ("PdbID1 = '{0}'", pdbId));
			foreach (DataRow reduntRow in reduntRows)
			{
				if (! reduntEntryList.Contains (reduntRow["PdbID2"].ToString ()))
				{
					reduntEntryList.Add (reduntRow["PdbID2"].ToString ());
				}
			}
			return reduntEntryList.ToArray ();
		}
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hitRow"></param>
		private void ReverseAlignmentRow (DataRow hitRow)
		{
			object temp = null;
			temp = hitRow["QueryEntry"];
			hitRow["QueryEntry"] = hitRow["HitEntry"];
			hitRow["HitEntry"] = temp;
			temp = hitRow["QueryChain"];
			hitRow["QueryChain"] = hitRow["HitChain"];
			hitRow["HitChain"] = temp;
			temp = hitRow["QueryLength"];
			hitRow["QueryLength"] = hitRow["HitLength"];
			hitRow["HitLength"] = temp;
			temp = hitRow["QueryStart"];
			hitRow["QueryStart"] = hitRow["HitStart"];
			hitRow["HitStart"] = temp;
			temp = hitRow["QueryEnd"];
			hitRow["QueryEnd"] = hitRow["HitEnd"];
			hitRow["HitEnd"] = temp;
			temp = hitRow["QuerySequence"];
			hitRow["QuerySequence"] = hitRow["HitSequence"];
			hitRow["HitSequence"] = temp;
            if (hitRow.Table.Columns.IndexOf("QueryAsymChain") > -1)
            {
                temp = hitRow["QueryAsymChain"];
                hitRow["QueryAsymChain"] = hitRow["HitAsymChain"];
                hitRow["HitAsymChain"] = temp;
            }
        }
        #endregion
        #endregion

        #region add alignment info to table
        /// <summary>
		/// 
		/// </summary>
		/// <param name="groupNum"></param>
		/// <param name="tableNum"></param>
		/// <param name="repPdbId"></param>
		/// <param name="homoPdbId"></param>
		/// <param name="hitTable"></param>
		/// <param name="leftRepEntityList"></param>
		/// <param name="leftHomoEntityList"></param>
		private void AssignHitEntryAlignInfoToTable (int groupNum, int tableNum, string repPdbId, string homoPdbId, 
			DataTable hitTable, ref List<int> leftRepEntityList, ref List<int> leftHomoEntityList)
		{
			foreach (DataRow hitRow in hitTable.Rows)
			{
				if (leftRepEntityList.Contains (Convert.ToInt32 (hitRow["QueryEntity"].ToString ())) && 
					leftHomoEntityList.Contains (Convert.ToInt32 (hitRow["HitEntity"].ToString ())))
				{
					DataRow entryRow = HomoGroupTables.homoGroupTables[tableNum].NewRow ();				
					entryRow["GroupSeqID"] = groupNum;
					entryRow["PdbID1"] = repPdbId;
					entryRow["EntityID1"] = hitRow["QueryEntity"];
					entryRow["PdbID2"] = homoPdbId;
					entryRow["EntityID2"] = hitRow["HitEntity"];
					entryRow["Identity"] = hitRow["Identity"];
					entryRow["QueryStart"] = hitRow["QueryStart"];
					entryRow["QueryEnd"] = hitRow["QueryEnd"];
					entryRow["HitStart"] = hitRow["HitStart"];
					entryRow["HitEnd"] = hitRow["hitEnd"];
					entryRow["QuerySequence"] = hitRow["QuerySequence"];
					entryRow["HitSequence"] = hitRow["HitSequence"];
					HomoGroupTables.homoGroupTables[tableNum].Rows.Add (entryRow);
					leftRepEntityList.Remove (Convert.ToInt32 (hitRow["QueryEntity"].ToString ()));
					leftHomoEntityList.Remove (Convert.ToInt32 (hitRow["HitEntity"].ToString ()));
				}
			}
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupNum"></param>
        /// <param name="tableNum"></param>
        /// <param name="repPdbId"></param>
        /// <param name="homoPdbId"></param>
        /// <param name="hitTable"></param>
        /// <param name="leftRepEntityList"></param>
        /// <param name="leftHomoEntityList"></param>
        private void AssignHitEntryAlignInfoToTable(int groupNum, int tableNum, string repPdbId, string homoPdbId, DataTable hitTable, ref List<string> addedEntityPairList)
        {
            string entityPair = "";
            foreach (DataRow hitRow in hitTable.Rows)
            {
                entityPair = hitRow["QueryEntity"].ToString() + "_" + hitRow["HitEntity"].ToString();

                if (addedEntityPairList.Contains(entityPair))
                {
                    DataRow[] alignRows = HomoGroupTables.homoGroupTables[tableNum].Select 
                        (string.Format ("PdbID1 = '{0}' AND EntityID1 = '{1}' AND " + 
                        " PdbID2 = '{2}' AND EntityID2 = '{3}'", 
                        repPdbId, hitRow["QueryEntity"].ToString (), homoPdbId, hitRow["hitEntity"].ToString ()));
                    if (alignRows.Length == 1) // must have only one row
                    {
                        // if the new alignment is better, remove the old one
                        if (IsNewAlignmentBetter(hitRow, alignRows[0]))
                        {
                            HomoGroupTables.homoGroupTables[tableNum].Rows.Remove(alignRows[0]);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        throw new Exception("Add alignment data errors: the entity pair must have only one.");
                    }
                }

                DataRow entryRow = HomoGroupTables.homoGroupTables[tableNum].NewRow();
                entryRow["GroupSeqID"] = groupNum;
                entryRow["PdbID1"] = repPdbId;
                entryRow["EntityID1"] = hitRow["QueryEntity"];
                entryRow["PdbID2"] = homoPdbId;
                entryRow["EntityID2"] = hitRow["HitEntity"];
                entryRow["Identity"] = hitRow["Identity"];
                entryRow["QueryStart"] = hitRow["QueryStart"];
                entryRow["QueryEnd"] = hitRow["QueryEnd"];
                entryRow["HitStart"] = hitRow["HitStart"];
                entryRow["HitEnd"] = hitRow["hitEnd"];
                entryRow["QuerySequence"] = hitRow["QuerySequence"];
                entryRow["HitSequence"] = hitRow["HitSequence"];
                HomoGroupTables.homoGroupTables[tableNum].Rows.Add(entryRow);
          
                if (! addedEntityPairList.Contains(entityPair))
                {
                    addedEntityPairList.Add(entityPair);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newAlignRow"></param>
        /// <param name="existAlignRow"></param>
        public bool IsNewAlignmentBetter (DataRow newAlignRow, DataRow existAlignRow)
        {
            if (IsRedundantChains(existAlignRow))
            {
                return false;
            }
            int newAlignScore = blosumScore.GetScoreForTheAlignment(newAlignRow["QuerySequence"].ToString().TrimEnd(),
                newAlignRow["HitSequence"].ToString().TrimEnd());
            int existAlignScore = blosumScore.GetScoreForTheAlignment(existAlignRow["QuerySequence"].ToString().TrimEnd(),
                existAlignRow["HitSequence"].ToString().TrimEnd());
            if (newAlignScore > existAlignScore)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// the aligned chains are same
        /// </summary>
        /// <param name="alignRow"></param>
        /// <returns></returns>
        private bool IsRedundantChains(DataRow alignRow)
        {
            if (Convert.ToDouble(alignRow["Identity"].ToString()) == 100.0 &&
                Convert.ToInt16(alignRow["QueryStart"].ToString()) == 0 &&
                Convert.ToInt16(alignRow["HitStart"].ToString()) == 0)
            {
                return true;
            }
            return false;
        } 
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="groupNum"></param>
		/// <param name="tableNum"></param>
		/// <param name="repPdbId"></param>
		/// <param name="homoPdbId"></param>
		/// <param name="redundantChainTable"></param>
		/// <param name="leftRepEntityList"></param>
		/// <param name="leftHomoEntityList"></param>
		private void AddReduntEntryAlignInfoToTable (int groupNum, int tableNum, string repPdbId, string homoPdbId, DataTable redundantChainTable, ref List<int> leftRepEntityList, ref List<int> leftHomoEntityList)
		{	
			DataRow[] reduntRows = redundantChainTable.Select (string.Format ("PdbID1 = '{0}' AND PdbID2 = '{1}'", 
				repPdbId, homoPdbId));
			foreach (DataRow reduntRow in reduntRows)
			{
				if (leftRepEntityList.Contains (Convert.ToInt32 (reduntRow["EntityID1"].ToString ())) && 
					leftHomoEntityList.Contains (Convert.ToInt32 (reduntRow["EntityID2"].ToString ())))
				{
					DataRow entryRow = HomoGroupTables.homoGroupTables[tableNum].NewRow ();				
					entryRow["GroupSeqID"] = groupNum;
					entryRow["PdbID1"] = repPdbId;
					entryRow["EntityID1"] = reduntRow["EntityID1"];
					entryRow["PdbID2"] = homoPdbId;
					entryRow["EntityID2"] = reduntRow["EntityID2"];
					entryRow["Identity"] = 100;
					entryRow["QueryStart"] = 0;
					entryRow["QueryEnd"] = 0;
					entryRow["HitStart"] = 0;
					entryRow["HitEnd"] = 0;
					entryRow["QuerySequence"] = "-";
					entryRow["HitSequence"] = "-";
					HomoGroupTables.homoGroupTables[tableNum].Rows.Add (entryRow);
					leftRepEntityList.Remove (Convert.ToInt32 (reduntRow["EntityID1"].ToString ()));
					leftHomoEntityList.Remove (Convert.ToInt32 (reduntRow["EntityID2"].ToString ()));
				}
			}
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupNum"></param>
        /// <param name="tableNum"></param>
        /// <param name="repPdbId"></param>
        /// <param name="homoPdbId"></param>
        /// <param name="redundantChainTable"></param>
        /// <param name="addedEntityPairList"></param>
        private void AddReduntEntryAlignInfoToTable(int groupNum, int tableNum, string repPdbId, string homoPdbId, DataTable redundantChainTable, ref List<string> addedEntityPairList)
        {
            // get the redundant chain rows with repPdbId as representative chains
            DataRow[] reduntRows = GetRedundantChainRows(repPdbId, homoPdbId, redundantChainTable);
            string entityPair = "";

            foreach (DataRow reduntRow in reduntRows)
            {
                entityPair = reduntRow["EntityID1"].ToString() + "_" + reduntRow["EntityID2"].ToString();
                if (addedEntityPairList.Contains(entityPair))
                {
                    continue;
                }

                DataRow entryRow = HomoGroupTables.homoGroupTables[tableNum].NewRow();
                entryRow["GroupSeqID"] = groupNum;
                entryRow["PdbID1"] = repPdbId;
                entryRow["EntityID1"] = reduntRow["EntityID1"];
                entryRow["PdbID2"] = homoPdbId;
                entryRow["EntityID2"] = reduntRow["EntityID2"];
                entryRow["Identity"] = 100;
                entryRow["QueryStart"] = 0;
                entryRow["QueryEnd"] = 0;
                entryRow["HitStart"] = 0;
                entryRow["HitEnd"] = 0;
                entryRow["QuerySequence"] = "-";
                entryRow["HitSequence"] = "-";
                HomoGroupTables.homoGroupTables[tableNum].Rows.Add(entryRow);
                addedEntityPairList.Add(entityPair);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="repPdbId"></param>
        /// <param name="homoPdbId"></param>
        /// <param name="redundantChainTable"></param>
        /// <returns></returns>
        private DataRow[] GetRedundantChainRows(string repPdbId, string homoPdbId, DataTable redundantChainTable)
        {
            DataRow[] reduntRows = redundantChainTable.Select(string.Format("PdbID1 = '{0}' AND PdbID2 = '{1}'",
               repPdbId, homoPdbId));
            DataRow[] inverseRows = redundantChainTable.Select(string.Format("PdbID1 = '{0}' AND PdbID2 = '{1}'",
               homoPdbId, repPdbId));
            List<DataRow> reduntRowList = new List<DataRow> (reduntRows);
            // reverse the redundant chain rows so the repPdbId as representative chains
            foreach (DataRow inverseRow in inverseRows)
            {
                DataRow newReduntRow = redundantChainTable.NewRow();
                newReduntRow.ItemArray = inverseRow.ItemArray;
                ReverseReduntRow(newReduntRow);
                reduntRowList.Add(newReduntRow);
            }
            if (reduntRowList.Count == 0)
            {
                DataRow[] sameRepReduntRows = GetReduntRowsWithSameRep(repPdbId, homoPdbId, redundantChainTable);
                return sameRepReduntRows;
            }

            return reduntRowList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="redundantChainTable"></param>
        /// <returns></returns>
        private DataRow[] GetReduntRowsWithSameRep (string pdbId1, string pdbId2, DataTable redundantChainTable)
        {
            DataRow[] dataRows1 = redundantChainTable.Select(string.Format ("PdbID2 = '{0}'", pdbId1));
            DataRow[] dataRows2 = redundantChainTable.Select(string.Format ("PdbID2 = '{0}'", pdbId2));

            List<DataRow> reduntRowList = new List<DataRow> ();
            foreach (DataRow dataRow1 in dataRows1)
            {
                foreach (DataRow dataRow2 in dataRows2)
                {
                    if (AreTwoRedundantRowsSameRep(dataRow1, dataRow2))
                    {
                        DataRow reduntRow = redundantChainTable.NewRow();
                        reduntRow["PdbID1"] = dataRow1["PdbID2"];
                        reduntRow["AsymChainID1"] = dataRow1["AsymChainID2"];
                        reduntRow["EntityID1"] = dataRow1["EntityID2"];
                        reduntRow["ChainID1"] = dataRow1["ChainID2"];
                        reduntRow["PdbID2"] = dataRow2["PdbID2"];
                        reduntRow["AsymChainID2"] = dataRow2["AsymChainID2"];
                        reduntRow["EntityID2"] = dataRow2["EntityID2"];
                        reduntRow["ChainID2"] = dataRow2["ChainID2"];
                        reduntRowList.Add(reduntRow);
                    }
                }
            }

            return reduntRowList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reduntRow1"></param>
        /// <param name="reduntRow2"></param>
        /// <returns></returns>
        private bool AreTwoRedundantRowsSameRep (DataRow reduntRow1, DataRow reduntRow2)
        {
            if (reduntRow1["PdbID1"].ToString() == reduntRow2["PdbID1"].ToString() &&
                       reduntRow1["AsymChainID1"].ToString() == reduntRow2["AsymChainID1"].ToString())
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reduntRow"></param>
        public void ReverseReduntRow (DataRow reduntRow)
        {
            object temp = reduntRow["PdbID1"];
            reduntRow["PdbID1"] = reduntRow["PdbID2"];
            reduntRow["PdbID2"] = temp;

            temp = reduntRow["EntityID1"];
            reduntRow["EntityID1"] = reduntRow["EntityID2"];
            reduntRow["EntityID2"] = temp;

            temp = reduntRow["AsymChainID1"];
            reduntRow["AsymChainID1"] = reduntRow["AsymChainID2"];
            reduntRow["AsymChainID2"] = temp;

            temp = reduntRow["ChainID1"];
            reduntRow["ChainID1"] = reduntRow["ChainID2"];
            reduntRow["ChainID2"] = temp;
        }
		#endregion
		
		#region add entityID to Table
		/// <summary>
		/// add entity id to each chain in query entry and hit entry
		/// </summary>
		/// <param name="hitTable"></param>
		/// <param name="entityTable">the entity table for all entry in the hittable</param>
		public void AddEntityIDToHitTable (ref DataTable hitTable, DataTable entityTable)
		{
            if (! hitTable.Columns.Contains("QueryEntity"))
            {
                DataColumn queryEntityCol = new DataColumn("QueryEntity", typeof(Int32));
                queryEntityCol.DefaultValue = -1;
                hitTable.Columns.Add(queryEntityCol);
                DataColumn hitEntityCol = new DataColumn("HitEntity", typeof (Int32));
                hitEntityCol.DefaultValue = -1;
                hitTable.Columns.Add(hitEntityCol);
            }
			string queryEntry = "";
			string queryChain = "";
			string hitEntry = "";
			string hitChain = "";
			int queryEntity = -1;
			int hitEntity = -1;
			foreach (DataRow dRow in hitTable.Rows)
			{
				queryEntry = dRow["QueryEntry"].ToString ();
				queryChain = dRow["QueryChain"].ToString ().Trim ();
                queryEntity = Convert.ToInt32(dRow["QueryEntity"].ToString ());
                if (queryEntity == -1)
                {
                    queryEntity = GetEntityFromEntityTable(queryEntry, queryChain, entityTable);
                    if (queryEntity != -1)
                    {
                        dRow["QueryEntity"] = queryEntity;
                    }
                }

				hitEntry = dRow["HitEntry"].ToString ();
				hitChain = dRow["HitChain"].ToString ().Trim ();
                hitEntity = Convert.ToInt32(dRow["HitEntity"].ToString ());
                if (hitEntity == -1)
                {
                    hitEntity = GetEntityFromEntityTable(hitEntry, hitChain, entityTable);
                    if (hitEntity != -1)
                    {
                        dRow["HitEntity"] = hitEntity;
                    }
                }				
				hitTable.AcceptChanges ();
			}
		}

        /// <summary>
        /// the entity ID from redundant pdb chains table
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <param name="redundantChainTable"></param>
        /// <returns></returns>
        private int GetEntityFromRedundantChainTable(string pdbId, string authChain, DataTable redundantChainTable)
        {
            DataRow[] repChainRows = redundantChainTable.Select(string.Format ("PdbID1 = '{0}' AND ChainID1 = '{1}'", 
                pdbId, authChain));
            if (repChainRows.Length > 0)
            {
                return Convert.ToInt16(repChainRows[0]["EntityID1"].ToString());
            }
            else
            {
                DataRow[] reduntChainRows = redundantChainTable.Select(string.Format("PdbID2 = '{0}' AND ChainID2 = '{1}'",
                pdbId, authChain));
                if (reduntChainRows.Length > 0)
                {
                    return Convert.ToInt16(reduntChainRows[0]["EntityID2"].ToString ());
                }
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <param name="entityTable"></param>
        /// <returns></returns>
        private int GetEntityFromEntityTable(string pdbId, string authChain, DataTable entityTable)
        {
            DataRow[] entityRows = entityTable.Select(string.Format ("PdbID = '{0}' AND AuthorChain = '{1}'", 
                pdbId, authChain));
            if (entityRows.Length == 0)
            {
                string entityChain = entityTable.Rows[0]["AuthorChain"].ToString().TrimEnd();
                if (entityChain == "_" && authChain == "A")
                {
                    entityRows = entityTable.Select(string.Format("PdbID = '{0}' AND AuthorChain = '_'",
                                pdbId));
                }
            }
            if (entityRows.Length > 0)
            {
                return Convert.ToInt32(entityRows[0]["EntityID"].ToString());
            }
            return -1;
        }
		/// <summary>
		/// add entity id to each chain in ce/psiblast alignment table
		/// </summary>
		/// <param name="hitTable"></param>
        private void AddEntityIDToHitTable(ref DataTable hitTable)
        {
            if (hitTable.Rows.Count == 0)
            {
                return;
            }
            // read distinct entry from the hittable
            List<string> pdbList = new List<string> ();
            foreach (DataRow dRow in hitTable.Rows)
            {
                if (!pdbList.Contains(dRow["QueryEntry"].ToString()))
                {
                    pdbList.Add(dRow["QueryEntry"].ToString());
                }
                if (!pdbList.Contains(dRow["HitEntry"].ToString()))
                {
                    pdbList.Add(dRow["HitEntry"].ToString());
                }
            }
            DataTable entityTable = GetEntityTable (pdbList.ToArray ());
            AddEntityIDToHitTable(ref hitTable, entityTable);            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbList"></param>
        /// <returns></returns>
        private DataTable GetEntityTable (string[] pdbList)
        {
            string[] subPdbList = null;
            string queryString = "";
            DataTable entityTable = null;
            for (int i = 0; i < pdbList.Length; i += 300)
            {
                subPdbList = ParseHelper.GetSubArray(pdbList, i, 300);
                // retrieve entity id from redundant chains table
                queryString = string.Format("Select Distinct PdbID, EntityID, AsymID, AuthorChain, Sequence From AsymUnit " +
                    " Where PDBID IN ({0}) AND PolymerType = 'polypeptide';", ParseHelper.FormatSqlListString(subPdbList));
                DataTable subEntityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(subEntityTable, ref entityTable);
            }
            return entityTable;
        }
		#endregion

		#region redundant entries	        
		/// <summary>
		/// get all redundant chains for chains of this pdb
        /// the input pdbid may have several chains, 
        /// and each chain may or may not be a representative chain
		/// </summary>
		/// <param name="pdbId"></param>
		/// <returns></returns>
        private DataTable GetReduntChainTable(string pdbId)
        {
            reduntChainTable.Clear();
            string queryString = string.Format("Select distinct crc From PdbCrcMap Where PdbID = '{0}';", pdbId);
            DataTable entryCrcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string crc = "";
            DataRow inputEntryRepChainRow = null;
            foreach (DataRow crcRow in entryCrcTable.Rows)
            {
                crc = crcRow["crc"].ToString();
                queryString = string.Format("Select PdbID, EntityID, AsymID, AuthorChain, isrep From PdbCrcMap Where Crc = '{0}';", crc);
                DataTable crcChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                DataRow[] inputEntryRepRows = crcChainTable.Select(string.Format("PdbID = '{0}' AND IsRep = '1'", pdbId));
                if (inputEntryRepRows.Length > 0)
                {
                    inputEntryRepChainRow = inputEntryRepRows[0];
                }
                else
                {
                    inputEntryRepRows = crcChainTable.Select(string.Format("PdbID = '{0}'", pdbId));
                    inputEntryRepChainRow = inputEntryRepRows[0];
                }
                foreach (DataRow crcChainRow in crcChainTable.Rows)
                {
                    if (crcChainRow["PdbID"].ToString() == inputEntryRepChainRow["PdbID"].ToString () &&
                        crcChainRow["AsymID"].ToString() == inputEntryRepChainRow["AsymID"].ToString())
                    {
                        continue;
                    }
                    DataRow reduntRow = reduntChainTable.NewRow();
                    reduntRow["PdbID1"] = pdbId;
                    reduntRow["AsymChainID1"] = inputEntryRepChainRow["AsymID"];
                    reduntRow["EntityID1"] = inputEntryRepChainRow["EntityID"];
                    reduntRow["ChainID1"] = inputEntryRepChainRow["AuthorChain"];
                    reduntRow["PdbID2"] = crcChainRow["PdbID"];
                    reduntRow["AsymChainID2"] = crcChainRow["AsymID"];
                    reduntRow["EntityID2"] = crcChainRow["EntityID"];
                    reduntRow["ChainID2"] = crcChainRow["AuthorChain"];
                    reduntChainTable.Rows.Add(reduntRow);
                }
            }
            return reduntChainTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable InitializeReduntTable ()
        {
            string[] reduntColumns = { "PdbID1", "ChainID1", "EntityID1", "AsymChainID1", "PdbID2", "ChainID2", "EntityID2", "AsymChainID2"};
            DataTable reduntChainTable = new DataTable("ReduntChains");
            foreach (string reduntCol in reduntColumns)
            {
                reduntChainTable.Columns.Add(new DataColumn (reduntCol));
            }
            return reduntChainTable;
        }		

		/// <summary>
		/// 
		/// </summary>
		/// <param name="pdbId"></param>
		/// <returns></returns>
		private string[] GetEntryRepEntries (string pdbId)
		{
			List<string> repEntryList = new List<string> ();
            string queryString = string.Format("Select distinct crc From PdbCrcMap Where PdbID = '{0}';", pdbId);
            DataTable entryCrcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string crc = "";
            foreach (DataRow crcRow in entryCrcTable.Rows)
            {
                crc = crcRow["crc"].ToString();
                queryString = string.Format("Select PdbID From PdbCrcMap Where crc = '{0}' AND IsRep = '1';", crc);
                DataTable crcRepEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                foreach (DataRow entryRow in crcRepEntryTable.Rows)
                {
                    if (!repEntryList.Contains(entryRow["PdbID"].ToString()))
                    {
                        repEntryList.Add(entryRow["PdbID"].ToString());
                    }
                }
            }
			return repEntryList.ToArray ();
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetRedundantRepEntries(string pdbId, DataTable repReduntChainTable)
        {
            List<string> repEntryList = new List<string> ();
            string repEntry = "";

            DataRow[] repChainRows = repReduntChainTable.Select(
                string.Format ("PdbID1 = '{0}' OR PdbID2 = '{0}'", pdbId));
            foreach (DataRow repChainRow in repChainRows)
            {
                repEntry = repChainRow["PdbID1"].ToString();
                if (!repEntryList.Contains(repEntry))
                {
                    repEntryList.Add(repEntry);
                }
            }

            if (!repEntryList.Contains(pdbId)) // add itself
            {
                repEntryList.Add(pdbId);
            }
            return repEntryList.ToArray ();
        }
		#endregion

        #region get redundant table        
       

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reduntRow1"></param>
        /// <param name="reduntRow2"></param>
        /// <returns></returns>
        private bool AreTwoRowsSameChain(DataRow dataRow1, DataRow dataRow2)
        {
            if (dataRow1["PdbID"].ToString() == dataRow2["PdbID"].ToString() &&
                       dataRow1["AsymID"].ToString() == dataRow2["AsymID"].ToString())
            {
                return true;
            }
            return false;
        }
             

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="repReduntChainTable"></param>
        private void AddChainsToRepRedundantChainTable(string pdbId, DataTable repReduntChainTable)
        {
            string queryString = string.Format("Select PdbID, AuthorChain, AsymID, EntityID From AsymUnit " +
                " Where PdbID = '{0}' and PolymerType = 'polypeptide' Order by EntityID, AuthorChain;", pdbId);
            DataTable chainInfoTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<int> addedEntityList = new List<int> ();
            int entityId = -1;
            foreach (DataRow chainInfoRow in chainInfoTable.Rows)
            {
                entityId = Convert.ToInt16 (chainInfoRow["EntityID"].ToString ().TrimEnd ());
                // for each entity, only add one chain
                if (addedEntityList.Contains(entityId))
                {
                    continue;
                }
                DataRow newReduntRow = repReduntChainTable.NewRow();
                newReduntRow["PdbID1"] = pdbId;
                newReduntRow["ChainID1"] = chainInfoRow["AuthorChain"];
                newReduntRow["AsymChainID1"] = chainInfoRow["AsymID"];
                newReduntRow["EntityID1"] = entityId;
                newReduntRow["PdbID2"] = "-";
                newReduntRow["ChainID2"] = "-";
                newReduntRow["AsymChainID2"] = "-";
                newReduntRow["EntityID2"] = -1;
                repReduntChainTable.Rows.Add(newReduntRow);
                addedEntityList.Add(entityId);
            }
        }
        #endregion

        #region using crc hh alignments
        /// <summary>
        /// Redundant Chains table for entries
        /// </summary>
        /// <param name="pdbList"></param>
        /// <returns>Table contains all redundant chains for each entry</returns>
        private DataTable GetRepChainTable(string[] pdbIds)
        {
            reduntChainTable.Clear();;
            string[] crcs = GetEntryCrcCodes(pdbIds);
            DataTable pdbCrcTable = GetPdbCrcMap(crcs);
            DataRow repEntityRow = null;
            foreach (string crc in crcs)
            {
                DataRow[] crcEntityRows = pdbCrcTable.Select(string.Format("crc = '{0}'", crc));
                repEntityRow = GetCrcRepEntryRow(crcEntityRows);
                foreach (DataRow entityRow in crcEntityRows)
                {
                    if (AreTwoRowsSameChain(entityRow, repEntityRow))
                    {
                        continue;
                    }
                    if (Array.IndexOf (pdbIds, entityRow["PdbID"].ToString()) > -1 || 
                        entityRow["PdbID"].ToString () == repEntityRow["PdbID"].ToString ())  // add same entry
                    {
                        DataRow newReduntRow = reduntChainTable.NewRow();
                        newReduntRow["PdbID1"] = repEntityRow["PdbID"];
                        newReduntRow["ChainID1"] = repEntityRow["AuthorChain"];
                        newReduntRow["AsymChainID1"] = repEntityRow["AsymID"];
                        newReduntRow["EntityID1"] = repEntityRow["EntityID"];
                        newReduntRow["PdbID2"] = entityRow["PdbID"];
                        newReduntRow["ChainID2"] = entityRow["AuthorChain"];
                        newReduntRow["AsymChainID2"] = entityRow["AsymID"];
                        newReduntRow["EntityID2"] = entityRow["EntityID"];
                        reduntChainTable.Rows.Add(newReduntRow);
                    }                   
                }
            }
            return reduntChainTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <param name="crcPdbTable"></param>
        /// <returns></returns>
        private string[] GetEntryCrcCodes(string[] pdbIds)
        {
            string queryString = "";
            List<string> crcList = new List<string> ();
            string crc = "";
            for (int i = 0; i < pdbIds.Length; i += 300)
            {
                string[] subPdbIds = ParseHelper.GetSubArray(pdbIds, i, 300);
                queryString = string.Format("Select distinct crc From PdbCrcMap Where PdbID IN ({0});", ParseHelper.FormatSqlListString(subPdbIds));
                DataTable crcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                foreach (DataRow crcRow in crcTable.Rows)
                {
                    crc = crcRow["crc"].ToString().TrimEnd();
                    if (!crcList.Contains(crc))
                    {
                        crcList.Add(crc);
                    }
                }
            }

            return crcList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crcs"></param>
        /// <returns></returns>
        private DataTable GetPdbCrcMap (string[] crcs)
        {
            string queryString = "";
            DataTable crcPdbMapTable = null;
            for (int i = 0; i < crcs.Length; i += 300)
            {
                string[] subCrcs = ParseHelper.GetSubArray(crcs, i, 300);
                queryString = string.Format("Select * From PdbCrcMap Where crc in ({0});", ParseHelper.FormatSqlListString (subCrcs));
                DataTable crcPdbTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(crcPdbTable, ref crcPdbMapTable);
            }
            return crcPdbMapTable;
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="crcEntityTable"></param>
        /// <param name="pdbList"></param>
        /// <returns></returns>
        private DataRow GetCrcRepEntryRow(DataRow[] crcEntityRows)
        {
            DataRow repEntityRow = null;

            foreach (DataRow entityRow in crcEntityRows)
            {
                if (entityRow["IsRep"].ToString() == "1")
                {
                    repEntityRow = entityRow;
                }
            }
            if (repEntityRow == null && crcEntityRows.Length > 0)
            {
                repEntityRow = crcEntityRows[0];
            }
            return repEntityRow;
        } 
        #endregion

        #region entity info
        /// <summary>
		/// the number of entities in an entry
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="entityTable"></param>
		/// <returns></returns>
		public int GetEntityNum (string pdbId, DataTable entityTable)
		{	
			List<int> thisEntityList = GetEntityList (pdbId, entityTable);
			return thisEntityList.Count;
		}

		/// <summary>
		/// the entities of entry
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="entityTable"></param>
		/// <returns></returns>
		public List<int> GetEntityList (string pdbId, DataTable entityTable)
		{
			List<int> thisEntityList = new List<int> ();
			// only count those entities with scop classification if the entry is in SCOP
			DataRow[] entityRows = entryEntityFamilyTable.Select (string.Format ("PdbID = '{0}'", pdbId));
			if (entityRows.Length > 0)
			{
				foreach (DataRow entityRow in entityRows)
				{
					thisEntityList.Add (Convert.ToInt32 (entityRow["EntityID"].ToString ()));
				}
			}
			else
			{
				// otherwise, count the number of entities from asymunit table
				DataRow[] thisEntityRows = entityTable.Select (string.Format ("PdbID = '{0}'", pdbId));
			
				foreach (DataRow entityRow in thisEntityRows)
				{
					string sequence = entityRow["Sequence"].ToString ().Trim ();
					// exclude entities with size less than minimum residues number
					if (sequence.Length < AppSettings.parameters.contactParams.minNumResidueInChain)
					{
						continue;
					}
					int entityId = Convert.ToInt32 (entityRow["EntityID"].ToString ());
					if (! thisEntityList.Contains (entityId))
					{
						thisEntityList.Add (entityId);
					}
				}
			}
			return thisEntityList;
		}
		/// <summary>
		/// get entity info table
		/// </summary>
		/// <param name="pdbList"></param>
		/// <returns></returns>
		private DataTable GetEntityTable (List<string> pdbList)
		{
            DataTable entityTable = null;
            if (pdbList.Count > 100)  // limit pdb to be 100 in the IN condition
            {
                entityTable = GetEntityTableForBigList(pdbList);
            }
            else
            {
                string queryString = string.Format("Select Distinct PdbID, AsymID, AuthorChain, EntityID, Sequence From AsymUnit " +
                    " Where PDBID IN ({0}) AND PolymerType = 'polypeptide';", ParseHelper.FormatSqlListString(pdbList.ToArray ()));
                entityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
			return entityTable;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbList"></param>
        /// <returns></returns>
        private DataTable GetEntityTableForBigList (List<string> pdbList)
        {
            List<string> partPdbList = new List<string> ();
            DataTable entityTable = null;
            for (int i = 0; i < pdbList.Count; i+=100)
            {
                if (i + 100 < pdbList.Count)
                {
                    partPdbList = pdbList.GetRange(i, 100);
                }
                else
                {
                    partPdbList = pdbList.GetRange(i, pdbList.Count - i);
                }
                string queryString = string.Format("Select Distinct PdbID, AsymID, AuthorChain, EntityID, Sequence From AsymUnit " +
                        " Where PDBID IN ({0}) AND PolymerType = 'polypeptide';", ParseHelper.FormatSqlListString(partPdbList.ToArray ()));
                DataTable partEntityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(partEntityTable, ref entityTable);
            }
            return entityTable;
        }
		#endregion

        #region entry alignments
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupNum"></param>
        /// <param name="tableNum"></param>
        /// <param name="alignmentTable"></param>
        public void AssignAlignmentInfoToTable(int groupNum, int tableNum, DataTable alignmentTable)
        {
            List<string> addedEntityList = new List<string> ();
            string entityPair = "";
            foreach (DataRow alignmentRow in alignmentTable.Rows)
            {
                entityPair = alignmentRow["QueryEntry"].ToString() + alignmentRow["QueryEntity"].ToString() +
                    "_" + alignmentRow["HitEntry"].ToString() + alignmentRow["HitEntity"].ToString();
                // overhead, but just make sure no duplicate data are added
                // the alignments data collecting is complicated, 
                // cannot be sure each step is error-proof or bug-proof
                if (addedEntityList.Contains(entityPair)) 
                {
                    continue;
                }
                DataRow entryRow = HomoGroupTables.homoGroupTables[tableNum].NewRow();
                entryRow["GroupSeqID"] = groupNum;
                entryRow["PdbID1"] = alignmentRow["QueryEntry"];
                entryRow["EntityID1"] = alignmentRow["QueryEntity"];
                entryRow["PdbID2"] = alignmentRow["HitEntry"];
                entryRow["EntityID2"] = alignmentRow["HitEntity"];
                entryRow["Identity"] = alignmentRow["Identity"];
                entryRow["QueryStart"] = alignmentRow["QueryStart"];
                entryRow["QueryEnd"] = alignmentRow["QueryEnd"];
                entryRow["HitStart"] = alignmentRow["HitStart"];
                entryRow["HitEnd"] = alignmentRow["hitEnd"];
                entryRow["QuerySequence"] = alignmentRow["QuerySequence"];
                entryRow["HitSequence"] = alignmentRow["HitSequence"];
                HomoGroupTables.homoGroupTables[tableNum].Rows.Add(entryRow);
                addedEntityList.Add(entityPair);
            }
        }       

        /// <summary>
        /// The alignments between all sequences from these two input entries
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        public DataTable RetrieveEntryHHAlignments(string pdbId1, string pdbId2)
        {        
            DataTable alignmentTable = hhAlignments.RetrieveAllHHAlignments(pdbId1, pdbId2);        
            return alignmentTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignmentTable"></param>
        /// <param name="reduntAlignTable"></param>
        private void AddReduntAlignRows(DataTable alignmentTable, DataTable reduntAlignTable)
        {
            foreach (DataRow reduntRow in reduntAlignTable.Rows)
            {
                DataRow[] alignRows = alignmentTable.Select
                (string.Format ("QueryEntry = '{0}' AND QueryEntity = '{1}' AND " + 
                " HitEntry = '{2}' AND HitEntity = '{3}'",
                reduntRow["QueryEntry"], reduntRow["QueryEntity"], reduntRow["HitEntry"], reduntRow["HitEntity"]));
                if (alignRows.Length > 0)
                {
                    alignRows[0]["QueryLength"] = 0;
                    alignRows[0]["QueryStart"] = 0;
                    alignRows[0]["QueryEnd"] = 0;
                    alignRows[0]["QuerySequence"] = "-";
                    alignRows[0]["HitLength"] = 0;
                    alignRows[0]["HitStart"] = 0;
                    alignRows[0]["HitEnd"] = 0;
                    alignRows[0]["HitSequence"] = "-";
                    alignRows[0]["Identity"] = 100;
                }
                else
                {
                    DataRow alignRow = alignmentTable.NewRow();
                    alignRow["QueryEntry"] = reduntRow["QueryEntry"];
                    alignRow["QueryChain"] = reduntRow["QueryChain"];
                    alignRow["QueryEntity"] = reduntRow["QueryEntity"];
                    alignRow["HitEntry"] = reduntRow["HitEntry"];
                    alignRow["HitEntity"] = reduntRow["HitEntity"];
                    alignRow["HitChain"] = reduntRow["HitChain"];
                    alignRow["QueryStart"] = 0;
                    alignRow["QueryEnd"] = 0;
                    alignRow["QuerySequence"] = "-";
                    alignRow["HitStart"] = 0;
                    alignRow["HitEnd"] = 0;
                    alignRow["HitSequence"] = "-";
                    alignRow["Identity"] = 100;
                    alignmentTable.Rows.Add(alignRow);
                }
            }
            alignmentTable.AcceptChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="repChainTable"></param>
        /// <returns></returns>
        private DataTable GetRedundantPdbChainsAlign (string pdbId1, string pdbId2, DataTable repChainTable)
        {
            List<string> repReduntRepEntries = new List<string> (GetRedundantRepEntries(pdbId1, repChainTable));
            List<string> homoReduntRepEntries = new List<string> (GetRedundantRepEntries(pdbId2, repChainTable));

            DataTable alignmentTable = InitializeAlignTable ();

            string entityPair = "";
            List<string> addedEntityPairList = new List<string> ();
            // find all the alignment between these two entries
            foreach (string repReduntRepEntry in repReduntRepEntries)
            {
                foreach (string homoReduntRepEntry in homoReduntRepEntries)
                {
                    if (repReduntRepEntry == homoReduntRepEntry)
                    {
                        if (repReduntRepEntry == pdbId1)
                        {
                            DataRow[] reduntRows = repChainTable.Select (string.Format("PdbID1 = '{0}' AND PdbID2 = '{1}'", 
                                pdbId1, pdbId2));
                            foreach (DataRow reduntRow in reduntRows)
                            {
                                entityPair = reduntRow["EntityID1"].ToString () + "_" +
                                    reduntRow["EntityID2"].ToString ();

                                if (addedEntityPairList.Contains(entityPair))
                                {
                                    continue;
                                }
                                addedEntityPairList.Add(entityPair);

                                DataRow alignRow = alignmentTable.NewRow();
                                alignRow["QueryEntry"] = pdbId1;
                                alignRow["QueryChain"] = reduntRow["ChainID1"];
                                alignRow["QueryEntity"] = reduntRow["EntityID1"];
                                alignRow["HitEntry"] = pdbId2;
                                alignRow["HitEntity"] = reduntRow["EntityID2"];
                                alignRow["HitChain"] = reduntRow["ChainID2"];
                                alignRow["QueryLength"] = 100;
                                alignRow["QueryStart"] = 0;
                                alignRow["QueryEnd"] = 0;
                                alignRow["QuerySequence"] = "-";
                                alignRow["HitLength"] = 0;
                                alignRow["HitStart"] = 0;
                                alignRow["HitEnd"] = 0;
                                alignRow["HitSequence"] = "-";
                                alignRow["Identity"] = 100;
                                alignmentTable.Rows.Add(alignRow);
                            }
                        }
                        else if (repReduntRepEntry == pdbId2)
                        {
                            DataRow[] reduntRows = repChainTable.Select(string.Format("PdbID1 = '{0}' AND PdbID2 = '{1}'",
                               pdbId2, pdbId1));
                            foreach (DataRow reduntRow in reduntRows)
                            {
                                entityPair = reduntRow["EntityID1"].ToString() + "_" +
                                   reduntRow["EntityID2"].ToString();

                                if (addedEntityPairList.Contains(entityPair))
                                {
                                    continue;
                                }
                                addedEntityPairList.Add(entityPair);

                                DataRow alignRow = alignmentTable.NewRow();
                                alignRow["QueryEntry"] = pdbId1;
                                alignRow["QueryChain"] = reduntRow["ChainID2"];
                                alignRow["QueryEntity"] = reduntRow["EntityID2"];
                                alignRow["HitEntry"] = pdbId2;
                                alignRow["HitChain"] = reduntRow["ChainID1"];
                                alignRow["HitEntity"] = reduntRow["EntityID1"];
                                alignRow["QueryLength"] = 100;
                                alignRow["QueryStart"] = 0;
                                alignRow["QueryEnd"] = 0;
                                alignRow["QuerySequence"] = "-";
                                alignRow["HitLength"] = 0;
                                alignRow["HitStart"] = 0;
                                alignRow["HitEnd"] = 0;
                                alignRow["HitSequence"] = "-";
                                alignRow["Identity"] = 100;
                                alignmentTable.Rows.Add(alignRow);
                            }
                        }
                        else // both are not rep entry
                        {
                            DataRow[] repRows = repChainTable.Select(string.Format("PdbID1 = '{0}' AND PdbID2 = '{1}'", 
                                repReduntRepEntry, pdbId1));
                            foreach (DataRow repRow in repRows)
                            {
                                DataRow[] repRows2 = repChainTable.Select(string.Format ("PdbID1 = '{0}' AND EntityID1 = '{1}' " + 
                                    "AND PdbID2 = '{2}'", repRow["PdbID1"], repRow["EntityID1"], pdbId2));
                                if (repRows2.Length == 0)
                                {
                                    continue;
                                }
                                entityPair = repRow["EntityID2"].ToString() + "_" + repRows2[0]["EntityID2"].ToString();
                                if (addedEntityPairList.Contains(entityPair))
                                {
                                    continue;
                                }
                                addedEntityPairList.Add(entityPair);

                                DataRow alignRow = alignmentTable.NewRow();
                                alignRow["QueryEntry"] = pdbId1;
                                alignRow["QueryChain"] = repRow["ChainID2"];
                                alignRow["QueryEntity"] = repRow["EntityID2"];
                                alignRow["HitEntry"] = pdbId2;
                                alignRow["HitChain"] = repRows2[0]["ChainID2"];
                                alignRow["HitEntity"] = repRows2[0]["EntityID2"];
                                alignRow["QueryLength"] = 100;
                                alignRow["QueryStart"] = 0;
                                alignRow["QueryEnd"] = 0;
                                alignRow["QuerySequence"] = "-";
                                alignRow["HitLength"] = 0;
                                alignRow["HitStart"] = 0;
                                alignRow["HitEnd"] = 0;
                                alignRow["HitSequence"] = "-";
                                alignRow["Identity"] = 100;
                                alignmentTable.Rows.Add(alignRow);
                            }
                        }
                        break; // since only pair entries can be same
                    }
                }
            }
            return alignmentTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="alignTable"></param>
        /// <param name="entityTable"></param>
        /// <returns></returns>
        private bool AreEntitiesLeft(string pdbId1, string pdbId2, DataTable alignTable, DataTable entityTable)
        {
            List<int> entityList1 = GetEntityList(pdbId1, entityTable);
            List<int> entityList2 = GetEntityList(pdbId2, entityTable);

            List<int> alignEntityList1 = new List<int>();
            List<int> alignEntityList2 = new List<int>();
            int entityId1 = 0;
            int entityId2 = 0;
            foreach (DataRow alignRow in alignTable.Rows)
            {
                entityId1 = Convert.ToInt32(alignRow["QueryEntity"].ToString ());
                entityId2 = Convert.ToInt32(alignRow["HitEntity"].ToString ());
                if (!alignEntityList1.Contains(entityId1))
                {
                    alignEntityList1.Add(entityId1);
                }
                if (!alignEntityList2.Contains(entityId2))
                {
                    alignEntityList2.Add(entityId2);
                }
            }
            if (entityList1.Count == alignEntityList1.Count && entityList2.Count == alignEntityList2.Count)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable InitializeAlignTable()
        {
            string queryString = "SELECT First 1 QueryEntry, QueryChain, HitEntry, HitChain, " +
                        " QueryLength, QueryStart, QueryEnd, QuerySequence, " +
                        " HitLength, HitStart, HitEnd, HitSequence, Identity" +
                        " FROM FatcatAlignments";
            DataTable alignTable = ProtCidSettings.alignmentQuery.Query(queryString);
            alignTable.Clear();
            alignTable.Columns.Add(new DataColumn("QueryEntity"));
            alignTable.Columns.Add(new DataColumn("HitEntity"));
            return alignTable;
        }

        /// <summary>
        /// alignment between representative and its homologous entries
        /// </summary>
        /// <param name="repPdbId"></param>
        /// <param name="pdbList"></param>
        /// <param name="groupNum"></param>
        public void RetrieveRepEntryAllAlignment(string repPdbId, string[] alignPdbIds, int groupNum)
        {
            if (alignPdbIds.Length == 0)
            {
                return;
            }
            string[] theWholeList = new string[alignPdbIds.Length + 1];
            theWholeList[0] = repPdbId;
            Array.Copy(alignPdbIds, 0, theWholeList, 1, alignPdbIds.Length);
            // the redundant chains info from redundant pdb chains table
            DataTable reduntChainTable = GetRepChainTable(theWholeList);
            // entity info for each entry in pdbList
            DataTable entityTable = GetEntityTable(theWholeList);

            RetrieveRepEntryAllAlignment(repPdbId, alignPdbIds, groupNum, HomoGroupTables.HomoRepEntryAlign, reduntChainTable, entityTable);
        }
        /// <summary>
        /// sequence alignment between repsentative entry and 
        /// its homologous entries
        /// </summary>
        /// <param name="repPdbId"></param>
        /// <param name="pdbList"></param>
        /// <param name="groupNum"></param>
        private void RetrieveRepEntryAllAlignment(string repPdbId, string[] alignPdbIds, int groupNum, int tableNum, DataTable reduntChainTable, DataTable entityTable)
        {
            List<int> leftRepEntityList = null;
            List<int> homoEntityList = null;
            List<int> leftHomoEntityList = null;

            // entity list of repPdbId
            List<int> repEntityList = GetEntityList(repPdbId, entityTable);
            // entity pairs are added
            List<string> addedEntityPairList = new List<string> ();

            foreach (string pdbId in alignPdbIds)
            {
                addedEntityPairList.Clear();

                // add redundant chains to table which have identity = 100%
                AddReduntEntryAlignInfoToTable(groupNum, tableNum, repPdbId, pdbId,
                    reduntChainTable, ref addedEntityPairList);

                leftRepEntityList = new List<int> (repEntityList);
                // entity list for pdbId
                homoEntityList = GetEntityList(pdbId, entityTable);
                leftHomoEntityList = new List<int> (homoEntityList);
                GetLeftEntityList(addedEntityPairList, ref leftRepEntityList, ref leftHomoEntityList);

                if (leftRepEntityList.Count == 0 && leftHomoEntityList.Count == 0)
                {
                    continue;
                }

                try
                {
                    GetEntryAllAlignments(groupNum, tableNum, repPdbId, pdbId, reduntChainTable, entityTable,
                        ref leftRepEntityList, ref leftHomoEntityList, ref addedEntityPairList);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving alignments between " +
                        repPdbId + " and " + pdbId + " errors: " + ex.Message);
                    continue;
                }

                GetLeftEntityList(addedEntityPairList, ref leftRepEntityList, ref leftHomoEntityList);

#if DEBUG
                WriterNonAlignedEntityPairsIntoLogFile(repPdbId, pdbId, leftRepEntityList, repEntityList,
                    leftHomoEntityList, homoEntityList, entityTable);
                nonAlignedDataWriter.Flush();
#endif
            }

        }
        #endregion

        #region entry alignments -- added on July 11, 2018
        /// <summary>
        /// The alignments between all sequences from these two input entries
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        public DataTable RetrieveEntryAlignments(string pdbId1, string pdbId2)
        {
            DataTable alignTable = hhAlignments.RetrieveAllHHAlignments(pdbId1, pdbId2);
            DataTable fatcatAlignmentTable = GetAlignTable(pdbId1, pdbId2, "FatcatAlignments");
            CompareAlignments(alignTable, fatcatAlignmentTable);

            DataTable alignmentTable = AssignAlignmentInfoToTable(alignTable);
            return alignmentTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupNum"></param>
        /// <param name="tableNum"></param>
        /// <param name="alignmentTable"></param>
        public DataTable AssignAlignmentInfoToTable(DataTable hitTable)
        {
            DataTable alignmentTable = new DataTable();
            string[] repAlignCols = {"PdbID1", "EntityID1", "PdbID2", "EntityID2",
										"Identity", "QueryStart", "QueryEnd", "HitStart", "HitEnd", 
										"QuerySequence", "HitSequence"};
            foreach (string repCol in repAlignCols)
            {
                alignmentTable.Columns.Add(new DataColumn(repCol));
            }
            List<string> addedEntityList = new List<string> ();
            string entityPair = "";
            foreach (DataRow hitRow in hitTable.Rows)
            {
                entityPair = hitRow["QueryEntry"].ToString() + hitRow["QueryEntity"].ToString() +
                    "_" + hitRow["HitEntry"].ToString() + hitRow["HitEntity"].ToString();
                // overhead, but just make sure no duplicate data are added
                // the alignments data collecting is complicated, 
                // cannot be sure each step is error-proof or bug-proof
                if (addedEntityList.Contains(entityPair))
                {
                    continue;
                }
                DataRow alignRow = alignmentTable.NewRow();
                alignRow["PdbID1"] = hitRow["QueryEntry"];
                alignRow["EntityID1"] = hitRow["QueryEntity"];
                alignRow["PdbID2"] = hitRow["HitEntry"];
                alignRow["EntityID2"] = hitRow["HitEntity"];
                alignRow["Identity"] = hitRow["Identity"];
                alignRow["QueryStart"] = hitRow["QueryStart"];
                alignRow["QueryEnd"] = hitRow["QueryEnd"];
                alignRow["HitStart"] = hitRow["HitStart"];
                alignRow["HitEnd"] = hitRow["hitEnd"];
                alignRow["QuerySequence"] = hitRow["QuerySequence"];
                alignRow["HitSequence"] = hitRow["HitSequence"];
                alignmentTable.Rows.Add(alignRow);
                addedEntityList.Add(entityPair);
            }
            return alignmentTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private DataTable GetAlignTable(string pdbId1, string pdbId2, string tableName)
        {
            string[] pdbIds = new string[2];
            pdbIds[0] = pdbId1;
            pdbIds[1] = pdbId2;
            DataTable entityTable = GetEntityTable(pdbIds);
            string[] repEntries1 = GetRepRedundantEntries(pdbId1);
            string[] repEntries2 = GetRepRedundantEntries(pdbId2);
            string[] nonrepEntries1 = GetNonRepRedundantEntries(pdbId1);
            string[] nonrepEntries2 = GetNonRepRedundantEntries(pdbId2);

            string queryString = "";

            queryString = string.Format("SELECT QueryEntry, QueryChain, HitEntry, HitChain, " +
               " QueryLength, QueryStart, QueryEnd, QuerySequence, " +
               " HitLength, HitStart, HitEnd, HitSequence, Identity" +
               " FROM {0} WHERE QueryEntry In ({1}) AND HitEntry In ({2});",  // representative entries should not be a big number 
               tableName, ParseHelper.FormatSqlListString(repEntries1), ParseHelper.FormatSqlListString(repEntries2));
            DataTable hitTable = ProtCidSettings.alignmentQuery.Query(queryString);
            queryString = string.Format("SELECT QueryEntry, QueryChain, HitEntry, HitChain, " +
                        " QueryLength, QueryStart, QueryEnd, QuerySequence, " +
                        " HitLength, HitStart, HitEnd, HitSequence, Identity" +
                        " FROM {0} WHERE QueryEntry In ({2}) AND HitEntry In ({1});",
                        tableName, ParseHelper.FormatSqlListString(repEntries1), ParseHelper.FormatSqlListString(repEntries2));
            DataTable reversehitTable = ProtCidSettings.alignmentQuery.Query(queryString);

            foreach (DataRow thisHitRow in reversehitTable.Rows)
            {
                // reverse the alignment
                ReverseAlignmentRow(thisHitRow);
                DataRow newRow = hitTable.NewRow();
                newRow.ItemArray = thisHitRow.ItemArray;
                hitTable.Rows.Add(newRow);
            }
            if (hitTable.Rows.Count == 0)
            {
                foreach (string reduntEntry1 in nonrepEntries1)
                {
                    foreach (string reduntEntry2 in nonrepEntries2)
                    {
                        queryString = string.Format("SELECT QueryEntry, QueryChain, HitEntry, HitChain, " +
                              " QueryLength, QueryStart, QueryEnd, QuerySequence, " +
                              " HitLength, HitStart, HitEnd, HitSequence, Identity" +
                              " FROM {0} WHERE QueryEntry = '{1}' AND HitEntry = '{2}';",  // representative entries should not be a big number 
                              tableName, reduntEntry1, reduntEntry2);
                        hitTable = ProtCidSettings.alignmentQuery.Query(queryString);
                        if (hitTable.Rows.Count == 0)
                        {
                            queryString = string.Format("SELECT QueryEntry, QueryChain, HitEntry, HitChain, " +
                              " QueryLength, QueryStart, QueryEnd, QuerySequence, " +
                              " HitLength, HitStart, HitEnd, HitSequence, Identity" +
                              " FROM {0} WHERE QueryEntry = '{2}' AND HitEntry = '{1}';",  // representative entries should not be a big number 
                              tableName, reduntEntry1, reduntEntry2);
                            reversehitTable = ProtCidSettings.alignmentQuery.Query(queryString);

                            foreach (DataRow thisHitRow in reversehitTable.Rows)
                            {
                                // reverse the alignment
                                ReverseAlignmentRow(thisHitRow);
                                DataRow newRow = hitTable.NewRow();
                                newRow.ItemArray = thisHitRow.ItemArray;
                                hitTable.Rows.Add(newRow);
                            }
                        }
                        if (hitTable.Rows.Count > 0)
                        {
                            break;
                        }
                    }
                    if (hitTable.Rows.Count > 0)
                    {
                        break;
                    }
                }
            }
            UpdateHitTable(hitTable, pdbId1, pdbId2);
            AddEntityIDToHitTable(ref hitTable, entityTable);
            return hitTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hitTable"></param>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        private void UpdateHitTable(DataTable hitTable, string pdbId1, string pdbId2)
        {
            Dictionary<string, string> repCrcChainDict = new Dictionary<string, string>();
            string crcChain = "";
            string queryPdbId = "";
            string queryChainId = "";
            string hitPdbId = "";
            string hitChainId = "";
            List<DataRow> removeHitRowList = new List<DataRow>();
            for (int i = 0; i < hitTable.Rows.Count; i++)
            {
                queryPdbId = hitTable.Rows[i]["QueryEntry"].ToString();

                if (queryPdbId != pdbId1)
                {
                    queryChainId = hitTable.Rows[i]["QueryChain"].ToString();
                    crcChain = GetSameCrcChain(queryPdbId, queryChainId, pdbId1, repCrcChainDict);
                    if (crcChain == "")
                    {
                        removeHitRowList.Add(hitTable.Rows[i]);
                        continue;
                    }
                    else
                    {
                        hitTable.Rows[i]["QueryEntry"] = pdbId1;
                        hitTable.Rows[i]["QueryChain"] = crcChain;
                    }
                }
                hitPdbId = hitTable.Rows[i]["HitEntry"].ToString();
                if (hitPdbId != pdbId2)
                {
                    hitChainId = hitTable.Rows[i]["HitChain"].ToString();
                    crcChain = GetSameCrcChain(hitPdbId, hitChainId, pdbId2, repCrcChainDict);
                    if (crcChain == "")
                    {
                        removeHitRowList.Add(hitTable.Rows[i]);
                    }
                    else
                    {
                        hitTable.Rows[i]["HitEntry"] = pdbId2;
                        hitTable.Rows[i]["HitChain"] = crcChain;
                    }
                }
            }
            foreach (DataRow removeRow in removeHitRowList)
            {
                try
                {
                    hitTable.Rows.Remove(removeRow); // should be find if not removed
                }
                catch { }                     
            }
            hitTable.AcceptChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetRepRedundantEntries(string pdbId)
        {
            string[] entries = new string[1];
            entries[0] = pdbId;
            string[] crcs = GetEntryCrcCodes(entries);
            string queryString = string.Format("Select Distinct PdbID From PdbCrcMap Where Crc In ({0}) AND IsRep = '1';", ParseHelper.FormatSqlListString(crcs));
            DataTable sameCrcEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] sameCrcEntries = new string[sameCrcEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in sameCrcEntryTable.Rows)
            {
                sameCrcEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return sameCrcEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <param name="searchPdbId"></param>
        /// <returns></returns>
        private string GetSameCrcChain(string pdbId, string authChain, string searchPdbId, Dictionary<string, string> repCrcChainDict)
        {
            if (repCrcChainDict.ContainsKey(pdbId + authChain))
            {
                return repCrcChainDict[pdbId + authChain].Substring(4, repCrcChainDict[pdbId + authChain].Length - 4);
            }
            string queryString = string.Format("Select AuthorChain From PdbCrcMap Where PdbID = '{0}' AND " +
                " Crc In (Select Crc From PdbCrcMap Where PdbID = '{1}' AND AuthorChain = '{2}');", searchPdbId, pdbId, authChain);
            DataTable chainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string crcAuthChain = "";
            if (chainTable.Rows.Count > 0)
            {
                crcAuthChain = chainTable.Rows[0]["AuthorChain"].ToString().TrimEnd();
                repCrcChainDict.Add(pdbId + authChain, searchPdbId + crcAuthChain);
            }
            return crcAuthChain;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetNonRepRedundantEntries(string pdbId)
        {
            string[] entries = new string[1];
            entries[0] = pdbId;
            string[] crcs = GetEntryCrcCodes(entries);
            string queryString = string.Format("Select Distinct PdbID From PdbCrcMap Where Crc In ({0}) AND IsRep = '0';", ParseHelper.FormatSqlListString(crcs));
            DataTable sameCrcEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] sameCrcEntries = new string[sameCrcEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in sameCrcEntryTable.Rows)
            {
                sameCrcEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return sameCrcEntries;
        }
        #endregion

        #region chain alignments
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="entityId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="entityId2"></param>
        /// <returns></returns>
        public DataTable RetrieveChainAlignment(string pdbId1, int entityId1, string pdbId2, int entityId2)
        {
            string repPdbId1 = "";
            string repChainId1 = "";
            string chainId1 = ""; ;
            GetRepChain(pdbId1, entityId1, out repPdbId1, out repChainId1, out chainId1);

            string repPdbId2 = "";
            string repChainId2 = "";
            string chainId2 = "";
            GetRepChain(pdbId2, entityId2, out repPdbId2, out repChainId2, out chainId2);

            DataTable alignmentTable = FindAlignmentTable(repPdbId1, repChainId1, repPdbId2, repChainId2);
            if (alignmentTable.Rows.Count == 0)
            {
                alignmentTable = FindAlignmentTable(pdbId1, chainId1, pdbId2, chainId2);
                if (alignmentTable.Rows.Count == 0)
                {
                    alignmentTable = FindAlignmentTable(pdbId1, chainId1, repPdbId2, repChainId2);
                    if (alignmentTable.Rows.Count == 0)
                    {
                        alignmentTable = FindAlignmentTable(repPdbId1, repChainId1, pdbId2, chainId2);
                    }
                }
            }

            if (alignmentTable.Columns.IndexOf("QueryEntityID") < 0)
            {
                alignmentTable.Columns.Add(new DataColumn("QueryEntityID"));
                alignmentTable.Columns.Add(new DataColumn("HitEntityID"));
            }
            foreach (DataRow alignmentRow in alignmentTable.Rows)
            {
                if (repPdbId1 != pdbId1)
                {
                    alignmentRow["QueryEntry"] = pdbId1;
                    alignmentRow["QueryChain"] = chainId1;
                }
                else if (repChainId1 != chainId1)
                {
                    alignmentRow["QueryChain"] = chainId1;
                }
                if (repPdbId2 != pdbId2)
                {
                    alignmentRow["HitEntry"] = pdbId2;
                    alignmentRow["HitChain"] = chainId2;
                }
                else if (repChainId2 != chainId2)
                {
                    alignmentRow["HitChain"] = chainId2;
                }

                alignmentRow["QueryEntityID"] = entityId1;
                alignmentRow["HitEntityID"] = entityId2;
            }
            return alignmentTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="entityId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="entityId2"></param>
        /// <returns></returns>
        public DataTable RetrieveChainHHAlignment(string pdbId1, int entityId1, string pdbId2, int entityId2)
        {
            DataTable alignmentTable = hhAlignments.RetrieveAllHHAlignments(pdbId1, entityId1, pdbId2, entityId2);        
           
            return alignmentTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="entityId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="entityId2"></param>
        public void RetrieveOrigChainAlignment(string pdbId1, int entityId1, string pdbId2, int entityId2)
        {
            string chainId1 = GetAuthorChainForEntity(pdbId1, entityId1);
            string chainId2 = GetAuthorChainForEntity(pdbId2, entityId2);
            DataTable alignmentTable = FindAlignmentTable(pdbId1, chainId1, pdbId2, chainId2);

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="chainId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="chainId2"></param>
        public DataTable RetrieveChainAlignment(string pdbId1, string chainId1, string pdbId2, string chainId2)
        {
            string repPdbId1 = "";
            string repChainId1 = "";
            int entityId1 = -1;
            GetRepChain(pdbId1, chainId1, out repPdbId1, out repChainId1, out entityId1);

            string repPdbId2 = "";
            string repChainId2 = "";
            int entityId2 = -1;
            GetRepChain(pdbId2, chainId2, out repPdbId2, out repChainId2, out entityId2);

            DataTable alignmentTable = FindAlignmentTable(repPdbId1, repChainId1, repPdbId2, repChainId2);

            if (alignmentTable.Columns.IndexOf("QueryEntityID") < 0)
            {
                alignmentTable.Columns.Add(new DataColumn("QueryEntityID"));
                alignmentTable.Columns.Add(new DataColumn ("HitEntityID"));
            }
            foreach (DataRow alignmentRow in alignmentTable.Rows)
            {
                if (repPdbId1 != pdbId1)
                {
                    alignmentRow["QueryEntry"] = pdbId1;
                    alignmentRow["QueryChain"] = chainId1;
                }
                else if (repChainId1 != chainId1)
                {
                    alignmentRow["QueryChain"] = chainId1;
                }
                if (repPdbId2 != pdbId2)
                {
                    alignmentRow["HitEntry"] = pdbId2;
                    alignmentRow["HitChain"] = chainId2;
                }
                else if (repChainId2 != chainId2)
                {
                    alignmentRow["HitChain"] = chainId2;
                }

                alignmentRow["QueryEntityID"] = entityId1;
                alignmentRow["HitEntityID"] = entityId2;
            }
            return alignmentTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainId"></param>
        /// <param name="repPdbId"></param>
        /// <param name="repChainId"></param>
        private void GetRepChain(string pdbId, string chainId, 
            out string repPdbId, out string repChainId, out int entityId)
        {
            repPdbId = "-";
            repChainId = "-";
            entityId = -1;   // entity ID for the input PdbID, not for the repPdbId
            string queryString = string.Format("Select crc, EntityID From PdbCrcMap Where PdbID = '{0}' AND AuthorChain = '{1}';", pdbId, chainId);
            DataTable chainCrcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (chainCrcTable.Rows.Count > 0)
            {
                string crc = chainCrcTable.Rows[0]["crc"].ToString();
                entityId = Convert.ToInt32(chainCrcTable.Rows[0]["EntityID"].ToString ());
                queryString = string.Format("Select PdbID, AuthorChain  From PdbCrcMap Where Crc = '{0}' AND IsRep = '1';", crc);
                DataTable repChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                if (repChainTable.Rows.Count > 0)
                {
                    repPdbId = repChainTable.Rows[0]["PdbID"].ToString();
                    repChainId = repChainTable.Rows[0]["AuthorChain"].ToString().TrimEnd();
                }
            }
            if (repPdbId == "-")
            {
                repPdbId = pdbId;
                repChainId = chainId;
                entityId = GetEntityIdForAuthorChain(pdbId, chainId);
            }           
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainId"></param>
        /// <param name="repPdbId"></param>
        /// <param name="repChainId"></param>
        private void GetRepChain(string pdbId, int entityId, 
            out string repPdbId, out string repChainId, out string chainId)
        {
            repPdbId = "-";
            repChainId = "-";
            chainId = "-";   // the author chain ID for the input PdbID, not for the repPdbId

            string queryString = string.Format("Select crc, AuthorChain From PdbCrcMap Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable chainCrcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (chainCrcTable.Rows.Count > 0)
            {
                string crc = chainCrcTable.Rows[0]["crc"].ToString();
                chainId = chainCrcTable.Rows[0]["AuthorChain"].ToString();
                queryString = string.Format("Select PdbID, AuthorChain From PdbCrcMap Where Crc = '{0}' AND IsRep = '1';", crc);
                DataTable repChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                if (repChainTable.Rows.Count > 0)
                {
                    repPdbId = repChainTable.Rows[0]["PdbID"].ToString();
                    repChainId = repChainTable.Rows[0]["AuthorChain"].ToString().TrimEnd();
                }
            }
            if (repPdbId == "-")
            {
                repPdbId = pdbId;
                chainId = GetAuthorChainForEntity(pdbId, entityId);
                repChainId = chainId;
            }           
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string GetAuthorChainForEntity(string pdbId, int entityId)
        {
            string authChain = "-";
            string queryString = string.Format("Select AuthorChain From AsymUnit Where " + 
                " PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable authChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (authChainTable.Rows.Count > 0)
            {
                authChain = authChainTable.Rows[0]["AuthorChain"].ToString().TrimEnd();
            }
            return authChain;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <returns></returns>
        private int GetEntityIdForAuthorChain(string pdbId, string authChain)
        {
            string queryString = string.Format("Select EntityID From AsymUnit " + 
                " Where PdbId = '{0}' And AuthorChain = '{1}' AND PolymerType = 'polypeptide';", 
                pdbId, authChain);
            DataTable entityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            int entityId = -1;
            if (entityTable.Rows.Count > 0)
            {
                entityId = Convert.ToInt32(entityTable.Rows[0]["EntityID"].ToString ()); 
            }
            return entityId;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="chainId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="chainId2"></param>
        private DataTable FindAlignmentTable (string pdbId1, string chainId1, string pdbId2, string chainId2)
        {
            DataTable alignmentTable = null;
            if (pdbId1 == pdbId2 && chainId1 == chainId2)
            {
                string queryString = "Select First 1 * From FatcatAlignments;";
                alignmentTable = ProtCidSettings.alignmentQuery.Query(queryString);
                alignmentTable.Clear();
                DataRow alignRow = alignmentTable.NewRow();
                alignRow["QueryEntry"] = pdbId1;
                alignRow["QueryChain"] = chainId1;
                alignRow["HitEntry"] = pdbId2;
                alignRow["HitChain"] = chainId2;
                alignRow["Identity"] = 100.0;
                alignRow["QueryStart"] = 0;
                alignRow["QueryEnd"] = 0;
                alignRow["HitStart"] = 0;
                alignRow["HitEnd"] = 0;
                alignRow["QuerySequence"] = "-";
                alignRow["hitSequence"] = "-";
                alignmentTable.Rows.Add(alignRow);
                AddEntityIDToHitTable(ref alignmentTable);
            }
            else
            {
                alignmentTable = hhAlignments.RetrieveHHAlignments(pdbId1, chainId1, pdbId2, chainId2);
                DataTable fatcatAlignTable = GetAlignment(pdbId1, chainId1, pdbId2, chainId2, "fatcat");
                CompareAlignments(alignmentTable, fatcatAlignTable);
            }

            return alignmentTable;
        }

       /// <summary>
       /// the alignments tables must have the same format and same entry order
       /// </summary>
       /// <param name="alignmentTable"></param>
       /// <param name="typeAlignmentTable"></param>
        public void CompareAlignments(DataTable alignmentTable, DataTable typeAlignmentTable)
        {
            foreach (DataRow typeAlignRow in typeAlignmentTable.Rows)
            {
                DataRow[] existAlignmentRows = alignmentTable.Select
                    (string.Format ("QueryEntry = '{0}' AND QueryChain = '{1}' " + 
                    " AND HitEntry = '{2}' AND HitChain = '{3}'", 
                    typeAlignRow["QueryEntry"], typeAlignRow["QueryChain"], 
                    typeAlignRow["HitEntry"], typeAlignRow["HitChain"]));
                if (existAlignmentRows.Length > 0)
                {
                    foreach (DataRow existAlignmentRow in existAlignmentRows)
                    {
                        if (IsNewAlignmentBetter(typeAlignRow, existAlignmentRow))
                        {
                            alignmentTable.Rows.Remove(existAlignmentRow);
                        /*    DataRow newRow = alignmentTable.NewRow();
                            newRow.ItemArray = typeAlignRow.ItemArray;
                            alignmentTable.Rows.Add(newRow);*/
                            DataRow alignRow = alignmentTable.NewRow();
                            alignRow["QueryEntry"] = typeAlignRow["QueryEntry"];
                            alignRow["QueryChain"] = typeAlignRow["QueryChain"];                            
                            alignRow["HitEntry"] = typeAlignRow["HitEntry"];
                            alignRow["HitChain"] = typeAlignRow["HitChain"];                            
                            alignRow["Identity"] = typeAlignRow["Identity"];
                            alignRow["QueryStart"] = typeAlignRow["QueryStart"];
                            alignRow["QueryEnd"] = typeAlignRow["QueryEnd"];
                            alignRow["HitStart"] = typeAlignRow["HitStart"];
                            alignRow["HitEnd"] = typeAlignRow["HitEnd"];
                            alignRow["QuerySequence"] = typeAlignRow["QuerySequence"];
                            alignRow["hitSequence"] = typeAlignRow["hitSequence"];
                            if (alignmentTable.Columns.Contains("QueryEntity"))
                            {
                                if (typeAlignmentTable.Columns.Contains("QueryEntity"))
                                {
                                    alignRow["HitEntity"] = typeAlignRow["HitEntity"];
                                    alignRow["QueryEntity"] = typeAlignRow["QueryEntity"];
                                }
                                else
                                {
                                    alignRow["HitEntity"] = -1;
                                    alignRow["QueryEntity"] = -1;
                                }
                            }
                            alignmentTable.Rows.Add(alignRow);
                        }
                    }
                }
                else
                {
                    DataRow alignRow = alignmentTable.NewRow();
                    alignRow["QueryEntry"] = typeAlignRow["QueryEntry"];
                    alignRow["QueryChain"] = typeAlignRow["QueryChain"];
                    alignRow["HitEntry"] = typeAlignRow["HitEntry"];
                    alignRow["HitChain"] = typeAlignRow["HitChain"];
                    alignRow["Identity"] = typeAlignRow["Identity"];
                    alignRow["QueryStart"] = typeAlignRow["QueryStart"];
                    alignRow["QueryEnd"] = typeAlignRow["QueryEnd"];
                    alignRow["HitStart"] = typeAlignRow["HitStart"];
                    alignRow["HitEnd"] = typeAlignRow["HitEnd"];
                    alignRow["QuerySequence"] = typeAlignRow["QuerySequence"];
                    alignRow["hitSequence"] = typeAlignRow["hitSequence"];

                    if (alignmentTable.Columns.Contains("QueryEntity"))
                    {
                        if (typeAlignmentTable.Columns.Contains("QueryEntity"))
                        {
                            alignRow["HitEntity"] = typeAlignRow["HitEntity"];
                            alignRow["QueryEntity"] = typeAlignRow["QueryEntity"];
                        }
                        else
                        {
                            alignRow["HitEntity"] = -1;
                            alignRow["QueryEntity"] = -1;
                        }
                    }

                    alignmentTable.Rows.Add(alignRow);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="chainId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="chainId2"></param>
        /// <param name="alignType"></param>
        /// <returns></returns>
        public DataTable GetAlignment(string pdbId1, string chainId1, string pdbId2, string chainId2,string alignType)
        {
            string queryString = string.Format("Select QueryEntry, QueryChain, HitEntry, HitChain, " +
                        " QueryLength, QueryStart, QueryEnd, QuerySequence, " +
                        " HitLength, HitStart, HitEnd, HitSequence, Identity" +
                        " From {0}Alignments " + 
                        " Where QueryEntry = '{1}' AND QueryChain = '{2}' " + 
                        " AND HitEntry = '{3}' AND HitCHain = '{4}';", alignType, pdbId1, chainId1, pdbId2, chainId2);
            DataTable alignmentTable = ProtCidSettings.alignmentQuery.Query(queryString);

            if (alignmentTable.Rows.Count == 0)
            {
                queryString = string.Format("Select QueryEntry, QueryChain, HitEntry, HitChain, " +
                        " QueryLength, QueryStart, QueryEnd, QuerySequence, " +
                        " HitLength, HitStart, HitEnd, HitSequence, Identity" +
                        " From {0}Alignments " +
                        " Where QueryEntry = '{1}' AND QueryChain = '{2}' " +
                        " AND HitEntry = '{3}' AND HitCHain = '{4}';", alignType, pdbId2, chainId2, pdbId1, chainId1);
                alignmentTable = ProtCidSettings.alignmentQuery.Query(queryString);
                ReverseAlignmentTable(alignmentTable);
            }
            AddEntityIDToHitTable(ref alignmentTable);
            return alignmentTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignmentTable"></param>
        public void ReverseAlignmentTable(DataTable alignmentTable)
        {
            foreach (DataRow alignmentRow in alignmentTable.Rows)
            {
                ReverseAlignmentRow(alignmentRow);
            }
            alignmentTable.AcceptChanges();
        }
        #endregion

        #region entry structure alignment
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public DataTable GetFatcatAlignTable(string[] entries)
        {
            string[] subEntries = null;
            DataTable hitTable = null;
            for (int i = 0; i < entries.Length; i += 200)
            {
                subEntries = ParseHelper.GetSubArray(entries, i, 200);
                string queryString = string.Format("SELECT QueryEntry, QueryChain, HitEntry, HitChain, " +
                           " QueryLength, QueryStart, QueryEnd, QuerySequence, " +
                           " HitLength, HitStart, HitEnd, HitSequence, Identity" +
                           " FROM FatcatAlignments WHERE " +
                           " QueryEntry IN ({0}) AND HitEntry IN ({0});",
                           ParseHelper.FormatSqlListString(subEntries));
                DataTable subHitTable = ProtCidSettings.alignmentQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(subHitTable, ref hitTable);
            }           
            return hitTable;
        }

        /// <summary>
        /// query and hit in the same order as entryPairsToBeAligned
        /// </summary>
        /// <param name="repAlignTable"></param>
        /// <param name="entryPairsToBeAligned"></param>
        /// <param name="entityRepCrcTable"></param>
        /// <returns></returns>
        public DataTable ChangeRepAlignToEntryAlign  (DataTable repAlignTable, string[] entryPairsToBeAligned, DataTable entityRepCrcTable)
        {
            DataTable alignTable = repAlignTable.Clone();
            if (! alignTable.Columns.Contains("QueryEntity"))
            {
                DataColumn queryEntityCol = new DataColumn("QueryEntity", typeof(Int32));
                queryEntityCol.DefaultValue = -1;
                alignTable.Columns.Add(queryEntityCol);
                DataColumn hitEntityCol = new DataColumn("HitEntity", typeof(Int32));
                hitEntityCol.DefaultValue = -1;
                alignTable.Columns.Add(hitEntityCol);
            }
            string repPdb1 = "";
            string repChain1 = "";
            string repPdb2 = "";
            string repChain2 = "";
            string entryPair = "";
            string reversedEntryPair = "";
            foreach (DataRow repAlignRow in repAlignTable.Rows)
            {
                repPdb1 = repAlignRow["QueryEntry"].ToString();
                repChain1 = repAlignRow["QueryChain"].ToString();
                repPdb2 = repAlignRow["HitEntry"].ToString();
                repChain2 = repAlignRow["HitChain"].ToString();

                DataRow[] homoChainRows1 = entityRepCrcTable.Select(string.Format("RepPdbID = '{0}' AND RepAuthorChain = '{1}'", repPdb1, repChain1));
                DataRow[] homoChainRows2 = entityRepCrcTable.Select(string.Format("RepPdbID = '{0}' AND RepAuthorChain = '{1}'", repPdb2, repChain2));

                foreach (DataRow homoChainRow1 in homoChainRows1 )
                {
                    foreach (DataRow homoChainRow2 in homoChainRows2)
                    {
                        if (homoChainRow1["PdbID"] == homoChainRow2["PdbID"])
                        {
                            continue;
                        }
                        entryPair = homoChainRow1["PdbID"].ToString() + homoChainRow2["PdbID"].ToString();
                        reversedEntryPair = homoChainRow2["PdbID"].ToString() + homoChainRow1["PdbID"].ToString();
                        if (Array.IndexOf (entryPairsToBeAligned, entryPair) > -1)
                        {
                            DataRow alignRow = alignTable.NewRow();
                            alignRow["QueryEntry"] = homoChainRow1["PdbID"];
                            alignRow["QueryChain"] = homoChainRow1["AuthorChain"];
                            alignRow["QueryEntity"] = homoChainRow1["EntityID"];
                            alignRow["HitEntry"] = homoChainRow2["PdbID"];
                            alignRow["HitChain"] = homoChainRow2["AuthorChain"];
                            alignRow["HitEntity"] = homoChainRow2["EntityID"];
                            alignRow["QueryLength"] = repAlignRow["QueryLength"];
                            alignRow["QueryStart"] = repAlignRow["QueryStart"];
                            alignRow["QueryEnd"] = repAlignRow["QueryEnd"];
                            alignRow["QuerySequence"] = repAlignRow["QuerySequence"];
                            alignRow["HitLength"] = repAlignRow["HitLength"];
                            alignRow["HitStart"] = repAlignRow["HitStart"];
                            alignRow["HitEnd"] = repAlignRow["HitEnd"];
                            alignRow["HitSequence"] = repAlignRow["HitSequence"];
                            alignRow["Identity"] = repAlignRow["Identity"];
                            alignTable.Rows.Add(alignRow);
                        }
                        else if (Array.IndexOf (entryPairsToBeAligned, reversedEntryPair) > -1)
                        {
                            DataRow alignRow = alignTable.NewRow();
                            alignRow["QueryEntry"] = homoChainRow2["PdbID"];
                            alignRow["QueryChain"] = homoChainRow2["AuthorChain"];
                            alignRow["QueryEntity"] = homoChainRow2["EntityID"];
                            alignRow["HitEntry"] = homoChainRow1["PdbID"];
                            alignRow["HitChain"] = homoChainRow1["AuthorChain"];
                            alignRow["HitEntity"] = homoChainRow1["EntityID"];
                            alignRow["QueryLength"] = repAlignRow["QueryLength"];
                            alignRow["QueryStart"] = repAlignRow["QueryStart"];
                            alignRow["QueryEnd"] = repAlignRow["QueryEnd"];
                            alignRow["QuerySequence"] = repAlignRow["QuerySequence"];
                            alignRow["HitLength"] = repAlignRow["HitLength"];
                            alignRow["HitStart"] = repAlignRow["HitStart"];
                            alignRow["HitEnd"] = repAlignRow["HitEnd"];
                            alignRow["HitSequence"] = repAlignRow["HitSequence"];
                            alignRow["Identity"] = repAlignRow["Identity"];
                            alignTable.Rows.Add(alignRow);
                        }
                    }
                }
            }
            return alignTable;
        }
        #endregion
    }
}