using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace CrystalInterfaceLib.Crystal
{
	/// <summary>
	/// crystal data from PDB XML
	/// </summary>
	[XmlRoot("CrystalInfo")]
	public class EntryCrystal
	{
		private string pdbId = "";
        [XmlElement("EntityCategory")]
        public EntityInfoCategory entityCat = new EntityInfoCategory();
		[XmlElement("BuGenCategory")] public BuGenCategory buGenCat = new BuGenCategory ();
		[XmlElement("Crystal1")] public CrystalInfo crystal1 = new CrystalInfo();
		[XmlElement("ScaleCategory")] public ScaleCategory scaleCat = new ScaleCategory ();
		[XmlElement("NcsCategory")] public NcsCategory ncsCat = new NcsCategory (); 
		[XmlElement("AtomCategory")] public AtomCategory atomCat = new AtomCategory ();
        
		public EntryCrystal()
		{
		}

		public EntryCrystal(string pdbId)
		{
			this.pdbId = pdbId;
		}

		[XmlAttribute("Entry")]
		public string PdbId 
		{
			get 
			{
				return pdbId;
			}
			set
			{
				pdbId = value;
			}
		}
	}

	#region Biological Unit Generate Matrix
	/// <summary>
	/// category used to generate a biological unit
    /// // this follows PDB XML schema version 3.2 
    /// (http://pdbml.pdb.org/schema/pdbx-v32-v1.0667.xsd )
	/// </summary>
	public class BuGenCategory
	{
        List<BuStatusInfo> buStatusInfoList = new List<BuStatusInfo> ();
		List<BuGenStruct> buGenStructList = new List<BuGenStruct> ();
		List<SymOpMatrix> buSymmetryMatrixList = new List<SymOpMatrix> ();

        [XmlElement("BuStatusInfo")]
        public BuStatusInfo[] BuStatusInfoList
        {
            get
            {
                return buStatusInfoList.ToArray ();
            }
            set
            {
                if (value == null) return;
                BuStatusInfo[] itemList = (BuStatusInfo[])value;
                buStatusInfoList.Clear();
                buStatusInfoList.AddRange(itemList);
            }
        }

		[XmlElement("BuGenStruct")]
		public BuGenStruct[] BuGenStructList
		{
			get
			{				
                return buGenStructList.ToArray ();
			}
			set
			{
				if (value == null) return;
				BuGenStruct[] itemList = (BuGenStruct[]) value;
				buGenStructList.Clear ();
				buGenStructList.AddRange (itemList);
			}
		}

		[XmlElement("BuGenSymmetryMatrix")]
        public SymOpMatrix[] BuSymmetryMatrixList
		{
			get
			{
                return buSymmetryMatrixList.ToArray ();
			}
			set
			{
				if (value == null) return;
                SymOpMatrix[] itemList = (SymOpMatrix[])value;
                buSymmetryMatrixList.Clear();
                buSymmetryMatrixList.AddRange(itemList);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="buGenStruct"></param>
        public void AddBuGenStruct (BuGenStruct buGenStruct)
		{
			buGenStructList.Add (buGenStruct);
		}

        /// <summary>
        /// find the symmetry chains and operators to generate this biological unit
        /// </summary>
        /// <param name="biolUnitId"></param>
        /// <returns></returns>
        public BuGenStruct FindBuGenStruct(string biolUnitId)
        {
            foreach (BuGenStruct buGenStruct in buGenStructList)
            {
                if (buGenStruct.biolUnitId == biolUnitId)
                {
                    return buGenStruct;
                }
            }
            return null;
        }
		/// <summary>
		/// 
		/// </summary>
		/// <param name="symOpMatrix"></param>
        public void AddBuSymMatrix(SymOpMatrix symOpMatrix)
		{
            buSymmetryMatrixList.Add(symOpMatrix);
		}

        /// <summary>
        /// find the symmetry operator matrix for the specific symmetry operator id
        /// </summary>
        /// <param name="symOperId"></param>
        /// <returns></returns>
        public SymOpMatrix FindSymOpMatrix(string symOperId)
        {
            foreach (SymOpMatrix symOpMatrix in buSymmetryMatrixList)
            {
                if (symOpMatrix.symmetryOpNum == symOperId)
                {
                    return symOpMatrix;
                }
            }
            return null;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="buStatusInfo"></param>
        public void AddBuStatusInfo(BuStatusInfo buStatusInfo)
        {
            buStatusInfoList.Add(buStatusInfo);
        }

        /// <summary>
        /// the status info for the biological unit
        /// </summary>
        /// <param name="biolUnitId"></param>
        /// <returns></returns>
        public BuStatusInfo FindBuStatusInfo(string biolUnitId)
        {
            foreach (BuStatusInfo buStatusInfo in buStatusInfoList)
            {
                if (buStatusInfo.biolUnitId == biolUnitId)
                {
                    return buStatusInfo;
                }
            }
            return null;
        }

	}

	#endregion

	#region atom category
	/// <summary>
	/// category for atoms of each asymmetric chains
	/// </summary>
	public class AtomCategory
	{
		internal List<ChainAtoms> chainAtomsList = new List<ChainAtoms> ();

		[XmlElement("AsymmetryChainAtoms")]
		public ChainAtoms[] ChainAtomList
		{
			get
			{
                return chainAtomsList.ToArray ();
			}
			set
			{
				if (value == null) return;
				chainAtomsList.Clear ();
				chainAtomsList.AddRange ((ChainAtoms[])value);
			}
		}

		/// <summary>
		/// chains and their Calpha atoms
		/// </summary>
		/// <returns></returns>
		public ChainAtoms[] CalphaAtomList ()
		{
			ChainAtoms[] calphaChainList = new ChainAtoms [chainAtomsList.Count];
			for (int i = 0; i < chainAtomsList.Count; i ++)
			{
				ChainAtoms chainCalphaAtoms = new ChainAtoms ();
				chainCalphaAtoms.CartnAtoms = ((ChainAtoms)chainAtomsList[i]).CalphaAtoms();
				chainCalphaAtoms.AsymChain = ((ChainAtoms)chainAtomsList[i]).AsymChain;
				chainCalphaAtoms.EntityID = ((ChainAtoms)chainAtomsList[i]).EntityID;
				chainCalphaAtoms.PolymerType = ((ChainAtoms)chainAtomsList[i]).PolymerType;
				calphaChainList[i] = chainCalphaAtoms;
			}
			return calphaChainList;
		}

		/// <summary>
		/// chains and their Cbeta atoms
		/// </summary>
		/// <returns></returns>
		public ChainAtoms[] CbetaAtomList ()
		{
			ChainAtoms[] cbetaChainList = new ChainAtoms [chainAtomsList.Count];
			for (int i = 0; i < chainAtomsList.Count; i ++)
			{
				ChainAtoms chainCbetaAtoms = new ChainAtoms ();
				chainCbetaAtoms.CartnAtoms = ((ChainAtoms)chainAtomsList[i]).CbetaAtoms ();
				chainCbetaAtoms.AsymChain = ((ChainAtoms)chainAtomsList[i]).AsymChain;
				chainCbetaAtoms.EntityID = ((ChainAtoms)chainAtomsList[i]).EntityID;
				chainCbetaAtoms.PolymerType = ((ChainAtoms)chainAtomsList[i]).PolymerType;
				cbetaChainList[i] = chainCbetaAtoms;
			}
			return cbetaChainList;
		}

		/// <summary>
		/// chains and their Calpha and Cbeta atoms
		/// </summary>
		/// <returns></returns>
		public ChainAtoms[] CalphaCbetaAtomList ()
		{
			ChainAtoms[] calphaCbetaChainList = new ChainAtoms [chainAtomsList.Count];
			for (int i = 0; i < chainAtomsList.Count; i ++)
			{
				ChainAtoms chainCalphaCbetaAtoms = new ChainAtoms ();
				chainCalphaCbetaAtoms.CartnAtoms = ((ChainAtoms)chainAtomsList[i]).CalphaCbetaAtoms ();
				chainCalphaCbetaAtoms.AsymChain = ((ChainAtoms)chainAtomsList[i]).AsymChain;
				chainCalphaCbetaAtoms.EntityID = ((ChainAtoms)chainAtomsList[i]).EntityID;
				chainCalphaCbetaAtoms.PolymerType = ((ChainAtoms)chainAtomsList[i]).PolymerType;
				calphaCbetaChainList[i] = chainCalphaCbetaAtoms;
			}
			return calphaCbetaChainList;
		}

		/// <summary>
		/// chains and their backbone atoms
		/// </summary>
		public ChainAtoms[] BackboneAtomList ()
		{
			ChainAtoms[] backboneChainList = new ChainAtoms [chainAtomsList.Count];
			for (int i = 0; i < chainAtomsList.Count; i ++)
			{
				ChainAtoms backboneChainAtoms = new ChainAtoms ();
				backboneChainAtoms.CartnAtoms = ((ChainAtoms)chainAtomsList[i]).BackboneAtoms ();
				backboneChainAtoms.AsymChain = ((ChainAtoms)chainAtomsList[i]).AsymChain;
				backboneChainAtoms.EntityID = ((ChainAtoms)chainAtomsList[i]).EntityID;
				backboneChainAtoms.PolymerType = ((ChainAtoms)chainAtomsList[i]).PolymerType;
				backboneChainList[i] = backboneChainAtoms;
			}
			return backboneChainList;			
		}

		/// <summary>
		/// add asymmetry chain
		/// </summary>
		/// <param name="chainAtoms"></param>
		public void AddChainAtoms(ChainAtoms chainAtoms)
		{
			chainAtomsList.Add (chainAtoms);
		}

        /// <summary>
        /// if one chain only contains c-alpha residues, it is a bad structure
        /// return true;
        /// otherwise return false
        /// </summary>
        /// <returns></returns>
        public bool IsOnlyCalphaChains()
        {
            foreach (ChainAtoms chain in chainAtomsList)
            {
                if (chain.IsProtChainOnlyContainCalpha())
                {
                    return true;
                }
            }
            return false;
        }
	}
	#endregion

	#region scale matrix
	/// <summary>
	/// scale matrix from PDB format file
	/// e.g. pdbxxxx.ent.Z
	/// </summary>
	public class ScaleCategory
	{
		internal Matrix scaleMatrix = new Matrix ();
		[XmlElement("Scale")]
		public Matrix ScaleMatrix
		{
			get
			{
				return scaleMatrix;
			}
			set
			{
				scaleMatrix = value;
			}
		}

	}
	#endregion

	#region NcsCategory
	public class NcsCategory
	{
		private List<NcsOperator> ncsOperatorList = new List<NcsOperator> ();

		[XmlElement("NcsOperators")]
		public NcsOperator[] NcsOperatorList
		{
			get
			{
                return ncsOperatorList.ToArray ();
			}
			set
			{
				if (value == null) return;
				NcsOperator[] ncsOperators = (NcsOperator[])value;
				ncsOperatorList.Clear ();
				ncsOperatorList.AddRange (ncsOperators);
			}
		}

		public void Add (NcsOperator ncsOp)
		{
			ncsOperatorList.Add (ncsOp);
		}
	}
	#endregion

    #region entity sequences
    public class EntityInfoCategory
    {
        private List<EntityInfo> entityInfoList = new List<EntityInfo> ();

        [XmlElement("EntityInfos")]
        public EntityInfo[] EntityInfoList
        {
            get
            {
                return entityInfoList.ToArray ();
            }
            set
            {
                if (value == null) return;
                EntityInfo[] entityInfos = (EntityInfo[])value;
                entityInfoList.Clear();
                entityInfoList.AddRange(entityInfos);
            }
        }

        public void Add(EntityInfo entityInfo)
        {
            entityInfoList.Add(entityInfo);
        }
    }
    #endregion
}
