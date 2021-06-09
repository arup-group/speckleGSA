using SpeckleGSAInterfaces;
using SpeckleGSAProxy;
using System.Collections.Generic;
using System.Linq;

namespace SpeckleGSAUI.Test
{
  public static class MockGSAProxyMethods
  {
    public delegate void ParseCallback(string fullGwa, out string keyword, out int? index, out string streamId, out string applicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType, bool includeKwVersion = false);

    public static int nodeIndex = 0;
    //Copied over from the GSAProxy
    public static Dictionary<string, string[]> IrregularKeywordGroups = new Dictionary<string, string[]> {
      { "LOAD_BEAM", new string[] { "LOAD_BEAM_POINT", "LOAD_BEAM_UDL", "LOAD_BEAM_LINE", "LOAD_BEAM_PATCH", "LOAD_BEAM_TRILIN" } }
    };
    private static readonly string SID_APPID_TAG = "speckle_app_id";
    private static readonly string SID_STRID_TAG = "speckle_stream_id";

    public static int[] ConvertGSAList(string list, GSAEntity type)
    {
      var elements = list.Split(new[] { ' ' });

      var indices = new List<int>();
      foreach (var e in elements)
      {
        if (e.All(c => char.IsDigit(c)) && int.TryParse(e, out int index))
        {
          indices.Add(index);
        }
      }

      //It's assumed for now that any list of GSA indices that would correspond to the App IDs in the list would be a sequence from 1
      return indices.ToArray();
    }

    public static int NodeAt(double x, double y, double z, double coincidenceTol) => ++nodeIndex;

    public static string FormatApplicationIdSidTag(string value)
    {
      return (string.IsNullOrEmpty(value) ? "" : "{" + SID_APPID_TAG + ":" + value.Replace(" ", "") + "}");
    }

    public static string FormatStreamIdSidTag(string value)
    {
      return (string.IsNullOrEmpty(value) ? "" : "{" + SID_STRID_TAG + ":" + value.Replace(" ", "") + "}");
    }

    public static string FormatSidTags(string streamId = "", string applicationId = "")
    {
      return FormatStreamIdSidTag(streamId) + FormatApplicationIdSidTag(applicationId);
    }
  }
}
