using System;
using System.Data;
using System.IO;
using System.Collections.Generic;
using DbLib;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.BuIO;
using CrystalInterfaceLib.Settings;
using ProtCidSettingsLib;
using AuxFuncLib;
using BuQueryLib;

namespace CrystalInterfaceLib.Crystal
{
	/// <summary>
	/// Generate BU files
	/// </summary>
	public class BuAssemblyGenerator
	{
		#region member variables
	    private PisaBuGenerator pisaBuGenerator = new PisaBuGenerator ();
        private PdbBuGenerator pdbBuGenerator = new PdbBuGenerator();
		private BuWriter buWriter = new BuWriter ();
		private DbQuery dbQuery = new DbQuery ();
        private StreamWriter logWriter = new StreamWriter("PisaPdbBuGenLog.txt", true);
        private BiolUnitQuery buQuery = new BiolUnitQuery();
        // copy bu files to linux server
        private CmdOperations fileCopy = new CmdOperations();
        private string buFilePathInLinux = "/home/qifang/pisa/";
		#endregion

		public BuAssemblyGenerator()
		{
			if (ProtCidSettings.dirSettings == null)
			{
				ProtCidSettings.LoadDirSettings ();
			}

			if (AppSettings.parameters == null)
			{
				AppSettings.LoadParameters ();
			}
			if (ProtCidSettings.pdbfamDbConnection == null)
			{
                ProtCidSettings.pdbfamDbConnection = new DbConnect ("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" + 
					ProtCidSettings.dirSettings.pdbfamDbPath);
			}
        }

        #region generate BU assemblies from PISA and/or PDB
        /* BUs: if PISA BUs defined, 
         * 
         * if ASU heteromultimer and PISA homomultimer
         * then generate BUs from PDB
         * else
         * then generate BUs from PISA
         * 
         * else PISA BUs not defined
         * generate BUs from PDB
         * 
         */
        /// <summary>
        /// 
        /// </summary>
        public void GenerateBuAssemblies()
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }
            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            bool needLigands = true;
            bool authorDefined = true;
            logWriter.WriteLine(DateTime.Today.ToShortDateString());

            string queryString = "Select Distinct PdbID From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = entryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = entryTable.Rows.Count;
            ProtCidSettings.progressInfo.currentOperationLabel = "Generate BU files";

            string lsFile = Path.Combine(ProtCidSettings.dirSettings.pisaPath, "ls.txt");
            StreamWriter lsFileWriter = new StreamWriter(lsFile);

            string pdbId = "";
            string[] buFiles = null;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                if (AreFilesGenerated(pdbId))
                {
                    continue;
                }

                try
                {
                    buFiles = GenerateEntryBuFiles(pdbId, needLigands, authorDefined);

                    if (buFiles != null && buFiles.Length > 0)
                    {
                        CopyEntryBuFilesToLinux(buFiles);

                        foreach (string buFile in buFiles)
                        {
                            FileInfo fileInfo = new FileInfo(buFile);
                            lsFileWriter.WriteLine(fileInfo.Name);
                        }
                        lsFileWriter.Flush();
                    }
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine(pdbId + " Generate BU files error: " + ex.Message);
                    logWriter.Flush();
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " Generate BU files error: " + ex.Message);
                }
            }
            try
            {
                Directory.Delete(ProtCidSettings.tempDir);
            }
            catch { }
            logWriter.Close();
            lsFileWriter.Close();

            CopyLsFileToLinux(lsFile);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.progressInfo.threadFinished = true;
        }

        private bool AreFilesGenerated(string pdbId)
        {
            string[] buFiles = Directory.GetFiles(ProtCidSettings.dirSettings.pisaPath, pdbId + "*");
            if (buFiles.Length > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        public void UpdateBuAssemblies()
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();
            }
            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }

            bool needLigands = true;
            bool authorDefined = true;
            // add new/updated BU files on May 24, 2011 for Qiong's paper
            string[] updateEntries = GetUpdateFileList();
            //         string[] updateEntries = GetEntriesWithBugs();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;
            ProtCidSettings.progressInfo.currentOperationLabel = "Generate BU files";

            string[] entryBuFiles = null;
            string newLsFileName = Path.Combine (ProtCidSettings.dirSettings.pisaPath, "newls.txt");
            StreamWriter lsFileWriter = new StreamWriter(newLsFileName);

            foreach (string pdbId in updateEntries)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                entryBuFiles = GenerateEntryBuFiles(pdbId, needLigands, authorDefined);
                if (entryBuFiles != null && entryBuFiles.Length > 0)
                {
                    CopyEntryBuFilesToLinux(entryBuFiles);

                    foreach (string buFile in entryBuFiles)
                    {
                        FileInfo fileInfo = new FileInfo(buFile);
                        lsFileWriter.WriteLine(fileInfo.Name);
                    }
                    lsFileWriter.Flush();
                }
            }
            try
            {
                Directory.Delete(ProtCidSettings.tempDir);
            }
            catch { }
            logWriter.Close();
            lsFileWriter.Close();

            CopyLsFileToLinux (newLsFileName);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.progressInfo.threadFinished = true;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="needLigands"></param>
        /// <param name="authorDefined"></param>
        private string[] GenerateEntryBuFiles(string pdbId, bool needLigands, bool authorDefined)
        {
            string[] buFiles = null;
            DataTable asuInfoTable = GetProtAsuTable(pdbId);
            if (asuInfoTable.Rows.Count == 0)
            {
                logWriter.WriteLine(pdbId + ": no protein chains in crystal.");
                logWriter.Flush();
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": no protein chains in crystal.");
                return null;
            }
            try
            {
                if (IsPisaBUStatusOk(pdbId))
                {
                    Dictionary<string, string> pisaBuAbcFormatHash = buQuery.GetPisaBuAbcFormatHash(pdbId);
                    if (IsAsuHeteromultimer(asuInfoTable))
                    {
                        if (!IsPisaEntryHeteromultimer(pisaBuAbcFormatHash))
                        {
                            buFiles = GeneratePdbAssemblies(pdbId, needLigands, authorDefined);
                            return buFiles;
                        }
                    }
                    buFiles = GeneratePisaAssemblies(pdbId, needLigands);
                }
                else
                {
                    buFiles = GeneratePdbAssemblies(pdbId, needLigands, authorDefined);
                }
            }
            catch (Exception ex)
            {
                logWriter.WriteLine(pdbId + ": " + ex.Message);
                logWriter.Flush();
            }
            return buFiles;
        }
        #endregion

        #region generate PISA BUs
		/// <summary>
		/// Generate PISA assemblies
		/// </summary>
		/// <param name="pdbId"></param>
		public string[] GeneratePisaAssemblies (string pdbId)
        {
            List<string> buFileList = new List<string> ();
            string buFileName = "";
			Dictionary<string, Dictionary<string, AtomInfo[]>> pisaMultimersHash = pisaBuGenerator.BuildPisaAssemblies (pdbId);
            DataTable asuTable = GetProtAsuTable(pdbId);
            if (asuTable.Rows.Count == 0)
            {
                logWriter.WriteLine(pdbId + ": no protein chains in crystal.");
                logWriter.Flush();
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": no protein chains in crystal.");
                return null;
            }
			foreach (string assemblyId in pisaMultimersHash.Keys)
			{
				Dictionary<string, AtomInfo[]> pisaAssemblyHash = pisaMultimersHash[assemblyId];
				if (pisaAssemblyHash.Count > 0)
				{
                    buFileName = buWriter.WriteBiolUnitFile(pdbId, assemblyId.ToString(),
                        pisaAssemblyHash, ProtCidSettings.dirSettings.pisaPath, asuTable, "pisa");
                    ParseHelper.ZipPdbFile(buFileName);
                    buFileList.Add(buFileName + ".gz");
				}
			}
            string[] buFileNames = new string[buFileList.Count];
            buFileList.CopyTo(buFileNames);
            return buFileNames;
		}

        /// <summary>
        /// Generate PISA assemblies
        /// </summary>
        /// <param name="pdbId"></param>
        public string[] GeneratePisaAssemblies(string pdbId, bool needLigands)
        {
            if (! HasEntryPeptideChains(pdbId))
            {
                logWriter.WriteLine("No polypeptide chains in " + pdbId);
                return null;
            }
            List<string> buFileList = new List<string> ();
            string buFileName = "";
            Dictionary<string, Dictionary<string, AtomInfo[]>> pisaMultimersHash = pisaBuGenerator.BuildPisaAssemblies(pdbId, needLigands);
            DataTable asuTable = GetProtAsuTable(pdbId);
            if (asuTable.Rows.Count == 0)
            {
                logWriter.WriteLine(pdbId + ": no protein chains in crystal.");
                logWriter.Flush();
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": no protein chains in crystal.");
                return null;
            }
            try
            {
                foreach (string assemblyId in pisaMultimersHash.Keys)
                {
                    Dictionary<string, AtomInfo[]> pisaAssemblyHash = pisaMultimersHash[assemblyId];
                    if (pisaAssemblyHash.Count > 0)
                    {
                        buFileName = buWriter.WriteBiolUnitFile(pdbId, assemblyId.ToString(),
                            pisaAssemblyHash, ProtCidSettings.dirSettings.pisaPath, asuTable, "pisa");
                        ParseHelper.ZipPdbFile(buFileName);
                        buFileList.Add(buFileName + ".gz");
                    }
                }
            }
            catch (Exception ex)
            {
                logWriter.WriteLine(pdbId + ": Generate PISA BU files error: " + ex.Message);
                logWriter.Flush();
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": Generate PISA BU files error: " + ex.Message);
            }
            string[] buFileNames = new string[buFileList.Count];
            buFileList.CopyTo(buFileNames);
            return buFileNames;
        }

		/// <summary>
		/// generate the assembly files for the list of pdb entries
		/// </summary>
		/// <param name="pdbIdList"></param>
		public void GeneratePisaAssemblies (string[] pdbIdList)
		{
			foreach (string pdbId in pdbIdList)
			{
				RemoveExistingFiles (pdbId);
				GeneratePisaAssemblies (pdbId);
			}
		}

        /// <summary>
        /// generate the assembly files for the list of pdb entries
        /// </summary>
        /// <param name="pdbIdList"></param>
        public void GeneratePisaAssemblies(string[] pdbIdList, bool needLigands)
        {
            foreach (string pdbId in pdbIdList)
            {
                RemoveExistingFiles(pdbId);
                GeneratePisaAssemblies(pdbId, needLigands);
            }
        }

		/// <summary>
		/// 
		/// </summary>
		public void UpdatePisaAssemblies ()
		{
            bool needLigands = true;
			string[] updateEntries = GetUpdateFileList ();
			GeneratePisaAssemblies (updateEntries, needLigands);
        }
        #endregion

        #region generate PDB BUs
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="needLigands"></param>
        /// <param name="authorDefined"></param>
        public string[] GeneratePdbAssemblies(string pdbId, bool needLigands, bool authorDefined)
        {
            string[] buIDs = null;
            Dictionary<string, Dictionary<string, AtomInfo[]>> pdbBuHash = null;
            try
            {
                if (authorDefined)
                {
                    buIDs = GetAuthorDefinedBuIDs(pdbId);
                    pdbBuHash = pdbBuGenerator.BuildPdbBus(pdbId, buIDs, needLigands);
                    if (buIDs.Length == 0)
                    {
                        pdbBuHash = pdbBuGenerator.BuildPdbBus(pdbId, needLigands);
                    }
                }
                else
                {
                    pdbBuHash = pdbBuGenerator.BuildPdbBus(pdbId, needLigands);
                }
            }
            catch (Exception ex)
            {
                logWriter.WriteLine(pdbId + ": retrieve PDB BU info error: " + ex.Message);
                logWriter.Flush();
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": retrieve PDB BU info error: " + ex.Message);
            }
            
            string buFileName = "";
            List<string> buFileList = new List<string> ();
            DataTable asuTable = GetProtAsuTable(pdbId);
            if (asuTable.Rows.Count == 0)
            {
                logWriter.WriteLine(pdbId + ": no protein chains in crystal.");
                logWriter.Flush();
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": no protein chains in crystal.");
                return null;
            }
            try
            {
                foreach (string buId in pdbBuHash.Keys)
                {
                    Dictionary<string, AtomInfo[]> buChainsHash = pdbBuHash[buId];
                    buFileName = buWriter.WriteBiolUnitFile(pdbId, buId, buChainsHash, ProtCidSettings.dirSettings.pisaPath, asuTable, "pdb");
                    ParseHelper.ZipPdbFile(buFileName);
                    buFileList.Add(buFileName + ".gz");
                }
            }
            catch (Exception ex)
            {
                logWriter.WriteLine(pdbId + ": geneate PDB BU info error: " + ex.Message);
                logWriter.Flush();
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": generate PDB BU info error: " + ex.Message);
            }
            return buFileList.ToArray();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetAuthorDefinedBuIDs(string pdbId)
        {
            string queryString = string.Format("Select * From PdbBuStat Where PdbID = '{0}';", pdbId);
            DataTable pdbBuStatTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> buIdList = new List<string> ();
            string method = "";
            foreach (DataRow buStatRow in pdbBuStatTable.Rows)
            {
                method = buStatRow["Details"].ToString().TrimEnd();
                if (method.ToLower() == "software_defined_assembly")
                {
                    continue;
                }
                buIdList.Add(buStatRow["BiolUnitID"].ToString());
           /*     if (method.IndexOf("author") > -1)
                {
                    buIdList.Add(buStatRow["BiolUnitID"].ToString ());
                }*/
            }
            string[] authorDefinedBuIDs = buIdList.ToArray();
            return authorDefinedBuIDs;
        }
        #endregion

        #region ASU info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetProtAsuTable(string pdbId)
        {
            string queryString = string.Format("Select * From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable asuInfoTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            return asuInfoTable;
        }

        /// <summary>
        ///  check if the entry contains protein chains
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool HasEntryPeptideChains(string pdbId)
        {
            string queryString = string.Format("Select * From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable asuTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (asuTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
    
		/// <summary>
		/// remove the existing files 
		/// </summary>
		/// <param name="pdbId"></param>
		private void RemoveExistingFiles (string pdbId)
		{
			string[] existingFiles = Directory.GetFiles (ProtCidSettings.dirSettings.pisaPath, pdbId + "*");
			foreach (string existFile in existingFiles)
			{
				File.Delete (existFile);
			}
        }
        #endregion

        #region hetero info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        private bool IsAsuHeteromultimer(DataTable asuTable)
        {
            List<int> protEntityList = new List<int> ();
            int entityId = -1;
            foreach (DataRow asuRow in asuTable.Rows)
            {
                if (asuRow["PolymerType"].ToString().TrimEnd() == "polypeptide")
                {
                    entityId = Convert.ToInt32(asuRow["EntityID"].ToString());
                    if (!protEntityList.Contains(entityId))
                    {
                        protEntityList.Add(entityId);
                    }
                }
            }
            if (protEntityList.Count > 1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private bool IsPisaEntryHeteromultimer (Dictionary<string, string> pisaBUsHash)
        {
            foreach (string buId in pisaBUsHash.Keys)
            {
                string abcFormat = pisaBUsHash[buId];
                if (abcFormat.IndexOf("B") > -1)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region pisa bu status
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsPisaBUStatusOk(string pdbId)
        {
            string queryString = string.Format("Select Status From PisaBUStatus Where PDbID = '{0}';", pdbId);
            DataTable statusTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string status = "";
            if (statusTable.Rows.Count > 0)
            {
                status = statusTable.Rows[0]["Status"].ToString().TrimEnd();
                if (status == "Ok")
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region update entries
        /// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		private string[] GetUpdateFileList ()
		{
			StreamReader dataReader = new StreamReader (Path.Combine (ProtCidSettings.dirSettings.xmlPath, "newls-pdb.txt"));
			List<string> entryList = new List<string> ();
			string line = "";
			while ((line = dataReader.ReadLine ()) != null)
			{
				if (line == "")
				{
					continue;
				}
				entryList.Add (line.Substring (0, 4));
			}
			dataReader.Close ();
        /*    string[] xmlFiles = Directory.GetFiles(ProtCidSettings.dirSettings.xmlPath, "*.xml.gz");
            ArrayList entryList = new ArrayList();
            DateTime preDt = new DateTime (2011, 6, 30);
            foreach (string xmlFile in xmlFiles)
            {
                FileInfo fileInfo = new FileInfo(xmlFile);
                if (DateTime.Compare(fileInfo.LastWriteTime, preDt) > 0)
                {
                    entryList.Add(fileInfo.Name.Substring (0, 4));
                }
            }*/
			string[] updateEntries =  new string [entryList.Count];
			entryList.CopyTo (updateEntries);
			return updateEntries;
        }

        private string[] GetEntriesWithBugs()
        {
            List<string> entryList = new List<string> ();
            StreamReader dataReader = new StreamReader("PisaPdbBuGenLog1.txt");
            string line = "";
            string entry = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("error") > -1)
                {
                    entry = line.Substring(0, 4);
                    if (!entryList.Contains(entry))
                    {
                        entryList.Add(entry);
                    }
                }
            }
            dataReader.Close();
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }
        #endregion

        #region copy bu files to linux server
        /// <summary>
        /// copy files to linux machine, added on July 16, 2010
        /// </summary>
        /// <param name="pdbCodes"></param>
        private void CopyEntryBuFilesToLinux(string[] entryBuFilesInWindows)
        {
            string buFileInLinux = "";

            foreach (string buFileInWindows in entryBuFilesInWindows)
            {
                if (File.Exists(buFileInWindows))
                {
                    FileInfo fileInfo = new FileInfo(buFileInWindows);
                    buFileInLinux = buFilePathInLinux + fileInfo.Name;
                    fileCopy.CopyWindowsDataToLinux(buFileInWindows, buFileInLinux);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lsFile"></param>
        private void CopyLsFileToLinux(string lsFile)
        {
            FileInfo fileInfo = new FileInfo(lsFile);
            string lsFileInLinux = buFilePathInLinux + fileInfo.Name;
            fileCopy.CopyWindowsDataToLinux(lsFile, lsFileInLinux);
        }
        #endregion
    }
}
