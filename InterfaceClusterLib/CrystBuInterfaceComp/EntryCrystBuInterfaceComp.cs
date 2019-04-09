using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using CrystalInterfaceLib.Contacts;
using ProtCidSettingsLib;
using CrystalInterfaceLib.StructureComp;
using CrystalInterfaceLib.Settings;
using AuxFuncLib;
using DbLib;
using BuCompLib;

/* rewritten on July, 2009
       * remove PQS info
       * change the codes for the retrieval of BU interfaces 
       * by calculating the interfaces from XML files and the Bu interface definition from BuComp database
       * */

namespace InterfaceClusterLib.InterfaceComp
{
	/// <summary>
	/// Summary description for EntryCrystBuInterfaceComp.
	/// </summary>
	public class EntryCrystBuInterfaceComp
    {
        #region member variables
        enum BuType
        {
            PDB, PISA
        }
        protected ContactInCrystal contactInCrystal = new ContactInCrystal();
        private BuInterfaceRetriever buInterfaceRetriever = new BuInterfaceRetriever();
        private InterfacesComp interfaceComp = new InterfacesComp();
        public DataTable[] crystBuInterfaceCompTables = null;
        protected DbInsert dbInsert = new DbInsert();
        protected DbQuery dbQuery = new DbQuery();
        #endregion

        public EntryCrystBuInterfaceComp()
		{
		}

		#region public functions to compare cryst interfaces to PDB/PISA BU interfaces
		/// <summary>
		/// Compare cryst and bu interfaces for all entries in db
		/// </summary>
		public void CompareEntryCrystBuInterfaces ()
		{
            Initialize ();

			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
		
	//		string crystEntryQuery = "SELECT Distinct PdbID FROM CrystEntryInterfaces;";	
	//		DataTable crystEntryTable = dbQuery.Query (crystEntryQuery);
	 		string[] nonBuCompEntries = GetNonBuCompEntries ();
     //      string[] nonBuCompEntries = {"1v9t"};
          
			ProtCidSettings.progressInfo.currentOperationLabel = "Comparing Cryst BU Interface";
			ProtCidSettings.progressInfo.totalOperationNum = nonBuCompEntries.Length;
			ProtCidSettings.progressInfo.totalStepNum = nonBuCompEntries.Length;
			
#if DEBUG
			StreamWriter entryWriter = new StreamWriter ("notInDbEntryList.txt", true);
#endif
            foreach (string pdbId in nonBuCompEntries)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                try
                {
                    CompareEntryCrystBuInterfaces(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine("Comparing " + pdbId + " cryst bu interfaces errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Comparing " + pdbId + " cryst bu interfaces errors: " + ex.Message);
                }

                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, crystBuInterfaceCompTables);
                ClearTables();
            }
			try
			{
				Directory.Delete (ProtCidSettings.tempDir, true);
			}
			catch {}
#if DEBUG
			entryWriter.Close ();
#endif
		}

		/// <summary>
		/// update crystal interfaces and bu interfaces
		/// </summary>
		/// <param name="updateEntries"></param>
		public void UpdateEntryCrystBuInterfaces (string[] updateEntries)
		{
            Initialize();

			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update comparing cryst and bu interfaces.");
			
			ProtCidSettings.progressInfo.currentOperationLabel = "Comparing Cryst BU Interface";
			ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
			ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;

            foreach (string pdbId in updateEntries)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                try
                {
                    DeleteObsData(pdbId);
                    CompareEntryCrystBuInterfaces(pdbId);

                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, crystBuInterfaceCompTables);

                    ClearTables();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine(pdbId + ": Compare cryst bu interfaces error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": Compare cryst bu interfaces error: " + ex.Message);
                }
            }
			try
			{
				Directory.Delete (ProtCidSettings.tempDir, true);
			}
			catch {}
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
		}
		/// <summary>
		/// compare cryst interfaces with interfaces in pdb and/or pqs BUs
		/// </summary>
		/// <param name="pdbId"></param>
		public void CompareEntryCrystBuInterfaces (string pdbId)
		{
            Initialize();

            if (AreThereBuInterfaces(pdbId))
            {
                InterfaceChains[] crystInterfaces = GetCrystInterfacesFromDb(pdbId);
                if (crystInterfaces == null)
                {
                    return;
                }
                CompareCrystBuInterfaces(pdbId, crystInterfaces);
            }
		}

        /// <summary>
        /// check if any bu interface exist
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool AreThereBuInterfaces(string pdbId)
        {
            string queryString = string.Format("Select * From PdbBuInterfaces Where PdbID = '{0}';", pdbId);
            DataTable pdbBuInterfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            queryString = string.Format("Select * From PisaBuInterfaces Where PdbID = '{0}';", pdbId);
            DataTable pisaBuInterfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            if (pdbBuInterfaceTable.Rows.Count > 0 || pisaBuInterfaceTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
		#endregion

        #region compare cryst and PDB/PISA BU interfaces
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="crystInterfaces"></param>
        private void CompareCrystBuInterfaces(string pdbId, InterfaceChains[] crystInterfaces)
        {
            // key: pdb bu id, value: a list of same BUs for the input entry
            Dictionary<string, List<string>> pdbSameBuHash = null;
            // the unique PDB BUs for the entry
            string[] pdbDifBUs = GetEntryDifBUs(pdbId, "pdb", ref pdbSameBuHash);

            Dictionary<string, InterfaceChains[]> pdbBuInterfacesHash = buInterfaceRetriever.GetEntryBuInterfaces(pdbId, pdbDifBUs, "pdb");

            if (pdbBuInterfacesHash.Count == 0)
            {
                CompareEntryCrystBuInterfaces(pdbId, crystInterfaces, "pisa");
            }
            else
            {
                // key: pdb bu id, value: a list of pisa BUs which are same as the pdb bu
                Dictionary<string, List<string>> samePdbBuHash = null;
                // get those PISA BUs which are not same as any PDB BUs
                string[] difPisaBUs = GetBUsDifFromPdb(pdbId, "pisa", ref samePdbBuHash);

                foreach (string buId in pdbBuInterfacesHash.Keys)
                {
                    InterfaceChains[] buInterfaces = (InterfaceChains[])pdbBuInterfacesHash[buId];
                    InterfacePairInfo[] interfaceCompPairs = interfaceComp.CompareInterfacesBetweenCrystals(crystInterfaces, buInterfaces);
                    List<string> sameBuList = new List<string> ();
                    if (pdbSameBuHash.ContainsKey(buId))
                    {
                        sameBuList = pdbSameBuHash[buId];
                    }
                    string[] sameBUs = new string[sameBuList.Count];
                    sameBuList.CopyTo(sameBUs);
                    AssignInterfaceCompInfoIntoTable(pdbId, buId, interfaceCompPairs, sameBUs, (int)BuType.PDB);
                    if (samePdbBuHash.ContainsKey(buId))
                    {
                        List<string> difTypeSameBuList = samePdbBuHash[buId];
                        string[] difTypeSameBUs = new string[difTypeSameBuList.Count];
                        difTypeSameBuList.CopyTo(difTypeSameBUs);
                        AssignInterfaceCompInfoIntoTableForSameBUs(pdbId, buId, interfaceCompPairs, 
                            difTypeSameBUs, "PdbPisaBuInterfaceComp", (int)BuType.PISA);
                    }
                }

                if (difPisaBUs.Length > 0)
                {
                    Dictionary<string, InterfaceChains[]> pisaBuInterfacesHash = buInterfaceRetriever.GetEntryBuInterfaces(pdbId, difPisaBUs, "pisa");
                    foreach (string buId in pisaBuInterfacesHash.Keys)
                    {
                        InterfaceChains[] buInterfaces = (InterfaceChains[])pisaBuInterfacesHash[buId];
                        InterfacePairInfo[] interfaceCompPairs = interfaceComp.CompareInterfacesBetweenCrystals(crystInterfaces, buInterfaces);
                        AssignInterfaceCompInfoIntoTable(pdbId, buId, interfaceCompPairs, (int)BuType.PISA);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="crystInterfaces"></param>
        private void CompareEntryCrystBuInterfaces(string pdbId, InterfaceChains[] crystInterfaces, string buType)
        {
            // key: bu id, value: a list of same BUs for the input entry
            Dictionary<string, List<string>> sameBuHash = null;
            // the unique PDB BUs for the entry
            string[] difBUs = GetEntryDifBUs(pdbId, buType, ref sameBuHash);

            Dictionary<string, InterfaceChains[]> buInterfacesHash = buInterfaceRetriever.GetEntryBuInterfaces(pdbId, difBUs, buType);

            int tableNum = -1;
            if (buType == "pdb")
            {
                tableNum = (int)BuType.PDB;
            }
            else if (buType == "pisa")
            {
                tableNum = (int)BuType.PISA;
            }
               
            foreach (string buId in buInterfacesHash.Keys)
            {
                InterfaceChains[] buInterfaces = (InterfaceChains[])buInterfacesHash[buId];
                InterfacePairInfo[] interfaceCompPairs = interfaceComp.CompareInterfacesBetweenCrystals(crystInterfaces, buInterfaces);
                List<string> sameBuList = new List<string> ();
                if (sameBuHash.ContainsKey(buId))
                {
                    sameBuList = sameBuHash[buId];
                }
                string[] sameBUs = new string[sameBuList.Count];
                sameBuList.CopyTo(sameBUs);
                AssignInterfaceCompInfoIntoTable(pdbId, buId, interfaceCompPairs, sameBUs, tableNum);
            }
        }

        /// <summary>
        /// the BU IDs which are not same as any PDB BUs
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private string[] GetBUsDifFromPdb(string pdbId, string buType, ref Dictionary<string, List<string>> sameBuHash)
        {
            string buCompType = "Pdb" + buType;
            string queryString = string.Format("Select * From {0}BuComp Where PdbID = '{1}';", buCompType, pdbId);
            DataTable buCompTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            string buId1 = "";
            string buId2 = "";
            int numOfInterfaces1 = -1;
            int numOfInterfaces2 = -1;
            List<string> buList2 = new List<string> ();
            foreach (DataRow buCompRow in buCompTable.Rows)
            {
                buId2 = buCompRow["BuID2"].ToString().TrimEnd();
                if (!buList2.Contains(buId2))
                {
                    buList2.Add(buId2);
                }
            }
            sameBuHash = new Dictionary<string,List<string>> ();
            foreach (DataRow buCompRow in buCompTable.Rows)
            {
                buId1 = buCompRow["BuID1"].ToString().TrimEnd();
                buId2 = buCompRow["BuID2"].ToString().TrimEnd();

                numOfInterfaces1 = Convert.ToInt32(buCompRow["InterfaceNum1"].ToString ());
                numOfInterfaces2 = Convert.ToInt32(buCompRow["InterfaceNum2"].ToString ());

                if (buCompRow["IsSame"].ToString() == "1" &&
                    numOfInterfaces1 == numOfInterfaces2)
                {
                    buList2.Remove(buId2);
                    if (sameBuHash.ContainsKey (buId1))
                    {
                        sameBuHash[buId1].Add(buId2);
                    }
                    else
                    {
                        List<string> sameBuList2 = new List<string> ();
                        sameBuList2.Add(buId2);
                        sameBuHash.Add(buId1, sameBuList2);
                    }
                }
            }

            string[] leftBuIDs2 = new string[buList2.Count];
            buList2.CopyTo(leftBuIDs2);
            return leftBuIDs2;
        }

        /// <summary>
        /// the unique BUs for this entry
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buType"></param>
        /// <param name="sameBuHash"></param>
        /// <returns></returns>
        private string[] GetEntryDifBUs(string pdbId, string buType, ref Dictionary<string, List<string>> sameBuHash)
        {
            string queryString = string.Format("Select * From {0}EntryBuComp Where PdbID = '{1}';", 
                buType, pdbId);
            DataTable entryBuCompTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            string[] entryBUs = GetEntryBuIDs(pdbId, buType);

            string buId1 = "";
            string buId2 = "";
            sameBuHash = new Dictionary<string,List<string>> ();

            List<string> difBuList = new List<string> (entryBUs);
            foreach (DataRow buCompRow in entryBuCompTable.Rows)
            {
                buId1 = buCompRow["BuID1"].ToString().TrimEnd();
                buId2 = buCompRow["BuID2"].ToString().TrimEnd();

                if (buCompRow["SameBUs"].ToString() == "1" && (
                    Convert.ToInt32(buCompRow["NumOfInterfaces1"].ToString()) == Convert.ToInt32(buCompRow["NumOfInterfaces2"].ToString()) ||
                    buCompRow["EntityFormat1"].ToString().TrimEnd() == buCompRow["EntityFormat2"].ToString().TrimEnd()))
                {
                    if (!difBuList.Contains(buId1))
                    {
                        continue;
                    }
                    difBuList.Remove(buId2);
                    
                    if (sameBuHash.ContainsKey(buId1))
                    {
                        sameBuHash[buId1].Add(buId2);
                    }
                    else
                    {
                        List<string> sameBuList = new List<string> ();
                        sameBuList.Add(buId2);
                        sameBuHash.Add(buId1, sameBuList);
                    }
                }
            }
            string[] difBUs = new string[difBuList.Count];
            difBuList.CopyTo(difBUs);
            return difBUs;
        }
        /// <summary>
        /// all the BUs of the entry
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private string[] GetEntryBuIDs(string pdbId, string buType)
        {
            string queryString = string.Format("Select Distinct BuID From {0}BuInterfaces " + 
                " Where PdbID = '{1}';", buType, pdbId);
            DataTable buIdTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            string[] entryBUs = new string[buIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow buIdRow in buIdTable.Rows)
            {
                entryBUs[count] = buIdRow["BuID"].ToString ().TrimEnd ();
                count++;
            }
            return entryBUs;
        }
        #endregion

        #region assign data into table
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <param name="interfaceCompPairs"></param>
        /// <param name="entrySameBuHash"></param>
        /// <param name="difEntrySameBuHash"></param>
        private void AssignInterfaceCompInfoIntoTable (string pdbId, string buId, InterfacePairInfo[] interfaceCompPairs, 
            string[] sameBUs, int tableNum)
        {
            foreach (InterfacePairInfo compPair in interfaceCompPairs)
            {
                if (compPair.qScore < AppSettings.parameters.contactParams.minQScore)
                {
                    continue;
                }
                DataRow dataRow = crystBuInterfaceCompTables[tableNum].NewRow();
                dataRow["PdbID"] = pdbId;
                dataRow["InterfaceId"] = compPair.interfaceInfo1.interfaceId;
                dataRow["BUID"] = buId;
                dataRow["BuInterfaceID"] = compPair.interfaceInfo2.interfaceId;
                dataRow["Qscore"] = compPair.qScore;
                crystBuInterfaceCompTables[tableNum].Rows.Add(dataRow);
            }
            if (sameBUs.Length > 0)
            {
                string dbTableName = "PdbEntryBuInterfaceComp";
                AssignInterfaceCompInfoIntoTableForSameBUs(pdbId, buId, interfaceCompPairs, sameBUs, dbTableName, tableNum);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <param name="interfaceCompPairs"></param>
        /// <param name="tableNum"></param>
        private void AssignInterfaceCompInfoIntoTable(string pdbId, string buId, InterfacePairInfo[] interfaceCompPairs, int tableNum)
        {
            foreach (InterfacePairInfo compPair in interfaceCompPairs)
            {
                if (compPair.qScore < AppSettings.parameters.contactParams.minQScore)
                {
                    continue;
                }
                DataRow dataRow = crystBuInterfaceCompTables[tableNum].NewRow();
                dataRow["PdbID"] = pdbId;
                dataRow["InterfaceId"] = compPair.interfaceInfo1.interfaceId;
                dataRow["BUID"] = buId;
                dataRow["BuInterfaceID"] = compPair.interfaceInfo2.interfaceId;
                dataRow["Qscore"] = compPair.qScore;
                crystBuInterfaceCompTables[tableNum].Rows.Add(dataRow);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <param name="interfaceCompPairs"></param>
        /// <param name="difTypeSameBUs"></param>
        /// <param name="buCompType"></param>
        private void AssignInterfaceCompInfoIntoTableForSameBUs(string pdbId, string buId, InterfacePairInfo[] interfaceCompPairs,
            string[] sameBUs, string dbTableName, int tableNum)
        {
            Dictionary<string, List<string>> existInterfacePairHash = new Dictionary<string,List<string>> ();
            string interfacePairString = "";

            string queryString = string.Format("Select * From {0} " + 
                " Where PdbID = '{1}' AND BuID1 = '{2}' AND BuID2 In ({3}) " + 
                " AND Qscore >= {4};",
                dbTableName, pdbId, buId, ParseHelper.FormatSqlListString(sameBUs), 
                AppSettings.parameters.simInteractParam.interfaceSimCutoff);
            DataTable sameBUInterfaceCompTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
           
            foreach (InterfacePairInfo compPair in interfaceCompPairs)
            {
                if (compPair.qScore < AppSettings.parameters.contactParams.minQScore)
                {
                    continue;
                }
                foreach (string sameBu in sameBUs)
                {
                    int[] sameInterfaces = GetSameInterfacesInBUs(compPair.interfaceInfo2.interfaceId, sameBu, sameBUInterfaceCompTable);
                    List<string> interfacePairList = new List<string> ();
                    if (existInterfacePairHash.ContainsKey(sameBu))
                    {
                        interfacePairList = (List<string>)existInterfacePairHash[sameBu];
                    }
                    foreach (int sameInterface in sameInterfaces)
                    {
                        interfacePairString = compPair.interfaceInfo1.interfaceId.ToString() + "_" + sameInterface.ToString();
                        if (interfacePairList.Contains(interfacePairString))
                        {
                            continue;
                        }
                        interfacePairList.Add(interfacePairString);
                        DataRow dataRow = crystBuInterfaceCompTables[tableNum].NewRow();
                        dataRow["PdbID"] = pdbId;
                        dataRow["InterfaceID"] = compPair.interfaceInfo1.interfaceId;
                        dataRow["BuID"] = sameBu;
                        dataRow["BuInterfaceID"] = sameInterface;
                        dataRow["Qscore"] = compPair.qScore;
                        crystBuInterfaceCompTables[tableNum].Rows.Add(dataRow);
                    }
                    if (existInterfacePairHash.ContainsKey(sameBu))
                    {
                        existInterfacePairHash[sameBu] = interfacePairList;
                    }
                    else
                    {
                        existInterfacePairHash.Add(sameBu, interfacePairList);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sameBu"></param>
        /// <param name="interfaceCompTable"></param>
        /// <returns></returns>
        private int[] GetSameInterfacesInBUs(int interfaceId, string sameBu, DataTable interfaceCompTable)
        {
            DataRow[] interfaceCompRows = interfaceCompTable.Select
                (string.Format ("InterfaceID1 = '{0}' AND BuID2 = '{1}'", interfaceId, sameBu));
            int[] sameInterfaces = new int[interfaceCompRows.Length];
            int count = 0;
            foreach (DataRow interfaceCompRow in interfaceCompRows)
            {
                sameInterfaces[count] = Convert.ToInt32(interfaceCompRow["InterfaceID2"].ToString ());
                count++;
            }
            return sameInterfaces;
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId1"></param>
        /// <param name="interfaceId1"></param>
        /// <param name="buId2"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private int[] GetSameInterfacesInEntryBUs(string pdbId, string buId1, int interfaceId1, string buId2, string buType)
        {
            string queryString = string.Format("Select * From {0}EntryBuInterfaceComp " + 
                " Where PdbID = '{1}' AND BuID1 = '{2}' AND InterfaceID1 = {3} " + 
                " AND BuID2 = '{4}' AND Qscore >= {5};", 
                buType, pdbId, buId1, interfaceId1, buId2, 
                AppSettings.parameters.simInteractParam.interfaceSimCutoff);
            DataTable interfaceCompTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            int[] sameInterfaceIds = new int[interfaceCompTable.Rows.Count];
            int count = 0;
            foreach (DataRow interfaceCompRow in interfaceCompTable.Rows)
            {
                sameInterfaceIds[count] = Convert.ToInt32(interfaceCompRow["InterfaceID2"].ToString ());
                count++;
            }
            return sameInterfaceIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId1"></param>
        /// <param name="interfaceId1"></param>
        /// <param name="buId2"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private int[] GetSameInterfacesFromNonPdbBUs(string pdbId, string buId1, int interfaceId1, string buId2, string buType)
        {
            string queryString = string.Format("Select * From Pdb{0}BuInterfaceComp " + 
                " Where PdbID = '{1}' AND BuID1 = '{2}' AND InterfaceID1 = {3} " + 
                " AND BuID2 = '{4}' AND Qscore >= {5};", 
                buType, pdbId, buId1, interfaceId1, buId2, 
                AppSettings.parameters.simInteractParam.interfaceSimCutoff);
            DataTable buInterfaceCompTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            int[] sameInterfaces = new int[buInterfaceCompTable.Rows.Count];
            int count = 0;
            foreach (DataRow interfaceCompRow in buInterfaceCompTable.Rows)
            {
                sameInterfaces[count] = Convert.ToInt32(interfaceCompRow["InterfaceID2"].ToString ());
                count++;
            }
            return sameInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        private void ClearTables()
        {
            foreach (DataTable dataTable in crystBuInterfaceCompTables)
            {
                dataTable.Clear();
            }
        }
        #endregion

        #region user-defined pdb list
        /// <summary>
		/// Compare cryst and bu interfaces for the entries
		/// </summary>
		public void CompareEntryCrystBuInterfaces (string[] updatePdbList)
		{
			if (! Directory.Exists (ProtCidSettings.tempDir))
			{
				Directory.CreateDirectory (ProtCidSettings.tempDir);
			}
			if (ProtCidSettings.dirSettings == null)
			{
				ProtCidSettings.LoadDirSettings ();
			}
			if (AppSettings.parameters == null)
			{
				AppSettings.LoadParameters ();
			}			
			InitializeTables ();

			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
		
			ProtCidSettings.progressInfo.currentOperationLabel = "Comparing Cryst BU Interface";
			ProtCidSettings.progressInfo.totalOperationNum = updatePdbList.Length;
			ProtCidSettings.progressInfo.totalStepNum = updatePdbList.Length;

            foreach (string pdbId in updatePdbList)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                if (IsBuCompExist(pdbId))
                {
                    continue;
                }
                CompareEntryCrystBuInterfaces(pdbId);

                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, crystBuInterfaceCompTables);
                ClearTables();
            }
			try
			{
				Directory.Delete (ProtCidSettings.tempDir, true);
			}
			catch {}
		}
	
		/// <summary>
		/// 
		/// </summary>
		/// <param name="pdbId"></param>
		/// <returns></returns>
		private bool IsBuCompExist (string pdbId)
		{
			string queryString = string.Format ("Select * From {0} Where PdbId = '{1}';", 
                 crystBuInterfaceCompTables[(int)BuType.PDB].TableName, pdbId);
            DataTable buCompTable = ProtCidSettings.protcidQuery.Query( queryString);
			if (buCompTable.Rows.Count > 0)
			{
				return true;
			}

            queryString = string.Format("Select * From {0} Where PdbId = '{1}';",
                 crystBuInterfaceCompTables[(int)BuType.PISA].TableName, pdbId);
            buCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (buCompTable.Rows.Count > 0)
            {
                return true;
            }

			return false;
		}
		#endregion

        #region initialize process
        /// <summary>
        /// 
        /// </summary>
        private void Initialize()
        {
            InitializeTables();
            InitializeDbTables();
        }
        
        #region initialize tables
        /// <summary>
        /// 
        /// </summary>
        private void InitializeTables()
        {
          /*  string[] interfaceCompCols = {"PdbID", "InterfaceId", 
										"PdbBuID", "PdbInterfaceID", "PdbQScore", 
								//		"PqsBuId", "PqsInterfaceId", "PqsQScore", 
										"PisaBuId", "PisaInterfaceId", "PisaQScore"};*/
            if (crystBuInterfaceCompTables == null)
            {
                string[] interfaceCompCols = {"PdbID", "InterfaceId", 
										"BuID", "BuInterfaceID", "QScore"};
                crystBuInterfaceCompTables = new DataTable[2];
                crystBuInterfaceCompTables[(int)BuType.PDB] = new DataTable("CrystPdbBuInterfaceComp");
                crystBuInterfaceCompTables[(int)BuType.PISA] = new DataTable("CrystPisaBuInterfaceComp");
                foreach (string interfaceCompCol in interfaceCompCols)
                {
                    crystBuInterfaceCompTables[(int)BuType.PDB].Columns.Add(new DataColumn(interfaceCompCol));
                    crystBuInterfaceCompTables[(int)BuType.PISA].Columns.Add(new DataColumn(interfaceCompCol));
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void InitializeDbTables()
        {
            DbCreator dbCreate = new DbCreator();
            if (dbCreate.IsTableExist(ProtCidSettings.protcidDbConnection, crystBuInterfaceCompTables[(int)BuType.PDB].TableName))
            {
                return;
            }
            string createTableString = string.Format("Create Table {0} ( " +
                "PdbID CHAR(4) NOT NULL, InterfaceId INTEGER NOT NULL, " +
                "BuID VARCHAR(8), BuInterfaceID INTEGER, QScore FLOAT);", 
                crystBuInterfaceCompTables[(int)BuType.PDB].TableName);
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, crystBuInterfaceCompTables[(int)BuType.PDB].TableName);
            
            string indexString = string.Format("Create INDEX {0}_Idx1 " +
                " ON {0} (PdbID, InterfaceID);", crystBuInterfaceCompTables[(int)BuType.PDB].TableName);
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, crystBuInterfaceCompTables[(int)BuType.PDB].TableName);


            createTableString = string.Format("Create Table {0} ( " +
               "PdbID CHAR(4) NOT NULL, InterfaceId INTEGER NOT NULL, " +
               "BuID VARCHAR(8), BuInterfaceID INTEGER, QScore FLOAT);",
               crystBuInterfaceCompTables[(int)BuType.PISA].TableName);
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, crystBuInterfaceCompTables[(int)BuType.PISA].TableName);
           
            indexString = string.Format("Create INDEX {0}_Idx1 " +
                " ON {0} (PdbID, InterfaceID);", crystBuInterfaceCompTables[(int)BuType.PISA].TableName);
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, crystBuInterfaceCompTables[(int)BuType.PISA].TableName);
        }
        #endregion
        #endregion

        #region entries needed to be compared
        private string[] GetBuCompEntries ()
		{
			string queryString = "Select Distinct PdbID FROM CrystEntryInterfaces;";
            DataTable crystEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
			string[] entries = new string [crystEntryTable.Rows.Count];
			int i = 0;
			foreach (DataRow dRow in crystEntryTable.Rows)
			{
				entries[i] = dRow["PdbID"].ToString ();
				i ++;
			}
			return entries;
		}
		/// <summary>
		/// entries have not compared to BU interfaces yet
		/// </summary>
		/// <returns></returns>
		private string[] GetNonBuCompEntries ()
		{
			string crystEntryQuery = "SELECT Distinct PdbID FROM CrystEntryInterfaces;";
            DataTable crystEntryTable = ProtCidSettings.protcidQuery.Query( crystEntryQuery);
			
			string bucompEntryQuery = string.Format ("SELECT Distinct PdbID FROM {0};", 
                crystBuInterfaceCompTables[(int)BuType.PDB].TableName);
            DataTable pdbBuCompEntryTable = ProtCidSettings.protcidQuery.Query( bucompEntryQuery);

            bucompEntryQuery = string.Format("SELECT Distinct PdbID FROM {0};",
                crystBuInterfaceCompTables[(int)BuType.PISA].TableName);
            DataTable pisaBuCompEntryTable = ProtCidSettings.protcidQuery.Query( bucompEntryQuery);

			List<string> nonBuCompEntryList = new List<string> ();
			string pdbId = "";
            foreach (DataRow entryRow in crystEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                DataRow[] buCompRows = pdbBuCompEntryTable.Select(string.Format("PdbID = '{0}'", pdbId));
                // already in the db
                if (buCompRows.Length > 0)
                {
                    continue;
                }

                buCompRows = pisaBuCompEntryTable.Select(string.Format("PdbID = '{0}'", pdbId));
                // already in the db
                if (buCompRows.Length > 0)
                {
                    continue;
                }
                nonBuCompEntryList.Add(pdbId);
            }
            return nonBuCompEntryList.ToArray ();
        }
        #endregion

        #region cryst interfaces from DB
        /// <summary>
		/// interfaces in crystal structures
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="interfaceExist"></param>
		/// <returns></returns>
		private InterfaceChains[] GetCrystInterfacesFromDb (string pdbId)
		{
			string xmlFile = Path.Combine (ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
			if (! File.Exists (xmlFile))
			{
				return null;
			}
			string coordXmlFile = ParseHelper.UnZipFile (xmlFile, ProtCidSettings.tempDir);
			Dictionary<int, string> interfaceDefHash = null;
			Dictionary<int, string> interfaceEntityInfoHash = null;
			bool interfaceExist = true;
			GetCrystInterfaceDef (pdbId, ref interfaceDefHash, ref interfaceEntityInfoHash);
			contactInCrystal.FindInteractChains (coordXmlFile, interfaceDefHash, interfaceEntityInfoHash, out interfaceExist);
			if (! interfaceExist)
			{
				return null;
			}
			File.Delete (coordXmlFile);
			return contactInCrystal.InterfaceChainsList;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="interfaceDefHash"></param>
		/// <param name="interfaceEntityInfoHash"></param>
		private void GetCrystInterfaceDef (string pdbId, ref Dictionary<int, string> interfaceDefHash, ref Dictionary<int, string> interfaceEntityInfoHash)
		{
			interfaceDefHash = new Dictionary<int,string> ();
			interfaceEntityInfoHash = new Dictionary<int,string> ();
			string queryString = string.Format ("Select * From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable crystInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
			if (crystInterfaceTable.Rows.Count > 0)
			{
				// build cryst interfaces based on database info
				foreach (DataRow dRow in crystInterfaceTable.Rows)
				{
					string symOpString1 = dRow["AsymChain1"].ToString ().Trim () + "_" + 
						dRow["SymmetryString1"].ToString ().Trim ();
					string symOpString2 = dRow["AsymChain2"].ToString ().Trim () + "_" + 
						dRow["SymmetryString2"].ToString ().Trim ();
					if (! interfaceDefHash.ContainsKey (Convert.ToInt32 (dRow["InterfaceId"].ToString ())))
					{
						interfaceDefHash.Add (Convert.ToInt32 (dRow["InterfaceId"].ToString ()), 
							symOpString1 + ";" + symOpString2);
						interfaceEntityInfoHash.Add (Convert.ToInt32 (dRow["InterfaceId"].ToString ()), 
							dRow["EntityID1"].ToString () + "_" + dRow["EntityID2"].ToString ());
					}
				}
			}
		}
		#endregion

        #region delete obsolete data
        /// <summary>
        /// delete data from database
        /// </summary>
        /// <param name="updatePdbIds"></param>
        protected void DeleteObsData(string[] updatePdbIds)
        {
            foreach (string pdbId in updatePdbIds)
            {
                DeleteObsData(pdbId);
            }
        }

        /// <summary>
        /// delete data from database
        /// </summary>
        /// <param name="pdbId"></param>
        protected void DeleteObsData(string pdbId)
        {
            string deleteString = "";
            foreach (DataTable dataTable in crystBuInterfaceCompTables)
            {
                deleteString = string.Format("DELETE FROM {0} WHERE PdbID = '{1}';", dataTable.TableName, pdbId);
                ProtCidSettings.protcidQuery.Query( deleteString);
            }
        }
        #endregion
	}
}
