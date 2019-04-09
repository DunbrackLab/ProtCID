using System;
using CrystalInterfaceLib.Contacts;

namespace CrystalInterfaceLib.StructureComp
{
	/// <summary>
	/// information for two similar interfaces
	/// </summary>
	public class InterfacePairInfo
	{
		public InterfaceInfo interfaceInfo1 = new InterfaceInfo ();
		public InterfaceInfo interfaceInfo2 = new InterfaceInfo ();
		public bool sameAsym = false;
        public bool isInterface2Reversed = false;
		public double qScore = -1.0;
		public double identity = -1.0;

		public InterfacePairInfo()
		{
		}

		public InterfacePairInfo (InterfaceInfo info1, InterfaceInfo info2)
		{
			interfaceInfo1 = info1;
			interfaceInfo2 = info2;
		}
	}
}
