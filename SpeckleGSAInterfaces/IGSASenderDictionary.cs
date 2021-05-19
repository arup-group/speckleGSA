using System;
using System.Collections.Generic;

namespace SpeckleGSAInterfaces
{
  public interface IGSASenderDictionary
  {
    bool Add<T>(T o);

    bool AddRange<T>(List<T> os);

    int Count<T>();
    int Count(Type t);

    List<T> Get<T>();
    Dictionary<Type, List<object>> GetAll();

    void RemoveAll<T>(List<T> os);

    void Clear();
  }
}
