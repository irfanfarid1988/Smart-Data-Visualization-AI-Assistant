
using System;
using System.Data;
using System.IO;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Net;
using System.Web;
using System.Web.UI.WebControls;

namespace SDVA
{
    #region Sql Native Client Data Class    
    public class SqlDatabase
    {
        //private const string ServerName = "(local)";
        //private const string SAPassword = "";
        //private const string DatabaseName = "SDVA";

        //private const string DefaultConnection = "Data Source="+ServerName+";Initial Catalog="+DatabaseName+";User ID=SA;Password="+SAPassword+";TrustServerCertificate=true;";
        private const string DefaultConnection = "Data Source="+ServerName+ ";Initial Catalog=" + DatabaseName + ";Integrated Security=True;TrustServerCertificate=true;";

        int varExecutionTimeOut = 120;

        public int ExecutionTimeOut
        {
            get{
                return varExecutionTimeOut;
            }
            set{
                varExecutionTimeOut = value;
            }
        }
        /// <summary>
        ///     ''' Check If value is Null/Empty
        ///     ''' </summary>
        ///     ''' <param name="Text">String/Object to evaluation</param>
        ///     ''' <returns>Return Boolean</returns>
        public static bool IsEmpty(object Text)
        {
            try
            {
                if (Text == null)
                    return true;
                else if (Convert.IsDBNull(Text))
                    return true;
                else if (Text is string | Text is String)
                {
                    if (string.IsNullOrEmpty(Text.ToString().Trim()))
                        return true;
                    else if (string.IsNullOrWhiteSpace(Text.ToString().Trim()))
                        return true;
                    else
                        return false;
                }
                else
                    return false;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        ///     ''' Check If value is Null/Empty then return given Default value
        ///     ''' </summary>
        ///     ''' <param name="Text">String/Object to evaluation</param>
        ///     ''' <param name="DefaultValue">Default Value in case of Null/Empty</param>
        ///     ''' <returns>Return String</returns>
        public static string IsNull(object Text, string DefaultValue)
        {
            try
            {
                if (Text == null)
                    return DefaultValue;
                else if (Convert.IsDBNull(Text))
                    return DefaultValue;
                else if (Text is string | Text is String)
                {
                    if (string.IsNullOrEmpty(Text.ToString()))
                        return DefaultValue;
                    else if (string.IsNullOrWhiteSpace(Text.ToString()))
                        return DefaultValue;
                    else
                        return Text.ToString().Trim();
                }
                else
                    return Text.ToString().Trim();
            }
            catch
            {
                return DefaultValue;
            }
        }
        /// <summary>
        ///     ''' Check If value is Null/Empty then return given Default value
        ///     ''' </summary>
        ///     ''' <param name="Text">String/Object to evaluation</param>
        ///     ''' <param name="DefaultValue">Default Value in case of Null/Empty</param>
        ///     ''' <returns>Return object</returns>
        public static object IsNull(object Text, object DefaultValue)
        {
            try
            {
                if (Text == null)
                    return DefaultValue;
                else if (Convert.IsDBNull(Text))
                    return DefaultValue;
                else if (Text is string | Text is String)
                {
                    if (string.IsNullOrEmpty(Text.ToString()))
                        return DefaultValue;
                    else if (string.IsNullOrWhiteSpace(Text.ToString()))
                        return DefaultValue;
                    else
                        return Text.ToString().Trim();
                }
                else
                    return Text;
            }
            catch
            {
                return DefaultValue;
            }
        }
        /// <summary>
        /// Default Constructor of Class.
        /// </summary>
        public SqlDatabase()
        {
        }
        /// <summary>
        /// Destructor of Class
        /// </summary>
        ~SqlDatabase()
        {
        }
        public SqlDataReader getDataReader(string strSQL, List<SqlParameter> Params, string conStr= DefaultConnection)
        {
            try
            {
                if (conStr == "")
                    conStr = DefaultConnection;

                using (SqlConnection objConnection = new SqlConnection(conStr))
                {
                    objConnection.Open();
                    using (SqlCommand objCommand = new SqlCommand())
                    {
                        try
                        {
                            objCommand.CommandTimeout = varExecutionTimeOut;
                            objCommand.CommandType = System.Data.CommandType.Text;
                            objCommand.CommandText = strSQL;
                            objCommand.Connection = objConnection;

                            if (Params != null)
                                objCommand.Parameters.AddRange(Params.ToArray());

                            return objCommand.ExecuteReader();
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                        finally
                        {
                            // Detach the SqlParameters from the command object, so they can be used again
                            objCommand.Parameters.Clear();

                            if (objConnection.State == ConnectionState.Open)
                            {
                                objConnection.Close();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public SqlDataReader getDataReader(string strSQL, List<SqlParameter> Params, ref SqlConnection OpenedConnection)
        {
            try
            {
                if (OpenedConnection == null)
                    throw new Exception("Database connection object is not initialized.");

                if (OpenedConnection.State != ConnectionState.Open)
                    OpenedConnection.Open();

                using (SqlCommand objCommand = new SqlCommand(strSQL, OpenedConnection))
                {
                    try
                    {
                        objCommand.CommandTimeout = varExecutionTimeOut;
                        objCommand.CommandType = System.Data.CommandType.Text;
                        objCommand.CommandText = strSQL;
                        objCommand.Connection = OpenedConnection;

                        if (Params != null)
                            objCommand.Parameters.AddRange(Params.ToArray());

                        return objCommand.ExecuteReader();
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }

                    finally
                    {
                        // Detach the SqlParameters from the command object, so they can be used again
                        objCommand.Parameters.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public System.Data.DataSet getDataset(string strSQL, List<SqlParameter> Params, string conStr = DefaultConnection)
        {
            try
            {
                if (conStr == "")
                    conStr = DefaultConnection;

                using (SqlConnection objConnection = new SqlConnection(conStr))
                {
                    objConnection.Open();
                    using (SqlCommand objCommand = new SqlCommand())
                    {
                        try 
                        { 
                            System.Data.DataSet ds = new System.Data.DataSet();
                            SqlDataAdapter da;
                            objCommand.CommandTimeout = varExecutionTimeOut;
                            objCommand.CommandType = System.Data.CommandType.Text;
                            objCommand.CommandText = strSQL;
                            objCommand.Connection = objConnection;

                            if (Params != null)
                                objCommand.Parameters.AddRange(Params.ToArray());

                            da = new SqlDataAdapter(objCommand);
                            da.Fill(ds);
                            
                            ds.DataSetName = Guid.NewGuid().ToString();

                            return ds;
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                        finally
                        {
                            objCommand.Parameters.Clear();

                            if (objConnection.State == ConnectionState.Open)
                            {
                                objConnection.Close();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public DataTable getDataTable(string strQuery, List<SqlParameter> Params, string conStr = DefaultConnection)
        {
            try
            {
                if (conStr == "")
                    conStr = DefaultConnection;

                using (SqlConnection objConnection = new SqlConnection(conStr))
                {
                    objConnection.Open();
                    using (SqlCommand objCommand = new SqlCommand())
                    {
                        try
                        {
                            DataTable dt = new DataTable();
                            SqlDataAdapter da;

                            objCommand.CommandTimeout = varExecutionTimeOut;
                            objCommand.CommandType = System.Data.CommandType.Text;
                            objCommand.CommandText = strQuery;
                            objCommand.Connection = objConnection;

                            if (Params != null)
                                objCommand.Parameters.AddRange(Params.ToArray());

                            da = new SqlDataAdapter(objCommand);
                            da.Fill(dt);

                            dt.TableName = Guid.NewGuid().ToString();

                            return dt;
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                        finally
                        {
                            objCommand.Parameters.Clear();

                            if (objConnection.State == ConnectionState.Open)
                            {
                                objConnection.Close();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public object getScalar(string strSQL, List<SqlParameter> Params, string conStr = DefaultConnection)
        {
            try
            {
                if (conStr == "")
                    conStr = DefaultConnection;

                using (SqlConnection objConnection = new SqlConnection(conStr))
                {
                    objConnection.Open();
                    using (SqlCommand objCommand = new SqlCommand())
                    {
                        try
                        { 
                            objCommand.CommandTimeout = varExecutionTimeOut;
                            objCommand.CommandType = System.Data.CommandType.Text;
                            objCommand.CommandText = strSQL;
                            objCommand.Connection = objConnection;

                            if (Params != null)
                                objCommand.Parameters.AddRange(Params.ToArray());

                            return objCommand.ExecuteScalar();

                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                        finally
                        {
                            objCommand.Parameters.Clear();

                            if (objConnection.State == ConnectionState.Open)
                            {
                                objConnection.Close();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public System.Data.DataRow getDataRow(string strSQL, List<SqlParameter> Params, string conStr = DefaultConnection)
        {
            try
            {
                if (conStr == "")
                    conStr = DefaultConnection;

                using (SqlConnection objConnection = new SqlConnection(conStr))
                {
                    objConnection.Open();
                    using (SqlCommand objCommand = new SqlCommand())
                    {
                        try
                        {
                            objCommand.CommandTimeout = varExecutionTimeOut;
                            objCommand.CommandType = System.Data.CommandType.Text;
                            objCommand.CommandText = strSQL;
                            objCommand.Connection = objConnection;

                            if (Params != null)
                                objCommand.Parameters.AddRange(Params.ToArray());

                            SqlDataAdapter da = new SqlDataAdapter(objCommand);
                            DataTable dt = new DataTable();

                            da.Fill(dt);
                            
                            dt.TableName = Guid.NewGuid().ToString();

                            return dt.Rows[0];
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                        finally
                        {
                            objCommand.Parameters.Clear();

                            if (objConnection.State == ConnectionState.Open)
                            {
                                objConnection.Close();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public int RunSQL(string strSQL, List<SqlParameter> Params, string conStr = DefaultConnection)
        {
            try
            {
                if (conStr == "")
                    conStr = DefaultConnection;

                using (SqlConnection objConnection = new SqlConnection(conStr))
                {
                    objConnection.Open();
                    using (SqlCommand objCommand = new SqlCommand())
                    {
                        int intRowEffected;

                        try
                        {
                            objCommand.CommandTimeout = varExecutionTimeOut;
                            objCommand.CommandType = System.Data.CommandType.Text;
                            objCommand.CommandText = strSQL;
                            objCommand.Connection = objConnection;

                            if (Params != null)
                                objCommand.Parameters.AddRange(Params.ToArray());

                            intRowEffected = objCommand.ExecuteNonQuery();

                            return intRowEffected;
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                        finally
                        {
                            // Detach the SqlParameters from the command object, so they can be used again
                            objCommand.Parameters.Clear();

                            if (objConnection.State == ConnectionState.Open)
                            {
                                objConnection.Close();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public int RunSQL(string strSQL, List<SqlParameter> Params, ref SqlConnection OpenedConnection )
        {
            try
            {
                if (OpenedConnection == null)
                    throw new Exception("Database connection object is not initialized.");
                
                if (OpenedConnection.State != ConnectionState.Open)
                    OpenedConnection.Open();

                using (SqlCommand objCommand = new SqlCommand())
                {
                    int intRowEffected;

                    try
                    {
                        objCommand.CommandTimeout = varExecutionTimeOut;
                        objCommand.CommandType = System.Data.CommandType.Text;
                        objCommand.CommandText = strSQL;
                        objCommand.Connection = OpenedConnection;

                        if (Params != null)
                            objCommand.Parameters.AddRange(Params.ToArray());

                        intRowEffected = objCommand.ExecuteNonQuery();

                        return intRowEffected;
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                    finally
                    {
                        // Detach the SqlParameters from the command object, so they can be used again
                        objCommand.Parameters.Clear();
                    }
                }
                
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        /// <summary>
        /// Enumeration for executing sql command query as Scalar/NonQuery/Reader.
        /// </summary>
        public enum ExecuteAs
        {
            Scalar,
            NonQuery,
            Reader
        }
        /// <summary>
        /// Execute Sql Query on database
        /// </summary>
        /// <param name="varQuery">Sql Query</param>
        /// <param name="varCommandType">Sql Command type (Text/Table/StoredProcedure) by Default set to Text</param>
        /// <param name="Execute">Execution mode of Sql Query (Scalar/NonQuery/Reader).</param>
        /// <returns>Return DataReader Object in Case of ExecuteReader Mode else return Long</returns>       
        public object ExecuteCommand(String varQuery, CommandType varCommandType = CommandType.Text, ExecuteAs Execute = ExecuteAs.NonQuery, String conStr = DefaultConnection)
        {
            try
            {
                if (conStr == "")
                    conStr = DefaultConnection;

                using (SqlConnection objConnection = new SqlConnection(conStr))
                {
                    objConnection.Open();
                    using (SqlCommand objCommand = new SqlCommand(varQuery, objConnection))
                    {
                        try
                        {
                            objCommand.CommandType = CommandType.Text;
                            objCommand.Connection = objConnection;
                            objCommand.CommandTimeout = varExecutionTimeOut;
                            if (Execute == ExecuteAs.Scalar)
                                return objCommand.ExecuteScalar();
                            else if (Execute == ExecuteAs.NonQuery)
                                return objCommand.ExecuteNonQuery();
                            else
                                return objCommand.ExecuteReader();
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                        finally
                        {
                            objCommand.Parameters.Clear();

                            if (objConnection.State == ConnectionState.Open)
                            {
                                objConnection.Close();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        /// <summary>
        /// Execute Sql Query on database
        /// </summary>
        /// <param name="varQuery">Sql Query</param>
        /// <param name="Params">Provide the List of SqlParameter Class </param>
        /// <param name="conStr">Connection String</param>
        /// <param name="varCommandType">Sql Command type (Text/Table/StoredProcedure) by Default set to Text</param>
        /// <param name="Execute">Execution mode of Sql Query (Scalar/NonQuery/Reader).</param>
        /// <returns>Return DataReader Object in Case of ExecuteReader Mode else return Long</returns>       
        public object ExecuteCommand(String varQuery, List<SqlParameter> Params, String conStr = DefaultConnection, CommandType varCommandType = CommandType.Text, ExecuteAs Execute = ExecuteAs.NonQuery)
        {
            try
            {
                if (conStr == "")
                    conStr = DefaultConnection;

                using (SqlConnection objConnection = new SqlConnection(conStr))
                {
                    objConnection.Open();
                    using (SqlCommand objCommand = new SqlCommand(varQuery, objConnection))
                    {
                        try
                        {
                            objCommand.CommandType = varCommandType;
                            objCommand.Connection = objConnection;
                            objCommand.CommandTimeout = varExecutionTimeOut;
                            if (Params != null)
                                objCommand.Parameters.AddRange(Params.ToArray());

                            if (Execute == ExecuteAs.Scalar)
                                return (long)objCommand.ExecuteScalar();
                            else if (Execute == ExecuteAs.NonQuery)
                                return (long)objCommand.ExecuteNonQuery();
                            else
                                return objCommand.ExecuteReader();
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                        finally
                        {
                            objCommand.Parameters.Clear();

                            if (objConnection.State == ConnectionState.Open)
                            {
                                objConnection.Close();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public void AddParameter(ref List<SqlParameter> iParams, String ParameterName, Object ParameterValue, System.Data.SqlDbType ParameterType, int ParameterSize = 0, string SourceColumn = "", System.Data.ParameterDirection Direction = System.Data.ParameterDirection.Input)
        {
            try
            {
                SqlParameter Parameter = new SqlParameter(ParameterName, ParameterType);
                if (ParameterSize > 0)
                    Parameter.Size = ParameterSize;
                if (SourceColumn != "")
                    Parameter.SourceColumn = SourceColumn;
                if (Direction != System.Data.ParameterDirection.Input)
                    Parameter.Direction = Direction;
                if (ParameterValue == null)
                    Parameter.Value = DBNull.Value;
                else
                    Parameter.Value = ParameterValue;
                iParams.Add(Parameter);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public void AddParameter(ref List<SqlParameter> iParams, String ParameterName, System.Data.SqlDbType ParameterType, System.Data.ParameterDirection Direction = System.Data.ParameterDirection.Input)
        {
            try
            {
                SqlParameter Parameter = new SqlParameter(ParameterName, ParameterType);
                
                if (Direction != System.Data.ParameterDirection.Input)
                    Parameter.Direction = Direction;

                iParams.Add(Parameter);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public bool AppendMessage(string ConversationID,string UserID, string MessageType, string Message,DateTime? MessageTime)
        {
            List<SqlParameter> Params = new List<SqlParameter>();
            try
            {
                if(MessageTime is null)
                    MessageTime = DateTime.Now;

                string InsertQuery = "";

                if (ConversationID == "0")
                {
                    InsertQuery += "DECLARE @ConversationID INT  ";
                    InsertQuery += "INSERT INTO Conversations (UserId,Title,[Timestamp]) ";
                    InsertQuery += "VALUES(@UserID,@Title,@MessageTime) ";
                    InsertQuery += "SELECT @ConversationID=SCOPE_IDENTITY() ";
                    InsertQuery += "INSERT INTO ConversationMessages(ConversationId,[Message],MessageType,[Timestamp]) ";
                    AddParameter(ref Params, "Title", IsNull(HttpContext.Current.Session["ChatTitle"], ""), SqlDbType.NVarChar);
                }
                else
                {
                    InsertQuery += "INSERT INTO ConversationMessages(ConversationId,[Message],MessageType,[Timestamp]) ";
                    AddParameter(ref Params, "ConversationID", ConversationID, SqlDbType.Int);
                }
                InsertQuery += "VALUES(@ConversationID,@Message,@MessageType,@MessageTime) ";
                InsertQuery += " SELECT @ConversationID ";

                AddParameter(ref Params, "UserID", UserID, SqlDbType.Int);
                AddParameter(ref Params, "MessageType", MessageType, SqlDbType.Char, 50);
                AddParameter(ref Params, "Message", Message, SqlDbType.NVarChar);
                AddParameter(ref Params, "MessageTime", MessageTime, SqlDbType.DateTime);


                int varConversationID = (int) getScalar(InsertQuery, Params);

                if (varConversationID > 0)
                {
                    if(ConversationID=="0")
                    {
                        HttpContext.Current.Session["ConversationID"] = varConversationID;
                        HttpContext.Current.Session["IsNewChat"] = null;
                    }
                    return true;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                //throw ex;
                return false;
            }
            finally
            {
                Params.Clear();
                Params = null;
                GC.Collect();
            }

        }
        public DataTable LoadChat(string UserID, string ConversationID)
        {
            List<SqlParameter> Params = new List<SqlParameter>();
            try
            {
                string LoadChatQuery = " SELECT c.ConversationId, UserId, Title, MessageId, Message, MessageType, cm.[Timestamp] ";
                LoadChatQuery += " FROM Conversations c INNER JOIN ConversationMessages cm ON  cm.ConversationId = c.ConversationId ";
                LoadChatQuery += " WHERE cm.MessageType IN ('User','Chatbot') AND c.UserID=@UserID And c.ConversationID=@ConversationID ";
                LoadChatQuery += " ORDER BY cm.[TimeStamp] ";
                
                AddParameter(ref Params, "ConversationID", ConversationID, SqlDbType.Int);
                AddParameter(ref Params, "UserID", UserID, SqlDbType.Int);

                return getDataTable(LoadChatQuery, Params);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                Params.Clear();
                Params = null;
                GC.Collect();
            }
        }
        public bool DeleteChat(string UserID, string ConversationID)
        {
            List<SqlParameter> Params = new List<SqlParameter>();
            try
            {
                string DeleteQuery = "";
                DeleteQuery += "DELETE FROM CONVERSATIONS WHERE USERID=@USERID AND CONVERSATIONID=@CONVERSATIONID;";
                DeleteQuery += "DELETE FROM CONVERSATIONMESSAGES WHERE CONVERSATIONID=@CONVERSATIONID;";

                AddParameter(ref Params, "USERID", UserID, SqlDbType.Int);
                AddParameter(ref Params, "CONVERSATIONID", ConversationID, SqlDbType.Int);


                int varStatus = (int)RunSQL(DeleteQuery, Params);

                if (varStatus > 0)
                    return true;
                else
                    return false;
            }
            catch (Exception ex)
            {
                //throw ex;
                return false;
            }
            finally
            {
                Params.Clear();
                Params = null;
                GC.Collect();
            }

        }
    }
    #endregion
}
