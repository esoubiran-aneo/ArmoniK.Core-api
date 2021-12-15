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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using ArmoniK.Core;
using ArmoniK.Core.Exceptions;
using ArmoniK.Core.gRPC;
using ArmoniK.Core.gRPC.V1;
using ArmoniK.Core.Storage;
using ArmoniK.Core.Utils;

using JetBrains.Annotations;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;
using MongoDB.Driver.Linq;

using TaskStatus = ArmoniK.Core.gRPC.V1.TaskStatus;

namespace ArmoniK.Adapters.MongoDB
{
  [PublicAPI]
  //TODO : wrap all exceptions into ArmoniKExceptions
  public class TableStorage : ITableStorage
  {
    private readonly ILogger<TableStorage>                     logger_;
    private readonly MongoCollectionProvider<SessionDataModel> sessionCollectionProvider_;
    private readonly SessionProvider                           sessionProvider_;
    private readonly MongoCollectionProvider<TaskDataModel>    taskCollectionProvider_;

    public TableStorage(
      MongoCollectionProvider<SessionDataModel> sessionCollectionProvider,
      MongoCollectionProvider<TaskDataModel>    taskCollectionProvider,
      SessionProvider                           sessionProvider,
      IOptions<Options.TableStorage>            options,
      ILogger<TableStorage>                     logger
    )
    {
      sessionCollectionProvider_ = sessionCollectionProvider;
      taskCollectionProvider_    = taskCollectionProvider;
      sessionProvider_           = sessionProvider;
      PollingDelay               = options.Value.PollingDelay;
      logger_                    = logger;
    }

    public TimeSpan PollingDelay { get; }


    public async Task CancelSessionAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
      using var _                 = logger_.LogFunction(sessionId.ToString());
      var       sessionHandle     = await sessionProvider_.GetAsync();
      var       sessionCollection = await sessionCollectionProvider_.GetAsync();

      var filterDefinition = Builders<SessionDataModel>.Filter
                                                       .Where(sdm => sessionId.Session == sdm.SessionId &&
                                                                     (sessionId.SubSession == sdm.SubSessionId ||
                                                                      (sdm.ParentsId.Any(
                                                                                         id => id.Id == sessionId.SubSession) &&
                                                                       !sdm.IsClosed)));

      var updateDefinition = Builders<SessionDataModel>.Update
                                                       .Set(model => model.IsCancelled,
                                                            true)
                                                       .Set(model => model.IsClosed,
                                                            true);

      var res = await sessionCollection.UpdateOneAsync(
        filterDefinition,
        updateDefinition,
        cancellationToken: cancellationToken);
      if (res.MatchedCount < 1)
        throw new InvalidOperationException("No open session found. Was the session closed?");
    }

    public async Task CloseSessionAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
      using var _                 = logger_.LogFunction(sessionId.ToString());
      var       sessionHandle     = await sessionProvider_.GetAsync();
      var       sessionCollection = await sessionCollectionProvider_.GetAsync();

      var filterDefinition = Builders<SessionDataModel>.Filter
                                                       .Where(sdm => sessionId.Session == sdm.SessionId &&
                                                                     (sessionId.SubSession == sdm.SubSessionId ||
                                                                      sdm.ParentsId.Any(
                                                                        id => id.Id == sessionId.SubSession)));

      var definitionBuilder = new UpdateDefinitionBuilder<SessionDataModel>();

      var updateDefinition = definitionBuilder.Set(model => model.IsClosed,
                                                   true);

      var res = await sessionCollection.UpdateOneAsync(
                                                       filterDefinition,
                                                       updateDefinition,
                                                       cancellationToken: cancellationToken);
      if (res.MatchedCount < 1)
        throw new InvalidOperationException("No open session found. Was the session already closed?");
    }

    public async Task<int> CountTasksAsync(TaskFilter filter, CancellationToken cancellationToken = default)
    {
      using var _              = logger_.LogFunction();
      var       sessionHandle  = await sessionProvider_.GetAsync();
      var       taskCollection = await taskCollectionProvider_.GetAsync();

      return await taskCollection.FilterQueryAsync(sessionHandle,
                                                   filter)
                                 .CountAsync(cancellationToken);
    }

    public async Task<SessionId> CreateSessionAsync(SessionOptions    sessionOptions,
                                                    CancellationToken cancellationToken = default)
    {
      using var _                 = logger_.LogFunction();
      var       sessionHandle     = await sessionProvider_.GetAsync();
      var       sessionCollection = await sessionCollectionProvider_.GetAsync();

      var                             subSession = false;
      List<SessionDataModel.ParentId> parents    = new();
      if (sessionOptions.ParentSession != null)
        if (!string.IsNullOrEmpty(sessionOptions.ParentSession.Session))
        {
          subSession = true;

          if (!string.IsNullOrEmpty(sessionOptions.ParentSession.SubSession))
          {
            var t = await sessionCollection.AsQueryable(sessionHandle)
                                           .Where(x => x.SessionId == sessionOptions.ParentSession.Session &&
                                                       x.SubSessionId == sessionOptions.ParentSession.SubSession)
                                           .FirstAsync(cancellationToken);
            parents.AddRange(t.ParentsId);
            parents.Add(new SessionDataModel.ParentId
                          { Id = sessionOptions.ParentSession.SubSession });
          }
        }

      var data = new SessionDataModel
                 {
                   IdTag       = sessionOptions.IdTag,
                   IsCancelled = false,
                   IsClosed    = false,
                   Options     = sessionOptions.DefaultTaskOption,
                   ParentsId   = parents,
                 };
      if (subSession)
        data.SessionId = sessionOptions.ParentSession.Session;

      await sessionCollection.InsertOneAsync(data,
                                             cancellationToken: cancellationToken);

      if (!subSession)
      {
        data.SessionId = data.SubSessionId;
        var updateDefinition = Builders<SessionDataModel>.Update
                                                         .Set(model => model.SessionId,
                                                              data.SessionId);
        await sessionCollection.UpdateOneAsync(
          x => x.SubSessionId == data.SubSessionId,
          updateDefinition,
          cancellationToken: cancellationToken);
      }

      return new SessionId { Session = data.SessionId, SubSession = data.SubSessionId };
    }

    public async Task DeleteTaskAsync(TaskId id, CancellationToken cancellationToken = default)
    {
      using var _              = logger_.LogFunction(id.ToPrintableId());
      var       sessionHandle  = await sessionProvider_.GetAsync();
      var       taskCollection = await taskCollectionProvider_.GetAsync();

      await taskCollection.DeleteOneAsync(
                                          tdm => tdm.SessionId == id.Task &&
                                                 tdm.SubSessionId == id.SubSession &&
                                                 tdm.TaskId == id.Task,
                                          cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> UpdateTaskStatusAsync(TaskFilter        filter,
                                                 TaskStatus        status,
                                                 CancellationToken cancellationToken = default)
    {
      using var _              = logger_.LogFunction();
      var       sessionHandle  = await sessionProvider_.GetAsync();
      var       taskCollection = await taskCollectionProvider_.GetAsync();

      var updateDefinition = new UpdateDefinitionBuilder<TaskDataModel>().Set(tdm => tdm.Status,
                                                                              status);

      filter.ExcludedStatuses.Add(TaskStatus.Completed);
      filter.ExcludedStatuses.Add(TaskStatus.Canceled);

      var result = await taskCollection.UpdateManyAsync(
                                                        filter.ToFilterDefinition(),
                                                        updateDefinition,
                                                        cancellationToken: cancellationToken
                                                       );
      return (int)result.MatchedCount;
    }

    public async Task IncreaseRetryCounterAsync(TaskId id, CancellationToken cancellationToken = default)
    {
      using var _              = logger_.LogFunction(id.ToPrintableId());
      var       sessionHandle  = await sessionProvider_.GetAsync();
      var       taskCollection = await taskCollectionProvider_.GetAsync();

      var updateDefinition = Builders<TaskDataModel>.Update
                                                    .Inc(tdm => tdm.Retries,
                                                         1);

      var res = await taskCollection.UpdateManyAsync(
                                                     tdm => tdm.SessionId == id.Session &&
                                                            tdm.SubSessionId == id.SubSession &&
                                                            tdm.TaskId == id.Task,
                                                     updateDefinition,
                                                     cancellationToken: cancellationToken
                                                    );
      switch (res.MatchedCount)
      {
        case 0:
          throw new ArmoniKException("Task not found");
        case > 1:
          throw new ArmoniKException("Multiple tasks modified");
      }
    }

    public async Task<IEnumerable<(TaskId id, bool HasPayload, byte[] Payload)>> InitializeTaskCreation(
      SessionId            session,
      TaskOptions          options,
      IEnumerable<Payload> payloads,
      CancellationToken    cancellationToken = default)
    {
      using var _              = logger_.LogFunction(session.ToString());
      var       sessionHandle  = await sessionProvider_.GetAsync();
      var       taskCollection = await taskCollectionProvider_.GetAsync();

      var tdms = payloads.Select(payload =>
                         {
                           var isPayloadStored = payload.CalculateSize() < 12000000;

                                   var tdm = new TaskDataModel
                                             {
                                               HasPayload   = isPayloadStored,
                                               Options      = options,
                                               Retries      = 0,
                                               SessionId    = session.Session,
                                               SubSessionId = session.SubSession,
                                               Status       = TaskStatus.Creating,
                                             };
                                   if (isPayloadStored)
                                     tdm.Payload = payload.Data.ToByteArray();

                           return tdm;
                         })
                         .ToList();

      await taskCollection.InsertManyAsync(tdms,
                                           cancellationToken: cancellationToken);

      return tdms.Select(tdm => (tdm.GetTaskId(), tdm.HasPayload, tdm.Payload));
    }

    public async Task<(TaskId id, bool isPayloadStored, IAsyncDisposable finalizer)> InitializeTaskCreation(
      SessionId         session,
      TaskOptions       options,
      Payload           payload,
      CancellationToken cancellationToken = default
    )
    {
      using var _              = logger_.LogFunction(session.ToString());
      var       sessionHandle  = await sessionProvider_.GetAsync();
      var       taskCollection = await taskCollectionProvider_.GetAsync();

      var isPayloadStored = payload.CalculateSize() < 12000000;

      var tdm = new TaskDataModel
      {
        HasPayload   = isPayloadStored,
        Options      = options,
        Retries      = 0,
        SessionId    = session.Session,
        SubSessionId = session.SubSession,
        Status       = TaskStatus.Creating,
      };
      if (isPayloadStored)
        tdm.Payload = payload.Data.ToByteArray();

      await taskCollection.InsertOneAsync(
        tdm,
        cancellationToken: cancellationToken);
      return (tdm.GetTaskId(), isPayloadStored, AsyncDisposable.Create(async () => await this.FinalizeTaskCreation(tdm.GetTaskId(),
                                                                                                                   cancellationToken)));
    }

    public async Task<bool> IsSessionCancelledAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
      using var _                 = logger_.LogFunction(sessionId.ToString());
      var       sessionHandle     = await sessionProvider_.GetAsync();
      var       sessionCollection = await sessionCollectionProvider_.GetAsync();

      var session = await sessionCollection.AsQueryable(sessionHandle)
                                           .Where(x => x.SessionId == sessionId.Session &&
                                                       x.SubSessionId == sessionId.SubSession)
                                           .FirstAsync(cancellationToken);

      return session.IsCancelled;
    }

    public async Task<bool> IsSessionClosedAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
      using var _                 = logger_.LogFunction(sessionId.ToString());
      var       sessionHandle     = await sessionProvider_.GetAsync();
      var       sessionCollection = await sessionCollectionProvider_.GetAsync();
      if (!string.IsNullOrEmpty(sessionId.SubSession))
      {
        var session = await sessionCollection.AsQueryable(sessionHandle)
                                             .Where(x => x.SessionId == sessionId.Session &&
                                                         x.SubSessionId == sessionId.SubSession)
                                             .FirstAsync(cancellationToken);
        return session.IsClosed;
      }

      return 0 ==
             await sessionCollection.AsQueryable(sessionHandle)
                                    .Where(x => x.SessionId == sessionId.Session && !x.IsClosed)
                                    .CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteSessionAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
      using var _ = logger_.LogFunction(sessionId.ToString());
      throw new NotImplementedException();
    }

    public async IAsyncEnumerable<TaskId> ListTasksAsync(TaskFilter filter,
                                                         [EnumeratorCancellation] CancellationToken cancellationToken =
                                                           default)
    {
      using var _              = logger_.LogFunction();
      var       sessionHandle  = await sessionProvider_.GetAsync();
      var       taskCollection = await taskCollectionProvider_.GetAsync();

      var output = taskCollection.FilterQueryAsync(sessionHandle,
                                                   filter)
                                 .Select(x => new TaskId
                                              {
                                                Session    = x.SessionId,
                                                SubSession = x.SubSessionId,
                                                Task       = x.TaskId,
                                              })
                                 .ToAsyncEnumerable();

      await foreach (var taskId in output.WithCancellation(cancellationToken))
        yield return taskId;
    }

    public async Task<TaskData> ReadTaskAsync(TaskId id, CancellationToken cancellationToken = default)
    {
      using var _              = logger_.LogFunction(id.ToPrintableId());
      var       sessionHandle  = await sessionProvider_.GetAsync();
      var       taskCollection = await taskCollectionProvider_.GetAsync();

      var res = await taskCollection.AsQueryable(sessionHandle)
                                    .Where(tdm => tdm.SessionId == id.Session &&
                                                  tdm.SubSessionId == id.SubSession &&
                                                  tdm.TaskId == id.Task)
                                    .SingleAsync(cancellationToken);
      return res.ToTaskData();
    }

    public async Task UpdateTaskStatusAsync(TaskId            id,
                                            TaskStatus        status,
                                            CancellationToken cancellationToken = default)
    {
      using var _              = logger_.LogFunction(id.ToPrintableId());
      var       sessionHandle  = await sessionProvider_.GetAsync();
      var       taskCollection = await taskCollectionProvider_.GetAsync();

      var updateDefinition = new UpdateDefinitionBuilder<TaskDataModel>().Set(tdm => tdm.Status,
                                                                              status);

      var res = await taskCollection.UpdateManyAsync(
                                                     x => x.SessionId == id.Session &&
                                                          x.SubSessionId == id.SubSession &&
                                                          x.TaskId == id.Task &&
                                                          x.Status != TaskStatus.Completed &&
                                                          x.Status != TaskStatus.Canceled,
                                                     updateDefinition,
                                                     cancellationToken: cancellationToken
                                                    );

      switch (res.MatchedCount)
      {
        case 0:
          throw new ArmoniKException("Task not found");
        case > 1:
          throw new ArmoniKException("Multiple tasks modified");
      }
    }

    public async Task<TaskOptions> GetDefaultTaskOption(SessionId sessionId, CancellationToken cancellationToken)
    {
      using var _                 = logger_.LogFunction(sessionId.ToString());
      var       sessionHandle     = await sessionProvider_.GetAsync();
      var       sessionCollection = await sessionCollectionProvider_.GetAsync();

      return await sessionCollection.AsQueryable(sessionHandle)
                                    .Where(sdm => sdm.SessionId == sessionId.Session &&
                                                  sdm.SubSessionId == sessionId.SubSession)
                                    .Select(sdm => sdm.Options)
                                    .FirstAsync(cancellationToken);
    }
  }
}