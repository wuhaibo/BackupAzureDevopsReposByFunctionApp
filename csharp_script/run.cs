
// 
using System;
using RestSharp;
using Azure.Storage.Blobs;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static async void Run(TimerInfo myTimer, ILogger log)
{
    
    log.LogInformation($"###########start: {DateTime.Now}###########");
     var connectionString = GetEnvironmentVariable("connectionString");
    var token = GetEnvironmentVariable("token");
    var organization = GetEnvironmentVariable("organization");
    var devopsURL = $"https://dev.azure.com/{organization}/";

    var version = "api-version=5.1";

    // make API request to get all projects
    string auth = "Basic " + Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", token)));
    var clientProjects = new RestClient($"{devopsURL}_apis/projects?{version}");
    var requestProjects = new RestRequest(Method.GET);
    requestProjects.AddHeader("Authorization", auth);
    var responseProjects = clientProjects.Execute(requestProjects);
    if (responseProjects.StatusCode != System.Net.HttpStatusCode.OK)
    {
        throw new Exception("API Request failed: " + responseProjects.StatusCode + " " + responseProjects.ErrorMessage);
    }
      // connect to Azure Storage
    BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
    BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("azuredevopsbackup");

    Projects projects = JsonConvert.DeserializeObject<Projects>(responseProjects.Content);
    foreach (Project project in projects.value)
    {
        log.LogInformation(project.name);
        
        // get repositories
        var clientRepos = new RestClient($"{devopsURL}{project.name}/_apis/git/repositories?{version}");
        var requestRepos = new RestRequest(Method.GET);
        requestRepos.AddHeader("Authorization", auth);
        var responseRepos = clientRepos.Execute(requestRepos);
        Repos repos = JsonConvert.DeserializeObject<Repos>(responseRepos.Content);

        
        foreach (Repo repo in repos.value)
        {
            
            log.LogInformation("Repo: " + repo.name);

            // get file mapping
            var clientItems = new RestClient($"{devopsURL}_apis/git/repositories/{repo.id}/items?recursionlevel=full&{version}");
            var requestItems = new RestRequest(Method.GET);
            requestItems.AddHeader("Authorization", auth);
            var responseItems = clientItems.Execute(requestItems);
            Items items = JsonConvert.DeserializeObject<Items>(responseItems.Content);

            log.LogInformation("Items count: " + items.count);

            if (items.count > 0)
            {
                // get files as zip
                var restClientBlob = new RestClient($"{devopsURL}_apis/git/repositories/{repo.id}/blobs?recursionlevel=full&{version}");
                var requestBlob = new RestRequest(Method.POST);
                requestBlob.AddJsonBody(items.value.Where(itm => itm.gitObjectType == "blob").Select(itm => itm.objectId).ToList());
                requestBlob.AddHeader("Authorization", auth);
                requestBlob.AddHeader("Accept", "application/zip");
				var response = restClientBlob.Execute(requestBlob);
				var status = (int)response.StatusCode;
				log.LogInformation($"response status code:{status}");
				
				if (status != 200)
				{
					log.LogError($"get data for {repo.name} failed.");
				}

				var zipfile = response.RawBytes;
                
                log.LogInformation($"upload: {zipfile.Length}");

                // upload blobs to Azure Storage
                string name = $"{project.name}_{repo.name}_blob.zip";
                log.LogInformation($"upload: {name}" );

                // Get a reference to a blob
                BlobClient blobClient = containerClient.GetBlobClient(name);
                await blobClient.DeleteIfExistsAsync();
                // Upload data from the local file
                await blobClient.UploadAsync(new BinaryData(zipfile), true);


                // upload file mapping
                string namejson = $"{project.name}_{repo.name}_tree.json";
                var blobjson = containerClient.GetBlobClient(namejson);
                await blobjson.DeleteIfExistsAsync();
                //blobjson.Properties.ContentType = "application/json";
                byte[] bytes = Encoding.UTF8.GetBytes(responseItems.Content);
                await blobjson.UploadAsync(new BinaryData(bytes));

                /* TODO:
                    * File mapping defines relationship between blob IDs and file names/paths.
                    * To reproduce a full file structure 
                    * 1. Recreate all folders for <item.isFolder>
                    * 2. Extract all other items to <item.path>
                    */

            }
        }


    }

}

public static string GetEnvironmentVariable(string name)
{
    return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
}
struct Project
{
    public string name ;
}
struct Projects
{
    public List<Project> value ;
}
struct Repo
{
    public string id  ;
    public string name  ;
}
struct Repos
{
    public List<Repo> value ;
}
struct Item
{
    public string objectId  ;
    public string gitObjectType  ;
    public string commitId  ;
    public string path  ;
    public bool isFolder ;
    public string url  ;
}
struct Items
{
    public int count ;
    public List<Item> value ;
}