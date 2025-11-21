![Header image](https://github.com/DougChisholm/App-Mod-Assist/blob/main/repo-header.png)

# App-Mod-Assist
A project to show how GitHub coding agent can turn screenshots of legacy apps into working proof-of-concepts for cloud native Azure replacements if the legacy database schema is also provided

## 🚀 Quick Start

This repository contains a modern Azure-based **Expense Management System** with:
- ✅ ASP.NET Core 8.0 Razor Pages application
- ✅ RESTful APIs with Swagger documentation
- ✅ Modern, responsive UI with Bootstrap 5
- ✅ Azure SQL Database integration with Managed Identity
- ✅ AI-powered chat assistant using Azure OpenAI GPT-4o
- ✅ Function calling for natural language database operations
- ✅ Infrastructure as Code with Bicep

## 📋 Prerequisites

- Azure subscription
- Azure CLI installed and configured (`az login`)
- .NET 8.0 SDK (for local development)
- Python 3.x with pip (for SQL setup scripts)

## 🛠️ Deployment Instructions

### Option 1: Basic Deployment (Without AI Assistant)

Deploys the expense management app with database connectivity but without AI features.

```bash
# 1. Clone the repository
git clone <your-repo-url>
cd AMAFORKFRI1028

# 2. Login to Azure
az login
az account set --subscription <your-subscription-id>

# 3. Deploy the infrastructure and application
./deploy.sh
```

**What gets deployed:**
- Azure App Service (B1 SKU)
- User-Assigned Managed Identity
- SQL database permissions configuration

**Access the app:** Navigate to `https://<app-url>/Index`

---

### Option 2: Full Deployment (With AI Assistant)

Deploys everything from Option 1 plus Azure OpenAI and Cognitive Search for the AI chat interface.

```bash
# 1. Clone the repository
git clone <your-repo-url>
cd AMAFORKFRI1028

# 2. Login to Azure
az login
az account set --subscription <your-subscription-id>

# 3. Deploy with GenAI services
./deploy-with-chat.sh
```

**What gets deployed:**
- Everything from Option 1
- Azure OpenAI (GPT-4o model in Sweden)
- Azure Cognitive Search (for RAG pattern)
- Full AI assistant functionality

**Access the app:** 
- Main app: `https://<app-url>/Index`
- AI Chat: `https://<app-url>/Chat`
- API Docs: `https://<app-url>/swagger`

---

## 🎯 Features

### Main Dashboard (`/Index`)
- View all expenses with filtering by status, category, and user
- Create new expenses
- Submit expenses for approval
- Approve/reject submitted expenses
- Real-time summary cards showing key metrics
- Modern, intuitive UI with Bootstrap 5 and Bootstrap Icons

### REST APIs (`/api/expenses`)
- **GET** `/api/expenses` - List all expenses (with optional filters)
- **GET** `/api/expenses/{id}` - Get expense details
- **POST** `/api/expenses` - Create new expense
- **PUT** `/api/expenses/{id}` - Update expense
- **PATCH** `/api/expenses/{id}/status` - Update expense status
- **DELETE** `/api/expenses/{id}` - Delete expense
- **GET** `/api/categories` - List all categories
- **GET** `/api/statuses` - List all statuses
- **GET** `/api/users` - List all users

### Swagger Documentation (`/swagger`)
- Interactive API documentation
- Test APIs directly from the browser
- View request/response schemas
- XML documentation comments

### AI Assistant (`/Chat`)
- Natural language querying: "Show me all submitted expenses"
- Create expenses: "Add a £50 travel expense for today"
- Get insights: "How many expenses does Alice have?"
- Function calling automatically queries the database
- Powered by Azure OpenAI GPT-4o

---

## 🏗️ Architecture

See [ARCHITECTURE.md](./ARCHITECTURE.md) for detailed architecture diagrams and component descriptions.

**Key Technologies:**
- **Frontend**: ASP.NET Core Razor Pages, Bootstrap 5, JavaScript
- **Backend**: .NET 8.0, Entity Framework-like data access
- **APIs**: RESTful with Swagger/OpenAPI
- **Database**: Azure SQL with Managed Identity authentication
- **AI**: Azure OpenAI GPT-4o with function calling
- **Search**: Azure Cognitive Search for RAG
- **Infrastructure**: Bicep (IaC)
- **Security**: Managed Identity (no passwords or API keys)

---

## 📁 Repository Structure

```
├── infrastructure/          # Bicep files for Azure resources
│   ├── main.bicep          # Main orchestration file
│   ├── app-service.bicep   # App Service and Managed Identity
│   ├── genai.bicep         # OpenAI and Cognitive Search
│   ├── run-sql.py          # Python script for SQL setup
│   └── script.sql          # SQL for managed identity permissions
├── src/                     # Application source code
│   └── ExpenseManagementApp/
│       ├── Pages/           # Razor Pages
│       ├── Controllers/     # API Controllers
│       ├── Models/          # Data models
│       ├── Services/        # Business logic services
│       └── wwwroot/         # Static files (CSS, JS)
├── Database-Schema/         # SQL database schema
├── Legacy-Screenshots/      # Reference screenshots
├── deploy.sh               # Deployment script (basic)
├── deploy-with-chat.sh     # Deployment script (with AI)
├── app.zip                 # Compiled application package
└── README.md               # This file
```

---

## 🔧 Configuration

### Environment Variables (Set by deployment scripts)

- `ConnectionStrings__ExpenseDb` - Azure SQL connection string
- `ManagedIdentity__ClientId` - Managed Identity client ID
- `OpenAI__Endpoint` - Azure OpenAI endpoint (with GenAI)
- `OpenAI__DeploymentName` - Model deployment name (with GenAI)
- `Search__Endpoint` - Azure Search endpoint (with GenAI)

### Database Connection

The application uses **Azure Active Directory Managed Identity** to connect to Azure SQL. No passwords are stored in configuration.

Connection string format:
```
Server=tcp:<server>.database.windows.net,1433;
Initial Catalog=ExpenseManagementDB;
Authentication=Active Directory Managed Identity;
User Id=<managed-identity-client-id>;
Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

---

## 🧪 Testing Locally

### Build the application
```bash
cd src/ExpenseManagementApp
dotnet build
dotnet run
```

Navigate to `https://localhost:5001/Index`

**Note:** Local development requires configuring Azure credentials via:
- Azure CLI (`az login`)
- Visual Studio
- Or environment variables

---

## 🔐 Security Features

- ✅ **No hardcoded secrets** - All authentication via Managed Identity
- ✅ **HTTPS only** - TLS 1.2 minimum
- ✅ **SQL Injection protection** - Parameterized queries
- ✅ **Error handling** - Graceful degradation with dummy data
- ✅ **RBAC** - Role-based access in database
- ✅ **Firewall rules** - Network security on Azure SQL

---

## 🤝 Contributing

For collaborators: **ALWAYS FORK** the repository before making changes to avoid polluting the base template.

Naming convention for forks: Use something like `AMA-YYYYMMDD-Description` instead of `App-Mod-Assist`

---

## 📝 Notes for Workshop Users

1. **Customization**: Update variables in deployment scripts for your environment
2. **SKU Selection**: Scripts use low-cost SKUs (B1, S0, Basic) for development
3. **Region**: App Service in UK South, OpenAI in Sweden Central (for GPT-4o)
4. **Database**: Connects to existing Azure SQL at `sql-expense-mgmt-xyz`
5. **Cleanup**: Delete the resource group when done to avoid charges

---

## 🐛 Troubleshooting

### App shows "Using dummy data"
- Check if the deployment script completed successfully
- Verify Managed Identity has database permissions
- Check the error message in the alert banner for details

### AI Chat shows "GenAI services not deployed"
- Use `deploy-with-chat.sh` instead of `deploy.sh`
- Verify Azure OpenAI resources are deployed
- Check app settings for `OpenAI__Endpoint`

### Build errors
- Ensure .NET 8.0 SDK is installed
- Run `dotnet restore` to restore NuGet packages
- Check for package compatibility issues

---

## 📚 Additional Resources

- [Azure App Service Documentation](https://learn.microsoft.com/azure/app-service/)
- [Azure OpenAI Documentation](https://learn.microsoft.com/azure/ai-services/openai/)
- [Managed Identity Documentation](https://learn.microsoft.com/azure/active-directory/managed-identities-azure-resources/)
- [Bicep Documentation](https://learn.microsoft.com/azure/azure-resource-manager/bicep/)

---

## 📄 License

See [LICENSE](./LICENSE) file for details.

---

**Built with ❤️ using GitHub Copilot and Azure**
