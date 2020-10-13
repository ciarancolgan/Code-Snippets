using Octopus.Client;
using Octopus.Client.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace GenesysOctopusClient
{
    /// <summary>
    /// Latest version of octopus.client api does not support build deployment because Allstate octopus has lower version - 2018. 
    /// Hence to deploy the build using this older version of the Octopus Client we have created separate project with a lower .Net framework version.
    /// This version of the Client does not support .NET Core, so we cant use the Inventory API to house this.
    /// TODO: WHEN ALLSTATE UPGRADES THEIR OCTOPUS VERSION TO 2019, CAN MOVE THIS CODE INTO THE INVENTORY.API PROJECT.
    /// </summary>
    public class OctopusDeployment
    {
        private readonly string _octopusUrl;
        private readonly string _octopusApi;

        public OctopusDeployment(string octopusUrl, string octopusApi)
        {
            _octopusUrl = octopusUrl;
            _octopusApi = octopusApi;
        }

        public async Task<Tuple<bool, string>> CreateDeployment(string octopusProductionProjectId, string octopusProductionProjectName, string octopusTestEnvironmentId, 
            string octopusTestEnvironmentName, string productionBuildVersion)
        {
            try
            {
                ServicePointManager.SecurityProtocol =
                   SecurityProtocolType.Tls12 |
                   SecurityProtocolType.Tls11 |
                   SecurityProtocolType.Tls;

                OctopusServerEndpoint endPoint = new OctopusServerEndpoint(_octopusUrl, _octopusApi);

                var client = await OctopusAsyncClient.Create(endPoint);

                var octopusTestEnvironmentToSyncTo = await client.Repository.Environments.Get(octopusTestEnvironmentId);

                // Find the version of the Release that was deployed to Production
                var octopusProductionProject = await client.Repository.Projects.Get(octopusProductionProjectId);
                var octopusProductionReleases = (await client.Repository.Projects.GetReleases(octopusProductionProject, searchByVersion: productionBuildVersion)).Items.ToList();

                // It is possible that there is more than 1 release with the Production version in it. 
                // It is also possible that some of these may not have been deployed correctly - ie they may not go through all
                // environments /stages in the Lifecycle that are required as 'gates' to allow this Release to be deployed to our target environment. 
                // If that is the case, the deployment of this Release will fail, so retry the others til we find one that works or return ultimate failure.
                var errorMessages = new List<string>();

                foreach (var productionRelease in octopusProductionReleases)
                {
                    // Try and deploy this release to the environment in question. 
                    var octopusNewDeployment = new DeploymentResource
                    {
                        ReleaseId = productionRelease.Id,
                        ProjectId = octopusProductionProject.Id,
                        EnvironmentId = octopusTestEnvironmentToSyncTo.Id
                    };

                    try
                    {
                        var deployment = await client.Repository.Deployments.Create(octopusNewDeployment);
                        return new Tuple<bool, string>(true, deployment.Id);
                    }
                    catch (Exception e)
                    {
                        errorMessages.Add(e.Message);
                        continue;
                    }
                }

                if (errorMessages.Any())
                {
                    var errorDisplayMessage =
                        $"Deployment to resync Environment: {octopusTestEnvironmentName} with Production Build Version: {productionBuildVersion} did not succeed. \r\nError(s):\r\n"
                        + string.Join(":::", errorMessages);

                    return new Tuple<bool, string>(false, errorDisplayMessage);
                }
                else if (!octopusProductionReleases.Any())
                {
                    return new Tuple<bool, string>(false,
                        $"Deployment to resync Environment: {octopusTestEnvironmentName} did not succeed as no Releases which contain a Deployment with the Production Build Version: " +
                        $"{productionBuildVersion} can be found in Octopus for Project: {octopusProductionProjectName}");
                }
                else
                {
                    return new Tuple<bool, string>(false,
                        $"Deployment to resync Environment: {octopusTestEnvironmentName} with Production Build Version: {productionBuildVersion} did not succeed. Please contact an Administrator.");
                }
            }
            catch (Exception ex)
            {
                return new Tuple<bool, string>(false, ex.Message);
            }
        }
    }
}
