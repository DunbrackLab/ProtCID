using System;

namespace CrystalInterfaceLib.DomainInterfaces
{
	/// <summary>
	/// start position and end position of a range
	/// </summary>
	public class Range
	{
		public int startPos;
		public int endPos;

		public Range()
		{
		}

		public Range (int startpos, int endpos)
		{
			startPos = startpos;
			endPos = endpos;
		}
	}
}
