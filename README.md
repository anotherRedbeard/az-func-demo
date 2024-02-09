# az-func-demo

Repo to demo azure function app with one or more functions. See the function definitions below for each different function

## Requirements

To run this example, you need the following:

- .NET Core 8
- Visual Studio Code
- Azure Functions Core Tools, at least version 4.0.5455

## Deploy to Azure

1. Create a new or use an existing Azure Function in the Azure Portal
   1. I created an function app using the Linux Operating System. You can find steps on how to create an azure function in the portal [here](https://learn.microsoft.com/en-us/azure/azure-functions/functions-create-function-app-portal#create-a-function-app).
2. Deploy this project to the Azure Function you are using from step 1.
   1. You can do this locally by running the following Functions Core Tools command `func azure functionapp publish <funcation-app-name>` or you can use the [GitHub (GH) action](.github/workflows/main_brd-testfuncapp.yml) that has been included in this repo.  If you'd like to use the GH action, please follow the steps below:
      1. You will want to add the following variables and secrets to your github `Secrets and Variables` Setting under Security:  `var.AZURE_FUNCTIONAPP_NAME` and `secrets.AZURE_FUNCTIONAPP_CREDS`
      2. The variable `AZURE_FUNCTIONAPP_NAME` is the name of the function app you created above.
      3. The secret `AZURE_FUNCTIONAPP_CREDS` is an Role-Based Access Control (RBAC) with a client id and secret that you can use to have access to deploy your function app to Azure.  This is done by creating a new or using an existing service principal.
         1. You will need to create a new client_id and secret on an existing or new service principal.
            - Here is the command to create the new service principal

              ```# Bash script
                az ad sp create-for-rbac --name myServicePrincipalName1 --role reader --scopes /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myRG1
              ```

      4. This is the section of the GH action that is using these variables and secrets:

      ```yaml
        - name: 'Login via Azure CLI'
          uses: azure/login@v1
          with:
            creds: ${{ secrets.AZURE_FUNCTIONAPP_CREDS }}

        - name: 'Run Azure Functions Action'
          uses: Azure/functions-action@v1
          id: fa
          with:
            app-name: '${{ vars.AZURE_FUNCTIONAPP_NAME }}'
            slot-name: ''
            package: '${{ env.AZURE_FUNCTIONAPP_PACKAGE_PATH }}/src/output'
      ```

## Individual Functions

---

### `HttpAzDOApprovalTrigger.cs`

This basic example shows an Azure Function that can be called from the `approvals and checks` section of an AzDO pipeline.

You should use this Azure Function in an [`Invoke Azure Function` check](https://learn.microsoft.com/azure/devops/pipelines/process/approvals?#invoke-azure-function) configured in **Callback (Asynchronous)** mode. This mode is ideal when evaluating a condition takes a while, for example, due to making a REST call.

To successfully run this example, your pipeline needs to have a single job that using the environment where you placed the check referred to above.

#### Steps

The Azure Function goes through the following steps:

1. Confirms the receipt of the check payload
2. Sends a status update to Azure Pipelines that the check started
3. Uses `{AuthToken}` to make a call into Azure DevOps api to to retrieve the pipeline run's [`Build Log`](https://learn.microsoft.com/en-us/rest/api/azure/devops/build/builds/get-build-log?view=azure-devops-rest-7.1) entry
4. Checks if the `Build Log` contains a reference to all the templates that were passed into the header
5. Sends a status update with the result of the search
6. Sends a check decision to Azure Pipelines

### Configuration

Follow these instructions to use this example as an `Invoke Azure Function` check:

1. In your Azure Pipelines, create a new [`Environment`](https://learn.microsoft.com/azure/devops/pipelines/process/environments) called _Sandbox_ with no resources
2. Add a Check of type `Invoke Azure Function` to _Sandbox_ with the following configuration:
   1. _Azure function URL_: the URL of the Azure Function deployed in Step 1, for example, https://azurefunctionbasichandler.azurewebsites.net/api/MyBasicFunction. You can get this URL using _Copy Function Url_ in Visual Studio Code. Make sure you are using just the url with the function name, you will not need the `?code=<function_key>` part here.
   2. _Function key_: a secret used to access the Azure Function, for example, the value of the _code_ query parameter after you do _Copy Function Url_ in Visual Studio Code. This is where you would use the value of the key from the `?code=<function_key>`.
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

        Don't forget to add `"BuildId": "$(Build.BuildId)"` and `"RequiredTemplates":"sandbox-job.yaml"`, otherwise your Azure Function check will not work
   4. In the _Advanced_ section, choose _Callback_ as completion event. This makes the check run asynchronously
   5. In the _Control options_ section: 
      1. Set _Time between evaluations (minutes)_ to 0
      2. Set _Timeout (minutes)_ to 5, so that the build times out quickly

#### Run the Check

To see your Invoke Azure Function check in action, follow these steps:

1. Create a new YAML pipeline with the following code. Make sure to replace the `project_name` and `repo_name` with your values. In this example I'm using a template named `sandbox-job.yaml`, but you can use any name you want for your template.  Just make sure that you make the appropriate changes above in the headers section of the AzDO check:

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

---
