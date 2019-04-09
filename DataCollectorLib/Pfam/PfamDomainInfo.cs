using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XtalLib.Crystal;

namespace DataCollectorLib.Pfam
{

    public class PfamDomainInfo
    {
        public string pdbId = "";
        public string pfamId = "";
        public string pfamAcc = "";
        public string domainId = "";
        public DomainSegmentInfo[] segmentInfos = null;
    }


    public class DomainSegmentInfo
    {
        public int entityId = 0;
        public string asymChain = "";
        public string authChain = "";
        public int seqStart = 0;
        public int seqEnd = 0;
        public int alignStart = 0;
        public int alignEnd = 0;
        public int hmmStart = 0;
        public int hmmEnd = 0;
        public int fileStart = 0;
        public int fileEnd = 0;
        public AtomInfo[] atoms = null;
        public string[] threeLetterResidues = null;
    }
}
