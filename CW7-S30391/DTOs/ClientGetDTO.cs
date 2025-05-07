using System.ComponentModel.DataAnnotations;

namespace CW7_S30391.Models.DTOs;

public class ClientGetDTO
{
    [Key]
    public int IdClient { get; set; }
    
    [MaxLength(120)]
    public string FirstName { get; set; }
    
    [MaxLength(120)]
    public string LastName { get; set; }
    
    public List<ClientTripGetDTO>? Trips { get; set; }
}