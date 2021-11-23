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
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Auth;

    /// <summary>
    /// Connection details of the Azure Storage account
    /// </summary>
    public sealed class StorageAccountDetails
    {
        /// <summary>
        /// The storage account credentials
        /// </summary>
        public StorageCredentials StorageCredentials { get; set; }

        /// <summary>
        /// The storage account name
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// The storage account endpoint suffix
        /// </summary>
        public string EndpointSuffix { get; set; }

        /// <summary>
        /// The storage account connection string.
        /// </summary>
        /// <remarks>
        /// If specified, this value overrides any other settings.
        /// </remarks>
        public string ConnectionString { get; set; }

        /// <summary>
        /// The data plane URI for the blob service of the storage account.
        /// </summary>
        public Uri BlobServiceUri { get; set; }

        /// <summary>
        /// The data plane URI for the queue service of the storage account.
        /// </summary>
        public Uri QueueServiceUri { get; set; }

        /// <summary>
        /// The data plane URI for the table service of the storage account.
        /// </summary>
        public Uri TableServiceUri { get; set; }

        /// <summary>
        /// Convert this to its equivalent <see cref="CloudStorageAccount"/>.
        /// </summary>
        /// <returns>The corresponding <see cref="CloudStorageAccount"/> instance.</returns>
        public CloudStorageAccount ToCloudStorageAccount()
        {
            if (!string.IsNullOrEmpty(this.ConnectionString))
            {
                return CloudStorageAccount.Parse(this.ConnectionString);
            }
            else if (this.BlobServiceUri != null || this.QueueServiceUri != null || this.TableServiceUri != null)
            {
                string accountName = this.ReconcileAccountName();
                return new CloudStorageAccount(
                    this.StorageCredentials,
                    this.BlobServiceUri ?? StorageAccount.GetDefaultServiceUri(accountName, StorageServiceType.Blob),
                    this.QueueServiceUri ?? StorageAccount.GetDefaultServiceUri(accountName, StorageServiceType.Queue),
                    this.TableServiceUri ?? StorageAccount.GetDefaultServiceUri(accountName, StorageServiceType.Table),
                    fileEndpoint: null);
            }
            else
            {
                return new CloudStorageAccount(
                    this.StorageCredentials,
                    this.AccountName,
                    this.EndpointSuffix,
                    useHttps: true);
            }
        }

        internal string ReconcileAccountName()
        {
            if (string.IsNullOrEmpty(this.AccountName))
            {
                return this.StorageCredentials.AccountName;
            }
            else if (
                !string.IsNullOrEmpty(this.StorageCredentials.AccountName) &&
                !this.StorageCredentials.AccountName.Equals(this.AccountName, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Account name '{this.AccountName}' does not match the name '{this.StorageCredentials.AccountName}' in the credentials.");
            }
            else
            {
                return this.AccountName;
            }
        }
    }
}
