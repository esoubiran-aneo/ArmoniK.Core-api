// This file is part of the ArmoniK project
// 
// Copyright (C) ANEO, 2021-2023. All rights reserved.
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY, without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ArmoniK.Api.gRPC.V1.Worker;
using ArmoniK.Core.Common.Utils;

namespace ArmoniK.Core.Common.Tests.Helpers;

public class ExceptionAsyncPipe<T> : IAsyncPipe<ProcessReply, ProcessRequest>
  where T : Exception, new()
{
  private readonly int delay_;

  public ExceptionAsyncPipe(int delay)
    => delay_ = delay;

  public async Task<ProcessReply> ReadAsync(CancellationToken cancellationToken)
  {
    await Task.Delay(TimeSpan.FromMilliseconds(delay_),
                     cancellationToken)
              .ConfigureAwait(false);
    cancellationToken.ThrowIfCancellationRequested();
    throw new T();
  }

  public Task WriteAsync(ProcessRequest message)
    => Task.CompletedTask;

  public Task WriteAsync(IEnumerable<ProcessRequest> message)
    => Task.CompletedTask;

  public Task CompleteAsync()
    => Task.CompletedTask;
}
