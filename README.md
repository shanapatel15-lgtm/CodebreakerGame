
# CodebreakerGame Developer Documentation

## 1. Purpose and scope

CodebreakerGame is a small server-rendered ASP.NET Core MVC application targeting .NET 8. It implements a single-player four-digit code-breaking game:

- A new game generates a secret made of four different digits.
- The player has eight attempts.
- Each valid guess reports exact and misplaced matches.
- The game and its history are stored in the user's ASP.NET Core session.
- There is no database, account system, API, persistent leaderboard, or multi-player feature.

This document explains the current architecture and the engineering decisions represented in the code. It is intended to help a developer understand the application before changing it. It describes the implementation as it exists, including known limitations and cleanup opportunities.

## 2. Technology and runtime

| Area | Current choice |
| --- | --- |
| Application type | ASP.NET Core MVC web application |
| Target framework | .NET 8 (`net8.0`) |
| Project SDK | `Microsoft.NET.Sdk.Web` |
| Language configuration | Nullable reference types enabled; implicit usings enabled |
| UI | Razor view rendered on the server |
| State storage | ASP.NET Core session backed by distributed memory cache |
| Persistence | None |
| Dependency injection | Built-in ASP.NET Core dependency injection |
| Authentication | None |
| External packages | None declared in the project file |

The project is intentionally small. The framework supplies routing, model binding, validation, Razor rendering, dependency injection, session middleware, antiforgery protection, and HTTPS middleware without requiring a separate application framework.

## 3. Architectural style

The application uses a conventional MVC architecture with a small service layer:

```text
Browser
  |
  | GET /Game/Index
  | POST /Game/Guess
  | POST /Game/Restart
  v
GameController  ---->  IGameService / GameService
  |                         |
  |                         +--> creates and evaluates GameState
  |
  +--> Session serializes GameState as JSON
  |
  +--> GameViewModel
			|
			v
	   Views/Game/Index.cshtml
```

### 3.1 Why MVC is used

MVC separates the HTTP/UI concerns from the game rules:

- **Model** represents data used by the game and by the view.
- **View** renders the HTML and collects input from the player.
- **Controller** coordinates HTTP requests, validation, session state, and view selection.
- **Service** owns game creation and guess evaluation so the rules are not embedded in Razor markup or request plumbing.

This is an appropriate level of architecture for a small form-based application. Introducing a repository, database abstraction, CQRS layer, or Web API would add complexity without solving a current requirement.

### 3.2 Responsibility boundaries

#### `Program.cs`

`Program.cs` is the composition root. It configures framework services and the HTTP request pipeline:

- Registers MVC controllers and views.
- Registers distributed memory cache and session.
- Registers `IGameService` with `GameService` using scoped lifetime.
- Enables HTTPS redirection, static files, routing, session, authorization, and production exception handling.
- Defines the default route as `{controller=Game}/{action=Index}/{id?}`.

The service registration uses the interface rather than the concrete class so the controller depends on an abstraction. That makes the game service replaceable in tests or in a future implementation.

#### `Controllers/GameController.cs`

`GameController` is the HTTP orchestration layer. It should not be treated as the source of game rules. Its responsibilities are:

1. Read or initialize the current session game.
2. Accept model-bound form input.
3. Handle invalid model state.
4. Delegate valid guesses to `IGameService`.
5. Save the updated game back to session.
6. Map internal state into `GameViewModel`.
7. Select a view or redirect after an action.

The controller uses constructor injection, which makes the dependency explicit and avoids creating `GameService` directly.

#### `Service/GameService.cs`

`GameService` contains the game rules:

- Generates a new four-digit secret.
- Enforces the eight-attempt limit.
- Validates the guess again at the domain/service boundary.
- Calculates exact and misplaced matches.
- Adds a `GuessRecord` to history.
- Decrements attempts.
- Sets win, loss, progress, and score values.

The service mutates the supplied `GameState` and returns it. This is simple and suitable for the current in-memory session model. If the application later adopts persistence or concurrency, an immutable result object or an explicit game aggregate may make state transitions easier to reason about.

#### Model classes

- `GameState` is the complete server-side state of an active game.
- `GameViewModel` is the controller-to-view contract. It intentionally contains display and input fields rather than exposing the secret code.
- `GuessRecord` represents one evaluated guess and its results.

Using a separate view model is an important boundary. It prevents the Razor view from binding directly to the internal game state and avoids rendering `SecretCode` to the browser.

#### `Views/Game/Index.cshtml`

The view is responsible for presentation:

- Displays attempts, score, status text, and history.
- Displays the guess form only while the game is active.
- Displays a win or loss state after the game ends.
- Provides a restart form.
- Includes antiforgery tokens in both POST forms.
- Uses HTML input constraints for user experience, while server-side validation remains authoritative.

The view currently keeps its CSS inline. This keeps the sample self-contained, but a larger application would normally move styles to a static stylesheet.

## 4. Request and state flows

### 4.1 First page load: `GET /Game/Index`

1. Routing selects `GameController.Index`.
2. `Index` calls `GetGameState`.
3. `GetGameState` reads the `CodebreakerGameState` value from session.
4. If valid JSON exists and the secret has length four, it is deserialized into `GameState`.
5. If there is no usable state, the controller calls `StartNewGame`, saves the result, and uses it for the response.
6. `ToViewModel` maps the state to `GameViewModel`.
7. Razor renders `Views/Game/Index.cshtml`.

The first visit therefore creates a game lazily rather than requiring a separate initialization endpoint.

### 4.2 Guess submission: `POST /Game/Guess`

1. The form posts a `GameViewModel` and an antiforgery token.
2. Model binding populates `Guess`.
3. `[ValidateAntiForgeryToken]` protects the request against cross-site request forgery.
4. Data annotations validate the submitted shape (`Required` and exactly four digits).
5. If model validation fails, the current game is not changed. The view is rendered again with the validation message and submitted guess.
6. If model validation succeeds, the controller calls `IGameService.SubmitGuess`.
7. The service applies domain validation, evaluates the guess, updates history and attempts, and sets the message.
8. The controller serializes the new state into session and renders the updated view.

The application uses a render-after-submit flow rather than redirect-after-post for guesses. This makes the current response immediately display the updated history and message. The restart action uses redirect-after-post, which prevents browser refresh from repeating a restart request.

### 4.3 Restart: `POST /Game/Restart`

1. The request is protected by an antiforgery token.
2. The controller calls `StartNewGame`.
3. The new state replaces the session state.
4. The action redirects to `Index`.

## 5. Game rules and algorithms

### 5.1 Secret generation

`StartNewGame` takes the digits `0` through `9`, randomly orders them using `Random.Shared`, and takes four values. Concatenating those values creates a four-character secret.

Consequences of this design:

- Digits are distinct by construction.
- Leading zeroes are allowed because the secret is a string, not an integer.
- The secret has exactly four characters.
- The generated secret is not intended to be cryptographically secret. This is a game, not a security or gambling system.

### 5.2 Guess validation

Validation exists at two levels:

1. `GameViewModel` validates the HTTP form shape using data annotations.
2. `GameService.SubmitGuess` validates again, including the rule that all four digits must be different.

The second layer is deliberate defensive design. The service should remain safe if it is later called by another controller, an API endpoint, a background process, or a unit test without MVC model validation.

### 5.3 Match calculation

For each of the four positions:

1. If the guessed character equals the secret character at that position, increment `ExactMatches`.
2. Otherwise, if the secret contains the guessed character, increment `MisplacedMatches`.

Because both the secret and accepted guesses contain distinct digits, each digit can be counted at most once and this straightforward algorithm is sufficient. It would need to change if repeated digits were allowed, because repeated characters require frequency-aware matching.

### 5.4 Attempts and game-over state

A valid guess is added to history and decrements `AttemptsLeft`. A game is over when either:

- `IsWon` is true; or
- `AttemptsLeft` reaches zero.

`GameState.IsGameOver` is a computed property, so callers cannot accidentally persist a separate, contradictory game-over flag.

Invalid guesses do not consume an attempt. A guess submitted after game over also does not change the game; it only updates the message explaining that a new game is required.

### 5.5 Scoring

A winning game receives:

```text
500 + (remaining attempts × 100)
```

This rewards solving the code earlier. A losing game receives no score assignment beyond its initial value of zero. The score is stored in the session state and is not persisted as a leaderboard entry.

## 6. State management and serialization

### 6.1 Session model

The controller stores one JSON value under the key `CodebreakerGameState`. The serialized object contains the secret, attempts, flags, score, message, and guess history.

This choice is appropriate for a small single-player demo because it avoids database setup and lets each browser session have an independent game. It also means the game follows the session rather than an authenticated user.

### 6.2 Session lifecycle

Session is configured with:

- A 30-minute idle timeout in the first session configuration block.
- An HTTP-only cookie, which prevents normal client-side scripts from reading the session cookie.
- An essential cookie designation, allowing the session cookie when consent enforcement is enabled.
- Distributed memory cache as the backing store.

The memory-backed implementation is process-local and non-durable. Restarting the process, deploying a new instance, or routing a user to a different instance without shared session storage can lose the game.

### 6.3 Production implications

For multiple instances or durable games, replace the memory backing store with a shared provider such as a distributed cache or database and review session-cookie protection and key-ring configuration. A database-backed game aggregate would be preferable if games must survive session expiration, support accounts, or be audited.

The secret is currently included in server-side session data. It is not included in `GameViewModel`, but developers should be careful not to log serialized session values or expose `GameState` through a future API.

## 7. Validation, security, and resilience

### 7.1 Input validation

Browser attributes (`pattern`, `maxlength`, `required`, and `inputmode`) improve the user experience but can be bypassed. The authoritative checks are the MVC data annotations and the service validation.

The current rules reject whitespace, non-ASCII digits, incorrect length, and repeated digits. The use of `char.IsAsciiDigit` deliberately limits accepted input to ASCII `0`-`9`, matching the UI and game rules.

### 7.2 CSRF protection

Both state-changing forms include `@Html.AntiForgeryToken()`, and both actions use `[ValidateAntiForgeryToken]`. This protects the guess and restart operations from cross-site request forgery when the application is used with cookie-based session state.

### 7.3 Transport and headers

The pipeline redirects HTTP to HTTPS. In non-development environments it also enables HSTS and the exception handler route. Static files are served before routing, and authorization middleware is present even though the application currently has no authentication or authorization rules.

### 7.4 Error handling

Malformed or invalid session JSON is not allowed to become the active game: `GetGameState` falls back to a new game when deserialization returns no usable state or the secret length is not four. The current check is intentionally small; a future version could validate every persisted field and catch malformed JSON explicitly if session data may be corrupted.

The configured production exception handler points to `/Home/Error`, but this project does not currently contain a `HomeController` or an error view. That is a deployment-readiness gap: an unhandled production exception may not reach a useful error page. Add the endpoint or change the configured error route before relying on production exception handling.

## 8. Dependency injection and lifetimes

`IGameService` is registered as scoped:

```text
IGameService -> GameService
```

A scoped lifetime gives one service instance per HTTP request. `GameService` is stateless: it does not hold a game in a field, so it does not need singleton lifetime and does not risk sharing mutable game state between requests.

The controller depends on `IGameService`, not `GameService`. This supports substitution with a fake service in unit tests and keeps the controller coupled to behavior rather than implementation.

## 9. Project structure

```text
CodebreakerGame/
├── CodebreakerGame.csproj       Project and .NET 8 settings
├── Program.cs                   Service registration and HTTP pipeline
├── Controllers/
│   └── GameController.cs        HTTP actions and session/view coordination
├── Models/
│   ├── GameState.cs             Server-side game aggregate/state
│   ├── GameViewModel.cs         Form and display model for the view
│   └── GuessRecord.cs            One guess result
├── Service/
│   ├── IGameService.cs          Game service abstraction
│   └── GameService.cs            Game creation and rules
├── Views/
│   ├── _ViewImports.cshtml      Razor imports and tag helpers
│   └── Game/
│       └── Index.cshtml         Main game page
├── Properties/
│   └── launchSettings.json      Local launch profiles
└── README.md                    Quick-start documentation
```

The folder is named `Service` while the namespace is `CodebreakerGame.Services`. The namespace is valid and consistent in the source, but renaming the folder to `Services` would make the physical layout clearer and align it with the namespace.

## 10. Configuration observations and technical debt

The current `Program.cs` contains duplicate registrations:

- `AddControllersWithViews()` appears twice.
- `AddDistributedMemoryCache()` appears twice.
- `AddSession()` appears once with options and once without options.

ASP.NET Core registration is generally last-registration-wins or additive, so the application can still run, but the duplication makes the effective configuration less obvious. The documented 30-minute timeout and cookie settings should be consolidated into one `AddSession` call as a maintenance improvement. This documentation does not change that behavior.

Other current limitations:

- No automated test project is present.
- No database or durable game history exists.
- No authentication or user identity exists.
- No rate limiting or abuse controls are configured.
- The service uses `Random.Shared`, which is suitable for gameplay but not for security-sensitive randomness.
- The view contains inline CSS rather than a reusable stylesheet.
- The production exception route appears incomplete.
- There is no structured logging or application health endpoint.

These are scope and maintainability observations, not requirements for the current game.

## 11. Testing strategy for future changes

The service layer is the easiest and highest-value place to start testing because it contains deterministic business outcomes. Recommended unit tests include:

### `GameService` tests

- New games have a four-digit secret, four distinct digits, and eight attempts.
- A valid winning guess sets `IsWon` and calculates the expected score.
- A valid non-winning guess records exact and misplaced counts.
- Invalid length, non-digit, repeated-digit, and empty guesses do not consume attempts.
- A guess after game over does not add history or consume attempts.
- The final unsuccessful attempt produces the losing message.

### Controller tests

- A missing session state creates and stores a new game.
- Invalid model state returns the index view without calling the service.
- A valid guess calls the service and stores the returned game.
- Restart creates a new game and redirects to index.
- Antiforgery behavior is covered by integration tests rather than ordinary controller unit tests.

### Integration and browser tests

An integration test can verify routing, session persistence, model binding, and Razor responses together. A browser test can verify that the form is hidden after game over, history is rendered, and the restart flow works from the user's perspective.

## 12. Extension guidance

### Adding a new game rule

Put the rule in `GameService` or a dedicated domain component, not only in the controller or view. Add a service-level test first, then update the view model and view if the rule changes displayed data.

### Adding an API

Do not return `GameState` directly because it contains `SecretCode`. Define API request and response DTOs that expose only the required fields. Reuse the service layer for rule execution and decide explicitly whether API clients use session, a game identifier, or authenticated persistence.

### Adding persistence

Introduce a repository or store abstraction only when there is a concrete persistence requirement. Decide whether the source of truth is per-session state, an authenticated user's active game, or a shareable game ID. Include concurrency handling before allowing multiple requests to update the same game.

### Adding authentication or a leaderboard

Move game ownership from anonymous session state to an authenticated user or a secure game identifier. Persist scores with the relevant user and game metadata, validate that score calculation happens server-side, and never trust a score submitted by the browser.

### Refactoring session mapping

If `GameController` grows, extract session serialization into a focused state store service. Keep the controller responsible for HTTP concerns and keep game transitions in the game service. This preserves the current separation while reducing controller size.

## 13. Local development workflow

1. Install the .NET 8 SDK and the ASP.NET/web development workload in Visual Studio.
2. Open the solution or project.
3. Restore dependencies.
4. Run with the Visual Studio launch profile or `dotnet run`.
5. Open the application at the URL shown by the launch profile.
6. Enter four distinct digits and submit guesses.
7. Use **New Game** to replace the current session game.

When debugging, start with `GameController` for request/session problems and `GameService` for rule or scoring problems. The view is the right place for rendering and browser behavior, but it should not become the authoritative validation or game-rule layer.

## 14. Decision summary

| Decision | Reason | Trade-off |
| --- | --- | --- |
| ASP.NET Core MVC | Simple server-rendered form workflow and clear separation of concerns | Less interactive than a client-side application |
| Separate service layer | Keeps game rules testable and out of the controller | Adds an abstraction for a small feature |
| Interface-based injection | Enables substitution and future implementations | Requires interface and registration maintenance |
| Session state | No database is needed for an anonymous single-player game | State is temporary and process/deployment sensitive |
| JSON session serialization | Stores the complete object graph with little code | Schema evolution and corruption handling are manual |
| String codes | Preserves leading zeroes and treats digits as symbols | Requires explicit validation instead of numeric parsing |
| Server-side validation | Client constraints cannot be trusted | Validation logic exists at both MVC and service boundaries |
| Antiforgery tokens | Protects cookie/session-backed state changes | Requires tokens on every state-changing form |
| Eight attempts and formula score | Provides a finite challenge and rewards speed | Rules are currently constants rather than configuration |
| No persistence/authentication | Keeps the sample small and easy to run | Cannot provide durable history, accounts, or leaderboard features |

## 15. Source of truth

When this document and the implementation differ, the implementation is authoritative until the code is intentionally changed. Update this document when changing:

- Routes or controller actions.
- Session keys, timeout, or storage.
- Game rules, scoring, or validation.
- Model/view-model fields.
- Security middleware or deployment behavior.
- Persistence, authentication, or testing strategy.
