using Newtonsoft.Json;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Services.Description;
using System.Text.Json;
using System.IO;

namespace SDVA.Controllers
{
    public class ChatController : Controller
    {
        private string OpenAI_API_Key = "Your OpenAI Key ";

        public ActionResult Chat()
        {
            //Prevent the user to access the page by clicking the browser back button
            Response.Cache.SetNoStore();
            Response.Cache.AppendCacheExtension("no-cache");

            if (SqlDatabase.IsEmpty(Session["EMAIL"]))
                return RedirectToAction("Login", "Account");

            Models.ChatViewModel historyList = new Models.ChatViewModel();
            historyList.HistoryList = new List<Models.ChatHistory>();

            SqlDatabase objDB = new SqlDatabase();
            List<SqlParameter> Params = new List<SqlParameter>();
            try
            {
                if( SqlDatabase.IsEmpty(Session["ConversationID"]))
                    Session["IsNewChat"] = "1";
            
                objDB.AddParameter(ref Params, "USERID", Session["USERID"].ToString(), SqlDbType.Int);
                DataTable dtChatHistory = objDB.getDataTable("SELECT * FROM Conversations WHERE USERID=@USERID ORDER BY ConversationId Desc", Params);

                foreach (DataRow row in dtChatHistory.Rows)
                {
                    var history = new Models.ChatHistory();
                    history.ConversationId = (int) row["ConversationId"];
                    history.UserId = (int) row["UserID"];
                    history.Title = row["Title"].ToString();
                    history.TimeStamp = DateTime.Parse(row["Timestamp"].ToString());
                    historyList.HistoryList.Add(history);
                }
                
                OpenAIAPI objOpenAI;
                Conversation DbChat, UserChat, ChartChat;

                DbChat = GetOpenAIModel("DbChat", Model.GPT4_Turbo);
                UserChat = GetOpenAIModel("UserChat", Model.GPT4_Turbo, 0.6);
                ChartChat = GetOpenAIModel("ChartChat", Model.GPT4_Turbo);

                return View(historyList);
            }
            catch(Exception ex)
            {
                return Json(new { Error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
            finally
            {
                Params.Clear();
                Params = null;
                objDB = null;
            }
        }
        [HttpGet]
        public async Task<ActionResult> NewChat()
        {
            try
            {
                if (SqlDatabase.IsEmpty(Session["EMAIL"]))
                    return RedirectToAction("Login", "Account");

                Session["IsNewChat"] = "1";
                Session["DbChat"] = null;
                Session["UserChat"] = null;
                Session["ChartChat"] = null;
                Session["ConversationID"] = null;

                return Json(true, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet]
        public async Task<ActionResult> DeleteChat(string id)
        {
            try
            {
                if (SqlDatabase.IsEmpty(Session["EMAIL"]))
                    return RedirectToAction("Login", "Account");

                if (id == "")
                    return Json(new { ChatID=id, Status = false, IsActiveChat = false }, JsonRequestBehavior.AllowGet);

                bool IsCurrentChat = false;

                if (SqlDatabase.IsNull(Session["ConversationID"], "").ToString() == id)
                {
                    Session["ConversationID"] = null;
                    IsCurrentChat = true;
                }

                SqlDatabase objDb = new SqlDatabase();
                bool isDeleted = objDb.DeleteChat(Session["UserID"].ToString(),id);

                return Json(new { ChatID = id, Status = isDeleted, IsActiveChat = IsCurrentChat }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { ChatID = id, Status = false, IsActiveChat = false }, JsonRequestBehavior.AllowGet);
                //return Json(new { Error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet]
        public async Task<ActionResult> LoadHistory(string id)
        {
            try
            {
                if (SqlDatabase.IsEmpty(Session["EMAIL"]))
                    return RedirectToAction("Login", "Account");
                if (SqlDatabase.IsEmpty(Session["ConversationID"]) & id == "")
                    return Json("{}", JsonRequestBehavior.AllowGet);
                else if (id != "")
                    Session["ConversationID"] = id;
                
                Session["IsNewChat"] = "0";

                var response = await GetChatHistory(Session["ConversationID"].ToString());
                
                return Json(JsonConvert.SerializeObject(response, Formatting.Indented), JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public async Task<ActionResult> SendMessage(string UserInputQuery)
        {
            SqlDatabase objDB = new SqlDatabase();
            string SqlQueryReturnedByAI = "";
            string FinalAIReturnedResponse = "";
            string ChatTitle = "";
            DateTime? UserMessageTime = DateTime.Now;
            DateTime? AIQueryReturnMessageTime = null;
            //DateTime? AIFinalMessageTime;

            try
            {
                if (SqlDatabase.IsEmpty(Session["EMAIL"]))
                    return RedirectToAction("Login", "Account");

                OpenAIAPI objOpenAI;
                Conversation DbChat, UserChat, ChartChat;

                DbChat = GetOpenAIModel("DbChat", Model.GPT4_Turbo);
                UserChat = GetOpenAIModel("UserChat", Model.GPT4_Turbo, 0.6);
                ChartChat = GetOpenAIModel("ChartChat", Model.GPT4_Turbo);

                objOpenAI = (OpenAIAPI)Session["OpenAIClient"];

                string IsSqlStatement = "", UserAskedForChart = "", ChartType = "";
                string val = "", lineToRemove, strFinalPrompt, pattern = @"```sql\s*(.*?)\s*```";

                string SQLPrompt = "Only give answer to this in an sql statement. If you fail to understand give an sql statement that you best understand encompasses the query given to you/or can help most in arriving at the answer the query has asked of you.";
                lineToRemove = "I'm sorry, but I am not able to provide SQL statements. However, I can provide you with the information you are looking for.";

                val = "Does the following statement ask for an sql query specifically as its answer or the output of it.query or output only.\nAll warehouses that are active of type General who are at less then max capacity of products.\n output\nGive me the sql for All warehouses that are active of type General who are at less then max capacity of products.\nquery\n";

                strFinalPrompt = SQLPrompt + UserInputQuery;

                OpenAIAPI api = new OpenAIAPI("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");
                var Auth = new APIAuthentication("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");
                var openAiApi = new OpenAIAPI(Auth);
                var completionRequest = new OpenAI_API.Completions.CompletionRequest
                {
                    Prompt = "Does this statement refer to the creation of a pie/bar chart or not. Give Yes or No and nothing else as your answer\" +\n" +
                    "query:" + UserInputQuery + "\nAnswer:\");",
                    Model = "gpt-3.5-turbo-instruct",
                    MaxTokens = 1000,
                    Temperature = 0
                };
                Task.Run(async () =>
                {
                    var completionResult = await openAiApi.Completions.CreateCompletionAsync(completionRequest);
                    UserAskedForChart = completionResult.Completions[0].Text;

                }).Wait();


                //Task.Run(async () =>
                //{
                //    UserAskedForChart = await objOpenAI.Completions.GetCompletion("Does this statement refer to the creation of a pie/bar chart or not. Give Yes or No and nothing else as your answer" +
                //            "for exmaple: query:Give me a bar chart for all the values in the Employee joined Department table\nAnswer:YES\nquery:" + UserInputQuery + "\nAnswer:");
                //}).Wait();
                if (UserAskedForChart.ToLower().Contains("yes"))
                {
                    Task.Run(async () =>
                    {
                        ChartType = await objOpenAI.Completions.GetCompletion("Does this statement refer to the creation of a pie/bar chart or not. Give pie or bar and nothing else as your answer" +
                                "for example: query:Give me a pie chart for all the values in the Employee joined Department table\nAnswer:pie\n" +
                                "for example: query:Give me a bar chart for all the values in the Employee joined Department table\nAnswer:bar\nquery:" + UserInputQuery + "\nAnswer:");

                    }).Wait();
                }
                if (UserInputQuery.Contains("query") || UserInputQuery.Contains("Query") || UserInputQuery.Contains("Sql") || UserInputQuery.Contains("sql"))
                {
                    Task.Run(async () =>
                    {
                        IsSqlStatement = await objOpenAI.Completions.GetCompletion(val + "\nStatement:" + UserInputQuery);
                    }).Wait();
                }

                Task.Run(async () =>
                {
                    ChatTitle = await objOpenAI.Completions.GetCompletion("Suggest the Subject/Title of statement" +
                            "for exmaple: query:Give me a pie chart for all the values in the Employee joined Department table\nAnswer:Generation Pie Chart for Joined Employee" +
                            "\nquery:" + UserInputQuery + "\nAnswer:");

                }).Wait();

                Session["ChatTitle"] = ChatTitle;

                DbChat.AppendUserInput(strFinalPrompt);

                Task.Run(async () =>
                {
                    SqlQueryReturnedByAI = await DbChat.GetResponseFromChatbotAsync();

                    Match match = Regex.Match(SqlQueryReturnedByAI, pattern, RegexOptions.Singleline);
                    if (match.Success)
                    {
                        SqlQueryReturnedByAI = match.Groups[1].Value;
                    }
                }).Wait();

                AIQueryReturnMessageTime = DateTime.Now;
                string AISqlQueryOutput = "";

                if (SqlQueryReturnedByAI.Equals(""))
                {
                    FinalAIReturnedResponse = "I could not understand wht you meant.\nCould you clarify?\n";
                }
                else if (!IsSqlStatement.Contains("query"))
                {
                    try
                    {
                        DataSet ds = objDB.getDataset(SqlQueryReturnedByAI.Replace("GO", "").Replace("USE [SDVA]", ""), null);
                        if (!SqlDatabase.IsEmpty(ds) & ds.Tables.Count > 0)
                        {
                            foreach (DataTable dt in ds.Tables)
                            {
                                //string tableName = "Table Name :";
                                //string tableValue = dt.TableName;

                                //AISqlQueryOutput += ($"{tableName}: {tableValue}\n");

                                if (!SqlDatabase.IsEmpty(dt) & dt.Rows.Count > 0)
                                {
                                    foreach (DataRow row in dt.Rows)
                                    {
                                        foreach (DataColumn col in dt.Columns)
                                        {
                                            string columnName = col.ColumnName;
                                            object columnValue = row[col.ColumnName];

                                            AISqlQueryOutput += ($"{columnName}: {columnValue}\n");
                                        }
                                    }
                                }
                                else
                                {
                                    FinalAIReturnedResponse = "I could not understand wht you meant.\nCould you clarify?\n";
                                }
                            }

                            if (UserAskedForChart.ToLower().Contains("yes") & !SqlDatabase.IsEmpty(AISqlQueryOutput))
                            {
                                AISqlQueryOutput += ($"{"chartType"}: {ChartType}\n");
                                Task.Run(async delegate
                                {
                                    ChartChat.AppendUserInput("You must not give anything but what is required in the given format. Go through this data and give all the labels you find and all the data you find.\nGive in json format { \"chartType\": [\"pie\"],\"labels\":[|\"januray\",\"febraury\",\"march\",\"april\"],\"data\":[32,55,64,123]}\n Number of labels and Data should be same and should be" +
                                        " in json format.Dont start with json.Query:" + AISqlQueryOutput + "\n");
                                    FinalAIReturnedResponse = await ChartChat.GetResponseFromChatbotAsync();
                                    FinalAIReturnedResponse = FinalAIReturnedResponse.Replace("json", "").Replace("`", "").Replace("\n", "");

                                }).Wait();
                            }
                            else if (UserAskedForChart.ToLower().Contains("yes"))
                            {
                                FinalAIReturnedResponse = "I could not understand wht you meant.\nCould you clarify?\n";
                            }
                        }
                        else
                        {
                            FinalAIReturnedResponse = "I could not understand wht you meant.\nCould you clarify?\n";
                        }
                    }
                    catch (Exception ex)
                    {
                        FinalAIReturnedResponse = "I could not understand wht you meant.\nCould you clarify?\n";
                    }
                }

                if (!UserAskedForChart.ToLower().Contains("yes") & !IsValidJson(FinalAIReturnedResponse))
                {
                    UserChat.AppendUserInput("You must behave as if you already know the context and not refer to it as you explain the Context keeping this line in mind " + strFinalPrompt + "\nContext:" + AISqlQueryOutput + "\nAi:");

                    Task.Run(async delegate
                    {
                        FinalAIReturnedResponse = await UserChat.GetResponseFromChatbotAsync();
                        FinalAIReturnedResponse = FinalAIReturnedResponse.Replace(lineToRemove, "").Trim();
                    }).Wait();

                    //----------Stream Method 01-----------------------
                    //await UserChat.StreamResponseFromChatbotAsync(res =>
                    //{
                    //    Response.Write(res);
                    //});


                    //----------Stream Method 02-----------------------

                    //var responseStream = new MemoryStream();

                    //await UserChat.StreamResponseFromChatbotAsync(res =>
                    //{
                    //    //var bytes = Encoding.UTF8.GetBytes(res);
                    //    //responseStream.Write(bytes, 0, res.Length);
                    //});

                    //// Reset the position of the response stream
                    ////responseStream.Position = 0;

                    //// Return the response stream as a FileResult
                    //return File(responseStream, "text/plain");
                }

                //-------For Stream Methods------------------
                //return new EmptyResult();

                return Json(FinalAIReturnedResponse, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
            finally
            {
                try
                {
                    if (Session["ConversationID"] is null | SqlDatabase.IsNull(Session["IsNewChat"], "0") == "1")
                        objDB.AppendMessage("0", Session["UserID"].ToString(), "User", UserInputQuery, UserMessageTime);
                    else
                        objDB.AppendMessage(Session["ConversationID"].ToString(), Session["UserID"].ToString(), "User", UserInputQuery, UserMessageTime);
                }
                catch { }

                try
                {
                    //Append the ChatBot Returned SQL Query In DB
                    _ = objDB.AppendMessage(Session["ConversationID"].ToString(), Session["UserID"].ToString(), "ChatbotSQL", SqlQueryReturnedByAI, AIQueryReturnMessageTime);
                }
                catch { }

                try
                {
                    //Append the ChatBot Returned SQL Query In DB
                    objDB.AppendMessage(Session["ConversationID"].ToString(), Session["UserID"].ToString(), "Chatbot", FinalAIReturnedResponse, DateTime.Now);
                }
                catch { }
            }
        }

        //Get the User Chat/Conversation messages
        private async Task<DataTable> GetChatHistory(string id)
        {
            SqlDatabase objDB = new SqlDatabase();
            return objDB.LoadChat(Session["UserID"].ToString(), id);
        }
        
        //Save OpenAIAPI object in session and retrive from the session for the login user
        public Conversation GetOpenAIModel(string modelName, Model objModel,double chatTemperature=0.0)
        {
            // Check if the session variable exists
            if (Session[modelName] == null)
            {
                // Create a new OpenAIAPI object with API key
                OpenAIAPI objOpenAI;
                if (Session["OpenAIClient"] == null)
                {
                    objOpenAI = new OpenAIAPI(OpenAI_API_Key);
                    
                    // Store the object in the session variable
                    Session["OpenAIClient"] = objOpenAI;
                }
                else
                {
                    objOpenAI = (OpenAIAPI)Session["OpenAIClient"];
                }
                
                //Create new Chat/Conversation object for the user
                Conversation objConversation= objOpenAI.Chat.CreateConversation();

                //Setting the Chat Object Model
                objConversation.Model = objModel;

                //Setting the Chat Object Temperature
                objConversation.RequestParameters.Temperature = chatTemperature;
                
                // Store the object in the session variable
                Session[modelName] = objConversation;
                if (modelName == "DbChat")
                {
                    string DBSchemaString = "";
                    string GetDbSchemaQuery = "SELECT t.TABLE_SCHEMA,t.TABLE_NAME,c.COLUMN_NAME,c.DATA_TYPE, ";
                    GetDbSchemaQuery += " c.CHARACTER_MAXIMUM_LENGTH,c.NUMERIC_PRECISION,c.IS_NULLABLE ";
                    GetDbSchemaQuery += " FROM INFORMATION_SCHEMA.TABLES t ";
                    GetDbSchemaQuery += " INNER JOIN INFORMATION_SCHEMA.COLUMNS c ON t.TABLE_SCHEMA = c.TABLE_SCHEMA ";
                    GetDbSchemaQuery += " AND t.TABLE_NAME = c.TABLE_NAME ";
                    GetDbSchemaQuery += " WHERE t.TABLE_NAME NOT IN('Conversations', 'ConversationMessages') ";
                    GetDbSchemaQuery += " and c.COLUMN_NAME NOT LIKE '%Password%'; ";

                    SqlDatabase objDB = new SqlDatabase();
                    try
                    {
                        DataTable dtDBSchema = objDB.getDataTable(GetDbSchemaQuery, null);

                        foreach (DataRow row in dtDBSchema.Rows)
                        {
                            foreach (DataColumn column in dtDBSchema.Columns)
                            {
                                DBSchemaString += row[column].ToString() + " | ";
                            }
                            DBSchemaString = DBSchemaString.TrimEnd(' ', '|') + Environment.NewLine; // Remove the last delimiter and add a new line
                        }
                    }
                    catch (Exception ex)
                    {
                        DBSchemaString = "USE [master]\r\nGO\r\n/****** Object:  Database [SDVA]    Script Date: 12.01.2024 13:40:54 ******/\r\nCREATE DATABASE [SDVA]\r\n CONTAINMENT = NONE\r\n ON  PRIMARY \r\n( NAME = N'SDVA', FILENAME = N'C:\\Program Files\\Microsoft SQL Server\\MSSQL15.MSSQLSERVER\\MSSQL\\DATA\\SDVA.mdf' , SIZE = 8192KB , MAXSIZE = UNLIMITED, FILEGROWTH = 65536KB )\r\n LOG ON \r\n( NAME = N'SDVA_log', FILENAME = N'C:\\Program Files\\Microsoft SQL Server\\MSSQL15.MSSQLSERVER\\MSSQL\\DATA\\SDVA_log.ldf' , SIZE = 8192KB , MAXSIZE = 2048GB , FILEGROWTH = 65536KB )\r\n WITH CATALOG_COLLATION = DATABASE_DEFAULT\r\nGO\r\nALTER DATABASE [SDVA] SET COMPATIBILITY_LEVEL = 150\r\nGO\r\nIF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))\r\nbegin\r\nEXEC [SDVA].[dbo].[sp_fulltext_database] @action = 'enable'\r\nend\r\nGO\r\nALTER DATABASE [SDVA] SET ANSI_NULL_DEFAULT OFF \r\nGO\r\nALTER DATABASE [SDVA] SET ANSI_NULLS OFF \r\nGO\r\nALTER DATABASE [SDVA] SET ANSI_PADDING OFF \r\nGO\r\nALTER DATABASE [SDVA] SET ANSI_WARNINGS OFF \r\nGO\r\nALTER DATABASE [SDVA] SET ARITHABORT OFF \r\nGO\r\nALTER DATABASE [SDVA] SET AUTO_CLOSE OFF \r\nGO\r\nALTER DATABASE [SDVA] SET AUTO_SHRINK OFF \r\nGO\r\nALTER DATABASE [SDVA] SET AUTO_UPDATE_STATISTICS ON \r\nGO\r\nALTER DATABASE [SDVA] SET CURSOR_CLOSE_ON_COMMIT OFF \r\nGO\r\nALTER DATABASE [SDVA] SET CURSOR_DEFAULT  GLOBAL \r\nGO\r\nALTER DATABASE [SDVA] SET CONCAT_NULL_YIELDS_NULL OFF \r\nGO\r\nALTER DATABASE [SDVA] SET NUMERIC_ROUNDABORT OFF \r\nGO\r\nALTER DATABASE [SDVA] SET QUOTED_IDENTIFIER OFF \r\nGO\r\nALTER DATABASE [SDVA] SET RECURSIVE_TRIGGERS OFF \r\nGO\r\nALTER DATABASE [SDVA] SET  DISABLE_BROKER \r\nGO\r\nALTER DATABASE [SDVA] SET AUTO_UPDATE_STATISTICS_ASYNC OFF \r\nGO\r\nALTER DATABASE [SDVA] SET DATE_CORRELATION_OPTIMIZATION OFF \r\nGO\r\nALTER DATABASE [SDVA] SET TRUSTWORTHY OFF \r\nGO\r\nALTER DATABASE [SDVA] SET ALLOW_SNAPSHOT_ISOLATION OFF \r\nGO\r\nALTER DATABASE [SDVA] SET PARAMETERIZATION SIMPLE \r\nGO\r\nALTER DATABASE [SDVA] SET READ_COMMITTED_SNAPSHOT OFF \r\nGO\r\nALTER DATABASE [SDVA] SET HONOR_BROKER_PRIORITY OFF \r\nGO\r\nALTER DATABASE [SDVA] SET RECOVERY FULL \r\nGO\r\nALTER DATABASE [SDVA] SET  MULTI_USER \r\nGO\r\nALTER DATABASE [SDVA] SET PAGE_VERIFY CHECKSUM  \r\nGO\r\nALTER DATABASE [SDVA] SET DB_CHAINING OFF \r\nGO\r\nALTER DATABASE [SDVA] SET FILESTREAM( NON_TRANSACTED_ACCESS = OFF ) \r\nGO\r\nALTER DATABASE [SDVA] SET TARGET_RECOVERY_TIME = 60 SECONDS \r\nGO\r\nALTER DATABASE [SDVA] SET DELAYED_DURABILITY = DISABLED \r\nGO\r\nALTER DATABASE [SDVA] SET ACCELERATED_DATABASE_RECOVERY = OFF  \r\nGO\r\nEXEC sys.sp_db_vardecimal_storage_format N'SDVA', N'ON'\r\nGO\r\nALTER DATABASE [SDVA] SET QUERY_STORE = OFF\r\nGO\r\nUSE [SDVA]\r\nGO\r\n/****** Object:  Table [dbo].[Carriers]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Carriers](\r\n\t[CarrierId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[Name] [nvarchar](100) NULL,\r\n\t[Headquarters] [nvarchar](100) NULL,\r\n\t[Founded] [date] NULL,\r\n\t[ServiceArea] [nvarchar](100) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[CarrierId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Conversations]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Conversations](\r\n\t[ConversationId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[UserId] [int] NULL,\r\n\t[Message] [nvarchar](max) NULL,\r\n\t[MessageType] [char](1) NULL,\r\n\t[Timestamp] [datetime] NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[ConversationId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Customers]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Customers](\r\n\t[CustomerId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[Name] [nvarchar](100) NULL,\r\n\t[Address] [nvarchar](100) NULL,\r\n\t[City] [nvarchar](50) NULL,\r\n\t[PostalCode] [nvarchar](10) NULL,\r\n\t[Country] [nvarchar](50) NULL,\r\n\t[Phone] [nvarchar](15) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[CustomerId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Departments]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Departments](\r\n\t[DepartmentId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[Name] [nvarchar](100) NULL,\r\n\t[Description] [nvarchar](max) NULL,\r\n\t[Location] [nvarchar](100) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[DepartmentId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Employee]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Employee](\r\n\t[EmployeeId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[FirstName] [nvarchar](100) NULL,\r\n\t[LastName] [nvarchar](100) NULL,\r\n\t[Position] [nvarchar](100) NULL,\r\n\t[HireDate] [datetime] NULL,\r\n\t[Salary] [decimal](18, 2) NULL,\r\n\t[DepartmentId] [int] NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[EmployeeId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Inventory]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Inventory](\r\n\t[InventoryId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[WarehouseId] [int] NULL,\r\n\t[ProductId] [int] NULL,\r\n\t[Quantity] [int] NULL,\r\n\t[Status] [nvarchar](50) NULL,\r\n\t[LastUpdated] [datetime] NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[InventoryId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Orders]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Orders](\r\n\t[OrderId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[CustomerId] [int] NULL,\r\n\t[OrderDate] [datetime] NULL,\r\n\t[RequiredDate] [datetime] NULL,\r\n\t[ShippedDate] [datetime] NULL,\r\n\t[Status] [nvarchar](50) NULL,\r\n\t[Comments] [nvarchar](max) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[OrderId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Products]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Products](\r\n\t[ProductId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[Name] [nvarchar](100) NULL,\r\n\t[Description] [nvarchar](max) NULL,\r\n\t[UnitPrice] [decimal](18, 2) NULL,\r\n\t[Category] [nvarchar](50) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[ProductId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Shipment]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Shipment](\r\n\t[ShipmentId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[Origin] [nvarchar](100) NULL,\r\n\t[Destination] [nvarchar](100) NULL,\r\n\t[ShipmentDate] [datetime] NULL,\r\n\t[EstimatedArrival] [datetime] NULL,\r\n\t[Status] [nvarchar](50) NULL,\r\n\t[CarrierId] [int] NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[ShipmentId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[TransactionHistory]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[TransactionHistory](\r\n\t[TransactionId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[OrderId] [int] NULL,\r\n\t[TransactionDate] [datetime] NULL,\r\n\t[Amount] [decimal](18, 2) NULL,\r\n\t[PaymentType] [nvarchar](50) NULL,\r\n\t[Status] [nvarchar](50) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[TransactionId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Users]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Users](\r\n\t[UserId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[Name] [nvarchar](100) NULL,\r\n\t[Email] [nvarchar](100) NULL,\r\n\t[DateOfBirth] [date] NULL,\r\n\t[PhoneNumber] [nvarchar](15) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[UserId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Vehicle]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Vehicle](\r\n\t[VehicleId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[CarrierId] [int] NULL,\r\n\t[Make] [nvarchar](100) NULL,\r\n\t[Model] [nvarchar](100) NULL,\r\n\t[Year] [int] NULL,\r\n\t[Capacity] [int] NULL,\r\n\t[Status] [nvarchar](50) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[VehicleId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[VehicleMaintenance]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[VehicleMaintenance](\r\n\t[MaintenanceId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[VehicleId] [int] NULL,\r\n\t[MaintenanceDate] [datetime] NULL,\r\n\t[Description] [nvarchar](max) NULL,\r\n\t[Cost] [decimal](18, 2) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[MaintenanceId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Warehouses]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Warehouses](\r\n\t[WarehouseId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[Name] [nvarchar](100) NULL,\r\n\t[Location] [nvarchar](100) NULL,\r\n\t[Capacity] [int] NULL,\r\n\t[Type] [nvarchar](50) NULL,\r\n\t[Status] [nvarchar](50) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[WarehouseId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\nALTER TABLE [dbo].[Conversations]  WITH CHECK ADD FOREIGN KEY([UserId])\r\nREFERENCES [dbo].[Users] ([UserId])\r\nGO\r\nALTER TABLE [dbo].[Employee]  WITH CHECK ADD FOREIGN KEY([DepartmentId])\r\nREFERENCES [dbo].[Departments] ([DepartmentId])\r\nGO\r\nALTER TABLE [dbo].[Inventory]  WITH CHECK ADD FOREIGN KEY([ProductId])\r\nREFERENCES [dbo].[Products] ([ProductId])\r\nGO\r\nALTER TABLE [dbo].[Inventory]  WITH CHECK ADD FOREIGN KEY([WarehouseId])\r\nREFERENCES [dbo].[Warehouses] ([WarehouseId])\r\nGO\r\nALTER TABLE [dbo].[Orders]  WITH CHECK ADD FOREIGN KEY([CustomerId])\r\nREFERENCES [dbo].[Customers] ([CustomerId])\r\nGO\r\nALTER TABLE [dbo].[Shipment]  WITH CHECK ADD FOREIGN KEY([CarrierId])\r\nREFERENCES [dbo].[Carriers] ([CarrierId])\r\nGO\r\nALTER TABLE [dbo].[TransactionHistory]  WITH CHECK ADD FOREIGN KEY([OrderId])\r\nREFERENCES [dbo].[Orders] ([OrderId])\r\nGO\r\nALTER TABLE [dbo].[Vehicle]  WITH CHECK ADD FOREIGN KEY([CarrierId])\r\nREFERENCES [dbo].[Carriers] ([CarrierId])\r\nGO\r\nALTER TABLE [dbo].[VehicleMaintenance]  WITH CHECK ADD FOREIGN KEY([VehicleId])\r\nREFERENCES [dbo].[Vehicle] ([VehicleId])\r\nGO\r\nUSE [master]\r\nGO\r\nALTER DATABASE [SDVA] SET  READ_WRITE \r\nGO\r";
                    }
                    finally
                    {
                        objDB = null;
                    }

                    //objConversation.AppendSystemMessage("USE [master]\r\nGO\r\n/****** Object:  Database [SDVA]    Script Date: 12.01.2024 13:40:54 ******/\r\nCREATE DATABASE [SDVA]\r\n CONTAINMENT = NONE\r\n ON  PRIMARY \r\n( NAME = N'SDVA', FILENAME = N'C:\\Program Files\\Microsoft SQL Server\\MSSQL15.MSSQLSERVER\\MSSQL\\DATA\\SDVA.mdf' , SIZE = 8192KB , MAXSIZE = UNLIMITED, FILEGROWTH = 65536KB )\r\n LOG ON \r\n( NAME = N'SDVA_log', FILENAME = N'C:\\Program Files\\Microsoft SQL Server\\MSSQL15.MSSQLSERVER\\MSSQL\\DATA\\SDVA_log.ldf' , SIZE = 8192KB , MAXSIZE = 2048GB , FILEGROWTH = 65536KB )\r\n WITH CATALOG_COLLATION = DATABASE_DEFAULT\r\nGO\r\nALTER DATABASE [SDVA] SET COMPATIBILITY_LEVEL = 150\r\nGO\r\nIF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))\r\nbegin\r\nEXEC [SDVA].[dbo].[sp_fulltext_database] @action = 'enable'\r\nend\r\nGO\r\nALTER DATABASE [SDVA] SET ANSI_NULL_DEFAULT OFF \r\nGO\r\nALTER DATABASE [SDVA] SET ANSI_NULLS OFF \r\nGO\r\nALTER DATABASE [SDVA] SET ANSI_PADDING OFF \r\nGO\r\nALTER DATABASE [SDVA] SET ANSI_WARNINGS OFF \r\nGO\r\nALTER DATABASE [SDVA] SET ARITHABORT OFF \r\nGO\r\nALTER DATABASE [SDVA] SET AUTO_CLOSE OFF \r\nGO\r\nALTER DATABASE [SDVA] SET AUTO_SHRINK OFF \r\nGO\r\nALTER DATABASE [SDVA] SET AUTO_UPDATE_STATISTICS ON \r\nGO\r\nALTER DATABASE [SDVA] SET CURSOR_CLOSE_ON_COMMIT OFF \r\nGO\r\nALTER DATABASE [SDVA] SET CURSOR_DEFAULT  GLOBAL \r\nGO\r\nALTER DATABASE [SDVA] SET CONCAT_NULL_YIELDS_NULL OFF \r\nGO\r\nALTER DATABASE [SDVA] SET NUMERIC_ROUNDABORT OFF \r\nGO\r\nALTER DATABASE [SDVA] SET QUOTED_IDENTIFIER OFF \r\nGO\r\nALTER DATABASE [SDVA] SET RECURSIVE_TRIGGERS OFF \r\nGO\r\nALTER DATABASE [SDVA] SET  DISABLE_BROKER \r\nGO\r\nALTER DATABASE [SDVA] SET AUTO_UPDATE_STATISTICS_ASYNC OFF \r\nGO\r\nALTER DATABASE [SDVA] SET DATE_CORRELATION_OPTIMIZATION OFF \r\nGO\r\nALTER DATABASE [SDVA] SET TRUSTWORTHY OFF \r\nGO\r\nALTER DATABASE [SDVA] SET ALLOW_SNAPSHOT_ISOLATION OFF \r\nGO\r\nALTER DATABASE [SDVA] SET PARAMETERIZATION SIMPLE \r\nGO\r\nALTER DATABASE [SDVA] SET READ_COMMITTED_SNAPSHOT OFF \r\nGO\r\nALTER DATABASE [SDVA] SET HONOR_BROKER_PRIORITY OFF \r\nGO\r\nALTER DATABASE [SDVA] SET RECOVERY FULL \r\nGO\r\nALTER DATABASE [SDVA] SET  MULTI_USER \r\nGO\r\nALTER DATABASE [SDVA] SET PAGE_VERIFY CHECKSUM  \r\nGO\r\nALTER DATABASE [SDVA] SET DB_CHAINING OFF \r\nGO\r\nALTER DATABASE [SDVA] SET FILESTREAM( NON_TRANSACTED_ACCESS = OFF ) \r\nGO\r\nALTER DATABASE [SDVA] SET TARGET_RECOVERY_TIME = 60 SECONDS \r\nGO\r\nALTER DATABASE [SDVA] SET DELAYED_DURABILITY = DISABLED \r\nGO\r\nALTER DATABASE [SDVA] SET ACCELERATED_DATABASE_RECOVERY = OFF  \r\nGO\r\nEXEC sys.sp_db_vardecimal_storage_format N'SDVA', N'ON'\r\nGO\r\nALTER DATABASE [SDVA] SET QUERY_STORE = OFF\r\nGO\r\nUSE [SDVA]\r\nGO\r\n/****** Object:  Table [dbo].[Carriers]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Carriers](\r\n\t[CarrierId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[Name] [nvarchar](100) NULL,\r\n\t[Headquarters] [nvarchar](100) NULL,\r\n\t[Founded] [date] NULL,\r\n\t[ServiceArea] [nvarchar](100) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[CarrierId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Conversations]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Conversations](\r\n\t[ConversationId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[UserId] [int] NULL,\r\n\t[Message] [nvarchar](max) NULL,\r\n\t[MessageType] [char](1) NULL,\r\n\t[Timestamp] [datetime] NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[ConversationId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Customers]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Customers](\r\n\t[CustomerId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[Name] [nvarchar](100) NULL,\r\n\t[Address] [nvarchar](100) NULL,\r\n\t[City] [nvarchar](50) NULL,\r\n\t[PostalCode] [nvarchar](10) NULL,\r\n\t[Country] [nvarchar](50) NULL,\r\n\t[Phone] [nvarchar](15) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[CustomerId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Departments]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Departments](\r\n\t[DepartmentId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[Name] [nvarchar](100) NULL,\r\n\t[Description] [nvarchar](max) NULL,\r\n\t[Location] [nvarchar](100) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[DepartmentId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Employee]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Employee](\r\n\t[EmployeeId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[FirstName] [nvarchar](100) NULL,\r\n\t[LastName] [nvarchar](100) NULL,\r\n\t[Position] [nvarchar](100) NULL,\r\n\t[HireDate] [datetime] NULL,\r\n\t[Salary] [decimal](18, 2) NULL,\r\n\t[DepartmentId] [int] NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[EmployeeId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Inventory]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Inventory](\r\n\t[InventoryId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[WarehouseId] [int] NULL,\r\n\t[ProductId] [int] NULL,\r\n\t[Quantity] [int] NULL,\r\n\t[Status] [nvarchar](50) NULL,\r\n\t[LastUpdated] [datetime] NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[InventoryId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Orders]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Orders](\r\n\t[OrderId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[CustomerId] [int] NULL,\r\n\t[OrderDate] [datetime] NULL,\r\n\t[RequiredDate] [datetime] NULL,\r\n\t[ShippedDate] [datetime] NULL,\r\n\t[Status] [nvarchar](50) NULL,\r\n\t[Comments] [nvarchar](max) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[OrderId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Products]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Products](\r\n\t[ProductId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[Name] [nvarchar](100) NULL,\r\n\t[Description] [nvarchar](max) NULL,\r\n\t[UnitPrice] [decimal](18, 2) NULL,\r\n\t[Category] [nvarchar](50) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[ProductId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Shipment]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Shipment](\r\n\t[ShipmentId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[Origin] [nvarchar](100) NULL,\r\n\t[Destination] [nvarchar](100) NULL,\r\n\t[ShipmentDate] [datetime] NULL,\r\n\t[EstimatedArrival] [datetime] NULL,\r\n\t[Status] [nvarchar](50) NULL,\r\n\t[CarrierId] [int] NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[ShipmentId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[TransactionHistory]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[TransactionHistory](\r\n\t[TransactionId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[OrderId] [int] NULL,\r\n\t[TransactionDate] [datetime] NULL,\r\n\t[Amount] [decimal](18, 2) NULL,\r\n\t[PaymentType] [nvarchar](50) NULL,\r\n\t[Status] [nvarchar](50) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[TransactionId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Users]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Users](\r\n\t[UserId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[Name] [nvarchar](100) NULL,\r\n\t[Email] [nvarchar](100) NULL,\r\n\t[DateOfBirth] [date] NULL,\r\n\t[PhoneNumber] [nvarchar](15) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[UserId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Vehicle]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Vehicle](\r\n\t[VehicleId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[CarrierId] [int] NULL,\r\n\t[Make] [nvarchar](100) NULL,\r\n\t[Model] [nvarchar](100) NULL,\r\n\t[Year] [int] NULL,\r\n\t[Capacity] [int] NULL,\r\n\t[Status] [nvarchar](50) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[VehicleId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[VehicleMaintenance]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[VehicleMaintenance](\r\n\t[MaintenanceId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[VehicleId] [int] NULL,\r\n\t[MaintenanceDate] [datetime] NULL,\r\n\t[Description] [nvarchar](max) NULL,\r\n\t[Cost] [decimal](18, 2) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[MaintenanceId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]\r\nGO\r\n/****** Object:  Table [dbo].[Warehouses]    Script Date: 12.01.2024 13:40:54 ******/\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\r\nCREATE TABLE [dbo].[Warehouses](\r\n\t[WarehouseId] [int] IDENTITY(1,1) NOT NULL,\r\n\t[Name] [nvarchar](100) NULL,\r\n\t[Location] [nvarchar](100) NULL,\r\n\t[Capacity] [int] NULL,\r\n\t[Type] [nvarchar](50) NULL,\r\n\t[Status] [nvarchar](50) NULL,\r\nPRIMARY KEY CLUSTERED \r\n(\r\n\t[WarehouseId] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]\r\n) ON [PRIMARY]\r\nGO\r\nALTER TABLE [dbo].[Conversations]  WITH CHECK ADD FOREIGN KEY([UserId])\r\nREFERENCES [dbo].[Users] ([UserId])\r\nGO\r\nALTER TABLE [dbo].[Employee]  WITH CHECK ADD FOREIGN KEY([DepartmentId])\r\nREFERENCES [dbo].[Departments] ([DepartmentId])\r\nGO\r\nALTER TABLE [dbo].[Inventory]  WITH CHECK ADD FOREIGN KEY([ProductId])\r\nREFERENCES [dbo].[Products] ([ProductId])\r\nGO\r\nALTER TABLE [dbo].[Inventory]  WITH CHECK ADD FOREIGN KEY([WarehouseId])\r\nREFERENCES [dbo].[Warehouses] ([WarehouseId])\r\nGO\r\nALTER TABLE [dbo].[Orders]  WITH CHECK ADD FOREIGN KEY([CustomerId])\r\nREFERENCES [dbo].[Customers] ([CustomerId])\r\nGO\r\nALTER TABLE [dbo].[Shipment]  WITH CHECK ADD FOREIGN KEY([CarrierId])\r\nREFERENCES [dbo].[Carriers] ([CarrierId])\r\nGO\r\nALTER TABLE [dbo].[TransactionHistory]  WITH CHECK ADD FOREIGN KEY([OrderId])\r\nREFERENCES [dbo].[Orders] ([OrderId])\r\nGO\r\nALTER TABLE [dbo].[Vehicle]  WITH CHECK ADD FOREIGN KEY([CarrierId])\r\nREFERENCES [dbo].[Carriers] ([CarrierId])\r\nGO\r\nALTER TABLE [dbo].[VehicleMaintenance]  WITH CHECK ADD FOREIGN KEY([VehicleId])\r\nREFERENCES [dbo].[Vehicle] ([VehicleId])\r\nGO\r\nUSE [master]\r\nGO\r\nALTER DATABASE [SDVA] SET  READ_WRITE \r\nGO\r" +
                    objConversation.AppendSystemMessage(DBSchemaString+
                         "Some semantic knowledge.[Inventory] [Status] is either Available,Out of Stock or Low Stock." +
                         "[Orders] [Status] is either Shipped,Processing or Delivered" +
                         "[Shipment] [Status] is In Transit or Delivered" +
                         "[Warehouses] [Status] is Active or Under Maintenance\n" +
                         "[Vehicle] [Status] Active or Maintenance\n" +
                         "[Warehouses] have [Name] as [Warehouse <Number>] <Number> being a number" +
                         "[Warehouses] [Type] is General,Cold Storage, Automative or Electronics" +
                         "Inventory tells us which product is stored in which warehouse at what quantity" +
                         "[Carriers] have Headquaters whose format is Country, City With ServiceArea being a country or Global" +
                         "[Warehouse].location and [department].location are cities" +
                         "[Inventory] [Status] is Avaliable, Low Stock or Out of Stock" +
                         "Inventory holds data for products against warehouse.\n" +
                         "[TransactionHistory] [Payment Type] Credit Card,PayPal or Bank Transfer" +
                         "[TransactionHistory] [Status] Completed,Pending or Failed" +
                         "GIVE APPROPIATE COLUMN NAME WHEREEVER POSSIBLE LIKE COUNT OF VEHICLES IN MANTAINCE OR COUNT OF EMPLYEES IN DEPARTMENT 2 etc..\nIf you are using Count always use As also with apropirate name.\n" +
                        "In case of multiple queries seprate each Select query by an extra newline " +
                        "\n  all pie/bar chart queries should be treated as COunt queries" +
                        "all PIE OR BAR CHARTS SHOuld be dealt with count sql queries ");
                }
                else if (modelName == "UserChat")
                    objConversation.AppendSystemMessage("You are a highly Chat bot that is expert in reading data base data and communicating its main points to the user.\n Your job is to only chat with user and not participate or provide any technical details.\nDO NOT WRITE ANY SQL STATEMENTS.\n");
                else if (modelName == "ChartChat")
                    objConversation.AppendSystemMessage("You are a expert in finding labels and data from context keeping in mind this label and data will be fed to a used to make a chart and most of all you do not ever write anything extra or try to explain wht you have written through comments or otherwise.");
            }

            // Return the object from the session variable
            return (Conversation)Session[modelName];
        }

        public static bool IsValidJson(string input)
        {
            try
            {
                JsonDocument.Parse(input);
                return true;
            }
            catch (System.Text.Json.JsonException jex)
            {
                return false;
            }
        }

	}
}