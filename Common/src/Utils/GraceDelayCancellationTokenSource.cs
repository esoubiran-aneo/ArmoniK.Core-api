// This file is part of the ArmoniK project
// 
// Copyright (C) ANEO, 2021-2022. All rights reserved.
//   W. Kirschenmann   <wkirschenmann@aneo.fr>
//   J. Gurhem         <jgurhem@aneo.fr>
//   D. Dubuc          <ddubuc@aneo.fr>
//   L. Ziane Khodja   <lzianekhodja@aneo.fr>
//   F. Lemaitre       <flemaitre@aneo.fr>
//   S. Djebbar        <sdjebbar@aneo.fr>
//   J. Fonseca        <jfonseca@aneo.fr>
//   D. Brasseur       <dbrasseur@aneo.fr>
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
using System.Threading;

namespace ArmoniK.Core.Common.Utils;

public sealed class GraceDelayCancellationTokenSource : IDisposable
{
  private readonly TimeSpan                      t1_;
  private readonly TimeSpan                      t2_;
  private readonly TimeSpan                      t3_;
  private readonly TimeSpan                      t4_;
  private readonly TimeSpan                      t5_;
  private readonly CancellationTokenRegistration reg_;

  public GraceDelayCancellationTokenSource(CancellationTokenSource source,
                                           TimeSpan                t1,
                                           TimeSpan                t2 = default,
                                           TimeSpan                t3 = default,
                                           TimeSpan                t4 = default,
                                           TimeSpan                t5 = default)
  {
    t1_  = t1;
    t2_  = t2;
    t3_  = t3;
    t4_  = t4;
    t5_  = t5;
    reg_ = source.Token.Register(CancellationTrigger);
  }

  public CancellationTokenSource Token0 { get; } = new();
  public CancellationTokenSource Token1 { get; } = new();
  public CancellationTokenSource Token2 { get; } = new();
  public CancellationTokenSource Token3 { get; } = new();
  public CancellationTokenSource Token4 { get; } = new();
  public CancellationTokenSource Token5 { get; } = new();

  public void Dispose()
  {
    GC.SuppressFinalize(this);

    reg_.Unregister();
    Token0.Dispose();
    Token1.Dispose();
    Token2.Dispose();
    Token3.Dispose();
    Token4.Dispose();
    Token5.Dispose();
  }

  private void CancellationTrigger()
  {
    Token0.Cancel();
    Token1.CancelAfter(t1_);
    Token2.CancelAfter(t2_);
    Token3.CancelAfter(t3_);
    Token4.CancelAfter(t4_);
    Token5.CancelAfter(t5_);
  }
}
