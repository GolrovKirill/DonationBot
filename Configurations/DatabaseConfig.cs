// <copyright file="DatabaseConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Configurations;

/// <summary>
/// Represents database configuration settings for the application.
/// </summary>
public class DatabaseConfig
{
    /// <summary>
    /// Gets or sets the connection string used to connect to the database.
    /// </summary>
    /// <value>
    /// The connection string containing server, database, and authentication information.
    /// </value>
    /// <example>
    /// Server=localhost;Database=MyDb;UserId=myuser;Password=mypassword;.
    /// </example>
    public string ConnectionString { get; set; } = string.Empty;
}
