using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;

namespace ZoieBot.Helpers
{
    public static class QnAHelper
    {
        private static readonly string knowledgebaseId = "402acf73-c1cc-405f-bac7-5da0ec67937c";
        private static readonly string qnamakerSubscriptionKey = "2910347697ff44c48ebc3463b30b2665";
        private static readonly string qnamakerUriString = "https://westus.api.cognitive.microsoft.com/qnamaker/v2.0";

        public static QnAResult GetAnswer(string question)
        {
            string responseString = string.Empty;
            UriBuilder uriBuilder = new UriBuilder($"{new Uri(qnamakerUriString)}/knowledgebases/{knowledgebaseId}/generateAnswer");
            string postBody = $"{{\"question\": \"{question}\"}}";

            //Send the POST request
            using (WebClient client = new WebClient())
            {
                client.Encoding = System.Text.Encoding.UTF8;

                //Add the subscription key header
                client.Headers.Add("Ocp-Apim-Subscription-Key", qnamakerSubscriptionKey);
                client.Headers.Add("Content-Type", "application/json");
                responseString = client.UploadString(uriBuilder.Uri, postBody);
            }

            QnAResult response;
            try
            {
                response = JsonConvert.DeserializeObject<QnAResult>(responseString);
            }
            catch
            {
                throw new Exception("Unable to deserialize QnA Maker response string.");
            }

            return response;
        }


        public class QnAResult
        {
            /// <summary>
            /// The top answer found in the QnA Service.
            /// </summary>
            [JsonProperty(PropertyName = "answers")]
            public List<QnAAnswer> Answers { get; set; }
        }

        public class QnAAnswer
        {
            /// <summary>
            /// The top answer found in the QnA Service.
            /// </summary>
            [JsonProperty(PropertyName = "answer")]
            public string Answer { get; set; }

            /// <summary>
            /// The top questions in KB that match user's question.
            /// </summary>
            [JsonProperty(PropertyName = "questions")]
            public List<string> Questions { get; set; }

            /// <summary>
            /// The score in range [0, 100] corresponding to the top answer found in the QnA Service.
            /// </summary>
            [JsonProperty(PropertyName = "score")]
            public double Score { get; set; }
        }
    }
}