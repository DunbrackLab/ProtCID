using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Settings;
using CrystalInterfaceLib.KDops;

namespace CrystalInterfaceLib.Crystal
{
	/// <summary>
	/// Summary description for CrystalBuilder.
	/// </summary>
	public class CrystalBuilder
	{
		// used for store the symmetry chains and their transformed atom coordinates
		// key: asymChain_symmetry operation. e.g A_1_555
		// value: a list of atom coordinates
		//private Hashtable crystalChainsHash = new Hashtable ();
		private CrystalInfo crystal1 = new CrystalInfo ();
		protected string atomType = "CA";
		protected const string origSymOpString = "1_555";
        protected internal Matrix cartn2fractMatrix = null;
		protected internal Matrix fract2cartnMatrix = null;
//		private const int minNumResidueInChain = 15;


		#region public interfaces
		#region constructors
		public CrystalBuilder()
		{
			atomType = "ALL";
		}

		public CrystalBuilder(string atomtype)
		{
			atomType = atomtype;
		}
		#endregion

		#region properties
		/// <summary>
		/// read only property for the cystal1 record
		/// </summary>
		public CrystalInfo Crystal1 
		{
			get
			{
				return crystal1;
			}
		}
		#endregion
		/// <summary>
		/// build base unit cell of a crystal from atom coordinates from XML file, 
		/// symmetry operators for the space group
		/// </summary>
		/// <param name="crystalXmlFile">our own crystal XML file</param>
        public Dictionary<string, AtomInfo[]> BuildCrystal(string crystalXmlFile)
		{
			// read data from crystal xml file
			EntryCrystal thisEntryCrystal;
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(EntryCrystal));
			FileStream xmlFileStream = new FileStream(crystalXmlFile, FileMode.Open);
			thisEntryCrystal = (EntryCrystal) xmlSerializer.Deserialize (xmlFileStream);
			crystal1 = thisEntryCrystal.crystal1;
			xmlFileStream.Close ();

			if (AppSettings.symOps == null)
			{
				AppSettings.LoadSymOps ();
			}
			if (crystal1.spaceGroup == "-")
			{
				crystal1.spaceGroup = "P 1";
			}
			SymOpMatrix[] symOpMatrices = AppSettings.symOps.FindSpaceGroup(crystal1.spaceGroup);
			if (symOpMatrices == null)
			{
				throw new Exception ("No symmetry operators provided for this entry " + crystalXmlFile);
			}
			cartn2fractMatrix = thisEntryCrystal.scaleCat.ScaleMatrix;
			fract2cartnMatrix = cartn2fractMatrix.Inverse ();

			//ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
			ChainAtoms[] chainAtomsList = new ChainAtoms [thisEntryCrystal.atomCat.ChainAtomList.Length];
			switch (atomType.ToUpper ())
			{
				case "CA":
					chainAtomsList = thisEntryCrystal.atomCat.CalphaAtomList();
					// clear the whole atom list,free memory
					thisEntryCrystal.atomCat.ChainAtomList = null;
					break;

				case "CB":
					chainAtomsList = thisEntryCrystal.atomCat.CbetaAtomList();
					// clear the whole atom list, free memory
					thisEntryCrystal.atomCat.ChainAtomList = null;
					break;

				case "CA_CB":
					chainAtomsList = thisEntryCrystal.atomCat.CalphaCbetaAtomList();
					// clear the whole atom list, free memory
					thisEntryCrystal.atomCat.ChainAtomList = null;
					break;

				case "ALL":
					chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
					break;

				default:
					break;
			}
            Dictionary<string, AtomInfo[]> crystalChainsHash = BuildCrystalBySymOps(chainAtomsList, symOpMatrices);
				
			return crystalChainsHash;
		}
		/// <summary>
		/// build base unit cell of a crystal from atom coordinates from XML file, 
		/// using symmetry operations provided as parameters
		/// </summary>
		/// <param name="crystalXmlFile">our own crystal XML file</param>
		/// <param name="symOpStrings">specified symmetry operators</param>
        public Dictionary<string, AtomInfo[]> BuildCrystal(string crystalXmlFile, string[] symOpStrings)
		{
			SymOperator symOp = new SymOperator ();
			// read data from crystal xml file
			EntryCrystal thisEntryCrystal;
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(EntryCrystal));
			FileStream xmlFileStream = new FileStream(crystalXmlFile, FileMode.Open);
			thisEntryCrystal = (EntryCrystal) xmlSerializer.Deserialize (xmlFileStream);
			crystal1 = thisEntryCrystal.crystal1;
			xmlFileStream.Close ();

            Dictionary<string, AtomInfo[]> crystalChainsHash = new Dictionary<string, AtomInfo[]>();

			if (AppSettings.symOps == null)
			{
				AppSettings.LoadSymOps ();
			}
			if (crystal1.spaceGroup == "-" || crystal1.spaceGroup == "")
			{
				crystal1.spaceGroup = "P 1";
			}
			SymOpMatrix[] sgSymOpMatrices = AppSettings.symOps.FindSpaceGroup(crystal1.spaceGroup);
			if (sgSymOpMatrices == null)
			{
				throw new Exception ("No symmetry operators provided for this entry " + crystalXmlFile);
			}
			cartn2fractMatrix = thisEntryCrystal.scaleCat.ScaleMatrix;
			fract2cartnMatrix = cartn2fractMatrix.Inverse ();

		//	ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
			ChainAtoms[] chainAtomsList = new ChainAtoms [thisEntryCrystal.atomCat.ChainAtomList.Length];
			switch (atomType)
			{
				case "CA":
					chainAtomsList = thisEntryCrystal.atomCat.CalphaAtomList();
					// clear the whole atom list,free memory
					thisEntryCrystal.atomCat.ChainAtomList = null;
					break;

				case "CB":
					chainAtomsList = thisEntryCrystal.atomCat.CbetaAtomList();
					// clear the whole atom list, free memory
					thisEntryCrystal.atomCat.ChainAtomList = null;
					break;

				case "CA_CB":
					chainAtomsList = thisEntryCrystal.atomCat.CalphaCbetaAtomList();
					// clear the whole atom list, free memory
					thisEntryCrystal.atomCat.ChainAtomList = null;
					break;

				case "ALL":
					chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
					break;

				default:
					break;
			}
            // just in case, something is messed up. It should not happen often.
            // may hide some bugs
            // add on Feb. 20, 2009
            bool chainFound = false;  

			for (int i = 0; i < symOpStrings.Length; i ++)
			{
				string chainId = symOpStrings[i].Substring (0, symOpStrings[i].IndexOf ("_"));
				string symOpString = symOpStrings[i].Substring (symOpStrings[i].IndexOf ("_") + 1, 
					symOpStrings[i].Length - symOpStrings[i].IndexOf ("_") - 1);
				int asymCount = 0;
                chainFound = false;
				for (asymCount = 0; asymCount < chainAtomsList.Length; asymCount ++)
				{
					string asymChain = chainAtomsList[asymCount].AsymChain;
					if (asymChain == chainId)
					{
                        chainFound = true;
						break;
					}
				}
                if (!chainFound)
                {
                    continue;
                }
				if (symOpString == origSymOpString)
				{
					crystalChainsHash.Add (symOpStrings[i], chainAtomsList[asymCount].CartnAtoms);
					continue;
				}
				try
				{
					SymOpMatrix symOpMatrix = symOp.GetSymmetryMatrixFromSymmetryString (sgSymOpMatrices, symOpString);
					AtomInfo[] transformedAtoms = TransformChainBySpecificSymOp(chainAtomsList[asymCount].CartnAtoms, symOpMatrix);
					crystalChainsHash.Add (symOpStrings[i], transformedAtoms);
				}
				catch 
				{
					continue;
				}
			}
			return crystalChainsHash;
		}

		/// <summary>
		/// build a structure based on the coordinates and symmetry operator strings (1_555)
		/// </summary>
		/// <param name="thisEntryCrystal"></param>
		/// <param name="symOpStrings"></param>
		/// <returns></returns>
        public Dictionary<string, AtomInfo[]> BuildCrystal(EntryCrystal thisEntryCrystal, string[] symOpStrings)
		{
			SymOperator symOp = new SymOperator ();
			crystal1 = thisEntryCrystal.crystal1;
			if (AppSettings.symOps == null)
			{
				AppSettings.LoadSymOps ();
			}
			if (crystal1.spaceGroup == "-")
			{
				crystal1.spaceGroup = "P 1";
			}
			SymOpMatrix[] sgSymOpMatrices = AppSettings.symOps.FindSpaceGroup(crystal1.spaceGroup);

            Dictionary<string, AtomInfo[]> crystalChainsHash = new Dictionary<string, AtomInfo[]>();

			cartn2fractMatrix = thisEntryCrystal.scaleCat.ScaleMatrix;
			fract2cartnMatrix = cartn2fractMatrix.Inverse ();

			//ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
			ChainAtoms[] chainAtomsList = new ChainAtoms [thisEntryCrystal.atomCat.ChainAtomList.Length];
			switch (atomType)
			{
				case "CA":
					chainAtomsList = thisEntryCrystal.atomCat.CalphaAtomList();
					// clear the whole atom list,free memory
					thisEntryCrystal.atomCat.ChainAtomList = null;
					break;

				case "CB":
					chainAtomsList = thisEntryCrystal.atomCat.CbetaAtomList();
					// clear the whole atom list, free memory
					thisEntryCrystal.atomCat.ChainAtomList = null;
					break;

				case "CA_CB":
					chainAtomsList = thisEntryCrystal.atomCat.CalphaCbetaAtomList();
					// clear the whole atom list, free memory
					thisEntryCrystal.atomCat.ChainAtomList = null;
					break;

				case "ALL":
					chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
					break;

				default:
					break;
			}
			for (int i = 0; i < symOpStrings.Length; i ++)
			{
				if (crystalChainsHash.ContainsKey (symOpStrings[i]))
				{
					continue;
				}
				string chainId = symOpStrings[i].Substring (0, symOpStrings[i].IndexOf ("_"));
				string symOpString = symOpStrings[i].Substring (symOpStrings[i].IndexOf ("_") + 1, 
					symOpStrings[i].Length - symOpStrings[i].IndexOf ("_") - 1);
				int asymCount = 0;
				for (asymCount = 0; asymCount < chainAtomsList.Length; asymCount ++)
				{
					string asymChain = chainAtomsList[asymCount].AsymChain;
					if (asymChain == chainId)
					{
						break;
					}
				}
				if (symOpString == origSymOpString)
				{
					crystalChainsHash.Add (symOpStrings[i], chainAtomsList[asymCount].CartnAtoms);
					continue;
				}
				SymOpMatrix symOpMatrix = symOp.GetSymmetryMatrixFromSymmetryString (sgSymOpMatrices, symOpString);
				AtomInfo[] transformedAtoms = TransformChainBySpecificSymOp(chainAtomsList[asymCount].CartnAtoms, symOpMatrix);
				crystalChainsHash.Add (symOpStrings[i], transformedAtoms);
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
        public Dictionary<string, AtomInfo[]> BuildCrystal(EntryCrystal thisEntryCrystal, string[] symOpStrings, string[] fullSymOpStrings)
		{
			SymOperator symOp = new SymOperator ();
			crystal1 = thisEntryCrystal.crystal1;

            Dictionary<string, AtomInfo[]> crystalChainsHash = new Dictionary<string, AtomInfo[]>();

			cartn2fractMatrix = thisEntryCrystal.scaleCat.ScaleMatrix;
			fract2cartnMatrix = cartn2fractMatrix.Inverse ();

			//ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
			ChainAtoms[] chainAtomsList = new ChainAtoms [thisEntryCrystal.atomCat.ChainAtomList.Length];
			switch (atomType)
			{
				case "CA":
					chainAtomsList = thisEntryCrystal.atomCat.CalphaAtomList();
					// clear the whole atom list,free memory
					thisEntryCrystal.atomCat.ChainAtomList = null;
					break;

				case "CB":
					chainAtomsList = thisEntryCrystal.atomCat.CbetaAtomList();
					// clear the whole atom list, free memory
					thisEntryCrystal.atomCat.ChainAtomList = null;
					break;

				case "CA_CB":
					chainAtomsList = thisEntryCrystal.atomCat.CalphaCbetaAtomList();
					// clear the whole atom list, free memory
					thisEntryCrystal.atomCat.ChainAtomList = null;
					break;

				case "ALL":
					chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
					break;

				default:
					break;
			}
			for (int i = 0; i < symOpStrings.Length; i ++)
			{
				if (crystalChainsHash.ContainsKey (symOpStrings[i]))
				{
					continue;
				}
				string chainId = symOpStrings[i].Substring (0, symOpStrings[i].IndexOf ("_"));
				string symOpString = symOpStrings[i].Substring (symOpStrings[i].IndexOf ("_") + 1, 
					symOpStrings[i].Length - symOpStrings[i].IndexOf ("_") - 1);
				int asymCount = 0;
				for (asymCount = 0; asymCount < chainAtomsList.Length; asymCount ++)
				{
					string asymChain = chainAtomsList[asymCount].AsymChain;
					if (asymChain == chainId)
					{
						break;
					}
				}
				if (symOpString == origSymOpString)
				{
					crystalChainsHash.Add (symOpStrings[i], chainAtomsList[asymCount].CartnAtoms);
					continue;
				}
				SymOpMatrix symOpMatrix = symOp.GetSymMatrix(fullSymOpStrings[i], symOpStrings[i]);
				AtomInfo[] transformedAtoms = TransformChainBySpecificSymOp(chainAtomsList[asymCount].CartnAtoms, symOpMatrix);
				crystalChainsHash.Add (symOpStrings[i], transformedAtoms);
			}
			return crystalChainsHash;
		}

        /// <summary>
        /// calculate and transfer all chains based on the input symmetry strings
        /// </summary>
        /// <param name="thisEntryCrystal"></param>
        /// <param name="symmetryStrings">digits: 2_565</param>
        /// <returns></returns>
        public Dictionary<string, AtomInfo[]> BuildCrystalWithAllChains(EntryCrystal thisEntryCrystal, string[] symmetryStrings)
        {
            SymOperator symOp = new SymOperator();
            crystal1 = thisEntryCrystal.crystal1;
            if (AppSettings.symOps == null)
            {
                AppSettings.LoadSymOps();
            }
            if (crystal1.spaceGroup == "-")
            {
                crystal1.spaceGroup = "P 1";
            }
            SymOpMatrix[] sgSymOpMatrices = AppSettings.symOps.FindSpaceGroup(crystal1.spaceGroup);

            Dictionary<string, AtomInfo[]> crystalChainsHash = new Dictionary<string, AtomInfo[]>();

            cartn2fractMatrix = thisEntryCrystal.scaleCat.ScaleMatrix;
            fract2cartnMatrix = cartn2fractMatrix.Inverse();

            ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
            string symOpString = "";
            for (int i = 0; i < symmetryStrings.Length; i++)
            {
                for (int asymCount = 0; asymCount < chainAtomsList.Length; asymCount++)
                {
                    string asymChain = chainAtomsList[asymCount].AsymChain;
                    symOpString = asymChain + "_" + symmetryStrings[i];

                    if (crystalChainsHash.ContainsKey(symOpString))
                    {
                        continue;
                    }

                    if (symmetryStrings[i] == origSymOpString)
                    {
                        crystalChainsHash.Add(symOpString, chainAtomsList[asymCount].CartnAtoms);
                        continue;
                    }

                    SymOpMatrix symOpMatrix = symOp.GetSymmetryMatrixFromSymmetryString(sgSymOpMatrices, symmetryStrings[i]);
                    AtomInfo[] transformedAtoms = TransformChainBySpecificSymOp(chainAtomsList[asymCount].CartnAtoms, symOpMatrix);
                    crystalChainsHash.Add(symOpString, transformedAtoms);
                }
            }
            return crystalChainsHash;
        }
        #endregion

		#region unit cell builder
		/// <summary>
		/// build crystal based on symmetry operations 
		/// without translation
		/// </summary>
		/// <param name="coordAtomList"></param>
		/// <param name="buGenStructList"></param>
		/// <param name="buSymmetryMatrixList"></param>
        private Dictionary<string, AtomInfo[]> BuildCrystalBySymOps(ChainAtoms[] chainAtomsList, SymOpMatrix[] buSymmetryMatrixList)
		{
			Dictionary<string, AtomInfo[]> crystalChainsHash = new Dictionary<string,AtomInfo[]> ();
			string asymChain = "";
			for (int asymCount = 0; asymCount < chainAtomsList.Length; asymCount ++)
			{
				if (chainAtomsList[asymCount].GetNumOfResidues () < AppSettings.parameters.contactParams.minNumResidueInChain)
				{
					continue;
				}
				asymChain = chainAtomsList[asymCount].AsymChain;
				
				// at this step, only consider polypeptide chains
				if (chainAtomsList[asymCount].PolymerType != "polypeptide")
				{
					continue;
				}

				// the symmetry matrix without translation, e.g. 1_555
				// all translated chains should be derived from this chains
				foreach (SymOpMatrix symMatrix in buSymmetryMatrixList)
				{
					// just in case errors in crystal xml files
					// duplicate symmtery operators information
					if (crystalChainsHash.ContainsKey (asymChain + "_" + symMatrix.symmetryString))
					{
						continue;
					}
					if (symMatrix.symmetryString == origSymOpString)
					{
						SetFractCoordinates (chainAtomsList[asymCount].CartnAtoms);
						crystalChainsHash.Add (asymChain + "_" + symMatrix.symmetryString, chainAtomsList[asymCount].CartnAtoms);
						continue;
					}
					AtomInfo[] transformedAtomList = TransformChainBySpecificSymOp(chainAtomsList[asymCount].CartnAtoms, symMatrix);
					crystalChainsHash.Add (asymChain + "_" + symMatrix.symmetryString, transformedAtomList);
				}				
			}
			return crystalChainsHash;
		}
		#endregion

		#region chain transform
		/// <summary>
		/// transform a protein chain by applying a specific symmetry operation
		/// either from PDB xml or from a user-defined
		/// </summary>
		/// <param name="chainCoordList"></param>
		/// <param name="symMatrix"></param>
		/// <returns></returns>
		public AtomInfo[] TransformChainBySpecificSymOp(AtomInfo[] chainAtomList, SymOpMatrix symMatrix)
		{
			AtomInfo[] transformedAtomList = new AtomInfo [chainAtomList.Length];	
			for (int atomI = 0; atomI < chainAtomList.Length; atomI ++)
			{
				AtomInfo thisAtomInfo = chainAtomList[atomI].TransformAtom (symMatrix, cartn2fractMatrix, fract2cartnMatrix);		
				transformedAtomList[atomI] = thisAtomInfo;
			}
			return transformedAtomList;
		}

		/// <summary>
		/// get the fraction coordinates
		/// </summary>
		/// <param name="chainAtomList"></param>
		private void SetFractCoordinates (AtomInfo[] chainAtomList)
		{
			for (int atomI = 0; atomI < chainAtomList.Length; atomI ++)
			{
				chainAtomList[atomI].GetFractCoord (cartn2fractMatrix);
			}
		}
		/// <summary>
		/// transform a protein chain by applying a specific symmetry operation
		/// either from PDB xml or from a user-defined
		/// </summary>
		/// <param name="chainCoordList"></param>
		/// <param name="symMatrix"></param>
		/// <returns></returns>
		private AtomInfo[] TransformChainBySymOpAndTrans(AtomInfo[] chainAtomList, SymOpMatrix symMatrix, double[] transVector)
		{
			AtomInfo[] transformedAtomList = new AtomInfo [chainAtomList.Length];	
			for (int atomI = 0; atomI < chainAtomList.Length; atomI ++)
			{
				AtomInfo thisAtomInfo = chainAtomList[atomI].TransformAtom (symMatrix, cartn2fractMatrix, fract2cartnMatrix);		
				thisAtomInfo.xyz.X += transVector[0];
				thisAtomInfo.xyz.Y += transVector[1];
				thisAtomInfo.xyz.Z += transVector[2];
				transformedAtomList[atomI] = thisAtomInfo;
			}
			return transformedAtomList;
		}
		#endregion
		
		#region compact unit cell
		/// <summary>
		/// build base unit cell of a crystal from atom coordinates from XML file, 
		/// symmetry operators for the space group
		/// </summary>
		/// <param name="crystalXmlFile">our own crystal XML file</param>
        public int[] BuildCrystal(string crystalXmlFile, ref Dictionary<string, AtomInfo[]> crystalChainsHash)
		{
			// read data from crystal xml file
			EntryCrystal thisEntryCrystal;
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(EntryCrystal));
			FileStream xmlFileStream = new FileStream(crystalXmlFile, FileMode.Open);
			thisEntryCrystal = (EntryCrystal) xmlSerializer.Deserialize (xmlFileStream);
			crystal1 = thisEntryCrystal.crystal1;
			xmlFileStream.Close ();

       /*     if (thisEntryCrystal.atomCat.IsOnlyCalphaChains())
            {
                throw new Exception("The crystal structure " + thisEntryCrystal.PdbId + " contains only Calpha atoms for at least of one chain. Skip it.");
            }*/
			int[] maxSteps = BuildCrystal(thisEntryCrystal, ref crystalChainsHash);
			return maxSteps;
		}

        public int[] BuildCrystal(EntryCrystal thisEntryCrystal, ref Dictionary<string, AtomInfo[]> crystalChainsHash)
		{
			if (AppSettings.symOps == null)
			{
				AppSettings.LoadSymOps ();
			}
			if (crystal1.spaceGroup == "-")
			{
				crystal1.spaceGroup = "P 1";
			}
			SymOpMatrix[] symOpMatrices = AppSettings.symOps.FindSpaceGroup(crystal1.spaceGroup);
			if (symOpMatrices == null)
			{
				throw new Exception ("No symmetry operators provided for this entry " + thisEntryCrystal.PdbId);
			}
			cartn2fractMatrix = thisEntryCrystal.scaleCat.ScaleMatrix;
			fract2cartnMatrix = cartn2fractMatrix.Inverse ();

			//ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
			ChainAtoms[] chainAtomsList = new ChainAtoms [thisEntryCrystal.atomCat.ChainAtomList.Length];
			switch (atomType.ToUpper ())
			{
				case "CA":
					chainAtomsList = thisEntryCrystal.atomCat.CalphaAtomList();
					// clear the whole atom list,free memory
					thisEntryCrystal.atomCat.ChainAtomList = null;
					break;

				case "CB":
					chainAtomsList = thisEntryCrystal.atomCat.CbetaAtomList();
					// clear the whole atom list, free memory
					thisEntryCrystal.atomCat.ChainAtomList = null;
					break;

				case "CA_CB":
					chainAtomsList = thisEntryCrystal.atomCat.CalphaCbetaAtomList();
					// clear the whole atom list, free memory
					thisEntryCrystal.atomCat.ChainAtomList = null;
					break;

				case "ALL":
					chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
					break;

				default:
					break;
			}	
			crystalChainsHash = BuildCrystalBySymOps(chainAtomsList, symOpMatrices);
			int[] maxSteps = TranslateUnitCell (ref crystalChainsHash);
			return maxSteps;
		}
		/// <summary>
		/// compact symmetry chains into a unit cell
		/// by translating all other chains to a unit cell 
		/// with the center of the original asymmetric unit
		/// </summary>
		/// <param name="crystalChainsHash"></param>
		/// <returns></returns>
        private int[] TranslateUnitCell(ref Dictionary<string, AtomInfo[]> crystalChainsHash)
		{
			Dictionary<string, BoundingBox> chainBbHash = null;
		    Dictionary<string, double[]>  transCellNumHash = null;
			// center position of each chain
			GetCenterPosOfCellChains (crystalChainsHash, ref chainBbHash);
			// the original coordinate in fractional
			Coordinate origPos = GetOriginalPos (chainBbHash);
			// the maximum steps go outside of the cell
			int[] maxSteps = GetMaxSteps (chainBbHash, origPos, ref transCellNumHash);
			// translate chains into a more compact unit cell
			TranslateCellChains (transCellNumHash, ref crystalChainsHash);
			return maxSteps;
		}
		
		/// <summary>
		/// the center position of each chain in the symmetry unit cell
		/// </summary>
		/// <param name="crystalChainsHash"></param>
		/// <returns></returns>
		private void GetCenterPosOfCellChains (Dictionary<string, AtomInfo[]> crystalChainsHash, ref Dictionary<string, BoundingBox> chainBbHash)
		{
			chainBbHash = new Dictionary<string,BoundingBox> ();
			double minX = 100;
			double minY = 100;
			double minZ = 100;
			double maxX = -100;
			double maxY = -100;
			double maxZ = -100;

			foreach (string cellChain in crystalChainsHash.Keys)
			{
				minX = 100;
				minY = 100;
				minZ = 100;
				maxX = -100;
				maxY = -100;
				maxZ = -100;
				AtomInfo[] chainAtoms =(AtomInfo[]) crystalChainsHash[cellChain];
				foreach (AtomInfo atom in chainAtoms)
				{
					if (minX > atom.fractCoord.X)
					{
						minX = atom.fractCoord.X;
					}
					if (maxX < atom.fractCoord.X)
					{
						maxX = atom.fractCoord.X;
					}
					if (minY > atom.fractCoord.Y)
					{
						minY = atom.fractCoord.Y;
					}
					if (maxY < atom.fractCoord.Y)
					{
						maxY = atom.fractCoord.Y;
					}
					if (minZ > atom.fractCoord.Z)
					{
						minZ = atom.fractCoord.Z;
					}
					if (maxZ < atom.fractCoord.Z)
					{
						maxZ = atom.fractCoord.Z;
					}
				}
				BoundingBox bb = new BoundingBox ();
				bb.Add (minX, maxX);
				bb.Add (minY, maxY);
				bb.Add (minZ, maxZ);
				chainBbHash.Add (cellChain, bb);
			}
		}

		/// <summary>
		/// the center of the original asymmetric unit
		/// </summary>
		/// <param name="chainBbHash"></param>
		/// <returns></returns>
		private Coordinate GetOriginalPos (Dictionary<string, BoundingBox> chainBbHash)
		{
			double minX = 100;
			double minY = 100;
			double minZ = 100;
			double maxX = -100;
			double maxY = -100;
			double maxZ = -100;
			foreach (string cellChain in chainBbHash.Keys)
			{
				if (cellChain.Substring (cellChain.IndexOf ("_") + 1, cellChain.Length - cellChain.IndexOf ("_") - 1) == "1_555")
				{
					BoundingBox chainBb = (BoundingBox)chainBbHash[cellChain];
					if (minX > chainBb.MinMaxList[0].minimum)
					{
						minX = chainBb.MinMaxList[0].minimum;
					}
					if (maxX < chainBb.MinMaxList[0].maximum)
					{
						maxX = chainBb.MinMaxList[0].maximum;
					}
					if (minY > chainBb.MinMaxList[1].minimum)
					{
						minY = chainBb.MinMaxList[1].minimum;
					}
					if (maxY < chainBb.MinMaxList[1].maximum)
					{
						maxY = chainBb.MinMaxList[1].maximum;
					}
					if (minZ > chainBb.MinMaxList[2].minimum)
					{
						minZ = chainBb.MinMaxList[2].minimum;
					}
					if (maxZ < chainBb.MinMaxList[2].maximum)
					{
						maxZ = chainBb.MinMaxList[2].maximum;
					}
				}
			}
			double centerX = (maxX + minX) / 2.0;
			double centerY = (maxY + minY) / 2.0;
			double centerZ = (maxZ + minZ) / 2.0;

			Coordinate origCoord = new Coordinate ();

			
			/* the center of original asymmetric unit is always in the unit cell
			 the unit cell is always between origin coordinate and plus 1 from origCoord
			 e.g. center (0.23, 1.56, -4.5)
			 origCoord (0, 1, -5)
			 unit cell is between (0, 1, -5) and (1, 2, -4)
			 */
			origCoord.X = Math.Floor (centerX);
			origCoord.Y = Math.Floor (centerY);
			origCoord.Z = Math.Floor (centerZ);

			/*		if (centerX >= 0)
					{
						origCoord.X = Math.Floor (centerX);
					}
					else
					{
					// forgot why I did like this, it is wrong
						origCoord.X = Math.Floor (Math.Abs (centerX)) - 1;
					}
					if (centerY >= 0)
					{
						origCoord.Y = Math.Floor (centerY);
					}
					else
					{
						origCoord.Y = Math.Floor (Math.Abs (centerY)) - 1;
					}
					if (centerZ >= 0)
					{
						origCoord.Z = Math.Floor (centerZ);
					}
					else
					{
						origCoord.Z = Math.Floor (Math.Abs (centerZ)) - 1;
					}*/
			return origCoord;

		}

		/// <summary>
		/// get the steps for each direction
		/// </summary>
		/// <param name="chainBbHash"></param>
		/// <returns></returns>
        private int[] GetMaxSteps(Dictionary<string, BoundingBox> chainBbHash, Coordinate origPos, ref Dictionary<string, double[]> transCellNumHash)
		{
			int[] maxSteps = new int [6];
			double minX = 100;
			double maxX = -100;
			double minY = 100;
			double maxY = -100;
			double minZ = 100;
			double maxZ = -100;
			
			transCellNumHash = new Dictionary<string, double[]> ();

			List<string> chainList = new List<string> (chainBbHash.Keys);
			chainList.Sort ();
			foreach (string cellChain in chainList)
			{
				BoundingBox chainBb = (BoundingBox)chainBbHash[cellChain];

				Coordinate centerPos = new Coordinate ();
				centerPos.X = (chainBb.MinMaxList[0].minimum + chainBb.MinMaxList[0].maximum) / 2.0;
				centerPos.Y = (chainBb.MinMaxList[1].minimum + chainBb.MinMaxList[1].maximum) / 2.0;
				centerPos.Z = (chainBb.MinMaxList[2].minimum + chainBb.MinMaxList[2].maximum) / 2.0;


				double[] transCellVect = new double [3];
			
				if (cellChain.Substring (cellChain.IndexOf ("_") + 1, cellChain.Length - cellChain.IndexOf ("_") - 1) != "1_555")
				{
					if (centerPos.X >= origPos.X + 1)
					{
						double intX = Math.Ceiling (centerPos.X - origPos.X - 1);
						transCellVect[0] = intX * (-1);
						chainBb.MinMaxList[0].minimum -= intX;
						chainBb.MinMaxList[0].maximum -= intX;
					}
					else if (centerPos.X < origPos.X)
					{
						double intX = Math.Ceiling (Math.Abs (origPos.X - centerPos.X));
						transCellVect[0] = intX;
						chainBb.MinMaxList[0].minimum += intX;
						chainBb.MinMaxList[0].maximum += intX;
					}
					if (centerPos.Y >= origPos.Y + 1)
					{
						double intY = Math.Ceiling (centerPos.Y - origPos.Y - 1);
						transCellVect[1] = intY * (-1);
						chainBb.MinMaxList[1].minimum -= intY;
						chainBb.MinMaxList[1].maximum -= intY;
					}
					else if (centerPos.Y < origPos.Y)
					{
						double intY = Math.Ceiling (Math.Abs (origPos.Y - centerPos.Y));
						transCellVect[1] = intY;
						chainBb.MinMaxList[1].minimum += intY;
						chainBb.MinMaxList[1].maximum += intY;
					}
					if (centerPos.Z >= origPos.Z + 1)
					{
						double intZ = Math.Ceiling (centerPos.Z - origPos.Z - 1);
						transCellVect[2] = intZ * (-1);
						chainBb.MinMaxList[2].minimum -= intZ;
						chainBb.MinMaxList[2].maximum -= intZ;
					}
					else if (centerPos.Z < origPos.Z)
					{
						double intZ = Math.Ceiling (Math.Abs (origPos.Z - centerPos.Z));
						transCellVect[2] = intZ;
						chainBb.MinMaxList[2].minimum += intZ;
						chainBb.MinMaxList[2].maximum += intZ;
					}
					transCellNumHash.Add (cellChain, transCellVect);
				}
				
				if (minX > chainBb.MinMaxList[0].minimum)
				{
					minX = chainBb.MinMaxList[0].minimum;
				}
				if (maxX < chainBb.MinMaxList[0].maximum)
				{
					maxX = chainBb.MinMaxList[0].maximum;
				}
				if (minY > chainBb.MinMaxList[1].minimum)
				{
					minY = chainBb.MinMaxList[1].minimum;
				}
				if (maxY < chainBb.MinMaxList[1].maximum)
				{
					maxY = chainBb.MinMaxList[1].maximum;
				}
				if (minZ > chainBb.MinMaxList[2].minimum)
				{
					minZ = chainBb.MinMaxList[2].minimum;
				}
				if (maxZ < chainBb.MinMaxList[2].maximum)
				{
					maxZ = chainBb.MinMaxList[2].maximum;
				}
			}
			Coordinate interfaceCutoffCoord = new Coordinate ();
			interfaceCutoffCoord.X = AppSettings.parameters.contactParams.cutoffAtomDist;
			interfaceCutoffCoord.Y = AppSettings.parameters.contactParams.cutoffAtomDist;
			interfaceCutoffCoord.Z = AppSettings.parameters.contactParams.cutoffAtomDist;
			Coordinate cutoffFractCoord = cartn2fractMatrix * interfaceCutoffCoord;

			/* don't remember why I did like this
			 * maxSteps[0] = (int)(Math.Abs (minX) + Math.Abs (maxX) + cutoffFractCoord.X);			
			maxSteps[1] = (int)(Math.Abs (minY) + Math.Abs (maxY) + cutoffFractCoord.Y);			
			maxSteps[2] = (int)(Math.Abs (minZ) + Math.Abs (maxZ) + cutoffFractCoord.Z);	*/
			maxSteps[0] = GetDimTransCellNumInPos (minX, maxX, cutoffFractCoord.X);
			maxSteps[1] = GetDimTransCellNumInNeg (minX, maxX, cutoffFractCoord.X);
			maxSteps[2] = GetDimTransCellNumInPos (minY, maxY, cutoffFractCoord.Y);
			maxSteps[3] = GetDimTransCellNumInNeg (minY, maxY, cutoffFractCoord.Y);
			maxSteps[4] = GetDimTransCellNumInPos (minZ, maxZ, cutoffFractCoord.Z);
			maxSteps[5] = GetDimTransCellNumInNeg (minZ, maxZ, cutoffFractCoord.Z);
			return maxSteps;
		}

		private int GetDimTransCellNumInPos (double minVal, double maxVal, double cutoff)
		{
			double n = 0.0;
			// the bounding box move out of the bounding box with original asu in positive direction
			while (minVal + n < maxVal + cutoff)
			{
				n = n + 1.0;
			}
			return (int)n - 1;
		}

		private int GetDimTransCellNumInNeg (double minVal, double maxVal, double cutoff)
		{
			double n = 0.0;
			// the bounding box of the unit cell moves out of the bounding box of the base unit cell 
			// with original asu in negative direction
			while (maxVal - n > minVal - cutoff)
			{
				n = n + 1.0;
			}
			return (int)n - 1;
		}


		/// <summary>
		/// translate chains into the unit cell containing most of chains
		/// </summary>
		/// <param name="transCellNumHash"></param>
		/// <param name="crystalChainsHash"></param>
		private void TranslateCellChains (Dictionary<string, double[]> transCellNumHash, ref Dictionary<string, AtomInfo[]> crystalChainsHash)
		{
			Dictionary<string, AtomInfo[]> tempHash = new Dictionary<string,AtomInfo[]> (crystalChainsHash);
			crystalChainsHash = new Dictionary<string, AtomInfo[]> ();
			foreach (string cellChain in tempHash.Keys)
			{
				if (cellChain.IndexOf ("_1_555") > -1)
				{
					crystalChainsHash.Add (cellChain, tempHash[cellChain]);
				}
				else
				{
					double[] transCellVect = (double[])transCellNumHash[cellChain];
					double[] cartnVect = ComputeTransVectInCartn (transCellVect);
					string transSymOpString = GetTransSymOpString (cellChain, transCellVect);
					AtomInfo[] chainAtoms = (AtomInfo[]) tempHash[cellChain];
					for (int i = 0; i < chainAtoms.Length; i ++)
					{
						chainAtoms[i].fractCoord.X += transCellVect[0];
						chainAtoms[i].fractCoord.Y += transCellVect[1];
						chainAtoms[i].fractCoord.Z += transCellVect[2];
						chainAtoms[i].xyz.X += cartnVect[0];
						chainAtoms[i].xyz.Y += cartnVect[1];
						chainAtoms[i].xyz.Z += cartnVect[2];
					}
					crystalChainsHash.Add (transSymOpString, chainAtoms);
				}
			}

		}

		/// <summary>
		/// get the symmetry operator string for the translated chain
		/// </summary>
		/// <param name="origSymOpString"></param>
		/// <param name="transCellVect"></param>
		/// <returns></returns>
		private string GetTransSymOpString (string origSymOpString, double[] transCellVect)
		{
			bool isSign = false;
			string transSymOpString = origSymOpString.Substring (0, origSymOpString.LastIndexOf ("_") + 1);
			string symOpString = origSymOpString.Substring (origSymOpString.LastIndexOf ("_") + 1, 3);
			int vectX = Convert.ToInt32 (symOpString[0].ToString ()) + (int)transCellVect[0];
			int vectY = Convert.ToInt32 (symOpString[1].ToString ()) + (int)transCellVect[1];
			int vectZ = Convert.ToInt32 (symOpString[2].ToString ()) + (int)transCellVect[2];
			/* if translate vector >= 10 or negative numbers, 
			 * add signs as a separator 
			 * e.g. translate vector = [6, -7, 3], symmetry operator: 1_+11-1+8
			 */
			if (vectX > 9 || vectY > 9 || vectZ > 9 ||
				vectX < 0 || vectY < 0 || vectZ < 0)
			{
				isSign = true;
			}
			if (isSign)
			{
				AddTranslateVectToSymmetryString (vectX, ref transSymOpString);
				AddTranslateVectToSymmetryString (vectY, ref transSymOpString);
				AddTranslateVectToSymmetryString (vectZ, ref transSymOpString);
			}
			else
			{
				transSymOpString += (vectX.ToString () + vectY.ToString () + vectZ.ToString ());
			}
			return transSymOpString;
		}

		private void AddTranslateVectToSymmetryString (int transVect, ref string transSymOpString)
		{
			if (transVect >= 0)
			{
				transSymOpString += ("+" + transVect.ToString ());
			}
			else
			{
				transSymOpString += transVect.ToString ();
			}
		}
		#endregion

		#region helper functions	
		/// <summary>
		/// convert the translation vectors in fractional coordinate
		/// into vectors in cartesian coordinate
		/// Note: fract2CartMatrix has vectors which are used to compute cartesian coordinate
		/// do not use it to translate the translation vectors.
		/// </summary>
		/// <param name="transFractVectors"></param>
		/// <param name="fract2CartnMatrix"></param>
		/// <returns></returns>
		private double[] ComputeTransVectInCartn(double[] transFractVectors)
		{
			double[] cartnVectors = new double [3];
			cartnVectors[0] = fract2cartnMatrix.Value (0, 0) * transFractVectors[0] + 
				fract2cartnMatrix.Value (0, 1) * transFractVectors[1] +
				fract2cartnMatrix.Value (0, 2) * transFractVectors[2];
			cartnVectors[1] = fract2cartnMatrix.Value (1, 0) * transFractVectors[0] + 
				fract2cartnMatrix.Value (1, 1) * transFractVectors[1] +
				fract2cartnMatrix.Value (1, 2) * transFractVectors[2];
			cartnVectors[2] = fract2cartnMatrix.Value (2, 0) * transFractVectors[0] + 
				fract2cartnMatrix.Value (2, 1) * transFractVectors[1] +
				fract2cartnMatrix.Value (2, 2) * transFractVectors[2];
			return cartnVectors;
		}
		#endregion

        #region atoms transform
        public void SetCartn2fractMatrix(string xmlFile)
        {
            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(xmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            crystal1 = thisEntryCrystal.crystal1;
            xmlFileStream.Close();

            cartn2fractMatrix = thisEntryCrystal.scaleCat.ScaleMatrix;
            fract2cartnMatrix = cartn2fractMatrix.Inverse();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="atoms"></param>
        /// <param name="spaceGroup"></param>
        /// <param name="symOpString"></param>
        /// <returns></returns>
        public AtomInfo[] TransformChainBySpecificSymOp(AtomInfo[] atoms, string spaceGroup, string symOpString)
        {
            if (symOpString == origSymOpString)
            {
                return atoms;
            }

            if (AppSettings.symOps == null)
            {
                AppSettings.LoadSymOps();
            }
           
            SymOperator symOp = new SymOperator();
            SymOpMatrix[] sgSymOpMatrices = AppSettings.symOps.FindSpaceGroup(spaceGroup);

            SymOpMatrix symOpMatrix = symOp.GetSymmetryMatrixFromSymmetryString(sgSymOpMatrices, symOpString);
            AtomInfo[] transformedAtoms = TransformChainBySpecificSymOp(atoms, symOpMatrix);
            return transformedAtoms;
        }
        #endregion
    }
}
