using System;
using System.Collections.Generic;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.Contacts;

namespace CrystalInterfaceLib.ProtInterfaces
{
	/// <summary>
	/// Summary description for ProtInterface.
	/// </summary>
	public class ProtInterface : ProtInterfaceInfo
	{
		#region member variables
		private Dictionary<string, AtomInfo[]> residueAtoms1 = new Dictionary<string,AtomInfo[]> ();
		private Dictionary <string, AtomInfo[]> residueAtoms2 = new Dictionary<string,AtomInfo[]> ();
        private List<Contact> residuePairList = new List<Contact>();
		#endregion

		#region constructors
		public ProtInterface()
		{
		}
		
		public ProtInterface (ProtInterfaceInfo interfaceInfo)
		{
			this.ASA = interfaceInfo.ASA;
			this.Chain1 = interfaceInfo.Chain1;
			this.Chain2 = interfaceInfo.Chain2;
			this.ExistType = interfaceInfo.ExistType;
			this.InterfaceId = interfaceInfo.InterfaceId;
			this.Remark = interfaceInfo.Remark;
			this.ResiduesInChain1 = interfaceInfo.ResiduesInChain1;
			this.ResiduesInChain2 = interfaceInfo.ResiduesInChain2;
			this.SymmetryString1 = interfaceInfo.SymmetryString1;
			this.SymmetryString2 = interfaceInfo.SymmetryString2;
		}
		#endregion

		#region properties
		/// <summary>
		/// 
		/// </summary>
		public Dictionary<string, AtomInfo[]> ResidueAtoms1 
		{
			get
			{
				return residueAtoms1;
			}
			set
			{
				if (value != null)
				{
					residueAtoms1 = value;
				}
			}
		}

        public Dictionary<string, AtomInfo[]>  ResidueAtoms2 
		{
			get
			{
				return residueAtoms2;
			}
			set
			{
				if (value != null)
				{
					residueAtoms2 = value;
				}
			}
		}

		/// <summary>
		/// interface contacts
		/// residue, and sequence id
		/// </summary>
		public Contact[] Contacts
		{
			get 
			{				
                return residuePairList.ToArray ();
			}
			set
			{
				if (value == null) return;
				residuePairList.Clear ();
				residuePairList.AddRange (value);
			}
		}
		#endregion	

		/// <summary>
		/// the atoms of a residue
		/// </summary>
		/// <param name="seqId"></param>
		/// <param name="dim"></param>
		/// <returns></returns>
		public AtomInfo[] GetResidueAtoms (string seqId, int dim)
		{
		    AtomInfo[] atomList = null;
			if (dim == 1)
			{
				atomList = residueAtoms1[seqId];
			}
			else
			{
				atomList = residueAtoms2[seqId];
			}
            return atomList;
		}

		/// <summary>
		/// get chain atoms
		/// </summary>
		/// <param name="dim"></param>
		/// <returns></returns>
		public AtomInfo[] GetChainAtoms (int dim)
		{
			List<AtomInfo> atomList = new List<AtomInfo> ();
			if (dim == 1)
			{
				foreach (string seqId in residueAtoms1.Keys)
				{
					atomList.AddRange (residueAtoms1[seqId]);
				}
			}
			else
			{
				foreach (string seqId in residueAtoms2.Keys)
				{
					atomList.AddRange (residueAtoms2[seqId]);
				}
			}
            return atomList.ToArray ();
		}
	}
}
