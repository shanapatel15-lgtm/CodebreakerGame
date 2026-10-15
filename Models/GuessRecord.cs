namespace CodebreakerGame.Models;

public class GuessRecord
{
    public string Guess { get; set; } = "";
    public int ExactMatches { get; set; }
    public int MisplacedMatches { get; set; }
}
