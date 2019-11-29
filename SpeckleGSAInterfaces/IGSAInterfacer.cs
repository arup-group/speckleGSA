using System.Collections.Generic;

namespace SpeckleGSAInterfaces
{
	//Interface class
	public interface IGSAInterfacer
	{
		IGSAIndexer Indexer { get; set; }

		#region GWA Command
		/// <summary>
		/// Returns a list of GWA records with the index of the record prepended.
		/// </summary>
		/// <param name="command">GET GWA command</param>
		/// <returns>Array of GWA records</returns>
		string[] GetGWARecords(string command);

		/// <summary>
		/// Returns a list of deleted GWA records with the index of the record prepended.
		/// </summary>
		/// <param name="command">GET GWA command</param>
		/// <returns>Array of GWA records</returns>
		string[] GetDeletedGWARecords(string command);

		/// <summary>
		/// Runs a GWA command with the option to cache GET and SET commands.
		/// </summary>
		/// <param name="command">GWA command</param>
		/// <param name="cache">Use cache</param>
		/// <returns>GWA command return object</returns>
		object RunGWACommand(string command, bool cache = true);

		/// <summary>
		/// BLANKS all SET GWA records which are in the previous cache, but not the current cache.
		/// </summary>
		void BlankDepreciatedGWASetCommands();
		#endregion

		#region Nodes
		/// <summary>
		/// Create new node at the coordinate. If a node already exists, no new nodes are created. Updates Indexer with the index.
		/// </summary>
		/// <param name="keyword">GSA keyword a node</param>
		/// <param name="typeName">name of the type of the node</param>
		/// <param name="x">X coordinate of the node</param>
		/// <param name="y">Y coordinate of the node</param>
		/// <param name="z">Z coordinate of the node</param>
		/// <param name="applicationId">Application ID of the node</param>
		/// <returns>Node index</returns>
		int NodeAt(string keyword, string typeName, double x, double y, double z, double coincidentNodeAllowance, string applicationId = null);
		#endregion

    /*
		#region Polyline and Grids
		(string, string) GetPolylineDesc(int polylineRef);

		(int, string) GetGridPlaneRef(int gridSurfaceRef);

		(int, double, string) GetGridPlaneData(int gridPlaneRef);
		#endregion
  */

		#region Elements
		(double, string) GetGSATotal2DElementOffset(int propIndex, double insertionPointOffset);
		#endregion

		#region List
		/// <summary>
		/// Converts a GSA list to a list of indices.
		/// </summary>
		/// <param name="list">GSA list</param>
		/// <param name="type">GSA entity type</param>
		/// <returns></returns>
		int[] ConvertGSAList(string list, GSAEntity type);

		/// <summary>
		/// Converts a named GSA list to a list of indices.
		/// </summary>
		/// <param name="list">GSA list</param>
		/// <param name="type">GSA entity type</param>
		/// <returns></returns>
		int[] ConvertNamedGSAList(string list, GSAEntity type);
		#endregion

		#region Cache
		/// <summary>
		/// Move current cache into previous cache.
		/// </summary>
		void ClearCache();

		/// <summary>
		/// Clear current and previous cache.
		/// </summary>
		void FullClearCache();

		/// <summary>
		/// Blanks all records within the current and previous cache.
		/// </summary>
		void DeleteSpeckleObjects();
		#endregion

		#region SID

		string GetSID(string keyword, int id);

		string GetSID();
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
	}
}
