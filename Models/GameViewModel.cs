using System.ComponentModel.DataAnnotations;

namespace CodebreakerGame.Models;

public class GameViewModel
{
    [Required]
    [RegularExpression(@"^\d{4}$", ErrorMessage = "Enter exactly four digits.")]
    public string Guess { get; set; } = "";

    public int AttemptsLeft { get; set; }
    public int MaxAttempts { get; set; } = 8;
    public string Message { get; set; } = "";
    public bool IsWon { get; set; }
    public bool IsGameOver { get; set; }
    public int Score { get; set; }
    public List<GuessRecord> History { get; set; } = new();
}
