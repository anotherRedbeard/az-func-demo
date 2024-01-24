using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Common;

namespace azure.demo
{
    public class PipelineTaskHandler
    {

        private readonly TaskProperties _taskProperties;
        private readonly ILogger _logger;

        public PipelineTaskHandler(TaskProperties taskProperties, ILogger logger)
        {
            _taskProperties = taskProperties;
            _logger = logger;
        }

        public async Task<TaskResult> Execute(ILogger log, CancellationToken cancellationToken)
        {
            var taskClient = new TaskClient(_taskProperties);
            var taskResult = TaskResult.Failed;
            var allTemplatesFound = false;

            try
            {

                // Step #2: Send a status update to Azure Pipelines that the check started
                _logger.LogInformation("Check started!");

                //get build id
                var buildId = _taskProperties.MessageProperties["buildid"];

                //get required templates
                var requiredTemplatesCommaDelimited = _taskProperties.MessageProperties["requiredtemplates"];

                //create object with list of strings that represent template names from comma delimited string
                var templateNames = requiredTemplatesCommaDelimited.Split(',').ToList();

                // connect to api to look for template names
                using (var client = new HttpClient())
                {
                    _logger.LogInformation($"Checking for required templates");

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_taskProperties.AuthToken}")));

                    var url = $"{_taskProperties.PlanUri}{_taskProperties.ProjectId}/_apis/build/builds/{buildId}/logs/2?api-version=6.0";
                    var response = await client.GetStringAsync(url);

                    // look in response for template names and set a variable to true if they are all found
                    allTemplatesFound = templateNames.All(response.Contains);
                }


                // Step #5: Send a status update with the result of the search
                _logger.LogInformation($"All templates were found to be present: {allTemplatesFound}");
                taskResult = allTemplatesFound ? TaskResult.Succeeded : TaskResult.Failed;
                return await Task.FromResult(taskResult);
            }
            catch (Exception e)
            {
                if (_logger != null)
                {
                    if (e is VssServiceException)
                    {
                        _logger.LogError(e, "Make sure task's Completion event is set to Callback!");
                    }
                    _logger.LogError(e, "Error occurred while executing the task.");
                }
            }
            finally
            {
                if (_logger != null)
                {
                    _logger.LogInformation("Check completed!");
                }

                // Step #6: Send a check decision to Azure Pipelines
                await taskClient.ReportTaskCompleted(_taskProperties.TaskInstanceId, taskResult, cancellationToken).ConfigureAwait(false);
            }
            return taskResult;
        }
    }
}
