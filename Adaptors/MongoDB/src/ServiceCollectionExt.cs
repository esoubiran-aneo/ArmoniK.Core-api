﻿// This file is part of the ArmoniK project
// 
// Copyright (C) ANEO, 2021-2021. All rights reserved.
//   W. Kirschenmann   <wkirschenmann@aneo.fr>
//   J. Gurhem         <jgurhem@aneo.fr>
//   D. Dubuc          <ddubuc@aneo.fr>
//   L. Ziane Khodja   <lzianekhodja@aneo.fr>
//   F. Lemaitre       <flemaitre@aneo.fr>
//   S. Djebbar        <sdjebbar@aneo.fr>
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

using ArmoniK.Core.Adapters.MongoDB.Common;
using ArmoniK.Core.Adapters.MongoDB.Options;
using ArmoniK.Core.Common;
using ArmoniK.Core.Common.Injection;
using ArmoniK.Core.Common.Injection.Options;
using ArmoniK.Core.Common.Storage;

using JetBrains.Annotations;

using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;
using MongoDB.Driver.Core.Events;

namespace ArmoniK.Core.Adapters.MongoDB;

public static class ServiceCollectionExt
{
  [PublicAPI]
  public static IServiceCollection AddMongoComponents(
    this IServiceCollection services,
    ConfigurationManager    configuration,
    ILogger                 logger
  )
  {
    logger.LogInformation("Configure MongoDB client");

    var components = configuration.GetSection(Components.SettingSection);

    var isMongoRequired = false;

    if (components["TableStorage"] == "ArmoniK.Adapters.MongoDB.TableStorage")
    {
      services.AddOption<Options.TableStorage>(configuration,
                                               Options.TableStorage.SettingSection)
              .AddTransient<TableStorage>()
              .AddTransientWithHealthCheck<ITableStorage, TableStorage>($"MongoDB.{nameof(TableStorage)}");

      isMongoRequired = true;
    }

    if (components["QueueStorage"] == "ArmoniK.Adapters.MongoDB.LockedQueueStorage")
    {
      services.AddOption<QueueStorage>(configuration,
                                       QueueStorage.SettingSection)
              .AddTransientWithHealthCheck<LockedQueueStorage>($"MongoDB.{nameof(LockedQueueStorage)}")
              .AddTransientWithHealthCheck<IQueueStorage, LockedWrapperQueueStorage>($"MongoDB.{nameof(LockedWrapperQueueStorage)}")
              .AddTransient<ILockedQueueStorage, LockedQueueStorage>();

      isMongoRequired = true;
    }

    if (components["ObjectStorage"] == "ArmoniK.Adapters.MongoDB.ObjectStorage")
    {
      services.AddOption<Options.ObjectStorage>(configuration,
                                                Options.ObjectStorage.SettingSection)
              .AddTransient<ObjectStorage>()
              .AddTransientWithHealthCheck<IObjectStorage, ObjectStorage>($"MongoDB.{nameof(ObjectStorage)}");

      isMongoRequired = true;
    }

    if (components["LeaseProvider"] == "ArmoniK.Adapters.MongoDB.LeaseProvider")
    {
      services.AddOption<Options.LeaseProvider>(configuration,
                                                Options.LeaseProvider.SettingSection)
              .AddTransient<LeaseProvider>()
              .AddTransientWithHealthCheck<ILeaseProvider, LeaseProvider>($"MongoDB.{nameof(LeaseProvider)}");

      isMongoRequired = true;
    }

    if (isMongoRequired)
    {
      services.AddOption<Options.MongoDB>(configuration,
                                          Options.MongoDB.SettingSection,
                                          out var mongoOptions);

      using var _ = logger.BeginNamedScope("MongoDB configuration",
                                           ("host", mongoOptions.Host),
                                           ("port", mongoOptions.Port));

      if (string.IsNullOrEmpty(mongoOptions.Host))
        throw new ArgumentOutOfRangeException(Options.MongoDB.SettingSection,
                                              $"{nameof(Options.MongoDB.Host)} is not defined.");

      if (string.IsNullOrEmpty(mongoOptions.DatabaseName))
        throw new ArgumentOutOfRangeException(Options.MongoDB.SettingSection,
                                              $"{nameof(Options.MongoDB.DatabaseName)} is not defined.");

      if (!string.IsNullOrEmpty(mongoOptions.CredentialsPath))
      {
        configuration.AddJsonFile(mongoOptions.CredentialsPath);

        services.AddOption(configuration,
                           Options.MongoDB.SettingSection,
                           out mongoOptions);

        logger.LogTrace("Loaded mongodb credentials from file {path}",
                        mongoOptions.CredentialsPath);

        if (!string.IsNullOrEmpty(mongoOptions.CAFile))
        {
          X509Store                  localTrustStore       = new X509Store(StoreName.Root);
          X509Certificate2Collection certificateCollection = new X509Certificate2Collection();
          try
          {
            certificateCollection.Import(mongoOptions.CAFile);
            localTrustStore.Open(OpenFlags.ReadWrite);
            localTrustStore.AddRange(certificateCollection);
            logger.LogTrace("Imported mongodb certificate from file {path}",
                            mongoOptions.CAFile);
          }
          catch (Exception ex)
          {
            logger.LogError("Root certificate import failed: {error}",
                            ex.Message);
            throw;
          }
          finally
          {
            localTrustStore.Close();
          }
        }
      }
      else
      {
        logger.LogTrace("No credentials provided");
      }


      //var mongoUrl = new MongoUrlBuilder
      //{
      //  Scheme         = ConnectionStringScheme.MongoDB,
      //  Password       = mongoOptions.Password,
      //  Username       = mongoOptions.User,
      //  UseTls         = mongoOptions.Tls,
      //  ReplicaSetName = mongoOptions.ReplicaSet,
      //  Server = new MongoServerAddress(mongoOptions.Host,
      //                                  mongoOptions.Port),
      //  ReadPreference = ReadPreference.Nearest,
      //  DatabaseName   = mongoOptions.DatabaseName,
      //};

      //var settings = MongoClientSettings.FromUrl(mongoUrl.ToMongoUrl());

      //settings.SslSettings = new SslSettings
      //{
      //  ClientCertificates         = certificateCollection,
      //  CheckCertificateRevocation = true,
      //  EnabledSslProtocols        = System.Security.Authentication.SslProtocols.None,
      //};

      //settings.SslSettings.ClientCertificateSelectionCallback =
      //  (sender, host, certificates, certificate, issuers) => settings.SslSettings.ClientCertificates.ToList()[0];
      //settings.SslSettings.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true;

      string template = "mongodb://{0}:{1}@{2}:{3}/{4}";
      string connectionString = String.Format(template,
                                              mongoOptions.User,
                                              mongoOptions.Password,
                                              mongoOptions.Host,
                                              mongoOptions.Port,
                                              mongoOptions.DatabaseName);

      var settings = MongoClientSettings.FromUrl(new MongoUrl(connectionString));
      settings.AllowInsecureTls = mongoOptions.AllowInsecureTls;
      settings.UseTls           = mongoOptions.Tls;
      settings.DirectConnection = mongoOptions.DirectConnection;
      settings.Scheme           = ConnectionStringScheme.MongoDB;

      services.AddTransient<IMongoClient>(provider =>
              {
                var logger = provider.GetRequiredService<ILogger<IMongoClient>>();


                //if (logger.IsEnabled(LogLevel.Trace))
                //{
                //  settings.ClusterConfigurator = cb =>
                //  {
                //    cb.Subscribe<CommandStartedEvent>(e =>
                //    {
                //      logger
                //        .LogTrace("{CommandName} - {Command}",
                //                  e.CommandName,
                //                  e.Command.ToJson());
                //    });
                //  };
                //}

                return new MongoClient(settings);
              })
              .AddTransient(provider => provider.GetRequiredService<IMongoClient>().GetDatabase(mongoOptions.DatabaseName))
              .AddSingleton(typeof(MongoCollectionProvider<>))
              .AddSingletonWithHealthCheck<SessionProvider>($"MongoDB.{nameof(SessionProvider)}")
              .AddHealthChecks()
              .AddMongoDb(settings,
                          mongoOptions.DatabaseName,
                          "MongoDb Connection",
                          tags: new[] { nameof(HealthCheckTag.Startup), nameof(HealthCheckTag.Liveness), nameof(HealthCheckTag.Readiness) });

      logger.LogInformation("MongoDB configuration complete");
    }

    return services;
  }
}