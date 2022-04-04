﻿// This file is part of the ArmoniK project
// 
// Copyright (C) ANEO, 2021-2022. All rights reserved.
//   W. Kirschenmann   <wkirschenmann@aneo.fr>
//   J. Gurhem         <jgurhem@aneo.fr>
//   D. Dubuc          <ddubuc@aneo.fr>
//   L. Ziane Khodja   <lzianekhodja@aneo.fr>
//   F. Lemaitre       <flemaitre@aneo.fr>
//   S. Djebbar        <sdjebbar@aneo.fr>
//   J. Fonseca        <jfonseca@aneo.fr>
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

using ArmoniK.Api.gRPC.V1;
using ArmoniK.Core.Adapters.MongoDB.Table;
using ArmoniK.Core.Adapters.MongoDB.Table.DataModel;
using ArmoniK.Core.Common.Storage;

using Grpc.Core;

using NUnit.Framework;

namespace ArmoniK.Core.Adapters.MongoDB.Tests;

[TestFixture(TestOf = typeof(ExpressionsBuilders))]
internal class ExpressionsBuildersFieldFilterExpressionTests
{
  [Test]
  public void ShouldRecognizeSession()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.SessionId,
                                                         new[] { "Session" }).Compile();

    var model = new TaskData(
                                SessionId : "Session",
                                default,
                                default,
                                default,
                                default,
                                default,
                                default,
                                default,
                                default,
                                default,
                                default,
                                default,
                                default
                              );

    Assert.IsTrue(func(model));
  }

  [Test]
  public void ShouldRejectOtherSession()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.SessionId,
                                                         new[] { "Session" })
                                  .Compile();

    var model = new TaskData(SessionId: "OtherSession",
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsFalse(func(model));
  }

  [Test]
  public void ShouldExcludeSession()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.SessionId,
                                                         new[] { "Session" },
                                                         false)
                                  .Compile();

    var model = new TaskData(SessionId: "Session",
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsFalse(func(model));
  }

  [Test]
  public void ShouldIncludeOtherSession()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.SessionId,
                                                         new[] { "Session" },
                                                         false)
                                  .Compile();
    
    var model = new TaskData(SessionId: "OtherSession",
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsTrue(func(model));
  }

  [Test]
  public void ShouldRecognizeSubSession()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.ParentTaskId,
                                                         new[] { "SubSession" })
                                  .Compile();

    var model = new TaskData(default,
                             ParentTaskId: "SubSession",
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsTrue(func(model));
  }

  [Test]
  public void ShouldExcludeSubSession()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.ParentTaskId,
                                                         new[] { "SubSession" },
                                                         false)
                                  .Compile();

    var model = new TaskData(default,
                             ParentTaskId: "SubSession",
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsFalse(func(model));
  }

  [Test]
  public void ShouldRecognizeMultipleSubSession()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.ParentTaskId,
                                                         new[] { "SubSession", "SubSession2" })
                                  .Compile();

    var model = new TaskData(default,
                             ParentTaskId: "SubSession",
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsTrue(func(model));
  }

  [Test]
  public void ShouldExcludeMultipleSubSession()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.ParentTaskId,
                                                         new[] { "SubSession", "SubSession2" },
                                                         false)
                                  .Compile();

    var model = new TaskData(default,
                             ParentTaskId: "SubSession",
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsFalse(func(model));
  }

  [Test]
  public void ShouldRejectOtherSubSession()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.ParentTaskId,
                                                         new[] { "SubSession" })
                                  .Compile();
    
    var model = new TaskData(default,
                             ParentTaskId: "OtherSubSession",
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsFalse(func(model));
  }

  [Test]
  public void ShouldIncludeOtherSubSession()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.ParentTaskId,
                                                         new[] { "SubSession" },
                                                         false)
                                  .Compile(true);

    var model = new TaskData(default,
                             ParentTaskId: "OtherSubSession",
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsTrue(func(model));
  }

  [Test]
  public void ShouldRejectOtherMultipleSubSession()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.ParentTaskId,
                                                         new[] { "SubSession", "SubSession2" })
                                  .Compile();

    var model = new TaskData(default,
                             ParentTaskId: "OtherSubSession",
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsFalse(func(model));
  }

  [Test]
  public void ShouldIncludeOtherMultipleSubSession()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.ParentTaskId,
                                                         new[] { "SubSession", "SubSession2" },
                                                         false)
                                  .Compile();

    var model = new TaskData(default,
                             ParentTaskId: "OtherSubSession",
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsTrue(func(model));
  }

  [Test]
  public void ShouldRecognizeStatus()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.Status,
                                                         new[] { TaskStatus.Completed })
                                  .Compile();
    

    var model = new TaskData(default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             Status : TaskStatus.Completed,
                             default,
                             default,
                             default,
                             default);

    Assert.IsTrue(func(model));
  }

  [Test]
  public void ShouldExcludeStatus()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.Status,
                                                         new[] { TaskStatus.Completed },
                                                         false)
                                  .Compile();

    var model = new TaskData(default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             Status: TaskStatus.Completed,
                             default,
                             default,
                             default,
                             default);

    Assert.IsFalse(func(model));
  }

  [Test]
  public void ShouldRecognizeMultipleStatus()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.Status,
                                                         new[] { TaskStatus.Completed, TaskStatus.Canceled })
                                  .Compile();

    var model = new TaskData(default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             Status: TaskStatus.Completed,
                             default,
                             default,
                             default,
                             default);

    Assert.IsTrue(func(model));
  }

  [Test]
  public void ShouldExcludeMultipleStatus()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.Status,
                                                         new[] { TaskStatus.Completed, TaskStatus.Canceled },
                                                         false)
                                  .Compile();

    var model = new TaskData(default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             Status: TaskStatus.Completed,
                             default,
                             default,
                             default,
                             default);

    Assert.IsFalse(func(model));
  }

  [Test]
  public void ShouldRejectOtherStatus()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.Status,
                                                         new[] { TaskStatus.Completed })
                                  .Compile();

    var model = new TaskData(default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             Status: TaskStatus.Canceled,
                             default,
                             default,
                             default,
                             default);

    Assert.IsFalse(func(model));
  }

  [Test]
  public void ShouldIncludeOtherStatus()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.Status,
                                                         new[] { TaskStatus.Completed },
                                                         false)
                                  .Compile(true);

    var model = new TaskData(default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             Status: TaskStatus.Canceled,
                             default,
                             default,
                             default,
                             default);

    Assert.IsTrue(func(model));
  }

  [Test]
  public void ShouldRejectOtherMultipleStatus()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.Status,
                                                         new[] { TaskStatus.Completed, TaskStatus.Canceling })
                                  .Compile();

    var model = new TaskData(default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             Status: TaskStatus.Canceled,
                             default,
                             default,
                             default,
                             default);

    Assert.IsFalse(func(model));
  }

  [Test]
  public void ShouldIncludeOtherMultipleStatus()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.Status,
                                                         new[] { TaskStatus.Completed, TaskStatus.Canceling },
                                                         false)
                                  .Compile();

    var model = new TaskData(default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             default,
                             Status: TaskStatus.Canceled,
                             default,
                             default,
                             default,
                             default);

    Assert.IsTrue(func(model));
  }

  [Test]
  public void ShouldRecognizeTask()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.TaskId,
                                                         new[] { "Task" })
                                  .Compile();
    
    var model = new TaskData(default,
                             default,
                             default,
                             TaskId : "Task",
                             default,
                             default,
                             default,
                             default,
                             Status: default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsTrue(func(model));
  }

  [Test]
  public void ShouldExcludeTask()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.TaskId,
                                                         new[] { "Task" },
                                                         false)
                                  .Compile();

    var model = new TaskData(default,
                             default,
                             default,
                             TaskId: "Task",
                             default,
                             default,
                             default,
                             default,
                             Status: default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsFalse(func(model));
  }

  [Test]
  public void ShouldRecognizeMultipleTask()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.TaskId,
                                                         new[] { "Task", "Task2" })
                                  .Compile();

    var model = new TaskData(default,
                             default,
                             default,
                             TaskId: "Task",
                             default,
                             default,
                             default,
                             default,
                             Status: default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsTrue(func(model));
  }

  [Test]
  public void ShouldExcludeMultipleTask()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.TaskId,
                                                         new[] { "Task", "Task2" },
                                                         false)
                                  .Compile();

    var model = new TaskData(default,
                             default,
                             default,
                             TaskId: "Task",
                             default,
                             default,
                             default,
                             default,
                             Status: default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsFalse(func(model));
  }

  [Test]
  public void ShouldRejectOtherTask()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.TaskId,
                                                         new[] { "Task" })
                                  .Compile();
    
    var model = new TaskData(default,
                             default,
                             default,
                             TaskId: "OtherTask",
                             default,
                             default,
                             default,
                             default,
                             Status: default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsFalse(func(model));
  }

  [Test]
  public void ShouldIncludeOtherTask()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.TaskId,
                                                         new[] { "Task" },
                                                         false)
                                  .Compile(true);

    var model = new TaskData(default,
                             default,
                             default,
                             TaskId: "OtherTask",
                             default,
                             default,
                             default,
                             default,
                             Status: default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsTrue(func(model));
  }

  [Test]
  public void ShouldRejectOtherMultipleTask()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.TaskId,
                                                         new[] { "Task", "Task2" })
                                  .Compile();

    var model = new TaskData(default,
                             default,
                             default,
                             TaskId: "OtherTask",
                             default,
                             default,
                             default,
                             default,
                             Status: default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsFalse(func(model));
  }

  [Test]
  public void ShouldIncludeOtherMultipleTask()
  {
    var func = ExpressionsBuilders.FieldFilterExpression(model => model.TaskId,
                                                         new[] { "Task", "Task2" },
                                                         false)
                                  .Compile();

    var model = new TaskData(default,
                             default,
                             default,
                             TaskId: "OtherTask",
                             default,
                             default,
                             default,
                             default,
                             Status: default,
                             default,
                             default,
                             default,
                             default);

    Assert.IsTrue(func(model));
  }
}