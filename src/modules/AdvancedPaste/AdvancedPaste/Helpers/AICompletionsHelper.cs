// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ClientModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core.Pipeline;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using OpenAI;
using OpenAI.Chat;
using Windows.Security.Credentials;

namespace AdvancedPaste.Helpers
{
    public class AICompletionsHelper
    {
        // Return Response and Status code from the request.
        public struct AICompletionsResponse
        {
            public AICompletionsResponse(string response, int apiRequestStatus)
            {
                Response = response;
                ApiRequestStatus = apiRequestStatus;
            }

            public string Response { get; }

            public int ApiRequestStatus { get; }
        }

        private string _openAIKey;

        private string _modelName = "gpt-3.5-turbo-instruct";

        public bool IsAIEnabled => !string.IsNullOrEmpty(this._openAIKey);

        public AICompletionsHelper()
        {
            this._openAIKey = LoadOpenAIKey();
        }

        public void SetOpenAIKey(string openAIKey)
        {
            this._openAIKey = openAIKey;
        }

        public string GetKey()
        {
            return _openAIKey;
        }

        public static string LoadOpenAIKey()
        {
            PasswordVault vault = new PasswordVault();

            try
            {
                PasswordCredential cred = vault.Retrieve("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_OpenAIKey");
                if (cred is not null)
                {
                    return cred.Password.ToString();
                }
            }
            catch (Exception)
            {
            }

            return string.Empty;
        }

        private ClientResult<ChatCompletion> GetAICompletion(string systemInstructions, string userMessage)
        {
            var client = new OpenAIClient("ollama", new OpenAIClientOptions()
                {
                    Endpoint = new Uri("http://localhost:11434/v1/"),
                });

            var response = client.GetChatClient("phi3").CompleteChat(systemInstructions + "\n\n" + userMessage);

            if (response.Value.FinishReason.ToString() == "length")
            {
                Console.WriteLine("Cut off due to length constraints");
            }

            return response;
        }

        public AICompletionsResponse AIFormatString(string inputInstructions, string inputString)
        {
            string systemInstructions = $@"You are tasked with reformatting user's clipboard data. Use the user's instructions, and the content of their clipboard below to edit their clipboard content as they have requested it.

Do not output anything else besides the reformatted clipboard content.";

            string userMessage = $@"User instructions:
{inputInstructions}

Clipboard Content:
{inputString}

Output:
";

            string aiResponse = null;
            ClientResult<ChatCompletion> rawAIResponse = null;
            int apiRequestStatus = (int)HttpStatusCode.OK;
            try
            {
                rawAIResponse = this.GetAICompletion(systemInstructions, userMessage);
                aiResponse = rawAIResponse.Value.ToString();

                int promptTokens = rawAIResponse.Value.Usage.InputTokens;
                int completionTokens = rawAIResponse.Value.Usage.OutputTokens;
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteGenerateCustomFormatEvent(promptTokens, completionTokens, _modelName));
            }
            catch (Azure.RequestFailedException error)
            {
                Logger.LogError("GetAICompletion failed", error);
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteGenerateCustomErrorEvent(error.Message));
                apiRequestStatus = error.Status;
            }
            catch (Exception error)
            {
                Logger.LogError("GetAICompletion failed", error);
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteGenerateCustomErrorEvent(error.Message));
                apiRequestStatus = -1;
            }

            return new AICompletionsResponse(aiResponse, apiRequestStatus);
        }
    }
}
