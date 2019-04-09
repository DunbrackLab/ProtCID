using System;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;

namespace BuInterfacesLib.BuMatch
{
	/// <summary>
	/// match PDB asymmetric chains in RCSB-BU with
	/// PQS chains in PQS-BU by entities
	/// </summary>
	public class BuChainMatch : BiolUnitMatch
	{
		private ArrayList chainPairList = new ArrayList ();

		public BuChainMatch()
		{
		}

		/// <summary>
		/// get the chainpairlist
		/// </summary>
		[XmlElement("ChainPair")]
		public BuChainPair[] ChainPairList
		{
			get
			{
				BuChainPair[] chainPairs = new BuChainPair [chainPairList.Count];
				chainPairList.CopyTo (chainPairs);
				return chainPairs;
			}
			set
			{
				if (value == null) return;
				chainPairList.Clear ();
				chainPairList.AddRange ((BuChainPair[])value);
			}
		}

		/// <summary>
		/// add the chain pair into the list
		/// </summary>
		/// <param name="chainPair"></param>
		public void AddChainPair (BuChainPair chainPair)
		{
			if (FindChainPair (chainPair))
			{
				return;
			}
			else
			{
				chainPairList.Add (chainPair);
			}
		}

		/// <summary>
		/// find a chain pair in the list
		/// </summary>
		/// <param name="chainPair"></param>
		/// <returns></returns>
		public bool FindChainPair (BuChainPair chainPair)
		{
			foreach (object thePair in chainPairList)
			{
				BuChainPair thisPair = (BuChainPair)thePair;
				if (thisPair.asymChain == chainPair.asymChain &&
					thisPair.pqsChain ==  chainPair.pqsChain)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// check if PQS chain exist
		/// </summary>
		/// <param name="pqsChain"></param>
		/// <returns></returns>
		public int IsPqsChainExist (string pqsChain)
		{
			int i = 0;
			foreach (object thePair in chainPairList)
			{
				BuChainPair thisPair = (BuChainPair)thePair;
				if (thisPair.pqsChain ==  pqsChain)
				{
					return i;
				}
				i ++;
			}
			return -1;
		}

		/// <summary>
		/// check if PDB chain exist
		/// </summary>
		/// <param name="pqsChain"></param>
		/// <returns></returns>
		public int[] IsPdbChainExist (string pdbChain)
		{
			ArrayList pdbIndexList = new ArrayList ();
			int i = 0;
			foreach (object thePair in chainPairList)
			{
				BuChainPair thisPair = (BuChainPair)thePair;
				if (thisPair.asymChain ==  pdbChain)
				{
					pdbIndexList.Add (i);
				}
				i ++;
			}
			int[] pdbIndexes = new int [pdbIndexList.Count];
			pdbIndexList.CopyTo (pdbIndexes);
			return pdbIndexes;
		}

		public BuChainPair Index (int i)
		{
			if (i >= chainPairList.Count)
			{
				return null;
			}
			return (BuChainPair)chainPairList[i];
		}
	}		

	public class BuChainPair
	{
		// asymmetric chains in PDB
		[XmlElement("AsymChain")] public string asymChain;
		// chains in PQS
		[XmlElement("PqsChain")] public string pqsChain;
		// symmetry operators
		[XmlElement("SymmetryOp")] public string symOpStr;
		// symmetry operator number
		[XmlElement("SymmetryOpNo")] public int symOpNum;
		// author chain
		[XmlElement("AuthChain")] public string authChain;
		// entity
		[XmlElement("EntityID")] public int entityId;

		public BuChainPair ()
		{
			asymChain = "";
			pqsChain = "";
			symOpStr = "";
		}

		public BuChainPair (string pdbchain, string pqschain)
		{
			asymChain = pdbchain;
			pqsChain = pqschain;
			symOpStr = "";
		}

		public BuChainPair (string pdbchain, string pqschain, string symmetryString)
		{
			asymChain = pdbchain;
			pqsChain = pqschain;
			symOpStr = symmetryString;
		}

		public BuChainPair (string pdbchain, string pqschain, string symmetryString, int symOpNo)
		{
			asymChain = pdbchain;
			pqsChain = pqschain;
			symOpStr = symmetryString;
			symOpNum = symOpNo;
		}
	}
}
