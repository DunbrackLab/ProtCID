using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BuQueryLib
{
    public class BiolUnitInfo
    {
        public Dictionary<string, int> biolUnit_asym;
 //       public Dictionary<string, int> biolUnit_asymHash;
        public Dictionary<string, int> biolUnit_entityHash;
        public Dictionary<string, int> biolUnit_authorchainHash;
    //    public Dictionary<string, int> biolUnit_pqsHash;
        public Dictionary<string, string> biolUnit_namesHash;
        public string hasDNA;
        public string hasRNA;

        // entry level
        public BiolUnitInfo()
        {
            biolUnit_asym = new Dictionary<string, int> ();
    //        biolUnit_asymHash = new Dictionary<string,int> ();
            biolUnit_entityHash = new Dictionary<string,int> ();
            biolUnit_authorchainHash = new Dictionary<string,int> ();
   //         biolUnit_pqsHash = new Dictionary<string,int> ();
            biolUnit_namesHash = new Dictionary<string,string> ();
            hasDNA = "no";
            hasRNA = "no";
        }
    }
}
