using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;


namespace EdgeSerachEngineInstaller.Installer
{
    internal class InstallerDriver
    {

        #region Constructor

        public InstallerDriver()
        {

            Client = new HttpClient();

            // WebDriver server port
            Port = FindFreePort();

            // WebDriver server address
            BaseURL = $"http://localhost:{Port}";

            // Driver information to initialize the WebDriver process
            DriverInfo = InvokeWebDriver(Port);
        }

        #endregion Constructor

        #region Constants

        // WebDriver file name
        private const string EdgeDriverExeName = "msedgedriver.exe";

        #endregion Constants

        #region Public Members

        /// <summary>
        /// Hold the WebDriver server port
        /// </summary>
        public int Port { get; }

        #endregion Public Members

        #region Private Members

        /// <summary>
        /// Http handler
        /// </summary>
        private readonly HttpClient Client;
        /// <summary>
        /// Hold driver process information to initialize process
        /// </summary>
        private readonly ProcessStartInfo DriverInfo;
        /// <summary>
        /// Hold the local URL of the WebDriver server
        /// </summary>
        private readonly string BaseURL;
        /// <summary>
        /// Hold the session id
        /// </summary>
        private string SessionId;

        #endregion Private Members

        #region Helper Methods

        /// <summary>
        /// Helper method to create url
        /// </summary>
        /// <param name="relativeUrl"></param>
        /// <returns></returns>
        private string CreateURL(string relativeUrl)
        {
            return $"{BaseURL}/session/{SessionId}/{relativeUrl}";
        }
        /// <summary>
        /// Convert content to JSON and post it using <seealso cref="Client"/>
        /// </summary>
        /// <param name="url"></param>
        /// <param name="jsonContent"></param>
        /// <returns></returns>
        private async Task<HttpResponseMessage> PostAsync(string url, object jsonContent)
        {
            var stringContent = new StringContent(JsonConvert.SerializeObject(jsonContent), Encoding.UTF8, "application/json");
            return await Client.PostAsync(url, stringContent);
        }
        /// <summary>
        /// Send delete HTTP command
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private async Task DeleteAsync(string url)
        {
            await Client.DeleteAsync(url);
        }
        /// <summary>
        /// Get available port for the WebDriver server.
        /// </summary>
        /// <returns></returns>
        private static int FindFreePort()
        {
            int listeningPort = 0;
            Socket portSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                IPEndPoint socketEndPoint = new IPEndPoint(IPAddress.Any, 0);
                portSocket.Bind(socketEndPoint);
                socketEndPoint = (IPEndPoint)portSocket.LocalEndPoint;
                listeningPort = socketEndPoint.Port;
            }
            finally
            {
                portSocket.Close();
            }

            return listeningPort;
        }
        /// <summary>
        /// Start WebDriver server.
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        private static ProcessStartInfo InvokeWebDriver(int port)
        {
            var currentDir = Directory.GetCurrentDirectory();

            // Specify the name of the embedded executable
            string embeddedExecutableName = Path.Combine(currentDir, "Resources", EdgeDriverExeName);

            // Execute the extracted executable
            return new ProcessStartInfo
            {
                FileName = embeddedExecutableName,
                Arguments = $" --port={port}",
                CreateNoWindow = true, // This hides the window
                UseShellExecute = false // This is required when CreateNoWindow is true
            };
        }
        /// <summary>
        /// Read the content fromt the HTTP response.
        /// </summary>
        /// <param name="httpResponse"></param>
        /// <returns></returns>
        private static async Task<string> ReadHttpResponse(HttpResponseMessage httpResponse)
        {
            return await httpResponse.Content.ReadAsStringAsync();
        }
        /// <summary>
        /// Deserialize JSON to <paramref name="T"/> object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        private static T DeserializeObject<T>(string data)
        {
            return JsonConvert.DeserializeObject<T>(data);
        }
        /// <summary>
        /// Create new session and return the new session ID.
        /// </summary>
        /// <param name="baseURL"></param>
        /// <param name="defaultUserProfileDir"></param>
        /// <returns></returns>
        private async Task<string> CreateSessionAsync(string baseURL, string defaultUserProfileDir)
        {
            // Create Edge-specific capabilities
            var edgeCapabilities = new JObject(
              new JProperty("capabilities", new JObject(
                  new JProperty("browserName", "MicrosoftEdge"),
                  new JProperty("ms:edgeOptions", new JObject(
                      new JProperty("useChromium", true),
                      new JProperty("args", new JObject(
                           new JProperty("user-data-dir", defaultUserProfileDir),
                           new JProperty("profile-directory", "Default")
                      ))
                  ))
              ))
            );

            // Create a new session
            var response = await PostAsync($"{baseURL}/session", edgeCapabilities);
            var jsonData = await ReadHttpResponse(response);

            // Deserialize the response to an object
            var deserializedData = DeserializeObject<Response<ResponseContent>>(jsonData);
            return deserializedData.value.sessionId;
        }

        #endregion Helper Methods

        #region Public Methods

        #region Public Methods->DOM / Elements

        /// <summary>
        /// Set a value by WEbDriver server element key.
        /// </summary>
        /// <param name="elementKey"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task SetElementValueAsync(string elementKey, string value)
        {
            // set one by one
            foreach (char c in value)
            {
                var inputRequest = new
                {
                    text = c.ToString()
                };
                await PostAsync(CreateURL($"element/{elementKey}/value"), inputRequest);
            }
        }
        /// <summary>
        /// Get WebServer element key by DOM element id.
        /// </summary>
        /// <param name="elementId"></param>
        /// <returns></returns>
        public async Task<string> GetElementKeyByIdAsync(string elementId)
        {
            var jsonInputContent = new
            {
                @using = "css selector",
                value = $"#{elementId}"
            };
            var response = await PostAsync(CreateURL($"element"), jsonInputContent);
            var jsonData = await ReadHttpResponse(response);
            return DeserializeObject<Response<Dictionary<string, string>>>(jsonData).value.First().Value;
        }
        /// <summary>
        /// Get the first element key by DOM element css condition
        /// </summary>
        /// <param name="elementKey"></param>
        /// <param name="attribute"></param>
        /// <returns></returns>
        public async Task<string> GetElementKeyByCssAsync(string cssCondition, string parentElementId = null)
        {
            string url;
            if (parentElementId != null)
            {
                url = CreateURL($"element/{parentElementId}/element");
            }
            else
            {
                url = CreateURL("element");
            }

            var jsonInputContent = new
            {
                @using = "css selector",
                value = $"{cssCondition}"
            };
            var response = await PostAsync(url, jsonInputContent);
            var jsonData = await ReadHttpResponse(response);
            return DeserializeObject<Response<Dictionary<string, string>>>(jsonData).value.First().Value;
        }
        /// <summary>
        /// Get collection of element keys by DOM element css condition
        /// </summary>
        /// <param name="elementKey"></param>
        /// <param name="attribute"></param>
        /// <returns></returns>
        public async Task<IEnumerable<string>> GetElementsByCssAsync(string elementType, string parentElementId = null)
        {
            string url;
            if (parentElementId != null)
            {
                url = CreateURL($"element/{parentElementId}/elements");
            }
            else
            {
                url = CreateURL("elements");
            }

            var jsonInputContent = new
            {
                @using = "css selector",
                value = $"{elementType}"
            };
            var response = await PostAsync(url, jsonInputContent);
            var jsonData = await ReadHttpResponse(response);
            return DeserializeObject<Response<IEnumerable<Dictionary<string, string>>>>(jsonData).value.Select(value => value.Values.First().ToString());
        }
        /// <summary>
        /// Perform a click to an element by WebDriver element key
        /// </summary>
        /// <param name="elementKey"></param>
        /// <returns></returns>
        public async Task PerformElementClickAsync(string elementKey)
        {
            // Click the button
            var emptyJson = new { };
            await PostAsync(CreateURL($"element/{elementKey}/click"), emptyJson);
        }

        #endregion Public Methods->DOM / Elements

        #region Public Methods->Scripts

        /// <summary>
        /// Execute script on browser context
        /// </summary>
        /// <param name="script"></param>
        /// <returns></returns>
        public async Task ScriptAsync(string script)
        {
            var jsonInputContent = new
            {
                script = script,
                args = new object[0]
            };

            await PostAsync(CreateURL("execute/sync"), jsonInputContent);
        }
        /// <summary>
        /// Execute script on browser context and return the result
        /// </summary>
        /// <param name="script"></param>
        /// <returns></returns>
        public async Task<string> ScriptAndGetAsync(string script)
        {
            var jsonInputContent = new
            {
                script = script,
                args = new object[0]
            };

            var response = await PostAsync(CreateURL("execute/sync"), jsonInputContent);
            var jsonData = await ReadHttpResponse(response);
            return DeserializeObject<Response<string>>(jsonData).value;
        }

        #endregion Public Methods->Scripts

        #region Public Methods->Navigation

        /// <summary>
        /// Navigate to <paramref name="relativeUrl"/>
        /// </summary>
        /// <param name="relativeUrl"></param>
        /// <returns></returns>
        public async Task SetURLAsync(string relativeUrl)
        {
            var jsonInputContent = new
            {
                url = relativeUrl
            };
            await PostAsync(CreateURL($"url"), jsonInputContent);
        }

        #endregion Public Methods->Navigation

        #region Public Methods->Connectivity

        /// <summary>
        /// Delete current session and close browser
        /// </summary>
        /// <returns></returns>
        public async Task CloseSessionAsync()
        {
            await DeleteAsync($"{BaseURL}/session/{SessionId}");
        }
        /// <summary>
        /// Start WebDriver server and create new session
        /// </summary>
        /// <param name="defaulrUserProfileDir"></param>
        /// <returns></returns>
        public async Task StartDriverAsync(string defaulrUserProfileDir)
        {
            Process.Start(DriverInfo);
            SessionId = await CreateSessionAsync(BaseURL, defaulrUserProfileDir);
        }
        /// <summary>
        /// Shutdown the WebDriver
        /// </summary>
        /// <returns></returns>
        public async Task StopDriverAsync()
        {
            await DeleteAsync($"{BaseURL}/shutdown");

            // double-check driver shutdown
            // Find the msedgedriver process
            var edgeDriverProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(EdgeDriverExeName));

            // Check if the process is running
            if (edgeDriverProcesses.Length > 0)
            {
                // Terminate the process
                edgeDriverProcesses[0].Kill();
            }
            else
            {
            }
        }

        #endregion Public Methods->Connectivity

        #endregion Public Methods

        private class Response<T>
        {
            public T value { get; set; }
        }
        private class ResponseContent
        {
            public string sessionId { get; set; }
        }

    }
}
