using System;
using System.Collections;
using XtalLib.Crystal;
using XtalLib.Contacts;
using XtalLib.Settings;

namespace XtalLib.KDops
{
	/* Implement a quick collision detection using k-DOPS by C#
	 * Details please refer to:
	 * James T. Klosowski, Martin Held, Joseph S.B. Mitchell, Henry Sowizral, Karel Zikan
		"Efficient Collision Detection Using Bounding Volume Hierarchies of k-DOPs"
		IEEE Transactions on Visualization and Computer Graphics, March 1998, Volume 4, Number 1 
	 * */
	/// <summary>
	/// a binary tree for kDops bounding box tree
	/// </summary>
	public class BVTree
	{
		// the atom number of a leaf node
		// used to prune the tree
		// default is 1 atom
		internal static int leafAtomNumber = 1;
		// 0: atom, 1: residue; -1: other
		public static char treeType = '0';
		// tree structure elements
		private Node root;
		private BVTree leftBranch = null;
		private BVTree rightBranch = null;

		#region constructors
		public BVTree()
		{
		}

		public BVTree(BVTree extTree)
		{
			root = extTree.Root;
			rightBranch = extTree.RightBranch;
			leftBranch = extTree.LeftBranch;
		}
		#endregion

		#region properties
		/// <summary>
		/// root of the tree
		/// </summary>
		public Node Root
		{
			get
			{
				return root;
			}
			set
			{
				root = value;
			}
		}

		/// <summary>
		/// left branch
		/// </summary>
		public BVTree LeftBranch
		{
			get
			{
				return leftBranch;
			}
			set
			{
				leftBranch = value;
			}
		}

		/// <summary>
		/// right branch 
		/// </summary>
		public BVTree RightBranch
		{
			get
			{
				return rightBranch;
			}
			set
			{
				rightBranch = value;
			}
		}
		#endregion

		#region BVTree builder
		
		/// <summary>
		/// build a binary tree by up-down split
		/// </summary>
		/// <param name="atoms"></param>
		/// <param name="pruneAtomNumber"></param>
		/// <param name="method"></param>
		public void BuildBVTree(AtomInfo[] atoms, string method, bool isResidue)
		{
			if (isResidue)
			{
				BuildBVTree(GetResidues (atoms), method);
			}		
			else
			{
				BuildBVTree(atoms, AppSettings.parameters.kDopsParam.atomsInLeaf, method);
			}
		}

		/// <summary>
		/// build a binary tree by up-down split
		/// </summary>
		/// <param name="atoms"></param>
		/// <param name="pruneAtomNumber"></param>
		/// <param name="method"></param>
		public void BuildBVTree(AtomInfo[] atoms, int pruneAtomNumber, string method)
		{
			treeType = '0';
			leafAtomNumber = pruneAtomNumber;
			root = new Node (atoms, AppSettings.kVector);
			switch (method.ToLower ())
			{
				case "splatter":
					BuildBVTreeBySplatter (root);
					break;

				case "amplitude":
					BuildBVTreeByAmplitude (root);
					break;

				case "minsum":					
					break;

				case "minmax":					
					break;

				default:
					break;
			}
		}
		#endregion

		/* Choice of a plane orthogonal to the x-, y- or z-axis 
		 * based upon one of the following objective functions
		 * Splatter: Projects each point onto each of the three coordinate axes and 
		 *			calculate the variance of each of the resulting distributions. 
		 *			Choose the axis yielding the largest variance.
		 *			Computation Time: O(|Sv|), linear time
		 * Amplitude: Choose the axis along which the k-dop, bounding box b(Sv), is longest
		 *			Computation Time: 3 subtractions and 2 comparisons
		 * Min Sum: Choose the axis that minimizes the sum of the volumes 
		 *			of the two resulting children.
		 *			Computation Time: O(k|Sv|) + O(klogk) 
		 * Min Max: Choose the axis that minimizes 
		 *			the larger of the volumes of the two resulting children.
		 * ("Min Sum" and "Min Max" not implemented yet)
		 * */
		#region split 
		#region splatter
		/// <summary>
		/// recursively build the binary tree
		/// by splatter
		/// </summary>
		/// <param name="root"></param>
		private void BuildBVTreeBySplatter (Node root)
		{
			if (root == null)
			{
				return;
			}
			if (root.AtomList.Length <= leafAtomNumber)
			{
				return;
			}
			leftBranch = new BVTree ();
			rightBranch = new BVTree ();
			SplitBySplatter(root, ref leftBranch, ref rightBranch);
			leftBranch.BuildBVTreeBySplatter (leftBranch.Root);
			rightBranch.BuildBVTreeBySplatter (rightBranch.Root);
		}

		/// <summary>
		/// split a node by splatter
		/// </summary>
		/// <param name="thisRoot"></param>
		private void SplitBySplatter(Node thisRoot, ref BVTree left, ref BVTree right)
		{			
			AtomInfo [] leftAtoms = null;
			AtomInfo [] rightAtoms = null;
			SplitNodeBySplatter ( thisRoot.AtomList, ref leftAtoms, ref rightAtoms );			
			left.Root = new Node (leftAtoms);			
			right.Root = new Node (rightAtoms);				
		}
		
		/// <summary>
		/// split a node by splatter
		/// return left node and right node
		/// </summary>
		/// <param name="atoms"></param>
		/// <param name="leftAtoms"></param>
		/// <param name="rightAtoms"></param>
		private void SplitNodeBySplatter (AtomInfo[] atoms, ref AtomInfo[] leftAtoms, ref AtomInfo[] rightAtoms)
		{
			double [] xVals = new double [atoms.Length];
			double [] yVals = new double [atoms.Length];
			double [] zVals = new double [atoms.Length];
			for (int i = 0; i < atoms.Length; i ++)
			{
				xVals[i] = atoms[i].xyz.X;
				yVals[i] = atoms[i].xyz.Y;
				zVals[i] = atoms[i].xyz.Z;
			}
			double xVar = Variance(xVals);
			double yVar = Variance(yVals);
			double zVar = Variance(zVals);
			if (Math.Max (xVar, yVar) < zVar)
			{
				SplitIntoTwo(atoms, zVals, ref leftAtoms, ref rightAtoms);			
			}
			else
			{
				if (xVar > yVar)
				{
					SplitIntoTwo(atoms, xVals, ref leftAtoms, ref rightAtoms);
				}
				else
				{
					SplitIntoTwo(atoms, yVals, ref leftAtoms, ref rightAtoms);
				}
			}
		}

		/// <summary>
		/// split the list of atoms into two 
		/// based on mean of projected values in x, y, or z
		/// </summary>
		/// <param name="atoms"></param>
		/// <param name="projectedVals"></param>
		/// <param name="leftAtoms"></param>
		/// <param name="rightAtoms"></param>
		private void SplitIntoTwo (AtomInfo[] atoms, double [] axisVals, 
			ref AtomInfo[] leftAtoms, ref AtomInfo[] rightAtoms)
		{
			float dimMean = (float)Mean(axisVals);
			ArrayList leftAtomList = new ArrayList ();
			ArrayList rightAtomList = new ArrayList ();
			for (int i = 0; i < axisVals.Length; i ++)
			{
				if ((float)axisVals[i] < dimMean)
				{
					leftAtomList.Add (atoms[i]);
				}
				else if ((float)axisVals[i] > dimMean)
				{
					rightAtomList.Add (atoms[i]);
				}
				else
				{
					// try to break the tie (avoid dead loop)
					// not so efficient, but distribute atoms evenly
					if (i % 2 == 0)
					{
						leftAtomList.Add (atoms[i]);
					}
					else
					{
						rightAtomList.Add (atoms[i]);
					}
				}
			}
			leftAtoms = new AtomInfo [leftAtomList.Count];
			leftAtomList.CopyTo (leftAtoms);
			rightAtoms = new AtomInfo [rightAtomList.Count];
			rightAtomList.CopyTo (rightAtoms);
		}
		#endregion

		#region amplitude
		/// <summary>
		/// recursively build the binary tree by Amplitude
		/// </summary>
		/// <param name="root"></param>
		private void BuildBVTreeByAmplitude (Node root)
		{
			if (root.AtomList.Length <= leafAtomNumber)
				return;

			leftBranch = new BVTree ();
			rightBranch = new BVTree ();
			SplitByAmplitude(root, ref leftBranch, ref rightBranch);
			BuildBVTreeByAmplitude (leftBranch.Root);
			BuildBVTreeByAmplitude (rightBranch.Root);
		}

		/// <summary>
		/// split a list of atoms by Amplitude
		/// </summary>
		/// <param name="thisRoot"></param>
		private void SplitByAmplitude(Node thisRoot, ref BVTree left, ref BVTree right)
		{
			AtomInfo [] leftAtoms = null;
			AtomInfo [] rightAtoms = null;
			int axisNum = FindSplitterAxis (thisRoot);
			SplitNodeByAmplitude ( thisRoot.AtomList, axisNum, ref leftAtoms, ref rightAtoms );
			left.Root = new Node (leftAtoms);
			right.Root = new Node (rightAtoms);
		}

		/// <summary>
		/// split a node by amplitude
		/// </summary>
		/// <param name="atoms"></param>
		/// <param name="leftAtoms"></param>
		/// <param name="rightAtoms"></param>
		private void SplitNodeByAmplitude (AtomInfo[] atoms, int axisNum, ref AtomInfo[] leftAtoms, ref AtomInfo[] rightAtoms)
		{
			ArrayList leftAtomList = new ArrayList ();
			ArrayList rightAtomList = new ArrayList ();
			double [] dimVals = new double [atoms.Length];
			switch (axisNum)
			{
				case 0:
					for (int i = 0; i < atoms.Length; i ++)
					{
						dimVals[i] = atoms[i].xyz.X;
					}
					break;

				case 1:
					for (int i = 0; i < atoms.Length; i ++)
					{
						dimVals[i] = atoms[i].xyz.Y;
					}
					break;


				case 2:
					for (int i = 0; i < atoms.Length; i ++)
					{
						dimVals[i] = atoms[i].xyz.Z;
					}
					break;

				default:
					break;

			}
			SplitIntoTwo(atoms, dimVals, ref leftAtoms, ref rightAtoms);
			
		}

		/// <summary>
		/// find the splitter axis based on the amplitude of the bound box in x, y, or z
		/// </summary>
		/// <param name="node"></param>
		/// <returns></returns>
		private int FindSplitterAxis (Node node)
		{
			double [] dimDifs = new double [KVector.baseDim];
			for (int i = 0; i < KVector.baseDim; i ++)
			{
				dimDifs[i] = node.BoundBox.MinMaxList[i].maximum - node.BoundBox.MinMaxList[i].minimum;
			}
			int indexWithMax = 0;
			double maxVar = 0.0;
			for (int i = 0; i < dimDifs.Length; i ++)
			{
				if (maxVar < dimDifs[i])
				{
					maxVar = dimDifs[i];
					indexWithMax = i;
				}
			}
			return indexWithMax;
		}
		#endregion

		#endregion

		#region update tree
		/// <summary>
		/// update the tree based on translation vector
		/// </summary>
		/// <param name="transVector"></param>
		/// <returns></returns>
		public BVTree UpdateBVTree (double [] transVector)
		{
			BVTree updatedTree = new BVTree ();
			double [] kDopTransVector = AppSettings.kVector * transVector;			
			// update the tree
			UpdateTreeNodes (updatedTree, kDopTransVector);
			return updatedTree;
		}

		/// <summary>
		/// update the tree to another tree with exactly same structure
		/// using bottom-up
		/// </summary>
		/// <param name="extBvTree"></param>
		private void UpdateTreeNodes(BVTree extBvTree, double[] kDopTransVector)
		{
			if (this.root.AtomList.Length <= leafAtomNumber)
			{
				extBvTree.root = new Node (this.root);
				extBvTree.root.UpdateLeafNode (kDopTransVector);
				return ;
			}
			extBvTree.leftBranch = new BVTree ();
			this.leftBranch.UpdateTreeNodes (extBvTree.leftBranch, kDopTransVector);
			extBvTree.rightBranch = new BVTree ();
			this.rightBranch.UpdateTreeNodes (extBvTree.rightBranch, kDopTransVector);
			// combine two child nodes
			extBvTree.Root = new Node();
			extBvTree.root.Merge2Nodes (extBvTree.leftBranch.root, extBvTree.rightBranch.root);			
		}
	
		#endregion

		#region residue BVTree 
		/// <summary>
		/// build a binary tree by up-down split
		/// </summary>
		/// <param name="atoms"></param>
		/// <param name="pruneAtomNumber"></param>
		/// <param name="method"></param>
		public void BuildBVTree(Residue[] residues, string method)
		{
			treeType = '1';
			leafAtomNumber = 1;
			root = new Node (GetResidueCentersAtoms (residues), AppSettings.kVector);
			switch (method.ToLower ())
			{
				case "splatter":
					BuildBVTreeBySplatter (root);
					break;

				case "amplitude":
					BuildBVTreeByAmplitude (root);
					break;

				case "minsum":					
					break;

				case "minmax":					
					break;

				default:
					break;
			}
			UpdateResiudeBvTreeBb (residues);
		}

		/// <summary>
		/// convert from a list atoms to a list of residues
		/// </summary>
		/// <param name="atoms"></param>
		/// <returns></returns>
		private Residue[] GetResidues (AtomInfo[] atoms)
		{
			Hashtable residueSeqHash = new Hashtable ();
			foreach (AtomInfo atom in atoms)
			{
				if (residueSeqHash.ContainsKey (atom.seqId))
				{
					ArrayList atomList = (ArrayList)residueSeqHash[atom.seqId];
					atomList.Add (atom);
					residueSeqHash[atom.seqId] = atomList;
				}
				else
				{
					ArrayList atomList = new ArrayList ();
					atomList.Add (atom);
					residueSeqHash.Add (atom.seqId, atomList);
				}
			}
			Residue[] residues = new Residue [residueSeqHash.Count];
			int count = 0;
			foreach (string seqId in residueSeqHash.Keys)
			{
				AtomInfo[] residueAtoms = new AtomInfo [((ArrayList)residueSeqHash[seqId]).Count];
				((ArrayList)residueSeqHash[seqId]).CopyTo (residueAtoms);
				residues[count] = new Residue (residueAtoms);
				count ++;
			}
			return residues;
		}

		/// <summary>
		/// residue centers, used to build the BV tree
		/// </summary>
		/// <param name="residues"></param>
		/// <returns></returns>
		private AtomInfo[] GetResidueCentersAtoms (Residue[] residues)
		{
			AtomInfo[] atoms = new AtomInfo [residues.Length];
			for (int i = 0; i < residues.Length; i ++)
			{
				atoms[i] = new AtomInfo ();
				atoms[i].residue = residues[i].ResidueName;
				atoms[i].seqId = residues[i].SeqID;
				atoms[i].xyz = residues[i].Center;
			}
			return atoms;
		}
		/// <summary>
		/// update the bounding box for each node
		/// </summary>
		/// <param name="residues"></param>
		private void UpdateResiudeBvTreeBb (Residue[] residues)
		{
			Hashtable residueSeqHash = new Hashtable ();
			foreach (Residue residue in residues)
			{
				residueSeqHash.Add (residue.SeqID, residue);
			}
			UpdateNodeBb (residueSeqHash);
		}

		/// <summary>
		/// update the tree to another tree with exactly same structure
		/// using bottom-up
		/// </summary>
		/// <param name="extBvTree"></param>
		private void UpdateNodeBb(Hashtable residueSeqHash)
		{
			AtomInfo[] residueCenters = this.Root.AtomList;
			ArrayList atomList = new ArrayList ();
			foreach (AtomInfo residueCenter in residueCenters)
			{
				Residue residue = ((Residue)residueSeqHash[residueCenter.seqId]);
				atomList.AddRange (residue.Atoms);
			}
			AtomInfo[] atoms = new AtomInfo [atomList.Count];
			atomList.CopyTo (atoms);
			this.Root.AtomList = atoms;
			this.Root.BoundBox = new BoundingBox (atoms);

			if (this.leftBranch == null && this.rightBranch == null)
			{
				return ;
			}			
			this.leftBranch.UpdateNodeBb (residueSeqHash);			
			this.rightBranch.UpdateNodeBb (residueSeqHash);		
		}
		#endregion

		#region  Math helper functions
		/// <summary>
		/// variance of an array of double values
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		private double Variance (double [] data)
		{
			double sqrSum = 0.0;
			double mean = Sum(data) / (double)data.Length;
			foreach (double val in data)
			{
				sqrSum += (val - mean) * (val - mean);
			}
			return sqrSum / (double)data.Length;
		}

		/// <summary>
		/// sum of an array of double values
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		private double Sum(double [] data)
		{
			double dataSum = 0.0;
			foreach (double val in data)
			{
				dataSum += val;
			}
			return dataSum;
		}

		/// <summary>
		/// mean
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		private double Mean(double [] data)
		{
			return Sum(data) / data.Length;
		}

		/// <summary>
		/// median
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		private double Median(double[] data)
		{
			Array.Sort (data);
			if (data.Length % 2 == 0)
			{
				int midIndex = (int) (data.Length / 2);
				return (data[midIndex] + data[midIndex - 1]) / 2;
			}
			else
			{
				int midIndex = (int)Math.Floor ((double)data.Length / 2.0);
				return data[midIndex];
			}
		}
		#endregion

	}
}
