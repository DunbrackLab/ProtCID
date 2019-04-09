using System;
using System.Collections.Generic;
using System.Data;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.Settings;

namespace CrystalInterfaceLib.StructureComp.HomoEntryComp
{
	/// <summary>
	/// Summary description for SuperpositionPsiBlast.
	/// </summary>
	public class SuperpositionInterfaces
	{
		public SuperpositionInterfaces()
		{
		}

		#region Superpose interfaces/interchains by sequence/structure alignment

		#region superpose interface chains
		/// <summary>
		/// superpose interfaces based on psiblast alignment
		/// </summary>
		/// <param name="interChains2"></param>
		/// <param name="alignInfoTable"></param>
		public double[] SuperposeInterfaces (InterfaceChains[] interfaceChains2, DataTable alignInfoTable)
		{
			double[] identities = new double [interfaceChains2.Length];
			double identity = 0.0;
			DataRow alignRow = null;
			DataRow alignRow2 = null;
			int count = 0;
			foreach (InterfaceChains interfaceChains in interfaceChains2)
			{
				AtomInfo[] chain1 = interfaceChains.chain1;				
				string asymChain1 = interfaceChains.firstSymOpString.Substring (0, interfaceChains.firstSymOpString.IndexOf ("_"));
				alignRow = GetAlignRow (alignInfoTable, asymChain1);
				if (alignRow != null)
				{
					identity = Convert.ToDouble (alignRow["Identity"].ToString ());		
					SuperposeChain (chain1, alignRow);
				}
		
				AtomInfo[] chain2 = interfaceChains.chain2;
				string asymChain2 = interfaceChains.secondSymOpString.Substring (0, interfaceChains.secondSymOpString.IndexOf ("_"));
				if (asymChain1 != asymChain2)
				{
					alignRow2 = GetAlignRow (alignInfoTable, asymChain2);
					if (alignRow2 != null)
					{
						if (identity  > Convert.ToDouble (alignRow2["Identity"].ToString ()))
						{
							identity  = Convert.ToDouble (alignRow2["Identity"].ToString ());
						}			
						SuperposeChain (chain2, alignRow2);
						interfaceChains.seqDistHash = 
							SuperposeInterface (interfaceChains.seqDistHash, alignRow, alignRow2);
					}
				}
				else
				{
					SuperposeChain (chain2, alignRow);
					interfaceChains.seqDistHash = 
						SuperposeInterface (interfaceChains.seqDistHash, alignRow);
				}
				interfaceChains.ResetSeqResidueHash ();
				// smaller identity
				if (identity < AppSettings.parameters.simInteractParam.identityCutoff)
				{
					identity = AppSettings.parameters.simInteractParam.identityCutoff;
				}
				identities[count] = identity;
				count ++;
			}
			return identities;
		}

		/// <summary>
		/// superpose interfaces based on psiblast alignment
		/// </summary>
		/// <param name="interChains2"></param>
		/// <param name="alignInfoTable"></param>
		public double SuperposeInterfaces (InterfaceChains interface1, 
			InterfaceChains interface2, DataTable alignInfoTable, bool isReverse)
		{
			DataRow alignRow = null;
			DataRow alignRow2 = null;
			double identity = 100.0;
			
			if (isReverse)
			{
				interface2.Reverse ();
			}
			int entityId11 = interface1.entityId1;
			AtomInfo[] chain21 = interface2.chain1;				
			int entityId21 = interface2.entityId1;
			
			int entityId12 = interface1.entityId2;
			AtomInfo[] chain22 = interface2.chain2;
			int entityId22 = interface2.entityId2;
			
			alignRow = GetAlignRow (alignInfoTable, entityId11, entityId21);
			if (alignRow != null)
			{
				identity = Convert.ToDouble (alignRow["Identity"].ToString ());		
				SuperposeChain (chain21, alignRow);
			}
			if (entityId21 == entityId22 && entityId11 == entityId12)
			{
				SuperposeChain (chain22, alignRow);
				interface2.seqDistHash = 
					SuperposeInterface (interface2.seqDistHash, alignRow);
			}
			else
			{
				alignRow2 = GetAlignRow (alignInfoTable, entityId12, entityId22);
				if (alignRow2 != null)
				{
					if (identity  > Convert.ToDouble (alignRow2["Identity"].ToString ()))
					{
						identity  = Convert.ToDouble (alignRow2["Identity"].ToString ());
					}			
					SuperposeChain (chain22, alignRow2);
				}
				interface2.seqDistHash = 
					SuperposeInterface (interface2.seqDistHash, alignRow, alignRow2);
			}
			// reset the hashtable for residue and its bb atoms.
			interface2.ResetSeqResidueHash ();

			return identity;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="interface2"></param>
		/// <param name="alignRow"></param>
		public void SuperposeInterfaces (InterfaceChains interface2, DataTable alignInfoTable)
		{
			AtomInfo[] chain1 = interface2.chain1;				
			string asymChain1 = interface2.firstSymOpString.Substring (0, interface2.firstSymOpString.IndexOf ("_"));
			DataRow alignRow = GetAlignRow (alignInfoTable, asymChain1);
			SuperposeChain (chain1, alignRow);

			AtomInfo[] chain2 = interface2.chain2;
			string asymChain2 = interface2.secondSymOpString.Substring (0, interface2.secondSymOpString.IndexOf ("_"));
			if (asymChain1 != asymChain2)
			{
				DataRow alignRow2 = GetAlignRow (alignInfoTable, asymChain2);
				SuperposeChain (chain2, alignRow2);
				interface2.seqDistHash = 
					SuperposeInterface (interface2.seqDistHash, alignRow, alignRow2);
			}
			else
			{
				SuperposeChain (chain2, alignRow);
				interface2.seqDistHash = 
					SuperposeInterface (interface2.seqDistHash, alignRow);
			}
			interface2.ResetSeqResidueHash ();
		}

		/// <summary>
		/// change the residue sequence numbers to it psiblast aligned pair
		/// </summary>
		/// <param name="chain"></param>
		/// <param name="psiblastInfo"></param>
		protected void SuperposeChain (AtomInfo[] chain, DataRow alignInfoRow)
		{
            Dictionary<string, string> seqPairHash = GetSeqMatch(alignInfoRow);
			if (seqPairHash == null)
			{
				return;
			}
			foreach (AtomInfo atom in chain)
			{
				if (seqPairHash.ContainsKey (atom.seqId))
				{
					atom.seqId = seqPairHash[atom.seqId].ToString ();
				}
				else
				{
					// not superposed, change to negative number
					atom.seqId = "-" + atom.seqId;
				}
			}
		}
		#endregion

		#region superpose interfaces
		/// <summary>
		/// Superpose interfaces
		/// two chains in the interface are same sequence
		/// </summary>
		/// <param name="atomPairHash">key: sequence id pair, value:atom pair</param>
		/// <param name="alignRow">PSI-BLAST alignment information</param>
        protected Dictionary<string, double> SuperposeInterface(Dictionary<string, double> seqDistHash, DataRow alignRow)
		{
			Dictionary<string, string> seqPairHash = GetSeqMatch (alignRow);
			if (seqPairHash == null)
			{
				return seqDistHash;
			}
			Dictionary<string, double> newSeqDistHash = new Dictionary<string,double> ();
			foreach (string seqPair in seqDistHash.Keys)
			{
				string newSeqPair = "";
				string[] seqIds = seqPair.Split ('_');
				if (seqPairHash.ContainsKey (seqIds[0]))
				{
					newSeqPair += seqPairHash[seqIds[0]].ToString ();
				}
				else
				{
					newSeqPair += ("-" + seqIds[0]);
				}
				newSeqPair += "_";
				if (seqPairHash.ContainsKey (seqIds[1]))
				{
					newSeqPair += seqPairHash[seqIds[1]].ToString ();	
				}
				else
				{
					newSeqPair += ("-" + seqIds[1]);;
				}
				newSeqDistHash.Add (newSeqPair, seqDistHash[seqPair]);
			}
			return newSeqDistHash;
		}

		/// <summary>
		/// Superpose interfaces
		/// two chains with different sequence
		/// </summary>
		/// <param name="atomPairHash">key: sequence id pair, value:atom pair</param>
		/// <param name="alignRow1"></param>
		/// <param name="alignRow2"></param>
		protected Dictionary<string, double> SuperposeInterface (Dictionary<string, double> seqDistHash, DataRow alignRow1, DataRow alignRow2)
		{
			Dictionary<string, string> seqPairHash1 = GetSeqMatch (alignRow1);
			Dictionary<string, string> seqPairHash2 = GetSeqMatch (alignRow2);
			if (seqPairHash1 == null || seqPairHash2 == null)
			{
				return seqDistHash;
			}
			Dictionary<string, double> newSeqDistHash = new Dictionary<string,double> ();
			foreach (string seqPair in seqDistHash.Keys)
			{
				string newSeqPair = "";
				string[] seqIds = seqPair.Split ('_');
				if (seqPairHash1.ContainsKey (seqIds[0]))
				{
					newSeqPair += seqPairHash1[seqIds[0]].ToString ();
				}
				else
				{
					newSeqPair += ("-" + seqIds[0]);
				}
				newSeqPair += "_";
				if (seqPairHash2.ContainsKey (seqIds[1]))
				{
					newSeqPair += seqPairHash2[seqIds[1]].ToString ();	
				}
				else
				{
					newSeqPair += ("-" + seqIds[1]);
				}
				// should check
				//seqPair = newSeqPair;
                if (!newSeqDistHash.ContainsKey(newSeqPair))
                {
                    newSeqDistHash.Add(newSeqPair, seqDistHash[seqPair]);
                }
			}
			return newSeqDistHash;
		}
		#endregion

		#region match residue sequence id
		/// <summary>
		/// residue sequence pair: hit residue sequence number 
		/// and corresponding query residue sequence number
		/// </summary>
		/// <param name="alignInfoRow"></param>
		/// <returns></returns>
		private Dictionary<string, string> GetSeqMatch (DataRow alignInfoRow)
		{
			if (alignInfoRow == null)
			{
				return null;
			}
			// used to store the corresponding sequence pairs
			// key: hit sequence id, value: query sequence id
			Dictionary<string, string> seqPairHash = null;

            if (alignInfoRow.Table.Columns.Contains("QuerySeqNumbers"))
            {
                seqPairHash = GetSeqMatchFromAlignedSeqNumbers(alignInfoRow);
            }
            else
            {
                seqPairHash = new Dictionary<string,string> ();
                int hSeqStart = Convert.ToInt32(alignInfoRow["HitStart"].ToString());
                int qSeqStart = Convert.ToInt32(alignInfoRow["QueryStart"].ToString());
                int hSeqEnd = Convert.ToInt32(alignInfoRow["HitEnd"].ToString());
                int qSeqEnd = Convert.ToInt32(alignInfoRow["QueryEnd"].ToString());
                // same alignment length
                //  no gap, don't have to check the aligned sequences
                if ((qSeqEnd - qSeqStart) == (hSeqEnd - hSeqStart))
                {
                    if (qSeqEnd - qSeqStart == 0)
                    {
                        return null;
                    }
                }
                // have to check the aligned sequences
                int hCount = 0;
                int qCount = 0;
                int hitSeqNum = -1;
                int querySeqNum = -1;

                string qSequence = alignInfoRow["QuerySequence"].ToString();
                string hSequence = alignInfoRow["HitSequence"].ToString();

                for (int i = 0; i < qSequence.Length; i++)
                {
                    hitSeqNum = -1;
                    if (hSequence[i] != '-')
                    {
                        hitSeqNum = hSeqStart + hCount;
                        hCount++;
                    }
                    querySeqNum = -1;
                    if (qSequence[i] != '-')
                    {
                        querySeqNum = qSeqStart + qCount;
                        qCount++;
                    }
                    if (hitSeqNum > 0 && querySeqNum > 0)
                    {
                        if (!seqPairHash.ContainsKey(hitSeqNum.ToString()))
                        {
                            seqPairHash.Add(hitSeqNum.ToString(), querySeqNum.ToString());
                        }
                    }
                }
            }
			return seqPairHash;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignInfoRow"></param>
        /// <returns></returns>
        private Dictionary<string,string> GetSeqMatchFromAlignedSeqNumbers (DataRow alignInfoRow)
        {
            string querySeqNumberString = alignInfoRow["QuerySeqNumbers"].ToString().TrimEnd (',');
            if (querySeqNumberString == "-")
            {
                return null;
            }
            string hitSeqNumberString = alignInfoRow["HitSeqNumbers"].ToString().TrimEnd(',');
            string[] querySeqNumbers = querySeqNumberString.Split(',');
            string[] hitSeqNumbers = hitSeqNumberString.Split(',');
            Dictionary<string, string> seqPairHash = new Dictionary<string,string> ();
            for (int i = 0; i < querySeqNumbers.Length; i++)
            {
                if ((querySeqNumbers[i] != "-" && hitSeqNumbers[i] != "-") &&
                    (querySeqNumbers[i] != "-1" && hitSeqNumbers[i] != "-1"))
                {
                    // in case the overlap of domain segments for those combined domains
                    if (! seqPairHash.ContainsKey(hitSeqNumbers[i])) 
                    {
                        seqPairHash.Add(hitSeqNumbers[i], querySeqNumbers[i]);
                    }
                }
            }
            return seqPairHash;
        }
		#endregion

		#endregion

		#region reverse superposition
		#region reverse the superposed chain
		/// <summary>
		/// reverse the residue sequence id after interface comparing
		/// </summary>
		/// <param name="interChains2"></param>
		/// <param name="alignInfoTable"></param>
		public void ReverseSupInterfaces (InterfaceChains interface1, InterfaceChains interface2, 
			DataTable alignInfoTable, bool isReverse)
		{
			DataRow alignRow = null;
			DataRow alignRow2 = null;
			
			int entityId11 = interface1.entityId1;
			AtomInfo[] chain21 = interface2.chain1;				
			int entityId21 = interface2.entityId1;
			
			int entityId12 = interface1.entityId2;
			AtomInfo[] chain22 = interface2.chain2;
			int entityId22 = interface2.entityId2;
			
			alignRow = GetAlignRow (alignInfoTable, entityId11, entityId21);
			if (alignRow != null)
			{	
				ReverseSupChain (chain21, alignRow);
			}
			if (entityId21 == entityId22 && entityId11 == entityId12)
			{
				ReverseSupChain (chain22, alignRow);
				interface2.seqDistHash = 
					ReverseSupInterface (interface2.seqDistHash, alignRow);
			}
			else
			{
				alignRow2 = GetAlignRow (alignInfoTable, entityId12, entityId22);
				if (alignRow2 != null)
				{	
					ReverseSupChain (chain22, alignRow2);
					interface2.seqDistHash = 
						ReverseSupInterface (interface2.seqDistHash, alignRow, alignRow2);
				}	
			}
			if (isReverse)
			{
				interface2.Reverse ();
			}
			interface2.ClearSeqResidueHash ();
		}
		/// <summary>
		/// reverse the residue sequence id after interface comparing
		/// </summary>
		/// <param name="interChains2"></param>
		/// <param name="alignInfoTable"></param>
		public void ReverseSupInterfaces (InterfaceChains[] interfaceChains2, DataTable alignInfoTable)
		{
			DataRow alignRow = null;
			DataRow alignRow2 = null;

			foreach (InterfaceChains interfaceChains in interfaceChains2)
			{
				AtomInfo[] chain1 = interfaceChains.chain1;				
				string asymChain1 = interfaceChains.firstSymOpString.Substring (0, interfaceChains.firstSymOpString.IndexOf ("_"));
				alignRow = GetAlignRow (alignInfoTable, asymChain1);
				ReverseSupChain (chain1, alignRow);

				AtomInfo[] chain2 = interfaceChains.chain2;
				string asymChain2 = interfaceChains.secondSymOpString.Substring (0, interfaceChains.secondSymOpString.IndexOf ("_"));
				if (asymChain1 != asymChain2)
				{
					alignRow2 = GetAlignRow (alignInfoTable, asymChain2);
					ReverseSupChain (chain2, alignRow2);
					interfaceChains.seqDistHash = 
						ReverseSupInterface (interfaceChains.seqDistHash, alignRow, alignRow2);
				}
				else
				{
					ReverseSupChain (chain2, alignRow);
					interfaceChains.seqDistHash = 
						ReverseSupInterface (interfaceChains.seqDistHash, alignRow);
				}
				interfaceChains.ClearSeqResidueHash ();
			}
		}

		/// <summary>
		/// reverse the residue sequence id after interface comparing
		/// </summary>
		/// <param name="interChains2"></param>
		/// <param name="alignInfoTable"></param>
		public void ReverseSupInterfaces (InterfaceChains interface2, DataTable alignInfoTable)
		{
			DataRow alignRow = null;
			DataRow alignRow2 = null;

				AtomInfo[] chain1 = interface2.chain1;				
				string asymChain1 = interface2.firstSymOpString.Substring (0, interface2.firstSymOpString.IndexOf ("_"));
				alignRow = GetAlignRow (alignInfoTable, asymChain1);
				ReverseSupChain (chain1, alignRow);

				AtomInfo[] chain2 = interface2.chain2;
				string asymChain2 = interface2.secondSymOpString.Substring (0, interface2.secondSymOpString.IndexOf ("_"));
				if (asymChain1 != asymChain2)
				{
					alignRow2 = GetAlignRow (alignInfoTable, asymChain2);
					ReverseSupChain (chain2, alignRow2);
					interface2.seqDistHash = 
						ReverseSupInterface (interface2.seqDistHash, alignRow, alignRow2);
				}
				else
				{
					ReverseSupChain (chain2, alignRow);
					interface2.seqDistHash = 
						ReverseSupInterface (interface2.seqDistHash, alignRow);
				}
			interface2.ClearSeqResidueHash ();
			
		}
		/// <summary>
		/// change the residue sequence numbers back to its original sequence id
		/// </summary>
		/// <param name="chain"></param>
		/// <param name="psiblastInfo"></param>
		protected void ReverseSupChain (AtomInfo[] chain, DataRow alignInfoRow)
		{
            Dictionary<string, string> seqPairHash = GetRevSeqMatch(alignInfoRow);
			if (seqPairHash == null)
			{
				return;
			}
			foreach (AtomInfo atom in chain)
			{
				if (seqPairHash.ContainsKey (atom.seqId))
				{
					atom.seqId = seqPairHash[atom.seqId].ToString ();
				}
				else
				{
					// change -seqid to original seqid
					atom.seqId = atom.seqId.TrimStart ('-');
				}
			}
		}
		#endregion

		#region reverse superposed interfaces
		/// <summary>
		/// reverse an interface with two same sequences chains
		/// </summary>
		/// <param name="atomPairHash"></param>
		/// <param name="alignInfoRow"></param>
        protected Dictionary<string, double> ReverseSupInterface(Dictionary<string, double> seqDistHash, DataRow alignInfoRow)
		{
            Dictionary<string, string> seqPairHash = GetRevSeqMatch(alignInfoRow);
			if (seqPairHash == null)
			{
				return seqDistHash;
			}
            Dictionary<string, double> newSeqDistHash = new Dictionary<string, double>();
			foreach (string seqPair in seqDistHash.Keys)
			{
				string newSeqPair = "";
				string[] seqIds = seqPair.Split ('_');
				if (seqPairHash.ContainsKey (seqIds[0]))
				{
					newSeqPair += seqPairHash[seqIds[0]].ToString ();
				}
				else
				{
					newSeqPair += seqIds[0].TrimStart ('-');
				}
				newSeqPair += "_";
				if (seqPairHash.ContainsKey (seqIds[1]))
				{
					newSeqPair += seqPairHash[seqIds[1]].ToString ();	
				}
				else
				{
					newSeqPair += seqIds[1].TrimStart ('-');;
				}
				//seqPair = newSeqPair;
				//		if (! newSeqDistHash.ContainsKey (newSeqPair))
				//		{
				newSeqDistHash.Add (newSeqPair, seqDistHash[seqPair]);
				//		}
			}
			return newSeqDistHash;
		}

		/// <summary>
		/// reverse an interface with two same sequences chains
		/// </summary>
		/// <param name="atomPairHash"></param>
		/// <param name="alignInfoRow"></param>
        protected Dictionary<string, double> ReverseSupInterface(Dictionary<string, double> seqDistHash, DataRow alignInfoRow1, DataRow alignInfoRow2)
		{
            Dictionary<string, string> seqPairHash1 = GetRevSeqMatch(alignInfoRow1);
            Dictionary<string, string> seqPairHash2 = GetRevSeqMatch(alignInfoRow2);
			if (seqPairHash1 == null || seqPairHash2 == null)
			{
				return seqDistHash;
			}
            Dictionary<string, double> newSeqDistHash = new Dictionary<string, double>();
			foreach (string seqPair in seqDistHash.Keys)
			{
				string newSeqPair = "";
				string[] seqIds = seqPair.Split ('_');
				if (seqPairHash1.ContainsKey (seqIds[0]))
				{
					newSeqPair += seqPairHash1[seqIds[0]].ToString ();
				}
				else
				{
					newSeqPair += seqIds[0].TrimStart ('-');
				}
				newSeqPair += "_";
				if (seqPairHash2.ContainsKey (seqIds[1]))
				{
					newSeqPair += seqPairHash2[seqIds[1]].ToString ();	
				}
				else
				{
					newSeqPair += seqIds[1].TrimStart ('-');;
				}
			//	seqPair = newSeqPair;
                newSeqDistHash.Add(newSeqPair, seqDistHash[seqPair]);
			}
			return newSeqDistHash;
		}
		#endregion

		#region reversed sequence id pair
		/// <summary>
		/// get reversed sequence ids
		/// </summary>
		/// <param name="alignInfoRow"></param>
		/// <returns></returns>
        private Dictionary<string, string> GetRevSeqMatch(DataRow alignInfoRow)
		{
			if (alignInfoRow == null)
			{
				return null;
			}
			// used to store the corresponding sequence pairs
			// key: hit sequence id, value: query sequence id
            Dictionary<string, string> seqPairHash = null;

            if (alignInfoRow.Table.Columns.Contains("QuerySeqNumbers"))
            {
                seqPairHash = GetRevSeqMatchFromAlignedSeqNumbers(alignInfoRow);
            }
            else
            {
                seqPairHash = new Dictionary<string,string> ();

                int hSeqStart = Convert.ToInt32(alignInfoRow["HitStart"].ToString());
                int qSeqStart = Convert.ToInt32(alignInfoRow["QueryStart"].ToString());
                int hSeqEnd = Convert.ToInt32(alignInfoRow["HitEnd"].ToString());
                int qSeqEnd = Convert.ToInt32(alignInfoRow["QueryEnd"].ToString());
                // same alignment length
                //  no gap, don't have to check the aligned sequences
                if ((qSeqEnd - qSeqStart) == (hSeqEnd - hSeqStart) &&
                    qSeqEnd - qSeqStart == 0)
                {
                    return null;
                }
                // have to check the aligned sequences

                int hCount = 0;
                int qCount = 0;
                int hitSeqNum = -1;
                int querySeqNum = -1;

                string qSequence = alignInfoRow["QuerySequence"].ToString();
                string hSequence = alignInfoRow["HitSequence"].ToString();

                for (int i = 0; i < qSequence.Length; i++)
                {
                    hitSeqNum = -1;
                    if (hSequence[i] != '-')
                    {
                        hitSeqNum = hSeqStart + hCount;
                        hCount++;
                    }
                    querySeqNum = -1;
                    if (qSequence[i] != '-')
                    {
                        querySeqNum = qSeqStart + qCount;
                        qCount++;
                    }
                    if (hitSeqNum > 0 && querySeqNum > 0)
                    {
                        seqPairHash.Add(querySeqNum.ToString(), hitSeqNum.ToString());
                    }
                }
            }
			return seqPairHash;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignInfoRow"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetRevSeqMatchFromAlignedSeqNumbers(DataRow alignInfoRow)
        {
            string querySeqNumberString = alignInfoRow["QuerySeqNumbers"].ToString();
            if (querySeqNumberString == "-")
            {
                return null;
            }
            string hitSeqNumberString = alignInfoRow["HitSeqNumbers"].ToString();
            string[] querySeqNumbers = querySeqNumberString.Split(',');
            string[] hitSeqNumbers = hitSeqNumberString.Split(',');
            Dictionary<string, string> revSeqPairHash = new Dictionary<string,string> ();
            for (int i = 0; i < querySeqNumbers.Length; i++)
            {
                if (querySeqNumbers[i] != "-" && hitSeqNumbers[i] != "-")
                {
                    revSeqPairHash.Add(querySeqNumbers[i], hitSeqNumbers[i]);
                }
            }
            return revSeqPairHash;
        }
		#endregion
		#endregion

		#region get alignment row
		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		private DataRow GetAlignRow (DataTable alignInfoTable, int entityId1, int entityId2)
		{
			DataRow[] alignRows = alignInfoTable.Select 
				(string.Format ("EntityID1 = '{0}' AND EntityID2 = '{1}'", entityId1, entityId2));
			if (alignRows.Length > 0)
			{
				return alignRows[0];
			}
			else
			{
				return null;
			}
		}
		/// <summary>
		/// get the alignment information row for the chain
		/// </summary>
		/// <param name="alignInfoTable"></param>
		/// <param name="asymId"></param>
		/// <returns></returns>
		private DataRow GetAlignRow (DataTable alignInfoTable, string asymId)
		{
			foreach (DataRow dRow in alignInfoTable.Rows)
			{
				string asymList = dRow["AsymChainList2"].ToString ();
				string[] asymChains = asymList.Split (' ');
				foreach (string asymChain in asymChains)
				{
					if (asymChain == asymId)
					{
						return dRow;
					}
				}
			}
			return null;
		}

		/// <summary>
		/// get the alignment information row for the chain
		/// </summary>
		/// <param name="alignInfoTable"></param>
		/// <param name="asymId"></param>
		/// <returns></returns>
		private DataRow GetAlignRow (DataTable alignInfoTable, string asymId1, string asymId2)
		{
			bool alignExist = false;
			foreach (DataRow dRow in alignInfoTable.Rows)
			{
				alignExist = false;
				string asymList1 = dRow["AsymChainList1"].ToString ();
				string[] asymChains1 = asymList1.Split (' ');
				foreach (string asymChain1 in asymChains1)
				{
					if (asymChain1 == asymId1)
					{
						alignExist = true;
					}
				}
				string asymList2 = dRow["AsymChainList2"].ToString ();
				string[] asymChains2 = asymList2.Split (' ');
				foreach (string asymChain2 in asymChains2)
				{
					if (asymChain2 == asymId2)
					{
						if (alignExist)
						{
							return dRow;
						}
					}
				}
			}
			return null;
		}
		#endregion
    }
}
