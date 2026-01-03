// <copyright file="IReceiverService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot;

/// <summary>
/// Defines a contract for receiving and processing updates in the Telegram bot.
/// </summary>
public interface IReceiverService
{
    /// <summary>
    /// Starts receiving and processing updates from the Telegram bot.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the receiving process.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ReceiveAsync(CancellationToken stoppingToken);
}