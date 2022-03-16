﻿// This file is part of the ArmoniK project
// 
// Copyright (C) ANEO, 2021-2022. All rights reserved.
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ArmoniK.Core.Adapters.MongoDB.Common;
using ArmoniK.Core.Adapters.MongoDB.Table.DataModel;
using ArmoniK.Core.Common;
using ArmoniK.Core.Common.Exceptions;
using ArmoniK.Core.Common.Storage;

using Microsoft.Extensions.Logging;

using MongoDB.Driver;
using MongoDB.Driver.Linq;

using Result = ArmoniK.Core.Adapters.MongoDB.Table.DataModel.Result;

namespace ArmoniK.Core.Adapters.MongoDB;

public class ResultTable : IResultTable
{
  private readonly ILogger                                                 logger_;
  private readonly SessionProvider                                         sessionProvider_;
  private readonly MongoCollectionProvider<Result, ResultDataModelMapping> resultCollectionProvider_;
  private readonly ActivitySource                                          activitySource_;

  public ResultTable(SessionProvider sessionProvider, MongoCollectionProvider<Result, ResultDataModelMapping> resultCollectionProvider,
                     ActivitySource  activitySource,  ILogger<ResultTable> logger)
  {
    sessionProvider_          = sessionProvider;
    resultCollectionProvider_ = resultCollectionProvider;
    activitySource_           = activitySource;
    logger_                   = logger;
  }

  /// <inheritdoc />
  public async Task Create(IEnumerable<Core.Common.Storage.Result> results, CancellationToken cancellationToken = default)
  {
    using var _        = logger_.LogFunction();
    using var activity = activitySource_.StartActivity($"{nameof(Create)}");

    var resultCollection = await resultCollectionProvider_.GetAsync();

    try
    {
      var writeResult = await resultCollection.BulkWriteAsync(results.Select(result => new InsertOneModel<Result>(result.ToResultDataModel())),
                                                          new()
                                                          {
                                                            IsOrdered = false,
                                                          },
                                                          cancellationToken);
    }
    catch
    {
      throw new ArmoniKException("Key already exists");
    }
  }

  /// <inheritdoc />
  public async Task<Core.Common.Storage.Result> GetResult(string sessionId, string key, CancellationToken cancellationToken = default)
  {
    using var _                = logger_.LogFunction(key);
    using var activity         = activitySource_.StartActivity($"{nameof(GetResult)}");
    var       sessionHandle    = await sessionProvider_.GetAsync();
    var       resultCollection = await resultCollectionProvider_.GetAsync();

    try
    {
      return await resultCollection.AsQueryable(sessionHandle)
                                   .Where(model => model.Id ==
                                                   Result.GenerateId(sessionId,
                                                                     key))
                                   .FirstAsync(cancellationToken);
    }
    catch(InvalidOperationException)
    {
      throw new ArmoniKException($"Key '{key}' not found");
    }
  }

  /// <inheritdoc />
  public async Task<bool> AreResultsAvailableAsync(string sessionId, IEnumerable<string> keys, CancellationToken cancellationToken = default)
  {
    using var _                = logger_.LogFunction(sessionId);
    using var activity         = activitySource_.StartActivity($"{nameof(AreResultsAvailableAsync)}");
    var       sessionHandle    = await sessionProvider_.GetAsync();
    var       resultCollection = await resultCollectionProvider_.GetAsync();

    return !await resultCollection.AsQueryable(sessionHandle)
                                  .AnyAsync(model => !model.IsResultAvailable && model.SessionId == sessionId && keys.Contains(model.Key),
                                            cancellationToken);
  }

  /// <inheritdoc />
  public async Task SetResult(string sessionId, string ownerTaskId, string key, byte[] smallPayload, CancellationToken cancellationToken = default)
  {
    using var _        = logger_.LogFunction(key);
    using var activity = activitySource_.StartActivity($"{nameof(SetResult)}");

    var resultCollection = await resultCollectionProvider_.GetAsync();

    var res = await resultCollection.UpdateOneAsync(Builders<Result>.Filter
                                                                             .Where(model => model.Key == key &&
                                                                                             model.OwnerTaskId == ownerTaskId &&
                                                                                             model.SessionId == sessionId),
                                                    Builders<Result>.Update
                                                                             .Set(model => model.IsResultAvailable,
                                                                                  true)
                                                                             .Set(model => model.Data,
                                                                                  smallPayload),
                                                    cancellationToken: cancellationToken);
    if (res.ModifiedCount == 0)
      throw new ArmoniKException($"Key '{key}' not found");
  }

  /// <inheritdoc />
  public async Task SetResult(string sessionId, string ownerTaskId, string key, CancellationToken cancellationToken = default)
  {
    var id= key;
    if (logger_.IsEnabled(LogLevel.Trace))
    {
      id = JsonSerializer.Serialize(new Dictionary<string, string>
      {
        { "sessionId", sessionId },
        { "ownerTaskId", ownerTaskId },
        { "key", key },
      });
    }

    using var _        = logger_.LogFunction(id);
    using var activity = activitySource_.StartActivity($"{nameof(SetResult)}");

    var resultCollection = await resultCollectionProvider_.GetAsync();

    var res = await resultCollection.UpdateOneAsync(Builders<Result>.Filter
                                                                             .Where(model => model.Key == key && model.OwnerTaskId == ownerTaskId),
                                                    Builders<Result>.Update
                                                                             .Set(model => model.IsResultAvailable,
                                                                                  true),
                                                    cancellationToken: cancellationToken);
    if (res.ModifiedCount == 0)
      throw new ArmoniKException($"Key '{key}' not found");
  }

  /// <inheritdoc />
  public async Task ChangeResultDispatch(string sessionId, string oldDispatchId, string newDispatchId, CancellationToken cancellationToken)
  {
    using var _        = logger_.LogFunction(sessionId);
    using var activity = activitySource_.StartActivity($"{nameof(ChangeResultDispatch)}");

    var resultCollection = await resultCollectionProvider_.GetAsync().ConfigureAwait(false);

    var sessionIsValid = await resultCollection.FindAsync(model => model.SessionId == sessionId,
                                                          null,
                                                          cancellationToken);
    if (await sessionIsValid.FirstOrDefaultAsync(cancellationToken) == default)
    {
      await Task.FromException<ArmoniKException>(new ArmoniKException($"Key '{sessionId}' not found"));
      return;
    }

    await resultCollection.UpdateManyAsync(model => model.OriginDispatchId == oldDispatchId,
                                           Builders<Result>.Update.Set(model => model.OriginDispatchId,
                                                                       newDispatchId),
                                           cancellationToken: cancellationToken)
                          .ConfigureAwait(false);


  }


  /// <inheritdoc />
  public async Task ChangeResultOwnership(string sessionId, IEnumerable<string> keys, string oldTaskId, string newTaskId, CancellationToken cancellationToken)
  {
    var id = oldTaskId;
    if (logger_.IsEnabled(LogLevel.Trace))
    {
      id = JsonSerializer.Serialize(new Dictionary<string, string>
      {
        { "sessionId", sessionId },
        { "oldTaskId", oldTaskId },
        { "newTaskId", newTaskId },
        { "keys", JsonSerializer.Serialize(keys) },
      });
    }

    using var _ = logger_.LogFunction(id);
    using var activity = activitySource_.StartActivity($"{nameof(ChangeResultOwnership)}");
    if (keys.Any())
    {
      var resultCollection = await resultCollectionProvider_.GetAsync();

      var result = await resultCollection.UpdateManyAsync(model => model.OwnerTaskId == oldTaskId && keys.Contains(model.Key) && model.SessionId == sessionId,
                                             Builders<Result>.Update
                                                             .Set(model => model.OwnerTaskId,
                                                                  newTaskId),
                                             cancellationToken: cancellationToken);
      if (result.ModifiedCount != keys.Count())
        throw new Exception("The number of modified values should correspond to the number of keys provided");
    }
  }


  /// <inheritdoc />
  public async Task DeleteResult(string session, string key, CancellationToken cancellationToken = default)
  {
    using var _                = logger_.LogFunction(key);
    using var activity         = activitySource_.StartActivity($"{nameof(DeleteResult)}");
    var       resultCollection = await resultCollectionProvider_.GetAsync();

    await resultCollection.DeleteOneAsync(model => model.Key == key && model.SessionId == session,
                                          cancellationToken);
  }

  /// <inheritdoc />
  public async IAsyncEnumerable<string> ListResultsAsync(string sessionId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    using var _                = logger_.LogFunction(sessionId);
    using var activity         = activitySource_.StartActivity($"{nameof(ListResultsAsync)}");
    var       sessionHandle    = await sessionProvider_.GetAsync();
    var       resultCollection = await resultCollectionProvider_.GetAsync();

    await foreach (var result in resultCollection.AsQueryable(sessionHandle)
                                                 .Where(model => model.SessionId == sessionId)
                                                 .Select(model => model.Id)
                                                 .ToAsyncEnumerable()
                                                 .WithCancellation(cancellationToken))
      yield return result;
  }

  /// <inheritdoc />
  public async Task DeleteResults(string sessionId, CancellationToken cancellationToken = default)
  {
    using var _                = logger_.LogFunction();
    using var activity         = activitySource_.StartActivity($"{nameof(DeleteResults)}");
    var       resultCollection = await resultCollectionProvider_.GetAsync();

    await resultCollection.DeleteManyAsync(model => model.SessionId == sessionId,
                                           cancellationToken);
  }


}