using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Serverless.Day21
{
    public class RegistryList : IRegistryList
    {
        public string Id { get; set; }
        public List<string> Items { get; set; }

        public RegistryList()
        {
            Items = new List<string>();
        }

        public void AddItem(string item)
        {
            this.Items.Add(item);
        }

        public void New(string id)
        {
            this.Id = id;
        }

        [FunctionName(nameof(RegistryList))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx)
            => ctx.DispatchAsync<RegistryList>();
    }
}
