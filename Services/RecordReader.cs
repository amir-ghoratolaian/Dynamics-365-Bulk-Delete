using BulkDeleteParallel.Configuration;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace BulkDeleteParallel.Services;

public class RecordReader
{
    private readonly IOrganizationService _service;
    private readonly DeleteConfiguration _config;

    public RecordReader(
        IOrganizationService service,
        DeleteConfiguration config)
    {
        _service = service;
        _config = config;
    }

    public async IAsyncEnumerable<Guid[]> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int page = 1;
        string? cookie = null;

        var buffer =
            new List<Guid>(_config.BatchSize);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fetch =
                BuildFetchXml(
                    page,
                    cookie);

            var result =
                await Task.Run(() =>
                        _service.RetrieveMultiple(
                            new FetchExpression(fetch)),
                    cancellationToken);

            Console.WriteLine(
                $"Read page {page}: {result.Entities.Count}");

            foreach (var entity in result.Entities)
            {
                buffer.Add(entity.Id);

                if (buffer.Count >= _config.BatchSize)
                {
                    yield return buffer.ToArray();

                    buffer.Clear();
                }
            }

            if (!result.MoreRecords)
                break;

            cookie = result.PagingCookie;
            page++;
        }

        if (buffer.Count > 0)
        {
            yield return buffer.ToArray();
        }

        Console.WriteLine("Reading finished");
    }

    private string BuildFetchXml(
        int page,
        string? cookie)
    {
        var paging =
            string.IsNullOrEmpty(cookie)
                ? ""
                : $"paging-cookie=\"{SecurityElement.Escape(cookie)}\"";

        return $@"
<fetch page='{page}'
       count='{_config.FetchPageSize}'
       {paging}>

 <entity name='{_config.EntityLogicalName}'>

   <attribute name='{_config.EntityLogicalName}id'/>

   {_config.FilterXml}

   <order attribute='{_config.EntityLogicalName}id'/>

 </entity>

</fetch>";
    }
}