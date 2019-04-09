using System;
using System.Collections.Generic;
using System.Data;
using CrystalInterfaceLib.SimMethod;
using CrystalInterfaceLib.KDops;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Settings;


namespace CrystalInterfaceLib.StructureComp
{
	/// <summary>
	/// Summary description for SimInterfaces.
	/// </summary>
	public class InterfacesComp
	{
		protected SimScoreFunc qFunc = new SimScoreFunc ();
		//private double uniqueCutoff = 0.5;

		public InterfacesComp()
		{
		}

	/*	public InterfacesComp(double uniqueCutoff)
		{
			this.uniqueCutoff = uniqueCutoff;
		}
*/
		#region Interfaces within one structure
		/// <summary>
		/// get q scores within a crystal
		/// </summary>
		/// <param name="interChains">input: all pairwise interactive chains</param>
		/// return: an array of unique pairwise interfaces and their q scores
		public InterfacePairInfo[] CompareInterfacesWithinCrystal (ref InterfaceChains[] interfaceChains)
		{
			List<InterfacePairInfo> interfacesList = new List<InterfacePairInfo> ();

			// pairwise compare two interfaces
			for (int i = 0; i < interfaceChains.Length - 1; i ++)
			{				
				for (int j = i + 1; j < interfaceChains.Length; j ++)
				{
					// get q scores for each interaction chains: Q_weight
					float qScore = (float)qFunc.WeightQFunc (interfaceChains[i], interfaceChains[j]);
					if (qScore < AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff)
					{
						interfaceChains[j].Reverse ();
						float reversedQScore = (float)qFunc.WeightQFunc (interfaceChains[i], interfaceChains[j]);
						if (reversedQScore > qScore)
						{
							qScore = reversedQScore;
						}
						else
						{
							interfaceChains[j].Reverse ();
						}
					}
					InterfacePairInfo interfacesInfo = new InterfacePairInfo ((InterfaceInfo)interfaceChains[i], 
						(InterfaceInfo)interfaceChains[j]);
					interfacesInfo.qScore = qScore;
					interfacesList.Add (interfacesInfo);
				}
			}
			// return an array of interface info
            return interfacesList.ToArray ();
		}
		#endregion

		#region Unique interfaces
		/// <summary>
		/// get q scores within a crystal
		/// </summary>
		/// <param name="interChains">input: all pairwise interactive chains
		/// output: unique pairwise interactive chains</param>
		/// return: an array of pairwise interfaces and their q scores
		public InterfacePairInfo[] FindUniqueInterfacesWithinCrystal (ref InterfaceChains[] interChains)
		{
			List<InterfacePairInfo> interfacesCompList = new List<InterfacePairInfo>  ();
			List<int> uniqueInterChainsIdList =new List<int>  ();

			// initialize the unique interchains list
			for (int i = 0; i < interChains.Length; i ++)
			{
				uniqueInterChainsIdList.Add (i + 1);
			}

			// pairwise compare two interfaces 
			for (int i = 0; i < interChains.Length - 1; i ++)
			{
				// should have similar structure to a structure
				if (! uniqueInterChainsIdList.Contains (i + 1))
				{
					continue;
				}
				for (int j = i + 1; j < interChains.Length; j ++)
				{
					// should have similar structures
					if (! uniqueInterChainsIdList.Contains (j + 1))
					{
						continue;
					}

					// get q scores for each interaction chains: Q_weight
					float qScore = (float)qFunc.WeightQFunc (interChains[i], interChains[j]);
					if (qScore < AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff)
					{
						interChains[j].Reverse ();
						float reversedQScore = (float)qFunc.WeightQFunc (interChains[i], interChains[j]);
						if (reversedQScore > qScore)
						{
							qScore = reversedQScore;
						}
						else
						{
							interChains[j].Reverse ();
						}
					}
					InterfacePairInfo interfacesInfo = new InterfacePairInfo ((InterfaceInfo)interChains[i], (InterfaceInfo)interChains[j]);
					interfacesInfo.qScore = qScore;					
					// remove same interfaces with Qscore >= cutoff and with same asymmetric chains
					if (qScore > AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff && 
						 AreAsymChainsSame (interChains[i], interChains[j]) > -1)
					{
						interfacesInfo.sameAsym = true;
						uniqueInterChainsIdList.Remove (j + 1);
					}
                    interfacesCompList.Add(interfacesInfo);
				}
			}
			// keep unique interfaces
			InterfaceChains[] uniqueInterChains = new InterfaceChains [uniqueInterChainsIdList.Count];
			for (int i = 0; i < uniqueInterChainsIdList.Count; i ++)
			{
				uniqueInterChains[i] = interChains[uniqueInterChainsIdList[i] - 1];
			}
			interChains = uniqueInterChains; 
			// return an array of interface info
            return interfacesCompList.ToArray();
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="interChains"></param>
		/// <param name="pqsAsymChainMatchHash"></param>
		/// <returns></returns>
		public InterfacePairInfo[] FindUniqueInterfacesWithinCrystal (ref InterfaceChains[] interChains, Dictionary<string, string> pqsAsymChainMatchHash)
		{
			List<InterfacePairInfo> interfacesList = new List<InterfacePairInfo> ();
			List<int> uniqueInterChainsIdList = new List<int> ();

			// initialize the unique interchains list
			for (int i = 0; i < interChains.Length; i ++)
			{
				uniqueInterChainsIdList.Add (i + 1);
			}

			// pairwise compare two interfaces
			for (int i = 0; i < interChains.Length - 1; i ++)
			{
				// should have similar structure to a structure
				if (! uniqueInterChainsIdList.Contains (i + 1))
				{
					continue;
				}
				for (int j = i + 1; j < interChains.Length; j ++)
				{
					// should have similar structures
					if (! uniqueInterChainsIdList.Contains (j + 1))
					{
						continue;
					}

					// get q scores for each interaction chains: Q_weight
					float qScore = (float)qFunc.WeightQFunc (interChains[i], interChains[j]);
					if (qScore < AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff)
					{
						interChains[j].Reverse ();
						float reversedQScore = (float)qFunc.WeightQFunc (interChains[i], interChains[j]);
						if (reversedQScore > qScore)
						{
							qScore = reversedQScore;
						}
						else
						{
							interChains[j].Reverse ();
						}
					}
					InterfacePairInfo interfacesInfo = new InterfacePairInfo ((InterfaceInfo)interChains[i], (InterfaceInfo)interChains[j]);
					interfacesInfo.qScore = qScore;					
					// remove same interfaces with Qscore >= cutoff and with same asymmetric chains
					if (qScore > AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff && 
						AreAsymChainsSame (interChains[i], interChains[j], pqsAsymChainMatchHash))
					{
						interfacesInfo.sameAsym = true;
						uniqueInterChainsIdList.Remove (j + 1);
					}
					interfacesList.Add (interfacesInfo);
				}
			}
			// keep unique interfaces
			InterfaceChains[] uniqueInterChains = new InterfaceChains [uniqueInterChainsIdList.Count];
			for (int i = 0; i < uniqueInterChainsIdList.Count; i ++)
			{
				uniqueInterChains[i] = interChains[(int)uniqueInterChainsIdList[i] - 1];
			}
			interChains = uniqueInterChains; 
			// return an array of interface info
			InterfacePairInfo[] interfacePairList = new InterfacePairInfo [interfacesList.Count];
			interfacesList.CopyTo (interfacePairList);
			return interfacePairList;
		}

		/// <summary>
		/// check if two interfaces are from same asymmetric chains
		/// For PDB interfaces
		/// </summary>
		/// <param name="interface1"></param>
		/// <param name="interface2"></param>
		/// <returns></returns>
		private int AreAsymChainsSame (InterfaceChains interface1, InterfaceChains interface2)
		{
			string asymChain11 = interface1.firstSymOpString.Substring (0, interface1.firstSymOpString.IndexOf ("_"));
			string asymChain12 = interface1.secondSymOpString.Substring (0, interface1.secondSymOpString.IndexOf ("_"));
			string asymChain21 = interface2.firstSymOpString.Substring (0, interface2.firstSymOpString.IndexOf ("_"));
			string asymChain22 = interface2.secondSymOpString.Substring (0, interface2.secondSymOpString.IndexOf ("_"));
			if (asymChain11 == asymChain21 && asymChain12 == asymChain22)
			{
				return 0; // same
			}
		    else if (asymChain11 == asymChain22 && asymChain12 == asymChain21)
     	    {
				return 1; // reversed same
			}
			return -1; // not same
		}

		/// <summary>
		/// check if two interfaces are from same asymmetric chains
		/// For PQS interfaces
		/// </summary>
		/// <param name="interface1"></param>
		/// <param name="interface2"></param>
		/// <returns></returns>
		private bool AreAsymChainsSame (InterfaceChains interface1, InterfaceChains interface2, Dictionary<string,string> pqsAsymChainMatchHash)
		{
			// get asymmetric chains
			string asymChain11 = "";
			string asymChain12 = "";
			string asymChain21 = "";
			string asymChain22 = "";
			if (pqsAsymChainMatchHash.ContainsKey (interface1.firstSymOpString))
			{
				asymChain11 = pqsAsymChainMatchHash[interface1.firstSymOpString];
			}
			else
			{
				asymChain11 = interface1.firstSymOpString;
			}
			if (pqsAsymChainMatchHash.ContainsKey (interface1.secondSymOpString))
			{
				asymChain12 = pqsAsymChainMatchHash[interface1.secondSymOpString];
			}
			else
			{
				asymChain12 = interface1.secondSymOpString;
			}

			if (pqsAsymChainMatchHash.ContainsKey (interface2.firstSymOpString))
			{
				asymChain21 = pqsAsymChainMatchHash[interface2.firstSymOpString];
			}
			else
			{
				asymChain21 = interface2.firstSymOpString;
			}
			if (pqsAsymChainMatchHash.ContainsKey (interface2.secondSymOpString))
			{
				asymChain22 = pqsAsymChainMatchHash[interface2.secondSymOpString];
			}
			else
			{
				asymChain22 = interface2.secondSymOpString;
			}


			if ((asymChain11 == asymChain21 && asymChain12 == asymChain22) || 
				(asymChain11 == asymChain22 && asymChain12 == asymChain21))
			{
				return true;
			}
			return false;
		}
		#endregion

		#region compare one interface with a list of interfaces
		/// <summary>
		/// compare one pair interactive chains with a list of interactive chains
		/// if no similar interfaces, return true
		/// otherwise return false
		/// </summary>
		/// <param name="interChains"></param>
		/// <param name="interChainsList"></param>
		/// <returns></returns>
		public bool IsInterfaceUnique (InterfaceChains interChains, List<InterfaceChains> interChainsList)
		{
			// pairwise compare two interfaces
			for (int i = 0; i < interChainsList.Count; i ++)
			{				
				// get q scores for each interaction chains: Q_weight
				float qScore = (float)qFunc.WeightQFunc (interChains, interChainsList[i]);
					
				InterfacePairInfo interfacesInfo = new InterfacePairInfo ((InterfaceInfo)interChains, (InterfaceInfo)interChainsList[i]);
				interfacesInfo.qScore = qScore;
				if (qScore >= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
				{
					return false;
				}
			}
			return true;
		}
		#endregion

		#region Compare 2 interfaces
		/// <summary>
		/// compare two interfaces
		/// if no similar interfaces, return true
		/// otherwise return false
		/// </summary>
		/// <param name="interChains"></param>
		/// <param name="interChainsList"></param>
		/// <returns></returns>
		public bool AreInterfacesSame (InterfaceChains interface1, InterfaceChains interface2)
		{
			// compare two interfaces,
			// if different asymmetric chains, then not same
			int asymChainCompResult = AreAsymChainsSame (interface1, interface2);
			if (asymChainCompResult < 0)
			{
				return false;
			}
			else if (asymChainCompResult == 1) // reversed same
			{
				interface2.Reverse ();  // match the asymmetric chains to interface1
			}
			// get q scores for the interaction chains: Q_weight				
			float qScore = (float)qFunc.WeightQFunc (interface1, interface2);
			if (qScore >= AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff)
			{
				return true;
			}
			else
			{				
				interface2.Reverse ();
				qScore = (float)qFunc.WeightQFunc (interface1, interface2);
				if (qScore >= AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff)
				{
					return true;
				}
				else
				{
					// reverse it back
					if (asymChainCompResult != 1)
					{
						interface2.Reverse ();
					}
					return false;
				}
			}
		}

		/// <summary>
		/// compare two interfaces
		/// </summary>
		/// <param name="interChains"></param>
		/// <param name="interChainsList"></param>
		/// <returns></returns>
		public bool AreChainInterfacesSame (InterfaceChains interface1, InterfaceChains interface2)
		{
			// get q scores for the interaction chains: Q_weight				
			float qScore = (float)qFunc.WeightQFunc (interface1, interface2);
			if (qScore >= AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff)
			{
				return true;
			}
			else
			{				
				interface2.Reverse ();
				qScore = (float)qFunc.WeightQFunc (interface1, interface2);
				if (qScore >= AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff)
				{
					return true;
				}
				else
				{
					// reverse it back
					interface2.Reverse ();
					return false;
				}
			}
		}
		#endregion

		#region Interfaces between 2 structures - no superposition	
		/// <summary>
		/// pairwise interfaces comparisons between two crystal structures
		/// </summary>
		/// <param name="interChains1">interactive chains and interfaces</param>
		/// <param name="interChains2">interactive chains and </param>
		public InterfacePairInfo[] CompareInterfacesBetweenCrystals (InterfaceChains[] interChains1, InterfaceChains[] interChains2)
		{
			List<InterfacePairInfo> interfacesList = new List<InterfacePairInfo> ();
			
			for (int i = 0; i < interChains1.Length; i ++)
			{
				for (int j = 0; j < interChains2.Length; j ++)
				{
					// get q scores for each interaction chains: Q_weight
					float qScore = (float)qFunc.WeightQFunc (interChains1[i], interChains2[j]);
					if (qScore < AppSettings.parameters.simInteractParam.interfaceSimCutoff)
					{
						interChains2[j].Reverse ();
						float reversedQScore = (float)qFunc.WeightQFunc (interChains1[i], interChains2[j]);
						if (qScore < reversedQScore)
						{
							qScore = reversedQScore;
						}
						else
						{
							interChains2[j].Reverse ();
						}
					}
					if (qScore >= 0.0)
					{
						InterfacePairInfo interfacesInfo = new InterfacePairInfo ((InterfaceInfo)interChains1[i], (InterfaceInfo)interChains2[j]);
						interfacesInfo.qScore = qScore;
						interfacesList.Add (interfacesInfo);
					}
				}
			}

            return interfacesList.ToArray ();
		}


		/// <summary>
		/// remove the interface pair from the interfaces list
		/// </summary>
		/// <param name="interfacePairList"></param>
		/// <param name="interfaceInfo"></param>
		private void RemoveInterfacePairInfo (ref List<InterfacePairInfo> interfacePairList, InterfacePairInfo interfaceInfo)
		{
			int removedIndex = -1;
			bool interfaceFound = false;
			foreach (InterfacePairInfo interfacePair in interfacePairList)
			{
				removedIndex ++;
				if (interfacePair.interfaceInfo1.interfaceId  == interfaceInfo.interfaceInfo1.interfaceId && 
					interfacePair.interfaceInfo2.interfaceId == interfaceInfo.interfaceInfo2.interfaceId)
				{
					interfaceFound = true;
					break;
				}	
			}
			if (interfaceFound)
			{
				interfacePairList.RemoveAt (removedIndex);
			}
		}
		#endregion

		#region Similarity 2 interfaces 
		/// <summary>
		/// Similarity between 2 interfaces
		/// </summary>
		/// <param name="interChains1">structure1</param>
		/// <param name="interChains2">structure2</param>
		public double CompareSuperimposedInterfaces (InterfaceChains interface1, InterfaceChains interface2)
		{			
			return qFunc.WeightQFunc (interface1, interface2);
		}
		#endregion
	}
}
