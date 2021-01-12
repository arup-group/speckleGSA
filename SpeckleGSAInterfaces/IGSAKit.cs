using System;
using System.Collections.Generic;

namespace SpeckleGSAInterfaces
{
  public interface IGSAKit
  {
    void Clear();

    //These are set by the app, for use by the kit
    IGSASettings Settings { get; set; }
    IGSAProxy Interface { get; set; }
    IGSACacheForKit Cache { get; set; }
    ISpeckleGSAAppUI AppUI { get; set; }

    //These are set by the kit, for use by the app

    //The variable below must be a property (i.e. with { get; }) and of Dictionary<Type, List<object>> type so that SpeckleGSA
    //can recognise this as a kit it can work with
    IGSASenderDictionary GSASenderObjects { get; }
    //This is a dictionary of types (and their keywords for advance GSA record index reservation) which can be processed in parallel 
    //ensure the order they appear in GSA matches the order in the stream.
    //In most cases a type is added to this list if:
    //- in practice objects of the type are one of the most numerous in a typical stream
    //- indices can be managed as a COM client - for doesn't work for nodes since the NodeAt does it
    //- there is a simple 1:1 relationship between objects of that type and GSA records, enabling GSA record indices to be reserved first, 
    //  then processed in parallel
    Dictionary<Type, string> RxParallelisableTypes { get; }
    Dictionary<Type, List<Type>> RxTypeDependencies { get; }
    Dictionary<Type, List<Type>> TxTypeDependencies { get; }
    List<string> Keywords { get; }
  }
}
