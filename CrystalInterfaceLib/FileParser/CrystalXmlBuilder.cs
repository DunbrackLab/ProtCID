using System;
using System.IO;
using System.Data;
using System.Collections.Generic;
using System.Xml;
using System.Net;
using System.Xml.Serialization;
using System.Text.RegularExpressions;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Settings;
using CrystalInterfaceLib.BuIO;
using ProgressLib;
using AuxFuncLib;
using DbLib;
using ProtCidSettingsLib;

namespace CrystalInterfaceLib.FileParser
{
	/// <summary>
	/// Summary description for CrystalXmlBuilder.
	/// </summary>
	public class CrystalXmlBuilder
	{
		#region member variables
		// data source file paths
		//private string entFilePath = "";
		private string xmlFilePath = "";
		private string destFilePath = "";
        private string pdbTextPath = "";
		// create or update
		public string modifyType = "";
		// display progress in the user interface
//		public static ProgressInfo progressInfo =  new ProgressInfo ();

		// extension names for file type
		string xmlExtName = ".xml.gz";
		
		private DbQuery dbQuery = new DbQuery ();
		private DbInsert dbInsert = new DbInsert ();
        private BuWriter buWriter = new BuWriter();

        private CmdOperations fileCopy = new CmdOperations();
        private string pdbFilePathInLinux = "/home/qifang/pdb/regular/";
        private string pdbWebAddress = "https://files.rcsb.org/download/";
        private WebClient webClient = new WebClient();
		#endregion

		#region public interface
		public CrystalXmlBuilder()
		{
		}

		/// <summary>
		/// create crystal XML files
		/// </summary>
		public void CreateCrystalXmlFiles()
		{
			AsymUnitBuilder asuBuilder = new AsymUnitBuilder ();
		//	bool asuChanged = false;
			ProtCidSettings.LoadDirSettings ();
			AppSettings.LoadParameters ();
            ProtCidSettings.pdbfamDbConnection = new DbConnect ("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                ProtCidSettings.dirSettings.pdbfamDbPath);

            ProtCidSettings.progressInfo.Reset();
           
			SetFilePaths();

			string atomType = "";
			if (AppSettings.parameters.contactParams.atomType == "CA")
			{
				atomType = "CA";
			} 
			else if (AppSettings.parameters.contactParams.atomType == "CB")
			{
				atomType = "CB";
			}
			else
			{
				atomType = "ALL";
			}

			// save the file list for those just created
			List<string> parsedCoordXmlFiles = new List<string> ();

			XmlAtomParser xmlParser = new XmlAtomParser ();

			if (!Directory.Exists (destFilePath.Trim ('\\')))
			{
				Directory.CreateDirectory (destFilePath.Trim ('\\'));
			}
            modifyType = "update";
			// temporary directory
            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }

			// get the common list of XML files, ent files and BU symmetry matrix files
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving file list. Please wait...");
			  string [] pdbCodes = GetFileNames();
         /* string[] pdbCodes = { "4p6d"};
            string[] pdbCodes = GetMissingEntries();*/

              ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");

              ProtCidSettings.progressInfo.currentOperationLabel = "PDB Processing";
              ProtCidSettings.progressInfo.totalStepNum = pdbCodes.Length;
              ProtCidSettings.progressInfo.totalOperationNum = pdbCodes.Length;
              ProtCidSettings.progressInfo.progressInterval = 1;

              ProtCidSettings.progressInfo.progStrQueue.Enqueue("Building Coordinate XML files. Please wait...");
            string crystalXmlFile = "";
			foreach (string pdbCode in pdbCodes)
			{
                /////////////////////////////////////////
                // display progress information
                // get just the fileName				
                ProtCidSettings.progressInfo.currentFileName = pdbCode;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                crystalXmlFile = destFilePath + pdbCode + ".xml";
                FileInfo fileInfo = new FileInfo(crystalXmlFile + ".gz");
                if (fileInfo.LastWriteTime.Day == DateTime.Today.Day)
                {
                    continue;
                }
               
				string zippedXmlFile = xmlFilePath + pdbCode + xmlExtName;
                if (!File.Exists(zippedXmlFile))
                {
                    webClient.DownloadFile(pdbWebAddress + pdbCode + ".xml.gz", pdbCode + ".xml.gz");
                    File.Move(pdbCode + ".xml.gz", zippedXmlFile);
                }

				string xmlFile = ParseHelper.UnZipFile(zippedXmlFile, ProtCidSettings.tempDir);				
				EntryCrystal thisEntryCrystal = new EntryCrystal (pdbCode);
				try
				{
                    xmlParser.ParseXmlFile (xmlFile, ref thisEntryCrystal, atomType);
					/*if (thisEntryCrystal != null)
					{
						if (thisEntryCrystal.ncsCat.NcsOperatorList.Length > 0)
						{
							asuBuilder.BuildAsymUnitFromNcs (ref thisEntryCrystal, out asuChanged);
							if (asuChanged)
							{
								UpdateAsymUnitDbTable (thisEntryCrystal);
							}
						}
					}*/
				}
				catch (Exception ex)
				{
					// record the error, continue to the next file
					string errorMsg = string.Format("Processing {0} file errors: {1}. Skip it.", 
						pdbCode, ex.Message);
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(errorMsg);
				}
				finally
				{
					// delete this processed file
					File.Delete (xmlFile);
				}
				// save entry crystal data into a XML file
				try
				{
					if (thisEntryCrystal != null)
					{
						XmlSerializer xmlSerializer = new XmlSerializer (thisEntryCrystal.GetType ()); 
						TextWriter crystalWriter = new StreamWriter (crystalXmlFile);
						xmlSerializer.Serialize (crystalWriter, thisEntryCrystal);
						crystalWriter.Close ();
						ParseHelper.ZipPdbFile (crystalXmlFile);
						parsedCoordXmlFiles.Add (pdbCode + ".xml.gz");

                        WriteXmlFileToPdbFile(pdbCode, thisEntryCrystal);
                //        CopyPdbFileToLinux(pdbCode);
					}
				}
				catch (Exception ex)
				{
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
				}
			}
			try
			{
				Directory.Delete (ProtCidSettings.tempDir, true);
			}
			catch {}
			SaveFileList (parsedCoordXmlFiles);
        /*    progressInfo.progStrQueue.Enqueue("Copy PDB files to Linux server.");
            CopyPdbFilesToLinux(pdbCodes);
			progressInfo.progStrQueue.Enqueue ("Done!");
*/
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Copy updated files to new directory");
            string updateFilePath = pdbTextPath.Replace ("regular", "updateRegular");
            if (Directory.Exists(updateFilePath))
            {
                Directory.Delete(updateFilePath, true);
            }
            Directory.CreateDirectory(updateFilePath);

            DateTime dtTime = DateTime.Today;
            ParseHelper.CopyNewFiles(pdbTextPath, updateFilePath, dtTime);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");

            ProtCidSettings.progressInfo.threadFinished = true;
		}

        /// <summary>
        /// Format any PDB xml file to my format
        /// </summary>
        /// <param name="pdbId"></param>
        public string WritePdbXmlToMyCoordXmlFile (string pdbXmlFile)
        {
            FileInfo fileInfo = new FileInfo(pdbXmlFile);
            string pdbCode = fileInfo.Name.Substring(0, 4);
            if (!File.Exists(pdbXmlFile))
            {
                webClient.DownloadFile(pdbWebAddress + fileInfo.Name, pdbXmlFile);                
            }

            if (pdbXmlFile.IndexOf (".gz")  > -1)
            {
                ParseHelper.UnZipFile(pdbXmlFile);
                pdbXmlFile = pdbXmlFile.Replace(".gz", "");
            }
            XmlAtomParser xmlParser = new XmlAtomParser();
            EntryCrystal thisEntryCrystal = new EntryCrystal(pdbCode);
            try
            {
                xmlParser.ParseXmlFile(pdbXmlFile, ref thisEntryCrystal, "ALL");
            }
            catch (Exception ex)
            {
                // record the error, continue to the next file
                string errorMsg = string.Format("Processing {0} file errors: {1}. Skip it.",
                    pdbCode, ex.Message);
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(errorMsg);
            }
            string coordXmlFile = Path.Combine (fileInfo.DirectoryName, pdbCode + "_coord.xml");
            // save entry crystal data into a XML file
            try
            {
                if (thisEntryCrystal != null)
                {
                    XmlSerializer xmlSerializer = new XmlSerializer(thisEntryCrystal.GetType());
                    TextWriter crystalWriter = new StreamWriter(coordXmlFile);
                    xmlSerializer.Serialize(crystalWriter, thisEntryCrystal);
                    crystalWriter.Close();
         //           ParseHelper.ZipPdbFile(coordXmlFile);
                }
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
            }
            return coordXmlFile;
        }       
		#endregion

        #region update asymunit table
        /// <summary>
		/// update asymmetric unit db table
		/// </summary>
		/// <param name="thisEntryCrystal"></param>
		private void UpdateAsymUnitDbTable (EntryCrystal thisEntryCrystal)
		{
			string queryString = string.Format ("Select * From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", 
				thisEntryCrystal.PdbId);
            DataTable asuTable = ProtCidSettings.pdbfamQuery.Query(queryString);
			asuTable.TableName = "AsymUnit";
			foreach (ChainAtoms chain in thisEntryCrystal.atomCat.ChainAtomList)
			{
				DataRow[] existRows = asuTable.Select (string.Format ("PdbID = '{0}' AND AsymID = '{1}'", 
					thisEntryCrystal.PdbId, chain.AsymChain));
				if (existRows.Length > 0)
				{
					continue;
				}
				DataRow[] existEntityRows = asuTable.Select (string.Format ("PdbID = '{0}' AND EntityID = '{1}'", 
					thisEntryCrystal.PdbId, chain.EntityID));
				existEntityRows[0]["AsymID"] = chain.AsymChain;
                dbInsert.InsertDataIntoDb(ProtCidSettings.pdbfamDbConnection, existEntityRows[0]);
			}
		}
		#endregion

        #region generate PDB text files using XML sequential numbers for residues
        /// <summary>
        /// writer XML coordinates into a PDB text file in which residues are numbered by sequential numbers.
        /// </summary>
        /// <param name="xmlFile"></param>
        public void WriteXmlFileToPdbFile(string pdbId, EntryCrystal thisEntryCrystal)
        {
            string[] nonpolymerAsymChains = GetNonpolymerAsymChains(thisEntryCrystal.entityCat.EntityInfoList);
            ChainAtoms[] chains = thisEntryCrystal.atomCat.ChainAtomList;
            Dictionary<string, AtomInfo[]> asuChainHash = new Dictionary<string, AtomInfo[]>();
            Dictionary<string, string> chainMatchHash = new Dictionary<string,string> ();
            string updateAsymChain = "";
            int chainIndex = 26; // the capital letters are used for asymIds
            List<string> chainsInOrderList = new List<string> ();
            List<string> asymChainsInOrderList = new List<string> ();
            foreach (ChainAtoms chain in chains)
            {
                updateAsymChain = chain.asymChain;
                if (chain.AsymChain.Length > 1) // it is a problem in the pdb formatted file
                {
                    if (chainIndex >= ParseHelper.chainLetters.Length)
                    {
                        chainIndex = 26;
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + chain.AsymChain + ": the length of chain name >= 2 and out of chain letters index.");
                    }
                    updateAsymChain = ParseHelper.chainLetters[chainIndex].ToString();
                    chainIndex++;
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + chain.AsymChain + ": the length of chain name >= 2.");
                }
                asuChainHash.Add(chain.asymChain, chain.CartnAtoms);
                chainMatchHash.Add(chain.asymChain, updateAsymChain);
                chainsInOrderList.Add(updateAsymChain);
                asymChainsInOrderList.Add(chain.asymChain);
            }
            string[] fileChains = new string[chainsInOrderList.Count];
            chainsInOrderList.CopyTo(fileChains);
            string[] asymChainsInFile = new string[asymChainsInOrderList.Count];
            asymChainsInOrderList.CopyTo(asymChainsInFile);
            string seqresRecords = GetSeqresRecords(thisEntryCrystal.entityCat, chainMatchHash, asymChainsInFile);
            string tempAsuFileName = buWriter.WriteAsymUnitFile(pdbId, asuChainHash, asymChainsInFile, fileChains, nonpolymerAsymChains, ProtCidSettings.tempDir, seqresRecords);
            string hashFolder = Path.Combine(pdbTextPath, GetHashFolder(pdbId));
            if (!Directory.Exists(hashFolder))
            {
                Directory.CreateDirectory(hashFolder);
                fileCopy.CreateDirectoryInLinux(pdbFilePathInLinux + "/" + pdbId.Substring(1, 2));
            }
            string asuFileName = Path.Combine(hashFolder, pdbId + ".ent");
            File.Move(tempAsuFileName, asuFileName);
            ParseHelper.ZipPdbFile(asuFileName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="thisEntryCrystal"></param>
        /// <param name="selectedAsymChains"></param>
        /// <returns></returns>
        public string WriteChainAtomsToPdbFile(string pdbId, EntryCrystal thisEntryCrystal, string[] selectedAsymChains)
        {
            string[] nonpolymerAsymChains = GetNonpolymerAsymChains(thisEntryCrystal.entityCat.EntityInfoList);
            ChainAtoms[] chains = thisEntryCrystal.atomCat.ChainAtomList;
            Dictionary<string, AtomInfo[]> asuChainHash = new Dictionary<string,AtomInfo[]> ();
            Dictionary<string, string> chainMatchHash = new Dictionary<string,string> ();
            List<string> fileChainsInOrderList = new List<string> ();
            string fileChain = "";
            int chainIndex = 0;
            List<string> asymChainsInOrderList = new List<string> ();
            foreach (ChainAtoms chain in chains)
            {
                if (Array.IndexOf(selectedAsymChains, chain.asymChain) > -1)
                {
                    continue;
                }
                if (chainIndex == ParseHelper.chainLetters.Length)
                {
                    chainIndex = 0;
                }
                fileChain = ParseHelper.chainLetters[chainIndex].ToString ();
                asuChainHash.Add(chain.asymChain, chain.CartnAtoms);
                chainMatchHash.Add(chain.asymChain, fileChain);
                asymChainsInOrderList.Add(chain.asymChain);
                fileChainsInOrderList.Add(fileChain);
                chainIndex++;
            }

            string[] fileChains = new string[fileChainsInOrderList.Count];
            fileChainsInOrderList.CopyTo(fileChains);
            string[] asymChainsInFile = new string[asymChainsInOrderList.Count];
            asymChainsInOrderList.CopyTo(asymChainsInFile);

            string seqresRecords = GetSeqresRecords(thisEntryCrystal.entityCat, chainMatchHash, asymChainsInFile);
            string tempAsuFileName = buWriter.WriteAsymUnitFile(pdbId, asuChainHash, asymChainsInFile, fileChains, nonpolymerAsymChains, ProtCidSettings.tempDir, seqresRecords);
            string hashFolder = Path.Combine(pdbTextPath, GetHashFolder(pdbId));
            if (!Directory.Exists(hashFolder))
            {
                Directory.CreateDirectory(hashFolder);
                fileCopy.CreateDirectoryInLinux(pdbFilePathInLinux + "/" + pdbId.Substring(1, 2));
            }
            string asuFileName = Path.Combine(hashFolder, pdbId + ".ent");
            File.Move(tempAsuFileName, asuFileName);
            return asuFileName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityInfos"></param>
        /// <returns></returns>
        private string[] GetNonpolymerAsymChains(EntityInfo[] entityInfos)
        {
            List<string> nonpolymerAsymChainList = new List<string> ();
            foreach (EntityInfo entityInfo in entityInfos)
            {
                if (entityInfo.type == "non-polymer")
                {
                    string asymChainField = entityInfo.asymChains;
                    string[] asymChains = asymChainField.Split(',');
                    nonpolymerAsymChainList.AddRange(asymChains);
                }
            }
            string[] nonpolymerAsymChains = new string[nonpolymerAsymChainList.Count];
            nonpolymerAsymChainList.CopyTo(nonpolymerAsymChains);
            return nonpolymerAsymChains;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityInfoCat"></param>
        /// <returns></returns>
        private string GetSeqresRecords(EntityInfoCategory entityInfoCat, 
            Dictionary<string, string> chainMatchHash, string[] asymChainsInFile)
        {
            string remarkString = "REMARK   1\r\n";
            remarkString += "REMARK   2 AsymChains     FileChains\r\n";
            Dictionary<string, string> chainResidueHash = new Dictionary<string,string> ();

            foreach (EntityInfo entityInfo in entityInfoCat.EntityInfoList)
            {
                string[] asymChains = entityInfo.asymChains.Split(',');
                foreach (string asymChain in asymChains)
                {
                    chainResidueHash.Add(asymChain, entityInfo.threeLetterSeq);
                }
            }
            string seqresRecords = "";
          
            string remarkAsymChains = "";
            string fileChains = "";
            string fileChain = "";
            foreach (string asymChain in asymChainsInFile)
            {
                if (chainResidueHash.ContainsKey(asymChain))
                {
                    string[] residues = ((string)chainResidueHash[asymChain]).Split(' ');

                    remarkAsymChains += (asymChain + ",");
                    fileChain = chainMatchHash[asymChain];
                    fileChains += (fileChain + ",");
                    string seqresLine = ParseHelper.FormatChainSeqResRecords(fileChain, residues);
                    seqresRecords += (seqresLine + "\r\n");
                }
            }
            remarkString += ("REMARK   2 " + remarkAsymChains.TrimEnd (',') + "\r\n");
            remarkString += ("REMARK   2 " + fileChains.TrimEnd (','));
            seqresRecords = seqresRecords.TrimEnd("\r\n".ToCharArray ());
            seqresRecords = remarkString + "\r\n" + seqresRecords;
            return seqresRecords;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlFile"></param>
        /// <returns></returns>
        private string GetEntryCodeFromFile(string xmlFile)
        {
            int exeIndex = xmlFile.IndexOf(".xml");
            int fileIndex = xmlFile.LastIndexOf("\\");
            string entry = xmlFile.Substring(fileIndex + 1, exeIndex - fileIndex - 1);
            return entry;
        }

        private string GetHashFolder(string pdbId)
        {
            return pdbId.Substring(1, 2);
        }
        #endregion

        #region write asu to pdb ent files
        public void WriteAsuToPdbFiles()
          {
            SetFilePaths();
            ProtCidSettings.LoadDirSettings();
            ProtCidSettings.pdbfamDbConnection = new DbConnect ("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
              ProtCidSettings.dirSettings.pdbfamDbPath);
            ProtCidSettings.pdbfamDbConnection.ConnectToDatabase();

            string[] pdbCodes = GetMissingEntries();
            string gzCoordXmlFile = "";
            string coordXmlFile = "";
            string coordXmlFileDir = @"D:\Qifang\ProjectData\DbProjectData\CoordXml";
            foreach (string pdbCode in pdbCodes)
            {
                try
                {
                    gzCoordXmlFile = Path.Combine(coordXmlFileDir, pdbCode + ".xml.gz");
                    coordXmlFile = ParseHelper.UnZipFile(gzCoordXmlFile, ProtCidSettings.tempDir);
                    // read data from crystal xml file
                    EntryCrystal thisEntryCrystal;
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
                    FileStream xmlFileStream = new FileStream(coordXmlFile, FileMode.Open);
                    thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
                    xmlFileStream.Close();
                    WriteXmlFileToPdbFile(pdbCode, thisEntryCrystal);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbCode + ": " + ex.Message);
                }
            }
            ProtCidSettings.pdbfamDbConnection.DisconnectFromDatabase();
        }
        #endregion

        #region Get file list

        /// <summary>
		/// get a list of common pdb entries from XML files and matrix files
		/// </summary>
		/// <returns></returns>
		private string [] GetFileNames()
		{
			if (modifyType == "build")
			{
				return GetAllFilesFromDirs();
			}
			else if (modifyType == "update")
			{
				if (File.Exists (Path.Combine (xmlFilePath, "newls-pdb.txt")))
				{
					return GetUpdatedFilesFromFile();
				}
				else
				{
					return GetUpdatedFilesFromDirs();
				}
			}
			return null;
		}
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetMissingEntries()
        {
            List<string> missingEntryList = new List<string> ();
             string pdbId = "";
           string queryString = "Select Distinct PdbID From AsymUnit Where PolymerType = 'polypeptide';";
           DataTable entryTable = ProtCidSettings.pdbfamQuery.Query(queryString);            
            string xmlFile = "";
            
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                xmlFile = Path.Combine(destFilePath, pdbId + ".xml.gz");
                if (File.Exists(xmlFile))
                {
                    continue;
                }
                missingEntryList.Add(pdbId);
            }
   /*         StreamReader dataReader = new StreamReader("missentrylist.txt");
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                int entryIndex = line.IndexOf("Generate domain file ");
                if (entryIndex > -1)
                {
                    string[] fields = line.Split(' ');
                    pdbId = fields[0].Substring (0, 4);
                    if (!missingEntryList.Contains(pdbId))
                    {
                        missingEntryList.Add(pdbId);
                    }
                }
            }
            dataReader.Close();
            StreamReader dataReader = new StreamReader("WrongEntEntries.txt");
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                missingEntryList.Add(line.Substring(0, 4));
            }
            dataReader.Close();*/
            return missingEntryList.ToArray ();
        }

		/// <summary>
		/// get all xml files with matched matrix files
		/// in the specific directory
		/// for build 
		/// </summary>
		/// <returns></returns>
		private string[] GetAllFilesFromDirs()
		{
			string[] srcFiles = Directory.GetFiles (xmlFilePath, "*xml*");
			List<string> pdbCodeList = new List<string> ();

			foreach (string xmlFile in srcFiles)
			{
				string pdbCode = xmlFile.Substring (xmlFile.LastIndexOf ("\\")+ 1, 4);
				pdbCodeList.Add (pdbCode);
			}
			ClearObseleteCoordFiles ();

            return pdbCodeList.ToArray ();
		}

        private string[] GetAllPdbEntries()
        {
            string queryString = "Select PdbID From PdbEntry Order By PdbID;";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] entries = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                entries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return entries;
        }
		/// <summary>
		/// get a list of new or updated XML files with matched matrix
		/// from the newls file
		/// for update
		/// </summary>
		/// <returns></returns>
		private string[] GetUpdatedFilesFromFile()
		{
			List<string> fileList = new List<string> ();
			string[] xmlFiles = null;
			using (StreamReader dataReader = new StreamReader(xmlFilePath + "\\newls-pdb.txt"))
			{
				string line;
					
				// Read lines from the file until the end of 
				// the file is reached.
				while ((line = dataReader.ReadLine()) != null)
				{
					if (line == "")
						continue;
					fileList.Add (line);
				}
				xmlFiles = new string [fileList.Count];
				fileList.CopyTo (xmlFiles);				
			}

			List<string> pdbCodeList = new List<string> ();

			//no large number of files to be updated
			// so check file exists directly
			foreach (string xmlFile in xmlFiles)
			{
				string pdbCode = xmlFile.Substring (xmlFile.LastIndexOf ("\\")+ 1, 4);
				pdbCodeList.Add (pdbCode);
		/*		if (File.Exists (destFilePath + pdbCode + xmlExtName))
				{
					File.Delete (destFilePath + pdbCode + xmlExtName);
				}*/
			}

            return pdbCodeList.ToArray ();
		}

		/// <summary>
		/// get a list of updated files which are not parsed 
		/// and with matched matrix 
		/// for update
		/// </summary>
		/// <returns></returns>
		private string[] GetUpdatedFilesFromDirs ()
		{
			string[] srcFiles = Directory.GetFiles (xmlFilePath, "*xml*");
			List<string> pdbCodeList = new List<string> ();
			foreach (string xmlFile in srcFiles)
			{
				string pdbCode = xmlFile.Substring (xmlFile.LastIndexOf ("\\")+ 1, 4);	
				string coordXmlFile = Path.Combine (destFilePath, pdbCode + xmlExtName);
				if (File.Exists (coordXmlFile))
				{
                    continue;
				}
				pdbCodeList.Add (pdbCode);
			}

            return pdbCodeList.ToArray ();
		}

		/// <summary>
		/// clear obselte coord files in the directory
		/// </summary>
		private void ClearObseleteCoordFiles ()
		{
			List<string> obseleteFileList = new List<string> ();

			string[] coordFiles = Directory.GetFiles (destFilePath, "*.xml.gz");
			string[] xmlFiles = Directory.GetFiles (xmlFilePath, "*.xml.gz");
			Array.Sort (xmlFiles);

			string[] xmlEntries = new string [xmlFiles.Length];
			for (int i = 0; i < xmlFiles.Length; i ++)
			{
				string xmlPdb = xmlFiles [i].Substring (xmlFiles[i].LastIndexOf ("\\") + 1, 4);
				xmlEntries[i] = xmlPdb;
			}

			foreach (string coordFile in coordFiles)
			{
				DateTime coordFileTime = File.GetCreationTime (coordFile);
				int entryIndex = Array.BinarySearch (xmlEntries, 
					coordFile.Substring (coordFile.LastIndexOf ("\\") + 1, 4));
				if (entryIndex < 0)
				{
					obseleteFileList.Add (coordFile);
				}
			}
			foreach (string file in obseleteFileList)
			{
				File.Delete (file);
			}
		}
		/// <summary>
		/// get data source file paths and coordinate xml file path 
		/// from setting xml file
		/// </summary>
		private void SetFilePaths ()			
		{
			ProtCidSettings.LoadDirSettings ();
			this.xmlFilePath = ProtCidSettings.dirSettings.xmlPath;
			if (this.xmlFilePath[this.xmlFilePath.Length - 1] != '\\')
			{
				this.xmlFilePath += "\\";
			}
			this.destFilePath = ProtCidSettings.dirSettings.coordXmlPath;
			if (this.destFilePath[this.destFilePath.Length - 1] != '\\')
			{
				this.destFilePath += "\\";
			}
            pdbTextPath = Regex.Replace (ProtCidSettings.dirSettings.xmlPath, "xml", "regular", RegexOptions.IgnoreCase);
		}

		private void SaveFileList (List<string> fileList)
		{
			FileStream fileStream = new FileStream(Path.Combine (destFilePath, "newls.txt"), FileMode.Create, FileAccess.Write, FileShare.None);
			StreamWriter fileWriter = new StreamWriter(fileStream); 
			foreach (string file in fileList)
			{
				fileWriter.WriteLine (file.ToString ());
			}
			fileWriter.Close ();

            StreamWriter pdbListFileWriter = new StreamWriter(Path.Combine(pdbTextPath, "newls.txt"));
            foreach (string file in fileList)
            {
                pdbListFileWriter.WriteLine(file);
            }
            pdbListFileWriter.Close();
		}

        /// <summary>
        /// copy files to linux machine, added on July 16, 2010
        /// </summary>
        /// <param name="pdbCodes"></param>
        private void CopyPdbFilesToLinux(string[] pdbCodes)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Copy files";

            string pdbFileInLinux = "";
            string pdbFileInWindows = "";
            ProtCidSettings.progressInfo.totalOperationNum = pdbCodes.Length;
            ProtCidSettings.progressInfo.totalStepNum = pdbCodes.Length;

            foreach (string pdbCode in pdbCodes)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbCode;

                pdbFileInWindows = Path.Combine(pdbTextPath, pdbCode.Substring(1, 2) + "\\" + pdbCode + ".ent.gz");
                if (File.Exists(pdbFileInWindows))
                {
                    pdbFileInLinux = pdbFilePathInLinux + pdbCode.Substring(1, 2) + "/" + pdbCode + ".ent.gz";
                    fileCopy.CopyWindowsDataToLinux(pdbFileInWindows, pdbFileInLinux);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void CopyPdbFileToLinux(string pdbId)
        {
            string pdbFileInLinux = "";
            string pdbFileInWindows = Path.Combine(pdbTextPath, pdbId.Substring(1, 2) + "\\" + pdbId + ".ent.gz");
            if (File.Exists(pdbFileInWindows))
            {
                pdbFileInLinux = pdbFilePathInLinux + pdbId.Substring(1, 2) + "/" + pdbId + ".ent.gz";
                fileCopy.CopyWindowsDataToLinux(pdbFileInWindows, pdbFileInLinux);
            }
        }
		#endregion
	}
}
