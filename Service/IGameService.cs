
using CodebreakerGame.Models;

namespace CodebreakerGame.Services;

public interface IGameService
{
    GameState StartNewGame();

    GameState SubmitGuess(GameState game, string guess);
}
