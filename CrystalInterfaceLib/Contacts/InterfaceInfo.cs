using System;

namespace CrystalInterfaceLib.Contacts
{
	/// <summary>
	/// symmetry information about two interactive chains
	/// </summary>
	public class InterfaceInfo
	{
        public string pdbId = "";
		public int interfaceId = 0;
		// chain_symOpNo_symOp
		public string firstSymOpString = "";
		public string secondSymOpString = "";
		public int entityId1 = -1;
		public int entityId2 = -1;
        public double surfaceArea = -1;

		public InterfaceInfo()
		{
		}

		public InterfaceInfo(string symOpStr1, string symOpStr2)
		{
			firstSymOpString = symOpStr1;
			secondSymOpString = symOpStr2;
		}

		public InterfaceInfo(int interChainId, string symOpStr1, string symOpStr2)
		{
			interfaceId = interChainId;
			firstSymOpString = symOpStr1;
			secondSymOpString = symOpStr2;
		}

		public InterfaceInfo(InterfaceInfo extInterChainInfo)
		{
			interfaceId = extInterChainInfo.interfaceId;
			firstSymOpString = extInterChainInfo.firstSymOpString;
			secondSymOpString = extInterChainInfo.secondSymOpString;
		}

	/*	public static bool operator == (InterfaceInfo info1, InterfaceInfo info2)
		{
            if (info1 == null && info2 == null)
            {
                return true;
            }
            else if (info1 == null || info2 == null)
            {
                return false;
            }
            else
            {
                return (info1.firstSymOpString == info2.firstSymOpString &&
                    info1.secondSymOpString == info2.secondSymOpString);
            }
		}

		public static bool operator != (InterfaceInfo info1, InterfaceInfo info2)
		{
            if (info1 == null && info2 == null)
            {
                return false;
            }
            else if (info1 == null || info2 == null)
            {
                return true;
            }
            else
            {
                return (info1.firstSymOpString != info2.firstSymOpString ||
                    info1.secondSymOpString != info2.secondSymOpString);
            }
		}
        */
        /// <summary>
        /// override equal function so that operator == and != override in the class
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
	}
}
