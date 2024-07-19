using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ApiClientApp
{
    public partial class Form1 : Form
    {
        private static readonly HttpClient client = new HttpClient();
        private TextBox textBoxSearch;
        private TextBox textBoxResult;
        private TextBox textBoxPostData;
        private Button buttonFetch;
        private ComboBox comboBoxMethod;
        private TextBox textBoxLog;
        private TextBox textBoxToken;

        private string username = "admin";
        private string password = "Admin!23";
        private string baseUrl = "http://127.0.0.1:8083/Values";
        private BearerToken token;

        public Form1()
        {
            InitializeCustomComponents();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiClientApp/1.0");
        }

        private void InitializeCustomComponents()
        {
            this.Size = new System.Drawing.Size(350, 650);

            textBoxSearch = new TextBox
            {
                Location = new System.Drawing.Point(15, 15),
                Width = 300,
                PlaceholderText = "Enter ID to search"
            };
            Controls.Add(textBoxSearch);

            comboBoxMethod = new ComboBox
            {
                Location = new System.Drawing.Point(15, 45),
                Width = 300,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboBoxMethod.Items.AddRange(new string[] { "GET", "POST", "PUT", "DELETE", "TRENDS", "DATABASE" });
            comboBoxMethod.SelectedIndex = 0;
            Controls.Add(comboBoxMethod);

            textBoxPostData = new TextBox
            {
                Location = new System.Drawing.Point(15, 75),
                Width = 300,
                Height = 60,
                Multiline = true,
                PlaceholderText = "POST/PUT Data"
            };
            Controls.Add(textBoxPostData);

            textBoxResult = new TextBox
            {
                Location = new System.Drawing.Point(15, 145),
                Width = 300,
                Height = 200,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            Controls.Add(textBoxResult);

            buttonFetch = new Button
            {
                Text = "Send Request",
                Location = new System.Drawing.Point(15, 355)
            };
            buttonFetch.Click += new EventHandler(ButtonFetch_Click);
            Controls.Add(buttonFetch);

            textBoxLog = new TextBox
            {
                Location = new System.Drawing.Point(15, 385),
                Width = 300,
                Height = 100,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            Controls.Add(textBoxLog);

            textBoxToken = new TextBox
            {
                Location = new System.Drawing.Point(15, 500),
                Width = 300,
                ReadOnly = true,
                PlaceholderText = "Bearer Token"
            };
            Controls.Add(textBoxToken);
        }

        private void Log(string message)
        {
            textBoxLog.AppendText(message + Environment.NewLine);
        }

        private async Task ObtainBearerTokenAsync()
        {
            Uri tokenUri = new Uri("http://127.0.0.1:8083/GetToken");
            try
            {
                string credentials = $"{username}:{password}";
                string base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
                Log($"Base64 Encoded Credentials: {base64Credentials}");

                var handler = new HttpClientHandler();
                using (var client = new HttpClient(handler))
                {
                    var credentialsContent = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "password"),
                        new KeyValuePair<string, string>("username", username),
                        new KeyValuePair<string, string>("password", password)
                    });

                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiClientApp/1.0");
                    client.DefaultRequestHeaders.Connection.Add("keep-alive");
                    client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                    client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
                    client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
                    client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

                    string requestBody = await credentialsContent.ReadAsStringAsync();
                    Log($"Request URL: {tokenUri}");
                    Log($"Request Headers: {client.DefaultRequestHeaders}");
                    Log($"Request Body: {requestBody}");

                    HttpResponseMessage response = await client.PostAsync(tokenUri, credentialsContent);
                    string responseContent = await response.Content.ReadAsStringAsync();

                    Log($"Response Status Code: {response.StatusCode}");
                    Log($"Response Content: {responseContent}");

                    if (response.IsSuccessStatusCode)
                    {
                        token = JsonConvert.DeserializeObject<BearerToken>(responseContent);
                        token.HasExpired = false;
                        Log($"Token obtained: {token.AccessToken}");
                        textBoxToken.Text = token.AccessToken;
                    }
                    else
                    {
                        MessageBox.Show($"Failed to obtain access token: {response.StatusCode}. Response: {responseContent}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error obtaining token: {ex.Message}");
                MessageBox.Show($"Error obtaining token: {ex.Message}");
            }
        }

        private async void ButtonFetch_Click(object sender, EventArgs e)
        {
            try
            {
                if (token == null || token.HasExpired)
                {
                    await ObtainBearerTokenAsync();
                }

                if (token == null || string.IsNullOrEmpty(token.AccessToken))
                {
                    Log("Token is null or has expired. Cannot proceed with the request.");
                    MessageBox.Show("Failed to obtain a valid token.");
                    return;
                }

                string searchQuery = textBoxSearch.Text ?? string.Empty;
                string apiUrl = $"{baseUrl}/{Uri.EscapeDataString(searchQuery)}";

                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiClientApp/1.0");

                Log($"Making API request to: {apiUrl}");
                Log($"Token used: {token.AccessToken}");

                string response = string.Empty;
                string method = comboBoxMethod.SelectedItem?.ToString() ?? "GET";

                switch (method)
                {
                    case "GET":
                        response = await FetchDataFromDatabase();
                        break;
                    case "POST":
                        response = await InsertDataToDatabase(textBoxPostData?.Text ?? string.Empty);
                        break;
                    case "PUT":
                        response = await UpdateDataInDatabase(textBoxPostData?.Text ?? string.Empty);
                        break;
                    case "DELETE":
                        response = await DeleteDataFromDatabase(textBoxPostData?.Text ?? string.Empty);
                        break;
                    case "TRENDS":
                        response = await GetTrendsAsync();
                        break;
                }

                textBoxResult.Text = ParseJson(response);
            }
            catch (HttpRequestException httpEx)
            {
                Log($"HTTP Error: {httpEx.Message}");
                MessageBox.Show($"HTTP Error: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                Log($"Error fetching data: {ex.Message}");
                MessageBox.Show($"Error fetching data: {ex.Message}");
            }
        }

        private async Task<string> FetchDataFromDatabase()
        {
            string connectionString = "Server=TERHI_BERRY\\SQLEXPRESS;Database=APIdb;Trusted_Connection=True;";
            string query = "SELECT * FROM APIdata"; 

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (reader.HasRows)
                            {
                                var dataTable = new DataTable();
                                dataTable.Load(reader);
                                return DataTableToJson(dataTable);
                            }
                            else
                            {
                                return "No data found.";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error fetching data from database: {ex.Message}");
                return $"Error fetching data from database: {ex.Message}";
            }
        }

        private async Task<string> InsertDataToDatabase(string sqlCommand)
        {
            string connectionString = "Server=TERHI_BERRY\\SQLEXPRESS;Database=APIdb;Trusted_Connection=True;";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(sqlCommand, connection))
                    {
                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        return $"{rowsAffected} rows inserted.";
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error inserting data to database: {ex.Message}");
                return $"Error inserting data to database: {ex.Message}";
            }
        }

        private async Task<string> UpdateDataInDatabase(string sqlCommand)
        {
            string connectionString = "Server=TERHI_BERRY\\SQLEXPRESS;Database=APIdb;Trusted_Connection=True;";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(sqlCommand, connection))
                    {
                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        return $"{rowsAffected} rows updated.";
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating data in database: {ex.Message}");
                return $"Error updating data in database: {ex.Message}";
            }
        }

        private async Task<string> DeleteDataFromDatabase(string sqlCommand)
        {
            string connectionString = "Server=TERHI_BERRY\\SQLEXPRESS;Database=APIdb;Trusted_Connection=True;";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(sqlCommand, connection))
                    {
                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        return $"{rowsAffected} rows deleted.";
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error deleting data from database: {ex.Message}");
                return $"Error deleting data from database: {ex.Message}";
            }
        }

        private async Task<string> GetTrendsAsync()
        {
            string connectionString = "Server=TERHI_BERRY\\SQLEXPRESS;Database=APIdb;Trusted_Connection=True;";
            string query = "SELECT TrendColumn FROM TrendTable"; 

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (reader.HasRows)
                            {
                                var dataTable = new DataTable();
                                dataTable.Load(reader);
                                return DataTableToJson(dataTable); 
                            }
                            else
                            {
                                return "No data found.";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error fetching trends from database: {ex.Message}");
                return $"Error fetching trends from database: {ex.Message}";
            }
        }

        private string DataTableToJson(DataTable table)
        {
            return JsonConvert.SerializeObject(table, Formatting.Indented);
        }

        private string ParseJson(string jsonData)
        {
            try
            {
                dynamic parsedJson = JsonConvert.DeserializeObject<dynamic>(jsonData);
                return JsonConvert.SerializeObject(parsedJson, Formatting.Indented);
            }
            catch (JsonReaderException)
            {
                return jsonData;
            }
        }
    }

    public class BearerToken
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonIgnore]
        public bool HasExpired { get; set; }
    }
}
