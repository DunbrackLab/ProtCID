using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace CrystalInterfaceLib.Crystal
{
	/// <summary>
	/// atoms of a symmetric chain
	/// </summary>
	public class ChainAtoms
	{
		internal string asymChain = "";
		internal int entityId = 0;
		internal string polymerType = "";
		// store the list of atoms for this asymmetric chain
		internal List<AtomInfo> cartnAtoms = null;
		public ChainAtoms()
		{
			cartnAtoms = new List<AtomInfo> (); 
		}

		public ChainAtoms(ChainAtoms extChain)
		{
			this.asymChain = extChain.AsymChain;
			this.entityId = extChain.entityId;
			this.polymerType = extChain.PolymerType;
            this.cartnAtoms = new List<AtomInfo> ();
			foreach (AtomInfo atom in extChain.CartnAtoms)
			{
				this.cartnAtoms.Add ((AtomInfo)atom.Clone ());
			}
		}

		#region properties
		// asymmetric chain id
		[XmlAttribute]
		public string AsymChain
		{
			get
			{
				return asymChain;
			}
			set
			{
				asymChain = value;
			}
		}

		// entity id
		[XmlAttribute]
		public int EntityID
		{
			get 
			{
				return entityId;
			}
			set
			{
				entityId = value;
			}
		}
		[XmlAttribute]
		public string PolymerType
		{
			get
			{
				return polymerType;
			}
			set
			{
				polymerType = value;
			}
		}


		// the list of atoms for this asymmetric chain
		[XmlElement("Atom")]
		public AtomInfo [] CartnAtoms
		{
			get
			{
				AtomInfo[] atomInfoList = new AtomInfo[cartnAtoms.Count];
				cartnAtoms.CopyTo (atomInfoList);
				return atomInfoList;
			}
			set
			{
				if (value == null)
				{
					return;
				}
				AtomInfo[] atomInfoList = (AtomInfo[]) value;
				cartnAtoms.Clear ();
				cartnAtoms.AddRange (atomInfoList);
			}
		}
		#endregion

		#region atom types
		/// <summary>
		/// c-alpha atoms
		/// </summary>
		public AtomInfo[] CalphaAtoms ()
		{
			List<AtomInfo> calphaList = new List<AtomInfo> ();
			foreach (AtomInfo atom in cartnAtoms)
			{
				if (atom.atomName.ToUpper () == "CA")
				{
					calphaList.Add (new AtomInfo(atom));
				}
			}
            return calphaList.ToArray ();
		}

		/// <summary>
		/// c-beta atoms
		/// </summary>
		public AtomInfo[] CbetaAtoms ()
		{
			List<AtomInfo> cbetaList = new List<AtomInfo> ();
			foreach (AtomInfo atom in cartnAtoms)
			{
				if (atom.atomName.ToUpper () == "CB")
				{
					cbetaList.Add (new AtomInfo(atom));
				}
			}
            return cbetaList.ToArray ();
		}

		/// <summary>
		/// c-beta and C-alpha atoms
		/// </summary>
		public AtomInfo[] CalphaCbetaAtoms ()
		{
			List<AtomInfo> calphaCbetaList = new List<AtomInfo> ();
			foreach (AtomInfo atom in cartnAtoms)
			{
				if (atom.atomName.ToUpper () == "CB" || atom.atomName.ToUpper () == "CA")
				{
					calphaCbetaList.Add (new AtomInfo(atom));
				}
			}
            return calphaCbetaList.ToArray ();
		}


		/// <summary>
		/// get backbone atoms
		/// </summary>
		public AtomInfo[] BackboneAtoms ()
		{			
			List<AtomInfo> backboneList = new List<AtomInfo> ();
			foreach (AtomInfo atom in cartnAtoms)
			{
				if (atom.atomName == "CA" || atom.atomName == "N" ||
					atom.atomName == "C" || atom.atomName == "O" || atom.atomName == "OXT")
				{
					backboneList.Add (new AtomInfo(atom));
				}
			}
            return backboneList.ToArray ();			
		}
		#endregion

		/// <summary>
		/// add an atom to the list
		/// </summary>
		/// <param name="atom"></param>
		/// <returns></returns>
		public void AddAtom(AtomInfo atom)
		{
			cartnAtoms.Add (atom);
		}

        /// <summary>
        /// the polypeptide residues only contain c-alpha
        /// </summary>
        /// <returns></returns>
        public bool IsProtChainOnlyContainCalpha()
        {
            if (polymerType == "polypeptide")
            {
                AtomInfo[] calphaAtoms = CalphaAtoms();
                if (calphaAtoms.Length == cartnAtoms.Count)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The polypeptide contains more than 50% only c-alpha residues
        /// </summary>
        /// <returns></returns>
        public bool IsProtChainContainMostCalpha()
        {
            if (polymerType == "polypeptide")
            {
                AtomInfo[] calphaAtoms = CalphaAtoms();
                if ((double)calphaAtoms.Length / (double)cartnAtoms.Count >= 0.5)
                {
                    return true;
                }
            }
            return false;
        }
		
		/// <summary>
		/// number of residues based on number of C-alpha
		/// </summary>
		/// <returns></returns>
		public int GetNumOfResidues ()
		{
			return this.CalphaAtoms ().Length;
		}


		#region sort by atom ID
		/// <summary>
		/// sort chains by atom ID
		/// </summary>
		public void SortByAtomId ()
		{
			if (cartnAtoms == null)
			{
				return ;
			}
			AtomInfo[] chain = this.CartnAtoms;
			Dictionary<int, AtomInfo> seqHash = new Dictionary<int,AtomInfo> ();
			foreach (AtomInfo atom in chain)
			{
				// sould have unique atom id
				seqHash.Add (atom.atomId, atom);
			}
			List<int> atomIdList = new List<int> (seqHash.Keys);
			atomIdList.Sort ();
			AtomInfo[] sortedChain = new AtomInfo [chain.Length];
			int count = 0;
			foreach (int atomId in atomIdList)
			{
				sortedChain[count] = seqHash[atomId];
				count ++;
			}
			this.CartnAtoms = sortedChain;
		}
		#endregion
	}
}
