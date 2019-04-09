using System;
using System.Data;
using System.Collections;
using XtalLib.Contacts;
using XtalLib.KDops;
using XtalLib.Settings;
using XtalLib.Crystal;
using XtalLib.StructureComp;
using XtalLib.ProtInterfaces;
using BuInterfacesLib.BuInterfacesComp;
using BuInterfacesLib.BuMatch;

namespace BuInterfacesLib.BuInterfaces
{
	/// <summary>
	/// Summary description for InterfacesInBu.
	/// </summary>
	public class InterfacesInBu
	{
		#region member variables
		private InterfacesComp interfaceComp = new InterfacesComp ();
		#endregion

		public InterfacesInBu()
		{
		}

		#region interfaces from BU
		/// <summary>
		/// get chain contacts (interfaces) in the biological unit
		/// </summary>
		/// <param name="biolUnit">key: chainid, value: atom list</param>
		/// <returns></returns>
		public InterfaceChains[] GetInterfacesInBiolUnit (Hashtable biolUnit)
		{
			if (biolUnit == null || biolUnit.Count < 2)
			{
				return null;
			}
			// build trees for the biological unit
			Hashtable buChainTreesHash = BuildBVtreesForBiolUnit (biolUnit);

			// calculate interfaces
			ArrayList interChainsList = new ArrayList ();
			ArrayList keyList = new ArrayList (buChainTreesHash.Keys);
			keyList.Sort ();
			int interChainId = 0;
			for (int i = 0; i < keyList.Count - 1; i ++)
			{
				for (int j = i + 1; j < keyList.Count; j ++)
				{
					ChainContact chainContact = new ChainContact (keyList[i].ToString (), keyList[j].ToString ());
					ChainContactInfo contactInfo = chainContact.GetChainContactInfo ((BVTree)buChainTreesHash[keyList[i]], 
						(BVTree)buChainTreesHash[keyList[j]]);
					if (contactInfo != null)
					{
						interChainId ++;
						
						InterfaceChains interfaceChains = new InterfaceChains (keyList[i].ToString (), keyList[j].ToString ());
						// no need to change the tree node data
						// only assign the refereces
						interfaceChains.chain1 = ((BVTree)buChainTreesHash[keyList[i]]).Root.AtomList;
						interfaceChains.chain2 = ((BVTree)buChainTreesHash[keyList[j]]).Root.AtomList;
						interfaceChains.interfaceId = interChainId;
						interfaceChains.seqDistHash = contactInfo.GetDistHash ();
						interfaceChains.seqContactHash = contactInfo.GetContactsHash ();
						interChainsList.Add (interfaceChains);	
						//chainContact = null;
					}
				}
			}
			InterfaceChains[] interChainArray = new InterfaceChains[interChainsList.Count];
			interChainsList.CopyTo (interChainArray);
	//		InterfacePairInfo[] interfacePairs = base.FindUniqueInterfacesWithinCrystal (ref interChainArray);			
			return interChainArray;
		}

		/// <summary>
		/// get chain contacts (interfaces) in the biological unit
		/// </summary>
		/// <param name="biolUnit">key: chainid, value: atom list</param>
		/// <returns></returns>
		public InterfaceChains[] GetInterfacesInBiolUnit (BuChainMatch buMatch, Hashtable biolUnit, string type)
		{
			if (biolUnit == null || biolUnit.Count < 2)
			{
				return null;
			}

			// build trees for the biological unit
			Hashtable buChainTreesHash = BuildBVtreesForBiolUnit (biolUnit);

			// calculate interfaces
			ArrayList interChainsList = new ArrayList ();
			ArrayList keyList = new ArrayList (buChainTreesHash.Keys);
			// PDB and PQS should have same order of chains
			if (type == "pqs")
			{
				SortPqsChainsByAsymChains (ref keyList, buMatch);	
			}
			else // for PDB and PISA
			{
				SortChainsBySymOps (ref keyList);
			}
			int interChainId = 0;
			for (int i = 0; i < keyList.Count - 1; i ++)
			{
				for (int j = i + 1; j < keyList.Count; j ++)
				{
					ChainContact chainContact = new ChainContact (keyList[i].ToString (), keyList[j].ToString ());
					ChainContactInfo contactInfo = chainContact.GetChainContactInfo ((BVTree)buChainTreesHash[keyList[i]], 
						(BVTree)buChainTreesHash[keyList[j]]);
					if (contactInfo != null)
					{
						interChainId ++;
						
						InterfaceChains interfaceChains = new InterfaceChains (keyList[i].ToString (), keyList[j].ToString ());
						// no need to change the tree node data
						// only assign the refereces
						interfaceChains.chain1 = ((BVTree)buChainTreesHash[keyList[i]]).Root.AtomList;
						interfaceChains.chain2 = ((BVTree)buChainTreesHash[keyList[j]]).Root.AtomList;
						interfaceChains.interfaceId = interChainId;
						interfaceChains.seqDistHash = contactInfo.GetDistHash ();
						interfaceChains.seqContactHash = contactInfo.GetContactsHash ();
						interChainsList.Add (interfaceChains);	
						//chainContact = null;
					}
				}
			}
			InterfaceChains[] uniqueInterfaces = new InterfaceChains[interChainsList.Count];
			interChainsList.CopyTo (uniqueInterfaces);
			InterfacePairInfo[] interfacePairs = null;
			if (type == "pqs")
			{
				Hashtable pqsAsymChainMatch = MatchPqsAsymChains (buMatch);
				interfacePairs = interfaceComp.FindUniqueInterfacesWithinCrystal (ref uniqueInterfaces, pqsAsymChainMatch);
			}
			else
			{
				interfacePairs = interfaceComp.FindUniqueInterfacesWithinCrystal (ref uniqueInterfaces);	
			}
			AddInterfaceInfoToTables (buMatch, interfacePairs, uniqueInterfaces, type);
			return uniqueInterfaces;
		}

		private Hashtable MatchPqsAsymChains (BuChainMatch buMatch)
		{
			Hashtable pqsAsymChainMatch = new Hashtable ();
			foreach (BuChainPair chainPair in buMatch.ChainPairList)
			{
				if (! pqsAsymChainMatch.ContainsKey (chainPair.pqsChain))
				{
					pqsAsymChainMatch.Add (chainPair.pqsChain, chainPair.asymChain);
				}
			}
			return pqsAsymChainMatch;
		}

		/// <summary>
		/// order chains with symmetry operators
		/// (A, B, C, ...)_1_555, 2_555
		/// asymmetric unit chains first
		/// </summary>
		/// <param name="chainAndSymOps"></param>
		/// <returns></returns>
		private void SortChainsBySymOps (ref ArrayList chainAndSymOps)
		{
			ArrayList orderedChainSymOpList = new ArrayList ();
			Hashtable symOpChainHash = new Hashtable ();
			foreach (string chainSymOpString in chainAndSymOps)
			{
				int chainIndex = chainSymOpString.IndexOf ("_");
				string symOpString = chainSymOpString.Substring (chainIndex + 1, chainSymOpString.Length - chainIndex - 1);
				string chain = chainSymOpString.Substring (0, chainIndex);
				if (symOpChainHash.ContainsKey (symOpString))
				{
					ArrayList chainList = (ArrayList)symOpChainHash[symOpString];
					chainList.Add (chain);
				}
				else
				{
					ArrayList chainList = new ArrayList ();
					chainList.Add (chain);
					symOpChainHash.Add (symOpString, chainList);
				}
			}
			ArrayList symOpStringList = new ArrayList (symOpChainHash.Keys);
			symOpStringList.Sort ();
			foreach (string symOpString in symOpStringList)
			{
				ArrayList chainList = (ArrayList)symOpChainHash[symOpString];
				chainList.Sort ();
				foreach (string chain in chainList)
				{
					orderedChainSymOpList.Add (chain + "_" + symOpString);
				}
			}
			chainAndSymOps.Clear ();
			chainAndSymOps.AddRange (orderedChainSymOpList);
		}
		/// <summary>
		/// sort PQS chains in the order of X, Y, Z (unit operator), 
		/// then the other operators
		/// </summary>
		/// <param name="pdbChains"></param>
		/// <param name="buMatch"></param>
		private void SortPqsChainsByAsymChains (ref ArrayList pqsChains, BuChainMatch buMatch)
		{
			Hashtable symOpChainsHash = new Hashtable ();
			ArrayList noMatchedChains = new ArrayList ();
			bool inserted = false;
			foreach (string pqsChain in pqsChains)
			{
				inserted = false;
				int pqsIndex = buMatch.IsPqsChainExist (pqsChain);
				if (pqsIndex > -1)
				{
					BuChainPair chainPair = buMatch.Index (pqsIndex);
					if (symOpChainsHash.ContainsKey (chainPair.symOpNum))
					{
						ArrayList chainList = (ArrayList)symOpChainsHash[chainPair.symOpNum];
						for(int i = 0; i < chainList.Count; i ++)
						{
							if (string.Compare (((BuChainPair)chainList[i]).asymChain, chainPair.asymChain) > 0)
							{
								chainList.Insert (i, chainPair);
								inserted = true;
								break;
							}
						}
						if (! inserted)
						{
							chainList.Add (chainPair);
						}
					}
					else
					{
						ArrayList chainList = new ArrayList ();
						chainList.Add (chainPair);
						symOpChainsHash.Add (chainPair.symOpNum, chainList);
					}
				}
					// without matched PDB chain
					// make things more complicated
				else
				{
					noMatchedChains.Add (pqsChain);
				}
			}
			int[] keyArray = new int [symOpChainsHash.Count];
			symOpChainsHash.Keys.CopyTo (keyArray, 0);
			Array.Sort (keyArray);
			pqsChains.Clear ();
			foreach (int symOpNo in keyArray)
			{
				ArrayList chainList = (ArrayList)symOpChainsHash[symOpNo];
				foreach (BuChainPair chainPair in chainList)
				{
					pqsChains.Add (chainPair.pqsChain);
				}
			}
			noMatchedChains.Sort ();
			// could be skipped ????
			foreach (string leftPqsChain in noMatchedChains)
			{
				pqsChains.Add (leftPqsChain);
			}
		}

		/// <summary>
		/// build BVtrees for chains in a biological unit
		/// </summary>
		/// <param name="biolUnit"></param>
		/// <returns></returns>
		private Hashtable BuildBVtreesForBiolUnit (Hashtable biolUnitHash)
		{
			Hashtable chainTreesHash = new Hashtable ();
			// for each chain in the biological unit
			// build BVtree
			foreach (object chainAndSymOp in biolUnitHash.Keys)
			{
				BVTree chainTree = new BVTree ();
				chainTree.BuildBVTree ((AtomInfo[])biolUnitHash[chainAndSymOp], 
					AppSettings.parameters.kDopsParam.bvTreeMethod, true);
				chainTreesHash.Add (chainAndSymOp, chainTree);
			}
			return chainTreesHash;
		}
		#endregion

		#region add data to datatable
		/// <summary>
		/// add entry unique interfaces and same interfaces data into tables
		/// </summary>
		/// <param name="interfacePairs"></param>
		/// <param name="interChainArray"></param>
		/// <param name="type"></param>
		private void AddInterfaceInfoToTables (BuChainMatch buMatch, 
			InterfacePairInfo[] interfacePairs, InterfaceChains[] uniqueInterfaces, string type)
		{
			int interfaceTableNum = -1;
			int sameInterfaceTableNum = -1;
			int residueTableNum = -1;
			int contactTableNum = -1;
			string buId = "-1";

			if (SkipDataInserting (buMatch, uniqueInterfaces.Length, type))
			{
				return;
			}
			switch (type)
			{
				case "pdb":
					interfaceTableNum = BuCompTables.PdbBuInterfaces;
					sameInterfaceTableNum = BuCompTables.PdbBuSameInterfaces;
					residueTableNum = BuCompTables.PdbInterfaceResidues;
					contactTableNum = BuCompTables.PdbBuContacts;
					buId = buMatch.pdbBuId;
                    break;

				case "pqs":
					interfaceTableNum = BuCompTables.PqsBuInterfaces;
					sameInterfaceTableNum = BuCompTables.PqsBuSameInterfaces;
					residueTableNum = BuCompTables.PqsInterfaceResidues;
					contactTableNum = BuCompTables.PqsBuContacts;
					buId = buMatch.pqsBuId;
					break;

				case "pisa":
					interfaceTableNum = BuCompTables.PisaBuInterfaces;
					buId = buMatch.pqsBuId;
					break;

				default:
					break;
			}
			
			int uniqueInterfaceId = 0;
			foreach (InterfaceChains theInterface in uniqueInterfaces)
			{
				uniqueInterfaceId ++;
				theInterface.interfaceId = uniqueInterfaceId;
				// for itself
				int numOfCopies = 1;
				DataRow interfaceRow = BuCompTables.buCompTables [interfaceTableNum].NewRow ();
				interfaceRow["PdbID"] = buMatch.pdbId;
				interfaceRow["BuID"] = buId;
				interfaceRow["InterfaceID"] = uniqueInterfaceId;
				if (type == "pqs")
				{
					interfaceRow["PqsChain1"] = theInterface.firstSymOpString;
					interfaceRow["PqsChain2"] = theInterface.secondSymOpString;
					interfaceRow["AsymChain1"] = FindAsymChain (theInterface.firstSymOpString, buMatch);
					interfaceRow["AsymChain2"] = FindAsymChain (theInterface.secondSymOpString, buMatch);
				}
				else // for PDB and PISA
				{
					interfaceRow["AsymChain1"] = theInterface.firstSymOpString.Substring (0, theInterface.firstSymOpString.IndexOf ("_")); 
					interfaceRow["AsymChain2"] = theInterface.secondSymOpString.Substring (0, theInterface.secondSymOpString.IndexOf ("_"));					
				}
				interfaceRow["AuthChain1"] = FindAuthChain (interfaceRow["AsymChain1"].ToString (), buMatch);
				interfaceRow["AuthChain2"] = FindAuthChain (interfaceRow["AsymChain2"].ToString (), buMatch);
				interfaceRow["EntityID1"] = FindEntityId (interfaceRow["AsymChain1"].ToString (), buMatch);
				interfaceRow["EntityID2"] = FindEntityId (interfaceRow["AsymChain2"].ToString (), buMatch);

				if (type != "pisa")
				{
					// add data to same interfaces table
					// add the first unique interface
					DataRow sameInterfaceRow = BuCompTables.buCompTables [sameInterfaceTableNum].NewRow ();
					sameInterfaceRow["PdbID"] = buMatch.pdbId;
					sameInterfaceRow["BuID"] = buId;
					sameInterfaceRow["InterfaceID"] = uniqueInterfaceId;
					sameInterfaceRow["SameInterfaceID"] = theInterface.interfaceId;
					if (type == "pdb")
					{
						string[] chainSymOpStrings = theInterface.firstSymOpString.Split ('_');
						sameInterfaceRow["Chain1"] = chainSymOpStrings[0];
                        if (chainSymOpStrings.Length == 3)
                        {
                            sameInterfaceRow["SymmetryString1"] = chainSymOpStrings[1] + "_" + chainSymOpStrings[2];
                        }
                        else if (chainSymOpStrings.Length == 2)
                        {
                            sameInterfaceRow["SymmetryString1"] = chainSymOpStrings[1];
                        }
						chainSymOpStrings = theInterface.secondSymOpString.Split ('_');
						sameInterfaceRow["Chain2"] = chainSymOpStrings[0];
                        if (chainSymOpStrings.Length == 3)
                        {
                            sameInterfaceRow["SymmetryString2"] = chainSymOpStrings[1] + "_" + chainSymOpStrings[2];
                        }
                        else
                        {
                            sameInterfaceRow["SymmetryString2"] = chainSymOpStrings[1];
                        }
					}
					else
					{
						sameInterfaceRow["Chain1"] = theInterface.firstSymOpString;
						sameInterfaceRow["SymmetryString1"] = FindPqsSymOpString (theInterface.firstSymOpString, buMatch);
						sameInterfaceRow["Chain2"] = theInterface.secondSymOpString;
						sameInterfaceRow["SymmetryString2"] = FindPqsSymOpString (theInterface.secondSymOpString, buMatch);
					}
					sameInterfaceRow["QScore"] = 1;
					BuCompTables.buCompTables[sameInterfaceTableNum].Rows.Add (sameInterfaceRow);
				}
				// add same interfaces
				foreach (InterfacePairInfo pairInfo in interfacePairs)
				{
					if (pairInfo.qScore > AppSettings.parameters.simInteractParam.uniqueInterfaceCutoff)
					{
						if (pairInfo.interfaceInfo1 == (InterfaceInfo)theInterface && pairInfo.sameAsym)
						{
							numOfCopies ++;
							if (type != "pisa")
							{
								DataRow sameInterfaceRow = BuCompTables.buCompTables [sameInterfaceTableNum].NewRow ();
								sameInterfaceRow["PdbID"] = buMatch.pdbId;
								sameInterfaceRow["BuID"] = buId;
								sameInterfaceRow["InterfaceID"] = uniqueInterfaceId;
								sameInterfaceRow["SameInterfaceID"] = pairInfo.interfaceInfo2.interfaceId;
								if (type == "pdb")
								{
									string[] chainSymOpStrings = pairInfo.interfaceInfo2.firstSymOpString.Split ('_');
									sameInterfaceRow["Chain1"] = chainSymOpStrings[0];
                                    if (chainSymOpStrings.Length == 3)
                                    {
                                        sameInterfaceRow["SymmetryString1"] = chainSymOpStrings[1] + "_" + chainSymOpStrings[2];
                                    }
                                    else if (chainSymOpStrings.Length == 2)
                                    {
                                        sameInterfaceRow["SymmetryString1"] = chainSymOpStrings[1];
                                    }
                                    chainSymOpStrings = pairInfo.interfaceInfo2.secondSymOpString.Split('_');
									sameInterfaceRow["Chain2"] = chainSymOpStrings[0];
                                    if (chainSymOpStrings.Length == 3)
                                    {
                                        sameInterfaceRow["SymmetryString2"] = chainSymOpStrings[1] + "_" + chainSymOpStrings[2];
                                    }
                                    else if (chainSymOpStrings.Length == 2)
                                    {
                                        sameInterfaceRow["SymmetryString2"] = chainSymOpStrings[1];
                                    }
								}
								else
								{
									sameInterfaceRow["Chain1"] = pairInfo.interfaceInfo2.firstSymOpString;
									sameInterfaceRow["SymmetryString1"] = FindPqsSymOpString (pairInfo.interfaceInfo2.firstSymOpString, buMatch);
									sameInterfaceRow["Chain2"] = pairInfo.interfaceInfo2.secondSymOpString;
									sameInterfaceRow["SymmetryString2"] = FindPqsSymOpString (pairInfo.interfaceInfo2.secondSymOpString, buMatch);
								}
								sameInterfaceRow["QScore"] = pairInfo.qScore;
								BuCompTables.buCompTables[sameInterfaceTableNum].Rows.Add (sameInterfaceRow);
							}
						}
					}
				}
				interfaceRow["NumOfCopy"] = numOfCopies;
				// have to compute surface area later
				interfaceRow["SurfaceArea"] = -1.0;
				BuCompTables.buCompTables [interfaceTableNum].Rows.Add (interfaceRow);

				if (type != "pisa")
				{
					// Add residues to table
					AddResidueDataToTable (buMatch.pdbId, buId, uniqueInterfaceId, theInterface, residueTableNum);

					// add contacts to table
					AddContactDataToTable (buMatch.pdbId, buId, uniqueInterfaceId, theInterface, contactTableNum);
				}
			}
		}

		public bool SkipDataInserting (BuChainMatch buMatch, int uniqueInterfaceNum, string type)
		{
			string queryString = "";
			bool skip = false;
			switch (type)
			{
				case "pdb":
					queryString = string.Format ("Select * From PdbBuInterfaces " + 
						" Where PdbID = '{0}' AND BuID = '{1}';", buMatch.pdbId, buMatch.pdbBuId);
					DataTable pdbBuInterfaceTable = BuInterfaceDbBuilder.dbQuery.Query (queryString);
					if (pdbBuInterfaceTable.Rows.Count == uniqueInterfaceNum)
					{
						skip = true;
					}
					else
					{
						queryString = string.Format ("DELETE From PdbBuInterfaces " + 
							"Where PdbID = '{0}' AND BuID = '{1}';", buMatch.pdbId, buMatch.pdbBuId);
						BuInterfaceDbBuilder.dbQuery.Query (queryString);
						queryString = string.Format ("DELETE From PdbBuSameInterfaces " + 
							"Where PdbID = '{0}' AND BuID = '{1}';", buMatch.pdbId, buMatch.pdbBuId);
						BuInterfaceDbBuilder.dbQuery.Query (queryString);

					}
					break;

				case "pqs":
					queryString = string.Format ("Select * From PqsBuInterfaces " + 
						" Where PdbID = '{0}' AND BuID = '{1}';", buMatch.pdbId, buMatch.pqsBuId);
					DataTable pqsBuInterfaceTable = BuInterfaceDbBuilder.dbQuery.Query (queryString);
					if (pqsBuInterfaceTable.Rows.Count == uniqueInterfaceNum)
					{
						skip = true;
					}
					else
					{
						queryString = string.Format ("DELETE From PqsBuInterfaces " + 
							"Where PdbID = '{0}' AND BuID = '{1}';", buMatch.pdbId, buMatch.pqsBuId);
						BuInterfaceDbBuilder.dbQuery.Query (queryString);
						queryString = string.Format ("DELETE From PqsBuSameInterfaces " + 
							"Where PdbID = '{0}' AND BuID = '{1}';", buMatch.pdbId, buMatch.pqsBuId);
						BuInterfaceDbBuilder.dbQuery.Query (queryString);
					}
					break;

				case "pisa":
					queryString = string.Format ("Select * From PisaBuInterfaces " + 
						" Where PdbID = '{0}' AND BuID = '{1}';", buMatch.pdbId, buMatch.pisaBuId);
					DataTable pisaBuInterfaceTable = BuInterfaceDbBuilder.dbQuery.Query (queryString);
					if (pisaBuInterfaceTable.Rows.Count == uniqueInterfaceNum)
					{
						skip = true;
					}
					else
					{
						queryString = string.Format ("DELETE From PisaBuInterfaces " + 
							"Where PdbID = '{0}' AND BuID = '{1}';", buMatch.pdbId, buMatch.pisaBuId);
						BuInterfaceDbBuilder.dbQuery.Query (queryString);
					}
					break;

				default:
					break;
			}
			return skip;
		}

		/// <summary>
		/// add residues to table
		/// </summary>
		private void AddResidueDataToTable (string pdbId, string buId, int interfaceId, 
			InterfaceChains theInterface, int residueTableNum)
		{
			// a residue of one chain may has distance <= 12A 
			// with several residues in the other chain
			foreach (string seqStr in theInterface.seqDistHash.Keys )
			{
				string[] seqIds = seqStr.Split ('_');
				DataRow residueRow = BuCompTables.buCompTables [residueTableNum].NewRow ();
				residueRow["PdbID"] = pdbId;
				residueRow["BuID"] = buId;
				residueRow["InterfaceID"] = interfaceId;
				residueRow["Residue1"] = theInterface.GetAtom (seqIds[0], 1).residue;					
				residueRow["SeqID1"] = seqIds[0];
				residueRow["Residue2"] = theInterface.GetAtom (seqIds[1], 2).residue;
				residueRow["SeqID2"] = seqIds[1];	
				residueRow["Distance"] = theInterface.seqDistHash[seqStr];
				BuCompTables.buCompTables [residueTableNum].Rows.Add (residueRow);
			}
		}

		private void AddContactDataToTable (string pdbId, string buId, int interfaceId, 
			InterfaceChains theInterface, int contactTableNum)
		{
			// a residue of one chain may has distance <= 12A 
			// with several residues in the other chain
			foreach (string seqStr in theInterface.seqContactHash.Keys )
			{
				string[] seqIds = seqStr.Split ('_');
				Contact contact = (Contact)theInterface.seqContactHash[seqStr];
				DataRow residueRow = BuCompTables.buCompTables [contactTableNum].NewRow ();
				residueRow["PdbID"] = pdbId;
				residueRow["BuID"] = buId;
				residueRow["InterfaceID"] = interfaceId;
				residueRow["Residue1"] = contact.Residue1;					
				residueRow["SeqID1"] = contact.SeqID1;
				residueRow["Atom1"] = contact.Atom1;
				residueRow["Residue2"] = contact.Residue2;
				residueRow["SeqID2"] = contact.SeqID2;				
				residueRow["Atom2"] = contact.Atom2;
				residueRow["Distance"] = contact.Distance;
				BuCompTables.buCompTables [contactTableNum].Rows.Add (residueRow);
			}
		}

		private string FindAsymChain (string pqsChain, BuChainMatch buMatch)
		{
			BuChainPair[] chainPairList = buMatch.ChainPairList;
			int pqsChainIndex = buMatch.IsPqsChainExist (pqsChain);
			if (pqsChainIndex == -1)
			{
				return "-";
			}
			return chainPairList[pqsChainIndex].asymChain;
		}

		private string FindPqsSymOpString (string pqsChain, BuChainMatch buMatch)
		{
			BuChainPair[] chainPairList = buMatch.ChainPairList;
			int pqsChainIndex = buMatch.IsPqsChainExist (pqsChain);
			if (pqsChainIndex == -1)
			{
				return "-";
			}
			return chainPairList[pqsChainIndex].symOpStr;
		}

		private string FindAuthChain (string asymChain, BuChainMatch buMatch)
		{
			BuChainPair[] chainPairList = buMatch.ChainPairList;
			int[] authChainIndex = buMatch.IsPdbChainExist (asymChain);
			if (authChainIndex.Length == 0)
			{
				return "-";
			}
			return chainPairList[authChainIndex[0]].authChain;
		}

		private int FindEntityId (string asymChain, BuChainMatch buMatch)
		{
			BuChainPair[] chainPairList = buMatch.ChainPairList;
			int[] entityIndex = buMatch.IsPdbChainExist (asymChain);
			if (entityIndex.Length == 0)
			{
				return -1;
			}
			return chainPairList[entityIndex[0]].entityId;
		}
		#endregion
	}
}
