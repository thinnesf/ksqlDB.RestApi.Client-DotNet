﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ksqlDB.RestApi.Client.KSql.RestApi.Http;
using ksqlDB.RestApi.Client.KSql.RestApi.Query;
using Microsoft.Extensions.Logging;

namespace ksqlDB.RestApi.Client.KSql.RestApi
{
  internal abstract class KSqlDbProvider : IKSqlDbProvider
  {
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger logger;

    protected KSqlDbProvider(IHttpClientFactory httpClientFactory, ILogger logger = null)
    {
      this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
      this.logger = logger;
    }

    public abstract string ContentType { get; }

    protected abstract string QueryEndPointName { get; }

    protected virtual HttpClient OnCreateHttpClient()
    {
      return httpClientFactory.CreateClient();
    }

    public async Task<QueryStream<T>> RunAsync<T>(object parameters, CancellationToken cancellationToken = default)
    {
      logger?.LogInformation($"Executing query {parameters}");

      var streamReader = await GetStreamReaderAsync<T>(parameters, cancellationToken).ConfigureAwait(false);

      cancellationToken.Register(() => streamReader?.Dispose());

      var queryId = await ReadHeaderAsync<T>(streamReader).ConfigureAwait(false);

      return new QueryStream<T>
      {
        EnumerableQuery = ConsumeAsync<T>(streamReader, cancellationToken),
        QueryId = queryId
      };
    }

    /// <param name="parameters">Query parameters</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the asynchronous operation.</param>
    public async IAsyncEnumerable<T> Run<T>(object parameters, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      logger?.LogInformation($"Executing query {parameters}");

      using var streamReader = await GetStreamReaderAsync<T>(parameters, cancellationToken).ConfigureAwait(false);

      await foreach (var entity in ConsumeAsync<T>(streamReader, cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
        yield return entity;
    }

    private async Task<StreamReader> GetStreamReaderAsync<T>(object parameters, CancellationToken cancellationToken)
    {
      using var httpClient = OnCreateHttpClient();

      var httpRequestMessage = CreateQueryHttpRequestMessage(httpClient, parameters);

      //https://docs.ksqldb.io/en/latest/developer-guide/api/
      var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage,
          HttpCompletionOption.ResponseHeadersRead,
          cancellationToken)
        .ConfigureAwait(false);

#if NET
      var stream = await httpResponseMessage.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
      var stream = await httpResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif

      var streamReader = new StreamReader(stream);

      return streamReader;
    }

    private async IAsyncEnumerable<T> ConsumeAsync<T>(StreamReader streamReader, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
      while (!streamReader.EndOfStream)
      {
        if (cancellationToken.IsCancellationRequested)
          yield break;

        var rawData = await streamReader.ReadLineAsync()
          .ConfigureAwait(false);

        logger?.LogDebug($"Raw data received: {rawData}");

        var record = OnLineRead<T>(rawData);

        if (record != null) yield return record.Value;
      }
    }

    private async Task<string> ReadHeaderAsync<T>(StreamReader streamReader)
    {
      var rawData = await streamReader.ReadLineAsync()
        .ConfigureAwait(false);

      return OnReadHeader<T>(rawData);
    }

    protected abstract string OnReadHeader<T>(string rawJson);

    protected abstract RowValue<T> OnLineRead<T>(string rawJson);

    private JsonSerializerOptions jsonSerializerOptions;

    protected JsonSerializerOptions GetOrCreateJsonSerializerOptions()
    {
      if (jsonSerializerOptions == null)
        jsonSerializerOptions = OnCreateJsonSerializerOptions();

      return jsonSerializerOptions;
    }

    protected virtual JsonSerializerOptions OnCreateJsonSerializerOptions()
    {
      jsonSerializerOptions = new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true
      };

      return jsonSerializerOptions;
    }

    protected virtual HttpRequestMessage CreateQueryHttpRequestMessage(HttpClient httpClient, object parameters)
    {
      var json = JsonSerializer.Serialize(parameters);

      var data = new StringContent(json, Encoding.UTF8, "application/json");
      
      httpClient.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue(ContentType));

      var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, QueryEndPointName)
      {
        Content = data
      };
      
      return httpRequestMessage;
    }

    protected bool IsErrorRow(string rawJson)
    {
      return rawJson.StartsWith("{\"@type\":\"statement_error\"") || rawJson.StartsWith("{\"@type\":\"generic_error\"");
    }
  }
}