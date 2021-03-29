﻿using SpeckleGSA.UI.Models;
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
    private static List<SpeckleAccount> rndAccountSource = new List<SpeckleAccount>
    {
      new SpeckleAccount("Aaron Aardvark", "https://hongkong.speckle.arup.com", "aaron.aardvark@arup.com"),
      new SpeckleAccount("Brian Barrelolaughs", "https://canada.speckle.arup.com", "brian.Barrelolaughs@arup.com"),
      new SpeckleAccount("Charlie Chaplin", "https://ireland.speckle.arup.com", "charlie.chaplin@arup.com"),
      new SpeckleAccount("Dan de Man", "https://australia.speckle.arup.com", "dan.deman@arup.com")
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

    public static SpeckleAccount GetAccount()
    {
      return GetRandomItem(rndAccountSource);
    }

    private static T GetRandomItem<T>(List<T> data)
    {
      return data[rnd.Next(0, data.Count())];
    }
  }
}
