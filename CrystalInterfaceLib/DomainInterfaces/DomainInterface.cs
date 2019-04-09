using System;
using System.Collections.Generic;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Crystal;

namespace CrystalInterfaceLib.DomainInterfaces
{
	/// <summary>
	/// definition of domain-domain interface
	/// </summary>
	public class DomainInterface : InterfaceChains
    {
        #region member variables
        public long domainId1 = 0;
		public long domainId2 = 0;
        public int chainDomainId1 = 0;
        public int chainDomainId2 = 0;
		public string familyCode1 = "";
		public string familyCode2 = "";
		public int domainInterfaceId = 0;
        public string remark = "";
        #endregion

        public DomainInterface()
		{
			
		}

		public DomainInterface (InterfaceChains chainInterface)
		{
			this.interfaceId = chainInterface.interfaceId;
			this.firstSymOpString = chainInterface.firstSymOpString;
			this.secondSymOpString = chainInterface.secondSymOpString;
			this.entityId1 = chainInterface.entityId1;
			this.entityId2 = chainInterface.entityId2;
			this.chain1 = (AtomInfo[])chainInterface.chain1.Clone ();
			this.chain2 = (AtomInfo[])chainInterface.chain2.Clone ();
            this.seqDistHash = new Dictionary<string, double> (chainInterface.seqDistHash);
		}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="extDomainInterface"></param>
        /// <param name="deepCopy"></param>
        public DomainInterface(DomainInterface extDomainInterface, bool deepCopy)
        {
            this.domainId1 = extDomainInterface.domainId1;
            this.domainId2 = extDomainInterface.domainId2;
            this.domainInterfaceId = extDomainInterface.domainInterfaceId;
            this.familyCode1 = extDomainInterface.familyCode1;
            this.familyCode2 = extDomainInterface.familyCode2;
            this.remark = extDomainInterface.remark;

            this.interfaceId = extDomainInterface.interfaceId;
            this.firstSymOpString = extDomainInterface.firstSymOpString;
            this.secondSymOpString = extDomainInterface.secondSymOpString;
            this.entityId1 = extDomainInterface.entityId1;
            this.entityId2 = extDomainInterface.entityId2;

            if (deepCopy)
            {
                DeepCopyChains(extDomainInterface.chain1, 1);
                DeepCopyChains(extDomainInterface.chain2, 2);
                DeepCopySeqDistHash(extDomainInterface.seqDistHash);
                DeepCopySeqContactHash(extDomainInterface.seqContactHash);
            }
            else
            {
                this.chain1 = (AtomInfo[])extDomainInterface.chain1.Clone();
                this.chain2 = (AtomInfo[])extDomainInterface.chain2.Clone();
                this.seqDistHash = new Dictionary<string, double>(extDomainInterface.seqDistHash);
                this.seqContactHash = new Dictionary<string, Contact>(extDomainInterface.seqContactHash);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public new void Reverse()
        {
            base.Reverse();

            long tempDomainId = domainId1;
            this.domainId1 = this.domainId2;
            this.domainId2 = tempDomainId;

            int tempChainDomainId = chainDomainId1;
            this.chainDomainId1 = this.chainDomainId2;
            this.chainDomainId2 = tempChainDomainId;

            string tempPfam = familyCode1;
            this.familyCode1 = this.familyCode2;
            this.familyCode2 = tempPfam;
        }
	}
}
