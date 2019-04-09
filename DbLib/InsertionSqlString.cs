using System;
using System.Collections;
using System.Text;
using System.Diagnostics;

namespace DbLib
{
	/// <summary>
	/// Summary description for InsertSqlString.
	/// </summary>
	public class InsertionSqlString
	{
		Hashtable columnValueHashtable = new Hashtable();
		string tableName;
		/// <summary>
		/// constructor
		/// </summary>
		public InsertionSqlString()
		{
		}
		/// <summary>
		/// Constructs InsertSqlString object
		/// </summary>
		/// <param name="table">table name to insert to</param>
		public InsertionSqlString(string tableName)
		{
			this.tableName = tableName;
		}

		/// <summary>
		/// Adds item to Insert object
		/// </summary>
		/// <param name="name">item name</param>
		/// <param name="val">item value</param>
		public void AddKeyValuePair(string columnName, object columnValue)
		{
			columnValueHashtable.Add(columnName, columnValue);
		}
		/// <summary>
		/// clear the hashtable
		/// </summary>
		public void ClearSqlInsertString()
		{
			columnValueHashtable.Clear ();
		}
		
		/// <summary>
		/// format the insert string
		/// e.g. Insert Into tableName(column 1, column 2, ...)
		/// Values ('string value', numeric value, ...)
		/// </summary>
		/// <returns>System.String</returns>
		public override string ToString()
		{
			StringBuilder columnsStringBuilder = new StringBuilder();
			StringBuilder valuesStringBuilder = new StringBuilder();

			// retrive the key and value pair from the hash table 
			IDictionaryEnumerator enumInterface = columnValueHashtable.GetEnumerator();
			bool first = true;
			while(enumInterface.MoveNext())
			{
				if (first) 
					first = false;
				else 
				{
					columnsStringBuilder.Append(", ");
					valuesStringBuilder.Append(", ");
				}
				columnsStringBuilder.Append(enumInterface.Key.ToString());
				
				valuesStringBuilder.Append ("'");
				// if there is ' inside a string value, put another ' to skip it
				// this is only used for Firebird 
				string valueString = enumInterface.Value.ToString();
				string singleQuotSkipedValue = valueString.Replace( "\'", "\'\'" );
				valuesStringBuilder.Append( singleQuotSkipedValue );
				valuesStringBuilder.Append ("'");				
			}
			
			return "INSERT INTO " + tableName + " (" + columnsStringBuilder + ") VALUES (" + valuesStringBuilder + ");";
		}

		/// <summary>
		/// Gets or sets item into Insert object
		/// </summary>
		object this[string columnName]
		{
			get {Debug.Assert(columnValueHashtable.Contains(columnName), "column name not found"); return columnValueHashtable[columnName];}
			set {columnValueHashtable[columnName]=value;}
		}

		public bool IsNumeric(string aString) 
		{ 	
			bool isNumeric = false;
			// if only minus symbol
			if (aString == "-")
				return false;

			// + allows in the first,don't allow in middle of the string 
			// - allows in the first,don't allow in middle of the string 
			// , allows in the middle,don't allow in first char of the string 
			// . allows in the first,middle, allows in all the indexs 
			for(int i = 0; i < aString.Length; i++) 
			{ 
				//for 1st indexchar 
				char pChar = char.Parse(aString.Substring(i,1)); 

				if(isNumeric) 
					isNumeric = false; 

				if((!isNumeric) &&(aString.IndexOf(pChar)==0)) 
				{ 
					isNumeric = ( "+-.0123456789".IndexOf(pChar) > -1 ) ? true : false; 
				} 
				//for middle characters 
				if((!isNumeric) && (aString.IndexOf(pChar)>0) && (aString.IndexOf(pChar) < (aString.Length-1))) 
				{ 
					isNumeric = ( ",.0123456789".IndexOf(pChar) > -1 ) ? true : false; 
				} 
				//for last characters 
				if((!isNumeric) && (aString.IndexOf(pChar) == (aString.Length-1))) 
				{ 
					isNumeric = ( "0123456789".IndexOf(pChar) > -1 ) ? true : false; 
				} 
				if(!isNumeric) break; 
			} 

			return isNumeric; 
		} 
	}
}
