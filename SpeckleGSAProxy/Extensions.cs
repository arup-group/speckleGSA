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
    #region general
    public static List<T> AddIfNotContains<T>(this List<T> list, T val)
    {
      if (val != null && list != null && !list.Contains(val))
      {
        list.Add(val);
      }
      return list;
    }
    #endregion
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

		/// <summary>
		/// Convert degrees to radians.
		/// </summary>
		/// <param name="degrees">Angle in degrees</param>
		/// <returns>Angle in radians</returns>
		public static double ToRadians(this double degrees)
		{
			return degrees * (Math.PI / 180);
		}

		/// <summary>
		/// Converts value from one unit to another.
		/// </summary>
		/// <param name="value">Value to scale</param>
		/// <param name="originalDimension">Original unit</param>
		/// <param name="targetDimension">Target unit</param>
		/// <returns></returns>
		public static double ConvertUnit(this double value, string originalDimension, string targetDimension)
		{
			if (originalDimension == targetDimension)
				return value;

			if (targetDimension == "m")
			{
				switch (originalDimension)
				{
					case "mm":
						return value / 1000;
					case "cm":
						return value / 100;
					case "ft":
						return value / 3.281;
					case "in":
						return value / 39.37;
					default:
						return value;
				}
			}
			else if (originalDimension == "m")
			{
				switch (targetDimension)
				{
					case "mm":
						return value * 1000;
					case "cm":
						return value * 100;
					case "ft":
						return value * 3.281;
					case "in":
						return value * 39.37;
					default:
						return value;
				}
			}
			else
				return value.ConvertUnit(originalDimension, "m").ConvertUnit("m", targetDimension);
		}
	}
}
