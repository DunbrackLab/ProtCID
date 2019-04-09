using System;
using System.Collections.Generic;
using CrystalInterfaceLib.KDops;

namespace CrystalInterfaceLib.Crystal
{
	/// <summary>
	/// Summary description for Residue.
	/// </summary>
	public class Residue
	{
		private List<AtomInfo> atomList = new List<AtomInfo> ();
		private BoundingBox bb = null;
		private Coordinate center = null;
		private string seqId = "";
		private string residueName = "";

		public Residue()
		{
		}

		public Residue (AtomInfo[] atoms)
		{
			atomList.AddRange (atoms);
			bb = new BoundingBox (atoms);
			center = new Coordinate ();
		    center.X = (bb.MinMaxList[0].maximum + bb.MinMaxList[0].minimum) / 2.0;
			center.Y = (bb.MinMaxList[1].maximum + bb.MinMaxList[1].minimum) / 2.0;
			center.Z = (bb.MinMaxList[2].maximum + bb.MinMaxList[2].minimum) / 2.0;
			residueName = atoms[0].residue;
			seqId = atoms[0].seqId;
		}
		#region properties
		public string SeqID
		{
			get
			{
				return seqId;
			}
			set
			{
				seqId = value;
			}
		}

		public string ResidueName
		{
			get
			{
				return residueName;
			}
			set
			{
				residueName = (string)value;
			}
		}

		public AtomInfo[] Atoms 
		{
			get
			{
                return atomList.ToArray ();
			}
			set
			{
				atomList.Clear ();
				atomList.AddRange ((AtomInfo[])value);
			}
		}

		public Coordinate Center 
		{
			get
			{
				return center;
			}
			set
			{
				center = value;
			}
		}

		public BoundingBox ResidueBox 
		{
			get
			{
				return bb;
			}
			set
			{
				bb = new BoundingBox ((BoundingBox)value);
			}
		}
		#endregion

		public void SetResidueBoundingBox ()
		{
			bb = new BoundingBox (Atoms);
		}

		public void Add (AtomInfo atom)
		{
			atomList.Add (atom);
		}
	}
}
