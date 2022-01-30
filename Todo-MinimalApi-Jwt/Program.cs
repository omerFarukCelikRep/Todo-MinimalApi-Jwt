using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var conntectionString = builder.Configuration["ConnectionStrings:DefaultConnection"];
builder.Services.AddDbContext<ApiDbContext>(options =>
{
    options.UseSqlite(conntectionString);
});

var securityScheme = new OpenApiSecurityScheme()
{
    Name = "Authorization",
    Type = SecuritySchemeType.ApiKey,
    Scheme = "Bearer",
    BearerFormat = "JWT",
    In = ParameterLocation.Header,
    Description = "JWT Authentication for Minimal API"
};

var securityRequirements = new OpenApiSecurityRequirement()
{
    {
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        },
        Array.Empty<string>()
    }
};

var contactInfo = new OpenApiContact()
{
    Name = "Ömer Faruk",
    Email = "email@email.com",
    Url = new Uri("https://localhost:7053")
};

var license = new OpenApiLicense()
{
    Name = "Free License"
};

var info = new OpenApiInfo()
{
    Version = "V1",
    Title = "Todo List Api with Jwt Authentication",
    Description = "Todo List Api with Jwt Authentication",
    Contact = contactInfo,
    License = license
};

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", info);
    options.AddSecurityDefinition("Bearer", securityScheme);
    options.AddSecurityRequirement(securityRequirements);
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateAudience = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
            ValidateLifetime = false, //In any other application other than demo this needs to be 'true'
            ValidateIssuerSigningKey = true
        };
    });

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

//builder.Services.AddSingleton<ItemRepository>();

var app = builder.Build();

#region Actions
app.MapGet("/items", [Authorize] async (ApiDbContext context) =>
{
return await context.Items.ToListAsync();
});

app.MapPost("/items", async (ApiDbContext context, Item item) =>
{
    if (await context.Items.FirstOrDefaultAsync(a => a.Id == item.Id) != null)
    {
        return Results.BadRequest();
    }

    context.Items.Add(item);
    await context.SaveChangesAsync();

    return Results.Created($"/items/{item.Id}", item);
});

app.MapGet("/items/{id}", async (ApiDbContext context, int id) =>
{
    var item = await context.Items.FindAsync(id);

    return item == null ? Results.NotFound() : Results.Ok(item);
});

app.MapPut("/items/{id}", async (ApiDbContext context, int id, Item item) =>
{
    var existItem = await context.Items.FirstOrDefaultAsync(a => a.Id == item.Id);
    if (existItem == null)
    {
        return Results.NotFound();
    }

    existItem.Title = item.Title;
    existItem.IsCompleted = item.IsCompleted;

    await context.SaveChangesAsync();

    return Results.Ok(item);
});

app.MapDelete("/items/{id}", async (ApiDbContext context, int id) =>
{
    var existItem = await context.Items.FirstOrDefaultAsync(a => a.Id == id);
    if (existItem == null)
    {
        return Results.NotFound();
    }

    context.Items.Remove(existItem);
    await context.SaveChangesAsync();

    return Results.NoContent();
});

app.MapPost("/accounts/login", [AllowAnonymous] (UserDto user) =>
{
    if (user.username == "omer.faruk@email.com" && user.password == "Password123")
    {
        var secureKey = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]);
        var issuer = builder.Configuration["Jwt:Issuer"];
        var audience = builder.Configuration["Jwt:Audience"];
        var securityKey = new SymmetricSecurityKey(secureKey);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512);

        var jwtTokenHandler = new JwtSecurityTokenHandler();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new Claim[]
            {
                new Claim("Id", "1"),
                new Claim(JwtRegisteredClaimNames.Sub, user.username),
                new Claim(JwtRegisteredClaimNames.Email, user.username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }),
            Expires = DateTime.Now.AddMinutes(5),
            Audience = audience,
            Issuer = issuer,
            SigningCredentials = credentials
        };

        var token = jwtTokenHandler.CreateToken(tokenDescriptor);
        var jwtToken = jwtTokenHandler.WriteToken(token);

        return Results.Ok(jwtToken);
    }

    return Results.Unauthorized();
});
#endregion

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello from Minimal API");

app.Run();

record UserDto(string username, string password);

class Item
{
    public int Id { get; set; }
    public string Title { get; set; }
    public bool IsCompleted { get; set; }
}

//record Item(int id, string title, bool IsCompleted);

//class ItemRepository
//{
//    private Dictionary<int, Item> items = new();

//    public ItemRepository()
//    {
//        Item item1 = new(1, "Go to the gym", false);
//        Item item2 = new(2, "Drink Water", true);
//        Item item3 = new(3, "Watch Movie", false);

//        items.Add(item1.id, item1);
//        items.Add(item2.id, item2);
//        items.Add(item3.id, item3);
//    }

//    public IEnumerable<Item> GetAll() => items.Values;

//    public Item GetById(int id)
//    {
//        if (items.ContainsKey(id))
//        {
//            return items[id];
//        }

//        return null;
//    }

//    public void Add(Item item)
//    {
//        items.Add(item.id, item);
//    }

//    public void Update(Item item)
//    {
//        items[item.id] = item;
//    }

//    public void Delete(int id) => items.Remove(id);
//}

class ApiDbContext : DbContext
{
    public DbSet<Item> Items { get; set; }
    public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options)
    {

    }
}