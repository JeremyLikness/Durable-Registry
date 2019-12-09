using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Threading.Tasks;

namespace Serverless.Day21
{
    public class RegistryStats : IRegistryStats
    {
        public int RegistryCount { get; set; }
        public int ItemsCount { get; set;}

        public void NewItem()
        {
            ItemsCount += 1;
        }

        public void NewRegistry()
        {
            RegistryCount += 1;
        }

        [FunctionName(nameof(RegistryStats))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx)
            => ctx.DispatchAsync<RegistryStats>();
    }
}