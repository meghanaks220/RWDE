using Microsoft.VisualBasic.FileIO;
using System;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinFormsApp7
{
    public partial class Form1 : Form
    {
        private readonly string connectionString = "Data Source=SOFTSELL\\MSSQLSERVER01;Initial Catalog=RWDE;Integrated Security=True";

        public Form1()
        {
            InitializeComponent();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            // You can leave this event handler empty or use it for any additional processing if needed.
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
                {
                    folderBrowserDialog.Description = "Select a folder containing CSV files.";

                    if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                    {
                        string selectedFolderPath = folderBrowserDialog.SelectedPath;
                        textBox1.Text = selectedFolderPath; // Set the selected folder path in the text box
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                string selectedFolderPath = textBox1.Text;

                // Get all CSV files in the selected folder
                string[] csvFiles = Directory.GetFiles(selectedFolderPath, "*.csv");

                if (csvFiles.Length == 0)
                {
                    MessageBox.Show("No CSV files found in the selected folder.");
                    return;
                }

                // Process each CSV file
                foreach (string csvFilePath in csvFiles)
                {
                    ProcessCsvFile(csvFilePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void ProcessCsvFile(string csvFilePath)
        {
            try
            {
                string tableName = Path.GetFileNameWithoutExtension(csvFilePath);
                string[] headers = GetCsvHeaders(csvFilePath);

                CreateTableInDatabase(tableName, headers);

                // Store data in the database
                StoreDataInDatabase(csvFilePath, tableName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing CSV file '{csvFilePath}': {ex.Message}");
            }
        }

        private string[] GetCsvHeaders(string csvFilePath)
        {
            using (StreamReader reader = new StreamReader(csvFilePath))
            {
                // Read the header line and split it into an array of headers
                string headerLine = reader.ReadLine();
                return headerLine.Split(',');
            }
        }

        private void CreateTableInDatabase(string tableName, string[] headers)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Clean up the table name
                    string cleanedTableName = CleanUpTableName(tableName);

                    // Create a table if not exists
                    StringBuilder createTableQuery = new StringBuilder($"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{cleanedTableName}') ");
                    createTableQuery.Append($"CREATE TABLE {cleanedTableName} (ID INT PRIMARY KEY IDENTITY(1,1), ");

                    int idCounter = 1;
                    foreach (string header in headers)
                    {
                        // Clean up column names and use NVARCHAR(MAX) as default
                        createTableQuery.Append($"{CleanUpColumnName(header)}{idCounter++} NVARCHAR(MAX), ");
                    }

                    // Remove the trailing comma and space
                    createTableQuery.Length -= 2;

                    createTableQuery.Append(")");

                    using (SqlCommand command = new SqlCommand(createTableQuery.ToString(), connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating table '{tableName}': {ex.Message}");
            }
        }

        private string CleanUpTableName(string tableName)
        {
            // Remove invalid characters from the table name
            return new string(tableName.Where(c => Char.IsLetterOrDigit(c) || c == '_').ToArray());
        }

        private string CleanUpColumnName(string columnName)
        {
            // Remove invalid characters from the column name
            return new string(columnName.Where(c => Char.IsLetterOrDigit(c) || c == '_').ToArray());
        }

        private void StoreDataInDatabase(string csvFilePath, string tableName)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (StreamReader reader = new StreamReader(csvFilePath))
                    {
                        // Skip the header line
                        reader.ReadLine();

                        while (!reader.EndOfStream)
                        {
                            var parser = new TextFieldParser(new StringReader(reader.ReadLine()));
                            parser.HasFieldsEnclosedInQuotes = true;
                            parser.SetDelimiters(",");

                            string[] fields = parser.ReadFields();

                            using (SqlCommand command = new SqlCommand(
                                GetInsertQueryForTable(fields, tableName),
                                connection))
                            {
                                AddParametersBasedOnTable(fields, command, tableName);

                                command.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error instead of displaying a message box
                Console.WriteLine($"Error storing data in the database for table {tableName}: {ex.Message}");
            }
        }

        private string GetInsertQueryForTable(string[] fields, string tableName)
        {
            StringBuilder insertQuery = new StringBuilder($"INSERT INTO {CleanUpTableName(tableName)} VALUES (");

            for (int i = 0; i < fields.Length; i++)
            {
                insertQuery.Append($"@Param{i}, ");
            }

            // Remove the trailing comma and space
            insertQuery.Length -= 2;

            insertQuery.Append(")");

            return insertQuery.ToString();
        }

        private void AddParametersBasedOnTable(string[] fields, SqlCommand command, string tableName)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                command.Parameters.AddWithValue($"@Param{i}", fields[i]);
            }
        }
    }
}
