using System;
using System.Collections.Generic;

namespace SpeckleGSAInterfaces
{
  public struct ProxyGwaLine
  {
    public string Keyword;
    public int Index;
    public string StreamId;
    public string ApplicationId;
    public string GwaWithoutSet;
    public GwaSetCommandType GwaSetType;
  }

  //Interface class
  public interface IGSAProxy
  {
    //This interface aims to provide an abstracted layer for users to obtain GSA model data for caching purposes, to alter the GSA model and to access its in-built tools.
    //Assumptions of callers of objects implementing this interface:
    //  - callers don't need to deal with raw SIDs - implementations of this interface will extract them from SIDs using a hard-coded tag value
    //  - callers know about GWA representations of Speckle objects
    //  - callers know about keywords, indices and Application IDs
    //  - callers will manage the timing of changes to the model
    //  - callers will have no direct access to the GSA instance
    //Other than offering the ability to store a queue of GWA representations to synchronise with the model at a later point, this class doesn't attempt to manage resources of the GSA instance.

    #region ModelContent
    /// <summary>
    /// Returns a list of (keyword, index, Application ID, GWA command) tuples
    /// </summary>
    /// <param name="command">GET GWA command</param>
    /// <returns>Array of GWA records</returns>
    List<ProxyGwaLine> GetGwaData(IEnumerable<string> keywords, bool nodeApplicationIdFilter, IProgress<int> incrementProgress = null);

    //Queueing up a new addition to the model
    //Assumed to be the full SET or SET_AT command
    void SetGwa(string gwaCommand);

    //Applying the queued changes to the model
    void Sync();
    #endregion

    #region GSATools
    /// <summary>
    /// Create new node at the coordinate. If a node already exists, no new nodes are created. Updates Indexer with the index.
    /// </summary>
    /// <param name="x">X coordinate of the node</param>
    /// <param name="y">Y coordinate of the node</param>
    /// <param name="y">Z coordinate of the node</param>
    /// <param name="coincidenceTol">Coincidence tolerance</param>
    /// <returns>Node index</returns>
    int NodeAt(double x, double y, double z, double coincidenceTol);

    void GetGSATotal2DElementOffset(int index, double insertionPointOffset, out double offset, out string offsetRec);

    /// <summary>
    /// Converts a GSA list to a list of indices.
    /// </summary>
    /// <param name="list">GSA list</param>
    /// <param name="type">GSA entity type</param>
    /// <returns></returns>
    int[] ConvertGSAList(string list, GSAEntity type);

    #endregion

    #region Results
    /// <summary>
    /// General extraction
    /// </summary>
    /// <param name="id">GSA entity ID</param>
    /// <param name="loadCase">Load case</param>
    /// <param name="axis">Result axis</param>
    /// <returns>Dictionary of reactions with keys {x,y,z,xx,yy,zz}.</returns>
    Dictionary<string, object> GetGSAResult(int id, int resHeader, int flags, List<string> keys, string loadCase, string axis = "local", int num1DPoints = 2);

    /// <summary>
    /// Checks if the load case exists in the GSA file
    /// </summary>
    /// <param name="loadCase">GSA load case description</param>
    /// <returns>True if load case exists</returns>
    bool CaseExist(string loadCase);

    #endregion

    string GetUnits();

    string GetTitle();

    char GwaDelimiter { get; }

    bool UpdateCasesAndTasks();

    bool UpdateViews();

    void DeleteGWA(string keyword, int index, GwaSetCommandType gwaSetCommandType);

    string GetGwaForNode(int index);

    string FilePath { get; set; }

    void NewFile(bool showWindow = true, object gsaInstance = null);

    void OpenFile(string path, bool showWindow = true, object gsaInstance = null);

    //Used to update a node without having to BLANK then SET it - which is the case for all other types
    string SetSid(string gwa, string streamId, string applicationId);

    int SaveAs(string filePath);

    string[] GetTolerances();

    bool SetTopLevelSid(string sidRecord);

    string GetTopLevelSid();

    void Close();

    string FormatApplicationIdSidTag(string value);

    string FormatStreamIdSidTag(string value);

    string FormatSidTags(string streamId = "", string applicationId = "");

    //void ParseGeneralGwa(string fullGwa, out string keyword, out int? index, out string streamId, out string applicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType, bool includeKwVersion = false);
  }
}
