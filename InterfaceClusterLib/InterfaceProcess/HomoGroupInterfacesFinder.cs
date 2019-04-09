using System;
using System.IO;
using System.Data;
using System.Collections.Generic;
using DbLib;
using CrystalInterfaceLib.Crystal;
using ProtCidSettingsLib;
using ProgressLib;
using AuxFuncLib;
using InterfaceClusterLib.DataTables;
using CrystalInterfaceLib.Settings;

namespace InterfaceClusterLib.InterfaceProcess
{
	/// <summary>
	/// Detect common interface in a same-seq group
	/// </summary>
	public class HomoGroupInterfacesFinder
	{
		public static string modifyType = "";

		public HomoGroupInterfacesFinder()
		{
            AppSettings.LoadCrystMethods();
			//
			// TODO: Add constructor logic here
			//
		}
		
		#region Detect Interfaces in a group
		/// <summary>
		/// create tables for the specific database
		/// </summary>
		/// <param name="dbName"></param>
        public void DetectHomoGroupInterfaces(int[] updateGroups)
        {
            if (!File.Exists(ProtCidSettings.dirSettings.protcidDbPath))
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Database not exist. Please check the path, try it again.");
                ProtCidSettings.progressInfo.threadAborted = true;
                return;
            }

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            if (modifyType == "build")
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Create database tables from schema file.");
                try
                {
         //          InitializeFamilyTablesInDb();
                  // InitializeResiduesContactsTables();
                  // dbCreator.CreateTablesFromFile (ProtCidSettings.applicationStartPath + "\\dbSchema\\" + ProtCidSettings.dataType + "InterfaceDbSchema.txt");
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                    ProtCidSettings.progressInfo.threadAborted = true;
                    return;
                }
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done.");
            }

            // build X-ray crystal and detect chain contacts
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Detect interfaces in a crystal from XML coordinate files.");
            GroupInterfacesRetriever homoGroupInterfaces = new GroupInterfacesRetriever();

            Dictionary<int, Dictionary<string, List<string>>> groupEntryHash = GetGroupEntries(updateGroups);

            if (groupEntryHash == null || groupEntryHash.Count == 0)
            {
                return;
            }
            homoGroupInterfaces.DetectGroupInterfacesCompDb(groupEntryHash);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done.");

            // finish
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Database created. ");
        }

		/// <summary>
		/// update interface tables
		/// </summary>
		/// <param name="dbName"></param>
		public void UpdateHomoGroupInterfaces (Dictionary<int, string[]> updateGroupHash)
		{
			if (! File.Exists (ProtCidSettings.dirSettings.protcidDbPath))
			{
				ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Database not exist. Please check the path, try it again.");
				ProtCidSettings.progressInfo.threadAborted = true;
				return ;
			}

			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
					
			// build X-ray crystal and detect chain contacts
			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Update interfaces in a crystal from XML coordinate files.");
			GroupInterfacesRetriever homoGroupInterfaces = new GroupInterfacesRetriever ();

			List<int> updateGroupList = new List<int> (updateGroupHash.Keys);
            Dictionary<int, Dictionary<string, List<string>>> groupEntryHash = GetGroupEntries(updateGroupList.ToArray());					
			if (groupEntryHash == null || groupEntryHash.Count == 0)
			{
				return;
			}
			homoGroupInterfaces.UpdateGroupInterfacesCompDb (groupEntryHash, updateGroupHash);

			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Done.");

			// finish
			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Database created. ");
		}

        /// <summary>
        /// 
        /// </summary>
        public void RemoveObsoleteData()
        {
            StreamReader lsFileReader = new StreamReader(@"D:\DbProjectData\PDB\newls-pdb.txt");
            string line = "";
            List<string> entryList = new List<string> ();
            while ((line = lsFileReader.ReadLine()) != null)
            {
                entryList.Add(line.Substring (0, 4));
            }
            lsFileReader.Close();

            DbUpdate dbDelete = new DbUpdate();
            string deleteString = "";
            string crystInterfaceFileDir = @"D:\DbProjectData\InterfaceFiles_update\cryst";
            string hashFolder = "";
            foreach (string pdbId in entryList)
            {
                deleteString = string.Format("Delete From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
                dbDelete.Delete(ProtCidSettings.protcidDbConnection, deleteString);

                deleteString = string.Format("Delete From EntryInterfaceComp Where PdbID = '{0}';", pdbId);
                dbDelete.Delete(ProtCidSettings.protcidDbConnection, deleteString);

                deleteString = string.Format("Delete From DifEntryInterfaceComp " +
                    "Where PdbID1 = '{0}' OR PdbID2 = '{0}';", pdbId);
                dbDelete.Delete(ProtCidSettings.protcidDbConnection, deleteString);

                deleteString = string.Format("Delete From FATCATALignments " +
                    " Where QueryEntry = '{0}' OR HitEntry = '{0}';", pdbId);
                dbDelete.Delete(ProtCidSettings.alignmentDbConnection, deleteString);

                hashFolder = Path.Combine(crystInterfaceFileDir, pdbId.Substring (1, 2));
                string[] crystInterfaceFiles = Directory.GetFiles(hashFolder, pdbId + "*");
                foreach (string crystInterfaceFile in crystInterfaceFiles)
                {
                    File.Delete(crystInterfaceFile);
                }
            }
        }
		#endregion

		#region file list
		/// <summary>
		/// get entries in a group and different space groups
		/// from database
		/// </summary>
		/// <returns></returns>
		private Dictionary<int, Dictionary<string, List<string>>>  GetGroupEntries (int[] updateGroups)
		{
			DbQuery dbQuery = new DbQuery ();
	
			string groupString = "";
			string repEntryString = "";
            DataTable homoSeqInfoTable = null;
            DataTable repEntryTable = null;
			if (updateGroups != null)
			{
                foreach (int updateGroup in updateGroups)
                {
                    groupString = string.Format("Select * From {0}" +
                        " Where GroupSeqID = {1} Order by GroupSeqID, PdbID;",
                        GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo], updateGroup);
                    DataTable groupTable = ProtCidSettings.protcidQuery.Query( groupString);
                    if (homoSeqInfoTable == null)
                    {
                        homoSeqInfoTable = groupTable.Copy();
                    }
                    else
                    {
                        foreach (DataRow dataRow in groupTable.Rows)
                        {
                            DataRow newRow = homoSeqInfoTable.NewRow();
                            newRow.ItemArray = dataRow.ItemArray;
                            homoSeqInfoTable.Rows.Add(newRow);
                        }
                    }
                    repEntryString = string.Format("Select * From {0} Where GroupSeqID = {1} " +
                        " ORDER BY GroupSeqID, PdbID1, PdbID2;",
                        GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoRepEntryAlign], updateGroup);
                    DataTable repHomoTable = ProtCidSettings.protcidQuery.Query( repEntryString);
                    if (repEntryTable == null)
                    {
                        repEntryTable = repHomoTable.Copy();
                    }
                    else
                    {
                        foreach (DataRow homoRow in repHomoTable.Rows)
                        {
                            DataRow newRow = repEntryTable.NewRow();
                            newRow.ItemArray = homoRow.ItemArray;
                            repEntryTable.Rows.Add(newRow);
                        }
                    }

                }
			}
			else
			{
				groupString = string.Format ("Select * From {0} Order by GroupSeqID, PdbID;", 
					GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo]);
				repEntryString = string.Format ("Select * From {0} order by GroupSeqID, PdbID1, PdbID2;", 
					GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoRepEntryAlign]);
                homoSeqInfoTable = ProtCidSettings.protcidQuery.Query( groupString);
                repEntryTable = ProtCidSettings.protcidQuery.Query( repEntryString);
			}

            Dictionary<int, Dictionary<string, List<string>>> crystFileHash = GetCrystFileHash(homoSeqInfoTable, repEntryTable);

            return crystFileHash;
		}

		/// <summary>
		/// get entries in a group and different space groups
		/// from database
		/// </summary>
		/// <returns></returns>
        private Dictionary<int, Dictionary<string, List<string>>> GetGroupEntries(int startGroupId, int endGroupId)
		{
			DbQuery dbQuery = new DbQuery ();	

			string groupString = string.Format ("Select * From {0}" + 
				" Where GroupSeqID >= {1} AND GroupSeqID <= {2} Order by GroupSeqID, PdbID;", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo], startGroupId, endGroupId);
            DataTable homoSeqInfoTable = ProtCidSettings.protcidQuery.Query( groupString);

			string repEntryString = string.Format ("Select * From {0} Where GroupSeqID >= {1} AND GroupSeqID <= {2}" + 
				" ORDER BY GroupSeqID, PdbID1, PdbID2;", 
				GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoRepEntryAlign], startGroupId, endGroupId);
            DataTable repEntryTable = ProtCidSettings.protcidQuery.Query( repEntryString);

            Dictionary<int, Dictionary<string, List<string>>> crystFileHash = GetCrystFileHash(homoSeqInfoTable, repEntryTable);

			return crystFileHash;
		}

        /// <summary>
        /// get entries in a group and different space groups
        /// from database
        /// </summary>
        /// <returns></returns>
        private Dictionary<int, Dictionary<string, List<string>>> GetGroupEntries(int startGroupId)
        {
            DbQuery dbQuery = new DbQuery();

            string groupString = string.Format("Select * From {0}" +
                " Where GroupSeqID >= {1} Order by GroupSeqID, PdbID;",
                GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoSeqInfo], startGroupId);
            DataTable homoSeqInfoTable = ProtCidSettings.protcidQuery.Query( groupString);

            string repEntryString = string.Format("Select * From {0} Where GroupSeqID >= {1}" +
                " ORDER BY GroupSeqID, PdbID1, PdbID2;",
                GroupDbTableNames.dbTableNames[GroupDbTableNames.HomoRepEntryAlign], startGroupId);
            DataTable repEntryTable = ProtCidSettings.protcidQuery.Query( repEntryString);

            Dictionary<int, Dictionary<string, List<string>>> crystFileHash = GetCrystFileHash(homoSeqInfoTable, repEntryTable);

            return crystFileHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="homoSeqInfoTable"></param>
        /// <param name="repEntryTable"></param>
        /// <returns></returns>
        private Dictionary<int, Dictionary<string, List<string>>> GetCrystFileHash(DataTable homoSeqInfoTable, DataTable repEntryTable)
        {
            Dictionary<int, Dictionary<string, List<string>>> crystFileHash = new Dictionary<int, Dictionary<string, List<string>>>();
            string repPdbId = "";
            string spaceGroup = "";
            string asu = "";
            int groupId = 0;
            foreach (DataRow dRow in homoSeqInfoTable.Rows)
            {
                groupId = Convert.ToInt32(dRow["GroupSeqId"].ToString());

                repPdbId = dRow["PdbID"].ToString();
                spaceGroup = dRow["SpaceGroup"].ToString().Trim();
                asu = dRow["ASU"].ToString().Trim();
                DataRow[] homoEntryRows = repEntryTable.Select
                    (string.Format("GroupSeqID = '{0}' and PdbID1 = '{1}'", groupId, repPdbId));

                if (!crystFileHash.ContainsKey(groupId))
                {
                    Dictionary<string, List<string>> sgPdbListHash = new Dictionary<string,List<string>> ();
                    List<string> pdbList = new List<string> ();
                    pdbList.Add(repPdbId);
                    foreach (DataRow homoEntryRow in homoEntryRows)
                    {
                        if (!pdbList.Contains(homoEntryRow["PdbID2"].ToString()))
                        {
                            pdbList.Add(homoEntryRow["PdbID2"].ToString());
                        }
                    }
                    sgPdbListHash.Add(spaceGroup + "_" + asu, pdbList);
                    crystFileHash.Add(groupId, sgPdbListHash);
                }
                else
                {
                    Dictionary<string, List<string>> sgPdbListHash = crystFileHash[groupId];
                    // it should be a new space group
                    // since space group_ASU in a homogroup is unique
                    List<string> pdbList = new List<string> ();
                    pdbList.Add(repPdbId);
                    foreach (DataRow homoEntryRow in homoEntryRows)
                    {
                        if (!pdbList.Contains(homoEntryRow["PdbID2"].ToString()))
                        {
                            pdbList.Add(homoEntryRow["PdbID2"].ToString());
                        }
                    }
                    sgPdbListHash.Add(spaceGroup + "_" + asu, pdbList);
                    crystFileHash[groupId] = sgPdbListHash;
                }
            }
            return crystFileHash;
        }
		#endregion

		#region initialize db tables
		private void InitializeFamilyTablesInDb ()
		{
			DbCreator dbCreator = new DbCreator ();

			string tableName = ProtCidSettings.dataType + "SgInterfaces";
			string createTableString = string.Format ("CREATE TABLE {0} (" + 
				" GroupSeqID INTEGER NOT NULL, " + 
				" SpaceGroup VARCHAR(30) NOT NULL, " +
                " ASU BLOB Sub_Type TEXT NOT NULL, " + 
				" PDBID CHAR(4) NOT NULL, " + 
				" InterfaceID INTEGER NOT NULL, " + 
				" NumOfSG INTEGER NOT NULL, " + 
				" NumOfSG_Identity INTEGER NOT NULL, " + 
				" SurfaceArea FLOAT );", tableName);
            dbCreator.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
			string createIdxString = string.Format ("CREATE INDEX {0}_Idx1 " + 
				" ON {0} (GroupSeqID, PdbID, InterfaceID);", tableName);
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIdxString, tableName);
		}

        /// <summary>
        /// 
        /// </summary>
        private void InitializeResiduesContactsTables()
        {
            DbCreator dbCreator = new DbCreator ();

            string tableName = "SgInterfaceResidues";
            string createTableString = "CREATE TABLE " + tableName + " ( " +
                                "PDBID        CHAR(4) NOT NULL, " +
                                "INTERFACEID  INTEGER NOT NULL, " +
                                "RESIDUE1     CHAR(3) NOT NULL, " +
                                "SEQID1       CHAR(5) NOT NULL,  " +
                                "RESIDUE2     CHAR(3) NOT NULL, " +
                                "SEQID2       CHAR(5) NOT NULL, " +
                                "DISTANCE     FLOAT NOT NULL);";
            dbCreator.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            string createIdxString = string.Format("CREATE INDEX {0}_Idx1 ON {0} (PdbID, InterfaceID);", tableName);
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIdxString, tableName);


            tableName = "SgInterfaceContacts";
            createTableString = "CREATE TABLE " + tableName + " ( " +
                                "PDBID        CHAR(4) NOT NULL, " +
                                "INTERFACEID  INTEGER NOT NULL, " +
                                "RESIDUE1     CHAR(3) NOT NULL, " +
                                "SEQID1       CHAR(5) NOT NULL,  " +
                                "RESIDUE2     CHAR(3) NOT NULL, " +
                                "SEQID2       CHAR(5) NOT NULL, " +
                                "DISTANCE     FLOAT NOT NULL);";
            dbCreator.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
            createIdxString = string.Format("CREATE INDEX {0}_Idx1 ON {0} (PdbID, InterfaceID);", tableName);
            dbCreator.CreateIndex(ProtCidSettings.protcidDbConnection, createIdxString, tableName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
		private void DeleteGroupData (int groupId)
		{
			string tableName = ProtCidSettings.dataType + "SgInterfaces";
			DbQuery dbQuery = new DbQuery ();
			string deleteString = string.Format ("Delete From {0} WHERE GroupSeqID = {1};", 
				tableName, groupId);
            ProtCidSettings.protcidQuery.Query( deleteString);
		}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupIds"></param>
        private void DeleteGroupData(int[] groupIds)
        {
            foreach (int groupId in groupIds)
            {
                DeleteGroupData(groupId);
            }
        }
		#endregion

        #region debug
        public void InsertCrystInterfaceDataDueInsertBug()
        {
            DbInsert dbInsert = new DbInsert();
            DbQuery dbQuery = new DbQuery ();
            StreamWriter logWriter = new StreamWriter("MissCrystEntryInterfaceLog.txt");
            string queryString = "Select First 1 * From CrystEntryInterfaces;";
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            interfaceTable.Clear();
            interfaceTable.TableName = "CrystEntryInterfaces";
            StreamReader dataReader = new StreamReader("dbInsertErrorLog.txt");
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("EXECUTE BLOCK AS BEGIN") > -1)
                {
                    line = line.Remove(0, "EXECUTE BLOCK AS BEGIN".Length + 1);
                    line = line.Replace("END", "");
                    string[] insertFields = line.Split(';');
                    foreach (string insertString in insertFields)
                    {
                        try
                        {
                            dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, insertString);
                        }
                        catch (Exception ex)
                        {
                            logWriter.WriteLine(ex.Message);
                            logWriter.WriteLine(insertString);
                            logWriter.Flush();
                        }
                    }
                }
            }
            dataReader.Close();
            logWriter.Close();
        }
        #endregion

    }
}
