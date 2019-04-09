using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using ProtCidSettingsLib;
using DbLib;
using AuxFuncLib;
using DataCollectorLib.FatcatAlignment;
using InterfaceClusterLib.DataTables;

namespace InterfaceClusterLib.Alignments
{
    /*
     * due to the "too many open file handles " from the database or Windows 10,
     * I changed the update of missing entry alignments on entry-based interfaces
     * on April 4, 2017
     * */
    public class GroupEntryAlignmentsUpdate : GroupEntryAlignments 
    {

        #region update group entry alignments
        /*
         * Due to the exception "too many open handles"
         * optimize codes on April 1, 2017
         * */
        private DataTable entityCrcRepTable = null;
        StreamWriter delDataWriter = null;
        private void InitializeEntityCrcRepTable()
        {
            entityCrcRepTable = new DataTable("EntityCrcRep");
            string[] mapColumns = { "PdbID", "EntityID", "AsymID", "AuthorChain", "crc", "RepPdbID", "RepEntityID", "RepAsymID", "RepAuthorChain" };
            foreach (string mapCol in mapColumns)
            {
                entityCrcRepTable.Columns.Add(new DataColumn(mapCol));
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateGroups"></param>
        public void UpdateGroupMissingEntryAlignments(int[] updateGroups)
        {
            HomoGroupTables.InitializeTables();
            InitializeEntityCrcRepTable();

            delDataWriter = new StreamWriter("DeletedAlignData.txt", true);
            delDataWriter.WriteLine(DateTime.Today.ToShortDateString ());

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve rep entries alignments info in each group.");
            ProtCidSettings.progressInfo.currentOperationLabel = "Alignments info";

            string tableName = ProtCidSettings.dataType + "HomoGroupEntryAlign";
            ProtCidSettings.logWriter.WriteLine(tableName);
            ProtCidSettings.logWriter.WriteLine("Retrieve rep entries alignments info in each group.");

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue(tableName);
            ProtCidSettings.progressInfo.totalOperationNum = updateGroups.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateGroups.Length;

            foreach (int groupId in updateGroups)
            {
                ProtCidSettings.progressInfo.currentFileName = groupId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    DataTable missingAlignTable = GetMissingAlignTable(groupId, tableName);
                    if (missingAlignTable.Rows.Count == 0)
                    {
                        continue;
                    }
                    string[] groupEntries = GetGroupAlignEntries(missingAlignTable);
                    SetEntryPdbCrcMapTable(groupEntries);
                    UpdateMissingAlignEntryTable(groupId, missingAlignTable, entityCrcRepTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine(groupId + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(groupId + " " + ex.Message);
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue(tableName + " Done!");

            tableName = ProtCidSettings.dataType + "HomoRepEntryAlign";
            ProtCidSettings.logWriter.WriteLine(tableName);
            ProtCidSettings.logWriter.WriteLine("Retrieve rep entries alignments info in each group.");

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue(tableName);
            ProtCidSettings.progressInfo.totalOperationNum = updateGroups.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateGroups.Length;

            foreach (int groupId in updateGroups)
            {
                ProtCidSettings.progressInfo.currentFileName = groupId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    DataTable missingAlignTable = GetMissingAlignTable(groupId, tableName);
                    if (missingAlignTable.Rows.Count == 0)
                    {
                        continue;
                    }
                    string[] groupEntries = GetGroupAlignEntries(missingAlignTable);
                    SetEntryPdbCrcMapTable(groupEntries);
                    UpdateMissingAlignEntryTable(groupId, missingAlignTable, entityCrcRepTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.logWriter.WriteLine(groupId + " " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(groupId + " " + ex.Message);
                }
            }
            delDataWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue(tableName + " Done!");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Done!");
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private DataTable GetMissingAlignTable(int groupId, string tableName)
        {
            string queryString = string.Format("Select * From {0} Where GroupSeqID = {1} AND (EntityId1 = -1 OR EntityID2 = -1);",
                       tableName, groupId);
            DataTable alignTable = ProtCidSettings.protcidQuery.Query(queryString);
            alignTable.TableName = tableName;
            return alignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignTable"></param>
        /// <returns></returns>
        private string[] GetGroupAlignEntries(DataTable alignTable)
        {
            List<string> entryList = new List<string> ();
            string pdbId = "";
            foreach (DataRow dataRow in alignTable.Rows)
            {
                pdbId = dataRow["PdbID1"].ToString();
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }

                pdbId = dataRow["PdbID2"].ToString();
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            return entryList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        private void SetEntryPdbCrcMapTable(string[] entries)
        {
            entityCrcRepTable.Clear();
            string queryString = "";
            string[] subEntries = null;
            int subLength = 100;
            DataTable pdbCrcTable = null;
            for (int i = 0; i < entries.Length; i += subLength)
            {
                subEntries = ParseHelper.GetSubArray(entries, i, subLength);
                queryString = string.Format("Select PdbID, EntityID, AsymID, AuthorChain, Crc From PdbCrcMap Where PdbID IN ({0});",
                    ParseHelper.FormatSqlListString (subEntries));
                DataTable subPdbCrcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(subPdbCrcTable, ref pdbCrcTable);
            }

            List<string> crcList = new List<string> ();
            string crc = "";
            foreach (DataRow pdbCrcRow in pdbCrcTable.Rows)
            {
                crc = pdbCrcRow["crc"].ToString().TrimEnd();
                if (! crcList.Contains (crc))
                {
                    crcList.Add(crc);
                }
            }
            List<string> subCrcList = null;
            DataTable crcRepChainTable = null;
            for (int i = 0; i < crcList.Count; i += subLength)
            {
                subCrcList = ParseHelper.GetSubList(crcList, i, subLength);
                queryString = string.Format("Select PdbID, EntityID, AsymID, AuthorChain, crc From PdbCrcMap Where crc IN ({0}) AND IsRep = '1';",
                    ParseHelper.FormatSqlListString (subCrcList.ToArray ()));
                DataTable repChainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                ParseHelper.AddNewTableToExistTable(repChainTable, ref crcRepChainTable);
            }
            foreach (DataRow chainRow in pdbCrcTable.Rows)
            {
                crc = chainRow["crc"].ToString ().TrimEnd ();
                DataRow repChainRow = entityCrcRepTable.NewRow();
                List<object> itemList =new List<object> (chainRow.ItemArray);
                DataRow[] crcRepRows = crcRepChainTable.Select(string.Format ("crc = '{0}'", crc));
                if (crcRepRows.Length > 0)
                {
                    itemList.Add(crcRepRows[0]["PdbID"]);
                    itemList.Add(crcRepRows[0]["EntityID"]);
                    itemList.Add(crcRepRows[0]["AsymID"]);
                    itemList.Add(crcRepRows[0]["AuthorChain"]);
                }
                else
                {
                    itemList.Add(chainRow["PdbID"]);
                    itemList.Add(chainRow["EntityID"]);
                    itemList.Add(chainRow["AsymID"]);
                    itemList.Add(chainRow["AuthorChain"]);
                }
                repChainRow.ItemArray = itemList.ToArray ();
                entityCrcRepTable.Rows.Add(repChainRow);
            }
        }
        #endregion

        #region update missing entry alignments 
        HHAlignments hhAlign = new HHAlignments();
        public void UpdateMissingAlignEntryTable (int groupId, DataTable missingEntryTable, DataTable groupEntityCrcRepTable)
        {
            string[] groupCrcs = GetAlignCrcs(groupEntityCrcRepTable);
            string[] repEntries = GetRepEntries(groupEntityCrcRepTable);
            string[] missingAlignEntryPairs = GetMissingAlignEntryPairs(missingEntryTable);
            DataTable alignmentTable = GetAlignmentTable(groupCrcs, repEntries, missingAlignEntryPairs, groupEntityCrcRepTable);
            DataTable fillEntryTable = FillMissingAlignTable(groupId, missingEntryTable, alignmentTable);
            if (fillEntryTable.Rows.Count > 0)
            {
       //         DeleteEntryAlignments(groupId, alignedEntryPairs, missingEntryTable.TableName);
                DeleteEntryPairAlignments (groupId, missingEntryTable.TableName);
                dbInsert.BatchInsertDataIntoDBtables (ProtCidSettings.protcidDbConnection, fillEntryTable);
                fillEntryTable.Clear();
            }          
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="alignedEntryPairs"></param>
        /// <param name="tableName"></param>
        private void DeleteEntryAlignments (int groupId, string[] alignedEntryPairs, string tableName)
        {
            string pdbId1 = "";
            string pdbId2 = "";
            foreach (string entryPair in alignedEntryPairs)
            {
                pdbId1 = entryPair.Substring(0, 4);
                pdbId2 = entryPair.Substring(4, 4);
                DeleteEntryAlignments(groupId, pdbId1, pdbId2, tableName);
                delDataWriter.WriteLine(tableName + " " + groupId + " " + entryPair);
            }
            delDataWriter.Flush();
        }

        /// <summary>
        /// this function is to delete entry alignments
        /// in which there is one entityid to be -1
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="tableName"></param>
        private void DeleteEntryPairAlignments(int groupId, string tableName)
        {
            string deleteString = string.Format("Delete From {0} WHere GroupSeqID = {1} AND (EntityID1 = -1 OR EntityID2 = -1)", tableName, groupId);
            ProtCidSettings.protcidQuery.Query(deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="missingEntryTable"></param>
        /// <param name="alignmentTable"></param>
        /// <returns></returns>
        public DataTable FillMissingAlignTable (int groupId, DataTable missingEntryTable, DataTable alignmentTable)
        {
            DataTable groupEntryAlignTable = missingEntryTable.Clone();
            string pdbId1 = "";
            string pdbId2 = "";
            string entityPair = "";
            List<string> addedEntityPairList = new List<string> ();
            foreach (DataRow alignRow in missingEntryTable.Rows)
            {
                pdbId1 = alignRow["PdbID1"].ToString();
                pdbId2 = alignRow["PdbID2"].ToString();
                
                DataRow[] alignmentRows = alignmentTable.Select(string.Format ("QueryEntry = '{0}' AND HitEntry = '{1}'", pdbId1, pdbId2));
                if (alignmentRows.Length == 0)  // if there is no alignments, then put it back to table
                {
                    DataRow dataRow = groupEntryAlignTable.NewRow();
                    dataRow.ItemArray = alignRow.ItemArray;
                    groupEntryAlignTable.Rows.Add(dataRow);
                }
                // add alignments to table
                foreach (DataRow alignmentRow in alignmentRows)
                {
                    entityPair = alignmentRow["QueryEntry"].ToString() + alignmentRow["QueryEntity"].ToString() +
                   "_" + alignmentRow["HitEntry"].ToString() + alignmentRow["HitEntity"].ToString();
                    if (addedEntityPairList.Contains(entityPair))
                    {
                        continue;
                    }

                    DataRow entryRow = groupEntryAlignTable.NewRow();
                    entryRow["GroupSeqID"] = groupId;
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
                    groupEntryAlignTable.Rows.Add(entryRow);
                    addedEntityPairList.Add(entityPair);
                }
                
                // no need to check the reversed order, since they are already checked when get the alignment table
/*
                DataRow[] reversedAlignmentRows = alignmentTable.Select(string.Format("HitEntry = '{0}' AND QueryEntry = '{1}'", pdbId1, pdbId2));
                foreach (DataRow alignmentRow in reversedAlignmentRows)
                {
                    entityPair = alignmentRow["HitEntry"].ToString() + alignmentRow["HitEntity"].ToString() +
                   "_" + alignmentRow["QueryEntry"].ToString() + alignmentRow["QueryEntity"].ToString();
                    if (addedEntityPairList.Contains(entityPair))
                    {
                        continue;
                    }

                    DataRow entryRow = groupEntryAlignTable.NewRow();
                    entryRow["GroupSeqID"] = groupId;
                    entryRow["PdbID1"] = alignmentRow["HitEntry"];
                    entryRow["EntityID1"] = alignmentRow["HitEntity"];
                    entryRow["PdbID2"] = alignmentRow["QueryEntry"];
                    entryRow["EntityID2"] = alignmentRow["QueryEntity"];
                    entryRow["Identity"] = alignmentRow["Identity"];
                    entryRow["QueryStart"] = alignmentRow["HitStart"];
                    entryRow["QueryEnd"] = alignmentRow["HitEnd"];
                    entryRow["HitStart"] = alignmentRow["QueryStart"];
                    entryRow["HitEnd"] = alignmentRow["QueryEnd"];
                    entryRow["QuerySequence"] = alignmentRow["HitSequence"];
                    entryRow["HitSequence"] = alignmentRow["QuerySequence"];
                    groupEntryAlignTable.Rows.Add(entryRow);
                    addedEntityPairList.Add(entityPair);

                    if (!addedEntryPairList.Contains(pdbId1 + pdbId2))
                    {
                        addedEntryPairList.Add(pdbId1 + pdbId2);
                    }
                }*/
            }

            return groupEntryAlignTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="crcs"></param>
        /// <param name="repEntries"></param>
        /// <param name="groupEntityCrcRepTable"></param>
        /// <returns></returns>
        public DataTable GetAlignmentTable (string[] crcs, string[] repEntries, string[] entryPairsToBeAligned, DataTable groupEntityCrcRepTable)
        {
            DataTable crcHhAlignTable = hhAlign.GetCrcAlignTable(crcs);
            DataTable hhAlignTable = hhAlign.ChangeCrcAlignToEntryAlign(crcHhAlignTable, entryPairsToBeAligned, groupEntityCrcRepTable);

            DataTable repStructAlignTable = entryAlignment.GetFatcatAlignTable (repEntries);
            DataTable structAlignTable = entryAlignment.ChangeRepAlignToEntryAlign(repStructAlignTable, entryPairsToBeAligned, groupEntityCrcRepTable);
            
            entryAlignment.CompareAlignments(hhAlignTable, structAlignTable);

            return hhAlignTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupEntityCrcRepTable"></param>
        /// <returns></returns>
        private string[] GetAlignCrcs (DataTable groupEntityCrcRepTable)
        {
            List<string> crcList = new List<string> ();
            string crc = "";
            foreach (DataRow crcRow in groupEntityCrcRepTable.Rows)
            {
                crc = crcRow["crc"].ToString().TrimEnd();
                if (! crcList.Contains (crc))
                {
                    crcList.Add(crc);
                }
            }
            return crcList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupEntityCrcRepTable"></param>
        /// <returns></returns>
        private string[] GetRepEntries (DataTable groupEntityCrcRepTable)
        {
            List<string> repEntryList = new List<string> ();
            string repPdbId = "";
            foreach (DataRow repRow in groupEntityCrcRepTable.Rows)
            {
                repPdbId = repRow["RepPdbID"].ToString();
                if (! repEntryList.Contains (repPdbId))
                {
                    repEntryList.Add(repPdbId);
                }
            }
            return repEntryList.ToArray ();
        }      

        /// <summary>
        /// 
        /// </summary>
        /// <param name="missingAlignTable"></param>
        /// <returns></returns>
        private string[] GetMissingAlignEntryPairs (DataTable missingAlignTable)
        {
            List<string> entryPairList = new List<string> ();
            string entryPair = "";
            foreach (DataRow alignRow in missingAlignTable.Rows)
            {
                entryPair = alignRow["PdbID1"].ToString() + alignRow["PdbID2"].ToString();
                if (! entryPairList.Contains (entryPair))
                {
                    entryPairList.Add(entryPair);
                }
            }
            return entryPairList.ToArray ();
        }
        #endregion
    }
}
