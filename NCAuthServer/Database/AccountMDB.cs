using MongoDB.Driver;
using MongoDB.Driver.Builders;
using NCAuthServer.Config;
using NCAuthServer.Model.Account;

namespace NCAuthServer.Database
{
    public class AccountMDB
    {
        private static AccountMDB Instance;

        public static AccountMDB GetInstance()
        {
            return (Instance != null) ? Instance : Instance = new AccountMDB();
        }

        private MongoClient m_Client;
        private MongoServer m_Server;
        private MongoDatabase m_Database;
        private MongoCollection<Account> m_Collection;

        private string MDBTable = "accounts";

        public AccountMDB()
        {
            m_Client = new MongoClient(Configuration.Database.Url);
            m_Server = m_Client.GetServer();
            m_Database = m_Server.GetDatabase(Configuration.Database.Name);
            m_Collection = m_Database.GetCollection<Account>(MDBTable);
        }

        public Account GetAccountByLoginName(string login)
        {
            var query = Query<Account>.EQ(a => a.Login, login);
            var account = m_Collection.FindOne(query);
            return (account != null) ? account : null;
        }

        public void AddAccount(Account acc)
        {
            m_Collection.Insert(acc);
        }

        public void UpdateAccount(Account acc)
        {
            var query = Query<Account>.EQ(e => e.Id, acc.Id);
            var update = Update<Account>
                .Set(e => e.LastAddress, acc.LastAddress);
            m_Collection.Update(query, update);
        }
    }
}
