using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace CrystalInterfaceLib.Crystal
{
	/// <summary>
	/// matrix 3 * 4
	/// </summary>
	public class Matrix : ICloneable
	{
		[XmlIgnore] internal const int matrixRowCount = 3;
		[XmlIgnore] internal const int elemColCount = 4;
		
		internal MatrixElement[] matrix = null;

		/// <summary>
		/// default constructor
		/// initialize a 3*4 matrix
		/// </summary>
		public Matrix()
		{
			matrix = new MatrixElement [matrixRowCount];
			for(int i = 0; i < matrixRowCount; i ++)
			{
				MatrixElement matrixElement = new MatrixElement ();
				matrix[i] = matrixElement;
			}
		}

		/// <summary>
		/// deep copy a matrix
		/// </summary>
		/// <returns></returns>
		public Object Clone()
		{
			Matrix newMatrix = new Matrix ();
			for (int i = 0; i < matrixRowCount; i ++)
			{
				(newMatrix.MatrixElements)[i] = (MatrixElement)matrix[i].Clone ();
			}
			return newMatrix;
		}

		[XmlElement("Matrix")]
		public MatrixElement[] MatrixElements
		{
			get
			{
				return matrix;
			}
			set
			{
				if (value == null)
				{
					return;
				}
				MatrixElement[] matrixElements = (MatrixElement[]) value;
				if (matrixElements.Length > matrixRowCount)
				{
					return;
				}
				matrix = value;
			}
		}
		/// <summary>
		/// add a value to the specified position
		/// </summary>
		/// <param name="index1"></param>
		/// <param name="index2"></param>
		/// <param name="val"></param>
		public void Add(int index1, int index2, double val)
		{
			if (index1 >= matrixRowCount || index2 >= elemColCount)
			{
				throw new Exception ("Index out of range.");
			}
			matrix[index1].dimId = index1;
			switch (index2)
			{
				case 0:
					matrix[index1].a = val;
					break;
				case 1:
					matrix[index1].b = val;
					break;
				case 2:
					matrix[index1].c = val;
					break;
				case 3:
					matrix[index1].vector = val;
					break;
				default:
					break;
			}
		}

		/// <summary>
		/// add a row
		/// </summary>
		/// <param name="rowId"></param>
		/// <param name="vals"></param>
		public void Add(int rowId, double[] vals)
		{
			if (rowId > matrixRowCount - 1 || vals.Length > elemColCount)
			{
				throw new Exception ("Index Out of Range.");
			}
			int i = 0;
			foreach (double val in vals)
			{
				this.Add (rowId, i, val);
				i ++;
			}
		}

		/// <summary>
		///  return the value at the specified position
		/// </summary>
		/// <param name="index1"></param>
		/// <param name="index2"></param>
		/// <returns></returns>
		public double Value (int index1, int index2)
		{
			double val = 0.0;
			if (index1 >= matrixRowCount || index2 >= elemColCount)
			{
				throw new Exception ("Index out of range.");
			}
			
			MatrixElement matrixElement = (MatrixElement) matrix[index1];
			switch (index2)
			{
				case 0:
					val = matrixElement.a;
					break;
				case 1:
					val = matrixElement.b;
					break;
				case 2:
					val = matrixElement.c;
					break;
				case 3:
					val = matrixElement.vector;
					break;
				default:
					break;
			}
			return val;
		}

		/// <summary>
		///  get the inverse matrix with inversed vector
		///  M * M(inv) = Indentity Matrix
		///  Vector(inv) = (-1) (M(inv)* Vector)
		/// </summary>
		/// <returns></returns>
		public Matrix Inverse ()
		{
			Matrix inverseMatrix = new Matrix ();
			// compute the determinant for a symmetry operator
			double deter = Value(0, 0) * (Value(1, 1) * Value(2, 2) - Value(1, 2) * Value(2, 1)) -
				Value(0, 1) * (Value(1, 0) * Value(2, 2) - Value(1, 2) * Value(2, 0)) +
				Value(0, 2) * (Value(1, 0) * Value(2, 1) - Value(1, 1) * Value(2, 0));
			inverseMatrix.Add(0, 0, (Value(1, 1) * Value(2, 2) - Value(2, 1) * Value(1, 2)) / deter);
			inverseMatrix.Add(1, 0, (Value(2, 0) * Value(1, 2) - Value(1, 0) * Value(2, 2)) / deter);
			inverseMatrix.Add(2, 0, (Value(1, 0) * Value(2, 1) - Value(2, 0) * Value(1, 1)) / deter);

			inverseMatrix.Add(0, 1, (Value(2, 1) * Value(0, 2) - Value(0, 1) * Value(2, 2)) / deter);
			inverseMatrix.Add(1, 1, (Value(0, 0) * Value(2, 2) - Value(2, 0) * Value(0, 2)) / deter);
			inverseMatrix.Add(2, 1, (Value(2, 0) * Value(0, 1) - Value(0, 0) * Value(2, 1)) / deter);

			inverseMatrix.Add(0, 2, (Value(0, 1) * Value(1, 2) - Value(1, 1) * Value(0, 2)) / deter);
			inverseMatrix.Add(1, 2, (Value(1, 0) * Value(0, 2) - Value(0, 0) * Value(1, 2)) / deter);
			inverseMatrix.Add(2, 2, (Value(0, 0) * Value(1, 1) - Value(1, 0) * Value(0, 1)) / deter);

			// inversed vector
			// inverseMatrix * vector of symmetry matrix  * -1
			Coordinate vectorCoord = new Coordinate (Value(0, 3), Value(1, 3), Value(2, 3));
			Coordinate inversedVectorCoord = inverseMatrix * vectorCoord;
			inverseMatrix.Add(0, 3, inversedVectorCoord.X * -1);
			inverseMatrix.Add(1, 3, inversedVectorCoord.Y * -1);
			inverseMatrix.Add(2, 3, inversedVectorCoord.Z * -1);

			return inverseMatrix;
		}

		/// <summary>
		/// override operator * 
		/// (symmetry matrix (3 * 3) * coordinate (3 * 1) + vector (3 * 1))
		/// transform a coordinate to its symmetric position 
		/// based on the specific symmetry operator
		/// </summary>
		/// <param name="symMatrix"></param>
		/// <param name="xyz"></param>
		/// <returns></returns>
		public static Coordinate operator * (Matrix symMatrix, Coordinate xyz)
		{
			Coordinate transformedAtomCoord = new Coordinate ();
			transformedAtomCoord.X =  symMatrix.Value(0, 0) * xyz.X + symMatrix.Value(0, 1) * xyz.Y + 
				symMatrix.Value(0, 2) * xyz.Z + symMatrix.Value(0, 3);
			transformedAtomCoord.Y = symMatrix.Value(1, 0) * xyz.X + symMatrix.Value(1, 1) * xyz.Y + 
				symMatrix.Value(1, 2) * xyz.Z + symMatrix.Value(1, 3);
			transformedAtomCoord.Z = symMatrix.Value(2, 0) * xyz.X + symMatrix.Value(2, 1) * xyz.Y + 
				symMatrix.Value(2, 2) * xyz.Z + symMatrix.Value(2, 3);

			return transformedAtomCoord;
		}
	}

	/// <summary>
	/// symmetry matrix, inherited from matrix
	/// </summary>
	public class SymOpMatrix : Matrix, ICloneable
	{
		[XmlAttribute("SymmetryOpNum")] public string symmetryOpNum = "";
		[XmlAttribute("SymmetryString")] public string symmetryString = "";
		[XmlAttribute("FullSymmetryString")] public string fullSymmetryString = "";

		public new Object Clone()
		{
			SymOpMatrix newMatrix = new SymOpMatrix ();
			newMatrix.MatrixElements = ((Matrix)(base.Clone ())).MatrixElements;
			newMatrix.symmetryOpNum = this.symmetryOpNum;
			newMatrix.symmetryString = this.symmetryString;
			newMatrix.fullSymmetryString = this.fullSymmetryString;			
			return newMatrix;
		}
		
		#region Symmetry matrix to symmetry string
		/// <summary>
		/// convert a symmetry matrix into a full symmetry string
		/// e.g.  1 0 0 0.5; 0 -1 0 0; 0 0 1 0.5
		/// to 
		/// X+0.5,-Y,Z+0.5 
		/// </summary>
		/// <returns></returns>
		public string ToFullSymString ()
		{
			string fullSymString = "";
			for (int row = 0; row < matrixRowCount; row ++)
			{
				for (int col = 0; col < elemColCount; col ++)
				{
					if (col < elemColCount - 1)
					{
						string dimStr = GetDimString ( col, (int)Value(row, col) );
						// add + between X and/or Y and/or Z
						if (dimStr != "")
						{
							if (fullSymString != "" && 
								fullSymString[fullSymString.Length - 1] != ',' &&
								(dimStr[0] == 'X' || dimStr[0] == 'Y' || dimStr[0] == 'Z'))
							{
								fullSymString += "+";
							}
							fullSymString += dimStr;
						}
					}
					else
					{
						// vector
						string vectString = Value (row, col).ToString ();
						if ( vectString != "0")
						{
							if (Char.IsDigit (vectString[0]))
							{
								// add "+" before the number
								fullSymString += "+";
							}
							fullSymString += vectString;
						}
					}
				}
				fullSymString += ",";
			}
			return fullSymString.TrimEnd (',');
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="col"></param>
		/// <param name="val"></param>
		/// <returns></returns>
		private string GetDimString (int col, int val)
		{
			string colStr = "";
			switch (val)
			{
				case -1:
				{
					switch (col)
					{
						case 0: 
							colStr = "-X";
							break;
						case 1:
							colStr = "-Y";
							break;
						case 2:
							colStr = "-Z";
							break;
						default:
							break;
					}
					break;
				}

				case 1:
				{
					switch (col)
					{
						case 0: 
							colStr = "X";
							break;
						case 1:
							colStr = "Y";
							break;
						case 2:
							colStr = "Z";
							break;
						default:
							break;
					}
					break;
				}

				default:
					break;
			}
			return colStr;
		}
		#endregion
       // end of functions
	}
}
