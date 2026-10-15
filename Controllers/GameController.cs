
using System.Text.Json;
using CodebreakerGame.Models;
using CodebreakerGame.Services;
using Microsoft.AspNetCore.Mvc;

namespace CodebreakerGame.Controllers;

public class GameController : Controller
{
    private const string GameStateKey = "CodebreakerGameState";

    private readonly IGameService _gameService;

    public GameController(IGameService gameService)
    {
        _gameService = gameService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var game = GetGameState();
        return View(ToViewModel(game));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Guess(GameViewModel model)
    {
        var game = GetGameState();

        if (!ModelState.IsValid)
        {
            model.Message = "Enter exactly four digits.";
            return View("Index", ToViewModel(game, model.Guess, model.Message));
        }

        game = _gameService.SubmitGuess(game, model.Guess);
        SaveGameState(game);

        return View("Index", ToViewModel(game));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Restart()
    {
        var game = _gameService.StartNewGame();
        SaveGameState(game);

        return RedirectToAction(nameof(Index));
    }

    private GameState GetGameState()
    {
        var json = HttpContext.Session.GetString(GameStateKey);

        if (!string.IsNullOrEmpty(json))
        {
            var game = JsonSerializer.Deserialize<GameState>(json);

            if (game != null && game.SecretCode.Length == 4)
                return game;
        }

        var newGame = _gameService.StartNewGame();
        SaveGameState(newGame);

        return newGame;
    }

    private void SaveGameState(GameState game)
    {
        var json = JsonSerializer.Serialize(game);
        HttpContext.Session.SetString(GameStateKey, json);
    }

    private static GameViewModel ToViewModel(
        GameState game,
        string guess = "",
        string? message = null)
    {
        return new GameViewModel
        {
            Guess = guess,
            AttemptsLeft = game.AttemptsLeft,
            MaxAttempts = 8,
            Message = message ?? game.Message,
            IsWon = game.IsWon,
            IsGameOver = game.IsGameOver,
            Score = game.Score,
            History = game.History
        };
    }
}
