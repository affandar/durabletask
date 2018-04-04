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

namespace DurableTask.AzureStorage
{
    using System;
    using System.Runtime.Serialization;
    using DurableTask.Core;
    using Microsoft.WindowsAzure.Storage.Queue;

    /// <summary>
    /// Protocol class for all Azure Queue messages.
    /// </summary>
    [DataContract]
    class MessageData
    {
        public MessageData(TaskMessage message, Guid activityId, string queueName)
        {
            this.TaskMessage = message;
            this.ActivityId = activityId;
            this.QueueName = queueName;
        }

        public MessageData()
        { }

        [DataMember]                                                                                                                                                                                                                                                                                                                                                                  
        public Guid ActivityId { get; private set; }

        [DataMember]
        public TaskMessage TaskMessage { get; private set; }

        [DataMember]
        public string CompressedBlobName { get; set; }

        internal string QueueName { get; set; }

        internal CloudQueueMessage OriginalQueueMessage { get; set; }

        internal long TotalMessageSizeBytes { get; set; }

        internal MessageFormatFlags MessageFormat { get; set; }
    }

    /// <summary>
    /// The message type.
    /// </summary>
    [Flags]
    public enum MessageFormatFlags
    {
        /// <summary>
        /// Inline JSON message type.
        /// </summary>
        InlineJson = 0b0000,

        /// <summary>
        /// Blob message type.
        /// </summary>
        StorageBlob = 0b0001
    }
}
