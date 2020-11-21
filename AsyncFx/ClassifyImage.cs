using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;

namespace AsyncFx
{
    public static class ClassifyImage
    {
        [FunctionName("ClassifyImage")]
        public async static Task Run([BlobTrigger("uploaded/{name}", Connection = "intergationasync_STORAGE")]Stream myBlob, string name, ILogger log)
        {
            log.LogInformation($"Analyzing uploaded image {name} for adult content...");
            log.LogInformation($"SubscriptionKey: {System.Environment.GetEnvironmentVariable("SubscriptionKey")}");
            log.LogInformation($"VisionEndpoint: {System.Environment.GetEnvironmentVariable("VisionEndpoint")}");
            log.LogInformation($"AzureWebJobsStorage: {System.Environment.GetEnvironmentVariable("AzureWebJobsStorage")}");

            var result = await AnalyzeImageAsync(myBlob, log);

            log.LogInformation("Is Adult: " + result.adult.isAdultContent.ToString());
            log.LogInformation("Adult Score: " + result.adult.adultScore.ToString());
            log.LogInformation("Is Racy: " + result.adult.isRacyContent.ToString());
            log.LogInformation("Racy Score: " + result.adult.racyScore.ToString());

            if (result.adult.isAdultContent || result.adult.isRacyContent)
            {
                await StoreBlobWithMetadata(myBlob, "rejected", name, result, log);
            }
            else
            {
                await StoreBlobWithMetadata(myBlob, "accepted", name, result, log);
            }        
        }

        private async static Task<ImageAnalysisInfo> AnalyzeImageAsync(Stream blob, ILogger log)
        {
            var client = new HttpClient();

            var key = System.Environment.GetEnvironmentVariable("SubscriptionKey");
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);

            var payload = new StreamContent(blob);
            payload.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/octet-stream");

            var endpoint = System.Environment.GetEnvironmentVariable("VisionEndpoint");
            var results = await client.PostAsync(endpoint + "vision/v2.0/analyze?visualFeatures=Adult", payload);
            var result = await results.Content.ReadAsAsync<ImageAnalysisInfo>();

            return result;
        }

        private async static Task StoreBlobWithMetadata(Stream image, string containerName, string blobName, ImageAnalysisInfo info, ILogger log)
        {
            log.LogInformation($"Writing blob and metadata to {containerName} container...");

            var connection = System.Environment.GetEnvironmentVariable("AzureWebJobsStorage").ToString();
            var account = CloudStorageAccount.Parse(connection);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(containerName);

            try
            {
                var blob = container.GetBlockBlobReference(blobName);

                if (blob != null)
                {
                    await blob.UploadFromStreamAsync(image);

                    await blob.FetchAttributesAsync();

                    blob.Metadata["isAdultContent"] = info.adult.isAdultContent.ToString();
                    blob.Metadata["adultScore"] = info.adult.adultScore.ToString("P0").Replace(" ","");
                    blob.Metadata["isRacyContent"] = info.adult.isRacyContent.ToString();
                    blob.Metadata["racyScore"] = info.adult.racyScore.ToString("P0").Replace(" ","");

                    await blob.SetMetadataAsync();
                }
            }
            catch (Exception ex)
            {
                log.LogInformation(ex.Message);
                throw;
            }
        }
    }

    public class ImageAnalysisInfo
    {
        public Adult adult { get; set; }
        public string requestId { get; set; }
    }

    public class Adult
    {
        public bool isAdultContent { get; set; }
        public bool isRacyContent { get; set; }
        public float adultScore { get; set; }
        public float racyScore { get; set; }
    }    
}
