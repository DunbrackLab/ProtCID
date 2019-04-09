using System;
using System.Data;
using System.Collections;
using XtalLib.KDops;
using XtalLib.Settings;
using XtalLib.Crystal;
using XtalLib.Contacts;
using XtalLib.ProtInterfaces;
using XtalLib.StructureComp;
using BuInterfacesLib.BuMatch;
using BuInterfacesLib.BuInterfaces;

namespace BuInterfacesLib.BuInterfacesComp
{
	/// <summary>
	/// Summary description for SimBu.
	/// </summary>
	public class BuComp : InterfacesComp
	{
		#region member variables
		// the Q score cutoff for two similar interfaces
		private double simInterfaceCutoff = 0.45;
		private InterfacesInBu buInterfaces = new InterfacesInBu ();
		public InterfacePairInfo[][] compPairInfos = null;
		#endregion

		#region consturctors
		public BuComp()
		{
		}

		public BuComp ( double cutoff)
		{
			simInterfaceCutoff = cutoff;
		}
		#endregion

		#region properties

		/// <summary>
		/// the similarity cutoff of two interfaces
		/// </summary>
		public double SimInterfaceCutoff 
		{
			get
			{
				return simInterfaceCutoff;
			}
			set
			{
				simInterfaceCutoff = value;
			}
		}
		#endregion

		#region compare BUs
		/// <summary>
		/// 
		/// </summary>
		/// <param name="biolUnits"></param>
		/// <param name="buMatch"></param>
		/// <param name="interfaceNums"></param>
		/// <param name="compResults">values are initialized from the calling function, 
		/// in order to use the existing PDB/PQS comparison</param>
		/// <returns></returns>
		public void CompareEntryBiolUnits (Hashtable[] biolUnits, BuChainMatch buMatch, 
			out int[] interfaceNums, ref int[] compResults)
		{
			compPairInfos = new InterfacePairInfo[3][];
			int compResult = -1;
			interfaceNums = new int[3];
			bool pdbPqsCompared = false;

			// interfaces in a PDB biolunit
			InterfaceChains[] pdbInterfaces = buInterfaces.GetInterfacesInBiolUnit (buMatch, 
				biolUnits[(int)BuInterfaceDbBuilder.DataType.PDB], "pdb");
			// interfaces in a PISA biological unit
			InterfaceChains[] pisaInterfaces = buInterfaces.GetInterfacesInBiolUnit (buMatch, 
				biolUnits[(int)BuInterfaceDbBuilder.DataType.PISA], "pisa");
			// interfaces in a PQS biological unit
			InterfaceChains[] pqsInterfaces = buInterfaces.GetInterfacesInBiolUnit (buMatch, 
					biolUnits[(int)BuInterfaceDbBuilder.DataType.PQS], "pqs");
			
			// no interfaces from two sources, no need to compare
			if ((pdbInterfaces == null && pisaInterfaces == null) || 
				(pdbInterfaces == null && pqsInterfaces == null) ||
				(pisaInterfaces == null && pqsInterfaces == null))
			{
				compResults[(int)BuInterfaceDbBuilder.CompType.PDBPQS] = -1;
				compResults[(int)BuInterfaceDbBuilder.CompType.PISAPDB] = -1;
				compResults[(int)BuInterfaceDbBuilder.CompType.PISAPQS] = -1;
				return;
			}
			interfaceNums = GetBuInterfaceNums (buMatch);

			// PDB/PQS BUs not compared yet
			if (compResults[(int)BuInterfaceDbBuilder.CompType.PDBPQS] < 0)
			{
				// compare the PDB and PQS BUs
				InterfacePairInfo[] pdbPqsInterfacePairs = CompareTwoBuInterfaces (pdbInterfaces, pqsInterfaces, 
					interfaceNums[(int)BuInterfaceDbBuilder.DataType.PDB], 
					interfaceNums[(int)BuInterfaceDbBuilder.DataType.PQS], out compResult);
				compPairInfos[(int)BuInterfaceDbBuilder.CompType.PDBPQS] = pdbPqsInterfacePairs;
				compResults[(int)BuInterfaceDbBuilder.CompType.PDBPQS] = compResult;
			}
			else
			{
				// if PDB and PQS BUs are already compared, set the value from compResults to be -1
				compResults[(int)BuInterfaceDbBuilder.CompType.PDBPQS] = -1;
				pdbPqsCompared = true;
			}
			// compare the PISA and PDB BUs
			InterfacePairInfo[] pisaPdbInterfacePairs = CompareTwoBuInterfaces (pisaInterfaces, pdbInterfaces, 
				interfaceNums[(int)BuInterfaceDbBuilder.DataType.PISA], 
				interfaceNums[(int)BuInterfaceDbBuilder.DataType.PDB], out compResult);
			compPairInfos[(int)BuInterfaceDbBuilder.CompType.PISAPDB] = pisaPdbInterfacePairs;
			compResults[(int)BuInterfaceDbBuilder.CompType.PISAPDB] = compResult;

			// if the PQS BU is not identical to the PDB BU 
			// compare PISA BU to the PQS BU
			if (! (pdbPqsCompared && 
				interfaceNums[(int)BuInterfaceDbBuilder.DataType.PDB] == 
				interfaceNums[(int)BuInterfaceDbBuilder.DataType.PQS])) 
			{
				InterfacePairInfo[] pisaPqsInterfacePairs = CompareTwoBuInterfaces (pisaInterfaces, pqsInterfaces, 
					interfaceNums[(int)BuInterfaceDbBuilder.DataType.PISA], 
					interfaceNums[(int)BuInterfaceDbBuilder.DataType.PQS], out compResult);
				compPairInfos[(int)BuInterfaceDbBuilder.CompType.PISAPQS] = pisaPqsInterfacePairs;
				compResults[(int)BuInterfaceDbBuilder.CompType.PISAPQS] = compResult;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="interfaces1"></param>
		/// <param name="interfaces2"></param>
		/// <param name="interfaceNum1"></param>
		/// <param name="interfaceNum2"></param>
		/// <returns>compResult: 0(same = identical or substruct), 1(dif), -1(no comparing)</returns>
		public InterfacePairInfo[] CompareTwoBuInterfaces (InterfaceChains[] interfaces1, InterfaceChains[] interfaces2, 
			int interfaceNum1, int interfaceNum2, out int compResult)
		{
			compResult = -1;
			if (interfaces1 == null || interfaces2 == null)
			{
				return null;
			}
			// compute Q scores between all unique interfaces pairs
			InterfacePairInfo[] interfacePairs = base.CompareInterfacesBetweenCrystals (interfaces1, interfaces2); 

			ArrayList simInterfaceList1 = new ArrayList ();
			ArrayList simInterfaceList2 = new ArrayList ();

			foreach (InterfacePairInfo pairInfo in interfacePairs)
			{
				if (pairInfo.qScore > AppSettings.parameters.simInteractParam.interfaceSimCutoff)
				{
					if (! simInterfaceList1.Contains (pairInfo.interfaceInfo1.interfaceId))
					{
						simInterfaceList1.Add (pairInfo.interfaceInfo1.interfaceId);
					}
					if (! simInterfaceList2.Contains (pairInfo.interfaceInfo2.interfaceId))
					{
						simInterfaceList2.Add (pairInfo.interfaceInfo2.interfaceId);
					}
				}
			}
			if (interfaceNum1 == interfaceNum2) 
			{
				// identical BUs
				if (simInterfaceList1.Count == interfaces1.Length &&
					simInterfaceList2.Count == interfaces2.Length)
				{
					compResult = 0;
				}
				else
				{
					compResult = 1;
				}
			}
			else 
			{
				// substructure, one contains the other one 
				if (Math.Min (simInterfaceList1.Count, simInterfaceList2.Count) == 
					Math.Min (interfaces1.Length, interfaces2.Length))
				{
					compResult = 0;
				}
				else
				{
					compResult = 1;
				}
			}	
			return interfacePairs;
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="buCompType"></param>
		/// <param name="buInterfaceTable1"></param>
		/// <param name="buInterfaceTable2"></param>
		private int[] GetBuInterfaceNums (BiolUnitMatch buMatch)
		{
			int[] interfaceNums = new int [3];
			int pdbInterfaceNum = 0;
			int pqsInterfaceNum = 0;
			int pisaInterfaceNum = 0;
			string queryString = "";
			// the number of copies in the BU
			DataRow[] dRows = BuCompTables.buCompTables[BuCompTables.PdbBuInterfaces].Select 
				(string.Format ("PdbID = '{0}' AND BuID = '{1}'", buMatch.pdbId, buMatch.pdbBuId));
			if (dRows.Length == 0)
			{
				queryString = string.Format ("Select * From PdbBuInterfaces " + 
					" Where PdbID = '{0}' AND BuID = '{1}';", buMatch.pdbId, buMatch.pdbBuId);
				DataTable pdbBuInterfaceTable = BuInterfaceDbBuilder.dbQuery.Query (queryString);
				dRows = pdbBuInterfaceTable.Select ();
			}
			foreach (DataRow interfaceNumRow in dRows)
			{
				pdbInterfaceNum += Convert.ToInt32 (interfaceNumRow["NumOfCopy"].ToString ());
			}

			dRows = BuCompTables.buCompTables[BuCompTables.PqsBuInterfaces].Select 
				(string.Format ("PdbID = '{0}' AND BuID = '{1}'", buMatch.pdbId, buMatch.pqsBuId));
			if (dRows.Length == 0)
			{
				queryString = string.Format ("Select * From PqsBuInterfaces " + 
					" Where PdbID = '{0}' AND BuID = '{1}';", buMatch.pdbId, buMatch.pqsBuId);
				DataTable pqsBuInterfaceTable = BuInterfaceDbBuilder.dbQuery.Query (queryString);
				dRows = pqsBuInterfaceTable.Select ();
			}
			foreach (DataRow interfaceNumRow in dRows)
			{
				pqsInterfaceNum += Convert.ToInt32 (interfaceNumRow["NumOfCopy"].ToString ());
			}

			dRows = BuCompTables.buCompTables[BuCompTables.PisaBuInterfaces].Select 
				(string.Format ("PdbID = '{0}' AND BuID = '{1}'", buMatch.pdbId, buMatch.pisaBuId));
			if (dRows.Length == 0)
			{
				queryString = string.Format ("Select * From PisaBuInterfaces " + 
					" Where PdbID = '{0}' AND BuID = '{1}';", buMatch.pdbId, buMatch.pisaBuId);
				DataTable pisaBuInterfaceTable = BuInterfaceDbBuilder.dbQuery.Query (queryString);
				dRows = pisaBuInterfaceTable.Select ();
			}
			foreach (DataRow interfaceNumRow in dRows)
			{
				pisaInterfaceNum += Convert.ToInt32 (interfaceNumRow["NumOfCopy"].ToString ());
			}
			interfaceNums[(int)BuInterfaceDbBuilder.DataType.PDB] = pdbInterfaceNum;
			interfaceNums[(int)BuInterfaceDbBuilder.DataType.PQS] = pqsInterfaceNum;
			interfaceNums[(int)BuInterfaceDbBuilder.DataType.PISA] = pisaInterfaceNum;
			return interfaceNums;
		}
		#endregion

		#region unique interfaces for PQS
		/// get q scores within a crystal
		/// </summary>
		/// <param name="interChains">input: all pairwise interactive chains
		/// output: unique pairwise interactive chains</param>
		/// return: an array of pairwise interfaces and their q scores
		public InterfacePairInfo[] FindUniqueInterfacesWithinCrystal (ref InterfaceChains[] interChains, BuChainMatch buMatch)
		{
			ArrayList interfacesList = new ArrayList ();
			ArrayList uniqueInterChainsIdList = new ArrayList ();

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
					// remove same interfaces
					if (qScore > AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff && 
						AreAsymChainsSame (interChains[i], interChains[j], buMatch))
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
		/// For PQS interfaces
		/// </summary>
		/// <param name="interface1"></param>
		/// <param name="interface2"></param>
		/// <returns></returns>
		private bool AreAsymChainsSame (InterfaceChains interface1, InterfaceChains interface2, BuChainMatch buMatch)
		{
			// get asymmetric chains
			string asymChain11 = "";
			string asymChain12 = "";
			string asymChain21 = "";
			string asymChain22 = "";
			int pqsIndex = buMatch.IsPqsChainExist (interface1.firstSymOpString);
			if (pqsIndex > -1)
			{
				asymChain11 = buMatch.Index (pqsIndex).asymChain;
			}
			else
			{
				asymChain11 = interface1.firstSymOpString;
			}
			pqsIndex = buMatch.IsPqsChainExist (interface1.secondSymOpString);
			if (pqsIndex > -1)
			{
				asymChain12 = buMatch.Index (pqsIndex).asymChain;
			}
			else
			{
				asymChain12 = interface1.secondSymOpString;
			}

			pqsIndex = buMatch.IsPqsChainExist (interface2.firstSymOpString);
			if (pqsIndex > -1)
			{
				asymChain21 = buMatch.Index (pqsIndex).asymChain;
			}
			else
			{
				asymChain21 = interface2.firstSymOpString;
			}
			pqsIndex = buMatch.IsPqsChainExist (interface2.secondSymOpString);
			if (pqsIndex > -1)
			{
				asymChain22 = buMatch.Index (pqsIndex).asymChain;
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
	}
}
