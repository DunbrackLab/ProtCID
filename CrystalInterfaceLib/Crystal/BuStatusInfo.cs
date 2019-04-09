using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace CrystalInterfaceLib.Crystal
{
    public class BuStatusInfo
    {
        [XmlAttribute("biol_id")]
        public string biolUnitId = "";

        [XmlElement("details")]
        public string details = "";

        [XmlElement("method_details")]
        public string method_details = "";

        [XmlElement("oligomeric_details")]
        public string oligomeric_details = "";
    }
}
