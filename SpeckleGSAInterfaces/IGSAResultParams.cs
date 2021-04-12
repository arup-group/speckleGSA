using System.Collections.Generic;

namespace SpeckleGSAInterfaces
{
  public interface IGSAResultParams
  {
    int ResHeader { get; set; }
    int Flags { get; set; }
    List<string> Keys { get; set; } //Field/column headers
    string Keyword { get; set; }
  }
}
