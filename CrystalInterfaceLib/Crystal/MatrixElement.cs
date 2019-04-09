using System;
using System.Xml;
using System.Xml.Serialization;

namespace CrystalInterfaceLib.Crystal
{
	/// <summary>
	/// Summary description for MatrixElement.
	/// </summary>
	public class MatrixElement : ICloneable
	{
		[XmlAttribute("DimID")] public int dimId = -1;
		[XmlElement("A")] public double a = 0.0;
		[XmlElement("B")] public double b = 0.0;
		[XmlElement("C")] public double c = 0.0;
		[XmlElement("Vector")] public double vector = 0.0;

		public MatrixElement()
		{
		}

		public Object Clone()
		{
			MatrixElement newElement = new MatrixElement ();
			newElement.dimId = this.dimId;
			newElement.a = this.a;
			newElement.b = this.b;
			newElement.c = this.c;
			newElement.vector = this.vector;
			return newElement;
		}
	}
}
