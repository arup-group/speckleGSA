using SpeckleGSA.UI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA.UI.DataAccess
{
  public class DataAccess
  {

    private static Random rnd = new Random();
    private static List<StreamListItem> rndStreamSource = new List<StreamListItem>
    {
      new StreamListItem("A", "Stream A"),
      new StreamListItem("B", "Stream B"),
      new StreamListItem("C", "Stream C"),
      new StreamListItem("D", "Stream D"),
      new StreamListItem("E", "Stream E"),
      new StreamListItem("F", "Stream F"),
      new StreamListItem("G", "Stream G")
    };

    private static List<SpeckleAccountForUI> rndAccountSource = new List<SpeckleAccountForUI>
    {
      new SpeckleAccountForUI("Aaron Aardvark", "https://hongkong.speckle.arup.com", "aaron.aardvark@arup.com", "token1"),
      new SpeckleAccountForUI("Brian Barrelolaughs", "https://canada.speckle.arup.com", "brian.Barrelolaughs@arup.com", "token2"),
      new SpeckleAccountForUI("Charlie Chaplin", "https://ireland.speckle.arup.com", "charlie.chaplin@arup.com", "token3"),
      new SpeckleAccountForUI("Dan de Man", "https://australia.speckle.arup.com", "dan.deman@arup.com", "token4")
    };

    private static List<string> rndFilePaths = new List<string>
    {
      @"C:\Temp\Source1.gwb",
      @"C:\Workspace\MyAwesomeFile.gwb",
      @"C:\Repo\TestData\FirstFile.gwb"
    };

    public static StreamList GetStreamList()
    {
      var streamList = new StreamList
      {
        StreamListItems = new List<StreamListItem>()
        {
          GetRandomItem(rndStreamSource),
          GetRandomItem(rndStreamSource)
        }
      };

      return streamList;
    }

    public static SpeckleAccountForUI GetAccount() => GetRandomItem(rndAccountSource);

    public static SpeckleAccountForUI GetDefaultAccount() => null;

    public static string GetFilePath() => GetRandomItem(rndFilePaths);

    private static T GetRandomItem<T>(List<T> data)
    {
      return data[rnd.Next(0, data.Count())];
    }
  }
}
