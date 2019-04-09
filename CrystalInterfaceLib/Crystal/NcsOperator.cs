using System;
using System.Xml.Serialization;

namespace CrystalInterfaceLib.Crystal
{
	/// <summary>
	/// Summary description for NcsOperator.
	/// </summary>
	public class NcsOperator
	{
		public NcsOperator()
		{
		}

		[XmlElement("Code")] public string code = "";
		[XmlAttribute("ID")] public int ncsId = -1;
		[XmlElement("NcsMatrix")] public Matrix ncsMatrix = new Matrix ();
	}
}
