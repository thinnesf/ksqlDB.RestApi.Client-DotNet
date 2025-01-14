﻿using System;
using System.Threading.Tasks;
using ksqlDB.RestApi.Client.Infrastructure.Extensions;
using ksqlDb.RestApi.Client.Infrastructure.Logging;
using ksqlDB.RestApi.Client.KSql.Disposables;
using ksqlDB.RestApi.Client.KSql.Linq;
using ksqlDB.RestApi.Client.KSql.Linq.Statements;
using ksqlDb.RestApi.Client.KSql.Query.Context.Options;
using ksqlDB.RestApi.Client.KSql.RestApi;
using ksqlDB.RestApi.Client.KSql.RestApi.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ksqlDB.RestApi.Client.KSql.Query.Context
{
  public abstract class KSqlDBContextDependenciesProvider : AsyncDisposableObject
  {
    private readonly KSqlDBContextOptions kSqlDbContextOptions;
    private readonly ILoggerFactory loggerFactory;
    private readonly IServiceCollection serviceCollection;

    protected KSqlDBContextDependenciesProvider(KSqlDBContextOptions kSqlDbContextOptions, ILoggerFactory loggerFactory = null)
      : this()
    {
      this.kSqlDbContextOptions = kSqlDbContextOptions ?? throw new ArgumentNullException(nameof(kSqlDbContextOptions));
      this.loggerFactory = loggerFactory;
    }

    protected KSqlDBContextDependenciesProvider()
    {
      serviceCollection = new ServiceCollection();
    }

    protected ServiceProvider ServiceProvider { get; set; }
    
    private bool wasConfigured;

    internal IServiceScopeFactory Initialize(KSqlDBContextOptions contextOptions)
    {
      if (!wasConfigured)
      {
        wasConfigured = true;

        RegisterDependencies(contextOptions);

        ServiceProvider = serviceCollection.BuildServiceProvider(new ServiceProviderOptions {ValidateScopes = true});
      }

      var serviceScopeFactory = ServiceProvider.GetService<IServiceScopeFactory>();

      return serviceScopeFactory;
    }

    private void RegisterDependencies(KSqlDBContextOptions contextOptions)
    {
      OnConfigureServices(serviceCollection, contextOptions);
    }

    internal void Configure(Action<IServiceCollection> receive)
    {
      receive?.Invoke(serviceCollection);
    }

    protected ILogger Logger { get; private set; }

    protected virtual void OnConfigureServices(IServiceCollection serviceCollection, KSqlDBContextOptions contextOptions)
    {
      if (loggerFactory != null)
      {
        serviceCollection.TryAddSingleton(_ => loggerFactory);

        Logger = loggerFactory.CreateLogger(LoggingCategory.Name);

        serviceCollection.TryAddSingleton(Logger);
      }

      serviceCollection.AddSingleton<KSqlDbProviderOptions>(contextOptions);
      serviceCollection.AddSingleton(contextOptions);

      serviceCollection.TryAddScoped<IKSqlQbservableProvider, QbservableProvider>();
      serviceCollection.TryAddScoped<ICreateStatementProvider, CreateStatementProvider>();

      var uri = new Uri(contextOptions.Url);

      serviceCollection.TryAddTransient<IKSqlQueryGenerator, KSqlQueryGenerator>();

      if (!serviceCollection.HasRegistration<IHttpClientFactory>())
      {
        if(kSqlDbContextOptions.UseBasicAuth)
        {
          serviceCollection.AddSingleton<IHttpClientFactory, HttpClientFactoryWithBasicAuth>(sp =>
          {
            var credentials = new BasicAuthCredentials(kSqlDbContextOptions.BasicAuthUserName,
              kSqlDbContextOptions.BasicAuthPassword);

            return new HttpClientFactoryWithBasicAuth(uri, credentials);
          });
        }
        else
        {
          serviceCollection.AddSingleton<IHttpClientFactory, HttpClientFactory>(_ =>
            new HttpClientFactory(uri));
        }
      }

      serviceCollection.TryAddSingleton(contextOptions);
      serviceCollection.TryAddScoped<IKStreamSetDependencies, KStreamSetDependencies>();
      serviceCollection.TryAddScoped<IKSqlDbRestApiClient, KSqlDbRestApiClient>();
    }

    private void RegisterHttpClientFactory<TFactory>()
      where TFactory: class, IHttpClientFactory
    {
      serviceCollection.AddSingleton<IHttpClientFactory, TFactory>();
    }

    private void RegisterKSqlDbProvider<TProvider>()
      where TProvider: class, IKSqlDbProvider
    {
      serviceCollection.AddScoped<IKSqlDbProvider, TProvider>();
    }
    
    protected override async ValueTask OnDisposeAsync()
    {
      if(ServiceProvider != null)
        await ServiceProvider.DisposeAsync();
    }
  }
}