using System;

namespace DataCollectorLib.FatcatAlignment
{
	/// <summary>
	/// Summary description for AlignSeqInfo.
	/// </summary>
	public class AlignSeqInfo
	{
		public string pdbId = "";
		public string chainId = "";
        public string asymChainId = "";
		public string alignSequence = "";
		public int alignStart = -1;
		public int alignEnd = -1;

		public AlignSeqInfo()
		{
			
		}

		public string AlignInfoString 
		{
			get
			{
				string seqInfoString = pdbId + " " + chainId + " " + asymChainId + " " +
					alignStart.ToString () + " " + alignEnd.ToString () + " " +
					alignSequence;
				return seqInfoString;
			}
		}
	}
}
