# Azure Services Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Azure Resource Group                                │
│                         (rg-expense-mgmt-dev)                                 │
└─────────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────────┐
│                                                                               │
│  ┌─────────────────────┐                                                     │
│  │  User/Developer     │                                                     │
│  └──────────┬──────────┘                                                     │
│             │                                                                 │
│             │ HTTPS                                                           │
│             ▼                                                                 │
│  ┌─────────────────────────────────────────────────────┐                    │
│  │          Azure App Service (Linux B1)               │                    │
│  │   - ASP.NET Core 8.0 Razor Pages App                │                    │
│  │   - REST APIs for Expense Management                 │                    │
│  │   - Swagger/OpenAPI Documentation                    │                    │
│  │   - AI-Powered Chat Interface                        │                    │
│  └───────────┬─────────────────────────┬────────────────┘                    │
│              │                         │                                     │
│              │ Uses                    │ Uses                                │
│              │ Managed Identity        │ Managed Identity                    │
│              ▼                         ▼                                     │
│  ┌─────────────────────────┐  ┌──────────────────────────────────┐         │
│  │ User-Assigned           │  │   Azure OpenAI Service (S0)      │         │
│  │ Managed Identity        │  │   - Location: Sweden Central      │         │
│  │ mid-AppModAssist-*      │  │   - Model: GPT-4o                 │         │
│  └───────────┬─────────────┘  │   - Function Calling Support      │         │
│              │                 │   - Managed Identity Auth         │         │
│              │ Authenticate    └───────────────────────────────────┘         │
│              ▼                                                                │
│  ┌─────────────────────────────────────────────────┐                        │
│  │     Azure SQL Database (Existing)                │                        │
│  │   - Server: sql-expense-mgmt-xyz                 │                        │
│  │   - Database: ExpenseManagementDB                │                        │
│  │   - Managed Identity Authentication              │                        │
│  │   - Tables: Expenses, Users, Categories, etc.    │                        │
│  └──────────────────────────────────────────────────┘                        │
│                                                                               │
│  ┌─────────────────────────────────────────────────┐                        │
│  │   Azure Cognitive Search (Basic)                 │                        │
│  │   - RAG Pattern Support                          │                        │
│  │   - Managed Identity Access                      │                        │
│  │   - Document Indexing                            │                        │
│  └──────────────────────────────────────────────────┘                        │
│                                                                               │
└───────────────────────────────────────────────────────────────────────────────┘

## Component Descriptions

### App Service
- **SKU**: B1 (Basic, low-cost for development)
- **Location**: UK South
- **Framework**: .NET 8.0 on Linux
- **Features**:
  - Expense management dashboard
  - RESTful APIs
  - AI-powered chat assistant
  - Swagger API documentation

### Managed Identity
- **Type**: User-Assigned
- **Purpose**: Secure authentication to Azure SQL and OpenAI
- **Naming**: mid-AppModAssist-{Day-Hour-Minute}
- **Permissions**:
  - SQL: db_datareader, db_datawriter
  - OpenAI: Cognitive Services OpenAI User
  - Search: Search Index Data Contributor

### Azure SQL Database
- **Server**: sql-expense-mgmt-xyz.database.windows.net
- **Database**: ExpenseManagementDB
- **Authentication**: Managed Identity (no passwords)
- **Schema**: Users, Expenses, Categories, Status tables

### Azure OpenAI
- **Location**: Sweden Central (for GPT-4o availability)
- **SKU**: S0 (Standard)
- **Model**: GPT-4o (latest)
- **Features**: Function calling for database operations
- **Auth**: Managed Identity (no API keys)

### Azure Cognitive Search
- **SKU**: Basic (low-cost)
- **Purpose**: RAG (Retrieval-Augmented Generation) pattern
- **Integration**: Supports contextual AI responses

## Deployment Options

### Option 1: Basic Deployment (deploy.sh)
- Deploys App Service + Managed Identity
- Connects to existing Azure SQL
- No AI features

### Option 2: Full Deployment (deploy-with-chat.sh)
- Deploys everything from Option 1
- Adds Azure OpenAI + Cognitive Search
- Enables AI Assistant chat interface
- Full function calling capabilities

## Data Flow

1. **User Request** → App Service
2. **App Service** → Authenticates using Managed Identity
3. **Managed Identity** → Provides token for Azure SQL
4. **App Service** → Queries/Updates database
5. **AI Chat** → User message sent to OpenAI
6. **OpenAI** → Processes with function calling
7. **Function Call** → Executes database operations
8. **Response** → Formatted results back to user

## Security Features

- ✅ No hardcoded credentials
- ✅ Managed Identity for all Azure services
- ✅ HTTPS only
- ✅ TLS 1.2 minimum
- ✅ Azure SQL with firewall rules
- ✅ Role-based access control (RBAC)
