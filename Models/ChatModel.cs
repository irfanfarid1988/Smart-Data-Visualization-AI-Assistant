using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;

namespace SDVA.Models
{
    public class ChatViewModel
    {
        public List<Models.ChatHistory> HistoryList { get; set; }
    }
    public class ChatHistory
    {
        public int ConversationId { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; }
        public DateTime TimeStamp { get; set; }
    }

    public class Message
    {
        public int MessageId { get; set; }
        public int ConversationId { get; set; }
        public string ChatMessage { get; set; }
        public string MessageType { get; set; }
        public string TimeStamp { get; set; }
    }

}