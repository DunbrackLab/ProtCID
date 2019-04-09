using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace CrystalInterfaceLib.Crystal
{
	/// <summary>
	/// Summary description for AtomInfo.
	/// </summary>
	public class AtomInfo : ICloneable
	{
		#region XML fields		
		[XmlElement("Residue")] public string residue = "";
		[XmlElement("SeqID")] public string seqId = "";

		[XmlAttribute("AtomID")] public int atomId = 0;
		[XmlElement("Coordinate")] public Coordinate xyz = new Coordinate ();		
		[XmlElement("AtomName")] public string atomName = "";
		[XmlElement("AtomType")] public string atomType = "";	

		[XmlElement("AuthSeqID")] public string authSeqId = "";
		[XmlElement("AuthResidue")] public string authResidue = "";
        [XmlElement("Bfactor")] public string bfactor = "";
        [XmlElement("Occupancy")]
        public string occupancy = "";

		[XmlIgnore] public double[] ProjectedCoord = null; // used until a bvtree created	
		[XmlIgnore] public Coordinate fractCoord = null; // used until base unit cell created
		#endregion

		public AtomInfo()
		{
		}

		/// <summary>
		/// deep copy of external atom information
		/// </summary>
		/// <param name="extAtomInfo"></param>
		public AtomInfo (AtomInfo extAtomInfo)
		{
			this.residue = extAtomInfo.residue;
			this.seqId = extAtomInfo.seqId;
			this.authSeqId = extAtomInfo.authSeqId;
			this.atomId = extAtomInfo.atomId;
			this.authResidue = extAtomInfo.authResidue;
			this.xyz = new Coordinate (extAtomInfo.xyz.X, extAtomInfo.xyz.Y, extAtomInfo.xyz.Z);
			this.atomName = extAtomInfo.atomName;
			this.atomType = extAtomInfo.atomType;
            this.bfactor = extAtomInfo.bfactor;
            this.occupancy = extAtomInfo.occupancy;
			if (extAtomInfo.ProjectedCoord != null)
			{
				this.ProjectedCoord = (double[]) extAtomInfo.ProjectedCoord.Clone ();
			}
			// don't copy the fractional coordinates
			this.fractCoord = null;
		}


		/// <summary>
		/// deep copy every fields
		/// </summary>
		/// <returns></returns>
		public object Clone ()
		{
			// showdow copy
			AtomInfo clonedAtomInfo = (AtomInfo)this.MemberwiseClone ();
			// deep copy fields with reference type
			clonedAtomInfo.xyz =  new Coordinate (xyz.X, xyz.Y, xyz.Z);
			if (this.ProjectedCoord != null)
			{
				clonedAtomInfo.ProjectedCoord = (double[]) this.ProjectedCoord.Clone ();
			}
			if (this.fractCoord != null)
			{
				clonedAtomInfo.fractCoord = new Coordinate (this.fractCoord.X, this.fractCoord.Y, this.fractCoord.Z);;
			}
			return clonedAtomInfo;
		}
		/// <summary>
		/// a. change a cartesian coordinate to fractional coordinate
		/// b. transform the fractional coordinate
		/// c. compute the cartesian coordinate 
		/// for the transformed fractional coordinate
		/// </summary>
		/// <param name="symOpMatrix"></param>
		/// <param name="fractTransMatrix"></param>
		/// <param name="cartnInverseMatrix"></param>
		/// <returns></returns>
		public AtomInfo TransformAtom(SymOpMatrix symOpMatrix, Matrix fractTransMatrix, Matrix cartnInverseMatrix)
		{
			AtomInfo transformedAtom = (AtomInfo)this.Clone ();;
			if (this.fractCoord == null)
			{
				this.fractCoord = fractTransMatrix * this.xyz;
			}
			transformedAtom.fractCoord = symOpMatrix * this.fractCoord;
			transformedAtom.xyz = cartnInverseMatrix * (symOpMatrix * this.fractCoord);
			return transformedAtom;
		}

		/// <summary>
		/// apply cartesian symmetry operators on cartesian coordinates
		/// directly to provide transformed cartesian coordinate
		/// </summary>
		/// <param name="symOpMatrix"></param>
		/// <param name="fractTransMatrix"></param>
		/// <param name="cartnInverseMatrix"></param>
		/// <returns></returns>
		public AtomInfo TransformAtom(SymOpMatrix symOpMatrix)
		{
			AtomInfo transformedAtom = (AtomInfo)this.Clone ();;
			transformedAtom.xyz = symOpMatrix * this.xyz;
			return transformedAtom;
		}

		/// <summary>
		/// a. change a cartesian coordinate to fractional coordinate
		/// </summary>
		/// <param name="fractTransMatrix"></param>
		public void GetFractCoord(Matrix fractTransMatrix)
		{
			if (this.fractCoord == null)
			{
				this.fractCoord = fractTransMatrix * this.xyz;
			}
		}
		/// <summary>
		/// Euclidean distance between two 3d points
		/// </summary>
		/// <param name="atomA"></param>
		/// <param name="atomB"></param>
		/// <returns></returns>
		public static double operator - (AtomInfo atomA, AtomInfo atomB)
		{
			double sqrSum = 0.0;
			sqrSum += (atomA.xyz.X - atomB.xyz.X) * (atomA.xyz.X - atomB.xyz.X);
			sqrSum += (atomA.xyz.Y - atomB.xyz.Y) * (atomA.xyz.Y - atomB.xyz.Y);
			sqrSum += (atomA.xyz.Z - atomB.xyz.Z) * (atomA.xyz.Z - atomB.xyz.Z);
			return Math.Sqrt (sqrSum);
		}
	}

	/*public class AsuAtom : AtomInfo, ICloneable
	{
		[XmlIgnore] public Coordinate fractCoord = null; // used until base unit cell created
		
		public object Clone()
		{
			AsuAtom atom = new AsuAtom();
			atom = (AsuAtom)base.Clone();
			atom.fractCoord = this.fractCoord;
		}
	}*/
}
