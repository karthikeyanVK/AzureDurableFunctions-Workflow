using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace AzDurableFunctions
{
    public static class TriggerHttpFunction
    {
        [FunctionName("HttpStart")]
        public static async Task<HttpResponseMessage> Run(
             [HttpTrigger(AuthorizationLevel.Function, methods: "post", Route = "orchestrators/{functionName}")] HttpRequestMessage req,
             [DurableClient] IDurableClient starter,
             string functionName,
             ILogger log)
        {
            // Function input comes from the request content.
            object eventData = await req.Content.ReadAsAsync<object>();
            string instanceId = await starter.StartNewAsync(functionName, eventData);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("OrderWorkflowEmail")]
        public static async Task<HttpResponseMessage> OrderWorkflowEmail(
            [HttpTrigger(AuthorizationLevel.Function, methods: "get", Route = "Order/Workflow")] HttpRequestMessage req,
            [DurableClient] IDurableClient starter, 
            ILogger log)
        {
            string instanceId = req.RequestUri.ParseQueryString().GetValues("instanceid")[0];
            int approvedValue =  Int16.Parse(req.RequestUri.ParseQueryString().GetValues("approve")[0]);
            var status = await starter.GetStatusAsync(instanceId);
            if (status != null && (status.RuntimeStatus == OrchestrationRuntimeStatus.Running || status.RuntimeStatus == OrchestrationRuntimeStatus.Pending))
            {
                
                await starter.RaiseEventAsync(instanceId, "approvalStatus", approvedValue);
                log.LogInformation($"Approval process Completed");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Thanks for your selection! :)") };
            }
            else
            {
                log.LogInformation($"Issue with Approval process");
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("oops! Something went wrong! :(, Probably expired") };
            }



        }
    }
}
