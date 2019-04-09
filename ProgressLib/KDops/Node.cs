using System;
using System.Collections;
using XtalLib.Crystal;

namespace XtalLib.KDops
{
	/// <summary>
	/// Summary description for Node.
	/// </summary>
	public class Node
	{
		private BoundingBox boundBox = null;
		private AtomInfo[] atomList = null;

		#region constructors
		public Node()
		{
		}

		public Node (AtomInfo[] atoms, KVector kVector)
		{
			atomList = atoms;
			foreach (AtomInfo atom in atomList)
			{
				double[] coord = new double [3] {atom.xyz.X, atom.xyz.Y, atom.xyz.Z};
				atom.ProjectedCoord = kVector * coord;
			}
			boundBox = new BoundingBox (atoms, kVector);
		}

		public Node (AtomInfo[] inAtomList)
		{
			atomList = new AtomInfo[inAtomList.Length];
			for (int i = 0; i < inAtomList.Length; i ++)
			{
				atomList[i] = new AtomInfo (inAtomList[i]);
			}
			boundBox = new BoundingBox (inAtomList);
		}

		/// <summary>
		/// shadow copy
		/// </summary>
		/// <param name="atoms"></param>
		/// <param name="bb"></param>
		public Node (AtomInfo[] atoms, BoundingBox bb)
		{
			this.atomList = atoms;
			this.boundBox = bb;
		}

		/// <summary>
		/// deep copy constructor
		/// </summary>
		/// <param name="extNode"></param>
		public Node (Node extNode)
		{
			atomList = new AtomInfo [extNode.atomList.Length];
			for (int i = 0; i < atomList.Length; i ++)
			{
				atomList[i] = new AtomInfo ((extNode.atomList)[i]);
			}
			boundBox = new BoundingBox (extNode.boundBox);
		}
		#endregion

		#region properties
		public BoundingBox BoundBox 
		{
			get
			{
				return boundBox;
			}
			set
			{
				boundBox = value;
			}
		}

		public AtomInfo[] AtomList
		{
			get
			{
				return atomList;
			}
			set
			{
				atomList = value;
			}
		}
		#endregion

		#region Calpha -- cbeta atoms
		/// <summary>
		/// Calpha, Cbeta atoms
		/// </summary>
		/// <returns></returns>
		public AtomInfo[] CalphaCbetaAtoms ()
		{
			ArrayList bbAtomList = new  ArrayList ();
			foreach (AtomInfo atom in atomList)
			{
				if (atom.atomName == "CB" || atom.atomName == "CA")
				{
					bbAtomList.Add (atom);
				}
			}
			AtomInfo[] atoms = new AtomInfo [bbAtomList.Count];
			bbAtomList.CopyTo (atoms);
			return atoms;
		}
		#endregion

		#region update node
		/// <summary>
		/// update root node 
		/// atoms and projected coordinates
		/// </summary>
		/// <param name="transVector"></param>
		public void UpdateLeafNode(double [] transVector)
		{
			foreach (AtomInfo atom in atomList)
			{
				atom.xyz.X += transVector[0];
				atom.xyz.Y += transVector[1];
				atom.xyz.Z += transVector[2];
				if (atom.ProjectedCoord != null)
				{
					for (int i = 0; i < transVector.Length; i ++)
					{
						atom.ProjectedCoord[i] += transVector[i];
					}
				}
			}
			this.boundBox = new BoundingBox (this.atomList);
		}

		/// <summary>
		/// merge two nodes together
		/// </summary>
		/// <param name="node1"></param>
		/// <param name="node2"></param>
		/// <returns></returns>
		public void Merge2Nodes (Node node1, Node node2)
		{
			this.atomList = new AtomInfo[node1.atomList.Length + node2.atomList.Length];
			node1.atomList.CopyTo (this.atomList, 0);
			node2.atomList.CopyTo (this.atomList, node1.atomList.Length);
			this.boundBox = new BoundingBox (this.atomList);
		}
		#endregion
	}
}
