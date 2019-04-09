using System;
using System.Collections;
using XtalLib.Crystal;

namespace XtalLib.KDops
{
	/// <summary>
	/// Summary description for ResidueNode.
	/// </summary>
	public class ResidueNode : Node
	{
		private Residue[] residueList = null;
		public ResidueNode()
		{
		}

		public ResidueNode (Residue[] residues)
		{
			residueList = residues;
			ArrayList atomsList = new ArrayList ();
			
			foreach (Residue residue in residues)
			{
				atomsList.AddRange (residue.Atoms);
			}
			atomList = new AtomInfo [atomsList.Count];
			atomsList.CopyTo (atomList);
			boundBox = new BoundingBox (atomList);
		}
	}
}
