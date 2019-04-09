using System;
using System.Xml;
using System.Xml.Serialization;

namespace CrystalInterfaceLib.Crystal
{
    public class EntityInfo
    {
        [XmlAttribute("EntityId")] public int entityId = -1;
        [XmlElement("Name")] public string name = "";
        [XmlElement("Type")] public string type = "";
        [XmlElement("OneLetterSeq")] public string oneLetterSeq = "";
        [XmlElement("ThreeLetterSeq")] public string threeLetterSeq = "";
        [XmlElement("AsymChains")] public string asymChains = "";
        [XmlElement("AuthorChains")]
        public string authorChains = "";
    }
}
