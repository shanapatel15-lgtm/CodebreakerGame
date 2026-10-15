
using CodebreakerGame.Models;

namespace CodebreakerGame.Services;

public class GameService : IGameService
{
    private const int MaxAttempts = 8;

    public GameState StartNewGame()
    {
        string secret = string.Concat(
            Enumerable.Range(0, 10)
                .OrderBy(_ => Random.Shared.Next())
                .Take(4));

        return new GameState
        {
            SecretCode = secret,
            AttemptsLeft = MaxAttempts,
            Message = "A secret code is waiting. Can you crack it?"
        };
    }

    public GameState SubmitGuess(GameState game, string guess)
    {
        if (game.IsGameOver)
        {
            game.Message = "This game has ended. Start a new game!";
            return game;
        }

        if (string.IsNullOrWhiteSpace(guess) ||
            guess.Length != 4 ||
            !guess.All(char.IsAsciiDigit))
        {
            game.Message = "Enter exactly four digits.";
            return game;
        }

        if (guess.Distinct().Count() != 4)
        {
            game.Message = "Use four different digits!";
            return game;
        }

        int exact = 0;
        int misplaced = 0;

        for (int i = 0; i < 4; i++)
        {
            if (guess[i] == game.SecretCode[i])
            {
                exact++;
            }
            else if (game.SecretCode.Contains(guess[i]))
            {
                misplaced++;
            }
        }

        game.History.Add(new GuessRecord
        {
            Guess = guess,
            ExactMatches = exact,
            MisplacedMatches = misplaced
        });

        game.AttemptsLeft--;

        if (exact == 4)
        {
            game.IsWon = true;
            game.Score = 500 + game.AttemptsLeft * 100;
            game.Message = $"You cracked the code! Score: {game.Score}";
        }
        else if (game.AttemptsLeft == 0)
        {
            game.Message =
                $"Game over! The secret code was {game.SecretCode}.";
        }
        else
        {
            game.Message =
                $"Exact matches: {exact} | Misplaced matches: {misplaced}";
        }

        return game;
    }
}
