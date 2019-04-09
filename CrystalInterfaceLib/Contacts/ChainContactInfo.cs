using System;
using System.Collections.Generic;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.ProtInterfaces;

namespace CrystalInterfaceLib.Contacts
{
	/// <summary>
	/// Interatomic contacts between 2 chains
	/// </summary>
	public class ChainContactInfo
	{
		public string firstSymOpString = "";
		public string secondSymOpString = "";
		public string firstChain = "";
		public string secondChain = "";
		// store a list of contact atom pairs
		// key: a pair of sequence id; value: a pair of atoms
		public Dictionary<string, AtomPair> atomContactHash = new Dictionary<string, AtomPair> ();
        public Dictionary<string, AtomPair> cbetaContactHash = new Dictionary<string, AtomPair>();

		#region seqId string, atomId string
		public string SeqIdPair (int i)
		{
			if (i > atomContactHash.Count - 1)
			{
				throw new Exception ("Index out of range");
			}
            List<string> keyValList = new List<string>(atomContactHash.Keys);
            return atomContactHash[keyValList[i]].firstAtom.seqId + "_" + atomContactHash[keyValList[i]].secondAtom.seqId;
		}
		
		public string AtomIdPair (int i)
		{
			if (i > atomContactHash.Count - 1)
			{
				throw new Exception ("Index out of range");
			}
            List<string> keyValList = new List<string>(atomContactHash.Keys);
            return atomContactHash[keyValList[i]].firstAtom.atomId + "_" + atomContactHash[keyValList[i]].secondAtom.atomId;
		}
		#endregion

		/// <summary>
		/// get the distance for the residue pairs
		/// // for calculating Q score for Calpha and/or cbeta
		/// </summary>
		/// <returns></returns>
		public Dictionary<string, double> GetBbDistHash ()
		{
            Dictionary<string, double> seqDistHash = new Dictionary<string, double>();
			if (cbetaContactHash.Count == 0)
			{
				foreach (string seqString in atomContactHash.Keys)
				{
					seqDistHash.Add (seqString, atomContactHash[seqString].distance);
				}
			}
			else
			{
				foreach (string seqString in cbetaContactHash.Keys)
				{
					seqDistHash.Add (seqString, cbetaContactHash[seqString].distance);
				}
			}
			return seqDistHash;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, double> GetAtomContactHash()
        {
            Dictionary<string, double> seqContactHash = new Dictionary<string, double>();
            foreach (string seqString in atomContactHash.Keys)
            {
                seqContactHash.Add(seqString, atomContactHash[seqString].distance);
            }
            return seqContactHash;
        }
		/// <summary>
		/// get the contact distance for the residue pairs
		///  inter-atomic contacts for all atoms
		/// </summary>
		/// <returns></returns>
        public Dictionary<string, double> GetDistHash()
		{
            Dictionary<string, double> seqDistHash = new Dictionary<string, double>();
			foreach (string seqString in atomContactHash.Keys)
			{
				seqDistHash.Add (seqString, atomContactHash[seqString].distance);
			}
			return seqDistHash;
		}

		/// <summary>
		/// contacts 
		/// </summary>
		/// <returns></returns>
        public Dictionary<string, Contact> GetContactsHash()
		{
            Dictionary<string, Contact> contactsHash = new Dictionary<string, Contact>();
			foreach (string seqString in atomContactHash.Keys)
			{
				AtomPair atomPair = (AtomPair)atomContactHash[seqString];
				Contact contact = new Contact ();
				contact.Atom1 = atomPair.firstAtom.atomName;
				contact.Atom2 = atomPair.secondAtom.atomName;
				contact.Residue1 = atomPair.firstAtom.residue;
				contact.Residue2 = atomPair.secondAtom.residue;
				contact.SeqID1 = atomPair.firstAtom.seqId;
				contact.SeqID2 = atomPair.secondAtom.seqId;
				contact.Distance = atomPair.distance;
				contactsHash.Add (seqString, contact);
			}
			return contactsHash;
		}
	}
}
