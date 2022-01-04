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
using System.Threading;
using System.Threading.Tasks;

using Amqp;

using ArmoniK.Core;
using ArmoniK.Core.gRPC.V1;
using ArmoniK.Core.Storage;

using Microsoft.Extensions.Logging;

namespace ArmoniK.Adapters.Amqp
{
  public class QueueMessage : IQueueMessage
  {
    private readonly ILogger       logger_;
    private readonly Message       message_;
    private readonly IReceiverLink receiver_;
    private readonly ISenderLink   sender_;

    public QueueMessage(Message message, ISenderLink sender, IReceiverLink receiver, TaskId taskId, ILogger logger, CancellationToken cancellationToken)
    {
      message_          = message;
      sender_           = sender;
      receiver_         = receiver;
      logger_           = logger;
      TaskId            = taskId;
      CancellationToken = cancellationToken;
    }

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; }

    /// <inheritdoc />
    public string MessageId => message_.Properties.MessageId;

    /// <inheritdoc />
    public TaskId TaskId { get; }

    /// <inheritdoc />
    public QueueMessageStatus Status { get; set; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
      logger_.LogFunction(MessageId,
                          functionName: $"{nameof(QueueStorage)}.{nameof(DisposeAsync)}");
      switch (Status)
      {
        case QueueMessageStatus.Postponed:
          await sender_.SendAsync(message_);
          receiver_.Accept(message_);
          break;
        case QueueMessageStatus.Failed:
          receiver_.Release(message_);
          break;
        case QueueMessageStatus.Processed:
          receiver_.Accept(message_);
          break;
        case QueueMessageStatus.Poisonous:
          receiver_.Reject(message_);
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof(Status),
                                                Status,
                                                null);
      }

      GC.SuppressFinalize(this);
    }
  }
}
