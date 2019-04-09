using System;
using System.IO;
using System.Collections.Generic;
using System.Data;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Crystal;

namespace CrystalInterfaceLib.BuIO
{
	/// <summary>
	/// Write data to a PDB formatted text file
	/// </summary>
	public class BuWriter
	{
		private string filePath = "";
		public const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private int maxAtomId = 99999;

		public BuWriter()
		{
		}

		public BuWriter (string fileDir)
		{
			filePath = fileDir;
		}

		#region write a biological unit

		#region public interface
		/// <summary>
		/// write the interactive chains into a pdb formated text file
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="interChains"></param>
		public string WriteBiolUnitFile (string pdbId, string buId, Dictionary<string, AtomInfo[]> buChains, string destDir,
			CrystalInfo cryst1, Matrix fractMatrix)
		{
			string fileName = Path.Combine (destDir, pdbId + "_" + buId.ToString () +  ".pdb");
			FileStream fileStream = new FileStream (Path.Combine (filePath, fileName), FileMode.Create, FileAccess.Write);
			StreamWriter fileWriter = new StreamWriter (fileStream);
			string header = "HEADER    " + pdbId + "                    " + DateTime.Today;
			fileWriter.WriteLine (header);
			string remark = "REMARK 290 ";
			int count = 0;
			List<string> chainSymOpList = new List<string> (buChains.Keys);
			string[] chainSymOpStrs = SortChainSymOpList (chainSymOpList);
			foreach (string chainAndSymOpStr in chainSymOpStrs)
			{
				string thisRemark = remark + letters[count].ToString () + ": ";
				// format of chainAndSymOpStr: chain_symOpNum_fullsymString
				string[] fields = chainAndSymOpStr.Split ('_');
				thisRemark += fields[0].PadRight (3, ' ');
				thisRemark += fields[1].PadRight (3, ' ');
				thisRemark += fields[2].PadRight (30, ' ');
				fileWriter.WriteLine (thisRemark);
				count ++;	
			}
			string crystString = FormatCrystString (cryst1);
			string fractString = FormatFractString (fractMatrix);
			fileWriter.WriteLine (crystString);
			fileWriter.WriteLine (fractString);


			int atomId = 0;
			count = 0;
			try
			{
				string preSymOpNum = "0";
				foreach (string chain in chainSymOpStrs)
				{
					string[] fields = chain.Split ('_');
					string symOpNum = fields[1];
					if (preSymOpNum != symOpNum)
					{
						atomId = 0;
					}
					ChainAtoms buChain = new ChainAtoms ();
					buChain.CartnAtoms = (AtomInfo[]) buChains[chain];
					buChain.SortByAtomId ();
					string chainId = letters[count % letters.Length].ToString ();
					WriteOneChain(buChain.CartnAtoms, chainId, ref atomId, fileWriter);
					count ++;
					preSymOpNum = symOpNum;
				}
				fileWriter.WriteLine ("END");
			}
			catch (Exception ex)
			{
				string errorMsg = ex.Message;
				throw ex;
			}
			finally
			{
				fileWriter.Close ();
			}
			return fileName;
		}

		/// <summary>
		/// write the interactive chains into a pdb formated text file
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="interChains"></param>
		public string WriteBiolUnitFile (string pdbId, string buId, Dictionary<string, AtomInfo[]> buChains, string destDir, string type)
		{
			string fileName = Path.Combine (destDir, pdbId + "_" + buId +  "." + type);
			FileStream fileStream = new FileStream (Path.Combine (filePath, fileName), FileMode.Create, FileAccess.Write);
			StreamWriter fileWriter = new StreamWriter (fileStream);
			string header = "HEADER    " + pdbId + "                    " + DateTime.Today;
			fileWriter.WriteLine (header);
		
			List<string> chainSymOpList = new List<string> (buChains.Keys);
			string[] chainSymOpStrs = SortChainSymOpList (chainSymOpList);

			int atomId = 0;
			int count = 0;
			try
			{
				string preSymOpNum = "0";
				foreach (string chain in chainSymOpStrs)
				{
					string[] fields = chain.Split ('_');
					string symOpNum = fields[1];
					if (preSymOpNum != symOpNum)
					{
						atomId = 0;
					}
					ChainAtoms buChain = new ChainAtoms ();
					buChain.CartnAtoms = (AtomInfo[]) buChains[chain];
					buChain.SortByAtomId ();
					string chainId = letters[count % letters.Length].ToString ();
					WriteOneChain(buChain.CartnAtoms, chainId, ref atomId, fileWriter);
					count ++;
					preSymOpNum = symOpNum;
				}
				fileWriter.WriteLine ("END");
			}
			catch (Exception ex)
			{
				string errorMsg = ex.Message;
				throw ex;
			}
			finally
			{
				fileWriter.Close ();
			}
			return fileName;
		}

        /// <summary>
        /// write the interactive chains into a pdb formated text file
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interChains"></param>
        public string WriteBiolUnitFile(string pdbId, string buId, Dictionary<string, AtomInfo[]> buChains,  string destDir, string remark, string type)
        {
            string fileName = Path.Combine(destDir, pdbId + "_" + buId + "." + type);
            FileStream fileStream = new FileStream(Path.Combine(filePath, fileName), FileMode.Create, FileAccess.Write);
            StreamWriter fileWriter = new StreamWriter(fileStream);
            string header = "HEADER    " + pdbId + "                    " + DateTime.Today;
            fileWriter.WriteLine(header);
            fileWriter.WriteLine(remark);

            List<string> chainSymOpList = new List<string> (buChains.Keys);
            string[] chainSymOpStrs = SortChainSymOpList(chainSymOpList);

            int atomId = 0;
            int count = 0;
            try
            {
                string preSymOpNum = "0";
                foreach (string chain in chainSymOpStrs)
                {
                    string[] fields = chain.Split('_');
                    string symOpNum = fields[1];
                    if (preSymOpNum != symOpNum)
                    {
                        atomId = 0;
                    }
                    ChainAtoms buChain = new ChainAtoms();
                    buChain.CartnAtoms = (AtomInfo[])buChains[chain];
                    buChain.SortByAtomId();
                    string chainId = letters[count % letters.Length].ToString();
                    WriteOneChain(buChain.CartnAtoms, chainId, ref atomId, fileWriter);
                    count++;
                    preSymOpNum = symOpNum;
                }
                fileWriter.WriteLine("END");
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message;
                throw ex;
            }
            finally
            {
                fileWriter.Close();
            }
            return fileName;
        }

        /// <summary>
        /// write the interactive chains into a pdb formated text file
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interChains"></param>
        public string WriteBiolUnitFile(string pdbId, string buId, Dictionary<string, AtomInfo[]> buChains, string destDir, DataTable asuInfoTable, string type)
        {
            string fileName = Path.Combine(destDir, pdbId + "_" + buId + "." + type);
            FileStream fileStream = new FileStream(Path.Combine(filePath, fileName), FileMode.Create, FileAccess.Write);
            StreamWriter fileWriter = new StreamWriter(fileStream);
            string header = "HEADER    " + pdbId + "                    " + DateTime.Today;
            fileWriter.WriteLine(header);

            List<string> chainSymOpList = new List<string> (buChains.Keys);
            chainSymOpList.Sort();
            string[] chainSymOpStrs = new string[chainSymOpList.Count];
            chainSymOpList.CopyTo(chainSymOpStrs);
     //       string[] chainSymOpStrs = SortChainSymOpList(chainSymOpList);

            Dictionary<string, string> asymPolymerTypeHash = GetAsymChainPolymerTypeHash(asuInfoTable);
            string chainRemarkString = GetChainComponentRemarkString(chainSymOpStrs, asuInfoTable, type);
            fileWriter.WriteLine(chainRemarkString);

            bool isChainLigand = false;
            int atomId = 0;
            int count = 0;
            try
            {
                string preSymOpNum = "0";
                foreach (string chain in chainSymOpStrs)
                {
                    string[] fields = chain.Split('_');
                    string symOpNum = fields[1];
                    if (preSymOpNum != symOpNum)
                    {
                        atomId = 0;
                    }
                    isChainLigand = IsChainLigand(fields[0], asymPolymerTypeHash);
                    ChainAtoms buChain = new ChainAtoms();
                    buChain.CartnAtoms = (AtomInfo[])buChains[chain];
                    buChain.SortByAtomId();
                    string chainId = letters[count % letters.Length].ToString();
                    WriteOneChain(buChain.CartnAtoms, chainId, ref atomId, isChainLigand, fileWriter);
                    count++;
                    preSymOpNum = symOpNum;
                }
                fileWriter.WriteLine("END");
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message;
                throw ex;
            }
            finally
            {
                fileWriter.Close();
            }
            return fileName;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="asuInfoTable"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetAsymChainPolymerTypeHash (DataTable asuInfoTable)
        {
            Dictionary<string, string> asymChainPolymerTypeHash = new Dictionary<string, string>();
            string asymId = "";
            string polymerType = "";
            foreach (DataRow dataRow in asuInfoTable.Rows)
            {
                asymId = dataRow["AsymID"].ToString().TrimEnd();
                polymerType = dataRow["PolymerType"].ToString().TrimEnd();
                if (polymerType == "-")
                {
                    polymerType = "ligand";
                }
                asymChainPolymerTypeHash.Add(asymId, polymerType);
            }
            return asymChainPolymerTypeHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymId"></param>
        /// <param name="asymChainPolymerTypeHash"></param>
        /// <returns></returns>
        private bool IsChainLigand(string asymId, Dictionary<string, string> asymChainPolymerTypeHash)
        {
            if (asymChainPolymerTypeHash.ContainsKey(asymId))
            {
                string polymerType = (string)asymChainPolymerTypeHash[asymId];
                if (polymerType != "ligand")
                {
                    return false;
                }
            }
            return true;
        }
		#endregion

        #region write asu
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asuChains"></param>
        /// <param name="destDir"></param>
        /// <param name="remark"></param>
        /// <returns></returns>
        public string WriteAsymUnitFile(string pdbId, Dictionary<string, AtomInfo[]> asuChainsHash, string destDir, string remark)
        {
            string fileName = Path.Combine(destDir, pdbId + ".ent");
            FileStream fileStream = new FileStream(Path.Combine(filePath, fileName), FileMode.Create, FileAccess.Write);
            StreamWriter fileWriter = new StreamWriter(fileStream);
            string header = "HEADER    " + pdbId + "                    " + DateTime.Today;
            fileWriter.WriteLine(header);
            fileWriter.WriteLine(remark);

            List<string> asymChainList = new List<string>(asuChainsHash.Keys);
            asymChainList.Sort();
            int atomId = 0;
            try
            {
                foreach (string asymChain in asymChainList)
                {
                    ChainAtoms asuChain = new ChainAtoms();
                    asuChain.CartnAtoms = asuChainsHash[asymChain];
                    asuChain.SortByAtomId();
                    WriteOneChain(asuChain.CartnAtoms, asymChain, ref atomId, fileWriter);
                }
                fileWriter.WriteLine("END");
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message;
                throw ex;
            }
            finally
            {
                fileWriter.Close();
            }
            return fileName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asuChains"></param>
        /// <param name="destDir"></param>
        /// <param name="remark"></param>
        /// <returns></returns>
        public string WriteAsymUnitFile(string pdbId, Dictionary<string, AtomInfo[]> asuChainsHash, string[] asymChainsInOrder, 
            string[] fileChainsInOrder, string[] nonpolymerAsymChains, string destDir, string remark)
        {
            string fileName = Path.Combine(destDir, pdbId + ".ent");
            FileStream fileStream = new FileStream(Path.Combine(filePath, fileName), FileMode.Create, FileAccess.Write);
            StreamWriter fileWriter = new StreamWriter(fileStream);
            string header = "HEADER    " + pdbId + "                    " + DateTime.Today;
            fileWriter.WriteLine(header);
            fileWriter.WriteLine(remark);

            int atomId = 0;
            int chainCount = 0;
            string fileChain = "";
            bool nonpolymerChain = false;
            try
            {
                foreach (string asymChain in asymChainsInOrder)
                {
                    nonpolymerChain = false;
                    ChainAtoms asuChain = new ChainAtoms();
                    asuChain.CartnAtoms = asuChainsHash[asymChain];
                    asuChain.SortByAtomId();
                    fileChain = fileChainsInOrder[chainCount];
                    if (Array.IndexOf(nonpolymerAsymChains, asymChain) > -1)
                    {
                        nonpolymerChain = true;
                    }
                    WriteOneChain(asuChain.CartnAtoms, fileChain, ref atomId, nonpolymerChain, fileWriter);
                    chainCount++;
                }
                fileWriter.WriteLine("END");
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message;
                throw ex;
            }
            finally
            {
                fileWriter.Close();
            }
            return fileName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asuChains"></param>
        /// <param name="destDir"></param>
        /// <param name="remark"></param>
        /// <returns></returns>
        public void WriteAsymUnitFile(string coordFileName, Dictionary<string, AtomInfo[]> asuChainsHash, string[] asymChainsInOrder, string[] fileChainsInOrder, string[] hetChains, string remark)
        {
            FileStream fileStream = new FileStream(coordFileName, FileMode.Create, FileAccess.Write);
            StreamWriter fileWriter = new StreamWriter(fileStream);
            fileWriter.WriteLine(remark);

            int atomId = 0;
            int chainCount = 0;
            string fileChain = "";
            bool isHetatmChain = false;
            try
            {
                foreach (string asymChain in asymChainsInOrder)
                {
                    isHetatmChain = false;
                    if (asuChainsHash.ContainsKey(asymChain))
                    {
                        ChainAtoms asuChain = new ChainAtoms();
                        asuChain.CartnAtoms = asuChainsHash[asymChain];
                        asuChain.SortByAtomId();
                        fileChain = fileChainsInOrder[chainCount];
                        if (Array.IndexOf(hetChains, asymChain) > -1)
                        {
                            isHetatmChain = true;
                        }
                        WriteOneChain(asuChain.CartnAtoms, fileChain, ref atomId, isHetatmChain, fileWriter);
                        chainCount++;
                    }
                }
                fileWriter.WriteLine("END");
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message;
                throw ex;
            }
            finally
            {
                fileWriter.Close();
            }
        }
        #endregion

        #region helper functions
        /// <summary>
		/// sort chains by symmetry operator numbers and chain IDs
		/// </summary>
		/// <param name="chainSymOpList"></param>
		/// <returns></returns>
		private string[] SortChainSymOpList (List<string> chainSymOpList)
		{
			Dictionary<string, List<string>> symOpHash = new Dictionary<string,List<string>> ();
			string[] chainAndSymOpStrings = new string [chainSymOpList.Count];
			if (chainSymOpList.Count == 1)
			{
				chainAndSymOpStrings[0] = chainSymOpList[0].ToString ();
				return chainAndSymOpStrings;
			}
			string symOpNum = "-1";
			foreach (string chainAndSymOp in chainSymOpList)
			{
				string chainAndSymOpStr = chainAndSymOp.ToString ();
				int symOpStartIndex = chainAndSymOpStr.IndexOf ("_");
				int symOpEndIndex = chainAndSymOpStr.LastIndexOf ("_");
				if (symOpStartIndex >= symOpEndIndex)
				{
					symOpNum = chainAndSymOpStr.Substring (symOpStartIndex + 1, chainAndSymOpStr.Length - symOpStartIndex - 1);
				}
				else
				{
					symOpNum = chainAndSymOpStr.Substring (symOpStartIndex + 1, symOpEndIndex - symOpStartIndex - 1);
				}
				if (symOpHash.ContainsKey (symOpNum))
				{
                    symOpHash[symOpNum].Add(chainAndSymOp);
				}
				else
				{
					List<string> chainsList = new List<string> ();
					chainsList.Add (chainAndSymOp);
					symOpHash.Add (symOpNum, chainsList);
				}
			}
			List<string> symOpList = new List<string> (symOpHash.Keys);
			symOpList.Sort ();
			int startIndex = 0;
			foreach (string symOp in symOpList)
			{
				List<string> chainsList = symOpHash[symOp];
				chainsList.Sort ();
				chainsList.CopyTo (0, chainAndSymOpStrings, startIndex, chainsList.Count);
				startIndex += chainsList.Count;
			}
			return chainAndSymOpStrings;
		}

		/// <summary>
		/// write a chain into a pdb formatted file
		/// </summary>
		/// <param name="chain">atoms</param>
		/// <param name="chainId">chain id</param>
		/// <param name="atomId">start atom sequential number for this chain</param>
		/// <param name="fileWriter">file id to write</param>
		public void WriteOneChain (AtomInfo[] chain, string chainId, ref int atomId, StreamWriter fileWriter)
		{
			string line = "";
			AtomInfo lastAtom = new AtomInfo ();
            List<string> addedAtomsList = new List<string> ();
            string seqAtomName = "";
			foreach (AtomInfo atom in chain)
			{
                seqAtomName = atom.seqId + "_" + atom.residue + "_" + atom.atomName;
                // in case duplicated although not duplicate in coordinates in original PDB file.
                // we use the atom A here
                // e.g. 4DAU: 
                //    ATOM    309  CA ACYS A  38      24.056  25.197  -5.623  0.70 11.48           C  
                //    ATOM    310  CA BCYS A  38      24.032  25.181  -5.606  0.30 13.22           C  
                // modified on March 15, 2013, especially for Pymol pair_fit which needs exactly same atoms aligned.
                if (addedAtomsList.Contains(seqAtomName))  
                {
                    continue;
                }
				atomId ++;
                if (atomId > maxAtomId)
                {
                    atomId = 1;
                }	
                line = FormatAtomLine(atom, chainId, atomId);
                fileWriter.WriteLine(line);
				lastAtom = atom;
                addedAtomsList.Add(seqAtomName);
			}
			atomId ++;
            string lastLine = FormatLastAtomLine(lastAtom, chainId, atomId);
            fileWriter.WriteLine(lastLine);
		}

        /// <summary>
        /// write a chain into a pdb formatted file
        /// </summary>
        /// <param name="chain">atoms</param>
        /// <param name="chainId">chain id</param>
        /// <param name="atomId">start atom sequential number for this chain</param>
        /// <param name="fileWriter">file id to write</param>
        public void WriteOneChainInAuthNumbering(AtomInfo[] chain, string chainId, ref int atomId, StreamWriter fileWriter)
        {
            string line = "";
            AtomInfo lastAtom = new AtomInfo();
            List<string> addedAtomsList = new List<string>();
            string seqAtomName = "";
            foreach (AtomInfo atom in chain)
            {
                seqAtomName = atom.authSeqId + "_" + atom.authResidue + "_" + atom.atomName;
                // in case duplicated although not duplicate in coordinates in original PDB file.
                // we use the atom A here
                // e.g. 4DAU: 
                //    ATOM    309  CA ACYS A  38      24.056  25.197  -5.623  0.70 11.48           C  
                //    ATOM    310  CA BCYS A  38      24.032  25.181  -5.606  0.30 13.22           C  
                // modified on March 15, 2013, especially for Pymol pair_fit which needs exactly same atoms aligned.
                if (addedAtomsList.Contains(seqAtomName))
                {
                    continue;
                }
                atomId++;
                if (atomId > maxAtomId)
                {
                    atomId = 1;
                }
                line = FormatAtomLineAuthResidueNumbering (atom, chainId, atomId);
                fileWriter.WriteLine(line);
                lastAtom = atom;
                addedAtomsList.Add(seqAtomName);
            }
            atomId++;
            string lastLine = FormatLastAtomLineInAuthResidueNumbering (lastAtom, chainId, atomId);
            fileWriter.WriteLine(lastLine);
        }

        /// <summary>
        /// write a chain into a pdb formatted file
        /// </summary>
        /// <param name="chain">atoms</param>
        /// <param name="chainId">chain id</param>
        /// <param name="atomId">start atom sequential number for this chain</param>
        /// <param name="fileWriter">file id to write</param>
        public void WriteOneChain(AtomInfo[] chain, string chainId, ref int atomId, bool isLigand, StreamWriter fileWriter)
        {
            string lineHeader = "";
            string line = "";
            AtomInfo lastAtom = new AtomInfo();
            string seqAtomName = "";
            List<string> addedAtomsList = new List<string> ();
            foreach (AtomInfo atom in chain)
            {
                seqAtomName = atom.seqId + "_" + atom.residue + "_" + atom.atomName;
                if (addedAtomsList.Contains(seqAtomName))
                {
                    continue;
                }
                atomId++;
                if (atomId > maxAtomId)
                {
                    atomId = 1;
                }
                if (isLigand)
                {
                    lineHeader = "HETATM";
                }
                else
                {
                    lineHeader = "ATOM  ";
                }
                line = FormatAtomLine(lineHeader, atom, chainId, atomId);
                fileWriter.WriteLine(line);
                lastAtom = atom;
                addedAtomsList.Add(seqAtomName);
            }
            if (!isLigand)
            {
                atomId++;
                line = FormatLastAtomLine(lastAtom, chainId, atomId);
                fileWriter.WriteLine(line);
            }
        }

        /// <summary>
        /// write a chain into a pdb formatted file
        /// </summary>
        /// <param name="chain">atoms</param>
        /// <param name="chainId">chain id</param>
        /// <param name="atomId">start atom sequential number for this chain</param>
        /// <param name="fileWriter">file id to write</param>
        public void WriteOneChainInAuthNumbering (AtomInfo[] chain, string chainId, ref int atomId, bool isLigand, StreamWriter fileWriter)
        {
            string lineHeader = "";
            string line = "";
            AtomInfo lastAtom = new AtomInfo();
            string seqAtomName = "";
            List<string> addedAtomsList = new List<string>();
            foreach (AtomInfo atom in chain)
            {
                seqAtomName = atom.authSeqId + "_" + atom.authResidue + "_" + atom.atomName;
                if (addedAtomsList.Contains(seqAtomName))
                {
                    continue;
                }
                atomId++;
                if (atomId > maxAtomId)
                {
                    atomId = 1;
                }
                if (isLigand)
                {
                    lineHeader = "HETATM";
                }
                else
                {
                    lineHeader = "ATOM  ";
                }
                line = FormatAtomLineAuthResidueNumbering (lineHeader, atom, chainId, atomId);
                fileWriter.WriteLine(line);
                lastAtom = atom;
                addedAtomsList.Add(seqAtomName);
            }
            if (!isLigand)
            {
                atomId++;
                line = FormatLastAtomLineInAuthResidueNumbering (lastAtom, chainId, atomId);
                fileWriter.WriteLine(line);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="atom"></param>
        /// <param name="atomId"></param>
        /// <returns></returns>
        public string FormatAtomLine(AtomInfo atom, string chainId, int atomId)
        {
            string atomLineHeader = "ATOM  ";
            string atomLine = FormatAtomLine(atomLineHeader, atom, chainId, atomId);
            return atomLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="atom"></param>
        /// <param name="atomId"></param>
        /// <returns></returns>
        public string FormatAtomLineAuthResidueNumbering (AtomInfo atom, string chainId, int atomId)
        {
            string atomLineHeader = "ATOM  ";
            string atomLine = FormatAtomLineAuthResidueNumbering (atomLineHeader, atom, chainId, atomId);
            return atomLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lastAtom"></param>
        /// <param name="chainId"></param>
        /// <param name="atomId"></param>
        /// <returns></returns>
        public string FormatLastAtomLine(AtomInfo lastAtom, string chainId, int atomId)
        {
            string line = "TER   ";
            line += atomId.ToString().PadLeft(5, ' ');
            line += "      ";
            line += lastAtom.residue.PadLeft(3, ' ');
            line += " ";
            line += chainId;
            line += lastAtom.seqId.PadLeft(4, ' ');
            return line;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lastAtom"></param>
        /// <param name="chainId"></param>
        /// <param name="atomId"></param>
        /// <returns></returns>
        public string FormatLastAtomLineInAuthResidueNumbering (AtomInfo lastAtom, string chainId, int atomId)
        {
            string line = "TER   ";
            line += atomId.ToString().PadLeft(5, ' ');
            line += "      ";
            line += lastAtom.authResidue.PadLeft(3, ' ');
            line += " ";
            line += chainId;
            line += lastAtom.authSeqId.PadLeft(4, ' ');
            return line;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="atom"></param>
        /// <param name="atomId"></param>
        /// <returns></returns>
        public string FormatAtomLine(string lineHeader, AtomInfo atom, string chainId, int atomId)
        {
            string pdbAtomLine = lineHeader;
            string atomIdStr = atomId.ToString();
            pdbAtomLine += atomIdStr.PadLeft(5, ' ');
            pdbAtomLine += " ";
            string atomName = atom.atomName;
            if (atomName != ""/* && atom.atomType != "H" */&& atom.atomName.Length < 4)
            {
                atomName = " " + atomName;
            }
            pdbAtomLine += atomName.PadRight(4, ' ');
            pdbAtomLine += " ";
            pdbAtomLine += atom.residue.PadLeft(3, ' ');
            pdbAtomLine += " ";
            pdbAtomLine += chainId;
            pdbAtomLine += atom.seqId.PadLeft(4, ' ');
            pdbAtomLine += "    ";
            pdbAtomLine += FormatDoubleString(atom.xyz.X, 4, 3);
            pdbAtomLine += FormatDoubleString(atom.xyz.Y, 4, 3);
            pdbAtomLine += FormatDoubleString(atom.xyz.Z, 4, 3);
            if (atom.occupancy != null)
            {
                pdbAtomLine +=  atom.occupancy.PadLeft (6, ' ');
            }
            else
            {
                pdbAtomLine += "  1.00";
            }
            if (atom.bfactor != null)
            {
                pdbAtomLine += atom.bfactor.ToString().PadLeft (6, ' ');
            }
            else
            {
                pdbAtomLine += "  0.00";
            }
            pdbAtomLine += "           ";
            pdbAtomLine += atom.atomType + "  ";
            return pdbAtomLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="atom"></param>
        /// <param name="atomId"></param>
        /// <returns></returns>
        public string FormatAtomLineAuthResidueNumbering (string lineHeader, AtomInfo atom, string chainId, int atomId)
        {
            string pdbAtomLine = lineHeader;
            string atomIdStr = atomId.ToString();
            pdbAtomLine += atomIdStr.PadLeft(5, ' ');
            pdbAtomLine += " ";
            string atomName = atom.atomName;
            if (atomName != ""/* && atom.atomType != "H" */&& atom.atomName.Length < 4)
            {
                atomName = " " + atomName;
            }
            pdbAtomLine += atomName.PadRight(4, ' ');
            pdbAtomLine += " ";
            pdbAtomLine += atom.authResidue.PadLeft(3, ' ');
            pdbAtomLine += " ";
            pdbAtomLine += chainId;
            pdbAtomLine += atom.authSeqId.PadLeft(4, ' ');
            pdbAtomLine += "    ";
            pdbAtomLine += FormatDoubleString(atom.xyz.X, 4, 3);
            pdbAtomLine += FormatDoubleString(atom.xyz.Y, 4, 3);
            pdbAtomLine += FormatDoubleString(atom.xyz.Z, 4, 3);
            if (atom.occupancy != null)
            {
                pdbAtomLine += atom.occupancy.PadLeft(6, ' ');
            }
            else
            {
                pdbAtomLine += "  1.00";
            }
            if (atom.bfactor != null)
            {
                pdbAtomLine += atom.bfactor.ToString().PadLeft(6, ' ');
            }
            else
            {
                pdbAtomLine += "  0.00";
            }
            pdbAtomLine += "           ";
            pdbAtomLine += atom.atomType + "  ";
            return pdbAtomLine;
        }
		#endregion

		#endregion

		#region write atoms to a file
		/// <summary>
		/// write list of atoms into a file
		/// </summary>
		/// <param name="pdbId">pdb entry</param>
		/// <param name="chain">a list of atoms</param>
		/// <param name="fileNum">an integer number for file sequence id</param>
		/// <param name="remark">description of the file, can be empty</param>
		public string WriteAtoms (string pdbId, string chainId, AtomInfo[] chain, int fileNum, string remark)
		{
			string fileName = pdbId;
			if (fileNum > 0)
			{
				fileName  += ("_" + fileNum.ToString ());
			}
			fileName += ".pdb";
			if (chainId == "")
			{
				chainId = "A";
			}
			FileStream fileStream = new FileStream (Path.Combine (filePath, fileName), FileMode.Create, FileAccess.Write);
			StreamWriter fileWriter = new StreamWriter (fileStream);
			string header = "HEADER    " + pdbId + "                    " + DateTime.Now;
			fileWriter.WriteLine (header);
			fileWriter.WriteLine (remark);

			try
			{
                int atomId = 0;
                WriteOneChain (chain, chainId, ref  atomId, fileWriter);
			
				fileWriter.WriteLine ("END");
			}
			catch (Exception ex)
			{
				string errorMsg = ex.Message;
				throw ex;
			}
			finally
			{
				fileWriter.Close ();
			}
			return Path.Combine (filePath, fileName);
		}

       /// <summary>
       /// write atomic coordinates to the file named fileName
       /// </summary>
       /// <param name="fileName"></param>
       /// <param name="atoms"></param>
       /// <param name="remark"></param>
       /// <returns></returns>
        public string WriteAtoms(string fileName, string pdbId, string chainId, AtomInfo[] atoms, string remark)
        {
            FileStream fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            StreamWriter fileWriter = new StreamWriter(fileStream);
            string header = "Header    " + pdbId.ToString() + "      " + DateTime.Today.ToShortDateString ();
            fileWriter.WriteLine(header);
            fileWriter.WriteLine(remark);

            try
            {
                int atomId = 0;
                WriteOneChain(atoms, chainId, ref  atomId, fileWriter);

                fileWriter.WriteLine("END");
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message;
                throw ex;
            }
            finally
            {
                fileWriter.Close();
            }
            return fileName;
        }

       /// <summary>
       /// 
       /// </summary>
       /// <param name="fileName"></param>
       /// <param name="pdbId"></param>
       /// <param name="chainAtomsHash"></param>
       /// <param name="remark"></param>
       /// <returns></returns>
        public string WriteAtoms(string fileName, string pdbId, Dictionary<string, AtomInfo[]> chainAtomsHash, string remark)
        {
            FileStream fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            StreamWriter fileWriter = new StreamWriter(fileStream);
            string header = "Header    " + pdbId.ToString() + "      " + DateTime.Today.ToShortDateString();
            fileWriter.WriteLine(header);
            fileWriter.WriteLine(remark);

            List<string> asymChainList = new List<string> (chainAtomsHash.Keys);
            asymChainList.Sort();
            int atomId = 0;
            try
            {
                foreach (string asymChain in asymChainList)
                {
                    ChainAtoms asuChain = new ChainAtoms();
                    asuChain.CartnAtoms = (AtomInfo[])chainAtomsHash[asymChain];
                    asuChain.SortByAtomId();
                    WriteOneChain(asuChain.CartnAtoms, asymChain, ref atomId, fileWriter);
                }
                fileWriter.WriteLine("END");
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message;
                throw ex;
            }
            finally
            {
                fileWriter.Close();
            }
            return fileName;
        }
		#endregion

		#region format strings
		/// <summary>
		/// format a double into a string 
		/// (8.3) (1234.123)
		/// </summary>
		/// <param name="val"></param>
		/// <returns></returns>
		private string FormatDoubleString (double val, int numPre, int numPost)
		{
			string valStr = val.ToString ();
			int dotIndex = valStr.IndexOf (".");
			if (dotIndex == -1)
			{
				// return the int part, plus ".0  "
				valStr = valStr.PadLeft (numPre, ' ');
				valStr += ".";
				int i = 0;
				while (i < numPost)
				{
					valStr += "0";
					i ++;
				}
				return valStr;
			}
			string intPartStr = valStr.Substring (0, dotIndex).PadLeft (numPre, ' ');
			int subStrLen = valStr.Length - dotIndex - 1;
			if (subStrLen > numPost)
			{
				subStrLen = numPost;
			}
			string fractStr = valStr.Substring (dotIndex + 1, subStrLen).PadRight (3, '0');
			return intPartStr + "." + fractStr;
		}

		/// <summary>
		/// format cryst1 information into PDB format string
		/// </summary>
		/// <param name="crystal1"></param>
		/// <returns></returns>
		private string FormatCrystString (CrystalInfo crystal1)
		{
			string crystString = "CRYST1";
			crystString += FormatDoubleString (crystal1.length_a, 5, 3);
			crystString += FormatDoubleString (crystal1.length_b, 5, 3);
			crystString += FormatDoubleString (crystal1.length_c, 5, 3);
			crystString += FormatDoubleString (crystal1.angle_alpha, 4, 2);
			crystString += FormatDoubleString (crystal1.angle_beta, 4, 2);
			crystString += FormatDoubleString (crystal1.angle_gamma, 4, 2);
			crystString += " ";
			crystString += crystal1.spaceGroup.PadRight (11, ' ');
			crystString += crystal1.zpdb.ToString ().PadLeft (4, ' ');
			return crystString;
		}

		/// <summary>
		/// format fract matrix into pdb format
		/// </summary>
		/// <param name="fractMatrix"></param>
		/// <returns></returns>
		private string FormatFractString (Matrix fractMatrix)
		{
			string fractString = "";
			foreach (MatrixElement row in fractMatrix.matrix)
			{
				fractString += ("SCALE" + (row.dimId + 1).ToString ());
				fractString += "    ";
				fractString += FormatDoubleString (row.a, 3, 6);
				fractString += FormatDoubleString (row.b, 3, 6);
				fractString += FormatDoubleString (row.c, 3, 6);
				fractString += "     ";
				fractString += FormatDoubleString (row.vector, 4, 5);
				fractString += "\r\n";
			}
			
			return fractString.TrimEnd ("\r\n".ToCharArray ());
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="assemblyChainSymStrings"></param>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        private string GetChainComponentRemarkString(string[] assemblyChainSymStrings, DataTable asuTable, string buType)
        {
            string asuInfoRemark = "";
            string asymChain = "";
            string symString = "";
            int chainIndex = -1;
            asuInfoRemark = "REMARK   1 " + buType.ToUpper() + " BU\r\n";
            int count = 0;
            foreach (string chainSymString in assemblyChainSymStrings)
            {
                asuInfoRemark += "REMARK   2 ";
                chainIndex = chainSymString.IndexOf("_");
                if (chainIndex > -1)
                {
                    asymChain = chainSymString.Substring(0, chainIndex);
                    symString = chainSymString.Substring(chainIndex + 1, chainSymString.Length - chainIndex - 1);
                }
                else
                {
                    asymChain = chainSymString;
                    symString = "";
                }
                string chainId = letters[count % letters.Length].ToString();
                count++;
                asuInfoRemark += ("BU Chain: " + chainId + ", ");
                asuInfoRemark += ("Asymmetric Chain: " + asymChain + ", ");
                DataRow[] chainRows = asuTable.Select(string.Format("AsymID = '{0}'", asymChain));
                if (chainRows.Length > 0)
                {
                    asuInfoRemark += ("Author Chain: " + chainRows[0]["AuthorChain"].ToString().TrimEnd() + ", ");
                    asuInfoRemark += ("Entity ID: " + chainRows[0]["EntityID"].ToString());
                }
                if (symString != "")
                {
                    asuInfoRemark += (", Symmetry String: " + symString);
                }
                if (chainRows.Length > 0)
                {
                    asuInfoRemark += (", PolymerStatus: " + chainRows[0]["PolymerStatus"].ToString ().TrimEnd ());
                    asuInfoRemark += (", Name: " + chainRows[0]["Name"].ToString().TrimEnd());
                }
                asuInfoRemark += "\r\n";
            }
            return asuInfoRemark;
        }
		#endregion
	}
}
