using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HelperFx
{
    public static class KeyPhrasesToPlainList
    {
        [FunctionName("KeyPhrasesToPlainList")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var phrases = await req.ReadAsStringAsync();
            var phrasesArray = JsonConvert.DeserializeObject<string[]>(phrases);
            var joinedPhrases = string.Join(", ", phrasesArray);

            return new OkObjectResult(joinedPhrases);
        }
    }
}
