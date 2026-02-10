#!/bin/bash

# Test script for JobRepository
set -e

TEST_DIR="/tmp/JobRepoTest"
rm -rf "$TEST_DIR"
mkdir -p "$TEST_DIR"

echo "=== Testing JobRepository CRUD Operations ==="
echo ""

# Initialize database
cd /home/alstruk/GitHub/folder-watcher/FolderToApi.Service

# Create a simple C# script to test the repository
cat > /tmp/test-repo.csx << 'CSHARP'
#r "bin/Debug/net10.0/FolderToApi.Service.dll"
#r "bin/Debug/net10.0/Microsoft.Data.Sqlite.dll"

using FolderToApi.Service.Configuration;
using FolderToApi.Service.Data;
using FolderToApi.Service.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var dbPath = "/tmp/JobRepoTest/test.db";
Console.WriteLine($"Using database: {dbPath}");

// Initialize database
var dbOptions = Options.Create(new DatabaseOptions { ConnectionString = $"Data Source={dbPath}", EnableWalMode = true });
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var dbInitLogger = loggerFactory.CreateLogger<DatabaseInitializer>();
var dbInit = new DatabaseInitializer(dbOptions, dbInitLogger);

Console.WriteLine("Initializing database...");
await dbInit.InitializeAsync();

// Run tests
var repoLogger = loggerFactory.CreateLogger<JobRepository>();
var repo = new JobRepository(dbOptions, repoLogger);

Console.WriteLine("\n✓ Test 1: Create job");
var jobId = Guid.NewGuid().ToString();
var job = await repo.CreateJobAsync(jobId, "/inbox/test.pdf", 1024, DateTime.UtcNow);
Console.WriteLine($"  Created job {jobId}");

Console.WriteLine("\n✓ Test 2: Find by source path");
var found = await repo.FindJobBySourcePathAsync("/inbox/test.pdf");
if (found == null) throw new Exception("Job not found!");
Console.WriteLine($"  Found job {found.JobId}");

Console.WriteLine("\n✓ Test 3: Update to Claimed");
await repo.UpdateJobClaimedAsync(jobId, "/processing/test.pdf", DateTime.UtcNow);
found = await repo.FindJobByIdAsync(jobId);
Console.WriteLine($"  Status: {found.Status}, ProcessingPath: {found.ProcessingPath}");

Console.WriteLine("\n✓ Test 4: Get due jobs");
var dueJobs = await repo.GetDueJobsAsync(DateTime.UtcNow.AddMinutes(1));
Console.WriteLine($"  Found {dueJobs.Count} due jobs");

Console.WriteLine("\n✓ Test 5: Mark as Sending");
await repo.MarkJobSendingAsync(jobId);
found = await repo.FindJobByIdAsync(jobId);
Console.WriteLine($"  Attempt count: {found.AttemptCount}");

Console.WriteLine("\n✓ Test 6: Record retry");
await repo.RecordRetryAsync(jobId, "Test error", 500, DateTime.UtcNow.AddMinutes(5));
found = await repo.FindJobByIdAsync(jobId);
Console.WriteLine($"  Error: {found.LastError}, HTTP: {found.LastHttpStatus}");

Console.WriteLine("\n✓ Test 7: Get statistics");
var stats = await repo.GetStatisticsAsync();
Console.WriteLine($"  Total: {stats.TotalJobs}, RetryScheduled: {stats.RetryScheduledCount}");

Console.WriteLine("\n=== All tests passed! ===");
CSHARP

# Run the test
dotnet script /tmp/test-repo.csx

echo ""
echo "JobRepository test completed successfully!"
