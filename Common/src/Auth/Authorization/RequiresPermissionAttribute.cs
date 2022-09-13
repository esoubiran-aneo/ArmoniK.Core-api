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

using Microsoft.AspNetCore.Authorization;

namespace ArmoniK.Core.Common.Auth.Authorization;

/// <summary>
///   Function attribute defining the authorization policy name for the function
/// </summary>
public class RequiresPermissionAttribute : AuthorizeAttribute
{
  public const string PolicyPrefix = "RequiresPermission:";

  private Permissions.Permission? permission_;

  public RequiresPermissionAttribute(string category,
                                     string function)
    => Permission = new Permissions.Permission(category,
                                               function);

  public Permissions.Permission? Permission
  {
    get => permission_;
    set
    {
      Policy      = $"{PolicyPrefix}{value}";
      permission_ = new Permissions.Permission(Policy[PolicyPrefix.Length..]);
    }
  }
}