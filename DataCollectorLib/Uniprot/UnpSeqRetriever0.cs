using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Net;
using System.Data;
using System.IO;
using System.Threading;
using DbLib;
using XtalLib.Settings;
using AuxFuncLib;

namespace DataCollectorLib.Uniprot
{
    public class UnpSeqRetriever
    {
        private WebClient webClient = new WebClient();
        private DbQuery dbQuery = new DbQuery();
        private DbInsert dbInsert = new DbInsert();
        private string httpAddress = "http://www.uniprot.org/uniprot/";
        private string dataFileDir = @"E:\DbProjectData\UniProt\UnpSequencesInPdb1\";

        #region retrieve unp sequence files
        /// <summary>
        /// 
        /// </summary>
        public void RetrieveUniProtSequencesForPdbSequences()
        {
            Initialize();

            string queryString = "Select Distinct PdbId, EntityID From AsymUnit WHere PolymerType = 'polypeptide'";
            DataTable protEntityTable = dbQuery.Query(queryString);
            string[] entryEntities = new string[protEntityTable.Rows.Count];
            int count = 0;
            foreach (DataRow protEntityRow in protEntityTable.Rows)
            {
                entryEntities[count] = protEntityRow["PdbID"].ToString() + protEntityRow["EntityID"].ToString();
                count++;
            }
            RetrieveUnpSequences(entryEntities);
        }
        /// <summary>
        /// 
        /// </summary>
        private void Initialize()
        {
            if (AppSettings.dirSettings == null)
            {
                AppSettings.LoadDirSettings();
                DbBuilder.dbConnect.ConnectString =
                    "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=masterkey;DATABASE=" + AppSettings.dirSettings.dbPath;
            }
            DbBuilder.dbConnect.ConnectToDatabase();

            if (!Directory.Exists(dataFileDir))
            {
                Directory.CreateDirectory(dataFileDir);
            }

            AppSettings.progressInfo.ResetCurrentProgressInfo();
            AppSettings.progressInfo.currentOperationLabel = "Retrieve UNP sequences";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryEntities"></param>
        public void RetrieveUnpSequences(string[] entryEntities)
        {
            StreamWriter dataWriter = new StreamWriter("UndefinedUniprotInPdb.txt");

            AppSettings.progressInfo.totalStepNum = entryEntities.Length;
            AppSettings.progressInfo.totalOperationNum = entryEntities.Length;

            string pdbId = "";
            int entityId = -1;
            string srcType = "";
            foreach (string entryEntity in entryEntities)
            {
                pdbId = entryEntity.Substring (0, 4);
                entityId = Convert.ToInt32(entryEntity.Substring (4, entryEntity.Length - 4));

                AppSettings.progressInfo.currentOperationNum++;
                AppSettings.progressInfo.currentStepNum++;
                AppSettings.progressInfo.currentFileName = pdbId + entityId;

         //       string[] dbAccessions = GetUnpAccessions(pdbId, entityId, out srcType, true);
                string[] dbAccessions = GetUnpAccessionsFromSifts(pdbId, entityId);
                if (dbAccessions != null && dbAccessions.Length == 0)
                {
                    dataWriter.WriteLine(pdbId + entityId.ToString() + " no UNP mapping.");
                }
                foreach (string dbAccession in dbAccessions)
                {
                    if (File.Exists(dataFileDir + dbAccession + ".fasta"))
                    {
                        continue;
                    }
                    try
                    {
                        //   Thread.Sleep(1000);
                        webClient.DownloadFile(httpAddress + dbAccession + ".fasta", dataFileDir + dbAccession + ".fasta");
                    }
                    catch (Exception ex)
                    {
                        dataWriter.WriteLine(dbAccession + " : Cannot find in the UniProt: " + ex.Message);
                        dataWriter.Flush();
                    }
                }
            }
            dataWriter.Close();
            DbBuilder.dbConnect.DisconnectFromDatabase();
            AppSettings.progressInfo.progStrQueue.Enqueue("Done!");
            AppSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string[] GetUnpAccessionsFromSifts (string pdbId, int entityId)
        {
            string queryString = string.Format("Select Distinct DbAccession From PdbDbRefSifts " +
               " Where PdbID = '{0}' AND EntityID = {1} AND DbName = 'UNP' ORDER BY RefID;", pdbId, entityId);
            DataTable dbAccTable = dbQuery.Query(queryString);
            string[] dbAccessions = new string[dbAccTable.Rows.Count];
            if (dbAccTable.Rows.Count > 0)
            {
                int count = 0;
                foreach (DataRow unpAccRow in dbAccTable.Rows)
                {
                    dbAccessions[count] = unpAccRow["DbAccession"].ToString().TrimEnd();
                    count++;
                }
            }
            return dbAccessions;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string[] GetUnpAccessions(string pdbId, int entityId, out string srcType, bool needUnique)
        {
            srcType = "";
            string queryString = string.Format("Select DbAccession From PdbDbRefSifts " + 
                " Where PdbID = '{0}' AND EntityID = {1} AND DbName = 'UNP' ORDER BY RefID;", pdbId, entityId);
            DataTable dbAccTable = dbQuery.Query(queryString);
            string[] dbAccessions = null;
            if (dbAccTable.Rows.Count > 0)
            {
                dbAccessions = new string[dbAccTable.Rows.Count];
                int count = 0;
                foreach (DataRow unpAccRow in dbAccTable.Rows)
                {
                    dbAccessions[count] = unpAccRow["DbAccession"].ToString().TrimEnd();
                    count++;
                }
            }
            else
            {
                dbAccessions = GetUnpAccessionsFromOtherSrc (pdbId, entityId, out srcType);
            }
           
            if (needUnique)
            {
                ArrayList distinctDbAccList = new ArrayList();
                foreach (string dbAcc in dbAccessions)
                {
                    if (!distinctDbAccList.Contains(dbAcc))
                    {
                        distinctDbAccList.Add(dbAcc);
                    }
                }
                dbAccessions = new string[distinctDbAccList.Count];
                distinctDbAccList.CopyTo(dbAccessions);
            }
            return dbAccessions;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string[] GetUnpAccessionsFromOtherSrc(string pdbId, int entityId, out string srcType)
        {
            srcType = "";
            string queryString = string.Format("Select AuthorChain, DbAccession From PdbUnpSsmapResidueMap " +
                " Where PdbID = '{0}' AND EntityId = {1} ORDER BY AuthorChain;", pdbId, entityId);
            DataTable dbAccTable = dbQuery.Query(queryString);
            if (dbAccTable.Rows.Count > 0)
            {
                srcType = "ssmap";
            }
            ArrayList dbAccList = new ArrayList();
            if (dbAccTable.Rows.Count == 0)
            {
                queryString = string.Format("Selec  AuthorChain, DbAccession From PdbUnpSwsResidueMap " +
                " Where PdbID = '{0}' AND EntityId = {1} ORDER BY AuthorChain;", pdbId, entityId);
                dbAccTable = dbQuery.Query(queryString);
                if (dbAccTable.Rows.Count > 0)
                {
                    srcType = "sws";
                }
            }
            string authChain = "";
            string preAuthChain = "";
            foreach (DataRow dbAccRow in dbAccTable.Rows)
            {
                authChain = dbAccRow["AuthorChain"].ToString().TrimEnd();
                if (preAuthChain == "")
                {
                    preAuthChain = authChain;
                }
                if (authChain != preAuthChain)
                {
                    break;
                }
                dbAccList.Add(dbAccRow["DbAccession"].ToString().TrimEnd());
            }
            string[] dbAccessions = new string[dbAccList.Count];
            dbAccList.CopyTo(dbAccessions);
            return dbAccessions;
        }
       
        #endregion

        #region update pdb db ref data from ssmap and sws
        private DataTable dbRefSeqTable = null;
        private DataTable dbRefTable = null;
        private StreamWriter updateLogWriter = null;
        private int totalNumOfEntitiesUpdated = 0;
        /// <summary>
        /// 
        /// </summary>
        public void UpdatePdbDbRefByDifSources()
        {
            if (AppSettings.dirSettings == null)
            {
                AppSettings.LoadDirSettings();
                DbBuilder.dbConnect.ConnectString =
                    "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=masterkey;DATABASE=" + AppSettings.dirSettings.dbPath;
            }
            DbBuilder.dbConnect.ConnectToDatabase();
            updateLogWriter = new StreamWriter("DbRefUpdateLog.txt", true);

            string dataTableString = "Select First 1 * From PdbDbRefSeq;";
            dbRefSeqTable = dbQuery.Query(dataTableString);
            dbRefSeqTable.TableName = "PdbDbRefSeq";
            dbRefSeqTable.Clear();
            dataTableString = "Select First 1 * From PdbDbRef;";
            dbRefTable = dbQuery.Query(dataTableString);
            dbRefTable.TableName = "PdbDbRef";
            dbRefTable.Clear();

            string queryString = "Select Distinct PdbId, EntityID From AsymUnit WHere PolymerType = 'polypeptide'";
            DataTable protEntityTable = dbQuery.Query(queryString);
            string pdbId = "";
            int entityId = -1;
            Hashtable entryRefIdHash = new Hashtable();
            Hashtable entryAlignIdHash = new Hashtable();
            int refId = 0;
            int alignId = 0;
            foreach (DataRow entityRow in protEntityTable.Rows)
            {
                pdbId = entityRow["PdbID"].ToString();
                if (string.Compare (pdbId, "2w2d") < 0)
                {
                    continue;
                }
                entityId = Convert.ToInt32(entityRow["EntityID"].ToString ());
                if (!entryRefIdHash.ContainsKey(pdbId))
                {
                    entryRefIdHash.Add(pdbId, 1);
                }
                if (!entryAlignIdHash.ContainsKey(pdbId))
                {
                    entryAlignIdHash.Add(pdbId, 1);
                }
                refId = (int)entryRefIdHash[pdbId];
                alignId = (int)entryAlignIdHash [pdbId];
                UpdateEntryEntityDbRefData(pdbId, entityId, ref refId, ref alignId);
                entryRefIdHash[pdbId] = refId;
                entryAlignIdHash[pdbId] = alignId;
             }
            updateLogWriter.WriteLine("Total # of Entities are updated: " + totalNumOfEntitiesUpdated);
            updateLogWriter.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        public void UpdateEntryEntityDbRefData(string pdbId,  nt entityId, ref int refId, ref int alignId)
        {
            string srcType = "";
            bool dbAccValid = true;
            string[] dbAccessions = GetUnpAccessions(pdbId, entityId, out srcType, true);
            try
            {
                dbAccValid = DoesEntityHaveValidDbAccessions(dbAccessions);
            }
            catch (Exception ex)
            {
                string stop = "";
            }
            if (dbAccValid && srcType != "")
            {
                updateLogWriter.WriteLine("Update " + pdbId + entityId.ToString () + " from " + srcType);
                // delete related data to this entity
                DeleteDbRefData(pdbId, entityId);
                totalNumOfEntitiesUpdated++;

                DataTable residueMapTable = GetEntityResidueMapTable(pdbId, entityId, srcType);
                UpdatePdbDbRefInfoFromDifSrc(residueMapTable, ref refId, ref alignId, srcType);
         /*       if (srcType == "ssmap")
                {
                    UpdatePdbDbRefFromSSmap(residueMapTable, ref refId, ref alignId);
                }
                else if (srcType == "sws")
                {
                    UpdatePdbDbRefFromSws(residueMapTable, ref refId, ref alignId);
                }
                */
                dbInsert.InsertDataIntoDBtables(dbRefTable);
                dbInsert.InsertDataIntoDBtables(dbRefSeqTable);
                dbRefTable.Clear();
                dbRefSeqTable.Clear();
            }
        }

       
        #region update from SSMap
        /// <summary>
        /// 
        /// </summary>
        /// <param name="residueMapTable"></param>
        /// <param name="refId"></param>
        /// <param name="alignId"></param>
        private void UpdatePdbDbRefInfoFromDifSrc (DataTable residueMapTable, ref int refId, ref int alignId, string srcType)
        {
            string[] unpSeqNumbers = null;
            string[] authorSeqNumbers = null;
            string[] seqNumbers = null;
            string[] authStartEndPoses = new string[2];
            int[] seqStartEndNumbers = null;
            string pdbId = residueMapTable.Rows[0]["PdbID"].ToString ();
            int entityId = Convert.ToInt32 (residueMapTable.Rows[0]["EntityID"].ToString ());
            string authChain = residueMapTable.Rows[0]["AuthorChain"].ToString().TrimEnd ();
            foreach (DataRow residueMapRow in residueMapTable.Rows)
            {
                DataRow dbRefRow = dbRefTable.NewRow();
                dbRefRow["PdbID"] = pdbId;
                dbRefRow["EntityID"] = entityId;
                dbRefRow["RefID"] = refId;
                dbRefRow["DbName"] = "UNP";
                dbRefRow["DbCode"] = "-";
                dbRefRow["DbAccession"] = residueMapRow["DbAccession"];
           //     dbRefRow["Source"] = "ssmap";
                dbRefRow["Source"] = srcType;
                dbRefTable.Rows.Add(dbRefRow);

                
                int[] startEndIndices = GetUnpSeqStartEndPosIndex(residueMapRow["UnpSeqNumbers"].ToString());
                DataRow dbRefSeqRow = dbRefSeqTable.NewRow();
                dbRefSeqRow["PdbID"] = pdbId;
                dbRefSeqRow["RefID"] = refId;
                dbRefSeqRow["AlignID"] = alignId;
                dbRefSeqRow ["AuthorChain"] = authChain;
                unpSeqNumbers = residueMapRow["UnpSeqNumbers"].ToString().Split(',');
                dbRefSeqRow["DBAlignBeg"] = unpSeqNumbers[startEndIndices[0]];
                dbRefSeqRow["DBAlignEnd"] = unpSeqNumbers[startEndIndices[1]];
                authorSeqNumbers = residueMapRow["AuthorSeqNumbers"].ToString().Split(',');
                dbRefSeqRow["AuthorAlignBeg"] = authorSeqNumbers[startEndIndices[0]];
                dbRefSeqRow["AuthorAlignEnd"] = authorSeqNumbers[startEndIndices[1]];
                
                if (srcType == "ssmap")
                {
                    seqNumbers = residueMapRow["SeqNumbers"].ToString().Split(',');
                    dbRefSeqRow["SeqAlignBeg"] = seqNumbers[startEndIndices[0]];
                    dbRefSeqRow["SeqAlignEnd"] = seqNumbers[startEndIndices[1]];
                }
                else if (srcType == "sws")
                {
                    authStartEndPoses[0] = authorSeqNumbers[startEndIndices[0]];
                    authStartEndPoses[1] = authorSeqNumbers[startEndIndices[1]];
                    seqStartEndNumbers = GetSeqNumberForAuthorSeqNumber(pdbId, authChain, authStartEndPoses);
                    dbRefSeqRow["SeqAlignBeg"] = seqStartEndNumbers[0];
                    dbRefSeqRow["SeqAlignEnd"] = seqStartEndNumbers[1];
                }
                dbRefSeqTable.Rows.Add(dbRefSeqRow);

                if (IsGapExistInAlignment(unpSeqNumbers, startEndIndices[0], startEndIndices[1]))
                {
                    updateLogWriter.WriteLine("Gaps exist in " + pdbId + entityId + ", authorchain=" + 
                        authChain + ", AlignID=" + alignId +  ", DbAccession=" + residueMapRow["DbAccession"].ToString () + 
                        ", UNP=" + residueMapRow["UnpSeqNumbers"].ToString ());
                }
                if (IsGapExistInAlignment(authorSeqNumbers, startEndIndices[0], startEndIndices[1]))
                {
                    updateLogWriter.WriteLine("Gaps exist in " + pdbId + entityId + ", authorchain=" +
                        authChain + ", AlignID=" + alignId + ", DbAccession=" + residueMapRow["DbAccession"].ToString() +
                        ", PDB=" + residueMapRow["SeqNumbers"].ToString());
                }
                refId++;
                alignId++;
            }
            AddOtherChainsForTheEntity(pdbId, entityId, authChain, ref alignId);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="alignId"></param>
        private void AddOtherChainsForTheEntity(string pdbId, int entityId, string authChain, ref int alignId)
        {
            string[] authChains = GetAuthorChainsForEntity(pdbId, entityId);
            DataRow[] oneChainSeqRows = dbRefSeqTable.Select();
            foreach (string entityAuthChain in authChains)
            {
                if (entityAuthChain != authChain)
                {
                    foreach (DataRow seqRow in oneChainSeqRows)
                    {
                        DataRow dbRefSeqRow = dbRefSeqTable.NewRow();
                        dbRefSeqRow.ItemArray = seqRow.ItemArray;
                        dbRefSeqRow["AuthorChain"] = entityAuthChain;
                        dbRefSeqRow["AlignID"] = alignId;
                        dbRefSeqTable.Rows.Add(dbRefSeqRow);
                        alignId++;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string[] GetAuthorChainsForEntity(string pdbId, int entityId)
        {
            string queryString = string.Format("Select Distinct AuthorChain From AsymUnit Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable authChainTable = dbQuery.Query(queryString);
            string[] authChains = new string[authChainTable.Rows.Count];
            int count = 0;
            foreach (DataRow authChainRow in authChainTable.Rows)
            {
                authChains[count] = authChainRow["AuthorChain"].ToString().TrimEnd();
                count++;
            }
            return authChains;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpSeqNumbers"></param>
        /// <returns></returns>
        private int[] GetUnpSeqStartEndPosIndex(string unpSeqNumbers)
        {
            string[] numbers = unpSeqNumbers.Split(',');
            int[] startEndIndices = new int[2];
            for (int i = 0; i < numbers.Length; i++)
            {
                if (numbers[i] != "-")
                {
                    startEndIndices[0] = i;
                    break;
                }
            }
            for (int i = numbers.Length - 1; i >= 0; i--)
            {
                if (numbers[i] != "-")
                {
                    startEndIndices[1] = i;
                    break;
                }
            }
            return startEndIndices;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqNumbers"></param>
        /// <param name="startIndex"></param>
        /// <param name="endIndex"></param>
        /// <returns></returns>
        private bool IsGapExistInAlignment(string[] seqNumbers, int startIndex, int endIndex)
        {
            for (int i = startIndex; i <= endIndex; i++)
            {
                if (seqNumbers[i] == "-")
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqNumbers"></param>
        /// <returns></returns>
        private bool IsGapExistInAlignment(string[] seqNumbers)
        {
            for (int i = 0; i < seqNumbers.Length; i++)
            {
                if (seqNumbers[i] == "-")
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region update from SWS
        /// <summary>
        /// 
        /// </summary>
        /// <param name="residueMapTable"></param>
        /// <param name="refId"></param>
        /// <param name="alignId"></param>
        private void UpdatePdbDbRefFromSws(DataTable residueMapTable, ref int refId, ref int alignId)
        {
            string[] unpSeqNumbers = null;
            string[] authorSeqNumbers = null;
            int[] seqStartEndNumbers = null;
            string pdbId = residueMapTable.Rows[0]["PdbID"].ToString();
            int entityId = Convert.ToInt32(residueMapTable.Rows[0]["EntityID"].ToString());
            string authChain = residueMapTable.Rows[0]["AuthorChain"].ToString().TrimEnd();
            string[] authStartEndPoses = new string[2];
            foreach (DataRow residueMapRow in residueMapTable.Rows)
            {
                DataRow dbRefRow = dbRefTable.NewRow();
                dbRefRow["PdbID"] = pdbId;
                dbRefRow["EntityID"] = entityId;
                dbRefRow["RefID"] = refId;
                dbRefRow["DbName"] = "UNP";
                dbRefRow["DbCode"] = "-";
                dbRefRow["DbAccession"] = residueMapRow["DbAccession"];
                dbRefRow["Source"] = "sws";
                dbRefTable.Rows.Add(dbRefRow);

                DataRow dbRefSeqRow = dbRefSeqTable.NewRow();
                dbRefSeqRow["PdbID"] = pdbId;
                dbRefSeqRow["RefID"] = refId;
                dbRefSeqRow["AlignID"] = alignId;
                dbRefSeqRow["AuthorChain"] = authChain;
                unpSeqNumbers = residueMapRow["UnpSeqNumbers"].ToString().Split(',');
                dbRefSeqRow["DBAlignBeg"] = unpSeqNumbers[0];
                dbRefSeqRow["DBAlignEnd"] = unpSeqNumbers[unpSeqNumbers.Length - 1];
                authorSeqNumbers = residueMapRow["AuthorSeqNumbers"].ToString().Split(',');
                dbRefSeqRow["AuthorAlignBeg"] = authorSeqNumbers[0];
                dbRefSeqRow["AuthorAlignEnd"] = authorSeqNumbers[authorSeqNumbers.Length - 1];
                authStartEndPoses[0] = authorSeqNumbers[0];
                authStartEndPoses[1] = authorSeqNumbers[authorSeqNumbers.Length - 1];
                seqStartEndNumbers = GetSeqNumberForAuthorSeqNumber(pdbId, authChain, authStartEndPoses);
                dbRefSeqRow["SeqAlignBeg"] = seqStartEndNumbers[0];
                dbRefSeqRow["SeqAlignEnd"] = seqStartEndNumbers[1];
                dbRefSeqTabl .Rows.Add(dbRefSeqRow);

                if (IsGapExistInAlignment(unpSeqNumbers))
                {
                    updateLogWriter.WriteLine("Gaps exist in " + pdbId + entityId + ", authorchain=" +
                        authChain + ", AlignID=" + alignId + ", DbAccession=" + residueMapRow["DbAccession"].ToString() +
                        ", UNP=" + residueMapRow["UnpSeqNumbers"].ToString());
                }
                if (IsGapExistInAlignment(authorSeqNumbers))
                {
                    updateLogWriter.WriteLine("Gaps exist in " + pdbId + entityId + ", authorchain=" +
                        authChain + ", AlignID=" + alignId + ", DbAccession=" + residueMapRow["DbAccession"].ToString() +
                        ", PDB=" + residueMapRow["AuthorSeqNumbers"].ToString());
                }

                refId++;
                alignId++;
            }
            AddOtherChainsForTheEntity(pdbId, entityId, authChain, ref alignId);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authorChain"></param>
        /// <param name="authSeqNum"></param>
        /// <returns></returns>
        private int[] GetSeqNumberForAuthorSeqNumber(string pdbId, string authorChain, string[] authSeqNums)
        {
            string queryString = string.Format("Select authSeqNumbers From AsymUnit " +
                " Where PdbID = '{0}' AND AuthorChain = '{1}' AND PolymerType = 'polypeptide';", pdbId, authorChain);
            DataTable authSeqNumbersTable = dbQuery.Query(queryString);
            string[] authSeqNumbers = authSeqNumbersTable.Rows[0]["AuthSeqNumbers"].ToString().Split(',');
            int[] seqNumbers = new int[authSeqNums.Length];
            int authSeqNumIndex = -1;
            for (int i = 0; i < authSeqNums.Length; i++)
            {
                authSeqNumIndex = Array.IndexOf(authSeqNumbers, authSeqNums[i]);
                seqNumbers[i] = authSeqNumIndex + 1;
            }
            return seqNumbers;
        }
        #endregion
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="srcType"></param>
        /// <returns></returns>
        private DataTable GetEntityResidueMapTable(string pdbId, int entityId, string srcType)
        {
            DataTable entityResidueMapTable = null;
            if (srcType == "ssmap")
            {
                entityResidueMapTable = GetResidueMapTableFromSsmap (pdbId, entityId);
            }
            else if (srcType == "sws")
            {
                entityResidueMapTable = GetResidueMapTableFromSws (pdbId, entityId);
            }
            return entityResidueMapTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private DataTable GetResidueMapTableFromSsmap(string pdbId, int entityId)
        {
            string queryString = string.Format("Select * From PdbUnpSSmapResidueMap Where PdbID = '{0}' AND EntityID = {1} Order By AuthorChain, IsoForm;",
                 pdbId, entityId);   
            DataTable residueMapTable = dbQuery.Query(queryString);
            DataTable entityResidueMapTable = residueMapTable.Clone();
            string authorChain = "";
            string preAuthorChain = "";
            int isoform = 0;
            int preIsoForm = 0;
            foreach (DataRow residueMapRow in residueMapTable.Rows)
            {
                authorChain = residueMapRow["AuthorChain"].ToString().TrimEnd();
                isoform = Convert.ToInt32(residueMapRow["IsoForm"].ToString ());
                if (preAuthorChain == "")
                {
                    preAuthorChain = authorChain;
                }
                if (preIsoForm == 0)
                {
                    preIsoForm = isoform;
                }
                if (preIsoForm != isoform)
                {
                    break;
                }
                if (preAuthorChain != authorChain)
                {
                    break; // only pick up one chain for the entity
                }
                DataRow dataRow = entityResidueMapTable.NewRow();
                dataRow.ItemArray = residueMapRow.ItemArray;
                entityResidueMapTable.Rows.Add(dataRow);
            }
            return entityResidueMapTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="?"></param>
        /// <returns></returns>
        private DataTable GetResidueMapTableFromSws (string pdbId, int entityId)
        {
              string queryString = string.Format("Select * From PdbUnpSwsResidueMap Where PdbID = '{0}' AND EntityID = {1} Order By AuthorChain;",
                pdbId, entityId);
        
            DataTable residueMapTable = dbQuery.Query(queryString);
            DataTable entityResidueMapTable = residueMapTable.Clone();
            string authorChain = "";
            string preAuthorChain = "";
            foreach (DataRow residueMapRow in residueMapTable.Rows)
            {
                authorChain = residueMapRow["AuthorChain"].ToString ().TrimEnd ();
                if (preAuthorChain == "")
                {
                    preAuthorChain = authorChain;
                }
                if (preAuthorChain != authorChain)
                {
                    break; // only pick up one chain for the entity
                }
                DataRow dataRow = entityResidueMapTable.NewRow();
                dataRow.ItemArray = residueMapRow.ItemArray;
                entityResidueMapTable.Rows.Add(dataRow);
            }
            return entityResidueMapTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbAccessions"></param>
        /// <returns></returns>
        private bool DoesEntityHaveValidDbAccessions(string[] dbAccessions)
        {
            if (dbAccessions.Length == 0)
            {
                return false;
            }
            foreach (string dbAccession in dbAccessions)
            {
                if (!File.Exists(Path.Combine(dataFileDir, dbAccession + ".fasta")))
                {
                    return false;
                }
            }
            return true;
        }
        #endregion

        #region delete data
        private void DeleteDbRefData(string pdbId, int entityId)
        {
            int[] refIds = GetRefIdForTheEntity(pdbId, entityId);
            if (refIds.Length == 0)
            {
                return;
            }
            int[] alignIds = GetAlignIDs(pdbId, refIds);

            // record deleted data
            string queryString = "";
            updateLogWriter.WriteLine("Deleted data for " + pdbId + entityId.ToString ());
            queryString = string.Format("Select * From PdbDbRef Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable entityDbRefTable = dbQuery.Query(queryString);
            updateLogWriter.WriteLine("PdbDbRef");
            WriteDataTableToFile(entityDbRefTable);
            queryString = string.Format("Select * From PdbDbRefSeq Where PdbID = '{0}' AND RefID IN ({1});",
                pdbId, ParseHelper.FormatSqlListString(new ArrayList (refIds)));
            DataTable entityDbRefSeqTable = dbQuery.Query(queryString);
            updateLogWriter.WriteLine("PdbDbRefSeq");
            WriteDataTableToFile(entityDbRefSeqTable);
            if (alignIds.Length > 0)
            {
                queryString = string.Format("Select * From PdbDbRefSeqDif Where PdbID = '{0}' AND AlignID IN ({1});",
                    pdbId, ParseHelper.FormatSqlListString(new ArrayList(alignIds)));
                DataTable entityDbRefSeqDifTable = dbQuery.Q ery(queryString);
                updateLogWriter.WriteLine("PdbDbRefSeqDif");
                WriteDataTableToFile(entityDbRefSeqDifTable);
            }
            // delete data
            string deleteString = "";
            deleteString = string.Format("Delete From PdbDBRef Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            dbQuery.Query(deleteString);
            deleteString = string.Format("Delete From PdbDbRefSeq Where PdbID = '{0}' AND RefID IN ({1});", 
                pdbId, ParseHelper.FormatSqlListString (new ArrayList (refIds)));
            dbQuery.Query(deleteString);
            if (alignIds.Length > 0)
            {
                deleteString = string.Format("Delete From PdbDbRefSeqDif Where PdbID = '{0}' AND AlignID IN ({1});",
                    pdbId, ParseHelper.FormatSqlListString(new ArrayList(alignIds)));
                dbQuery.Query(deleteString);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataTable"></param>
        private void WriteDataTableToFile(DataTable dataTable)
        {
            string dataLine = "";
            foreach (DataRow dataRow in dataTable.Rows)
            {
                dataLine = "";
                foreach (object item in dataRow.ItemArray)
                {
                    dataLine += (item.ToString() + ",");
                }
                updateLogWriter.WriteLine(dataLine.TrimEnd (','));
            }
            updateLogWriter.Flush ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private int[] GetRefIdForTheEntity(string pdbId, int entityId)
        {
            string queryString = string.Format("Select RefID From PdbDbRef Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable refTable = dbQuery.Query (queryString);
            int[] refIds = new int[refTable.Rows.Count];
            int count = 0;
            foreach (DataRow refRow in refTable.Rows)
            {
                refIds[count] = Convert.ToInt32 (refRow["RefID"].ToString());
                count++;
            }
            return refIds;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="refIds"></param>
        /// <returns></returns>
        private int[] GetAlignIDs(string pdbId, int[] refIds)
        {
            string queryString = string.Format("Select AlignID From PdbDbRefSeq Where PdbID = '{0}' AND RefID In ({1});", 
                pdbId, ParseHelper.FormatSqlListString (new ArrayList (refIds)));
            DataTable alignIdTable = dbQuery.Query(queryString);
            int[] alignIds = new  int[alignIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow alignRow in alignIdTable.Rows)
            {
                alignIds[count] = Convert.ToInt32 (alignRow["AlignID"].ToString());
                count++;
            }
            return alignIds;
        }
        #endregion

        #region for protein entities with incorrect or missing or obsolete dbaccession
        public void UpdateUnpSequenceFilesFromLog ()
        {
            Initialize();
            StreamWriter dataWriter = new StreamWriter("UndefinedUniprotInLogFile.txt");

            string[] entryEntities = GetPdbEntitiesWithIncorrectUnpCodes();

            AppSettings.progressInfo.totalStepNum = entryEntities.Length;
            AppSettings.progressInfo.totalOperationNum = entryEntities.Length;

            string pdbId = "";
            int entityId = -1;
            string srcType = "";
            foreach (string entryEntity in entryEntities)
            {
                pdbId = entryEntity.Substring(0, 4);
                entityId = Convert.ToInt32(entryEntity.Substring(4, entryEntity.Length - 4));

                AppSettings.pro ressInfo.currentOperationNum++;
                AppSettings.progressInfo.currentStepNum++;
                AppSettings.progressInfo.currentFileName = pdbId + entityId;

                string[] dbAccessions = GetUnpAccessionsFromOtherSrc(pdbId, entityId, out srcType);
                if (dbAccessions.Length == 0)
                {
                    dataWriter.WriteLine(pdbId + entityId.ToString() + " no UNP mapping.");
                }
                foreach (string dbAccession in dbAccessions)
                {
                    if (File.Exists(dataFileDir + dbAccession + ".fasta"))
                    {
                        continue;
                    }
                    try
                    {
                        //   Thread.Sleep(1000);
                        webClient.DownloadFile(httpAddress + dbAccession + ".fasta", dataFileDir + dbAccession + ".fasta");
                    }
                    catch (Exception ex)
                    {
                        dataWriter.WriteLine(dbAccession + " : Cannot find in the UniProt: " + ex.Message);
                        dataWriter.Flush();
                    }
                }
            }
            dataWriter.Close();
            DbBuilder.dbConnect.DisconnectFromDatabase();
            AppSettings.progressInfo.progStrQueue.Enqueue("Done!");
            AppSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetPdbEntitiesWithIncorrectUnpCodes()
        {
            StreamReader dataReader = new StreamReader("UndefinedUniprotInPdb.txt");
            ArrayList entryEntityList = new ArrayList();
            string line = "";
            string dbAccession = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("The remote server returned an error") > -1)
                {
                    string[] fields = line.Split(':');
                    dbAccession = fields[0].TrimEnd();
                    string[] entryEntities = GetEntryEntitiesForUnpAcc(dbAccession);
                    entryEntityList.AddRange (entryEntities);
                }
            }
            dataReader.Close();
            DirectoryInfo dirInfo = new DirectoryInfo(dataFileDir);
            FileInfo[] fileInfos = dirInfo.GetFiles ();
            foreach (FileInfo fileInfo in fileInfos)
            {
                if (fileInfo.Length == 0)
                {
                    dbAccession = GetDbAccessionFromFileName(fileInfo.Name);
                    string[] entryEntities = GetEntryEntitiesForUnpAcc(dbAccession);
                    entryEntityList.AddRange(entryEntities);
                }
            }
            string[] allEntryEntities = new string[entryEntityList.Count];
            entryEntityList.CopyTo(allEntryEntities);
            return allEntryEntities;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private string GetDbAccessionFromFileName(string fileName)
        {
            int exeIndex = fileName.IndexOf(".fasta");
            return fileName.Substring(0, exeIndex);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbAccession"></param>
        /// <returns></returns>
        private string[] GetEntryEntitiesForUnpAcc(string dbAccession)
        {
            string queryString = string.Format("Select Distinct PdbID, EntityID From PdbDbRef " + 
                " Where DbAccession = '{0}';", dbAccession);
            DataTable entityTable = dbQuery.Query(queryString);
            string[] entryEntities = new string[entityTable.Rows.Count];
            int count = 0;
            foreach (DataRow entityRow in entityTable.Rows)
            {
                entryEntities[count] = entityRow["PdbID"].ToString () + entityRow["EntityID"].ToSt ing();
                count++;
            }
            return entryEntities;
        }
        #endregion

        #region combine unp sequence files into one file
        /// <summary>
        /// 
        /// </summary>
        public void CombineUnpFastaFiles()
        {
      //      string[] parseAccessions = GetParsedUnpSequences();
            DateTime dt = new DateTime(2010, 3, 30);
            string[] fastaFiles = Directory.GetFiles(dataFileDir, "*.fasta");
            StreamWriter dataWriter = new StreamWriter("UnpSequencesInPdbLeft.txt");
            string line = "";
            string dbAccession = "";
            foreach (string fastaFile in fastaFiles)
            {
                dbAccession = GetDbAccFromFullFileName(fastaFile);
         /*       if (Array.IndexOf(parseAccessions, dbAccession) > -1) // some of those are already aligned to HMMer
                {
                    continue;
                }*/
                FileInfo fileInfo = new FileInfo(fastaFile);
                if (DateTime.Compare(fileInfo.CreationTime, dt) < 0)
                {
                    continue;
                }
                StreamReader dataReader = new StreamReader(fastaFile);
                while ((line = dataReader.ReadLine()) != null)
                {
                    dataWriter.WriteLine(line);
                }
                dataReader.Close();
                dataWriter.Flush();
            }
            dataWriter.Close();

        }

        private string GetDbAccFromFullFileName(string fastaFile)
        {
            int exeIndex = fastaFile.IndexOf(".fasta");
            int lastDashIndex = fastaFile.LastIndexOf("\\");
            return fastaFile.Substring(lastDashIndex + 1, exeIndex - lastDashIndex - 1);
        }
        private string[] GetParsedUnpSequences()
        {
            StreamReader dataReader = new StreamReader("UnpSequencesInPdb.txt");
            string line = "";
            ArrayList unpSeqList = new ArrayList();
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.Substring(0, 1) == ">")
                {
                    string[] fields = line.Split(' ');
                    string[] unpcodeFields = fields[0].Split('|');
                    unpSeqList.Add(unpcodeFields[1]);
                }
            }
            dataReader.Close();
            string[] parsedUnpAccs = new  string[unpSeqList.Count];
            unpSeqList.CopyTo(parsedUnpAccs);
            return parsedUnpAccs;
        }
        #endregion
    }
}
