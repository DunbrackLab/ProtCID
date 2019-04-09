using System;
using System.Collections.Generic;
using System.Data;

namespace CrystalInterfaceLib.Crystal
{
	/// <summary>
	/// build a real asymmetric unit from non-crystalgraphic symmetry operators
	/// </summary>
	public class AsymUnitBuilder
	{
		private string chainLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
		public AsymUnitBuilder()
		{
        }

        #region build asymmetric unit from NCS
        /// <summary>
		/// build asymmetric unit from NCS
		/// </summary>
		/// <param name="thisEntryCrystal"></param>
		/// <returns></returns>
	//	public void BuildAsymUnitFromNcs (ref EntryCrystal thisEntryCrystal, string[] origAsymChains, out bool asuChanged)
        public void BuildAsymUnitFromNcs(ref EntryCrystal thisEntryCrystal, out bool asuChanged)
		{
			asuChanged = false;
	//		chainLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
	//		ChainAtoms[] chainAtomsList = new ChainAtoms [thisEntryCrystal.atomCat.ChainAtomList.Length];
			
			int genNcsNum = 0;
			foreach (NcsOperator ncsOp in thisEntryCrystal.ncsCat.NcsOperatorList)
			{
				if (ncsOp.code == "generate")
				{
					genNcsNum ++;
				}
			}
			if (genNcsNum == 0)
			{	
				return;
			}
			else if (genNcsNum * thisEntryCrystal.atomCat.ChainAtomList.Length > chainLetters.Length)
         /*   else if (genNcsNum > chainLetters.Length) */
			{
				return;
			}

            List<ChainAtoms> asuChainList = new List<ChainAtoms>();
		//	RemoveOrigAsuChainNames (thisEntryCrystal.atomCat.ChainAtomList, ref chainLetters);
		//	int ncsChainCount = 1;
			foreach (ChainAtoms chain in thisEntryCrystal.atomCat.ChainAtomList)
			{
			/*	if (chain.PolymerType != "polypeptide")
				{
					continue;
				}*/
				asuChainList.Add (chain);
			//	ncsChainCount = 1;
				foreach (NcsOperator ncsOp in thisEntryCrystal.ncsCat.NcsOperatorList)
				{
					if (ncsOp.code == "given")
					{
						continue;
					}
				
					AtomInfo[] ncsAtoms = ApplyNcsMatrix (chain.CartnAtoms, ncsOp.ncsMatrix);
					if (DoesChainExist (ncsAtoms, thisEntryCrystal.atomCat.ChainAtomList))
					{
						continue;
					}
					asuChanged = true;
					ChainAtoms newChain = new ChainAtoms (chain);
					newChain.CartnAtoms = ncsAtoms;
				//	newChain.AsymChain = chain.AsymChain + chainLetters[ncsChainCount].ToString ();
				//	newChain.AsymChain =GetAsymChainNameForNcsChain (ref ncsChainCount, chain.AsymChain, origAsymChains);
                    // name the new chain with original asymmetric chain + a number, 
                    // this should distinguish the original asymmetri chains with these regenerated chains
              //      newChain.AsymChain = chain.AsymChain + ncsChainCount.ToString(); 
                    newChain.AsymChain = chain.AsymChain + ncsOp.ncsId.ToString(); 
             //       ncsChainCount ++;
					asuChainList.Add (newChain);
				}
			}
            thisEntryCrystal.atomCat.ChainAtomList = asuChainList.ToArray ();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="atoms"></param>
		/// <param name="ncsMatrix"></param>
		/// <returns></returns>
		private AtomInfo[] ApplyNcsMatrix (AtomInfo[] atoms, Matrix ncsMatrix)
		{
			AtomInfo[] newAtoms = new AtomInfo [atoms.Length];
			int i = 0;
			foreach (AtomInfo origAtom in atoms)
			{
				newAtoms[i] = (AtomInfo)origAtom.Clone ();
				newAtoms[i].xyz = ncsMatrix * origAtom.xyz;
				i ++;
			}
			return newAtoms;
		}

		/// <summary>
		/// are two chains same
		/// </summary>
		/// <param name="chainAtoms1"></param>
		/// <param name="chainAtoms2"></param>
		/// <returns></returns>
		private bool DoesChainExist (AtomInfo[] chainAtoms1, ChainAtoms[] chains)
		{
			int count = 0;
			bool isChainExist = false;
			foreach (ChainAtoms chain  in chains)
			{
				if (isChainExist)
				{
					break;
				}
				foreach (AtomInfo atom1 in chainAtoms1)
				{
					if (count == 10)
					{
						isChainExist = true;
						break;
					}
					foreach (AtomInfo atom2 in chain.CartnAtoms)
					{
						if (atom1.atomId == atom2.atomId)
						{
							double dist = atom1 - atom2;
							if (double.Equals (dist, 0.0))
							{
								count ++;
								break;
							}

						}
					}
				}
			}
			return isChainExist;
		}

		private void RemoveOrigAsuChainNames (ChainAtoms[] chains, ref string chainLetters)
		{
			foreach (ChainAtoms chain in chains)
			{
				int chainIdx = chainLetters.IndexOf (chain.AsymChain);
				chainLetters = chainLetters.Remove (chainIdx, 1);
			}
        }
        #endregion

        #region generate asymmetric unit for multi-chain domain
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mergeChains"></param>
        /// <param name="chainDomainId"></param>
        /// <param name="domainDefTable"></param>
        private void MergeMultiChainToOneChain(ChainAtoms[] mergeChains, int chainDomainId, DataTable domainDefTable)
        {
            DataRow[] domainRows = domainDefTable.Select(string.Format ("ChainDomainID = '{0}'", chainDomainId), "HmmStart ASC");
            string asymChain = "";
            foreach (DataRow domainRow in domainRows)
            {
                asymChain = domainRow["AsymChain"].ToString().TrimEnd();

            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainDefTable"></param>
        /// <returns></returns>
        private Dictionary<int, List<string>> GetDomainChainHash(DataTable domainDefTable)
        {
            Dictionary<int, List<string>> domainChainHash = new Dictionary<int,List<string>> ();
            int chainDomainId = 0;
            string asymChain = "";
            foreach (DataRow domainRow in domainDefTable.Rows)
            {
                chainDomainId = Convert.ToInt32(domainRow["ChainDomainID"].ToString());
                asymChain = domainRow["AsymChain"].ToString().TrimEnd();
                if (domainChainHash.ContainsKey(chainDomainId))
                {
                    domainChainHash[chainDomainId].Add(asymChain);
                }
                else
                {
                    List<string> chainList = new List<string> ();
                    chainList.Add(asymChain);
                    domainChainHash.Add(chainDomainId, chainList);
                }
            }
            return domainChainHash;
        }
        #endregion
    }
}
