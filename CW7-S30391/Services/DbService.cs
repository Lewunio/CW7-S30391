using System.Data;
using CW7_S30391.Exceptions;
using CW7_S30391.Models;
using CW7_S30391.Models.DTOs;
using Microsoft.Data.SqlClient;

namespace CW7_S30391.Services;

public interface IDbService
{
    public Task<IEnumerable<TripGetDTO>> GetTripsDetailsAsync();
    public Task<IEnumerable<ClientTripGetDTO>> GetTripsDetailsOfClientAsync(int id);
    public Task<Client> CreateClientAsync(ClientCreateDTO body);
    public Task PutTripToClientAsync(int id, int tripId);
    public Task RemoveTripFromClientAsync(int id, int tripId);
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
        //walidacja czy istnieje klient o podanym id
        const string sqlVal = "select 1 from Client where IdClient = @id";
        await using var commandVal = new SqlCommand(sqlVal, connection);
        commandVal.Parameters.AddWithValue("@id", id);
        await connection.OpenAsync();
        await using (var readerVal = await commandVal.ExecuteReaderAsync())
        {
            if (!readerVal.HasRows)
            {
                throw new NotFoundException($"Client with id: {id} does not exist");
            }
        }
        
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
            throw new NotFoundException("Client has no trips");
        }
        return trips;

    }

    public async Task<Client> CreateClientAsync(ClientCreateDTO body)
    {
        await using var connection = await GetConnectionAsync();
        //wstaw Clienta do tabeli i id tego obiektu
        const string sql = "insert into Client (FirstName, LastName, Email, Telephone, Pesel) values (@FirstName, @LastName, @Email, @Telephone, @Pesel);Select SCOPE_IDENTITY()";
        await using var command = new SqlCommand(sql,connection);
        command.Parameters.AddWithValue("@FirstName", body.FirstName);
        command.Parameters.AddWithValue("@LastName", body.LastName);
        command.Parameters.AddWithValue("@Email", body.Email);
        command.Parameters.AddWithValue("@Telephone", body.Telephone);
        command.Parameters.AddWithValue("@Pesel", body.Pesel);
        var id = Convert.ToInt32(await command.ExecuteScalarAsync());
        return new Client
        {
            IdClient = id,
            FirstName = body.FirstName,
            LastName = body.LastName,
            Email = body.Email,
            Telephone = body.Telephone,
            Pesel = body.Pesel
        };
    }

    public async Task PutTripToClientAsync(int id, int tripId)
    {
        await using var connection = await GetConnectionAsync();
        //sprawdz czy jest taki klient
        var clientCmd = new SqlCommand("SELECT 1 FROM Client WHERE IdClient = @ClientId", connection);
        clientCmd.Parameters.AddWithValue("@ClientId", id);
        if (clientCmd.ExecuteScalar() == null)
            throw new NotFoundException($"Client {id} does not exist");
        
        //sprawdz czy jest taka wycieczka i pobierz liczbe max ludzi
        var tripCmd = new SqlCommand("SELECT MaxPeople FROM Trip WHERE IdTrip = @TripId", connection);
        tripCmd.Parameters.AddWithValue("@TripId", tripId);
        var maxPeopleObj = tripCmd.ExecuteScalar();
        if (maxPeopleObj == null)
            throw new NotFoundException($"Trip {tripId} does not exist");
        int maxPeople = Convert.ToInt32(maxPeopleObj);
        
        //pobierz liczbe osob zapisana na wycieczke do sprawdzenia
        var countCmd = new SqlCommand("SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @TripId", connection);
        countCmd.Parameters.AddWithValue("@TripId", tripId);
        int currentCount = countCmd.ExecuteScalar() == null ? 0 : (int)countCmd.ExecuteScalar();
        
        if (currentCount >= maxPeople)
            throw new BadRequestException("Osiągnięto maksymalną liczbę uczestników.");
        
        //sprawdz czy nie jest juz zapisany
        var checkCmd = new SqlCommand("SELECT 1 FROM Client_Trip WHERE IdClient = @ClientId AND IdTrip = @TripId", connection);
        checkCmd.Parameters.AddWithValue("@ClientId", id);
        checkCmd.Parameters.AddWithValue("@TripId", tripId);
        if (checkCmd.ExecuteScalar() != null)
            throw new BadRequestException($"Klient {id} już zapisany na tę wycieczkę.");
        
        //dodaj do tabeli Client_Trip
        var cmd = new SqlCommand("INSERT INTO Client_Trip (IdClient, IdTrip,RegisteredAt) VALUES (@ClientId, @TripId, @RegisteredAt)", connection);
        cmd.Parameters.AddWithValue("@ClientId", id);
        cmd.Parameters.AddWithValue("@TripId", tripId);
        cmd.Parameters.AddWithValue("@RegisteredAt", DateTime.Now.ToString("yyyyMMdd"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveTripFromClientAsync(int id, int tripId)
    {
        await using var connection = await GetConnectionAsync();
        //usun z tabeli Client_trip
        const string sql = "DELETE FROM Client_Trip WHERE IdClient = @ClientId AND IdTrip = @TripId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ClientId", id);
        command.Parameters.AddWithValue("@TripId", tripId);
        var numOfRows = await command.ExecuteNonQueryAsync();
        if (numOfRows == 0)
        {
            throw new NotFoundException($"Client {id} has no trip with id {tripId}");
        }
    }
}