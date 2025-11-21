using Microsoft.AspNetCore.Mvc;
using ExpenseManagementApp.Models;
using ExpenseManagementApp.Services;

namespace ExpenseManagementApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(IExpenseService expenseService, ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expenses, optionally filtered by user ID or status ID
    /// </summary>
    /// <param name="userId">Filter by user ID (optional)</param>
    /// <param name="statusId">Filter by status ID (optional)</param>
    /// <returns>List of expenses</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Expense>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Expense>>> GetExpenses([FromQuery] int? userId = null, [FromQuery] int? statusId = null)
    {
        try
        {
            var expenses = await _expenseService.GetExpensesAsync(userId, statusId);
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific expense by ID
    /// </summary>
    /// <param name="id">Expense ID</param>
    /// <returns>Expense details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Expense>> GetExpense(int id)
    {
        try
        {
            var expense = await _expenseService.GetExpenseByIdAsync(id);
            
            if (expense == null)
            {
                return NotFound(new { error = "Expense not found", id });
            }

            return Ok(expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense {ExpenseId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    /// <param name="userId">User ID creating the expense</param>
    /// <param name="dto">Expense details</param>
    /// <returns>Created expense</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Expense>> CreateExpense([FromQuery] int userId, [FromBody] ExpenseCreateDto dto)
    {
        try
        {
            if (dto.AmountMinor <= 0)
            {
                return BadRequest(new { error = "Amount must be greater than zero" });
            }

            var expense = await _expenseService.CreateExpenseAsync(userId, dto);
            return CreatedAtAction(nameof(GetExpense), new { id = expense.ExpenseId }, expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing expense
    /// </summary>
    /// <param name="id">Expense ID</param>
    /// <param name="dto">Updated expense details</param>
    /// <returns>No content on success</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateExpense(int id, [FromBody] ExpenseUpdateDto dto)
    {
        try
        {
            var success = await _expenseService.UpdateExpenseAsync(id, dto);
            
            if (!success)
            {
                return NotFound(new { error = "Expense not found or no changes made", id });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense {ExpenseId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Update expense status (submit, approve, reject)
    /// </summary>
    /// <param name="id">Expense ID</param>
    /// <param name="dto">Status update details</param>
    /// <param name="reviewedBy">User ID of reviewer (for approve/reject)</param>
    /// <returns>No content on success</returns>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateExpenseStatus(int id, [FromBody] ExpenseStatusUpdateDto dto, [FromQuery] int? reviewedBy = null)
    {
        try
        {
            var success = await _expenseService.UpdateExpenseStatusAsync(id, dto.StatusId, reviewedBy);
            
            if (!success)
            {
                return NotFound(new { error = "Expense not found", id });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense status {ExpenseId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Delete an expense
    /// </summary>
    /// <param name="id">Expense ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        try
        {
            var success = await _expenseService.DeleteExpenseAsync(id);
            
            if (!success)
            {
                return NotFound(new { error = "Expense not found", id });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense {ExpenseId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get all expense categories
    /// </summary>
    /// <returns>List of categories</returns>
    [HttpGet("~/api/categories")]
    [ProducesResponseType(typeof(IEnumerable<ExpenseCategory>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ExpenseCategory>>> GetCategories()
    {
        try
        {
            var categories = await _expenseService.GetCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving categories");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get all expense statuses
    /// </summary>
    /// <returns>List of statuses</returns>
    [HttpGet("~/api/statuses")]
    [ProducesResponseType(typeof(IEnumerable<ExpenseStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ExpenseStatus>>> GetStatuses()
    {
        try
        {
            var statuses = await _expenseService.GetStatusesAsync();
            return Ok(statuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statuses");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get all users
    /// </summary>
    /// <returns>List of users</returns>
    [HttpGet("~/api/users")]
    [ProducesResponseType(typeof(IEnumerable<User>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        try
        {
            var users = await _expenseService.GetUsersAsync();
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }
}
