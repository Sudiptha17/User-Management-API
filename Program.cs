using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "https://your-issuer.com",
            ValidAudience = "https://your-audience.com",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("your-secret-key"))
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Middleware for logging requests and responses
app.Use(async (context, next) =>
{
    app.Logger.LogInformation($"Incoming Request: {context.Request.Method} {context.Request.Path}");
    await next();
    app.Logger.LogInformation($"Outgoing Response: {context.Response.StatusCode}");
});

// Middleware for standardized error handling
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var error = new
        {
            Message = "An unexpected error occurred. Please try again later.",
            StatusCode = context.Response.StatusCode
        };

        app.Logger.LogError("Unhandled exception occurred.");
        await context.Response.WriteAsJsonAsync(error);
    });
});

// Middleware for authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.Map("/error", () => Results.Problem("An unexpected error occurred."));

// In-memory user storage for demonstration purposes
var users = new List<User>
{
    new User { Id = 1, Name = "John Doe", Email = "john.doe@example.com" },
    new User { Id = 2, Name = "Jane Smith", Email = "jane.smith@example.com" }
};

// GET: Retrieve a list of users with pagination
app.MapGet("/users", async (int? page, int? pageSize) =>
{
    try
    {
        page ??= 1;
        pageSize ??= 10;

        var paginatedUsers = await Task.Run(() =>
            users.Skip((page.Value - 1) * pageSize.Value).Take(pageSize.Value).ToList()
        );

        return Results.Ok(paginatedUsers);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred while retrieving users.");
        return Results.Problem("An unexpected error occurred.");
    }
}).WithName("GetUsers").RequireAuthorization();

// GET: Retrieve a specific user by ID
app.MapGet("/users/{id:int}", (int id) =>
{
    try
    {
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null)
        {
            app.Logger.LogWarning($"User with ID {id} not found.");
            return Results.NotFound(new { Message = "User not found" });
        }
        return Results.Ok(user);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred while retrieving the user.");
        return Results.Problem("An unexpected error occurred.");
    }
}).WithName("GetUserById");

// POST: Add a new user
app.MapPost("/users", (User newUser) =>
{
    if (string.IsNullOrWhiteSpace(newUser.Name) || string.IsNullOrWhiteSpace(newUser.Email))
    {
        return Results.BadRequest(new { Message = "Name and Email are required" });
    }

    if (!Regex.IsMatch(newUser.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
    {
        return Results.BadRequest(new { Message = "Invalid email format" });
    }

    if (users.Any(u => u.Email == newUser.Email))
    {
        return Results.BadRequest(new { Message = "A user with this email already exists" });
    }

    newUser.Id = users.Any() ? users.Max(u => u.Id) + 1 : 1;
    users.Add(newUser);
    return Results.Created($"/users/{newUser.Id}", newUser);
}).WithName("AddUser").RequireAuthorization();

// PUT: Update an existing user's details
app.MapPut("/users/{id:int}", (int id, User updatedUser) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(updatedUser.Name) || string.IsNullOrWhiteSpace(updatedUser.Email))
        {
            return Results.BadRequest(new { Message = "Name and Email are required" });
        }

        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null)
        {
            app.Logger.LogWarning($"User with ID {id} not found.");
            return Results.NotFound(new { Message = "User not found" });
        }

        user.Name = updatedUser.Name;
        user.Email = updatedUser.Email;
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred while updating the user.");
        return Results.Problem("An unexpected error occurred.");
    }
}).WithName("UpdateUser").RequireAuthorization();

// DELETE: Remove a user by ID
app.MapDelete("/users/{id:int}", (int id) =>
{
    try
    {
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null)
        {
            app.Logger.LogWarning($"User with ID {id} not found.");
            return Results.NotFound(new { Message = "User not found" });
        }

        users.Remove(user);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred while deleting the user.");
        return Results.Problem("An unexpected error occurred.");
    }
}).WithName("DeleteUser").RequireAuthorization();

app.Run();

record User
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
}