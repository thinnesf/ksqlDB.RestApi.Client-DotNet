﻿using System;
using System.Net.Http;
using FluentAssertions;
using ksqlDB.Api.Client.Tests.Helpers;
using ksqlDB.RestApi.Client.KSql.RestApi.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTests;

namespace ksqlDB.Api.Client.Tests.KSql.RestApi.Http
{
  [TestClass]
  public class HttpClientFactoryTests : TestBase
  {
    [TestMethod]
    public void CreateClient_BaseAddressWasSet()
    {
      //Arrange
      var httpClientFactory = new HttpClientFactory(new Uri(TestParameters.KsqlDBUrl));

      //Act
      var httpClient = httpClientFactory.CreateClient();

      //Assert
      httpClient.Should().BeOfType<HttpClient>();
      httpClient.BaseAddress.OriginalString.Should().BeEquivalentTo(TestParameters.KsqlDBUrl);
    }
  }
}