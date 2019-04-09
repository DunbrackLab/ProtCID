using System;
using System.Collections;
using XtalLib.Contacts;
using XtalLib.KDops;
using XtalLib.Settings;
using XtalLib.Crystal;
using XtalLib.StructureComp;


namespace BuInterfacesLib.BuInterfaces
{
	/// <summary>
	/// Interfaces from PISA assemblies
	/// </summary>
	public class PisaInterfaces
	{
//		private InterfacesComp interfaceComp = new InterfacesComp (); 
		private PisaBuGenerator pisaBuGenerator = new PisaBuGenerator ();

		public PisaInterfaces()
		{
		}

		#region calculate interfaces from one pisa multimer
		/// <summary>
		/// interfaces from pisa multimers
		/// </summary>
		/// <param name="pdbId"></param>
		/// <returns></returns>
		public Hashtable GetInterfacesFromPisaMultimers (string pdbId)	
		{
			Hashtable pisaBuInterfacesHash = new Hashtable ();
			Hashtable pisaMultimersHash = pisaBuGenerator.BuildPisaAssemblies (pdbId);
			foreach (int assemblyId in pisaMultimersHash.Keys)
			{
				InterfaceChains[] buInterfaces = GetInterfacesFromMultimer ((Hashtable)pisaMultimersHash[assemblyId]);
				pisaBuInterfacesHash.Add (assemblyId, buInterfaces);
			}
			return pisaBuInterfacesHash;
		}

		/// <summary>
		/// interfaces from pisa multimers
		/// </summary>
		/// <param name="pdbId"></param>
		/// <returns></returns>
		public InterfaceChains[] GetInterfacesFromPisaMultimers (string pdbId, string buId)	
		{
			Hashtable pisaMultimerHash = pisaBuGenerator.BuildOnePisaAssembly (pdbId, buId);
			
			InterfaceChains[] buInterfaces = GetInterfacesFromMultimer (pisaMultimerHash);
			return buInterfaces;
		}
		/// <summary>
		/// get chain contacts (interfaces) in the biological unit
		/// </summary>
		/// <param name="biolUnit">key: chainid, value: atom list</param>
		/// <returns></returns>
		public InterfaceChains[] GetInterfacesFromMultimer (Hashtable pisaMultimerHash)
		{
			// build trees for the biological unit
			Hashtable buChainTreesHash = BuildBVtreesForMultimer (pisaMultimerHash);

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
						interfaceChains.seqDistHash = contactInfo.GetBbDistHash ();
						interfaceChains.seqContactHash = contactInfo.GetContactsHash ();
						interChainsList.Add (interfaceChains);	
						//chainContact = null;
					}
				}
			}
			InterfaceChains[] interChainArray = new InterfaceChains[interChainsList.Count];
			interChainsList.CopyTo (interChainArray);
/*			InterfacePairInfo[] interfacePairs = 
				interfaceComp.FindUniqueInterfacesWithinCrystal (ref interChainArray);*/
			
			// return unique interactive chains
			return interChainArray;
		}

		/// <summary>
		/// build BVtrees for chains in a biological unit
		/// </summary>
		/// <param name="biolUnit"></param>
		/// <returns></returns>
		private Hashtable BuildBVtreesForMultimer (Hashtable pisaMultimerHash)
		{
			Hashtable chainTreesHash = new Hashtable ();
			// for each chain in the biological unit
			// build BVtree
			foreach (object chainAndSymOp in pisaMultimerHash.Keys)
			{
				BVTree chainTree = new BVTree ();
				chainTree.BuildBVTree ((AtomInfo[])pisaMultimerHash[chainAndSymOp], 
					AppSettings.parameters.kDopsParam.bvTreeMethod, true);
				chainTreesHash.Add (chainAndSymOp, chainTree);
			}
			return chainTreesHash;
		}
		#endregion
	}
}
