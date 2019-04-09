using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using ProtCidSettingsLib;
using CrystalInterfaceLib.BuIO;
using AuxFuncLib;


namespace CrystalInterfaceLib.Crystal
{
	/// <summary>
	/// Generate PDB BUs from XML files
	/// </summary>
	public class PdbBuGenerator : CrystalBuilder
	{
		public PdbBuGenerator()
		{
		}

		const string standardAAs = "ALA ARG ASN ASP ASX CYS GLU GLN " + 
			" GLX GLY HIS ILE LEU LYS MET PHE PRO SER THR SEC TRP TYR VAL";

		#region generate PDB BUs
		/// <summary>
		/// 
		/// </summary>
		/// <param name="coordXmlPath"></param>
		public void GenerateAllPdbBus (string srcPath, string destPath)
		{
			if (! Directory.Exists (destPath))
			{
				Directory.CreateDirectory (destPath);
			}
			string tempDir = "C:\\xml_temp";
			if (! Directory.Exists (tempDir))
			{
				Directory.CreateDirectory (tempDir);
			}
			string[] xmlFiles = GetFileList (srcPath, destPath);
			foreach (string xmlFile in xmlFiles)
			{
				string unzippedXmlFile = ParseHelper.UnZipFile (xmlFile, tempDir);
                GeneratePdbBUs(unzippedXmlFile, destPath);
				File.Delete (unzippedXmlFile);
			}
			Directory.Delete (tempDir, true);
		}
		/// <summary>
		/// build biological units from XML coordinates 
		/// and symmetry Matrix files provided by RCSB
		/// since RCSB directly provide symmetry matrix in cartesian coordinates
		/// no transfromation from cartesian to fract, and fract to cartesian
		/// </summary>
		/// <param name="crystalXmlFile">XML file with coordinates and symmetry operations</param>
		/// <returns>biological units build from coordinates and symmetry operations</returns>
		public void GeneratePdbBUs(string crystalXmlFile, string destPath)
		{
			// read data from crystal xml file
            EntryCrystal thisEntryCrystal;
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(EntryCrystal));
			FileStream xmlFileStream = new FileStream(crystalXmlFile, FileMode.Open);
			thisEntryCrystal = (EntryCrystal) xmlSerializer.Deserialize (xmlFileStream);
			xmlFileStream.Close ();
			/* build crystal using symmetry operations from PDB XML file*/
			GeneratePdbBusByPdbSymOps(thisEntryCrystal.PdbId, thisEntryCrystal, destPath);
		}

		/// <summary>
		/// build PDB biological units
		/// by applying the symmetry operations provided in XML
		/// </summary>
		/// <param name="chainAtomsList">chains in XML</param>
		/// <param name="buGenStructList">symmetry operations</param>
		/// <param name="buSymmetryMatrixList">symmetry matrix</param>
		/// <returns>biological units with corresponding chains</returns>
		private void GeneratePdbBusByPdbSymOps(string pdbId,  EntryCrystal thisEntryCrystal, string destPath)
		{
			ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
			BuGenStruct[] buGenStructList = thisEntryCrystal.buGenCat.BuGenStructList;

			BuWriter buWriter = new BuWriter ();

			List<string> buIdList = new List<string>  ();
			for (int asymCount = 0; asymCount < buGenStructList.Length; asymCount ++)
			{
				string biolUnitId = buGenStructList[asymCount].biolUnitId;
				if (! buIdList.Contains (biolUnitId))
				{
					buIdList.Add (biolUnitId);
				}
			}
			// process biological units
			foreach (string buId in buIdList)
			{
				try
				{	
					Dictionary<string, AtomInfo[]> buChains = BuildPdbBuByPdbSymOps(pdbId, buId.ToString (), thisEntryCrystal);		
					
					// writer bu into a PDB formatted file
					string fileName = buWriter.WriteBiolUnitFile (pdbId, buId.ToString (), buChains, destPath, 
						thisEntryCrystal.crystal1, thisEntryCrystal.scaleCat.ScaleMatrix);	
					// Compress file
					ParseHelper.ZipPdbFile (fileName);
				}
				catch (Exception ex)
				{
					throw new Exception (string.Format ("Cannot generate biological unit file: {0}_{1}: {2}", pdbId, buId.ToString (), ex.Message)); 
				}
			}
        }
        #endregion

        #region generate PDB BUs
        private PdbDbBuGenerator buGenerator = new PdbDbBuGenerator();
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="needLigands"></param>
        /// <param name="authorOnly"></param>
        /// <param name="destPath"></param>
        public Dictionary<string, Dictionary<string, AtomInfo[]>> BuildPdbBus(string pdbId, string[] biolUnitIDs, bool needLigands)
        {
            EntryCrystal thisEntryCrystal = ReadEntryCrystalFromXml(pdbId);

            Dictionary<string, Dictionary<string, AtomInfo[]>> buHash = new Dictionary<string,Dictionary<string,AtomInfo[]>> ();
       //     buHash = buGenerator.BuildPdbBUsFromDb(pdbId, biolUnitIDs, thisEntryCrystal, needLigands);

            if (thisEntryCrystal.buGenCat.BuGenStructList.Length == 0) // no bu gen info, use asu
            {
                if (thisEntryCrystal.buGenCat.BuStatusInfoList.Length > 0 &&
                    thisEntryCrystal.buGenCat.BuStatusInfoList[0].details == "NMR")
                {
                    Dictionary<string, AtomInfo[]> asuChainHash = new Dictionary<string,AtomInfo[]> ();
                    foreach (ChainAtoms chain in thisEntryCrystal.atomCat.ChainAtomList)
                    {
                        if ((!needLigands) && (chain.PolymerType != "polypeptide"))
                        {
                            continue;
                        }
                        if (!asuChainHash.ContainsKey(chain.asymChain + "_1_555"))
                        {
                            asuChainHash.Add(chain.asymChain + "_1_555", chain.CartnAtoms);
                        }
                    }
                    buHash.Add("1", asuChainHash);
                }
                else
                {
                    buHash = buGenerator.BuildPdbBUsFromDb(pdbId, biolUnitIDs, thisEntryCrystal, needLigands);
                }
            }
            else
            {
                Dictionary<string, AtomInfo[]> buChainHash = null;
                try
                {
                    foreach (string buId in biolUnitIDs)
                    {
                        if (needLigands)
                        {
                            buChainHash = BuildFullPdbBuByPdbSymOps(pdbId, buId, thisEntryCrystal);
                        }
                        else
                        {
                            buChainHash = BuildPdbBuByPdbSymOps(pdbId, buId, thisEntryCrystal);
                        }
                        buHash.Add(buId, buChainHash);
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate " + pdbId + " PDB BUs errors: " + ex.Message);
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate BUs from DB info.");
                    buHash = buGenerator.BuildPdbBUsFromDb(pdbId, biolUnitIDs, thisEntryCrystal, needLigands);
                }
            }
            return buHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="needLigands"></param>
        /// <param name="authorOnly"></param>
        /// <param name="destPath"></param>
        public Dictionary<string, Dictionary<string, AtomInfo[]>> BuildPdbBus(string pdbId, bool needLigands)
        {
            EntryCrystal thisEntryCrystal = ReadEntryCrystalFromXml(pdbId);
           
            Dictionary<string, Dictionary<string, AtomInfo[]>> buHash = null;
            if (thisEntryCrystal.buGenCat.BuGenStructList.Length == 0) // no bu gen info, use asu
            {
                if (thisEntryCrystal.buGenCat.BuStatusInfoList.Length > 0 &&
                    thisEntryCrystal.buGenCat.BuStatusInfoList[0].details == "NMR")
                {
                    Dictionary<string, AtomInfo[]> asuChainHash = new Dictionary<string,AtomInfo[]> ();

                    foreach (ChainAtoms chain in thisEntryCrystal.atomCat.ChainAtomList)
                    {
                        if ((!needLigands) && (chain.PolymerType != "polypeptide"))
                        {
                            continue;
                        }
                        asuChainHash.Add(chain.asymChain + "_1_555", chain.CartnAtoms);
                    }
                    buHash.Add("1", asuChainHash);
                }
                else
                {
                    buHash = buGenerator.BuildPdbBUsFromDb(pdbId, thisEntryCrystal, needLigands);
                }
            }
            else
            {
                try
                {
                    if (needLigands)
                    {
                        buHash = BuildFullPdbBusByPdbSymOps(pdbId, thisEntryCrystal);
                    }
                    else
                    {
                        buHash = BuildPdbBusByPdbSymOps(pdbId, thisEntryCrystal);
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate " + pdbId + " PDB BUs errors: " + ex.Message);
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate BUs from DB info.");
                    buHash = buGenerator.BuildPdbBUsFromDb(pdbId, thisEntryCrystal, needLigands);
                }
            }
            return buHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbXmlFile">a xml file from pdb web site</param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, AtomInfo[]>> BuildPdbBAsAllAtoms(string pdbXmlFile)
        {
            if (pdbXmlFile.IndexOf(".gz") > -1)
            {
                string tempDir = "C:\\xml_temp";
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                pdbXmlFile = ParseHelper.UnZipFile(pdbXmlFile, tempDir);
            }

            FileParser.XmlAtomParser xmlParser = new CrystalInterfaceLib.FileParser.XmlAtomParser();
            EntryCrystal thisEntryCrystal = xmlParser.ParseXmlFile(pdbXmlFile);

            Dictionary<string, Dictionary<string, AtomInfo[]>> baChainHash = BuildPdbBAsAllAtoms(thisEntryCrystal);
            return baChainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="thisEntryCrystal"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, AtomInfo[]>> BuildPdbBAsAllAtoms(EntryCrystal thisEntryCrystal)
        {
            string pdbId = thisEntryCrystal.PdbId;
            Dictionary<string, Dictionary<string, AtomInfo[]>> buHash = null;
            if (thisEntryCrystal.buGenCat.BuGenStructList.Length == 0) // no bu gen info, use asu
            {
                if (thisEntryCrystal.buGenCat.BuStatusInfoList.Length > 0 &&
                    thisEntryCrystal.buGenCat.BuStatusInfoList[0].details == "NMR")
                {
                    Dictionary<string, AtomInfo[]> asuChainHash = new Dictionary<string,AtomInfo[]> ();

                    foreach (ChainAtoms chain in thisEntryCrystal.atomCat.ChainAtomList)
                    {
                        asuChainHash.Add(chain.asymChain + "_1_555", chain.CartnAtoms);
                    }
                    buHash.Add("1", asuChainHash);
                }
                else
                {
                    buHash = buGenerator.BuildPdbBUsFromDb(pdbId, thisEntryCrystal, true);
                }
            }
            else
            {
                try
                {
                    buHash = BuildFullPdbBusByPdbSymOps(pdbId, thisEntryCrystal);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate " + pdbId + " PDB BUs errors: " + ex.Message);
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Generate BUs from DB info.");
                    buHash = buGenerator.BuildPdbBUsFromDb(pdbId, thisEntryCrystal, true);
                }
            }
            return buHash;
        }

        /// <summary>
        /// build a PDB biological unit
        /// by applying the symmetry operations provided in XML
        /// </summary>
        /// <param name="chainAtomsList">chains in XML</param>
        /// <param name="buGenStructList">symmetry operations</param>
        /// <param name="buSymmetryMatrixList">symmetry matrix</param>
        /// <returns>biological units with corresponding chains</returns>
        /// </summary>
        private Dictionary<string, AtomInfo[]> BuildFullPdbBuByPdbSymOps(string pdbId, string biolUnitId, EntryCrystal thisEntryCrystal)
        {
            Dictionary<string, AtomInfo[]> buHash = null;

            ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
            BuGenStruct[] buGenStructList = thisEntryCrystal.buGenCat.BuGenStructList;
            
            List<string> symOpStrList = new List<string> ();
            List<string> fullSymOpStrList = new List<string> ();
            List<SymOpMatrix> symOpMatrixList = new List<SymOpMatrix> ();
            for (int asymCount = 0; asymCount < buGenStructList.Length; asymCount++)
            {
                if (biolUnitId != buGenStructList[asymCount].biolUnitId)
                {
                    continue;
                }
                string asymId = buGenStructList[asymCount].asymId;
                foreach (ChainAtoms chain in chainAtomsList)
                {
                    if (chain.AsymChain == asymId)
                    {
                        SymOpMatrix symOpMatrix =
                            thisEntryCrystal.buGenCat.FindSymOpMatrix(buGenStructList[asymCount].symOperId);
                        if (symOpMatrix == null)
                        {
                            fullSymOpStrList.Add(buGenStructList[asymCount].symmetryMatrix);
                            symOpStrList.Add(asymId + "_" + buGenStructList[asymCount].symmetryString);
                        }
                        else
                        {
                            if (symOpMatrix.symmetryString == ""  || symOpMatrix.symmetryString == "-")
                            {
                                // put the symmetry operator id if the symmetry string is empty
                                symOpStrList.Add(asymId + "_" + buGenStructList[asymCount].symOperId);
                            }
                            else
                            {
                                symOpStrList.Add(asymId + "_" + symOpMatrix.symmetryString);
                            }
                            symOpMatrixList.Add(symOpMatrix);
                        }
                    }
                }
            }
            
            string[] symOpStrings = symOpStrList.ToArray ();
            if (symOpMatrixList.Count > 0)
            {                
                buHash = BuildPdbBu(thisEntryCrystal, symOpStrings, symOpMatrixList.ToArray ());
            }
            else if (fullSymOpStrList.Count > 0)
            {
                buHash = BuildPdbBu(thisEntryCrystal, symOpStrings, fullSymOpStrList.ToArray ());
            }
            return buHash;
        }

        /// <summary>
        /// build PDB biological units
        /// by applying the symmetry operations provided in XML
        /// </summary>
        /// <param name="chainAtomsList">chains in XML</param>
        /// <param name="buGenStructList">symmetry operations</param>
        /// <param name="buSymmetryMatrixList">symmetry matrix</param>
        /// <returns>biological units with corresponding chains</returns>
        private Dictionary<string, Dictionary<string, AtomInfo[]>> BuildFullPdbBusByPdbSymOps(string pdbId, EntryCrystal thisEntryCrystal)
        {
            ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
            BuGenStruct[] buGenStructList = thisEntryCrystal.buGenCat.BuGenStructList;

            BuWriter buWriter = new BuWriter();

            List<string> buIdList = new List<string> ();
            for (int asymCount = 0; asymCount < buGenStructList.Length; asymCount++)
            {
                string biolUnitId = buGenStructList[asymCount].biolUnitId;
                if (!buIdList.Contains(biolUnitId))
                {
                    buIdList.Add(biolUnitId);
                }
            }
            // process biological units
            Dictionary<string, Dictionary<string, AtomInfo[]>> buHash = new Dictionary<string,Dictionary<string,AtomInfo[]>> ();
            foreach (string buId in buIdList)
            {
                try
                {
                    Dictionary<string, AtomInfo[]> buChains = BuildFullPdbBuByPdbSymOps(pdbId, buId.ToString(), thisEntryCrystal);
                    buHash.Add(buId, buChains);                   
                }
                catch (Exception ex)
                {
                    throw new Exception(string.Format("Cannot generate biological unit file: {0}_{1}: {2}", pdbId, buId.ToString(), ex.Message));
                }
            }
            return buHash;
        }

        /// <summary>
        /// build PDB biological units
        /// by applying the symmetry operations provided in XML
        /// </summary>
        /// <param name="chainAtomsList">chains in XML</param>
        /// <param name="buGenStructList">symmetry operations</param>
        /// <param name="buSymmetryMatrixList">symmetry matrix</param>
        /// <returns>biological units with corresponding chains</returns>
        private Dictionary<string, Dictionary<string, AtomInfo[]>> BuildPdbBusByPdbSymOps(string pdbId, EntryCrystal thisEntryCrystal)
        {
            ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
            BuGenStruct[] buGenStructList = thisEntryCrystal.buGenCat.BuGenStructList;

            BuWriter buWriter = new BuWriter();

            List<string> buIdList = new List<string> ();
            for (int asymCount = 0; asymCount < buGenStructList.Length; asymCount++)
            {
                string biolUnitId = buGenStructList[asymCount].biolUnitId;
                if (!buIdList.Contains(biolUnitId))
                {
                    buIdList.Add(biolUnitId);
                }
            }
            // process biological units
            Dictionary<string, Dictionary<string, AtomInfo[]>> buHash = new Dictionary<string,Dictionary<string,AtomInfo[]>> ();
            foreach (string buId in buIdList)
            {
                try
                {
                    Dictionary<string, AtomInfo[]> buChains = BuildPdbBuByPdbSymOps(pdbId, buId.ToString(), thisEntryCrystal);

                    buHash.Add(buId, buChains);
                }
                catch (Exception ex)
                {
                    throw new Exception(string.Format("Cannot generate biological unit file: {0}_{1}: {2}", pdbId, buId.ToString(), ex.Message));
                }
            }
            return buHash;
        }
		#endregion
       
        #region build PDB BUs from coordinate XML file
        /// <summary>
		/// build PDB biological units
		/// by applying the symmetry operations provided in XML
		/// </summary>
		/// <param name="xmlFile"></param>
		/// <returns></returns>
        public Dictionary<string, Dictionary<string, AtomInfo[]>> BuildPdbBusFromCoordFile(string xmlFile, string atomType)
		{
			string pdbId = xmlFile.Substring (xmlFile.LastIndexOf ("\\") + 1, 4);
			// key: biolUnitID,  Value: Hashtable (key: )
			Dictionary<string, Dictionary<string, AtomInfo[]>> entryBuChainsHash = new Dictionary<string,Dictionary<string,AtomInfo[]>> ();
			// read data from crystal xml file
			EntryCrystal thisEntryCrystal;
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(EntryCrystal));
			FileStream xmlFileStream = new FileStream(xmlFile, FileMode.Open);
			thisEntryCrystal = (EntryCrystal) xmlSerializer.Deserialize (xmlFileStream);
			xmlFileStream.Close ();

			ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
			BuGenStruct[] buGenStructList = thisEntryCrystal.buGenCat.BuGenStructList;

			List<string> buIdList = new List<string>  ();
			for (int asymCount = 0; asymCount < buGenStructList.Length; asymCount ++)
			{
				string biolUnitId = buGenStructList[asymCount].biolUnitId;
				if (! buIdList.Contains (biolUnitId))
				{
					buIdList.Add (biolUnitId);
				}
			}
			// process biological units
			foreach (string buId in buIdList)
			{
				try
				{	
					Dictionary<string, AtomInfo[]> buChains = BuildPdbBuByPdbSymOps(pdbId, buId.ToString (), thisEntryCrystal);
                    if (buChains == null || buChains.Count == 0)
                    {
                        continue;
                    }
                    entryBuChainsHash.Add (buId.ToString (), buChains);
				}
				catch (Exception ex)
				{
					string msg = string.Format ("Cannot generate biological unit file: {0}_{1}: {2}", pdbId, buId.ToString (), ex.Message); 
				}
			}
			return entryBuChainsHash;
		}

        /// <summary>
        /// build PDB biological units
        /// by applying the symmetry operations provided in XML
        /// </summary>
        /// <param name="xmlFile"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, AtomInfo[]>> BuildPdbBusFromCoordFile(string xmlFile, string[] buIDs, string atomType)
        {
            string pdbId = xmlFile.Substring(xmlFile.LastIndexOf("\\") + 1, 4);
            // key: biolUnitID,  Value: Hashtable (key: )
            Dictionary<string, Dictionary<string, AtomInfo[]>> entryBuChainsHash = new Dictionary<string, Dictionary<string, AtomInfo[]>>();
            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(xmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

     //       ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
    //        BuGenStruct[] buGenStructList = thisEntryCrystal.buGenCat.BuGenStructList;

            // process biological units
            foreach (string buId in buIDs)
            {
                try
                {
                    Dictionary<string, AtomInfo[]> buChains = BuildPdbBuByPdbSymOps(pdbId, buId, thisEntryCrystal);
                    if (buChains == null || buChains.Count == 0)
                    {
                        continue;
                    }
                    entryBuChainsHash.Add(buId.ToString(), buChains);
                }
                catch (Exception ex)
                {
                    string msg = string.Format("Cannot generate biological unit file: {0}_{1}: {2}", pdbId, buId.ToString(), ex.Message);
                }
            }
            return entryBuChainsHash;
        }
		/// <summary>
		/// build PDB biological units
		/// by applying the symmetry operations provided in XML
		/// </summary>
		/// <param name="xmlFile"></param>
		/// <returns></returns>
        public Dictionary<string, Dictionary<string, AtomInfo[]>> BuildPdbBusFromCoordFile(string pdbId, EntryCrystal thisEntryCrystal, string atomType)
		{
			// key: biolUnitID,  Value: Hashtable (key: )
            Dictionary<string, Dictionary<string, AtomInfo[]>> entryBuChainsHash = new Dictionary<string, Dictionary<string, AtomInfo[]>>();

			ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
			BuGenStruct[] buGenStructList = thisEntryCrystal.buGenCat.BuGenStructList;

			List<string> buIdList = new List<string> ();
			for (int asymCount = 0; asymCount < buGenStructList.Length; asymCount ++)
			{
				string biolUnitId = buGenStructList[asymCount].biolUnitId;
				if (! buIdList.Contains (biolUnitId))
				{
					buIdList.Add (biolUnitId);
				}
			}
			// process biological units
			foreach (string buId in buIdList)
			{
				try
				{	
					Dictionary<string, AtomInfo[]> buChains = BuildPdbBuByPdbSymOps(pdbId, buId.ToString (), thisEntryCrystal);
                    if (buChains == null || buChains.Count == 0)
                    {
                        continue;
                    }
                    entryBuChainsHash.Add (buId.ToString (), buChains);
				}
				catch (Exception ex)
				{
					string msg = string.Format ("Cannot generate biological unit file: {0}_{1}: {2}", pdbId, buId.ToString (), ex.Message); 
				}
			}
			return entryBuChainsHash;
		}
		#endregion

		#region Build a PDB BU by specific symmetry operations
		/// <summary>
		/// build a structure from XML coordinate files 
		/// and specific symmetry operations
		/// write the structure to a pdb formated file
		/// </summary>
		/// <param name="xmlFile"></param>
		/// <param name="buId"></param>
		/// <param name="symOpStrings"></param>
		/// <param name="destPath"></param>
		public void BuildPdbBuBySymOps (string xmlFile, string[] symOpStrings, string buId, 
			int interfaceGroupId, int groupId, string destPath)
		{
			if (!  Directory.Exists (Path.Combine (destPath, "temp")))
			{
				Directory.CreateDirectory (Path.Combine (destPath, "temp"));
			}
			BuWriter buWriter = new BuWriter ();
			Dictionary<string, AtomInfo[]> buChainsHash = base.BuildCrystal (xmlFile, symOpStrings);
			string pdbId = xmlFile.Substring (xmlFile.LastIndexOf ("\\") + 1, 4);
			string fileName = buWriter.WriteBiolUnitFile (pdbId, buId, buChainsHash, Path.Combine (destPath, "temp"), base.Crystal1, base.cartn2fractMatrix);
#if DEBUG
			// add group number, interface group number into the file name
			// e.g. 29_3_PDBID_buID: group ID = 29, interface group ID = 3
			int fileNameIndex = fileName.LastIndexOf ("\\");
			string newFileName = Path.Combine (fileName.Substring (0, fileNameIndex - 4), 
				groupId + "_" + interfaceGroupId + "_" + fileName.Substring (fileNameIndex + 1, fileName.Length - fileNameIndex - 1));
			if (File.Exists (newFileName))
			{
				File.Delete (newFileName);
			}
			File.Move (fileName, newFileName);
			File.Delete (fileName);
			ParseHelper.ZipPdbFile (newFileName);
#endif
		}

		/// <summary>
		/// build a structure from XML coordinate files 
		/// and specific symmetry operations
		/// write the structure to a pdb formated file
		/// </summary>
		/// <param name="xmlFile"></param>
		/// <param name="buId"></param>
		/// <param name="symOpStrings"></param>
		/// <param name="destPath"></param>
		public void BuildPdbBuBySymOps (string xmlFile, string[] symOpStrings, string buId, string destPath)
		{
			BuWriter buWriter = new BuWriter ();
			Dictionary<string, AtomInfo[]> buChainsHash = base.BuildCrystal (xmlFile, symOpStrings);
			string pdbId = xmlFile.Substring (xmlFile.LastIndexOf ("\\") + 1, 4);
			string fileName = buWriter.WriteBiolUnitFile (pdbId, buId, buChainsHash, destPath, base.Crystal1, base.cartn2fractMatrix);
			ParseHelper.ZipPdbFile (fileName);
		}
		
		#endregion

		#region Build a specific PDB BU containiong only peptide chains
		/// <summary>
		/// build biological units from coordinates XML 
		/// and symmetry Matrix files provided by RCSB
		/// </summary>
		/// <param name="crystalXmlFile">XML file with coordinates and symmetry operations</param>
		/// <returns>biological units build from coordinates and symmetry operations</returns>
        public Dictionary<string, AtomInfo[]> BuildPdbBU(string crystalXmlFile, string biolUnitId, string atomType)
		{
			// read data from crystal xml file
			EntryCrystal thisEntryCrystal;
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(EntryCrystal));
			FileStream xmlFileStream = new FileStream(crystalXmlFile, FileMode.Open);
			thisEntryCrystal = (EntryCrystal) xmlSerializer.Deserialize (xmlFileStream);
			xmlFileStream.Close ();

			/* build crystal using symmetry operations from PDB XML file*/
			return BuildPdbBuByPdbSymOps(thisEntryCrystal.PdbId, biolUnitId, thisEntryCrystal);
		}

		/// <summary>
		/// build a PDB biological unit
		/// by applying the symmetry operations provided in XML
		/// </summary>
		/// <param name="chainAtomsList">chains in XML</param>
		/// <param name="buGenStructList">symmetry operations</param>
		/// <param name="buSymmetryMatrixList">symmetry matrix</param>
		/// <returns>biological units with corresponding chains</returns>
        /// </summary>
        private Dictionary<string, AtomInfo[]> BuildPdbBuByPdbSymOps(string pdbId, string biolUnitId, EntryCrystal thisEntryCrystal)
        {
            ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
            BuGenStruct[] buGenStructList = thisEntryCrystal.buGenCat.BuGenStructList;

            List<string> symOpStrList = new List<string> ();
            List<string> fullSymOpStrList = new List<string> ();
            List<SymOpMatrix> symOpMatrixList = new List<SymOpMatrix> ();
            for (int asymCount = 0; asymCount < buGenStructList.Length; asymCount++)
            {
                if (biolUnitId != buGenStructList[asymCount].biolUnitId)
                {
                    continue;
                }
                string asymId = buGenStructList[asymCount].asymId;
                foreach (ChainAtoms chain in chainAtomsList)
                {
                    if (chain.AsymChain == asymId)
                    {
                        // at this step, only consider polypeptide chains
                        if (chain.PolymerType == "polypeptide")
                        {
                            SymOpMatrix symOpMatrix =
                                thisEntryCrystal.buGenCat.FindSymOpMatrix(buGenStructList[asymCount].symOperId);
                            if (symOpMatrix == null)
                            {
                                fullSymOpStrList.Add(buGenStructList[asymCount].symmetryMatrix);
                                symOpStrList.Add(asymId + "_" + buGenStructList[asymCount].symmetryString);
                            }
                            else
                            {
                                if (symOpMatrix.symmetryString == "")
                                {
                                    // put the symmetry operator id if the symmetry string is empty
                                    symOpStrList.Add(asymId + "_" + buGenStructList[asymCount].symOperId);
                                }
                                else
                                {
                                    symOpStrList.Add(asymId + "_" + symOpMatrix.symmetryString);
                                }
                                symOpMatrixList.Add(symOpMatrix);
                            }
                            break;
                        }
                    }
                }
            }
            Dictionary<string, AtomInfo[]> buHash = null;
            string[] symOpStrings = new string[symOpStrList.Count];
            symOpStrList.CopyTo(symOpStrings);
            if (symOpMatrixList.Count > 0)
            {
                SymOpMatrix[] symOpMatrices = new SymOpMatrix[symOpMatrixList.Count];
                symOpMatrixList.CopyTo(symOpMatrices);
                buHash = BuildPdbBu(thisEntryCrystal, symOpStrings, symOpMatrices);
            }
            else if (fullSymOpStrList.Count > 0)
            {
                string[] fullSymmetryStrings = new string[fullSymOpStrList.Count];
                fullSymOpStrList.CopyTo(fullSymmetryStrings);
                buHash = BuildPdbBu(thisEntryCrystal, symOpStrings, fullSymmetryStrings);
            }
            return buHash;
        }

        /// <summary>
        /// build crystal from the xml serialization object and full symmetry operators
        /// </summary>
        /// <param name="thisEntryCrystal"></param>
        /// <param name="symOpStrings"></param>
        /// <param name="fullSymOpStrings"></param>
        /// <returns></returns>
        public Dictionary<string, AtomInfo[]> BuildPdbBu(EntryCrystal thisEntryCrystal, string[] symOpStrings, SymOpMatrix[] symOpMatrices)
        {
            AtomInfo[] transformedAtoms = null;
            Dictionary<string, AtomInfo[]> crystalChainsHash = new Dictionary<string, AtomInfo[]>();

            //ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
            ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;

            for (int i = 0; i < symOpStrings.Length; i++)
            {
                if (crystalChainsHash.ContainsKey(symOpStrings[i]))
                {
                    continue;
                }
                string chainId = symOpStrings[i].Substring(0, symOpStrings[i].IndexOf("_"));
                string symOpString = symOpStrings[i].Substring(symOpStrings[i].IndexOf("_") + 1,
                    symOpStrings[i].Length - symOpStrings[i].IndexOf("_") - 1);
                int asymCount = 0;
                for (asymCount = 0; asymCount < chainAtomsList.Length; asymCount++)
                {
                    string asymChain = chainAtomsList[asymCount].AsymChain;
                    if (asymChain == chainId)
                    {
                        break;
                    }
                }
                if (symOpString == origSymOpString)
                {
                    crystalChainsHash.Add(symOpStrings[i], chainAtomsList[asymCount].CartnAtoms);
                    continue;
                }
                transformedAtoms = TransformChainByCartesianSymOp (chainAtomsList[asymCount].CartnAtoms, symOpMatrices[i]);

                crystalChainsHash.Add(symOpStrings[i], transformedAtoms);
            }
            return crystalChainsHash;
        }

        /// <summary>
        /// build crystal from the xml serialization object and full symmetry operators
        /// </summary>
        /// <param name="thisEntryCrystal"></param>
        /// <param name="symOpStrings"></param>
        /// <param name="fullSymOpStrings"></param>
        /// <returns></returns>
        public Dictionary<string, AtomInfo[]> BuildPdbBu(EntryCrystal thisEntryCrystal, string[] symOpStrings, string[] fullSymOpStrings)
        {
            SymOperator symOp = new SymOperator();
            CrystalInfo crystal1 = thisEntryCrystal.crystal1;

            Dictionary<string, AtomInfo[]> crystalChainsHash = new Dictionary<string, AtomInfo[]>();

            base.cartn2fractMatrix = thisEntryCrystal.scaleCat.ScaleMatrix;
            base.fract2cartnMatrix = cartn2fractMatrix.Inverse();
            AtomInfo[] transformedAtoms = null;
            SymOpMatrix symOpMatrix = null;

            //ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
            ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;

            for (int i = 0; i < symOpStrings.Length; i++)
            {
                if (crystalChainsHash.ContainsKey(symOpStrings[i]))
                {
                    continue;
                }
                string chainId = symOpStrings[i].Substring(0, symOpStrings[i].IndexOf("_"));
                string symOpString = symOpStrings[i].Substring(symOpStrings[i].IndexOf("_") + 1,
                    symOpStrings[i].Length - symOpStrings[i].IndexOf("_") - 1);
                int asymCount = 0;
                for (asymCount = 0; asymCount < chainAtomsList.Length; asymCount++)
                {
                    string asymChain = chainAtomsList[asymCount].AsymChain;
                    if (asymChain == chainId)
                    {
                        break;
                    }
                }
                if (symOpString == origSymOpString)
                {
                    crystalChainsHash.Add(symOpStrings[i], chainAtomsList[asymCount].CartnAtoms);
                    continue;
                }
                // get the symmetry operator matrix

                if (IsFullSymOpStringAMatrix(fullSymOpStrings[i]))
                {
                    symOpMatrix = GetSymMatrixFromMatrixString(fullSymOpStrings[i]);
                    transformedAtoms = TransformChainByCartesianSymOp
                        (chainAtomsList[asymCount].CartnAtoms, symOpMatrix);
                }
                else
                {
                    symOpMatrix = symOp.GetSymMatrix(fullSymOpStrings[i], symOpStrings[i]);
                    transformedAtoms = TransformChainBySpecificSymOp (chainAtomsList[asymCount].CartnAtoms, symOpMatrix);
                }
                crystalChainsHash.Add(symOpStrings[i], transformedAtoms);
            }
            return crystalChainsHash;
        }

        /// <summary>
        /// the symmetry matrix string has 12 fields
        /// </summary>
        /// <param name="fullSymOpString"></param>
        /// <returns></returns>
        private bool IsFullSymOpStringAMatrix(string fullSymOpString)
        {
            string[] fields = fullSymOpString.Split(',');
            if (fields.Length != 12)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// the symmetry matrix from matrix string
        /// s00, s01, s02, v0, s10, s11, s12, v1, s20, s21, s22, v2
        /// </summary>
        /// <param name="matrixString"></param>
        /// <returns></returns>
        private SymOpMatrix GetSymMatrixFromMatrixString(string matrixString)
        {
            string[] fields = matrixString.Split(',');
            SymOpMatrix symOpMatrix = new SymOpMatrix();
            int numOfCol = 4;
            int colNum = -1;
            int rowNum = -1;
            for (int i = 0; i < fields.Length; i++)
            {
                colNum++;
                if (i % numOfCol == 0)
                {
                    rowNum++;
                    colNum = 0;
                }
                symOpMatrix.Add(rowNum, colNum, Convert.ToDouble(fields[i]));
            }
            return symOpMatrix;
        }
		#endregion

		#region others
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public EntryCrystal ReadEntryCrystalFromXml(string pdbId)
        {
            string xmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string crystalXmlFile = ParseHelper.UnZipFile(xmlFile, ProtCidSettings.tempDir);

            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(crystalXmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            File.Delete(crystalXmlFile);

            return thisEntryCrystal;
        }

        /// <summary>
        /// transform a protein chain by applying a specific symmetry operation
        /// either from PDB xml or from a user-defined
        /// </summary>
        /// <param name="chainCoordList"></param>
        /// <param name="symMatrix"></param>
        /// <returns></returns>
        private AtomInfo[] TransformChainByCartesianSymOp(AtomInfo[] chainAtomList, SymOpMatrix symMatrix)
        {
            AtomInfo[] transformedAtomList = new AtomInfo[chainAtomList.Length];
            for (int atomI = 0; atomI < chainAtomList.Length; atomI++)
            {
                AtomInfo thisAtomInfo = chainAtomList[atomI].TransformAtom(symMatrix);
                transformedAtomList[atomI] = thisAtomInfo;
            }
            return transformedAtomList;
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="srcPath"></param>
		/// <param name="destPath"></param>
		/// <returns></returns>
        private string[] GetFileList (string srcPath, string destPath)
		{
			string[] srcFiles = Directory.GetFiles (srcPath, "*.xml.gz");
			string[] destFiles = Directory.GetFiles (destPath, "*.pbu*");
			List<string> destPdbIdList = new List<string>  ();
			for (int i = 0; i < destFiles.Length; i ++)
			{
				int fileIndex = destFiles[i].LastIndexOf ("\\");
				string pdbId = destFiles[i].Substring (fileIndex + 1, 4);
				destPdbIdList.Add (pdbId);
			}
			destPdbIdList.Sort ();
			if (destFiles.Length == 0)
			{
				return srcFiles;
			}
			else
			{
				List<string> fileList = new List<string>  ();
				foreach (string srcFile in srcFiles)
				{
					int fileIndex = srcFile.LastIndexOf ("\\");
					string pdbId = srcFile.Substring (fileIndex + 1, 4);
					if (destPdbIdList.BinarySearch (pdbId) < 0)
					{
						fileList.Add (srcFile);
					}
				}
				string[] files = new string [fileList.Count];
				fileList.CopyTo (files);
				return files;
			}
		}
		#endregion
	}
}
