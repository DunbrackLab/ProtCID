using System;
using CrystalInterfaceLib.Contacts;

namespace CrystalInterfaceLib.DomainInterfaces
{
	/// <summary>
	/// the pair info for two domain-domain interfaces.
	/// </summary>
	public class DomainInterfacePairInfo
    {
        #region member variables
        public DomainInterfaceInfo interfaceInfo1 = null;
		public DomainInterfaceInfo interfaceInfo2 = null;
		public double qScore = -1.0;
        public double identity = -1.0;
        public bool isInterface2Reversed = false;
        #endregion

        public DomainInterfacePairInfo()
		{
			interfaceInfo1 = new DomainInterfaceInfo ();
			interfaceInfo2 = new DomainInterfaceInfo ();
		}

		public DomainInterfacePairInfo (DomainInterfaceInfo info1, DomainInterfaceInfo info2)
		{
			interfaceInfo1 = info1;
			interfaceInfo2 = info2;
		}

		public DomainInterfacePairInfo (InterfaceInfo info1, InterfaceInfo info2)
		{
			interfaceInfo1 = new DomainInterfaceInfo (info1);
			interfaceInfo2 = new DomainInterfaceInfo (info2);
		}
	}
}
