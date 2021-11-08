﻿using System.Linq.Expressions;
using ksqlDB.RestApi.Client.KSql.Query.Context;
using ksqlDB.RestApi.Client.KSql.Query.Windows;

namespace ksqlDB.RestApi.Client.KSql.Linq
{
  public class SourceBase
  {
    internal Duration Duration { get; set; }
  }

  public class Source<T> : SourceBase, ISource<T>
  {
    public QueryContext QueryContext { get; set; }

    public Source(QueryContext queryContext)
    {
      Expression = Expression.Constant(this);

      QueryContext = queryContext;
    }
    
    public Expression Expression { get; }
  }

  public static class Source
  {
    public static ISource<T> Of<T>(string fromItemName = null)
    {      
      var queryStreamContext = new QueryContext
      {
        FromItemName = fromItemName
      };

      return new Source<T>(queryStreamContext);
    }
  }
}