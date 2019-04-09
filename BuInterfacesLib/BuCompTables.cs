using System;
using System.Data;

namespace BuInterfacesLib
{
	/// <summary>
	/// Summary description for BuCompTables.
	/// </summary>
	public class BuCompTables
	{
		public static DataTable[] buCompTables = new DataTable [15];
		public const int PdbPqsBUsComp = 0;
		public const int PdbPqsBuInterfaceComp = 1;
		public const int PdbBuInterfaces = 2;
		public const int PqsBuInterfaces = 3;
		public const int PdbBuSameInterfaces = 4;
		public const int PqsBuSameInterfaces = 5;
		public const int PdbInterfaceResidues = 6;
		public const int PqsInterfaceResidues = 7;
		public const int PdbBuContacts = 8;
		public const int PqsBuContacts = 9;

		public const int PisaBuInterfaces = 10;
		public const int PisaPdbBUsComp = 11;
		public const int PisaPqsBUsComp = 12;
		public const int PisaPdbBuInterfaceComp = 13;
		public const int PisaPqsBuInterfaceComp = 14;

		public BuCompTables()
		{
		}

		public static void InitializeTables ()
		{
			// PDB and PQS BU comparison table
			string[] pdbPqsBuCompColumns = {"PdbID", "PdbBuID", "PqsBuID", "PdbInterfaceNum", "PqsInterfaceNum", "IsSame"};
			buCompTables[PdbPqsBUsComp] = new DataTable ("PdbPqsBUComp");
			foreach (string buCompCol in pdbPqsBuCompColumns)
			{
				if (buCompCol.ToLower ().IndexOf ("issame") > -1)
				{
					buCompTables[PdbPqsBUsComp].Columns.Add (new DataColumn (buCompCol, System.Type.GetType ("System.Int32")));
				}
				else
				{
					buCompTables[PdbPqsBUsComp].Columns.Add (new DataColumn (buCompCol, System.Type.GetType ("System.String")));
				}
			}

			string[] pisaPdbBuCompColumns = {"PdbID", "PisaBuID", "PdbBuID", "PisaInterfaceNum", "PdbInterfaceNum", "IsSame"};
			buCompTables[PisaPdbBUsComp] = new DataTable ("PisaPdbBUComp");
			foreach (string buCompCol in pisaPdbBuCompColumns)
			{
				if (buCompCol.ToLower ().IndexOf ("issame") > -1)
				{
					buCompTables[PisaPdbBUsComp].Columns.Add (new DataColumn (buCompCol, System.Type.GetType ("System.Int32")));
				}
				else
				{
					buCompTables[PisaPdbBUsComp].Columns.Add (new DataColumn (buCompCol, System.Type.GetType ("System.String")));
				}
			}
			string[] pisaPqsBuCompColumns = {"PdbID", "PisaBuID", "PqsBuID", "PisaInterfaceNum", "PqsInterfaceNum", "IsSame"};
			buCompTables[PisaPqsBUsComp] = new DataTable ("PisaPqsBUComp");
			foreach (string buCompCol in pisaPqsBuCompColumns)
			{
				if (buCompCol.ToLower ().IndexOf ("issame") > -1)
				{
					buCompTables[PisaPqsBUsComp].Columns.Add (new DataColumn (buCompCol, System.Type.GetType ("System.Int32")));
				}
				else
				{
					buCompTables[PisaPqsBUsComp].Columns.Add (new DataColumn (buCompCol, System.Type.GetType ("System.String")));
				}
			}
			
			// PDB and PQS interfaces comparison table
			string[] pdbPqsInterfaceCompColumns = {"PdbID", "PdbBuID", "PqsBuID", "PdbInterfaceID", "PqsInterfaceID", "QScore"};
			buCompTables[PdbPqsBuInterfaceComp] = new DataTable ("PdbPqsBuInterfaceComp");
			foreach (string interfaceCol in pdbPqsInterfaceCompColumns)
			{
				if (interfaceCol.ToLower ().IndexOf ("qscore") > -1)
				{
					buCompTables[PdbPqsBuInterfaceComp].Columns.Add (new DataColumn (interfaceCol, System.Type.GetType ("System.Double")));
				}
				else
				{
					buCompTables[PdbPqsBuInterfaceComp].Columns.Add (new DataColumn (interfaceCol, System.Type.GetType ("System.String")));
				}
			}

			string[] pisaPdbInterfaceCompColumns = {"PdbID", "PisaBuID", "PdbBuID", "PisaInterfaceID", "PdbInterfaceID", "QScore"};
			buCompTables[PisaPdbBuInterfaceComp] = new DataTable ("PisaPdbBuInterfaceComp");
			foreach (string interfaceCol in pisaPdbInterfaceCompColumns)
			{
				if (interfaceCol.ToLower ().IndexOf ("qscore") > -1)
				{
					buCompTables[PisaPdbBuInterfaceComp].Columns.Add (new DataColumn (interfaceCol, System.Type.GetType ("System.Double")));
				}
				else
				{
					buCompTables[PisaPdbBuInterfaceComp].Columns.Add (new DataColumn (interfaceCol, System.Type.GetType ("System.String")));
				}
			}
			string[] pisaPqsInterfaceCompColumns = {"PdbID", "PisaBuID", "PqsBuID", "PisaInterfaceID", "PqsInterfaceID", "QScore"};
			buCompTables[PisaPqsBuInterfaceComp] = new DataTable ("PisaPqsBuInterfaceComp");
			foreach (string interfaceCol in pisaPqsInterfaceCompColumns)
			{
				if (interfaceCol.ToLower ().IndexOf ("qscore") > -1)
				{
					buCompTables[PisaPqsBuInterfaceComp].Columns.Add (new DataColumn (interfaceCol, System.Type.GetType ("System.Double")));
				}
				else
				{
					buCompTables[PisaPqsBuInterfaceComp].Columns.Add (new DataColumn (interfaceCol, System.Type.GetType ("System.String")));
				}
			}

			// PDB and PQS unique interfaces tables
			string[] uniqueInterfaceColumns = {"PdbID", "BuID", "InterfaceID", "AsymChain1", "AsymChain2", 
									"AuthChain1", "AuthChain2", "EntityID1", "EntityID2", "SurfaceArea", "NumOfCopy"};
			buCompTables[PdbBuInterfaces] = new DataTable ("PdbBuInterfaces");
			buCompTables[PqsBuInterfaces] = new DataTable ("PqsBuInterfaces");
			buCompTables[PisaBuInterfaces] = new DataTable ("PisaBuInterfaces");
			foreach (string interfaceCol in uniqueInterfaceColumns)
			{
				buCompTables[PdbBuInterfaces].Columns.Add (new DataColumn (interfaceCol));
				buCompTables[PqsBuInterfaces].Columns.Add (new DataColumn (interfaceCol));
				buCompTables[PisaBuInterfaces].Columns.Add (new DataColumn (interfaceCol));
			}
			buCompTables[PqsBuInterfaces].Columns.Add (new DataColumn ("PqsChain1"));
			buCompTables[PqsBuInterfaces].Columns.Add (new DataColumn ("PqsChain2"));

			// PDB and PQS BuSameInterfaces
			string[] sameInterfaceColumns = {"PdbID", "BuID", "InterfaceID", "SameInterfaceID", "Chain1", "SymmetryString1", 
												"Chain2", "SymmetryString2", "QScore"};
			buCompTables[PdbBuSameInterfaces] = new DataTable ("PdbBuSameInterfaces");
			buCompTables[PqsBuSameInterfaces] = new DataTable ("PqsBuSameInterfaces");
			foreach (string sameInterfaceCol in sameInterfaceColumns)
			{
				buCompTables[PdbBuSameInterfaces].Columns.Add (new DataColumn (sameInterfaceCol));
				buCompTables[PqsBuSameInterfaces].Columns.Add (new DataColumn (sameInterfaceCol));
			}
			// add full symmetry strings to PDB same interfaces table
			buCompTables[PdbBuSameInterfaces].Columns.Add (new DataColumn ("FullSymmetryString1"));
			buCompTables[PdbBuSameInterfaces].Columns.Add (new DataColumn ("FullSymmetryString2"));

			// residue tables
			string[] residuesColumns = {"PdbID", "BuID", "InterfaceID", 
										"Residue1", "SeqID1", "Residue2", "SeqID2", "Distance"};
			buCompTables[PdbInterfaceResidues] = new DataTable ("PdbInterfaceResidues");
			buCompTables[PqsInterfaceResidues] = new DataTable ("PqsInterfaceResidues");
			foreach (string residueCol in residuesColumns)
			{
				buCompTables[PdbInterfaceResidues].Columns.Add (new DataColumn (residueCol));
				buCompTables[PqsInterfaceResidues].Columns.Add (new DataColumn (residueCol));
			}

			// contact tables
			buCompTables[PdbBuContacts] = new DataTable ("PdbBuContacts");
			buCompTables[PqsBuContacts] = new DataTable ("pqsBuContacts");
			string[] contactsColumns = {"PdbID", "BuID", "InterfaceID", 
										"Residue1", "SeqID1", "Atom1", 
										"Residue2", "SeqID2", "Atom2", "Distance"};
			foreach (string contactCol in contactsColumns)
			{
				buCompTables[PdbBuContacts].Columns.Add (new DataColumn (contactCol));
				buCompTables[PqsBuContacts].Columns.Add (new DataColumn (contactCol));
			}
		}
	}
}
