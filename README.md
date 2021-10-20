# Backup azure devops repos to azure storage account using azure function app

Inspired by https://dev.to/ib1/a-missing-step-backup-azure-devops-repositories-16p7, 
I came with the idea to backup the azure devops repo using azure function app and save the backup data to azure storage account. 

I tested the idea using c# script in azure function. Because in this way I could code and test the code directly in azure function app.  
Then I rewrite the code using so called c# precompiled model for azure function app for better performance and easier code maintenance.

The steps to create such a function app could be summarized as following:

- step 1: create function app(c# script or precompiled)
- step 2: set application setting for 
    - connectionString: for azure storage account connection
    - token: azure devops access token
    - organization: azure devops organization name
- step 3: deploy the source code accordingly, c# script or c# precompiled, the code could be found at https://github.com/wuhaibo/BackupAzureDevopsReposByFunctionApp.git

The most of the code is from https://dev.to/ib1/a-missing-step-backup-azure-devops-repositories-16p7, I mainly changed 2 parts:
- use Azure.Storage.Blobs instead of Microsoft.WindowsAzure.Storage for azure storage account accessing, because Microsoft.WindowsAzure.Storage is out of date.
- instead of using downloaddata function from restsharp, I changed to use execute to log the response status code. 
