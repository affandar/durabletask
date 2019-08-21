﻿using DurableTask.Core.Tracking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DurableTask.SqlServer.Tests
{
    [TestClass]
    public class WriteEntitiesTests : BaseTestClass
    {
        [TestMethod]
        public async Task VerifyOrchestrationStatePersistedTest()
        {
            var entities = new List<InstanceEntityBase>();
            entities.AddRange(Utils.InfiniteOrchestrationTestData().Take(5));

            await InstanceStore.WriteEntitiesAsync(entities);

            //second call should simply update each entity, not write new ones
            await InstanceStore.WriteEntitiesAsync(entities);

            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"SELECT COUNT(1) FROM {Settings.OrchestrationStateTableName}";

                await connection.OpenAsync();
                var count = (int)await command.ExecuteScalarAsync();

                Assert.AreEqual(entities.OfType<OrchestrationStateInstanceEntity>().Count(), count, "Incorrect Orchestration Instance row count.");
            }
        }

        [TestMethod]
        public async Task VerifyWorkItemStatePersistedTest()
        {
            var entities = new List<InstanceEntityBase>();

            entities.Add(Utils.InfiniteWorkItemTestData(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")).First());

            await InstanceStore.WriteEntitiesAsync(entities);

            //second call should simply update each entity, not write new ones
            await InstanceStore.WriteEntitiesAsync(entities);

            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"SELECT COUNT(1) FROM {Settings.WorkItemTableName}";

                await connection.OpenAsync();
                var count = (int)await command.ExecuteScalarAsync();

                Assert.AreEqual(entities.OfType<OrchestrationWorkItemInstanceEntity>().Count(), count, "Incorrect Work Item row count.");
            }
        }

        [TestMethod]
        public async Task VerifyWriteEntitiesFailsForUnexpectedType()
        {
            var state = new OrchestrationJumpStartInstanceEntity();

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => InstanceStore.WriteEntitiesAsync(new[] { state }));
        }
    }
}
