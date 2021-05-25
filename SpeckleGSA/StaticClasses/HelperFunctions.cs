using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SpeckleGSAInterfaces;

namespace SpeckleGSA
{
  /// <summary>
  /// Static class containing helper functions used throughout SpeckleGSA
  /// </summary>
  public static class HelperFunctions
  {
    #region Math
    /// <summary>
    /// Convert degrees to radians.
    /// </summary>
    /// <param name="degrees">Angle in degrees</param>
    /// <returns>Angle in radians</returns>
    public static double ToRadians(this double degrees)
    {
      return degrees * (Math.PI / 180);
    }
    #endregion

    #region Unit Conversion
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

    /// <summary>
    /// Converts short unit name to long unit name
    /// </summary>
    /// <param name="unit">Short unit name</param>
    /// <returns>Long unit name</returns>
    public static string LongUnitName(this string unit)
    {
      switch (unit.ToLower())
      {
        case "m":
          return "meters";
        case "mm":
          return "millimeters";
        case "cm":
          return "centimeters";
        case "ft":
          return "feet";
        case "in":
          return "inches";
        default:
          return unit;
      }
    }

    /// <summary>
    /// Converts long unit name to short unit name
    /// </summary>
    /// <param name="unit">Long unit name</param>
    /// <returns>Short unit name</returns>
    public static string ShortUnitName(this string unit)
    {
      switch (unit.ToLower())
      {
        case "meters":
          return "m";
        case "millimeters":
          return "mm";
        case "centimeters":
          return "cm";
        case "feet":
          return "ft";
        case "inches":
          return "in";
        default:
          return unit;
      }
    }
		#endregion

		#region Miscellaneous

		/// <summary>
		/// Extract attribute from attribute type.
		/// </summary>
		/// <param name="t">GSAObject objects or type</param>
		/// <param name="attribute">Attribute to extract</param>
		/// <returns>Attribute value</returns>
		public static object GetAttribute(this object t, string attribute)
    {
			var attributeType = typeof(GSAObject);
      try
      {
				var attObj = (t is Type) ? Attribute.GetCustomAttribute((Type)t, attributeType) : Attribute.GetCustomAttribute(t.GetType(), attributeType);
				return attributeType.GetProperty(attribute).GetValue(attObj);
			}
      catch { return null; }
    }

    public static Func<Action, string, string, bool> tryCatchWithEvents = (action, msgSuccessful, msgFailure) =>
    {
      bool success = false;
      try
      {
        action();
        success = true;
      }
      catch (Exception ex)
      {
        if (!string.IsNullOrEmpty(msgFailure))
        {
          GSA.GsaApp.Messenger.Message(MessageIntent.Display, MessageLevel.Error, msgFailure, GSA.App.LocalSettings.VerboseErrors ? ex.Message : null);
          GSA.GsaApp.Messenger.Message(MessageIntent.TechnicalLog, MessageLevel.Error, ex, msgFailure);
        }
      }
      if (success)
      {
        if (!string.IsNullOrEmpty(msgSuccessful))
        {
          GSA.GsaApp.Messenger.Message(MessageIntent.Display, MessageLevel.Information, msgSuccessful);
        }
      }
      return success;
    };

    public static string Combine(string uri1, string uri2)
    {
      uri1 = uri1.TrimEnd('/');
      uri2 = uri2.TrimStart('/');
      return string.Format("{0}/{1}", uri1, uri2);
    }

    public static List<string> Combine(string param, List<string> endParams)
    {
      return (new List<string>() { param }).Concat(endParams).ToList();
    }

    public static bool GetSidSpeckleRecords(string emailAddress, string serverAddress, IGSAProxy proxy, 
      out List<SidSpeckleRecord> receiverStreamInfo, out List<SidSpeckleRecord> senderStreamInfo)
    {
      receiverStreamInfo = new List<SidSpeckleRecord>();
      senderStreamInfo = new List<SidSpeckleRecord>();

      try
      {
        string key = emailAddress + "&" + serverAddress.Replace(':', '&');

        string res = proxy.GetTopLevelSid();

        if (res == "")
        {
          return true;
        }

        List<string[]> sids = Regex.Matches(res, @"(?<={).*?(?=})").Cast<Match>()
                .Select(m => m.Value.Split(new char[] { ':' }))
                .Where(s => s.Length == 2)
                .ToList();

        string[] senderList = sids.Where(s => s[0] == "SpeckleSender&" + key).FirstOrDefault();
        string[] receiverList = sids.Where(s => s[0] == "SpeckleReceiver&" + key).FirstOrDefault();

        if (senderList != null && !string.IsNullOrEmpty(senderList[1]))
        {
          string[] senders = senderList[1].Split(new char[] { '&' });

          for (int i = 0; i < senders.Length; i += 3)
          {
            senderStreamInfo.Add(new SidSpeckleRecord(senders[i + 1], senders[i], senders[i + 2]));
          }
        }

        if (receiverList != null && !string.IsNullOrEmpty(receiverList[1]))
        {
          string[] receivers = receiverList[1].Split(new char[] { '&' });

          for (int i = 0; i < receivers.Length; i += 2)
          {
            receiverStreamInfo.Add(new SidSpeckleRecord(receivers[i], receivers[i + 1]));
          }
        }
        return true;
      }
      catch
      {
        // If fail to read, clear client SIDs
        //SenderInfo.Clear();
        //ReceiverInfo.Clear();
        return SetSidSpeckleRecords(emailAddress, serverAddress, proxy, null, null);
      }
    }

    public static bool SetSidSpeckleRecords(string emailAddress, string serverAddress, IGSAProxy proxy, 
      List<SidSpeckleRecord> receiverStreamInfo, List<SidSpeckleRecord> senderStreamInfo)
    {
      string key = emailAddress + "&" + serverAddress.Replace(':', '&');
      string res = GSA.App.Proxy.GetTopLevelSid();

      List<string[]> sids = Regex.Matches(res, @"(?<={).*?(?=})").Cast<Match>()
              .Select(m => m.Value.Split(new char[] { ':' }))
              .Where(s => s.Length == 2)
              .ToList();

      sids.RemoveAll(S => S[0] == "SpeckleSender&" + key || S[0] == "SpeckleReceiver&" + key || string.IsNullOrEmpty(S[1]));

      List<string> senderList = new List<string>();
      if (senderStreamInfo != null)
      {
        foreach (var si in senderStreamInfo)
        {
          senderList.AddRange(new[] { si.Bucket, si.StreamId, si.ClientId });
        }
        if (senderList.Count() > 0)
        {
          sids.Add(new string[] { "SpeckleSender&" + key, string.Join("&", senderList) });
        }
      }

      List<string> receiverList = new List<string>();
      if (receiverStreamInfo != null)
      {
        foreach (var si in receiverStreamInfo)
        {
          receiverList.AddRange(new[] { si.StreamId, si.Bucket });
        }
        if (receiverList.Count() > 0)
        {
          sids.Add(new string[] { "SpeckleReceiver&" + key, string.Join("&", receiverList) });
        }
      }

      string sidRecord = "";
      foreach (string[] s in sids)
      {
        sidRecord += "{" + s[0] + ":" + s[1] + "}";
      }

      return proxy.SetTopLevelSid(sidRecord);
    }
    #endregion
  }
}
