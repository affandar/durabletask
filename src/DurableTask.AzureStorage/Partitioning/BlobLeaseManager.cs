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

namespace DurableTask.AzureStorage.Partitioning
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using DurableTask.AzureStorage.Monitoring;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.WindowsAzure.Storage.Blob.Protocol;
    using Newtonsoft.Json;

    sealed class BlobLeaseManager : ILeaseManager<BlobLease>
    {
        const string TaskHubInfoBlobName = "taskhub.json";
        static readonly TimeSpan StorageMaximumExecutionTime = TimeSpan.FromMinutes(2);

        readonly AzureStorageOrchestrationServiceSettings settings;
        readonly string storageAccountName;
        readonly string taskHubName;
        readonly string workerName;
        readonly string blobPrefix;
        readonly string leaseContainerName;
        readonly string leaseType;
        readonly bool skipBlobContainerCreation;
        readonly TimeSpan leaseInterval;
        readonly CloudBlobClient storageClient;
        readonly BlobRequestOptions renewRequestOptions;
        readonly AzureStorageOrchestrationServiceStats stats;

        CloudBlobContainer taskHubContainer;
        CloudBlobDirectory leaseDirectory;
        CloudBlockBlob taskHubInfoBlob;

        public BlobLeaseManager(
            AzureStorageOrchestrationServiceSettings settings,
            string leaseContainerName,
            string blobPrefix,
            string leaseType,
            CloudBlobClient storageClient,
            bool skipBlobContainerCreation,
            AzureStorageOrchestrationServiceStats stats)
        {
            this.settings = settings;
            this.storageAccountName = storageClient.Credentials.AccountName;
            this.taskHubName = settings.TaskHubName;
            this.workerName = settings.WorkerId;
            this.leaseContainerName = leaseContainerName;
            this.blobPrefix = blobPrefix;
            this.leaseType = leaseType;
            this.storageClient = storageClient;
            this.leaseInterval = settings.LeaseInterval;
            this.skipBlobContainerCreation = skipBlobContainerCreation;
            this.renewRequestOptions = new BlobRequestOptions { ServerTimeout = settings.LeaseRenewInterval };
            this.stats = stats ?? new AzureStorageOrchestrationServiceStats();

            this.Initialize();
        }

        public async Task<bool> LeaseStoreExistsAsync()
        {
            try
            {
                return await this.taskHubContainer.ExistsAsync();
            }
            finally
            {
                this.stats.StorageRequests.Increment();
            }
        }

        public async Task<bool> CreateLeaseStoreIfNotExistsAsync(TaskHubInfo eventHubInfo, bool checkIfStale)
        {
            bool result = false;
            if (!this.skipBlobContainerCreation)
            {
                result = await this.taskHubContainer.CreateIfNotExistsAsync();
                this.stats.StorageRequests.Increment();
            }

            await this.GetOrCreateTaskHubInfoAsync(eventHubInfo, checkIfStale: checkIfStale);

            return result;
        }

        public async Task<IEnumerable<BlobLease>> ListLeasesAsync()
        {
            var blobLeases = new List<BlobLease>();

            BlobContinuationToken continuationToken = null;
            do
            {
                BlobResultSegment segment = await TimeoutHandler.ExecuteWithTimeout("ListLeases", this.storageAccountName, this.settings, (context, timeoutToken) =>
                {
                    return this.leaseDirectory.ListBlobsSegmentedAsync(
                            useFlatBlobListing: true,
                            blobListingDetails: BlobListingDetails.Metadata,
                            maxResults: null,
                            currentToken: continuationToken,
                            options: null,
                            operationContext: context,
                            cancellationToken: timeoutToken);
                });
                
                continuationToken = segment.ContinuationToken;

                var downloadTasks = new List<Task<BlobLease>>();
                foreach (IListBlobItem blob in segment.Results)
                {
                    CloudBlockBlob lease = blob as CloudBlockBlob;
                    if (lease != null)
                    {
                        downloadTasks.Add(this.DownloadLeaseBlob(lease));
                    }
                }

                await Task.WhenAll(downloadTasks);

                blobLeases.AddRange(downloadTasks.Select(t => t.Result));
            }
            while (continuationToken != null);

            return blobLeases;
        }

        public async Task CreateLeaseIfNotExistAsync(string partitionId)
        {
            CloudBlockBlob leaseBlob = this.leaseDirectory.GetBlockBlobReference(partitionId);
            BlobLease lease = new BlobLease(leaseBlob) { PartitionId = partitionId };
            string serializedLease = JsonConvert.SerializeObject(lease);
            try
            {
                this.settings.Logger.PartitionManagerInfo(
                    this.storageAccountName,
                    this.taskHubName,
                    this.workerName,
                    partitionId,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "CreateLeaseIfNotExistAsync - leaseContainerName: {0}, leaseType: {1}, partitionId: {2}. blobPrefix: {3}",
                        this.leaseContainerName,
                        this.leaseType,
                        partitionId,
                        this.blobPrefix ?? string.Empty));

                await leaseBlob.UploadTextAsync(serializedLease, null, AccessCondition.GenerateIfNoneMatchCondition("*"), null, null);
            }
            catch (StorageException se)
            {
                // eat any storage exception related to conflict
                // this means the blob already exist
                if (se.RequestInformation.HttpStatusCode != 409)
                {
                    this.settings.Logger.PartitionManagerInfo(
                        this.storageAccountName,
                        this.taskHubName,
                        this.workerName,
                        partitionId,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "CreateLeaseIfNotExistAsync - leaseContainerName: {0}, leaseType: {1}, partitionId: {2}, blobPrefix: {3}, exception: {4}",
                            this.leaseContainerName,
                            this.leaseType,
                            partitionId,
                            this.blobPrefix ?? string.Empty,
                            se.Message));
                }
            }
            finally
            {
                this.stats.StorageRequests.Increment();
            }
        }

        public async Task<BlobLease> GetLeaseAsync(string paritionId)
        {
            CloudBlockBlob leaseBlob = this.leaseDirectory.GetBlockBlobReference(paritionId);
            if (await leaseBlob.ExistsAsync())
            {
                return await this.DownloadLeaseBlob(leaseBlob);
            }

            return null;
        }

        public async Task<bool> RenewAsync(BlobLease lease)
        {
            CloudBlockBlob leaseBlob = lease.Blob;
            try
            {
                await leaseBlob.RenewLeaseAsync(accessCondition: AccessCondition.GenerateLeaseCondition(lease.Token), options: this.renewRequestOptions, operationContext: null);
            }
            catch (StorageException storageException)
            {
                throw HandleStorageException(lease, storageException);
            }
            finally
            {
                this.stats.StorageRequests.Increment();
            }

            return true;
        }

        public async Task<bool> AcquireAsync(BlobLease lease, string owner)
        {
            CloudBlockBlob leaseBlob = lease.Blob;
            try
            {
                await leaseBlob.FetchAttributesAsync();
                string newLeaseId = Guid.NewGuid().ToString();
                if (leaseBlob.Properties.LeaseState == LeaseState.Leased)
                {
                    lease.Token = await leaseBlob.ChangeLeaseAsync(newLeaseId, accessCondition: AccessCondition.GenerateLeaseCondition(lease.Token));
                }
                else
                {
                    lease.Token = await leaseBlob.AcquireLeaseAsync(this.leaseInterval, newLeaseId);
                }

                this.stats.StorageRequests.Increment();
                lease.Owner = owner;
                // Increment Epoch each time lease is acquired or stolen by new host
                lease.Epoch += 1;
                await leaseBlob.UploadTextAsync(JsonConvert.SerializeObject(lease), null, AccessCondition.GenerateLeaseCondition(lease.Token), null, null);
                this.stats.StorageRequests.Increment();
            }
            catch (StorageException storageException)
            {
                this.stats.StorageRequests.Increment();
                throw HandleStorageException(lease, storageException);
            }

            return true;
        }

        public async Task<bool> ReleaseAsync(BlobLease lease)
        {
            CloudBlockBlob leaseBlob = lease.Blob;
            try
            {
                string leaseId = lease.Token;

                BlobLease copy = new BlobLease(lease);
                copy.Token = null;
                copy.Owner = null;
                await leaseBlob.UploadTextAsync(JsonConvert.SerializeObject(copy), null, AccessCondition.GenerateLeaseCondition(leaseId), null, null);
                this.stats.StorageRequests.Increment();
                await leaseBlob.ReleaseLeaseAsync(accessCondition: AccessCondition.GenerateLeaseCondition(leaseId));
                this.stats.StorageRequests.Increment();
            }
            catch (StorageException storageException)
            {
                this.stats.StorageRequests.Increment();
                throw HandleStorageException(lease, storageException);
            }

            return true;
        }

        public async Task DeleteAsync(BlobLease lease)
        {
            CloudBlockBlob leaseBlob = lease.Blob;
            try
            {
                await leaseBlob.DeleteIfExistsAsync();
            }
            finally
            {
                this.stats.StorageRequests.Increment();
            }
        }

        public async Task DeleteAllAsync()
        {
            try
            {
                await this.taskHubContainer.DeleteIfExistsAsync();
            }
            finally
            {
                this.stats.StorageRequests.Increment();
            }
        }

        public async Task<bool> UpdateAsync(BlobLease lease)
        {
            if (lease == null || string.IsNullOrWhiteSpace(lease.Token))
            {
                return false;
            }

            CloudBlockBlob leaseBlob = lease.Blob;
            try
            {
                // First renew the lease to make sure checkpoint will go through
                await leaseBlob.RenewLeaseAsync(accessCondition: AccessCondition.GenerateLeaseCondition(lease.Token));
            }
            catch (StorageException storageException)
            {
                throw HandleStorageException(lease, storageException);
            }
            finally
            {
                this.stats.StorageRequests.Increment();
            }

            try
            {
                await leaseBlob.UploadTextAsync(JsonConvert.SerializeObject(lease), null, AccessCondition.GenerateLeaseCondition(lease.Token), null, null);
            }
            catch (StorageException storageException)
            {
                throw HandleStorageException(lease, storageException, true);
            }
            finally
            {
                this.stats.StorageRequests.Increment();
            }

            return true;
        }

        public async Task CreateTaskHubInfoIfNotExistAsync(TaskHubInfo taskHubInfo)
        {
            string serializedInfo = JsonConvert.SerializeObject(taskHubInfo);
            try
            {
                await this.taskHubInfoBlob.UploadTextAsync(serializedInfo, null, AccessCondition.GenerateIfNoneMatchCondition("*"), null, null);
            }
            catch (StorageException)
            {
                // eat any storage exception related to conflict
                // this means the blob already exist
            }
            finally
            {
                this.stats.StorageRequests.Increment();
            }
        }

        internal async Task<TaskHubInfo> GetOrCreateTaskHubInfoAsync(TaskHubInfo newTaskHubInfo, bool checkIfStale)
        {
            TaskHubInfo currentTaskHubInfo = await this.GetTaskHubInfoAsync();
            if (currentTaskHubInfo != null)
            {
                if (checkIfStale && IsStale(currentTaskHubInfo, newTaskHubInfo))
                {
                    this.settings.Logger.PartitionManagerWarning(
                       this.storageAccountName,
                       this.taskHubName,
                       this.workerName,
                       string.Empty,
                       $"Partition count for the task hub {currentTaskHubInfo.TaskHubName} from {currentTaskHubInfo.PartitionCount} to {newTaskHubInfo.PartitionCount}. This could result in errors for in-flight orchestrations.");

                    try
                    {
                        string serializedInfo = JsonConvert.SerializeObject(newTaskHubInfo);
                        await this.taskHubInfoBlob.UploadTextAsync(serializedInfo, null, AccessCondition.GenerateEmptyCondition(), null, null);
                    } 
                    catch (StorageException)
                    {
                        // eat any storage exception as this is a best effort to
                        // to update the metadata used by Azure Functions scaling logic
                    }
                    finally
                    {
                        this.stats.StorageRequests.Increment();
                    }

                }
                return currentTaskHubInfo;
            }

            await this.CreateTaskHubInfoIfNotExistAsync(newTaskHubInfo);
            return newTaskHubInfo;
        }

        private bool IsStale(TaskHubInfo currentTaskHubInfo, TaskHubInfo newTaskHubInfo)
        {
            return !currentTaskHubInfo.TaskHubName.Equals(newTaskHubInfo.TaskHubName, StringComparison.OrdinalIgnoreCase)
                    || !currentTaskHubInfo.PartitionCount.Equals(newTaskHubInfo.PartitionCount);
        }

        void Initialize()
        {
            this.storageClient.DefaultRequestOptions.MaximumExecutionTime = StorageMaximumExecutionTime;

            this.taskHubContainer = this.storageClient.GetContainerReference(this.leaseContainerName);

            string leaseDirectoryName = string.IsNullOrWhiteSpace(this.blobPrefix)
                ? this.leaseType
                : this.blobPrefix + this.leaseType;
            this.leaseDirectory = this.taskHubContainer.GetDirectoryReference(leaseDirectoryName);

            string taskHubInfoBlobFileName = string.IsNullOrWhiteSpace(this.blobPrefix)
                ? TaskHubInfoBlobName
                : this.blobPrefix + TaskHubInfoBlobName;

            this.taskHubInfoBlob = this.taskHubContainer.GetBlockBlobReference(taskHubInfoBlobFileName);
        }

        async Task<TaskHubInfo> GetTaskHubInfoAsync()
        {
            if (await this.taskHubInfoBlob.ExistsAsync())
            {
                await taskHubInfoBlob.FetchAttributesAsync();
                this.stats.StorageRequests.Increment();
                string serializedEventHubInfo = await this.taskHubInfoBlob.DownloadTextAsync();
                this.stats.StorageRequests.Increment();
                return JsonConvert.DeserializeObject<TaskHubInfo>(serializedEventHubInfo);
            }

            this.stats.StorageRequests.Increment();
            return null;
        }

        async Task<BlobLease> DownloadLeaseBlob(CloudBlockBlob blob)
        {
            string serializedLease = await blob.DownloadTextAsync();
            this.stats.StorageRequests.Increment();
            BlobLease deserializedLease = JsonConvert.DeserializeObject<BlobLease>(serializedLease);
            deserializedLease.Blob = blob;

            // Workaround: for some reason storage client reports incorrect blob properties after downloading the blob
            await blob.FetchAttributesAsync();
            this.stats.StorageRequests.Increment();
            return deserializedLease;
        }

        static Exception HandleStorageException(Lease lease, StorageException storageException, bool ignoreLeaseLost = false)
        {
            if (storageException.RequestInformation.HttpStatusCode == (int)HttpStatusCode.Conflict
                || storageException.RequestInformation.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed)
            {
                // Don't throw LeaseLostException if caller chooses to ignore it.
                StorageExtendedErrorInformation extendedErrorInfo = storageException.RequestInformation.ExtendedErrorInformation;
                if (!ignoreLeaseLost || extendedErrorInfo == null || extendedErrorInfo.ErrorCode != BlobErrorCodeStrings.LeaseLost)
                {
                    return new LeaseLostException(lease, storageException);
                }
            }

            return storageException;
        }
    }
}
