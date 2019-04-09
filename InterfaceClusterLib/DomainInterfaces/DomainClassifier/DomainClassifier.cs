using System;
using System.Data;
using System.IO;
using System.Collections.Generic;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;
using InterfaceClusterLib.DataTables;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.DomainInterfaces;
using CrystalInterfaceLib.Settings;
using InterfaceClusterLib.InterfaceProcess;
using PfamLib;
//using DataCollectorLib.Pfam;

namespace InterfaceClusterLib.DomainInterfaces
{
	/// <summary>
	/// classify scop domain-domain interactions based on scop family
	/// </summary>
	public class DomainClassifier
	{
		#region member variables
		public DbQuery dbQuery = new DbQuery ();
		public DbInsert dbInsert = new DbInsert ();
        public DbUpdate dbUpdate = new DbUpdate();
		public DomainInterfaceWithinProtein chainDomainInterfaces = new DomainInterfaceWithinProtein ();
        private DomainInterfaceRetriever domainInterfaceRetriever = new DomainInterfaceRetriever ();
        private CrystInterfaceRetriever interfaceRetriever = new CrystInterfaceRetriever();
        private PfamLib.PfamArch.PfamArchitecture pfamArch = new PfamLib.PfamArch.PfamArchitecture();
        private StreamWriter dataWriter = null;
  //      private PfamDomainGenerator domainFileGen = new PfamDomainGenerator();\
        private DataTable pfamEntryCfGroupTable = null;
        private DataTable homoRepTable = null;
		#endregion

		/// <summary>
		/// Find the domain-domain interfaces for each entry with crystal interfaces
		/// </summary>
        public void RetrieveDomainInterfaces()
        {
            if (dataWriter == null)
            {
                dataWriter = new StreamWriter("IntraChainDomainInterfaces.txt");
            }

            GroupDbTableNames.SetGroupDbTableNames(ProtCidSettings.dataType);
            CrystInterfaceTables.InitializeTables();
            InitializeTableInDb();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Domain-domain interactions";

            string[] entriesWithoutDomainInterfaces = GetEntriesWithoutDomainInterfaces();
          //  string[] entriesWithoutDomainInterfaces = GetEntriesForDomainInterfaces();
          //  string[] entriesWithoutDomainInterfaces = GetEntriesInSpecificPfams();
          //  string[] entriesWithoutDomainInterfaces = {"12e8"};

            ProtCidSettings.progressInfo.totalOperationNum = entriesWithoutDomainInterfaces.Length;
            ProtCidSettings.progressInfo.totalStepNum = entriesWithoutDomainInterfaces.Length;
            
            foreach (string pdbId in entriesWithoutDomainInterfaces)
            {
                // progress info
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                try
                {
                    GetDomainInterfacesFromCrystInterfaces(pdbId);

                    // insert data into db
                    dbInsert.InsertDataIntoDBtables
                        (ProtCidSettings.protcidDbConnection, DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces]);
                    DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces].Clear();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue
                        (pdbId + " retrieve domain interactions error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " retrieve domain interactions error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }// group

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Add chain domain ids");
            AddChainDomainIdToInterfaces();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Change RelSeqIDs in the order of familycode1 and familycode2");
            UpdateRelSeqIDs();
            AddClanSeqIds();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Set CfGroupIDs in the relations");
            GetRelationCfGroupIDs();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        #region update domain interfaces
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public Dictionary<int, string[]> UpdateDomainInterfaces(string[] updateEntries)
        {
            if (dataWriter == null)
            {
                dataWriter = new StreamWriter("IntraChainDomainInterfaces.txt");
            }

            GroupDbTableNames.SetGroupDbTableNames(ProtCidSettings.dataType);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Domain-domain interactions";

            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;

            string domainInterfaceTableName = ProtCidSettings.dataType + "DomainInterfaces";
           
#if DEBUG
            StreamWriter oldDataWriter = new StreamWriter("OldEntryDomainInterfaces.txt", true);
#endif
            Dictionary<int, List<string>> updateRelationEntryListDict = new Dictionary<int, List<string>>();
            foreach (string pdbId in updateEntries)
            {
                // progress info
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                try
                {
                    int[] oldRelSeqIds = GetEntryRelSeqIds(pdbId);
#if DEBUG
                    GetDbEntryDomainInterfaces(pdbId, oldDataWriter);
#endif                 

                    GetDomainInterfacesFromCrystInterfaces(pdbId);

                    // get the relation seq ids of the entry
                    int[] updateRelSeqIds = GetUpdatedRelationsOfEntry();
                    foreach (int relSeqId in updateRelSeqIds)
                    {
                        if (updateRelationEntryListDict.ContainsKey(relSeqId))
                        {
                            if (!updateRelationEntryListDict[relSeqId].Contains(pdbId))
                            {
                                updateRelationEntryListDict[relSeqId].Add(pdbId);
                            }
                        }
                        else
                        {
                            List<string> updateEntryList = new List<string> ();
                            updateEntryList.Add(pdbId);
                            updateRelationEntryListDict.Add(relSeqId, updateEntryList);
                        }
                    }
                    // some of relation SeqIDs containing different number of entries because of update, 
                    // so need check these relations too
                    // added on Sept. 18, 2017
                    foreach (int relSeqId in oldRelSeqIds )
                    {
                        if (!updateRelationEntryListDict.ContainsKey(relSeqId))
                        {
                            List<string> relEntryList = new List<string> ();
                            updateRelationEntryListDict.Add(relSeqId, relEntryList);
                            DeleteInterfaceCompTable(relSeqId, pdbId);
                        }
                    }

                    // delete obsolete data
                    DeleteObsData(pdbId);
                    // insert data into db
                    dbInsert.InsertDataIntoDBtables
                        (ProtCidSettings.protcidDbConnection, DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces]);
                    DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces].Clear();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " updateing domain interface errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " updateing domain interface errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }// group

            // add relations with deleted obsolete entries on Sept. 18, 2017
            AddRelationsWithDeletedObsoleteEntries(updateRelationEntryListDict);
            
 //           Hashtable updateRelationEntryHash = GetUpdateRelationsOfUpdateEntries(updateEntries);
            string updateRelEntryFile = "UpdateRelationEntries.txt";
            StreamWriter updataDataWriter = new StreamWriter(updateRelEntryFile);
            string line = "";
            Dictionary<int, string[]> updateRelationEntryDict = new Dictionary<int, string[]>();
            foreach (int lsRelSeqId in updateRelationEntryListDict.Keys)
            {
                string[] entries = updateRelationEntryListDict[lsRelSeqId].ToArray();
                updateRelationEntryDict[lsRelSeqId] = entries;

                line = lsRelSeqId.ToString() + " ";
                foreach (string entry in entries)
                {
                    line += (entry + " ");
                }
                line = line.TrimEnd(' ');
                updataDataWriter.WriteLine(line);
            }
            updataDataWriter.Close();                        

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Add Chain Domain IDs.");
            UpdateChainDomainIdToInterfaces(updateEntries);
            
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating Clan Seq IDs.");
            UpdateClanSeqIds();
            
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update CfGroups");
            UpdateRelationCfGroupIDs(updateEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            
#if DEBUG
//            oldDataWriter.Close();
#endif

            return updateRelationEntryDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int[] GetUpdatedRelationsOfEntry()
        {
            List<int> updateRelationList = new List<int> ();
            int relSeqId = 0;
            foreach (DataRow domainInterfaceRow in DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces].Rows)
            {
                relSeqId = Convert.ToInt32(domainInterfaceRow["RelSeqID"].ToString());
                if (!updateRelationList.Contains(relSeqId))
                {
                    updateRelationList.Add(relSeqId);
                }
            }
            return updateRelationList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private Dictionary<int, List<string>> GetUpdateRelationsOfUpdateEntries(string[] updateEntries)
        {
            string queryString = "";
            Dictionary<int, List<string>> updateRelEntryHash = new Dictionary<int,List<string>> ();
            int relSeqId = 0;
            string pdbId = "";
            for (int i = 0; i < updateEntries.Length; i += 500)
            {
                string[] subArray = ParseHelper.GetSubArray (updateEntries, i, 500);
                queryString = string.Format("Select Distinct RelSeqID, PdbID From PfamDomainInterfaces Where PdbID In ({0});", ParseHelper.FormatSqlListString (subArray));
                DataTable relEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow entryRow in relEntryTable.Rows)
                {
                    relSeqId = Convert.ToInt32(entryRow["RelSeqID"].ToString ());
                    pdbId = entryRow["PdbID"].ToString();
                    if (updateRelEntryHash.ContainsKey (relSeqId))
                    {
                        if (!updateRelEntryHash[relSeqId].Contains(pdbId))
                        {
                            updateRelEntryHash[relSeqId].Add(pdbId);
                        }
                    }
                    else
                    {
                        List<string> entryList = new List<string> ();
                        entryList.Add(pdbId);
                        updateRelEntryHash.Add(relSeqId, entryList);
                    }
                }
            }
            StreamReader dataReader = new StreamReader("UpdateRelationsFromDebug.txt");
            string line = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                if (line.IndexOf ("string[0]") > -1)
                {
                    string[] fields = line.Split('\t');
                    relSeqId = Convert.ToInt32 (fields[2].Trim("[]".ToCharArray ()));
                    if (! updateRelEntryHash.ContainsKey (relSeqId))
                    {
                        List<string> entryList = new List<string> ();
                        updateRelEntryHash.Add(relSeqId, entryList);
                    }
                }
            }
            dataReader.Close();
            return updateRelEntryHash;
        }

        #region obsolete entries
        /// <summary>
        /// 
        /// </summary>
        /// <param name="obsEntries"></param>
        /// <param name="updateRelationEntryHash"></param>
        private void AddRelationsWithDeletedObsoleteEntries(Dictionary<int, List<string>> updateRelationEntryDict)
        {
            InterfaceClusterLib.EntryInterfaces.ProtCidObsoleteDataRemover obsEntryRetriever = new EntryInterfaces.ProtCidObsoleteDataRemover();
            string[] obsEntries = obsEntryRetriever.GetObsoleteDomainEntries();

            foreach (string pdbId in obsEntries)
            {
                int[] relSeqIds = GetEntryRelSeqIds(pdbId);
                foreach (int relSeqId in relSeqIds)
                {
                    if (!updateRelationEntryDict.ContainsKey(relSeqId))
                    {
                        List<string> updateEntries = new List<string>();
                        updateRelationEntryDict.Add(relSeqId, updateEntries);
                    }
                }

                DeleteObsData(pdbId);
                DeleteInterfaceCompTable(pdbId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetEntryRelSeqIds (string pdbId)
        {
            string queryString = string.Format("Select Distinct RelSeqID From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int[] relSeqIds = new int[relSeqIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqIds[count] = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString ());
                count++;
            }
            return relSeqIds;
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="dataWriter"></param>
        private void GetDbEntryDomainInterfaces(string pdbId, StreamWriter dataWriter)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string line = "";
            foreach (DataRow domainInterfaceRow in domainInterfaceTable.Rows)
            {
                line = "";
                foreach (object item in domainInterfaceRow.ItemArray)
                {
                    line += (item.ToString().TrimEnd() + ",");
                }
                dataWriter.WriteLine(line.TrimEnd (','));
            }
            dataWriter.Flush();
        }

        #endregion

        #region entries to be updated
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetEntriesWithoutDomainInterfaces()
        {
            string queryString = "Select Distinct PdbID From CrystEntryInterfaces;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);

            queryString = string.Format("Select Distinct PdbID From {0}DomainInterfaces;", ProtCidSettings.dataType);
            DataTable domainInterfaceEntryTable = ProtCidSettings.protcidQuery.Query( queryString);

            List<string> entryNotInDomainInterfaceList = new List<string> ();
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                DataRow[] domainInterfaceRows = domainInterfaceEntryTable.Select(string.Format("PdbID = '{0}'", pdbId));
                if (domainInterfaceRows.Length == 0)
                {
                    entryNotInDomainInterfaceList.Add(pdbId);
                }
            }
            return entryNotInDomainInterfaceList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetEntriesForDomainInterfaces()
        {
            //    string queryString = "Select Distinct PdbId From AsymUnit Where PolymerType = 'polypeptide';";
            //    DataTable entryTable = dbQuery.Query(queryString);
            string queryString = "Select Distinct PdbID From CrystEntryInterfaces;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] entries = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                entries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return entries;
        }

        private string[] GetEntriesInSpecificPfams()
        {
            string[] pfamIds = { "Pkinase", "Pkinase_Tyr", "SH2", "SH3_1", "PDZ", "CBS" };
            List<string> entryList = new List<string> ();
            foreach (string pfamId in pfamIds)
            {
                string[] pfamEntries = GetEntriesWithPfamDomain(pfamId);
                foreach (string pdbId in pfamEntries)
                {
                    if (!entryList.Contains(pdbId))
                    {
                        entryList.Add(pdbId);
                    }
                }
            }
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }

        private string[] GetEntriesWithPfamDomain(string pfamId)
        {
            string queryString = string.Format("Select Distinct PdbId From PdbPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] pfamEntries = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pfamEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return pfamEntries;
        }
        #endregion

        #region delete obsolete data
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteObsData(string pdbId)
        {
            string deleteString = string.Format("Delete From {0} Where PdbID = '{1}';", ProtCidSettings.dataType + "DomainInterfaces", pdbId);
            ProtCidSettings.protcidQuery.Query( deleteString);

            DeleteDomainInterfaceFiles(pdbId);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteDomainInterfaceFiles(string pdbId)
        {
            string fileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, ProtCidSettings.dataType + "domain");
            fileDir = Path.Combine(fileDir, pdbId.Substring(1, 2));
            if (Directory.Exists(fileDir))
            {
                string[] domainInterfaceFiles = Directory.GetFiles(fileDir, pdbId + "*");
                foreach (string interfaceFile in domainInterfaceFiles)
                {
                    if (interfaceFile.IndexOf(pdbId) > -1)
                    {
                        File.Delete(interfaceFile);
                    }
                }
            }
        }
        #endregion

        #region domain-domain interactions from interfaces
        /// <summary>
		/// find domain DomainIDs for the interfaces
		/// </summary>
		/// <param name="groupId"></param>
		/// <param name="pdbId"></param>
		public void GetDomainInterfacesFromCrystInterfaces (string pdbId)
		{
            DataTable interfaceTable = GetEntryInterfaceTable(pdbId);
            
            // 
			// domain definition
			DataTable domainDefTable = GetDomainDefTable (pdbId);

			int dInterfaceId = 1;

            if (DoesEntryContainMultiChainDomains(pdbId))
            {
                GetMultiChainDomainInterfaces(pdbId, interfaceTable, domainDefTable, ref dInterfaceId);
            }
            else
            {
                GetDomainInterfaces(pdbId, interfaceTable, domainDefTable, ref dInterfaceId);
            }
			GetMultiDomainInterfaces (pdbId,  domainDefTable, ref dInterfaceId);
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryInterfaceTable(string pdbId)
        {
            // interface definition
            string queryString = string.Format("Select Distinct * From CrystEntryInterfaces " +
                " Where PdbID = '{0}'  Order By InterfaceID;", pdbId);
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return interfaceTable;
        }

		/// <summary>
		/// check the domain interaction exist in domain interfaces or not
		/// </summary>
		/// <param name="sgInterfaceTable"></param>
		/// <param name="contactsTable"></param>
		/// <param name="domainDefTable"></param>
		private void GetDomainInterfaces (string pdbId, DataTable interfaceTable, DataTable contactsTable,
				DataTable interfaceResiduesTable, DataTable domainDefTable, ref int dInterfaceId)
		{
            
            foreach (DataRow interfaceRow in interfaceTable.Rows)
			{
                AddInterfaceDomainInteractions(interfaceRow, contactsTable, interfaceResiduesTable, domainDefTable, ref dInterfaceId);
			} // interfaceRows
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceRow"></param>
        /// <param name="contactsTable"></param>
        /// <param name="interfaceResiduesTable"></param>
        /// <param name="domainDefTable"></param>
        /// <param name="dInterfaceId"></param>
        private void AddInterfaceDomainInteractions(DataRow interfaceRow, DataTable contactsTable, DataTable interfaceResiduesTable, 
            DataTable domainDefTable, ref int dInterfaceId)
        {
            Dictionary<long, Range[]> entityDomainHash1 = null;
            Dictionary<long, Range[]> entityDomainHash2 = null;
            Dictionary<long, string> entityDomainPfamHash1 = null;
            Dictionary<long, string> entityDomainPfamHash2 = null;
            int relSeqId = -1;
            string family1 = "";
            string family2 = "";
            char isReversed = '0';
            int maxSeq1 = -1;
            int minSeq1 = -1;
            int maxSeq2 = -1;
            int minSeq2 = -1;


            int interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
            int entityId1 = Convert.ToInt32(interfaceRow["EntityID1"].ToString());
            int entityId2 = Convert.ToInt32(interfaceRow["EntityID2"].ToString());
            entityDomainHash1 = GetEntityDomainHash(domainDefTable, entityId1, out entityDomainPfamHash1);
            if (entityId1 == entityId2)
            {
                entityDomainHash2 = entityDomainHash1;
                entityDomainPfamHash2 = entityDomainPfamHash1;
            }
            else
            {
                entityDomainHash2 = GetEntityDomainHash(domainDefTable, entityId2, out entityDomainPfamHash2);
            }

            DataRow[] contactRows = contactsTable.Select(string.Format("InterfaceID = {0}", interfaceId));
            // the maximum and minimum sequential numbers for both chains
            GetMaxMinContactInfo(contactRows, ref maxSeq1, ref minSeq1, ref maxSeq2, ref minSeq2);

            foreach (long domainId1 in entityDomainHash1.Keys)
            {
                Range[] domainRanges1 = (Range[])entityDomainHash1[domainId1];
                family1 = (string)entityDomainPfamHash1[domainId1];
                if (!IsDomainOverlapInteraction(domainRanges1, maxSeq1, minSeq1))
                {
                    continue;
                }
                foreach (long domainId2 in entityDomainPfamHash2.Keys)
                {
                    Range[] domainRanges2 = (Range[])entityDomainHash2[domainId2];
                    family2 = (string)entityDomainPfamHash2[domainId2];
                    if (!IsDomainOverlapInteraction(domainRanges2, maxSeq2, minSeq2))
                    {
                        continue;
                    }
                    if (AreDomainsInteracted(contactsTable, interfaceResiduesTable,
                            domainRanges1, domainRanges2))
                    {
                        relSeqId = GetRelSeqId(family1, family2, out isReversed);

                        DataRow domainInterfaceRow =
                            DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces].NewRow();
                        domainInterfaceRow["RelSeqID"] = relSeqId;
                        domainInterfaceRow["PdbID"] = interfaceRow["PdbID"];
                        domainInterfaceRow["InterfaceID"] = interfaceId;
                        domainInterfaceRow["DomainInterfaceID"] = dInterfaceId;
                        dInterfaceId++;
                        domainInterfaceRow["DomainID1"] = domainId1;
                        domainInterfaceRow["DomainID2"] = domainId2;
                        domainInterfaceRow["AsymChain1"] = interfaceRow["AsymChain1"];
                        domainInterfaceRow["AsymChain2"] = interfaceRow["AsymChain2"];
                        domainInterfaceRow["SurfaceArea"] = -1;
                        domainInterfaceRow["IsReversed"] = isReversed;
                        DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces].Rows.Add(domainInterfaceRow);
                    }
                } // domain2
            } // domain1
        }

        /// <summary>
        /// check the domain interaction exist in domain interfaces or not
        /// </summary>
        /// <param name="sgInterfaceTable"></param>
        /// <param name="contactsTable"></param>
        /// <param name="domainDefTable"></param>
        private void GetDomainInterfaces(string pdbId, DataTable interfaceTable, DataTable domainDefTable, ref int dInterfaceId)
        {
           
            InterfaceChains[] chainInterfaces = null;
            if (interfaceTable.Rows.Count == 0) // no interface defined in the db
            {
                chainInterfaces = interfaceRetriever.FindUniqueInterfaces(pdbId, true);
                interfaceTable = GetEntryInterfaceTable(pdbId);
            }
            else
            {
                // get the chain interface from interface files if available 
                // or recovered from domain interface defintion and xml file
                chainInterfaces = domainInterfaceRetriever.GetCrystInterfaces(pdbId, "cryst");
            }
            if (chainInterfaces == null)
            {
                return;
            }

            foreach (DataRow interfaceRow in interfaceTable.Rows)
            {
                AddInterfaceDomainInteractions(interfaceRow, chainInterfaces, domainDefTable, ref dInterfaceId);
            } 
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceRow"></param>
        /// <param name="chainInterfaces"></param>
        /// <param name="domainDefTable"></param>
        private void AddInterfaceDomainInteractions(DataRow interfaceRow, InterfaceChains[] chainInterfaces, DataTable domainDefTable, ref int dInterfaceId)
        {
            int entityId1 = 0;
            int entityId2 = 0;
            Dictionary<long, Range[]> entityDomainHash1 = null;
            Dictionary<long, Range[]> entityDomainHash2 = null;
            Dictionary<long, string> entityDomainPfamHash1 = null;
            Dictionary<long, string> entityDomainPfamHash2 = null;
            int relSeqId = -1;
            string family1 = "";
            string family2 = "";
            char isReversed = '0';
            int maxSeq1 = -1;
            int minSeq1 = -1;
            int maxSeq2 = -1;
            int minSeq2 = -1;

            int interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
            InterfaceChains chainInterface = domainInterfaceRetriever.GetChainInterface(chainInterfaces, interfaceId);
            if (chainInterface == null)
            {
                return;
            }
            entityId1 = Convert.ToInt32(interfaceRow["EntityID1"].ToString());
            entityId2 = Convert.ToInt32(interfaceRow["EntityID2"].ToString());
            entityDomainHash1 = GetEntityDomainHash(domainDefTable, entityId1, out entityDomainPfamHash1);
            if (entityId1 == entityId2)
            {
                entityDomainHash2 = entityDomainHash1;
                entityDomainPfamHash2 = entityDomainPfamHash1;
            }
            else
            {
                entityDomainHash2 = GetEntityDomainHash(domainDefTable, entityId2, out entityDomainPfamHash2);
            }

            // the maximum and minimum sequential numbers for both chains
            GetMaxMinContactInfo(chainInterface, ref maxSeq1, ref minSeq1, ref maxSeq2, ref minSeq2);

            foreach (long domainId1 in entityDomainHash1.Keys)
            {
                Range[] domainRanges1 = (Range[])entityDomainHash1[domainId1];
                family1 = (string)entityDomainPfamHash1[domainId1];

                if (!IsDomainOverlapInteraction(domainRanges1, maxSeq1, minSeq1))
                {
                    continue;
                }
                foreach (long domainId2 in entityDomainHash2.Keys)
                {
                    Range[] domainRanges2 = (Range[])entityDomainHash2[domainId2];
                    family2 = (string)entityDomainPfamHash2[domainId2];

                    if (!IsDomainOverlapInteraction(domainRanges2, maxSeq2, minSeq2))
                    {
                        continue;
                    }
                    if (AreDomainsInteracted(chainInterface, domainRanges1, domainRanges2))
                    {
                        relSeqId = GetRelSeqId(family1, family2, out isReversed);

                        DataRow domainInterfaceRow =
                            DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces].NewRow();
                        domainInterfaceRow["RelSeqID"] = relSeqId;
                        domainInterfaceRow["PdbID"] = interfaceRow["PdbID"];
                        domainInterfaceRow["InterfaceID"] = interfaceId;
                        domainInterfaceRow["DomainInterfaceID"] = dInterfaceId;
                        dInterfaceId++;
                        domainInterfaceRow["DomainID1"] = domainId1;
                        domainInterfaceRow["DomainID2"] = domainId2;
                        domainInterfaceRow["AsymChain1"] = interfaceRow["AsymChain1"];
                        domainInterfaceRow["AsymChain2"] = interfaceRow["AsymChain2"];
                        domainInterfaceRow["SurfaceArea"] = -1;
                        domainInterfaceRow["IsReversed"] = isReversed;
                        DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces].Rows.Add(domainInterfaceRow);
                    }
                } // domain2
            } // domain1
        }

       
        /// <summary>
        /// first step to check whether the domain regions have some interaction 
        /// </summary>
        /// <param name="domainRanges"></param>
        /// <param name="maxSeq"></param>
        /// <param name="minSeq"></param>
        /// <returns></returns>
        private bool IsDomainOverlapInteraction (Range[] domainRanges, int maxSeq, int minSeq)
        {
            bool isInInterface = false;
            foreach (Range domainRange in domainRanges)
            {
                if ((domainRange.startPos >= minSeq && domainRange.startPos <= maxSeq) ||
                    (domainRange.endPos >= minSeq && domainRange.endPos <= maxSeq) ||
                    (minSeq >= domainRange.startPos && minSeq <= domainRange.endPos) ||
                    (maxSeq >= domainRange.startPos && maxSeq <= domainRange.endPos))
                {
                    isInInterface = true;
                }
            }
            return isInInterface;
        }

        /// <summary>
        /// To determine if an domain-domain interaction exists
        /// at least 10 residure pairs (12ang) and at least one atomic contact (5ang) in the interface
        /// OR at least 5 atomic contacts (5ang)
        /// </summary>
        /// <param name="contactsTable"></param>
        /// <param name="interfaceResiduesTable"></param>
        /// <param name="domainStart1"></param>
        /// <param name="domainEnd1"></param>
        /// <param name="domainStart2"></param>
        /// <param name="domainEnd2"></param>
        /// <returns></returns>
        private bool AreDomainsInteracted (DataTable contactsTable, DataTable interfaceResiduesTable,
            Range[] domainRanges1, Range[] domainRanges2)
        {
            int numOfResidueContacts = 0;
            int seqId1 = -1;
            int seqId2 = -1;
            bool hasEnoughResiduePairs = false;
            bool hasContacts = false;
            int numOfAtomContacts = 0;
            foreach (DataRow residueRow in interfaceResiduesTable.Rows)
            {
                seqId1 = Convert.ToInt32(residueRow["SeqID1"].ToString ());
                seqId2 = Convert.ToInt32(residueRow["SeqID2"].ToString ());
                if (IsResidueSeqIdInDomain (seqId1, domainRanges1) &&
                    IsResidueSeqIdInDomain (seqId2, domainRanges2))
                {
                    numOfResidueContacts++;
                    if (numOfResidueContacts >= AppSettings.parameters.contactParams.numOfResidueContacts)
                    {
                        hasEnoughResiduePairs = true;
                        break;
                    }
                }
            }
            foreach (DataRow contactRow in contactsTable.Rows)
            {
                seqId1 = Convert.ToInt32 (contactRow["SeqID1"].ToString ());
                seqId2 = Convert.ToInt32(contactRow["SeqID2"].ToString ());
                if (IsResidueSeqIdInDomain (seqId1, domainRanges1) &&
                   IsResidueSeqIdInDomain (seqId2, domainRanges2))
                {
                    hasContacts = true;
               //     break;
                    numOfAtomContacts++;
                }
            }
         //   if (hasContacts && hasEnoughResiduePairs)
            // the domain-domain interaction: >= 10 residue pairs with <= 12ang 
            // and at least one atomic contact with <= 5ang
            // OR number of atomic contacts >= 5, since some of domains may be short,
            // July 24, 2012
            // domainNumOfAtomContacts (=5) 
            if ((hasContacts && hasEnoughResiduePairs) ||
                numOfAtomContacts >= AppSettings.parameters.contactParams.domainNumOfAtomContacts)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// use the same rule to determine if an domain-domain interaction exists
        /// at least 10 residue pairs and at least one contact in the interface
        /// </summary>
        /// <param name="chainInterface"></param>
        /// <param name="domainRanges1"></param>
        /// <param name="domainRanges2"></param>
        /// <returns></returns>
        private bool AreDomainsInteracted(InterfaceChains chainInterface, Range[] domainRanges1, Range[] domainRanges2)
        {
            int[] numbersOfInteracted = GetDomainsInteractedNumbers(chainInterface, domainRanges1, domainRanges2);
            if (numbersOfInteracted[0] >= AppSettings.parameters.contactParams.numOfResidueContacts && numbersOfInteracted[1] >= 1)
            {
                return true;
            }
            else if (numbersOfInteracted[1] >= AppSettings.parameters.contactParams.domainNumOfAtomContacts)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// use the same rule to determine if an domain-domain interaction exists
        /// at least 10 residue pairs and at least one contact in the interface
        /// </summary>
        /// <param name="chainInterface"></param>
        /// <param name="domainRanges1"></param>
        /// <param name="domainRanges2"></param>
        /// <returns></returns>
        public int[] GetDomainsInteractedNumbers(InterfaceChains chainInterface, Range[] domainRanges1, Range[] domainRanges2)
        {
            int numOfResidueContacts = 0;
            int seqId1 = -1;
            int seqId2 = -1;
            int numOfAtomContacts = 0;
            foreach (string seqString in chainInterface.seqDistHash.Keys)
            {
                string[] seqIds = seqString.Split('_');
                seqId1 = Convert.ToInt32(seqIds[0]);
                seqId2 = Convert.ToInt32(seqIds[1]);
                if (IsResidueSeqIdInDomain(seqId1, domainRanges1) &&
                    IsResidueSeqIdInDomain(seqId2, domainRanges2))
                {
                    numOfResidueContacts++;
                }
            }
            foreach (string seqString in chainInterface.seqContactHash.Keys)
            {
                string[] seqIds = seqString.Split('_');
                seqId1 = Convert.ToInt32(seqIds[0]);
                seqId2 = Convert.ToInt32(seqIds[1]);
                if (IsResidueSeqIdInDomain(seqId1, domainRanges1) &&
                   IsResidueSeqIdInDomain(seqId2, domainRanges2))
                {
                    numOfAtomContacts++;
                }
            }
            int[] numbersOfInteracted = new int[2];
            numbersOfInteracted[0] = numOfResidueContacts;
            numbersOfInteracted[1] = numOfAtomContacts;
            return numbersOfInteracted;
        }

        /// <summary>
        /// the residue with seqId located in domain or not
        /// </summary>
        /// <param name="seqId"></param>
        /// <param name="domainRanges"></param>
        /// <returns></returns>
        public bool IsResidueSeqIdInDomain(int seqId, Range[] domainRanges)
        {
            foreach (Range range in domainRanges)
            {
                if (seqId >= range.startPos && seqId <= range.endPos)
                {
                    return true;
                }
            }
            return false;
        }
       
		/// <summary>
		/// the domain relation group sequential number
		/// from database
		/// </summary>
		/// <param name="family1"></param>
		/// <param name="family2"></param>
		/// <param name="isReversed"></param>
		/// <param name="isUpdate"></param>
		/// <returns></returns>
		public static int GetRelSeqId (string family1, string family2, out char isReversed)
		{
            DbQuery dbQuery = new DbQuery();
			int relSeqId = -1;
			isReversed = '0';
            if (family2 == "peptide")
            {
                isReversed = '0';
            }
            else
            {
                if (string.Compare(family1, family2) > 0)
                {
                    isReversed = '1';
                }
                else
                {
                    isReversed = '0';
                }
            }
			string queryString = string.Format ("SELECT * FROM {0}DomainFamilyRelation " + 
				" WHERE FamilyCode1 = '{1}' AND FamilyCode2 = '{2}';", 
                ProtCidSettings.dataType, family1, family2);
            DataTable familyRelTable = ProtCidSettings.protcidQuery.Query( queryString);
			if (familyRelTable.Rows.Count == 0)
			{
				queryString = string.Format ("SELECT * FROM {0}DomainFamilyRelation " + 
					" WHERE FamilyCode2 = '{1}' AND FamilyCode1 = '{2}';", 
                    ProtCidSettings.dataType, family1, family2);
                familyRelTable = ProtCidSettings.protcidQuery.Query( queryString);
			}
			if (familyRelTable.Rows.Count > 0)
			{
				relSeqId = Convert.ToInt32 (familyRelTable.Rows[0]["RelSeqID"].ToString ());
			}
			else
			{
                queryString = string.Format("SELECT Distinct RelSeqID FROM {0}DomainFamilyRelation;", ProtCidSettings.dataType);
                familyRelTable = ProtCidSettings.protcidQuery.Query( queryString);

                int maxRelSeqId = GetMaxRelSeqIDInDb (familyRelTable);
                relSeqId = maxRelSeqId + 1;
                
				if (isReversed == '0')
				{
					queryString = string.Format ("INSERT INTO {0}DomainFamilyRelation VALUES ({1}, '{2}', '{3}', -1);", 
						ProtCidSettings.dataType, relSeqId, family1, family2);
				}
				else
				{
					queryString = string.Format ("INSERT INTO {0}DomainFamilyRelation VALUES ({1}, '{2}', '{3}', -1);", 
						ProtCidSettings.dataType, relSeqId, family2, family1);
				}
                ProtCidSettings.protcidQuery.Query( queryString);
			}
			return relSeqId;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="familyRelTable"></param>
        /// <returns></returns>
        public static int GetMaxRelSeqIDInDb (DataTable familyRelTable)
        {
            int maxRelSeqId = 0;
            int relSeqId = 1;
            foreach (DataRow relSeqRow in familyRelTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqRow["RelSeqID"].ToString ());
                if (maxRelSeqId < relSeqId)
                {
                    maxRelSeqId = relSeqId;
                }
            }
            return maxRelSeqId;
        }
		/// <summary>
		/// the maximum sequence nubmers and minimum sequence numbers for contacts
		/// </summary>
		/// <param name="contactRows"></param>
		/// <param name="maxSeq1"></param>
		/// <param name="minSeq1"></param>
		/// <param name="maxSeq2"></param>
		/// <param name="minSeq2"></param>
		private void GetMaxMinContactInfo (DataRow[] contactRows, ref int maxSeq1, ref int minSeq1, ref int maxSeq2, ref int minSeq2)
		{
			maxSeq1 = -1;
			minSeq1 = 999999;
			maxSeq2 = -1;
			minSeq2 = 999999;
			foreach (DataRow contactRow in contactRows)
			{
				int seqId1 = ParseHelper.ConvertSeqToInt (contactRow["SeqID1"].ToString ());
				if (maxSeq1 < seqId1)
				{
					maxSeq1 = seqId1;
				}
				if (minSeq1 > seqId1)
				{
					minSeq1 = seqId1;
				}
				int seqId2 = ParseHelper.ConvertSeqToInt (contactRow["SeqID2"].ToString ());
				if (maxSeq2 < seqId2)
				{
					maxSeq2 = seqId2;
				}
				if (minSeq2 > seqId2)
				{
					minSeq2 = seqId2;
				}
			}
		}

        /// <summary>
        /// the maximum sequence nubmers and minimum sequence numbers for contacts
        /// </summary>
        /// <param name="contactRows"></param>
        /// <param name="maxSeq1"></param>
        /// <param name="minSeq1"></param>
        /// <param name="maxSeq2"></param>
        /// <param name="minSeq2"></param>
        private void GetMaxMinContactInfo(InterfaceChains chainInterface, ref int maxSeq1, ref int minSeq1, ref int maxSeq2, ref int minSeq2)
        {
            maxSeq1 = -1;
            minSeq1 = 999999;
            maxSeq2 = -1;
            minSeq2 = 999999;
           
            foreach (string seqString in chainInterface.seqContactHash.Keys)
            {
                string[] seqIds = seqString.Split('_');
                int seqId1 = ParseHelper.ConvertSeqToInt(seqIds[0]);
                if (maxSeq1 < seqId1)
                {
                    maxSeq1 = seqId1;
                }
                if (minSeq1 > seqId1)
                {
                    minSeq1 = seqId1;
                }
                int seqId2 = ParseHelper.ConvertSeqToInt(seqIds[1]);
                if (maxSeq2 < seqId2)
                {
                    maxSeq2 = seqId2;
                }
                if (minSeq2 > seqId2)
                {
                    minSeq2 = seqId2;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainDefTable"></param>
        /// <param name="dInterfaceId"></param>
        private void GetMultiDomainInterfaces(string pdbId, DataTable domainDefTable, ref int dInterfaceId)
        {
            if (DoesEntryContainIntraChainDomains (pdbId, domainDefTable))
            {
                chainDomainInterfaces.RetrieveDomainInteractionsWithinProteins(pdbId, domainDefTable, ref dInterfaceId);
            }
        }

        /// <summary>
        /// multiple domains in one chain
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entryDomainDefTable"></param>
        /// <returns></returns>
        private bool DoesEntryContainIntraChainDomains(string pdbId, DataTable entryDomainDefTable)
        {
            List<int> entityIdList = new List<int> ();
            foreach (DataRow domainDefRow in entryDomainDefTable.Rows)
            {
                int entityId = Convert.ToInt32 (domainDefRow["EntityID"].ToString ());
                if (!entityIdList.Contains(entityId))
                {
                    entityIdList.Add(entityId);
                }
            }
            
            foreach (int entityId in entityIdList)
            {
                List<long> domainIdList = new List<long> ();
                DataRow[] domainRows = entryDomainDefTable.Select(string.Format ("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId));
                foreach (DataRow domainRow in domainRows)
                {
                    long domainId = Convert.ToInt64(domainRow["DomainID"].ToString ());
                    if (!domainIdList.Contains(domainId))
                    {
                        domainIdList.Add(domainId);
                    }
                }
                if (domainIdList.Count > 1)
                {
                    return true;
                }
            }
            return false;
        }

		#region domain definition table 
		/// <summary>
		/// scop definitions of the entry domains if entry is in SCOP
		/// otherwise, find the domain definition with largest ZScores to SCOP domains
		/// </summary>
		/// <param name="pdbId"></param>
		/// <returns></returns>
		private DataTable GetDomainDefTable (string pdbId)
		{
            DataTable domainDefTable = pfamArch.GetPfamDomainDefTable (pdbId);
			return domainDefTable;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainDefTable"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private Dictionary<long, Range[]> GetEntityDomainHash(DataTable domainDefTable, int entityId, out Dictionary<long, string> entityDomainPfamHash)
        {
            Dictionary<long, List<Range>> entityDomainListHash = new Dictionary<long,List<Range>> ();
            entityDomainPfamHash = new Dictionary<long,string> ();
            long domainId = 0;
            DataRow[] entityDomainRows = domainDefTable.Select(string.Format ("EntityID = {0}", entityId));
            foreach (DataRow domainRow in entityDomainRows)
            {
                domainId = Convert.ToInt64(domainRow["DomainID"].ToString ());
                Range region = new Range();
                region.startPos = Convert.ToInt32(domainRow["SeqStart"].ToString ());
                region.endPos = Convert.ToInt32(domainRow["SeqEnd"].ToString ());
                if (entityDomainListHash.ContainsKey(domainId))
                {
                    entityDomainListHash[domainId].Add(region);
                }
                else
                {
                    List<Range> rangeList = new List<Range> ();
                    rangeList.Add(region);
                    entityDomainListHash.Add(domainId, rangeList);
                }
                if (! entityDomainPfamHash.ContainsKey(domainId))
                {
                    entityDomainPfamHash.Add(domainId, domainRow["Pfam_ID"].ToString ().TrimEnd ());
                }
            }
            Dictionary<long, Range[]> entityDomainHash = new Dictionary<long, Range[]>();
            foreach (long hashDomainId in entityDomainListHash.Keys)
            {
                entityDomainHash.Add (hashDomainId, entityDomainListHash[hashDomainId].ToArray ()); 
            }
            return entityDomainHash;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainDefTable"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private Dictionary<long, Range[]> GetChainDomainHash(DataTable domainDefTable, DataTable chainDomainTable, string asymChain, out Dictionary<long, string> chainDomainPfamHash)
        {
            long domainId = 0;
            Dictionary<long, Range[]> chainDomainHash = new Dictionary<long,Range[]> ();
            chainDomainPfamHash = new Dictionary<long, string>();

            DataRow[] chainDomainRows = chainDomainTable.Select(string.Format ("AsymChain = '{0}'", asymChain));
            List<long> chainDomainIdList = new List<long> ();
            foreach (DataRow domainRow in chainDomainRows)
            {
                domainId = Convert.ToInt64(domainRow["DomainID"].ToString ());
                if (!chainDomainIdList.Contains(domainId))
                {
                    chainDomainIdList.Add(domainId);
                }
            }
            foreach (long chainDomainId in chainDomainIdList)
            {
                DataRow[] domainRows = domainDefTable.Select(string.Format("DomainID= '{0}'", chainDomainId));
                List<Range> domainRangeList = new List<Range> ();
                foreach (DataRow domainRow in domainRows)
                {
                    Range region = new Range();
                    region.startPos = Convert.ToInt32(domainRow["SeqStart"].ToString());
                    region.endPos = Convert.ToInt32(domainRow["SeqEnd"].ToString());
                    domainRangeList.Add(region);
                }
                Range[] domainRanges = new Range[domainRangeList.Count];
                domainRangeList.CopyTo(domainRanges);
                chainDomainHash.Add(chainDomainId, domainRanges);
                chainDomainPfamHash.Add(chainDomainId, domainRows[0]["Pfam_ID"].ToString().TrimEnd());
            }
            return chainDomainHash;
        }
		/// <summary>
		/// domain definition for Non-SCOP entry
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="groupId"></param>
		/// <returns></returns>
		private void GetNonScopDomainDefTable (string pdbId, ref DataTable domainDefTable)
		{
			string queryString = string.Format ("SELECT PdbID, Class, Fold, SuperFamily, Family, " + 
				" AsymChain, DomainID, StartPos, EndPos, SeqStartPos, SeqEndPos FROM NonscopDomainDef " + 
				" Where PdbID = '{0}';", pdbId);
            DataTable nonScopDomainDefTable = ProtCidSettings.pdbfamQuery.Query( queryString);
			foreach (DataRow dRow in nonScopDomainDefTable.Rows)
			{
				DataRow newRow = domainDefTable.NewRow ();
				newRow["PdbID"] = pdbId;
				newRow["Class"] = dRow["Class"].ToString ();
				newRow["Fold"] = dRow["Fold"];
				newRow["Superfamily"] = dRow["Superfamily"];
				newRow["Family"] = dRow["Family"];
				newRow["AsymChain"] = dRow["Chain"];
				newRow["DomainID"] = dRow["DomainID"];
				newRow["StartPos"] = dRow["StartPos"];
				newRow["EndPos"] = dRow["EndPos"];
				newRow["SeqStartPos"] = dRow["SeqStartPos"];
				newRow["SeqEndPos"] = dRow["SeqEndPos"];
		//		newRow.ItemArray = dRow.ItemArray;
				domainDefTable.Rows.Add (newRow);
			}
		}
		#endregion

		/// <summary>
		/// the family codes for the domain interactions
		/// </summary>
		/// <param name="DomainID1"></param>
		/// <param name="DomainID2"></param>
		/// <returns></returns>
		private string GetDomainFamilyCode (DataRow domainRow)
		{
            string familyCode = "";
            if (ProtCidSettings.dataType == "scop")
            {
                familyCode = domainRow["Class"].ToString().Trim() + "." +
                     domainRow["Fold"].ToString() + "." +
                     domainRow["Superfamily"].ToString() + "." +
                     domainRow["Family"].ToString();
            }
            else if (ProtCidSettings.dataType == "pfam")
            {
                familyCode = domainRow["Pfam_ACC"].ToString().TrimEnd();
            }
			return familyCode;
		}
		/// <summary>
		/// family combinations of domain interactions 
		/// </summary>
		public void SetDomainInteractionFamilyRel (List<string> familyRelList)
		{
			for (int i = 0; i < familyRelList.Count; i ++)
			{
				DataRow relRow = 
					DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainFamilyRel].NewRow ();
				string[] familyCodes = familyRelList[i].ToString ().Split ('_');
				relRow["RelSeqID"] = i + 1;
				relRow["FamilyCode1"] = familyCodes[0];
				relRow["FamilyCode2"] = familyCodes[1];
				DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainFamilyRel].Rows.Add (relRow);
			}
			dbInsert.InsertDataIntoDBtables (ProtCidSettings.protcidDbConnection,
				DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainFamilyRel]);
		}
		#endregion	

        #region update family relations
        /// <summary>
        /// change relation seq ids to be in the order of familycode1 and familycode2
        /// </summary>
        public void UpdateRelSeqIDs()
        {
            StreamWriter dataWriter = new StreamWriter("UpdateRelSeqIds.txt");

            AddNewRelSeqIDColumnToTables();

            string queryString = "Select RelSeqID From PfamDomainFamilyRelation Order By FamilyCode1, FamilyCode2;";
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int newRelSeqId = 1;
            int orgRelSeqId = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                orgRelSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString());
                UpdateNewRelSeqIdsInDb(orgRelSeqId, newRelSeqId);
                dataWriter.WriteLine(orgRelSeqId.ToString() + "\t" + newRelSeqId.ToString());
                newRelSeqId++;
            }
            dataWriter.Close();

            UpdateRelSeqIdFromNewRelSeqIds();
            DropNewRelSeqIdColumnInTables();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="orgRelSeqId"></param>
        /// <param name="newRelSeqId"></param>
        private void UpdateNewRelSeqIdsInDb(int orgRelSeqId, int newRelSeqId)
        {
            string dbTableName = "PfamDomainFamilyRelation";
            UpdateNewRelSeqId(dbTableName, orgRelSeqId, newRelSeqId);

            dbTableName = "PfamDomainInterfaces";
            UpdateNewRelSeqId(dbTableName, orgRelSeqId, newRelSeqId);

            /*     dbTableName = "PfamDomainInterfaceComp";
                 UpdateNewRelSeqId(dbTableName, orgRelSeqId, newRelSeqId);

                 dbTableName = "PfamEntryDomainInterfaceComp";
                 UpdateNewRelSeqId(dbTableName, orgRelSeqId, newRelSeqId);*/
        }

        #region add and drop newrelseqid
        /// <summary>
        /// 
        /// </summary>
        private void AddNewRelSeqIDColumnToTables()
        {
            string[] tableNames = { "PfamDomainFamilyRelation", "PfamDomainInterfaces" };
            foreach (string tableName in tableNames)
            {
                AddNewRelSeqIdColumnToTable(tableName);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableName"></param>
        private void AddNewRelSeqIdColumnToTable(string tableName)
        {
            string alterTableString = string.Format("Alter Table {0} Add NewRelSeqID Integer;", tableName);
            ProtCidSettings.protcidQuery.Query( alterTableString);
        }

        /// <summary>
        /// 
        /// </summary>
        private void DropNewRelSeqIdColumnInTables()
        {
            string[] tableNames = { "PfamDomainFamilyRelation", "PfamDomainInterfaces" };
            foreach (string tableName in tableNames)
            {
                DropNewRelSeqIdColumn(tableName);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableName"></param>
        private void DropNewRelSeqIdColumn(string tableName)
        {
            string dropColString = string.Format("ALTER TABLE {0} DROP NewRelSeqID;", tableName);
            ProtCidSettings.protcidQuery.Query( dropColString);
        }

        /// <summary>
        /// 
        /// </summary>
        private void UpdateRelSeqIdFromNewRelSeqIds()
        {
            string[] tableNames = { "PfamDomainFamilyRelation", "PfamDomainInterfaces" };
            foreach (string tableName in tableNames)
            {
                UpdateRelSeqIdsFromNewRelSeqIds(tableName);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableName"></param>
        private void UpdateRelSeqIdsFromNewRelSeqIds(string tableName)
        {
            string updateString = string.Format("Update {0} Set RelSeqID = NewRelSeqID;", tableName);
            ProtCidSettings.protcidQuery.Query( updateString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbTableName"></param>
        /// <param name="orgRelSeqId"></param>
        /// <param name="newRelSeqId"></param>
        private void UpdateNewRelSeqId(string dbTableName, int orgRelSeqId, int newRelSeqId)
        {
            string updateString = string.Format("Update {0} Set NewRelSeqId = {1} " +
                " Where RelSeqID = {2};", dbTableName, newRelSeqId, orgRelSeqId);
            ProtCidSettings.protcidQuery.Query( updateString);
        }
        #endregion

        #region re-order cf groups in a relation
        private int nextGroupSeqId = 0;
        private Dictionary<string, int[]> entryFakeCfGroupIdHash = new Dictionary<string,int[]> ();
        /// <summary>
        /// 
        /// </summary>
        public void GetRelationCfGroupIDs()
        {
            string cfGroupString = "Select PdbID, GroupSeqID, CfGroupID From PfamNonRedundantCfGroups;";
            pfamEntryCfGroupTable = ProtCidSettings.protcidQuery.Query( cfGroupString);
            string repHomoString = "Select PdbID1, PdbID2 From PfamHomoRepEntryAlign;";
            homoRepTable = ProtCidSettings.protcidQuery.Query( repHomoString);

            AppSettings.LoadCrystMethods();
            nextGroupSeqId = GetMaxGroupSeqID() + 1;
            DataTable relationCfGroupTable = CreateRelationCfGroupTable();

            string queryString = "Select Distinct RelSeqID From PfamDomainFamilyRelation;";
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = relSeqIdTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = relSeqIdTable.Rows.Count;

            int relSeqId = -1;
            int relCfGroupId = 1;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString ());

                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                relCfGroupId = 1;
                Dictionary<int, List<int>> groupCfGroupIdHash = GetGroupCfGroups(relSeqId);
                List<int> groupIdList = new List<int> (groupCfGroupIdHash.Keys);
                groupIdList.Sort();
                foreach (int groupId in groupIdList)
                {
                    List<int> cfGroupIdList = groupCfGroupIdHash[groupId];
                    cfGroupIdList.Sort();
                    foreach (int cfGroupId in cfGroupIdList)
                    {
                        DataRow cfGroupRow = relationCfGroupTable.NewRow();
                        cfGroupRow["RelSeqID"] = relSeqId;
                        cfGroupRow["RelCfGroupID"] = relCfGroupId;
                        cfGroupRow["GroupSeqID"] = groupId;
                        cfGroupRow["CfGroupID"] = cfGroupId;
                        relationCfGroupTable.Rows.Add(cfGroupRow);
                        relCfGroupId++;
                    }
                }
                dbInsert.BatchInsertDataIntoDBtables (ProtCidSettings.protcidDbConnection, relationCfGroupTable);
                relationCfGroupTable.Clear();
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateRelationCfGroupIDs(string[] updateEntries)
        {
            AppSettings.LoadCrystMethods();
            nextGroupSeqId = GetMaxGroupSeqID() + 1;
            DataTable relationCfGroupTable = CreateRelationCfGroupTable();
        //    string[] updateEntries = { "1ocq", "1w3l", "2v38", "3izs", "3izr", "1ni1", "3c72", "3u9g" };
            int[] relSeqIds = GetUpdateRelSeqIDs(updateEntries);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = relSeqIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = relSeqIds.Length;
            int relCfGroupId = 1;
            foreach (int relSeqId in relSeqIds)
            {
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                DeleteRelCfGroupInfo(relSeqId);

                relCfGroupId = 1;
                Dictionary<int, List<int>> groupCfGroupIdHash = GetGroupCfGroups(relSeqId);
                List<int> groupIdList = new List<int> (groupCfGroupIdHash.Keys);
                groupIdList.Sort();
                foreach (int groupId in groupIdList)
                {
                    List<int> cfGroupIdList = groupCfGroupIdHash[groupId];
                    cfGroupIdList.Sort();
                    foreach (int cfGroupId in cfGroupIdList)
                    {
                        DataRow cfGroupRow = relationCfGroupTable.NewRow();
                        cfGroupRow["RelSeqID"] = relSeqId;
                        cfGroupRow["RelCfGroupID"] = relCfGroupId;
                        cfGroupRow["GroupSeqID"] = groupId;
                        cfGroupRow["CfGroupID"] = cfGroupId;
                        relationCfGroupTable.Rows.Add(cfGroupRow);
                        relCfGroupId++;
                    }
                }
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, relationCfGroupTable);
                relationCfGroupTable.Clear();
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        private void DeleteRelCfGroupInfo(int relSeqId)
        {
            string deleteString = string.Format("Delete From PfamDomainCfGroups WHere RelSeqID = {0};", relSeqId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private int[] GetUpdateRelSeqIDs(string[] updateEntries)
        {
            string queryString = "";
            int relSeqId = 0;
            List<int> relSeqIdList = new List<int> ();
            foreach (string pdbId in updateEntries)
            {
                queryString = string.Format("Select Distinct RelSeqID From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
                DataTable relSeqTable = ProtCidSettings.protcidQuery.Query( queryString);
                foreach (DataRow relSeqRow in relSeqTable.Rows)
                {
                    relSeqId = Convert.ToInt32(relSeqRow["RelSeqID"].ToString ());
                    if (!relSeqIdList.Contains(relSeqId))
                    {
                        relSeqIdList.Add(relSeqId);
                    }
                }
            }
            return relSeqIdList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable CreateRelationCfGroupTable()
        {
            string[] relCfGroupColumns = { "RelSeqID", "RelCfGroupID", "GroupSeqID", "CfGroupID"};
            DataTable relCfGroupTable = new DataTable("PfamDomainCfGroups");
            foreach (string cfGroupCol in relCfGroupColumns)
            {
                relCfGroupTable.Columns.Add(new DataColumn (cfGroupCol));
            }
            return relCfGroupTable;
        }

        private int GetMaxGroupSeqID()
        {
            string queryString = "Select Max(GroupSeqID) As MaxSeqID From PfamGroups;";
            DataTable maxSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            return Convert.ToInt32(maxSeqIdTable.Rows[0]["MaxSeqID"].ToString ());
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private Dictionary<int, List<int>> GetGroupCfGroups(int relSeqId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamDomainInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            Dictionary<int, List<int>> groupCfGroupHash = new Dictionary<int,List<int>> ();
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                int[] groupCfGroupIds = GetEntryCfGroup(pdbId);
                if (groupCfGroupIds == null)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("No CfGroupIDs for " + pdbId);
                    ProtCidSettings.logWriter.WriteLine("No CfGroupIDs for " + pdbId);
                    ProtCidSettings.logWriter.Flush();
                }
                else 
                {
                    if (groupCfGroupHash.ContainsKey(groupCfGroupIds[0]))
                    {
                        if (!groupCfGroupHash[groupCfGroupIds[0]].Contains(groupCfGroupIds[1]))
                        {
                            groupCfGroupHash[groupCfGroupIds[0]].Add(groupCfGroupIds[1]);
                        }
                    }
                    else 
                    {
                        List<int> cfGroupIdList = new List<int> ();
                        cfGroupIdList.Add(groupCfGroupIds[1]);
                        groupCfGroupHash.Add(groupCfGroupIds[0], cfGroupIdList);
                    }
                }
            }
            return groupCfGroupHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public int[] GetEntryCfGroup(string pdbId)
        {
            DataRow[] entryCfGroupRows = null;
            if (pfamEntryCfGroupTable != null)
            {
                entryCfGroupRows = pfamEntryCfGroupTable.Select(string.Format ("PdbID = '{0}'", pdbId));
                if (entryCfGroupRows.Length == 0)
                {
                    string repEntry = GetRepEntry(pdbId);
                    entryCfGroupRows = pfamEntryCfGroupTable.Select(string.Format("PdbID = '{0}'", repEntry));
                }
            }
            else
            {
                string queryString = string.Format("Select GroupSeqID, CfGroupID From PfamNonRedundantCfGroups " +
                    " Where PdbID = '{0}' Order By GroupSeqID, CfGroupID;", pdbId);
                DataTable cfGroupTable = ProtCidSettings.protcidQuery.Query( queryString);
                if (cfGroupTable.Rows.Count == 0)
                {
                    string repEntry = GetRepEntry(pdbId);
                    queryString = string.Format("Select GroupSeqID, CfGroupID From PfamNonRedundantCfGroups " +
                    " Where PdbID = '{0}' Order By GroupSeqID, CfGroupID;", repEntry);
                    cfGroupTable = ProtCidSettings.protcidQuery.Query( queryString);
                }
                entryCfGroupRows = cfGroupTable.Select();
            }
            if (entryCfGroupRows.Length > 0)
            {
                int[] groupCfGroupIds = new int[2];
                groupCfGroupIds[0] = Convert.ToInt32(entryCfGroupRows[0]["GroupSeqID"].ToString());
                groupCfGroupIds[1] = Convert.ToInt32(entryCfGroupRows[0]["CfGroupID"].ToString());
                return groupCfGroupIds;
            }
            else
            {
                int[] groupCfGroupIds = null;
                if (entryFakeCfGroupIdHash.ContainsKey(pdbId))
                {
                    groupCfGroupIds = entryFakeCfGroupIdHash[pdbId];
                }
                else
                {
                    groupCfGroupIds = new int[2];
                    groupCfGroupIds[0] = nextGroupSeqId;
                    groupCfGroupIds[1] = 1;
                    entryFakeCfGroupIdHash.Add(pdbId, groupCfGroupIds);

                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("No CfGroupIDs for " + pdbId + " fake group id: " + nextGroupSeqId.ToString());
                    ProtCidSettings.logWriter.WriteLine("No CfGroupIDs for " + pdbId + " fake group id: " + nextGroupSeqId.ToString());
                    ProtCidSettings.logWriter.Flush();

                    nextGroupSeqId++;
                }
                return groupCfGroupIds;
            }
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetRepEntry(string pdbId)
        {
            if (homoRepTable != null)
            {
                DataRow[] repRows = homoRepTable.Select(string.Format ("PdbID2 = '{0}'", pdbId));
                if (repRows.Length > 0)
                {
                    return repRows[0]["PdbID1"].ToString();
                }
            }
            else
            {
                string queryString = string.Format("Select PdbID1 From PfamHomoRepEntryAlign Where PdbID2 = '{0}';", pdbId);
                DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
                if (entryTable.Rows.Count > 0)
                {
                    return entryTable.Rows[0]["PdbID1"].ToString();
                }
            }
            return "";
        }
        #endregion
        #endregion

        #region clan seq ids
        /// <summary>
        /// 
        /// </summary>
        public void AddClanSeqIds()
        {
            string queryString = "Select RelSeqId, FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Order by RelSeqID;";
            DataTable familyRelationTable = ProtCidSettings.protcidQuery.Query( queryString);
            UpdateClanSeqIds(familyRelationTable);
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateClanSeqIds()
        {
            string queryString = "Select RelSeqID, FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation " +
                " Where ClanSeqId = -1;";
            DataTable familyRelationTable = ProtCidSettings.protcidQuery.Query( queryString);
            UpdateClanSeqIds(familyRelationTable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="familyRelationTable"></param>
        public void UpdateClanSeqIds(DataTable familyRelationTable)
        {
            int relSeqId = 0;
            int clanSeqId = 1;
            string familyId1 = "";
            string familyId2 = "";
            string clanId1 = "";
            string clanId2 = "";
            Dictionary<string, string> familyClanHash = new Dictionary<string,string> ();
            foreach (DataRow relRow in familyRelationTable.Rows)
            {
                relSeqId = Convert.ToInt32(relRow["RelSeqID"].ToString());
                familyId1 = relRow["FamilyCode1"].ToString().TrimEnd();
                familyId2 = relRow["FamilyCode2"].ToString().TrimEnd();

                clanId1 = GetClanId(familyId1, ref familyClanHash);
                if (clanId1 == "")
                {
                    clanId1 = familyId1;
                }
                clanId2 = GetClanId(familyId2, ref familyClanHash);
                if (clanId2 == "")
                {
                    clanId2 = familyId2;
                }

                clanSeqId = GetClanSeqID(clanId1, clanId2);
                UpdateClanSeqId(relSeqId, clanSeqId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clanSeqId"></param>
        private void UpdateClanSeqId(int relSeqId, int clanSeqId)
        {
            string updateString = string.Format("Update PfamDomainFamilyRelation Set ClanSeqID = {0} " +
                "WHERE RelSeqID = {1};", clanSeqId, relSeqId);
            ProtCidSettings.protcidQuery.Query( updateString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanId1"></param>
        /// <param name="clanId2"></param>
        /// <returns></returns>
        private int GetClanSeqID(string clanId1, string clanId2)
        {
            int clanSeqId = -1;
            bool isReversed = false;
            if (string.Compare(clanId1, clanId2) > 0)
            {
                isReversed = true;
            }

            string queryString = string.Format("SELECT * FROM {0}DomainClanRelation " +
                " WHERE ClanCode1 = '{1}' AND ClanCode2 = '{2}';",
                ProtCidSettings.dataType, clanId1, clanId2);
            DataTable clanRelTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (clanRelTable.Rows.Count == 0)
            {
                queryString = string.Format("SELECT * FROM {0}DomainClanRelation " +
                    " WHERE ClanCode2 = '{1}' AND ClanCode1 = '{2}';",
                    ProtCidSettings.dataType, clanId1, clanId2);
                clanRelTable = ProtCidSettings.protcidQuery.Query( queryString);
            }
            if (clanRelTable.Rows.Count > 0)
            {
                clanSeqId = Convert.ToInt32(clanRelTable.Rows[0]["ClanSeqID"].ToString());
            }
            else
            {
                queryString = string.Format("SELECT Distinct ClanSeqID FROM {0}DomainClanRelation;", ProtCidSettings.dataType);
                clanRelTable = ProtCidSettings.protcidQuery.Query( queryString);

                int maxClanSeqId = GetMaxClanSeqIDInDb(clanRelTable);
                clanSeqId = maxClanSeqId + 1;

                if (!isReversed)
                {
                    queryString = string.Format("INSERT INTO {0}DomainClanRelation VALUES ({1}, '{2}', '{3}');",
                        ProtCidSettings.dataType, clanSeqId, clanId1, clanId2);
                }
                else
                {
                    queryString = string.Format("INSERT INTO {0}DomainClanRelation VALUES ({1}, '{2}', '{3}');",
                        ProtCidSettings.dataType, clanSeqId, clanId2, clanId1);
                }
                ProtCidSettings.protcidQuery.Query( queryString);
            }
            return clanSeqId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="familyRelTable"></param>
        /// <returns></returns>
        public static int GetMaxClanSeqIDInDb(DataTable clanRelTable)
        {
            int maxClanSeqId = 0;
            int clanSeqId = 1;
            foreach (DataRow relSeqRow in clanRelTable.Rows)
            {
                clanSeqId = Convert.ToInt32(relSeqRow["ClanSeqID"].ToString());
                if (maxClanSeqId < clanSeqId)
                {
                    maxClanSeqId = clanSeqId;
                }
            }
            return maxClanSeqId;
        }

        #region get clan id
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="familyClanHash"></param>
        /// <returns></returns>
        private string GetClanId(string pfamId, ref Dictionary<string, string> familyClanHash)
        {
            string clanId = "";
            if (familyClanHash.ContainsKey(pfamId))
            {
                clanId = familyClanHash[pfamId];
            }
            else
            {
                string pfamAcc = GetPfamAccFromPfamId(pfamId);
                if (pfamAcc == "")
                {
                    return "";
                }
                string clanAcc = GetClanAccFromPfamAcc(pfamAcc);
                if (clanAcc == "")
                {
                    return "";
                }
                clanId = GetClanIdFromClanAcc(clanAcc);
                familyClanHash.Add(pfamId, clanId);
            }
            return clanId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string GetPfamAccFromPfamId(string pfamId)
        {
            string queryString = string.Format("Select Pfam_Acc From PfamHmm Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamAccTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pfamAcc = "";
            if (pfamAccTable.Rows.Count > 0)
            {
                pfamAcc = pfamAccTable.Rows[0]["Pfam_Acc"].ToString().TrimEnd();
                int dotIndex = pfamAcc.IndexOf(".");
                if (dotIndex > 0)
                {
                    pfamAcc = pfamAcc.Substring(0, pfamAcc.IndexOf("."));
                }
            }
            return pfamAcc;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAcc"></param>
        /// <returns></returns>
        private string GetClanAccFromPfamAcc(string pfamAcc)
        {
            string queryString = string.Format("Select Clan_Acc From PfamClanFamily WHere Pfam_Acc = '{0}';", pfamAcc);
            DataTable clanAccTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string clanAcc = "";
            if (clanAccTable.Rows.Count > 0)
            {
                clanAcc = clanAccTable.Rows[0]["Clan_Acc"].ToString().TrimEnd();
            }
            return clanAcc;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanAcc"></param>
        /// <returns></returns>
        private string GetClanIdFromClanAcc(string clanAcc)
        {
            string queryString = string.Format("Select Clan_ID From PfamClans Where Clan_Acc = '{0}';", clanAcc);
            DataTable clanIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string clanId = "";
            if (clanIdTable.Rows.Count > 0)
            {
                clanId = clanIdTable.Rows[0]["Clan_ID"].ToString().TrimEnd();
            }
            return clanId;
        }
        #endregion
        #endregion

        #region check short peptides
        /// <summary>
        /// 
        /// </summary>
        public void CheckShortPeptides()
        {
            StreamWriter dataWriter = new StreamWriter("EntryWithChainNoInterfaces.txt");
            string queryString = "Select PdbId, AsymID From AsymUnit " + 
                " Where PolymerType = 'polypeptide' Order By PdbID, AsymID;";
            DataTable entryChainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pdbId = "";
            string asymId = "";
            string prePdbId = "";
            List<string> chainList = new List<string> ();
            List<string> entryList = new List<string> ();
            foreach (DataRow chainRow in entryChainTable.Rows)
            {
                pdbId = chainRow["PdbID"].ToString();
                asymId = chainRow["AsymID"].ToString().TrimEnd();

                if (pdbId != prePdbId)
                {
                    if (chainList.Count > 0)
                    {
                        DataTable entrySeqTable = GetEntrySequenceTable(prePdbId);
                        string[] entryChains = new string[chainList.Count];
                        chainList.CopyTo(entryChains);
                        string[] leftChains = GetChainsNotInInterfaces(prePdbId, entryChains);
                        if (leftChains.Length > 0)
                        {
                            entryList.Add(prePdbId);
                        }
                        foreach (string leftChain in leftChains)
                        {
                            int[] seqLengths = GetSequenceLengths(leftChain, entrySeqTable);
                            dataWriter.WriteLine(prePdbId + "\t" + leftChain + "\t" + 
                                seqLengths[0].ToString () + "\t" + seqLengths[1].ToString ());
                        }
                        dataWriter.Flush();
                        chainList.Clear();
                        
                    }
                }
                chainList.Add(asymId);
                prePdbId = pdbId;
            }
            if (chainList.Count > 0)
            {
                DataTable entrySeqTable = GetEntrySequenceTable(pdbId);
                string[] entryChains = new string[chainList.Count];
                chainList.CopyTo(entryChains);
                string[] leftChains = GetChainsNotInInterfaces(pdbId, entryChains);
                foreach (string leftChain in leftChains)
                {
                    int[] seqLengths = GetSequenceLengths(leftChain, entrySeqTable);
                    dataWriter.WriteLine(pdbId + "\t" + leftChain + "\t" +
                        seqLengths[0].ToString() + "\t" + seqLengths[1].ToString());
                }
                if (leftChains.Length > 0)
                {
                    entryList.Add(pdbId);
                }
            }
            dataWriter.WriteLine("#Entries: " + entryList.Count.ToString ());
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChains"></param>
        /// <returns></returns>
        private string[] GetChainsNotInInterfaces(string pdbId, string[] asymChains)
        {
            string queryString = string.Format("Select Distinct AsymChain1, AsymChain2 From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable interfaceChainTable = ProtCidSettings.protcidQuery.Query( queryString);
            string interfaceAsymChain = "";
            List<string> interfaceChainList = new List<string> ();
            foreach (DataRow chainRow in interfaceChainTable.Rows)
            {
                interfaceAsymChain = chainRow["AsymChain1"].ToString().TrimEnd();
                if (!interfaceChainList.Contains(interfaceAsymChain))
                {
                    interfaceChainList.Add(interfaceAsymChain);
                }
                interfaceAsymChain = chainRow["AsymChain2"].ToString().TrimEnd();
                if (!interfaceChainList.Contains(interfaceAsymChain))
                {
                    interfaceChainList.Add(interfaceAsymChain);
                }
            }
            List<string> leftChainList = new List<string> ();
            foreach (string asymChain in asymChains)
            {
                if (! interfaceChainList.Contains(asymChain))
                {
                    leftChainList.Add(asymChain);
                }
            }
            return leftChainList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chains"></param>
        /// <returns></returns>
        private string FormatChainsToString(string[] chains)
        {
            string dataLine = "";
            foreach (string chain in chains)
            {
                dataLine += (chain + ",");
            }
            dataLine = dataLine.TrimEnd(',');
            return dataLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntrySequenceTable(string pdbId)
        {
            string queryString = string.Format("Select AsymID, Sequence, SequenceInCoord From AsymUnit " +
                " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable sequenceTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return sequenceTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChain"></param>
        /// <param name="sequenceTable"></param>
        /// <returns></returns>
        private int[] GetSequenceLengths(string asymChain, DataTable sequenceTable)
        {
            DataRow[] seqRows = sequenceTable.Select(string.Format ("AsymID = '{0}'", asymChain));
            int[] seqLengths = new int[2];
            string sequence = "";
            string sequenceInCoord = "";
            if (seqRows.Length > 0)
            {
                sequence = seqRows[0]["Sequence"].ToString().TrimEnd();
                sequenceInCoord = seqRows[0]["SequenceInCoord"].ToString().TrimEnd();
            }
            seqLengths[0] = sequence.Length;
            seqLengths[1] = GetNumOfCoords (sequenceInCoord);
            return seqLengths;
        }

        private int GetNumOfCoords(string sequenceInCoord)
        {
            int numOfCoords = 0;
            foreach (char ch in sequenceInCoord)
            {
                if (ch != '-')
                {
                    numOfCoords++;
                }
            }
            return numOfCoords;
        }
        #endregion
        // end of functions

        #region check multi-chain domain interfaces
        /// <summary>
        /// in the multi-chain domain interfaces, one domain interface may contain several rows
        /// </summary>
        public void RetrieveMultiChainDomainInterfaces()
        {
            string[] multiChainEntries = GetMultiChainEntries();
            UpdateDomainInterfaces(multiChainEntries);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetMultiChainEntries()
        {
             string queryString = "Select PdbID, DomainID, Count(Distinct EntityID) As EntityCount From PdbPfam Group By PdbID, DomainID;";
             DataTable entityCountTable = ProtCidSettings.pdbfamQuery.Query( queryString);
             List<string> multiChainEntryList = new List<string>();
            int entityCount = 0;
            string pdbId = "";
            foreach (DataRow entityCountRow in entityCountTable.Rows)
            {
                entityCount = Convert.ToInt32(entityCountRow["EntityCount"].ToString ());
                if (entityCount > 1)
                {
                    pdbId = entityCountRow["PdbID"].ToString ();
                    if (!multiChainEntryList.Contains(pdbId))
                    {
                        multiChainEntryList.Add(pdbId);
                    }
                }
            }
            return multiChainEntryList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceTable"></param>
        /// <param name="domainDefTable"></param>
        private void GetMultiChainDomainInterfaces(string pdbId, DataTable interfaceTable, DataTable domainDefTable, ref int dInterfaceId)
        {
            InterfaceChains[] chainInterfaces = null;
            if (interfaceTable.Rows.Count == 0) // no interface defined in the db
            {
                chainInterfaces = interfaceRetriever.FindUniqueInterfaces(pdbId, true);
                interfaceTable = GetEntryInterfaceTable(pdbId);
            }
            else
            {
                // get the chain interface from interface files if available 
                // or recovered from domain interface defintion and xml file
                chainInterfaces = domainInterfaceRetriever.GetCrystInterfaces(pdbId, "cryst");
            }
            if (chainInterfaces == null)
            {
                return;
            }

            GetMultiChainDomainInterfaces(pdbId, interfaceTable, domainDefTable, chainInterfaces, ref dInterfaceId);
        }

        /// <summary>
        /// check the domain interaction exist in domain interfaces or not
        /// 
        /// </summary>
        /// <param name="sgInterfaceTable"></param>
        /// <param name="contactsTable"></param>
        /// <param name="domainDefTable"></param>
        private void GetMultiChainDomainInterfaces(string pdbId, DataTable interfaceTable, DataTable domainDefTable, InterfaceChains[] chainInterfaces, ref int dInterfaceId)
        {
            int entityId1 = 0;
            int entityId2 = 0;
            int interfaceId = -1;
            Dictionary<long, Range[]> entityDomainHash1 = null;
            Dictionary<long, Range[]> entityDomainHash2 = null;
            Dictionary<long, string> entityDomainPfamHash1 = null;
            Dictionary<long, string> entityDomainPfamHash2 = null;
            int relSeqId = -1;
            string family1 = "";
            string family2 = "";
            char isReversed = '0';
            int maxSeq1 = -1;
            int minSeq1 = -1;
            int maxSeq2 = -1;
            int minSeq2 = -1;
            string asymChain1 = "";
            string asymChain2 = "";
            string symmetryString1 = "";
            string symmetryString2 = "";
            int chainDomainId1 = -1;
            int chainDomainId2 = -1;
            string domainEntityId1 = "";
            string domainEntityId2 = "";
            int[] numberOfDomainInteracted = new int[2];
            string domainPair = "";
            Dictionary<string, int[]> domainPairInteractedNumHash = new Dictionary<string,int[]> ();
            Dictionary<string, string> domainPairPfamIdHash = new Dictionary<string,string> ();
            Dictionary<string, int> domainPairInterfaceIdHash = new Dictionary<string,int>  ();
            List<string> domainPairWithChainReversedList = new List<string> ();

            DataTable domainChainTable = GetEntryChainPfamDomainTable(pdbId); 

            foreach (DataRow interfaceRow in interfaceTable.Rows)
            {
                interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                InterfaceChains chainInterface = domainInterfaceRetriever.GetChainInterface(chainInterfaces, interfaceId);
                if (chainInterface == null)
                {
                    return;
                }
                entityId1 = Convert.ToInt32(interfaceRow["EntityID1"].ToString());
                entityId2 = Convert.ToInt32(interfaceRow["EntityID2"].ToString());
                asymChain1 = interfaceRow["AsymChain1"].ToString().TrimEnd();
                asymChain2 = interfaceRow["AsymChain2"].ToString().TrimEnd();
                entityDomainHash1 = GetChainDomainHash (domainDefTable, domainChainTable, asymChain1, out entityDomainPfamHash1);
                if (asymChain1 == asymChain2)
                {
                    entityDomainHash2 = entityDomainHash1;
                    entityDomainPfamHash2 = entityDomainPfamHash1;
                }
                else
                {
                    entityDomainHash2 = GetChainDomainHash(domainDefTable, domainChainTable, asymChain2, out entityDomainPfamHash2);
                }

                // the maximum and minimum sequential numbers for both chains
                GetMaxMinContactInfo(chainInterface, ref maxSeq1, ref minSeq1, ref maxSeq2, ref minSeq2);

                foreach (long domainId1 in entityDomainHash1.Keys)
                {
                    Range[] domainRanges1 = entityDomainHash1[domainId1];
                    family1 = entityDomainPfamHash1[domainId1];
                    chainDomainId1 = GetChainDomainID(asymChain1, domainId1, domainChainTable);
                    symmetryString1 = interfaceRow["SymmetryString1"].ToString().TrimEnd();
                    domainEntityId1 = interfaceRow["EntityID1"].ToString();

                    if (!IsDomainOverlapInteraction(domainRanges1, maxSeq1, minSeq1))
                    {
                        continue;
                    }
                    foreach (long domainId2 in entityDomainHash2.Keys)
                    {
                        Range[] domainRanges2 = (Range[])entityDomainHash2[domainId2];
                        family2 = (string)entityDomainPfamHash2[domainId2];
                        chainDomainId2 = GetChainDomainID(asymChain2, domainId2, domainChainTable);
                        symmetryString2 = interfaceRow["SymmetryString2"].ToString().TrimEnd();
                        domainEntityId2 = interfaceRow["EntityID2"].ToString();

                        // they are same domain, no interaction
                        if (chainDomainId1 == chainDomainId2 && symmetryString1 == symmetryString2)
                        {
                            continue;
                        }

                        if (!IsDomainOverlapInteraction(domainRanges2, maxSeq2, minSeq2))
                        {
                            continue;
                        }

                        int[] numbersOfInteracted = GetDomainsInteractedNumbers(chainInterface, domainRanges1, domainRanges2);

                        if (chainDomainId1 > chainDomainId2)
                        {
                            domainPair = chainDomainId2.ToString() + "_" + asymChain2 + "_" + symmetryString2 + "_" + domainEntityId2 + ";" +
                                chainDomainId1.ToString() + "_" + asymChain1 + "_" + symmetryString1 + "_" + domainEntityId1 + ";";
                            domainPairWithChainReversedList.Add(domainPair); 
                        }
                        else
                        {
                            domainPair = chainDomainId1.ToString() + "_" + asymChain1 + "_" + symmetryString1 + "_" + domainEntityId1 + ";" +
                                chainDomainId2.ToString() + "_" + asymChain2 + "_" + symmetryString2 + "_" + domainEntityId2;
                        }
                        
                        domainPairInteractedNumHash.Add(domainPair, numbersOfInteracted);
                        domainPairPfamIdHash.Add(domainPair, family1 + ";" + family2);
                        domainPairInterfaceIdHash.Add (domainPair, interfaceId);
                    } // domain2
                } // domain1
            }

            List<string> domainPairList = new List<string>(domainPairInteractedNumHash.Keys);
            Dictionary<int, string[]> groupDomainPairHash = GroupDomainPairs(domainPairList.ToArray());
            List<int> groupIdList = new List<int> (groupDomainPairHash.Keys);
            groupIdList.Sort ();
            int[] totalInteractNumbers = new int[2];
            string familyGroup = "";
            foreach (int groupId in groupIdList)
            {
                string[] combinedDomainPairs = (string[])groupDomainPairHash[groupId];
                foreach (string comDomainPair in combinedDomainPairs)
                {
                    int[] numbersOfInteracted = (int[])domainPairInteractedNumHash[comDomainPair];
                    totalInteractNumbers[0] += numbersOfInteracted[0];
                    totalInteractNumbers[1] += numbersOfInteracted[1];
                }
               
                if (AreDomainInteraced(totalInteractNumbers))
                {
                    foreach (string comDomainPair in combinedDomainPairs)
                    {
                        familyGroup = (string)domainPairPfamIdHash[comDomainPair];
                        string[] familyFields = familyGroup.Split(';');
                        relSeqId = GetRelSeqId(familyFields[0], familyFields[1], out isReversed);
                        interfaceId = (int)domainPairInterfaceIdHash[comDomainPair];
                        string[] pairFields = comDomainPair.Split(';');
                        string[] domainFields1 = pairFields[0].Split('_');
                        string[] domainFields2 = pairFields[1].Split('_');

                        DataRow domainInterfaceRow =
                                DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces].NewRow();
                        domainInterfaceRow["RelSeqID"] = relSeqId; 
                        domainInterfaceRow["PdbID"] = pdbId;
                        domainInterfaceRow["InterfaceID"] = interfaceId;
                        domainInterfaceRow["DomainInterfaceID"] = dInterfaceId;
                        domainInterfaceRow["SurfaceArea"] = -1;
                        if (domainPairWithChainReversedList.Contains(comDomainPair))
                        {
                            // change it back
                            domainInterfaceRow["DomainID1"] = GetDomainPairEntityDomainID(domainFields2[1], domainFields2[0], domainChainTable);
                            domainInterfaceRow["DomainID2"] = GetDomainPairEntityDomainID(domainFields1[1], domainFields1[0], domainChainTable);
                            domainInterfaceRow["AsymChain1"] = domainFields2[1];
                            domainInterfaceRow["AsymChain2"] = domainFields1[1];
                            // same pfam, sort by chain domain id
                            if (isReversed == '0' &&  familyFields[0] == familyFields[1])
                            {
                                isReversed = '1';
                            }
                            domainInterfaceRow["IsReversed"] = isReversed;
                        }
                        else
                        {
                            domainInterfaceRow["DomainID1"] = GetDomainPairEntityDomainID(domainFields1[1], domainFields1[0], domainChainTable);
                            domainInterfaceRow["DomainID2"] = GetDomainPairEntityDomainID(domainFields2[1], domainFields2[0], domainChainTable);
                            domainInterfaceRow["AsymChain1"] = domainFields1[1];
                            domainInterfaceRow["AsymChain2"] = domainFields2[1];
                            domainInterfaceRow["IsReversed"] = isReversed;
                        }
                        DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces].Rows.Add(domainInterfaceRow);
                    }
                    dInterfaceId++;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChain"></param>
        /// <param name="chainDomainId"></param>
        /// <param name="chainPfamTable"></param>
        /// <returns></returns>
        private long GetDomainPairEntityDomainID(string asymChain, string chainDomainId, DataTable chainPfamTable)
        {
            long entityDomainId = 0;

            DataRow[] domainIdRows = chainPfamTable.Select(string.Format ("AsymChain = '{0}' AND ChainDomainID = '{1}'", asymChain, chainDomainId));
            if (domainIdRows.Length > 0)
            {
                entityDomainId = Convert.ToInt64(domainIdRows[0]["DomainID"].ToString ());
            }
            return entityDomainId;
        }
        /// <summary>
        /// /
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryChainPfamDomainTable(string pdbId)
        {
            string queryString = string.Format("Select * From PdbPfamChain Where PdbID = '{0}';", pdbId);
            DataTable chainPfamTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return chainPfamTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChain"></param>
        /// <param name="domainId"></param>
        /// <param name="chainPfamTable"></param>
        /// <returns></returns>
        private int GetChainDomainID(string asymChain, long domainId, DataTable chainPfamTable)
        {
            int chainDomainId = -1;
            DataRow[] chainDomainIdRows = chainPfamTable.Select(string.Format ("DomainID = '{0}' AND AsymChain = '{1}'", domainId, asymChain));
            if (chainDomainIdRows.Length > 0)
            {
                chainDomainId = Convert.ToInt32(chainDomainIdRows[0]["ChainDomainID"].ToString ());
            }
            return chainDomainId;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="numberOfDomainInteracted"></param>
        /// <returns></returns>
        private bool AreDomainInteraced(int[] numberOfDomainInteracted)
        {
            if (numberOfDomainInteracted[0] >= AppSettings.parameters.contactParams.numOfResidueContacts &&
                    numberOfDomainInteracted[1] >= 1)
            {
                return true;
            }
            if (numberOfDomainInteracted[1] >= AppSettings.parameters.contactParams.domainNumOfAtomContacts)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainPairs"></param>
        /// <returns></returns>
        private Dictionary<int, string[]> GroupDomainPairs(string[] domainPairs)
        {
            Dictionary<int, string[]> groupDomainPairHash = new Dictionary<int,string[]> ();
            List<string> leftDomainPairList = new List<string> (domainPairs);
            List<string> combinedDomainPairList = new List<string> ();
            int groupId = 1;
            for (int i = 0; i < domainPairs.Length; i++)
            {
                if (!leftDomainPairList.Contains(domainPairs[i]))
                {
                    continue;
                }
                string[] domainFields1 = domainPairs[i].Split(';');
                combinedDomainPairList.Clear();
                for (int j = i + 1; j < domainPairs.Length; j++)
                {
                    if (!leftDomainPairList.Contains(domainPairs[j]))
                    {
                        continue;
                    }
                    string[] domainFields2 = domainPairs[j].Split(';');
                    if (AreSameDomainInteraction(domainFields1[0], domainFields1[1], domainFields2[0], domainFields2[1]))
                    {
                        // if the chain of the domain is already added, it cannot be combined
                        // modified on Nov. 26, 2012
                        if (! IsSameEntityPairExist(combinedDomainPairList, domainPairs[j]))
                        {
                            combinedDomainPairList.Add(domainPairs[j]);
                            leftDomainPairList.Remove(domainPairs[j]);
                        }
                    }
                }

                combinedDomainPairList.Insert(0, domainPairs[i]);
                string[] combinedDomainPairs = new string[combinedDomainPairList.Count];
                combinedDomainPairList.CopyTo(combinedDomainPairs);
                groupDomainPairHash.Add(groupId, combinedDomainPairs);
                groupId++;
            }
            return groupDomainPairHash;
        }


        /// <summary>
        ///  the last field is the entityId
        ///  the format of domainPair: chain domainId _ asym chain id _ symmetry string (e.g. 1_555) _ entity id
        /// </summary>
        /// <param name="domainPairList"></param>
        /// <param name="domainPair"></param>
        /// <returns></returns>
        private bool IsSameEntityPairExist(List<string> domainPairList, string domainPair)
        {
            string entityPair = GetEntityPairForDomainPair(domainPair);
            string lsEntityPair = "";
            foreach (string lsDomainPair in domainPairList)
            {
                lsEntityPair = GetEntityPairForDomainPair(lsDomainPair);
                if (entityPair == lsEntityPair)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainPair"></param>
        /// <returns></returns>
        private string GetEntityPairForDomainPair(string domainPair)
        {
            string[] domainPairFields = domainPair.Split(';');
            string[] domainFields1 = domainPairFields[0].Split('_');
            string[] domainFields2 = domainPairFields[1].Split('_');

            string entryPair = domainFields1[domainFields1.Length - 1] + "_" + domainFields2[domainFields2.Length - 1];

            return entryPair;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="interactDomain11"></param>
        /// <param name="interactDomain12"></param>
        /// <param name="interactDomain21"></param>
        /// <param name="interactDomain22"></param>
        /// <returns></returns>
        private bool AreSameDomainInteraction(string interactDomain11, string interactDomain12, string interactDomain21, string interactDomain22)
        {
            if (AreDomainPairMultiChainDomain(interactDomain11, interactDomain21)) // multi-Chain domain
            {
                if (AreDomainPairMultiChainDomain(interactDomain12, interactDomain22)) // multi-chain domain
                {
                    return true;
                }
                else if (interactDomain12 == interactDomain22) // same chain and domain
                {
                    return true;
                }
            }
            else if (interactDomain11 == interactDomain21)
            {
                if (AreDomainPairMultiChainDomain(interactDomain12, interactDomain22))
                {
                    return true;
                }
            }
            else if (AreDomainPairMultiChainDomain(interactDomain11, interactDomain22))
            {
                if (AreDomainPairMultiChainDomain(interactDomain12, interactDomain21))
                {
                    return true;
                }
                else if (interactDomain12 == interactDomain21)
                {
                    return true;
                }
            }
            else if (interactDomain11 == interactDomain22)
            {
                if (AreDomainPairMultiChainDomain(interactDomain12, interactDomain21))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// same chaindomainId + same symmetry operator + different chain Ids: multi-chain domain
        /// </summary>
        /// <param name="interactDomain1"></param>
        /// <param name="interactDomain2"></param>
        /// <returns></returns>
        private bool AreDomainPairMultiChainDomain (string interactDomain1, string interactDomain2)
        {
            string[] pairFields1 = interactDomain1.Split('_');
            string[] pairFields2 = interactDomain2.Split('_');
            if (pairFields1[0] == pairFields2[0] && pairFields1[2] == pairFields2[2] && 
                pairFields1[3] == pairFields2[3] && pairFields1[1] != pairFields2[1])
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool DoesEntryContainMultiChainDomains(string pdbId)
        {
            string queryString = string.Format("Select DomainID, Count(Distinct EntityID) As EntityCount " +
                " From PdbPfam Where PdbID = '{0}' Group By DomainID;", pdbId);
            DataTable domainEntityTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            bool isMultiChain = false;
            int entityCount = 0;
            foreach (DataRow domainEntityRow in domainEntityTable.Rows)
            {
                entityCount = Convert.ToInt32(domainEntityRow["EntityCount"].ToString());
                if (entityCount > 1)
                {
                    isMultiChain = true;
                    break;
                }
            }
            return isMultiChain;
        }
        #endregion

        #region for missing domain-domain interactions
        /// <summary>
        /// 
        /// </summary>
        public void GetMissingDomainInterfaces(Dictionary<string, int[]> entryInterfaceHash)
        {
            GroupDbTableNames.SetGroupDbTableNames(ProtCidSettings.dataType);
            CrystInterfaceTables.InitializeTables();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = entryInterfaceHash.Count;
            ProtCidSettings.progressInfo.totalStepNum = entryInterfaceHash.Count;


            foreach (string pdbId in entryInterfaceHash.Keys)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    int[] interfaceIds = entryInterfaceHash[pdbId];
                    if (interfaceIds.Length > 100)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " too many interfaces. skip");
                        ProtCidSettings.logWriter.WriteLine(pdbId + " too many interfaces. skip");
                        ProtCidSettings.logWriter.Flush();
                        continue;
                    }
                    GetDomainInterfacesFromCrystInterfaces(pdbId, interfaceIds);

                    // insert data into db
                    dbInsert.InsertDataIntoDBtables (ProtCidSettings.protcidDbConnection, 
                        DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces]);
                    DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces].Clear();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + ": retrieve domain interfaces error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceIds"></param>
        private void DeleteDomainInterfaces(string pdbId, int[] interfaceIds)
        {
            string deleteString = string.Format("Delete From PfamDomainInterfaces WHere PdbID = '{0}' AND InterfaceID IN ({1});",
                pdbId, ParseHelper.FormatSqlListString (interfaceIds));
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }
        /// <summary>
        /// find domain DomainIDs for the interfaces
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="pdbId"></param>
        public void GetDomainInterfacesFromCrystInterfaces(string pdbId, int[] interfaceIds)
        {
            DataTable interfaceTable = GetEntryInterfaceTable(pdbId, interfaceIds);

            DeleteDomainInterfaces(pdbId, interfaceIds);
            // 
            // domain definition
            DataTable domainDefTable = GetDomainDefTable(pdbId);

            int dInterfaceId = GetTheMaxInterfaceId (pdbId);

            if (DoesEntryContainMultiChainDomains(pdbId))
            {
                GetMultiChainDomainInterfaces(pdbId, interfaceTable, domainDefTable, ref dInterfaceId);
            }
            else
            {
                GetDomainInterfaces(pdbId, interfaceTable, domainDefTable, ref dInterfaceId);
            }
    //        GetMultiDomainInterfaces(pdbId, domainDefTable, ref dInterfaceId);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int GetTheMaxInterfaceId(string pdbId)
        {
            string queryString = string.Format("Select Max(DomainInterfaceID) As MaxInterfaceID From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
            DataTable maxInterfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int nextInterfaceId = 1;
            if (maxInterfaceIdTable.Rows[0]["MaxInterfaceID"].ToString() == "")
            {
                nextInterfaceId = 1;
            }
            else
            {
                nextInterfaceId = Convert.ToInt32(maxInterfaceIdTable.Rows[0]["MaxInterfaceID"].ToString()) + 1;
            }
            return nextInterfaceId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryInterfaceTable(string pdbId, int[] interfaceIds)
        {
            DataTable interfaceTable = null;
            string queryString = "";
            if (interfaceIds.Length < 100)
            {
                // interface definition
                queryString = string.Format("Select Distinct * From CrystEntryInterfaces " +
                    " Where PdbID = '{0}' AND InterfaceID IN ({1});", pdbId, ParseHelper.FormatSqlListString(interfaceIds));
                interfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            }
            else
            {
                foreach (int interfaceId in interfaceIds)
                {
                    // interface definition
                    queryString = string.Format("Select Distinct * From CrystEntryInterfaces " +
                        " Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
                    DataTable oneInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
                    if (interfaceTable == null)
                    {
                        interfaceTable = oneInterfaceTable.Copy();
                    }
                    else
                    {
                        DataRow newInterfaceRow = interfaceTable.NewRow();
                        newInterfaceRow.ItemArray = oneInterfaceTable.Rows[0].ItemArray;
                        interfaceTable.Rows.Add(newInterfaceRow);
                    }
                }
            }
            return interfaceTable;
        }

        public void GetEntriesWithMissingDomainInteractions()
        {
            StreamWriter entryInterfaceWriter = new StreamWriter("MissingEntryInterfaces.txt");
            string queryString = "Select Distinct PdbID From SgInterfaceResidues;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            List<string> missingEntryList = new List<string>();
            string dataLine = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                int[] leftInterfaceIds = GetMissingInterfaces(pdbId);
                if (leftInterfaceIds.Length > 0)
                {
                    dataLine = pdbId + ":";
                    foreach (int interfaceId in leftInterfaceIds)
                    {
                        dataLine += (interfaceId.ToString() + ",");
                    }
                    entryInterfaceWriter.WriteLine(dataLine.TrimEnd (','));
                    entryInterfaceWriter.Flush();
                }
            }
            entryInterfaceWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetMissingInterfaces(string pdbId)
        {
            string queryString = string.Format("Select Distinct InterfaceID From SgInterfaceResidues Where PdbID = '{0}';", pdbId);
            DataTable sgInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            queryString = string.Format("Select InterfaceID From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable crystInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            List<int> leftInterfaceList = new List<int> ();
            int interfaceId = 0;
            foreach (DataRow crystInterfaceRow in crystInterfaceTable.Rows)
            {
                interfaceId = Convert.ToInt32(crystInterfaceRow["InterfaceID"].ToString ());
                DataRow[] sgInterfaceRows = sgInterfaceTable.Select("InterfaceID = " + interfaceId);
                if (sgInterfaceRows.Length == 0)
                {
                    leftInterfaceList.Add(interfaceId);
                }
            }
            return leftInterfaceList.ToArray ();
        }
        #endregion

        #region add chain domain id to domain interfaces
        public void AddChainDomainIdToInterfaces()
        {
            string queryString = "Select Distinct PdbID From PfamDomainInterfaces;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Add chain domain id";
            ProtCidSettings.progressInfo.totalOperationNum = entryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = entryTable.Rows.Count;

            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                try
                {
                    AddEntryChainDomainIDs(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " Add chain domain id errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " Add chain domain id errors: " + ex.Message);
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateChainDomainIdToInterfaces(string[] updateEntries)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Add chain domain id";
            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;

            foreach (string pdbId in updateEntries) 
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                try
                {
                    AddEntryChainDomainIDs(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " Add chain domain id errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " Add chain domain id errors: " + ex.Message);
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateMissingChainDomainIdToInterfaces()
        {
            string queryString = "Select Distinct PdbID From PfamDomainInterfaces Where ChainDomainID1 = 0 or ChainDomainId2 = 0;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Add chain domain id";
            ProtCidSettings.progressInfo.totalOperationNum = entryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = entryTable.Rows.Count;
            string pdbId = "";

            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString ();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                try
                {
                    AddEntryChainDomainIDs(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " Add chain domain id errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " Add chain domain id errors: " + ex.Message);
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        public void AddEntryChainDomainIDs(string pdbId)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces WHere PdbID = '{0}';", pdbId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            queryString = string.Format("Select * From PdbPfamChain Where PdbID = '{0}';", pdbId);
            DataTable domainChainTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            string[] domainAsymChains = GetDomainAsymChains(domainInterfaceTable);
            string asymChain = "";
            long domainId = 0;
            int chainDomainId = 0;
            foreach (string domainAsymChain in domainAsymChains)
            {
                string[] fields = domainAsymChain.Split('_');
                domainId = Convert.ToInt64(fields[0]);
                asymChain = fields[1];
                chainDomainId = GetChainDomainID(pdbId, domainId, asymChain, domainChainTable);
                UpdateChainDomainID(pdbId, domainId, asymChain, chainDomainId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private string[] GetDomainAsymChains(DataTable domainInterfaceTable)
        {
            List<string> domainAsymChainList = new List<string> ();
            string domainAsymChain = "";
            foreach (DataRow interfaceRow in domainInterfaceTable.Rows)
            {
                domainAsymChain = interfaceRow["DomainID1"].ToString() + "_" +
                    interfaceRow["AsymChain1"].ToString().TrimEnd();
                if (!domainAsymChainList.Contains(domainAsymChain))
                {
                    domainAsymChainList.Add(domainAsymChain);
                }
                domainAsymChain = interfaceRow["DomainID2"].ToString() + "_" +
                   interfaceRow["AsymChain2"].ToString().TrimEnd();
                if (!domainAsymChainList.Contains(domainAsymChain))
                {
                    domainAsymChainList.Add(domainAsymChain);
                }    
            }
            return domainAsymChainList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <param name="asymChain"></param>
        /// <param name="chainDomainTable"></param>
        /// <returns></returns>
        private int GetChainDomainID(string pdbId, long domainId, string asymChain, DataTable chainDomainTable)
        {
            DataRow[] chainDomainRows = chainDomainTable.Select(string.Format ("PdbID = '{0}' AND DomainID = '{1}' AND AsymChain = '{2}'",
                pdbId, domainId, asymChain));
            int chainDomainId = 0;
            if (chainDomainRows.Length > 0)
            {
                chainDomainId = Convert.ToInt32 (chainDomainRows[0]["ChainDomainID"].ToString ());
            }
            return chainDomainId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <param name="asymChain"></param>
        /// <param name="chainDomainId"></param>
        private void UpdateChainDomainID(string pdbId, long domainId, string asymChain, int chainDomainId)
        {
            string updateString = string.Format("Update PfamDomainInterfaces Set ChainDomainID1 = {0} " +
                " Where PdbID = '{1}' AND DomainID1 = {2} AND AsymChain1 = '{3}';", chainDomainId, pdbId, domainId, asymChain);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);

            updateString = string.Format("Update PfamDomainInterfaces Set ChainDomainID2 = {0} " +
                " Where PdbID = '{1}' AND DomainID2 = {2} AND AsymChain2 = '{3}';", chainDomainId, pdbId, domainId, asymChain);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }
        #endregion

        #region intialize 
        private void InitializeTableInDb()
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Creating database tables.");
            DomainInterfaceTables.InitializeDbTables();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done.");
        }
        #endregion

        #region for debug
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        public Dictionary<int, string[]> GetUpdateRelationEntryHash(string[] updateEntries)
        {
            Dictionary<int, List<string>> updateRelationEntryListHash = new Dictionary<int,List<string>> ();
            foreach (string pdbId in updateEntries)
            {
                int[] entryRelSeqId = GetEntryRelSeqId(pdbId);
                foreach (int relSeqId in entryRelSeqId)
                {
                    if (updateRelationEntryListHash.ContainsKey(relSeqId))
                    {
                        if (!updateRelationEntryListHash[relSeqId].Contains(pdbId))
                        {
                            updateRelationEntryListHash[relSeqId].Add(pdbId);
                        }
                    }
                    else
                    {
                        List<string> entryList = new List<string> ();
                        entryList.Add(pdbId);
                        updateRelationEntryListHash.Add(relSeqId, entryList);
                    }
                }
            }
            Dictionary<int, string[]> updateRelationEntryHash = new Dictionary<int,string[]> ();
            foreach (int lsRelSeqId in updateRelationEntryListHash.Keys)
            {
                updateRelationEntryHash.Add(lsRelSeqId, updateRelationEntryListHash[lsRelSeqId].ToArray ());
            }
            return updateRelationEntryHash;
        }
        /// <summary>
        /// 
        /// </summary>
        public void OutputChangedRelSeqIds()
        {
            string queryString = "Select Distinct RelSeqID, PdbID, NewRelSeqID From PfamDomainInterfaces Where RelSeqID <> NewRelSeqID;";
            DataTable difRelSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            Dictionary<int, List<string>> updateRelEntriesHash = new Dictionary<int,List<string>> ();
            int newRelSeqId = 0;
            string pdbId = "";
            
            foreach (DataRow difRelSeqIdRow in difRelSeqIdTable.Rows)
            {
                newRelSeqId = Convert.ToInt32(difRelSeqIdRow["NewRelSeqID"].ToString ());
                pdbId = difRelSeqIdRow["PdbID"].ToString();
                if (updateRelEntriesHash.ContainsKey(newRelSeqId))
                {
                    if (!updateRelEntriesHash[newRelSeqId].Contains(pdbId))
                    {
                        updateRelEntriesHash[newRelSeqId].Add(pdbId);
                    }
                }
                else
                {
                    List<string> entryList = new List<string> ();
                    entryList.Add(pdbId);
                    updateRelEntriesHash.Add(newRelSeqId, entryList);
                }
            }

            StreamWriter difRelSeqIdsWriter = new StreamWriter("DifRelSeqIdEntries.txt");
            string dataLine = "";
            foreach (int relSeqId in updateRelEntriesHash.Keys)
            {
                dataLine = relSeqId.ToString();
                foreach (string lsPdbId in updateRelEntriesHash[relSeqId])
                {
                    dataLine += (" " + lsPdbId);
                }
                difRelSeqIdsWriter.WriteLine(dataLine);
            }
            difRelSeqIdsWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateInterfaceCompRelSeqIds()
        {
            string queryString = "Select Distinct RelSeqID, PdbID, NewRelSeqID From PfamDomainInterfaces Where RelSeqID <> NewRelSeqID;";
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            int relSeqId = 0;
            int newRelSeqId = 0;
            foreach (DataRow domainInterfaceRow in domainInterfaceTable.Rows)
            {
                pdbId = domainInterfaceRow["PdbID"].ToString();
                relSeqId = Convert.ToInt32(domainInterfaceRow["RelSeqID"].ToString ());
                newRelSeqId = Convert.ToInt32(domainInterfaceRow["NewRelSeqID"].ToString ());
                UpdateInterfaceCompTables(relSeqId, pdbId, newRelSeqId);
                DeleteInterfaceCompTable(relSeqId, pdbId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="newRelSeqId"></param>
        private void UpdateInterfaceCompTables(int relSeqId, string pdbId, int newRelSeqId)
        {
            string updateString = string.Format("Update PfamEntryDomainInterfaceComp Set RelSeqId = {0} " +
                " Where RelSeqID = {1} AND PdbID = '{2}'", newRelSeqId, relSeqId, pdbId);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        private void DeleteInterfaceCompTable(string pdbId)
        {
            string deleteString = string.Format("Delete From PfamDomainInterfaceComp Where PdbID1 = '{0}' OR PdbID2 = '{0}';", pdbId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        private void DeleteInterfaceCompTable(int relSeqId, string pdbId)
        {
            string deleteString = string.Format("Delete From PfamDomainInterfaceComp  " +
                " Where RelSeqID = {0} AND (PdbID1 = '{1}' OR PdbID2 = '{1}');", relSeqId, pdbId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        public void CheckTheRelSeqIDsForDomainInterfaces()
        {
            StreamReader dataReader = new StreamReader("difRelSeqIds.sql");
            string line = "";
            string pdbId = "";
            int interfaceId = 0;
            int domainInterfaceId = 0;
            int relSeqId = 0;
            int newRelSeqId = 0;
            string updateString = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("INSERT") > -1)
                {
                    line = dataReader.ReadLine().Trim ();
                    line = line.Replace("VALUES", "");
                    line = line.TrimStart();
                    line = line.Replace("(", "");
                    line = line.Replace(");", "");
                    string[] fields = ParseHelper.SplitPlus(line, ' ');
                    relSeqId = Convert.ToInt32(fields[0].TrimEnd (','));
                    pdbId = fields[1].TrimEnd(',');
                    pdbId = pdbId.Trim('\'');
                    interfaceId = Convert.ToInt32(fields[2].TrimEnd (','));
                    domainInterfaceId = Convert.ToInt32(fields[3].TrimEnd (','));
                    newRelSeqId = Convert.ToInt32(fields[12]);
                    updateString = string.Format("Update PfamDomainInterfaces Set NewRelSeqID = {0} " +
                        " WHere RelSeqID = {1} AND PdbID = '{2}' AND InterfaceID = {3} AND DomainInterfaceID = {4};",
                        newRelSeqId, relSeqId, pdbId, interfaceId, domainInterfaceId);
                    dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
                }
            }
            dataReader.Close();
     /*       StreamWriter inconDomainRelationWriter = new StreamWriter("DomainInterfaceRelationInCon.txt");
            string queryString = "Select Distinct PdbID From PfamDomainInterfaces Where NewRelSeqID = -1;";
            DataTable entryTable = dbQuery.Query(queryString);
            string pdbId = "";
            Hashtable pfamPairRelSeqIdHash = new Hashtable();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = entryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalOperationNum = entryTable.Rows.Count;

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Checking RelSeqIDs!");

            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();

                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                SetRelSeqID(pdbId, ref pfamPairRelSeqIdHash);
            }
      //      inconDomainRelationWriter.Close();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");*/
        }

        public void InputDomainInterfacesWithMissingDomainIDs()
        {
            StreamReader dataReader = new StreamReader("DomainInterfacesWithDomainIds.txt");
            string line = "";
            string pdbId = "";
            int domainInterfaceId = 0;
            int interfaceId = 0;
        //    long domainId2 = 0;
        //    int chainDomainId2 = 0;
            string deleteString = "";
            string queryString = "Select First 1 * From PfamDomainInterfaces;";
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            domainInterfaceTable.TableName = "PfamDomainInterfaces";
            domainInterfaceTable.Clear();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                pdbId = fields[1];
                interfaceId = Convert.ToInt32(fields[2]);
                domainInterfaceId = Convert.ToInt32(fields[3]);
           //    domainId2 = Convert.ToInt64(fields[6]);
           //     chainDomainId2 = Convert.ToInt32(fields[11]);
                deleteString = string.Format("Delete From PfamDomainInterfaces Where PdbID = '{0}' AND InterfaceID = {1} AND DomainInterfaceID = {2};", 
                    pdbId, interfaceId, domainInterfaceId);
                dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
                DataRow newRow = domainInterfaceTable.NewRow();
                string[] items = new string[fields.Length + 1];
                Array.Copy(fields, 0, items, 0, fields.Length);
                items[fields.Length] = "-1";
                newRow.ItemArray = items;
                domainInterfaceTable.Rows.Add(newRow);

           /*     updateString = string.Format("Update PfamDomainInterfaces Set DomainID2 = {0}, ChainDomainID2 = {1} " +
                    " WHere PdbID = '{2}' AND InterfaceID = {3} AND DomainInterfaceID = {4};", 
                    domainId2, chainDomainId2, pdbId, interfaceId, domainInterfaceId);
                dbUpdate.Update(updateString);*/
            }
            dataReader.Close();

            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, domainInterfaceTable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pfamPairRelSeqIdHash"></param>
        private void SetRelSeqID(string pdbId, ref Dictionary<string, int> pfamPairRelSeqIdHash)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces Where PdbID = '{0}' AND NewRelSeqID = -1;", pdbId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            queryString = string.Format("Select * From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable pfamDomainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            List<string> domainPairList = new List<string> ();
            long domainId1 = 0;
            long domainId2 = 0;
            Dictionary<long, string> domainPfamIdHash = new Dictionary<long,string> ();
            string pfamId1 = "";
            string pfamId2 = "";
            int relSeqId = 0;
            foreach (DataRow domainInterfaceRow in domainInterfaceTable.Rows)
            {
                if (domainInterfaceRow["NewRelSeqID"].ToString() == "-1")
                {
                    domainId1 = Convert.ToInt64(domainInterfaceRow["DomainID1"].ToString());
                    pfamId1 = GetPfamId(domainId1, pfamDomainTable, domainPfamIdHash);

                    domainId2 = Convert.ToInt64(domainInterfaceRow["DomainID2"].ToString());
                    pfamId2 = GetPfamId(domainId2, pfamDomainTable, domainPfamIdHash);

                    relSeqId = GetRelSeqId(pfamId1, pfamId2, ref pfamPairRelSeqIdHash);

                    UpdateRelSeqId(domainId1, domainId2, relSeqId);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId1"></param>
        /// <param name="pfamId2"></param>
        /// <param name="pfamPairRelSeqIdHash"></param>
        /// <returns></returns>
        private int GetRelSeqId(string pfamId1, string pfamId2, ref Dictionary<string, int> pfamPairRelSeqIdHash)
        {
            string pfamPair = pfamId1 + ";" + pfamId2;
            if (string.Compare(pfamId1, pfamId2) > 0)
            {
                pfamPair = pfamId2 + ";" + pfamId1;
            }
            int relSeqId = 0;
            if (pfamPairRelSeqIdHash.ContainsKey(pfamPair))
            {
                relSeqId = pfamPairRelSeqIdHash[pfamPair];
            }
            else
            {
                relSeqId = GetRelSeqId(pfamId1, pfamId2);
                pfamPairRelSeqIdHash.Add(pfamPair, relSeqId);
            }
            return relSeqId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainId1"></param>
        /// <param name="domainId2"></param>
        /// <param name="newRelSeqId"></param>
        private void UpdateRelSeqId(long domainId1, long domainId2, int newRelSeqId)
        {
            string updateString = string.Format("Update PfamDomainInterfaces Set NewRelSeqID = {0} " +
                " Where (DomainID1 = {1} AND DomainID2 = {2}) OR (DomainID1 = {2} AND DomainID2 = {1});", newRelSeqId, domainId1, domainId2);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId1"></param>
        /// <param name="pfamId2"></param>
        /// <returns></returns>
        private int GetRelSeqId (string pfamId1, string pfamId2)
        {
            string queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation " + 
                " Where(FamilyCode1 = '{0}' AND FamilyCode2 = '{1}') OR " + 
                " (FamilyCode1 = '{1}' AND FamilyCode2 = '{0}');", pfamId1, pfamId2);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = -1;
            if (relSeqIdTable.Rows.Count > 0)
            {
                relSeqId = Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString ());
            }
            return relSeqId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainId"></param>
        /// <param name="pfamDomainTable"></param>
        /// <param name="domainPfamIdHash"></param>
        /// <returns></returns>
        private string GetPfamId(long domainId, DataTable pfamDomainTable, Dictionary<long, string> domainPfamIdHash)
        {
            string pfamId = "";
            if (domainPfamIdHash.ContainsKey(domainId))
            {
                pfamId = domainPfamIdHash[domainId];
            }
            else
            {
                DataRow[] domainRows = pfamDomainTable.Select(string.Format("DomainID = '{0}'", domainId));
                if (domainRows.Length > 0)
                {
                    pfamId = domainRows[0]["Pfam_ID"].ToString().TrimEnd();
                }
                domainPfamIdHash.Add(domainId, pfamId);
            }
            return pfamId;
        }

        public void InsertPfamDomainInterfacesFromLog()
        {
            StreamReader dataReader = new StreamReader("pfamDomainInterfacesLog.txt");
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("INSERT INTO ") > -1)
                {
                    ProtCidSettings.protcidQuery.Query( line);
                }
            }
            dataReader.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        public void WriteUpdateRelEntries()
        {
            StreamReader dataReader = new StreamReader("AddedDomainInterfacesInDb.txt");
            string line = "";
            Dictionary<string, List<string>> relEntryHash = new Dictionary<string, List<string>>();
            List<string> updateEntryList = new List<string> ();
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                if (relEntryHash.ContainsKey(fields[0]))
                {
                    if (!relEntryHash[fields[0]].Contains (fields[1]))
                    {
                        relEntryHash[fields[0]].Add(fields[1]);
                    }
                }
                else
                {
                    List<string> entryList = new List<string> ();
                    entryList.Add(fields[1]);
                    relEntryHash.Add(fields[0], entryList);
                }

                if (!updateEntryList.Contains(fields[1]))
                {
                    updateEntryList.Add(fields[1]);
                }
            }
            dataReader.Close();

            StreamWriter dataWriter = new StreamWriter("UpdateRelationEntries.txt");
            string dataLine = "";
            foreach (string relSeqId in relEntryHash.Keys)
            {
                dataLine = relSeqId;
                foreach (string entry in relEntryHash[relSeqId])
                {
                    dataLine += (" " + entry);
                }
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Close();

            dataWriter = new StreamWriter("newls-pdb.txt");
            foreach (string entry in updateEntryList)
            {
                dataWriter.WriteLine(entry);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// Find the domain-domain interfaces for each entry with crystal interfaces
        /// </summary>
        public void RetrieveDomainInterfacesForDebug ()
        {
            GroupDbTableNames.SetGroupDbTableNames(ProtCidSettings.dataType);
            CrystInterfaceTables.InitializeTables();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Domain-domain interactions";
            StreamWriter wrongDataWriter = new StreamWriter("WrongDomainInterfaces.txt", true);
            StreamWriter addedDataWriter = new StreamWriter("AddedDomainInterfaces.txt", true);
            StreamWriter domainInterfaceWriter = new StreamWriter("NewDomainInterfaces.txt", true);

            StreamWriter listFileWriter = new StreamWriter("WrongDInterfaceEntries.txt");
            string queryString = "Select Distinct PdbID From PfamDomainInterfaces Where SurfaceArea <= 0 OR ChainDomainID1 <= 0 OR ChainDomainID2 <= 0;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] updateEntries = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                updateEntries[count] = entryRow["PdbID"].ToString();
                listFileWriter.WriteLine(entryRow["PdbID"].ToString());
                count++;
            }
            listFileWriter.Close();

            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;
            foreach (string pdbId in updateEntries)
            {
                // progress info
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                try
                {
                    GetDomainInterfacesFromCrystInterfaces(pdbId);
                    domainInterfaceWriter.WriteLine(ParseHelper.FormatDataRows
                        (DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces].Select ()));
                    domainInterfaceWriter.Flush();

                    CompareEntryDomainInterfaces(pdbId, DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces], wrongDataWriter, addedDataWriter);

                    DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces].Clear();
                    // insert data into db
               /*     dbInsert.InsertDataIntoDBtables
                        (DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces]);
                    DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces].Clear();*/

                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue
                        (pdbId + " retrieve domain interactions error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " retrieve domain interactions error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }// group
            domainInterfaceWriter.Close();
            wrongDataWriter.Close();
            addedDataWriter.Close();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Add Chain Domain IDs.");
            UpdateChainDomainIdToInterfaces(updateEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Updating Clan Seq IDs.");
            UpdateClanSeqIds();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update CfGroups");
            UpdateRelationCfGroupIDs(updateEntries);
 
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        public void AddNewDomainInterfacesForDebug()
        {
            string line = "";
            int relSeqId = 0;
            string pdbId = "";
            int domainInterfaceId = 0;
        /*    int interfaceId = 0;
            string domainInterfaceFileDir = @"D:\DbProjectData\InterfaceFiles_update\pfamDomain";
            string domainInterfaceFile = "";
           StreamReader deleteDataReader = new StreamReader("WrongDomainInterfaces.txt");
            while ((line = deleteDataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                relSeqId = Convert.ToInt32(fields[0]);
                pdbId = fields[1];
                interfaceId = Convert.ToInt32(fields[2]);
                domainInterfaceId = Convert.ToInt32(fields[3]);

                domainInterfaceFile = Path.Combine(domainInterfaceFileDir,
                    pdbId.Substring(1, 2) + "\\" + pdbId + "_d" + domainInterfaceId.ToString() + ".cryst.gz");

                DeletePfamDomainInterfaceRow(relSeqId, pdbId, interfaceId, domainInterfaceId);

                File.Delete(domainInterfaceFile);
            }
            deleteDataReader.Close();
            */

            string querystring = "Select First 1 * From PfamDOmainInterfaces;";
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( querystring);
            domainInterfaceTable.Clear();
            domainInterfaceTable.TableName = "PfamDomainInterfaces";
            StreamReader newDataReader = new StreamReader("AddedDomainInterfaces.txt");
            
            List<string> relationEntryList = new List<string> ();
            while ((line = newDataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                DataRow interfaceRow = domainInterfaceTable.NewRow();
                interfaceRow["RelSeqID"] = fields[0];
                interfaceRow["PdbID"] = fields[1];
                interfaceRow["InterfaceID"] = fields[2];
                interfaceRow["DomainInterfaceID"] = fields[3];
                interfaceRow["DomainID1"] = fields[4];
                interfaceRow["DomainID2"] = fields[5];
                interfaceRow["AsymChain1"] = fields[6];
                interfaceRow["AsymChain2"] = fields[7];
                interfaceRow["IsReversed"] = fields[8];
                interfaceRow["SurfaceArea_old"] = -1;
                interfaceRow["SurfaceArea"] = -1;
                interfaceRow["ChainDomainID1"] = -1;
                interfaceRow["ChainDomainID2"] = -1;
                domainInterfaceTable.Rows.Add(interfaceRow);

                if (!relationEntryList.Contains(fields[1]))
                {
                    relationEntryList.Add(fields[1]);
                }
            }
            newDataReader.Close();

            string[] relationEntries = new string[relationEntryList.Count];
            relationEntryList.CopyTo(relationEntries);

            UpdateChainDomainIdToInterfaces(relationEntries);

            StreamWriter newDataWriter = new StreamWriter("AddedDomainInterfacesInDb.txt");
            Dictionary<string, List<int>> unusedDomainInterfaceIdHash = GetUnusedDomainInterfaceIdHash(relationEntries);
            Dictionary<string, int> nextDomainInterfaceIdHash = new Dictionary<string,int> ();
            for (int i = 0; i < domainInterfaceTable.Rows.Count; i++)
            //     foreach (DataRow domainInterfaceRow in domainInterfaceTable.Rows)
            {
                DataRow domainInterfaceRow = domainInterfaceTable.Rows[i];
                relSeqId = Convert.ToInt32 (domainInterfaceRow["RelSeqID"].ToString());
                pdbId = domainInterfaceRow["PdbID"].ToString();
                domainInterfaceId = GetDomainInterfaceId(pdbId, unusedDomainInterfaceIdHash, nextDomainInterfaceIdHash);
                domainInterfaceRow["DomainInterfaceID"] = domainInterfaceId;
                newDataWriter.WriteLine (ParseHelper.FormatDataRow (domainInterfaceRow));
                newDataWriter.Flush();
            }
            newDataWriter.Close();

            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, domainInterfaceTable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="relEntryDomainInterfaceIdHash"></param>
        /// <param name="nextDomainInterfaceIdHash"></param>
        /// <returns></returns>
        private int GetDomainInterfaceId(string pdbId, Dictionary<string, List<int>> relEntryDomainInterfaceIdHash, Dictionary<string, int> nextDomainInterfaceIdHash)
        { 
            int domainInterfaceId = 0;
            if (relEntryDomainInterfaceIdHash.ContainsKey(pdbId))
            {
                List<int> unusedDomainInterfaceIdList = relEntryDomainInterfaceIdHash[pdbId];
                domainInterfaceId = unusedDomainInterfaceIdList[0];
                unusedDomainInterfaceIdList.RemoveAt(0);
                if (unusedDomainInterfaceIdList.Count == 0)
                {
                    relEntryDomainInterfaceIdHash.Remove(pdbId);
                }
            }
            else
            {
                if (nextDomainInterfaceIdHash.ContainsKey(pdbId))
                {
                    domainInterfaceId =  nextDomainInterfaceIdHash[pdbId];
                    nextDomainInterfaceIdHash[pdbId] = domainInterfaceId + 1;
                }
                else
                {
                    int maxDomainInterfaceId = GetMaxDomainInterfaceId (pdbId);
                    domainInterfaceId = maxDomainInterfaceId + 1;
                    nextDomainInterfaceIdHash.Add (pdbId, domainInterfaceId + 1);
                }
            }
            return domainInterfaceId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int GetMaxDomainInterfaceId(string pdbId)
        {
            string queryString = string.Format("Select Max(DomainInterfaceID) As MaxDomainInterfaceId From PfamDomainInterfaces " + 
                " Where PdbID = '{0}';", pdbId);
            DataTable maxDomainInterfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int maxDomainInterfaceId = 0;
            if (maxDomainInterfaceIdTable.Rows[0]["MaxDomainInterfaceId"].ToString() != "")
            {
                maxDomainInterfaceId = Convert.ToInt32(maxDomainInterfaceIdTable.Rows[0]["MaxDomainInterfaceId"].ToString());
            }
            return maxDomainInterfaceId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relationEntries"></param>
        /// <returns></returns>
        private Dictionary<string, List<int>> GetUnusedDomainInterfaceIdHash(string[] relationEntries)
        {
            Dictionary<string, List<int>> relEntryDomainInterfaceIdHash = new Dictionary<string,List<int>> ();
            foreach (string pdbId in relationEntries)
            {
                List<int> unusedDomainInterfaceIdList = GetUnusedDomainInterfaceIds( pdbId);
                if (unusedDomainInterfaceIdList.Count > 0)
                {
                    relEntryDomainInterfaceIdHash.Add(pdbId, unusedDomainInterfaceIdList);
                }
            }
            return relEntryDomainInterfaceIdHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private List<int> GetUnusedDomainInterfaceIds(string pdbId)
        {
            string queryString = string.Format("Select Distinct DomainInterfaceId From PfamDomainInterfaces " + 
                " Where PdbID = '{0}' Order By DomainInterfaceID;", pdbId);
            DataTable domainInterfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int domainInterfaceId = 0;
            int maxDomainInterfaceId = 0;
            foreach (DataRow domainInterfaceIdRow in domainInterfaceIdTable.Rows)
            {
                domainInterfaceId = Convert.ToInt32(domainInterfaceIdRow["DomainInterfaceID"].ToString ());
                if (maxDomainInterfaceId < domainInterfaceId)
                {
                    maxDomainInterfaceId = domainInterfaceId;
                }
            }
            List<int> unusedDomainInterfaceIdList = new List<int> ();
            for (int i = 1; i <= maxDomainInterfaceId; i++)
            {
                unusedDomainInterfaceIdList.Add(i);
            }
            foreach (DataRow domainInterfaceIdRow in domainInterfaceIdTable.Rows)
            {
                domainInterfaceId = Convert.ToInt32(domainInterfaceIdRow["DomainInterfaceID"].ToString ());
                unusedDomainInterfaceIdList.Remove(domainInterfaceId);
            }

            return unusedDomainInterfaceIdList;
        }

        private void DeletePfamDomainInterfaceRow(int relSeqId, string pdbId, int interfaceId, int domainInterfaceId)
        {
            string deletestring = string.Format("Delete  From PfamDOmainInterfaces " + 
                " WHere RelSeqID = {0} AND PdbID = '{1}' AND InterfaceID = {2} AND DOmainInterfaceID = {3};", 
                relSeqId, pdbId, interfaceId, domainInterfaceId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deletestring);
        }

        private void CompareEntryDomainInterfaces(string pdbId, DataTable domainInterfaceTable, StreamWriter dataWriter, StreamWriter addedDataWriter)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
            DataTable dbDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string dataLine = "";
            foreach (DataRow dbDomainInterfaceRow in dbDomainInterfaceTable.Rows)
            {
                if (IsDomainInterfaceDefined(dbDomainInterfaceRow, domainInterfaceTable))
                {
                    continue;
                }
                dataLine = ParseHelper.FormatDataRow(dbDomainInterfaceRow);
                dataWriter.WriteLine(dataLine);
            }
            dataWriter.Flush();

            foreach (DataRow domainInterfaceRow in domainInterfaceTable.Rows)
            {
                if (IsDomainInterfaceDefined(domainInterfaceRow, dbDomainInterfaceTable))
                {
                    continue;
                }
                dataLine = ParseHelper.FormatDataRow(domainInterfaceRow);
                addedDataWriter.WriteLine(dataLine);
            }
            addedDataWriter.Flush();
        }

        private bool IsDomainInterfaceDefined (DataRow domainInterfaceRow, DataTable newDomainInterfaceTable)
        {
            DataRow[] domainInterfaceRows = newDomainInterfaceTable.Select(string.Format ("InterfaceID = '{0}' AND DomainID1 = '{1}' AND AsymChain1 = '{2}' " + 
                " AND DomainID2 = '{3}' AND AsymChain2 = '{4}'", domainInterfaceRow["InterfaceID"], domainInterfaceRow["DomainID1"],
                domainInterfaceRow["AsymChain1"], domainInterfaceRow["DomainID2"], domainInterfaceRow["AsymChain2"]));
            if (domainInterfaceRows.Length > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        public void GetEntriesWithInconsistentDomainIds()
        {
            StreamWriter dataWriter = new StreamWriter("InconsistentInterfaceDomainEntries.txt");
            string queryString = "Select Distinct PdbID From PfamDomainInterfaces;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
  
            long[] inconsistentDomainIds = null;
            
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString ();
                if (! AreDomainIdsConsistent(pdbId, out inconsistentDomainIds))
                {
                    dataWriter.WriteLine(pdbId + " " + FormatArrayToString(inconsistentDomainIds));
                    dataWriter.Flush();
                }
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void GetDomainInterfacesFilesNotExistPre()
        {
            StreamWriter dataWriter = new StreamWriter("InconsistentInterfaceDomainEntries.txt", true);
            DateTime dtCutoff = new DateTime (2013, 2, 4);
            string dataDir = @"D:\DbProjectData\InterfaceFiles_update\pfamDomain";
            string[] hashFolders = Directory.GetDirectories(dataDir);
            List<string> entryList = new List<string> ();
            string pdbId = "";
            foreach (string hashFolder in hashFolders)
            {
                DirectoryInfo dirInfo = new DirectoryInfo(hashFolder);
                if (DateTime.Compare(dirInfo.LastWriteTime, dtCutoff) >= 0)
                {
                    string[] files = Directory.GetFiles(hashFolder);
                    foreach (string file in files)
                    {
                        FileInfo fileInfo = new FileInfo(file);
                        if (DateTime.Compare(fileInfo.LastWriteTime, dtCutoff) >= 0)
                        {
                            pdbId = fileInfo.Name.Substring(0, 4);
                            if (!entryList.Contains(pdbId))
                            {
                                entryList.Add(pdbId);
                                dataWriter.WriteLine(pdbId);
                                dataWriter.Flush();
                            }
                        }
                    }
                }
            }
            dataWriter.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainIds"></param>
        /// <returns></returns>
        private string FormatArrayToString(long[] domainIds)
        {
            string domainsString = "";
            foreach (long domainId in domainIds)
            {
                domainsString += (domainId.ToString() + ",");
            }
            return domainsString.TrimEnd(',');
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="inconsistentDomainIds"></param>
        /// <returns></returns>
        private bool AreDomainIdsConsistent(string pdbId, out long[] inconsistentDomainIds)
        {
            string queryString = string.Format("Select DomainID From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable domainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            long[] interfaceDomainIds = GetInterfaceDomainIDs(pdbId);
            List<long> inconsistentDomainIdList = new List<long> ();
            foreach (long interfaceDomainId in interfaceDomainIds)
            {
                DataRow[] domainRows = domainTable.Select(string.Format ("DomainID = '{0}'", interfaceDomainId));
                if (domainRows.Length == 0)
                {
                    inconsistentDomainIdList.Add(interfaceDomainId);
                }
            }
            inconsistentDomainIds = new long[inconsistentDomainIdList.Count];
            inconsistentDomainIdList.CopyTo(inconsistentDomainIds);
            if (inconsistentDomainIdList.Count > 0)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private long[] GetInterfaceDomainIDs(string pdbId)
        {
            string queryString = string.Format("Select DomainID1, DomainID2 From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
            DataTable domainPairTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<long> domainList = new List<long> ();
            long domainId = 0;
            foreach (DataRow domainPairRow in domainPairTable.Rows)
            {
                domainId = Convert.ToInt64(domainPairRow["DomainID1"].ToString ());
                if (!domainList.Contains(domainId))
                {
                    domainList.Add(domainId);
                }
                domainId = Convert.ToInt64(domainPairRow["DomainID2"].ToString());
                if (!domainList.Contains(domainId))
                {
                    domainList.Add(domainId);
                }
            }
            return domainList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        public void UpdateDomainInterfacesForDebug()
        {
            GroupDbTableNames.SetGroupDbTableNames(ProtCidSettings.dataType);

            string[] updateEntries = GetChangeDomainEntries();
            //    string[] updateEntries = { "3zs0" };

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Domain-domain interactions";

            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;

            string domainInterfaceTableName = ProtCidSettings.dataType + "DomainInterfaces";

            foreach (string pdbId in updateEntries)
            {
                // progress info
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                // delete obsolete data
                DeleteObsData(pdbId);

                GetDomainInterfacesFromCrystInterfaces(pdbId);

                // insert data into db
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection,
                    DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces]);
                DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces].Clear();
            }// group
            UpdateClanSeqIds();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] GetChangeDomainEntries()
        {
            StreamReader dataReader = new StreamReader("WrongMultChainDomainInterfaces.txt");
            string line = "";
            List<string> entryList = new List<string> ();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                if (!entryList.Contains(fields[1]))
                {
                    entryList.Add(fields[1]);
                }
            }
            dataReader.Close();
            return entryList.ToArray ();
        }

        
        /// <summary>
        /// correct intra-chains domain interfaces due to a bug
        /// In retrieving the intra-chain domain interfaces, 
        /// did not clear the chainContactInfo in the ChainContact class
        /// </summary>
        public void UpdateIntraChainDomains()
        {
            string[] groupEntries = GetEntriesWithIntraChainDomainInterfaces();
            string[] fields = null;
            int groupId = -1;
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Intra-chain domain interfaces";
            ProtCidSettings.progressInfo.totalOperationNum = groupEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = groupEntries.Length;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate intra-chain domain interfaces.");

            foreach (string groupEntry in groupEntries)
            {
                ProtCidSettings.progressInfo.currentFileName = groupEntry;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                fields = groupEntry.Split('_');
                groupId = Convert.ToInt32(fields[0]);
                DeleteDomainInterfaceFiles(groupId, fields[1]);

                DataTable domainDefTable = GetDomainDefTable(fields[1]);

                int dInterfaceId = GetMaxNonIntraDomainInterfaceId(fields[1]);

                GetMultiDomainInterfaces(fields[1], domainDefTable, ref dInterfaceId);

                DeleteDataInDb(groupId, fields[1]);


                dbInsert.InsertDataIntoDBtables
                    (ProtCidSettings.protcidDbConnection, DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces]);
                DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaces].Clear();
            }
            dataWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetEntriesWithIntraChainDomainInterfaces()
        {
            string queryString = "Select Distinct RelSeqID, PdbID From PfamDomainInterfaces " +
                " Where InterfaceID = 0;";
            DataTable intraChainEntryTable = ProtCidSettings.protcidQuery.Query( queryString);

            string[] groupEntries = new string[intraChainEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in intraChainEntryTable.Rows)
            {
                groupEntries[count] = entryRow["RelSeqID"].ToString() + "_" + entryRow["PdbId"].ToString();
                count++;
            }
            return groupEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        private void DeleteDataInDb(int relSeqId, string pdbId)
        {
            string deleteString = string.Format("Delete From PfamDomainInterfaces " +
                " Where RelSeqID = {0} AND PdbID = '{1}';", relSeqId, pdbId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        private void DeleteDomainInterfaceFiles(int relSeqId, string pdbId)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces " +
                " Where RelSeqID = {0} AND PdbID = '{1}' AND InterfaceID = 0;", relSeqId, pdbId);
            DataTable intraDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string fileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, ProtCidSettings.dataType + "domain");
            fileDir = Path.Combine(fileDir, pdbId.Substring(1, 2));
            string fileName = "";
            string line = "";
            foreach (DataRow domainInterfaceRow in intraDomainInterfaceTable.Rows)
            {
                line = "";
                foreach (object item in domainInterfaceRow.ItemArray)
                {
                    line += (item.ToString().Trim() + ",");
                }
                dataWriter.WriteLine(line);
                fileName = pdbId + "_d" + domainInterfaceRow["DomainInterfaceID"].ToString() + ".cryst.gz";
                File.Delete(Path.Combine(fileDir, fileName));
            }
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int GetMaxNonIntraDomainInterfaceId(string pdbId)
        {
            string queryString = string.Format("Select Max(DomainInterfaceID) AS maxDInterfaceId From PfamDomainInterfaces " +
                " Where PdbID = '{0}' and InterfaceId <> 0;", pdbId);
            DataTable maxInterfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int maxDInterfaceId = 0;
            if (maxInterfaceIdTable.Rows.Count > 0)
            {
                try
                {
                    maxDInterfaceId = Convert.ToInt32(maxInterfaceIdTable.Rows[0]["maxDInterfaceId"].ToString());
                }
                catch { }
            }
            maxDInterfaceId++;
            return maxDInterfaceId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        public Dictionary<int, string[]> GetUpdateRelationEntries(string[] updateEntries)
        {
            Dictionary<int, List<string>> updateRelationEntryListHash = new Dictionary<int,List<string>> ();

            foreach (string pdbId in updateEntries)
            {
                int[] relSeqIds = GetEntryRelSeqId(pdbId);

                foreach (int relSeqId in relSeqIds)
                {
                    if (updateRelationEntryListHash.ContainsKey(relSeqId))
                    {
                        if (!updateRelationEntryListHash[relSeqId].Contains(pdbId))
                        {
                            updateRelationEntryListHash[relSeqId].Add(pdbId);
                        }
                    }
                    else
                    {
                        List<string> updateEntryList = new List<string> ();
                        updateEntryList.Add(pdbId);
                        updateRelationEntryListHash.Add(relSeqId, updateEntryList);
                    }
                }
            }
            Dictionary<int, string[]> updateRelationEntryHash = new Dictionary<int, string[]>();
            foreach (int relSeqId in updateRelationEntryListHash.Keys)
            {
                updateRelationEntryHash.Add (relSeqId, updateRelationEntryListHash[relSeqId].ToArray ());
            }
            return updateRelationEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetEntryRelSeqId(string pdbId)
        {
            string queryString = string.Format("Select Distinct RelSeqID From PfamDomainInterfaces Where PdbId = '{0}';",
            pdbId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] relSeqIds = new int[relSeqIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqIds[count] = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString());
                count++;
            }
            return relSeqIds;
        }
        #endregion
    }
}
