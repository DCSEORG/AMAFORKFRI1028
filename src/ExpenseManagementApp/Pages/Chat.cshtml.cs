using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Azure.AI.OpenAI;
using Azure;
using Azure.Identity;
using OpenAI.Chat;
using ExpenseManagementApp.Services;
using ExpenseManagementApp.Models;
using System.Text.Json;
using System.ClientModel;

namespace ExpenseManagementApp.Pages;

public class ChatModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ChatModel> _logger;

    [BindProperty]
    public string UserMessage { get; set; } = string.Empty;

    public List<DisplayMessage> Messages { get; set; } = new List<DisplayMessage>();
    public bool GenAIAvailable { get; set; } = true;
    public string? ErrorMessage { get; set; }

    public ChatModel(IConfiguration configuration, IExpenseService expenseService, ILogger<ChatModel> logger)
    {
        _configuration = configuration;
        _expenseService = expenseService;
        _logger = logger;
    }

    public void OnGet()
    {
        // Check if GenAI is configured
        var endpoint = _configuration["OpenAI:Endpoint"];
        GenAIAvailable = !string.IsNullOrEmpty(endpoint);
        
        if (!GenAIAvailable)
        {
            ErrorMessage = "GenAI services are not deployed. Please run deploy-with-chat.sh to enable the AI assistant.";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(UserMessage))
        {
            return Page();
        }

        try
        {
            var endpoint = _configuration["OpenAI:Endpoint"];
            var deploymentName = _configuration["OpenAI:DeploymentName"];

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(deploymentName))
            {
                ErrorMessage = "GenAI services are not configured. Run deploy-with-chat.sh first.";
                GenAIAvailable = false;
                return Page();
            }

            // Create OpenAI client with managed identity
            var credential = new DefaultAzureCredential();
            var client = new AzureOpenAIClient(new Uri(endpoint), credential);
            var chatClient = client.GetChatClient(deploymentName);

            // Define function calling tools for expense operations
            var tools = new List<ChatTool>
            {
                ChatTool.CreateFunctionTool(
                    functionName: "get_expenses",
                    functionDescription: "Retrieves expenses from the database, optionally filtered by user ID or status ID",
                    functionParameters: BinaryData.FromString("""
                        {
                            "type": "object",
                            "properties": {
                                "userId": {
                                    "type": "integer",
                                    "description": "Filter by user ID (optional)"
                                },
                                "statusId": {
                                    "type": "integer",
                                    "description": "Filter by status ID: 1=Draft, 2=Submitted, 3=Approved, 4=Rejected (optional)"
                                }
                            }
                        }
                        """)
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "create_expense",
                    functionDescription: "Creates a new expense in the database",
                    functionParameters: BinaryData.FromString("""
                        {
                            "type": "object",
                            "properties": {
                                "userId": {
                                    "type": "integer",
                                    "description": "User ID creating the expense"
                                },
                                "categoryId": {
                                    "type": "integer",
                                    "description": "Category ID: 1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other"
                                },
                                "amount": {
                                    "type": "number",
                                    "description": "Expense amount in GBP (e.g., 25.40)"
                                },
                                "date": {
                                    "type": "string",
                                    "description": "Expense date in YYYY-MM-DD format"
                                },
                                "description": {
                                    "type": "string",
                                    "description": "Description of the expense"
                                }
                            },
                            "required": ["userId", "categoryId", "amount", "date", "description"]
                        }
                        """)
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "get_categories",
                    functionDescription: "Retrieves all expense categories",
                    functionParameters: BinaryData.FromString("""
                        {
                            "type": "object",
                            "properties": {}
                        }
                        """)
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "get_users",
                    functionDescription: "Retrieves all users in the system",
                    functionParameters: BinaryData.FromString("""
                        {
                            "type": "object",
                            "properties": {}
                        }
                        """)
                )
            };

            // Build conversation history
            var conversationMessages = new List<ChatMessage>
            {
                new SystemChatMessage(
                    "You are a helpful AI assistant for an expense management system. " +
                    "You can help users view expenses, create new expenses, and get information about categories and users. " +
                    "When users ask about expenses, use the available functions to query the database. " +
                    "Always be friendly and provide clear, concise responses. " +
                    "Amounts are in GBP (British Pounds). " +
                    "When showing expense amounts, format them with a £ symbol.")
            };

            // Add user message
            conversationMessages.Add(new UserChatMessage(UserMessage));

            var chatCompletionOptions = new ChatCompletionOptions();
            foreach (var tool in tools)
            {
                chatCompletionOptions.Tools.Add(tool);
            }

            // Get response from OpenAI with function calling support
            var response = await chatClient.CompleteChatAsync(conversationMessages, chatCompletionOptions);

            // Handle function calls
            while (response.Value.FinishReason == ChatFinishReason.ToolCalls)
            {
                // Add assistant message with tool calls
                conversationMessages.Add(new AssistantChatMessage(response.Value));

                // Execute tool calls
                foreach (var toolCall in response.Value.ToolCalls)
                {
                    var functionName = toolCall.FunctionName;
                    var functionArgs = toolCall.FunctionArguments.ToString();

                    string functionResult;
                    try
                    {
                        functionResult = await ExecuteFunctionAsync(functionName, functionArgs);
                    }
                    catch (Exception ex)
                    {
                        functionResult = $"Error executing function: {ex.Message}";
                        _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
                    }

                    conversationMessages.Add(new ToolChatMessage(toolCall.Id, functionResult));
                }

                // Get next response
                response = await chatClient.CompleteChatAsync(conversationMessages, chatCompletionOptions);
            }

            // Store messages in TempData for display
            var displayMessages = new List<DisplayMessage>
            {
                new DisplayMessage { Role = "user", Content = UserMessage },
                new DisplayMessage { Role = "assistant", Content = response.Value.Content[0].Text }
            };

            TempData["ChatMessages"] = JsonSerializer.Serialize(displayMessages);
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            ErrorMessage = $"Error: {ex.Message}";
            GenAIAvailable = false;
            return Page();
        }
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string functionArgs)
    {
        try
        {
            switch (functionName)
            {
                case "get_expenses":
                    {
                        var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(functionArgs);
                        int? userId = args?.ContainsKey("userId") == true ? args["userId"].GetInt32() : null;
                        int? statusId = args?.ContainsKey("statusId") == true ? args["statusId"].GetInt32() : null;

                        var expenses = await _expenseService.GetExpensesAsync(userId, statusId);
                        return JsonSerializer.Serialize(expenses.Take(10)); // Limit to 10 for context size
                    }

                case "create_expense":
                    {
                        var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(functionArgs);
                        if (args == null) return "Error: Invalid arguments";

                        var userId = args["userId"].GetInt32();
                        var dto = new ExpenseCreateDto
                        {
                            CategoryId = args["categoryId"].GetInt32(),
                            AmountMinor = (int)(args["amount"].GetDecimal() * 100),
                            ExpenseDate = DateTime.Parse(args["date"].GetString() ?? DateTime.Now.ToString("yyyy-MM-dd")),
                            Description = args["description"].GetString()
                        };

                        var expense = await _expenseService.CreateExpenseAsync(userId, dto);
                        return JsonSerializer.Serialize(expense);
                    }

                case "get_categories":
                    {
                        var categories = await _expenseService.GetCategoriesAsync();
                        return JsonSerializer.Serialize(categories);
                    }

                case "get_users":
                    {
                        var users = await _expenseService.GetUsersAsync();
                        return JsonSerializer.Serialize(users);
                    }

                default:
                    return $"Unknown function: {functionName}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return $"Error: {ex.Message}";
        }
    }
}

public class DisplayMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
