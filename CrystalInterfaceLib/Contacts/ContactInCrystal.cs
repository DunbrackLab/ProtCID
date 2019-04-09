using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.Settings;
using CrystalInterfaceLib.KDops;
using CrystalInterfaceLib.StructureComp;

namespace CrystalInterfaceLib.Contacts
{
	/// <summary>
	/// detect contacts in a crystal
	/// </summary>
	public class ContactInCrystal
	{
		#region member variables

		string pdbId = "";
		/* store the BVTree for each asymmetric chains and 
		 * their symmetric chains after applying symmetry operations
		 * Key: asymChain, value: BVTree
		 */
		private Dictionary<string, BVTree> asymChainTreesHash = new Dictionary<string,BVTree> ();

		private int interChainId = 0;
		private List<string> parsedPairList = null;
		private List<InterfaceChains> interfaceChainsList = new List<InterfaceChains> ();
//		private ParameterSettings parameters = null;
		private CrystalInfo cryst1 = new CrystalInfo ();
		private Matrix fract2CartnMatrix = new Matrix ();
		private List<int> xStepList = null;
		private List<int> yStepList = null;
		private List<int> zStepList = null;

		InterfacesComp interfacesComp = null;
		//	Hashtable asymChainHash = null;
		private const int unitCellSize = 64;
        private const int maxUnitCellSize = 128;
		private const int maximumStep = 5;
		#endregion

		#region constructors
		/*	public ContactInCrystal(ParameterSettings inParameters, Hashtable asymHash)
			{
				parameters = inParameters;
				interfacesComp = new InterfacesComp ();
				asymChainHash = asymHash;
			}*/

		public ContactInCrystal ()
		{
			interfacesComp = new InterfacesComp ();
		}
		#endregion

		#region read only properties
		/// <summary>
		/// an array of unique interface chains in the crystal
		/// </summary>
		public InterfaceChains[] InterfaceChainsList
		{
			get
			{
				if (interfaceChainsList.Count == 0)
				{
					return null;
				}
				InterfaceChains[] interfaceChains = new InterfaceChains [interfaceChainsList.Count];
				interfaceChainsList.CopyTo (interfaceChains);
				return interfaceChains;
			}
		}
		#endregion

		#region interfaces ALL
		/// <summary>
		/// Detect all interfaces in a crystal 
		/// built from XML coordinates file
		/// </summary>
		/// <param name="xmlFile"></param>
		/// <param name="paramFile"></param>
		public int FindInteractChains(string xmlFile)
		{
			interChainId = 0;
			asymChainTreesHash.Clear ();
			interfaceChainsList.Clear ();
			pdbId = xmlFile.Substring (xmlFile.LastIndexOf ("\\") + 1, 4);
			CrystalBuilder crystalBuilder = new CrystalBuilder (AppSettings.parameters.contactParams.atomType);
			Dictionary<string, AtomInfo[]> unitCellChainsHash = null;
			int[] maxSteps = null;
			// build crystal based on all symmetry operations
			try
			{
				maxSteps = crystalBuilder.BuildCrystal (xmlFile, ref unitCellChainsHash);
				if (unitCellChainsHash == null)
				{
					throw new Exception ("Cannot build unit cell.");
				}
                // if there are too many chains in a unit cell
                // only compute the interfaces between original ASU chains
                // and other chains
                if (unitCellChainsHash.Count > unitCellSize)
                {
                    //	throw new Exception ("Unit cell too large. Skip processing " + pdbId + ".");
                    // if there are too many chains in the unit cell,
                    // only check the interfaces within the unit cell.
                    for (int i = 0; i < maxSteps.Length; i++)
                    {
                        maxSteps[i] = 0;
                    }
                }
				// if there are too many chains, only use chains in the asymmetric unit
                if (unitCellChainsHash.Count >= maxUnitCellSize)
                {
                    Dictionary<string, AtomInfo[]> asuChainHash = GetAsymUnitHash(unitCellChainsHash);
                    unitCellChainsHash = asuChainHash;
                    // if it is still too big due to Non-symmetry operators,
                    // only use chains in the original asu in the PDB file, may not be the real Asu
                    if (asuChainHash.Count >= unitCellSize)
                    {
                        Dictionary<string, AtomInfo[]> origAsuChainHash = GetOrigAsymUnitHash(asuChainHash);
                        unitCellChainsHash = origAsuChainHash;
                    }
                }
			}
			catch (Exception ex)
			{
				throw new Exception ("Building Crystal Errors: " + xmlFile + " " + ex.Message);
			}
			/* 
			 * build BVTree for original asymmetric chains + 
			 * their chains after applied symmetry operations
			 * without translations
			*/
			try
			{
				// for each chain in a cell including original and symmetric chains
                foreach (string chainAndSymOp in unitCellChainsHash.Keys)
				{
					BVTree chainTree = new BVTree ();
					chainTree.BuildBVTree (unitCellChainsHash[chainAndSymOp], AppSettings.parameters.kDopsParam.bvTreeMethod, true);
					asymChainTreesHash.Add (chainAndSymOp, chainTree);
				}
			}
			catch (Exception ex)
			{
				throw new Exception ("Building Bounding Volumn Tree Errors: " + xmlFile + ". " + ex.Message);
			}
			/* 
			 * the critical step for the computation time
			 * check all pairs of chains including translated chains
			 * return a list of atompairs if exists
			*/ 
			cryst1 = crystalBuilder.Crystal1;
			fract2CartnMatrix = crystalBuilder.fract2cartnMatrix;

			List<string> chainAndSymOps = new List<string> (asymChainTreesHash.Keys);
			List<string> orderedChainAndSymOps = SortChainsInUnitCell (chainAndSymOps);
			
			// check interactions between 2 different chains and their translated chains
			// chainAndSymOps: chainId_symmetry operator_full symmetry operations
			// e.g. A_1_545
			try
			{
				ComputeUniqueInterfacesFromCryst (orderedChainAndSymOps, maxSteps);
			}
			catch (Exception ex)
			{
			/*	ProtCidSettings.progressInfo.progStrQueue.Enqueue 
					("Retrieving unique interfaces of " + pdbId + " errors: " + ex.Message);*/
                throw new Exception("Retrieving unique interfaces of " + pdbId + " errors: " + ex.Message);
			}
			return unitCellChainsHash.Count;
		} 

		/// <summary>
		/// unique interfaces from crystal structure
		/// </summary>
		/// <param name="orderedChainAndSymOps"></param>
		private void ComputeUniqueInterfacesFromCryst (List<string> orderedChainAndSymOps, int[] maxSteps)
		{
			bool skipOrig = false;
			bool newInterfaceExist = false;
			parsedPairList = new List<string> ();
			int stepCount = 0;
			int maxStep = maxSteps[0];
			foreach (int step in maxSteps)
			{
				if (maxStep < step)
				{
					maxStep = step;
				}
			}
			if (maxStep > maximumStep)
			{
		//		maxStep = maximumStep;
                maxStep = 0;
		//		throw new Exception ("Step size " + maxStep + " is greater than " + maximumStep);
			}
			while (stepCount <= maxStep)
			{
				GetStepsList (stepCount, maxSteps);
				newInterfaceExist = false;
				for (int i = 0; i < orderedChainAndSymOps.Count; i ++)
				{
					/*		string symOpString = orderedChainAndSymOps[i].ToString ();
							if (asuInterfaceOnly && 
								symOpString.Substring (symOpString.IndexOf ("_") + 1, symOpString.Length - symOpString.IndexOf ("_") - 1) != "1_555")
							{
								continue;
							}*/
					for (int j = 0; j < orderedChainAndSymOps.Count; j ++)
					{
						skipOrig = false;
						if (i == j)
						{
							// interfaces between different translations for same chain
							bool interfaceExist = DetectChainContactWithTranslation (orderedChainAndSymOps[i].ToString (),	
								(BVTree)asymChainTreesHash[orderedChainAndSymOps[i]]);
							newInterfaceExist = newInterfaceExist || interfaceExist;
						}
						else
						{
							if (i > j)
							{
								// the pair is already done
								// e.g. A_1_555 and B_1_555 is same as B_1_555 and A_1_555
								skipOrig = true;
							}
							// interfaces between different translations of 2 different chains
							bool interfaceExist = DetectChainContactWithTranslation (orderedChainAndSymOps[i].ToString (), orderedChainAndSymOps[j].ToString (),
								(BVTree)asymChainTreesHash[orderedChainAndSymOps[i]], 
								(BVTree)asymChainTreesHash[orderedChainAndSymOps[j]], skipOrig);
							newInterfaceExist = newInterfaceExist || interfaceExist;
						}
					}
				}
				if (! newInterfaceExist && stepCount > 0)
				{
					break;
				}
				stepCount ++;
			}
		}
		/// <summary>
		/// order chains with symmetry operators
		/// (A, B, C, ...)_1_555, 2_555
		/// asymmetric unit chains first
		/// </summary>
		/// <param name="chainAndSymOps"></param>
		/// <returns></returns>
		private List<string> SortChainsInUnitCell (List<string> chainAndSymOps)
		{
			List<string> orderedChainSymOpList = new List<string> ();
			Dictionary<string, List<string>> symOpChainHash = new Dictionary<string,List<string>> ();
			foreach (string chainSymOpString in chainAndSymOps)
			{
				int chainIndex = chainSymOpString.IndexOf ("_");
				string symOpString = chainSymOpString.Substring (chainIndex + 1, chainSymOpString.Length - chainIndex - 1);
				string chain = chainSymOpString.Substring (0, chainIndex);
				if (symOpChainHash.ContainsKey (symOpString))
				{
                    symOpChainHash[symOpString].Add(chain);
				}
				else
				{
					List<string> chainList = new List<string> ();
					chainList.Add (chain);
					symOpChainHash.Add (symOpString, chainList);
				}
			}
			List<string> symOpStringList = new List<string> (symOpChainHash.Keys);
			symOpStringList.Sort ();
			foreach (string symOpString in symOpStringList)
			{
				List<string> chainList = symOpChainHash[symOpString];
				chainList.Sort ();
				foreach (string chain in chainList)
				{
					orderedChainSymOpList.Add (chain + "_" + symOpString);
				}
			}
			return orderedChainSymOpList;
		}

		#region Contact detecting
		#region contacts of same asymmetric chain
		/// <summary>
		/// check interaction a center chain with 
		/// its corresponding translated chains
		/// </summary>
		/// <param name="chainASymOp"></param>
		/// <param name="chainBSymOp"></param>
		/// <param name="treeA"></param>
		/// <param name="treeB"></param>
		private bool DetectChainContactWithTranslation(string chainASymOp, BVTree origTreeA)
		{
			string transChainASymOp = "";
			List<string> parsedSymOpsList = new List<string> ();		
			int origX = -1;
			int origY = -1;
			int origZ = -1;
			string transVectString = chainASymOp.Substring (chainASymOp.LastIndexOf ("_") + 1, 
				chainASymOp.Length - chainASymOp.LastIndexOf ("_") - 1);
			GetOrigPoint (transVectString, ref origX, ref origY, ref origZ);

			bool thisNewInterfaceExist = false;
		
			/* total number of BVTrees: (step * 2 + 1) ^ 3
				 * total comparison of BVTrees: (step * 2 + 1) - 1
				 * e.g. steps = 1; total comparisons: 26
				 * compare all neighbor unit cells with the original unit cell
				*/
			// compare a center chain with all its corresponding translated chains			
			foreach (int xA in xStepList)
			{
				foreach (int yA in yStepList)
				{
					foreach (int zA in zStepList)
					{
						// skip check contacts with itself
						// probably we need to check contacts within a chain also.
						if (xA == 0 && yA == 0 && zA == 0)
						{
							thisNewInterfaceExist = true;
							continue;
						}

						transChainASymOp = GetTransSymOpString (chainASymOp, origX + xA, origY + yA, origZ + zA);
						if (parsedPairList.Contains (chainASymOp + "_" + transChainASymOp))
						{
							continue;
						}
						else
						{
							parsedPairList.Add (chainASymOp + "_" + transChainASymOp);
						}

						double[] fractVectorA = new double [3] {xA, yA, zA};
						double[] translateVectorA = ComputeTransVectInCartn(fractVectorA);
					
						// get the BVtree for the translated chain
						//	BVTree treeA = origTreeA.UpdateBVTree (translateVectorA);
						
						ChainContact chainContact = new ChainContact (chainASymOp, transChainASymOp);
						ChainContactInfo contactInfo = chainContact.GetChainContactInfo (origTreeA, origTreeA, translateVectorA);
						if (contactInfo != null)
						{
							InterfaceChains interfaceChains = null;
							Node updateRoot = new Node (origTreeA.Root.CalphaCbetaAtoms ());
							updateRoot.UpdateLeafNode (translateVectorA);
							interfaceChains = new InterfaceChains (chainASymOp, transChainASymOp, 
								origTreeA.Root.CalphaCbetaAtoms (), updateRoot.AtomList);
							interfaceChains.seqDistHash = contactInfo.GetBbDistHash ();
							interfaceChains.seqContactHash = contactInfo.GetContactsHash ();
							if ( AddInterChainsToList (interfaceChains))
							{
								thisNewInterfaceExist = true;
							}
						}
					}
				}
			}
			return thisNewInterfaceExist;
		}
		#endregion

		#region contacts between two asymmetric chains
		/// <summary>
		/// interactive chains from all pairs of 2 asymmetric chains + 
		/// their corresponding translated chains
		/// </summary>
		/// <param name="chainASymOp"></param>
		/// <param name="chainBSymOp"></param>
		/// <param name="origTreeA">chain A in the center unit cell
		/// can be a transformed chain from one chain in the asymmetry unit 
		/// after applied symmetry operation </param>
		/// <param name="origTreeB">chain B in the center unit cell(description same as chain A)</param>
		private bool  DetectChainContactWithTranslation(string chainASymOp, string chainBSymOp, 
			BVTree origTreeA, BVTree origTreeB, bool skipOrig)
		{
			// compare center chain A with all translated chain B 
			// translated BVTrees for chain B
			return DetectTwoChainContactWithTranslation 
				(chainBSymOp, chainASymOp, origTreeB, origTreeA, skipOrig);
		}

		/// <summary>
		/// check interaction a center chain with 
		/// another translated chains
		/// </summary>
		/// <param name="chainASymOp">translated chain</param>
		/// <param name="chainBSymOp">center chain</param>
		/// <param name="treeA">translated tree</param>
		/// <param name="treeB">center tree</param>
		private bool DetectTwoChainContactWithTranslation(string translateChainSymOp, string centerChainSymOp, 
			BVTree translateTree, BVTree centerTree, bool skipOrig)
		{
			string transChainSymOp = "";
			int origX = -1;
			int origY = -1;
			int origZ = -1;
			string transVectString = translateChainSymOp.Substring (translateChainSymOp.LastIndexOf ("_") + 1, 
				translateChainSymOp.Length - translateChainSymOp.LastIndexOf ("_") - 1);
			GetOrigPoint (transVectString, ref origX, ref origY, ref origZ);
			
			bool thisNewInterfaceExist = false;
		

			try
			{
				ChainContact chainContact = null;
				/* total number of BVTrees: (step * 2 + 1) ^ 3
					 * total comparison of BVTrees: (step * 2 + 1) - 1
					 * e.g. steps = 1; total comparisons: 26
					 * compare all neighbor unit cells with the original unit cell
					 */
				// comare center chain B with center chain A and all its translated chains
				foreach (int xA in xStepList)
				{
					foreach (int yA in yStepList)
					{
						foreach (int zA in zStepList)
						{
							if (xA == 0 && yA == 0 && zA == 0 && skipOrig)
							{
								thisNewInterfaceExist = true;
								continue;
							}
							// chain_symmetry operator number_translation vector
							// e.g. A_1_556 (chain A, symmetry operator is 1, and translation vector (0, 0, 1))
							transChainSymOp = GetTransSymOpString (translateChainSymOp, origX + xA, origY + yA, origZ + zA);
							if (parsedPairList.Contains (centerChainSymOp + "_" + transChainSymOp))
							{
								continue;
							}
							else
							{
								parsedPairList.Add (centerChainSymOp + "_" + transChainSymOp);
							}

							double[] fractVectorA = new double [3] {xA, yA, zA};
							double[] translateVectorA = ComputeTransVectInCartn(fractVectorA);
							//	BVTree translatedTree = translateTree.UpdateBVTree (translateVectorA);
					
							chainContact = new ChainContact (centerChainSymOp, transChainSymOp);
							ChainContactInfo contactInfo = chainContact.GetChainContactInfo (centerTree, translateTree, translateVectorA);
							if ( contactInfo != null)
							{
								InterfaceChains interfaceChains = null;
								Node updateRoot = new Node (translateTree.Root.CalphaCbetaAtoms ());
								updateRoot.UpdateLeafNode (translateVectorA);
								interfaceChains = new InterfaceChains (centerChainSymOp, transChainSymOp,  
									centerTree.Root.CalphaCbetaAtoms (), updateRoot.AtomList);
								interfaceChains.seqDistHash = contactInfo.GetBbDistHash ();
								interfaceChains.seqContactHash = contactInfo.GetContactsHash ();
								if (AddInterChainsToList (interfaceChains))
								{
									thisNewInterfaceExist = true;
								}
							}
						}
					}
				}// end of translated chain 
			}
			catch (Exception ex)
			{
				throw new Exception (string.Format ("Comparisons of rotated and translated chains ({0}, {1}) errors: {2}", 
					translateChainSymOp, centerChainSymOp, ex.Message));
			}
			return thisNewInterfaceExist;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="transVectString"></param>
        /// <param name="origX"></param>
        /// <param name="origY"></param>
        /// <param name="origZ"></param>
		private void GetOrigPoint (string transVectString, ref int origX, ref int origY, ref int origZ)
		{
			if (transVectString.Length == 3)
			{
				origX = Convert.ToInt32 (transVectString[0].ToString ());
				origY = Convert.ToInt32 (transVectString[1].ToString ());
				origZ = Convert.ToInt32 (transVectString[2].ToString ());
			}
			else if (transVectString.Length >= 4)
			{
				string[] vectStrings = SeparateSymmetryString (transVectString);
				origX = Convert.ToInt32 (vectStrings[0]);
				origY = Convert.ToInt32 (vectStrings[1]);
				origZ = Convert.ToInt32 (vectStrings[2]);
			}
		}

		/// <summary>
		/// get the symmetry operator string for the translated chain
		/// </summary>
		/// <param name="origSymOpString"></param>
		/// <param name="transCellVect"></param>
		/// <returns></returns>
		private string GetTransSymOpString (string origSymOpString, int transX, int transY, int transZ)
		{
			bool isSign = false;
			string transSymOpString = origSymOpString.Substring (0, origSymOpString.LastIndexOf ("_") + 1);
		
			/* if translate vector >= 10 or negative numbers, 
			 * add signs as a separator 
			 * e.g. translate vector = [6, -7, 3], symmetry operator: 1_+11-1+8
			 */
			if (transX > 9 || transY > 9 || transZ > 9 ||
				transX < 0 || transY < 0 || transZ < 0)
			{
				isSign = true;
			}
			if (isSign)
			{
				AddTranslateVectToSymmetryString (transX, ref transSymOpString);
				AddTranslateVectToSymmetryString (transY, ref transSymOpString);
				AddTranslateVectToSymmetryString (transZ, ref transSymOpString);
			}
			else
			{
				transSymOpString += (transX.ToString () + transY.ToString () + transZ.ToString ());
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

		/// <summary>
		/// for symmetry string with large vectors 
		/// e.g. 1_+10-1+6
		/// </summary>
		/// <param name="symOpString"></param>
		private string[] SeparateSymmetryString (string transVectString)
		{
			string[] vectStrings = new string [3];
			int count = -1;
			foreach (char ch in transVectString)
			{
				if (ch == '+' || ch == '-')
				{
					count ++;
				}
				vectStrings[count] += ch.ToString ();
			}
			return vectStrings;
		}
		/// <summary>
		/// get the list of step numbers
		/// </summary>
		/// <param name="stepCount"></param>
		/// <returns></returns>
		private void GetStepsList (int stepCount, int[] maxSteps)
		{
			xStepList = new List<int> ();
			yStepList = new List<int> ();
			zStepList = new List<int> ();
			xStepList.Add (0);
			yStepList.Add (0);
			zStepList.Add (0);
			for (int i = 1; i <= stepCount; i ++)
			{
				if (i <= maxSteps[0])
				{
					xStepList.Add (i);
				}
				if (i <= maxSteps[1])
				{
					xStepList.Add (i * (-1));
				}
				if (i <= maxSteps[2])
				{
					yStepList.Add (i);
				}
				if (i <= maxSteps[3])
				{
					yStepList.Add (i * (-1));
				}
				if (i <= maxSteps[4])
				{
					zStepList.Add (i);
				}
				if (i < maxSteps[5])
				{
					zStepList.Add (i * (-1));
				}
			}
		}
		#endregion

		#region add an interface/interchains
		/// <summary>
		/// add the interactive chains if it is unique
		/// if it is not homomultimer, should consider the sequence alignment 
		/// within an entry
		/// </summary>
		/// <param name="interChains"></param>
		private bool AddInterChainsToList (InterfaceChains interfaceChains)
		{
			bool hasSameInterface = false;
			int interfaceIndex = 0;
			foreach (InterfaceChains interfaceChainsInList in interfaceChainsList)
			{
				if (interfacesComp.AreInterfacesSame (interfaceChainsInList, interfaceChains))
				{
					hasSameInterface = true;
					break;
				}
				interfaceIndex ++;
			}
			if (! hasSameInterface)
			{
				// assign a sequential number
				interChainId ++;
                interfaceChains.pdbId = pdbId;
				interfaceChains.interfaceId = interChainId;
				interfaceChainsList.Add (interfaceChains);
				
				return true;
			}
			else
			{
				InterfaceChains interfaceInList = (InterfaceChains)interfaceChainsList[interfaceIndex];
				if ((! HasOriginalChain (interfaceInList.firstSymOpString) && 
					! HasOriginalChain (interfaceInList.secondSymOpString)) 
					&&
					(HasOriginalChain (interfaceChains.firstSymOpString) || 
					HasOriginalChain (interfaceChains.secondSymOpString)))
				{
                    interfaceChains.pdbId = pdbId;
					interfaceChains.interfaceId = interfaceInList.interfaceId;
					interfaceChainsList[interfaceIndex] = interfaceChains;
					return true;
				}
				else if (((HasOriginalChain (interfaceInList.firstSymOpString) && 
					interfaceInList.secondSymOpString.IndexOf ("555") < 0) 
					|| 
					(interfaceInList.firstSymOpString.IndexOf ("555") < 0 && 
					HasOriginalChain (interfaceInList.secondSymOpString))) 
					&&
					((HasOriginalChain (interfaceChains.firstSymOpString) && 
					interfaceChains.secondSymOpString.IndexOf ("555") > -1)  
					|| 
					(interfaceChains.firstSymOpString.IndexOf ("555") > -1 && 
					HasOriginalChain (interfaceChains.secondSymOpString))))
				{
                    interfaceChains.pdbId = pdbId;
					interfaceChains.interfaceId = interfaceInList.interfaceId;
					interfaceChainsList[interfaceIndex] = interfaceChains;
					return true;
				}
				else if ((interfaceInList.firstSymOpString.IndexOf ("555") < 0 && 
					interfaceInList.secondSymOpString.IndexOf ("555") < 0) 
					&&
					(interfaceChains.firstSymOpString.IndexOf ("555") > -1 || 
					interfaceChains.secondSymOpString.IndexOf ("555") > -1))
				{
                    interfaceChains.pdbId = pdbId;
					interfaceChains.interfaceId = interfaceInList.interfaceId;
					interfaceChainsList[interfaceIndex] = interfaceChains;
					return true;
				}
				else
				{
					return false;
				}	
			}	
		}

		/// <summary>
		/// check if the symmetry string containing 1_555
		/// </summary>
		/// <param name="symOpString"></param>
		/// <returns></returns>
		private bool HasOriginalChain (string symOpString)
		{
			string symOpStr = symOpString.Substring (symOpString.IndexOf ("_") + 1, 
				symOpString.Length - symOpString.IndexOf ("_") - 1);
			if (symOpStr == "1_555")
			{
				return true;
			}
			return false;
		}
		#endregion
		#endregion
		#endregion

		#region Recover Interfaces From Db Info
		/// <summary>
		/// Detect all interfaces in a crystal 
		/// built from XML coordinates file
		/// </summary>
		/// <param name="xmlFile"></param>
		/// <param name="paramFile"></param>
		public void FindInteractChains(string xmlFile, Dictionary<int, string> interfaceDefHash, Dictionary<int, string> interfaceEntityInfoHash, out bool interfaceExist)
		{
			interChainId = 0;
			interfaceChainsList.Clear ();
			asymChainTreesHash.Clear ();
			interfaceExist = true;
			string[] symOpStrings = GetSymOpStrings (interfaceDefHash);

			pdbId = xmlFile.Substring (xmlFile.LastIndexOf ("\\") + 1, 4);
			CrystalBuilder crystalBuilder = new CrystalBuilder (AppSettings.parameters.contactParams.atomType);
			Dictionary<string, AtomInfo[]> interfaceChainHash = null;
			try
			{
				interfaceChainHash = crystalBuilder.BuildCrystal (xmlFile, symOpStrings);
			}
			catch (Exception ex)
			{
				throw new Exception ("Building interface chains errors: " + xmlFile + "  " + ex.Message);
			}

			// for each chain in a cell including original and symmetric chains
			foreach (string chainAndSymOp in interfaceChainHash.Keys)
			{
				BVTree chainTree = new BVTree ();
				chainTree.BuildBVTree (interfaceChainHash[chainAndSymOp], AppSettings.parameters.kDopsParam.bvTreeMethod, true);
				asymChainTreesHash.Add (chainAndSymOp, chainTree);
			}

			ChainContact chainContact = null;
			foreach (int interfaceId in interfaceDefHash.Keys)
			{
				string[] interfaceDefStrings = interfaceDefHash[interfaceId].ToString ().Split (';');
				chainContact = new ChainContact (interfaceDefStrings[0], interfaceDefStrings[1]);
				BVTree tree1 = (BVTree)asymChainTreesHash[interfaceDefStrings[0]];
				BVTree tree2 = (BVTree)asymChainTreesHash[interfaceDefStrings[1]];
				if (tree1 == null || tree2 == null)
				{
					continue;
				}
				ChainContactInfo contactInfo = chainContact.GetChainContactInfo (tree1, tree2);
				
				if (contactInfo == null)
				{
					interfaceExist = false;
					break;
				}
				string[] entityStrings = interfaceEntityInfoHash[interfaceId].Split ('_');
				InterfaceChains interfaceChains = null;
				interfaceChains = new InterfaceChains (interfaceDefStrings[0], interfaceDefStrings[1], tree1.Root.CalphaCbetaAtoms (), tree2.Root.CalphaCbetaAtoms ());
			//	interfaceChains = new InterfaceChains (symOpStrings[0], symOpStrings[1], tree1.Root.AtomList, tree2.Root.AtomList);
				interfaceChains.pdbId = pdbId;
                interfaceChains.interfaceId = interfaceId;
				interfaceChains.entityId1 = Convert.ToInt32 (entityStrings[0]);
				interfaceChains.entityId2 = Convert.ToInt32 (entityStrings[1]);
				interfaceChains.seqDistHash = contactInfo.GetBbDistHash ();
				interfaceChains.seqContactHash = contactInfo.GetContactsHash ();
				interfaceChainsList.Add (interfaceChains);
			}
		} 

		/// <summary>
		/// Detect all interfaces in a crystal 
		/// built from XML coordinates file
		/// </summary>
		/// <param name="xmlFile"></param>
		/// <param name="paramFile"></param>
		public void FindInteractChains(string xmlFile, Dictionary<int, string> interfaceDefHash, out bool interfaceExist)
		{	
			asymChainTreesHash.Clear ();
			interfaceChainsList.Clear ();
			interfaceExist = true;
			string[] symOpStrings = GetSymOpStrings (interfaceDefHash);

			pdbId = xmlFile.Substring (xmlFile.LastIndexOf ("\\") + 1, 4);
			CrystalBuilder crystalBuilder = new CrystalBuilder (AppSettings.parameters.contactParams.atomType);
			Dictionary<string, AtomInfo[]> interfaceChainHash = null;
			try
			{
				interfaceChainHash = crystalBuilder.BuildCrystal (xmlFile, symOpStrings);
			}
			catch (Exception ex)
			{
				throw new Exception ("Building interface chains errors: " + xmlFile + ". " + ex.Message);
			}

			// for each chain in a cell including original and symmetric chains
			foreach (string chainAndSymOp in interfaceChainHash.Keys)
			{
				BVTree chainTree = new BVTree ();
				chainTree.BuildBVTree (interfaceChainHash[chainAndSymOp], AppSettings.parameters.kDopsParam.bvTreeMethod, true);
				asymChainTreesHash.Add (chainAndSymOp, chainTree);
			}

			ChainContact chainContact = null;
            List<int> interfaceList = new List<int> (interfaceDefHash.Keys);
            interfaceList.Sort();
			foreach (int interfaceId in interfaceList)
			{
				string[] interfaceDefStrings = interfaceDefHash[interfaceId].ToString ().Split (';');
				chainContact = new ChainContact (interfaceDefStrings[0], interfaceDefStrings[1]);
				BVTree tree1 = (BVTree)asymChainTreesHash[interfaceDefStrings[0]];
				BVTree tree2 = (BVTree)asymChainTreesHash[interfaceDefStrings[1]];
				ChainContactInfo contactInfo = chainContact.GetChainContactInfo (tree1, tree2);
				
				if (contactInfo == null)
				{
					interfaceExist = false;
					continue;
				}
				InterfaceChains interfaceChains = null;
				interfaceChains = new InterfaceChains (interfaceDefStrings[0], interfaceDefStrings[1], 
					tree1.Root.CalphaCbetaAtoms (), tree2.Root.CalphaCbetaAtoms ());
                interfaceChains.pdbId = pdbId;
				interfaceChains.interfaceId = interfaceId;
				interfaceChains.seqDistHash = contactInfo.GetBbDistHash ();
				interfaceChains.seqContactHash = contactInfo.GetContactsHash ();
				interfaceChainsList.Add (interfaceChains);
                interfaceExist = true;
			}
		}

        /// <summary>
        /// Detect all interfaces in a crystal 
        /// built from XML coordinates file
        /// </summary>
        /// <param name="xmlFile"></param>
        /// <param name="paramFile"></param>
        public void FindInteractChains(string xmlFile, Dictionary<int, string> interfaceDefHash)
        {
            asymChainTreesHash.Clear();
            interfaceChainsList.Clear();
            string[] symOpStrings = GetSymOpStrings(interfaceDefHash);
            if (AppSettings.parameters == null)
            {
                AppSettings.LoadParameters();
            }

            pdbId = xmlFile.Substring(xmlFile.LastIndexOf("\\") + 1, 4);
            CrystalBuilder crystalBuilder = new CrystalBuilder("ALL");
            Dictionary<string, AtomInfo[]> interfaceChainHash = null;
            try
            {
                interfaceChainHash = crystalBuilder.BuildCrystal(xmlFile, symOpStrings);
            }
            catch (Exception ex)
            {
                throw new Exception("Building interface chains errors: " + xmlFile + ". " + ex.Message);
            }

            // for each chain in a cell including original and symmetric chains
            foreach (string chainAndSymOp in interfaceChainHash.Keys)
            {
                BVTree chainTree = new BVTree();
                chainTree.BuildBVTree(interfaceChainHash[chainAndSymOp], AppSettings.parameters.kDopsParam.bvTreeMethod, true);
                asymChainTreesHash.Add(chainAndSymOp, chainTree);
            }

            ChainContact chainContact = null;
            List<int> interfaceList = new List<int> (interfaceDefHash.Keys);
            interfaceList.Sort();
            foreach (int interfaceId in interfaceList)
            {
                string[] interfaceDefStrings = interfaceDefHash[interfaceId].ToString().Split(';');
                chainContact = new ChainContact(interfaceDefStrings[0], interfaceDefStrings[1]);
                BVTree tree1 = (BVTree)asymChainTreesHash[interfaceDefStrings[0]];
                BVTree tree2 = (BVTree)asymChainTreesHash[interfaceDefStrings[1]];
                ChainContactInfo contactInfo = chainContact.GetChainContactInfo(tree1, tree2);

                if (contactInfo == null)
                {
                    continue;
                }
                InterfaceChains interfaceChains = null;
                interfaceChains = new InterfaceChains(interfaceDefStrings[0], interfaceDefStrings[1],
                    tree1.Root.AtomList, tree2.Root.AtomList);
                interfaceChains.pdbId = pdbId;
                interfaceChains.interfaceId = interfaceId;
                interfaceChains.seqDistHash = contactInfo.GetBbDistHash();
          //      interfaceChains.seqDistHash = contactInfo.cbetaContactHash;
                interfaceChains.seqContactHash = contactInfo.GetContactsHash();
                interfaceChainsList.Add(interfaceChains);
            }
        } 

		/// <summary>
		/// symmetry operators for all interfaces in the pdb entry
		/// </summary>
		/// <param name="interfaceDefHash"></param>
		/// <returns></returns>
		private string[] GetSymOpStrings (Dictionary<int, string> interfaceDefHash)
		{
			List<string> symOpList = new List<string> ();
			foreach (int interfaceId in interfaceDefHash.Keys)
			{
				string[] symOpFields = interfaceDefHash[interfaceId].Split (';');
				if (! symOpList.Contains (symOpFields[0]))
				{
					symOpList.Add (symOpFields[0]);
				}
				if (! symOpList.Contains (symOpFields[1]))
				{
					symOpList.Add (symOpFields[1]);
				}
			}
            return symOpList.ToArray ();
		}
		#endregion

		#region translation vector fractional  to cartn
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
			cartnVectors[0] = fract2CartnMatrix.Value (0, 0) * transFractVectors[0] + 
							fract2CartnMatrix.Value (0, 1) * transFractVectors[1] +
							fract2CartnMatrix.Value (0, 2) * transFractVectors[2];
			cartnVectors[1] = fract2CartnMatrix.Value (1, 0) * transFractVectors[0] + 
				fract2CartnMatrix.Value (1, 1) * transFractVectors[1] +
				fract2CartnMatrix.Value (1, 2) * transFractVectors[2];
			cartnVectors[2] = fract2CartnMatrix.Value (2, 0) * transFractVectors[0] + 
				fract2CartnMatrix.Value (2, 1) * transFractVectors[1] +
				fract2CartnMatrix.Value (2, 2) * transFractVectors[2];
			return cartnVectors;
		}
		#endregion

        #region original unit in PDB
        /// <summary>
        ///  for those virus proteins, only get the original asymmetric unit 
        ///  to compute the interactions. 
        /// </summary>
        /// <param name="unitCellChainsHash"></param>
        /// <returns></returns>
        private Dictionary<string, AtomInfo[]> GetAsymUnitHash(Dictionary<string, AtomInfo[]> unitCellChainsHash)
        {
            Dictionary<string, AtomInfo[]> asuChainsHash = new Dictionary<string, AtomInfo[]>();
            foreach (string chainSymOpString in unitCellChainsHash.Keys)
            {
                if (IsSymOpStringOriginal(chainSymOpString))
                {
                    asuChainsHash.Add(chainSymOpString, unitCellChainsHash[chainSymOpString]);
                }
            }
            return asuChainsHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asuChainsHash"></param>
        /// <returns></returns>
        private Dictionary<string, AtomInfo[]> GetOrigAsymUnitHash(Dictionary<string, AtomInfo[]> asuChainsHash)
        {
            Dictionary<string, AtomInfo[]> origAsuChainsHash = new Dictionary<string, AtomInfo[]>();
            foreach (string chainSymOpString in asuChainsHash.Keys)
            {
                if (IsSymOpStringNonNcs(chainSymOpString))
                {
                    origAsuChainsHash.Add(chainSymOpString, asuChainsHash[chainSymOpString]);
                }
            }
            return origAsuChainsHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainSymOpString"></param>
        /// <returns></returns>
        private bool IsSymOpStringOriginal(string chainSymOpString)
        {
            if (chainSymOpString.IndexOf("1_555") > -1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainSymOpString"></param>
        /// <returns></returns>
        private bool IsSymOpStringNonNcs(string chainSymOpString)
        {
            string[] symOpFields = chainSymOpString.Split('_');
            foreach (char ch in symOpFields[0])
            {
                if (char.IsDigit(ch))
                {
                    return false;
                }
            }
            return true;
        }
        #endregion
    }

}
