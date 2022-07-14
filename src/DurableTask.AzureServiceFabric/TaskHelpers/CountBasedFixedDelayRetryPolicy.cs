﻿//  ----------------------------------------------------------------------------------
//  Copyright Microsoft Corporation
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ----------------------------------------------------------------------------------

namespace DurableTask.AzureServiceFabric.TaskHelpers
{
    using System;

    internal class CountBasedFixedDelayRetryPolicy : IRetryPolicy
    {
        private readonly TimeSpan delay;
        private int pendingAttempts;

        public CountBasedFixedDelayRetryPolicy(int maxNumberOfAttempts, TimeSpan delay)
        {
            this.delay = delay;
            this.pendingAttempts = maxNumberOfAttempts;
        }

        public bool ShouldExecute() => this.pendingAttempts-- > 0;

        public TimeSpan GetNextDelay() => this.pendingAttempts < 1 ? TimeSpan.Zero : this.delay;

        public static IRetryPolicy GetNewDefaultPolicy()
         => new CountBasedFixedDelayRetryPolicy(3, TimeSpan.FromMilliseconds(100));
    }
}
