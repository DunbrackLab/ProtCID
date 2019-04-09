using System;

namespace DataCollectorLib.Pfam
{
	/// <summary>
	/// Summary description for PfamAlignInfo.
	/// </summary>
	public class PfamAlignInfo
	{
		#region public member variables
        public long domainId = 0;
        public string pfamAcc = "";
        public string pfamId = "";
        public string type = "";
        public int startPos = -1;
        public int endPos = -1;
        public int hmmStartPos = -1;
        public int hmmEndPos = -1;
        public int alignStartPos = -1;
        public int alignEndPos = -1;
        public double iEvalue = -1.0;
        public double cEvalue = -1.0;
        public double bitScore = -1.0;
        public string mode = "";
        public string pfamClass = "";
		#endregion

		public PfamAlignInfo()
		{
			//
			// TODO: Add constructor logic here
			//
		}
	}
}
