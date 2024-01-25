# az-func-demo

Repo to demo azure function app with one or more functions. See the function definitions below for each different function

## HttpAzDOApprovalTrigger.cs

This basic example shows an Azure Function that can be called from the `approvals and checks` section of an AzDO pipeline.

You should use this Azure Function in an [`Invoke Azure Function` check](https://learn.microsoft.com/azure/devops/pipelines/process/approvals?#invoke-azure-function) configured in **Callback (Asynchronous)** mode. This mode is ideal when evaluating a condition takes a while, for example, due to making a REST call.

To successfully run this example, your pipeline needs to have a single job that using the environment where you placed the check referred to above.

## Requirements

To run this example, you need the following:

- .NET Core 8
- Visual Studio Code
- Azure Functions Core Tools, at least version 4.0.5455

## Steps

The Azure Function goes through the following steps:

1. Confirms the receipt of the check payload
2. Sends a status update to Azure Pipelines that the check started
3. Uses `{AuthToken}` to make a call into Azure DevOps api to to retrieve the pipeline run's [`Build Log`](https://learn.microsoft.com/en-us/rest/api/azure/devops/build/builds/get-build-log?view=azure-devops-rest-7.1) entry
4. Checks if the `Build Log` contains a reference to all the templates that were passed into the header
5. Sends a status update with the result of the search
6. Sends a check decision to Azure Pipelines

## Configuration

Follow these instructions to use this example as an `Invoke Azure Function` check:

1. Create a new or use an existing Azure Function in the Azure Portal
   a. I created an HttpTriggered function app using the Linux Operating System. You can find steps on how to create an azure function in the portal [here](https://learn.microsoft.com/en-us/azure/azure-functions/functions-create-function-app-portal#create-a-function-app).
2. Deploy the `HttpAzDOApprovalTrigger` to the Azure Function you are using from step 1.
  a. You can do this locally by running the following Functions Core Tools command `func azure functionapp publish <funcation-app-name>` or you can use the [pipeline that has been included in this repo](.github/workflows/main_brd-testfuncapp.yml).
    1. You will want to add the following variables and secrets to your github `Secrets and variables` Setting under Security:  `var.AZURE_FUNCTIONAPP_NAME` and `secrets.AZUREAPPSERVICE_PUBLISHPROFILE_84D22DD3A35845BBA467A9C4C2D71819`
    2. The variable `AZURE_FUNCTIONAPP_NAME` is the name of the function app you created above.
    3. The secret `AZUREAPPSERVICE_PUBLISHPROFILE_84D22DD3A35845BBA467A9C4C2D71819` is the publish profile that you will need to create from the Function apps ['Deployment Center'](https://learn.microsoft.com/en-us/azure/azure-functions/functions-how-to-use-azure-function-app-settings?tabs=portal#ftps-deployment-settings)

        ```yaml
          - name: 'Run Azure Functions Action'
            uses: Azure/functions-action@v1
            id: fa
            with:
            app-name: '${{ vars.AZURE_FUNCTIONAPP_NAME }}'
            slot-name: 'Production'
            package: '${{ env.AZURE_FUNCTIONAPP_PACKAGE_PATH }}/src/output'
            publish-profile: ${{ secrets.AZUREAPPSERVICE_PUBLISHPROFILE_84D22DD3A35845BBA467A9C4C2D71819 }}
        ```

3. In your Azure Pipelines, create a new [`Environment`](https://learn.microsoft.com/azure/devops/pipelines/process/environments) called _Sandbox_ with no resources
4. Add a Check of type `Invoke Azure Function` to _Sandbox_ with the following configuration:
   1. _Azure function URL_: the URL of the Azure Function deployed in Step 1, for example, https://azurefunctionbasichandler.azurewebsites.net/api/MyBasicFunction. You can get this URL using _Copy Function Url_ in Visual Studio Code
   2. _Function key_: a secret used to access the Azure Function, for example, the value of the _code_ query parameter after you do _Copy Function Url_ in Visual Studio Code
   3. _Headers_:

        ```json
        {
           "Content-Type":"application/json", 
           "PlanUrl": "$(system.CollectionUri)", 
           "ProjectId": "$(system.TeamProjectId)", 
           "HubName": "$(system.HostType)", 
           "PlanId": "$(system.PlanId)", 
           "JobId": "$(system.JobId)", 
           "TimelineId": "$(system.TimelineId)", 
           "TaskInstanceId": "$(system.TaskInstanceId)", 
           "AuthToken": "$(system.AccessToken)",
           "BuildId": "$(Build.BuildId)",
           "RequiredTemplates":"sandbox-job.yaml"
        }
        ```

        Don't forget to add `"BuildId": "$(Build.BuildId)"`, otherwise your Azure Function check will not work
   4. In the _Advanced_ section, choose _Callback_ as completion event. This makes the check run asynchronously
   5. In the _Control options_ section: 
      1. Set _Time between evaluations (minutes)_ to 0
      2. Set _Timeout (minutes)_ to 5, so that the build times out quickly

## Run the Check

To see your Invoke Azure Function check in action, follow these steps:

1. Create a new YAML pipeline with the following code. Make sure to replace the `project_name` and `repo_name` with your values:

  **./main-pipeline.yaml:**

  ```yml
    trigger:
      branches:
        include:
        - main

    resources:
      repositories:
        - repository: templates
          type: git
          name: <project_name>/<repo_name>

    stages:
      - stage: Sandbox
        dependsOn:
        - Build
        condition: succeeded('Build')
        jobs:
          - template: templates/sandbox-job.yaml@templates
            parameters:
              jobNum: '2'
  ```

  **./templates/sandbox-job.yaml**

  ```yml
    parameters:
  - name: jobNum
    default: '1'

  jobs:
    - deployment: Sandbox
      displayName: Sandbox Job ${{ parameters.jobNum}}
      pool:
        vmImage: windows-latest
      environment:
        name: Sandbox
      strategy:
        runOnce:
          deploy:
            steps:
            - task: PowerShell@2
              inputs:
                targetType: 'inline'
                script: |
                  write-host ***************************
                  write-host 'Sandbox Job ${{ parameters.jobNum }} template'
                  write-host ***************************
            - script: echo "Deploying to Sandbox"
  ```

2. _Save and run_ your pipeline
3. Go to your pipeline's run details page, and authorize it to use the _Sandbox_ environment
4. Wait for your pipeline to run successfully
5. Click on _1 check passed_ and explore the logs of your Invoke Azure Function check
