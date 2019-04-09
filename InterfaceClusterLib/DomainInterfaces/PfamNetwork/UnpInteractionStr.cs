using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamNetwork
{
    /// <summary>
    /// this class is to build/update the interactions between uniprot codes in the PDB
    /// </summary>
    public class UnpInteractionStr
    {
        #region member variables
        private DbInsert dbInsert = new DbInsert();
        private DbUpdate dbUpdate = new DbUpdate();
        private string unpInterNetTableName = "UnpPdbDomainInterfaces";
        private DataTable unpInterNetTable = null;
        private string unpPdbfamTableName = "UnpPdbfam";
        DataTable dbUnpPdbfamTable = null;
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void BuildUnpInteractionNetTable ()
        {
            bool isUpdate = false;
            CreatUnpInterNetTables (isUpdate);

            dbUnpPdbfamTable = SetUnpPdbfamTableInDb();

            ProtCidSettings.logWriter.WriteLine(DateTime.Today.ToShortDateString ());
            ProtCidSettings.logWriter.WriteLine("Uniprots interactions in PDB");

            ProtCidSettings.logWriter.WriteLine("Retrieve PDBfam from db");
            DataTable pdbfamTable = SetPdbPfamAssignTable();  // get pfam assignments of pdb

            ProtCidSettings.logWriter.WriteLine("Unp-Pdb entity table from DB");
            DataTable unpEntityTable = GetUnpEntityTable();  // unp and pdb entity match based on sifts and xml, modified on Dec. 3, 2018

            ProtCidSettings.logWriter.WriteLine("Add Unp info columns");
            DataTable unpPdbfamTable = SetUnpPdbfamAssignTable(pdbfamTable);  // pdbfam with unp columns: unpId, unpstart, unpend

            ProtCidSettings.logWriter.WriteLine("Unp-Pdb sequence match data");
            DataTable unpPdbEntitySeqMatchTableSifts = SetUnpPdbSeqMatchTable(); // sequence mapping between pdb and unp based on SIFTs
            DataTable unpPdbEntitySeqMatchTableXml = SetUnpPdbSeqMatchTableFromXml();

            // add unp info columns to pdbfam           
            ProtCidSettings.logWriter.WriteLine("Build UnpPdbfam table, add uniprot ID, start and end position to Pdbfam domains ");
            AddUnpToPdbfamData(pdbfamTable, unpEntityTable, unpPdbfamTable, unpPdbEntitySeqMatchTableSifts, unpPdbEntitySeqMatchTableXml);           
           
            ProtCidSettings.logWriter.WriteLine("Get the list of Uniprots with PDBfam");
            unpPdbfamTable = ReadUnpPdbfamFromDb();
            List<string> unpCodesInPfam = GetUnpCodesInPfam ();

            ProtCidSettings.logWriter.WriteLine("Build UnpPdbDomainInterfaces. Retrieve Unp-Unp domain interfaces");
            ProtCidSettings.logWriter.WriteLine("#Uniprots have Pfam domains in PDB: " + unpCodesInPfam.Count.ToString ());
            int totalPairwise = (unpCodesInPfam.Count * (unpCodesInPfam.Count + 1)) / 2;
            ProtCidSettings.logWriter.WriteLine("#pairwise uniprots: " + totalPairwise);
            int count = 0;

            for (int i = 0; i < unpCodesInPfam.Count; i++)
            {
                for (int j = i; j < unpCodesInPfam.Count; j++)
                {
                    count++;
                   
                    try
                    {
                        RetrievePfamInteractionsBtwUnps(unpCodesInPfam[i], unpCodesInPfam[j], unpPdbfamTable);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.logWriter.WriteLine(count.ToString() + ": " + unpCodesInPfam[i] + " " + unpCodesInPfam[j] + " " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                    if (unpInterNetTable.Rows.Count > 0)
                    {
                        InsertUnpDomainInterfaceTableToDb(unpInterNetTable);
                    }
                }
            }
            ProtCidSettings.logWriter.WriteLine("Build Unp Domain interfaces done!");
            ProtCidSettings.logWriter.Flush();
            ProtCidSettings.protcidQuery.Dispose();
            ProtCidSettings.pdbfamQuery.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpId1"></param>
        /// <param name="unpId2"></param>
        /// <returns></returns>
        private bool AreUnpPairExist(string unpId1, string unpId2)
        {
            string queryString = string.Format("Select * From UnpPdbDomainInterfaces Where UnpID1 = '{0}' AND UnpID2 = '{1}'", unpId1, unpId2);
            DataTable unpDomainInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (unpDomainInterfaceTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        #region update 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateUnpInteractionNetTable(string[] updateEntries)
        {
            // update UnpPdbfam table
            // update unppdbdomaininterfaces table
            bool isUpdate = true;
            CreatUnpInterNetTables(isUpdate);

            dbUnpPdbfamTable = SetUnpPdbfamTableInDb();

            ProtCidSettings.logWriter.WriteLine(DateTime.Today.ToShortDateString());
            ProtCidSettings.logWriter.WriteLine("Uniprots interactions in PDB");

            ProtCidSettings.logWriter.WriteLine("Retrieve PDBfam from db");
            DataTable pdbfamTable = SetPdbPfamAssignTable(updateEntries);  // get pfam assignments of pdb

            ProtCidSettings.logWriter.WriteLine("Unp-Pdb entity table from DB");
 //           DataTable unpEntityTable = GetUnpEntityTableSifts(updateEntries);  // unp and pdb entity match based on sifts
            DataTable unpEntityTable = GetUnpEntityTable(updateEntries);  // unp and pdb entity match based on sifts and xml, modified on Dec. 3, 2018 

            ProtCidSettings.logWriter.WriteLine("Add Unp info columns");
            DataTable unpPdbfamTable = SetUnpPdbfamAssignTable(pdbfamTable);  // pdbfam with unp columns: unpId, unpstart, unpend

            ProtCidSettings.logWriter.WriteLine("Unp-Pdb sequence match data");
            DataTable unpPdbEntitySeqMatchTableSifts = SetUnpPdbSeqMatchTable(updateEntries); // sequence mapping between pdb and unp 
            DataTable unpPdbEntitySeqMathTableXml = SetUnpPdbSeqMatchTableFromXml(unpPdbEntitySeqMatchTableSifts, updateEntries);

            ProtCidSettings.logWriter.WriteLine("Delete Unp-Pdbfam data for updated entries");
            DeleteUnpPdbfamData(updateEntries);

            // add unp info columns to pdbfam           
            ProtCidSettings.logWriter.WriteLine("Build UnpPdbfam table, add uniprot ID, start and end position to Pdbfam domains ");
            AddUnpToPdbfamData(pdbfamTable, unpEntityTable, unpPdbfamTable, unpPdbEntitySeqMatchTableSifts, unpPdbEntitySeqMathTableXml);

            ProtCidSettings.logWriter.WriteLine("Get the list of Uniprots with PDBfam");
            unpPdbfamTable = ReadUnpPdbfamFromDb(updateEntries);
            List<string> unpCodesInPfam = GetUnpCodesInPfam(unpPdbfamTable);

            ProtCidSettings.logWriter.WriteLine("Delete uniprot interaction data from updated entries");
            DeleteUnpInteractionData (updateEntries);

            ProtCidSettings.logWriter.WriteLine("Build UnpPdbDomainInterfaces. Retrieve Unp-Unp domain interfaces");
            ProtCidSettings.logWriter.WriteLine("#Uniprots have Pfam domains in PDB: " + unpCodesInPfam.Count.ToString());
            int totalPairwise = (unpCodesInPfam.Count * (unpCodesInPfam.Count + 1)) / 2;
            ProtCidSettings.logWriter.WriteLine("#pairwise uniprots: " + totalPairwise);
            int count = 0;

            for (int i = 0; i < unpCodesInPfam.Count; i++)
            {
                for (int j = i; j < unpCodesInPfam.Count; j++)
                {
                    count++;

                    try
                    {
                        RetrievePfamInteractionsBtwUnps(unpCodesInPfam[i], unpCodesInPfam[j], unpPdbfamTable);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.logWriter.WriteLine(count.ToString() + ": " + unpCodesInPfam[i] + " " + unpCodesInPfam[j] + " " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                    if (unpInterNetTable.Rows.Count > 0)
                    {
                        InsertUnpDomainInterfaceTableToDb(unpInterNetTable);
                    }
                }
            }
            ProtCidSettings.logWriter.WriteLine("Build Unp Domain interfaces done!");
            ProtCidSettings.logWriter.Flush();
            ProtCidSettings.protcidQuery.Dispose();
            ProtCidSettings.pdbfamQuery.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        private void DeleteUnpPdbfamData (string[] updateEntries)
        {
            string deleteString = "";
            for (int i = 0; i < updateEntries.Length; i += 300)
            {
                string[] subArray = ParseHelper.GetSubArray(updateEntries, i, 300);
                deleteString = string.Format("Delete From {0} Where PdbID IN ({1});", unpPdbfamTableName, ParseHelper.FormatSqlListString(subArray));
                dbUpdate.Delete(ProtCidSettings.pdbfamDbConnection, deleteString);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        private void DeleteUnpInteractionData (string[] updateEntries)
        {
            string deleteString = "";
            for (int i = 0; i < updateEntries.Length; i +=300)
            {
                string[] subArray = ParseHelper.GetSubArray (updateEntries, i, 300);
                deleteString = string.Format("Delete From {0} Where PdbID IN ({1});", unpInterNetTableName, ParseHelper.FormatSqlListString (subArray));
                dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
            }
        }
        #endregion

        #region unps and entries
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public DataTable GetUnpEntityTable ()
        {
            DataTable unpEntityTable = GetUnpEntityTableSifts ();
            string[] noSiftsUnpEntries = GetEntriesNoDbRefSifts();
            DataTable xmlUnpEntityTable = GetUnpEntityTableXml(noSiftsUnpEntries);
            ParseHelper.AddNewTableToExistTable(xmlUnpEntityTable, ref unpEntityTable);
            return unpEntityTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable GetUnpEntityTableSifts ()
        {
            string queryString = "Select DbCode As UnpID, PdbID, EntityID From PdbDbRefSifts Where DbName = 'UNP';";
            DataTable unpEntityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            return unpEntityTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        public DataTable GetUnpEntityTable (string[] updateEntries)
        {
            DataTable unpEntityTable = GetUnpEntityTableSifts (updateEntries);
            string[] noSiftsEntries = GetEntriesNoDbRefSifts(unpEntityTable, updateEntries);
            DataTable xmlUnpEntityTable = GetUnpEntityTableXml(noSiftsEntries);
            ParseHelper.AddNewTableToExistTable(xmlUnpEntityTable, ref unpEntityTable);
            return unpEntityTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private DataTable GetUnpEntityTableSifts (string[] updateEntries)
        {
            string queryString = "";
            DataTable unpEntityTable = null;
            for (int i = 0; i < updateEntries.Length; i += 300)
            {
                string[] subArray = ParseHelper.GetSubArray (updateEntries, i, 300);
                queryString = string.Format("Select DbCode As UnpID, PdbID, EntityID From PdbDbRefSifts " + 
                    " Where DbName = 'UNP' AND PdbID In ({0});", ParseHelper.FormatSqlListString (subArray));
                DataTable subUnpEntityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(subUnpEntityTable, ref unpEntityTable);
            }
            return unpEntityTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private DataTable GetUnpEntityTableXml(string[] entries)
        {
            string queryString = "";
            DataTable unpEntityTable = null;
            for (int i = 0; i < entries.Length; i += 300)
            {
                string[] subArray = ParseHelper.GetSubArray(entries, i, 300);
                queryString = string.Format("Select DbCode As UnpID, PdbID, EntityID From PdbDbRefXml " +
                    " Where DbName = 'UNP' AND PdbID In ({0});", ParseHelper.FormatSqlListString(subArray));
                DataTable subUnpEntityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(subUnpEntityTable, ref unpEntityTable);
            }
            return unpEntityTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetEntriesNoDbRefSifts ()
        {
            string queryString = "Select Distinct PdbID From PdbDbRefSifts Where DbName = 'UNP';";
            DataTable siftsUnpEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> siftsUnpEntryList = new List<string>();
            foreach (DataRow entryRow in siftsUnpEntryTable.Rows)
            {
                siftsUnpEntryList.Add(entryRow["PdbID"].ToString());
            }
            siftsUnpEntryList.Sort ();
            queryString = "Select Distinct PdbID From PdbDbRefXml Where DbName = 'UNP';";
            DataTable xmlUnpEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> noSiftsUnpEntryList = new List<string>();
            string pdbId = "";
            foreach (DataRow entryRow in xmlUnpEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (siftsUnpEntryList.BinarySearch (pdbId) < 0)
                {
                    noSiftsUnpEntryList.Add(pdbId);
                }
            }
            return noSiftsUnpEntryList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="siftsUnpEntryTable"></param>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private string[] GetEntriesNoDbRefSifts(DataTable siftsUnpEntryTable, string[] updateEntries)
        {
            string pdbId = "";
            List<string> siftsEntryList = new List<string> ();
            foreach (DataRow siftsRow in siftsUnpEntryTable.Rows)
            {
                pdbId = siftsRow["PdbID"].ToString();
                if (! siftsEntryList.Contains (pdbId))
                {
                    siftsEntryList.Add(pdbId);
                }
            }
            siftsEntryList.Sort();
            List<string> noSiftsEntryList = new List<string>();
            foreach (string lsPdbId in updateEntries)
            {
                if (siftsEntryList.BinarySearch (lsPdbId) < 0)
                {
                    noSiftsEntryList.Add(lsPdbId);
                }
            }
            return noSiftsEntryList.ToArray();
        }

        /// <summary>
        /// including Human and other uniprot codes in the PDB
        /// </summary>
        /// <returns></returns>
        private List<string> GetUnpCodesInPfam ()
        {
            string queryString = "Select Distinct UnpID From " + unpPdbfamTableName;
            DataTable unpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> unpCodeList = new List<string>();
            string unpCode = "";
            foreach (DataRow unpRow in unpTable.Rows)
            {
                unpCode = unpRow["UnpID"].ToString ().TrimEnd ();
                if (! unpCodeList.Contains (unpCode))
                {
                    unpCodeList.Add(unpCode);
                }
            }
            unpCodeList.Sort();
            return unpCodeList;
        }

        /// <summary>
        /// including Human and other uniprot codes in the PDB
        /// </summary>
        /// <returns></returns>
        private List<string> GetUnpCodesInPfam(DataTable unpPdbfamTable)
        {            
            List<string> unpCodeList = new List<string>();
            string unpCode = "";
            foreach (DataRow unpRow in unpPdbfamTable.Rows)
            {
                unpCode = unpRow["UnpID"].ToString().TrimEnd();
                if (!unpCodeList.Contains(unpCode))
                {
                    unpCodeList.Add(unpCode);
                }
            }
            unpCodeList.Sort();
            return unpCodeList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpId1"></param>
        /// <param name="unpId2"></param>
        /// <param name="unpEntityTable"></param>
        /// <returns></returns>
        private List<string> GetEntriesWithInputUniprots (string unpId1, string unpId2, DataTable unpPdbfamTable)
        {
            DataRow[] unpEntityRows1 = unpPdbfamTable.Select(string.Format("UnpID = '{0}'", unpId1));
            List<string> unpEntryList1 = GetUnpEntries(unpEntityRows1);
            DataRow[] unpEntityRows2 = null;
            if (unpId1 == unpId2)
            {
                return unpEntryList1;
            }
            else
            {
                unpEntityRows2 = unpPdbfamTable.Select(string.Format("UnpID = '{0}'", unpId2));
                List<string> unpEntryList2 = GetUnpEntries(unpEntityRows2);
                List<string> entryInBothList = new List<string>();
                foreach (string pdbId in unpEntryList1)
                {
                    if (unpEntryList2.Contains (pdbId))
                    {
                        entryInBothList.Add(pdbId);
                    }
                }
                return entryInBothList;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpEntityRows"></param>
        /// <returns></returns>
        private List<string> GetUnpEntries(DataRow[] unpEntityRows)
        {
            List<string> unpEntryList = new List<string>();
            string pdbId = "";
            foreach (DataRow unpEntityRow in unpEntityRows)
            {
                pdbId = unpEntityRow["PdbID"].ToString().TrimEnd();
                if (! unpEntryList.Contains (pdbId))
                {
                    unpEntryList.Add(pdbId);
                }
            }
            return unpEntryList;
        }

        /// <summary>
        /// the list of uniprot ids associated with the list of updated pdb entries
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private string[] GetUnpsForUpdateEntries(string[] updateEntries)
        {
            List<string> unpList = new List<string>();
            string queryString = "";
            string unpCode = "";
            for (int i = 0; i < updateEntries.Length; i+=300 )
            {
                string[] subArray = ParseHelper.GetSubArray(updateEntries, i, 300);
                queryString = string.Format("Select Distinct DbCode From PdbDbRefSifts Where PdbID IN ({0}) Where DbName = 'UNP';", ParseHelper.FormatSqlListString (subArray));
                DataTable unpCodeTable = ProtCidSettings.protcidQuery.Query (queryString);
                foreach (DataRow unpCodeRow in unpCodeTable.Rows )
                {
                    unpCode = unpCodeRow["DbCode"].ToString().TrimEnd();
                    if (! unpList.Contains (unpCode))
                    {
                        unpList.Add(unpCode);
                    }
                }
            }
           return unpList.ToArray();
        }
        #endregion

        #region add unp to pdbfam
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbfamTable"></param>
        /// <param name="unpEntityTable"></param>
        /// <param name="unpPdbfamTable"></param>
        /// <param name="unpPdbEntitySeqMatchTable">with one-to-ont residue match from SIFTs</param>
        /// <param name="unpPdbEntitySeqMathTableXml">no one-to-one residue match</param>
        public void AddUnpToPdbfamData(DataTable pdbfamTable, DataTable unpEntityTable, DataTable unpPdbfamTable,
            DataTable unpPdbEntitySeqMatchTable, DataTable unpPdbEntitySeqMathTableXml)
        {
            string pdbId = "";
            string unpId = "";
            int entityId = 0;

            foreach (DataRow entityRow in unpEntityTable.Rows)
            {
                pdbId = entityRow["PdbID"].ToString();
                unpId = entityRow["UnpID"].ToString().TrimEnd();
                entityId = Convert.ToInt32(entityRow["EntityID"].ToString ());
              
                try
                {
                    AddUnpInfoToEntityDomains(unpId, pdbId, entityId, pdbfamTable, unpPdbfamTable, unpPdbEntitySeqMatchTable, unpPdbEntitySeqMathTableXml);
                    InsertUnpPdbfamToDb(unpPdbfamTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine(unpId + " " + pdbId + entityId.ToString () + " add unp to pdbfam error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }               
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpId"></param>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private void AddUnpInfoToEntityDomains(string unpId, string pdbId, int entityId, DataTable pdbfamTable, DataTable unpPdbfamTable,
             DataTable unpPdbEntitySeqMatchTable, DataTable unpPdbEntitySeqMathTableXml)
        {
            DataRow[] domainRows = pdbfamTable.Select(string.Format("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId));

            DataTable unpEntityMatchTable = GetUnpPdbEntitySeqMatchTable (unpId, pdbId, entityId, unpPdbEntitySeqMatchTable);

            int[] domainRegions = new int[2];
            int[] unpSeqRegions = null;
            string outIndexErrorMsg = "";
            foreach (DataRow domainRow in domainRows)
            {
                domainRegions[0] = Convert.ToInt32(domainRow["SeqStart"].ToString());
                domainRegions[1] = Convert.ToInt32(domainRow["SeqEnd"].ToString());
                entityId = Convert.ToInt32(domainRow["EntityID"].ToString());
                unpSeqRegions = GetUnpPfamRegions(unpId, pdbId, entityId, domainRegions, unpPdbEntitySeqMatchTable, out outIndexErrorMsg);
                if (unpSeqRegions[0] == -1 || unpSeqRegions[1] == -1)
                {
                    // added on Dec. 3, 2018
                    unpSeqRegions = GetUnpPfamRegionsXml(unpId, pdbId, entityId, domainRegions, unpPdbEntitySeqMathTableXml, out outIndexErrorMsg);
                }
                if (unpSeqRegions[0] != -1 && unpSeqRegions[1] != -1)
                {
                    domainRegions[1] = Convert.ToInt32(domainRow["SeqEnd"].ToString());
                    DataRow unpDomainRow = unpPdbfamTable.NewRow();
                    foreach (DataColumn dCol in pdbfamTable.Columns)
                    {
                        unpDomainRow[dCol.ColumnName] = domainRow[dCol.ColumnName];
                    }
                    unpDomainRow["UnpID"] = unpId;
                    unpDomainRow["UnpStart"] = unpSeqRegions[0];
                    unpDomainRow["UnpEnd"] = unpSeqRegions[1];
                    unpPdbfamTable.Rows.Add(unpDomainRow);
                }
                else
                {                   
                    if (outIndexErrorMsg != "")
                    {
                        ProtCidSettings.logWriter.WriteLine(unpId + " " + ParseHelper.FormatDataRow (domainRow));
                        ProtCidSettings.logWriter.WriteLine(outIndexErrorMsg);
                    }
                    else
                    {
                        ProtCidSettings.logWriter.WriteLine(unpId + " " + ParseHelper.FormatDataRow (domainRow));
                        ProtCidSettings.logWriter.WriteLine("No unp-pdb seq match: " + unpSeqRegions[0].ToString () + " " + unpSeqRegions[1].ToString ());
                    }
                    ProtCidSettings.logWriter.Flush();
                }
            }
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpId"></param>
        /// <param name="pdbId"></param>
        /// <param name="unpEntityTable"></param>
        /// <returns></returns>
        private List<int> GetEntryUnpEntityList(string unpId, string pdbId, DataTable unpEntityTable)
        {
            DataRow[] entityRows = unpEntityTable.Select(string.Format("PdbID = '{0}' AND UnpID = '{1}'", pdbId, unpId));
            List<int> unpEntryEntityList = new List<int>();
            int entityId = 0;
            foreach (DataRow entityRow in entityRows)
            {
                entityId = Convert.ToInt32(entityRow["EntityID"].ToString());
                if (!unpEntryEntityList.Contains(entityId))
                {
                    unpEntryEntityList.Add(entityId);
                }
            }
            return unpEntryEntityList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpId"></param>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="unpEntityMatchTable"></param>
        /// <param name="pfamRegions"></param>
        /// <returns></returns>
        private int[] GetUnpPfamRegions(string unpId, string pdbId, int entityId, int[] pfamRegion, 
            DataTable unpPdbEntitySeqMatchTable, out string pdbIndexErrorMsg)
        {
            int[] unpPfamRegion = new int[2];
            unpPfamRegion[0] = -1;
            unpPfamRegion[1] = -1;
            DataRow[] entityMatchRows = unpPdbEntitySeqMatchTable.Select(string.Format("UnpID = '{0}' AND PdbID = '{1}' AND EntityID = '{2}'", 
                unpId, pdbId, entityId));
            pdbIndexErrorMsg = "";
            if (AreUnpRangesOutOfPfamRanges(entityMatchRows, pfamRegion))
            {
                pdbIndexErrorMsg = unpId + " is out of the Pfam region " + pdbId + entityId + 
                    ", PDB pfam region: [" + pfamRegion[0].ToString() + "-" + pfamRegion[1].ToString() + "]";
                return unpPfamRegion;
            }
            string pdbSeqNumberStr = "";
            string dbSeqNumberStr = "";
            pdbIndexErrorMsg = "";
            foreach (DataRow matchRow in entityMatchRows)
            {
                pdbSeqNumberStr += (matchRow["SeqNumbers"].ToString() + ",");
                dbSeqNumberStr += (matchRow["DbSeqNumbers"].ToString() + ",");
            }
            pdbSeqNumberStr = pdbSeqNumberStr.TrimEnd(',');
            dbSeqNumberStr = dbSeqNumberStr.TrimEnd(',');
            string[] pdbSeqNumbers = pdbSeqNumberStr.Split(',');
            string[] dbSeqNumbers = dbSeqNumberStr.Split(',');

            int startPosIndex = Array.IndexOf(pdbSeqNumbers, pfamRegion[0].ToString());
            int endPosIndex = Array.IndexOf(pdbSeqNumbers, pfamRegion[1].ToString());
            if (startPosIndex > -1 && endPosIndex > -1)
            {
                pdbIndexErrorMsg = "PDB pfam region: [" + pfamRegion[0].ToString() + "-" + pfamRegion[1].ToString() +
                    "] indexes:" + startPosIndex.ToString() + ", " + endPosIndex.ToString();
            }
            if (startPosIndex < 0)
            {
                startPosIndex = 0;
                pdbIndexErrorMsg += "\r\n change start index to be 0"; 
            }          
            if (endPosIndex > pdbSeqNumbers.Length - 1 || endPosIndex < 0)
            {
                endPosIndex = pdbSeqNumbers.Length - 1;

                pdbIndexErrorMsg += "\r\n change end index to be the length of sequence"; 
            }          
            
  //         if (startPosIndex > -1 && endPosIndex > -1)
            if (endPosIndex > startPosIndex)
            {               
                unpPfamRegion[0] = GetClosestUnpPosition(startPosIndex, dbSeqNumbers);
                unpPfamRegion[1] = GetClosestUnpPosition(endPosIndex, dbSeqNumbers);
            }
           
            return unpPfamRegion;
        }

        /// <summary>
        /// the range is derived from PdbDbRefSeqXml
        /// Can be applied to SIFTs data too, maybe no need PdbDbRefSeqAlignSifts
        /// </summary>
        /// <param name="unpId"></param>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="unpEntityMatchTable"></param>
        /// <param name="pfamRegions"></param>
        /// <returns></returns>
        private int[] GetUnpPfamRegionsXml(string unpId, string pdbId, int entityId, int[] pfamRegion,
            DataTable unpPdbEntitySeqMatchTableXml, out string pdbIndexErrorMsg)
        {
            int[] unpPfamRegion = new int[2];
            unpPfamRegion[0] = -1;
            unpPfamRegion[1] = -1;
            DataRow[] entityMatchRows = unpPdbEntitySeqMatchTableXml.Select(string.Format("UnpID = '{0}' AND PdbID = '{1}' AND EntityID = '{2}'",
                unpId, pdbId, entityId));
            pdbIndexErrorMsg = "";
            int dbBeg = 0;
            int dbEnd = 0;
            int seqBeg = 0;
            int seqEnd = 0;
            foreach (DataRow matchRow in entityMatchRows)
            {               
                seqBeg = Convert.ToInt32(matchRow["SeqAlignBeg"].ToString ());
                seqEnd = Convert.ToInt32(matchRow["SeqAlignEnd"].ToString ());
                if (pfamRegion[0] > seqEnd || pfamRegion[1] < seqBeg)
                {
                    continue;
                }
                dbBeg = Convert.ToInt32(matchRow["DbAlignBeg"].ToString());
                dbEnd = Convert.ToInt32(matchRow["DbAlignEnd"].ToString());
                if (pfamRegion[0] >= seqBeg && pfamRegion[0] <= seqEnd)
                {
                    unpPfamRegion[0] = dbBeg + (pfamRegion[0] - seqBeg);
                    if (pfamRegion[1] > seqEnd)
                    {
                        if (unpPfamRegion[1] < dbEnd || unpPfamRegion[1] == -1)
                        {
                            unpPfamRegion[1] = dbEnd;
                        }
                    }
                }
                if (pfamRegion[1] >= seqBeg && pfamRegion[1] <= seqEnd)
                {
                    unpPfamRegion[1] = dbEnd - (seqEnd - pfamRegion[1]);
                    if (pfamRegion[0] < seqBeg)
                    {
                        if (unpPfamRegion[0] > dbBeg || unpPfamRegion[0] == -1)
                        {
                            unpPfamRegion[0] = dbBeg;
                        }
                    }
                } 
                if (pfamRegion[0] < seqBeg && pfamRegion[1] > seqEnd)
                {
                    if (unpPfamRegion[0] > dbBeg || unpPfamRegion[0] == -1)
                    {
                        unpPfamRegion[0] = dbBeg;
                    }
                    if (unpPfamRegion[1] < dbEnd || unpPfamRegion[1] == -1)
                    {
                        unpPfamRegion[1] = dbEnd;
                    }
                }                
            }
            return unpPfamRegion;
        }
  
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityMatchRows"></param>
        /// <param name="pfamInPdbRange"></param>
        /// <returns></returns>
        private bool AreUnpRangesOutOfPfamRanges (DataRow[] entityMatchRows, int[] pfamInPdbRange)
        {
            int[] unpInPdbRange = new int[2];
            foreach (DataRow matchRow in entityMatchRows)
            {
                unpInPdbRange[0] = Convert.ToInt32(matchRow["SeqAlignBeg"].ToString ());
                unpInPdbRange[1] = Convert.ToInt32(matchRow["SeqAlignEnd"].ToString ());
                if (IsRangeInPfamRange (unpInPdbRange, pfamInPdbRange))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpInPdbRange"></param>
        /// <param name="pfamInPdbRange"></param>
        /// <returns></returns>
        private bool IsRangeInPfamRange (int[] unpInPdbRange, int[] pfamInPdbRange)
        {
            if (unpInPdbRange[0] > pfamInPdbRange[1] || unpInPdbRange[1] < pfamInPdbRange[0])
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="posIndex"></param>
        /// <param name="dbSeqNumbers"></param>
        /// <returns></returns>
        private int GetClosestUnpPosition(int posIndex, string[] dbSeqNumbers)
        {
            int dbSeqNum = -1;
            while (posIndex < dbSeqNumbers.Length)
            {
                if (Int32.TryParse(dbSeqNumbers[posIndex], out dbSeqNum))
                {
                    break;
                }
                posIndex++;
            }
            return dbSeqNum;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpId"></param>
        /// <param name="pdbId"></param>
        /// <param name="entities"></param>
        /// <param name="unpPdbEntitySeqMatchTable"></param>
        /// <returns></returns>
        private DataTable GetUnpPdbEntitySeqMatchTable (string unpId, string pdbId, int[] entities, DataTable unpPdbEntitySeqMatchTable)
        {
            DataTable unpEntityMatchTable = unpPdbEntitySeqMatchTable.Clone();
            if (unpPdbEntitySeqMatchTable != null)
            {
                DataRow[] unpEntityMatchRows = unpPdbEntitySeqMatchTable.Select(string.Format ("UnpID = '{0}' AND PdbID = '{1}' AND EntityID IN ({2})", 
                    unpId, pdbId, ParseHelper.FormatSqlListString (entities)));
                foreach (DataRow seqMatchRow in unpEntityMatchRows)
                {
                    DataRow newRow = unpEntityMatchTable.NewRow();
                    newRow.ItemArray = seqMatchRow.ItemArray;
                    unpEntityMatchTable.Rows.Add(newRow);
                }
            }
            else
            {
                string queryString = string.Format("Select EntityID, DbCode As UnpID, PdbDbRefSeqSifts.DbAlignBeg, PdbDbRefSeqSifts.DbAlignEnd, " +
                    " PdbDbRefSeqSifts.SeqAlignBeg, PdbDbRefSeqSifts.SeqAlignEnd, PdbDbRefSeqAlignSifts.* From PdbDbRefSifts, PdbDbRefSeqSifts, PdbDbRefSeqAlignSifts " +
                    " Where DbCode = '{0}' AND PdbDbRefSifts.PdbID = '{1}' AND EntityID IN ({2}) AND DbName = 'UNP' AND " +
                    " PdbDbRefSifts.PdbID = PdbDbRefSeqSifts.PdbID  AND PdbDbRefSifts.PdbID = PdbDbRefSeqAlignSifts.PdbID AND " +
                    " PdbDbRefSifts.RefID = PdbDbRefSeqSifts.RefID AND PdbDbRefSifts.RefID = PdbDbRefSeqAlignSifts.RefID AND " +
                    " PdbDbRefSeqSifts.AlignID = PdbDbRefSeqAlignSifts.AlignID AND PdbDbRefSeqSifts.AsymID = PdbDbRefSeqAlignSifts.AsymID;",
                    unpId, pdbId, ParseHelper.FormatSqlListString(entities));
                unpEntityMatchTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                RemoveOtherChainsSeqMatch(unpEntityMatchTable);
                
            }
            return unpEntityMatchTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpId"></param>
        /// <param name="pdbId"></param>
        /// <param name="entities"></param>
        /// <param name="unpPdbEntitySeqMatchTable"></param>
        /// <returns></returns>
        private DataTable GetUnpPdbEntitySeqMatchTable(string unpId, string pdbId, int entityId, DataTable unpPdbEntitySeqMatchTable)
        {
            DataTable unpEntityMatchTable = unpPdbEntitySeqMatchTable.Clone();
            if (unpPdbEntitySeqMatchTable != null)
            {
                DataRow[] unpEntityMatchRows = unpPdbEntitySeqMatchTable.Select(string.Format("UnpID = '{0}' AND PdbID = '{1}' AND EntityID = '{2}'", unpId, pdbId, entityId));
                foreach (DataRow seqMatchRow in unpEntityMatchRows)
                {
                    DataRow newRow = unpEntityMatchTable.NewRow();
                    newRow.ItemArray = seqMatchRow.ItemArray;
                    unpEntityMatchTable.Rows.Add(newRow);
                }
            }
            else
            {
                string queryString = string.Format("Select EntityID, DbCode As UnpID, PdbDbRefSeqSifts.DbAlignBeg, PdbDbRefSeqSifts.DbAlignEnd, " +
                    " PdbDbRefSeqSifts.SeqAlignBeg, PdbDbRefSeqSifts.SeqAlignEnd, PdbDbRefSeqAlignSifts.* From PdbDbRefSifts, PdbDbRefSeqSifts, PdbDbRefSeqAlignSifts " +
                    " Where DbCode = '{0}' AND PdbDbRefSifts.PdbID = '{1}' AND EntityID = {2} AND DbName = 'UNP' AND " +
                    " PdbDbRefSifts.PdbID = PdbDbRefSeqSifts.PdbID  AND PdbDbRefSifts.PdbID = PdbDbRefSeqAlignSifts.PdbID AND " +
                    " PdbDbRefSifts.RefID = PdbDbRefSeqSifts.RefID AND PdbDbRefSifts.RefID = PdbDbRefSeqAlignSifts.RefID AND " +
                    " PdbDbRefSeqSifts.AlignID = PdbDbRefSeqAlignSifts.AlignID AND PdbDbRefSeqSifts.AsymID = PdbDbRefSeqAlignSifts.AsymID;",                    
                    unpId, pdbId, entityId);
                unpEntityMatchTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                RemoveOtherChainsSeqMatch(unpEntityMatchTable);
            }
            return unpEntityMatchTable;
        }       
        #endregion

        #region unp-unp and pfam interactions
        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpId1"></param>
        /// <param name="unpId2"></param>
        /// <param name="unpPdbfamTable"></param>
        public void RetrievePfamInteractionsBtwUnps(string unpId1, string unpId2, DataTable unpPdbfamTable)
        {
            DataRow[] domainRows1 = unpPdbfamTable.Select(string.Format ("UnpID = '{0}'", unpId1));
            Dictionary<string, List<long>> entryDomainDict1 = GetUnpPfamEntryList(domainRows1);
            if (unpId1 != unpId2)
            {
                DataRow[] domainRows2 = unpPdbfamTable.Select(string.Format("UnpID = '{0}'", unpId2));
                Dictionary<string, List<long>> entryDomainDict2 = GetUnpPfamEntryList(domainRows2);
                RetrieveEntryEntityPfamInteractions(unpId1, unpId2, entryDomainDict1, entryDomainDict2);
            }
            else
            {
                RetrieveEntryEntityPfamInteractions(unpId1, entryDomainDict1);
            }            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpId"></param>
        /// <param name="entryDomainDict"></param>
        /// <returns></returns>
        private void RetrieveEntryEntityPfamInteractions (string unpId, Dictionary<string, List<long>> entryDomainDict)
        {
            foreach (string pdbId in entryDomainDict.Keys)
            {
                List<long> unpDomainIds = entryDomainDict[pdbId];
                DataTable domainInterTable = RetrieveEntryEntityPfamInteractions(pdbId, unpDomainIds.ToArray());
                AddUnpInfoToDomainInterfaceTable(unpId, unpId, domainInterTable);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpId1"></param>
        /// <param name="unpId2"></param>
        /// <param name="entryDomainDict1"></param>
        /// <param name="entryDomainDict2"></param>
        private void RetrieveEntryEntityPfamInteractions (string unpId1, string unpId2, 
            Dictionary<string, List<long>> entryDomainDict1, Dictionary<string, List<long>> entryDomainDict2)
        {
            foreach (string pdbId1 in entryDomainDict1.Keys)
            {
                List<long> domainIdList1 = entryDomainDict1[pdbId1];
                foreach (string pdbId2 in entryDomainDict2.Keys)
                {
                    if (pdbId1 == pdbId2)
                    {
                        List<long> domainIdList2 = entryDomainDict2[pdbId2];
                        DataTable domainInterTable = RetrieveEntryEntityPfamInteractions(pdbId1, domainIdList1.ToArray(), domainIdList2.ToArray());
                        AddUnpInfoToDomainInterfaceTable(unpId1, unpId2, domainInterTable);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpId1"></param>
        /// <param name="unpId2"></param>
        /// <param name="domainInterfaceTable"></param>
        private void AddUnpInfoToDomainInterfaceTable (string unpId1, string unpId2, DataTable domainInterfaceTable)
        {
            foreach(DataRow dInterfaceRow in domainInterfaceTable.Rows)
            {
                DataRow unpDInterfaceRow = unpInterNetTable.NewRow();              
                unpDInterfaceRow["UnpID1"] = unpId1;
                unpDInterfaceRow["UnpID2"] = unpId2;
                unpDInterfaceRow["PdbID"] = dInterfaceRow["PdbID"];
                unpDInterfaceRow["RelSeqID"] = dInterfaceRow["RelSeqID"];
                unpDInterfaceRow["DomainInterfaceID"] = dInterfaceRow["DomainInterfaceID"];
                unpDInterfaceRow["IsReversed"] = dInterfaceRow["IsReversed"];
                unpInterNetTable.Rows.Add(unpDInterfaceRow);
            }
        }
     
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainRows"></param>
        /// <returns></returns>
        private Dictionary<string, List<long>> GetUnpPfamEntryList(DataRow[] domainRows)
        {
            Dictionary<string, List<long>> entryDomainsDict = new Dictionary<string,List<long>> ();
            string pdbId = "";
            long domainId = 0;
            foreach (DataRow domainRow in domainRows)
            {
                pdbId = domainRow["PdbID"].ToString();
                domainId = Convert.ToInt64(domainRow["DomainID"].ToString ());
                if (entryDomainsDict.ContainsKey (pdbId))
                {
                    List<long> domainList = (List<long>)entryDomainsDict[pdbId];
                    if (!  domainList.Contains (domainId))
                    {
                        domainList.Add(domainId);
                    }
                }
                else
                {
                    List<long> domainList = new List<long>();
                    domainList.Add(domainId);
                    entryDomainsDict.Add(pdbId, domainList);
                }
            }
            return entryDomainsDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainIds1"></param>
        /// <param name="domainIds2"></param>
        /// <returns></returns>
        public DataTable RetrieveEntryEntityPfamInteractions(string pdbId, long[] domainIds1, long[] domainIds2)
        {
            string queryString = string.Format("Select RelSeqID, PdbID, DomainInterfaceID " +
                " From PfamDomainInterfaces Where PdbID = '{0}' AND DomainID1 IN ({1}) AND DomainID2 IN ({2});",
                pdbId, ParseHelper.FormatSqlListString(domainIds1), ParseHelper.FormatSqlListString(domainIds2));
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);

            DataColumn isReversedCol = new DataColumn("IsReversed");
            isReversedCol.DefaultValue = "0";
            domainInterfaceTable.Columns.Add(isReversedCol);

            List<int> domainInterfaceIdList = new List<int>();
            int dInterfaceId = 0;
            foreach (DataRow dInterfaceRow in domainInterfaceTable.Rows)
            {
                dInterfaceId = Convert.ToInt32(dInterfaceRow["DomainInterfaceID"].ToString());
                domainInterfaceIdList.Add(dInterfaceId);
            }
            if (domainInterfaceIdList.Count > 0)
            {
                queryString = string.Format("Select RelSeqID, PdbID, DomainInterfaceID " +
                    " From PfamDomainInterfaces Where PdbID = '{0}' AND DomainID1 IN ({1}) AND DomainID2 IN ({2})" +
                    " AND DomainInterfaceID NOT IN ({3});",
                    pdbId, ParseHelper.FormatSqlListString(domainIds2), ParseHelper.FormatSqlListString(domainIds1),
                    ParseHelper.FormatSqlListString(domainInterfaceIdList.ToArray()));
            }
            else
            {
                queryString = string.Format("Select RelSeqID, PdbID, DomainInterfaceID " +
                   " From PfamDomainInterfaces Where PdbID = '{0}' AND DomainID1 IN ({1}) AND DomainID2 IN ({2});",
                   pdbId, ParseHelper.FormatSqlListString(domainIds2), ParseHelper.FormatSqlListString(domainIds1));
            }
            DataTable domainInterfaceTableRev = ProtCidSettings.protcidQuery.Query(queryString);

            DataColumn isReversedColRev = new DataColumn("IsReversed");
            isReversedColRev.DefaultValue = "1";
            domainInterfaceTableRev.Columns.Add(isReversedColRev);

            foreach (DataRow revInterfaceRow in domainInterfaceTableRev.Rows)
            {
                DataRow dataRow = domainInterfaceTable.NewRow();
                dataRow.ItemArray = revInterfaceRow.ItemArray;
                domainInterfaceTable.Rows.Add(dataRow);
            }
            return domainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainIds1"></param>
        /// <param name="domainIds2"></param>
        /// <returns></returns>
        public DataTable RetrieveEntryEntityPfamInteractions(string pdbId, long[] domainIds)
        {
            string queryString = string.Format("Select RelSeqID, PdbID, DomainInterfaceID, DomainID1, DomainID2 " +
                " From PfamDomainInterfaces Where PdbID = '{0}' AND DomainID1 IN ({1}) AND DomainID2 IN ({1});",
                    pdbId, ParseHelper.FormatSqlListString(domainIds));
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);

            DataColumn isReversedCol = new DataColumn("IsReversed");
            isReversedCol.DefaultValue = "0";
            domainInterfaceTable.Columns.Add(isReversedCol);          
            return domainInterfaceTable;
        }
        #endregion

        #region insert data to db
        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpInterNetTable"></param>
        private void InsertUnpDomainInterfaceTableToDb (DataTable unpInterNetTable)
        {
            dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, unpInterNetTable);
            unpInterNetTable.Clear();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpPdbfamTable"></param>
        private void InsertUnpPdbfamToDb (DataTable unpPdbfamTable)
        {            
            foreach (DataRow pfamRow in unpPdbfamTable.Rows)
            {
                DataRow dbUnpfamRow = dbUnpPdbfamTable.NewRow();
                foreach (DataColumn dCol in dbUnpPdbfamTable.Columns)
                {
                    dbUnpfamRow[dCol.ColumnName] = pfamRow[dCol.ColumnName];
                }
                dbUnpPdbfamTable.Rows.Add(dbUnpfamRow);
            }
            dbInsert.BatchInsertDataIntoDBtables(ProtCidSettings.pdbfamDbConnection, dbUnpPdbfamTable);
            unpPdbfamTable.Clear();
            dbUnpPdbfamTable.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable SetUnpPdbfamTableInDb ()
        {
            DataTable unpPdbfamTableInDb = new DataTable(unpPdbfamTableName);
            string[] unpPdbfamColumns = {"UnpID", "PdbID", "EntityID", "DomainID", "UnpStart", "UnpEnd"};
            foreach (string col in unpPdbfamColumns)
            {
                unpPdbfamTableInDb.Columns.Add(new DataColumn (col));
            }
            return unpPdbfamTableInDb;
        }
        #endregion

        #region initialize
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable SetPdbPfamAssignTable ()
        {
            string queryString = "Select PdbID, EntityID, DomainID, Pfam_ID, AlignStart As SeqStart, AlignEnd As SeqEnd From PdbPfam;";
            DataTable pdbPfamAssignTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            pdbPfamAssignTable.TableName = "Pdbfam";
            return pdbPfamAssignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private DataTable SetPdbPfamAssignTable (string[] updateEntries)
        {
            string queryString = "";
            DataTable pdbPfamTable = null;
            for (int i = 0; i < updateEntries.Length; i+=300)
            {
                string[] subArray = ParseHelper.GetSubArray(updateEntries, i, 300);
                queryString = string.Format("Select PdbID, EntityID, DomainID, Pfam_ID, AlignStart As SeqStart, AlignEnd As SeqEnd " + 
                    " From PdbPfam Where PdbID IN ({0});", ParseHelper.FormatSqlListString (subArray));
                DataTable subPdbfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(subPdbfamTable, ref pdbPfamTable);
            }
            return pdbPfamTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable SetUnpPdbfamAssignTable (DataTable pdbfamTable)
        {
            DataTable unpPdbfamTable = new DataTable("UnpPdbfam");
            foreach (DataColumn dCol in pdbfamTable.Columns)
            {
                unpPdbfamTable.Columns.Add(new DataColumn(dCol.ColumnName));
            }
            unpPdbfamTable.Columns.Add(new DataColumn ("UnpID"));
            unpPdbfamTable.Columns.Add(new DataColumn ("UnpStart"));
            unpPdbfamTable.Columns.Add(new DataColumn ("UnpEnd"));
            return unpPdbfamTable;
        }     
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable SetUnpPdbSeqMatchTable()
        {
            string queryString = "Select EntityID, DbCode As UnpID, PdbDbRefSeqSifts.DbAlignBeg, PdbDbRefSeqSifts.DbAlignEnd, " +
                    " PdbDbRefSeqSifts.SeqAlignBeg, PdbDbRefSeqSifts.SeqAlignEnd, PdbDbRefSeqAlignSifts.* " +
                    " From PdbDbRefSifts, PdbDbRefSeqSifts, PdbDbRefSeqAlignSifts " +
                    " Where DbName = 'UNP' AND " +
                    " PdbDbRefSifts.PdbID = PdbDbRefSeqSifts.PdbID  AND PdbDbRefSifts.PdbID = PdbDbRefSeqAlignSifts.PdbID AND " +
                    " PdbDbRefSifts.RefID = PdbDbRefSeqSifts.RefID AND PdbDbRefSifts.RefID = PdbDbRefSeqAlignSifts.RefID AND " +
                    " PdbDbRefSeqSifts.AlignID = PdbDbRefSeqAlignSifts.AlignID AND PdbDbRefSeqSifts.AsymID = PdbDbRefSeqAlignSifts.AsymID " + 
                    " Order By PdbDbRefSifts.PdbID, EntityID, AsymID;";
            DataTable unpEntitySeqMatchTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            unpEntitySeqMatchTable.TableName = "UnpPdbEntitySeqMatch";
            queryString = "Select Distinct PdbID From PdbDbRefSeqAlignSifts;";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] entryList = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                entryList[count] = entryRow["PdbID"].ToString();
                count++;
            }
            RemoveEntryOtherChainsSeqMatch (entryList, unpEntitySeqMatchTable);
            return unpEntitySeqMatchTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable SetUnpPdbSeqMatchTable(string[] entries)
        {
            string queryString = "";
            DataTable unpEntitySeqMatchTable = null;
            for (int i = 0; i < entries.Length; i += 200)
            {
                string[] subEntries = ParseHelper.GetSubArray(entries, i, 200);
                queryString = string.Format("Select EntityID, DbCode As UnpID, PdbDbRefSeqSifts.DbAlignBeg, PdbDbRefSeqSifts.DbAlignEnd, " +
                    " PdbDbRefSeqSifts.SeqAlignBeg, PdbDbRefSeqSifts.SeqAlignEnd, PdbDbRefSeqAlignSifts.* " +
                    " From PdbDbRefSifts, PdbDbRefSeqSifts, PdbDbRefSeqAlignSifts " +
                    " Where DbName = 'UNP' AND PdbDbRefSifts.PdbID IN ({0}) AND " +
                    " PdbDbRefSifts.PdbID = PdbDbRefSeqSifts.PdbID  AND PdbDbRefSifts.PdbID = PdbDbRefSeqAlignSifts.PdbID AND " +
                    " PdbDbRefSifts.RefID = PdbDbRefSeqSifts.RefID AND PdbDbRefSifts.RefID = PdbDbRefSeqAlignSifts.RefID AND " +
                    " PdbDbRefSeqSifts.AlignID = PdbDbRefSeqAlignSifts.AlignID AND PdbDbRefSeqSifts.AsymID = PdbDbRefSeqAlignSifts.AsymID " +
                    " Order By PdbDbRefSifts.PdbID, EntityID, AsymID;", ParseHelper.FormatSqlListString(subEntries));
                DataTable subUnpEntitySeqMatchTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(subUnpEntitySeqMatchTable, ref unpEntitySeqMatchTable);
            }            
            unpEntitySeqMatchTable.TableName = "UnpPdbEntitySeqMatch";
            RemoveEntryOtherChainsSeqMatch(entries, unpEntitySeqMatchTable);
            return unpEntitySeqMatchTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpEntitySeqMatchTable"></param>
        /// <returns></returns>
        private DataTable SetUnpPdbSeqMatchTableFromXml ()
        {
            string[] noSiftsUnpEntries = GetEntriesNoDbRefSifts();
            DataTable seqMatchTableXml = SetUnpPdbSeqMatchTableFromXml(noSiftsUnpEntries);
            return seqMatchTableXml;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpEntitySeqMatchTable"></param>
        /// <param name="entries"></param>
        /// <returns></returns>
        private DataTable SetUnpPdbSeqMatchTableFromXml(string[] entries)
        {
            string queryString = "";
            DataTable xmlUnpEntitySeqMatchTable = null;
            for (int i = 0; i < entries.Length; i += 200)
            {
                string[] subEntries = ParseHelper.GetSubArray(entries, i, 200);
                queryString = string.Format("Select Distinct PdbDbRefXml.PdbID, EntityID, DbCode As UnpID, DbAlignBeg, DbAlignEnd, SeqAlignBeg, SeqAlignEnd " +
                    " From PdbDbRefXml, PdbDbRefSeqXml " +
                    " Where DbName = 'UNP' AND PdbDbRefXml.PdbID IN ({0}) AND " +
                    " PdbDbRefXml.PdbID = PdbDbRefSeqXml.PdbID  AND PdbDbRefXml.RefID = PdbDbRefSeqXml.RefID " +
                    " Order By PdbDbRefXml.PdbID, EntityID;", ParseHelper.FormatSqlListString(subEntries));
                DataTable subUnpEntitySeqMatchTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(subUnpEntitySeqMatchTable, ref xmlUnpEntitySeqMatchTable);
            }
            return xmlUnpEntitySeqMatchTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpEntitySeqMatchTable"></param>
        /// <param name="entries"></param>
        /// <returns></returns>
        private DataTable SetUnpPdbSeqMatchTableFromXml(DataTable unpEntitySeqMatchTable, string[] entries)
        {
            string[] noSiftsEntries = GetEntriesNoDbRefSifts(unpEntitySeqMatchTable, entries);
            DataTable xmlUnpEntitySeqMatchTable = SetUnpPdbSeqMatchTableFromXml(noSiftsEntries);
            return xmlUnpEntitySeqMatchTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpEntitySeqMatchTable"></param>
        private void RemoveEntryOtherChainsSeqMatch(string[] entryList, DataTable unpEntitySeqMatchTable)
        {           
            DataTable entrySubSetTable = unpEntitySeqMatchTable.Clone();
            DataTable oneChainUnpEntitySeqMatchTable = unpEntitySeqMatchTable.Clone();
            foreach (string lsPdbId in entryList)
            {
                DataRow[] unpEntitySeqMatchRows = unpEntitySeqMatchTable.Select(string.Format ("PdbID = '{0}'", lsPdbId));
                entrySubSetTable.Clear();
                foreach (DataRow matchRow in unpEntitySeqMatchRows)
                {
                    DataRow subMatchRow = entrySubSetTable.NewRow();
                    subMatchRow.ItemArray = matchRow.ItemArray;
                    entrySubSetTable.Rows.Add(subMatchRow);
                }
                RemoveOtherChainsSeqMatch(entrySubSetTable);
                ParseHelper.AddNewTableToExistTable(entrySubSetTable, ref oneChainUnpEntitySeqMatchTable);
                entrySubSetTable.Clear();
            }
            unpEntitySeqMatchTable = oneChainUnpEntitySeqMatchTable;
        }

        /// <summary>
        /// only keep match for one chain per entity
        /// </summary>
        /// <param name="unpEntitySeqMatchTable"></param>
        private void RemoveOtherChainsSeqMatch (DataTable unpEntitySeqMatchTable)
        {
            // use only one chain for each entity
            Dictionary<int, List<string>> entityChainDict = new Dictionary<int, List<string>>();
            int entityId = 0;
            string asymId = "";
            foreach (DataRow matchRow in unpEntitySeqMatchTable.Rows)
            {
                entityId = Convert.ToInt32(matchRow["EntityID"].ToString());
                asymId = matchRow["AsymID"].ToString().TrimEnd();
                if (entityChainDict.ContainsKey(entityId))
                {
                    List<string> chainList = (List<string>)entityChainDict[entityId];
                    if (!chainList.Contains(asymId))
                    {
                        chainList.Add(asymId);
                    }
                }
                else
                {
                    List<string> chainList = new List<string>();
                    chainList.Add(asymId);
                    entityChainDict.Add(entityId, chainList);
                }
            }
            DataTable unpEntityChainMatchTable = unpEntitySeqMatchTable.Clone();
            foreach (int keyEntityId in entityChainDict.Keys)
            {
                List<string> chainList = (List<string>)entityChainDict[keyEntityId];
                chainList.Sort();
                DataRow[] seqAlignRows = unpEntitySeqMatchTable.Select(string.Format("AsymID = '{0}'", chainList[0]));
                foreach (DataRow seqAlignRow in seqAlignRows)
                {
                    DataRow newRow = unpEntityChainMatchTable.NewRow();
                    newRow.ItemArray = seqAlignRow.ItemArray;
                    unpEntityChainMatchTable.Rows.Add(newRow);
                }
            }
            unpEntitySeqMatchTable = unpEntityChainMatchTable;
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        private void CreatUnpInterNetTables (bool isUpdate)
        {
            unpInterNetTable = new DataTable (unpInterNetTableName);
            string[] interNetColumns = { "UnpID1", "UnpID2", "RelSeqID", "PdbID", "DomainInterfaceID", "IsReversed"};
            foreach (string col in interNetColumns)
            {
                unpInterNetTable.Columns.Add(new DataColumn (col));
            }
            if (! isUpdate)
            {
                string createTableString = "CREATE TABLE " + unpInterNetTableName + "( " +
                    "UnpID1 Varchar(50) Not Null, " +
                    "UnpID2 Varchar(50) Not Null, " +
                    "RelSeqID Integer, " + 
                    "PdbID CHAR(4) Not Null, " +              
                    "DomainInterfaceID Integer Not Null, " +
                    "IsReversed CHAR(1) Not Null)";
                DbCreator dbCreate = new DbCreator();
                dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, unpInterNetTableName);
                string createIndexString = "Create Index UnpInterNet_unps On " + unpInterNetTableName + "(UnpID1, UnpID2)";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, unpInterNetTableName);
                createIndexString = "Create Index UnpInterNet_pdb On " + unpInterNetTableName + "(PdbID, DomainInterfaceID)";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, unpInterNetTableName);

                createTableString = "CREATE TABLE " + unpPdbfamTableName + "( " +
                    "UnpID Varchar(40) Not Null, " +
                    "PdbID CHAR(4) Not Null, " +
                    "EntityID Integer Not Null,  " +
                    "DomainID BigInt Not Null, " +
                    "UnpStart Integer, " +
                    "UnpEnd Integer);";
                dbCreate.CreateTableFromString(ProtCidSettings.pdbfamDbConnection, createTableString, unpPdbfamTableName);
                createIndexString = "Create Index UnpPdbfam_pdb On " + unpPdbfamTableName + "(PdbID)";
                dbCreate.CreateIndex(ProtCidSettings.pdbfamDbConnection, createIndexString, unpPdbfamTableName);
                createIndexString = "Create Index UnpPdbfam_Unp On " + unpPdbfamTableName + "(UnpID)";
                dbCreate.CreateIndex(ProtCidSettings.pdbfamDbConnection, createIndexString, unpPdbfamTableName);
             }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable ReadUnpPdbfamFromDb()
        {
            string queryString = "Select * From " + unpPdbfamTableName + ";";
            DataTable unpPdbfamTableDb = ProtCidSettings.pdbfamQuery.Query(queryString);
            unpPdbfamTableDb.TableName = unpPdbfamTableName;
            return unpPdbfamTableDb;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable ReadUnpPdbfamFromDb(string[] updateEntries)
        {
            string queryString = "";
            DataTable unpPdbfamTableDb = null;
            for (int i = 0; i < updateEntries.Length; i += 300)
            {
                string[] subArray = ParseHelper.GetSubArray(updateEntries, i, 300);
                queryString = string.Format ( "Select * From {0} Where PdbID IN ({1});", unpPdbfamTableName, ParseHelper.FormatSqlListString (subArray));
                DataTable subUnpPdbfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(subUnpPdbfamTable, ref unpPdbfamTableDb);
            }
            unpPdbfamTableDb.TableName = unpPdbfamTableName;
            return unpPdbfamTableDb;
        }
        #endregion

        #region debug -- add uniprot defined in xml but missed in sifts
        public void UpdateUnpInteractionNetTableFromXml ()
        {
            string[] noSiftsUnpEntries = GetEntriesNoDbRefSifts();
            AddMissingUnpInteractionNetTableFromXml (noSiftsUnpEntries);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void AddMissingUnpInteractionNetTableFromXml (string[] updateEntries)
        {
            // update UnpPdbfam table
            // update unppdbdomaininterfaces table
            bool isUpdate = true;
            CreatUnpInterNetTables(isUpdate);

            dbUnpPdbfamTable = SetUnpPdbfamTableInDb();

            ProtCidSettings.logWriter.WriteLine(DateTime.Today.ToShortDateString());
            ProtCidSettings.logWriter.WriteLine("Uniprots interactions in PDB");

            ProtCidSettings.logWriter.WriteLine("Retrieve PDBfam from db");
            DataTable pdbfamTable = SetPdbPfamAssignTable(updateEntries);  // get pfam assignments of pdb

            ProtCidSettings.logWriter.WriteLine("Unp-Pdb entity table from DB");
            //           DataTable unpEntityTable = GetUnpEntityTableSifts(updateEntries);  // unp and pdb entity match based on sifts
            DataTable unpEntityTable = GetUnpEntityTable(updateEntries);  // unp and pdb entity match based on sifts and xml, modified on Dec. 3, 2018 

            ProtCidSettings.logWriter.WriteLine("Add Unp info columns");
            DataTable unpPdbfamTable = SetUnpPdbfamAssignTable(pdbfamTable);  // pdbfam with unp columns: unpId, unpstart, unpend

            ProtCidSettings.logWriter.WriteLine("Unp-Pdb sequence match data");
            DataTable unpPdbEntitySeqMathTableXml = SetUnpPdbSeqMatchTableFromXml(updateEntries);

            ProtCidSettings.logWriter.WriteLine("Delete Unp-Pdbfam data for updated entries");
            DeleteUnpPdbfamData(updateEntries);

            // add unp info columns to pdbfam           
            ProtCidSettings.logWriter.WriteLine("Build UnpPdbfam table, add uniprot ID, start and end position to Pdbfam domains ");
            AddUnpToPdbfamData(pdbfamTable, unpEntityTable, unpPdbfamTable, unpPdbEntitySeqMathTableXml);

            ProtCidSettings.logWriter.WriteLine("Get the list of Uniprots with PDBfam");
            unpPdbfamTable = ReadUnpPdbfamFromDb(updateEntries);
            List<string> unpCodesInPfam = GetUnpCodesInPfam(unpPdbfamTable);

    //        ProtCidSettings.logWriter.WriteLine("Delete uniprot interaction data from updated entries");
    //        DeleteUnpInteractionData(updateEntries);

            ProtCidSettings.logWriter.WriteLine("Build UnpPdbDomainInterfaces. Retrieve Unp-Unp domain interfaces");
            ProtCidSettings.logWriter.WriteLine("#Uniprots have Pfam domains in PDB: " + unpCodesInPfam.Count.ToString());
            int totalPairwise = (unpCodesInPfam.Count * (unpCodesInPfam.Count + 1)) / 2;
            ProtCidSettings.logWriter.WriteLine("#pairwise uniprots: " + totalPairwise);
            int count = 0;

            for (int i = 0; i < unpCodesInPfam.Count; i++)
            {
                for (int j = i; j < unpCodesInPfam.Count; j++)
                {
                    count++;

                    try
                    {
                        RetrievePfamInteractionsBtwUnps(unpCodesInPfam[i], unpCodesInPfam[j], unpPdbfamTable);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.logWriter.WriteLine(count.ToString() + ": " + unpCodesInPfam[i] + " " + unpCodesInPfam[j] + " " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                    if (unpInterNetTable.Rows.Count > 0)
                    {
                        InsertUnpDomainInterfaceTableToDb(unpInterNetTable);
                    }
                }
            }
            ProtCidSettings.logWriter.WriteLine("Build Unp Domain interfaces done!");
            ProtCidSettings.logWriter.Flush();
            ProtCidSettings.protcidQuery.Dispose();
            ProtCidSettings.pdbfamQuery.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbfamTable"></param>
        /// <param name="unpEntityTable"></param>
        /// <param name="unpPdbfamTable"></param>
        /// <param name="unpPdbEntitySeqMathTableXml">no one-to-one residue match</param>
        public void AddUnpToPdbfamData(DataTable pdbfamTable, DataTable unpEntityTable, DataTable unpPdbfamTable, DataTable unpPdbEntitySeqMathTableXml)
        {
            string pdbId = "";
            string unpId = "";
            int entityId = 0;

            foreach (DataRow entityRow in unpEntityTable.Rows)
            {
                pdbId = entityRow["PdbID"].ToString();
                unpId = entityRow["UnpID"].ToString().TrimEnd();
                entityId = Convert.ToInt32(entityRow["EntityID"].ToString());

                try
                {
                    AddUnpInfoToEntityDomainsXml (unpId, pdbId, entityId, pdbfamTable, unpPdbfamTable, unpPdbEntitySeqMathTableXml);
                    InsertUnpPdbfamToDb(unpPdbfamTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine(unpId + " " + pdbId + entityId.ToString() + " add unp to pdbfam error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpId"></param>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private void AddUnpInfoToEntityDomainsXml(string unpId, string pdbId, int entityId, DataTable pdbfamTable, DataTable unpPdbfamTable,
             DataTable unpPdbEntitySeqMathTableXml)
        {
            DataRow[] domainRows = pdbfamTable.Select(string.Format("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId));
           
            int[] domainRegions = new int[2];
            int[] unpSeqRegions = null;
            string outIndexErrorMsg = "";
            foreach (DataRow domainRow in domainRows)
            {
                domainRegions[0] = Convert.ToInt32(domainRow["SeqStart"].ToString());
                domainRegions[1] = Convert.ToInt32(domainRow["SeqEnd"].ToString());
                entityId = Convert.ToInt32(domainRow["EntityID"].ToString());                
                unpSeqRegions = GetUnpPfamRegionsXml(unpId, pdbId, entityId, domainRegions, unpPdbEntitySeqMathTableXml, out outIndexErrorMsg);
                if (unpSeqRegions[0] != -1 && unpSeqRegions[1] != -1)
                {
                    domainRegions[1] = Convert.ToInt32(domainRow["SeqEnd"].ToString());
                    DataRow unpDomainRow = unpPdbfamTable.NewRow();
                    foreach (DataColumn dCol in pdbfamTable.Columns)
                    {
                        unpDomainRow[dCol.ColumnName] = domainRow[dCol.ColumnName];
                    }
                    unpDomainRow["UnpID"] = unpId;
                    unpDomainRow["UnpStart"] = unpSeqRegions[0];
                    unpDomainRow["UnpEnd"] = unpSeqRegions[1];
                    unpPdbfamTable.Rows.Add(unpDomainRow);
                }
                else
                {
                    if (outIndexErrorMsg != "")
                    {
                        ProtCidSettings.logWriter.WriteLine(unpId + " " + ParseHelper.FormatDataRow(domainRow));
                        ProtCidSettings.logWriter.WriteLine(outIndexErrorMsg);
                    }
                    else
                    {
                        ProtCidSettings.logWriter.WriteLine(unpId + " " + ParseHelper.FormatDataRow(domainRow));
                        ProtCidSettings.logWriter.WriteLine("No unp-pdb seq match: " + unpSeqRegions[0].ToString() + " " + unpSeqRegions[1].ToString());
                    }
                    ProtCidSettings.logWriter.Flush();
                }
            }
        }
        #endregion
    }
}
