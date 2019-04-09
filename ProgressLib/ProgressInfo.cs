using System;
using System.Collections.Generic;

namespace ProgressLib
{
	/// <summary>
	/// Summary description for ProgressInfo.
	/// </summary>
	public class ProgressInfo
	{
		public ProgressInfo()
		{
			//
			// TODO: Add constructor logic here
			//
		}

		public void Reset()
		{
			overallProgLabel = "";
			//currentOverallOperationNum = 0;
			//totalOverallOperationNum = 0;
			progStrQueue.Clear ();
			currentOperationIndex = 0;
			overallElapsedTime = DateTime.Now - DateTime.Now;

			ResetCurrentProgressInfo();
		}

		public void ResetCurrentProgressInfo()
		{
			// for current progress information 		
			currentOperationLabel = "";

			currentOperationNum = 0;
			totalOperationNum = 0;

			currentStepNum = 0;
			totalStepNum = 0;
			currentFileName = "";
			currentElapsedTime = DateTime.Now - DateTime.Now;
			threadFinished = false;
			threadAborted = false;
		}

		// constant maximum value for a progress bar
		public const int maxProgVal = 20;
		// for overall progress information
		public string overallProgLabel = "";
		public TimeSpan overallElapsedTime = DateTime.Now - DateTime.Now;
		public TimeSpan currentElapsedTime = DateTime.Now - DateTime.Now;

		// for current progress information 
		// processing status string
		//public string progressString = "";
		public Queue<string> progStrQueue = new Queue<string> ();
		// the index for current operation
		public int currentOperationIndex = 0;
		public string currentOperationLabel = "";

		public int currentOperationNum = 0;
		public int totalOperationNum = 0;

		public int currentStepNum = 0;
		public int totalStepNum = 0;
		public string currentFileName = "";
		// thread finishs
		public bool threadFinished = false;
		// thread aborted, errors
		public bool threadAborted = false;

		// progress step 
		public int progressInterval = 0;
	}
}
