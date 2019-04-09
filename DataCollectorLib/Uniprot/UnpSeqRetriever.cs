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
        private const int minSeqLen = 10;
        private PdbSequenceOutput pdbSeqOutput = new PdbSequenceOutput();
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
            RetrieveUnpSequences(entryEntities, false);
            AppSettings.progressInfo.progStrQueue.Enqueue("Combine UNP sequence files.");
            CombineUnpFastaFiles();
            AppSettings.progressInfo.progStrQueue.Enqueue("Done!");

            AppSettings.progressInfo.progStrQueue.Enqueue("Output Nonredundant PDB sequences");
            pdbSeqOutput.PrintNonredundantPdbChainSequences();
            AppSettings.progressInfo.progStrQueue.Enqueue("Done!");

            DbBuilder.dbConnect.DisconnectFromDatabase();
            AppSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateUniProtSequencesForPdbSequences()
        {
            Initialize();

            string[] updateEntries = GetUpdateEntries();
          //  DateTime lastDt = new DateTime(2010, 4, 28);
        //    string[] updateEntries = GetUpdateEntries(lastDt);
            ArrayList updateEntityList = new ArrayList();
            string queryString = "";
            foreach (string updateEntry in updateEntries)
            {
                queryString = string.Format("Select Distinct PdbID, EntityID From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", updateEntry);
                DataTable entryEntityTable = dbQuery.Query(queryString);
                foreach (DataRow entityRow in entryEntityTable.Rows)
                {
                    updateEntityList.Add(entityRow["PdbID"].ToString () + entityRow["EntityID"].ToString ());
                }
            }
            string[] updateEntities = new string[updateEntityList.Count];
            updateEntityList.CopyTo(updateEntities);

            string[] updateDbAccessions = RetrieveUnpSequences(updateEntities, true);
            AppSettings.progressInfo.progStrQueue.Enqueue("Combine UNP sequence files.");
            CombineUnpFastaFiles(updateDbAccessions);
            AppSettings.progressInfo.progStrQueue.Enqueue("Done!");
            
            AppSettings.progressInfo.progStrQueue.Enqueue("Output PDB Sequences.");
            pdbSeqOutput.OutputPdbEntryEntitySequences (updateEntities);
            AppSettings.progressInfo.progStrQueue.Enqueue("Done!");

            DbBuilder.dbConnect.DisconnectFromDatabase();
            AppSettings.progressInfo.threadFinished = true;
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

            if (!Directory.Exists(AppSettings.dirSettings.uniprotPath))
            {
                Directory.CreateDirectory(AppSettings.dirSettings.uniprotPath);
            }

            AppSettings.progressInfo.ResetCurrentProgressInfo();
            AppSettings.progressInfo.currentOperationLabel = "Retrieve UNP sequences";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryEntities"></param>
        public string[] RetrieveUnpSequences(string[] entryEntities, bool isUpdate)
        {
            StreamWriter logWriter = new StreamWriter(Path.Combine (AppSettings.dirSettings.uniprotPath, "UndefinedUniprotInPdbLog.txt"), true);
            logWriter.WriteLine(DateTime.Today.ToShortTimeString ());
            string pdbSequenceFile = "";
            if (isUpdate)
            {
                pdbSequenceFile = "NewPdbSequenceWithoutUnp.txt";
            }
            else
            {
                pdbSequenceFile = "PdbSequenceWithoutUnp.txt";
            }

            StreamWriter pdbSequenceWriter = new StreamWriter(Path.Combine (AppSettings.dirSettings.uniprotPath, pdbSequenceFile));

            AppSettings.progressInfo.totalStepNum = entryEntities.Length;
            AppSettings.progressInfo.totalOperationNum = entryEntities.Length;

            string pdbId = "";
            int entityId = -1;
            string unpFastaFile = "";
            string pdbSequence = "";
            ArrayList dbAccessionList = new ArrayList();
            foreach (string entryEntity in entryEntities)
            {
                pdbId = entryEntity.Substring (0, 4);
                entityId = Convert.ToInt32(entryEntity.Substring (4, entryEntity.Length - 4));

                AppSettings.progressInfo.currentOperationNum++;
                AppSettings.progressInfo.currentStepNum++;
                AppSettings.progressInfo.currentFileName = pdbId + entityId;

                string[] dbAccessions = GetUnpAccessionsFromSifts(pdbId, entityId);
                if (dbAccessions != null && dbAccessions.Length == 0)
                {
                    logWriter.WriteLine(pdbId + entityId.ToString() + " no UNP mapping.");
                    logWriter.Flush();
                    pdbSequence = GetEntryEntitySequence(pdbId, entityId);
                    if (IsValidSequence(pdbSequence))
                    {
                        pdbSequenceWriter.WriteLine(">" + entryEntity);
                        pdbSequenceWriter.WriteLine(pdbSequence);
                        pdbSequenceWriter.Flush();
                    }
                }
                foreach (string dbAccession in dbAccessions)
                {
                    unpFastaFile = Path.Combine(AppSettings.dirSettings.uniprotPath + "\\UnpSequencesInPdb", dbAccession + ".fasta");
                    if (!isUpdate)
                    {
                        if (File.Exists(unpFastaFile))
                        {
                            continue;
                        }
                    }
                    if (isUpdate)
                    {
                        if (!dbAccessionList.Contains(dbAccession))
                        {
                            dbAccessionList.Add(dbAccession);
                        }
                    }
                    try
                    {
                        //   Thread.Sleep(1000);

                        webClient.DownloadFile(httpAddress + dbAccession + ".fasta", unpFastaFile);
                        FileInfo fileInfo = new FileInfo(unpFastaFile);
                        if (fileInfo.Length == 0)
                        {
                            logWriter.WriteLine(dbAccession + " file size is 0  for " + pdbId + entityId.ToString());
                            logWriter.Flush();
                            File.Delete(unpFastaFile);
                            pdbSequence = GetEntryEntitySequence(pdbId, entityId);
                            if (IsValidSequence(pdbSequence))
                            {
                                pdbSequenceWriter.WriteLine(">" + entryEntity);
                                pdbSequenceWriter.WriteLine(pdbSequence);
                                pdbSequenceWriter.Flush();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine(dbAccession + " : Cannot find in the UniProt: " + ex.Message);
                        logWriter.Flush();
                    }
                }
            }
            logWriter.Close();
            pdbSequenceWriter.Close();
            DbBuilder.dbConnect.DisconnectFromDatabase();
            AppSettings.progressInfo.progStrQueue.Enqueue("Done!");
  //          AppSettings.progressInfo.threadFinished = true;
            string[] downloadDbAccessions = new string[dbAccessionList.Count];
            dbAccessionList.CopyTo(downloadDbAccessions);
            return downloadDbAccessions;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string GetEntryEntitySequence(string pdbId, int entityId)
        {
            string queryString = string.Format("Select Sequence From AsymUnit Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable sequenceTable = dbQuery.Query(queryString);
            string sequence = sequenceTable.Rows[0]["Sequence"].ToString().TrimEnd();
            return sequence;
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
            if (dbAccTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Distinct DbAccession From PdbDbRefXml " + 
                    " Where PdbID = '{0}' AND EntityID = {1} AND DbName = 'UNP' ORDER BY RefID;", 
                    pdbId, entityId);
                dbAccTable = dbQuery.Query(queryString);
            }
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
        #endregion

        #region update entries
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetUpdateEntries()
        {
            StreamReader dataReader = null; 
            string line = "";
            ArrayList entryList = new ArrayList();
            dataReader = new StreamReader(Path.Combine(AppSettings.dirSettings.xmlPath, "newls-pdb.txt"));
            while ((line = dataReader.ReadLine ()) != null)
            {
                entryList.Add(line.Substring (0, 4));
            }
            dataReader.Close();
            
            dataReader = new StreamReader(Path.Combine (AppSettings.dirSettings.siftsPath, "newls-sifts.txt"));
            while ((line = dataReader.ReadLine()) != null)
            {
                if (!entryList.Contains(line.Substring(0, 4)))
                {
                    entryList.Add(line.Substring (0, 4));
                }
            }
            dataReader.Close();

            string[] updateEntries = new string[entryList.Count];
            entryList.CopyTo(updateEntries);
            return updateEntries;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="lastDt"></param>
        /// <returns></returns>
        private string[] GetUpdateEntries(DateTime lastDt)
        {
            string[] xmlFiles = Directory.GetFiles(AppSettings.dirSettings.xmlPath);
            ArrayList entryList = new ArrayList();
            string entry = "";
            foreach (string xmlFile in xmlFiles)
            {
                FileInfo fileInfo = new FileInfo(xmlFile);
                if (DateTime.Compare(fileInfo.CreationTime, lastDt) > 0)
                {
                    entry = GetEntryName(xmlFile);
                    entryList.Add(entry);
                }
            }
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private string GetEntryName(string fileName)
        {
            int fileIndex = fileName.LastIndexOf("\\");
            string entryName = fileName.Substring(fileIndex + 1, 4);
            return entryName;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        private bool IsValidSequence(string sequence)
        {
            if (sequence.Length < minSeqLen)
            {
                return false;
            }
            int numOfXs = 0;
            foreach (char ch in sequence)
            {
                if (ch == 'X')
                {
                    numOfXs++;
                }
            }
            double coverage = (double)numOfXs / (double)sequence.Length;
            if (coverage > 0.9) // most of residues are X
            {
                return false;
            }
            if (numOfXs == 1) // only one aa residue
            {
                return false;
            }
            return true;
        }
        #endregion

        #region combine unp sequence files into one file
        /// <summary>
        /// 
        /// </summary>
        public void CombineUnpFastaFiles()
        {
            string[] fastaFiles = Directory.GetFiles(AppSettings.dirSettings.uniprotPath, "*.fasta");
            StreamWriter dataWriter = new StreamWriter(Path.Combine (AppSettings.dirSettings.uniprotPath, "UnpSequencesInPdb.txt"));
            string line = "";
            string dbAccession = "";
            foreach (string fastaFile in fastaFiles)
            {
                dbAccession = GetDbAccFromFullFileName(fastaFile);
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

        /// <summary>
        /// 
        /// </summary>
        public void CombineUnpFastaFiles(string[] updateDbAccessions)
        {
            StreamWriter dataWriter = new StreamWriter(Path.Combine (AppSettings.dirSettings.uniprotPath, "newUnpSequencesInPdb.txt"));
            string line = "";
            string fastaFile = "";
            StreamReader dataReader = null;
            foreach (string dbAccession in updateDbAccessions)
            {
                fastaFile = string.Format (Path.Combine (AppSettings.dirSettings.uniprotPath, dbAccession + ".fasta"));
                if (File.Exists(fastaFile))
                {
                    dataReader = new StreamReader(fastaFile);
                    while ((line = dataReader.ReadLine()) != null)
                    {
                        dataWriter.WriteLine(line);
                    }
                    dataReader.Close();
                    dataWriter.Flush();
                }
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fastaFile"></param>
        /// <returns></returns>
        private string GetDbAccFromFullFileName(string fastaFile)
        {
            int exeIndex = fastaFile.IndexOf(".fasta");
            int lastDashIndex = fastaFile.LastIndexOf("\\");
            return fastaFile.Substring(lastDashIndex + 1, exeIndex - lastDashIndex - 1);
        }
        #endregion

        #region output obsolete UNP codes in SIFTs
        public void PrintObsoleteUnpInSifts()
        {
            Initialize();

            StreamReader dataReader = new StreamReader(@"E:\DbProjectData\pfam\PfamHmmer\sequenceFileList\UndefinedUniprotInPdbLog.txt");
            StreamWriter dataWriter = new StreamWriter("PdbSequencesWithObsoleteUnpInSifts.txt");
            string line = "";
            string entryEntity = "";
            string pdbSequence = "";
            string dbAccession = "";
            string releaseFileDate = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("file size is 0") > -1)
                {
                    string[] fields = line.Split (' ');
                    dbAccession = fields[0];
                    entryEntity = fields[fields.Length - 1];
                    pdbSequence = GetEntryEntitySequence(entryEntity.Substring(0, 4),
                        Convert.ToInt32(entryEntity.Substring (4, entryEntity.Length - 4)));
                    releaseFileDate = GetEntryReleasefileDate(entryEntity.Substring(0, 4));
                    dataWriter.WriteLine(entryEntity + "\t" + dbAccession + "\t" + releaseFileDate + "\t" + pdbSequence);
                }
            }
            dataReader.Close();
            dataWriter.Close();
        }

        private string GetEntryReleasefileDate(string pdbId)
        {
            string queryString = string.Format("Select ReleaseFileDate From PdbEntry Where PdbID = '{0}';", pdbId);
            DataTable fileDateTable = dbQuery.Query(queryString);
            return fileDateTable.Rows[0]["ReleaseFileDate"].ToString().TrimEnd();
        }
        #endregion
    }
}
