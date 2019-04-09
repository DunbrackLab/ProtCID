using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using CrystalInterfaceLib.KDops;

namespace CrystalInterfaceLib.Crystal
{
	public class Coordinate
	{
		[XmlElement("X")] public double X = 0.0;
		[XmlElement("Y")] public double Y = 0.0;
		[XmlElement("Z")] public double Z = 0.0;

		public Coordinate ()
		{
		}

		public Coordinate(double xx, double yy, double zz)
		{
			X = xx;
			Y = yy;
			Z = zz;
		}

		internal Coordinate Scale(Matrix scaleMatrix)
		{
			Coordinate scaledCoordinate = new Coordinate ();
			scaledCoordinate.X = scaleMatrix.Value(0, 0) * this.X + scaleMatrix.Value(0, 1) * this.Y + 
				scaleMatrix.Value(0, 2) * this.Z + scaleMatrix.Value(0, 3);
			scaledCoordinate.Y = scaleMatrix.Value(1, 0) * this.X + scaleMatrix.Value(1, 1) * this.Y + 
				scaleMatrix.Value(1, 2) * this.Z + scaleMatrix.Value(1, 3);
			scaledCoordinate.Z = scaleMatrix.Value(2, 0) * this.X + scaleMatrix.Value(2, 1) * this.Y + 
				scaleMatrix.Value(2, 2) * this.Z + scaleMatrix.Value(2, 3);
			
			return scaledCoordinate;

		}

		internal Coordinate UnScale(Matrix inverseMatrix)
		{
			Coordinate inverseCoordinate = new Coordinate ();
			double x = this.X - inverseMatrix.Value(0, 3);
			double y = this.Y - inverseMatrix.Value(1, 3);
			double z = this.Z - inverseMatrix.Value(2, 3);

			inverseCoordinate.X = inverseMatrix.Value(0, 0) * this.X + inverseMatrix.Value(0, 1) * this.Y + 
				inverseMatrix.Value(0, 2) * this.Z + inverseMatrix.Value(0, 3);
			inverseCoordinate.Y = inverseMatrix.Value(1, 0) * this.X + inverseMatrix.Value(1, 1) * this.Y + 
				inverseMatrix.Value(1, 2) * this.Z + inverseMatrix.Value(1, 3);
			inverseCoordinate.Z = inverseMatrix.Value(2, 0) * this.X + inverseMatrix.Value(2, 1) * this.Y + 
				inverseMatrix.Value(2, 2) * this.Z + inverseMatrix.Value(2, 3);

			return inverseCoordinate;
		}

		/// <summary>
		/// transform the coordinate by symmetry operation
		/// </summary>
		/// <param name="symMatrix">symmetry operation matrix</param>
		/// <returns></returns>
		internal Coordinate TransformAtom(Matrix symMatrix)
		{
			Coordinate transformedAtomCoord = new Coordinate ();
			transformedAtomCoord.X =  symMatrix.Value(0, 0) * this.X + symMatrix.Value(0, 1) * this.Y + 
				symMatrix.Value(0, 2) * this.Z + symMatrix.Value(0, 3);
			transformedAtomCoord.Y = symMatrix.Value(1, 0) * this.X + symMatrix.Value(1, 1) * this.Y + 
				symMatrix.Value(1, 2) * this.Z + symMatrix.Value(1, 3);
			transformedAtomCoord.Z = symMatrix.Value(2, 0) * this.X + symMatrix.Value(2, 1) * this.Y + 
				symMatrix.Value(2, 2) * this.Z + symMatrix.Value(2, 3);

			return transformedAtomCoord;
		}
	}
}
