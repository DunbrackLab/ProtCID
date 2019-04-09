using System;
using System.Xml;
using System.Xml.Serialization;

namespace CrystalInterfaceLib.Crystal
{
	/// <summary>
	/// Summary description for CrystalInfo.
	/// </summary>
	public class CrystalInfo
	{
		[XmlAttribute("SpaceGroup")] public string spaceGroup = "";
		[XmlElement("Length_a")] public double length_a = 0.0;
		[XmlElement("Length_b")] public double length_b = 0.0;
		[XmlElement("Length_c")] public double length_c = 0.0;
		[XmlElement("Angle_alpha")] public double angle_alpha = 0.0;
		[XmlElement("Angle_beta")] public double angle_beta = 0.0;
		[XmlElement("Angle_gamma")] public double angle_gamma = 0.0;
		[XmlElement("Z_PDB")] public int zpdb = 0;

		public CrystalInfo()
		{
		}
	}
}
