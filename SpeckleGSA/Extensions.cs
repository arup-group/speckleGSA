using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SpeckleGSA
{
	public static class Extensions
	{
		public static Task ForEachAsync<TSource>(this IEnumerable<TSource> items, Func<TSource, Task> action,	int maxDegreesOfParallelism)
		{
			var actionBlock = new ActionBlock<TSource>(action, new ExecutionDataflowBlockOptions
			{
				MaxDegreeOfParallelism = maxDegreesOfParallelism
			});

			foreach (var item in items)
			{
				actionBlock.Post(item);
			}

			actionBlock.Complete();

			return actionBlock.Completion;
		}
	}
}
