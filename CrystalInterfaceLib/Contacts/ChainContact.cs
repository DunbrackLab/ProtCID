using System;
using System.Collections.Generic;
using CrystalInterfaceLib.KDops;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.Settings;

namespace CrystalInterfaceLib.Contacts
{
	/// <summary>
	/// Summary description for ChainContacts.
	/// </summary>
	public class ChainContact
	{
		#region member variables
		
		// default cutoff is 5 angstrom
		private double contactCutoff = 5.0;
		// threshold for the number of contacts
		// of a possible protein interaction
		private int contactNumber = 1;
		
		// a sequence id for each interactive chains
		public int interChainsId = 0;
		private ChainContactInfo chainContactInfo = new ChainContactInfo ();

		#endregion

		#region constructors
		public ChainContact()
		{
		}

		public ChainContact(double cutoff, int contactNum)
		{
			contactCutoff = cutoff;
			contactNumber = contactNum;
		}

		public ChainContact (string chainSymOpA, string chainSymOpB)
		{
			if (chainSymOpA.IndexOf ("_") > -1)
			{
				chainContactInfo.firstChain = chainSymOpA.Substring (0, chainSymOpA.IndexOf ("_"));
				chainContactInfo.firstSymOpString = chainSymOpA.Substring (chainSymOpA.IndexOf ("_") + 1, 
					chainSymOpA.Length - chainSymOpA.IndexOf ("_") - 1);
			}
			else
			{
				chainContactInfo.firstChain = chainSymOpA;
			}
			if (chainSymOpB.IndexOf ("_") > -1)
			{
				chainContactInfo.secondChain = chainSymOpB.Substring (0, chainSymOpB.IndexOf ("_"));
				chainContactInfo.secondSymOpString = chainSymOpB.Substring (chainSymOpB.IndexOf ("_") + 1, 
					chainSymOpB.Length - chainSymOpB.IndexOf ("_") - 1);
			}
			else
			{
				chainContactInfo.secondChain = chainSymOpB;
			}
		}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainSymOpA"></param>
        /// <param name="chainSymOpB"></param>
        /// <param name="cutoff"></param>
		public ChainContact (string chainSymOpA, string chainSymOpB, double cutoff)
		{
			if (chainSymOpA.IndexOf ("_") > -1)
			{
				chainContactInfo.firstChain = chainSymOpA.Substring (0, chainSymOpA.IndexOf ("_"));
				chainContactInfo.firstSymOpString = chainSymOpA.Substring (chainSymOpA.IndexOf ("_") + 1, 
					chainSymOpA.Length - chainSymOpA.IndexOf ("_") - 1);
			}
			else
			{
				chainContactInfo.firstChain = chainSymOpA;
			}
			if (chainSymOpB.IndexOf ("_") > -1)
			{
				chainContactInfo.secondChain = chainSymOpB.Substring (0, chainSymOpB.IndexOf ("_"));
				chainContactInfo.secondSymOpString = chainSymOpB.Substring (chainSymOpB.IndexOf ("_") + 1, 
					chainSymOpB.Length - chainSymOpB.IndexOf ("_") - 1);
			}
			else
			{
				chainContactInfo.secondChain = chainSymOpB;
			}
			contactCutoff = cutoff;
		}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainSymOpA"></param>
        /// <param name="chainSymOpB"></param>
        /// <param name="cutoff"></param>
        /// <param name="contactNum"></param>
		public ChainContact (string chainSymOpA, string chainSymOpB, double cutoff, int contactNum)
		{
			if (chainSymOpA.IndexOf ("_") > -1)
			{
				chainContactInfo.firstChain = chainSymOpA.Substring (0, chainSymOpA.IndexOf ("_"));
				chainContactInfo.firstSymOpString = chainSymOpA.Substring (chainSymOpA.IndexOf ("_") + 1, 
					chainSymOpA.Length - chainSymOpA.IndexOf ("_") - 1);
			}
			else
			{
				chainContactInfo.firstChain = chainSymOpA;
			}
			if (chainSymOpB.IndexOf ("_") > -1)
			{
				chainContactInfo.secondChain = chainSymOpB.Substring (0, chainSymOpB.IndexOf ("_"));
				chainContactInfo.secondSymOpString = chainSymOpB.Substring (chainSymOpB.IndexOf ("_") + 1, 
					chainSymOpB.Length - chainSymOpB.IndexOf ("_") - 1);
			}
			else
			{
				chainContactInfo.secondChain = chainSymOpB;
			}
			contactCutoff = cutoff;
			contactNumber = contactNum;
		}
		#endregion

		#region properties
		/// <summary>
		/// sequence id for interactive chains
		/// </summary>
		public int InterChainsId
		{
			get
			{
				return interChainsId;
			}
			set
			{
				interChainsId = value;
			}
		}

		/// <summary>
		/// cutoff for an interatomic contact
		/// </summary>
		public double ContactCutoff 
		{
			get
			{
				return contactCutoff;
			}
			set
			{
				contactCutoff = value;
			}
		}

		/// <summary>
		/// number of contacts for an interactive chains
		/// </summary>
		public int ContactNumber
		{
			get
			{
				return contactNumber;
			}
			set
			{
				contactNumber = value;
			}
		}

		/// <summary>
		/// read only property for contact atom pairs
		/// </summary>
		public ChainContactInfo ChainContactInfo
		{
			get
			{
				return chainContactInfo;
			}
		}
		#endregion	

		#region intersection -- BV Tree
		/*
		 * Alogithm Intersect(treeA, treeB)
		 * Input: 2 BVTrees
		 * if (root of treeA intersect root of treeB) then
		 *		if (root of treeA is a leaf) then
		 *			if (root of treeB is a leaf) then
		 *				foreach atom in root A
		 *					foreach atom in root B
		 *						check distance for each pair of atoms
		 *			else
		 *				foreach child of root A
		 *					Intersect(treeA, treeB.LeftBranch);
		 *					Intersect(treeA, treeB.RightBranch);
		 *		else
		 *			foreach child of root B
		 *				Intersect (treeA.LeftBranch, treeB)
		 *				Intersect (treeA.RightBranch, treeB)		
		 * */
		#region public interfaces
		/// <summary>
		/// get the interface between two input chains
		/// </summary>
		/// <param name="chain1"></param>
		/// <param name="chain2"></param>
		/// <returns></returns>
		public ChainContactInfo GetChainContactInfo (AtomInfo[] chain1, AtomInfo[] chain2)
		{
			BVTree chain1Tree = new BVTree ();
			chain1Tree.BuildBVTree (chain1, AppSettings.parameters.kDopsParam.bvTreeMethod, true);
			BVTree chain2Tree = new BVTree ();
			chain2Tree.BuildBVTree (chain2, AppSettings.parameters.kDopsParam.bvTreeMethod, true);
			return GetChainContactInfo (chain1Tree, chain2Tree);
		}

        /// <summary>
        /// get the interface between two input chains
        /// </summary>
        /// <param name="chain1"></param>
        /// <param name="chain2"></param>
        /// <returns></returns>
        public ChainContactInfo GetDomainContactInfo(AtomInfo[] chain1, AtomInfo[] chain2)
        {
            BVTree chain1Tree = new BVTree();
            chain1Tree.BuildBVTree(chain1, AppSettings.parameters.kDopsParam.bvTreeMethod, false);
            BVTree chain2Tree = new BVTree();
            chain2Tree.BuildBVTree(chain2, AppSettings.parameters.kDopsParam.bvTreeMethod, false);
            return GetDomainContactInfo(chain1Tree, chain2Tree);
        }


        /// <summary>
        /// get the interface between two input chains
        /// </summary>
        /// <param name="chain1"></param>
        /// <param name="chain2"></param>
        /// <returns></returns>
        public ChainContactInfo GetAllChainContactInfo(AtomInfo[] chain1, AtomInfo[] chain2)
        {
            BVTree chain1Tree = new BVTree();
            chain1Tree.BuildBVTree(chain1, AppSettings.parameters.kDopsParam.bvTreeMethod, true);
            BVTree chain2Tree = new BVTree();
            chain2Tree.BuildBVTree(chain2, AppSettings.parameters.kDopsParam.bvTreeMethod, true);
            return GetAnyChainContactInfo(chain1Tree, chain2Tree);
        }

		/// <summary>
		/// get interatomic contacts
		/// </summary>
		/// <param name="treeA"></param>
		/// <param name="treeB"></param>
		/// <returns></returns>
		public ChainContactInfo GetChainContactInfo(BVTree treeA, BVTree treeB)
		{
            chainContactInfo = new ChainContactInfo();
			if (BVTree.treeType == '1')
			{
				IntersectResidues (treeA, treeB);		
				if (chainContactInfo.atomContactHash.Count < AppSettings.parameters.contactParams.numOfAtomContacts || 
					chainContactInfo.cbetaContactHash.Count < AppSettings.parameters.contactParams.numOfResidueContacts)
				{
					chainContactInfo = null;
					return null;
				}
			}
			else
			{
				Intersect (treeA, treeB);
				if (chainContactInfo.atomContactHash.Count < AppSettings.parameters.contactParams.numOfResidueContacts)
				{
					chainContactInfo = null;
					return null;
				}
			}		
			return chainContactInfo;
		}

        /// <summary>
        /// get interatomic contacts
        /// </summary>
        /// <param name="treeA"></param>
        /// <param name="treeB"></param>
        /// <returns></returns>
        public ChainContactInfo GetDomainContactInfo(BVTree treeA, BVTree treeB)
        {
            chainContactInfo = new ChainContactInfo();
            Intersect(treeA, treeB);
            if (chainContactInfo.atomContactHash.Count < AppSettings.parameters.contactParams.domainNumOfAtomContacts)
            {
                chainContactInfo = null;
                return null;
            }
            return chainContactInfo;
        }

        /// <summary>
        /// get interatomic contacts
        /// </summary>
        /// <param name="treeA"></param>
        /// <param name="treeB"></param>
        /// <returns></returns>
        public ChainContactInfo GetAnyChainContactInfo(BVTree treeA, BVTree treeB)
        {
            chainContactInfo = new ChainContactInfo();
            if (BVTree.treeType == '1')
            {
                IntersectResidues(treeA, treeB);
            /*    if (chainContactInfo.atomContactHash.Count == 0 || chainContactInfo.cbetaContactHash.Count == 0)
                {
                    chainContactInfo = null;
                    return null;
                }*/
            }
            else
            {
                Intersect(treeA, treeB);
            /*    if (chainContactInfo.atomContactHash.Count == 0)
                {
                    chainContactInfo = null;
                    return null;
                }*/
            }
            return chainContactInfo;
        }

		/// <summary>
		/// get interatomic contacts
		/// </summary>
		/// <param name="treeA"></param>
		/// <param name="treeB"></param>
		/// <returns></returns>
		public ChainContactInfo GetChainContactInfo(BVTree treeA, BVTree treeB, double [] translateVectorA)
		{
            chainContactInfo = new ChainContactInfo();
			if (BVTree.treeType == '1')
			{
				IntersectResidues (treeA, treeB, translateVectorA);		
				if (chainContactInfo.atomContactHash.Count < AppSettings.parameters.contactParams.numOfAtomContacts || 
					chainContactInfo.cbetaContactHash.Count < AppSettings.parameters.contactParams.numOfResidueContacts)
				{
					chainContactInfo = null;
					return null;
				}
			}
			else
			{
				Intersect (treeA, treeB, translateVectorA);
				if (chainContactInfo.atomContactHash.Count < AppSettings.parameters.contactParams.numOfResidueContacts)
				{
					chainContactInfo = null;
					return null;
				}
			}		
			return chainContactInfo;
		}
		/// <summary>
		/// read only
		/// if two input chains overlap or not
		/// </summary>
		public bool IsChainInteract (BVTree treeA, BVTree treeB)
		{
            chainContactInfo = new ChainContactInfo();
			if (BVTree.treeType == '1')
			{
				IntersectResidues (treeA, treeB);
				if (chainContactInfo.atomContactHash.Count < AppSettings.parameters.contactParams.numOfAtomContacts || 
					chainContactInfo.cbetaContactHash.Count < AppSettings.parameters.contactParams.numOfResidueContacts)
				{
					chainContactInfo = null;
					return false;
				}
			}
			else
			{
				Intersect (treeA, treeB);
				if (chainContactInfo.atomContactHash.Count < AppSettings.parameters.contactParams.numOfResidueContacts)
				{
					chainContactInfo = null;
					return false;
				}
			}
			return true;
		}
		#endregion

		#region atom intersection of two trees
		/// <summary>
		/// Contacts between two chains
		/// update the treeB when it is necessary
		/// </summary>
		/// <param name="treeA">center tree</param>
		/// <param name="treeB">the tree need to be translated by translateVectorA</param>
		/// <param name="translateVectorA"></param>
		public void Intersect (BVTree treeA, BVTree treeB, double[] translateVectorA)
		{
            if (treeA == null || treeB == null)
            {
                return;
            }
			BoundingBox bb = treeB.Root.BoundBox.UpdateBoundingBox (translateVectorA);
			if (! treeA.Root.BoundBox.MayOverlap (bb, AppSettings.parameters.contactParams.cutoffAtomDist))
			{
				return ;
			}
			// if it is a leaf node of tree A
			if (treeA.LeftBranch == null && treeA.RightBranch == null)
			{
				// it is a leaf node of tree B
				if (treeB.LeftBranch == null && treeB.RightBranch == null)
				{
					Node leafNode = new Node (treeB.Root);
					leafNode.UpdateLeafNode (translateVectorA);
					foreach (AtomInfo atomA in treeA.Root.AtomList)
					{
						foreach (AtomInfo atomB in leafNode.AtomList)
						{
							float atomDist = (float)(atomA - atomB);
							// if distance is less than the threshold
							// possible atomic contact
                            if (atomDist < AppSettings.parameters.contactParams.cutoffAtomDist)
							{
								AtomPair atomPair = new AtomPair ();
								atomPair.firstAtom = atomA;
								atomPair.secondAtom = atomB;
								atomPair.distance = atomDist;
								string seqIdString = atomA.seqId + "_" + atomB.seqId;
								if (! chainContactInfo.atomContactHash.ContainsKey (seqIdString))
								{
									chainContactInfo.atomContactHash.Add (seqIdString, atomPair);
								}
								else
								{
									// give priority to Cbeta
									if (atomPair.firstAtom.atomName == "CB" && atomPair.secondAtom.atomName == "CB")
									{										
										chainContactInfo.atomContactHash[seqIdString] = atomPair;
									}
								}
							}
						}
					}
				}
				else
				{
					Intersect (treeA, treeB.LeftBranch, translateVectorA);
					Intersect (treeA, treeB.RightBranch, translateVectorA);
				}
			}
			else
			{
				Intersect (treeA.LeftBranch, treeB, translateVectorA);
				Intersect (treeA.RightBranch, treeB, translateVectorA);
			}
		}

		/// <summary>
		/// return the overlapped leaf nodes
		/// </summary>
		/// <param name="extTree"></param>
		/// <returns></returns>
		public void Intersect(BVTree treeA, BVTree treeB)
		{
            if (treeA == null || treeB == null)
            {
                return;
            }
			if (! treeA.Root.BoundBox.MayOverlap 
				(treeB.Root.BoundBox, AppSettings.parameters.contactParams.cutoffAtomDist))
			{
				return ;
			}
			// if it is a leaf node of tree A
			if (treeA.Root.AtomList.Length <= BVTree.leafAtomNumber)
			{
				// it is a leaf node of tree B
				if (treeB.Root.AtomList.Length <= BVTree.leafAtomNumber)
				{
					foreach (AtomInfo atomA in treeA.Root.AtomList)
					{
						foreach (AtomInfo atomB in treeB.Root.AtomList)
						{
							float atomDist = (float)(atomA - atomB);
							// if distance is less than the threshold
							// possible atomic contact
                            if (atomDist < AppSettings.parameters.contactParams.cutoffAtomDist)
							{
								AtomPair atomPair = new AtomPair ();
								atomPair.firstAtom = atomA;
								atomPair.secondAtom = atomB;
								atomPair.distance = atomDist;
								string seqIdString = atomA.seqId + "_" + atomB.seqId;
								if (! chainContactInfo.atomContactHash.ContainsKey (seqIdString))
								{
									chainContactInfo.atomContactHash.Add (seqIdString, atomPair);
								}
								else
								{
									// give priority to Cbeta then Calpha
									if (atomPair.firstAtom.atomName == "CB" && atomPair.secondAtom.atomName == "CB")
									{										
										chainContactInfo.atomContactHash[seqIdString] = atomPair;
									}
								}
							}
						}
					}
				}
				else
				{
					Intersect (treeA, treeB.LeftBranch);
					Intersect (treeA, treeB.RightBranch);
				}
			}
			else
			{
				Intersect (treeA.LeftBranch, treeB);
				Intersect (treeA.RightBranch, treeB);
			}
		}
		#endregion

		#region residue intersection of trees
		/// <summary>
		/// Contacts between two chains
		/// update the treeB when it is necessary
		/// </summary>
		/// <param name="treeA">center tree</param>
		/// <param name="treeB">the tree need to be translated by translateVectorA</param>
		/// <param name="translateVectorA"></param>
		private void IntersectResidues (BVTree treeA, BVTree treeB, double[] translateVectorA)
		{
			BoundingBox bb = treeB.Root.BoundBox.UpdateBoundingBox (translateVectorA);
			if (! treeA.Root.BoundBox.MayOverlap (bb, AppSettings.parameters.contactParams.cutoffResidueDist))
			{
				return ;
			}
			// if it is a leaf node of tree A
			if (treeA.LeftBranch == null && treeA.RightBranch == null)
			{
				// it is a leaf node of tree B
				if (treeB.LeftBranch == null && treeB.RightBranch == null)
				{
					Node leafNode = new Node (treeB.Root);
					leafNode.UpdateLeafNode (translateVectorA);
					foreach (AtomInfo atomA in treeA.Root.AtomList)
					{
						foreach (AtomInfo atomB in leafNode.AtomList)
						{
							float atomDist = (float)(atomA - atomB);
							if ((atomA.atomName == "CB" || atomA.atomName == "CA") &&
								(atomB.atomName == "CB" || atomB.atomName == "CA") &&
								atomDist < AppSettings.parameters.contactParams.cutoffResidueDist )
							{
								AtomPair atomPair = new AtomPair ();
								atomPair.firstAtom = atomA;
								atomPair.secondAtom = atomB;
								atomPair.distance = atomDist;
								string seqIdString = atomA.seqId + "_" + atomB.seqId;
								if (! chainContactInfo.cbetaContactHash.ContainsKey (seqIdString))
								{
									chainContactInfo.cbetaContactHash.Add (seqIdString, atomPair);
								}
								else
								{
									// minimum distance between bb atoms
									double dist = ((AtomPair)chainContactInfo.cbetaContactHash[seqIdString]).distance;
									if (dist > atomDist)
									{
										chainContactInfo.cbetaContactHash[seqIdString] = atomPair;
									}
									// give priority to Cbeta then Calpha
						/*			if (atomPair.firstAtom.atomName == "CB" && atomPair.secondAtom.atomName == "CB")
									{										
										chainContactInfo.cbetaContactHash[seqIdString] = atomPair;
									}*/
								}
							}
							// if distance is less than the threshold
							// atomic contact
							if (atomDist < AppSettings.parameters.contactParams.cutoffAtomDist)
							{								
								string seqIdString = atomA.seqId + "_" + atomB.seqId;
								if (! chainContactInfo.atomContactHash.ContainsKey (seqIdString))
								{
									AtomPair atomPair = new AtomPair ();
									atomPair.firstAtom = atomA;
									atomPair.secondAtom = atomB;
									atomPair.distance = atomDist;
									chainContactInfo.atomContactHash.Add (seqIdString, atomPair);
								}
								else
								{
									// find the atom pair with minimum distance
									AtomPair atomPair = (AtomPair)chainContactInfo.atomContactHash[seqIdString];
									if (atomDist < atomPair.distance)
									{
										atomPair.firstAtom = atomA;
										atomPair.secondAtom = atomB;
										atomPair.distance = atomDist;
										chainContactInfo.atomContactHash[seqIdString] = atomPair;
									}
								}
							}
						}
					}
				}
				else
				{
					IntersectResidues (treeA, treeB.LeftBranch, translateVectorA);
					IntersectResidues (treeA, treeB.RightBranch, translateVectorA);
				}
			}
			else
			{
				IntersectResidues (treeA.LeftBranch, treeB, translateVectorA);
				IntersectResidues (treeA.RightBranch, treeB, translateVectorA);
			}
		}

		/// <summary>
		/// Contacts between two chains
		/// update the treeB when it is necessary
		/// </summary>
		/// <param name="treeA">center tree</param>
		/// <param name="treeB">the tree need to be translated by translateVectorA</param>
		/// <param name="translateVectorA"></param>
		private void IntersectResidues (BVTree treeA, BVTree treeB)
		{
			if (! treeA.Root.BoundBox.MayOverlap 
				(treeB.Root.BoundBox, AppSettings.parameters.contactParams.cutoffResidueDist))
			{
				return ;
			}
			// if it is a leaf node of tree A
			if (treeA.LeftBranch == null && treeA.RightBranch == null)
			{
				// it is a leaf node of tree B
				if (treeB.LeftBranch == null && treeB.RightBranch == null)
				{
					foreach (AtomInfo atomA in treeA.Root.AtomList)
					{
						foreach (AtomInfo atomB in treeB.Root.AtomList)
						{
							float atomDist = (float)(atomA - atomB);
							if ((atomA.atomName == "CB" || atomA.atomName == "CA") &&
								(atomB.atomName == "CB" || atomB.atomName == "CA") &&
								atomDist < AppSettings.parameters.contactParams.cutoffResidueDist )
							{
								AtomPair atomPair = new AtomPair ();
								atomPair.firstAtom = atomA;
								atomPair.secondAtom = atomB;
								atomPair.distance = atomDist;
								string seqIdString = atomA.seqId + "_" + atomB.seqId;
								if (! chainContactInfo.cbetaContactHash.ContainsKey (seqIdString))
								{
									chainContactInfo.cbetaContactHash.Add (seqIdString, atomPair);
								}
								else
								{
									// give priority to Cbeta then Calpha
					/*				if (atomPair.firstAtom.atomName == "CB" && atomPair.secondAtom.atomName == "CB")
									{										
										chainContactInfo.cbetaContactHash[seqIdString] = atomPair;
									}*/
									// minimum distance between bb atoms
									double dist = ((AtomPair)chainContactInfo.cbetaContactHash[seqIdString]).distance;
									if (dist > atomDist)
									{
										chainContactInfo.cbetaContactHash[seqIdString] = atomPair;
									}
								}
							}
							// if distance is less than the threshold
							// atomic contact
							if (atomDist < AppSettings.parameters.contactParams.cutoffAtomDist)
							{								
								string seqIdString = atomA.seqId + "_" + atomB.seqId;
								if (! chainContactInfo.atomContactHash.ContainsKey (seqIdString))
								{
									AtomPair atomPair = new AtomPair ();
									atomPair.firstAtom = atomA;
									atomPair.secondAtom = atomB;
									atomPair.distance = atomDist;
									chainContactInfo.atomContactHash.Add (seqIdString, atomPair);
								}
								else
								{
									// find the atom pair with minimum distance
									AtomPair atomPair = (AtomPair)chainContactInfo.atomContactHash[seqIdString];
									if (atomDist < atomPair.distance)
									{
										atomPair.firstAtom = atomA;
										atomPair.secondAtom = atomB;
										atomPair.distance = atomDist;
										chainContactInfo.atomContactHash[seqIdString] = atomPair;
									}
								}
							}
						}
					}
				}
				else
				{
					IntersectResidues (treeA, treeB.LeftBranch);
					IntersectResidues (treeA, treeB.RightBranch);
				}
			}
			else
			{
				IntersectResidues (treeA.LeftBranch, treeB);
				IntersectResidues (treeA.RightBranch, treeB);
			}
		}
		#endregion
		#endregion

		#region residue based 
		/// <summary>
		/// get contacts between two chains
		/// </summary>
		/// <param name="chain1"></param>
		/// <param name="chain2"></param>
		/// <returns></returns>
		public void ComputeChainsContact (AtomInfo[] chain1, AtomInfo[] chain2)
		{
            chainContactInfo = new ChainContactInfo();
			Dictionary<string, AtomInfo[]> residueAtomsHash1 = null;
			Dictionary<string, BoundingBox> residueBbHash1 = null;
			Dictionary<string, AtomInfo[]> residueAtomsHash2 = null;
			Dictionary<string, BoundingBox> residueBbHash2 = null;
			GetResidueBb (chain1, ref residueAtomsHash1, ref residueBbHash1);
			GetResidueBb (chain2, ref residueAtomsHash2, ref residueBbHash2);
			double contactCutoffSquare = Math.Pow (AppSettings.parameters.contactParams.cutoffAtomDist, 2);
			foreach (string seqId1 in residueBbHash1.Keys)
			{
				foreach (string seqId2 in residueBbHash2.Keys)
				{
					// bounding box overlap
					if (((BoundingBox)residueBbHash1[seqId1]).MayOverlap 
						((BoundingBox)residueBbHash2[seqId2], AppSettings.parameters.contactParams.cutoffAtomDist))
					{
						double dX2 = 0.0;
						// inter-atomic contacts between 2 residues						
						bool contactCompDone = false;
						foreach (AtomInfo atom1 in residueAtomsHash1[seqId1])
						{
							if (contactCompDone)
							{
								break;
							}
							foreach (AtomInfo atom2 in residueAtomsHash2[seqId2])
							{
								// inter-atomic distance
								dX2 = Math.Pow (atom1.xyz.X - atom2.xyz.X, 2);
								if (dX2 > contactCutoffSquare)
								{
									continue;
								}
								else
								{
									double dXY2 = Math.Pow (atom1.xyz.Y - atom2.xyz.Y, 2) + dX2;
									if (dXY2 > contactCutoffSquare)
									{
										continue;
									} // X
									else 
									{
										double dXYZ2 = Math.Pow (atom1.xyz.Z - atom2.xyz.Z, 2) + dXY2;
										if (dXYZ2 > contactCutoffSquare)
										{
											continue;
										}// Y
										else
										{	
											AtomPair atomPair = new AtomPair ();
											atomPair.firstAtom = atom1;
											atomPair.secondAtom = atom2;
											atomPair.distance = Math.Sqrt (dXYZ2);
											string seqIdString = atom1.seqId + "_" + atom2.seqId;
											chainContactInfo.atomContactHash.Add (seqIdString, atomPair);
											contactCompDone = true;
											break;
										}//Z
									}// X, Y, Z
								}// inter-atomic distance 
							}
						}// inter-atomic contacts
					}// bounding box overlap
				}
			}// loop chains 
		}

		/// <summary>
		/// get residue bounding box for eash residue in a chain
		/// </summary>
		/// <param name="chain"></param>
		/// <param name="residueAtomsHash"></param>
		/// <param name="residueBbHash"></param>
		private void GetResidueBb (AtomInfo[] chain, ref Dictionary<string, AtomInfo[]> residueAtomsHash, ref Dictionary<string, BoundingBox> residueBbHash)
		{
			residueAtomsHash = new Dictionary<string,AtomInfo[]> ();
			residueBbHash = new Dictionary<string,BoundingBox> ();
            Dictionary<string, List<AtomInfo>> residueAtomListDict = new Dictionary<string, List<AtomInfo>>();
			foreach (AtomInfo atom in chain)
			{
                if (residueAtomListDict.ContainsKey(atom.seqId))
				{
                    residueAtomListDict[atom.seqId].Add(atom);
				}
				else
				{
					List<AtomInfo> atomList = new List<AtomInfo> ();
					atomList.Add (atom);
                    residueAtomListDict.Add(atom.seqId, atomList);
				}
			}
            foreach (string seqId in residueAtomListDict.Keys)
			{
                AtomInfo[] atoms = residueAtomListDict[seqId].ToArray();
                residueAtomsHash.Add(seqId, atoms);
				BoundingBox bb = new BoundingBox (atoms);
				residueBbHash.Add (seqId, bb);
			}
		}
		#endregion
	}
}
