using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using SendGrid.Helpers.Mail;
using System.IO;
using SendGrid;

namespace AzDurableFunctions
{
    public static class ApprovalWorkflow
    {
        [FunctionName("ApprovalWorkflow")]
        public static async Task<bool> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            OrderDetails orderDetails = context.GetInput<OrderDetails>();
            if (orderDetails is null)
            {
                throw new ArgumentNullException(
                    nameof(orderDetails),
                    "OrderDetails is required.");
            }
            EmailDetails emailDetails = new EmailDetails
            {
                FromAddress = "",
                Subject = CreateEmailBody(context.InstanceId)

            };

            await context.CallActivityAsync("SendApprovalEmail", emailDetails);

            if (!context.IsReplaying)
            {
                var expirationTimer = context.CurrentUtcDateTime.AddMinutes(2);
                await context.CreateTimer(expirationTimer, CancellationToken.None);
            }

            using (var timeoutCts = new CancellationTokenSource())
            {
                // The user has 24 hours to respond.
                var expirationTimer = context.CurrentUtcDateTime.AddHours(24);
                Task timeoutTask = context.CreateTimer(expirationTimer, timeoutCts.Token);

                Task<int> approveEvent = context.WaitForExternalEvent<int>("approvalStatus");

                var winner = await Task.WhenAny(approveEvent, timeoutTask);

                if (approveEvent == winner && context.CurrentUtcDateTime < expirationTimer)
                {

                    if (approveEvent.Result == 1)
                    {
                        log.LogInformation("Approved");
                    }
                    else
                    {
                        log.LogInformation("Rejected");
                    }
                }
                else
                {
                    log.LogInformation("TimeOut");
                }
                // All pending timers must be complete or canceled before the function exits.
                timeoutCts.Cancel();
                return true;
            }

        }

        [FunctionName("SendApprovalEmail")]
        public static void SendApprovalEmail([ActivityTrigger]EmailDetails emailDetails, ILogger log)
        {

            var apiKey = Environment.GetEnvironmentVariable("SendGridKey");
            var client = new SendGridClient(apiKey);
            var from = new EmailAddress("karthik3030@gmail.com", "BPB Electronics");
            var subject = "Order Status";
            var to = new EmailAddress("karthik3030@gmail.com", "BPB Customer");

            var htmlContent = emailDetails.Subject;
            var msg = MailHelper.CreateSingleEmail(from, to, subject, "", htmlContent);
            var response = client.SendEmailAsync(msg).GetAwaiter().GetResult();
            log.LogInformation("Response from SendGrid " + response.ToString());
            return;
        }

        public static string CreateEmailBody(string instanceId)
        {
            return string.Format(File.ReadAllText("mailFormat.html"), "http://localhost:7071", instanceId);

        }
    }
}