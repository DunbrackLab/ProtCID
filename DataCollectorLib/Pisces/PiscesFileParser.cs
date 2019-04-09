using System;
using System.Collections;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;

namespace DataCollectorLib.Pisces
{
    /// <summary>
    /// Parse PISCES data files
    /// use HHalignments from PISCES, psiblast and ce alignments are obsolete
    /// parse pdbaa.nr file only, January 2016
    /// </summary>
    public class PiscesFileParser
    {
        #region member variable
        private DbInsert dbInsert = new DbInsert();
        private DbQuery dbQuery = new DbQuery();
        private DbUpdate dbUpdate = new DbUpdate();
     //   private DbConnect AppSettings.ProtCidSettings.alignmentDbConnectionion = new DbConnect();
        private const int stepInterval = 60000;
        private AsuInfoFinder asymChainFinder = null;
        public string srcPath = "http://dunbrack.fccc.edu//Guoli/culledpdb/";
        #endregion

        public PiscesFileParser()
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }

            if (ProtCidSettings.alignmentDbConnection == null)
            {
                ProtCidSettings.alignmentDbConnection = new DbConnect();
            }
            if (ProtCidSettings.alignmentDbConnection.ConnectString == "")
            {
                ProtCidSettings.alignmentDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;" +
                "UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" + ProtCidSettings.dirSettings.alignmentDbPath;
            }

            asymChainFinder = new AsuInfoFinder(ProtCidSettings.dirSettings.pdbfamDbPath);
        }

       
        /// <summary>
        /// 
        /// </summary>
        public void ProcessPdbaanrData()
        {
            string webfileName = "pdbaanr.gz";
            // Create an instance of web client.					
            WebClient webClient = new WebClient();
            string destPath = ProtCidSettings.dirSettings.piscesPath;
            webClient.DownloadFile(srcPath + webfileName, Path.Combine (destPath, webfileName));

            PiscesDataTables.InitializeTables();

            bool isUpdate = false;
        
            char delimiter = ' ';
            string pdbaanrFile = Path.Combine(destPath, "pdbaa.nr");
            ParseHelper.UnZipFile(Path.Combine(destPath, webfileName));


            string[] obsRepChains = ProcessPdbaanrFile(pdbaanrFile, delimiter, isUpdate);
            // remove obsolete entry chains from alignmnets tables
            if (obsRepChains != null)
            {
                RemoveObsDataFromAlignments(obsRepChains);
            }

            UpdateAsymChainIDs();
        }    


        # region pdbaanr parse
        /// <summary>
        /// parse pdbaa nr file and insert data to database table
        /// set progress information
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="delimiter"></param>
        /// <returns></returns>
        public string[]  ProcessPdbaanrFile(string fileName, char delimiter, bool isUpdate)
        {
            int fileNameIndex = fileName.LastIndexOf("\\");
            ProtCidSettings.progressInfo.currentFileName = fileName.Substring(fileNameIndex + 1,
                fileName.Length - fileNameIndex - 1);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Processing PDBaaNR";
            ProtCidSettings.progressInfo.totalOperationNum = stepInterval;
            ProtCidSettings.progressInfo.totalStepNum = stepInterval;

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Parsing PDB aa nr file ...");
            string[] obsRepChains = null;
            try
            {
                obsRepChains = ParsePdbaanrFile(fileName, delimiter, isUpdate);
            }
            catch (Exception exception)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Parsing Pdb aa nr file Errors: " + exception.Message);
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            return obsRepChains;
        }

        /// <summary>
        /// pare pdbaa nr file, split 1d1wa into pdb code and chain
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="delimiter"></param>
        private string[] ParsePdbaanrFile(string fileName, char delimiter, bool isUpdate)
        {
            string sequence = "";
            string pdbId1 = "";
            string chainId1 = "";
            string redundantChains = "";
            DataTable origPdbaaTable = null;
            ArrayList obsRepEntryChainList = null;
            if (isUpdate)
            {
                /* used to recycle these asymmetric chains and entity IDs info
                 * since it is time-consuming to retrieve it and some of them are manually checked
                 * But there is small chance that the PDB author chains are changed for some entries
                */
                origPdbaaTable = GetDbPdbaaTable();

                obsRepEntryChainList = GetRepChains(origPdbaaTable);

                // delete data from the table
                DeleteDbPdbaaData ();
            }
            try
            {
                // Create an instance of StreamReader to read from a file.
                // The using statement also closes the StreamReader.
                using (StreamReader dataReader = new StreamReader(fileName))
                {
                    string line;

                    // Read lines from the file until the end of 
                    // the file is reached.
                    while ((line = dataReader.ReadLine()) != null)
                    {
                        if (line == "") // end of one entry
                        {
                            if (sequence != "")
                            {
                                ProtCidSettings.progressInfo.currentStepNum++;
                                ProtCidSettings.progressInfo.currentOperationNum++;
                                if (isUpdate)
                                {
                                    // if it still representative chain
                                    obsRepEntryChainList.Remove(pdbId1 + chainId1);
                                }
                                if (redundantChains.IndexOf(",") > -1)
                                {
                                    AddPdbaaDataToTables(pdbId1, chainId1, redundantChains, origPdbaaTable);
                                    dbInsert.InsertDataIntoDBtables(ProtCidSettings.alignmentDbConnection, PiscesDataTables.piscesDataTables[PiscesDataTables.RedundantPdbChains]);
                                    PiscesDataTables.piscesDataTables[PiscesDataTables.RedundantPdbChains].Clear();
                                }
                                sequence = "";
                                if (ProtCidSettings.progressInfo.currentStepNum % stepInterval == 0)
                                {
                                    ProtCidSettings.progressInfo.totalStepNum += stepInterval;
                                    ProtCidSettings.progressInfo.currentOperationNum = 0;
                                }
                            }
                            continue;
                        }
                        if (line[0] == '>')
                        {
                            // remove >
                            line = line.Remove(0, 1);
                            string[] items = line.Split();
                            string pdbIdChain1 = items[0];
                            // pdb id must be 4 characters long
                            pdbId1 = pdbIdChain1.Substring(0, 4).ToLower();
                            chainId1 = pdbIdChain1.Substring(4, pdbIdChain1.Length - 4);
                            int redunChainStartIndex = line.IndexOf("||");
                            if (redunChainStartIndex > -1)
                            {
                                redundantChains = line.Substring(redunChainStartIndex + 2, line.Length - redunChainStartIndex - 2);
                                redundantChains = redundantChains.Trim(); // remove space at begining and end of the string
                            }
                            else
                            {
                                redundantChains = "";
                            }
                        }
                        else
                        {
                            sequence += line;
                        }
                    }
                    ProtCidSettings.progressInfo.currentStepNum++;
                    ProtCidSettings.progressInfo.currentOperationNum++;
                    // add the last one
                    if (isUpdate)
                    {
                   //     int repIdx = obsRepEntryChainList.BinarySearch(pdbId1 + chainId1);
                        obsRepEntryChainList.Remove(pdbId1 + chainId1);
                    }
                    if (redundantChains.IndexOf(",") > -1)
                    {
                        AddPdbaaDataToTables(pdbId1, chainId1, redundantChains, origPdbaaTable);
                        dbInsert.InsertDataIntoDBtables(ProtCidSettings.alignmentDbConnection, PiscesDataTables.piscesDataTables[PiscesDataTables.RedundantPdbChains]);
                        PiscesDataTables.piscesDataTables[PiscesDataTables.RedundantPdbChains].Clear();
                    }
                    ProtCidSettings.progressInfo.currentOperationNum = ProtCidSettings.progressInfo.totalOperationNum;
                    ProtCidSettings.progressInfo.totalStepNum = ProtCidSettings.progressInfo.currentStepNum;
                }
            }
            catch (Exception exception)
            {
                throw exception;
            }
        /*    finally
            {
               File.Delete(fileName);
            }*/
#if DEBUG
            if (obsRepEntryChainList != null)
            {
                StreamWriter dataWriter = new StreamWriter("obsRepChainList.txt");
                foreach (string obsRepChain in obsRepEntryChainList)
                {
                    dataWriter.WriteLine(obsRepChain);
                }
                dataWriter.Close();
            }
#endif
            // used to remove the obsolete data from alignments table
            string[] obsRepChains = null;
            if (obsRepEntryChainList != null)
            {
                obsRepChains = new string[obsRepEntryChainList.Count];
                obsRepEntryChainList.CopyTo(obsRepChains);
            }
            return obsRepChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="chainId1"></param>
        /// <param name="redundantChains"></param>
        private void AddPdbaaDataToTables(string pdbId1, string chainId1, string redundantChains, DataTable origPdbaaTable)
        {
            string asymChain1 = "";
            int entityId1 = -1;
            string asymChain2 = "";
            int entityId2 = -1;
            // no redundant pdb chains
            if (redundantChains == "")
            {
                DataRow redunRow = PiscesDataTables.piscesDataTables[PiscesDataTables.RedundantPdbChains].NewRow();
                redunRow["PdbID1"] = pdbId1;
                redunRow["ChainID1"] = chainId1;
                GetAsymChainEntityInfo(pdbId1, chainId1, origPdbaaTable, out asymChain1, out entityId1);
                redunRow["AsymChainID1"] = asymChain1;
                redunRow["EntityID1"] = entityId1;

                redunRow["PdbID2"] = "-";
                redunRow["ChainID2"] = "-";
                redunRow["AsymChainID2"] = "-";
                redunRow["EntityID2"] = -1;
                PiscesDataTables.piscesDataTables[PiscesDataTables.RedundantPdbChains].Rows.Add(redunRow);
            }
            else
            {
          //      string[] entryChains = redundantChains.Split();
                string[] entryChains = redundantChains.Split(',');
                if (entryChains.Length == 0)
                {
                    entryChains = redundantChains.Split();
                }
                GetAsymChainEntityInfo(pdbId1, chainId1, origPdbaaTable, out asymChain1, out entityId1);

                foreach (string entryChain in entryChains)
                {
                    string pdbId2 = entryChain.Substring(0, 4).ToLower();
                    string chainId2 = entryChain.Substring(4, entryChain.Length - 4);
                    GetAsymChainEntityInfo(pdbId2, chainId2, origPdbaaTable, out asymChain2, out entityId2);

                    DataRow redunRow = PiscesDataTables.piscesDataTables[PiscesDataTables.RedundantPdbChains].NewRow();
                    redunRow["PdbID1"] = pdbId1;
                    redunRow["ChainID1"] = chainId1;
                    redunRow["AsymChainID1"] = asymChain1;
                    redunRow["EntityID1"] = entityId1;
                    redunRow["PdbID2"] = pdbId2;
                    redunRow["ChainID2"] = chainId2;
                    redunRow["AsymChainID2"] = asymChain2;
                    redunRow["EntityID2"] = entityId2;

                    PiscesDataTables.piscesDataTables[PiscesDataTables.RedundantPdbChains].Rows.Add(redunRow);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <param name="pdbaaTable"></param>
        /// <param name="asymChain"></param>
        /// <param name="entityId"></param>
        private void GetAsymChainEntityInfo(string pdbId, string authChain, DataTable pdbaaTable, 
            out string asymChain, out int entityId)
        {
            asymChain = "-";
            entityId = -1;
            if (pdbaaTable != null)
            {
                DataRow[] pdbaaRows = pdbaaTable.Select(string.Format("PdbID1 = '{0}' AND ChainID1 = '{1}'", pdbId, authChain));
                if (pdbaaRows.Length > 0)
                {
                    asymChain = pdbaaRows[0]["AsymChainID1"].ToString().TrimEnd();
                    entityId = Convert.ToInt16(pdbaaRows[0]["EntityID1"].ToString());
                }
                else
                {
                    pdbaaRows = pdbaaTable.Select(string.Format("PdbID2 = '{0}' AND ChainID2 = '{1}'", pdbId, authChain));
                    if (pdbaaRows.Length > 0)
                    {
                        asymChain = pdbaaRows[0]["AsymChainID2"].ToString().TrimEnd();
                        entityId = Convert.ToInt16(pdbaaRows[0]["EntityID2"].ToString());
                    }
                }
            }
        }
        #endregion

        #region Update Asymmetric Chain ID
        /// <summary>
        /// add asymmetric chains to Pisces tables
        /// </summary>
        public void AddAsymChainIDs()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Add asymmetric chain IDs";

            // get asymchain and entity info from the protbud database
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving asymmetric chain IDs and Entity IDs From ProtBUD database. Please wait.");
            DataTable asymChainTable = asymChainFinder.GetProtAsuInfoTable();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Data Retrieving done! Disconnect ProtBuD.");

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve entry without asymmetric chain info from redundant pdb chains table.");
            string queryString = "Select * From RedundantPdbChains;";
            DataTable redundantChainTable = dbQuery.Query(ProtCidSettings.alignmentDbConnection, queryString);
            ArrayList entryList = new ArrayList();

            string pdbId1 = "";
            string pdbId2 = "";
            string asymChain1 = "";
            string asymChain2 = "";
            foreach (DataRow dRow in redundantChainTable.Rows)
            {
                pdbId1 = dRow["PdbId1"].ToString();
                asymChain1 = dRow["AsymChainID1"].ToString().TrimEnd();
                if (asymChain1 == "-")
                {
                    if (!entryList.Contains(pdbId1))
                    {
                        entryList.Add(pdbId1);
                    }
                }
                pdbId2 = dRow["PdbId2"].ToString();
                if (pdbId2 == "-" || pdbId2 == pdbId1)
                {
                    continue;
                }
                asymChain2 = dRow["AsymChainID2"].ToString().TrimEnd();
                if (asymChain2 == "-")
                {
                    if (!entryList.Contains(pdbId2))
                    {
                        entryList.Add(pdbId2);
                    }
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Adding asymmetric chain IDs and entity IDs.");
            ProtCidSettings.progressInfo.totalOperationNum = entryList.Count;
            ProtCidSettings.progressInfo.totalStepNum = entryList.Count;
            
            foreach (string pdbId in entryList)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                DataRow[] dataRows = new DataRow[redundantChainTable.Rows.Count];
                redundantChainTable.Rows.CopyTo(dataRows, 0);
                string[] authChains = GetAuthChains(dataRows, pdbId);
                string[] asymChainEntityInfos = asymChainFinder.FindProtAsymChainEntity (asymChainTable, pdbId, authChains);
                UpdateAsymChainsInDb(pdbId, authChains, asymChainEntityInfos);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetAuthChains(string pdbId)
        {
            string queryString = string.Format("Select * From RedundantPdbChains " + 
                " WHere PdbID1 = '{0}' OR PdbID2 = '{0}';", pdbId);
            DataTable redundantChainTable = dbQuery.Query(ProtCidSettings.alignmentDbConnection, queryString);

            DataRow[] dataRows = new DataRow[redundantChainTable.Rows.Count];
            redundantChainTable.Rows.CopyTo(dataRows, 0);
            string[] authChains = GetAuthChains(dataRows, pdbId);

            return authChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="redundantRows"></param>
        /// <returns></returns>
        private string[] GetAuthChains(DataRow[] redundantRows, string pdbId)
        {
            ArrayList authChainList = new ArrayList();
            string chainId1 = "";
            string chainId2 = "";
            foreach (DataRow dRow in redundantRows)
            {
                chainId1 = dRow["ChainID1"].ToString().TrimEnd();
                if (dRow["PdbID1"].ToString () == pdbId && 
                    ! authChainList.Contains(chainId1))
                {
                    authChainList.Add(chainId1);
                }
                chainId2 = dRow["ChainID2"].ToString().TrimEnd();
                if (chainId2 != "-")
                {
                    if (dRow["PdbID2"].ToString () == pdbId && !authChainList.Contains(chainId2))
                    {
                        authChainList.Add(chainId2);
                    }
                }
            }
            string[] authChains = new string[authChainList.Count];
            authChainList.CopyTo(authChains);
            return authChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryAuthChains(string pdbId)
        {
            ArrayList authChainList = new ArrayList();

            string queryString = string.Format("Select * From RedundantPDbChains " +
                " Where PdbID1 = '{0}' OR (PdbID2 = '{0}');", pdbId);
            DataTable authainTable = dbQuery.Query(ProtCidSettings.alignmentDbConnection, queryString);

            string chainPdbId = "";
            string authChain = "";
            foreach (DataRow reduntChainRow in authainTable.Rows)
            {
                chainPdbId = reduntChainRow["PdbID1"].ToString();
                authChain = reduntChainRow["ChainID1"].ToString();
                if (chainPdbId == pdbId)
                {
                    if (!authChainList.Contains(authChain))
                    {
                        authChainList.Add(authChain);
                    }
                }
                chainPdbId = reduntChainRow["PdbID2"].ToString();
                authChain = reduntChainRow["ChainID2"].ToString();
                if (chainPdbId == pdbId)
                {
                    if (!authChainList.Contains(authChain))
                    {
                        authChainList.Add(authChain);
                    }
                }
            }
            string[] authChains = new string[authChainList.Count];
            authChainList.CopyTo(authChains);
            return authChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <param name="entryChainAsymchainHash"></param>
        private string GetAsymChainFromRedundantPdbChains(string pdbId, string authChain, ref Hashtable entryChainAsymchainHash)
        {
            string asymChain = "-";
            if (entryChainAsymchainHash.ContainsKey(pdbId + authChain))
            {
                asymChain = (string)entryChainAsymchainHash[pdbId + authChain];
            }
            else
            {
                asymChain = GetAsymChainFromRedundantPdbChains(pdbId, authChain);
                entryChainAsymchainHash.Add(pdbId + authChain, asymChain);
            }
            return asymChain;
        }

        private string GetAsymChainFromRedundantPdbChains(string pdbId, string authChain)
        {
            string asymChain = "-";
            string queryString = string.Format("Select * From RedundantPdbChains " +
                    " Where PdbID1 = '{0}' AND ChainID1 = '{1}';", pdbId, authChain);
            DataTable asymChainTable = dbQuery.Query(ProtCidSettings.alignmentDbConnection, queryString);
            if (asymChainTable.Rows.Count > 0)
            {
                asymChain = asymChainTable.Rows[0]["AsymChainID1"].ToString().TrimEnd();
            }
            return asymChain;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChains"></param>
        /// <param name="asymChains"></param>
        private void UpdateAsymChainsInDb(string pdbId, string[] authChains, string[] asymChainEntityInfos)
        {
            string updateString = "";
            string[] asymChainEntity = null;
            for (int i = 0; i < authChains.Length; i++)
            {
                asymChainEntity = asymChainEntityInfos[i].Split(',');
                // add asymmetric chain IDs and Entity IDs in RedundantPdbChains
                updateString = string.Format("Update RedundantPdbCHains Set AsymChainID1 = '{0}', EntityID1 = {1}" +
                    " Where PdbID1 = '{2}' AND ChainID1 = '{3}';",
                    asymChainEntity[0], asymChainEntity[1], pdbId, authChains[i]);
                dbQuery.Query(ProtCidSettings.alignmentDbConnection, updateString);

                updateString = string.Format("Update RedundantPdbCHains Set AsymChainID2 = '{0}', EntityID2 = {1} " +
                    " Where PdbID2 = '{2}' AND ChainID2 = '{3}';",
                    asymChainEntity[0], asymChainEntity[1], pdbId, authChains[i]);
                dbQuery.Query(ProtCidSettings.alignmentDbConnection, updateString);
            }
        }

        #region update asymmetric chain IDs
        /// <summary>
        /// 
        /// </summary>
        public void UpdateAsymChainIDs()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Add asymmetric chain IDs";

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get entries and chains without asymmetric chain IDs.");
            Hashtable updateEntryChainHash = GetUpdateEntryChainHash();
            Hashtable missingEntryChainHash = GetMissEntryChainHash();
            foreach (string entry in missingEntryChainHash.Keys)
            {
                if (updateEntryChainHash.ContainsKey(entry))
                {
                    continue;
                }
                updateEntryChainHash.Add(entry, missingEntryChainHash[entry]);
            }
     /*       Hashtable updateEntryChainHash = GetLowerCaseAuthChainHash();
*/
            // get asymchain and entity info from the protbud database
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving asymmetric chain IDs and Entity IDs From ProtBUD database. Please wait.");

            ArrayList updateEntryList = new ArrayList(updateEntryChainHash.Keys);
            string[] updateEntries = new string[updateEntryList.Count];
            updateEntryList.CopyTo(updateEntries);
            DataTable asymChainTable = asymChainFinder.GetProtAsuInfoTable(updateEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Adding asymmetric chain IDs and entity IDs.");
            ProtCidSettings.progressInfo.totalOperationNum = updateEntryChainHash.Count;
            ProtCidSettings.progressInfo.totalStepNum = updateEntryChainHash.Count;

            foreach (string pdbId in updateEntries)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                string[] authChains = (string[])updateEntryChainHash[pdbId];
                string[] asymChainEntityInfos = asymChainFinder.FindProtAsymChainEntity(asymChainTable, pdbId, authChains);
                UpdateAsymChainsInDb(pdbId, authChains, asymChainEntityInfos);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.progressInfo.threadFinished = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Hashtable GetMissEntryChainHash()
        {
            Hashtable missEntryChainHash = new Hashtable();
           
            string queryString = "Select Distinct PdbID1 AS PdbID, ChainID1 AS ChainID From RedundantPDbChains " +
                " Where AsymChainID1 = '-';";
            DataTable missRepChainTable = dbQuery.Query(ProtCidSettings.alignmentDbConnection, queryString);
            AddMissEntryChainToHash(missRepChainTable, ref missEntryChainHash);

            queryString = "Select PdbID2 As PdbID, ChainID2 As ChainID From RedundantPDbChains " + 
                "Where PDBID2 <> '-' AND AsymChainID2 = '-';";
            DataTable missReduntChainTable = dbQuery.Query(ProtCidSettings.alignmentDbConnection, queryString);
            AddMissEntryChainToHash(missReduntChainTable, ref missEntryChainHash);
            ArrayList entryList = new ArrayList(missEntryChainHash.Keys);
            foreach (string entry in entryList)
            {
                ArrayList chainList = (ArrayList)missEntryChainHash[entry];
                string[] chains = new string[chainList.Count];
                chainList.CopyTo(chains);
                missEntryChainHash[entry] = chains;
            }
            return missEntryChainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Hashtable GetUpdateEntryChainHash ()
        {
         //   string[] updateEntries = GetUpdateEntries();
            string[] updateEntries = GetLowerCaseEntries();
            Hashtable updateEntryChainHash = new Hashtable();
            foreach (string pdbId in updateEntries)
            {
                string[] authChains = GetEntryAuthChains(pdbId);
                updateEntryChainHash.Add(pdbId, authChains);
            }
            return updateEntryChainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetUpdateEntries()
        {
            string updatePdbEntryFile = Path.Combine(ProtCidSettings.dirSettings.xmlPath, "newls-pdb.txt");
            ArrayList entryList = new ArrayList();
            StreamReader dataReader = new StreamReader(updatePdbEntryFile);
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (! entryList.Contains(line.Substring(0, 4)))
                {
                    entryList.Add(line.Substring(0, 4));
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
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetUpdateEntryAuthChains(string pdbId)
        {
            ArrayList authChainList = new ArrayList();
            string queryString = string.Format("Select Distinct ChainID1 AS ChainID " +
                " From RedundantPDbChains Where PdbID1 = '{0}';", pdbId);
            DataTable updateAuthChainTable = dbQuery.Query(ProtCidSettings.alignmentDbConnection, queryString);
            foreach (DataRow authChainRow in updateAuthChainTable.Rows)
            {
                authChainList.Add(authChainRow["ChainID"].ToString ().TrimEnd ());
            }

            queryString = string.Format("Select Distinct ChainID2 AS ChainID " +
                " From RedundantPDbChains Where PdbID2 = '{0}';", pdbId);
            updateAuthChainTable = dbQuery.Query(ProtCidSettings.alignmentDbConnection, queryString);
            foreach (DataRow authChainRow in updateAuthChainTable.Rows)
            {
                authChainList.Add(authChainRow["ChainID"].ToString().TrimEnd());
            }

            string[] authChains = new string[authChainList.Count];
            authChainList.CopyTo(authChains);
            return authChains;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="missEntryChainTable"></param>
        /// <param name="missEntryChainHash"></param>
        private void AddMissEntryChainToHash(DataTable missEntryChainTable, ref Hashtable missEntryChainHash)
        {
            string pdbId = "";
            string authChain = "";
            foreach (DataRow dataRow in missEntryChainTable.Rows)
            {
                pdbId = dataRow["PdbID"].ToString();
                authChain = dataRow["ChainID"].ToString().TrimEnd();
                if (missEntryChainHash.Contains(pdbId))
                {
                    ArrayList chainList = (ArrayList)missEntryChainHash[pdbId];
                    chainList.Add(authChain);
                }
                else
                {
                    ArrayList chainList = new ArrayList();
                    chainList.Add(authChain);
                    missEntryChainHash.Add(pdbId, chainList);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Hashtable GetLowerCaseAuthChainHash()
        {
            string queryString = "Select Distinct PdbID1 As PdbID, ChainID1 As ChainID From RedundantPdbChains " +
                " Where ChainID1 in ('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', " +
               " 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z');";
            DataTable chainTable = dbQuery.Query(ProtCidSettings.alignmentDbConnection, queryString);
            queryString = "Select Distinct PdbID2 As PdbID, ChainID2 As ChainID From RedundantPdbChains " +
                " Where ChainID2 in ('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', " +
               " 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z');";
            DataTable chain2Table = dbQuery.Query(ProtCidSettings.alignmentDbConnection, queryString);

            foreach (DataRow chain2Row in chain2Table.Rows)
            {
                DataRow chainRow = chainTable.NewRow();
                chainRow.ItemArray = chain2Row.ItemArray;
                chainTable.Rows.Add(chainRow);
            }
            Hashtable lowerCaseChainHash = new Hashtable();
            AddMissEntryChainToHash(chainTable, ref lowerCaseChainHash);

            ArrayList entryList = new ArrayList(lowerCaseChainHash.Keys);
            foreach (string entry in entryList)
            {
                ArrayList chainList = (ArrayList)lowerCaseChainHash[entry];
                string[] chains = new string[chainList.Count];
                chainList.CopyTo(chains);
                lowerCaseChainHash[entry] = chains;
            }
            return lowerCaseChainHash;
        }

        private string[] GetLowerCaseEntries()
        {
            string queryString = "Select Distinct PdbID1 As PdbID From RedundantPdbChains " +
               " Where ChainID1 in ('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', " +
              " 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z');";
            DataTable entryTable = dbQuery.Query(ProtCidSettings.alignmentDbConnection, queryString);
            ArrayList entryList = new ArrayList();
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString ();
                if (! entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            queryString = "Select Distinct PdbID2 As PdbID From RedundantPdbChains " +
                " Where ChainID2 in ('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', " +
               " 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z');";
            entryTable = dbQuery.Query(ProtCidSettings.alignmentDbConnection, queryString);
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            string[] lowerCaseEntries = new string[entryList.Count];
            entryList.CopyTo(lowerCaseEntries);
            return lowerCaseEntries;
        }
        #endregion
        #endregion

        #region delete data 
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable GetDbPdbaaTable()
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Deleting the entries to be updated.");
            DeleteUpdateEntriesPdbaa();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Deletion done!");

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Save the current pdbaa data.");
            string queryString = "Select * From RedundantPdbChains;";
            DataTable pdbaaTable = dbQuery.Query(ProtCidSettings.alignmentDbConnection, queryString);
            // in case some erros happens in parsing PDBaaNr data, recover from the text file
            if (pdbaaTable.Rows.Count == 0)
            {
                ReadPdbaaDataFromFile(pdbaaTable);
            }
            else
            {
                WritePdbaaDataToFile(pdbaaTable);
            }
            pdbaaTable.CaseSensitive = true;
            return pdbaaTable;
        }

        /// <summary>
        /// 
        /// </summary>
        private void DeleteUpdateEntriesPdbaa()
        {
            string[] updateEntries = GetUpdateEntries();
            foreach (string pdbId in updateEntries)
            {
                DeleteUpdateEntryPdbaa(pdbId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteUpdateEntryPdbaa(string pdbId)
        {
            string deleteString = string.Format("Delete From RedundantPdbChains " + 
                " Where PdbID1 = '{0}' OR PdbID2 = '{0}';", pdbId);
            dbUpdate.Delete(ProtCidSettings.alignmentDbConnection, deleteString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private ArrayList GetRepChains(DataTable pdbaaTable)
        {
            string queryString = "Select Distinct PdbID1, ChainID1 From RedundantPdbChains;";
            DataTable repChainTable = dbQuery.Query(ProtCidSettings.alignmentDbConnection, queryString);
            ArrayList repChainList = new ArrayList();
            string repChain = "";
            if (repChainTable.Rows.Count > 0)
            {
#if DEBUG
                StreamWriter dataWriter = new StreamWriter("PdbaaRepChains.txt");
#endif
                foreach (DataRow repChainRow in repChainTable.Rows)
                {
                    repChain = repChainRow["PdbID1"].ToString() + repChainRow["ChainID1"].ToString().TrimEnd();
                    repChainList.Add(repChain);
#if DEBUG
                    dataWriter.WriteLine(repChain);
#endif
                }
#if DEBUG
                dataWriter.Close();
#endif
            }
            else
            {
                if (File.Exists("PdbaaRepChains.txt"))
                {
                    StreamReader dataReader = new StreamReader("PdbaaRepChains.txt");
                    string line = "";
                    while ((line = dataReader.ReadLine()) != null)
                    {
                        repChainList.Add(line);
                    }
                    dataReader.Close();
                }
                else
                {
#if DEBUG
                    StreamWriter dataWriter = new StreamWriter("PdbaaRepChains.txt");
#endif
                    ArrayList reversedRepChainList = new ArrayList();
                    foreach (DataRow dataRow in pdbaaTable.Rows)
                    {
                        repChain = dataRow["PdbID1"].ToString () + dataRow["ChainID1"].ToString ().TrimEnd ();
                        if (!reversedRepChainList.Contains(repChain))
                        {
                            // always put the first one, so that the Contains search save a lot of time
                            reversedRepChainList.Insert(0, repChain);
#if DEBUG
                            dataWriter.WriteLine(repChain);
#endif
                        }
                    }
#if DEBUG
                    dataWriter.Close();
#endif
                    for (int i = reversedRepChainList.Count - 1; i >= 0; i--)
                    {
                        repChainList.Add(reversedRepChainList[i]);
                    }
                }
            }
            return repChainList;
        }      

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obsRepChains"></param>
        private void RemoveObsDataFromAlignments(string[] obsRepChains)
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Delete Obsolete representative chains from alignments tables");
        //    string dbTableNameString = @"SELECT RDB$RELATION_NAME FROM RDB$RELATIONS WHERE RDB$SYSTEM_FLAG = 0;";
        //    DataTable tableNameTable = dbQuery.Query(ProtCidSettings.alignmentDbConnection, dbTableNameString);
       //     string[] piscesTableNames = {"CeAlignments", "FatcatAlignments", "PsiblastAlignments"};
            string[] piscesTableNames = {"FatcatAlignments"};
            foreach (string tableName in piscesTableNames)
            {
                ProtCidSettings.progressInfo.currentFileName = tableName;

                DeleteObsDataFromAlignmentTable(obsRepChains, tableName);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Obsolete data cleaning done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obsRepChains"></param>
        /// <param name="dbTable"></param>
        private void DeleteObsDataFromAlignmentTable(string[] obsRepChains, string dbTable)
        {
            string deleteString = "";
            foreach (string obsChain in obsRepChains)
            {
                deleteString = string.Format("Delete From {0} " + 
                    " Where (QueryEntry = '{1}' AND QueryChain = '{2}') OR " + 
                    " (HitEntry = '{1}' AND HitChain = '{2}');",
                    dbTable, obsChain.Substring (0, 4), obsChain.Substring (4, obsChain.Length - 4));
                dbQuery.Query(ProtCidSettings.alignmentDbConnection, deleteString);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void DeleteDbPdbaaData()
        {
            string deleteString = "Delete From RedundantPdbChains;";
            dbQuery.Query(ProtCidSettings.alignmentDbConnection, deleteString);
        }

        #region clear alignments data
        /// <summary>
        /// clear alignments data only for representative entry chains 
        /// from non-redundant pdb chains
        /// </summary>
        public void ClearAlignmentData()
        {
    //        ClearAlignmentDataInTable("PsiBlastAlignments");
    //        ClearAlignmentDataInTable("CeAlignments");
            ClearAlignmentDataInTable("FatcatAlignments");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableName"></param>
        private void ClearAlignmentDataInTable(string tableName)
        {
            string queryString = string.Format("Select Distinct QueryEntry, QueryChain From {0};", tableName);
            DataTable queryChainTable = dbQuery.Query(ProtCidSettings.alignmentDbConnection, queryString);
            string pdbId = "";
            string chainId = "";
            string deleteString = "";
            foreach (DataRow queryChainRow in queryChainTable.Rows)
            {
                pdbId = queryChainRow["QueryEntry"].ToString();
                chainId = queryChainRow["QueryChain"].ToString().TrimEnd();
                if (! IsChainRepresentative(pdbId, chainId))
                {
                    deleteString = string.Format("Delete From {0} " + 
                        " Where QueryEntry = '{1}' AND QueryChain = '{2}';", 
                        tableName, pdbId, chainId);
                    dbQuery.Query(ProtCidSettings.alignmentDbConnection, deleteString);
                }
            }

            queryString = string.Format("Select Distinct HitEntry, HitChain From {0};", tableName);
            queryChainTable = dbQuery.Query(ProtCidSettings.alignmentDbConnection, queryString);
            foreach (DataRow queryChainRow in queryChainTable.Rows)
            {
                pdbId = queryChainRow["HitEntry"].ToString();
                chainId = queryChainRow["HitChain"].ToString().TrimEnd();
                if (!IsChainRepresentative(pdbId, chainId))
                {
                    deleteString = string.Format("Delete From {0} " +
                        " Where HitEntry = '{1}' AND HitChain = '{2}';",
                        tableName, pdbId, chainId);
                    dbQuery.Query(ProtCidSettings.alignmentDbConnection, deleteString);
                }
            }
        }

        /// <summary>
        /// check if the chain is the representative chain
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainId"></param>
        /// <returns></returns>
        private bool IsChainRepresentative(string pdbId, string chainId)
        {
            string queryString = string.Format("Select * From RedundantPdbChains " + 
                " Where PdbID1 = '{0}' AND ChainID1 = '{1}';", pdbId, chainId);
            DataTable repChainTable = dbQuery.Query(ProtCidSettings.alignmentDbConnection, queryString);
            if (repChainTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        #endregion
        #endregion

        #region last collect time
        /// <summary>
        /// the last date collecting PISCES data
        /// </summary>
        /// <returns></returns>
        private string GetTheLastCollectTime()
        {
            string[] psiblastAlignFiles = Directory.GetFiles(ProtCidSettings.dirSettings.piscesPath, "newpsiblast*");
            DateTime lastCollectTime = new DateTime ();
            foreach (string psiblastFile in psiblastAlignFiles)
            {
                FileInfo fileInfo = new FileInfo(psiblastFile);
                if (lastCollectTime == null)
                {
                    lastCollectTime = fileInfo.LastWriteTime;
                }
                else
                {
                    int dtComp = DateTime.Compare(fileInfo.LastWriteTime, lastCollectTime);
                    if (dtComp > 0)
                    {
                        lastCollectTime = fileInfo.LastWriteTime;
                    }
                }
            }
          /*  string lastCollectDateString = lastCollectTime.Month.ToString() + "-" +
                lastCollectTime.Day.ToString() + "-" + lastCollectTime.Year.ToString();
            return lastCollectDateString;*/
            return lastCollectTime.ToString("yyyy-MM-dd");
        }
        #endregion

        #region save PDBaa data
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbaaTable"></param>
        private void WritePdbaaDataToFile(DataTable pdbaaTable)
        {
            StreamWriter dataWriter = new StreamWriter("pdbaa.txt");
            string line = "";
            foreach (DataRow dataRow in pdbaaTable.Rows)
            {
                line = "";
                foreach (object item in dataRow.ItemArray)
                {
                    line += (item.ToString() + ",");
                }
                dataWriter.WriteLine(line.TrimEnd (','));
            }
            dataWriter.Close();
        }
        
        /// <summary>
        /// Read PDBaaNR and its asym chain and entity info from the text file
        /// </summary>
        /// <param name="pdbaaTable"></param>
        private void ReadPdbaaDataFromFile(DataTable pdbaaTable)
        {
            if (File.Exists("pdbaa.txt"))
            {
                StreamReader dataReader = new StreamReader("pdbaa.txt");
                string line = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    string[] items = line.Split(',');
                    DataRow newRow = pdbaaTable.NewRow();
                    newRow.ItemArray = items;
                    pdbaaTable.Rows.Add(newRow);
                }
                dataReader.Close();
                pdbaaTable.AcceptChanges();
            }
        }
        #endregion

    }
}
