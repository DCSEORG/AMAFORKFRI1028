using Microsoft.Data.SqlClient;
using Azure.Identity;
using Azure.Core;
using ExpenseManagementApp.Models;

namespace ExpenseManagementApp.Services;

public interface IExpenseService
{
    Task<IEnumerable<Expense>> GetExpensesAsync(int? userId = null, int? statusId = null);
    Task<Expense?> GetExpenseByIdAsync(int expenseId);
    Task<Expense> CreateExpenseAsync(int userId, ExpenseCreateDto dto);
    Task<bool> UpdateExpenseAsync(int expenseId, ExpenseUpdateDto dto);
    Task<bool> UpdateExpenseStatusAsync(int expenseId, int statusId, int? reviewedBy = null);
    Task<bool> DeleteExpenseAsync(int expenseId);
    Task<IEnumerable<ExpenseCategory>> GetCategoriesAsync();
    Task<IEnumerable<ExpenseStatus>> GetStatusesAsync();
    Task<IEnumerable<User>> GetUsersAsync();
    bool IsUsingDummyData { get; }
    string? ErrorMessage { get; }
}

public class ExpenseService : IExpenseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExpenseService> _logger;
    private bool _useDummyData = false;
    private string? _errorMessage = null;

    public bool IsUsingDummyData => _useDummyData;
    public string? ErrorMessage => _errorMessage;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private async Task<SqlConnection> GetConnectionAsync()
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("ExpenseDb");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string 'ExpenseDb' not found in configuration");
            }

            var connection = new SqlConnection(connectionString);
            
            // Use managed identity authentication if specified in connection string
            if (connectionString.Contains("Authentication=Active Directory Managed Identity", StringComparison.OrdinalIgnoreCase))
            {
                var credential = new DefaultAzureCredential();
                var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net/.default" });
                var token = await credential.GetTokenAsync(tokenRequestContext);
                connection.AccessToken = token.Token;
            }

            await connection.OpenAsync();
            _useDummyData = false;
            _errorMessage = null;
            return connection;
        }
        catch (Exception ex)
        {
            _useDummyData = true;
            _errorMessage = $"Database connection error in ExpenseService.GetConnectionAsync (Line 62): {ex.Message}";
            _logger.LogError(ex, "Failed to connect to database. Using dummy data. Error: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<IEnumerable<Expense>> GetExpensesAsync(int? userId = null, int? statusId = null)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            var sql = @"
                SELECT e.ExpenseId, e.UserId, u.UserName, e.CategoryId, c.CategoryName, 
                       e.StatusId, s.StatusName, e.AmountMinor, e.Currency, e.ExpenseDate, 
                       e.Description, e.ReceiptFile, e.SubmittedAt, e.ReviewedBy, 
                       r.UserName as ReviewedByName, e.ReviewedAt, e.CreatedAt
                FROM dbo.Expenses e
                INNER JOIN dbo.Users u ON e.UserId = u.UserId
                INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
                INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
                LEFT JOIN dbo.Users r ON e.ReviewedBy = r.UserId
                WHERE (@UserId IS NULL OR e.UserId = @UserId)
                  AND (@StatusId IS NULL OR e.StatusId = @StatusId)
                ORDER BY e.CreatedAt DESC";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
            command.Parameters.AddWithValue("@StatusId", (object?)statusId ?? DBNull.Value);

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpenseFromReader(reader));
            }

            return expenses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses");
            return GetDummyExpenses();
        }
    }

    public async Task<Expense?> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            var sql = @"
                SELECT e.ExpenseId, e.UserId, u.UserName, e.CategoryId, c.CategoryName, 
                       e.StatusId, s.StatusName, e.AmountMinor, e.Currency, e.ExpenseDate, 
                       e.Description, e.ReceiptFile, e.SubmittedAt, e.ReviewedBy, 
                       r.UserName as ReviewedByName, e.ReviewedAt, e.CreatedAt
                FROM dbo.Expenses e
                INNER JOIN dbo.Users u ON e.UserId = u.UserId
                INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
                INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
                LEFT JOIN dbo.Users r ON e.ReviewedBy = r.UserId
                WHERE e.ExpenseId = @ExpenseId";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                return MapExpenseFromReader(reader);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense {ExpenseId}", expenseId);
            return GetDummyExpenses().FirstOrDefault(e => e.ExpenseId == expenseId);
        }
    }

    public async Task<Expense> CreateExpenseAsync(int userId, ExpenseCreateDto dto)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            var sql = @"
                INSERT INTO dbo.Expenses (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, CreatedAt)
                VALUES (@UserId, @CategoryId, 1, @AmountMinor, 'GBP', @ExpenseDate, @Description, SYSUTCDATETIME());
                SELECT SCOPE_IDENTITY();";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@CategoryId", dto.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", dto.AmountMinor);
            command.Parameters.AddWithValue("@ExpenseDate", dto.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)dto.Description ?? DBNull.Value);

            var expenseId = Convert.ToInt32(await command.ExecuteScalarAsync());
            
            return (await GetExpenseByIdAsync(expenseId))!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            throw new InvalidOperationException("Failed to create expense. Using dummy data mode.", ex);
        }
    }

    public async Task<bool> UpdateExpenseAsync(int expenseId, ExpenseUpdateDto dto)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            var updates = new List<string>();
            var command = new SqlCommand { Connection = connection };

            if (dto.CategoryId.HasValue)
            {
                updates.Add("CategoryId = @CategoryId");
                command.Parameters.AddWithValue("@CategoryId", dto.CategoryId.Value);
            }
            if (dto.AmountMinor.HasValue)
            {
                updates.Add("AmountMinor = @AmountMinor");
                command.Parameters.AddWithValue("@AmountMinor", dto.AmountMinor.Value);
            }
            if (dto.ExpenseDate.HasValue)
            {
                updates.Add("ExpenseDate = @ExpenseDate");
                command.Parameters.AddWithValue("@ExpenseDate", dto.ExpenseDate.Value);
            }
            if (dto.Description != null)
            {
                updates.Add("Description = @Description");
                command.Parameters.AddWithValue("@Description", dto.Description);
            }

            if (updates.Count == 0) return false;

            command.CommandText = $@"
                UPDATE dbo.Expenses 
                SET {string.Join(", ", updates)}
                WHERE ExpenseId = @ExpenseId";
            
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense {ExpenseId}", expenseId);
            return false;
        }
    }

    public async Task<bool> UpdateExpenseStatusAsync(int expenseId, int statusId, int? reviewedBy = null)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            var sql = @"
                UPDATE dbo.Expenses 
                SET StatusId = @StatusId,
                    SubmittedAt = CASE WHEN @StatusId = 2 AND SubmittedAt IS NULL THEN SYSUTCDATETIME() ELSE SubmittedAt END,
                    ReviewedBy = CASE WHEN @StatusId IN (3, 4) THEN @ReviewedBy ELSE ReviewedBy END,
                    ReviewedAt = CASE WHEN @StatusId IN (3, 4) THEN SYSUTCDATETIME() ELSE ReviewedAt END
                WHERE ExpenseId = @ExpenseId";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@StatusId", statusId);
            command.Parameters.AddWithValue("@ReviewedBy", (object?)reviewedBy ?? DBNull.Value);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense status {ExpenseId}", expenseId);
            return false;
        }
    }

    public async Task<bool> DeleteExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            var sql = "DELETE FROM dbo.Expenses WHERE ExpenseId = @ExpenseId";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense {ExpenseId}", expenseId);
            return false;
        }
    }

    public async Task<IEnumerable<ExpenseCategory>> GetCategoriesAsync()
    {
        try
        {
            using var connection = await GetConnectionAsync();
            var sql = "SELECT CategoryId, CategoryName, IsActive FROM dbo.ExpenseCategories WHERE IsActive = 1 ORDER BY CategoryName";

            using var command = new SqlCommand(sql, connection);
            var categories = new List<ExpenseCategory>();
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new ExpenseCategory
                {
                    CategoryId = reader.GetInt32(0),
                    CategoryName = reader.GetString(1),
                    IsActive = reader.GetBoolean(2)
                });
            }

            return categories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving categories");
            return GetDummyCategories();
        }
    }

    public async Task<IEnumerable<ExpenseStatus>> GetStatusesAsync()
    {
        try
        {
            using var connection = await GetConnectionAsync();
            var sql = "SELECT StatusId, StatusName FROM dbo.ExpenseStatus ORDER BY StatusId";

            using var command = new SqlCommand(sql, connection);
            var statuses = new List<ExpenseStatus>();
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32(0),
                    StatusName = reader.GetString(1)
                });
            }

            return statuses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statuses");
            return GetDummyStatuses();
        }
    }

    public async Task<IEnumerable<User>> GetUsersAsync()
    {
        try
        {
            using var connection = await GetConnectionAsync();
            var sql = @"
                SELECT u.UserId, u.UserName, u.Email, u.RoleId, r.RoleName, u.ManagerId, u.IsActive, u.CreatedAt
                FROM dbo.Users u
                INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
                WHERE u.IsActive = 1
                ORDER BY u.UserName";

            using var command = new SqlCommand(sql, connection);
            var users = new List<User>();
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new User
                {
                    UserId = reader.GetInt32(0),
                    UserName = reader.GetString(1),
                    Email = reader.GetString(2),
                    RoleId = reader.GetInt32(3),
                    RoleName = reader.GetString(4),
                    ManagerId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    IsActive = reader.GetBoolean(6),
                    CreatedAt = reader.GetDateTime(7)
                });
            }

            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return GetDummyUsers();
        }
    }

    private static Expense MapExpenseFromReader(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(0),
            UserId = reader.GetInt32(1),
            UserName = reader.GetString(2),
            CategoryId = reader.GetInt32(3),
            CategoryName = reader.GetString(4),
            StatusId = reader.GetInt32(5),
            StatusName = reader.GetString(6),
            AmountMinor = reader.GetInt32(7),
            Currency = reader.GetString(8),
            ExpenseDate = reader.GetDateTime(9),
            Description = reader.IsDBNull(10) ? null : reader.GetString(10),
            ReceiptFile = reader.IsDBNull(11) ? null : reader.GetString(11),
            SubmittedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
            ReviewedBy = reader.IsDBNull(13) ? null : reader.GetInt32(13),
            ReviewedByName = reader.IsDBNull(14) ? null : reader.GetString(14),
            ReviewedAt = reader.IsDBNull(15) ? null : reader.GetDateTime(15),
            CreatedAt = reader.GetDateTime(16)
        };
    }

    private static IEnumerable<Expense> GetDummyExpenses()
    {
        return new List<Expense>
        {
            new Expense
            {
                ExpenseId = 1,
                UserId = 1,
                UserName = "Alice Example",
                CategoryId = 1,
                CategoryName = "Travel",
                StatusId = 2,
                StatusName = "Submitted",
                AmountMinor = 2540,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-10),
                Description = "Taxi from airport to client site",
                SubmittedAt = DateTime.Now.AddDays(-9),
                CreatedAt = DateTime.Now.AddDays(-10)
            },
            new Expense
            {
                ExpenseId = 2,
                UserId = 1,
                UserName = "Alice Example",
                CategoryId = 2,
                CategoryName = "Meals",
                StatusId = 3,
                StatusName = "Approved",
                AmountMinor = 1425,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-20),
                Description = "Client lunch meeting",
                SubmittedAt = DateTime.Now.AddDays(-19),
                ReviewedBy = 2,
                ReviewedByName = "Bob Manager",
                ReviewedAt = DateTime.Now.AddDays(-18),
                CreatedAt = DateTime.Now.AddDays(-20)
            }
        };
    }

    private static IEnumerable<ExpenseCategory> GetDummyCategories()
    {
        return new List<ExpenseCategory>
        {
            new ExpenseCategory { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new ExpenseCategory { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new ExpenseCategory { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new ExpenseCategory { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new ExpenseCategory { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };
    }

    private static IEnumerable<ExpenseStatus> GetDummyStatuses()
    {
        return new List<ExpenseStatus>
        {
            new ExpenseStatus { StatusId = 1, StatusName = "Draft" },
            new ExpenseStatus { StatusId = 2, StatusName = "Submitted" },
            new ExpenseStatus { StatusId = 3, StatusName = "Approved" },
            new ExpenseStatus { StatusId = 4, StatusName = "Rejected" }
        };
    }

    private static IEnumerable<User> GetDummyUsers()
    {
        return new List<User>
        {
            new User
            {
                UserId = 1,
                UserName = "Alice Example",
                Email = "alice@example.co.uk",
                RoleId = 1,
                RoleName = "Employee",
                ManagerId = 2,
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-100)
            },
            new User
            {
                UserId = 2,
                UserName = "Bob Manager",
                Email = "bob.manager@example.co.uk",
                RoleId = 2,
                RoleName = "Manager",
                ManagerId = null,
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-200)
            }
        };
    }
}
