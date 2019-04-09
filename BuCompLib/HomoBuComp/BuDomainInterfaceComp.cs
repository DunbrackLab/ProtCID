using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using ProtCidSettingsLib;
using CrystalInterfaceLib.DomainInterfaces;
using CrystalInterfaceLib.Settings;
using DbLib;

namespace BuCompLib.HomoBuComp
{
    public class BuDomainInterfaceComp
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        private DbInsert dbInsert = new DbInsert();
        private BuDomainInterfaceRetriever domainInterfaceRetriever = new BuDomainInterfaceRetriever();
        private DomainInterfaceComp domainInterfaceComp = new DomainInterfaceComp();
        private DataTable domainInterfaceCompTable = null;
#if DEBUG
        private StreamWriter dataWriter = null;
#endif
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void CompareBuDomainInterfaces()
        {
            InitializeTable();
            InitializeDbTable();

            domainInterfaceComp.nonAlignDomainWriter = new StreamWriter("BuDomainInterfaceCompLog.txt", true);
            domainInterfaceComp.nonAlignDomainWriter.WriteLine(BuCompBuilder.BuType + " domain interface comparison");

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Compare Domain Interfaces";
#if DEBUG
            dataWriter = new StreamWriter(BuCompBuilder.BuType + "DomainInterfaceComp.txt");
#endif

            string tableName = "";
            if (BuCompBuilder.BuType == "asu")
            {
                tableName = "asu" + "PfamDomainInterfaces";
            }
            else
            {
                tableName = BuCompBuilder.BuType + "PfamBuDomainInterfaces";
            }
            string queryString = string.Format("Select RelSeqID, count(Distinct pdbId) AS EntryCount " +
                " From {0} Group by RelSeqID;", tableName);
            DataTable relationTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            int relSeqId = -1;
            int entryCount = -1;

            ProtCidSettings.progressInfo.totalOperationNum = relationTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = relationTable.Rows.Count;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare Domain-domain interfaces in BUs.");
            foreach (DataRow relRow in relationTable.Rows)
            {
                relSeqId = Convert.ToInt32(relRow["RelSeqID"].ToString());
                entryCount = Convert.ToInt32(relRow["EntryCount"].ToString());

                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                if (entryCount < 60)
                //       if (entryCount < 90 && entryCount >= 60)
                //        if (entryCount < 120 && entryCount >= 90)
                //        if (entryCount < 150 && entryCount >= 120)
                //        if (entryCount < 240 && entryCount >= 200)
             //   if (entryCount < 250 && entryCount >= 240)
                {
                    CompareBuDomainInterfaces(relSeqId);
                }
            }
#if DEBUG
            dataWriter.Close();
#endif
            domainInterfaceComp.nonAlignDomainWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        public void CompareBuDomainInterfaces(int relSeqId)
        {
            bool isIndividualRelation = false;
#if DEBUG
            if (dataWriter == null)
            {
                dataWriter = new StreamWriter(BuCompBuilder.BuType + "DomainInterfaceComp" + relSeqId.ToString() + ".txt");
                isIndividualRelation = true;

                ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
                ProtCidSettings.progressInfo.currentOperationLabel = "Compare Domain Interfaces";
            }
#endif
            if (domainInterfaceCompTable == null)
            {
                InitializeTable();
            }

            string tableName = "";
            if (BuCompBuilder.BuType == "asu")
            {
                tableName = BuCompBuilder.BuType + "PfamDomainInterfaces";
            }
            else
            {
                tableName = BuCompBuilder.BuType + "PfamBuDomainInterfaces";
            }
            string queryString = string.Format("Select Distinct PdbID From {0} Where RelSeqID = {1};",
                tableName, relSeqId);
            DataTable relEntryTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            string pdbId = "";
            Dictionary<string,Dictionary<string, DomainInterface[]>> entryBuDomainInterfaceHash = new Dictionary<string,Dictionary<string,DomainInterface[]>> ();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString());

            if (isIndividualRelation)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving domain interfaces for each entry.");

                ProtCidSettings.progressInfo.totalOperationNum = relEntryTable.Rows.Count;
                ProtCidSettings.progressInfo.totalStepNum = relEntryTable.Rows.Count;
            }
            foreach (DataRow entryRow in relEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                if (isIndividualRelation)
                {
                    ProtCidSettings.progressInfo.currentOperationNum++;
                    ProtCidSettings.progressInfo.currentStepNum++;
                }
                Dictionary<string, DomainInterface[]> buDomainInterfaceHash = domainInterfaceRetriever.RetrieveDomainInterfaces(relSeqId, pdbId, BuCompBuilder.BuType);
                entryBuDomainInterfaceHash.Add(pdbId, buDomainInterfaceHash);
            }

            List<string> entryList = new List<string> (entryBuDomainInterfaceHash.Keys);
            entryList.Sort();
            if (isIndividualRelation)
            {
                ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
                ProtCidSettings.progressInfo.totalOperationNum = (int)((double)(entryList.Count * (entryList.Count - 1)) / 2.0);
                ProtCidSettings.progressInfo.totalStepNum = (int)((double)(entryList.Count * (entryList.Count - 1)) / 2.0);

                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare Domain-domain interfaces in BUs.");
            }
            for (int i = 0; i < entryList.Count; i++)
            {
                Dictionary<string, DomainInterface[]> buDomainInterfacesHash1 = entryBuDomainInterfaceHash[entryList[i]];
                for (int j = i + 1; j < entryList.Count; j++)
                {
                    if (isIndividualRelation)
                    {
                        ProtCidSettings.progressInfo.currentFileName = entryList[i].ToString() + "_" + entryList[j].ToString();
                        ProtCidSettings.progressInfo.currentOperationNum++;
                        ProtCidSettings.progressInfo.currentStepNum++;
                    }

                    if (IsEntryCompExisting(entryList[i].ToString(), entryList[j].ToString()))
                    {
                        continue;
                    }
                    ProtCidSettings.progressInfo.currentFileName = entryList[i].ToString() + "_" + entryList[j].ToString();

                    Dictionary<string, DomainInterface[]> buDomainInterfacesHash2 = entryBuDomainInterfaceHash[entryList[j]];
                    foreach (string buId1 in buDomainInterfacesHash1.Keys)
                    {
                        DomainInterface[] domainInterfaces1 = (DomainInterface[])buDomainInterfacesHash1[buId1];
                        foreach (string buId2 in buDomainInterfacesHash2.Keys)
                        {
                            DomainInterface[] domainInterfaces2 = (DomainInterface[])buDomainInterfacesHash2[buId2];
                            DomainInterfacePairInfo[] compPairInfos =
                                domainInterfaceComp.CompareDomainInterfaces(domainInterfaces1, domainInterfaces2);
                            InsertDataToTable(relSeqId, entryList[i].ToString(), entryList[j].ToString(),
                                buId1, buId2, compPairInfos);
                        }
                    }
#if DEBUG
                    WriteCompDataToFile(domainInterfaceCompTable);
#endif
                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.buCompConnection, domainInterfaceCompTable);
                    domainInterfaceCompTable.Clear();
                }
            }
            if (isIndividualRelation)
            {
#if DEBUG
                dataWriter.Close();
#endif
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            }
        }

        #region update db table
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateCompareBuDomainInterfaces(string[] updateEntries)
        {
#if DEBUG
            dataWriter = new StreamWriter(BuCompBuilder.BuType + "DomainInterfaceComp.txt");
#endif

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Compare Domain Interfaces";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update BU domain interfaces comparison");

            InitializeTable();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Delete existing data in Db.");
            DeleteObsDataInDb(updateEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get the relations needed to be updated.");
            Dictionary<int, List<string>> updateRelationEntriesHash = GetRelSeqIDsForUpdate(updateEntries);

            ProtCidSettings.progressInfo.totalOperationNum = updateRelationEntriesHash.Count;
            ProtCidSettings.progressInfo.totalStepNum = updateRelationEntriesHash.Count;

            List<int> updateRelSeqIdList = new List<int> (updateRelationEntriesHash.Keys);
            updateRelSeqIdList.Sort();
            foreach (int relSeqId in updateRelSeqIdList)
            {
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                List<string> relationUpdateEntryList = updateRelationEntriesHash[relSeqId];
                relationUpdateEntryList.Sort();
                string[] relationUpdateEntries = new string[relationUpdateEntryList.Count];
                relationUpdateEntryList.CopyTo(relationUpdateEntries);
                try
                {
                    UpdateCompareBuDomainInterfaces(relSeqId, relationUpdateEntries);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update relation " + relSeqId.ToString () + " errors: " + ex.Message);
                }
            }
#if DEBUG
            dataWriter.Close();
#endif
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private Dictionary<int, List<string>> GetRelSeqIDsForUpdate(string[] updateEntries)
        {
            string tableName = "";
            if (BuCompBuilder.BuType == "asu")
            {
                tableName = "asu" + "PfamDomainInterfaces";
            }
            else
            {
                tableName = BuCompBuilder.BuType + "PfamBuDomainInterfaces";
            }

            Dictionary<int, List<string>> updateRelationEntriesHash = new Dictionary<int, List<string>>();
            foreach (string entry in updateEntries)
            {
                int[] entryRelSeqIDs = GetRelSeqIDForEntry(entry, tableName);
                foreach (int relSeqId in entryRelSeqIDs)
                {
                    if (updateRelationEntriesHash.ContainsKey(relSeqId))
                    {
                        updateRelationEntriesHash[relSeqId].Add(entry);
                    }
                    else
                    {
                        List<string> entryList = new List<string> ();
                        entryList.Add(entry);
                        updateRelationEntriesHash.Add(relSeqId, entryList);
                    }
                }
            }
            return updateRelationEntriesHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="dbTable"></param>
        /// <returns></returns>
        private int[] GetRelSeqIDForEntry(string pdbId, string dbTable)
        {
            string queryString = string.Format("Select Distinct RelSeqID From {0} Where PdbID = '{1}';", 
                dbTable, pdbId);
            DataTable relSeqIdTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
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
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="updateEntries"></param>
        public void UpdateCompareBuDomainInterfaces(int relSeqId, string[] updateEntries)
        {
            string tableName = "";
            if (BuCompBuilder.BuType == "asu")
            {
                tableName = BuCompBuilder.BuType + "PfamDomainInterfaces";
            }
            else
            {
                tableName = BuCompBuilder.BuType + "PfamBuDomainInterfaces";
            }
            string queryString = string.Format("Select Distinct PdbID From {0} Where RelSeqID = {1};",
                tableName, relSeqId);
            DataTable relEntryTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            string pdbId = "";
            Dictionary<string, Dictionary<string, DomainInterface[]>> entryBuDomainInterfaceHash = new Dictionary<string,Dictionary<string,DomainInterface[]>> ();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString());

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve BU domain interfaces for each entry");
            foreach (DataRow entryRow in relEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                Dictionary<string, DomainInterface[]> buDomainInterfaceHash = domainInterfaceRetriever.RetrieveDomainInterfaces(relSeqId, pdbId, BuCompBuilder.BuType);
                entryBuDomainInterfaceHash.Add(pdbId, buDomainInterfaceHash);
            }

            List<string> entryList = new List<string> (entryBuDomainInterfaceHash.Keys);
            entryList.Sort();

            List<string> parsedUpdateEntryList = new List<string> ();

            for (int i = 0; i < updateEntries.Length; i++)
            {
                Dictionary<string, DomainInterface[]> buDomainInterfacesHash1 = entryBuDomainInterfaceHash[updateEntries[i]];
                for (int j = 0; j < entryList.Count; j++)
                {
                    ProtCidSettings.progressInfo.currentFileName = updateEntries[i].ToString() + "_" + entryList[j].ToString();

                    if (updateEntries[i] == entryList[j].ToString())
                    {
                        continue;
                    }

                    if (parsedUpdateEntryList.Contains(entryList[j]))
                    {
                        continue;
                    }
                    Dictionary<string, DomainInterface[]> buDomainInterfacesHash2 = entryBuDomainInterfaceHash[entryList[j]];

                    if (string.Compare(updateEntries[i], entryList[j].ToString()) > 0)
                    {
                        foreach (string buId2 in buDomainInterfacesHash2.Keys)
                        {
                            DomainInterface[] domainInterfaces2 = (DomainInterface[])buDomainInterfacesHash2[buId2];
                            foreach (string buId1 in buDomainInterfacesHash1.Keys)
                            {
                                DomainInterface[] domainInterfaces1 = (DomainInterface[])buDomainInterfacesHash1[buId1];
                                DomainInterfacePairInfo[] compPairInfos =
                                    domainInterfaceComp.CompareDomainInterfaces(domainInterfaces2, domainInterfaces1);
                                InsertDataToTable(relSeqId, entryList[j].ToString(), updateEntries[i], 
                                    buId2, buId1, compPairInfos);
                            }
                        }
                    }
                    else
                    {
                        foreach (string buId1 in buDomainInterfacesHash1.Keys)
                        {
                            DomainInterface[] domainInterfaces1 = (DomainInterface[])buDomainInterfacesHash1[buId1];
                            foreach (string buId2 in buDomainInterfacesHash2.Keys)
                            {
                                DomainInterface[] domainInterfaces2 = (DomainInterface[])buDomainInterfacesHash2[buId2];
                                DomainInterfacePairInfo[] compPairInfos =
                                    domainInterfaceComp.CompareDomainInterfaces(domainInterfaces1, domainInterfaces2);
                                InsertDataToTable(relSeqId, updateEntries[i], entryList[j].ToString(),
                                    buId1, buId2, compPairInfos);
                            }
                        }
                    }
#if DEBUG
                    WriteCompDataToFile(domainInterfaceCompTable);
#endif
                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.buCompConnection, domainInterfaceCompTable);
                    domainInterfaceCompTable.Clear();
                }
                parsedUpdateEntryList.Add(updateEntries[i]);
            }
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <returns></returns>
        private bool IsEntryCompExisting(string pdbId1, string pdbId2)
        {
            string queryString = string.Format("Select * From {0} Where PdbID1 = '{1}' AND PdbID2 = '{2}';", 
                domainInterfaceCompTable.TableName, pdbId1, pdbId2);
            DataTable compTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            if (compTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
#if DEBUG
        private void WriteCompDataToFile(DataTable domainInterfaceCompTable)
        {
            string dataLine = "";
            foreach (DataRow compRow in domainInterfaceCompTable.Rows)
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
#endif

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="buId1"></param>
        /// <param name="buId2"></param>
        /// <param name="compPairInfos"></param>
        private void InsertDataToTable(int relSeqId, string pdbId1, string pdbId2, string buId1, string buId2, 
            DomainInterfacePairInfo[] compPairInfos)
        {
            foreach (DomainInterfacePairInfo pairInfo in compPairInfos)
            {
                if (pairInfo.qScore >= AppSettings.parameters.contactParams.minQScore)
                {
                    DataRow dataRow = domainInterfaceCompTable.NewRow();
                    dataRow["RelSeqID"] = relSeqId;
                    dataRow["PdbID1"] = pdbId1;
                    dataRow["PdbID2"] = pdbId2;
                    dataRow["BuID1"] = buId1;
                    dataRow["BuID2"] = buId2;
                    dataRow["DomainInterfaceID1"] = pairInfo.interfaceInfo1.domainInterfaceId;
                    dataRow["DomainInterfaceID2"] = pairInfo.interfaceInfo2.domainInterfaceId;
                    dataRow["Qscore"] = pairInfo.qScore;
                    domainInterfaceCompTable.Rows.Add(dataRow);
                }
            }
        }

        #region delete obsolete data from db
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        private void DeleteObsDataInDb(string[] updateEntries)
        {
            foreach (string entry in updateEntries)
            {
                DeleteObsEntryDataInDb(entry);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteObsEntryDataInDb(string pdbId)
        {
            string deleteString = string.Format("Delete From {0} Where PdbID1 = '{1}' OR PdbID2 = '{1}';", 
                domainInterfaceCompTable.TableName, pdbId);
            dbQuery.Query(ProtCidSettings.buCompConnection, deleteString);
        }
        #endregion

        #region initialize table
        /// <summary>
        /// 
        /// </summary>
        private void InitializeTable()
        {
            string tableName = "";
            if (BuCompBuilder.BuType == "asu")
            {
                tableName = BuCompBuilder.BuType + "DomainInterfaceComp";
            }
            else
            {
                tableName = BuCompBuilder.BuType + "BuDomainInterfaceComp";
            }
            domainInterfaceCompTable = new DataTable(tableName);

            string[] compColumns = {"RelSeqID", "PdbID1", "PdbID2", "BuID1", "BuID2", "DomainInterfaceID1", 
                                   "DomainInterfaceID2", "QScore"};
            foreach (string compCol in compColumns)
            {
                domainInterfaceCompTable.Columns.Add(new DataColumn (compCol));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void InitializeDbTable()
        {
            DbCreator dbCreator = new DbCreator();
            if (!dbCreator.IsTableExist(ProtCidSettings.buCompConnection, domainInterfaceCompTable.TableName))
            {
                string createTableString = "CREATE TABLE " + domainInterfaceCompTable.TableName + " ( " +
                    "RelSeqID INTEGER NOT NULL, " +
                    "PdbID1 VARCHAR(4) NOT NULL, " +
                    "PdbID2 VARCHAR(4) NOT NULL, " +
                    "BuID1 VARCHAR(8) NOT NULL, " +
                    "BuID2 VARCHAR(8) NOT NULL, " +
                    "DomainInterfaceID1 INTEGER NOT NULL, " +
                    "DomainInterfaceID2 INTEGER NOT NULL, " +
                    "Qscore FLOAT NOT NULL);";
                dbCreator.CreateTableFromString(ProtCidSettings.buCompConnection, createTableString, domainInterfaceCompTable.TableName);
            }
        }
        #endregion

        #region import data into database
        public void ImportBuDomainInterfacedCompDataIntoDb()
        {
            string dataSrcDir = @"E:\DbProjectData\PfamBuDomainInterfaceComp";
            ProtCidSettings.progressInfo.Reset();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Import Bu Domain Interface comparison data into db.");

            ProtCidSettings.progressInfo.progStrQueue.Enqueue(BuCompBuilder.BuType);

            InitializeTable();
            InitializeDbTable();

            ImportBuDomainInterfaceCompDataIntoDb(BuCompBuilder.BuType, dataSrcDir + "_" + BuCompBuilder.BuType);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buType"></param>
        /// <param name="dataSrcDir"></param>
        public void ImportBuDomainInterfaceCompDataIntoDb(string buType, string dataSrcDir)
        {
            string[] dirFiles = Directory.GetFiles(dataSrcDir);
            List<string> dataFileList = new List<string> ();
            foreach (string dirFile in dirFiles)
            {
                if (dirFile.IndexOf("DomainInterfaceComp") > -1 &&
                    dirFile.IndexOf("Log") < 0 && dirFile.IndexOf ("JobList") < 0)
                {
                    dataFileList.Add(dirFile);
                }
            }

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = dataFileList.Count;
            ProtCidSettings.progressInfo.totalStepNum = dataFileList.Count;

            foreach (string dataFile in dataFileList)
            {
                ProtCidSettings.progressInfo.currentFileName = dataFile;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(dataFile);

                // use as parameter to insert into the db
                DataRow dataRow = domainInterfaceCompTable.NewRow();

                ParseBuDomainInterfaceCompFile(dataFile, dataRow);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataFile"></param>
        private void ParseBuDomainInterfaceCompFile(string dataFile, DataRow dataRow)
        {
            StreamReader dataReader = new StreamReader(dataFile);
            string line = "";
            
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split(',');
                dataRow.ItemArray = fields;
                dbInsert.InsertDataIntoDb(ProtCidSettings.buCompConnection, dataRow);
            }
            dataReader.Close();
        }
        #endregion
    }
}
