using System;
using System.IO;
using System.Net;
using System.Data;
using System.Collections;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;

namespace DataCollectorLib.Pisces
{
	/// <summary>
	/// Collecting data from MySQL database
	/// </summary>
	public class PiscesDataCollector
    {
        #region member variables
   //     private string dbPath = @"C:\ProgramData\MySQL\MySQL Server 5.7\Data\alignments";
        private string remoteMysqlServer = "10.40.16.45";
   //     private string uname = "\'qifang\'@\'10.40.16.29\'";
        private string uname = "qifang";
        private string psw = "mysqlqxu";
		private string dbName = "alignments";
        public string srcPath = "http://dunbrack.fccc.edu//Guoli/culledpdb/";
        private DbConnect localMysqlDbConnect = new DbConnect ();
        private DbConnect remotePiscesDbConnect = new DbConnect ();
        private DbConnect protcidDbConnect = new DbConnect();
        private DbQuery dbQuery = new DbQuery();
        private DbInsert dbInsert = new DbInsert();
        private DbUpdate dbUpdate = new DbUpdate();
        private Hashtable monthHash = new Hashtable();
        private string centryStrCutoff = "71";
        private string[] preCrcCodesInTable = null;
        private Hashtable methodHash = new Hashtable ();
        #endregion

        #region initialize
        public PiscesDataCollector()
		{
            InitializeDataHashes();

            InitializeDbConnect();

            preCrcCodesInTable = GetCrcCodesInDb();
		}

        /// <summary>
        /// 
        /// </summary>
        private void InitializeDataHashes ()
        {
            monthHash.Add("JAN", 1);
            monthHash.Add("FEB", 2);
            monthHash.Add("MAR", 3);
            monthHash.Add("APR", 4);
            monthHash.Add("MAY", 5);
            monthHash.Add("JUN", 6);
            monthHash.Add("JUL", 7);
            monthHash.Add("AUG", 8);
            monthHash.Add("SEP", 9);
            monthHash.Add("OCT", 10);
            monthHash.Add("NOV", 11);
            monthHash.Add("DEC", 12);

            methodHash.Add("ELECTRON CRYSTALLOGRAPHY", "ELEC");
            methodHash.Add("ELECTRON MICROSCOPY", "EM");
            methodHash.Add("FIBER DIFFRACTION", "FIBER");
            methodHash.Add("FLUORESCENCE TRANSFER", "FTIR");
            methodHash.Add("INFRARED SPECTROSCOPY", "FTIR");
            methodHash.Add("NEUTRON DIFFRACTION", "NEUTRON");
            methodHash.Add("POWDER DIFFRACTION", "POWDER");
            methodHash.Add("SOLID-STATE NMR", "NMR");
            methodHash.Add("SOLUTION NMR", "NMR");
            methodHash.Add("SOLUTION SCATTERING", "SCATTER");
            methodHash.Add("X-RAY DIFFRACTION", "XRAY");
        }

        /// <summary>
        /// 
        /// </summary>
        private void InitializeDbConnect ()
        {
            string connectString = "Driver={MySQL ODBC 5.3 Unicode Driver}; Server=localhost;Database=" + dbName + ";UID=qifang;PWD=DunbrackR462*;";
            localMysqlDbConnect.ConnectString = connectString;
            localMysqlDbConnect.ConnectToDatabase();

    //        connectString = string.Format ("Driver={MySQL ODBC 5.3 Unicode Driver}; Server={0};Database=bioinformatics;UID={1};PWD={2};", remoteMysqlServer, uname, psw); 
            // always got input format error, so hard code the server, uid, psw
 /*           connectString = "Driver={MySQL ODBC 5.3 Unicode Driver}; Server=10.40.16.45;Database=bioinformatics;UID=qifang;PWD=mysqlqxu;";
            remotePiscesDbConnect.ConnectString = connectString;
            remotePiscesDbConnect.ConnectToDatabase();
*/
            ProtCidSettings.LoadDirSettings();
            protcidDbConnect.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" + ProtCidSettings.dirSettings.pdbfamDbPath;
            protcidDbConnect.ConnectToDatabase();
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void SynchronizeHHalignmentsDb ()
        {
            string newLsFile = Path.Combine (ProtCidSettings.dirSettings.xmlPath, "xml\\newls-pdb.txt");
            string obsoleteLsFile = Path.Combine(ProtCidSettings.dirSettings.xmlPath, "updateData\\obsolete.dat");
            string logFile = Path.Combine(ProtCidSettings.dirSettings.xmlPath, "updateData\\hhmysqlupdatedatetime.log");
            DateTime lastUpdateDt = GetLastUpdateDateTime(logFile);
            string[] updateEntries = ReadNewPdbEntries(newLsFile);
            string[] obsoleteEntries = GetObsoleteEntries(lastUpdateDt, obsoleteLsFile);

            StreamWriter logWriter = new StreamWriter(logFile, true);
            logWriter.WriteLine("Update date: " + DateTime.Today.ToShortDateString ());
            logWriter.WriteLine("#update entries = " + updateEntries.Length);
            logWriter.WriteLine("#obsolete entries = " + obsoleteEntries.Length);

            string[] needDbDelEntries = new string[updateEntries.Length + obsoleteEntries.Length];
            Array.Copy(updateEntries, 0, needDbDelEntries, 0, updateEntries.Length);
            Array.Copy(obsoleteEntries, 0, needDbDelEntries, updateEntries.Length, obsoleteEntries.Length);
            string[] crcWithDel = GetCrcCodesWithChange(needDbDelEntries);
            logWriter.WriteLine("#crc with deletions  = " + crcWithDel.Length);

            string[] updateCrcCodes = ImportNewEntriesCrcCodes(updateEntries);
            logWriter.WriteLine("#update crc = " + updateCrcCodes.Length);

            string[] obsoleteCrcCodes = GetObsoleteCrcCodes(crcWithDel);
            logWriter.WriteLine("#obsolete crc = " + obsoleteCrcCodes.Length);

            string[] newCrcCodes = GetNewCrcCodes(updateCrcCodes, preCrcCodesInTable);
            logWriter.WriteLine("#new crc = " + newCrcCodes.Length);
            
            // this happens when the protcid pdb data is not consistent with the pdb data in pisces mysql database
            // in order to avoid the inconsistency, update protcid pdb and mysql pdb in the same week
            // try best to avoid te inconsistency
            string[] addedCrcCodes = AddEntryCrcCodes(updateEntries);
            ArrayList newAddedCrcList = new ArrayList(newCrcCodes);
            ArrayList newObsoleteCrcList = new ArrayList(obsoleteCrcCodes);
            foreach (string addedCrc in addedCrcCodes)
            {
                if (Array.IndexOf (newCrcCodes, addedCrc) < 0)
                {
                    newAddedCrcList.Add(addedCrc);
                }
                if (Array.IndexOf (obsoleteCrcCodes, addedCrc) > 0)
                {
                    newObsoleteCrcList.Remove(addedCrc);
                }
            }

            newCrcCodes = new string[newAddedCrcList.Count];
            newAddedCrcList.CopyTo(newCrcCodes);
            obsoleteCrcCodes = new string[newObsoleteCrcList.Count];
            newObsoleteCrcList.CopyTo(obsoleteCrcCodes);

            ImportHHalignments(newCrcCodes, obsoleteCrcCodes);

            logWriter.WriteLine("Update successfully!");
            logWriter.Close();

            remotePiscesDbConnect.DisconnectFromDatabase();
        }

        #region hhglobal alignments table
        /// <summary>
        /// 
        /// </summary>
        /// <param name="newCrcCodes"></param>
        /// <param name="obsoleteCrcCodes"></param>
        public void ImportHHalignments (string[] newCrcCodes, string[] obsoleteCrcCodes)
        {
            DeleteHHalignments(obsoleteCrcCodes);
            ImportHHalignments(newCrcCodes);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crcCodes"></param>
        public void  ImportHHalignments (string[] crcCodes)
        {
            string queryString = "";
            foreach (string crc in crcCodes)
            {
                queryString = string.Format("Select * From HHGlobal Where query = '{0}' OR hit = '{0}';", crc);
                DataTable hhalignmentTable = dbQuery.Query(remotePiscesDbConnect, queryString);
                hhalignmentTable.TableName = "hhglobal";
                dbInsert.InsertDataIntoDBtables(localMysqlDbConnect, hhalignmentTable);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obsoleteCrcCodes"></param>
        public void DeleteHHalignments (string[] obsoleteCrcCodes)
        {
            string deleteString = "";
            foreach (string crc in obsoleteCrcCodes)
            {
                deleteString = string.Format("Delete From hhglobal Where crc1 = '{0}' OR crc2 = '{0}';", crc);
                dbUpdate.Delete(localMysqlDbConnect, deleteString);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateCrcCodes"></param>
        /// <param name="preCrcCodesInTable"></param>
        /// <returns></returns>
        private string[] GetNewCrcCodes (string[] updateCrcCodes, string[] preCrcCodesInTable)
        {
            ArrayList newCrcCodeList = new ArrayList();
            StreamWriter newCrcWriter = new StreamWriter("newcrccodes.txt");
            foreach (string crc in updateCrcCodes)
            {
                if (Array.IndexOf (preCrcCodesInTable, crc) <  0)
                {
                    newCrcCodeList.Add(crc);
                    newCrcWriter.WriteLine(crc);
                }
            }
            newCrcWriter.Close();
            string[] newCrcs = new string[newCrcCodeList.Count];
            newCrcCodeList.CopyTo(newCrcs);
            return newCrcs;
        }
        #endregion

        #region pdbcrcmap table
        public string[] GetCrcCodesInDb ()
        {
            string queryString = "Select Distinct crc From pdbcrcmap;";
            DataTable crcTable = dbQuery.Query(localMysqlDbConnect, queryString);
            string[] crcCodes = new string[crcTable.Rows.Count];
            for (int i = 0; i < crcTable.Rows.Count; i ++)
            {
                crcCodes[i] = crcTable.Rows[i]["crc"].ToString();
            }
            return crcCodes;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        private string[] ImportNewEntriesCrcCodes (string[] updateEntries)
        {
            int numOfEntries = 30;
            string queryString = "Select * From PDBCRCMap;";
            DataTable piscesPdbCrcTable = dbQuery.Query(remotePiscesDbConnect, queryString);
            ArrayList updateCrcList = new ArrayList();
            for (int i = 0; i < updateEntries.Length; i += numOfEntries)
            {
                if (i + numOfEntries >= updateEntries.Length)
                {
                    numOfEntries = updateEntries.Length - i;
                }
                string[] queryEntries = new string[numOfEntries];
                Array.Copy(updateEntries, i, queryEntries, 0, numOfEntries);
                queryString = string.Format("Select * From PDBCRCMap Where pdb IN {0};", ParseHelper.FormatSqlListString (queryEntries));
                DataTable pdbCrcTable =  dbQuery.Query(remotePiscesDbConnect, queryString);
                pdbCrcTable.TableName = "pdbcrcmap";
                dbInsert.BatchInsertDataIntoDBtables (localMysqlDbConnect, pdbCrcTable);

                string[] crcCodesInTable = GetCrcCodesInTable(pdbCrcTable);
                foreach (string crc in crcCodesInTable)
                {
                    if (! updateCrcList.Contains (crc))
                    {
                        updateCrcList.Add(crc);
                    }
                }
            }
            string[] updateCrcCodes = new string[updateCrcList.Count];
            updateCrcList.CopyTo(updateCrcCodes);
            return updateCrcCodes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crcTable"></param>
        /// <returns></returns>
        private string[] GetCrcCodesInTable (DataTable crcTable)
        {
            ArrayList crcList = new ArrayList();
            string crc = "";
            foreach (DataRow mapRow in crcTable.Rows)
            {
                crc = mapRow["crc"].ToString();
                if (! crcList.Contains (crc))
                {
                    crcList.Add(crc);
                }
            }
            string[] crcCodes = new string[crcList.Count];
            crcList.CopyTo(crcCodes);
            return crcCodes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        private void DeletePdbCrcRecords (string[] delEntries)
        {
            string deleteString = "";
            DbUpdate dbDelete = new DbUpdate();
            foreach (string pdbId in delEntries)
            {
                deleteString = string.Format("Delete From pdbcrcmap Where pdb = '{0}';", pdbId);
                dbDelete.Delete(localMysqlDbConnect, deleteString);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="delEntries"></param>
        /// <returns></returns>
        private string[] GetCrcCodesWithChange (string[] delEntries)
        {
            string queryString = "";
            ArrayList crcWithDelList = new ArrayList();
            string crc = "";
            StreamWriter dataWriter = new StreamWriter("CrcWithDeleltions.txt");
            foreach (string pdbId in delEntries)
            {
                queryString = string.Format("Select Distinct crc From pdbcrcmap where pdb = '{0}';", pdbId);
                DataTable crcTable = dbQuery.Query(localMysqlDbConnect, queryString);
                foreach (DataRow crcRow in crcTable.Rows)
                {
                    crc = crcRow["crc"].ToString();
                    if (! crcWithDelList.Contains (crc))
                    {
                        crcWithDelList.Add(crc);
                        dataWriter.WriteLine(crc);
                    }
                }
            }
            dataWriter.Close();
            string[] crcCodesWithChange = new string[crcWithDelList.Count];
            crcWithDelList.CopyTo(crcCodesWithChange);
            return crcCodesWithChange;
        }

        /// <summary>
        /// crc codes were in the previous table, but not any more
        /// </summary>
        /// <param name="crcWithChanges"></param>
        /// <returns></returns>
        private string[] GetObsoleteCrcCodes (string[] crcWithChanges)
        {
            ArrayList obsoleteCrcList = new ArrayList();
            string queryString = "";
            StreamWriter dataWriter = new StreamWriter("obsoletecrccodes.txt");
            foreach (string crc in crcWithChanges)
            {
                queryString = string.Format("Select pdb From pdbcrcmap where crc = '{0}';", crc);
                DataTable chainTable = dbQuery.Query(localMysqlDbConnect, queryString);
                if (chainTable.Rows.Count == 0)
                {
                    obsoleteCrcList.Add(crc);
                    dataWriter.WriteLine(crc);
                }
            }
            dataWriter.Close();
            string[] obsoleteCrcs = new string[obsoleteCrcList.Count];
            obsoleteCrcList.CopyTo(obsoleteCrcs);
            return obsoleteCrcs;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] ReadNewPdbEntries (string newPdbLsFile)
        {         
            ArrayList entryList = new ArrayList();
            string line = "";
            StreamReader dataReader = new StreamReader(newPdbLsFile);
            string pdbId = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                pdbId = line.Substring(0, 4);
                if (IsProtEntry(pdbId))
                {
                    if (!entryList.Contains(pdbId))
                    {
                        entryList.Add(pdbId);
                    }
                }
            }
            dataReader.Close();
            string[] newEntries = new string[entryList.Count];
            entryList.CopyTo(newEntries);
            return newEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsProtEntry (string pdbId)
        {
            string queryString = string.Format("Select AsymID From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable protChainTable = dbQuery.Query(protcidDbConnect, queryString);
            if (protChainTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lastUpdatDate"></param>
        /// <param name="obsoleteLsFile"></param>
        /// <returns></returns>
        private string[] GetObsoleteEntries (DateTime lastUpdatDate, string obsoleteLsFile)
        {
            StreamReader dataReader = new StreamReader(obsoleteLsFile);
            string line = "";
            int year = 0;
            int month = 0;
            int date = 0;
            ArrayList obsoleteEntryList = new ArrayList();
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = ParseHelper.SplitPlus(line, ' ');
                string[] dateFields = fields[1].Split('-');
                if (string.Compare (dateFields[2], centryStrCutoff) < 0)
                {
                    year = Convert.ToInt32("20" + dateFields[2]);
                }
                else
                {
                    year = Convert.ToInt32("19" + dateFields[2]);
                }
                month = (int)monthHash[dateFields[1]];
                date = Convert.ToInt32(dateFields[0]);
                DateTime entryDt = new DateTime(year, month, date);
                if (DateTime.Compare (entryDt, lastUpdatDate) >= 0)
                {
                    obsoleteEntryList.Add(fields[2].ToCharArray ());

                }
            }
            dataReader.Close();
            string[] obsoleteEntries = new string[obsoleteEntryList.Count];
            obsoleteEntryList.CopyTo(obsoleteEntries);
            return obsoleteEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateDateTimeLogFile"></param>
        /// <returns></returns>
        private DateTime GetLastUpdateDateTime (string updateDateTimeLogFile)
        {
            string line = "";
            StreamReader dataReader = new StreamReader(updateDateTimeLogFile);
            int year = 0;
            int month = 0;
            int day = 0;
            string dtString = "";
            DateTime lastUpdateDt = new DateTime ();
            while ((line = dataReader.ReadLine ()) != null)
            {
                 if (line.IndexOf ("Update date: ") > -1)
                 {
                     dtString = line.Substring("Update date: ".Length, line.Length - "Update date: ".Length);
                     string[] dtFields = dtString.Split('/');
                     year = Convert.ToInt32(dtFields[2]);
                     month = Convert.ToInt32(dtFields[0]);
                     day = Convert.ToInt32(dtFields[1]);
                     lastUpdateDt = new DateTime(year, month, day);
                 }
            }
            dataReader.Close();
            return lastUpdateDt;
        }
        #endregion

        #region add pdbs with no crc in remote server
        // the function in the region is used when the protcid pdb is not consistent with the pdb data in the remote mysql database
        private SeqCrc seqCrc = new SeqCrc();      
        public string[] AddEntryCrcCodes (string[] updateEntries)
        {
            DataTable pdbcrcTable = InitializeMapTable();
            ArrayList addedCrcList = new ArrayList();
            foreach (string pdbId in updateEntries)
            {
                if (DoesEntryHaveCrc (pdbId))
                {
                    continue;
                }
                SetPdbCrcMap(pdbId, pdbcrcTable);
                pdbcrcTable.TableName = "pdbcrcmap";
                dbInsert.InsertDataIntoDBtables(localMysqlDbConnect, pdbcrcTable);
                pdbcrcTable.Clear();

                string[] crcCodes = GetCrcCodesInTable(pdbcrcTable);
                foreach (string crc in crcCodes)
                {
                    if (! addedCrcList.Contains (crc))
                    {
                        addedCrcList.Add(crc);
                    }
                }
            }
            protcidDbConnect.DisconnectFromDatabase();
            localMysqlDbConnect.DisconnectFromDatabase();
            string[] addedCrcCodes = new string[addedCrcList.Count];
            addedCrcList.CopyTo(addedCrcCodes);
            return addedCrcCodes;
        }

        /// <summary>
        /// 
        /// </summary>
        public void AddMissingEntryCrcCodes()
        {
            protcidDbConnect.ConnectToDatabase();
            string[] missingEntries = GetMissingCrcEntries();
            DataTable pdbcrcTable = InitializeMapTable();
            foreach (string pdbId in missingEntries)
            {
                if (DoesEntryHaveCrc(pdbId))
                {
                    continue;
                }
                SetPdbCrcMap(pdbId, pdbcrcTable);
                pdbcrcTable.TableName = "pdbcrcmap";
                dbInsert.InsertDataIntoDBtables(localMysqlDbConnect, pdbcrcTable);
                pdbcrcTable.Clear();
            }
     //       protcidDbConnect.DisconnectFromDatabase();
    //        localMysqlDbConnect.DisconnectFromDatabase();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetMissingCrcEntries ()
        {
            string missCrcEntryFile = "NoCrcEntries.txt";
            ArrayList noCrcEntryList = new ArrayList();
            if (File.Exists(missCrcEntryFile))
            {
                StreamReader dataReader = new StreamReader(missCrcEntryFile);
                string line = "";
                while ((line = dataReader.ReadLine ()) != null)
                {
                    noCrcEntryList.Add(line);
                }
                dataReader.Close();
            }
            else
            {
                StreamWriter dataWriter = new StreamWriter(missCrcEntryFile);
                string queryString = "Select Distinct PdbID From AsymUnit where PolymerType = 'polypeptide';";
                DataTable protEntryTable = dbQuery.Query(protcidDbConnect, queryString);
                string pdbId = "";
                foreach (DataRow entryRow in protEntryTable.Rows)
                {
                    pdbId = entryRow["PdbID"].ToString();
                    if (DoesEntryHaveCrc(pdbId))
                    {
                        continue;
                    }
                    noCrcEntryList.Add(pdbId);
                    dataWriter.WriteLine(pdbId);
                }
                dataWriter.Close();
            }
            string[] noCrcEntries = new string[noCrcEntryList.Count];
            noCrcEntryList.CopyTo(noCrcEntries);
            return noCrcEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable InitializeMapTable ()
        {
            string queryString = "Select * From pdbcrcmap limit 1;";
            DataTable pdbcrcTable = dbQuery.Query(localMysqlDbConnect, queryString);
            pdbcrcTable.Clear();
            pdbcrcTable.TableName = "pdbcrcmap";
            return pdbcrcTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool DoesEntryHaveCrc (string pdbId)
        {
            string queryString = string.Format ("Select crc From pdbcrcmap where pdb = '{0}';", pdbId);
            DataTable crcTable = dbQuery.Query(localMysqlDbConnect, queryString);
            if (crcTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pdbcrcmapTable"></param>
        /// <returns></returns>
        public void SetPdbCrcMap (string pdbId, DataTable pdbcrcmapTable )
        {
            string queryString = string.Format("Select AuthorChain, EntityID, AsymID, Name, Species, Sequence, SequenceInCoord From AsymUnit" +
                " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable entrySeqTable = dbQuery.Query(protcidDbConnect, queryString);
            Hashtable entityCrcHash = new Hashtable();
            int entityId = 0;
            string sequence = "";
            string sequenceInCoord = "";
            int numOfMissingCoord = 0;
            string crc64 = "";
            ArrayList crcRowList = new ArrayList();
            string[] entryInfo = GetCrystalInfo (pdbId);
            foreach (DataRow chainRow in entrySeqTable.Rows)
            {
                entityId = Convert.ToInt32(chainRow["EntityID"].ToString());
                sequenceInCoord = chainRow["SequenceInCoord"].ToString();
                numOfMissingCoord = GetMissingCoordNumber(sequenceInCoord);
                if (entityCrcHash.ContainsKey(entityId))
                {
                    crc64 = (string)entityCrcHash[entityId];
                }
                else
                {
                    sequence = chainRow["Sequence"].ToString();
                    crc64 = seqCrc.GetCrc64(sequence);
                    entityCrcHash.Add(entityId, crc64);
                }
                DataRow dataRow = pdbcrcmapTable.NewRow();
                dataRow["pdb"] = pdbId;
                dataRow["chain_id"] = chainRow["AuthorChain"];
                dataRow["entity_id"] = chainRow["EntityID"];
                dataRow["asym_id"] = chainRow["AsymID"];
                dataRow["length"] = chainRow["Sequence"].ToString().Length;
                dataRow["method"] = entryInfo[0];
                dataRow["resolution"] = entryInfo[1];
                dataRow["rfactor"] = entryInfo[2];
                dataRow["freerf"] = entryInfo[3];
                dataRow["caonly"] = 0;   
                dataRow["missing"] = numOfMissingCoord;
                dataRow["sifts_source"] = "-";
                dataRow["source"] = "-";
                dataRow["sifts_species"] = "-";
                dataRow["species"] = chainRow["Species"];
                dataRow["swiss"] = "-";
                dataRow["name"] = chainRow["name"];
                dataRow["isentrep"] = 0;
                dataRow["isrep"] = 0;
                pdbcrcmapTable.Rows.Add(dataRow);
             } 
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="sequenceInCoord"></param>
        /// <returns></returns>
        private int GetMissingCoordNumber (string sequenceInCoord)
        {
            int numOfMissingCoord = 0;
            foreach (char ch in sequenceInCoord)
            {
                if (ch == '-')
                {
                    numOfMissingCoord++;
                }
            }
            return numOfMissingCoord;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetCrystalInfo  (string pdbId)
        {
            string queryString = string.Format("Select Method, Resolution, R_factor_R_work, R_factor_R_free From PdbEntry Where PdbID = '{0}';", pdbId);
            DataTable entryInfoTable = dbQuery.Query(protcidDbConnect, queryString);
            string[] entryInfo = new string[4];
            if (entryInfoTable.Rows.Count > 0)
            {  
                string method = entryInfoTable.Rows[0]["Method"].ToString().ToUpper();
                if (methodHash.ContainsKey(method))
                {
                    entryInfo[0] = (string)methodHash[method];
                }
                else
                {
                    entryInfo[0] = "-";
                }
                entryInfo[1] = entryInfoTable.Rows[0]["Resolution"].ToString ();
                entryInfo[2] = entryInfoTable.Rows[0]["R_factor_R_work"].ToString ();
                entryInfo[3] = entryInfoTable.Rows[0]["R_factor_R_free"].ToString ();
            }
            return entryInfo;
        }
        #endregion

        #region missing crc alignments
        private string crcSeqFileDir = @"D:\Qifang\ProjectData\crcSeqFiles";
        public void GetCrcsMissingAlignments ()
        {
            string queryString = "Select Distinct crc From pdbcrcmap;";
            DataTable crcTable = dbQuery.Query(localMysqlDbConnect, queryString);
            string crc = "";
            StreamWriter crcWriter = new StreamWriter("NoAlignCrcList.txt");
            ArrayList noAlignCrcList = new ArrayList();
            foreach (DataRow crcRow in crcTable.Rows)
            {
                crc = crcRow["crc"].ToString();
                if (DoesCrcHaveHHalignments (crc))
                {
                    continue;
                }
                crcWriter.WriteLine(crc);
                WriteCrcSequenceFile(crc);
            }
            crcWriter.Close();

            protcidDbConnect.DisconnectFromDatabase();
            localMysqlDbConnect.DisconnectFromDatabase();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crc"></param>
        private void WriteCrcSequenceFile (string crc)
        {
            string queryString = string.Format("Select pdb, chain_id, length, name, species from pdbcrcmap where crc = '{0}' Limit 1;", crc);
            DataTable chainTable = dbQuery.Query(localMysqlDbConnect, queryString);
            string pdbId = "";
            string authChain = "";
            string sequence = "";
            string crcSeqFile = Path.Combine(crcSeqFileDir, crc + ".fasta");
            if (chainTable.Rows.Count> 0)
            {
                pdbId = chainTable.Rows[0]["pdb"].ToString();
                authChain = chainTable.Rows[0]["chain_id"].ToString();
                sequence = GetChainSequence(pdbId, authChain);
                StreamWriter seqWriter = new StreamWriter(crcSeqFile);
                seqWriter.Write (">" + crc + " " + chainTable.Rows[0]["length"].ToString () + " " + 
                    chainTable.Rows[0]["name"].ToString () + " "  + chainTable.Rows[0]["species"].ToString () + "\n");
                seqWriter.Write(FormatSequence (sequence));
                seqWriter.Close();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        private string FormatSequence (string sequence)
        {
            string formatSequence = "";
            for (int i = 0; i < sequence.Length; i += 80)
            {
                if (i + 80 < sequence.Length)
                {
                    formatSequence += (sequence.Substring(i, 80) + "\n");
                }
                else
                {
                    formatSequence += (sequence.Substring(i, sequence.Length - i) + "\n");
                }

            }
            return formatSequence.TrimEnd('\n');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <returns></returns>
        private string GetChainSequence (string pdbId, string authChain)
        {
            string queryString = string.Format ("Select Sequence From AsymUnit Where PdbID = '{0}' AND AuthorChain = '{1}';", pdbId, authChain);
            DataTable seqTable = dbQuery.Query(protcidDbConnect, queryString);
            if (seqTable.Rows.Count > 0)
            {
                return seqTable.Rows[0]["Sequence"].ToString();
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crc"></param>
        /// <returns></returns>
        private bool DoesCrcHaveHHalignments (string crc)
        {
            string queryString = string.Format("Select query, hit from hhglobal where query = '{0}' AND hit = '{0}';", crc);
            DataTable alignTable = dbQuery.Query(localMysqlDbConnect, queryString);
            if (alignTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion
    }
}
