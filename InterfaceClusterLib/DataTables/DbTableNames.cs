using System;

namespace InterfaceClusterLib.DataTables
{
	/// <summary>
	/// Summary description for DbTableNames.
	/// </summary>
	public class GroupDbTableNames
	{
		public static string[] dbTableNames = new string [9];
		
		public const int HomoSeqInfo = 0;
		public const int HomoRepEntryAlign = 1;
		public const int HomoGroupEntryAlign = 2;
		public const int FamilyGroups = 3;
		public const int SgInterfaces = 4;
		public const int InterfaceClusters = 5;
		public const int ReduntCrystForms = 6;
		public const int NonredundantCfGroups = 7;
        public const int SuperInterfaceClusters = 8;

		public GroupDbTableNames()
		{
		}
		
		public static void SetGroupDbTableNames (string dataType)
		{
			switch (dataType.ToLower ())
			{
				case "scop":
					dbTableNames[HomoSeqInfo] = "ScopHomoSeqInfo";
					dbTableNames[HomoRepEntryAlign] = "ScopHomoRepEntryAlign";
					dbTableNames[HomoGroupEntryAlign] = "ScopHomoGroupEntryAlign";
					dbTableNames[FamilyGroups] = "ScopGroups";
					dbTableNames[SgInterfaces] = "ScopSgInterfaces";
					dbTableNames[InterfaceClusters] = "ScopInterfaceClusters";
					dbTableNames[ReduntCrystForms] = "ScopReduntCrystForms";
					dbTableNames[NonredundantCfGroups] = "ScopNonredundantCfGroups";
                    dbTableNames[SuperInterfaceClusters] = "ScopSuperInterfaceClusters";
					break;

				case "pfam":
					dbTableNames[HomoSeqInfo] = "PfamHomoSeqInfo";
					dbTableNames[HomoRepEntryAlign] = "PfamHomoRepEntryAlign";
					dbTableNames[HomoGroupEntryAlign] = "PfamHomoGroupEntryAlign";
					dbTableNames[FamilyGroups] = "PfamGroups";
					dbTableNames[SgInterfaces] = "PfamSgInterfaces";
					dbTableNames[InterfaceClusters] = "PfamInterfaceClusters";
					dbTableNames[ReduntCrystForms] = "PfamReduntCrystForms";
					dbTableNames[NonredundantCfGroups] = "PfamNonredundantCfGroups";
                    dbTableNames[SuperInterfaceClusters] = "PfamSuperInterfaceClusters";
					break;

				case "id50":
					dbTableNames[HomoSeqInfo] = "Id50HomoSeqInfo";
					dbTableNames[HomoRepEntryAlign] = "Id50HomoRepEntryAlign";
					dbTableNames[HomoGroupEntryAlign] = "Id50HomoGroupEntryAlign";
					dbTableNames[FamilyGroups] = "Id50Groups";
					dbTableNames[SgInterfaces] = "Id50SgInterfaces";
					dbTableNames[InterfaceClusters] = "Id50InterfaceClusters";
					dbTableNames[ReduntCrystForms] = "Id50ReduntCrystForms";
					dbTableNames[NonredundantCfGroups] = "Id50NonredundantCfGroups";
                    dbTableNames[SuperInterfaceClusters] = "Id50SuperInterfaceClusters";
					break;

				default:
					dbTableNames[HomoSeqInfo] = "HomoSeqInfo";
					dbTableNames[HomoRepEntryAlign] = "HomoRepEntryAlign";
					dbTableNames[HomoGroupEntryAlign] = "HomoGroupEntryAlign";
					dbTableNames[FamilyGroups] = "FamilyGroups";
					dbTableNames[SgInterfaces] = "SgInterfaces";
					dbTableNames[InterfaceClusters] = "InterfaceClusters";
					dbTableNames[ReduntCrystForms] = "ReduntCrystForms";
					dbTableNames[NonredundantCfGroups] = "NonredundantCfGroups";
                    dbTableNames[SuperInterfaceClusters] = "SuperInterfaceClusters";
					break;
			}
		}
	}
}
