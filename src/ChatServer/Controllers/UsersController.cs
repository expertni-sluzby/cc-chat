using Microsoft.AspNetCore.Mvc;
using ChatServer.Models.DTOs;
using ChatServer.Services;

namespace ChatServer.Controllers;

/// <summary>
/// Controller for user management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new user
    /// </summary>
    /// <param name="request">Registration request with nickname</param>
    /// <returns>Created user information</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        _logger.LogInformation("Attempting to register user: {Nickname}", request.Nickname);

        var user = await _userService.RegisterUserAsync(request.Nickname);

        if (user == null)
        {
            // Check if it's because the nickname is already taken
            var isAvailable = await _userService.IsNicknameAvailableAsync(request.Nickname);
            if (!isAvailable)
            {
                _logger.LogWarning("Registration failed - nickname already exists: {Nickname}", request.Nickname);
                return Conflict(new ErrorResponse
                {
                    Error = "Nickname already exists",
                    Details = $"The nickname '{request.Nickname}' is already taken"
                });
            }

            // Otherwise it's a validation error
            _logger.LogWarning("Registration failed - invalid nickname: {Nickname}", request.Nickname);
            return BadRequest(new ErrorResponse
            {
                Error = "Invalid nickname",
                Details = "Nickname must be 3-20 characters and contain only alphanumeric characters and underscores"
            });
        }

        _logger.LogInformation("User registered successfully: {Nickname}", user.Nickname);

        var response = new UserResponse
        {
            Nickname = user.Nickname,
            RegisteredAt = user.RegisteredAt,
            IsOnline = user.IsOnline,
            CurrentRoomId = user.CurrentRoomId
        };

        return CreatedAtAction(nameof(GetAllUsers), response);
    }

    /// <summary>
    /// Logs in an existing user
    /// </summary>
    /// <param name="request">Login request with nickname</param>
    /// <returns>User information</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Login([FromBody] LoginUserRequest request)
    {
        _logger.LogInformation("Attempting to login user: {Nickname}", request.Nickname);

        var user = await _userService.LoginUserAsync(request.Nickname);

        if (user == null)
        {
            _logger.LogWarning("Login failed - user not found: {Nickname}", request.Nickname);
            return NotFound(new ErrorResponse
            {
                Error = "User not found",
                Details = $"No user found with nickname '{request.Nickname}'"
            });
        }

        _logger.LogInformation("User logged in successfully: {Nickname}", user.Nickname);

        var response = new UserResponse
        {
            Nickname = user.Nickname,
            RegisteredAt = user.RegisteredAt,
            IsOnline = user.IsOnline,
            CurrentRoomId = user.CurrentRoomId
        };

        return Ok(response);
    }

    /// <summary>
    /// Gets all registered users
    /// </summary>
    /// <returns>List of all users</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUsers()
    {
        _logger.LogInformation("Retrieving all users");

        var users = await _userService.GetAllUsersAsync();

        var response = users.Select(u => new UserResponse
        {
            Nickname = u.Nickname,
            RegisteredAt = u.RegisteredAt,
            IsOnline = u.IsOnline,
            CurrentRoomId = u.CurrentRoomId
        });

        return Ok(response);
    }
}
