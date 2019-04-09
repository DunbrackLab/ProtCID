using System;
using System.Collections.Generic;
using System.Data;
using DbLib;
using AuxFuncLib;
using InterfaceClusterLib.DataTables;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.Settings;
using InterfaceClusterLib.Alignments;
using InterfaceClusterLib.AuxFuncs;

namespace InterfaceClusterLib.EntryInterfaces
{
	/// <summary>
	/// Classify a list of entries
	/// </summary>
	public class HomoEntryClassifier
	{
		protected EntryAlignment entryAlignment = new EntryAlignment ();
		protected DbQuery dbQuery = new DbQuery ();
		protected DbInsert dbInsert = new DbInsert ();
        protected DbUpdate dbUpdate = new DbUpdate();
		public const string chainNames = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
		protected int groupSeqNum = 1;
		private Dictionary<string, string[]> sameSgHash = null;
        

		public HomoEntryClassifier()
		{
			GetDuplicateSgHash ();
            AppSettings.LoadCrystMethods();
		}

		public HomoEntryClassifier(int groupId)
		{
			groupSeqNum = groupId;
			GetDuplicateSgHash ();
		} 

		public void ClassifyHomoEntries (string[] entryList)
		{
			InitializeTables ();
			DeleteObsHomoGroupDataInDb (groupSeqNum);
			AddEntryDataToTable (entryList, groupSeqNum);
			RetrieveRepEntries ();
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables);
			HomoGroupTables.ClearTables ();		
		}

		/// <summary>
		/// used for user-defined groups
		/// </summary>
		/// <param name="entryList"></param>
		/// <param name="groupName"></param>
		public void ClassifyHomoEntries (string[] entryList, string groupName)
		{
			InitializeTables ();
			DeleteObsHomoGroupDataInDb (groupSeqNum);
			DataRow familyRow = HomoGroupTables.homoGroupTables[HomoGroupTables.FamilyGroups].NewRow ();
			familyRow["GroupSeqId"] = groupSeqNum;
			familyRow["FamilyString"] = groupName;
			HomoGroupTables.homoGroupTables[HomoGroupTables.FamilyGroups].Rows.Add (familyRow);

			AddEntryDataToTable (entryList, groupSeqNum);
			RetrieveRepEntries ();
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables);
			HomoGroupTables.ClearTables ();		
		}
		#region Add entry info data
		/// <summary>
		/// add entry info data of a group into table 
		/// </summary>
		/// <param name="resultTable"></param>
		/// <param name="groupSeqNum"></param>
		public void AddEntryDataToTable (string[] pdbList, int groupSeqNum)
		{
		/*	string entryInfoQueryString = string.Format ("Select PdbID, SpaceGroup, Method, Resolution, " + 
				" Length_A, Length_B, Length_C, Angle_Alpha, Angle_Beta, Angle_Gamma " + 
				" From PdbEntry Where PdbID IN ({0});", ParseHelper.FormatSqlListString (pdbList));
			DataTable entryInfoTable = dbQuery.Query (entryInfoQueryString);*/
            DataTable entryInfoTable = GetEntryInfoTable(pdbList);
            DataTable asuTable = GetEntryEntityNumberTable(pdbList);
			// check if the entry in scop or not
		//	DataTable domainEntryTable = GetDomainEntryTable (pdbList);

			string spaceGroup = "";
			string method = "";
            string abbrevMethod = "";
			double resolution = 0.0;
            Dictionary<string, List<UnitCellInfo>> sgAsuEntryCellInfoHash = new Dictionary<string, List<UnitCellInfo>>();
			foreach (DataRow dRow in entryInfoTable.Rows)
			{
				spaceGroup = dRow["SpaceGroup"].ToString ().Trim ();
				method = dRow["Method"].ToString ().Trim ();
				resolution = Convert.ToDouble (dRow["Resolution"].ToString ());

		/*		if (method.ToUpper ().IndexOf ("NMR") < 0 && method.ToUpper ().IndexOf ("X-RAY") < 0 &&
					method.ToUpper ().IndexOf ("XRAY") < 0 )
				{
					continue;
				}*/
                if (spaceGroup == "-")
                {
                    spaceGroup = "P 1";
                }
                abbrevMethod = (string)AppSettings.crystMethodHash[method];
			//	if (method.ToUpper ().IndexOf ("NMR") > -1)
                if (Array.IndexOf (AppSettings.abbrevNonXrayCrystMethods, abbrevMethod ) > -1)
				{
                    if (abbrevMethod == "NMR")
                    {
                        spaceGroup = "NMR";
                    }
                    else
                    {
                        spaceGroup = spaceGroup + "("  + abbrevMethod + ")";
                    }
				}
                
				DataRow[] entityCountRows = asuTable.Select (string.Format ("PdbID = '{0}'", dRow["PdbID"]), "EntityNum DESC");
				// format ASU string 
				string asuString = "";
				int chainCount = 0;
                int chainIndex = 0;
				foreach (DataRow entityCountRow in entityCountRows)
				{
					int entityNum = Convert.ToInt32 (entityCountRow["EntityNum"].ToString ());
                    chainIndex = chainCount % chainNames.Length;
					if (entityNum == 1)
					{
						asuString += chainNames[chainIndex].ToString ();
					}
					else
					{
						asuString += chainNames[chainIndex].ToString () + entityNum.ToString ();
					}
					chainCount ++;
				}
				// if NMR monomer, no interface, skip it
			/*	if (spaceGroup == "NMR" && asuString == "A")
				{
					continue;
				}*/
				spaceGroup = UseStandardSgString (spaceGroup);

				DataRow homoRow = HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].NewRow ();
				homoRow["GroupSeqID"] = groupSeqNum;
				homoRow["PdbID"] = dRow["PdbID"];
				homoRow["SpaceGroup"] = spaceGroup;
				homoRow["ASU"] = asuString;
				homoRow["Method"] = method;
				homoRow["Resolution"] = dRow["Resolution"];
                homoRow["InPfam"] = 1; // currently for pfam groups only

				// check if the entry has domain classified, and set the column to be 1 or 0
			//	SetRowDomainClass (ref homoRow, domainEntryTable, dRow["PdbID"].ToString ());

				HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].Rows.Add (homoRow);

				UnitCellInfo unitCellInfo = new UnitCellInfo (dRow["PdbID"].ToString (),
					Convert.ToDouble (dRow["Length_A"].ToString ()),
					Convert.ToDouble (dRow["Length_B"].ToString ()), 
					Convert.ToDouble (dRow["Length_C"].ToString ()),
					Convert.ToDouble (dRow["Angle_Alpha"].ToString ()), 
					Convert.ToDouble (dRow["Angle_Beta"].ToString ()),
					Convert.ToDouble (dRow["Angle_Gamma"].ToString ()));
				if (sgAsuEntryCellInfoHash.ContainsKey (spaceGroup + "_" + asuString))
				{
                    sgAsuEntryCellInfoHash[spaceGroup + "_" + asuString].Add(unitCellInfo);
				} 
				else
				{
					List<UnitCellInfo> unitCellList = new List<UnitCellInfo>  ();
                    unitCellList.Add(unitCellInfo);
                    sgAsuEntryCellInfoHash.Add(spaceGroup + "_" + asuString, unitCellList);
				}
			}
			ClassifyUnitCellGroups (groupSeqNum, sgAsuEntryCellInfoHash);
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbList"></param>
        /// <returns></returns>
        private DataTable GetEntryInfoTable(string[] pdbList)
        {
            string entryInfoQueryString = "";
            DataTable entryInfoTable = null;
            string[] subEntryList = null;
            int count = 100;
            for (int i = 0; i < pdbList.Length; i += count )
            {
                subEntryList = GetSubList(pdbList, i, count);
                entryInfoQueryString = string.Format("Select PdbID, SpaceGroup, Method, Resolution, " +
                                " Length_A, Length_B, Length_C, Angle_Alpha, Angle_Beta, Angle_Gamma " +
                                " From PdbEntry Where PdbID IN ({0});", ParseHelper.FormatSqlListString (subEntryList));
                DataTable thisEntryInfoTable = ProtCidSettings.pdbfamQuery.Query( entryInfoQueryString);
                ParseHelper.AddNewTableToExistTable(thisEntryInfoTable, ref entryInfoTable);
            }
            return entryInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbList"></param>
        /// <returns></returns>
        private DataTable GetEntryEntityNumberTable(string[] pdbList)
        {
            string asuQueryString = "";
            DataTable entityCountTable = null;
            string[] subEntryList = null;
            int count = 100;
            for (int i = 0; i < pdbList.Length; i += count )
            {
                subEntryList = GetSubList(pdbList, i, count);
                asuQueryString = string.Format("Select PdbID, EntityID, Count(EntityID) AS EntityNum From AsymUnit " +
                      " Where PdbID In ({0}) AND polymerType = 'polypeptide' GROUP BY PdbID, EntityID;", 
                      ParseHelper.FormatSqlListString (subEntryList));
                DataTable entryAsuTable = ProtCidSettings.pdbfamQuery.Query( asuQueryString);
                ParseHelper.AddNewTableToExistTable(entryAsuTable, ref entityCountTable);               
            }               
            return entityCountTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="itemList"></param>
        /// <param name="i"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private string[] GetSubList (string[] itemList, int i, int count)
        {
            string[] subItemList = null;
            if (i + count <= itemList.Length)
            {
                subItemList = new string[count];
                Array.Copy(itemList, i, subItemList, 0, count);
            }
            else
            {
                subItemList = new string[itemList.Length - i];
                Array.Copy(itemList, i, subItemList, 0, subItemList.Length);
            }
            return subItemList;
        }
	
		/// <summary>
		/// check if the entry has domain classification (scop, pfam)
		/// if yes, set column (InScop or InPfam) to be 1
		/// </summary>
		/// <param name="homoRow"></param>
		/// <param name="domainEntryTable"></param>
		/// <param name="pdbId"></param>
		private void SetRowDomainClass (ref DataRow homoRow, DataTable domainEntryTable, string pdbId)
		{
			if (domainEntryTable == null)
			{
				return;
			}
            string domainColName = "In" + ProtCidSettings.dataType;
            if (ProtCidSettings.dataType.IndexOf("pfam") > -1)
            {
                domainColName = "InPfam";
                homoRow["domainColName"] = 1;
            }
            else
            {
                DataRow[] domainEntryRows = domainEntryTable.Select(string.Format("PdbID = '{0}'", pdbId));
                
                if (domainEntryRows.Length > 0)
                {
                    homoRow[domainColName] = 1;
                }
                else
                {
                    homoRow[domainColName] = 0;
                }
            }
		}
		#endregion

		#region Classigy Unit Cells into similar groups
		/// <summary>
		/// classify unit cells into similar groups
		/// 
		/// </summary>
		/// <param name="groupId"></param>
		/// <param name="unitCellInfoHash"></param>
		internal void ClassifyUnitCellGroups (int groupId, Dictionary<string, List<UnitCellInfo>> unitCellInfoHash)
		{
			foreach (string sgAsu in unitCellInfoHash.Keys)
			{
                if (unitCellInfoHash[sgAsu].Count == 1)
				{
					continue;
				}
				string[] sgAsuFields = sgAsu.Split ('_');
                string abbrevMethod = ParseHelper.GetNonXrayAbbrevMethod (sgAsuFields[0]);
				// NMR structures,
				// treat each NMR entries as one crystal form
			//	if (sgAsuFields[0] == "NMR")
                if (Array.IndexOf(AppSettings.abbrevNonXrayCrystMethods, abbrevMethod) > -1)
				{
                    ClassifyNMREntries(groupId, sgAsuFields[1], unitCellInfoHash[sgAsu]);
					continue;
				}

                Dictionary<int, List<UnitCellInfo>> unitCellGroupHash = ClassifyUnitCells(unitCellInfoHash[sgAsu]);
				if (unitCellGroupHash.Count > 1)
				{
					foreach (int cellGroupId in unitCellGroupHash.Keys)
					{
                        foreach (UnitCellInfo cellInfo in unitCellGroupHash[cellGroupId])
						{
							DataRow[] entryRows = HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].Select
								(string.Format ("GroupSeqId = '{0}' AND PdbID = '{1}'", groupId, cellInfo.pdbId));
							entryRows[0]["ASU"] = entryRows[0]["ASU"].ToString () + "(" + cellGroupId.ToString () + ")";
						}
					}
					HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].AcceptChanges ();
				}
			}
		}
		/// <summary>
		/// group NMR entries, treat each entry as its own crystal form
		/// </summary>
		/// <param name="groupId"></param>
		/// <param name="asuString"></param>
		/// <param name="unitCellInfoList"></param>
		private void ClassifyNMREntries (int groupId, string asuString, List<UnitCellInfo> unitCellInfoList)
		{
			int nmrCount = 1;
			foreach (UnitCellInfo cellInfo in unitCellInfoList)
			{
				DataRow[] entryRows = HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].Select
					(string.Format ("GroupSeqId = '{0}' AND PdbID = '{1}'", groupId, cellInfo.pdbId));
				entryRows[0]["ASU"] = entryRows[0]["ASU"].ToString () + "(" + nmrCount.ToString () + ")";
				nmrCount ++;
			}
		}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="unitCellInfoList"></param>
        /// <returns></returns>
        private Dictionary<int, List<UnitCellInfo>> ClassifyUnitCells(List<UnitCellInfo> unitCellInfoList)
		{
			int[] groupData = new int [unitCellInfoList.Count];
			// initialize to -1
			for (int i = 0; i < groupData.Length; i ++)
			{
				groupData[i] = -1;
			}
			
			int clusterId = 1;
			for (int i = 0; i < unitCellInfoList.Count; i ++)
			{
				if (groupData[i] > -1)
				{
					continue;
				}
				groupData[i] = clusterId;
				for (int j = i + 1; j < unitCellInfoList.Count; j ++)
				{
					if (groupData[j] > -1)
					{
						continue;
					}
					if (((UnitCellInfo)unitCellInfoList[i]).AreTwoUnitCellsSame ((UnitCellInfo)unitCellInfoList[j]))
					{
						groupData[j] = clusterId;
					}
				}
				clusterId ++;
			}
			Dictionary<int, List<UnitCellInfo>> unitCellGroupHash = new Dictionary<int,List<UnitCellInfo>> ();
			for (int i = 0; i < groupData.Length; i ++)
			{
				if (unitCellGroupHash.ContainsKey (groupData[i]))
				{
                    unitCellGroupHash[groupData[i]].Add(unitCellInfoList[i]);
				}
				else
				{
					List<UnitCellInfo> unitCellList = new List<UnitCellInfo>  ();
					unitCellList.Add (unitCellInfoList[i]);
					unitCellGroupHash.Add (groupData[i], unitCellList);
				}
			}
			return unitCellGroupHash;
		}
		#endregion 

		#region duplicate space groups
		/// <summary>
		/// 
		/// </summary>
		/// <param name="spaceGroup"></param>
		/// <returns></returns>
		private string UseStandardSgString (string spaceGroup)
		{
			if (! sameSgHash.ContainsKey (spaceGroup))
			{
				return spaceGroup;
			}
			string[] sameSgStrings = (string[])sameSgHash[spaceGroup];
            List<string> sameSgStringList = new List<string>(sameSgStrings);
			sameSgStringList.Add (spaceGroup);
			sameSgStringList.Sort ();
			foreach (string sgString in sameSgStringList)
			{
				if (sgString.IndexOf ("(") > -1)
				{
					continue;
				}
				string[] fields = sgString.Split (' ');
				if (fields.Length == 4)
				{
					return sgString;
				}
			}
			return spaceGroup;
		}

		private void GetDuplicateSgHash ()
		{
			sameSgHash = new Dictionary<string,string[]> ();
			foreach (SpaceGroupSymmetryMatrices symMatrices in AppSettings.symOps.SymOpsInSpaceGroupList)
			{
				if (symMatrices.sameSgListString != "")
				{
					string[] sgStrings = symMatrices.sameSgListString.Split (';');
					sameSgHash.Add (symMatrices.spaceGroup, sgStrings);
				}
			}
		}
		#endregion

		#region Representative Entries
		/// <summary>
		/// Retrieve representative entries from each group
		/// </summary>
		internal bool RetrieveRepEntries ()
		{
			if (HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].Rows.Count == 0)
			{
				return false;
			}		
		
			if (ProtCidSettings.dataType == "scop" || ProtCidSettings.dataType.IndexOf ("pfam") > -1)
			{
				FindRepEntriesInDomainFamily ();
			}
			else
			{
				FindRepEntriesInFamily ();
			}
	
			return true;
		}

		/// <summary>
		/// representative entries in a domain family (scop/pfam)
		/// best resolution in scop/pfam, 
		/// if no entries with domain definition, best resolution entry
		/// </summary>
		/// <param name="sgEntryHash"></param>
		/// <param name="sgMinResRowHash"></param>
		private void FindRepEntriesInDomainFamily ()
		{
            Dictionary<string, List<string>> sgEntryHash = GetRepEntriesInDomainGroup();
            // the representative entries and their homologies in each group
            AddGroupChainPairsToTables(sgEntryHash);
		}

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetRepEntriesInDomainGroup()
        {
            // all entries with same crystal form: same space group, same ASU size, unit cell parameters in 1%
            Dictionary<string, List<string>> sgEntryHash = new Dictionary<string,List<string>> ();
            // choose entry with minimum resolution and in scop/pfam as representative in a crystal form group
            Dictionary<string, DataRow> sgMinResRowHash = new Dictionary<string,DataRow> ();

            string spaceGroup = "";
            double resolution = 0.0;
            string method = "";
            string asu = "";
            string sgAsuString = "";
            string dataType = ProtCidSettings.dataType;
            if (ProtCidSettings.dataType == "pfamclan")
            {
                dataType = "pfam";
            }

            DataRow[] groupRows = HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].Select
                (string.Format("In{0} = '1'", dataType));
            foreach (DataRow dRow in groupRows)
            {
                spaceGroup = dRow["SpaceGroup"].ToString().Trim();
                asu = dRow["ASU"].ToString();
                method = dRow["Method"].ToString().Trim();
                resolution = Convert.ToDouble(dRow["Resolution"].ToString());
                // same space group, same ASU size, unit cell parameters in 1%
                sgAsuString = spaceGroup + "_" + asu;
                if (sgEntryHash.ContainsKey(sgAsuString))
                {                   
                    if (!sgEntryHash[sgAsuString].Contains(dRow["PdbID"].ToString()))
                    {
                        sgEntryHash[sgAsuString].Add(dRow["PdbID"].ToString());
                    }

                    DataRow minResRow = (DataRow)sgMinResRowHash[sgAsuString];
                    double minResolution = Convert.ToDouble(minResRow["Resolution"].ToString());
                    if ((minResolution > resolution || minResolution == 0.0) &&
                        method.ToUpper().IndexOf("X-RAY") > -1)
                    {
                        sgMinResRowHash[sgAsuString] = dRow;
                        // remove duplicate rows with same space group	
                        HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].Rows.Remove(minResRow);
                        continue;
                    }
                    HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].Rows.Remove(dRow);
                    continue;
                }
                sgMinResRowHash.Add(sgAsuString, dRow);
                List<string> sgEntryList = new List<string> ();              
                sgEntryHash.Add(sgAsuString, sgEntryList);
                // choose the best one as representative entry
                // high resolution for X-ray, or non-monomer NMR entries
            }

            groupRows = HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].Select
                (string.Format("In{0} = '0'", dataType));
            List<string> nonScopSgList = new List<string>();
            foreach (DataRow dRow in groupRows)
            {
                spaceGroup = dRow["SpaceGroup"].ToString().Trim();
                asu = dRow["ASU"].ToString();
                method = dRow["Method"].ToString().Trim();
                resolution = Convert.ToDouble(dRow["Resolution"].ToString());
                // same space group, different asymmetric units, different crystal forms
                sgAsuString = spaceGroup + "_" + asu;
                if (sgEntryHash.ContainsKey(sgAsuString))
                {
                    if (!sgEntryHash[sgAsuString].Contains(dRow["PdbID"].ToString()))
                    {
                        sgEntryHash[sgAsuString].Add(dRow["PdbID"].ToString());
                    }

                    /* for this crystal form, no entry in scop,
                     * then choose the entry with minimum resolution
                     * if there is entry in scop, keep that one
                     * */
                    if (nonScopSgList.Contains(sgAsuString))
                    {
                        DataRow minResRow = (DataRow)sgMinResRowHash[sgAsuString];
                        double minResolution = Convert.ToDouble(minResRow["Resolution"].ToString());
                        if ((minResolution > resolution || minResolution == 0.0) &&
                            method.ToUpper().IndexOf("X-RAY") > -1)
                        {
                            sgMinResRowHash[sgAsuString] = dRow;
                            // remove duplicate rows with same space group, keep the entry with best resolution	
                            HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].Rows.Remove(minResRow);
                            continue;
                        }
                    }
                    // remove worse resolution rows
                    HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].Rows.Remove(dRow);
                    continue;
                }
                sgMinResRowHash.Add(sgAsuString, dRow);
                List<string> sgEntryList = new List<string> ();
                sgEntryHash.Add(sgAsuString, sgEntryList);
                nonScopSgList.Add(sgAsuString);
            }
            return sgEntryHash;
        }

		/// <summary>
		/// representative entries in a family
		/// </summary>
		/// <param name="sgEntryHash"></param>
		/// <param name="sgMinResRowHash"></param>
		private void FindRepEntriesInFamily ()
		{
            // record entries with same space groups in a group
            Dictionary<string, List<string>> sgEntryHash = new Dictionary<string,List<string>> ();
            // choose entry with minimum resolution and in scop/pfam as representative
            Dictionary<string, DataRow> sgMinResRowHash = new Dictionary<string,DataRow> ();
		
			string spaceGroup = "";
			double resolution = 0.0;
			string method = "";
			string asu = "";

			DataRow[] groupRows = HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].Select ();
			foreach (DataRow dRow in groupRows)
			{
				spaceGroup = dRow["SpaceGroup"].ToString ().Trim ();
				asu = dRow["ASU"].ToString ();
				method = dRow["Method"].ToString ().Trim ();
				resolution = Convert.ToDouble (dRow["Resolution"].ToString ());
				// same space group, different asymmetric units, different crystal forms
				string sgAsuString = spaceGroup + "_" + asu;
				if (sgEntryHash.ContainsKey (sgAsuString))
				{
                    if (!sgEntryHash[sgAsuString].Contains(dRow["PdbID"].ToString()))
					{
                        sgEntryHash[sgAsuString].Add(dRow["PdbID"].ToString());
					}

					DataRow minResRow = (DataRow)sgMinResRowHash[sgAsuString];
					double minResolution = Convert.ToDouble (minResRow["Resolution"].ToString ());
					if ((minResolution > resolution || minResolution == 0.0 ) && 
						method.ToUpper ().IndexOf ("X-RAY") > -1)
					{
						sgMinResRowHash[sgAsuString] = dRow;
						// remove duplicate rows with same space group	
                        // keep the entry with best resolution
						HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].Rows.Remove  (minResRow);
						continue;
					}
					HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].Rows.Remove  (dRow);					
					continue;
				}
				sgMinResRowHash.Add (sgAsuString, dRow);
				List<string> sgEntryList = new List<string>  ();				
				sgEntryList.Add (dRow["PdbID"].ToString ());
				sgEntryHash.Add (sgAsuString, sgEntryList);
			}
            AddGroupChainPairsToTables(sgEntryHash);
		}

		#endregion

        #region add data to data tables
        /* Modified on Feb. 02, 2009
         * The alignments will be added at the next step, 
         * because the best alignment is choosed from PSIBLAST, CE and FATCAT alignments.
         * And missed alignments will be added by outside of the project (FATCAT/CE no Windows executable)
        */
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sgEntryHash"></param>
        public void AddGroupChainPairsToTables(Dictionary<string, List<string>> sgEntryHash)
        {
            List<string> repPdbList = new List<string> ();
#if DEBUG
            string repEntryString = "";
            string repHomoEntryString = "";
            int subGroupNum = 1;
#endif
            foreach (DataRow dRow in HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].Rows)
            {
                List<string> pdbList = sgEntryHash[dRow["SpaceGroup"].ToString() + "_" + dRow["ASU"].ToString()];
                pdbList.Remove(dRow["PdbID"].ToString());
#if DEBUG
                repEntryString += dRow["PdbID"].ToString();
                repEntryString += ",";
#endif
                if (!repPdbList.Contains(dRow["PdbID"].ToString()))
                {
                    repPdbList.Add(dRow["PdbID"].ToString());
                }
                if (pdbList.Count > 0)
                {
                   AddDataToHomoRepEntryAlignTable(dRow["PdbID"].ToString(), pdbList, groupSeqNum);
           //          entryAlignment.RetrieveRepEntryAlignment(dRow["PdbID"].ToString(), pdbList, groupSeqNum);
#if DEBUG
                    repHomoEntryString = dRow["PdbID"].ToString();
                    foreach (string pdbId in pdbList)
                    {
                        repHomoEntryString += ",";
                        repHomoEntryString += pdbId;
                    }
                    subGroupNum++;
#endif
                }
            }
            dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign]);
            HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign].Clear();

            // retrieve pairwise entries information for each group
           AddDataToRepEntryAlignTable(repPdbList, groupSeqNum);
         //    entryAlignment.GetEntryPairAlignment(repPdbList, groupSeqNum);
        }

        /// <summary>
        /// the chain pairs between representative entry and its homologues
        /// </summary>
        /// <param name="repPdbId"></param>
        /// <param name="homoPdbList"></param>
        /// <param name="groupSeqNum"></param>
        private void AddDataToHomoRepEntryAlignTable(string repPdbId, List<string> homoPdbList, int groupNum)
        {
            foreach (string homoPdbId in homoPdbList)
            {
                DataRow entryRow = HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign].NewRow();
                entryRow["GroupSeqID"] = groupNum;
                entryRow["PdbID1"] = repPdbId;
                entryRow["EntityID1"] = -1;
                entryRow["PdbID2"] = homoPdbId;
                entryRow["EntityID2"] = -1;
                entryRow["Identity"] = 100;
                entryRow["QueryStart"] = -1;
                entryRow["QueryEnd"] = -1;
                entryRow["HitStart"] = -1;
                entryRow["HitEnd"] = -1;
                entryRow["QuerySequence"] = "-";
                entryRow["HitSequence"] = "-";
                HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign].Rows.Add(entryRow);
            }
        }

        /// <summary>
        /// the chain pairs between representative entries in a group
        /// </summary>
        /// <param name="repPdbList"></param>
        /// <param name="groupSeqNum"></param>
        private void AddDataToRepEntryAlignTable(List<string> repPdbList, int groupNum)
        {
            repPdbList.Sort(); // added on Feb. 9, 2010
            for (int i = 0; i < repPdbList.Count; i++)
            {
                for (int j = i + 1; j < repPdbList.Count; j++)
                {
                    DataRow entryRow = HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign].NewRow();
                    entryRow["GroupSeqID"] = groupNum;
                    entryRow["PdbID1"] = repPdbList[i];
                    entryRow["EntityID1"] = -1;
                    entryRow["PdbID2"] = repPdbList[j];
                    entryRow["EntityID2"] = -1;
                    entryRow["Identity"] = 100;
                    entryRow["QueryStart"] = -1;
                    entryRow["QueryEnd"] = -1;
                    entryRow["HitStart"] = -1;
                    entryRow["HitEnd"] = -1;
                    entryRow["QuerySequence"] = "-";
                    entryRow["HitSequence"] = "-";
                    HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign].Rows.Add(entryRow);
                }
                // for big group, got outOfMemory message
                dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign]);
                HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign].Clear();
            }
            dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign]);
            HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign].Clear();
        }
        #endregion

        #region update homo group info and align info
        // added on April 1, 2017
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateHomoGroupRepAlignTables ()
        {
            Dictionary<string, List<string>> sgEntryHash = GetRepEntriesInDomainGroup();
            // the representative entries and their homologies in each group
            UpdateGroupRepHomoAlignTables (sgEntryHash);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sgEntryHash"></param>
        public void UpdateGroupRepHomoAlignTables(Dictionary<string, List<string>> sgEntryHash)
        {
            Dictionary<string, List<string>> repHomoListHash = new Dictionary<string,List<string>> ();
            string pdbId = "";
            foreach (DataRow dRow in HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].Rows)
            {
                pdbId = dRow["PdbID"].ToString();
                List<string> homoPdbList = sgEntryHash[dRow["SpaceGroup"].ToString() + "_" + dRow["ASU"].ToString()];
                homoPdbList.Remove(pdbId);

                if (! repHomoListHash.ContainsKey (pdbId))
                {
                    repHomoListHash.Add(pdbId, homoPdbList);
                }
            }
            List<string> repPdbList = new List<string> (repHomoListHash.Keys);
            repPdbList.Sort();
            string[] repEntries = repPdbList.ToArray ();

            string[][] sameObsRepEntries = GetSameObsoleteRepEntries(repEntries, groupSeqNum); // 0: same 1: obsolete
            DeleteGroupAlignData(sameObsRepEntries[1]);

            UpdateRepHomoEntryAlignTable(groupSeqNum, repEntries, repHomoListHash, sameObsRepEntries[0]);
            // retrieve pairwise entries information for each group
            UpdateGroupRepEntryAlignTable (groupSeqNum, repEntries, sameObsRepEntries[0]);
        }


        /// <summary>
        /// the chain pairs between representative entry and its homologues
        /// </summary>
        /// <param name="repPdbId"></param>
        /// <param name="homoPdbList"></param>
        /// <param name="groupSeqNum"></param>
        private void UpdateRepHomoEntryAlignTable(int groupId, string[] repEntries, Dictionary<string, List<string>> repHomoListHash, string[] sameRepEntries)
        {
            DeleteOrigHomoEntriesNowRepEntries(groupId, repEntries);

            Dictionary<string, string[]> existRepHomoListHash = GetSameRepHomoList(groupId, sameRepEntries);
            string[] existHomoEntries = null;
            foreach (string repEntry in repEntries)
            {
                existHomoEntries = new string[0];
                if (repHomoListHash[repEntry].Count == 0)
                {
                    continue;
                }
                if (existRepHomoListHash.ContainsKey(repEntry))
                {
                    existHomoEntries = existRepHomoListHash[repEntry];
                }
                foreach (string homoPdbId in repHomoListHash[repEntry])
                {
                    if (Array.IndexOf (existHomoEntries, homoPdbId) > -1)
                    {
                        continue;
                    }
                    DataRow entryRow = HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign].NewRow();
                    entryRow["GroupSeqID"] = groupId;
                    entryRow["PdbID1"] = repEntry;
                    entryRow["EntityID1"] = -1;
                    entryRow["PdbID2"] = homoPdbId;
                    entryRow["EntityID2"] = -1;
                    entryRow["Identity"] = 100;
                    entryRow["QueryStart"] = -1;
                    entryRow["QueryEnd"] = -1;
                    entryRow["HitStart"] = -1;
                    entryRow["HitEnd"] = -1;
                    entryRow["QuerySequence"] = "-";
                    entryRow["HitSequence"] = "-";
                    HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign].Rows.Add(entryRow);
                }
            }
            
            dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign]);
            HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign].Clear();
        }


        /// <summary>
        /// /
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="repEntries"></param>
        private void DeleteOrigHomoEntriesNowRepEntries (int groupId, string[] repEntries)
        {
            string queryString = string.Format("Select Distinct PdbID2 From {0} Where GroupSeqID = {1};", HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign].TableName, groupId);
            DataTable homoEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId = "";
            string deleteString = "";
            List<string> nowRepEntryList = new List<string>();
            foreach (DataRow homoEntryRow in homoEntryTable.Rows)
            {
                pdbId = homoEntryRow["PdbID2"].ToString();
                if (Array.IndexOf (repEntries, pdbId) > -1)
                {
                    nowRepEntryList.Add(pdbId);
                    deleteString = string.Format("Delete From {0} WHere PdbID2 = '{1}';", HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign].TableName, pdbId);
                    dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
                }
            }           
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="sameRepEntries"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> GetSameRepHomoList (int groupId, string[] sameRepEntries)
        {
            string queryString = string.Format("Select Distinct PdbID1, PdbID2 From {0} Where GroupSeqId = {1};",
                HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign].TableName, groupId);
            DataTable repHomoTable = ProtCidSettings.protcidQuery.Query( queryString);
            Dictionary<string, string[]> repHomoListHash = new Dictionary<string,string[]> ();
            foreach (string repEntry in sameRepEntries)
            {
                DataRow[] homoEntryRows = repHomoTable.Select(string.Format ("PdbID1 = '{0}'", repEntry));                
                string[] homoEntries = new string[homoEntryRows.Length];
                for (int i = 0; i < homoEntryRows.Length; i ++)
                {
                    homoEntries[i] = homoEntryRows[i]["PdbID2"].ToString();
                }
                repHomoListHash.Add(repEntry, homoEntries);
            }
            return repHomoListHash;
        }

        /// <summary>
        /// the chain pairs between representative entries in a group
        /// </summary>
        /// <param name="repPdbList"></param>
        /// <param name="groupSeqNum"></param>
        private void UpdateGroupRepEntryAlignTable(int groupNum, string[] groupRepEntries, string[] sameRepEntries)
        {                
            for (int i = 0; i < groupRepEntries.Length; i++)
            {
                for (int j = i + 1; j < groupRepEntries.Length; j++)
                {
                    if (Array.IndexOf(sameRepEntries, groupRepEntries[i]) > -1 && Array.IndexOf(sameRepEntries, groupRepEntries[j]) > -1)
                    {
                        continue;
                    }
                    else
                    {
                        DataRow entryRow = HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign].NewRow();
                        entryRow["GroupSeqID"] = groupNum;
                        entryRow["PdbID1"] = groupRepEntries[i];
                        entryRow["EntityID1"] = -1;
                        entryRow["PdbID2"] = groupRepEntries[j];
                        entryRow["EntityID2"] = -1;
                        entryRow["Identity"] = 100;
                        entryRow["QueryStart"] = -1;
                        entryRow["QueryEnd"] = -1;
                        entryRow["HitStart"] = -1;
                        entryRow["HitEnd"] = -1;
                        entryRow["QuerySequence"] = "-";
                        entryRow["HitSequence"] = "-";
                        HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign].Rows.Add(entryRow);
                    }
                }
            }
            dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign]);
            HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign].Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="repPdbList"></param>
        /// <param name="groupNum"></param>
        /// <returns></returns>
        private string[][] GetSameObsoleteRepEntries(string[] repEntries, int groupNum)
        {
            string queryString = string.Format("Select Distinct PdbID From {0} Where GroupSeqID = {1};",
               HomoGroupTables.homoGroupTables[HomoGroupTables.HomoSeqInfo].TableName, groupNum);
            DataTable repEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> obsRepList = new List<string>();
            List<string> sameRepList = new List<string>();
            string pdbId = "";
            foreach (DataRow repRow in repEntryTable.Rows)
            {
                pdbId = repRow["PdbID"].ToString();
                if ( Array.IndexOf (repEntries, pdbId) < 0)
                {
                    obsRepList.Add(pdbId);
                }
                else
                {
                    sameRepList.Add(pdbId);
                }
            }
            string[][] sameObsRepEntries = new string[2][];
            sameObsRepEntries[0] = sameRepList.ToArray ();
            sameObsRepEntries[1] = obsRepList.ToArray (); 
            return sameObsRepEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="tableName"></param>
        private void DeleteEntryAlignData (string pdbId, string tableName)
        {
            string deleteString = string.Format("Delete From {0} Where PdbID1 = '{1}' OR PdbID2 = '{1}';", tableName, pdbId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obsRepEntries"></param>
        private void DeleteGroupAlignData (string[] obsRepEntries)
        {
            string tableName = HomoGroupTables.homoGroupTables[HomoGroupTables.HomoRepEntryAlign].TableName;
            foreach (string obsEntry in obsRepEntries)
            {
                DeleteEntryAlignData(obsEntry, tableName);
            }
            tableName = HomoGroupTables.homoGroupTables[HomoGroupTables.HomoGroupEntryAlign].TableName;
            foreach (string obsEntry in obsRepEntries)
            {
                DeleteEntryAlignData(obsEntry, tableName);
            }
        }
        #endregion

        #region initialize and delete
        /// <summary>
		/// initialize tables
		/// </summary>
		/// <param name="isUpdate"></param>
		protected void InitializeTables ()
		{
			// create tables in memory
			HomoGroupTables.InitializeTables ();
			GetDuplicateSgHash ();
		}

		/// <summary>
		/// delete obsolete groups from db
		/// </summary>
		/// <param name="groupList"></param>
		public void DeleteObsHomoGroupDataInDb (List<int> groupList)
		{
			foreach (int groupId in groupList)
			{
				DeleteObsHomoGroupDataInDb (groupId);
			}
		}
		/// <summary>
		/// delete obsolete groups from db
		/// </summary>
		/// <param name="groupList"></param>
		public void DeleteObsHomoGroupDataInDb (int groupId)
		{		
			string deleteString = string.Format ("DELETE FROM {0} WHERE GroupSeqID = {1};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo], groupId);
            ProtCidSettings.protcidQuery.Query( deleteString);
			deleteString = string.Format ("DELETE FROM {0} WHERE GroupSeqID = {1};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoRepEntryAlign], groupId);
            ProtCidSettings.protcidQuery.Query( deleteString);
			deleteString = string.Format ("DELETE FROM {0} WHERE GroupSeqID = {1};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoGroupEntryAlign], groupId);
            ProtCidSettings.protcidQuery.Query( deleteString);
		}

		/// <summary>
		/// get the maximum groupId in the db
		/// </summary>
		/// <returns></returns>
		public int GetMaxGroupId ()
		{
			string queryString = string.Format ("Select Max(GroupSeqID) As MaxGroupID From {0};", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.FamilyGroups]);
            DataTable maxGroupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
			return Convert.ToInt32 (maxGroupIdTable.Rows[0]["MaxGroupID"].ToString ());
		}
		#endregion
	}
}
