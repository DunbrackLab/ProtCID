using System;
using System.Data;
using System.IO;
using System.Collections.Generic;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.StructureComp;
using CrystalInterfaceLib.StructureComp.HomoEntryComp;
using CrystalInterfaceLib.SimMethod;
using CrystalInterfaceLib.Settings;
using ProtCidSettingsLib;
using CrystalInterfaceLib.DomainInterfaces;
using DbLib;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces
{
	/// <summary>
	/// domain-domain interaction comparison
	/// </summary>
	public class PfamDomainInterfaceComp  : DomainInterfaceComp
	{
		#region member variables
		public DomainInterfaceRetriever domainInterfaceRetriever = new DomainInterfaceRetriever ();
		public DbQuery dbQuery = new DbQuery ();
		public DbInsert dbInsert = new DbInsert ();
		public FamilyDomainAlign domainAlign = new FamilyDomainAlign ();	
    //    public StreamWriter nonAlignDomainWriter = null;
        public StreamWriter logWriter = null;
        public StreamWriter noSimDomainInterfacesWriter = null;
        // the list of antiboy entries in pdb, exclude those entries in the computing
        private string[] antibodyEntries = null;
        public StreamWriter compResultWriter = null;
        public StreamWriter entryCompResultWriter = null;
		#endregion

        #region constructors
        public PfamDomainInterfaceComp()
		{
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }
            // entry pairs donot have any Q scores between interfaces > the minimum Q score
            noSimDomainInterfacesWriter = new StreamWriter("NoSimilarDomainInterfacesEntryPairs.txt", true);
            nonAlignDomainWriter = new StreamWriter("NonAlignDomainsLog.txt", true);
            DomainInterfaceBuilder.nonAlignDomainsWriter = new StreamWriter("NonAlignedDomainPairs.txt", true);
           
     //       ReadAntibodyEntries();
        }

        public PfamDomainInterfaceComp(string alignType)
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }
            // entry pairs donot have any Q scores between interfaces > the minimum Q score
            noSimDomainInterfacesWriter = new StreamWriter("NoSimilarDomainInterfacesEntryPairs.txt", true);
            nonAlignDomainWriter = new StreamWriter("NonAlignDomainsLog.txt", true);
            DomainInterfaceBuilder.nonAlignDomainsWriter = new StreamWriter("NonAlignedDomainPairs.txt", true);

            domainAlignType = alignType;
            if (alignType == "pfam")
            {
                DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp].TableName =
                        DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp].TableName + "Hmm";
            }
        }
        /// <summary>
        /// Read antibody entries from Andreas's file 
        /// Marh 18, 2009
        /// </summary>
        private void ReadAntibodyEntries()
        {
            StreamReader dataReader = new StreamReader(Path.Combine (ProtCidSettings.dirSettings.pfamPath, "antibodyEntries.txt"));
            string line = "";
            List<string> antibodyEntryList = new List<string>();
            string entry = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                line = line.ToLower();
                if (line.IndexOf("start time") > -1)
                {
                    continue;
                }
                if (line.IndexOf("end time") > -1)
                {
                    continue;
                }
                if (line == "")
                {
                    continue;
                }
                if (line.IndexOf("counter") > -1)
                {
                    continue;
                }
                entry = line.Substring (0, 4);
                if (! antibodyEntryList.Contains (entry))
                {
                    antibodyEntryList.Insert (0, entry);
                }
            }
            dataReader.Close ();
            antibodyEntryList.Sort ();
            antibodyEntries = new string[antibodyEntryList.Count];
            antibodyEntryList.CopyTo (antibodyEntries);
        }
        #endregion

        #region compare domain interfaces between different entries
        /// <summary>
        /// 
        /// </summary>
        public void CompareDomainInterfaces()
        {
            //[2, 30]: 10930; [31, 100]: 1177; [101, 200]: 266; [201, 300]: 111; [301, 400]: 27; [401, 500]: 21; 
            // [501, 1000]: 12; [1001, 4000]: 7.  
            // 2274(Asp): 1058, 2986(C1-set): 3056, 3175 (C1-set)_(V-set): 2194, 14480 (Pkinase): 2598, 
            // 14511 (Pkinase_Tyr): 1232, 17706(Trypsin): 1874, 17915 (V-set): 2975.
            int minNumRepEntries = 1001;
            int maxNumRepEntries = 4000;
            int[] antibodyGroups = {2986, 3175, 17915};
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Calculate Q scores of domain interfaces: " + 
                minNumRepEntries.ToString () + "-" +  maxNumRepEntries.ToString ());

            nonAlignDomainWriter.WriteLine(DateTime.Today.ToShortDateString());
            nonAlignDomainWriter.WriteLine("The Q scores between domain interfaces " +
                minNumRepEntries.ToString() + "-" + maxNumRepEntries.ToString());

            noSimDomainInterfacesWriter.WriteLine(DateTime.Today.ToShortDateString());
            noSimDomainInterfacesWriter.WriteLine("The Q scores between domain interfaces " +
                minNumRepEntries.ToString() + "-" + maxNumRepEntries.ToString());

            DomainInterfaceBuilder.nonAlignDomainsWriter.WriteLine(DateTime.Today.ToShortDateString());
            DomainInterfaceBuilder.nonAlignDomainsWriter.WriteLine("The Q scores between domain interfaces " +
                minNumRepEntries.ToString() + "-" + maxNumRepEntries.ToString());

            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults_" + minNumRepEntries.ToString () + 
                    "less" + maxNumRepEntries.ToString () + domainAlignType + ".txt", true);
                logWriter = new StreamWriter("CrystDomainInterfaceCompLog_" + minNumRepEntries.ToString() +
                    "less" + maxNumRepEntries.ToString() + domainAlignType + ".txt", true);
                entryCompResultWriter = new StreamWriter("EntryInterfaceCompResults_" + minNumRepEntries.ToString() +
                    "less" + maxNumRepEntries.ToString() + domainAlignType + ".txt", true);
            }

    /*        string queryString = string.Format("Select RelSeqID, Count(distinct PDbID) As EntryCount " +
                  " From {0}DomainInterfaces Group By RelSeqID;", ProtCidSettings.dataType);
            DataTable relSeqTable = ProtCidSettings.protcidQuery.Query( queryString);
            WriteRelationRepEntriesToFile(relSeqTable);*/
           
            Dictionary<int, int> relationNumRepEntriesHash = GetNumOfRepEntriesInRelations();
        //    int relSeqId = -1;
            int entryCount = -1;

            List<int> groupList = new List<int> ();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare " + ProtCidSettings.dataType + " domain interfaces. " );
            ProtCidSettings.progressInfo.currentOperationLabel = "Domain Interface Comp";
            //      ProtCidSettings.progressInfo.totalOperationNum = relSeqTable.Rows.Count;
            //      ProtCidSettings.progressInfo.totalStepNum = relSeqTable.Rows.Count;
            List<int> relSeqIdList = new List<int> (relationNumRepEntriesHash.Keys);
            relSeqIdList.Sort();
//            foreach (DataRow relSeqRow in relSeqTable.Rows)
            int relationCount = 1;
            foreach (int relSeqId in relSeqIdList)
            {
                /*
                relSeqId = Convert.ToInt32(relSeqRow["RelSeqID"].ToString());
                entryCount = Convert.ToInt32(relSeqRow["EntryCount"].ToString());
              
            */
                entryCount = (int)relationNumRepEntriesHash[relSeqId];
                if (entryCount < maxNumRepEntries && entryCount >= minNumRepEntries)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(relationCount.ToString());
                    if (relSeqId != 2986)  // calculate antibody groups one by one
                    {
                        relationCount++;
                        continue;
                    }

                    //      CompareDomainInterfacesInBigRelation(relSeqId);
        /*            if (Array.IndexOf(antibodyGroups, relSeqId) > -1)
                    {                       
                        ProtCidSettings.logWriter.WriteLine(relSeqId + " antibody group: skip.");    
                        relationCount++;
                        continue;
                    }*/

                    if (entryCount > 50)
                    {
                        CompareDomainInterfacesInBigRelation(relSeqId);
                    }
                    else
                    {
                        CompareDomainInterfacesInRelation(relSeqId);
                    }

                    relationCount++;
                }
            }
            DomainInterfaceBuilder.nonAlignDomainsWriter.Close();
            nonAlignDomainWriter.Close();
            noSimDomainInterfacesWriter.Close();
            logWriter.Close();
            compResultWriter.Close();
            entryCompResultWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");

            // copy chain interface comp to domain interface comp, if and only if the chain contains only one domain
            // and the domain cover more than 90% of the sequence
 //           SynchronizeDomainChainInterfaceComp();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary <int, int> GetNumOfRepEntriesInRelations()
        {
            StreamReader dataReader = new StreamReader("RelationEntryCount.txt");
            Dictionary<int, int> relationNumRelEntryHash = new Dictionary<int,int> ();
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                relationNumRelEntryHash.Add(Convert.ToInt32 (fields[0]), Convert.ToInt32 (fields[1]));
            }
            dataReader.Close();
            return relationNumRelEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relEntryCountTable"></param>
        private void WriteRelationRepEntriesToFile(DataTable relEntryCountTable)
        {
            StreamWriter dataWriter = new StreamWriter("RelationEntryCount.txt");
            int entryCount = 0;
            int relSeqId = 0;
            foreach (DataRow relEntryCountRow in relEntryCountTable.Rows)
            {
                relSeqId = Convert.ToInt32(relEntryCountRow["RelSeqID"].ToString ());
                if (relSeqId == 2553 || relSeqId == 20083)  // exclude antiboday interactions
                {
                    continue;
                }
                entryCount = Convert.ToInt32(relEntryCountRow["EntryCount"].ToString ());
                if (entryCount > 1)
                {
                    dataWriter.WriteLine(relEntryCountRow["RelSeqID"].ToString() + "\t" +
                        relEntryCountRow["EntryCount"].ToString());
                }
            }
            dataWriter.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqIds"></param>
        public void CompareDomainInterfacesInRelations(int[] relSeqIds)
        {
            foreach (int relSeqId in relSeqIds)
            {
                // set the streamwriter to be null
                compResultWriter = null; ;
                entryCompResultWriter = null;
                logWriter = null;

                CompareDomainInterfacesInRelation(relSeqId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void CompareDomainInterfacesInSpecificRelations()
        {
        /*    string queryString = "SELECT * FROM pfamdomainfamilyrelation " + 
                " WHERE familycode1 IN ('Pkinase', 'Pkinase_Tyr', 'SH2', 'SH3_1', 'PDZ', 'CBS')" +
                " AND familycode2 IN ('Pkinase', 'Pkinase_Tyr', 'SH2', 'SH3_1', 'PDZ', 'CBS');";*/
            string queryString = "Select Distinct RelSeqID From PfamDomainFamilyRelation " +
                //   " where (familycode1 in ('Pkinase', 'Pkinase_Tyr')) or (familycode2 in ('Pkinase', 'Pkinase_Tyr'));";  
                    " where (familycode1 in ('Integrase_Zn', 'rve', 'IN_DBD_C', 'zf-H2C2')) " + 
                    " or (familycode2 in ('Integrase_Zn', 'rve', 'IN_DBD_C', 'zf-H2C2'));";
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Total # of Relations: " + relSeqIdTable.Rows.Count);
            int relSeqId = 0;
            int count = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                // set the streamwriter to be null
                compResultWriter = null; ;
                entryCompResultWriter = null;
                logWriter = null;

                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString ());
                count++;

            /*    if (relSeqId == 11619 || relSeqId == 11642)
                {
                    continue;
                }
                if (relSeqId <= 3446)
                {
                    continue;
                }*/
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(count.ToString ());
                CompareDomainInterfacesInBigRelation (relSeqId);
            }
        }

        /// <summary>
        /// Compare each pair of representative entries in the relation 
        /// and also compare the representative entry and its homologous entries
        /// </summary>
        /// <param name="relSeqId"></param>
        public void CompareDomainInterfacesInRelation(int relSeqId)
        {
            bool resultNeedClosed = false;
            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults" + relSeqId.ToString () + ".txt");
                entryCompResultWriter = new StreamWriter("EntryInterfaceCompResults" + relSeqId.ToString () + ".txt");
                logWriter = new StreamWriter("DomainInterfaceCompLog" + relSeqId.ToString () + ".txt");

                resultNeedClosed = true;
            }
           
            ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString ());
            ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
            noSimDomainInterfacesWriter.WriteLine(relSeqId.ToString());
            nonAlignDomainWriter.WriteLine(relSeqId.ToString ());
            logWriter.WriteLine(relSeqId.ToString ());

            /*  modified on August 18, 2009, 
             *  changed into pairwise compare representative entries and 
             *  compare each rep entry and its homologies, follow the same idea as chain interface
             *  not every pair of all entries in the relation
             *  should reduce the computation
             * */
            Dictionary<string, string[]> repHomoEntryHash = GetRelationRepHomoEntries (relSeqId);
            List<string> repEntryList = new List<string> (repHomoEntryHash.Keys);
            repEntryList.Sort();
            string[] repEntries = new string[repEntryList.Count];
            repEntryList.CopyTo(repEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve domain interface files from inter-chain interface files and intra-chain interface files");
            Dictionary<string, DomainInterface[]> entryDomainInterfaceHash = new Dictionary<string,DomainInterface[]> ();
            Dictionary<string, string[]> needCompEntryHash = new Dictionary<string, string[]> ();
            List<string> needCompEntryList = new List<string> ();
            
            int entryPairCount = 0;
            List<string> entryNeedDomainInterfaceList = new List<string> ();
            for (int i = 0; i < repEntries.Length; i++)
            {
                needCompEntryList = new List<string>();
                string[] homoEntries = (string[])repHomoEntryHash[repEntries[i]];
                foreach (string homoEntry in homoEntries)
                {
                    if (ExistDomainInterfaceCompInDb(relSeqId, repEntries[i], homoEntry))
                    {
                        continue;
                    }

                    //     DeleteDomainInterfaceComp(relSeqId, repEntries[i], homoEntry);

                    needCompEntryList.Add(homoEntry);
                    entryPairCount++;

                    if (!entryNeedDomainInterfaceList.Contains(homoEntry))
                    {
                        entryNeedDomainInterfaceList.Add(homoEntry);
                    }
                }  // temporary comment it out since the rep-homo pairs are already updated on Oct. 13, 2009.
                for (int j = i + 1; j < repEntries.Length; j++)
                {
                    if (ExistDomainInterfaceCompInDb(relSeqId, repEntries[i], repEntries[j]))
                    {
                        continue;
                    }

                    //       DeleteDomainInterfaceComp(relSeqId, repEntries[i], repEntries[j]);

                    entryPairCount++;
                    needCompEntryList.Add(repEntries[j]);
                    if (!entryNeedDomainInterfaceList.Contains(repEntries[j]))
                    {
                        entryNeedDomainInterfaceList.Add(repEntries[j]);
                    }
                }
                // the domain interfaces for those entries needed to be compared
                if (needCompEntryList.Count > 0)
                {
                    string[] compEntries = new string[needCompEntryList.Count];
                    needCompEntryList.CopyTo(compEntries);
                    needCompEntryHash.Add(repEntries[i], compEntries);

                    // this entry has domain interfaces generated here
                    entryNeedDomainInterfaceList.Remove(repEntries[i]);

                    ProtCidSettings.progressInfo.currentFileName = repEntries[i];
                    DomainInterface[] uniqueDomainInterfaces =
                        GetEntryUniqueDomainInterfaces(relSeqId, repEntries[i]);
                    entryDomainInterfaceHash.Add(repEntries[i], uniqueDomainInterfaces);
                }
            }
            foreach (string entry in entryNeedDomainInterfaceList)
            {
                ProtCidSettings.progressInfo.currentFileName = entry;
                DomainInterface[] uniqueDomainInterfaces = GetEntryUniqueDomainInterfaces(relSeqId, entry);
                entryDomainInterfaceHash.Add(entry, uniqueDomainInterfaces);
            }

            CompareEntryDomainInterfaces(relSeqId, needCompEntryHash, entryDomainInterfaceHash, entryPairCount);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
            if (resultNeedClosed)
            {
                compResultWriter.Close();
                entryCompResultWriter.Close();
                logWriter.Close();
            }     
        }

        /// <summary>
        /// divide the list of entries into rep entry and its homo entries
        /// </summary>
        /// <param name="pdbEntries"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> DivideEntriesIntoRepHomoEntries(string[] pdbEntries)
        {
            List<string> leftEntryList = new List<string> (pdbEntries);
            Dictionary<string, string[]> repHomoEntryHash = new Dictionary<string,string[]> ();
            foreach (string entry in pdbEntries)
            {
                if (!leftEntryList.Contains(entry))
                {
                    continue;
                }
                if (IsRepresentativeEntry(entry))
                {
                    string[] homoEntries = GetHomoEntriesForRepEntry(entry);
                    repHomoEntryHash.Add(entry, homoEntries);
                    leftEntryList.Remove(entry);
                    foreach (string homoEntry in homoEntries)
                    {
                        leftEntryList.Remove(homoEntry);
                    }
                }
            }
            return repHomoEntryHash;
        }
       /// <summary>
       /// 
       /// </summary>
       /// <param name="relSeqId"></param>
       /// <param name="needCompEntryHash"></param>
       /// <param name="entryDomainInterfaceHash"></param>
       /// <param name="entryPairCount"></param>
        private void CompareEntryDomainInterfaces(int relSeqId, Dictionary<string, string[]> needCompEntryHash, Dictionary<string, DomainInterface[]> entryDomainInterfaceHash, int entryPairCount)
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare  domain interfaces.");
            ProtCidSettings.progressInfo.totalOperationNum = entryPairCount;
            ProtCidSettings.progressInfo.totalStepNum = entryPairCount;
            ProtCidSettings.progressInfo.currentOperationNum = 0;
            ProtCidSettings.progressInfo.currentStepNum = 0;

            List<string> needCompEntryList = new List<string> (needCompEntryHash.Keys);
            needCompEntryList.Sort();
            foreach (string entry in needCompEntryList)
            {
                string[] compEntries = (string[])needCompEntryHash[entry];
                CompareEntryDomainInterfaces(relSeqId, entry, compEntries, entryDomainInterfaceHash);

                // remove the domain interfaces from the hash in order to reduce memory usage. Hopefully.
                //       needCompEntryHash.Remove(entry);  
                entryDomainInterfaceHash.Remove(entry);
                noSimDomainInterfacesWriter.Flush();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="needCompEntryHash"></param>
        /// <param name="entryDomainInterfaceHash"></param>
        /// <param name="entryPairCount"></param>
        private void CompareEntryDomainInterfaces(int relSeqId, Dictionary<string, List<string>> needCompEntryHash, Dictionary<string, DomainInterface[]> entryDomainInterfaceHash, int entryPairCount)
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare  domain interfaces.");
            ProtCidSettings.progressInfo.totalOperationNum = entryPairCount;
            ProtCidSettings.progressInfo.totalStepNum = entryPairCount;
            ProtCidSettings.progressInfo.currentOperationNum = 0;
            ProtCidSettings.progressInfo.currentStepNum = 0;

            List<string> needCompEntryList = new List<string>(needCompEntryHash.Keys);
            needCompEntryList.Sort();
            foreach (string entry in needCompEntryList)
            {
                CompareEntryDomainInterfaces(relSeqId, entry, needCompEntryHash[entry].ToArray (), entryDomainInterfaceHash);

                // remove the domain interfaces from the hash in order to reduce memory usage. Hopefully.
                //       needCompEntryHash.Remove(entry);  
                entryDomainInterfaceHash.Remove(entry);
                noSimDomainInterfacesWriter.Flush();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="entry"></param>
        /// <param name="compEntries"></param>
        /// <param name="entryDomainInterfaceHash"></param>
        private void CompareEntryDomainInterfaces(int relSeqId, string entry, string[] compEntries, Dictionary<string, DomainInterface[]> entryDomainInterfaceHash)
        {
            DomainInterfacePairInfo[] pairCompInfos = null;
            foreach (string compEntry in compEntries)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = entry + "_" + compEntry;
                pairCompInfos = null;

                if (entry == compEntry)
                {
                    continue;
                }
                
                try
                {
                    DomainInterface[] domainInterfaces1 = (DomainInterface[])entryDomainInterfaceHash[entry];
                    if (domainInterfaces1 == null)
                    {
                        noSimDomainInterfacesWriter.WriteLine("No domain interfaces in entry " + entry);
                        continue;
                    }

                    DomainInterface[] domainInterfaces2 = (DomainInterface[])entryDomainInterfaceHash[compEntry];
                    if (domainInterfaces2 == null)
                    {
                        noSimDomainInterfacesWriter.WriteLine("No domain interfaces in entry " + compEntry);
                        continue;
                    }
                    try
                    {
                        pairCompInfos = CompareDomainInterfaces(domainInterfaces1, domainInterfaces2);
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine("Compare Inter-chain domain interfaces for " + entry
                            + " and " + compEntry + " errors: " + ex.Message);
                        logWriter.Flush();
                    }
                    if (pairCompInfos == null || pairCompInfos.Length == 0)
                    {
                        noSimDomainInterfacesWriter.WriteLine(entry + "_" + compEntry);
                        continue;
                    }
                    try
                    {
                        AssignDomainInterfaceCompTable(relSeqId, entry, compEntry, pairCompInfos);
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine("Assign comparison of " + entry + "_" + compEntry + " to data table errors:  "
                                     + ex.Message);
                        logWriter.Flush();
                    }
                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp]);
                    WriteCompResultToFile(DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp], compResultWriter);
                    DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp].Clear();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(entry + "_" + compEntry + " error: " + ex.Message);

                    logWriter.WriteLine("Compare " + entry + "_" + compEntry + " domain interfaces error:  "
                                     + ex.Message);
                    logWriter.Flush();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsEntryAntibody(string pdbId)
        {
            int entryIdx = Array.BinarySearch(antibodyEntries, pdbId);
            if (entryIdx > -1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Is this entry a representative in the sg + asu + 1% unit cell group
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsRepresentativeEntry(string pdbId)
        {
            string queryString = string.Format("Select * From {0}HomoSeqInfo Where PdbID = '{1}';", 
                ProtCidSettings.dataType, pdbId);
            DataTable repEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (repEntryTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// the list of homologous entries for this representative entry
        /// </summary>
        /// <param name="repEntry"></param>
        /// <returns></returns>
        private string[] GetHomoEntriesForRepEntry(string repEntry)
        {
            string queryString = string.Format("Select Distinct PdbID2 From {0}HomoRepEntryAlign " + 
                " Where PdbID1 = '{1}';", ProtCidSettings.dataType, repEntry);
            DataTable homoEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
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
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
        public bool ExistDomainInterfaceCompInDb(int relSeqId, string pdbId1, string pdbId2)
        {
            string queryString = string.Format("Select * From {0} Where (RelSeqID = {1} AND " + 
                " ((PdbID1 = '{2}' AND PdbID2 = '{3}')) OR (PdbID1 = '{3}' AND PdbID2 = '{2}'));",
                DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp].TableName, relSeqId, pdbId1, pdbId2);
            DataTable interfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (interfaceCompTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region compare domain interfaces for big relation
        /// <summary>
        /// Compare each pair of representative entries in the relation 
        /// and also compare the representative entry and its homologous entries
        /// the domain interfaces are not precalculated due to the huge memory usage
        /// </summary>
        /// <param name="relSeqId"></param>
        public void CompareDomainInterfacesInBigRelation(int relSeqId)
        {
            bool resultNeedClosed = false;
            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults" + relSeqId.ToString() +  ".txt", true);
                entryCompResultWriter = new StreamWriter("EntryInterfaceCompResults" + relSeqId.ToString() + "_" + ".txt", true);
                logWriter = new StreamWriter("DomainInterfaceCompLog" + relSeqId.ToString() + ".txt", true);

                resultNeedClosed = true;
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString());
            ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
            noSimDomainInterfacesWriter.WriteLine(relSeqId.ToString());
            nonAlignDomainWriter.WriteLine(relSeqId.ToString());
            logWriter.WriteLine(relSeqId.ToString());

            /*  modified on August 18, 2009, 
             *  changed into pairwise compare representative entries and 
             *  compare each rep entry and its homologies, follow the same idea as chain interface
             *  not every pair of all entries in the relation
             *  should reduce the computation
             * */
    //        Hashtable repNoSimEntryHash = ReadNoSimEntryHash();  // for recovering the failure of the running
            Dictionary<string, string[]> repHomoEntryHash = GetRelationRepHomoEntries(relSeqId);
            List<string> repEntryList = new List<string> (repHomoEntryHash.Keys);
            repEntryList.Sort();
            string[] repEntries = new string[repEntryList.Count];
            repEntryList.CopyTo(repEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve domain interface files from inter-chain interface files and intra-chain interface files");
            Dictionary<string, string[]> needCompEntryHash = new Dictionary<string,string[]> ();
            List<string> needCompEntryList = new List<string> ();

            int entryPairCount = 0;
       //     ArrayList noSimEntryList = new ArrayList(); ;
            for (int i = 0; i < repEntries.Length; i++)
            {
                needCompEntryList = new List<string> ();
                string[] homoEntries = (string[])repHomoEntryHash[repEntries[i]];
                foreach (string homoEntry in homoEntries)
                {
                    if (ExistDomainInterfaceCompInDb(relSeqId, repEntries[i], homoEntry))
                    {
                        continue;
                    }
                
                    needCompEntryList.Add(homoEntry);
                    entryPairCount++;
                }  // temporary comment it out since the rep-homo pairs are already updated on Oct. 13, 2009.
                for (int j = i + 1; j < repEntries.Length; j++)
                {
                    if (ExistDomainInterfaceCompInDb(relSeqId, repEntries[i], repEntries[j]))
                    {
                        continue;
                    }
                    entryPairCount++;
                    needCompEntryList.Add(repEntries[j]);
                }
                // the domain interfaces for those entries needed to be compared
                if (needCompEntryList.Count > 0)
                {
                    string[] compEntries = new string[needCompEntryList.Count];
                    needCompEntryList.CopyTo(compEntries);
                    needCompEntryHash.Add(repEntries[i], compEntries);
                }
            }
            CompareEntryDomainInterfaces(relSeqId, needCompEntryHash, entryPairCount);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
            if (resultNeedClosed)
            {
                compResultWriter.Close();
                entryCompResultWriter.Close();
                logWriter.Close();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, List<string>> ReadNoSimEntryHash()
        {
            Dictionary<string, List<string>> repNoSimEntryHash = new Dictionary<string,List<string>> ();
            StreamReader dataReader = new StreamReader("NoSimilarDomainInterfacesEntryPairs_0.txt");
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('_');
                if (fields.Length == 2)
                {
                    if (repNoSimEntryHash.ContainsKey(fields[0]))
                    {
                        if (!repNoSimEntryHash[fields[0]].Contains(fields[1]))
                        {
                            repNoSimEntryHash[fields[0]].Add(fields[1]);
                        }
                    }
                    else
                    {
                        List<string> noSimEntryList = new List<string> ();
                        noSimEntryList.Add(fields[1]);
                        repNoSimEntryHash.Add(fields[0], noSimEntryList);
                    }
                }
            }
            dataReader.Close();
            return repNoSimEntryHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="needCompEntryHash"></param>
        /// <param name="entryDomainInterfaceHash"></param>
        /// <param name="entryPairCount"></param>
        private void CompareEntryDomainInterfaces(int relSeqId, Dictionary<string, string[]> needCompEntryHash, int entryPairCount)
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare  domain interfaces.");
            ProtCidSettings.progressInfo.totalOperationNum = entryPairCount;
            ProtCidSettings.progressInfo.totalStepNum = entryPairCount;
            ProtCidSettings.progressInfo.currentOperationNum = 0;
            ProtCidSettings.progressInfo.currentStepNum = 0;

            List<string> needCompEntryList = new List<string> (needCompEntryHash.Keys);
            needCompEntryList.Sort();
            foreach (string entry in needCompEntryList)
            {   
                // resume work
 /*               if (relSeqId == 3175 && string.Compare (entry, "1l0y") < 0)
                {
                    continue;
                }*/
                string[] compEntries = (string[])needCompEntryHash[entry];              
                CompareEntryDomainInterfaces(relSeqId, entry, compEntries);
                noSimDomainInterfacesWriter.Flush();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="entry"></param>
        /// <param name="origCompEntries"></param>
        /// <returns></returns>
        private string[] GetLeftCompEntries (int relSeqId, string entry, string[] origCompEntries)
        {
            string queryString = string.Format("Select Distinct PdbID2 From PfamDomainInterfaceComp Where RelSeqId = {0} AND PdbID1 = '{1}';", relSeqId, entry);
            DataTable comparedEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> leftCompEntryList = new List<string> (origCompEntries);
            string compEntry = "";
            foreach (DataRow compEntryRow in comparedEntryTable.Rows)
            {
                compEntry = compEntryRow["PdbID2"].ToString ();
                if (Array.IndexOf (origCompEntries, compEntry) > -1)
                {
                    leftCompEntryList.Remove(compEntry);
                }
            }
            string[] leftCompEntries = new string[leftCompEntryList.Count];
            leftCompEntryList.CopyTo(leftCompEntries);
            return leftCompEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="entry"></param>
        /// <param name="compEntries"></param>
        /// <param name="entryDomainInterfaceHash"></param>
        public void CompareEntryDomainInterfaces(int relSeqId, string entry, string[] compEntries)
        {
            DomainInterfacePairInfo[] pairCompInfos = null;
            DomainInterface[] domainInterfaces1 =
                        GetEntryUniqueDomainInterfaces(relSeqId, entry);
            if (domainInterfaces1 == null || domainInterfaces1.Length == 0)
            {
                ProtCidSettings.progressInfo.currentStepNum += compEntries.Length;
                ProtCidSettings.progressInfo.currentOperationNum += compEntries.Length;

                noSimDomainInterfacesWriter.WriteLine("No domain interfaces in entry " + entry);
                return;
            }
            foreach (string compEntry in compEntries)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = entry + "_" + compEntry;
                pairCompInfos = null;

                if (entry == compEntry)
                {
                    continue;
                }

                if (IsDomainInterfaceCompExist(relSeqId, entry, compEntry))
                {
                    continue;
                }

                try
                {
                    DomainInterface[] domainInterfaces2 = GetEntryUniqueDomainInterfaces(relSeqId, compEntry);
                    if (domainInterfaces2 == null)
                    {
                        noSimDomainInterfacesWriter.WriteLine("No domain interfaces in entry " + compEntry);
                        continue;
                    }
                    try
                    {
                        pairCompInfos = CompareDomainInterfaces(domainInterfaces1, domainInterfaces2);
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine("Compare Inter-chain domain interfaces for " + entry
                            + " and " + compEntry + " errors: " + ex.Message);
                        logWriter.Flush();
                    }
                    if (pairCompInfos == null || pairCompInfos.Length == 0)
                    {
                        noSimDomainInterfacesWriter.WriteLine(entry + "_" + compEntry);
                        continue;
                    }
                    try
                    {
                        AssignDomainInterfaceCompTable(relSeqId, entry, compEntry, pairCompInfos);
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine("Assign comparison of " + entry + "_" + compEntry + " to data table errors:  "
                                     + ex.Message);
                        logWriter.Flush();
                    }
                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, 
                        DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp]);
                    WriteCompResultToFile(DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp], compResultWriter);
                    DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp].Clear();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(entry + "_" + compEntry + " error: " + ex.Message);

                    logWriter.WriteLine("Compare " + entry + "_" + compEntry + " domain interfaces error:  "
                                     + ex.Message);
                    logWriter.Flush();
                }
            }
        }
        #endregion

        #region Assign interface comp info to table
        /// <summary>
		/// assign interface pair info into table
		/// </summary>
		/// <param name="relSeqId"></param>
		/// <param name="pdbId1"></param>
		/// <param name="pdbId2"></param>
		/// <param name="pairCompInfos"></param>
		private void AssignDomainInterfaceCompTable (int relSeqId, string pdbId1, string pdbId2, DomainInterfacePairInfo[] pairCompInfos)
		{
			foreach (DomainInterfacePairInfo pairInfo in pairCompInfos)
			{
                AssignDomainInterfaceCompTable(relSeqId, pdbId1, pdbId2, pairInfo);
			}
		}

        /// <summary>
        /// assign interface pair info into table
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="pairCompInfos"></param>
        private void AssignDomainInterfaceCompTable(int relSeqId, string pdbId1, string pdbId2, DomainInterfacePairInfo pairCompInfo)
        {
            if (pairCompInfo != null &&
               pairCompInfo.qScore >= AppSettings.parameters.contactParams.minQScore)
            {
                DataRow compRow =
                      DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp].NewRow();
                compRow["RelSeqID"] = relSeqId;
                compRow["PdbID1"] = pdbId1;
                compRow["PdbID2"] = pdbId2;
                compRow["DomainInterfaceID1"] = pairCompInfo.interfaceInfo1.domainInterfaceId;
                compRow["DomainInterfaceID2"] = pairCompInfo.interfaceInfo2.domainInterfaceId;
                compRow["QScore"] = pairCompInfo.qScore;
                compRow["Identity"] = pairCompInfo.identity;
                if (pairCompInfo.isInterface2Reversed)
                {
                    compRow["IsReversed"] = 1;
                }
                else
                {
                    compRow["IsReversed"] = 0;
                }
                DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp].Rows.Add(compRow);
            }
        }
		#endregion

        #region compare domain interfaces for each entry
        public void CompuateEntryUniqueDomainInterfaces()
        {
            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults_debug" + domainAlignType + ".txt", true);
                logWriter = new StreamWriter("CrystDomainInterfaceCompLog_debug" + domainAlignType + ".txt", true);
                entryCompResultWriter = new StreamWriter("EntryInterfaceCompResults_debug" + domainAlignType + ".txt", true);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare the domain interfaces for each entry.");

            string queryString = "Select Distinct RelSeqID From PfamDomainInterfaces;";
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString ());
                CompareRelationEntryUniqueDomainInterfaces(relSeqId);
            }
            compResultWriter.Close();
            logWriter.Close();
            entryCompResultWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done! ");
        }

        public void CompareRelationEntryUniqueDomainInterfaces(int relSeqId)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = relSeqId.ToString();

            string queryString = string.Format("Select Distinct PdbID From PfamDomainInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable relEntryTable = ProtCidSettings.protcidQuery.Query( queryString);


            ProtCidSettings.progressInfo.totalOperationNum = relEntryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = relEntryTable.Rows.Count;

            string pdbId = "";
            foreach (DataRow entryRow in relEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                DomainInterface[] uniqueDomainInterfaces = GetEntryUniqueDomainInterfaces(relSeqId, pdbId);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public DomainInterface[] GetEntryUniqueDomainInterfaces(int relSeqId, string pdbId)
        {
            DomainInterface[] uniqueDomainInterfaces = null;
            try
            {
                DomainInterface[] domainInterfaces =
                    domainInterfaceRetriever.GetDomainInterfacesFromFiles (pdbId, relSeqId, "cryst");
                uniqueDomainInterfaces =
                    GetUniqueEntryDomainInterfaces(relSeqId, pdbId, domainInterfaces);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get domain interfaces from " +
                    pdbId + " errors: " + ex.Message);
                logWriter.WriteLine("Get domain interfaces from " + pdbId + " errors: " + ex.Message);
                logWriter.Flush();
            }
            return uniqueDomainInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public DomainInterface[] GetEntryUniqueDomainInterfacesFromDb (int relSeqId, string pdbId)
        {
            DomainInterface[] uniqueDomainInterfaces = null;
            try
            {
                DomainInterface[] domainInterfaces =
                    domainInterfaceRetriever.GetDomainInterfacesFromFiles (pdbId, relSeqId, "cryst");
                uniqueDomainInterfaces = GetUniqueDomainInterfacesFromDb(relSeqId, pdbId, domainInterfaces);
                if (uniqueDomainInterfaces.Length == 0)
                {
                    uniqueDomainInterfaces = domainInterfaces;
                }
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get domain interfaces from " +
                    pdbId + " errors: " + ex.Message);
                logWriter.WriteLine("Get domain interfaces from " + pdbId + " errors: " + ex.Message);
                logWriter.Flush();
            }
            return uniqueDomainInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaces"></param>
        public DomainInterface[] GetUniqueEntryDomainInterfaces(int relSeqId, string pdbId, DomainInterface[] domainInterfaces)
        {
            if (domainInterfaces.Length <= 1)
            {
                return domainInterfaces;
            }
            DomainInterface[] uniqueDomainInterfaces = GetUniqueDomainInterfacesFromDb(relSeqId, pdbId, domainInterfaces);
            if (uniqueDomainInterfaces.Length > 0)
            {
                return uniqueDomainInterfaces;
            }
            List<int> uniqueDomainInterfaceList = new List<int> ();
            foreach (DomainInterface domainInterface in domainInterfaces)
            {
                uniqueDomainInterfaceList.Add(domainInterface.domainInterfaceId);
            }
            for (int i = 0; i < domainInterfaces.Length; i++)
            {
                if (!uniqueDomainInterfaceList.Contains(domainInterfaces[i].domainInterfaceId))
                {
                    continue;
                }
                for (int j = i + 1; j < domainInterfaces.Length; j++)
                {
                    if (!uniqueDomainInterfaceList.Contains(domainInterfaces[j].domainInterfaceId))
                    {
                        continue;
                    }

                    DomainInterfacePairInfo pairInfo =
                        CompareEntryDomainInterfaces(domainInterfaces[i], domainInterfaces[j]);
                    AssignEntryDomainInterfaceCompTable(relSeqId, pdbId, pairInfo);
                    if (pairInfo.qScore >= AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff)
                    /*&& AreDomainInterfacesSameDomainIds(domainInterfaces[i], domainInterfaces[j])*/
                    {
                        uniqueDomainInterfaceList.Remove(domainInterfaces[j].domainInterfaceId);
                    }
                }
            }
            uniqueDomainInterfaces = new DomainInterface[uniqueDomainInterfaceList.Count];
            int count = 0;
            foreach (int domainInterfaceId in uniqueDomainInterfaceList)
            {
                foreach (DomainInterface domainInterface in domainInterfaces)
                {
                    if (domainInterface.domainInterfaceId == domainInterfaceId)
                    {
                        uniqueDomainInterfaces[count] = domainInterface;
                        count++;
                    }
                }
            }
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, 
                DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.EntryDomainInterfaceComp]);
            WriteCompResultToFile(DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.EntryDomainInterfaceComp], entryCompResultWriter);
            DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.EntryDomainInterfaceComp].Clear();

            return uniqueDomainInterfaces;
        }

        /// <summary>
        /// the unique domain interfaces for the input entry 
        /// unique domain interfaces means with Q Score less than 0.90
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaces"></param>
        /// <returns></returns>
        private DomainInterface[] GetUniqueDomainInterfacesFromDb(int relSeqId, string pdbId, DomainInterface[] domainInterfaces)
        {
            string queryString = string.Format("Select * From {0}EntryDomainInterfaceComp " + 
                " Where RelSeqID = {1} AND PdbID = '{2}';", 
                ProtCidSettings.dataType, relSeqId, pdbId);
            DataTable entryDomainInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
    /*        string queryString = string.Format("Select * From {0}EntryDomainInterfaceComp " + 
                " Where RelSeqID = {1} AND PdbID = '{2}' AND QScore >= {3};", 
                ProtCidSettings.dataType, relSeqId, pdbId, AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff);
            DataTable entryDomainInterfaceCompTable = dbQuery.Query(queryString);*/
            if (entryDomainInterfaceCompTable.Rows.Count == 0)
            {
                 DomainInterface[] dbUniqueDomainInterfaces = new DomainInterface[0];
                 return dbUniqueDomainInterfaces;
            }
            List<int> removeDomainInterfaceIdList = new List<int> ();
            int domainInterfaceId = -1;
            double qScore = -1.0;
            foreach (DataRow interfaceCompRow in entryDomainInterfaceCompTable.Rows)
            {
                qScore = Convert.ToDouble(interfaceCompRow["QScore"].ToString ());
                if (qScore >= AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff)
                {
                    domainInterfaceId = Convert.ToInt32(interfaceCompRow["DomainInterfaceID2"].ToString());
                    if (!removeDomainInterfaceIdList.Contains(domainInterfaceId))
                    {
                        removeDomainInterfaceIdList.Add(domainInterfaceId);
                    }
                }
            }
            bool removed = false;
            List<DomainInterface> uniqueDomainInterfaceList = new List<DomainInterface> ();
      //      uniqueDomainInterfaces = new DomainInterface[domainInterfaces.Length - removeDomainInterfaceIdList.Count];
            foreach (DomainInterface domainInterface in domainInterfaces)
            {
                removed = false;
                foreach (int interfaceId in removeDomainInterfaceIdList)
                {
                    if (domainInterface.domainInterfaceId == interfaceId)
                    {
                        removed = true;
                        break;
                    }
                }
                if (! removed)
                {
                //    uniqueDomainInterfaces[count] = domainInterface;
                //    count++;
                    uniqueDomainInterfaceList.Add(domainInterface);
                }
            }
            return uniqueDomainInterfaceList.ToArray ();
        }
       
        /// <summary>
        /// assign interface pair info into table
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="pairCompInfos"></param>
        private void AssignEntryDomainInterfaceCompTable(int relSeqId, string pdbId, DomainInterfacePairInfo pairCompInfos)
        {
            if (pairCompInfos != null && //pairCompInfos.qScore > -1)
                pairCompInfos.qScore >= AppSettings.parameters.contactParams.minQScore)
            {
                DataRow compRow =
                      DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.EntryDomainInterfaceComp].NewRow();
                compRow["RelSeqID"] = relSeqId;
                compRow["PdbID"] = pdbId;
                compRow["DomainInterfaceID1"] = pairCompInfos.interfaceInfo1.domainInterfaceId;
                compRow["DomainInterfaceID2"] = pairCompInfos.interfaceInfo2.domainInterfaceId;
                if (pairCompInfos.interfaceInfo1.domainInterfaceId > pairCompInfos.interfaceInfo2.domainInterfaceId)
                {
                    compRow["DomainInterfaceID1"] = pairCompInfos.interfaceInfo2.domainInterfaceId;
                    compRow["DomainInterfaceID2"] = pairCompInfos.interfaceInfo1.domainInterfaceId;
                }
                compRow["QScore"] = pairCompInfos.qScore;
                if (pairCompInfos.isInterface2Reversed)
                {
                    compRow["IsReversed"] = 1;
                }
                else
                {
                    compRow["IsReversed"] = 0;
                }
                DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.EntryDomainInterfaceComp].Rows.Add(compRow);
            }
        }
        #endregion

        #region read/writer interface comp from/into file
        private DataTable WriterRelationInterfaceCompIntoFile(int relSeqId)
        {
            StreamWriter dataWriter = new StreamWriter("DomainInterfaceComp_" + relSeqId.ToString () + ".txt");
            string line = "";
            string queryString = string.Format("Select * From {0}DomainInterfaceComp " + 
                " Where RelSeqID = {1};", ProtCidSettings.dataType, relSeqId);
            DataTable dinterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            foreach (DataRow compRow in dinterfaceCompTable.Rows)
            {
                line = "";
                foreach (object item in compRow.ItemArray)
                {
                    line += (item.ToString().TrimEnd() + ",");
                }
                dataWriter.WriteLine(line.TrimEnd (','));
            }
            dataWriter.Close ();
            return dinterfaceCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private DataTable ReadRelationInterfaceCompDataFromFile(int relSeqId)
        {
            StreamReader dataReader = new StreamReader("DomainInterfaceComp_" + relSeqId.ToString() + ".txt");
            string line = "";
            string queryString = "Select First 1 * From " + DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp].TableName;
            DataTable dinterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            dinterfaceCompTable.Clear();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] items = line.Split(',');
                DataRow newRow = dinterfaceCompTable.NewRow();
                newRow.ItemArray = items;
                dinterfaceCompTable.Rows.Add(newRow);
            }
            dataReader.Close();
            return dinterfaceCompTable;
        }
        #endregion

        #region update domain interface comp
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
     //   public void UpdateEntryDomainInterfaceComp (string[] updateEntries)
        public void UpdateEntryDomainInterfaceComp (Dictionary<int, string[]> relationUpdateEntryDict)
        {
            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults_update.txt", true);
                logWriter = new StreamWriter("CrystDomainInterfaceCompLog_update.txt", true);
                entryCompResultWriter = new StreamWriter("EntryInterfaceCompResults_update.txt", true);
            }

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Cryst domain interface comp";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update cryst domain interface comparison.");
            ProtCidSettings.logWriter.WriteLine("Update cryst domain interface comparison.");
       
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Delete obsolete comparison info");
            ProtCidSettings.logWriter.WriteLine("Delete obsolete comparison info");
            DeleteObsDomainInterfaceCompInfo(relationUpdateEntryDict);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Domain Interface comp.");
            ProtCidSettings.logWriter.WriteLine("Update Domain Interface comp.");
            UpdateRelationDomainInterfaceComp(relationUpdateEntryDict);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Domain Interface comp for rep and homo entries.");
            ProtCidSettings.logWriter.WriteLine(DateTime.Today.ToShortDateString () + "\nUpdate Domain Interface comp for rep and homo entries.");
            CompareRepHomoEntryDomainInterfaces();
            ProtCidSettings.logWriter.WriteLine(DateTime.Today.ToShortDateString () + " Done!");
            ProtCidSettings.logWriter.Flush();

            compResultWriter.Close();
            logWriter.Close();
            entryCompResultWriter.Close();
            
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Add single-domain chain interfaces to domain interface comp");
            ProtCidSettings.logWriter.WriteLine("Add single-domain chain interfaces to domain interface comp");
            string[] updateEntries = GetUpdateEntries(relationUpdateEntryDict);
            SynchronizeDomainChainInterfaceComp(updateEntries);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relationUpdateEntryHash"></param>
        /// <returns></returns>
        private string[] GetUpdateEntries(Dictionary<int, string[]> relationUpdateEntryDict)
        {
            List<string> updateEntryList = new List<string> ();
            foreach (int relSeqId in relationUpdateEntryDict.Keys)
            {
                string[] entries = relationUpdateEntryDict[relSeqId];
                foreach (string entry in entries)
                {
                    if (!updateEntryList.Contains(entry))
                    {
                        updateEntryList.Add(entry);
                    }
                }
            }
            return updateEntryList.ToArray ();
        }
        /// <summary>
        /// update relations due to the change of some entries
        /// </summary>
        /// <param name="relationUpdateEntryHash">key: relSeqId, value: a list of entries to be updated</param>
        public void UpdateRelationDomainInterfaceComp(Dictionary<int, string[]> relationUpdateEntryDict)
        {
             Dictionary<string, string[]> needCompEntryHash = new Dictionary<string,string[]> ();
            int entryPairCount = 0;
            List<int> relationList = new List<int> (relationUpdateEntryDict.Keys);
            relationList.Sort();
            foreach (int relSeqId in relationList)
            {  
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString ());
                logWriter.WriteLine(relSeqId.ToString () + " processing. ");

                needCompEntryHash.Clear();

                entryPairCount = 0;
                string[] relationUpdateEntryList = relationUpdateEntryDict[relSeqId];

                List<string> updateRepEntryList = new List<string> ();
                foreach (string updateEntry in relationUpdateEntryList)
                {
                    // if not a representative entry, only need to be compared with its rep entry
                    if (!IsRepresentativeEntry(updateEntry))
                    {
                        string repEntry = GetRepEntryForHomoEntry(updateEntry);
                        List<string> needCompEntryList = new List<string> ();
                        needCompEntryList.Add(repEntry);
                        string[] needCompEntries = new string[needCompEntryList.Count];
                        needCompEntryList.CopyTo(needCompEntries);
                        entryPairCount++;
                        needCompEntryHash.Add(updateEntry, needCompEntries);
                    }
                    else
                    {
                        updateRepEntryList.Add(updateEntry);
                    }
                }

                if (updateRepEntryList.Count > 0)
                {
                    Dictionary<string, string[]> relationRepHomoEntryHash = GetRelationRepHomoEntries(relSeqId);
                    List<string> relationRepEntryList = new List<string> (relationRepHomoEntryHash.Keys);

                    List<string> notUpdatedRepEntryList = new List<string> (relationRepEntryList);
                    foreach (string updateRepEntry in updateRepEntryList)
                    {
                        notUpdatedRepEntryList.Remove(updateRepEntry);
                    }
                    /*  the new/updated rep entries need to be compared to 
                     *  1. each non-update rep entry in the relation 
                     *  2. its homologies
                     *  3. left update entries
                     * */
                    for (int i = 0; i < updateRepEntryList.Count; i++)
                    {
                        List<string> needCompEntryList = new List<string> (notUpdatedRepEntryList);
                        if (relationRepHomoEntryHash.ContainsKey(updateRepEntryList[i]))
                        {
                            needCompEntryList.AddRange((string[])relationRepHomoEntryHash[updateRepEntryList[i]]);
                        }
                        needCompEntryList.AddRange(updateRepEntryList.GetRange(i + 1, updateRepEntryList.Count - i - 1));
                        string[] needCompEntries = new string[needCompEntryList.Count];
                        needCompEntryList.CopyTo(needCompEntries);
                        needCompEntryHash.Add((string)updateRepEntryList[i], needCompEntries);
                        entryPairCount += needCompEntryList.Count;
                    }
                }
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving domain interfaces.");

                CompareEntryDomainInterfaces(relSeqId, needCompEntryHash, entryPairCount);

                logWriter.WriteLine(relSeqId + " updated.");
                logWriter.Flush();
            }            
        }
        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private Dictionary<int, List<string>> GetRelSeqIDs(string[] updateEntries)
        {
            Dictionary<int, List<string>> relationEntryHash = new Dictionary<int, List<string>>();
            foreach (string pdbId in updateEntries)
            {
                int[] entryRelSeqIDs = GetEntryRelSeqIDs(pdbId);
                foreach (int relSeqId in entryRelSeqIDs)
                {
                    if (relationEntryHash.ContainsKey(relSeqId))
                    {
                        relationEntryHash[relSeqId].Add(pdbId);
                    }
                    else
                    {
                        List<string> entryList = new List<string> ();
                        entryList.Add(pdbId);
                        relationEntryHash.Add(relSeqId, entryList);
                    }
                }
            }
            return relationEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="homoEntry"></param>
        /// <returns></returns>
        private string GetRepEntryForHomoEntry(string homoEntry)
        {
            string queryString = string.Format("Select PdbID1 From {0}HomoRepEntryAlign " + 
                " Where PdbID2 = '{1}';", ProtCidSettings.dataType, homoEntry);
            DataTable repEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (repEntryTable.Rows.Count > 0)
            {
                return repEntryTable.Rows[0]["PdbID1"].ToString();
            }
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetEntryRelSeqIDs(string pdbId)
        {
            string queryString = string.Format("Select Distinct RelSeqID From {0}DomainInterfaces " + 
                " Where PdbID = '{1}';", ProtCidSettings.dataType, pdbId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] relSeqIDs = new int[relSeqIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqIDs[count] = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString ());
                count++;
            }
            return relSeqIDs;
        }
  
        /// <summary>
        /// update domain interface comp data due to bugs
        /// </summary>
        /// <param name="relSeqId"></param>
        public void UpdateDomainInterfaceCompInRelation(int relSeqId, string[] relationUpdateEntries)
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString());
            ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();

            noSimDomainInterfacesWriter.WriteLine(relSeqId.ToString());
            logWriter.WriteLine(relSeqId.ToString());

            string[] relationEntries = GetRelationEntries(relSeqId); // include the update entries
            Dictionary<string, DomainInterface[]> entryDomainInterfaceHash = new Dictionary<string,DomainInterface[]> ();
            foreach (string entry in relationEntries)
            {
                DomainInterface[] entryUniqueDomainInterfaces = GetEntryUniqueDomainInterfaces(relSeqId, entry);
                entryDomainInterfaceHash.Add(entry, entryUniqueDomainInterfaces);
            }

            ProtCidSettings.progressInfo.totalStepNum = relationUpdateEntries.Length * relationEntries.Length;
            ProtCidSettings.progressInfo.totalOperationNum = relationUpdateEntries.Length * relationEntries.Length;

            foreach (string updateEntry in relationUpdateEntries)
            {
                CompareEntryDomainInterfaces(relSeqId, updateEntry, relationEntries, entryDomainInterfaceHash);
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Done!!");
        }

        public string[] GetRelationEntries(int relSeqId)
        {
            List<string> pdbList = new List<string> ();
            string queryString = string.Format("Select Distinct PdbID From {0}DomainInterfaces " +
                " Where RelSeqID = {1} Order BY PdbID;",
                    ProtCidSettings.dataType, relSeqId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            foreach (DataRow interfaceRow in domainInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
               /*
                if (!IsEntryAntibody(pdbId))
                {
                    pdbList.Add(pdbId);
                }*/
                pdbList.Add(pdbId);
            }
            pdbList.Sort();
            string[] pdbIds = new string[pdbList.Count];
            pdbList.CopyTo(pdbIds);
            return pdbIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> GetRelationRepHomoEntries(int relSeqId)
        {
            string[] relationEntries = GetRelationEntries(relSeqId);
            Dictionary<string, string[]> repHomoEntryHash = DivideEntriesIntoRepHomoEntries(relationEntries);
            return repHomoEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        public string[] GetRelationRepEntries(int relSeqId)
        {
            string[] relationEntries = GetRelationEntries(relSeqId);
            List<string> repEntryList = new List<string> ();
            foreach (string pdbId in relationEntries)
            {
                if (IsRepresentativeEntry(pdbId))
                {
                    repEntryList.Add(pdbId);
                }
            }
            return repEntryList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
        private bool ExistDomainInterfacesComp(string pdbId1, string pdbId2, DataTable existInterfaceCompTable)
        {
            DataRow[] compRows = GetInterfaceCompRows(pdbId1, pdbId2, existInterfaceCompTable);
            if (compRows.Length > 0)
            {
                foreach (DataRow compRow in compRows)
                {
                    DataRow newRow =
                        DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp].NewRow();
                    newRow.ItemArray = compRow.ItemArray;
                    DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp].Rows.Add(newRow);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        /// <param name="domainInterfaceCompTable"></param>
        /// <returns></returns>
        private DataRow GetInterfaceCompRow(string pdbId1, int domainInterfaceId1,
            string pdbId2, int domainInterfaceId2, DataTable domainInterfaceCompTable)
        {
            string selectString = string.Format("PdbID1 = '{0}' AND DomainInterfaceID1 = '{1}' " +
                " AND PdbID2 = '{2}' AND DomainInterfaceID2 = '{3}'",
                pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2);
            DataRow[] compRows = domainInterfaceCompTable.Select(selectString);
            if (compRows.Length == 0)
            {
                selectString = string.Format("PdbID1 = '{0}' AND DomainInterfaceID1 = '{1}' " +
                    " AND PdbID2 = '{2}' AND DomainInterfaceID2 = '{3}'",
                    pdbId2, domainInterfaceId2, pdbId1, domainInterfaceId1);
                compRows = domainInterfaceCompTable.Select(selectString);
                ReverseDomainInterfaceCompRows(compRows);
            }
            if (compRows.Length > 0)
            {
                return compRows[0];
            }
            return null;
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceCompTable"></param>
        /// <returns></returns>
        private DataRow[] GetInterfaceCompRows(string pdbId1, string pdbId2, DataTable domainInterfaceCompTable)
        {
            string selectString = string.Format("PdbID1 = '{0}' AND PdbID2 = '{1}'", pdbId1, pdbId2);
            DataRow[] compRows = domainInterfaceCompTable.Select(selectString);
            if (compRows.Length == 0)
            {
                selectString = string.Format("PdbID1 = '{0}' AND PdbID2 = '{1}'", pdbId2, pdbId1);
                compRows = domainInterfaceCompTable.Select(selectString);
                ReverseDomainInterfaceCompRows(compRows);
            }
            return compRows;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="compRow"></param>
        private void ReverseDomainInterfaceCompRow(DataRow compRow)
        {
            object temp = compRow["PdbID1"];
            compRow["PdbID1"] = compRow["PdbID2"];
            compRow["PdbID2"] = temp;
            temp = compRow["DomainInterfaceID1"];
            compRow["DomainInterfaceID1"] = compRow["DomainInterfaceID2"];
            compRow["DomainInterfaceID2"] = temp;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="compRows"></param>
        private void ReverseDomainInterfaceCompRows(DataRow[] compRows)
        {
            foreach (DataRow compRow in compRows)
            {
                ReverseDomainInterfaceCompRow(compRow);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaces"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private DomainInterface GetDomainInterface(DomainInterface[] domainInterfaces, int domainInterfaceId)
        {
            foreach (DomainInterface domainInterface in domainInterfaces)
            {
                if (domainInterface.domainInterfaceId == domainInterfaceId)
                {
                    return domainInterface;
                }
            }
            return null;
        }
        #endregion

        #region writer results to files
        /// <summary>
        /// 
        /// </summary>
        /// <param name="compResultTable"></param>
        /// <param name="dataWriter"></param>
        public void WriteCompResultToFile(DataTable compResultTable, StreamWriter dataWriter)
        {
            string dataLine = "";
            foreach (DataRow compRow in compResultTable.Rows)
            {
                dataLine = "";
                foreach (object item in compRow.ItemArray)
                {
                    dataLine += (item.ToString() + ",");
                }
                dataWriter.WriteLine(dataLine.TrimEnd (','));
            }
            dataWriter.Flush();
        }
        #endregion

        #region import domain interface comp data into DB
        /// <summary>
        /// 
        /// </summary>
        public void ImportDomainInterfaceCompDataToDb()
        {
            //    DbBuilder.dbConnect.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
        //            ProtCidSettings.dirSettings.dbPath;
            ProtCidSettings.dataType = "pfam";

    //        string domainInterfaceCompDir = @"F:\domainintefacecomp";
     //       string[] dataFiles = Directory.GetFiles(domainInterfaceCompDir);
            string[] dataFiles = { "InterfaceCompResults_update0.txt", "EntryInterfaceCompResults_update0.txt" };
            string[] tableNames = {"PfamDomainInterfaceComp",  "PfamEntryDomainInterfaceComp"};

            StreamWriter importlogWriter = new StreamWriter("improtLog.txt", true);
            importlogWriter.WriteLine(DateTime.Today .ToShortDateString ());

            string fileName = "";
            foreach (string dataFile in dataFiles)
            {
                if (dataFile.IndexOf("JobList") > -1)
                {
                    continue;
                }
               
                importlogWriter.WriteLine(dataFile);
                fileName = GetDataFileName(dataFile) ;
                if (fileName.IndexOf("log") > -1)
                {
                    continue;
                }
                if (fileName.IndexOf("interfacecomp") > -1 && fileName.IndexOf("entry") > -1)
                {
                    ImportDomainInterfaceCompDataToDb (dataFile, tableNames[1], importlogWriter);
                }
                else if (fileName.IndexOf("interfacecomp") > -1)
                {
                    ImportDomainInterfaceCompDataToDb(dataFile, tableNames[0], importlogWriter);
                }
            }
            importlogWriter.Close();
        }

        public void DeleteObsRelationInterfaceCompData(int[] relSeqIDs)
        {
            if (notNeedCompEntries == null)
            {
                notNeedCompEntries = GetUpdateDomainInterfaceChangedEntries();
            }
            Dictionary<int, List<string>> relationNotNeedCompEntryHash = GetRelSeqIDs(notNeedCompEntries);
            StreamWriter compDataWriter = null;

            foreach (int relSeqId in relSeqIDs)
            {
                compDataWriter = new StreamWriter("neededCompData" + relSeqId.ToString() + ".txt");
                //      DeleteRelationRepEntryDomainInterfaceCompData(relSeqId);
                DeleteRelationInterfaceCompData(relSeqId, relationNotNeedCompEntryHash[relSeqId].ToArray (), compDataWriter);
            }
        }

        private void DeleteRelationInterfaceCompData(int relSeqId, string[] notNeedDeleteEntries, 
            StreamWriter compDataWriter)
        {
            string queryString = string.Format("Select * From {0} " +
                 "WHere RelSeqID = {1} AND (PdbID1 In ({2}) OR PdbID2 IN ({2}));", 
                 DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp].TableName, 
                 relSeqId, ParseHelper.FormatSqlListString (notNeedDeleteEntries));
            DataTable notNeedDeleteCompDataTable = ProtCidSettings.protcidQuery.Query( queryString);
            notNeedDeleteCompDataTable.TableName = DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp].TableName;
            string dataLine = "";
            foreach (DataRow dataRow in notNeedDeleteCompDataTable.Rows)
            {
                dataLine = "";
                foreach (object item in dataRow.ItemArray)
                {
                    dataLine += (item.ToString() + ",");
                }
                compDataWriter.WriteLine(dataLine.TrimEnd (','));
            }
            compDataWriter.Close();
          
            DeleteRelationInterfaceCompData(relSeqId);

            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, notNeedDeleteCompDataTable);
        } 
        /// <summary>
        /// delete the comparison info for rep entries in the relation
        /// </summary>
        /// <param name="relSeqId"></param>
        private void DeleteRelationRepEntryDomainInterfaceCompData(int relSeqId)
        {
            string[] repEntries = null;
            string[] homoEntries = null;
            if (notNeedCompEntries == null)
            {
                notNeedCompEntries = GetUpdateDomainInterfaceChangedEntries();
            }
            Dictionary<string, string[]> repHomoEntryHash = GetRepHomoEntryHash(relSeqId, out repEntries, out homoEntries);
            for (int i = 0; i < repEntries.Length; i++)
            {
                if (Array.IndexOf(notNeedCompEntries, repEntries[i]) > -1)
                {
                    continue;
                }
                for (int j = i + 1; j < repEntries.Length; j++)
                {
                    if (Array.IndexOf(notNeedCompEntries, repEntries[j]) > -1)
                    {
                        continue;
                    }
                    DeleteDomainInterfaceComp(relSeqId, repEntries[i], repEntries[j]);
                }
            }
        }
  
        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceCompFile">"InterfaceCompResultsXX.txt"</param>
        /// <returns></returns>
        private int GetRelSeqIdFromResultFileName(string interfaceCompFile)
        {
            int extIndex = interfaceCompFile.IndexOf(".txt");
            int fileIndex = interfaceCompFile.LastIndexOf ("\\");
            string fileName = interfaceCompFile.Substring(fileIndex + 1, extIndex - fileIndex - 1);
            string relSeqIdString = fileName.Remove(0, "InterfaceCompResults".Length);
            return Convert.ToInt32 (relSeqIdString);
        }

        private string GetDataFileName(string dataFile)
        {
            int fileIdx = dataFile.LastIndexOf("\\");
            string fileName = dataFile.Substring(fileIdx + 1, dataFile.Length - fileIdx - 1);
            return fileName.ToLower();
        }

        public void ImportDomainInterfaceCompDataToDb(string dataFileName, string tableName, StreamWriter importlogWriter)
        {
            string lastLine = "";
            bool dataStart = true;
            if (File.Exists("lastLine.txt"))
            {
                StreamReader lastLineReader = new StreamReader("lastLine.txt");
                lastLine = lastLineReader.ReadLine();
                lastLineReader.Close();
                dataStart = false;
            }

            StreamReader dataReader = new StreamReader(dataFileName);
            string line = "";
            string queryString = string.Format("Select First 1 * From {0};", tableName);
            DataTable interfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            interfaceCompTable.TableName = tableName;
            interfaceCompTable.Clear();
            DataRow dataRow = interfaceCompTable.NewRow();
            char delimitor = ',';
            string[] items = null;
   //         int relSeqId = -1;
   //         int preRelSeqId = -1;
            while ((line = dataReader.ReadLine()) != null)
            {
                try
                {
                    if (line == "")
                    {
                        continue;
                    }
                    line = line.TrimEnd(',');
                    if (line == lastLine)
                    {
                        dataStart = true;
                    }
                    if (!dataStart)
                    {
                        continue;
                    }

                    if (line.IndexOf("RELSEQID") > -1)
                    {
                        delimitor = ' ';
                        continue;
                    }
                    if (line.IndexOf("====") > -1)
                    {
                        continue;
                    }
                    if (delimitor == ' ')
                    {
                        items = AuxFuncLib.ParseHelper.SplitPlus(line, delimitor);
                    }
                    else
                    {
                        items = line.Split(',');
                    }
            /*        relSeqId = Convert.ToInt32(items[0]);
                    if (relSeqId != preRelSeqId)
                    {
                        DeleteRelationInterfaceCompData(relSeqId);
                    }*/
                    dataRow.ItemArray = items;
                    dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, dataRow);
              //      preRelSeqId = relSeqId;
                }
                catch
                {
                    importlogWriter.WriteLine(line);
                    StreamWriter lastLineWriter = new StreamWriter("lastLine.txt");
                    lastLineWriter.WriteLine(line);
                    lastLineWriter.Close();
                    break;
                }
            }
            dataReader.Close();
        }

        public void RemoveDuplicateDataFromDomainInterfaceComp()
        {
    /*        StreamWriter dataWriter = new StreamWriter("PfamDomainInterfaceComp869.txt");
            string queryString = "Select Distinct * From PfamDomainInterfaceComp Where RelSeqID = 869;";
            DataTable domainInterfaceCompTable = dbQuery.Query(queryString);
            string line = "";
            foreach (DataRow compRow in domainInterfaceCompTable.Rows)
            {
                line = "";
                foreach (object item in compRow.ItemArray)
                {
                    line += (item.ToString() + ",");
                }
                dataWriter.WriteLine(line.TrimEnd (','));
            }
            dataWriter.Close();
            
            domainInterfaceCompTable.TableName = "PFamDomainInterfaceComp";
            string deleteString = "Delete From PfamDomainInterfaceComp Where RelSeqID = 869;";
            dbQuery.Query(deleteString);*/

            StreamReader dataReader = new StreamReader("PfamDomainInterfaceComp869.txt");
            string line = "";
            string preLine = "";
            string lastLine = "869,1zsl,2,2a7c,1,0.02008856,0.05736311";
            bool dataStart = false;
            string queryString = "Select First 1 * From PfamDomainInterfaceComp;";
            DataTable interfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            interfaceCompTable.TableName = "PfamDomainInterfaceComp";
            interfaceCompTable.Clear();
            DataRow dataRow = interfaceCompTable.NewRow();
            try
            {
                while ((line = dataReader.ReadLine()) != null)
                {
                    if (line == lastLine)
                    {
                        dataStart = true;
                    }
                    if (!dataStart)
                    {
                        continue;
                    }
                    if (preLine != line)
                    {
                        string[] items = line.Split(',');
                        dataRow.ItemArray = items;
                        dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, dataRow);
                        preLine = line;
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message;
            }
            finally
            {
                dataReader.Close();
            }
        }
        #endregion

        #region update 
        #region missing domain interface comparison
        /// <summary>
        /// 
        /// </summary>
        public void CompareMissingDomainSgInterfaces()
        {
            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults.txt");
                logWriter = new StreamWriter("DomainInterfaceCompLog.txt");
                entryCompResultWriter = new StreamWriter("EntryInterfaceCompResults.txt");
            }

            string queryString = string.Format ("Select Distinct RelSeqID From {0}DomainInterfaces;", ProtCidSettings.dataType);
            DataTable relSeqTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = -1;

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare " + ProtCidSettings.dataType + " domain interfaces.");
            ProtCidSettings.progressInfo.currentOperationLabel = "Domain Interface Comp";

            foreach (DataRow relSeqRow in relSeqTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqRow["RelSeqID"].ToString());
           
                CompareMisssingDomainInterfacesInRelation (relSeqId);
            }
            DomainInterfaceBuilder.nonAlignDomainsWriter.Close();
            nonAlignDomainWriter.Close();
            noSimDomainInterfacesWriter.Close();
            logWriter.Close();
            compResultWriter.Close();
            entryCompResultWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }
        /// <summary>
        /// this is function is to make up those entries which are representative in homoSeqInfo
        /// but not in sginterfaces. Since the first time, 
        /// I only retrieve the domain interfaces for those entries in sginterfaces. 
        /// But then I think those domain interfaces from homoseqinfo but not in sginterfaces entries 
        /// must be included. 
        /// </summary>
        /// <param name="relSeqId"></param>
        public void CompareMisssingDomainInterfacesInRelation(int relSeqId)
        {
            bool resultNeedClosed = false;
            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults" + relSeqId.ToString() + ".txt");
                entryCompResultWriter = new StreamWriter("EntryInterfaceCompResults" + relSeqId.ToString() + ".txt");
                logWriter = new StreamWriter("DomainInterfaceCompLog" + relSeqId.ToString() + ".txt");

                resultNeedClosed = true;
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString());
            ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
            noSimDomainInterfacesWriter.WriteLine(relSeqId.ToString());
            nonAlignDomainWriter.WriteLine(relSeqId.ToString());
            logWriter.WriteLine(relSeqId.ToString());

            List<string> pdbList = new List<string> ();
           
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve domain interface files from inter-chain interface files and intra-chain interface files");
            Dictionary<string, DomainInterface[]> entryDomainInterfaceHash = new Dictionary<string,DomainInterface[]> ();
            Dictionary<string, string[]> needCompEntryHash =new Dictionary<string,string[]> ();
            List<string> needCompEntryList = new List<string> ();

            string[] entriesInSg = null;
            string[] entriesNotInSg = null;
            SplitEntryInOrNotInSgInterfaces(relSeqId, out entriesInSg, out entriesNotInSg);

            if (entriesNotInSg.Length == 0)
            {
                return;
            }
            if (relSeqId == 839)
            {
                List<string> realCompEntryList = null;
                for (int i = 0; i < entriesNotInSg.Length; i++)
                {
                    needCompEntryList = new List<string> ();
                    realCompEntryList = new List<string> ();
                    needCompEntryList.AddRange(entriesInSg);
                    needCompEntryList.AddRange(entriesNotInSg);
                    needCompEntryList.Remove(entriesNotInSg[i]);
                    
                    foreach (string compEntry in needCompEntryList)
                    {
                        if (ExistDomainInterfaceCompInDb(relSeqId, entriesNotInSg[i], compEntry))
                        {
                            continue;
                        }
                        realCompEntryList.Add(compEntry);
                    }
                    if (realCompEntryList.Count > 0)
                    {
                        string[] compEntries = new string[realCompEntryList.Count];
                        realCompEntryList.CopyTo(compEntries);
                        needCompEntryHash.Add(entriesNotInSg[i], compEntries);
                    }
                }
            }
            else
            {
                for (int i = 0; i < entriesNotInSg.Length; i++)
                {
                    needCompEntryList = new List<string> ();
                    needCompEntryList.AddRange(entriesInSg);
                    needCompEntryList.AddRange(entriesNotInSg);
                    needCompEntryList.Remove(entriesNotInSg[i]);
                    string[] compEntries = new string[needCompEntryList.Count];
                    needCompEntryList.CopyTo(compEntries);
                    needCompEntryHash.Add(entriesNotInSg[i], compEntries);
                }
            }

            GetEntryUniqueInterfaces(relSeqId, entriesNotInSg, ref entryDomainInterfaceHash);
            GetEntryUniqueInterfaces(relSeqId, entriesInSg, ref entryDomainInterfaceHash);

            int entryPairCount = entriesInSg.Length * entriesNotInSg.Length;

            CompareEntryDomainInterfaces(relSeqId, needCompEntryHash, entryDomainInterfaceHash, entryPairCount);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
            if (resultNeedClosed)
            {
                compResultWriter.Close();
                entryCompResultWriter.Close();
                logWriter.Close();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="entries"></param>
        /// <param name="entryDomainInterfaceHash"></param>
        private void GetEntryUniqueInterfaces(int relSeqId, string[] entries, ref Dictionary<string, DomainInterface[]> entryDomainInterfaceHash)
        {
            foreach (string entry in entries)
            {
                try
                {
                    ProtCidSettings.progressInfo.currentFileName = entry;
                    DomainInterface[] domainInterfaces =
                        domainInterfaceRetriever.GetDomainInterfacesFromFiles (entry, relSeqId, "cryst");
                    DomainInterface[] uniqueDomainInterfaces =
                        GetUniqueEntryDomainInterfaces(relSeqId, entry, domainInterfaces);
                    entryDomainInterfaceHash.Add(entry, uniqueDomainInterfaces);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get domain interfaces from " +
                        entry + " errors: " + ex.Message);
                    logWriter.WriteLine("Get domain interfaces from " + entry + " errors: " + ex.Message);
                    logWriter.Flush();
                }
            }
        }
        /// <summary>
        /// to find the representative entries which are not in sginterfaces table
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="entriesInSg"></param>
        /// <param name="entriesNotInSg"></param>
        private void SplitEntryInOrNotInSgInterfaces(int relSeqId, out string[] entriesInSg, out string[] entriesNotInSg)
        {
            List<string> entryInSgList = new List<string>();
            List<string> entryNotInSgList = new List<string>();
            string queryString = string.Format("Select Distinct PdbID From {0}DomainInterfaces Where RelSeqID = {1} ORDER BY PdbID;",
                   ProtCidSettings.dataType, relSeqId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            foreach (DataRow interfaceRow in domainInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                /*   if (! IsEntryAntibody(pdbId))
                   {*/
                if (IsSgRepEntry(pdbId))
                {
                    if (IsSgEntry(pdbId))
                    {
                        entryInSgList.Add(pdbId);
                    }
                    else
                    {
                        entryNotInSgList.Add(pdbId);
                    }
                }
            }
            entriesInSg = new string[entryInSgList.Count];
            entryInSgList.CopyTo(entriesInSg);
            entriesNotInSg = new string[entryNotInSgList.Count];
            entryNotInSgList.CopyTo(entriesNotInSg);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsSgEntry(string pdbId)
        {
            string queryString = string.Format("Select * From {0}SgInterfaces Where PdbID = '{1}';", ProtCidSettings.dataType, pdbId);
            DataTable sgInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (sgInterfaceTable.Rows.Count > 0)
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
        private bool IsSgRepEntry(string pdbId)
        {
            string queryString = string.Format("Select * From {0}HomoSeqInfo Where PdbID = '{1}';", ProtCidSettings.dataType, pdbId);
            DataTable repEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (repEntryTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region representative entry and homologous entries 
        string[] notNeedCompEntries = null;
        /// <summary>
        /// 
        /// </summary>
        public void CompareRepHomoEntryDomainInterfaces()
        {
            Dictionary<int, string[]> relNoCompHomoEntryDict = GetMissingDomainInterfaceCompRepHomoRelDict ();
            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults_RepHomo.txt");
                logWriter = new StreamWriter("DomainInterfaceCompLog_RepHomo.txt");
                entryCompResultWriter = new StreamWriter("EntryInterfaceCompResults_RepHomo.txt");
            }
        /*     string queryString = string.Format ("Select Distinct RelSeqID From {0}DomainInterfaces;", ProtCidSettings.dataType);
            DataTable relSeqTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = -1;

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare " + ProtCidSettings.dataType + " domain interfaces.");
            ProtCidSettings.progressInfo.currentOperationLabel = "Domain Interface Comp";

           foreach (DataRow relSeqRow in relSeqTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqRow["RelSeqID"].ToString());
               
                CompareRepHomoDomainInterfacesInRelation(relSeqId);
            }*/

            UpdateRelationDomainInterfaceComp(relNoCompHomoEntryDict);
            
      /*      DomainInterfaceBuilder.nonAlignDomainsWriter.Close();
            nonAlignDomainWriter.Close();
            noSimDomainInterfacesWriter.Close();
            logWriter.Close();
            compResultWriter.Close();
            entryCompResultWriter.Close();*/
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<int, string[]> GetMissingDomainInterfaceCompRepHomoRelDict ()
        {
            string[] noCompHomoEntries = GetMissingDomainInterfaceCompHomoEntries();
            Dictionary<int, string[]> noDCompRelHomoEntryDict = GetRelationEntryHash(noCompHomoEntries);
            return noDCompRelHomoEntryDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetMissingDomainInterfaceCompHomoEntries ()
        {
            string noCompHomoEntryFile = "NoDomainInterfaceCompHomoEntries.txt";
            List<string> noCompHomoEntryList = new List<string>();
            if (File.Exists(noCompHomoEntryFile))
            {
                StreamReader dataReader = new StreamReader(noCompHomoEntryFile);
                string line = "";
                while ((line = dataReader.ReadLine ()) != null)
                {
                    noCompHomoEntryList.Add(line);
                }
                dataReader.Close();
            }
            else
            {
                StreamWriter dataWriter = new StreamWriter(noCompHomoEntryFile);
                string queryString = "Select Distinct PdbID1 From PfamHomoRepEntryAlign;";
                DataTable repEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
                queryString = "Select Distinct PdbID1, PdbID2 From PfamHomoRepEntryAlign;";
                DataTable repHomoEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
                string repPdb = "";
                string homoPdb = "";

                foreach (DataRow repRow in repEntryTable.Rows)
                {
                    repPdb = repRow["PdbID1"].ToString();
                    DataRow[] homoRows = repHomoEntryTable.Select(string.Format("PdbID1 = '{0}'", repPdb));
                    foreach (DataRow homoRow in homoRows)
                    {
                        homoPdb = homoRow["PdbID2"].ToString();
                        if (!IsEntryDomainInterfaceCompExist(repPdb, homoPdb))
                        {
                            noCompHomoEntryList.Add(homoPdb);
                            dataWriter.WriteLine(homoPdb);
                        }
                    }
                }
                dataWriter.Close();
            }
            string[] noCompEntries = new string[noCompHomoEntryList.Count];
            noCompHomoEntryList.CopyTo(noCompEntries);
            return noCompEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        private Dictionary<int, string[]> GetRelationEntryHash (string[] entries)
        {
            string queryString = "";
            Dictionary<int, List<string>> relEntryDict = new Dictionary<int,List<string>> ();
            int relSeqId = 0;
            string pdbId = "";
            for (int i = 0; i < entries.Length; i += 100)
            {
                string[] subEntries = ParseHelper.GetSubArray(entries, i, 100);
                queryString = string.Format("Select Distinct RelSeqID, PdbID From PfamDomainInterfaces Where PdbID IN ({0});", 
                    ParseHelper.FormatSqlListString (subEntries));
                DataTable relEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
               
                foreach (DataRow entryRow in relEntryTable.Rows)
                {
                    relSeqId = Convert.ToInt32(entryRow["RelSeqID"].ToString ());
                    pdbId = entryRow["PdbID"].ToString ();
                    if (relEntryDict.ContainsKey(relSeqId))
                    {
                        if (! relEntryDict[relSeqId].Contains(pdbId))
                        {
                            relEntryDict[relSeqId].Add(pdbId);
                        }
                    }
                    else
                    {
                        List<string> relEntryList = new List<string>();
                        relEntryList.Add(pdbId);
                        relEntryDict.Add(relSeqId, relEntryList);
                    }
                }
            }
            StreamWriter dataWriter = new StreamWriter("NoCompRepHomoEntriesRelations.txt");
            Dictionary<int, string[]> updateRelEntryDict = new Dictionary<int, string[]>();
            foreach (int lsRelSeqId in relEntryDict.Keys)
            {
                updateRelEntryDict.Add(lsRelSeqId, relEntryDict[lsRelSeqId].ToArray());

                dataWriter.WriteLine(lsRelSeqId.ToString() + " " + ParseHelper.FormatStringFieldsToString(relEntryDict[lsRelSeqId].ToArray()));
            }
            dataWriter.Close();
            return updateRelEntryDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
        private bool IsEntryDomainInterfaceCompExist (string pdbId1, string pdbId2)
        {
           string queryString = string.Format ("Select * From PfamDomainInterfaceComp " + 
               " Where (PdbID1 = '{0}' AND PdbID2 = '{1}') OR (PdbID1 = '{1}' AND PdbID2 = '{0}');", pdbId1, pdbId2);
           DataTable domainInterfaceCompTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (domainInterfaceCompTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
       }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        private void CompareRepHomoDomainInterfacesInRelation(int relSeqId)
        {
            bool resultNeedClosed = false;
            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults" + relSeqId.ToString() + ".txt");
                entryCompResultWriter = new StreamWriter("EntryInterfaceCompResults" + relSeqId.ToString() + ".txt");
                logWriter = new StreamWriter("DomainInterfaceCompLog" + relSeqId.ToString() + ".txt");

                resultNeedClosed = true;
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString());
            ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
            noSimDomainInterfacesWriter.WriteLine(relSeqId.ToString());
            nonAlignDomainWriter.WriteLine(relSeqId.ToString());
            logWriter.WriteLine(relSeqId.ToString());

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve domain interface files from inter-chain interface files and intra-chain interface files");
            Dictionary<string, DomainInterface[]> entryDomainInterfaceHash = new Dictionary<string,DomainInterface[]> ();
            Dictionary<string, string[]> needCompEntryHash = new Dictionary<string,string[]> (); // representative entry and its homologous entries in same crystal form
            string[] repEntries = null;
            string[] homoEntries = null;

            needCompEntryHash = GetRepHomoEntryHash(relSeqId, out repEntries, out homoEntries);

            // delete the original comparison data
            DeleteRepHomoDomainInterfaceComp(relSeqId, needCompEntryHash);

            List<string> compRepEntryList = new List<string>(needCompEntryHash.Keys);
            string[] compRepEntries = new string[compRepEntryList.Count];
            compRepEntryList.CopyTo (compRepEntries);
            GetEntryUniqueInterfaces(relSeqId, compRepEntries, ref entryDomainInterfaceHash);
            GetEntryUniqueInterfaces(relSeqId, homoEntries, ref entryDomainInterfaceHash);

            int entryPairCount = 0;
            foreach (string repEntry in needCompEntryHash.Keys)
            {
                string[] entryHomoEntries = (string[])needCompEntryHash[repEntry];
                entryPairCount += entryHomoEntries.Length;
            }

            CompareEntryDomainInterfaces(relSeqId, needCompEntryHash, entryDomainInterfaceHash, entryPairCount);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
            if (resultNeedClosed)
            {
                compResultWriter.Close();
                entryCompResultWriter.Close();
                logWriter.Close();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="repEntries">all representative entris in the relation</param>
        /// <param name="homoEntries">all homologous entries with same CF in the relation</param>
        /// <returns>key: repEntry, value: a list of homol</returns>
        private Dictionary<string, string[]> GetRepHomoEntryHash(int relSeqId, out string[] repEntries, out string[] homoEntries)
        {
            string queryString = string.Format("Select Distinct PdbID From {0}DomainInterfaces " + 
                " Where RelSeqId = {1};", ProtCidSettings.dataType, relSeqId);
            DataTable relEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            string repPdbId = "";
            Dictionary<string, List<string>> repHomoEntryListHash = new Dictionary<string,List<string>> ();
            List<string> repEntryList = new List<string>();
            List<string> homoEntryList = new List<string>();

            foreach (DataRow entryRow in relEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                // exclude those entries since they are already updated this time on Oct. 12, 2009
                if (Array.IndexOf(notNeedCompEntries, pdbId) > -1)
                {
                    continue;
                }
                repPdbId = IsHomoEntry(pdbId);
                if (repPdbId == "")
                {
                    if (!repEntryList.Contains(pdbId))
                    {
                        repEntryList.Add(pdbId);
                    }
                    continue;
                }
                else
                {
                    homoEntryList.Add(pdbId);

                    if (repHomoEntryListHash.ContainsKey(repPdbId))
                    {
                        if (!repHomoEntryListHash[repPdbId].Contains(pdbId))
                        {
                            repHomoEntryListHash[repPdbId].Add(pdbId);
                        }
                    }
                    else
                    {
                        List<string> entryHomoEntryList = new List<string> ();
                        entryHomoEntryList.Add(pdbId);
                        repHomoEntryListHash.Add(repPdbId, entryHomoEntryList);

                        if (!repEntryList.Contains(repPdbId))
                        {
                            repEntryList.Add(repPdbId);
                        }
                    }
                }
            }
            repEntries = new string[repEntryList.Count];
            repEntryList.CopyTo(repEntries);
            homoEntries = new string[homoEntryList.Count];
            homoEntryList.CopyTo(homoEntries);
            Dictionary<string, string[]> repHomoEntryHash = new Dictionary<string, string[]>();
            foreach (string repEntry in repHomoEntryListHash.Keys)
            {
                repHomoEntryHash.Add (repEntry, repHomoEntryListHash[repEntry].ToArray ());
            }
            return repHomoEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string IsHomoEntry(string pdbId)
        {
            string queryString = string.Format("Select * From {0}HomoRepEntryAlign " + 
                " Where PdbId2 = '{1}';", ProtCidSettings.dataType, pdbId);
            DataTable repHomoEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (repHomoEntryTable.Rows.Count > 0)
            {
                return repHomoEntryTable.Rows[0]["PdbID1"].ToString();
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetRepHomoEntryHash()
        {
            string queryString = string.Format("Select Distinct PdbID1, PdbID2 From {0}HomoRepEntryAlign;", ProtCidSettings.dataType);
            DataTable repHomoEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            Dictionary<string, List<string>> repHomoEntryHash = new Dictionary<string, List<string>>();
            string repPdbId = "";
            string homoPdbId = "";
            foreach (DataRow repHomoRow in repHomoEntryTable.Rows)
            {
                repPdbId = repHomoRow["PdbID1"].ToString();
                homoPdbId = repHomoRow["PdbID2"].ToString();
                if (repHomoEntryHash.ContainsKey(repPdbId))
                {
                    if (!repHomoEntryHash[repPdbId].Contains(homoPdbId))
                    {
                        repHomoEntryHash[repPdbId].Add(homoPdbId);
                    }
                }
                else
                {
                    List<string> homoEntryList = new List<string> ();
                    homoEntryList.Add(homoPdbId);
                    repHomoEntryHash.Add(repPdbId, homoEntryList);
                }
            }
            return repHomoEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetUpdateDomainInterfaceChangedEntries()
        {
            string entryFile = "DomainInterfaceChangedEntries.txt"; // the interface comparison not done yet.
            StreamReader dataReader = new StreamReader(entryFile);
            string line = "";
            List<string> entryList = new List<string> ();
            while ((line = dataReader.ReadLine()) != null)
            {
                if (!entryList.Contains(line.Substring(0, 4)))
                {
                    entryList.Add(line.Substring(0, 4));
                }
            }

            return entryList.ToArray ();
        }

        #endregion
        #endregion

        #region data deleteion
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="repHomoEntryHash"></param>
        private void DeleteRepHomoDomainInterfaceComp(int relSeqId, Dictionary<string,string[]> repHomoEntryHash)
        {
            foreach (string repPdb in repHomoEntryHash.Keys)
            {
                string[] homoEntries = (string[])repHomoEntryHash[repPdb];
                foreach (string homoEntry in homoEntries)
                {
                    DeleteDomainInterfaceComp(relSeqId, repPdb, homoEntry);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        private void DeleteDomainInterfaceComp(int relSeqId, string pdbId1, string pdbId2)
        {
            string deleteString = string.Format("Delete From {0}DomainInterfaceComp " +
                       " Where RelSeqID = {1} AND ((PdbID1 = '{2}' AND PdbID2 = '{3}') OR " +
                       " (PdbID1 = '{3}' AND PdbID2 = '{2}'));",
                       ProtCidSettings.dataType, relSeqId, pdbId1, pdbId2);
            ProtCidSettings.protcidQuery.Query( deleteString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryCompEntryPairHash"></param>
        private void DeleteObsCompEntryPairs(Dictionary<string, List<string>> entryCompEntryPairHash)
        {
            foreach (string entry in entryCompEntryPairHash.Keys)
            {
                foreach (string compEntry in entryCompEntryPairHash[entry])
                {
                    DeleteObsCompEntryPair(entry, compEntry);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        private void DeleteObsCompEntryPair(string pdbId1, string pdbId2)
        {
            string deleteString = string.Format("Delete From {0}DomainInterfaceComp " +
                " Where (PdbID1 = '{1}' AND PdbID2 = '{2}') OR " + 
                " (PdbID1 = '{2}' AND PdbID2 = '{1}');", ProtCidSettings.dataType, pdbId1, pdbId2);
            ProtCidSettings.protcidQuery.Query( deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        private void DeleteObsDomainInterfaceCompInfo(Dictionary<int, string[]> relationUpdateEntryDict)
        {
            string deleteString = "";
            foreach (int relSeqId in relationUpdateEntryDict.Keys)
            {
                string[] updateEntries = relationUpdateEntryDict[relSeqId];
                for (int i = 0; i < updateEntries.Length; i += 300)
                {
                    string[] subUpdateEntries = ParseHelper.GetSubArray(updateEntries, i, 300);
                    deleteString = string.Format("Delete From {0}DomainInterfaceComp " +
                        "Where RelSeqID = {1} AND (PdbID1 IN ({2}) OR PdbID2 IN ({2}));",
                        ProtCidSettings.dataType, relSeqId, ParseHelper.FormatSqlListString(subUpdateEntries));
                    dbUpdate.Delete (ProtCidSettings.protcidDbConnection, deleteString);

                    deleteString = string.Format("Delete From {0}EntryDomainInterfaceComp " +
                        "Where RelSeqID = {1} AND PdbID IN ({2});",
                        ProtCidSettings.dataType, relSeqId, ParseHelper.FormatSqlListString(subUpdateEntries));
                    dbUpdate.Delete (ProtCidSettings.protcidDbConnection, deleteString);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteObsDomainInterfaceCompInfo(int relSeqId, string pdbId)
        {
            string deleteString = string.Format("Delete From {0}DomainInterfaceComp " +
                "Where RelSeqID = {1} AND (PdbID1 = '{2}' OR PdbID2 = '{2}');", 
                ProtCidSettings.dataType, relSeqId, pdbId);
            ProtCidSettings.protcidQuery.Query( deleteString);

            deleteString = string.Format("Delete From {0}EntryDomainInterfaceComp " +
                "Where RelSeqID = {1} AND PdbID = '{2}';", ProtCidSettings.dataType, relSeqId, pdbId);
            ProtCidSettings.protcidQuery.Query( deleteString);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        private void DeleteObsDomainInterfaceCompInfo(string[] updateEntries)
        {
            foreach (string entry in updateEntries)
            {
                DeleteObsDomainInterfaceCompInfo(entry);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteObsDomainInterfaceCompInfo(string pdbId)
        {
            string deleteString = string.Format("Delete From {0}DomainInterfaceComp " +
                "Where PdbID1 = '{1}' OR PdbID2 = '{1}';", ProtCidSettings.dataType, pdbId);
            ProtCidSettings.protcidQuery.Query( deleteString);

            deleteString = string.Format("Delete From {0}EntryDomainInterfaceComp " +
                "Where PdbID = '{1}';", ProtCidSettings.dataType, pdbId);
            ProtCidSettings.protcidQuery.Query( deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        private void DeleteRelationInterfaceCompData(int relSeqId)
        {
            string deleteString = string.Format("Delete From {0} Where RelSeqID = {1};",
                DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp].TableName, relSeqId);
            ProtCidSettings.protcidQuery.Query( deleteString);
        }
        #endregion

        #region synchronize domain interface comparisons and single-domain chain interfaces
        #region debug - remove those not single domain chainns

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bugDataLines"></param>
        /// <returns></returns>
        private Dictionary<int, string[]> GetRelBugDomainInterfaces()
        {
            string[] bugDataLines = GetNonSingleDomainChainsInterfaces();

            Dictionary<int, List<string>> relBugDomainInterfaceListHash = new Dictionary<int,List<string>> ();
            int relSeqId = 0;
            string domainInterface = "";
            foreach (string dataLine in bugDataLines)
            {
                string[] fields = dataLine.Split('\t');
                relSeqId = Convert.ToInt32(fields[0]);
                domainInterface = fields[1] + fields[3];
                if (relBugDomainInterfaceListHash.ContainsKey(relSeqId))
                {
                    relBugDomainInterfaceListHash[relSeqId].Add(domainInterface);
                }
                else
                {
                    List<string> domainInterfaceList = new List<string>();
                    domainInterfaceList.Add (domainInterface);
                    relBugDomainInterfaceListHash.Add(relSeqId, domainInterfaceList);
                }
            }
            Dictionary<int, string[]> relBugDomainInterfaceHash = new Dictionary<int, string[]>();
            foreach (int keyRelSeqId in relBugDomainInterfaceListHash.Keys)
            {
                relBugDomainInterfaceHash.Add (keyRelSeqId, relBugDomainInterfaceListHash[keyRelSeqId].ToArray ());
            }
            return relBugDomainInterfaceHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetNonSingleDomainChainsInterfaces()
        {
            string singleDomainFile_bug = "SingleDomainChainInterfaces_bug.txt";
            StreamReader dataReader= new StreamReader (singleDomainFile_bug);
            string singleDomainFile = "SingleDomainChainInterfaces.txt";

            string[] singleDomainChainLines = ReadDataLineFromFile(singleDomainFile);
            string bugLine = "";
            List<string> bugDataLineList = new List<string> ();
            while ((bugLine = dataReader.ReadLine()) != null)
            {
                if (Array.BinarySearch(singleDomainChainLines, bugLine) > -1)
                {
                    continue;
                }
                bugDataLineList.Add(bugLine);
            }

            string[] bugDataLines = new string[bugDataLineList.Count];
            bugDataLineList.CopyTo(bugDataLines);
            return bugDataLines;
        }

        private string[] ReadDataLineFromFile(string dataFile)
        {
            StreamReader dataReader = new StreamReader(dataFile);
            string line = "";
            List<string> dataLineList = new List<string> ();
            while ((line = dataReader.ReadLine()) != null)
            {
                dataLineList.Add(line);
            }
            dataLineList.Sort();

            return dataLineList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="singleDomainChainInterfaceTable"></param>
        private void RemoveChainInterfaceCompInfoToDomain(int relSeqId, DataTable singleDomainChainInterfaceTable, string[] bugDomainInterfaces)
        {
            DataRow[] relDomainInterfaceRows = singleDomainChainInterfaceTable.Select(string.Format("RelSeqID = '{0}'", relSeqId));
            string pdbId1 = "";
            int domainInterfaceId1 = 0;
            string domainInterface1 = "";
            string pdbId2 = "";
            int domainInterfaceId2 = 0;
            string domainInterface2 = "";
            for (int i = 0; i < relDomainInterfaceRows.Length; i++)
            {
                pdbId1 = relDomainInterfaceRows[i]["PdbID"].ToString();
                domainInterfaceId1 = Convert.ToInt32 (relDomainInterfaceRows[i]["DomainInterfaceID"].ToString());
                domainInterface1 = pdbId1 + relDomainInterfaceRows[i]["DomainInterfaceID"].ToString();
                for (int j = i + 1; j < relDomainInterfaceRows.Length; j++)
                {
                    pdbId2 = relDomainInterfaceRows[j]["PdbID"].ToString();
                    domainInterfaceId2 = Convert.ToInt32(relDomainInterfaceRows[j]["DomainInterfaceID"].ToString());
                    domainInterface2 = pdbId2 + relDomainInterfaceRows[j]["DomainInterfaceID"].ToString();
                    if (Array.IndexOf(bugDomainInterfaces, domainInterface1)  > -1 || Array.IndexOf (bugDomainInterfaces, domainInterface2) > -1)
                    {
                        DeleteDomainInterfaceComp(relSeqId, pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        private void DeleteDomainInterfaceComp(int relSeqId, string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2)
        {
            string deleteString = string.Format("Select * From PfamDomainInterfaceComp " +
                " WHere RelSeqId = '{0}' AND PdbID1 = '{1}' AND DomainInterfaceID1 = {2} AND PdbID2 = '{3}' AND DomainInterfaceID2 = {4};",
                relSeqId, pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        public void RemoveChainInterfaceCompInfoToDomain()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            StreamWriter interfaceCompWriter = new StreamWriter("AddedDomainInterfaceCompFromChain.txt", true);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Add single-domain chain interface comp to domain interface comp");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get single-domain chain interfaces");
            DataTable singleDomainChainInterfaceTable = GetSingleDomainChainInterfaceTable(null);

            Dictionary<int, string[]> relBugDomainInterfaceHash = GetRelBugDomainInterfaces();

            int[] relSeqIds = GetRelSeqIds(singleDomainChainInterfaceTable);
            ProtCidSettings.progressInfo.totalOperationNum = relSeqIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = relSeqIds.Length;

            DataTable bugDomainInterfaceCompTable = ReadDomainChainInterfaceCompTable("AddedDomainInterfaceCompFromChain_bug.txt");
            StreamWriter dataWriter = new StreamWriter("AddedDomainInterfaceCompFromChain_fixed.txt", true);

            string pdbId = "";
            string domainInterfaceId = "";
            foreach (int relSeqId in relSeqIds)
            {
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                if (relSeqId >= 8107 && relSeqId < 10899)
                {
                    DataTable subBugInterfaceCompTable = GetSubTable(relSeqId, bugDomainInterfaceCompTable);

                    if (relBugDomainInterfaceHash.ContainsKey(relSeqId))
                    {
                        string[] bugDomainInterfaces = (string[])relBugDomainInterfaceHash[relSeqId];
                        foreach (string bugDomainInterface in bugDomainInterfaces)
                        {
                            pdbId = bugDomainInterface.Substring(0, 4);
                            domainInterfaceId = bugDomainInterface.Substring (4, bugDomainInterface.Length - 4);
                            DataRow[] bugDataRows = subBugInterfaceCompTable.Select(string.Format("RelSeqID = '{0}' AND ((PdbID1 = '{1}' AND DomainInterfaceID1 = '{2}') OR " +
                                "(PdbID2 = '{1}' AND DomainInterfaceID2 = '{2}'))", 
                                relSeqId, pdbId, domainInterfaceId));
                            foreach (DataRow bugDataRow in bugDataRows)
                            {
                                subBugInterfaceCompTable.Rows.Remove(bugDataRow);
                            }
                        }
                   //     RemoveChainInterfaceCompInfoToDomain(relSeqId, singleDomainChainInterfaceTable, bugDomainInterfaces);
                    }
                    DataRow[] compRows = subBugInterfaceCompTable.Select ();
                    DataRow[] textCompRows = null;
                    for (int i = 0; i < compRows.Length; i += 1000)
                    {
                        if (i + 1000 < compRows.Length)
                        {
                            textCompRows = new DataRow[1000];
                            Array.Copy(compRows, i, textCompRows, 0, 1000);
                        }
                        else
                        {
                            textCompRows = new DataRow[compRows.Length - i];
                            Array.Copy(compRows, i, textCompRows, 0, compRows.Length - i);
                        }
                        dataWriter.WriteLine(ParseHelper.FormatDataRows(textCompRows));
                        dataWriter.Flush();
                    }      
                }
            }
            dataWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="dataTable"></param>
        /// <returns></returns>
        private DataTable GetSubTable(int relSeqId, DataTable dataTable)
        {
            DataTable subTable = dataTable.Clone();
            DataRow[] relRows = dataTable.Select(string.Format("RelSeqID = '{0}'", relSeqId));
            foreach (DataRow relRow in relRows)
            {
                DataRow subRow = subTable.NewRow();
                subRow.ItemArray = relRow.ItemArray;
                subTable.Rows.Add(subRow);
            }
            return subTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceCompFile"></param>
        /// <returns></returns>
        private DataTable ReadDomainChainInterfaceCompTable(string domainInterfaceCompFile)
        {
            DataTable singleDomainChainCompTable = new DataTable();
            singleDomainChainCompTable.Columns.Add(new DataColumn("RelSeqID"));
            singleDomainChainCompTable.Columns.Add(new DataColumn("PdbID1"));
            singleDomainChainCompTable.Columns.Add(new DataColumn("DomainInterfaceID1"));
            singleDomainChainCompTable.Columns.Add(new DataColumn("PdbID2"));
            singleDomainChainCompTable.Columns.Add(new DataColumn("DomainInterfaceID2"));
            singleDomainChainCompTable.Columns.Add(new DataColumn("Qscore"));
            singleDomainChainCompTable.Columns.Add(new DataColumn("Identity"));
            singleDomainChainCompTable.Columns.Add(new DataColumn("IsReversed"));
            StreamReader dataReader = new StreamReader(domainInterfaceCompFile);
            string line = "";
            int relSeqId = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                relSeqId = Convert.ToInt32(fields[0]);
                DataRow dataRow = singleDomainChainCompTable.NewRow();
                dataRow.ItemArray = fields;
                singleDomainChainCompTable.Rows.Add(dataRow);
            }
            dataReader.Close();
            return singleDomainChainCompTable;
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void SynchronizeDomainChainInterfaceComp()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            StreamWriter interfaceCompWriter = new StreamWriter("AddedDomainInterfaceCompFromChain.txt", true);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Add single-domain chain interface comp to domain interface comp");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get single-domain chain interfaces");
    //        string[] entries = {"3m50" };
            DataTable singleDomainChainInterfaceTable = GetSingleDomainChainInterfaceTable(null);

            int[] relSeqIds = GetRelSeqIds(singleDomainChainInterfaceTable);
            ProtCidSettings.progressInfo.totalOperationNum = relSeqIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = relSeqIds.Length;

            foreach (int relSeqId in relSeqIds)
            {
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                AddChainInterfaceCompInfoToDomain(relSeqId, singleDomainChainInterfaceTable, interfaceCompWriter);
            }
            interfaceCompWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        public void SynchronizeDomainChainInterfaceComp(string[] updateEntries)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            StreamWriter interfaceCompWriter = new StreamWriter("AddedDomainInterfaceCompFromChain_new.txt");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Add single-domain chain interface comp to domain interface comp");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get single-domain chain interfaces");
            string[] compEntries = GetEntriesWithGoodChainQScores(updateEntries);
            DataTable singleDomainChainInterfaceTable = GetSingleDomainChainInterfaceTable(compEntries);

            int[] relSeqIds = GetRelSeqIds(singleDomainChainInterfaceTable);
            ProtCidSettings.progressInfo.totalOperationNum = relSeqIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = relSeqIds.Length;

            foreach (int relSeqId in relSeqIds)
            {
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                AddChainInterfaceCompInfoToDomain(relSeqId, singleDomainChainInterfaceTable, interfaceCompWriter);
            }
            interfaceCompWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        private string[] GetEntriesWithGoodChainQScores (string[] updateEntries)
        {
            List<string> chainEntryList = new List<string>();
            string queryString = string.Format("Select Distinct PdbID1 From DifEntryInterfaceComp Where PdbID2 IN ({0}) AND Qscore >= 0.5;", ParseHelper.FormatSqlListString (updateEntries));
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow entryRow in entryTable.Rows)
            {
                chainEntryList.Add(entryRow["PdbID1"].ToString());
            }

            queryString = string.Format("Select Distinct PdbID2 From DifEntryInterfaceComp Where PdbID1 IN ({0}) AND Qscore >= 0.5;", ParseHelper.FormatSqlListString(updateEntries));
            entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow entryRow in entryTable.Rows)
            {
                if (!chainEntryList.Contains (entryRow["PdbID2"].ToString ()))
                {
                    chainEntryList.Add(entryRow["PdbID2"].ToString());
                }
            }
            return chainEntryList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="singleDomainChainInterfaceTable"></param>
        private void AddChainInterfaceCompInfoToDomain(int relSeqId, DataTable singleDomainChainInterfaceTable, StreamWriter interfaceCompWriter)
        {
            DataRow[] relDomainInterfaceRows = singleDomainChainInterfaceTable.Select(string.Format("RelSeqID = '{0}'", relSeqId));
            string pdbId1 = "";
            string pdbId2 = "";
            int interfaceId1 = 0;
            int interfaceId2 = 0;
            int domainInterfaceId1 = 0;
            int domainInterfaceId2 = 0;
            bool chainInterfaceReversed = false;
            double qscore = 0;
            double qscore_id = 0;
            string dataLine = "";
            for (int i = 0; i < relDomainInterfaceRows.Length; i++)
            {
                pdbId1 = relDomainInterfaceRows[i]["PdbID"].ToString();
               
                domainInterfaceId1 = Convert.ToInt32(relDomainInterfaceRows[i]["DomainInterfaceID"].ToString ());
                interfaceId1 = Convert.ToInt32(relDomainInterfaceRows[i]["InterfaceID"].ToString ());
                for (int j = i + 1; j < relDomainInterfaceRows.Length; j++)
                {
                    pdbId2 = relDomainInterfaceRows[j]["PdbID"].ToString();
                    domainInterfaceId2 = Convert.ToInt32(relDomainInterfaceRows[j]["DomainInterfaceID"].ToString());
                    interfaceId2 = Convert.ToInt32(relDomainInterfaceRows[j]["InterfaceID"].ToString());
                    if (pdbId1 == pdbId2)
                    {
                        continue;
                    }
                    if (IsDomainInterfaceCompExist(relSeqId, pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2))
                    {
                        continue;
                    }
                    DataTable chainInterfaceTable = GetChainInterfaceCompTable(pdbId1, interfaceId1, pdbId2, interfaceId2, out chainInterfaceReversed);
                    if (chainInterfaceTable.Rows.Count == 0)
                    {
                        continue;
                    }
                    qscore = Convert.ToDouble(chainInterfaceTable.Rows[0]["Qscore"].ToString ());
                    qscore_id = Convert.ToDouble(chainInterfaceTable.Rows[0]["Identity"].ToString ());
                    AddChainInterfaceCompToDomain(relSeqId, pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2, qscore, qscore_id, false);
                    dataLine = relSeqId.ToString() + "\t" + pdbId1 + "\t" + domainInterfaceId1.ToString() + "\t" +
                        pdbId2 + "\t" + domainInterfaceId2.ToString() + "\t" + qscore.ToString() + "\t" +
                        qscore_id.ToString() + "\t0";
                    interfaceCompWriter.WriteLine(dataLine);
                }
                interfaceCompWriter.Flush();
            } 
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        /// <param name="qscore"></param>
        /// <param name="qscore_id"></param>
        /// <param name="isReversed"></param>
        private void AddChainInterfaceCompToDomain(int relSeqId, string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2,
            double qscore, double qscore_id, bool isReversed)
        {
            string reversedChar = "0";
            if (isReversed)
            {
                reversedChar = "1";
            }
            string insertString = string.Format("Insert INTO PfamDomainInterfaceComp (RelSeqId, PdbID1, DomainInterfaceID1, PdbID2, DomainInterfaceID2, " + 
                " Qscore, Identity, IsReversed) Values ({0}, '{1}', {2}, '{3}', {4}, {5}, {6}, '{7}');", 
                relSeqId, pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2, qscore, qscore_id, reversedChar);
            dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, insertString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        /// <returns></returns>
        private bool IsDomainInterfaceCompExist(int relSeqId, string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaceComp " +
                " WHere RelSeqId = '{0}' AND ((PdbID1 = '{1}' AND DomainInterfaceID1 = {2} AND PdbID2 = '{3}' AND DomainInterfaceID2 = {4}) " +
                " OR (PdbID1 = '{3}' AND DomainInterfaceID1 = {4} AND PdbID2 = '{1}' AND DomainInterfaceID2 = {2}));", 
                relSeqId, pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2);
            DataTable domainInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (domainInterfaceCompTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        /// <returns></returns>
        private bool IsDomainInterfaceCompExist(int relSeqId, string pdbId1, string pdbId2)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaceComp " +
                " WHere RelSeqId = '{0}' AND ((PdbID1 = '{1}' AND PdbID2 = '{2}') OR (PdbID1 = '{2}' AND PdbID2 = '{1}'));",
                relSeqId, pdbId1, pdbId2);
            DataTable domainInterfaceCompTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (domainInterfaceCompTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="interfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="interfaceId2"></param>
        /// <returns></returns>
        private DataTable GetChainInterfaceCompTable(string pdbId1, int interfaceId1, string pdbId2, int interfaceId2, out bool isReversed)
        {
            isReversed = false;
            string queryString = string.Format("Select * From DifEntryInterfaceComp " + 
                " Where PdbID1 = '{0}' AND InterfaceID1 = {1} AND PdbID2 = '{2}' AND InterfaceID2 = {3};", 
                pdbId1, interfaceId1, pdbId2, interfaceId2);
            DataTable interfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (interfaceCompTable.Rows.Count == 0)
            {
                queryString = string.Format("Select * From DifEntryInterfaceComp " +
                       " Where PdbID1 = '{2}' AND InterfaceID1 = {3} AND PdbID2 = '{0}' AND InterfaceID2 = {1};",
                       pdbId1, interfaceId1, pdbId2, interfaceId2);
                interfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
                isReversed = true;
            }
            return interfaceCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="singleDomainChainInterfaceTable"></param>
        /// <returns></returns>
        private int[] GetRelSeqIds(DataTable singleDomainChainInterfaceTable)
        {
            int relSeqId = 0;
            List<int> relSeqIdList = new List<int> ();
            foreach (DataRow domainInterfaceRow in singleDomainChainInterfaceTable.Rows)
            {
                relSeqId = Convert.ToInt32(domainInterfaceRow["RelSeqID"].ToString ());
                if (!relSeqIdList.Contains(relSeqId))
                {
                    relSeqIdList.Add(relSeqId);
                }
            }
            relSeqIdList.Sort();

            return relSeqIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable GetSingleDomainChainInterfaceTable(string[] entries)
        {
            DataTable singleDomainChainInterfaceTable = null;
            string singleDomainFile = "SingleDomainChainInterfaces.txt";
            if (File.Exists(singleDomainFile))
            {
                StreamReader dataReader = new StreamReader(singleDomainFile);
                string line = "";
                singleDomainChainInterfaceTable = new DataTable();
                singleDomainChainInterfaceTable.Columns.Add(new DataColumn ("RelSeqID"));
                singleDomainChainInterfaceTable.Columns.Add (new DataColumn ("PdbID"));
                singleDomainChainInterfaceTable.Columns.Add (new DataColumn ("InterfaceID"));
                singleDomainChainInterfaceTable.Columns.Add (new DataColumn ("DomainInterfaceID"));
                while ((line = dataReader.ReadLine()) != null)
                {
                    string[] fields = line.Split('\t');
                    DataRow dataRow = singleDomainChainInterfaceTable.NewRow();
                    dataRow.ItemArray = fields;
                    singleDomainChainInterfaceTable.Rows.Add(dataRow);
                }
                dataReader.Close();
            }
            else
            {
                Dictionary<string, List<string>> entrySingleDomainChainHash = null;
                if (entries == null)
                {
                    entrySingleDomainChainHash = GetSingleDomainChains();
                }
                else
                {
                    entrySingleDomainChainHash = GetSingleDomainChains(entries);
                }

                List<string> entryList = new List<string> (entrySingleDomainChainHash.Keys);
                entryList.Sort();

                StreamWriter dataWriter = new StreamWriter(singleDomainFile);

                foreach (string pdbId in entryList)
                {
                    List<string> singleDomainChainList = entrySingleDomainChainHash[pdbId];
                    string[] singleDomainChains = new string[singleDomainChainList.Count];
                    singleDomainChainList.CopyTo(singleDomainChains);

                    DataTable domainInterfaceTable = GetDomainInterfaceTable(pdbId, singleDomainChains);
                    foreach (DataRow domainInterfaceRow in domainInterfaceTable.Rows)
                    {
                        dataWriter.WriteLine(ParseHelper.FormatDataRow(domainInterfaceRow));
                    }
                    dataWriter.Flush();
                    ParseHelper.AddNewTableToExistTable(domainInterfaceTable, ref singleDomainChainInterfaceTable);
                }
                dataWriter.Close();
            }
            if (singleDomainChainInterfaceTable == null)
            {
                singleDomainChainInterfaceTable = new DataTable();  // return an empty table instead of a null which will throw an exception to abort the program
            }
            return singleDomainChainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="singleDomainChains"></param>
        /// <returns></returns>
        private DataTable GetDomainInterfaceTable(string pdbId, string[] singleDomainChains)
        {
            string queryString = string.Format("Select RelSeqID, PdbID, InterfaceID, DomainInterfaceID " + 
                " From PfamDomainInterfaces Where PdbID = '{0}' AND AsymChain1 IN ({1}) AND AsymChain2 IN ({1});", 
                pdbId, ParseHelper.FormatSqlListString (singleDomainChains));
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return domainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetSingleDomainChains()
        {
            string queryString = "Select PdbID, AsymID, Sequence From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable chainSeqTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            queryString = "Select PdbPfam.PdbID, PdbPfam.EntityID, AsymChain, PdbPfam.DomainID, SeqStart, SeqEnd From PdbPfam, PdbPfamChain " + 
                " WHere PdbPfam.PdbID = PdbPfamCHain.PdbID AND PdbPfam.DomainID = PdbPfamChain.DomainID AND PdbPfam.EntityID = PdbPfamCHain.EntityID;";
            DataTable chainDomainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            queryString = "Select PdbID, AsymChain, Count(Distinct DomainID) As DomainCount From PdbPfamChain Group By PdbID, AsymChain;";
            DataTable chainDomainCountTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            Dictionary<string, List<string>> entrySingleDomainChainHash = new Dictionary<string,List<string>> ();
            int domainCount = 0;
            string pdbId = "";
            string asymChain = "";
            foreach (DataRow domainCountRow in chainDomainCountTable.Rows)
            {
                pdbId = domainCountRow["PdbID"].ToString();
                asymChain = domainCountRow["AsymChain"].ToString().TrimEnd();
                domainCount = Convert.ToInt32(domainCountRow["DomainCount"].ToString());
                if (domainCount == 1)
                {
                    if (IsChainSingleDomain(pdbId, asymChain, chainSeqTable, chainDomainTable))
                    {
                        if (entrySingleDomainChainHash.ContainsKey(pdbId))
                        {
                            entrySingleDomainChainHash[pdbId].Add(asymChain);
                        }
                        else
                        {
                            List<string> singleDomainChainList = new List<string> ();
                            singleDomainChainList.Add(asymChain);
                            entrySingleDomainChainHash.Add(pdbId, singleDomainChainList);
                        }
                    }
                }
            }
            return entrySingleDomainChainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="chainSeqTable"></param>
        /// <param name="chainDomainTable"></param>
        /// <returns></returns>
        private bool IsChainSingleDomain(string pdbId, string asymChain, DataTable chainSeqTable, DataTable chainDomainTable)
        {
            DataRow[] chainSeqRows = chainSeqTable.Select(string.Format ("PdbID = '{0}' AND AsymID = '{1}'", pdbId, asymChain));
            string sequence = chainSeqRows[0]["Sequence"].ToString().TrimEnd();
            int seqLength = sequence.Length;

            DataRow[] chainDomainRows = chainDomainTable.Select(string.Format ("PdbID = '{0}' AND AsymChain = '{1}'", pdbId, asymChain));
            int domainLength = 0;
            foreach (DataRow domainRow in chainDomainRows)
            {
                domainLength += (Convert.ToInt32 (domainRow["SeqEnd"].ToString ()) - Convert.ToInt32 (domainRow["SeqStart"].ToString ()) + 1);
            }

            double coverage = (double)domainLength / (double)seqLength;
            if (coverage >= 0.9)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetSingleDomainChains(string[] updateEntries)
        {
            string queryString = "Select PdbID, AsymID, Sequence From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable chainSeqTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            queryString = "Select PdbPfam.PdbID, PdbPfam.EntityID, AsymChain, PdbPfam.DomainID, SeqStart, SeqEnd From PdbPfam, PdbPfamChain " +
                " WHere PdbPfam.PdbID = PdbPfamCHain.PdbID AND PdbPfam.DomainID = PdbPfamChain.DomainID AND PdbPfam.EntityID = PdbPfamCHain.EntityID;";
            DataTable chainDomainTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            queryString = "Select PdbID, AsymChain, Count(Distinct DomainID) As DomainCount From PdbPfamChain Group By PdbID, AsymChain;";
            DataTable chainDomainCountTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            Dictionary<string, List<string>> entrySingleDomainChainHash = new Dictionary<string,List<string>>  ();
            int domainCount = 0;
            string pdbId = "";
            string asymChain = "";
            foreach (DataRow domainCountRow in chainDomainCountTable.Rows)
            {
                pdbId = domainCountRow["PdbID"].ToString();
                asymChain = domainCountRow["AsymChain"].ToString().TrimEnd();
                if (Array.IndexOf(updateEntries, pdbId) > -1)
                {
                    domainCount = Convert.ToInt32(domainCountRow["DomainCount"].ToString());
                    if (domainCount == 1)
                    {
                        if (IsChainSingleDomain(pdbId, asymChain, chainSeqTable, chainDomainTable))
                        {
                            if (entrySingleDomainChainHash.ContainsKey(pdbId))
                            {
                                entrySingleDomainChainHash[pdbId].Add(asymChain);
                            }
                            else
                            {
                                List<string> singleDomainChainList = new List<string> ();
                                singleDomainChainList.Add(asymChain);
                                entrySingleDomainChainHash.Add(pdbId, singleDomainChainList);
                            }
                        }
                    }
                }
            }
            return entrySingleDomainChainHash;
        }
        #endregion

        #region compare specific domain interfaces
        public void CompareSpecificDomainInterfaces()
        {
            logWriter = new StreamWriter("DifRelationDInterfacesCompLog.txt");
            string[] pfamIds = { "Pkinase_Tyr", "Pkinase" };
            // get the structure alignment between different Pfams
      /*      PfamLib.DomainAlign.PfamDomainAlign domainAlign = new PfamLib.DomainAlign.PfamDomainAlign();
            Hashtable userGroupPfamsHash = new Hashtable();
            
            userGroupPfamsHash.Add(111111, pfamIds);
            domainAlign.AlignPfamDomainsForUserDefinedGroups(userGroupPfamsHash);
            */
            Dictionary<int, string> relKinasePfamsHash = GetKinaseHomoRelations(pfamIds);
            List<int> kinaseRelSeqIdList = new List<int>  (relKinasePfamsHash.Keys);
            int[] kinaseRelSeqIds = new int[kinaseRelSeqIdList.Count];
            kinaseRelSeqIdList.CopyTo(kinaseRelSeqIds);

            StreamWriter dataWriter = new StreamWriter(@"D:\DbProjectData\InterfaceFiles_update\kinase_data\KinaseInterfaceComp_4m69.txt");
            string pdbId = "4m69";
            int domainInterfaceId = 1;
            string queryString = "Select Distinct PdbID From PdbPfam Where Pfam_ID IN ('Pkinase', 'Pkinase_Tyr')";
            DataTable kinaseEntryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string compPdbId = "";
            foreach (DataRow entryRow in kinaseEntryTable.Rows)
            {
                compPdbId = entryRow["PdbID"].ToString();
                DataTable compDomainInterfaceTable = GetEntryDomainInterfaceTable(compPdbId, kinaseRelSeqIds);
                CompareDifRelationDomainInterfaces(pdbId, domainInterfaceId, compPdbId, compDomainInterfaceTable, relKinasePfamsHash, dataWriter);
            }
            dataWriter.Close();
            logWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="entry"></param>
        /// <param name="compEntries"></param>
        /// <param name="entryDomainInterfaceHash"></param>
        private void CompareDifRelationDomainInterfaces(string pdbId, int domainInterfaceId, string compPdbId, DataTable compEntryDomainInterfaceTable, 
            Dictionary<int, string> relPfamsHash, StreamWriter dataWriter)
        {
            int[] compDomainInterfaceIds = GetEntryDomainInterfaceIds(compEntryDomainInterfaceTable);
            if (pdbId == compPdbId)
            {
                List<int> compDomainInterfaceIdList = new List<int> (compDomainInterfaceIds);
                compDomainInterfaceIdList.Remove(domainInterfaceId);

                compDomainInterfaceIds = new int[compDomainInterfaceIdList.Count];
                compDomainInterfaceIdList.CopyTo(compDomainInterfaceIds);
            }

            int[] domainInterfaceIds = new int[1];
            domainInterfaceIds[0] = domainInterfaceId;
            DomainInterface[] domainInterfaces = domainInterfaceRetriever.ReadDomainInterfacesFromFiles(pdbId, domainInterfaceIds);

            DomainInterface[] compDomainInterfaces = domainInterfaceRetriever.ReadDomainInterfacesFromFiles(compPdbId, compDomainInterfaceIds);

            try
            {
                DomainInterfacePairInfo[] pairCompInfos = CompareDifRelationDomainInterfaces (domainInterfaces, compDomainInterfaces);
                string dataLine = "";
                string relPfams = "";
                foreach (DomainInterfacePairInfo compInfo in pairCompInfos)
                {
                    relPfams = GetRelationPfamString(compInfo.interfaceInfo2.pdbId, compInfo.interfaceInfo2.domainInterfaceId, compEntryDomainInterfaceTable, relPfamsHash);
                    dataLine = compInfo.interfaceInfo1.pdbId + "\t" + compInfo.interfaceInfo1.domainInterfaceId.ToString() + "\t" +
                        compInfo.interfaceInfo2.pdbId + "\t" + compInfo.interfaceInfo2.domainInterfaceId.ToString() + "\t" +
                        compInfo.qScore.ToString() + "\t" + compInfo.identity.ToString() + "\t" + compInfo.isInterface2Reversed.ToString() + "\t" + relPfams;
                    dataWriter.WriteLine(dataLine);
                }
            }
            catch (Exception ex)
            {
                logWriter.WriteLine("Compare Inter-chain domain interfaces for " + pdbId
                    + " and " + compPdbId + " errors: " + ex.Message);
                logWriter.Flush();
            }
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <param name="relPfamsHash"></param>
        /// <returns></returns>
        private string GetRelationPfamString(string pdbId, int domainInterfaceId, DataTable domainInterfaceTable, Dictionary<int, string> relPfamsHash)
        {
            DataRow[] domainInterfaceRows = domainInterfaceTable.Select(string.Format ("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            int relSeqId = Convert.ToInt32(domainInterfaceRows[0]["RelSeqID"].ToString());
            string pfamString = relPfamsHash[relSeqId];
            return pfamString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamIds"></param>
        /// <returns></returns>
        private Dictionary<int, string> GetKinaseHomoRelations(string[] pfamIds)
        {
            Dictionary<int, string> relPfamsHash = new Dictionary<int, string>();
            int relSeqId = 0;
            for (int i = 0; i < pfamIds.Length; i++)
            {
                relSeqId = GetHomoRelationId(pfamIds[i]);
                relPfamsHash.Add(relSeqId, pfamIds[i]);
            }
            return relPfamsHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int GetHomoRelationId(string pfamId)
        {
            string queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation WHere FamilyCOde1 = '{0}' AND FamilyCode2 = '{0}';", pfamId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = -1;
            if (relSeqIdTable.Rows.Count > 0)
            {
                relSeqId = Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString());
            }

            return relSeqId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="relSeqIds"></param>
        /// <returns></returns>
        private int[] GetEntryDomainInterfaceIds(DataTable entryDomainInterfaceTable)
        {
            int[] domainInterfaceIds = new int[entryDomainInterfaceTable.Rows.Count];
            int count = 0;
            foreach (DataRow domainInterfaceRow in entryDomainInterfaceTable.Rows)
            {
                domainInterfaceIds[count] = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
                count++;
            }
            return domainInterfaceIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="relSeqIds"></param>
        /// <returns></returns>
        private DataTable GetEntryDomainInterfaceTable(string pdbId, int[] relSeqIds)
        {
            string queryString = string.Format("Select RelSeqID, PdbID, DomainInterfaceID From PfamDomainInterfaces " +
                   " Where PdbID = '{0}' AND RelSeqID IN ({1});", pdbId, ParseHelper.FormatSqlListString(relSeqIds));
            DataTable entryDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return entryDomainInterfaceTable;
        }
        #endregion

        #region for debug
        public void RemoveDifDomainInterfaceCompForSimDomainInterfaces()
        {
            StreamWriter dataWriter = new StreamWriter("RemovedDifDomainInterfaceComp.txt", true);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Remove same domain interface comp";

            string queryString = "Select Distinct RelSeqID From PfamEntryDomainInterfaceComp;";
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString());

                if (relSeqId < 10735)
                {
                    continue;
                }
                RemoveDuplicateDomainInterfaceComp(relSeqId, dataWriter);

            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        private void RemoveDuplicateDomainInterfaceComp(int relSeqId, StreamWriter dataWriter)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = relSeqId.ToString();

            string queryString = string.Format("Select * From PfamEntryDomainInterfaceComp " + 
                " Where RelSeqID = {0} Order By PdbID, DomainInterfaceID1, DomainInterfaceID2;", relSeqId);
            DataTable entryDomainInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> entryList = new List<string> ();
            string pdbId = "";
            foreach (DataRow interfaceCompRow in entryDomainInterfaceCompTable.Rows)
            {
                pdbId = interfaceCompRow["PdbID"].ToString();
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }

         //   ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
         //   ProtCidSettings.progressInfo.totalOperationNum = entryList.Count;
         //   ProtCidSettings.progressInfo.totalStepNum = entryList.Count;


            foreach (string lsPdbId in entryList)
            {
                ProtCidSettings.progressInfo.currentFileName = lsPdbId;
          //      ProtCidSettings.progressInfo.currentOperationNum++;
          //      ProtCidSettings.progressInfo.currentStepNum++;

                DataRow[] interfaceCompRows = entryDomainInterfaceCompTable.Select(string.Format("PdbID = '{0}'", lsPdbId));
           //     int[] removedDomainInterfaceIds = GetEntryDomainInterfaceIdsToBeRemoved(interfaceCompRows);
                Dictionary<int, int[]> repSimDomainInterfaceHash = GetEntryRemoveDomainInterfaceIdsHash(interfaceCompRows);
                if (repSimDomainInterfaceHash.Count > 0)
                {
               //     WriteRemoveInterfaceCompData(relSeqId, lsPdbId, removedDomainInterfaceIds, dataWriter);

                    RemoveDifDomainInterfaceCompRows(relSeqId, lsPdbId, repSimDomainInterfaceHash, dataWriter);
                }
            }
        }

        private void WriteRemoveInterfaceCompData(int relSeqId, string pdbId, int[] removeDomainInterfaceIds, StreamWriter dataWriter)
        {
            string queryString = "";
            DataTable removeInterfaceCompTable = null;
            if (removeDomainInterfaceIds.Length < 50)
            {
                queryString = string.Format("select * From PfamDomainInterfaceComp Where RelSeqID = {0} AND " +
                          "((PdbID1 = '{1}' AND DomainInterfaceId1 IN ({2})) OR (PdbID2 = '{1}' AND DomainInterfaceID2 IN ({2})));",
                          relSeqId, pdbId, ParseHelper.FormatSqlListString(removeDomainInterfaceIds));
                removeInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            }
            else
            {
                foreach (int domainInterfaceId in removeDomainInterfaceIds)
                {
                    queryString = string.Format("select * From PfamDomainInterfaceComp Where RelSeqID = {0} AND " +
                          "((PdbID1 = '{1}' AND DomainInterfaceId1  = {2}) OR (PdbID2 = '{1}' AND DomainInterfaceID2 = {2}));",
                          relSeqId, pdbId, domainInterfaceId);
                    DataTable interfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
                    ParseHelper.AddNewTableToExistTable(interfaceCompTable, ref removeInterfaceCompTable);
                }
            }
            DataRow[] interfaceCompRows = removeInterfaceCompTable.Select();
            if (interfaceCompRows.Length > 0)
            {
                string dataLine = ParseHelper.FormatDataRows(interfaceCompRows);
                dataWriter.WriteLine(dataLine);
                dataWriter.Flush();
            }
        }

        private void RemoveDifDomainInterfaceCompRows(int relSeqId, string pdbId, int[] removeDomainInterfaceIds)
        {
            string deleteString = "";
            foreach (int domainInterfaceId in removeDomainInterfaceIds)
            {
                deleteString = string.Format("Delete From PfamDomainInterfaceComp Where RelSeqID = {0} AND " +
                    "((PdbID1 = '{1}' AND DomainInterfaceId1 = {2}) OR (PdbID2 = '{1}' AND DomainInterfaceID2 = {2}));",
                    relSeqId, pdbId, domainInterfaceId);
                dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <param name="repSimDomainInterfaceHash"></param>
        /// <param name="dataWriter"></param>
        private void RemoveDifDomainInterfaceCompRows(int relSeqId, string pdbId, Dictionary<int, int[]> repSimDomainInterfaceHash, StreamWriter dataWriter)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaceComp Where RelSeqID = {0} AND " +
                " (PdbID1 = '{1}' OR PdbID2 = '{1}');", relSeqId, pdbId);
            DataTable interfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            foreach (int repDomainInterfaceId in repSimDomainInterfaceHash.Keys)
            {
                int[] simDomainInterfaceIds = repSimDomainInterfaceHash[repDomainInterfaceId];
                if (IsRepDomainInterfaceCompExist(pdbId, repDomainInterfaceId, interfaceCompTable))
                {
                    WriteInterfaceCompDataToFile(pdbId, simDomainInterfaceIds, interfaceCompTable, dataWriter);
                    RemoveDifDomainInterfaceCompRows(relSeqId, pdbId, simDomainInterfaceIds);
                }
                else
                {
                    int[] leftSimDomainInterfaceIds = ReplaceByRepDomainInterface(relSeqId, pdbId, repDomainInterfaceId, simDomainInterfaceIds, interfaceCompTable);
                    WriteInterfaceCompDataToFile(pdbId, leftSimDomainInterfaceIds, interfaceCompTable, dataWriter);
                    RemoveDifDomainInterfaceCompRows(relSeqId, pdbId, leftSimDomainInterfaceIds);    
                }
            }
        }

        private void WriteInterfaceCompDataToFile(string pdbId, int[] domainInterfaceIds, DataTable interfaceCompTable, StreamWriter dataWriter)
        {
            string dataLine = "";
            foreach (int domainInterfaceId in domainInterfaceIds)
            {
                DataRow[] interfaceCompRows = interfaceCompTable.Select(string.Format ("PdbID1 = '{0}' AND DomainInterfaceId1 = '{1}'", pdbId, domainInterfaceId));
                if (interfaceCompRows.Length > 0)
                {
                    dataLine = ParseHelper.FormatDataRows(interfaceCompRows);
                    dataWriter.WriteLine(dataLine);
                }
                interfaceCompRows = interfaceCompTable.Select(string.Format ("PdbID2 = '{0}' AND DomainInterfaceId2 = '{1}'", pdbId, domainInterfaceId));
                if (interfaceCompRows.Length > 0)
                {
                    dataLine = ParseHelper.FormatDataRows(interfaceCompRows);
                    dataWriter.WriteLine(dataLine);
                } 
            }
            dataWriter.Flush();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="interfaceCompTable"></param>
        /// <returns></returns>
        private bool IsRepDomainInterfaceCompExist(string pdbId, int domainInterfaceId, DataTable interfaceCompTable)
        {
            DataRow[] interfaceCompRows = interfaceCompTable.Select(string.Format ("PdbID1 = '{0}' AND DomainInterfaceID1 = '{1}'", pdbId, domainInterfaceId));
            if (interfaceCompRows.Length > 0)
            {
                return true;
            }
            interfaceCompRows = interfaceCompTable.Select(string.Format ("PdbID2 = '{0}' ANd DomainInterfaceID2 = '{1}'", pdbId, domainInterfaceId));
            if (interfaceCompRows.Length > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <param name="repDomainInterfaceId"></param>
        /// <param name="simDomainInterfaceIds"></param>
        /// <param name="interfaceCompTable"></param>
        /// <returns></returns>
        private int[] ReplaceByRepDomainInterface(int relSeqId, string pdbId, int repDomainInterfaceId, int[] simDomainInterfaceIds, DataTable interfaceCompTable)
        {
            List<int> leftSimDomainInterfaceList = new List<int> ();
            foreach (int domainInterfaceId in simDomainInterfaceIds)
            {
                DataRow[] interfaceCompRows1 = interfaceCompTable.Select(string.Format("PdbID1 = '{0}' AND DomainInterfaceID1 = '{1}'", pdbId, domainInterfaceId));
                DataRow[] interfaceCompRows2 = interfaceCompTable.Select(string.Format("PdbID2 = '{0}' ANd DomainInterfaceID2 = '{1}'", pdbId, domainInterfaceId));
                if (interfaceCompRows1.Length > 0 || interfaceCompRows2.Length > 0)
                {
                    leftSimDomainInterfaceList.Remove(domainInterfaceId);
                    UpdateDomainInterfaceIds(relSeqId, pdbId, domainInterfaceId, repDomainInterfaceId);
                    break;
                }
                else
                {
                    leftSimDomainInterfaceList.Remove(domainInterfaceId);
                }
            }
            return leftSimDomainInterfaceList.ToArray ();
        }

        private void UpdateDomainInterfaceIds(int relSeqId, string pdbId, int oldDomainInterfaceId, int newDomainInterfaceId)
        {
            string updateString = string.Format ("Update PfamDomainInterfaceComp Set DomainInterfaceId1 = {0} " + 
                " Where RelSeqId = {1} AND PdbID1 = '{2}' AND DomainInterfaceID1 = {3};", newDomainInterfaceId, relSeqId, pdbId, oldDomainInterfaceId);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);

            updateString = string.Format("Update PfamDomainInterfaceComp Set DomainInterfaceId2 = {0} " +
                " Where RelSeqId = {1} AND PdbID2 = '{2}' AND DomainInterfaceID2 = {3};", newDomainInterfaceId, relSeqId, pdbId, oldDomainInterfaceId);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryInterfaceCompRows"></param>
        /// <returns></returns>
        private int[] GetEntryDomainInterfaceIdsToBeRemoved(DataRow[] entryInterfaceCompRows)
        {
            List<int> removeDomainInterfaceIdList = new List<int> ();
            double qscore = 0;
            int domainInterfaceId = 0;
            foreach (DataRow interfaceCompRow in entryInterfaceCompRows)
            {
                qscore = Convert.ToDouble(interfaceCompRow["Qscore"].ToString());
                if (qscore >= AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff)
                {
                    domainInterfaceId = Convert.ToInt32(interfaceCompRow["DomainInterfaceID2"].ToString());
                    if (!removeDomainInterfaceIdList.Contains(domainInterfaceId))
                    {
                        removeDomainInterfaceIdList.Add(domainInterfaceId);
                    }
                }
            }
            return removeDomainInterfaceIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryInterfaceCompRows">sorted by DomainInterfaceID1, DomainInterfaceID2</param>
        /// <returns></returns>
        private Dictionary<int, int[]> GetEntryRemoveDomainInterfaceIdsHash(DataRow[] entryInterfaceCompRows)
        {
            double qscore = 0;
            int domainInterfaceId1 = 0;
            int domainInterfaceId2 = 0;
            Dictionary<int, List<int>> repSimDomainInterfaceListHash = new Dictionary<int, List<int>>();
            foreach (DataRow interfaceCompRow in entryInterfaceCompRows)
            {
                domainInterfaceId1 = Convert.ToInt32(interfaceCompRow["DomainInterfaceId1"].ToString ());
                qscore = Convert.ToDouble(interfaceCompRow["Qscore"].ToString());
                if (qscore >= AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff)
                {
                    domainInterfaceId2 = Convert.ToInt32(interfaceCompRow["DomainInterfaceID2"].ToString());
                    AddDomainInterfaceIdToHash(domainInterfaceId1, domainInterfaceId2, repSimDomainInterfaceListHash);
                }
            }
            Dictionary<int, int[]> repSimDomainInterfaceHash = new Dictionary<int, int[]>();
            foreach (int domainInterfaceId in repSimDomainInterfaceListHash.Keys)
            {
                repSimDomainInterfaceHash.Add (domainInterfaceId, repSimDomainInterfaceListHash[domainInterfaceId].ToArray ());
            }
            return repSimDomainInterfaceHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="domainInterfaceId2"></param>
        /// <param name="repSimDomainInterfaceHash"></param>
        private void AddDomainInterfaceIdToHash(int domainInterfaceId1, int domainInterfaceId2, Dictionary<int, List<int>> repSimDomainInterfaceHash)
        {
            if (repSimDomainInterfaceHash.ContainsKey(domainInterfaceId1))
            {
                if (!repSimDomainInterfaceHash[domainInterfaceId1].Contains(domainInterfaceId2))
                {
                    repSimDomainInterfaceHash[domainInterfaceId1].Add(domainInterfaceId2);
                }
            }
            else
            {
                bool simDomainInterfaceAdded = false;
                foreach (int domainInterfaceId in repSimDomainInterfaceHash.Keys)
                {
                    if (repSimDomainInterfaceHash[domainInterfaceId1].Contains(domainInterfaceId1))
                    {
                        if (!repSimDomainInterfaceHash[domainInterfaceId1].Contains(domainInterfaceId2))
                        {
                            repSimDomainInterfaceHash[domainInterfaceId1].Add(domainInterfaceId2);
                            simDomainInterfaceAdded = true;
                        }
                    }
                }
                if (!simDomainInterfaceAdded)
                {
                    List<int> simDomainInterfaceList = new List<int> ();
                    simDomainInterfaceList.Add(domainInterfaceId2);
                    repSimDomainInterfaceHash.Add(domainInterfaceId1, simDomainInterfaceList);
                }
            }
        }

        #region entry domain interfaces
        public void GetEntryDomainInterfaces(int relSeqId, string pdbId)
        {
            ProtCidSettings.dataType = "pfam";
            DomainInterfaceTables.InitializeTables();

            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults.txt");
                logWriter = new StreamWriter("CrystDomainInterfaceCompLog.txt");
                entryCompResultWriter = new StreamWriter("EntryInterfaceCompResults.txt");
            }
            
            DomainInterface[] uniqueDomainInterfaces =
                   GetEntryUniqueDomainInterfaces(relSeqId, pdbId);

            logWriter.Close();
            compResultWriter.Close();
            entryCompResultWriter.Close();
        }

        public void GetEntryDomainInterfaces()
        {
            ProtCidSettings.dataType = "pfam";
            DomainInterfaceTables.InitializeTables();

            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults.txt");
                logWriter = new StreamWriter("CrystDomainInterfaceCompLog.txt");
                entryCompResultWriter = new StreamWriter("EntryInterfaceCompResults.txt");
            }
   //         string[] entries = GetRelationEntries();
            string[] entries = { "2o5g" };
            foreach (string entry in entries)
            {
                DeleteEntryDomainInterfaceCompInfo(entry);
                int[] entryRelSeqIDs = GetEntryRelSeqIDs(entry);
                foreach (int relSeqId in entryRelSeqIDs)
                {
                    DomainInterface[] uniqueDomainInterfaces =
                      GetEntryUniqueDomainInterfaces(relSeqId, entry);
                }
            }
            logWriter.Close();
            compResultWriter.Close();
            entryCompResultWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<int, DomainInterface[]> GetEntryRelationDomainInterfaces(string pdbId)
        {
            Dictionary<int, DomainInterface[]> entryRelationDomainInterfacesHash = new Dictionary<int,DomainInterface[]> ();
            int[] entryRelSeqIDs = GetEntryRelSeqIDs(pdbId);
            foreach (int relSeqId in entryRelSeqIDs)
            {
                DomainInterface[] entryDomainInterfaces = GetEntryUniqueDomainInterfaces(relSeqId, pdbId);
                if (entryDomainInterfaces.Length > 0)
                {
                    entryRelationDomainInterfacesHash.Add(relSeqId, entryDomainInterfaces);
                }
            }
            return entryRelationDomainInterfacesHash;
        }
        #endregion

        private void DeleteEntryDomainInterfaceCompInfo(int relSeqId, string pdbId)
        {
            string deleteString = string.Format("Delete From PfamEntryDomainInterfaceComp " + 
                " Where RelSeqID = {0} AND PdbID = '{1}';", relSeqId, pdbId);
            ProtCidSettings.protcidQuery.Query( deleteString);
        }

        private void DeleteEntryDomainInterfaceCompInfo(string pdbId)
        {
            string deleteString = string.Format("Delete From PfamEntryDomainInterfaceComp " + 
                " Where PdbId = '{0}';", pdbId);
            ProtCidSettings.protcidQuery.Query( deleteString);
        }


         private string[] GetRelationEntries()
        {
            StreamReader dataReader = new StreamReader(@"C:\ProtBuDProject\xtal\XtalStat\bin\Debug\EntryCompInConsistentEntries.txt");
            string line = "";
            List<string> relationEntryList = new List<string> ();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = ParseHelper.SplitPlus(line, ' ');
                if (!relationEntryList.Contains(fields[1]))
                {
                    relationEntryList.Add(fields[1]);
                }
            }
            dataReader.Close();
            return relationEntryList.ToArray ();
        }

        public void UpdateDomainInterfaceCompForEntriesWithDomainAlignmentChanged()
        {
            ProtCidSettings.dataType = "pfam";
            DomainInterfaceTables.InitializeTables();

            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults.txt");
                logWriter = new StreamWriter("CrystDomainInterfaceCompLog.txt");
                entryCompResultWriter = new StreamWriter("EntryInterfaceCompResults.txt");
            }

            int totalEntryPairs = 0;
            Dictionary<string, string[]> entryCompEntriesHash = ReadUpdateEntryAndCompEntries(out totalEntryPairs);
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = totalEntryPairs;
            ProtCidSettings.progressInfo.totalOperationNum = totalEntryPairs;

            bool needReverse = false;
            List<string> keyEntryList = new List<string> (entryCompEntriesHash.Keys);
            keyEntryList.Sort();
            Dictionary<int, DomainInterface[]> entryRelationDomainInterfacesHash = null;
            foreach (string entry in keyEntryList)
            {

                try
                {
                    entryRelationDomainInterfacesHash = GetEntryRelationDomainInterfaces(entry);
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine("Compare domain interface errors for first entry: " + entry + " " + ex.Message);
                    logWriter.Flush();
                    continue;
                }
                string[] compEntries = (string[]) entryCompEntriesHash[entry];
                foreach (string compEntry in compEntries)
                {
                    ProtCidSettings.progressInfo.currentStepNum++;
                    ProtCidSettings.progressInfo.currentOperationNum++;
                    ProtCidSettings.progressInfo.currentFileName = entry + "_" + compEntry;
                    if (entry == compEntry)
                    {
                        continue;
                    }
                  
                    needReverse = false;
                    if (string.Compare(entry, compEntry) > 0)
                    {
                        needReverse = true;
                    }
                    try
                    {
                        Dictionary<int, DomainInterface[]> compEntryRelationDomainInterfacesHash = GetEntryRelationDomainInterfaces(compEntry);

                        foreach (int relSeqId in entryRelationDomainInterfacesHash.Keys)
                        {
                            if (compEntryRelationDomainInterfacesHash.ContainsKey(relSeqId))
                            {
                                DomainInterface[] domainInterfaces1 = entryRelationDomainInterfacesHash[relSeqId];
                                DomainInterface[] domainInterfaces2 = compEntryRelationDomainInterfacesHash[relSeqId];
                                if (needReverse)
                                {
                                    UpdateDomainInterfaceCompData(relSeqId, compEntry, entry, domainInterfaces2, domainInterfaces1);
                                }
                                else
                                {
                                    UpdateDomainInterfaceCompData(relSeqId, entry, compEntry, domainInterfaces1, domainInterfaces2);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine("Compare domain interface errors for entry pairs " 
                            + entry + ", " + compEntry + " errors: " + ex.Message);
                        logWriter.Flush();
                    }
                }
            }

            logWriter.Close();
            compResultWriter.Close();
            entryCompResultWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.progressInfo.threadFinished = true;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="totalEntryPairs"></param>
        /// <returns></returns>
        private Dictionary<string,string[]> ReadUpdateEntryAndCompEntries(out int totalEntryPairs)
        {
            Dictionary<string, string[]> entryCompEntriesHash = new Dictionary<string,string[]> ();
            string line = "";
            totalEntryPairs = 0;
            StreamReader dataReader = new StreamReader("EntryCompEntries.txt");
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split(',');
            /*    if (string.Compare(fields[0], "1q5k") <= 0)
                {
                    continue;
                }*/
                string[] compEntries = new string[fields.Length - 1];
                Array.Copy(fields, 1, compEntries, 0, fields.Length - 1);
                entryCompEntriesHash.Add(fields[0], compEntries);
                totalEntryPairs += compEntries.Length;
            }
            dataReader.Close();
            return entryCompEntriesHash;
        }
       
        /// <summary>
        /// compare those domain interfaces of the two input entry with the relation
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        public void CompareDomainInterfacesForEntries(int relSeqId, string pdbId1, string pdbId2)
        {
            ProtCidSettings.dataType = "pfam";
            DomainInterfaceTables.InitializeTables();

            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults.txt");
                logWriter = new StreamWriter("CrystDomainInterfaceCompLog.txt");
                entryCompResultWriter = new StreamWriter("EntryInterfaceCompResults.txt");
            }

            DomainInterface[] domainInterfaces1 = GetEntryUniqueDomainInterfaces(relSeqId, pdbId1);
            DomainInterface[] domainInterfaces2 = GetEntryUniqueDomainInterfaces(relSeqId, pdbId2);

            DomainInterfacePairInfo[] compInfos = CompareDomainInterfaces(domainInterfaces1, domainInterfaces2);

            UpdateDomainInterfaceComp(relSeqId, pdbId1, pdbId2, compInfos);

            logWriter.Close();
            compResultWriter.Close();
            entryCompResultWriter.Close();
        }

      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryPairs"></param>
        public void CompareDomainInterfacesForEntryPairs(string[] entryPairs)
        {
            ProtCidSettings.dataType = "pfam";
            DomainInterfaceTables.InitializeTables();

            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults.txt");
                logWriter = new StreamWriter("CrystDomainInterfaceCompLog.txt");
                entryCompResultWriter = new StreamWriter("EntryInterfaceCompResults.txt");
            }
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = entryPairs.Length;
            ProtCidSettings.progressInfo.totalOperationNum = entryPairs.Length;

            Dictionary<string, int[]> entryRelSeqIdsHash = new Dictionary<string,int[]> ();
            foreach (string entryPair in entryPairs)
            {
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentFileName = entryPair;

                string[] entries = ParseHelper.SplitPlus(entryPair, ' ');
                int[] relSeqIDs1 = GetEntryRelSeqIDs(entries[0], ref entryRelSeqIdsHash);
                int[] relSeqIDs2 = GetEntryRelSeqIDs(entries[1], ref entryRelSeqIdsHash);
                try
                {
                    for (int i = 0; i < relSeqIDs1.Length; i++)
                    {
                        for (int j = 0; j < relSeqIDs2.Length; j++)
                        {
                            if (relSeqIDs1[i] == relSeqIDs2[j])
                            {
                                UpdateDomainInterfaceCompData(relSeqIDs1[i], entries[0], entries[1]);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine("Parsing " + entryPair + " errors: " + ex.Message);
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Parsing " + entryPair + " errors: " + ex.Message);
                }
            }
            logWriter.Close();
            compResultWriter.Close();
            entryCompResultWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.progressInfo.threadFinished = true;
        }

        private void UpdateDomainInterfaceCompData(int relSeqId, string pdbId1, string pdbId2)
        {
            DomainInterface[] domainInterfaces1 = GetEntryUniqueDomainInterfaces(relSeqId, pdbId1);
            DomainInterface[] domainInterfaces2 = GetEntryUniqueDomainInterfaces(relSeqId, pdbId2);

            DomainInterfacePairInfo[] compInfos = CompareDomainInterfaces(domainInterfaces1, domainInterfaces2);

            UpdateDomainInterfaceComp(relSeqId, pdbId1, pdbId2, compInfos);
        }

        private void UpdateDomainInterfaceCompData(int relSeqId, string pdbId1, string pdbId2, DomainInterface[] domainInterfaces1, DomainInterface[] domainInterfaces2)
        {
            DomainInterfacePairInfo[] compInfos = CompareDomainInterfaces(domainInterfaces1, domainInterfaces2);
            UpdateDomainInterfaceComp(relSeqId, pdbId1, pdbId2, compInfos);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="compInfos"></param>
        private void UpdateDomainInterfaceComp(int relSeqId, string pdbId1, string pdbId2, 
            DomainInterfacePairInfo[] compInfos)
        {
            string deleteString = string.Format("Delete From {0} " + 
                " Where RelSeqId = {1} AND ((PdbID1 = '{2}' AND PdbID2 = '{3}') " + 
                " OR (PdbID1 = '{3}' AND PdbID2 = '{2}'));", 
                DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp].TableName,
                relSeqId, pdbId1, pdbId2);
            ProtCidSettings.protcidQuery.Query( deleteString);

            AssignDomainInterfaceCompTable(relSeqId, pdbId1, pdbId2, compInfos);

            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, 
                DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp]);

            WriteCompResultToFile(DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp], compResultWriter);
            
            DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp].Clear();
        }

        /// <summary>
        /// update those domain interface comp without fatcat alignments first
        /// </summary>
        public void UpdateMissingAlignmentsPfamDomainInterfaceComp()
        {
            bool resultNeedClosed = false;
            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("MissingAlignedInterfaceCompResults.txt");
                entryCompResultWriter = new StreamWriter("MissingAlignedEntryInterfaceCompResults.txt");
                logWriter = new StreamWriter("MissingAlignedDomainInterfaceCompLog.txt");

                resultNeedClosed = true;
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Read entry pairs from debug file.");
            Dictionary<string, List<string>> needCompEntryPairHash = ReadMissingAlignedEntryPairs();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Delete Obsolete Domain interface comp for those entry pairs.");
            DeleteObsCompEntryPairs(needCompEntryPairHash);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Formatting entry pairs to be compared in each relation.");
            Dictionary<int, Dictionary<string, List<string>>> relationCompEntryHash = GetRelationEntryCompPairs(needCompEntryPairHash);

            ProtCidSettings.progressInfo.totalOperationNum = relationCompEntryHash.Count;
            ProtCidSettings.progressInfo.totalStepNum = relationCompEntryHash.Count;
            ProtCidSettings.progressInfo.currentOperationLabel = "Update Pfam Domain Interface Comp";

            foreach (int relSeqId in relationCompEntryHash.Keys)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString ());

                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    Dictionary<string, List<string>> needCompEntryHash = relationCompEntryHash[relSeqId];
                    int entryPairCount = 0;
                    Dictionary<string, DomainInterface[]> entryDomainInterfaceHash = GetEntryDomainInterfaces(relSeqId, needCompEntryHash, out entryPairCount);
                    CompareEntryDomainInterfaces(relSeqId, needCompEntryHash, entryDomainInterfaceHash, entryPairCount);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Errors: " + relSeqId.ToString () + " " + ex.Message);
                    logWriter.WriteLine("Errors: " + relSeqId.ToString() + " " + ex.Message);
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
            if (resultNeedClosed)
            {
                compResultWriter.Close();
                entryCompResultWriter.Close();
                logWriter.Close();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="needCompEntryHash"></param>
        /// <returns></returns>
        private Dictionary<string, DomainInterface[]> GetEntryDomainInterfaces(int relSeqId, Dictionary<string, List<string>> needCompEntryHash, out int entryPairCount)
        {
            Dictionary<string, DomainInterface[]> entryDomainInterfaceHash = new Dictionary<string,DomainInterface[]>  ();
            entryPairCount = 0;
            foreach (string entry in needCompEntryHash.Keys)
            {
                List<string> compEntryList = needCompEntryHash[entry];
                entryPairCount += compEntryList.Count;
                compEntryList.Insert(0, entry);
                foreach (string pdbId in compEntryList)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId);
                    if (entryDomainInterfaceHash.ContainsKey(pdbId))
                    {
                        continue;
                    }
                    DomainInterface[] uniqueDomainInterfaces =
                        GetEntryUniqueDomainInterfaces(relSeqId, pdbId);
                    entryDomainInterfaceHash.Add(pdbId, uniqueDomainInterfaces);
                }
            }
            return entryDomainInterfaceHash;
        }
        /// <summary>
        /// seperate each entry into different relation and its comp entries
        /// entry and its comp entries in each relation
        /// </summary>
        /// <param name="needCompEntryPairHash"></param>
        /// <returns></returns>
        private Dictionary<int, Dictionary<string, List<string>>> GetRelationEntryCompPairs(Dictionary<string, List<string>> needCompEntryPairHash)
        {
            Dictionary<string,int[]> entryRelSeqIdsHash = new Dictionary<string,int[]> ();
            Dictionary<int, Dictionary<string, List<string>>> relationCompEntryHash = new Dictionary<int, Dictionary<string, List<string>>>();

            foreach (string entry in needCompEntryPairHash.Keys)
            {
                List<string> needCompEntryList = needCompEntryPairHash[entry];
                int[] relSeqIds = GetEntryRelSeqIDs(entry, ref entryRelSeqIdsHash);
                foreach (string compEntry in needCompEntryList)
                {
                    int[] compRelSeqIds = GetEntryRelSeqIDs(compEntry, ref entryRelSeqIdsHash);
                    foreach (int relSeqId in relSeqIds)
                    {
                        if (Array.IndexOf(compRelSeqIds, relSeqId) > -1)
                        {
                            if (relationCompEntryHash.ContainsKey(relSeqId))
                            {
                                if (relationCompEntryHash[relSeqId].ContainsKey(entry))
                                {
                                    relationCompEntryHash[relSeqId][entry].Add(compEntry);
                                }
                                else
                                {
                                    List<string> compEntryList = new List<string> ();
                                    compEntryList.Add(compEntry);
                                    relationCompEntryHash[relSeqId].Add(entry, compEntryList);
                                }
                            }
                            else
                            {
                                Dictionary<string, List<string>> entryCompEntryHash = new Dictionary<string,List<string>> ();
                                List<string> compEntryList = new List<string> ();
                                compEntryList.Add(compEntry);
                                entryCompEntryHash.Add(entry, compEntryList);
                                relationCompEntryHash.Add(relSeqId, entryCompEntryHash);
                            }
                        }
                    }
                }
            }
            return relationCompEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entryRelSeqIdsHash"></param>
        /// <returns></returns>
        private int[] GetEntryRelSeqIDs(string pdbId, ref Dictionary<string, int[]> entryRelSeqIdsHash)
        {
            if (entryRelSeqIdsHash.ContainsKey(pdbId))
            {
                return (int[])entryRelSeqIdsHash[pdbId];
            }
            int[] relSeqIds = GetEntryRelSeqIDs(pdbId);
            entryRelSeqIdsHash.Add(pdbId, relSeqIds);
            return relSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, List<string>> ReadMissingAlignedEntryPairs()
        {
            StreamReader dataReader = new StreamReader("NonAlignedDomainInterfacePairs0.txt");
            string line = "";
            Dictionary<string, List<string>> needCompEntryPairHash = new Dictionary<string, List<string>>();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = ParseHelper.SplitPlus(line, ' ');
                if (needCompEntryPairHash.ContainsKey(fields[0]))
                {
                    if (!needCompEntryPairHash[fields[0]].Contains(fields[1]))
                    {
                        needCompEntryPairHash[fields[0]].Add(fields[1]);
                    }
                }
                else
                {
                    List<string> compEntryList = new List<string> ();
                    compEntryList.Add(fields[1]);
                    needCompEntryPairHash.Add(fields[0], compEntryList);
                }
            }
            dataReader.Close();
            return needCompEntryPairHash;
        }
        /// <summary>
        /// 
        /// </summary>
        public void CopyCrystFiles()
        {
            string crystFileSrcDir = @"D:\DbProjectData\InterfaceFiles_update\cryst";
            string crystFileDestDir = @"D:\DbProjectData\InterfaceFiles_update\tempCryst";
            DateTime dt = new DateTime(2012, 01, 14);
            ParseHelper.CopyNewFiles(crystFileSrcDir, crystFileDestDir, dt);
        }
        #endregion

        #region for debug
        /// <summary>
        /// 
        /// </summary>
        public void UpdateMultiChainDomainInterfaces()
        {
            Dictionary<int, Dictionary<string, int[]>> updateRelDomainInterfaceHash = GetRelationMultiChainDomainInterfaceHash();
            Dictionary<int, string[]> updateRelEntryDict = new Dictionary<int,string[]> ();
            foreach (int relSeqId in updateRelDomainInterfaceHash.Keys)
            { 
                List<string> entryList = new List<string> (updateRelDomainInterfaceHash[relSeqId].Keys);
                string[] entries = new string[entryList.Count];
                entryList.CopyTo(entries);
                updateRelEntryDict.Add(relSeqId, entries);
            }

            UpdateEntryDomainInterfaceComp(updateRelEntryDict);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<int, Dictionary<string, int[]>> GetRelationMultiChainDomainInterfaceHash()
        {
            string updateRelDomainInterfaceFile = "UpdateRelMultiChainDomainInterfaces.txt";
            int relSeqId = 0;
            int domainInterfaceId = 0;
            string pdbId = "";
            long domainId = 0;
            Dictionary<int, Dictionary<string, int[]>> relMultiChainInterfaceHash = new Dictionary<int,Dictionary<string,int[]>> ();
            if (File.Exists(updateRelDomainInterfaceFile))
            {
                StreamReader dataReader = new StreamReader(updateRelDomainInterfaceFile);
                string dataLine = "";
                Dictionary<string, int[]> entryDomainInterfaceIdHash = new Dictionary<string,int[]> ();
                while ((dataLine = dataReader.ReadLine()) != null)
                {
                    if (dataLine == "")
                    {
                        continue;
                    }
                    if (dataLine.Substring(0, 1) == "#")
                    {
                        if (entryDomainInterfaceIdHash.Count > 0)
                        {
                            relMultiChainInterfaceHash.Add(relSeqId, entryDomainInterfaceIdHash);
                            entryDomainInterfaceIdHash = new Dictionary<string,int[]> ();
                        }
                        relSeqId = Convert.ToInt32(dataLine.Substring(1, dataLine.Length - 1));
                    }
                    else
                    {
                        string[] fields = dataLine.Split(',');
                        pdbId = fields[0];
                        int[] domainInterfaceIds = new int[fields.Length - 1];
                        for (int i = 1; i < fields.Length; i++)
                        {
                            domainInterfaceIds[i - 1] = Convert.ToInt32(fields[i]);
                        }
                        entryDomainInterfaceIdHash.Add(pdbId, domainInterfaceIds);
                    }
                }
                dataReader.Close();
            }
            else
            {
                Dictionary<int, Dictionary<string, List<int>>> relMultiChainInterfaceListHash = new Dictionary<int, Dictionary<string, List<int>>>();
                string queryString = "Select PdbId, DomainID, Count(Distinct EntityID) As EntityCount From PdbPfam Group By PdbID, DomainID;";
                DataTable domainEntityCountTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                int entityCount = 0;
                string entryDomain = "";
                List<string> multiChainDomainList = new List<string> ();
                foreach (DataRow entityCountRow in domainEntityCountTable.Rows)
                {
                    entityCount = Convert.ToInt32(entityCountRow["EntityCount"].ToString());
                    if (entityCount > 1)
                    {
                        entryDomain = entityCountRow["PdbID"].ToString() + entityCountRow["DomainID"].ToString();
                        multiChainDomainList.Add(entryDomain);
                    }
                }
                
                foreach (string lsEntryDomain in multiChainDomainList)
                {
                    pdbId = lsEntryDomain.Substring(0, 4);
                    domainId = Convert.ToInt64(lsEntryDomain.Substring(4, lsEntryDomain.Length - 4));
                    queryString = string.Format("Select Distinct RelSeqID, PdbID, DomainInterfaceID From PfamDomainInterfaces Where PdbID = '{0}' AND (DomainID1 = {1} OR DomainID2 = {1});",
                        pdbId, domainId);
                    DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
                    foreach (DataRow domainInterfaceRow in domainInterfaceTable.Rows)
                    {
                        relSeqId = Convert.ToInt32(domainInterfaceRow["RelSeqID"].ToString());
                        domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
                        if (relMultiChainInterfaceListHash.ContainsKey(relSeqId))
                        {
                            if (relMultiChainInterfaceListHash[relSeqId].ContainsKey(pdbId))
                            {
                                relMultiChainInterfaceListHash[relSeqId][pdbId].Add(domainInterfaceId);
                            }
                            else
                            {
                                List<int> domainInterfaceIdList = new List<int> ();
                                domainInterfaceIdList.Add(domainInterfaceId);
                                relMultiChainInterfaceListHash[relSeqId].Add(pdbId, domainInterfaceIdList);
                            }
                        }
                        else
                        {
                            Dictionary<string, List<int>> entryDomainInterfaceHash = new Dictionary<string,List<int>> ();
                            List<int> domainInterfaceIdList = new List<int> ();
                            domainInterfaceIdList.Add(domainInterfaceId);
                            entryDomainInterfaceHash.Add(pdbId, domainInterfaceIdList);
                            relMultiChainInterfaceListHash.Add(relSeqId, entryDomainInterfaceHash);
                        }
                    }
                }
                StreamWriter dataWriter = new StreamWriter(updateRelDomainInterfaceFile);
                string line = "";
                List<int> relSeqIdList = new List<int>(relMultiChainInterfaceListHash.Keys);
                relSeqIdList.Sort();
                foreach (int lsRelSeqId in relSeqIdList)
                {
                    dataWriter.WriteLine("#" + lsRelSeqId.ToString());
                    Dictionary<string, int[]> entryDomainInterfaceHash = new Dictionary<string, int[]>();
                    foreach (string lsPdbId in relMultiChainInterfaceListHash[lsRelSeqId].Keys)
                    {
                        entryDomainInterfaceHash.Add(lsPdbId, relMultiChainInterfaceListHash[lsRelSeqId][lsPdbId].ToArray());
                        line = lsPdbId;
                        foreach (int lsDomainInterfaceId in relMultiChainInterfaceListHash[lsRelSeqId][lsPdbId])
                        {
                            line += ("," + lsDomainInterfaceId.ToString());
                        }
                        dataWriter.WriteLine(line);
                    }
                    relMultiChainInterfaceHash.Add(lsRelSeqId, entryDomainInterfaceHash);
                }
                dataWriter.Close();
            }
            return relMultiChainInterfaceHash;
        }


        private DbUpdate dbUpdate = new DbUpdate();
        public void DeleteDomainInterfaceComp()
        {
            /*      StreamReader entryReader = new StreamReader("WrongDInterfaceEntries_notdelete.txt");
                  string line = "";
                  ArrayList entryList = new ArrayList();
                  while ((line = entryReader.ReadLine()) != null)
                  {
                      entryList.Add(line);
                  }
                  entryReader.Close();
            */
            StreamWriter dataWriter = new StreamWriter("EntryCompDeletedDomainInterfaces.txt");
            string queryString = "Select Distinct PdbID From PfamDomainInterfaces;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Delete interface comp for those not exist domain interfaces");
            ProtCidSettings.progressInfo.totalOperationNum = entryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = entryTable.Rows.Count;

            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();

                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                int[] domainInterfaceIds = GetEntryDomainInterfaceIdsInComp (pdbId);
                int[] domainInterfaceIdsNotDefined = GetCompDomainInterfaceIdsNotDefined(pdbId, domainInterfaceIds);
                if (domainInterfaceIdsNotDefined.Length > 0)
                {
                    DeleteEntryInterfaceCompData(pdbId, domainInterfaceIdsNotDefined);
                    dataWriter.WriteLine(pdbId + ","  + FormatIntArray(domainInterfaceIdsNotDefined));
                }
            }
            dataWriter.Close();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceIds"></param>
        /// <returns></returns>
        private string FormatIntArray(int[] domainInterfaceIds)
        {
            string domainInterfaceIdsString = "";
            foreach (int domainInterfaceId in domainInterfaceIds)
            {
                domainInterfaceIdsString += (domainInterfaceId.ToString() + ",");
            }
            domainInterfaceIdsString = domainInterfaceIdsString.TrimEnd(',');
            return domainInterfaceIdsString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceIds"></param>
        /// <returns></returns>
        private int[] GetCompDomainInterfaceIdsNotDefined(string pdbId, int[] domainInterfaceIds)
        {
            List<int> leftDomainInterfaceIdList = new List<int> (domainInterfaceIds);
            string queryString = string.Format("Select DomainInterfaceID From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
            DataTable domainInterfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int domainInterfaceId = 0;
            foreach (DataRow domainInterfaceRow in domainInterfaceIdTable.Rows)
            {
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
                leftDomainInterfaceIdList.Remove(domainInterfaceId);
            }
            return leftDomainInterfaceIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceIds"></param>
        /// <returns></returns>
        private int[] GetEntryCompDomainInterfaceIdsNotDefined(string pdbId, int[] domainInterfaceIds)
        {
            List<int> leftDomainInterfaceIdList = new List<int> (domainInterfaceIds);
            string queryString = string.Format("Select DomainInterfaceID From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
            DataTable domainInterfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int domainInterfaceId = 0;
            foreach (DataRow domainInterfaceRow in domainInterfaceIdTable.Rows)
            {
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
                leftDomainInterfaceIdList.Remove(domainInterfaceId);
            }

            return leftDomainInterfaceIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceIds"></param>
        private void DeleteInterfaceCompData(string pdbId, int[] domainInterfaceIds)
        {
            foreach (int domainInterfaceId in domainInterfaceIds)
            {
                DeleteDomainInterfaceCompData (pdbId, domainInterfaceId);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        private void DeleteDomainInterfaceCompData(string pdbId, int domainInterfaceId)
        {
            string deleteString = string.Format("Delete From PfamDomainInterfaceComp " + 
                " Where (PdbID1 = '{0}' AND DomainInterfaceID1 = {1}) OR (PdbID2 = '{0}' AND DomainInterfaceID2 = {1});", pdbId, domainInterfaceId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceIds"></param>
        private void DeleteEntryInterfaceCompData(string pdbId, int[] domainInterfaceIds)
        {
            foreach (int domainInterfaceId in domainInterfaceIds)
            {
                DeleteEntryDomainInterfaceCompData(pdbId, domainInterfaceId);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        private void DeleteEntryDomainInterfaceCompData(string pdbId, int domainInterfaceId)
        {
            string deleteString = string.Format("Delete From PfamEntryDomainInterfaceComp " +
                " Where PdbID = '{0}' AND (DomainInterfaceID1 = {1} OR DomainInterfaceID2 = {1});", pdbId, domainInterfaceId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetDomainInterfaceIdsInComp(string pdbId)
        {
            string queryString = string.Format("Select Distinct DomainInterfaceID1 From PfamDomainInterfaceComp Where PdbID1 = '{0}';", pdbId);
            DataTable domainInterfaceId1Table = ProtCidSettings.protcidQuery.Query( queryString);
            List<int> domainInterfaceIdList = new List<int> ();
            int domainInterfaceId = 0;
            foreach (DataRow domainInterfaceRow in domainInterfaceId1Table.Rows)
            {
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID1"].ToString());
                if (!domainInterfaceIdList.Contains(domainInterfaceId))
                {
                    domainInterfaceIdList.Add(domainInterfaceId);
                }
            }

            queryString = string.Format("Select Distinct DomainInterfaceID2 From PfamDomainInterfaceComp Where PdbID2 = '{0}';", pdbId);
            DataTable domainInterfaceId2Table = ProtCidSettings.protcidQuery.Query( queryString);
            foreach (DataRow domainInterfaceRow in domainInterfaceId2Table.Rows)
            {
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID2"].ToString());
                if (!domainInterfaceIdList.Contains(domainInterfaceId))
                {
                    domainInterfaceIdList.Add(domainInterfaceId);
                }
            }
            return domainInterfaceIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetEntryDomainInterfaceIdsInComp(string pdbId)
        {
            string queryString = string.Format("Select DomainInterfaceID1, DomainInterfaceID2 From PfamEntryDomainInterfaceComp Where PdbID = '{0}';", pdbId);
            DataTable domainInterfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<int> domainInterfaceIdList = new List<int> ();
            int domainInterfaceId = 0;
            foreach (DataRow domainInterfaceRow in domainInterfaceIdTable.Rows)
            {
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID1"].ToString());
                if (!domainInterfaceIdList.Contains(domainInterfaceId))
                {
                    domainInterfaceIdList.Add(domainInterfaceId);
                }

                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID2"].ToString());
                if (!domainInterfaceIdList.Contains(domainInterfaceId))
                {
                    domainInterfaceIdList.Add(domainInterfaceId);
                }
            }

            return domainInterfaceIdList.ToArray ();
        }

        /// <summary>
        /// /
        /// </summary>
        public void InsertDomainInterfaceCompInfoToDb()
        {
            StreamReader dataReader = new StreamReader("dbInsertErrorLog0.txt");
            string line = "";
            string insertString = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("INSERT INTO") > -1)
                {
                    insertString = line.Replace("NaN", "-1");
                    dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, insertString);
                }
            }
            dataReader.Close();
        }

        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="entry"></param>
        /// <param name="compEntries"></param>
        /// <param name="entryDomainInterfaceHash"></param>
        public void CompareEntryDomainInterfacesForDebugNoOutput(int relSeqId, string entry, string[] compEntries)
        {
            DomainInterfacePairInfo[] pairCompInfos = null;
            DomainInterface[] domainInterfaces1 =
                        GetEntryUniqueDomainInterfaces(relSeqId, entry);
            if (domainInterfaces1 == null)
            {
                noSimDomainInterfacesWriter.WriteLine("No domain interfaces in entry " + entry);
                return;
            }
            foreach (string compEntry in compEntries)
            {
                pairCompInfos = null;

                if (entry == compEntry)
                {
                    continue;
                }
                if (ExistDomainInterfaceCompInDb(relSeqId, entry, compEntry))
                {
                    continue;
                }
                try
                {
                    DomainInterface[] domainInterfaces2 = GetEntryUniqueDomainInterfaces(relSeqId, compEntry);
                    if (domainInterfaces2 == null)
                    {
                        noSimDomainInterfacesWriter.WriteLine("No domain interfaces in entry " + compEntry);
                        continue;
                    }
                    try
                    {
                        pairCompInfos = CompareDomainInterfaces(domainInterfaces1, domainInterfaces2);
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine("Compare Inter-chain domain interfaces for " + entry
                            + " and " + compEntry + " errors: " + ex.Message);
                        logWriter.Flush();
                    }
                    if (pairCompInfos == null || pairCompInfos.Length == 0)
                    {
                        noSimDomainInterfacesWriter.WriteLine(entry + "_" + compEntry);
                        continue;
                    }
                    try
                    {
                        AssignDomainInterfaceCompTable(relSeqId, entry, compEntry, pairCompInfos);
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine("Assign comparison of " + entry + "_" + compEntry + " to data table errors:  "
                                     + ex.Message);
                        logWriter.Flush();
                    }
                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, 
                        DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp]);
                    WriteCompResultToFile(DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp], compResultWriter);
                    DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp].Clear();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(entry + "_" + compEntry + " error: " + ex.Message);

                    logWriter.WriteLine("Compare " + entry + "_" + compEntry + " domain interfaces error:  "
                                     + ex.Message);
                    logWriter.Flush();
                }
            }
           
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private Dictionary<int, string[]> GetEntryRelationHash(string[] updateEntries)
        {
            Dictionary<int, List<string>> relEntryListHash = new Dictionary<int,List<string>> ();
            foreach (string pdbId in updateEntries)
            {
                int[] relSeqIds = GetEntryRelSeqIds(pdbId);
                foreach (int relSeqId in relSeqIds)
                {
                    if (relEntryListHash.ContainsKey(relSeqId))
                    {
                        relEntryListHash[relSeqId].Add(pdbId);
                    }
                    else
                    {
                        List<string> entryList = new List<string> ();
                        entryList.Add(pdbId);
                        relEntryListHash.Add(relSeqId, entryList);
                    }
                }
            }
            Dictionary<int, string[]> relEntryHash = new Dictionary<int, string[]>();
            foreach (int relSeqId in relEntryListHash.Keys)
            {
                relEntryHash.Add (relSeqId, relEntryListHash[relSeqId].ToArray ());
            }
            return relEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetEntryRelSeqIds(string pdbId)
        {
            string queryString = string.Format("Select Distinct RelSeqID From PfamDomainInterfaces WHere PdbID = '{0}';", pdbId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] relSeqIDs = new int[relSeqIdTable.Rows.Count];
            int count = 0;
            int relSeqId = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString ());
                relSeqIDs[count] = relSeqId;
                count++;
            }
            return relSeqIDs;
        }
        /// <summary>
        /// 
        /// </summary>
        public void DeleteInterfaceCompData()
        {
            Dictionary<int, List<string>> relationBugEntryHash = ReadRelationBugEntryHash();
            List<string> deleteEntryList = new List<string> ();
            foreach (int relSeqId in relationBugEntryHash.Keys)
            {
                foreach (string pdbId in relationBugEntryHash[relSeqId])
                {
                    DeleteEntryDomainInterfaceCompInfo(relSeqId, pdbId);
                    if (!deleteEntryList.Contains(pdbId))
                    {
                        deleteEntryList.Add(pdbId);
                    }
                }
            }
            foreach (string pdbId in deleteEntryList)
            {
                DeleteEntryDomainInterfaceCompInfo(pdbId);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<int, List<string>> ReadRelationBugEntryHash()
        {
            Dictionary<int, List<string>> relationBugEntryHash = new Dictionary<int,List<string>> ();
            StreamReader dataReader = new StreamReader("CrystDomainInterfaceCompLog_2less50_1.txt");
            string line = "";
            int relSeqId = 0;
            string pdbId = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("/") > -1)
                {
                    continue;
                }
                if (line.IndexOf("_") > -1)
                {
                    continue;
                }
                string[] fields = line.Split();
                if (fields.Length == 1)
                {
                    relSeqId = Convert.ToInt32(fields[0]);
                }
                else
                {
                    if (line.IndexOf("Index was outside the bounds of the array.") > -1)
                    {
                        pdbId = fields[4];
                        if (relationBugEntryHash.ContainsKey(relSeqId))
                        {
                            if (!relationBugEntryHash[relSeqId].Contains(pdbId))
                            {
                                relationBugEntryHash[relSeqId].Add(pdbId);
                            }
                        }
                        else
                        {
                            List<string> entryList = new List<string> ();
                            entryList.Add(pdbId);
                            relationBugEntryHash.Add(relSeqId, entryList);
                        }
                    }
                }
            }
            dataReader.Close();
            return relationBugEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="entry"></param>
        /// <param name="compEntries"></param>
        /// <param name="entryDomainInterfaceHash"></param>
        public void CompareEntryDomainInterfacesForDebug (int relSeqId, string entry, string[] compEntries)
        {
            bool resultNeedClosed = false;
            if (compResultWriter == null)
            {
                compResultWriter = new StreamWriter("InterfaceCompResults" + relSeqId.ToString() + ".txt");
                entryCompResultWriter = new StreamWriter("EntryInterfaceCompResults" + relSeqId.ToString() + ".txt");
                logWriter = new StreamWriter("DomainInterfaceCompLog" + relSeqId.ToString() + ".txt");

                resultNeedClosed = true;
            }

            DomainInterfacePairInfo[] pairCompInfos = null;
            DomainInterface[] domainInterfaces1 =
                        GetEntryUniqueDomainInterfaces(relSeqId, entry);
            if (domainInterfaces1 == null)
            {
                noSimDomainInterfacesWriter.WriteLine("No domain interfaces in entry " + entry);
                return;
            }
            foreach (string compEntry in compEntries)
            {
                pairCompInfos = null;

                if (entry == compEntry)
                {
                    continue;
                }

                try
                {
                    DomainInterface[] domainInterfaces2 = GetEntryUniqueDomainInterfaces(relSeqId, compEntry);
                    if (domainInterfaces2 == null)
                    {
                        noSimDomainInterfacesWriter.WriteLine("No domain interfaces in entry " + compEntry);
                        continue;
                    }
                    try
                    {
                        pairCompInfos = CompareDomainInterfaces(domainInterfaces1, domainInterfaces2);
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine("Compare Inter-chain domain interfaces for " + entry
                            + " and " + compEntry + " errors: " + ex.Message);
                        logWriter.Flush();
                    }
                    if (pairCompInfos == null || pairCompInfos.Length == 0)
                    {
                        noSimDomainInterfacesWriter.WriteLine(entry + "_" + compEntry);
                        continue;
                    }
                    try
                    {
                        AssignDomainInterfaceCompTable(relSeqId, entry, compEntry, pairCompInfos);
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine("Assign comparison of " + entry + "_" + compEntry + " to data table errors:  "
                                     + ex.Message);
                        logWriter.Flush();
                    }
                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, 
                        DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp]);
                    WriteCompResultToFile(DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp], compResultWriter);
                    DomainInterfaceTables.domainInterfaceTables[DomainInterfaceTables.DomainInterfaceComp].Clear();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(entry + "_" + compEntry + " error: " + ex.Message);

                    logWriter.WriteLine("Compare " + entry + "_" + compEntry + " domain interfaces error:  "
                                     + ex.Message);
                    logWriter.Flush();
                }
            }
            if (resultNeedClosed)
            {
                compResultWriter.Close();
                entryCompResultWriter.Close();
                logWriter.Close();
            }     
        }

        /// <summary>
        /// 
        /// </summary>
        public void GetDomainInterfaceWithMultiChainDomains()
        {
            StreamWriter dataWriter = new StreamWriter("WrongMultChainDomainInterfaces.txt");
            string queryString = "Select PdbID, DomainID, Count(Distinct EntityID) AS EntityCount From PdbPfam Group By PdbID, DomainID;";
            DataTable domainEntityCountTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pdbId = "";
            long domainId = 0;
            int entityCount = 0;
            int numOfWrongMultChainDomainInterfaces = 0;
            foreach (DataRow domainEntityCountRow in domainEntityCountTable.Rows)
            {
                pdbId = domainEntityCountRow["PdbID"].ToString();
                domainId = Convert.ToInt64(domainEntityCountRow["DomainID"].ToString ());
                entityCount = Convert.ToInt32 (domainEntityCountRow["EntityCount"].ToString ());
                if (entityCount > 1)
                {
                    GetDomainInterfacesWithMultEntities(pdbId, domainId, dataWriter, ref numOfWrongMultChainDomainInterfaces);

                }
            }
            dataWriter.Close();
        }

        private void GetDomainInterfacesWithMultEntities(string pdbId, long domainId, StreamWriter dataWriter, ref int numOfWrongMultChainDomainInterfaces)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces Where PdbID = '{0}' AND (DomainID1 = {1} OR DomainID2 = {1});", pdbId, domainId);
            DataTable multDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            queryString = string.Format("Select * From PdbPfamChain Where PdbID = '{0}' AND DomainID = {1};", pdbId, domainId);
            DataTable chainPfamTable = ProtCidSettings.pdbfamQuery.Query( queryString);


            List<int> domainInterfaceList = new List<int> ();
            int domainInterfaceId = 0;
            foreach (DataRow domainInterfaceRow in multDomainInterfaceTable.Rows)
            {
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString ());
                if (!domainInterfaceList.Contains(domainInterfaceId))
                {
                    domainInterfaceList.Add(domainInterfaceId);
                }
            }
            foreach (int dInterfaceId in domainInterfaceList)
            {
                DataRow[] domainInterfaceRows = multDomainInterfaceTable.Select(string.Format ("DomainInterfaceID = '{0}'", dInterfaceId));
                string[] entityPairs = GetEntityPairsInDomainInterface(domainInterfaceRows, chainPfamTable);
                if (IsEntityPairsOverlap(entityPairs))
                {
                    dataWriter.WriteLine(ParseHelper.FormatDataRows(domainInterfaceRows));
                    dataWriter.Flush();

                    numOfWrongMultChainDomainInterfaces++;
                }
            }
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="entityPairs"></param>
        /// <returns></returns>
        private bool IsEntityPairsOverlap(string[] entityPairs)
        {
            for (int i = 0; i < entityPairs.Length; i++)
            {
                for (int j = i + 1; j < entityPairs.Length; j++)
                {
                    if (entityPairs[i] == entityPairs[j])
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
        /// <param name="domainInterfaceRows"></param>
        /// <param name="chainPfamTable"></param>
        /// <returns></returns>
        private string[] GetEntityPairsInDomainInterface(DataRow[] domainInterfaceRows, DataTable chainPfamTable )
        {
            List<string> entityPairList = new List<string> ();
            string pdbId = "";
            long domainId = 0;
            string asymChain = "";
            int entityId1 = 0;
            int entityId2 = 0;
            foreach (DataRow domainInterfaceRow in domainInterfaceRows)
            {
                pdbId = domainInterfaceRow["PdbID"].ToString();
                domainId = Convert.ToInt64 (domainInterfaceRow["DomainID1"].ToString ());
                asymChain = domainInterfaceRow["AsymChain1"].ToString().TrimEnd();
                entityId1 = GetInterfaceEntity(pdbId, domainId, asymChain, chainPfamTable);

                domainId = Convert.ToInt64(domainInterfaceRow["DomainID2"].ToString());
                asymChain = domainInterfaceRow["AsymChain2"].ToString().TrimEnd();
                entityId2 = GetInterfaceEntity(pdbId, domainId, asymChain, chainPfamTable);

                entityPairList.Add(entityId1.ToString () + "_" + entityId2.ToString ());
            }

            return entityPairList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryChainPfam(string pdbId)
        {
            string queryString = string.Format("Select * From PdbPfamChain Where PdbID = '{0}';", pdbId);
            DataTable chainPfamTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return chainPfamTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <param name="asymChain"></param>
        /// <param name="pfamChainTable"></param>
        /// <returns></returns>
        private int GetInterfaceEntity(string pdbId, long domainId, string asymChain, DataTable pfamChainTable )
        {
            DataRow[] chainRows = pfamChainTable.Select(string.Format ("PdbID = '{0}' AND DomainID = '{1}' AND AsymChain = '{2}'", pdbId, domainId, asymChain));
            if (chainRows.Length > 0)
            {
                return Convert.ToInt32(chainRows[0]["EntityID"].ToString ());
            }
            return -1;
        }
        #endregion
    }
}
