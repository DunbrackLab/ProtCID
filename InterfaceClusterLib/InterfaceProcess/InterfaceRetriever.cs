using System;
using System.Data;
using System.IO;
using System.Collections.Generic;
using CrystalInterfaceLib.BuIO;
using CrystalInterfaceLib.Contacts;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Crystal;
using DbLib;
using AuxFuncLib;
using CrystalInterfaceLib.DomainInterfaces;

namespace InterfaceClusterLib.InterfaceProcess
{
	/// <summary>
	/// read interfaces from files
	/// </summary>
	public class InterfaceRetriever : InterfaceReader
	{
		private DbQuery dbQuery = new DbQuery ();

		public InterfaceRetriever()
		{
		}

		#region Read Interfaces From Files
		/// <summary>
		/// get the cryst interfaces from the cryst interface files
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="pqsBuId"></param>
		public InterfaceChains[] GetInterfacesFromFiles (string pdbId, string type)
		{
			string searchMode = pdbId + "*." + type + ".gz";
			string filePath = ProtCidSettings.dirSettings.interfaceFilePath + 
					"\\" + type + "\\" + pdbId.Substring (1, 2);
            List<InterfaceChains> interfaceList = new List<InterfaceChains>();
            if (Directory.Exists(filePath))
            {
                string[] interfaceFiles = Directory.GetFiles(filePath, searchMode);
              
                for (int i = 0; i < interfaceFiles.Length; i++)
                {
                    InterfaceChains crystInterface = new InterfaceChains();
                    string interfaceFile = ParseHelper.UnZipFile(interfaceFiles[i], ProtCidSettings.tempDir);
                    ReadInterfaceFromFile(interfaceFile, ref crystInterface, "ALL");
                    File.Delete(interfaceFile);                 
                    if (crystInterface.GetInterfaceResidueDist())
                    {
                        interfaceList.Add(crystInterface);
                    }
                }
            }
            return interfaceList.ToArray ();
		}

        /// <summary>
        /// get the cryst interfaces from the cryst interface files
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pqsBuId"></param>
        public InterfaceChains[] GetInterfacesFromFiles(string srcDir, string pdbId, string type)
        {
            string searchMode = pdbId + "*." + type + ".gz";
            List<InterfaceChains> interfaceList = new List<InterfaceChains> ();
            if (Directory.Exists(srcDir))
            {
                string[] interfaceFiles = Directory.GetFiles(srcDir, searchMode);

                for (int i = 0; i < interfaceFiles.Length; i++)
                {
                    InterfaceChains crystInterface = new InterfaceChains();
                    string interfaceFile = ParseHelper.UnZipFile(interfaceFiles[i], ProtCidSettings.tempDir);
                    ReadInterfaceFromFile(interfaceFile, ref crystInterface, "ALL");
                    File.Delete(interfaceFile);
                    
                    if (crystInterface.GetInterfaceResidueDist())
                    {
                        interfaceList.Add(crystInterface);
                    }
                }
            }
            return interfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="srcDir"></param>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public InterfaceChains GetInterfaceFromFile(string srcDir, string pdbId, int interfaceId, string type)
        {
            int[] interfaceIds = new int[1];
            interfaceIds[0] = interfaceId;
            InterfaceChains[] interfaces = GetInterfacesFromFiles (srcDir, pdbId, interfaceIds, type);
            if (interfaces.Length == 1)
            {
                return interfaces[0];
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="srcDir"></param>
        /// <param name="pdbId"></param>
        /// <param name="interfaceIds"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public InterfaceChains[] GetInterfacesFromFiles(string srcDir, string pdbId, int[] interfaceIds, string type)
        {
            List<InterfaceChains> interfaceList = new List<InterfaceChains> ();
            if (Directory.Exists(srcDir))
            {
                foreach (int interfaceId in interfaceIds)
                {
                    string searchMode = pdbId + "_" + interfaceId.ToString () + "." + type + ".gz";
                    string[] interfaceFiles = Directory.GetFiles(srcDir, searchMode);
                   if (interfaceFiles.Length > 0)
                   {
                        InterfaceChains crystInterface = new InterfaceChains();
                        crystInterface.pdbId = pdbId;
                        string interfaceFile = ParseHelper.UnZipFile(interfaceFiles[0], ProtCidSettings.tempDir);
                        ReadInterfaceFromFile(interfaceFile, ref crystInterface, "ALL");
                        File.Delete(interfaceFile);

                        if (crystInterface.GetInterfaceResidueDist())
                        {
                            interfaceList.Add(crystInterface);
                        }
                    }
                }
            }
            return interfaceList.ToArray ();
        }
        #endregion

        #region Interfaces from db or xml file
        /// <summary>
        /// retrieve interfaces for the entry from XML coordinate files and 
        /// interface definition in the database
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public InterfaceChains[] GetCrystInterfaces(string pdbId, string type)
        {
            string searchMode = pdbId + "*." + type + ".gz";
			string filePath = ProtCidSettings.dirSettings.interfaceFilePath + 
					"\\" + type + "\\" + pdbId.Substring (1, 2);
            InterfaceChains[] chainInterfaces = null;
            if (Directory.Exists(filePath))
            {
                string[] interfaceFiles = Directory.GetFiles(filePath, searchMode);
                if (interfaceFiles.Length > 0)
                {
                    chainInterfaces = GetInterfacesFromFiles(filePath, pdbId, type);
                }
            }
            if (chainInterfaces != null && chainInterfaces.Length == 0)
            {
                chainInterfaces = GetCrystInterfacesFromXmlFile(pdbId, type);  
            }
            return chainInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceIds"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public InterfaceChains[] GetCrystInterfaces(string pdbId, int[] interfaceIds, string type)
        {
            string searchMode = pdbId + "*." + type + ".gz";
            string filePath = ProtCidSettings.dirSettings.interfaceFilePath +
                    "\\" + type + "\\" + pdbId.Substring(1, 2);
            InterfaceChains[] chainInterfaces = null;
            if (Directory.Exists(filePath))
            {
                string[] interfaceFiles = Directory.GetFiles(filePath, searchMode);
                if (interfaceFiles.Length > 0)
                {
                    chainInterfaces = GetInterfacesFromFiles(filePath, pdbId, interfaceIds, type);
                }
            }
            if (chainInterfaces != null && chainInterfaces.Length == 0)
            {
                chainInterfaces =  GetCrystInterfacesFromXmlFile(pdbId, interfaceIds, type);
            }
            return chainInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceIds"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public InterfaceChains[] GetCrystInterfacesFromXmlFile(string pdbId, int[] interfaceIds, string type)
        {
            ContactInCrystal crystInterfaceRetriever = new ContactInCrystal();

            string queryString = string.Format("Select Distinct * From CrystEntryInterfaces " +
                " Where PdbID = '{0}';", pdbId);
            DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (interfaceDefTable.Rows.Count == 0)
            {
                return null;
            }
            Dictionary<int, string> interfaceDefHash = GetInterfaceDefHash(interfaceDefTable, interfaceIds);

            string xmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string coordXmlFile = ParseHelper.UnZipFile(xmlFile, ProtCidSettings.tempDir);

            bool interfaceExist = false;
            crystInterfaceRetriever.FindInteractChains(coordXmlFile, interfaceDefHash, out interfaceExist);

          /*  InterfaceChains[] chainInterfaces = new InterfaceChains[interfaceIds.Length];
            int count = 0;
            foreach (int interfaceId in interfaceIds)
            {
                foreach (InterfaceChains crystInterface in crystInterfaceRetriever.InterfaceChainsList)
                {
                    if (crystInterface.interfaceId == interfaceId)
                    {
                        chainInterfaces[count] = crystInterface;
                        count++;
                    }
                }
            }*/
            File.Delete(coordXmlFile);
            SetInterfaceEntities(crystInterfaceRetriever.InterfaceChainsList, interfaceDefTable);
            return crystInterfaceRetriever.InterfaceChainsList;
       //     return chainInterfaces;
        }

        /// <summary>
        /// rebuild the interfaces from XML coordinate file and the interface definition
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public InterfaceChains[] GetCrystInterfacesFromXmlFile(string pdbId, string type)
        {
            ContactInCrystal crystInterfaceRetriever = new ContactInCrystal();

            string queryString = string.Format("Select Distinct * From CrystEntryInterfaces " +
                " Where PdbID = '{0}';", pdbId);
            DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( queryString);
            Dictionary<int, string> interfaceDefHash = GetInterfaceDefHash(interfaceDefTable);

            string xmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string coordXmlFile = ParseHelper.UnZipFile(xmlFile, ProtCidSettings.tempDir);

            bool interfaceExist = false;
            try
            {
                crystInterfaceRetriever.FindInteractChains(coordXmlFile, interfaceDefHash, out interfaceExist);
                SetInterfaceEntities(crystInterfaceRetriever.InterfaceChainsList, interfaceDefTable);
            }
            catch
            {
                return null;
            }
            finally
            {
                File.Delete(coordXmlFile);
            }

            return crystInterfaceRetriever.InterfaceChainsList;
        }
        /// <summary>
        /// the symmetry strings for the interfaces of the entry
        /// </summary>
        /// <param name="interfaceDefTable"></param>
        /// <returns></returns>
        private Dictionary<int, string> GetInterfaceDefHash(DataTable interfaceDefTable)
        {
            Dictionary<int, string> interfaceDefHash = new Dictionary<int, string>();
            string symmetryStrings = "";
            int interfaceId = -1;
            foreach (DataRow defRow in interfaceDefTable.Rows)
            {
                interfaceId = Convert.ToInt32(defRow["InterfaceID"].ToString ());
                symmetryStrings = defRow["AsymChain1"].ToString().TrimEnd() + "_" +
                    defRow["SymmetryString1"].ToString().TrimEnd() + ";" + 
                    defRow["AsymChain2"].ToString ().TrimEnd () + "_" + 
                    defRow["SymmetryString2"].ToString ().TrimEnd ();
                interfaceDefHash.Add(interfaceId, symmetryStrings);
            }
            return interfaceDefHash;
        }

        /// <summary>
        /// the symmetry strings for the interfaces of the entry
        /// </summary>
        /// <param name="interfaceDefTable"></param>
        /// <returns></returns>
        private Dictionary<int, string> GetInterfaceDefHash(DataTable interfaceDefTable, int[] interfaceIds)
        {
            Dictionary<int, string> interfaceDefHash = new Dictionary<int,string> ();
            string symmetryStrings = "";
            int interfaceId = -1;
            foreach (DataRow defRow in interfaceDefTable.Rows)
            {
                interfaceId = Convert.ToInt32(defRow["InterfaceID"].ToString());
                if (Array.IndexOf(interfaceIds, interfaceId) > -1)
                {
                    symmetryStrings = defRow["AsymChain1"].ToString().TrimEnd() + "_" +
                        defRow["SymmetryString1"].ToString().TrimEnd() + ";" +
                        defRow["AsymChain2"].ToString().TrimEnd() + "_" +
                        defRow["SymmetryString2"].ToString().TrimEnd();
                    interfaceDefHash.Add(interfaceId, symmetryStrings);
                }
            }
            return interfaceDefHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crystInterfaces"></param>
        /// <param name="interfaceDefTable"></param>
        private void SetInterfaceEntities(InterfaceChains[] crystInterfaces, DataTable interfaceDefTable)
        {
            foreach (InterfaceChains crystInterface in crystInterfaces)
            {
                DataRow[] interfaceDefRows = interfaceDefTable.Select(string.Format ("InterfaceID = '{0}'", 
                    crystInterface.interfaceId));
                if (interfaceDefRows.Length > 0)
                {
                    crystInterface.entityId1 = Convert.ToInt32(interfaceDefRows[0]["EntityID1"].ToString());
                    crystInterface.entityId2 = Convert.ToInt32(interfaceDefRows[0]["EntityID2"].ToString ());
                }
            }
        }
        #endregion

        #region interfaces from BU
        /// <summary>
		/// get the cryst interfaces from the cryst interface files
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="pqsBuId"></param>
        public Dictionary<string, List<InterfaceChains>> GetBuInterfacesFromFiles(string pdbId, string type)
		{
            Dictionary<string, List<InterfaceChains>> buInterfacesListHash = new Dictionary<string, List<InterfaceChains>>();
			string searchMode = pdbId + "*." + type + ".gz";
            string fileDir = ProtCidSettings.dirSettings.interfaceFilePath + "\\" + type + "\\" + pdbId.Substring (1, 2);
            if (Directory.Exists(fileDir))
            {
                string[] interfaceFiles = Directory.GetFiles(fileDir, searchMode);
                string buId = "";
                string justFileName = "";
                //	InterfaceReader interfaceReader = new InterfaceReader ();
                foreach (string interfaceFile in interfaceFiles)
                {
                    justFileName = interfaceFile.Substring(interfaceFile.LastIndexOf("\\") + 1,
                        interfaceFile.Length - interfaceFile.LastIndexOf("\\") - 1);
                    buId = justFileName.Substring(4, justFileName.IndexOf("_") - 4);
                    string unzippedInterfaceFile = ParseHelper.UnZipFile(interfaceFile, ProtCidSettings.tempDir);
                    InterfaceChains thisInterface = new InterfaceChains();
                    ReadBuInterfaceFromFile(unzippedInterfaceFile, ref thisInterface, "ALL");
                    File.Delete(unzippedInterfaceFile);
                    if (type == "pqs")
                    {
                        ConvertPqsResidueNumToPdb(pdbId, ref thisInterface);
                    }
                    if (thisInterface.GetInterfaceResidueDist())
                    {
                        if (buInterfacesListHash.ContainsKey(buId))
                        {
                            buInterfacesListHash[buId].Add(thisInterface);
                        }
                        else
                        {
                            List<InterfaceChains> interfaceList = new List<InterfaceChains>();
                            interfaceList.Add(thisInterface);
                            buInterfacesListHash.Add(buId, interfaceList);
                        }
                    }
                } 
            }
            return buInterfacesListHash;
        }
       
        /// <summary>
		/// get the cryst interfaces from the cryst interface files
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="pqsBuId"></param>
		public InterfaceChains[] GetBuInterfacesFromFiles (string pdbId, string buId, string type)
		{
            List<InterfaceChains> interfaceList = new List<InterfaceChains> ();
			string searchMode = pdbId + buId +  "*." + type + ".gz";
            string fileDir = ProtCidSettings.dirSettings.interfaceFilePath + "\\" + type + "\\" + pdbId.Substring(1, 2);
            if (Directory.Exists(fileDir))
            {
                string[] interfaceFiles = Directory.GetFiles(fileDir, searchMode);
                foreach (string interfaceFile in interfaceFiles)
                {
                    try
                    {
                        InterfaceChains thisInterface = new InterfaceChains();
                        string decompInterfaceFile = ParseHelper.UnZipFile(interfaceFile, ProtCidSettings.tempDir);
                        ReadBuInterfaceFromFile(decompInterfaceFile, ref thisInterface, "ALL");
                        File.Delete(decompInterfaceFile);
                        if (type == "pqs")
                        {
                            string asymChain1 = thisInterface.firstSymOpString.Substring(0, thisInterface.firstSymOpString.IndexOf("_"));
                            string asymChain2 = thisInterface.secondSymOpString.Substring(0, thisInterface.secondSymOpString.IndexOf("_"));
                            if (asymChain1 == "-" || asymChain2 == "-")
                            {
                                continue;
                            }
                            ConvertPqsResidueNumToPdb(pdbId, ref thisInterface);
                        }
                        if (thisInterface.GetInterfaceResidueDist())
                        {
                            interfaceList.Add(thisInterface);
                        }
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue
                            ("Errors in retrieving interfaces from " + interfaceFile + ": " + ex.Message);
                    }
                }
            }
            return interfaceList.ToArray ();
		}

		/// <summary>
		/// convert PQS residue numbering to PDB sequential numbering
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="buChainsHash"></param>
		private void ConvertPqsResidueNumToPdb (string pdbId, ref InterfaceChains thisInterface)
		{
			List<string> pqsChainList = new List<string> ();
			string pqsChain1 = thisInterface.firstSymOpString.Substring (0, thisInterface.firstSymOpString.IndexOf ("_"));
			string pqsChain2 = thisInterface.secondSymOpString.Substring (0, thisInterface.secondSymOpString.IndexOf ("_"));
			pqsChainList.Add (pqsChain1);
			pqsChainList.Add (pqsChain2);
	
			string residueNumQueryString = string.Format 
				("Select AsymID, AuthSeqNumbers From AsymUnit" + 
				" WHERE PdbID = '{0}' AND PolymerType = 'polypeptide' AND AsymID IN ({1})", 
				pdbId, ParseHelper.FormatSqlListString (pqsChainList.ToArray ()));
            DataTable residueNumTable = ProtCidSettings.pdbfamQuery.Query( residueNumQueryString);
			// first chain in the interface
			DataRow[] residueNumRows = residueNumTable.Select (string.Format ("AsymID = '{0}'", pqsChain1));
			if (residueNumRows.Length == 0)
			{
				return;
			}
			string residueNums = residueNumRows[0]["AuthSeqNumbers"].ToString ();
			string[] authNums = residueNums.Split (',');
			bool needChanged = AreSeqNumsNeedChanged (authNums);
			if (needChanged)
			{
				foreach (AtomInfo atom in thisInterface.chain1)
				{
					int i = 0;
					for(; i < authNums.Length; i ++)
					{
						if (atom.seqId == authNums[i])
						{
							atom.seqId = (i + 1).ToString ();
							break;
						}
					}
					
				}
			}
			// second chain in the interface
			residueNumRows = residueNumTable.Select (string.Format ("AsymID = '{0}'", pqsChain2));
			if (residueNumRows.Length == 0)
			{
				return;
			}
			residueNums = residueNumRows[0]["AuthSeqNumbers"].ToString ();
			authNums = residueNums.Split (',');
			needChanged = AreSeqNumsNeedChanged (authNums);
			if (needChanged)
			{
				foreach (AtomInfo atom in thisInterface.chain2)
				{
					int i = 0;
					for(; i < authNums.Length; i ++)
					{
						if (atom.seqId == authNums[i])
						{
							atom.seqId = (i + 1).ToString ();
							break;
						}
					}					
				}
			}
		}

		private bool AreSeqNumsNeedChanged (string[] authNums)
		{
			// no author sequence numbers
			if (authNums.Length == 0)
			{
				return false;
			}
			// sequence numbers are same as sequential numbers
			if (authNums[0] == "1" && authNums[authNums.Length - 1] == (authNums.Length).ToString ())
			{
				return false;
			}
			return true;
		}
		#endregion

        #region domain interfaces
        /// <summary>
        /// get the cryst interfaces from the cryst interface files
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pqsBuId"></param>
        public DomainInterface[] GetDomainInterfacesFromFiles(string srcDir, string pdbId, string type)
        {
            DataTable pfamDomainTable = GetEntryPfamDomainTable(pdbId);
            long domainId = 0;

            string searchMode = pdbId + "*." + type + ".gz";
            List<DomainInterface> domainInterfaceList = new List<DomainInterface> ();
            if (Directory.Exists(srcDir))
            {
                string[] interfaceFiles = Directory.GetFiles(Path.Combine (srcDir, pdbId.Substring (1, 2)), searchMode);
                string remark = "";
                for (int i = 0; i < interfaceFiles.Length; i++)
                {
                    InterfaceChains crystInterface = new InterfaceChains();
                    string domainInterfaceFile = ParseHelper.UnZipFile(interfaceFiles[i], ProtCidSettings.tempDir);
                    remark = ReadInterfaceChainsFromFile(domainInterfaceFile, ref crystInterface);
                    DomainInterface domainInterface = new DomainInterface (crystInterface);
                    domainInterface.pdbId = pdbId;
                    domainInterface.remark = remark;
                    domainInterface.domainInterfaceId = GetDomainInterfaceId(domainInterfaceFile);

                    string[] domainChainStrings = GetDomainChainStrings(remark);
                    domainId = Convert.ToInt64(domainChainStrings[0].Substring(0, domainChainStrings[0].IndexOf("_")));
                    domainInterface.domainId1 = domainId;
                    domainInterface.familyCode1 = GetPfamId(domainId, pfamDomainTable);
                    domainId = Convert.ToInt64(domainChainStrings[1].Substring(0, domainChainStrings[1].IndexOf("_")));
                    domainInterface.domainId2 = domainId;
                    domainInterface.familyCode2 = GetPfamId(domainId, pfamDomainTable);

                    File.Delete(domainInterfaceFile);

                    if (domainInterface.GetInterfaceResidueDist ())
                    {
                        domainInterfaceList.Add(domainInterface);
                    }
                }
            }
            return domainInterfaceList.ToArray ();
        }

        /// <summary>
        /// get the cryst interfaces from the cryst interface files
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pqsBuId"></param>
        public DomainInterface GetDomainInterfacesFromFiles(string srcDir, string pdbId, int domainInterfaceId, string type)
        {
            int[] domainInterfaceIds = new int[1];
            domainInterfaceIds[0] = domainInterfaceId;
            DomainInterface[] domainInterfaces = GetDomainInterfacesFromFiles(srcDir, pdbId, domainInterfaceIds, type);
            if (domainInterfaces.Length == 1)
            {
                return domainInterfaces[0];
            }
            return null;
        }

        /// <summary>
        /// a list of domain interfaces for the input domainInterfaceIds
        /// </summary>
        /// <param name="srcDir"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceIds"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public DomainInterface[] GetDomainInterfacesFromFiles(string srcDir, string pdbId, int[] domainInterfaceIds, string type)
        {
            DataTable pfamDomainTable = GetEntryPfamDomainTable(pdbId);
            long domainId = 0;
            List<DomainInterface> domainInterfaceList = new List<DomainInterface> ();
            srcDir = Path.Combine(srcDir, pdbId.Substring(1, 2));
            string gzDomainInterfaceFile = "";
            string domainInterfaceFile = "";
            string remark = "";
            if (Directory.Exists(srcDir))
            {
                foreach (int dInterfaceId in domainInterfaceIds)
                {
                    gzDomainInterfaceFile = Path.Combine(srcDir, pdbId + "_d" + dInterfaceId.ToString() + "." + type + ".gz");
                    if (File.Exists (gzDomainInterfaceFile))
                    {
                        InterfaceChains crystInterface = new InterfaceChains();
                        domainInterfaceFile = ParseHelper.UnZipFile(gzDomainInterfaceFile, ProtCidSettings.tempDir);
                        remark = ReadInterfaceChainsFromFile(domainInterfaceFile, ref crystInterface);
                        DomainInterface domainInterface = new DomainInterface(crystInterface);
                        domainInterface.pdbId = pdbId;
                        domainInterface.remark = remark;
                        domainInterface.domainInterfaceId = GetDomainInterfaceId(domainInterfaceFile);

                        string[] domainChainStrings = GetDomainChainStrings(remark);
                        domainId = Convert.ToInt64(domainChainStrings[0].Substring(0, domainChainStrings[0].IndexOf("_")));
                        domainInterface.domainId1 = domainId;
                        domainInterface.familyCode1 = GetPfamId(domainId, pfamDomainTable);
                        domainId = Convert.ToInt64(domainChainStrings[1].Substring(0, domainChainStrings[1].IndexOf("_")));
                        domainInterface.domainId2 = domainId;
                        domainInterface.familyCode2 = GetPfamId(domainId, pfamDomainTable);

                        File.Delete(domainInterfaceFile);

                        if (domainInterface.GetInterfaceResidueDist())
                        {
                            domainInterfaceList.Add(domainInterface);
                        }
                    }
                }
            }
            return domainInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryPfamDomainTable(string pdbId)
        {
            string queryString = string.Format("Select Pfam_ID, DomainID From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable pfamDomainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return pfamDomainTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainId"></param>
        /// <param name="pfamDomainTable"></param>
        /// <returns></returns>
        private string GetPfamId(long domainId, DataTable pfamDomainTable)
        {
            DataRow[] domainRows = pfamDomainTable.Select(string.Format ("DomainID = '{0}'", domainId));
            string pfamId = "";
            if (domainRows.Length > 0)
            {
                pfamId = domainRows[0]["Pfam_ID"].ToString().TrimEnd();
            }
            return pfamId;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainFile"></param>
        /// <param name="domainInterface"></param>
        private void ReadDomainIdFromFile(string domainFile, DomainInterface domainInterface)
        {
            StreamReader dataReader = new StreamReader(domainFile);
            string line = "";
            int domainIdx = -1;
            int domainRangeIdx = -1;
            long domainId1 = -1;
            long domainId2 = -1;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("Domain 1") > -1)
                {
                    domainIdx = line.IndexOf("Domain 1") + "Domain 1".Length;
                    domainRangeIdx = line.IndexOf("Domain Ranges");
                    domainId1 = Convert.ToInt64(line.Substring (domainIdx, domainRangeIdx - domainIdx));
                }

                if (line.IndexOf("Domain 2") > -1)
                {
                    domainIdx = line.IndexOf("Domain 2") + "Domain 2".Length;
                    domainRangeIdx = line.IndexOf("Domain Ranges");
                    domainId2 = Convert.ToInt64(line.Substring(domainIdx, domainRangeIdx - domainIdx));
                }
                if (line.IndexOf("ATOM") > -1)
                {
                    break;
                }
            }
            dataReader.Close();
            domainInterface.domainId1 = domainId1;
            domainInterface.domainId2 = domainId2;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private int GetDomainInterfaceId(string fileName)
        {
            int nameIdx = fileName.LastIndexOf("\\");
            int exeIdx = fileName.LastIndexOf(".");

            string domainName = fileName.Substring(nameIdx + 1, exeIdx - nameIdx - 1);
            int domainIdx = domainName.IndexOf("_d");
            string domainIdString = domainName.Substring(domainIdx + 2, domainName.Length - domainIdx - 2);
            return Convert.ToInt32(domainIdString);
        }
        #endregion
    }
}
