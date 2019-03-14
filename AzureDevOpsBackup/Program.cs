using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Extensions;
namespace AzureDevOpsBackup
{
    struct Project
    {
        public string name;
    }
    struct Projects
    {
        public List<Project> value;
    }
    struct Repo
    {
        public string id;
        public string name;
    }
    struct Repos
    {
        public List<Repo> value;
    }
    struct Item
    {
        public string objectId;
        public string gitObjectType;
        public string commitId;
        public string path;
        public bool isFolder;
        public string url;
    }
    struct Items
    {
        public int count;
        public List<Item> value;
    }
    class Program
    {
        static void Main(string[] args)
        {
            string[] requiredArgs = { "--token", "--organization", "--outdir" };
            if (args.Intersect(requiredArgs).Count() == 3)
            {
                const string version = "api-version=5.1-preview.1";
                string auth = "Basic " + Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", args[Array.IndexOf(args, "--token") + 1])));
                string baseURL = "https://dev.azure.com/" + args[Array.IndexOf(args, "--organization") + 1] + "/";
                string outDir = args[Array.IndexOf(args, "--outdir") + 1] + "\\";
                var clientProjects = new RestClient(baseURL + "_apis/projects?" + version);
                var requestProjects = new RestRequest(Method.GET);
                requestProjects.AddHeader("Authorization", auth);
                IRestResponse responseProjects = clientProjects.Execute(requestProjects);
                Projects projects = JsonConvert.DeserializeObject<Projects>(responseProjects.Content);
                foreach (Project project in projects.value)
                {
                    Console.WriteLine(project.name);
                    var clientRepos = new RestClient(baseURL + project.name + "/_apis/git/repositories?" + version);
                    var requestRepos = new RestRequest(Method.GET);
                    requestRepos.AddHeader("Authorization", auth);
                    IRestResponse responseRepos = clientRepos.Execute(requestRepos);
                    Repos repos = JsonConvert.DeserializeObject<Repos>(responseRepos.Content);
                    foreach (Repo repo in repos.value)
                    {
                        Console.Write("\n\t" + repo.name);
                        var clientItems = new RestClient(baseURL + "_apis/git/repositories/" + repo.id + "/items?recursionlevel=full&" + version);
                        var requestItems = new RestRequest(Method.GET);
                        requestItems.AddHeader("Authorization", auth);
                        IRestResponse responseItems = clientItems.Execute(requestItems);
                        Items items = JsonConvert.DeserializeObject<Items>(responseItems.Content);
                        Console.Write(" - " + items.count + "\n");
                        if (items.count > 0)
                        {
                            var clientBlob = new RestClient(baseURL + "_apis/git/repositories/" + repo.id + "/blobs?" + version);
                            var requestBlob = new RestRequest(Method.POST);
                            requestBlob.AddJsonBody(items.value.Where(itm => itm.gitObjectType == "blob").Select(itm => itm.objectId).ToList());
                            requestBlob.AddHeader("Authorization", auth);
                            requestBlob.AddHeader("Accept", "application/zip");
                            clientBlob.DownloadData(requestBlob).SaveAs(outDir + project.name + "_" + repo.name + "_blob.zip");
                            File.WriteAllText(outDir + project.name + "_" + repo.name + "_tree.json", responseItems.Content);
                            if (Array.Exists(args, argument => argument == "--unzip"))
                            {
                                if (Directory.Exists(outDir + project.name + "_" + repo.name)) Directory.Delete(outDir + project.name + "_" + repo.name, true);
                                Directory.CreateDirectory(outDir + project.name + "_" + repo.name);
                                ZipArchive archive = ZipFile.OpenRead(outDir + project.name + "_" + repo.name + "_blob.zip");
                                foreach (Item item in items.value)
                                    if (item.isFolder) Directory.CreateDirectory(outDir + project.name + "_" + repo.name + item.path);
                                    else archive.GetEntry(item.objectId).ExtractToFile(outDir + project.name + "_" + repo.name + item.path, true);
                            }
                        }
                    }
                }
            }
        }
    }
}