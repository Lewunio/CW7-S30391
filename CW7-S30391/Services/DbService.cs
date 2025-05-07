using System.Data;
using CW7_S30391.Exceptions;
using CW7_S30391.Models.DTOs;
using Microsoft.Data.SqlClient;

namespace CW7_S30391.Services;

public interface IDbService
{
    public Task<IEnumerable<TripGetDTO>> GetTripsDetailsAsync();
    public Task<IEnumerable<ClientTripGetDTO>> GetTripsDetailsOfClientAsync(int id);
}

public class DbService(IConfiguration? config) : IDbService
{
    /* Helper method */
    private async Task<SqlConnection> GetConnectionAsync()
    {
        var connection = new SqlConnection(config.GetConnectionString("Default"));
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        return connection;
    }

    public async Task<IEnumerable<TripGetDTO>> GetTripsDetailsAsync()
    {
        var resultDict = new Dictionary<int,TripGetDTO>();
        
        await using var connection = await GetConnectionAsync();

        // pobiera w wierszu wszystkie dane z Tabeli Trip i z Country o id w jednym rekordzie tabeli wiele-wiele
        const string sql = """
                           SELECT T.IdTrip, T.Name, T.Description, T.DateFrom, T.DateTo, T.MaxPeople, C.IdCountry, C.Name FROM Trip T
                           LEFT JOIN Country_Trip CT on CT.IdTrip = T.IdTrip
                           LEFT JOIN Country C on C.IdCountry = CT.IdCountry
                           ;
                           """;
        await using var command = new SqlCommand(sql, connection);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var idTrip = reader.GetInt32(0);
            if (!resultDict.TryGetValue(idTrip, out var result))
            {
                result = new TripGetDTO
                {
                    IdTrip = idTrip,
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    DateFrom = reader.GetDateTime(3),
                    DateTo = reader.GetDateTime(4),
                    MaxPeople = reader.GetInt32(5),
                    Countries = []
                };
                resultDict.Add(idTrip, result);
            }

            if (!await reader.IsDBNullAsync(6))
            {
                result.Countries.Add(new CountryGetDTO
                {
                    IdCountry = reader.GetInt32(6),
                    Name = reader.GetString(7)
                });
            }
            
        }

        return resultDict.Values;
    }

    public async Task<IEnumerable<ClientTripGetDTO>> GetTripsDetailsOfClientAsync(int id)
    {
        var trips = new List<ClientTripGetDTO>();
        
        await using var connection = await GetConnectionAsync();
        //pobiera wszsytkie dane z tabeli Trip i Client Trip, gdzie IdClient jest rowne podanemu Id
        const string sql = """
                           SELECT T.IdTrip, T.Name, T.Description, T.DateFrom, T.DateTo, T.MaxPeople, CT.RegisteredAt, CT.PaymentDate FROM Client C
                           LEFT JOIN Client_Trip CT on CT.IdClient = C.IdClient
                           LEFT JOIN Trip T on T.IdTrip = CT.IdTrip
                           WHERE C.IdClient = @id
                           ;
                           """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            trips.Add(new ClientTripGetDTO
            {
                IdTrip = reader.GetInt32(0),
                RegisteredAt = reader.GetInt32(6),
                PaymentDate = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                DateFrom = reader.GetDateTime(3),
                DateTo = reader.GetDateTime(4),
                MaxPeople = reader.GetInt32(5),
            });
        }
        if (trips.Count == 0)
        {
            throw new NotFoundException("Trip or Client not found");
        }
        return trips;

    }
}