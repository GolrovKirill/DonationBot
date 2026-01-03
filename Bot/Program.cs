// <copyright file="Program.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Bot;
using Bot.Handlers;
using Bot.Services;
using Configurations;
using Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services;
using Telegram.Bot;
using Telegram.Bot.Polling;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Configure logging
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();

        // Add configuration
        builder.Services.Configure<BotConfig>(builder.Configuration.GetSection("BotConfig"));
        builder.Services.Configure<DatabaseConfig>(builder.Configuration.GetSection("DatabaseConfig"));
        builder.Configuration.AddUserSecrets<Program>();

        // Validate critical configurations
        ValidateConfiguration(builder);

        // Register data layer services
        builder.Services.AddScoped<IDapperRepository, DapperRepository>();

        // Register business logic services
        builder.Services.AddScoped<IGoalService, GoalService>();
        builder.Services.AddScoped<IDonationService, DonationService>();

        // Register update handling infrastructure
        builder.Services.AddScoped<IUpdateHandler, UpdateHandler>();
        builder.Services.AddScoped<IReceiverService, ReceiverService>();

        // Register command handlers
        builder.Services.AddScoped<MessageHandler>();
        builder.Services.AddScoped<CallbackQueryHandler>();
        builder.Services.AddScoped<PreCheckoutQueryHandler>();
        builder.Services.AddScoped<PaymentHandler>();
        builder.Services.AddScoped<CommandHandler>();
        builder.Services.AddScoped<AdminHandler>();

        // Register update handler commands
        builder.Services.AddScoped<IUpdateHandlerCommand, MessageHandler>();
        builder.Services.AddScoped<IUpdateHandlerCommand, CallbackQueryHandler>();
        builder.Services.AddScoped<IUpdateHandlerCommand, PreCheckoutQueryHandler>();

        // Register state management services
        builder.Services.AddSingleton<UserStateService>();
        builder.Services.AddSingleton<KeyboardService>();
        builder.Services.AddSingleton<AdminStateService>();

        // Register Telegram Bot Client
        builder.Services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<BotConfig>>().Value;

            if (string.IsNullOrEmpty(config.BotToken))
            {
                var logger = sp.GetRequiredService<ILogger<Program>>();
                logger.LogCritical("Bot token is not configured");
                throw new InvalidOperationException("Bot token is not configured");
            }

            var botClient = new TelegramBotClient(config.BotToken);

            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var botLogger = loggerFactory.CreateLogger<TelegramBotClient>();
            botLogger.LogInformation("Telegram Bot Client initialized successfully");

            return botClient;
        });

        // Register background service
        builder.Services.AddHostedService<PollingService>();

        var host = builder.Build();

        host.Run();
    }

    /// <summary>
    /// Validates critical configuration settings required for application startup.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    private static void ValidateConfiguration(HostApplicationBuilder builder)
    {
        var botConfig = builder.Configuration.GetSection("BotConfig").Get<BotConfig>();
        var databaseConfig = builder.Configuration.GetSection("DatabaseConfig").Get<DatabaseConfig>();

        if (botConfig == null)
        {
            throw new InvalidOperationException("BotConfig section is missing from configuration");
        }

        if (string.IsNullOrEmpty(botConfig.BotToken))
        {
            throw new InvalidOperationException("BotToken is not configured in BotConfig");
        }

        if (string.IsNullOrEmpty(botConfig.PaymentProviderToken))
        {
            throw new InvalidOperationException("PaymentProviderToken is not configured in BotConfig");
        }

        if (databaseConfig == null)
        {
            throw new InvalidOperationException("DatabaseConfig section is missing from configuration");
        }

        if (string.IsNullOrEmpty(databaseConfig.ConnectionString))
        {
            throw new InvalidOperationException("ConnectionString is not configured in DatabaseConfig");
        }

        // Log validation success (we'll use a temporary logger since we don't have DI yet)
        Console.WriteLine("Configuration validation completed successfully");
    }
}