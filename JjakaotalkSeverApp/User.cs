using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JjakaotalkSeverApp
{
    class User
    {
        public int id;
        public string account_id;
        public string name;
        public string phone_number;
        public string email;
        public string nick_name;

        public User(int id, string account_id, string name, string phone_number, string email, string nick_name)
        {
            this.id = id;
            this.account_id = account_id;
            this.name = name;
            this.phone_number = phone_number;
            this.email = email;
            this.nick_name = nick_name;
        }
    }
}
