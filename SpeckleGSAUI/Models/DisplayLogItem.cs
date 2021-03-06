﻿using System;

namespace SpeckleGSAUI.Models
{
  public class DisplayLogItem
  {
    public DateTime TimeStamp { get; set; }
    public string Description { get; set; }

    public DisplayLogItem(string description)
    {
      this.TimeStamp = DateTime.Now;
      this.Description = description;
    }
  }
}