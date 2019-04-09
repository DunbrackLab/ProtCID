using System;
using System.IO;
using System.Data;
using System.Collections.Generic;
using System.Xml.Serialization;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;

namespace CrystalInterfaceLib.Crystal
{
	/// <summary>
	/// Summary description for PisaBuGenerator.
	/// </summary>
	public class PisaBuGenerator : BuGenerator
	{
		private DbQuery dbQuery = new DbQuery ();

		public PisaBuGenerator()
		{

		}

		#region build pisa assemblies from xml file and the matrices
		/// <summary>
		/// 
		/// </summary>
		/// <param name="pdbId"></param>
		/// <returns></returns>
        public Dictionary<string, Dictionary<string, AtomInfo[]>> BuildPisaAssemblies(string pdbId)
		{
			Dictionary<string, Dictionary<string, List<SymOpMatrix>>> assemblyComponentHash = null;
			try
			{
				assemblyComponentHash = GetPisaBuMatrices (pdbId);
			}
			catch 
			{
				return null;
			}

			string gzXmlFile = Path.Combine (ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
			string xmlFile = ParseHelper.UnZipFile (gzXmlFile, ProtCidSettings.tempDir);
            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(xmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

			Dictionary<string, Dictionary<string, AtomInfo[]>> pisaMultimersHash = new Dictionary<string,Dictionary<string,AtomInfo[]>> ();
			foreach (string assemblyId  in assemblyComponentHash.Keys)
			{
                Dictionary<string, AtomInfo[]> pisaMultimerHash = BuildOneBuAssembly(thisEntryCrystal, assemblyComponentHash[assemblyId], "pisa");
                pisaMultimersHash.Add(assemblyId, pisaMultimerHash);
			}
			File.Delete (xmlFile);
			return pisaMultimersHash;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, AtomInfo[]>> BuildPisaAssemblies(string pdbId, bool needLigands)
        {
            Dictionary<string, Dictionary<string, List<SymOpMatrix>>> assemblyComponentHash = null;
            try
            {
                assemblyComponentHash = GetPisaBuMatrices(pdbId, needLigands);
            }
            catch (Exception ex)
            {
                throw new Exception("Retrieve PISA assembly component errors: " + ex.Message);
            }

            string gzXmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string xmlFile = ParseHelper.UnZipFile(gzXmlFile, ProtCidSettings.tempDir);
            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(xmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            Dictionary<string, Dictionary<string, AtomInfo[]>> pisaMultimersHash = new Dictionary<string, Dictionary<string, AtomInfo[]>>();
            if (assemblyComponentHash.Count == 0)
            {
                pisaMultimersHash = GetPisaMonomersFromAsu(thisEntryCrystal);
            }
            else
            {
                foreach (string assemblySeqId in assemblyComponentHash.Keys)
                {
                    Dictionary<string, AtomInfo[]> pisaMultimerHash =
                        BuildOneBuAssembly(thisEntryCrystal, assemblyComponentHash[assemblySeqId], "pisa");
                    pisaMultimersHash.Add(assemblySeqId, pisaMultimerHash);
                }
            }
            File.Delete(xmlFile);

            return pisaMultimersHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="thisEntryCrystal"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<string, AtomInfo[]>>  GetPisaMonomersFromAsu(EntryCrystal thisEntryCrystal)
        {
            int assemblySeqId = 1;
            string assemblySeqIdString = "";
            Dictionary<string, Dictionary<string, AtomInfo[]>> pisaMultimersHash = new Dictionary<string, Dictionary<string, AtomInfo[]>> ();
            foreach (ChainAtoms chain in thisEntryCrystal.atomCat.ChainAtomList)
            {
         /*       if (chain.polymerType != "polydeoxyribonucleotide" || chain.polymerType == "polyribonucleotide")
                {
                    continue;
                }*/
                if (chain.polymerType == "polypeptide")
                {
                    assemblySeqIdString = assemblySeqId.ToString();
                    if (pisaMultimersHash.ContainsKey(assemblySeqIdString))
                    {
                        pisaMultimersHash[assemblySeqIdString].Add(chain.asymChain + "_1_555", chain.CartnAtoms);
                    }
                    else
                    {
                        Dictionary<string, AtomInfo[]> chainAtomsHash = new Dictionary<string,AtomInfo[]> ();
                        chainAtomsHash.Add(chain.asymChain + "_1_555", chain.CartnAtoms);
                        pisaMultimersHash.Add(assemblySeqIdString, chainAtomsHash);
                        assemblySeqId++;
                    }
                }
                else // add ligands into the first monomer
                {
                    if (pisaMultimersHash.ContainsKey("1"))
                    {
                        pisaMultimersHash["1"].Add(chain.asymChain + "_1_555", chain.CartnAtoms);
                    }
                    else
                    {
                        Dictionary<string, AtomInfo[]> chainAtomsHash = new Dictionary<string,AtomInfo[]> ();
                        chainAtomsHash.Add(chain.asymChain + "_1_555", chain.CartnAtoms);
                        pisaMultimersHash.Add("1", chainAtomsHash);
                    }
                }
            }
            return pisaMultimersHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="assemblyIds"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, AtomInfo[]>> BuildPisaAssemblies(string pdbId, string[] assemblyIds)
        {
            string gzXmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string xmlFile = ParseHelper.UnZipFile(gzXmlFile, ProtCidSettings.tempDir);
            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(xmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            Dictionary<string, Dictionary<string, AtomInfo[]>> pisaMultimersHash = new Dictionary<string,Dictionary<string,AtomInfo[]>>  ();
            foreach (string assemblyId in assemblyIds)
            {
                Dictionary<string, AtomInfo[]> pisaBuHash = BuildOnePisaAssembly(thisEntryCrystal, pdbId, assemblyId);
                pisaMultimersHash.Add(assemblyId, pisaBuHash);
            }
            return pisaMultimersHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="assemblySeqId"></param>
        /// <returns></returns>
        public Dictionary<string, AtomInfo[]> BuildOnePisaAssembly(string pdbId, string assemblySeqId)
		{
			string gzXmlFile = Path.Combine (ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
			string xmlFile = ParseHelper.UnZipFile (gzXmlFile, ProtCidSettings.tempDir);
            Dictionary<string, AtomInfo[]> buFileHash = BuildOnePisaAssembly(xmlFile, pdbId, assemblySeqId);
			return buFileHash;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlFile"></param>
		/// <param name="chainMatrixHash"></param>
		/// <returns></returns>
        public Dictionary<string, AtomInfo[]> BuildOnePisaAssembly(string xmlFile, string pdbId, string assemblySeqId)
		{
			Dictionary<string, List<SymOpMatrix>> chainMatrixHash = GetPisaBuMatrices (pdbId, assemblySeqId);

			// read data from crystal xml file
			EntryCrystal thisEntryCrystal;
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(EntryCrystal));
			FileStream xmlFileStream = new FileStream(xmlFile, FileMode.Open);
			thisEntryCrystal = (EntryCrystal) xmlSerializer.Deserialize (xmlFileStream);
			xmlFileStream.Close ();

            Dictionary<string, AtomInfo[]> assemblyHash = BuildOneBuAssembly(thisEntryCrystal, chainMatrixHash, "pisa");
			
			return assemblyHash;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlFile"></param>
        /// <param name="chainMatrixHash"></param>
        /// <returns></returns>
        private Dictionary<string, AtomInfo[]> BuildOnePisaAssembly(EntryCrystal thisEntryCrystal, string pdbId, string assemblySeqId)
        {
            Dictionary<string, List<SymOpMatrix>> chainMatrixHash = GetPisaBuMatrices(pdbId, assemblySeqId);

            Dictionary<string, AtomInfo[]> assemblyHash = BuildOneBuAssembly(thisEntryCrystal, chainMatrixHash, "pisa");

            return assemblyHash;
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlFile"></param>
		/// <param name="chainMatrixHash"></param>
		/// <returns></returns>
        public Dictionary<string, AtomInfo[]> BuildOnePisaAssembly(string xmlFile, string pdbId, string assemblySeqId, DataTable buMatrixTable)
		{
			Dictionary<string, List<SymOpMatrix>> chainMatrixHash = GetPisaBuMatrices (pdbId, assemblySeqId, buMatrixTable);

			// read data from crystal xml file
			EntryCrystal thisEntryCrystal;
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(EntryCrystal));
			FileStream xmlFileStream = new FileStream(xmlFile, FileMode.Open);
			thisEntryCrystal = (EntryCrystal) xmlSerializer.Deserialize (xmlFileStream);
			xmlFileStream.Close ();

            Dictionary<string, AtomInfo[]> assemblyHash = BuildOneBuAssembly(thisEntryCrystal, chainMatrixHash, "pisa");
			
			return assemblyHash;
		}
		#endregion

		#region matrices to generate pisa assembly
        /// <summary>
        /// Matrices to generate PISA Assemblies
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, List<SymOpMatrix>>> GetPisaBuMatrices(string pdbId, bool needLigands)
        {
            Dictionary<string, Dictionary<string, List<SymOpMatrix>>> assemblyComponentHash = GetPisaBuMatrices(pdbId);
            if (needLigands)
            {
                GetPisaBuLigandsMatrices(pdbId, ref assemblyComponentHash); 
            }
            return assemblyComponentHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="assemblyComponentHash"></param>
        public void GetPisaBuLigandsMatrices(string pdbId, ref Dictionary<string, Dictionary<string, List<SymOpMatrix>>> assemblyComponentHash)
        {
            string queryString = string.Format("Select * From PisaBuLigand WHere PdbID = '{0}';", pdbId);
            DataTable ligandTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string assemblySeqId = "";
            string asymChain = "";
            string matrixString = "";
            SymOpMatrix symOpMatrix = null;
            foreach (DataRow dRow in ligandTable.Rows)
            {
                assemblySeqId = dRow["AssemblySeqID"].ToString();
                asymChain = dRow["AsymChain"].ToString().Trim();
                matrixString = dRow["Matrix"].ToString().Trim();
                symOpMatrix = GetCoordSymOpMatrix(matrixString);
                symOpMatrix.symmetryString = dRow["SymmetryString"].ToString().TrimEnd();

                if (assemblyComponentHash.ContainsKey(assemblySeqId))
                {
                    Dictionary<string, List<SymOpMatrix>> chainMatrixHash = assemblyComponentHash[assemblySeqId];
                    if (chainMatrixHash.ContainsKey(asymChain))
                    {
                        if (!IsSymMatrixExist(chainMatrixHash[asymChain], symOpMatrix))
                        {
                            chainMatrixHash[asymChain].Add(symOpMatrix);
                        }
                    }
                    else
                    {
                        List<SymOpMatrix> matrixList = new List<SymOpMatrix> ();
                        matrixList.Add(symOpMatrix);
                        chainMatrixHash.Add(asymChain, matrixList);
                    }
                }
                else
                {
                    Dictionary<string, List<SymOpMatrix>> chainMatrixHash = new Dictionary<string,List<SymOpMatrix>> ();
                    List<SymOpMatrix> matrixList = new List<SymOpMatrix> ();
                    matrixList.Add(symOpMatrix);
                    chainMatrixHash.Add(asymChain, matrixList);
                    assemblyComponentHash.Add(assemblySeqId, chainMatrixHash);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="matrixList"></param>
        /// <param name="symMatrix"></param>
        /// <returns></returns>
        private bool IsSymMatrixExist(List<SymOpMatrix> matrixList, SymOpMatrix symMatrix)
        {
            foreach (SymOpMatrix symOpMatrix in matrixList)
            {
                if (symOpMatrix.symmetryString == symMatrix.symmetryString)
                {
                    return true;
                }
            }
            return false;
        }
		/// <summary>
		/// Matrices to generate PISA Assemblies
		/// </summary>
		/// <param name="pdbId"></param>
		/// <returns></returns>
		public  Dictionary<string, Dictionary<string, List<SymOpMatrix>>> GetPisaBuMatrices (string pdbId)
		{
			string queryString = string.Format ("Select * From PisaBuMatrix Where PdbID = '{0}';", pdbId);
            DataTable pisaBusTable = ProtCidSettings.pdbfamQuery.Query(queryString);
			Dictionary<string, Dictionary<string, List<SymOpMatrix>>> assemblyComponentHash = new Dictionary<string,Dictionary<string,List<SymOpMatrix>>> ();
			string assemblySeqId = "";
			string asymChain = "";
			string matrixString = "";
			SymOpMatrix symOpMatrix = null;
			foreach (DataRow dRow in pisaBusTable.Rows)
			{
				assemblySeqId = dRow["AssemblySeqID"].ToString ();
				asymChain = dRow["AsymChain"].ToString ().Trim ();
				matrixString = dRow["Matrix"].ToString ().Trim ();
				symOpMatrix = GetCoordSymOpMatrix (matrixString);
                symOpMatrix.symmetryString = dRow["SymmetryString"].ToString().TrimEnd();
                symOpMatrix.symmetryOpNum = dRow["SymOpSeqID"].ToString().TrimEnd ();

				if (assemblyComponentHash.ContainsKey (assemblySeqId))
				{
                    if (assemblyComponentHash[assemblySeqId].ContainsKey(asymChain))
					{
                        assemblyComponentHash[assemblySeqId][asymChain].Add(symOpMatrix);
					}
					else
					{
						List<SymOpMatrix> matrixList = new List<SymOpMatrix> ();
						matrixList.Add (symOpMatrix);
                        assemblyComponentHash[assemblySeqId].Add(asymChain, matrixList);
					}
				}
				else
				{
					Dictionary<string, List<SymOpMatrix>> chainMatrixHash = new Dictionary<string, List<SymOpMatrix>> ();
					List<SymOpMatrix> matrixList = new List<SymOpMatrix> ();
					matrixList.Add (symOpMatrix);
					chainMatrixHash.Add (asymChain, matrixList);
					assemblyComponentHash.Add (assemblySeqId, chainMatrixHash);
				}
			}
			return assemblyComponentHash;
		}

		/// <summary>
		/// Matrices to generate PISA Assembly given an assemblySeqID
		/// </summary>
		/// <param name="pdbId"></param>
		/// <returns></returns>
        public Dictionary<string, List<SymOpMatrix>> GetPisaBuMatrices(string pdbId, string assemblySeqId)
		{
			string queryString = string.Format ("Select * From PisaBuMatrix " + 
				" Where PdbID = '{0}' AND AssemblySeqID = {1};", pdbId, assemblySeqId);
            DataTable pisaBusTable = ProtCidSettings.pdbfamQuery.Query(queryString);
			Dictionary<string, List<SymOpMatrix>> assemblyMatricesHash = new Dictionary<string,List<SymOpMatrix>>  ();
			
			string asymChain = "";
			string matrixString = "";
			SymOpMatrix symOpMatrix = null;
			foreach (DataRow dRow in pisaBusTable.Rows)
			{
				asymChain = dRow["AsymChain"].ToString ().Trim ();
				matrixString = dRow["Matrix"].ToString ().Trim ();
				symOpMatrix = GetCoordSymOpMatrix (matrixString);
                symOpMatrix.symmetryString = dRow["SymmetryString"].ToString().TrimEnd();
                symOpMatrix.symmetryOpNum = dRow["SymOpSeqId"].ToString().TrimEnd ();
				
				if (assemblyMatricesHash.ContainsKey (asymChain))
				{
                    assemblyMatricesHash[asymChain].Add(symOpMatrix);
				}
				else
				{
					List<SymOpMatrix> matrixList = new List<SymOpMatrix> ();
					matrixList.Add (symOpMatrix);
					assemblyMatricesHash.Add (asymChain, matrixList);
				}
			}
			return assemblyMatricesHash;
		}

		/// <summary>
		/// Matrices to generate PISA Assembly given an assemblySeqID
		/// </summary>
		/// <param name="pdbId"></param>
		/// <returns></returns>
		public  Dictionary<string, List<SymOpMatrix>> GetPisaBuMatrices (string pdbId, string assemblySeqId, DataTable buMatrixTable)
		{
			DataRow[] buMatrixRows = buMatrixTable.Select (string.Format ("PdbID = '{0}' AND AssemblySeqID = '{1}'", 
				pdbId, assemblySeqId));
            Dictionary<string, List<SymOpMatrix>> assemblyMatricesHash = new Dictionary<string, List<SymOpMatrix>>();
			
			string asymChain = "";
			string matrixString = "";
			SymOpMatrix symOpMatrix = null;
			foreach (DataRow dRow in buMatrixRows)
			{
				asymChain = dRow["AsymChain"].ToString ().Trim ();
				matrixString = dRow["Matrix"].ToString ().Trim ();
				symOpMatrix = GetCoordSymOpMatrix (matrixString);
                symOpMatrix.symmetryString = dRow["SymmetryString"].ToString().TrimEnd();
                symOpMatrix.symmetryOpNum = dRow["SymOpSeqID"].ToString().TrimEnd ();
				
				if (assemblyMatricesHash.ContainsKey (asymChain))
				{
                    assemblyMatricesHash[asymChain].Add(symOpMatrix);
				}
				else
				{
					List<SymOpMatrix> matrixList = new List<SymOpMatrix>  ();
					matrixList.Add (symOpMatrix);
					assemblyMatricesHash.Add (asymChain, matrixList);
				}
			}
			return assemblyMatricesHash;
		}
		#endregion
	}
}
