using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagementApp.Models;
using ExpenseManagementApp.Services;

namespace ExpenseManagementApp.Pages;

public class IndexModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<IndexModel> _logger;

    public IEnumerable<Expense> Expenses { get; set; } = new List<Expense>();
    public IEnumerable<ExpenseCategory> Categories { get; set; } = new List<ExpenseCategory>();
    public IEnumerable<ExpenseStatus> Statuses { get; set; } = new List<ExpenseStatus>();
    public IEnumerable<User> Users { get; set; } = new List<User>();
    public bool IsUsingDummyData { get; set; }
    public string? ErrorMessage { get; set; }

    public IndexModel(IExpenseService expenseService, ILogger<IndexModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            // Load data for the page
            Expenses = await _expenseService.GetExpensesAsync();
            Categories = await _expenseService.GetCategoriesAsync();
            Statuses = await _expenseService.GetStatusesAsync();
            Users = await _expenseService.GetUsersAsync();
            
            IsUsingDummyData = _expenseService.IsUsingDummyData;
            ErrorMessage = _expenseService.ErrorMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading index page data");
            IsUsingDummyData = true;
            ErrorMessage = $"Error loading data: {ex.Message}";
        }
    }
}
