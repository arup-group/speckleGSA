using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SpeckleGSAProxy
{
	public static class Extensions
	{
		#region Lists
		/// <summary>
		/// Splits lists, keeping entities encapsulated by "" together.
		/// </summary>
		/// <param name="list">String to split</param>
		/// <param name="delimiter">Delimiter</param>
		/// <returns>Array of strings containing list entries</returns>
		public static string[] ListSplit(this string list, string delimiter)
		{
			return Regex.Split(list, delimiter + "(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
		}
		#endregion

		#region Comparison
		/// <summary>
		/// Checks if the string contains only digits.
		/// </summary>
		/// <param name="str">String</param>
		/// <returns>True if string contails only digits</returns>
		public static bool IsDigits(this string str)
		{
			foreach (char c in str)
				if (c < '0' || c > '9')
					return false;

			return true;
		}
		#endregion
	}
}
