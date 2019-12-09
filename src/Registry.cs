using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Threading;
using System;

namespace Serverless.Day21
{
    // Functions to manage registry
    public static class Registry
    {
        // Event to raise when it's time to close the registry
        public const string CLOSE_TASK = "Close";

        // Global identifier for statistics
        public static readonly EntityId StatsId = new EntityId(
            nameof(RegistryStats), string.Empty);

        // Extension to turn string into unique id for registry
        public static EntityId AsRegistryId(this string id)
        {
            return new EntityId(nameof(RegistryList), id);
        }

        // Opens a new registry and returns the unique id
        [FunctionName(nameof(Open))]
        public static async Task<IActionResult> Open(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            [DurableClient]IDurableClient starter,
            ILogger log)
        {
            log.LogInformation("Request to open a new registry.");

            // kick off the workflow
            var id = await starter.StartNewAsync(nameof(RegistryOrchestration), (object)$"List opened at {DateTime.Now}");

            // return the instance id
            return new OkObjectResult(id);
        }

        // Returns global statistics
        [FunctionName(nameof(Stats))]
        public static async Task<IActionResult> Stats(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            [DurableClient]IDurableClient client,
            ILogger log)
        {
            log.LogInformation("Request for stats.");

            // read the entity and return the state or an empty object
            // when not found
            var stats = await client.ReadEntityStateAsync<RegistryStats>(StatsId);
            return new OkObjectResult(stats.EntityExists ?
                stats.EntityState : new RegistryStats());
        }

        // Adds an item to an open registry
        [FunctionName(nameof(Add))]
        public static async Task<IActionResult> Add(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Add/{id}")]
                HttpRequest req,
            string id,
            [DurableClient]IDurableClient client,
            ILogger log)
        {
            log.LogInformation("Request to add to registry {id}.", id);

            // id is required on route like Add/{id}
            if (string.IsNullOrWhiteSpace(id))
            {
                return new BadRequestObjectResult("Id is required after Add/ on the route.");
            }

            // item is required like Add/{id}?item={item}
            if (!req.Query.ContainsKey("item"))
            {
                return new BadRequestObjectResult("Item is required as a querystring parameter");
            }

            // grab the item
            var item = req.Query["item"];

            // confirm a workflow exists
            var instance = await client.GetStatusAsync(id);

            // doesn't exist
            if (instance == null)
            {
                return new BadRequestObjectResult($"Unable to find workflow with id {id}");
            }

            // no longer open/running
            if (instance.RuntimeStatus != OrchestrationRuntimeStatus.Running)
            {
                return new BadRequestObjectResult($"Instance {id} is not active.");
            }

            // add item to list
            await client.SignalEntityAsync<IRegistryList>(
                id.AsRegistryId(),
                list => list.AddItem(item));

            // update total item count
            await client.SignalEntityAsync<IRegistryStats>(
                StatsId,
                stats => stats.NewItem());

            return new OkResult();
        }

        // Closes a registry
        [FunctionName(nameof(Finish))]
        public static async Task<IActionResult> Finish(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Finish/{id}")] HttpRequest req,
            string id,
            [DurableClient]IDurableClient client,
            ILogger log)
        {
            log.LogInformation("Request to close to registry {id}.", id);

            // id is required on route Finish/{id}
            if (string.IsNullOrWhiteSpace(id))
            {
                return new BadRequestObjectResult("Id is required after Finish/ on the route.");
            }

            // confirm a workflow exists
            var instance = await client.GetStatusAsync(id);

            // not there
            if (instance == null)
            {
                return new BadRequestObjectResult($"Unable to find workflow with id {id}");
            }

            // not running
            if (instance.RuntimeStatus != OrchestrationRuntimeStatus.Running)
            {
                return new BadRequestObjectResult($"Instance {id} is not active.");
            }

            // signal to close
            await client.RaiseEventAsync(id, CLOSE_TASK, true);

            return new OkResult();
        }

        // Peeks at the status and contents of a registry
        [FunctionName(nameof(Peek))]
        public static async Task<IActionResult> Peek(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Peek/{id}")] HttpRequest req,
            string id,
            [DurableClient]IDurableClient client,
            ILogger log)
        {
            log.LogInformation("Request to peek at registry {id}.", id);

            // id is required on route Peek/{id}
            if (string.IsNullOrWhiteSpace(id))
            {
                return new BadRequestObjectResult("Id is required after Finish/ on the route.");
            }

            // default to open
            var status = "Open";

            // confirm a workflow exists
            var instance = await client.GetStatusAsync(id);

            // doesn't exist
            if (instance == null)
            {
                return new BadRequestObjectResult($"Unable to find workflow with id {id}");
            }

            // no longer running so set as "closed"
            if (instance.RuntimeStatus != OrchestrationRuntimeStatus.Running)
            {
                status = "Closed";
            }

            // get the registry
            var registry = await client.ReadEntityStateAsync<RegistryList>(
                id.AsRegistryId());

            // return status along with contents
            return new OkObjectResult(new
            {
                status,
                registry = registry.EntityState
            });
        }

        // Workflow for a registry
        [FunctionName(nameof(RegistryOrchestration))]
        public static async Task<bool> RegistryOrchestration(
            [OrchestrationTrigger]IDurableOrchestrationContext context,
            [DurableClient]IDurableClient client,
            ILogger log)
        {
            log.LogInformation("New orchestration started.");

            // this has side-effects (creating a new list) so it is
            // wrapped in an Activity task
            await context.CallActivityAsync(nameof(NewList), context.InstanceId);

            bool closedByUser = false;

            using (var timeoutCts = new CancellationTokenSource())
            {
                // 5 minutes to fill the registry
                var dueTime = context.CurrentUtcDateTime
                    .Add(TimeSpan.FromMinutes(5));

                // waiting for close ("Finish")
                var approvalEvent = context.WaitForExternalEvent<bool>(CLOSE_TASK);

                log.LogInformation($"Now: {context.CurrentUtcDateTime} Timeout: {dueTime}");

                // create the timeout
                var durableTimeout = context.CreateTimer(dueTime, timeoutCts.Token);

                // wait for whatever comes first: close event or 5 minute timeout
                var winner = await Task.WhenAny(approvalEvent, durableTimeout);

                // got here due to close event
                if (winner == approvalEvent && approvalEvent.Result)
                {
                    // timer not needed
                    timeoutCts.Cancel();
                    log.LogInformation("List {id} closed by timeout.", context.InstanceId);
                    closedByUser = true;
                }
                else
                {
                    // got here due to timeout
                    log.LogInformation("List {id} time is up!", context.InstanceId);
                }
            }

            log.LogInformation("Workflow ended. Closed by user = {status}",
                closedByUser);

            return closedByUser;
        }

        // Activity to create new registry
        [FunctionName(nameof(NewList))]
        public static async Task NewList(
            [ActivityTrigger]string id,
            [DurableClient]IDurableEntityClient client,
            ILogger log)
        {
            log.LogInformation("NewList activity for id {id}", id);

            // update the stats
            await client.SignalEntityAsync<IRegistryStats>(
                StatsId,
                entity => entity.NewRegistry()
            );

            // set up the registry entity 
            await client.SignalEntityAsync<IRegistryList>(
                id.AsRegistryId(),
                entity => entity.New(id));
        }
    }
}
