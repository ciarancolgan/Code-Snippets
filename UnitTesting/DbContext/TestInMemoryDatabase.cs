using ServiceStack.OrmLite;
using System.Collections.Generic;
using System.Data;

namespace API.Common.Testing
{
    /// <summary>
    /// It is not possible to directly mock the Dapper commands. There is a Nuget package called Moq.Dapper, but this approach doesnt need it.
    /// It is not possible to mock In-Memory properties of a .NET Core DbContext such as the IDbConnection - i.e. the bit we actually want for Dapper queries.
    /// for this reason, we need to use a different In-Memory database and load entities into it to query. Approach as per: https://mikhail.io/2016/02/unit-testing-dapper-repositories/
    /// </summary>
    public class TestInMemoryDatabase
    {
        private readonly OrmLiteConnectionFactory dbFactory =
            new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider);

        public IDbConnection OpenConnection() => this.dbFactory.OpenDbConnection();

        public void Insert<T>(IEnumerable<T> items)
        {
            using (var db = this.OpenConnection())
            {
                db.CreateTableIfNotExists<T>();
                foreach (var item in items)
                {
                    db.Insert(item);
                }
            }
        }
    }
}
