using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;

namespace BuCompLib.BamDb
{
    public class BamDbUpdate
    {
        private DbInsert dbInsert = new DbInsert();
        private DbQuery dbQuery = new DbQuery();
        private DbUpdate dbUpdate = new DbUpdate();
        private string bamDbFile = @"X:\Firebird\BAM\PDB.FDB";
        private DbConnect bamDbConnect = null;
        private string[] BamDbTableNames = { "pdb_entry", "pdb_dbref", "pdb_seq", "pdb_asym_auth", "pdb_pfam", "pdb_protbud" };

        /// <summary>
        /// 
        /// </summary>
        public BamDbUpdate()
        {
            bamDbConnect = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" + bamDbFile);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bamDbPathFile"></param>
        public BamDbUpdate(string bamDbPathFile)
        {
            bamDbFile = bamDbPathFile;
            bamDbConnect = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" + bamDbPathFile);
        }
   
        /// <summary>
        /// 
        /// </summary>
        public void BuildBamDatabase ()
        {
            InitializeBamDbTables();

            Console.WriteLine("Import data for pdb_entry");
            CreatePdbEntryTable();

            Console.WriteLine("Import data for pdb_dbref");
            CreatePdbDbRefTable();

            Console.WriteLine("Import data for pdb_seq");
            CreatePdbSeqTable();

            Console.WriteLine("Import data for pdb_asym_auth");
            CreatePdbAsymAuthTable();

            Console.WriteLine("Import data for pdb_pfam");
            CreatePdbPfamTable();

            Console.WriteLine("Import data for pdb_protbud");
            CreateProtBudBuTable();

            Console.WriteLine("Build BAM database done!");
        }

        /// <summary>
        /// update BAM database from pdbfam database
        /// </summary>
        public void UpdateBamDatabase(string[] updateEntries)
        {
 //           string[] updateEntries = GetUpdateEntries();

            Console.WriteLine ("Update data for pdb_entry");
            UpdatePdbEntryTable(updateEntries);  

            Console.WriteLine ("Update data for pdb_dbref");
            UpdatePdbDbRefTable(updateEntries);  

            Console.WriteLine ("Update data for pdb_seq");
            UpdatePdbSeqTable(updateEntries);  

            Console.WriteLine ("Update data for pdb_asym_auth");
            UpdatePdbAsymAuthTable(updateEntries);   

            Console.WriteLine ("Update data for pdb_pfam");
            UpdatePdbPfamTable(updateEntries);      
            
            Console.WriteLine ("Update data for pdb_protbud");
            UpdateProtBudBuTable(updateEntries);   

            Console.WriteLine ("Update BAM database done!");
        }

        #region create bam tables from pdbfam database
        /// <summary>
        /// update pdb_entry table 
        /// </summary>
        public void CreatePdbEntryTable()
        {
            string queryString = "Select Upper(PdbEntry.PdbID) As pdb_id, title, descript as description, method as expdta, keywords, keywords_text, " +
                " cast(depositfiledate as varchar(10)) as pdb_ori_date, resolution From PdbEntry, BamEntryKeywords " +
                " where PdbEntry.PdbID = BamEntryKeywords.PdbID;";
            DataTable pdbEntryTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            pdbEntryTable.TableName = "pdb_entry";
            dbInsert.BatchInsertDataIntoDBtables(bamDbConnect, pdbEntryTable);
        }

        /// <summary>
        /// pdb_dbref
        /// </summary>
        public void CreatePdbDbRefTable()
        {
            string queryString = "Select Upper(PdbID) as pdb_id, RefID as ref_id, EntityID as entity_id, dbname as db_name, dbcode as db_code, dbAccession as db_accession " +
                " From PdbDbRefSifts;";
            DataTable pdbDbRefTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            pdbDbRefTable.TableName = "pdb_dbref";
            dbInsert.BatchInsertDataIntoDBtables(bamDbConnect, pdbDbRefTable);

            AddDbRefRecordsFromXml();
        }

        /// <summary>
        /// 
        /// </summary>
        private void AddDbRefRecordsFromXml()
        {
            string queryString = "Select pdb_id from pdb_entry;";
            DataTable entryTable = dbQuery.Query(bamDbConnect, queryString);
            queryString = "Select pdb_id from pdb_dbref;";
            DataTable entryInDbRefTable = dbQuery.Query(bamDbConnect, queryString);
            string pdbId = "";
            List<string> leftPdbList = new List<string>();
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["pdb_id"].ToString();
                DataRow[] entryRows = entryInDbRefTable.Select(string.Format("pdb_id = '{0}'", pdbId));
                if (entryRows.Length > 0)
                {
                    continue;
                }
                leftPdbList.Add(pdbId);
            }
            DataTable dbRefTable = null;
            foreach (string lsPdb in leftPdbList)
            {
                queryString = string.Format("Select Upper(PdbID) as pdb_id, RefID as ref_id, EntityID as entity_id, dbname as db_name, dbcode as db_code, dbAccession as db_accession " +
                " From PdbDbRefXml Where PdbID = '{0}';", pdbId);
                DataTable xmlDbRefTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
                ParseHelper.AddNewTableToExistTable(xmlDbRefTable, ref dbRefTable);
            }
            dbRefTable.TableName = "pdb_dbref";
            dbInsert.BatchInsertDataIntoDBtables(bamDbConnect, dbRefTable);
        }

        /// <summary>
        /// pdb_seq
        /// </summary>
        public void CreatePdbSeqTable()
        {
            string queryString = "Select Upper(PdbID) as pdb_id, EntityID as entity_id, PolymerTypeInPdb as type, " +
                " NstdMonomer As nstd_monomer, SequenceNstd as seq_code, Sequence as seq_code_canonical " +
                " From EntityInfo;";
            DataTable pdbSeqTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            pdbSeqTable.TableName = "pdb_seq";
            dbInsert.BatchInsertDataIntoDBtables(bamDbConnect, pdbSeqTable);
        }

        /// <summary>
        /// pdb_asym_auth
        /// </summary>
        public void CreatePdbAsymAuthTable()
        {
            string queryString = "Select Upper(PdbID) as pdb_id, EntityID as entity_id, AsymID as asym_id, AuthorChain As auth_asym_id From AsymUnit;";
            DataTable chainInfoTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            chainInfoTable.TableName = "pdb_asym_auth";
            dbInsert.BatchInsertDataIntoDBtables(bamDbConnect, chainInfoTable);
        }

        /// <summary>
        /// pdb_pfam
        /// </summary>
        public void CreatePdbPfamTable()
        {
            string queryString = "Select Upper(PdbID) as pdb_id, EntityID as entity_id, Pfam_ID as model, Pfam_Acc as Pfam_accession, Description, " +
                " SeqStart as seq_f,  SeqEnd as seq_t, HmmStart as hmm_f, HmmEnd as hmm_t, BitScore as score, EValue as e_value " +
                " From PdbPfam;";
            DataTable pdbpfamTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            pdbpfamTable.TableName = "pdb_pfam";
            dbInsert.BatchInsertDataIntoDBtables(bamDbConnect, pdbpfamTable);        
        }

        /// <summary>
        /// pdb_protbud
        /// </summary>
        public void CreateProtBudBuTable()
        {
            string queryString = "Select Upper(PdbID) as pdb_id, names as name, pdbbuid, pisabuid, asu_entity, asu_asymid, asu_auth, asu_abc, " +
                " pdbbu_entity, pdbbu_asymid, pdbbu_auth, pdbbu_abc, pisabu_entity, pisabu_asymid, pisabu_auth, pisabu_abc, samebus, " + 
                " DNA, RNA, ligands, Resolution " +
                " From ProtBudBiolAssemblies;";
            DataTable protbudTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            protbudTable.TableName = "pdb_protbud";

            dbInsert.BatchInsertDataIntoDBtables(bamDbConnect, protbudTable);
        }

        /// <summary>
        /// 
        /// </summary>
        private void InitializeBamDbTables()
        {
            DbCreator dbCreator = new DbCreator();
            string createTableString = "";
            string createIndexString = "";
            string tableName = "pdb_entry";
            createTableString = "Create Table " + tableName + " ( " +
                                "PDB_ID                          CHAR(4) Not Null, " +
                                "TITLE                           BLOB sub_type TEXT Not Null, " +
                                "DESCRIPTION                     BLOB sub_type TEXT Not Null, " +
                                "EXPDTA                          VARCHAR(250) Not Null, " +
                                "KEYWORDS                        VARCHAR(250) Not Null, " +
                                "KEYWORDS_TEXT                   BLOB sub_type TEXT Not Null, " +
                                "PDB_ORI_DATE                    Date Not Null, " +
                                "RESOLUTION                      FLOAT);";
            dbCreator.CreateTableFromString(bamDbConnect, createTableString, tableName);
            createIndexString = "Create Index " + tableName + "_pdb ON " + tableName + "(PDB_ID);";
            dbCreator.CreateIndex(bamDbConnect, createIndexString, tableName);


            tableName = "pdb_dbref";
            createTableString = "Create Table " + tableName + " ( " +
                                "PDB_ID                          CHAR(4) Not Null, " +
                                "REF_ID                          INTEGER Not Null, " +
                                "ENTITY_ID                       INTEGER Not Null, " +
                                "DB_NAME                         VARCHAR(10) Not Null, " +
                                "DB_CODE                         VARCHAR(32) Not Null, " +
                                "DB_ACCESSION                    VARCHAR(32) Not Null);";
            dbCreator.CreateTableFromString(bamDbConnect, createTableString, tableName);
            createIndexString = "Create Index " + tableName + "_pdb ON " + tableName + "(PDB_ID, Entity_ID);";
            dbCreator.CreateIndex(bamDbConnect, createIndexString, tableName);

            tableName = "pdb_seq";
            createTableString = "Create Table " + tableName + " ( " +
                                "PDB_ID                          CHAR(4) Not Null, " +
                                "ENTITY_ID                       INTEGER Not Null, " +
                                "TYPE                            VARCHAR(64) Not Null, " +
                                "NSTD_MONOMER                    CHAR(1) Not Null, " +
                                "SEQ_CODE                        BLOB sub_type TEXT Not Null, " +
                                "SEQ_CODE_CANONICAL              BLOB sub_type TEXT Not Null);";
            dbCreator.CreateTableFromString(bamDbConnect, createTableString, tableName);
            createIndexString = "Create Index " + tableName + "_pdb ON " + tableName + "(PDB_ID, Entity_ID);";
            dbCreator.CreateIndex(bamDbConnect, createIndexString, tableName);

            tableName = "pdb_asym_auth";
            createTableString = "Create Table " + tableName + " ( " +
                                "PDB_ID                          CHAR(4) Not Null, " +
                                "ENTITY_ID                       INTEGER Not Null, " +
                                "ASYM_ID                         VARCHAR(3) Not Null, " +
                                "AUTH_ASYM_ID                    VARCHAR(4) Not Null);";
            dbCreator.CreateTableFromString(bamDbConnect, createTableString, tableName);
            createIndexString = "Create Index " + tableName + "_pdb ON " + tableName + "(PDB_ID);";
            dbCreator.CreateIndex(bamDbConnect, createIndexString, tableName);

            tableName = "pdb_pfam";
            createTableString = "Create Table " + tableName + " ( " +
                                "PDB_ID                          CHAR(4) Not Null, " +
                                "ENTITY_ID                       INTEGER Not Null, " +
                                "MODEL                           VARCHAR(50) Not Null, " +
                                "PFAM_accession                  VARCHAR(10) Not Null, " +
                                "Description                     VARCHAR(1200) Not Null, " +
                                "SEQ_F                           INTEGER Not Null, " +
                                "SEQ_T                           INTEGER Not Null, " +
                                "HMM_F                           INTEGER Not Null, " +
                                "HMM_T                           INTEGER Not Null, " +
                                "SCORE                           FLOAT, " +
                                "E_VALUE                         FLOAT);";
            dbCreator.CreateTableFromString(bamDbConnect, createTableString, tableName);
            createIndexString = "Create Index " + tableName + "_pdb ON " + tableName + "(PDB_ID, Entity_ID);";
            dbCreator.CreateIndex(bamDbConnect, createIndexString, tableName);

            tableName = "pdb_protbud";
            createTableString = "Create Table " + tableName + " ( " +
                                "PDB_ID                          CHAR(4) Not Null, " +
                                "NAME                            BLOB sub_type TEXT, " +
                                "PDBBUID                         VARCHAR(10) Not Null, " +
                                "PISABUID                        VARCHAR(10) Not Null, " +
                                "ASU_ENTITY                      BLOB sub_type TEXT Not Null, " +
                                "ASU_ASYMID                      BLOB sub_type TEXT Not Null, " +
                                "ASU_AUTH                        BLOB sub_type TEXT Not Null, " +
                                "ASU_ABC                         BLOB sub_type TEXT Not Null, " +
                                "PDBBU_ENTITY                    BLOB sub_type TEXT Not Null, " +
                                "PDBBU_ASYMID                    BLOB sub_type TEXT Not Null, " +
                                "PDBBU_AUTH                      BLOB sub_type TEXT Not Null, " +
                                "PDBBU_ABC                       BLOB sub_type TEXT Not Null, " +
                                "PISABU_ENTITY                   BLOB sub_type TEXT Not Null, " +
                                "PISABU_ASYMID                   BLOB sub_type TEXT Not Null, " +
                                "PISABU_AUTH                     BLOB sub_type TEXT Not Null, " +
                                "PISABU_ABC                      BLOB sub_type TEXT Not Null, " +
                                "SAMEBUS                         CHAR(9), " +
                                "DNA                             CHAR(3), " +
                                "RNA                             CHAR(3), " +
                                "LIGANDS                         CHAR(3), " +
                                "RESOLUTION                      VARCHAR(32));";
            dbCreator.CreateTableFromString(bamDbConnect, createTableString, tableName);
            createIndexString = "Create Index " + tableName + "_pdb ON " + tableName + "(PDB_ID);";
            dbCreator.CreateIndex(bamDbConnect, createIndexString, tableName);
        }
        #endregion

        #region update bam tables from pdbpfam database
        /// <summary>
        /// update pdb_entry table 
        /// </summary>
        public void UpdatePdbEntryTable(string[] updateEntries)
        {
            string tableName = "pdb_entry";
            foreach (string pdbId in updateEntries)
            {
                DeletePdbEntryData(pdbId, tableName);
                try
                {
                    DataTable pdbEntryTable = ReadPdbEntryTable(pdbId, tableName);
                    dbInsert.BatchInsertDataIntoDBtables(bamDbConnect, pdbEntryTable);
                }
                catch (Exception ex)
                {
                    BuCompBuilder.logWriter.WriteLine(pdbId + " Update pdb_entry error: " + ex.Message);
                    BuCompBuilder.logWriter.Flush();
                }
            }            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="tableName"></param>
        private void DeletePdbEntryData (string pdbId, string tableName)
        {
            string deleteString = string.Format("Delete From {0} Where pdb_id = '{1}';", tableName, pdbId);
            dbUpdate.Delete(bamDbConnect, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable ReadPdbEntryTable (string pdbId, string tableName)
        {
            string queryString = string.Format ("Select Upper(PdbEntry.PdbID) As pdb_id, title, descript as description, method as expdta, keywords, keywords_text, " +
                " cast(depositfiledate as varchar(10)) as pdb_ori_date, resolution From PdbEntry, BamEntryKeywords " +
                " where PdbEntry.PdbID = BamEntryKeywords.PdbID AND PdbEntry.PdbID = '{0}';", pdbId);
            DataTable pdbEntryTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            pdbEntryTable.TableName = tableName;
            return pdbEntryTable;
        }

        /// <summary>
        /// pdb_dbref
        /// </summary>
        public void UpdatePdbDbRefTable(string[] updateEntries)
        {
            string tableName = "pdb_dbref";
            foreach (string pdbId in updateEntries)
            {
                DeletePdbEntryData(pdbId, tableName);
                try
                {
                    DataTable pdbDbRefTable = GetPdbDbRefTable(pdbId, tableName);
                    dbInsert.BatchInsertDataIntoDBtables(bamDbConnect, pdbDbRefTable);
                }
                catch (Exception ex)
                {
                    BuCompBuilder.logWriter.WriteLine(pdbId + " Update pdb_dbref error: " + ex.Message);
                    BuCompBuilder.logWriter.Flush();
                }
            }
        }   

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private DataTable GetPdbDbRefTable (string pdbId, string tableName)
        {
            string queryString = string.Format ("Select Upper(PdbID) as pdb_id, RefID as ref_id, EntityID as entity_id, dbname as db_name, dbcode as db_code, dbAccession as db_accession " +
                " From PdbDbRefSifts Where PdbID = '{0}';", pdbId);
            DataTable pdbDbRefTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            if (pdbDbRefTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Upper(PdbID) as pdb_id, RefID as ref_id, EntityID as entity_id, dbname as db_name, dbcode as db_code, dbAccession as db_accession " +
                " From PdbDbRefXml Where PdbID = '{0}';", pdbId);
                pdbDbRefTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            }
            pdbDbRefTable.TableName = tableName;
            return pdbDbRefTable;
        }
     
        /// <summary>
        /// pdb_seq
        /// </summary>
        public void UpdatePdbSeqTable(string[] updateEntries)
        {
            string tableName = "pdb_seq";
            foreach (string pdbId in updateEntries)
            {
                DeletePdbEntryData(pdbId, tableName);
                try
                {
                    DataTable pdbSeqTable = GetPdbSeqInfoTable(pdbId, tableName);
                    dbInsert.BatchInsertDataIntoDBtables(bamDbConnect, pdbSeqTable);
                }
                catch (Exception ex)
                {
                    BuCompBuilder.logWriter.WriteLine(pdbId + " Update pdb_seq error: " + ex.Message);
                    BuCompBuilder.logWriter.Flush();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private DataTable GetPdbSeqInfoTable (string pdbId, string tableName)
        {
            string queryString = string.Format ("Select Upper(PdbID) as pdb_id, EntityID as entity_id, PolymerTypeInPdb as type, " +
                " NstdMonomer As nstd_monomer, SequenceNstd as seq_code, Sequence as seq_code_canonical " +
                " From EntityInfo Where PdbID = '{0}';", pdbId);
            DataTable pdbSeqTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            pdbSeqTable.TableName = tableName;
            return pdbSeqTable;
        }

        /// <summary>
        /// pdb_asym_auth
        /// </summary>
        public void UpdatePdbAsymAuthTable(string[] updateEntries)
        {
            string tableName = "pdb_asym_auth";
            foreach (string pdbId in updateEntries)
            {
                DeletePdbEntryData(pdbId, tableName);
                try
                {
                    DataTable chainInfoTable = GetPdbAsymAuthInfoTable(pdbId, tableName);
                    dbInsert.BatchInsertDataIntoDBtables(bamDbConnect, chainInfoTable);
                }
                catch (Exception ex)
                {
                    BuCompBuilder.logWriter.WriteLine(pdbId + " Update pdb_asym_auth error: " + ex.Message);
                    BuCompBuilder.logWriter.Flush();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private DataTable GetPdbAsymAuthInfoTable (string pdbId, string tableName)
        {
            string queryString = string.Format ("Select Upper(PdbID) as pdb_id, EntityID as entity_id, AsymID as asym_id, AuthorChain As auth_asym_id " + 
                " From AsymUnit Where PdbID = '{0}';", pdbId);
            DataTable chainInfoTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            chainInfoTable.TableName = tableName;
            return chainInfoTable;
        }

        /// <summary>
        /// pdb_pfam
        /// </summary>
        public void UpdatePdbPfamTable(string[] updateEntries)
        {
            string tableName = "pdb_pfam";
            foreach (string pdbId in updateEntries)
            {
                DeletePdbEntryData(pdbId, tableName);
                try
                {
                    DataTable pdbpfamTable = GetPdbPfamTable(pdbId, tableName);
                    dbInsert.BatchInsertDataIntoDBtables(bamDbConnect, pdbpfamTable);
                }
                catch (Exception ex)
                {
                    BuCompBuilder.logWriter.WriteLine(pdbId + " Update pdb_pfam error: " + ex.Message);
                    BuCompBuilder.logWriter.Flush();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private DataTable GetPdbPfamTable (string pdbId, string tableName)
        {
            string queryString = string.Format ("Select Upper(PdbID) as pdb_id, EntityID as entity_id, Pfam_ID as model, Pfam_Acc as Pfam_accession, Description, " +
               " SeqStart as seq_f,  SeqEnd as seq_t, HmmStart as hmm_f, HmmEnd as hmm_t, BitScore as score, EValue as e_value " +
               " From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable pdbpfamTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            pdbpfamTable.TableName = tableName;
            return pdbpfamTable;
        }

        /// <summary>
        /// pdb_protbud
        /// </summary>
        public void UpdateProtBudBuTable(string[] updateEntries)
        {
            string tableName = "pdb_protbud";
            foreach (string pdbId in updateEntries)
            {
                DeletePdbEntryData(pdbId, tableName);
                try
                {
                    DataTable protbudBaTable = GetProtBudBATable(pdbId, tableName);               
                    dbInsert.BatchInsertDataIntoDBtables(bamDbConnect, protbudBaTable);
                }
                catch (Exception ex)
                {
                    BuCompBuilder.logWriter.WriteLine(pdbId + " Update pdb_protbud error: " + ex.Message);
                    BuCompBuilder.logWriter.Flush();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private DataTable GetProtBudBATable (string pdbId, string tableName)
        {
            string queryString = string.Format ("Select Upper(PdbID) as pdb_id, names as name, pdbbuid, pisabuid, asu_entity, asu_asymid, asu_auth, asu_abc, " +
                " pdbbu_entity, pdbbu_asymid, pdbbu_auth, pdbbu_abc, pisabu_entity, pisabu_asymid, pisabu_auth, pisabu_abc, samebus, " +
                " DNA, RNA, ligands, Resolution " +
                " From ProtBudBiolAssemblies Where PdbID = '{0}';", pdbId);
            DataTable protbudTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            protbudTable.TableName = tableName;
            return protbudTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetUpdateEntries()
        {
            List<string> updateEntryList = new List<string> ();
            StreamReader dataReader = new StreamReader(Path.Combine(ProtCidSettings.dirSettings.xmlPath, "newls-pdb.txt"));
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                updateEntryList.Add(line.Substring(0, 4));
            }
            dataReader.Close();
            string[] updateEntries = new string[updateEntryList.Count];
            updateEntryList.CopyTo(updateEntries);
            return updateEntries;
        }
        #endregion
    }
}
