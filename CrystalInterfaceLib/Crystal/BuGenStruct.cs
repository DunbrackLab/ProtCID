using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace CrystalInterfaceLib.Crystal
{
    /// <summary>
    /// Generation info for a bu
    /// </summary>
    public class BuGenStruct
    {
        [XmlAttribute("BiolID")]
        public string biolUnitId = "";
        [XmlAttribute("AsymID")]
        public string asymId = "";
        [XmlAttribute("SymmetryID")]
        public string symOperId = "";
        [XmlAttribute("Symmetry")]
        public string symmetryString = "";
        [XmlElement("FullSymmetry")]
        public string symmetryMatrix = "";

        public BuGenStruct()
        {
        }

        public BuGenStruct(string buId, string asymChain, string symOpId, string symOpStr, string symMatrix)
        {
            this.biolUnitId = buId;
            this.asymId = asymChain;
            this.symOperId = symOpId;
            this.symmetryString = symOpStr;
            this.symmetryMatrix = symMatrix;
        }

        public BuGenStruct(string buId, string asymChain, string symOpId)
        {
            this.biolUnitId = buId;
            this.asymId = asymChain;
            this.symOperId = symOpId;
        }
    }

    public class BuSymOperInfo
    {
        [XmlAttribute("SymOperID")]
        public string symOperId = "";
        [XmlElement("SymmetryString")]
        public string symmetryString = "";
        [XmlElement("SymmetryMatrix")]
        public string symmetryMatrix = "";
    }
}
