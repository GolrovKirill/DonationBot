// <copyright file="IUpdateHandlerCommand.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers;

/// <summary>
/// Defines the contract for Telegram bot update command handlers.
/// </summary>
public interface IUpdateHandlerCommand
{
    /// <summary>
    /// Asynchronously processes an incoming update from Telegram Bot.
    /// </summary>
    /// <param name="botClient">Telegram Bot client for API interactions.</param>
    /// <param name="update">The update containing data to process.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous processing operation.</returns>
    /// <remarks>
    /// Implementations should only handle updates for which the <see cref="CanHandle"/> method returns true.
    /// In case of errors, it's recommended to log exceptions and handle them appropriately.
    /// </remarks>
    Task HandleAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether this handler can process the specified update.
    /// </summary>
    /// <param name="update">The update to check.</param>
    /// <returns>
    /// true if this handler can process the update; otherwise, false.
    /// </returns>
    /// <remarks>
    /// This method should be fast and not perform heavy operations.
    /// Typically checks the update type or presence of specific data.
    /// </remarks>
    bool CanHandle(Update update);
}