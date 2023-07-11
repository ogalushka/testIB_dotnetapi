using System.Data.SqlClient;
using System.Reflection;
using tracker.Repository.Entity;

namespace tracker.Repository
{
    public class TrackRepository
    {
        private readonly string Connection;
        private readonly string DatabaseName;

        public TrackRepository(IConfiguration configuration)
        {
            Connection = configuration.GetValue<string>("DBConnection") ?? throw new Exception("DBConnection not set in config");
            DatabaseName = configuration.GetValue<string>("DBName") ?? throw new Exception("DBName not set in config");
        }

        //TODO memory optimization? with IEnumerator
        public IAsyncEnumerator<TrackRecord> GetTracks(string imei)
        {
            var query = "SELECT latitude, longitude, date_track FROM TrackLocation WHERE IMEI = @Imei ORDER BY date_track ASC;";
            var param = new Dictionary<string, object>
            {
                { "@Imei", imei }
            };
            return ExecuteQuerry(query, param);
        }
        
        private async IAsyncEnumerator<TrackRecord> ExecuteQuerry(string query, Dictionary<string, object> queryParams)
        {
            using var connection = new SqlConnection(Connection);
            await connection.OpenAsync();
            await connection.ChangeDatabaseAsync(DatabaseName);
            using var sqlCommand = new SqlCommand(query, connection);
            foreach (var pair in queryParams)
            {
                sqlCommand.Parameters.AddWithValue(pair.Key, pair.Value);
            }
            using var sqlReader = await sqlCommand.ExecuteReaderAsync();

            var fields = new Dictionary<string, Field>();
            while (await sqlReader.ReadAsync())
            {
                fields.Clear();
                for (var i = 0; i < sqlReader.FieldCount; i++)
                {
                    fields.Add(sqlReader.GetName(i), new Field { Type = sqlReader.GetFieldType(i), Value = sqlReader.GetValue(i) });
                }
                yield return CastRecord<TrackRecord>(fields);
            }
        }

        /*
        //TODO memory optimization? with IEnumerator
        public async Task<IEnumerable<TrackRecord>> GetTracks(string imei)
        {
            var query = "SELECT latitude, longitude, date_track FROM TrackLocation WHERE IMEI = @Imei ORDER BY date_track ASC;";
            var param = new Dictionary<string, object>
            {
                { "@Imei", imei }
            };
            var results = await ExecuteQuerry(query, param);
            var trackRecords = results.Select(r => CastRecord<TrackRecord>(r)).ToArray();
            return trackRecords;
        }
        
        private async Task<List<Dictionary<string, Field>>> ExecuteQuerry(string query, Dictionary<string, object> queryParams)
        {
            var records = new List<Dictionary<string, Field>>();
            
            using var connection = new SqlConnection(Connection);
            await connection.OpenAsync();
            await connection.ChangeDatabaseAsync(DatabaseName);
            using var sqlCommand = new SqlCommand(query, connection);
            foreach (var pair in queryParams)
            {
                sqlCommand.Parameters.AddWithValue(pair.Key, pair.Value);
            }
            using var sqlReader = await sqlCommand.ExecuteReaderAsync();
            while (sqlReader.Read())
            {
                var fields = new Dictionary<string, Field>();
                for (var i = 0; i < sqlReader.FieldCount; i++)
                {
                    fields.Add(sqlReader.GetName(i), new Field { Type = sqlReader.GetFieldType(i), Value = sqlReader.GetValue(i) });
                }
                records.Add(fields);
            }

            return records;
        }
        */
        
        private class Field
        {
            public Type Type = typeof(int);
            public object Value = 0;

            public Field()
            {
            }
        }

        private static T CastRecord<T>(Dictionary<string, Field> recrodFields) where T : new()
        {
            var result = new T();

            var typeFields = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (var typeField in typeFields)
            {
                if (!recrodFields.TryGetValue(typeField.Name, out var recordField))
                {
                    continue;
                }

                if (typeField.PropertyType != recordField.Type)
                {
                    continue;
                }

                typeField.SetValue(result, recordField.Value);
            }

            return result;
        }
    }
}
