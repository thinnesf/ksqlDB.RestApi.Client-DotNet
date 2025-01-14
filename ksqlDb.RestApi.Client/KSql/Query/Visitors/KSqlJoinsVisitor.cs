﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using ksqlDB.RestApi.Client.Infrastructure.Extensions;
using ksqlDB.RestApi.Client.KSql.Linq;
using ksqlDB.RestApi.Client.KSql.Query.Context;
using Pluralize.NET;

namespace ksqlDB.RestApi.Client.KSql.Query.Visitors
{
  internal class KSqlJoinsVisitor : KSqlVisitor
  {
    private readonly KSqlDBContextOptions contextOptions;
    private readonly QueryContext queryContext;
    private readonly KSqlQueryMetadata queryMetadata;

    public KSqlJoinsVisitor(StringBuilder stringBuilder, KSqlDBContextOptions contextOptions, QueryContext queryContext, KSqlQueryMetadata queryMetadata)
      : base(stringBuilder, queryMetadata)
    {
      this.contextOptions = contextOptions ?? throw new ArgumentNullException(nameof(contextOptions));
      this.queryContext = queryContext ?? throw new ArgumentNullException(nameof(queryContext));
      this.queryMetadata = queryMetadata ?? throw new ArgumentNullException(nameof(queryMetadata));
    }

    private readonly Dictionary<string, string> aliasDictionary = new();

    private string GenerateAlias(string name)
    {
      if (aliasDictionary.ContainsKey(name))
        return aliasDictionary[name];

      var streamAlias = name[0].ToString();

      int i = 0;

      var streamAliasAttempt = streamAlias;

      while (aliasDictionary.Values.Any(c => c == streamAliasAttempt))
      {
        streamAliasAttempt = $"{streamAlias}{++i}";
      }

      aliasDictionary.Add(name, streamAliasAttempt);

      return streamAliasAttempt;
    }

    private PropertyInfo GetPropertyType(Type type)
    {
      var propertyInfo = type.GetProperties()[0];

      bool isAnonymous = propertyInfo.PropertyType.IsAnonymousType();

      if (isAnonymous)
        return GetPropertyType(propertyInfo.PropertyType);

      return propertyInfo;
    }

    internal void VisitJoinTable(IEnumerable<(MethodInfo, IEnumerable<Expression>, LambdaExpression)> joins)
    {
      bool isFirst = true;

      foreach (var join in joins)
      {
        var (methodInfo, e, groupJoin) = join;

        var expressions = e.ToArray();
      
        expressions = expressions.Select(StripQuotes).ToArray();

        Visit(expressions[0]);

        var outerItemAlias = GenerateAlias(queryContext.FromItemName);

        var itemAlias = GenerateAlias(fromItemName);

        if (groupJoin != null)
        {
          var prop = GetPropertyType(groupJoin.Parameters[0].Type);

          outerItemAlias = prop.Name;

          itemAlias = groupJoin.Parameters[1].Name;
        }

        var lambdaExpression = expressions[3] as LambdaExpression;

        if (isFirst)
        {
          isFirst = false;

          Append("SELECT ");

          var body = queryMetadata.Select?.Body ?? lambdaExpression?.Body;

          body = groupJoin != null ? groupJoin.Body : body;

          new KSqlJoinSelectFieldsVisitor(StringBuilder, queryMetadata).Visit(body);

          var fromItemAlias = queryMetadata.Joins.Where(c => c.Type == queryMetadata.FromItemType && !string.IsNullOrEmpty(c.Alias)).Select(c => c.Alias).FirstOrDefault();
          
          outerItemAlias = fromItemAlias ?? outerItemAlias;

          AppendLine($" FROM {queryContext.FromItemName} {outerItemAlias}");
        }

        var joinType = methodInfo.Name switch
        {
          nameof(QbservableExtensions.Join) => "INNER",
          nameof(QbservableExtensions.LeftJoin) => "LEFT",
          nameof(QbservableExtensions.GroupJoin) => "LEFT",
          nameof(QbservableExtensions.FullOuterJoin) => "FULL OUTER",
          _ => throw new ArgumentOutOfRangeException()
        };

        var itemType = join.Item2.First().Type.GetGenericArguments()[0];
        
        var joinItemAlias = queryMetadata.Joins.Where(c => c.Type == itemType && !string.IsNullOrEmpty(c.Alias)).Select(c => c.Alias).FirstOrDefault();

        itemAlias = joinItemAlias ?? itemAlias;

        AppendLine($"{joinType} JOIN {fromItemName} {itemAlias}");

        TryAppendWithin(@join);

        Append($"ON {outerItemAlias}.");
        Visit(expressions[1]);
        Append($" = {itemAlias}.");
        Visit(expressions[2]);
        Append(Environment.NewLine);
      }
    }

    private void TryAppendWithin((MethodInfo, IEnumerable<Expression>, LambdaExpression) @join)
    {
      var sourceExpression = @join.Item2.First() as ConstantExpression;

      if (sourceExpression?.Value is SourceBase source && (source.DurationBefore != null || source.DurationAfter != null))
      {
        Append("WITHIN ");

        if (source.DurationBefore != null && source.DurationAfter != null)
        {
          Append($"({source.DurationBefore.Value} {source.DurationBefore.TimeUnit}, ");
          Append($"{source.DurationAfter.Value} {source.DurationAfter.TimeUnit}) ");
        }
        else
        {
          var duration = source.DurationBefore ?? source.DurationAfter;

          Append($"{duration.Value} {duration.TimeUnit} ");
        }
      }
    }

    private string fromItemName;

    private static readonly IPluralize EnglishPluralizationService = new Pluralizer();

    protected virtual string InterceptFromItemName(string value)
    {
      if (contextOptions.ShouldPluralizeFromItemName)
        return EnglishPluralizationService.Pluralize(value);

      return value;
    }

    protected override Expression VisitMember(MemberExpression memberExpression)
    {
      if (memberExpression == null) throw new ArgumentNullException(nameof(memberExpression));

      if (memberExpression.Expression.NodeType == ExpressionType.Parameter)
      {
        Append(memberExpression.Member.Name);

        return memberExpression;
      }

      var fromItem = queryMetadata.Joins.FirstOrDefault(c => c.Type == memberExpression.Member.DeclaringType);

      if (queryMetadata.Joins != null && memberExpression.Expression.NodeType == ExpressionType.MemberAccess)
      {
        Append(memberExpression.Member.Name);
      }
      else
        base.VisitMember(memberExpression);

      return memberExpression;
    }

    protected override Expression VisitConstant(ConstantExpression constantExpression)
    {
      if (constantExpression == null) throw new ArgumentNullException(nameof(constantExpression));

      if (constantExpression.Value is ISource source)
      {
        fromItemName = constantExpression.Type.GenericTypeArguments[0].Name;

        fromItemName = source?.QueryContext?.FromItemName ?? fromItemName;

        fromItemName = InterceptFromItemName(fromItemName);
      }

      return constantExpression;
    }
  }
}