﻿using ksqlDB.RestApi.Client.KSql.Query;

namespace ksqlDB.Api.Client.Tests.Models
{
  internal class Transaction : Record
  {
    public string CardNumber { get; set; }
    public decimal Amount { get; set; }
  }
}