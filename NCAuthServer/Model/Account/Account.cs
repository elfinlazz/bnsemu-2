using MongoDB.Bson;

namespace NCAuthServer.Model.Account
{
    public class Account
    {
        public ObjectId Id { get; set; }

        public string Login { get; set; }

        public string Password { get; set; }

        public string LastAddress { get; set; }
    }
}
