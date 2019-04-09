using System;
using System.Collections.Generic;

namespace CrystalInterfaceLib.ProtInterfaces
{
	/// <summary>
	/// Protein Interface
	/// </summary>
	public class ProtInterfaceInfo
	{
		private int interfaceId = -1;
		// chain contains asymmetric chain 
		// and symmetry strings
		private string chain1 = "";
		private string symmetryString1 = "";
		private string chain2 = "";
		private string symmetryString2 = "";
		// residue sequence id list
		private Dictionary<string, string> resSeqIdHash1 = new Dictionary<string,string> ();
		private Dictionary<string, string> resSeqIdHash2 = new Dictionary<string,string> ();
		private string remark = "";
		private string existType = "";
		private double asa = -1.0;

		public ProtInterfaceInfo()
		{
		}

		#region properties
		/// <summary>
		/// the identifier number for this interface
		/// </summary>
		public int InterfaceId 
		{
			get
			{
				return interfaceId;
			}
			set
			{
				interfaceId = value;
			}
		}
		/// <summary>
		/// chain1
		/// </summary>
		public string Chain1
		{
			get
			{
				return chain1;
			}
			set
			{
				chain1 = value;
			}
		}

		/// <summary>
		/// the symmetry string for chain1
		/// </summary>
		public string SymmetryString1
		{
			get
			{
				return symmetryString1;
			}
			set
			{
				symmetryString1 = value;
			}
		}
		/// <summary>
		/// chain2
		/// </summary>
		public string Chain2
		{
			get
			{
				return chain2;
			}
			set
			{
				chain2 = value;
			}
		}
		/// <summary>
		/// the symmetry string for chain2
		/// </summary>
		public string SymmetryString2
		{
			get
			{
				return symmetryString2;
			}
			set
			{
				symmetryString2 = value;
			}
		}

		/// <summary>
		/// residues sequence ids in chain1
		/// </summary>
		public Dictionary<string, string> ResiduesInChain1 
		{
			get
			{
				return resSeqIdHash2;
			}
			set
			{
				if (value == null) return;
				resSeqIdHash1 = (Dictionary<string, string>)value;
			}
		}
        /// <summary>
        /// residue sequence ids in chain2
        /// </summary>
		public Dictionary<string, string> ResiduesInChain2
		{
			get
			{
				return resSeqIdHash2;
			}
			set
			{
				if (value == null) return;
				resSeqIdHash2 = (Dictionary<string, string>)value;
			}
		}

		/// <summary>
		/// the other information about this interface
		/// </summary>
		public string Remark 
		{
			get
			{
				return remark;
			}
			set
			{
				remark = value;
			}
		}

		/// <summary>
		/// exist type: pdb, pqs or both
		/// </summary>
		public string ExistType 
		{
			get
			{
				return existType;
			}
			set
			{
				existType = (string)value;
			}
		}

		/// <summary>
		/// accessible surface area
		/// </summary>
		public double ASA 
		{
			get
			{
				return asa;
			}
			set
			{
				asa = value;
			}
		}
		#endregion

		#region add
		/// <summary>
		/// Add a sequence id to sequence id list for an interface
		/// </summary>
		/// <param name="seqId">sequence id</param>
		/// <param name="dim">chain1 or chain2</param>
		public void Add (string seqId, string residue, int dim)
		{
			if (dim == 1)
			{
				resSeqIdHash1.Add (seqId, residue);
			}
			else if (dim == 2)
			{
				resSeqIdHash2.Add (seqId, residue);
			}
		}
		#endregion

		/// <summary>
		/// add residue info to remark
		/// </summary>
		/// <param name="residueRows"></param>
		/// <param name="chain"></param>
		/// <returns></returns>
		public string FormatResidues (string chain)
		{
			Dictionary<string, string> residueSeqHash = resSeqIdHash1;
			if (chain.ToUpper () == "B")
			{
				residueSeqHash = resSeqIdHash2;
			}
			List<string> seqStrList = new List<string> (residueSeqHash.Keys);
			SortStringSeqIds (ref seqStrList);
			string residueString = "Remark 350 chain " + chain + " interface residues";
			int residueNumPerLine = 8;
			int count = 0;
			foreach (string seqId in seqStrList)
			{
				if (count % residueNumPerLine == 0)
				{
					residueString += "\r\nRemark 350 ";
				}				
				residueString += residueSeqHash[seqId].ToString ();
				residueString += seqId.PadLeft (4, ' ');
				residueString += " ";	
				count ++;
			}
			return residueString += "\r\n";
		}

		/// <summary>
		/// Sort sequence string IDs
		/// </summary>
		/// <param name="seqStrList"></param>
		public void SortStringSeqIds (ref List<string> seqStrList)
		{
			List<string> tempList = new List<string> ();
			tempList.AddRange (seqStrList);
			int i = 0;
			foreach (string seqId in tempList)
			{
				seqStrList[i] = seqId.PadLeft (10, '0');
				i ++;
			}
			seqStrList.Sort ();
			tempList.Clear ();
			tempList.AddRange (seqStrList);
			i = 0;
			foreach (string seqId in tempList)
			{
				seqStrList[i] = seqId.TrimStart ('0');
				// case: 0
				if (seqStrList[i].ToString () == "")
				{
					seqStrList[i] = "0";
				}
					// case "0A"
				else if (seqStrList[i].ToString ().Length == 1 && Char.IsLetter (seqStrList[i].ToString ()[0]))
				{
					seqStrList[i] = "0" + seqStrList[i];
				}
				i ++;
			}
		}
	}
}
