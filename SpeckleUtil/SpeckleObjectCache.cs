using SpeckleCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpeckleUtil
{
  public class SpeckleObjectCache
  {
    private readonly Dictionary<Tuple<Type, string>, SpeckleObject> cache = new Dictionary<Tuple<Type, string>, SpeckleObject>();
    private readonly List<Type> typesAdded = new List<Type>();

    public void ClearCache()
    {
      cache.Clear();
      typesAdded.Clear();
    }

    public bool AddList(Type t, List<SpeckleObject> objects)
    {      
      var speckleObjects = objects.Where(o => o is SpeckleObject).Where(so => so.ApplicationId != null).ToList();
      for (int i = 0; i < speckleObjects.Count(); i++)
      {
        var key = new Tuple<Type, string>(t, speckleObjects[i].ApplicationId);
        cache[key] = speckleObjects[i];
      }
      if (!typesAdded.Contains(t))
      {
        typesAdded.Add(t);
      }
      return true;
    }

    public SpeckleObject GetCachedSpeckleObject(Type t, string applicationId)
    {
      if (string.IsNullOrEmpty(applicationId))
      {
        return null;
      }
      var key = new Tuple<Type, string>(t, applicationId);
      if (cache.ContainsKey(key))
      {
        return cache[key];
      }
      else
      {
        return null;
      }
    }

    public bool ContainsType(Type t)
    {
      return typesAdded.Contains(t);
    }

    public void Add(SpeckleObject speckleObject, Type t)
    {
      var key = new Tuple<Type, string>(t, speckleObject.ApplicationId);
      cache[key] = speckleObject;
    }
  }
}
