using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using System.Data;

namespace JjakaotalkSeverApp
{
    public class Receiver
    {
        byte[] buffer;
        public Socket client;
        Form1 form1;
        public bool mRunning = true;
        Thread t;
        static string strConn = "Server=localhost;Database=jjakaotalk;Uid=root;Charset=utf8";
        MySqlConnection conn = new MySqlConnection(strConn);
        static string user_id = null;
        static string user_pwd = null;
        static string reg_id = null;
        static string reg_pw = null;
        static string reg_name = null;
        static string reg_phone = null;
        static string reg_email = null;
        static string reg_nickname = null;
        User user;

        public Receiver(Form1 f1, Socket s)
        {
            buffer = new byte[1024];
            form1 = f1;
            client = s;
            conn.Open();
        }

        private void ThreadBody()
        {
            try
            {
                client.BeginReceive(buffer, 0, 1024, 0, DataReceived, null);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
        }

        private void DataReceived(IAsyncResult ar)
        {
            try
            {
                if (client.Connected)
                {
                    int byteAvailable = client.EndReceive(ar);

                    if (byteAvailable <= 0)
                    {
                        MessageBox.Show(form1.mReceiverList.Count + "");
                        client.Close();
                        form1.mReceiverList.Remove(this);
                        MessageBox.Show(form1.mReceiverList.Count + "");
                        return;
                    }
                    else if (byteAvailable > 0)
                    {
                        byte[] packetBytes = new byte[byteAvailable];
                        string data = null;
                        int bufferPosition = 0;

                        for (int i = 0; i < byteAvailable; i++)
                        {
                            byte b = buffer[i];

                            if (b == '\n')
                            {
                                byte[] encodeBytes = new byte[bufferPosition];
                                for (int j = 0; j < bufferPosition; j++)
                                    encodeBytes[j] = packetBytes[j];
                                data = Encoding.UTF8.GetString(encodeBytes);
                                LoginCheck(data);   // 로그인 정보 체크
                                RegistCheck(data);  // 회원가입 정보 체크
                                FriendCheck(data);  // 친구 정보 체크
                                ChatCheck(data);    // 채팅 메시지 체크

                                bufferPosition = 0;
                            }
                            else
                            {
                                packetBytes[bufferPosition++] = b;
                            }
                        }

                        client.BeginReceive(buffer, 0, 1024, 0, DataReceived, null);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
        }

        public void Start()
        {
            t = new Thread(new ThreadStart(ThreadBody));
            t.Start();
        }

        public void Close()
        {
            mRunning = false;
            t.Interrupt();
            conn.Close();
            conn.Dispose();
            byte[] sendBuffer = Encoding.Default.GetBytes("#,SYS,CLOSE,&\n");
            
            try
            {
                client.Send(sendBuffer);
                client.Shutdown(SocketShutdown.Both);
                client.Close();
                client.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        public void RegistCheck(string data)
        {
            if (data.StartsWith("#,REG,ID,"))
                reg_id = data.Substring(9, data.Length - 11);
            else if (data.StartsWith("#,REG,PW,"))
                reg_pw = data.Substring(9, data.Length - 11);
            else if (data.StartsWith("#,REG,NAME,"))
                reg_name = data.Substring(11, data.Length - 13);
            else if (data.StartsWith("#,REG,PHONE,"))
                reg_phone = data.Substring(12, data.Length - 14);
            else if (data.StartsWith("#,REG,EMAIL,"))
                reg_email = data.Substring(12, data.Length - 14);
            else if (data.StartsWith("#,REG,NICKNAME,"))
                reg_nickname = data.Substring(15, data.Length - 17);
            else if (data.StartsWith("#,CHECK,ID,")) // 아이디 중복 확인
            {
                string check_id = data.Substring(11, data.Length - 13);
                string sql = "select * from user where account_id = '" + check_id + "';";
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                MySqlDataReader msdr = cmd.ExecuteReader();

                if (msdr.Read())
                {
                    byte[] sendBuffer = Encoding.Default.GetBytes("#,CHECK,OK,&\n");
                    client.Send(sendBuffer);
                }
                else
                {
                    byte[] sendBuffer = Encoding.Default.GetBytes("#,CHECK,NO,&\n");
                    client.Send(sendBuffer);
                }

                msdr.Close();
                cmd.Dispose();
            }
            
            if (reg_id != null && reg_pw != null && reg_name != null && reg_phone != null && reg_email != null && reg_nickname != null)
            {
                string sql = "insert into user(account_id, account_pwd, name, phone_number, email, nick_name) values('" + reg_id + "','" +
                    reg_pw + "','" + reg_name + "','" + reg_phone + "','" + reg_email + "','" + reg_nickname + "');";
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
                cmd.Dispose();
                reg_id = null; reg_pw = null; reg_name = null;
                reg_phone = null; reg_email = null; reg_nickname = null;
            }
        }

        public void LoginCheck(string data)   // 로그인 정보 확인 후 허가 메시지 발신
        {
            if (data.StartsWith("#,LOG,ID,"))
            {
                user_id = data.Substring(9, data.Length - 11);
            }
            else if (data.StartsWith("#,LOG,PW,"))
            {
                user_pwd = data.Substring(9, data.Length - 11);

                if (user_id.ToString().Length > 2 && user_pwd.ToString().Length > 5)
                {
                    string sql = "select * from user where account_id = '" + user_id + "' and account_pwd = '" +
                        user_pwd + "';";
                    MySqlDataAdapter msda = new MySqlDataAdapter(sql, conn);
                    DataSet ds = new DataSet();
                    msda.Fill(ds, "Table");
                    
                    if (ds.Tables[0].Rows.Count != 0)    // Mysql에 들어있는 로그인 정보와 일치하는 정보가 있으면 로그인 허가
                    {
                        DataRow r = ds.Tables[0].Rows[0];
                        user = new User(int.Parse(r["_id"].ToString()), r["account_id"].ToString(), r["name"].ToString(), r["phone_number"].ToString(), r["email"].ToString(), r["nick_name"].ToString());
                        
                        byte[] sendBuffer = Encoding.Default.GetBytes("#,LOG,OK,ID," + r["_id"].ToString() +
                            ",NAME," + r["name"].ToString() + ",PHONENUMBER," + r["phone_number"] + ",EMAIL," +
                            r["email"].ToString() + ",NICKNAME," + r["nick_name"].ToString() + ",&\n");
                        client.Send(sendBuffer);
                    }
                    else
                    {
                        byte[] sendBuffer = Encoding.Default.GetBytes("#,LOG,NO,&\n");
                        client.Send(sendBuffer);
                    }

                    ds.Clear();
                    ds.Dispose();
                    msda.Dispose();
                }
            }
        }

        public void FriendCheck(string data)
        {
            if (data.StartsWith("#,REQUEST,NEWFRIEND,"))
            {
                string user_id = data.Substring(20, data.Length - 22);
                string sql = "select * from friend where friend_id = '" + user_id + "';";
                DataSet ds = new DataSet();
                MySqlDataAdapter msda = new MySqlDataAdapter(sql, conn);
                msda.Fill(ds, "Table");

                foreach (DataRow r in ds.Tables[0].Rows)
                {
                    string friend_id = r["user_id"].ToString();
                    string _sql = "select * from friend where user_id = '" + user_id + "' and friend_id = '" +
                        friend_id + "';";
                    DataSet _ds = new DataSet();
                    MySqlDataAdapter _msda = new MySqlDataAdapter(_sql, conn);
                    _msda.Fill(_ds, "Table");
                    
                    if (_ds.Tables[0].Rows.Count == 0)
                    {
                        String __sql = "select * from user where account_id = '" + friend_id + "';";
                        DataSet __ds = new DataSet();
                        MySqlDataAdapter __msda = new MySqlDataAdapter(__sql, conn);
                        __msda.Fill(__ds, "Table");

                        foreach (DataRow _r in __ds.Tables[0].Rows)
                        {
                            byte[] sendBytes = Encoding.Default.GetBytes("#,NEWFRIEND,ID," + friend_id +
                                ",ACCOUNT_ID," + _r["account_id"].ToString() + ",NAME," + _r["name"].ToString() +
                                ",PHONENUMBER," + _r["phone_number"].ToString() + ",EMAIL," + _r["email"].ToString()
                                + ",NICKNAME," + _r["nick_name"].ToString() + ",&\n");
                            Console.WriteLine(Encoding.Default.GetString(sendBytes));
                            client.Send(sendBytes);
                        }

                        __msda.Dispose();
                        __ds.Clear();
                        __ds.Dispose();
                    }

                    _msda.Dispose();
                    _ds.Clear();
                    _ds.Dispose();
                }

                msda.Dispose();
                ds.Clear();
                ds.Dispose();
            }
            else if (data.StartsWith("#,REQUEST,FRIENDS,"))
            {
                string user_id = data.Substring(18, data.Length - 20);
                string sql = "select * from user where account_id in (select friend_id from friend where user_id = '" +
                    user_id + "') order by name asc;";
                DataSet ds = new DataSet();
                MySqlDataAdapter msda = new MySqlDataAdapter(sql, conn);
                msda.Fill(ds, "Table");

                if (ds.Tables[0].Rows.Count == 0)
                {
                    byte[] sendBytes = Encoding.Default.GetBytes("#,FRIENDS,NO,&\n");
                    client.Send(sendBytes);
                }
                else
                {
                    foreach (DataRow r in ds.Tables[0].Rows)
                    {
                        byte[] sendBytes = Encoding.Default.GetBytes("#,FRIENDS,ID," + r["_id"].ToString() + ",ACCOUNT_ID," + r["account_id"].ToString() +
                            ",PHONE_NUMBER," + r["phone_number"].ToString() + ",NAME," + r["name"].ToString() + ",EMAIL," + r["email"].ToString() +
                            ",NICK_NAME," + r["nick_name"].ToString() + ",&\n");
                        client.Send(sendBytes);
                    }
                }

                msda.Dispose();
                ds.Clear();
                ds.Dispose();
            }
            else if (data.StartsWith("#,SEARCH,ID,"))
            {
                string add_id = data.Substring(12, data.Length - 14);
                string sql = "select * from user where account_id = '" + add_id + "';";

                DataSet ds = new DataSet();
                MySqlDataAdapter msda = new MySqlDataAdapter(sql, conn);
                msda.Fill(ds);

                if (ds.Tables[0].Rows.Count == 0)
                {
                    byte[] sendBytes = Encoding.Default.GetBytes("#,SEARCH,NO,&\n");
                    client.Send(sendBytes);
                }
                else
                {
                    foreach (DataRow r in ds.Tables[0].Rows)
                    {
                        byte[] sendBytes = Encoding.Default.GetBytes("#,SEARCH,NAME," + r["name"] + ",&\n");
                        client.Send(sendBytes);
                    }
                }

                msda.Dispose();
                ds.Clear();
                ds.Dispose();
            }
            else if (data.StartsWith("#,REQUEST,USERID,"))
            {
                string user_id = data.Substring(17, data.IndexOf(",FRIEND,ADD,") - 17);
                string add_id = data.Substring(data.IndexOf(",FRIEND,ADD,") + 12, data.Length - (data.IndexOf(",FRIEND,ADD,") + 14));
                string sql = "insert into friend(user_id, friend_id) values('" + user_id + "','" + add_id + "');";
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
                cmd.Dispose();
            }
        }

        public void ChatCheck(string data)
        {
            if (data.StartsWith("#,REQUEST,CHATROOMS,"))
            {
                Console.WriteLine(data);
                int user_id = int.Parse(data.Substring(20, data.IndexOf(",&") - 20));
                string sql = "select * from chat_rooms";
                MySqlDataAdapter msda = new MySqlDataAdapter(sql, conn);
                DataSet ds = new DataSet();
                msda.Fill(ds, "Table");
                
                foreach (DataRow r in ds.Tables[0].Rows)
                {
                    string members = r["members"].ToString();
                    string members_sub = members.Substring(members.IndexOf("[") + 1,
                        members.IndexOf("]") - (members.IndexOf("[") + 1));
                    string[] members_split = members_sub.Split(',');
                    
                    for (int i = 0; i < members_split.Length; i++)
                    {
                        if (user_id == int.Parse(members_split[i]))
                        {
                            byte[] sendBuffer = Encoding.Default.GetBytes("#,CHATROOMS,ID," + r["_id"]
                                + ",MEMBERS," + members + ",&\n");
                            client.Send(sendBuffer);
                        }
                    }
                }
            }
            else if (data.StartsWith("#,NEW,CHATROOM,MEMBERS,"))
            {
                int index_left = data.IndexOf("[");
                int index_right = data.IndexOf("]") + 1;
                string members = data.Substring(index_left, index_right - index_left);
                int chat_id;
                string msg = data.Substring(data.IndexOf(",MSG,") + 5, data.IndexOf(",&") - (data.IndexOf(",MSG,") + 5));
                string sql = "insert into chat_rooms(members) values('" + members + "');";
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
                cmd.Dispose();

                sql = "select * from chat_rooms where members = '" + members + "';";
                MySqlDataAdapter msda = new MySqlDataAdapter(sql, conn);
                DataSet ds = new DataSet();
                msda.Fill(ds, "Table");

                foreach (DataRow r in ds.Tables[0].Rows)
                {
                    chat_id = int.Parse(r["_id"].ToString());
                    byte[] sendBuffer = Encoding.Default.GetBytes("#,NEW,CHATROOM,ID," + chat_id + ",&\n");
                    client.Send(sendBuffer);
                }

                msda.Dispose();
                ds.Clear();
                ds.Dispose();
            }

            else if (data.StartsWith("#,CHAT,ID,"))
            {
                int chat_id = int.Parse(data.Substring(10, data.IndexOf(",SENDER_ID,") - 10));
                int sender_id = int.Parse(data.Substring(data.IndexOf(",SENDER_ID,") + 11,
                    data.IndexOf(",MEMBERS,") - (data.IndexOf(",SENDER_ID,") + 11)));
                string members = data.Substring(data.IndexOf(",MEMBERS,") + 9,
                    data.IndexOf(",MSG,") - (data.IndexOf(",MEMBERS,") + 9));
                string message = data.Substring(data.IndexOf(",MSG,") + 5,
                    data.IndexOf(",&") - (data.IndexOf(",MSG,") + 5));

                string members_sub = members.Substring(members.IndexOf("[") + 1,
                    members.IndexOf("]") - (members.IndexOf("[") + 1));
                string[] members_split = members_sub.Split(',');
                
                for (int i = 0; i < form1.mReceiverList.Count; i++)
                {
                    for (int j = 0; j < members_split.Length; j++)
                    {
                        if (sender_id == int.Parse(members_split[j]))   // member 중 보낸 사람은 제외시키고 메세지를 전달
                            continue;
                        if (form1.mReceiverList[i].user.id == int.Parse(members_split[j]))
                        {
                            byte[] send_data = Encoding.Default.GetBytes(data + "\n");
                            form1.mReceiverList[i].client.Send(send_data);
                        }
                    }
                }
            }
        }
    }
}