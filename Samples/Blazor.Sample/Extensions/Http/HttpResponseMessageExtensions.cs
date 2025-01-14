﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ksqlDB.RestApi.Client.KSql.RestApi.Extensions;
using ksqlDB.RestApi.Client.KSql.RestApi.Statements;

namespace Blazor.Sample.Extensions.Http
{
  public static class HttpResponseMessageExtensions
  {
    public static StatementResponse[] ConvertToStatementResponses(this HttpResponseMessage httpResponseMessage)
    {
      StatementResponse[] statementResponses;

      if (httpResponseMessage.IsSuccessStatusCode)
      {
        statementResponses = httpResponseMessage.ToStatementResponses();
      }
      else
      {
        statementResponses = new[] { httpResponseMessage.ToStatementResponse()};
      }

      return statementResponses;
    }
  }
}