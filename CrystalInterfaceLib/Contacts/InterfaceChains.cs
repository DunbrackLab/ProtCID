using System;
using System.Collections.Generic;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.Settings;

namespace CrystalInterfaceLib.Contacts
{
	/// <summary>
	/// InterfaceChains: two interactive chains, 
	/// and the contact interface info 
	/// </summary>
	public class InterfaceChains : InterfaceInfo
	{
		#region member variables
		public AtomInfo[] chain1 = null;
		public AtomInfo[] chain2 = null;
		// The contact interface: a list of contact atom pairs
		// key: a pair of residue sequence id; 
		// value: the distance for Cbeta or Calpha
		public Dictionary<string, double> seqDistHash = new Dictionary<string,double> ();
        public Dictionary<string, Contact> seqContactHash = new Dictionary<string, Contact>();
        public Dictionary<string, AtomPair> atomContactHash = new Dictionary<string, AtomPair>();

		#region For Q score calculation
		// get the seqId for Cbeta or Calpha
		// chain1
		private Dictionary<string, List<AtomInfo>>  seqResidueAtomsHash1 = null;
		// chain2
        private Dictionary<string, List<AtomInfo>> seqResidueAtomsHash2 = null;
		#endregion
		#endregion

		#region constructors
		public InterfaceChains()
		{
		}

		public InterfaceChains (string symOpString1, string symOpString2)
		{
			this.firstSymOpString = symOpString1;
			this.secondSymOpString = symOpString2;
		}

		public InterfaceChains (string symOpString1, string symOpString2, AtomInfo[] inChain1, AtomInfo[] inChain2)
		{
			this.firstSymOpString = symOpString1;
			this.secondSymOpString = symOpString2;
			chain1 = inChain1;
			chain2 = inChain2;
		}

		public InterfaceChains (InterfaceChains extInterChains)
		{
			this.interfaceId = extInterChains.interfaceId;
			this.firstSymOpString = extInterChains.firstSymOpString;
			this.secondSymOpString = extInterChains.secondSymOpString;
			this.entityId1 = extInterChains.entityId1;
			this.entityId2 = extInterChains.entityId2;
			this.chain1 = (AtomInfo[])extInterChains.chain1.Clone (); // shaldow copy
			this.chain2 = (AtomInfo[])extInterChains.chain2.Clone (); // shaldow copy
			this.seqDistHash = new Dictionary<string,double> (extInterChains.seqDistHash);
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="extInterChains"></param>
        /// <param name="deepCopy"></param>
        public InterfaceChains(InterfaceChains extInterChains, bool deepCopy)
        {
            this.interfaceId = extInterChains.interfaceId;
            this.firstSymOpString = extInterChains.firstSymOpString;
            this.secondSymOpString = extInterChains.secondSymOpString;
            this.entityId1 = extInterChains.entityId1;
            this.entityId2 = extInterChains.entityId2;
            if (deepCopy)
            {
                DeepCopyChains(extInterChains.chain1, 1); // deep copy
                DeepCopyChains(extInterChains.chain2, 2);
                DeepCopySeqDistHash(extInterChains.seqDistHash);
            }
            else
            {
                this.chain1 = (AtomInfo[])extInterChains.chain1.Clone(); // shaldow copy
                this.chain2 = (AtomInfo[])extInterChains.chain2.Clone(); // shaldow copy
                this.seqDistHash = new Dictionary<string,double> (extInterChains.seqDistHash);
            }
        }

        ///
		public InterfaceChains (string symOpString1, string symOpString2, 
			AtomInfo[] inChain1, AtomInfo[] inChain2, bool deepCopy)
		{
			this.firstSymOpString = symOpString1;
			this.secondSymOpString = symOpString2;
			if (! deepCopy)
			{
				chain1 = inChain1;
				chain2 = inChain2;
			}
			else
			{
				DeepCopyChains (inChain1, 1);
				DeepCopyChains (inChain2, 2);
			}
		} 
		#endregion

		#region deep copy
		public void DeepCopyChains (AtomInfo[] chain, int chainNo)
		{
			if (chainNo == 1)
			{
				this.chain1 = new AtomInfo[chain.Length];
			}
			else if (chainNo == 2)
			{
				this.chain2 = new AtomInfo[chain.Length];
			}
			for (int i = 0; i < chain.Length; i ++)
			{
				if (chainNo == 1)
				{
				//	chain1[i] = new AtomInfo (chain[i]);
                    this.chain1[i] = (AtomInfo)chain[i].Clone(); //clone of AtomInfo class is a deep copy
				}
				else if (chainNo == 2)
				{
				//	chain2[i] = new AtomInfo (chain[i]);
                    this.chain2[i] = (AtomInfo)chain[i].Clone();
				}
			}
		}

       /// <summary>
       /// 
       /// </summary>
       /// <param name="inSeqContactHash"></param>
        public void DeepCopySeqContactHash(Dictionary<string, Contact> inSeqContactHash)
        {
            if (inSeqContactHash != null)
            {
                this.seqContactHash = new Dictionary<string, Contact> ();
                foreach (string seqPair in inSeqContactHash.Keys)
                {
                    Contact atomContact = (Contact)inSeqContactHash[seqPair];
                    Contact newAtomContact = new Contact(atomContact);
                    this.seqContactHash.Add(seqPair, newAtomContact);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inSeqDistHash"></param>
        public void DeepCopySeqDistHash(Dictionary<string, double> inSeqDistHash)
        {
            if (inSeqDistHash != null)
            {
                this.seqDistHash = new Dictionary<string,double> ();
                double distance = 0;
                foreach (string seqPair in inSeqDistHash.Keys)
                {
                    distance = (double)inSeqDistHash[seqPair];
                    this.seqDistHash.Add(seqPair, distance);
                }
            }
        }
		#endregion

		#region Get Interface Residues and distance
		/// <summary>
		/// get the interface residues and corresponding distance
		/// </summary>
		public bool GetInterfaceResidueDist ()
		{
			ChainContact chainContact = new ChainContact (this.firstSymOpString, this.secondSymOpString, 
						AppSettings.parameters.contactParams.cutoffResidueDist, AppSettings.parameters.contactParams.numOfResidueContacts);
			ChainContactInfo interfaceInfo = chainContact.GetChainContactInfo (chain1, chain2);
			if (interfaceInfo == null)
			{
				return false;
			}
			seqDistHash = interfaceInfo.GetBbDistHash ();
            seqContactHash = interfaceInfo.GetContactsHash ();
			return true;
		}
		#endregion

		#region AtomInfo
		/// <summary>
		/// return the atom for the input sequence id
		/// </summary>
		/// <param name="seqId"></param>
		/// <param name="dim">chain1 or chain2</param>
		/// <returns></returns>
		public AtomInfo GetAtom (string seqId, int dim)
		{
			AtomInfo theAtom = null;
			switch (dim)
			{
				case 1: 
				{
					foreach (AtomInfo atom in chain1)
					{
						if (atom.seqId == seqId)
						{
							theAtom = atom;
							break;
						}
					}
					break;
				}
				case 2:
				{
					foreach (AtomInfo atom in chain2)
					{
						if (atom.seqId == seqId)
						{
							theAtom = atom;
							break;
						}
					}
					break;
				}
				default:
				{
					break;
				}
			}
			return theAtom;
		}
		#endregion

		#region reverse interface chains
		/// <summary>
		/// reverse the chains in the interface chain pair
		/// </summary>
		public void Reverse ()
		{
			string symOpString1 = this.firstSymOpString;
			this.firstSymOpString = this.secondSymOpString;
			this.secondSymOpString = symOpString1;
			AtomInfo[] chain1Copy = (AtomInfo[])this.chain1.Clone (); 
			this.chain1 = this.chain2;
			this.chain2 = chain1Copy;
            int entityTemp = this.entityId1;
            this.entityId1 = this.entityId2;
            this.entityId2 = entityTemp;
			if (seqResidueAtomsHash1 != null)
			{
				if (seqResidueAtomsHash2 != null)
				{
					Dictionary<string, List<AtomInfo>> tempHash = new Dictionary<string,List<AtomInfo>> (seqResidueAtomsHash2);
					seqResidueAtomsHash2 = seqResidueAtomsHash1;
					seqResidueAtomsHash1 = tempHash;
				}
				else
				{
					seqResidueAtomsHash2 = seqResidueAtomsHash1;
					seqResidueAtomsHash1 = null;
				}
			}
			ReverseInterfaceInfo ();
		}

		/// <summary>
		/// reverse the residue sequentail number in an interface
		/// </summary>
		private void ReverseInterfaceInfo ()
		{
			Dictionary<string, double> reversalDistHash = new Dictionary<string, double> ();
			string reversalSeqString = "";
			foreach (string seqString in seqDistHash.Keys)
			{
				string[] seqIds = seqString.Split ('_');
				reversalSeqString = seqIds[1] + "_" + seqIds[0];
                reversalDistHash.Add(reversalSeqString, seqDistHash[seqString]);
			}
            seqDistHash = reversalDistHash;

			Dictionary<string, Contact> reversalContactHash = new Dictionary<string,Contact> ();
			foreach (string seqString in seqContactHash.Keys)
			{
				string[] seqIds = seqString.Split ('_');
				reversalSeqString = seqIds[1] + "_" + seqIds[0];
				Contact contact = seqContactHash[seqString];
				contact.Reverse ();
				reversalContactHash.Add (reversalSeqString, contact);
			}
			seqContactHash = reversalContactHash;
		}
		#endregion

		#region clear 
		/// <summary>
		/// clear the interfaces
		/// </summary>
		public void Clear ()
		{
			this.chain1 = null;
			this.chain2 = null;
			this.seqDistHash = null;
			this.seqContactHash = null;
	//		this.Finalize ();
		}
		#endregion

		#region get distance for residue sequence pair
		/// <summary>
		/// input 2 sequence ids, output the distance between 2 
		/// sequence search, the atom list is not long
		/// </summary>
		/// <param name="seqId1"></param>
		/// <param name="seqId2"></param>
		/// <returns></returns>
		public double DistForSeqIds (string seqString)
		{
			string[] seqIds = seqString.Split ('_');
			if (chain1 == null || chain2 == null)
			{
				return -1;
			}
			AtomInfo atom1 = null;
			AtomInfo atom2 = null;
			foreach (AtomInfo atom in chain1)
			{
				if (atom.seqId == seqIds[0])
				{
					atom1 = atom;
					break;
				}
			}
			foreach (AtomInfo atom in chain2)
			{
				if (atom.seqId == seqIds[1])
				{
					atom2 = atom;
					break;
				}
			}
			if (atom1 == null || atom2 == null)
			{
				return -1;
			}
			return (atom1 - atom2);
		}

		/// <summary>
		/// sequence id hash table for Cbeta and Calpha
		/// </summary>
		/// <param name="chain"></param>
		/// <returns></returns>
		private Dictionary<string, List<AtomInfo>> GetSeqBbResidueHash (AtomInfo[] chain)
		{
            Dictionary<string, List<AtomInfo>> seqResidueAtomsDict = new Dictionary<string, List<AtomInfo>>();
			foreach (AtomInfo atom in chain)
			{
				if (atom.atomName != "CB" && atom.atomName != "CA")
				{
					continue;
				}
                if (seqResidueAtomsDict.ContainsKey(atom.seqId))
				{
                    seqResidueAtomsDict[atom.seqId].Add (atom);
				}
				else
				{
					List<AtomInfo> bbAtoms = new List<AtomInfo> ();
					bbAtoms.Add (atom);
                    seqResidueAtomsDict.Add(atom.seqId, bbAtoms);
				}
			}
            return seqResidueAtomsDict;
		}

		/// <summary>
		/// index for the sequence and bb atoms,
		/// used after each superposition
		/// </summary>
		public void ResetSeqResidueHash ()
		{
			seqResidueAtomsHash1 = GetSeqBbResidueHash (chain1);
			seqResidueAtomsHash2 = GetSeqBbResidueHash (chain2);
		}

		public void ClearSeqResidueHash ()
		{
			seqResidueAtomsHash1 = null;
			seqResidueAtomsHash2 = null;
		}
		/// <summary>
		/// input 2 sequence ids, output the distance between 2 
		/// sequence search, the atom list is not long
		/// </summary>
		/// <param name="seqId1"></param>
		/// <param name="seqId2"></param>
		/// <returns></returns>
		public double CbetaDistForSeqIds (string seqString)
		{
			if (chain1 == null || chain2 == null)
			{
				return -1;
			}
			if (seqResidueAtomsHash1 == null)
			{
				seqResidueAtomsHash1 = GetSeqBbResidueHash (chain1);
			}
			if (seqResidueAtomsHash2 == null)
			{
				seqResidueAtomsHash2 = GetSeqBbResidueHash (chain2);
			}
			string[] seqIds = seqString.Split ('_');
			
			List<AtomInfo> atomList1 = null;
            List<AtomInfo> atomList2 = null;
			if (seqResidueAtomsHash1.ContainsKey (seqIds[0]))
			{
				atomList1 = seqResidueAtomsHash1[seqIds[0]];
			}
			if (seqResidueAtomsHash2.ContainsKey (seqIds[1]))
			{
				atomList2 = seqResidueAtomsHash2[seqIds[1]];
			}
			if (atomList1 == null || atomList2 == null)
			{
				return -1;
			}
			double minDist = 10000.0;
			foreach (AtomInfo atom1 in atomList1)
			{
				foreach (AtomInfo atom2 in atomList2)
				{
					double dist = atom1 - atom2;
					if (minDist > dist)
					{
						minDist = dist;
					}
				}
			}
			return minDist;
		}
		#endregion

		#region distance for two cbeta atoms 
		/// <summary>
		/// the distance between two cbeta atoms within one chain 
		/// </summary>
		/// <param name="i"></param>
		/// <param name="j"></param>
		/// <returns></returns>
		public double GetIntraProtDistance (int i, int j)
		{
			if (seqResidueAtomsHash1 == null)
			{
				seqResidueAtomsHash1 = GetSeqBbResidueHash (chain1);
			}
			if (! seqResidueAtomsHash1.ContainsKey (i.ToString ()) || 
				! seqResidueAtomsHash1.ContainsKey (j.ToString ()))
			{
				return -1.0;																						  
			}
			List<AtomInfo> atomsListi = seqResidueAtomsHash1[i.ToString ()];
			List<AtomInfo> atomsListj = seqResidueAtomsHash1[j.ToString ()];
			double minDist = 10000.0;
			foreach (AtomInfo atom1 in atomsListi)
			{
				foreach (AtomInfo atom2 in atomsListj)
				{
					double dist = atom1 - atom2;
					if (minDist > dist)
					{
						minDist = dist;
					}
				}
			}
			return minDist;
		}
		#endregion

       
    }
}
