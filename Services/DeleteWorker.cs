using BulkDeleteParallel.Configuration;
using BulkDeleteParallel.Models;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BulkDeleteParallel.Services;

public class DeleteWorker(ServiceClient Client, DeleteConfiguration Config)
{
    public async Task<BatchResult> DeleteAsync(Guid[] ids, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = new ExecuteMultipleRequest
        {
            Settings = new ExecuteMultipleSettings
            {
                ContinueOnError = true,

                // Important:
                // We need responses to know failed deletes
                ReturnResponses = true
            },

            Requests = new OrganizationRequestCollection()
        };

        foreach (var id in ids)
        {
            var deleteRequest = new DeleteRequest
            {
                Target = new EntityReference(Config.EntityLogicalName, id)
            };

            if (Config.BypassSyncPlugins)
            {
                deleteRequest.Parameters.Add("BypassCustomPluginExecution", true);
            }

            request.Requests.Add(deleteRequest);
        }

        ExecuteMultipleResponse response;

        try
        {
            response = await RetryHelper.ExecuteAsync(
                    async () =>
                    {
                        return (ExecuteMultipleResponse)await Client.ExecuteAsync(request);
                    },
                    Config.RetryCount);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ExecuteMultiple failed completely: {ex.Message}");

            foreach (var id in ids)
            {
                Console.WriteLine($"Id={id} Status=Failed Reason={ex.Message}");
            }

            return new BatchResult
            {
                Deleted = 0,
                Failed = ids.Length,
                Seconds = stopwatch.Elapsed.TotalSeconds,
                FailedIds = ids.ToList()
            };
        }

        // Dataverse ObjectDoesNotExist fault code. A retry re-submitting
        // a batch that already succeeded server-side will hit this for
        // every record that was actually deleted the first time around.
        const int ObjectDoesNotExist = unchecked((int)0x80040217);

        int deleted = 0;
        int alreadyGone = 0;
        var failedIds = new List<Guid>();

        foreach (var item in response.Responses)
        {
            var sourceRequest = request.Requests[item.RequestIndex] as DeleteRequest;

            var id = sourceRequest?.Target.Id ?? Guid.Empty;

            if (item.Fault == null)
            {
                deleted++;
                Console.WriteLine($"Id={id} Status=Deleted");
                continue;
            }

            if (item.Fault.ErrorCode == ObjectDoesNotExist)
            {
                alreadyGone++;
                Console.WriteLine($"Id={id} Status=AlreadyGone Reason={item.Fault.Message}");
                continue;
            }

            failedIds.Add(id);

            Console.WriteLine($"Id={id} Status=Failed Reason={item.Fault.Message}");
        }

        stopwatch.Stop();

        return new BatchResult
        {
            Deleted = deleted,
            Failed = failedIds.Count,
            AlreadyGone = alreadyGone,
            Seconds = stopwatch.Elapsed.TotalSeconds,
            FailedIds = failedIds
        };
    }
}