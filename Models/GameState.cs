
namespace CodebreakerGame.Models;

public class GameState
{
    public string SecretCode { get; set; } = "";
    public int AttemptsLeft { get; set; } = 8;
    public bool IsWon { get; set; }
    public int Score { get; set; }
    public string Message { get; set; } = "";
    public List<GuessRecord> History { get; set; } = new();

    public bool IsGameOver => IsWon || AttemptsLeft == 0;
}
