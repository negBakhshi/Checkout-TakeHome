using System.Text.Json.Serialization;
using FluentValidation;
using PaymentGateway.Api.BankSimulator;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Validation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder
    .Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<IBankClient, BankClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["BankSimulator:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<IPaymentsRepository, PaymentsRepository>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<IValidator<PostPaymentRequest>, PostPaymentRequestValidator>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Makes the implicit Program class visible to WebApplicationFactory<Program> in the test project.
public partial class Program { }
