using System;
using CrystalInterfaceLib.Contacts;

namespace CrystalInterfaceLib.DomainInterfaces
{
	/// <summary>
	/// defintion of domain-domain interface without atomic info.
	/// </summary>
	public class DomainInterfaceInfo : InterfaceInfo
    {
        #region member variables
        public int domainInterfaceId = -1;
		public long domainID1 = 0;
		public long domainID2 = 0;
        #endregion

        public DomainInterfaceInfo()
		{
		}

		public DomainInterfaceInfo (InterfaceInfo interfaceInfo)
		{
            this.pdbId = interfaceInfo.pdbId;
			this.interfaceId = interfaceInfo.interfaceId;
			this.firstSymOpString = interfaceInfo.firstSymOpString;
			this.secondSymOpString = interfaceInfo.secondSymOpString;
			this.entityId1 = interfaceInfo.entityId1;
			this.entityId2 = interfaceInfo.entityId2;
		}

		public DomainInterfaceInfo (DomainInterface domainInterface)
		{
            this.pdbId = domainInterface.pdbId;
			this.interfaceId = domainInterface.interfaceId;
			this.firstSymOpString = domainInterface.firstSymOpString;
			this.secondSymOpString = domainInterface.secondSymOpString;
			this.entityId1 = domainInterface.entityId1;
			this.entityId2 = domainInterface.entityId2;
			this.domainID1 = domainInterface.domainId1;
			this.domainID2 = domainInterface.domainId2;
			this.domainInterfaceId = domainInterface.domainInterfaceId;
		}
	}
}
