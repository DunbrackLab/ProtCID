using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BuQueryLib
{
    public class AsymUnitInfo
    {
        public Dictionary<string, int> asymUnit_asym;
        public Dictionary<string, int> asymUnit_entityHash;
        public Dictionary<string, int> asymUnit_authorchainHash;
        public Dictionary<string, int> asymUnit_abcHash;
        public string hasLigands;
        public string resolution;

        public AsymUnitInfo()
        {
            asymUnit_asym = new Dictionary<string,int> ();
            asymUnit_entityHash = new Dictionary<string, int>();
            asymUnit_authorchainHash = new Dictionary<string, int>();
            asymUnit_abcHash = new Dictionary<string, int>();
            hasLigands = "no";
        }
    }
}
