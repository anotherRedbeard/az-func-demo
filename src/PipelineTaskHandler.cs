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
        private TaskLogger _taskLogger;


        public PipelineTaskHandler(TaskProperties taskProperties)
        {
            _taskProperties = taskProperties;
        }

        public async Task<TaskResult> Execute(ILogger log, CancellationToken cancellationToken)
        {
            var taskClient = new TaskClient(_taskProperties);
            var taskResult = TaskResult.Failed;
            var allTemplatesFound = false;

            try
            {
                // create timeline record if not provided
                _taskLogger = new TaskLogger(_taskProperties, taskClient);

                // Step #2: Send a status update to Azure Pipelines that the check started
                await _taskLogger.LogImmediately("Check started!");

                //get build id
                var buildId = _taskProperties.MessageProperties["buildid"];

                //get required templates
                var requiredTemplatesCommaDelimited = _taskProperties.MessageProperties["requiredtemplates"];

                //create object with list of strings that represent template names from comma delimited string
                var templateNames = requiredTemplatesCommaDelimited.Split(',').ToList();

                // connect to api to look for template names
                using (var client = new HttpClient())
                {
                    await _taskLogger.LogImmediately($"Connecting to Azure DevOps API to check for required templates");

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_taskProperties.AuthToken}")));
                    await _taskLogger.LogImmediately($"token={_taskProperties.AuthToken}");

                    var url = $"{_taskProperties.PlanUri}{_taskProperties.ProjectId}/_apis/build/builds/{buildId}/logs/2?api-version=6.0";
                    var response = await client.GetStringAsync(url);

                    // look in response for template names and set a variable to true if they are all found
                    allTemplatesFound = templateNames.All(response.Contains);
                }


                // Step #5: Send a status update with the result of the search
                await _taskLogger.LogImmediately($"All templates were found to be present: {allTemplatesFound}");
                taskResult = allTemplatesFound ? TaskResult.Succeeded : TaskResult.Failed;
                return await Task.FromResult(taskResult);
            }
            catch (Exception e)
            {
                if (_taskLogger != null)
                {
                    if (e is VssServiceException)
                    {
                        await _taskLogger.Log("\n Make sure task's Completion event is set to Callback!").ConfigureAwait(false);
                    }
                    await _taskLogger.Log(e.ToString()).ConfigureAwait(false);
                }
            }
            finally
            {
                if (_taskLogger != null)
                {
                    await _taskLogger.End().ConfigureAwait(false);
                }

                // Step #6: Send a check decision to Azure Pipelines
                await taskClient.ReportTaskCompleted(_taskProperties.TaskInstanceId, taskResult, cancellationToken).ConfigureAwait(false);
            }
            return taskResult;
        }
    }
}
