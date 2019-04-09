using System;
using System.IO;
using System.Data;
using System.Collections.Generic;
using System.Xml.Serialization;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.BuIO;
using CrystalInterfaceLib.FileParser;
using AuxFuncLib;
using DbLib;

namespace CrystalInterfaceLib.ProtInterfaces
{
	/// <summary>
	/// Summary description for InterfaceFileWriter.
	/// </summary>
	public class InterfaceFileWriter : CrystalBuilder
	{
		#region member variables
		public InterfaceWriter interfaceWriter = new InterfaceWriter ();
		
		public DbQuery dbQuery = new DbQuery ();
        private InterfaceAsa interfaceAsa = new InterfaceAsa();
		#endregion

		public InterfaceFileWriter()
		{
		}

		#region generate interface files
		/// <summary>
		/// generate interface files from the input defintion
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="interfaceInfoList"></param>
		/// <param name="type"></param>
		/// <param name="needAsaUpdated"></param>
		/// <returns></returns>
		public ProtInterface[] GenerateInterfaceFiles (string pdbId, ProtInterfaceInfo[] interfaceInfoList, string type, out bool needAsaUpdated)
		{
            string xmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string coordXmlFile = AuxFuncLib.ParseHelper.UnZipFile(xmlFile, ProtCidSettings.tempDir);
            string[] symOpStrings = GetChainSymOpStringsFromInterfaces(interfaceInfoList);
			Dictionary<string, AtomInfo[]> buChainsHash = base.BuildCrystal(coordXmlFile, symOpStrings);
			File.Delete (coordXmlFile);
			
			needAsaUpdated = false;
            string[] asaChains = interfaceAsa.GetChainsWithoutAsa(interfaceInfoList, type);
			if (asaChains.Length > 0)
			{
				needAsaUpdated = true;
			}
            Dictionary<string, double> chainSaHash = interfaceAsa.GetChainsSurfaceAreaInBu(pdbId, buChainsHash, asaChains, type);
		//	Hashtable chainSaHash = GetChainsSurfaceAreaInBu (pdbId, buChainsHash, type);
			int i = 0;
			string chain1 = "";
			string chain2 = "";
			ProtInterface[] interfaces = new ProtInterface [interfaceInfoList.Length];
			foreach (ProtInterfaceInfo interfaceInfo in interfaceInfoList)
			{			
				chain1 = interfaceInfo.Chain1 + "_" + interfaceInfo.SymmetryString1;
				chain2 = interfaceInfo.Chain2 + "_" + interfaceInfo.SymmetryString2;
                if (! buChainsHash.ContainsKey(chain1) || ! buChainsHash.ContainsKey(chain2))
                {
                    continue;
                }

				if (interfaceInfo.ASA < 0)
				{
					string interfaceComplexFile = interfaceWriter.WriteTempInterfaceToFile (pdbId, interfaceInfo.InterfaceId, 
						(AtomInfo[])buChainsHash[chain1], (AtomInfo[])buChainsHash[chain2], type);
                    double complexAsa = interfaceAsa.ComputeInterfaceSurfaceArea(interfaceComplexFile);

                    if (chainSaHash.ContainsKey(chain1) && chainSaHash.ContainsKey(chain2))
                    {
                        interfaceInfo.ASA = interfaceAsa.CalculateInterfaceBuriedSurfaceArea(
                            (double)chainSaHash[chain1], (double)chainSaHash[chain2], complexAsa);
                    }
				}
                interfaceInfo.Remark += FormatInterfaceAsa(interfaceInfo.ASA);

				string interfaceFile = interfaceWriter.WriteInterfaceToFile (pdbId, interfaceInfo.InterfaceId, buChainsHash[chain1], buChainsHash[chain2], interfaceInfo.Remark, type);
				ParseHelper.ZipPdbFile (interfaceFile);

				interfaces[i] = new ProtInterface (interfaceInfo);
                interfaces[i].ResidueAtoms1 = GetSeqAtoms(buChainsHash[chain1]);
                interfaces[i].ResidueAtoms2 = GetSeqAtoms(buChainsHash[chain2]);
				i ++;
			}
			return interfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="crystInterface"></param>
        /// <param name="remark"></param>
        /// <param name="type"></param>
        public void GenerateInterfaceFile(string interfaceFile, InterfaceChains crystInterface, string remark, string type)
        {
            interfaceWriter.WriteInterfaceToFile(crystInterface.pdbId , crystInterface.interfaceId, interfaceFile, crystInterface.chain1, crystInterface.chain2, remark, type);
            ParseHelper.ZipPdbFile(interfaceFile);
        }

        /// <summary>
        /// surface area string
        /// </summary>
        /// <param name="asa"></param>
        /// <returns></returns>
        private string FormatInterfaceAsa(double asa)
        {
            return "Remark 350 Interface surface area: " + asa.ToString() + "\r\n";
        }
        #endregion

		#region Read Atoms from a biological unit
		/// <summary>
		/// Atoms for interface residues in a chain of an interface
		/// </summary>
		/// <param name="chain"></param>
		/// <returns>Key: residue seqID, value: atoms for the residue</returns>
		public Dictionary<string, List<AtomInfo>> GetAllAtomsAuthSeq (AtomInfo[] chain)
		{		
			Dictionary<string, List<AtomInfo>> residueAtomsHash = new Dictionary<string,List<AtomInfo>> ();
			string seqStr = "";
			foreach (AtomInfo atom in chain)
			{
				seqStr = atom.authSeqId;
				if (residueAtomsHash.ContainsKey (seqStr))
				{					
                    residueAtomsHash[seqStr].Add(atom);
				}
				else
				{
					List<AtomInfo> atomList = new List<AtomInfo> ();
					atomList.Add (atom);
					residueAtomsHash.Add (seqStr, atomList);
				}
			}
			return residueAtomsHash;
		}

		/// <summary>
		/// Atoms for interface residues in a chain of an interface
		/// </summary>
		/// <param name="chain"></param>
		/// <returns>Key: residue seqID, value: atoms for the residue</returns>
		public Dictionary<string, AtomInfo[]> GetSeqAtoms (AtomInfo[] chain)
		{
            Dictionary<string, List<AtomInfo>> residueAtomListHash = new Dictionary<string, List<AtomInfo>>();
			string seqStr = "";
			foreach (AtomInfo atom in chain)
			{
				seqStr = atom.seqId;
				if (residueAtomListHash.ContainsKey (seqStr))
				{
                    residueAtomListHash[seqStr].Add(atom);
				}
				else
				{
					List<AtomInfo> atomList = new List<AtomInfo> ();
					atomList.Add (atom);
					residueAtomListHash.Add (seqStr, atomList);

				}
			}
            Dictionary<string, AtomInfo[]> residueAtomsHash = new Dictionary<string, AtomInfo[]>();
            foreach (string lsSeq in residueAtomListHash.Keys)
            {
                residueAtomsHash.Add(lsSeq, residueAtomListHash[lsSeq].ToArray());
            }
			return residueAtomsHash;
		}
		#endregion

        #region interface files with ligands
        /// <summary>
        /// Add the ligands with the same author name
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceInfoList"></param>
        /// <param name="type"></param>
        /// <param name="needAsaUpdated"></param>
        /// <param name="needSameAuthorLigands"></param>
        /// <returns></returns>
        public ProtInterface[] GenerateInterfaceFiles(string pdbId, ProtInterfaceInfo[] interfaceInfoList,
            string type, bool needSameAuthorLigands, string interfaceFileDir, bool isAsuInterface)
        {
            string xmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string coordXmlFile = AuxFuncLib.ParseHelper.UnZipFile(xmlFile, ProtCidSettings.tempDir);
            DataTable asuTable = GetAsuTable(pdbId);

            ProtInterface[] interfaces = GenerateInterfaceFiles(pdbId, coordXmlFile, interfaceInfoList, type, needSameAuthorLigands, 
                            interfaceFileDir, asuTable, isAsuInterface);

            File.Delete(coordXmlFile);

            return interfaces;
        }

        /// <summary>
        /// Add the ligands with the same author name
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceInfoList"></param>
        /// <param name="type"></param>
        /// <param name="needAsaUpdated"></param>
        /// <param name="needSameAuthorLigands"></param>
        /// <returns></returns>
        public ProtInterface[] GenerateInterfaceFiles (string pdbId, string coordXmlFile, ProtInterfaceInfo[] interfaceInfoList,
            string type, bool needSameAuthorLigands, string interfaceFileDir, DataTable asuTable, bool isAsuInterface)
        {        
            string[] symOpStrings = GetSymOpStringsFromInterfaces(interfaceInfoList);

            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(coordXmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            Dictionary<string, AtomInfo[]> entryChainsHash = BuildCrystalWithAllChains(thisEntryCrystal, symOpStrings);
           
            // protein asym id and its ligands with the same author name and the atoms for the ligands
            //    Hashtable asymIdLigandAtomInfoHash = GetEntryLigandAtomInfoHash(asuTable, thisEntryCrystal);
            Dictionary<string, string[]> asymLigandsHash = GetAsymChainLigandsHash(asuTable);

            int i = 0;
            string chain1 = "";
            string chain2 = "";
            string interfaceFile = "";
            ProtInterface[] interfaces = new ProtInterface[interfaceInfoList.Length];
            string[] symmetryStrings = new string[2];
            foreach (ProtInterfaceInfo interfaceInfo in interfaceInfoList)
            {
                chain1 = interfaceInfo.Chain1 + "_" + interfaceInfo.SymmetryString1;
                chain2 = interfaceInfo.Chain2 + "_" + interfaceInfo.SymmetryString2;
                if (!entryChainsHash.ContainsKey(chain1) || !entryChainsHash.ContainsKey(chain2))
                {
                    continue;
                }

                Dictionary<string, AtomInfo[]>[] ligandAtomInfoHashs = GetInterfaceLigandAtomInfoHashes (interfaceInfo, asymLigandsHash, entryChainsHash);

                interfaceInfo.Remark += FormatInterfaceAsa(interfaceInfo.ASA);
                symmetryStrings[0] = interfaceInfo.SymmetryString1;
                symmetryStrings[1] = interfaceInfo.SymmetryString2;
                interfaceInfo.Remark += FormatLigandsInfo(ligandAtomInfoHashs, symmetryStrings, asuTable);
                if (isAsuInterface)
                {
                    interfaceFile = Path.Combine(interfaceFileDir, pdbId + "_0" + interfaceInfo.InterfaceId.ToString() + ".cryst");
                }
                else
                {
                    interfaceFile = Path.Combine(interfaceFileDir, pdbId + "_" + interfaceInfo.InterfaceId.ToString() + ".cryst");
                }
                interfaceWriter.WriteInterfaceToFile(interfaceFile, entryChainsHash[chain1], entryChainsHash[chain2], interfaceInfo.Remark, type, ligandAtomInfoHashs);
                ParseHelper.ZipPdbFile(interfaceFile);

                interfaces[i] = new ProtInterface(interfaceInfo);
                interfaces[i].ResidueAtoms1 = GetSeqAtoms(entryChainsHash[chain1]);
                interfaces[i].ResidueAtoms2 = GetSeqAtoms(entryChainsHash[chain2]);
                i++;
            }
            return interfaces;
        }
       
        /// <summary>
        /// get the symmetry operator strings from all interfaces in crystal
        /// so that just build the crystal once
        /// </summary>
        /// <param name="interfaceList"></param>
        /// <returns></returns>
        private string[] GetSymOpStringsFromInterfaces(ProtInterfaceInfo[] interfaceList)
        {
            List<string> symOpStringList = new List<string> ();
            string symOpString = "";
            foreach (ProtInterfaceInfo crystInterface in interfaceList)
            {
                //     symOpString = crystInterface.Chain1 + "_" + crystInterface.SymmetryString1;
                symOpString = crystInterface.SymmetryString1;
                if (!symOpStringList.Contains(symOpString))
                {
                    symOpStringList.Add(symOpString);
                }
                symOpString = crystInterface.SymmetryString2;
                if (!symOpStringList.Contains(symOpString))
                {
                    symOpStringList.Add(symOpString);
                }
            }
            return symOpStringList.ToArray ();
        }

        /// <summary>
        /// get the symmetry operator strings from all interfaces in crystal
        /// so that just build the crystal once
        /// </summary>
        /// <param name="interfaceList"></param>
        /// <returns></returns>
        private string[] GetChainSymOpStringsFromInterfaces(ProtInterfaceInfo[] interfaceList)
        {
            List<string> symOpStringList = new List<string> ();
            string symOpString = "";
            foreach (ProtInterfaceInfo crystInterface in interfaceList)
            {
                symOpString = crystInterface.Chain1 + "_" + crystInterface.SymmetryString1;
                if (!symOpStringList.Contains(symOpString))
                {
                    symOpStringList.Add(symOpString);
                }
                symOpString = crystInterface.Chain2 + "_" + crystInterface.SymmetryString2;
                if (!symOpStringList.Contains(symOpString))
                {
                    symOpStringList.Add(symOpString);
                }
            }
            return symOpStringList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceInfo"></param>
        /// <param name="asymLigandsHash"></param>
        /// <returns></returns>
        private Dictionary<string, AtomInfo[]>[] GetInterfaceLigandAtomInfoHashes(ProtInterfaceInfo interfaceInfo, Dictionary<string, string[]> asymLigandsHash, Dictionary<string, AtomInfo[]> entryChainsHash)
        {
            Dictionary<string, AtomInfo[]>[] ligandAtomInfoHashes = new Dictionary<string, AtomInfo[]>[2];
            string[] ligandChains1 = asymLigandsHash[interfaceInfo.Chain1];
            Dictionary<string, AtomInfo[]> ligandAtomInfoHash1 = new Dictionary<string, AtomInfo[]>();
            string ligandSymOpString = "";
            foreach (string ligandChain in ligandChains1)
            {
                ligandSymOpString = ligandChain + "_" + interfaceInfo.SymmetryString1;
                if (entryChainsHash.ContainsKey(ligandSymOpString))
                {
                    ligandAtomInfoHash1.Add(ligandSymOpString, entryChainsHash[ligandSymOpString]);
                }
            }
            ligandAtomInfoHashes[0] = ligandAtomInfoHash1;
            string[] ligandChains2 = asymLigandsHash[interfaceInfo.Chain2];
            Dictionary<string, AtomInfo[]> ligandAtomInfoHash2 = new Dictionary<string, AtomInfo[]>();
            foreach (string ligandChain in ligandChains2)
            {
                ligandSymOpString = ligandChain + "_" + interfaceInfo.SymmetryString2;
                if (entryChainsHash.ContainsKey(ligandSymOpString))
                {
                    ligandAtomInfoHash2.Add(ligandSymOpString, (AtomInfo[])entryChainsHash[ligandSymOpString]);
                }
            }
            ligandAtomInfoHashes[1] = ligandAtomInfoHash2;
            return ligandAtomInfoHashes;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="thisEntryCrystal"></param>
        /// <returns>double hashtable. 1. key: protein asymid, value: hashtable; 2: key ligand asym id, value: atoms</returns>
        private Dictionary<string, Dictionary<string, AtomInfo[]>> GetEntryLigandAtomInfoHash(DataTable asuTable, EntryCrystal thisEntryCrystal)
        {
            string[] asymIds = GetProteinAsymIDs(asuTable);
            Dictionary<string, string[]> asymLigandsHash = GetAsymChainLigandsHash(asymIds, asuTable);
            Dictionary<string, Dictionary<string, AtomInfo[]>> asymIdLigandAtomInfoHash = GetLigandAtomInfoHash(asymLigandsHash, thisEntryCrystal);
            return asymIdLigandAtomInfoHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> GetAsymChainLigandsHash(DataTable asuTable)
        {
            string[] asymIds = GetProteinAsymIDs(asuTable);
            Dictionary<string, string[]> asymLigandsHash = GetAsymChainLigandsHash(asymIds, asuTable);
            return asymLigandsHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymIdLigandAsymIdHash"></param>
        /// <param name="thisEntryCrystal"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<string, AtomInfo[]>> GetLigandAtomInfoHash(Dictionary<string, string[]> asymIdLigandAsymIdHash, EntryCrystal thisEntryCrystal)
        {
            Dictionary<string, Dictionary<string, AtomInfo[]>> asymIdLigandAtomInfoHash = new Dictionary<string, Dictionary<string, AtomInfo[]>>();
            ChainAtoms[] chains = thisEntryCrystal.atomCat.ChainAtomList;
            foreach (string asymId in asymIdLigandAsymIdHash.Keys)
            {
                string[] ligandAsymIds = asymIdLigandAsymIdHash[asymId];
                Dictionary<string, AtomInfo[]> ligandAtomInfoHash = new Dictionary<string,AtomInfo[]> ();
                foreach (string ligandAsymId in ligandAsymIds)
                {
                    foreach (ChainAtoms chain in chains)
                    {
                        if (chain.asymChain == ligandAsymId)
                        {
                            ligandAtomInfoHash.Add(ligandAsymId, chain.CartnAtoms);
                        }
                    }
                }
                asymIdLigandAtomInfoHash.Add(asymId, ligandAtomInfoHash);
            }
            return asymIdLigandAtomInfoHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymIds"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> GetAsymChainLigandsHash(string[] asymIds, DataTable asuTable)
        {
            Dictionary<string, string> asymIdAuthIdHash = GetAsymChainAuthorIdsHash(asymIds, asuTable);
            Dictionary<string, string[]> asymIdLigangAsymIdHash = new Dictionary<string,string[]> ();
            foreach (string asymId in asymIdAuthIdHash.Keys)
            {
                string authorChain = asymIdAuthIdHash[asymId];
                string[] ligandAsymIds = GetAsymIdsOfLigandsInSameAuthorChain(authorChain, asuTable);
                asymIdLigangAsymIdHash.Add(asymId, ligandAsymIds);
            }
            return asymIdLigangAsymIdHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="authorChainId"></param>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        private string[] GetAsymIdsOfLigandsInSameAuthorChain(string authorChainId, DataTable asuTable)
        {
            DataRow[] ligandsRows = asuTable.Select
                (string.Format("AuthorChain = '{0}' AND PolymerStatus = 'non-polymer'", authorChainId));
            string[] ligandAsymIds = new string[ligandsRows.Length];
            int count = 0;
            foreach (DataRow ligandRow in ligandsRows)
            {
                ligandAsymIds[count] = ligandRow["AsymID"].ToString().TrimEnd();
                count++;
            }
            return ligandAsymIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymIds"></param>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetAsymChainAuthorIdsHash(string[] asymIds, DataTable asuTable)
        {
            Dictionary<string, string> asymIdAuthIdHash = new Dictionary<string, string>();
            foreach (string asymId in asymIds)
            {
                if (asymIdAuthIdHash.ContainsKey(asymId))
                {
                    continue;
                }
                DataRow[] authChainRows = asuTable.Select(string.Format("AsymID = '{0}'", asymId));
                if (authChainRows.Length > 0)
                {
                    asymIdAuthIdHash.Add(asymId, authChainRows[0]["AuthorChain"].ToString().TrimEnd());
                }
            }
            return asymIdAuthIdHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetAsuTable(string pdbId)
        {
            string queryString = string.Format("Select * From AsymUnit WHere PdbID = '{0}';", pdbId);
            DataTable asuTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            return asuTable;
        }    

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        private string[] GetProteinAsymIDs(DataTable asuTable)
        {
            List<string> protAsymIdList = new List<string> ();
            string polymerType = "";
            foreach (DataRow dataRow in asuTable.Rows)
            {
                polymerType = dataRow["PolymerType"].ToString().TrimEnd();
                if (polymerType == "polypeptide")
                {
                    protAsymIdList.Add(dataRow["AsymID"].ToString().TrimEnd());
                }
            }
            return protAsymIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandAtomInfoHashes"></param>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        public string FormatLigandsInfo(Dictionary<string, AtomInfo[]>[] ligandAtomInfoHashes, string[] symmetryStrings, DataTable asuTable)
        {
            string ligandInfoString = "Remark 300 Ligand Info \r\n";
            string ligandInfo = "";
            List<string> ligandChainList1 = new List<string> (ligandAtomInfoHashes[0].Keys);
            ligandChainList1.Sort ();
            string asymChain = "";
            foreach (string ligandChainId in ligandChainList1)
            {
                asymChain = ligandChainId.Substring(0, ligandChainId.IndexOf ("_"));
                ligandInfo = GetLigandInfo(asymChain, symmetryStrings[0], asuTable);
                ligandInfoString += ("Remark 300 Interface Chain A " + ligandInfo);
            }
            if (ligandAtomInfoHashes.Length == 2)
            {
                List<string> ligandChainList2 = new List<string> (ligandAtomInfoHashes[1].Keys);
                ligandChainList2.Sort();
                foreach (string ligandChainId in ligandChainList2)
                {
                    asymChain = ligandChainId.Substring(0, ligandChainId.IndexOf("_"));
                    ligandInfo = GetLigandInfo(asymChain, symmetryStrings[1], asuTable);
                    ligandInfoString += ("Remark 300 Interface Chain B " + ligandInfo);
                }
            }
            return ligandInfoString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymId"></param>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        public string GetLigandInfo(string asymId, string symmetryString, DataTable asuTable)
        {
            DataRow[] chainRows = asuTable.Select(string.Format("AsymID = '{0}'", asymId));
            string ligandInfoString = "";
            if (chainRows.Length > 0)
            {
                ligandInfoString = " For Asymmetric Chain " + asymId + 
                    " Author Chain " + chainRows[0]["AuthorChain"].ToString().TrimEnd() + " " +
                    " Entity ID " + chainRows[0]["EntityID"].ToString() + " Symmetry Operator " + symmetryString + "\r\n";
                ligandInfoString += ("Remark 300 Name " + chainRows[0]["Name"].ToString().TrimEnd() + " " +
                    "Sequence " + chainRows[0]["Sequence"].ToString().TrimEnd() + " " +
                    "PDB Sequence Number " + chainRows[0]["PdbSeqNumbers"].ToString() + " " +
                    "Author Sequence Number " + chainRows[0]["AuthSeqNumbers"].ToString() + "\r\n");
            }
            return ligandInfoString;
        }


        /// <summary>
        /// the ligands with the same author chains
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, AtomInfo[]>> GetEntryLigandAtomInfoWithSameAuthorChainsHash(string pdbId)
        {
            string xmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string coordXmlFile = AuxFuncLib.ParseHelper.UnZipFile(xmlFile, ProtCidSettings.tempDir);

            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(coordXmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            DataTable asuTable = GetAsuTable(pdbId);
            // protein asym id and its ligands with the same author name and the atoms for the ligands
            Dictionary<string, Dictionary<string, AtomInfo[]>> asymIdLigandAtomInfoHash = GetEntryLigandAtomInfoHash(asuTable, thisEntryCrystal);

            File.Delete(coordXmlFile);
            return asymIdLigandAtomInfoHash;
        }
        #endregion      

        #region interfaces in author residue numbering
        /// <summary>
        /// Add the ligands with the same author name
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceInfoList"></param>
        /// <param name="type"></param>
        /// <param name="needAsaUpdated"></param>
        /// <param name="needSameAuthorLigands"></param>
        /// <returns></returns>
        public ProtInterface[] GenerateInterfaceFilesInAuthNumbering(string pdbId, string coordXmlFile, ProtInterfaceInfo[] interfaceInfoList,
            string type, bool needSameAuthorLigands, string interfaceFileDir, DataTable asuTable, bool isAsuInterface)
        {
            string[] symOpStrings = GetSymOpStringsFromInterfaces(interfaceInfoList);

            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(coordXmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            Dictionary<string, AtomInfo[]> entryChainsHash = BuildCrystalWithAllChains(thisEntryCrystal, symOpStrings);
            UpdateResidueNumberingToAuthor(entryChainsHash);

            // protein asym id and its ligands with the same author name and the atoms for the ligands
            //    Hashtable asymIdLigandAtomInfoHash = GetEntryLigandAtomInfoHash(asuTable, thisEntryCrystal);
            Dictionary<string, string[]> asymLigandsHash = GetAsymChainLigandsHash(asuTable);

            int i = 0;
            string chain1 = "";
            string chain2 = "";
            string interfaceFile = "";
            ProtInterface[] interfaces = new ProtInterface[interfaceInfoList.Length];
            string[] symmetryStrings = new string[2];
            foreach (ProtInterfaceInfo interfaceInfo in interfaceInfoList)
            {
                chain1 = interfaceInfo.Chain1 + "_" + interfaceInfo.SymmetryString1;
                chain2 = interfaceInfo.Chain2 + "_" + interfaceInfo.SymmetryString2;
                if (!entryChainsHash.ContainsKey(chain1) || !entryChainsHash.ContainsKey(chain2))
                {
                    continue;
                }

                Dictionary<string, AtomInfo[]>[] ligandAtomInfoHashs = GetInterfaceLigandAtomInfoHashes(interfaceInfo, asymLigandsHash, entryChainsHash);

                interfaceInfo.Remark += FormatInterfaceAsa(interfaceInfo.ASA);
                symmetryStrings[0] = interfaceInfo.SymmetryString1;
                symmetryStrings[1] = interfaceInfo.SymmetryString2;
                interfaceInfo.Remark += FormatLigandsInfo(ligandAtomInfoHashs, symmetryStrings, asuTable);
                if (isAsuInterface)
                {
                    interfaceFile = Path.Combine(interfaceFileDir, pdbId + "_0" + interfaceInfo.InterfaceId.ToString() + ".cryst");
                }
                else
                {
                    interfaceFile = Path.Combine(interfaceFileDir, pdbId + "_" + interfaceInfo.InterfaceId.ToString() + ".cryst");
                }
                interfaceWriter.WriteInterfaceToFile(interfaceFile, entryChainsHash[chain1], entryChainsHash[chain2], interfaceInfo.Remark, type, ligandAtomInfoHashs);
                ParseHelper.ZipPdbFile(interfaceFile);

                interfaces[i] = new ProtInterface(interfaceInfo);
                interfaces[i].ResidueAtoms1 = GetSeqAtoms(entryChainsHash[chain1]);
                interfaces[i].ResidueAtoms2 = GetSeqAtoms(entryChainsHash[chain2]);
                i++;
            }
            return interfaces;
        }

        /// <summary>
        /// update seqID to author seqID
        /// </summary>
        /// <param name="chainAtomsDict"></param>
        private void UpdateResidueNumberingToAuthor (Dictionary<string, AtomInfo[]> chainAtomsDict)
        {
            foreach (string chainId in chainAtomsDict.Keys)
            {
                UpdateResidueNumberingToAuthor(chainAtomsDict[chainId]);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="atoms"></param>
        private void UpdateResidueNumberingToAuthor (AtomInfo[] atoms)
        {
            for (int i = 0; i < atoms.Length; i ++)
            {
                atoms[i].seqId = atoms[i].authSeqId;
                atoms[i].residue = atoms[i].authResidue;
            }
        }
        #endregion
    }
}
