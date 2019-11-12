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

    public static void ExtractKeywordApplicationId(this string fullGwa, out string keyword, out int? index, out string applicationId, out string gwaWithoutSet)
    {
      var pieces = fullGwa.ListSplit("\t").ToList();
      keyword = "";
      applicationId = null;
      index = null;
      gwaWithoutSet = fullGwa;

      if (pieces.Count() < 2)
      {
        return;
      }

      //Remove the Set for the purpose of this method
      if (pieces[0].StartsWith("set", StringComparison.InvariantCultureIgnoreCase))
      {
        if (pieces[0].StartsWith("set_at", StringComparison.InvariantCultureIgnoreCase))
        {
          if (int.TryParse(pieces[1], out int foundIndex))
          {
            index = foundIndex;
          }

          //For SET_ATs the format is SET_AT <index> <keyword> .., so remove the first two
          pieces.Remove(pieces[1]);
          pieces.Remove(pieces[0]);
        }
        else
        {
          if (int.TryParse(pieces[2], out int foundIndex))
          {
            index = foundIndex;
          }
          
          index = foundIndex;

          pieces.Remove(pieces[0]);
        }
      }
      else
      {
        if (int.TryParse(pieces[1], out int foundIndex))
        {
          index = foundIndex;
        }
      }

      var delimIndex = pieces[0].IndexOf(':');
      if (delimIndex > 0)
      {
        //An SID has been found
        keyword = pieces[0].Substring(0, delimIndex);
        var sid = pieces[0].Substring(delimIndex);
        var match = Regex.Match(sid, "(?<={" + "speckle_app_id" + ":).*?(?=})");
        applicationId = (!string.IsNullOrEmpty(match.Value)) ? match.Value : "";
      }
      else
      {
        keyword = pieces[0];
      }

      gwaWithoutSet = string.Join("\t", pieces);
      return;
    }

    public static bool SidValueCompare(this string a, string b)
    {
      if (a == null && b == null)
      {
        return true;
      }
      else if (b == null)
      {
        return false;
      }
      return a.Replace(" ", string.Empty).Equals(b.Replace(" ", string.Empty), StringComparison.InvariantCultureIgnoreCase);
    }

    public static string ExtractApplicationId(this string fullGwa)
    {
      var pieces = fullGwa.ListSplit("\t").ToList();

      if (pieces.Count() < 2)
      {
        return "";
      }

      //Remove the Set for the purpose of this method
      if (pieces[0].StartsWith("set", StringComparison.InvariantCultureIgnoreCase))
      {
        pieces.Remove(pieces[0]);
      }

      var delimIndex = pieces[0].IndexOf(':');
      if (delimIndex > 0)
      {
        //An SID has been found
        var match = Regex.Match(pieces[0].Substring(delimIndex), "(?<={" + "speckle_app_id" + ":).*?(?=})");

        return (!string.IsNullOrEmpty(match.Value)) ? match.Value : "";
      }
      else
      {
        return "";
      }
    }
  }
}
