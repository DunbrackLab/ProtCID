using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace AuxFuncLib
{
	/// <summary>
	/// Launch a command in command line
	/// </summary>
	public class CmdLauncher
	{
		// add parameters
		private Dictionary<string, string> cmdParameterHash = new Dictionary<string,string> ();

		public CmdLauncher()
		{
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="paramName"></param>
		/// <param name="paramValue"></param>
		public void AddCmdParameters (string paramName, string paramValue)
		{
			if (cmdParameterHash.ContainsKey (paramName))
			{
				cmdParameterHash[paramName] = paramValue;
			}
			else
			{
				cmdParameterHash.Add (paramName, paramValue);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="exeTool"></param>
		public void LaunchCmd (string exeTool)
		{
			try
			{
				ProcessStartInfo processInfo = new ProcessStartInfo();
				Process cmdProcess = null;	
				
				// set properties for the process
				string commandParam = FormatCmdParameterString (exeTool);
				processInfo.CreateNoWindow = true;
				processInfo.UseShellExecute = false;
				processInfo.FileName = "CMD.exe";
				processInfo.Arguments = commandParam;
				cmdProcess = Process.Start( processInfo );
				cmdProcess.WaitForExit ();
			}
			catch (Exception e)
			{
				throw e;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		private string FormatCmdParameterString (string exeTool)
		{
			string cmdParamString = "/C \"" + exeTool + "\"";
			foreach (string paramName in cmdParameterHash.Keys)
			{
				cmdParamString += (" -" + paramName + " " + cmdParameterHash[paramName].ToString ());
			}
			return cmdParamString;
		}
	}
}
