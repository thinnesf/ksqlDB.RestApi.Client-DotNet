﻿using ksqlDB.RestApi.Client.KSql.Query;

namespace ksqlDB.Api.Client.Tests.Models
{
  public class Person : Record
  {
    public string FirstName { get; set; }

    public string LastName { get; set; }
  }
}