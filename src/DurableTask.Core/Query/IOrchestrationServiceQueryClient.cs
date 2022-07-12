﻿//  ----------------------------------------------------------------------------------
//  Copyright Microsoft Corporation
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  ----------------------------------------------------------------------------------

namespace DurableTask.Core.Query
{
    using System.Threading;
    using System.Collections.Generic;

    /// <summary>
    /// Interface to allow query multi-instance status with filter.
    /// </summary>
    public interface IOrchestrationServiceQueryClient
    {
        /// <summary>
        /// Asynchronously enumerates over the status of all orchestration instances that match the specified conditions.
        /// </summary>
        /// <param name="query">Return orchestration instances that match the specified query.</param>
        /// <param name="cancellationToken">Cancellation token that can be used to cancel the query operation.</param>
        /// <returns>An asynchronous enumerable over the orchestration status for all instances.</returns>
        IAsyncEnumerable<OrchestrationState> GetOrchestrationWithQueryAsync(OrchestrationQuery query, CancellationToken cancellationToken = default);
    }
}
