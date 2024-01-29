using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
//what I added
//dotnet add package Microsoft.AspNetCore.Mvc
//dotnet add package Microsoft.TeamFoundationServer.Client
using System.ComponentModel;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.Services.Identity;
using Microsoft.VisualStudio.Services.Common;

namespace azure.demo
{
    public class HttpAzDOApprovalTrigger
    {
        private readonly ILogger _logger;

        public HttpAzDOApprovalTrigger(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<HttpAzDOApprovalTrigger>();
        }

        [Function("HttpAzDOApprovalTrigger")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            TypeDescriptor.AddAttributes(typeof(IdentityDescriptor), new TypeConverterAttribute(typeof(IdentityDescriptorConverter).FullName));
            TypeDescriptor.AddAttributes(typeof(SubjectDescriptor), new TypeConverterAttribute(typeof(SubjectDescriptorConverter).FullName));
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            // Get request body
            var messageBody = await req.ReadAsStringAsync().ConfigureAwait(false);

            // Fetch all the VSTS properties from the headers
            var taskProperties = GetTaskProperties(req.Headers);

            // Created task execution handler
            Task.Run(() =>
            {
                var executionHandler = new PipelineTaskHandler(taskProperties);
                _ = executionHandler.Execute(_logger, CancellationToken.None).Result;
            });

            // Step #1: Confirms the receipt of the check payload
            return new OkObjectResult("Request received and accepted!");
        }

        private TaskProperties GetTaskProperties(HttpHeadersCollection requestHeaders)
        {
            IDictionary<string, string> taskProperties = new Dictionary<string, string>();

            foreach (var requestHeader in requestHeaders)
            {
                _logger.LogInformation($"Request header: {requestHeader.Key.ToLower()} = {requestHeader.Value.First()}");
                taskProperties.Add(requestHeader.Key, requestHeader.Value.First());
            }

            return new TaskProperties(taskProperties);
        }

        private System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("Microsoft.VisualStudio.Services.WebApi"))
            {
                return typeof(IdentityDescriptor).Assembly;
            }
            return null;
        }
    }
}
