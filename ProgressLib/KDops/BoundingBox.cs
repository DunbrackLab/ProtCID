using System;
using System.Collections;
using XtalLib.Crystal;
using XtalLib.Contacts;

namespace XtalLib.KDops
{
	/// <summary>
	/// Summary description for BoundingBox.
	/// </summary>
	public class BoundingBox
	{
		public struct MinMaxInfo
		{
			public double minimum;
			public double maximum;
			public void Intialize()
			{
				minimum = 999999; // should be big enough
				maximum = -999999; // should be small enough
			}
		}
		
		#region member variable

		private MinMaxInfo[] minMaxList = null;

		#endregion

		#region constructors
		public BoundingBox()
		{
		}

		public BoundingBox (AtomInfo[] atomList)
		{
			SetBoundingBox (atomList);
		}

		public BoundingBox (AtomInfo[] atomList, KVector kVector)
		{
			SetBoundingBox (atomList, kVector);
		}

		public BoundingBox(BoundingBox bb)
		{
			minMaxList = new MinMaxInfo [bb.minMaxList.Length];
			
			
			bb.minMaxList.CopyTo (this.minMaxList, 0);
		}
		#endregion

		#region properties
		/// <summary>
		/// read only property 
		/// </summary>
		public MinMaxInfo[] MinMaxList
		{
			get
			{
				return minMaxList;
			}
		}
		#endregion

		#region set bounding box
		/// <summary>
		/// find bounding box in kdops
		/// </summary>
		/// <param name="atomList"></param>
		/// <param name="kVector"></param>
		private void SetBoundingBox(AtomInfo[] atomList, KVector kVector)
		{
			minMaxList = new MinMaxInfo[kVector.K];
			for (int i = 0; i < kVector.K; i ++)
			{
				minMaxList[i].Intialize ();
			}
			if (kVector.K == KVector.baseDim)
			{
				SetBoundingBox (atomList);
				return ;
			}
			foreach (AtomInfo atom in atomList)
			{
				double [] point3d = new double [3] {atom.xyz.X, atom.xyz.Y, atom.xyz.Z};
				double [] projectedValues = kVector * point3d;
				for (int i = 0; i < projectedValues.Length; i ++)
				{
					if (minMaxList[i].maximum < projectedValues[i])
					{
						minMaxList[i].maximum = projectedValues[i];
					}
					if (minMaxList[i].minimum > projectedValues[i])
					{
						minMaxList[i].minimum = projectedValues[i];
					}
				}
			}
		}

		/// <summary>
		/// find bounding box in kdops
		/// default kVector: 3D, no projection needed
		/// </summary>
		/// <param name="atomList"></param>
		private void SetBoundingBox(AtomInfo[] atomList)
		{
			minMaxList = new MinMaxInfo[KVector.baseDim];
			for (int i = 0; i < KVector.baseDim; i ++)
			{
				minMaxList[i].Intialize ();
			}
			foreach (AtomInfo atom in atomList)
			{	
				if (minMaxList[0].maximum < atom.xyz.X)
				{
					minMaxList[0].maximum = atom.xyz.X;
				}
				if (minMaxList[0].minimum > atom.xyz.X)
				{
					minMaxList[0].minimum = atom.xyz.X;
				}

				if (minMaxList[1].maximum < atom.xyz.Y)
				{
					minMaxList[1].maximum = atom.xyz.Y;
				}
				if (minMaxList[1].minimum > atom.xyz.Y)
				{
					minMaxList[1].minimum = atom.xyz.Y;
				}

				if (minMaxList[2].maximum < atom.xyz.Z)
				{
					minMaxList[2].maximum = atom.xyz.Z;
				}
				if (minMaxList[2].minimum > atom.xyz.Z)
				{
					minMaxList[2].minimum = atom.xyz.Z;
				}
			}
		}

		/// <summary>
		/// find bounding box in kdops
		/// </summary>
		/// <param name="atomList"></param>
		/// <param name="kVector"></param>
		private void SetKdopBoundingBox(AtomInfo[] atomListWithProCoord)
		{
			if (atomListWithProCoord.Length == 0)
			{
				return;
			}
			int k = atomListWithProCoord[0].ProjectedCoord.Length;
			minMaxList = new MinMaxInfo[k];
			for (int i = 0; i < k; i ++)
			{
				minMaxList[i].Intialize ();
			}
			foreach (AtomInfo atom in atomListWithProCoord)
			{
				for (int i = 0; i < k; i ++)
				{
					if (minMaxList[i].maximum < atom.ProjectedCoord[i])
					{
						minMaxList[i].maximum = atom.ProjectedCoord[i];
					}
					if (minMaxList[i].minimum > atom.ProjectedCoord[i])
					{
						minMaxList[i].minimum = atom.ProjectedCoord[i];
					}
				}
			}
		}
		#endregion

		#region add one element
		/// <summary>
		/// add a minMax info of a dimesion to the list
		/// </summary>
		/// <param name="minVal"></param>
		/// <param name="maxVal"></param>
		public void Add (double minVal, double maxVal)
		{
			ArrayList bbList = null;
			if (minMaxList != null)
			{
				bbList = new ArrayList (minMaxList);	
			}
			else
			{
				bbList = new ArrayList ();
			}
			MinMaxInfo minMax = new MinMaxInfo ();
			minMax.maximum = maxVal;
			minMax.minimum = minVal;
			bbList.Add (minMax);
			minMaxList = new MinMaxInfo [bbList.Count];
			bbList.CopyTo (minMaxList);
		}
		#endregion

		#region update bounding box
		/// <summary>
		/// update the bounding box 
		/// due to translation of the chain
		/// </summary>
		/// <param name="transVector">translation vector, the translation in X, Y, and Z</param>
		/// <param name="kVector">kdops vectors</param>
		/// <returns></returns>
		public BoundingBox UpdateBoundingBox (double [] transVector, KVector kVector)
		{
			BoundingBox updatedBoundBox = new BoundingBox ();
			if (minMaxList == null)
			{
					return null;
			}
			double [] transValues = kVector * transVector;
			updatedBoundBox.minMaxList = new MinMaxInfo [minMaxList.Length];
			for ( int i = 0; i < kVector.K; i ++ )
			{
				updatedBoundBox.minMaxList[i].maximum = minMaxList[i].maximum + transValues[i];
				updatedBoundBox.minMaxList[i].minimum = minMaxList[i].minimum + transValues[i];
			}
			return updatedBoundBox;
		}

		/// <summary>
		/// update the bounding box due to translation of the chain
		/// the default dimensions: 3D 
		/// </summary>
		/// <param name="transVector">translation vector</param>
		/// <returns></returns>
		public BoundingBox UpdateBoundingBox (double [] transVector)
		{
			BoundingBox updatedBoundBox = new BoundingBox ();
			if (minMaxList == null)
			{
				return null;
			}
			updatedBoundBox.minMaxList = new MinMaxInfo [minMaxList.Length];
			for ( int i = 0; i < transVector.Length; i ++ )
			{
				updatedBoundBox.minMaxList[i].maximum = minMaxList[i].maximum + transVector[i];
				updatedBoundBox.minMaxList[i].minimum = minMaxList[i].minimum + transVector[i];
			}
			return updatedBoundBox;
		}
		#endregion

		#region overlap of 2 bounding boxes
		/// <summary>
		/// check if 2 bounding boxes is overlap or not
		/// No overlap: Two k-dops D1, D2 do not overlap if at least one of the k/2 internals of D1 
		/// does not overlap the corresponding interval of D2
		/// May overlap: intervals are overlap along all k/2 directions 
		/// </summary>
		/// <param name="extBoundBox"></param>
		/// <returns></returns>
		public bool MayOverlap(BoundingBox extBoundBox, double contactCutoff)
		{
			if (minMaxList.Length != extBoundBox.MinMaxList.Length)
			{
				throw new Exception ("Bounding Boxes are not in same Kdops");
			}
			for(int i = 0; i < minMaxList.Length; i ++)
			{
				// if one interval not overlap, no overlap occurs
				// including the contact cutoff
				if (minMaxList[i].maximum < extBoundBox.MinMaxList[i].minimum - contactCutoff || 
					minMaxList[i].minimum > extBoundBox.MinMaxList[i].maximum + contactCutoff)
				{
					return false;
				}
			}
			// 2 boxes overlap
			return true;
		}
		#endregion

	}
}
