# In-Memory Account Ledger Core (C# .NET EF Core)

## Overview
A robust, append-only in-memory multi-currency account ledger core built in **C# .NET** using **Entity Framework Core (InMemory)**. It supports AED (2 decimal places) and BHD (3 decimal places), authorization holds, settlements, reversals, overdraft fee assessments, and daily interest capitalization across a 6-day simulation window.

## Prerequisites
- .NET 8.0 SDK or higher installed on your machine.
- Entity Framework Core InMemory NuGet package:
  ```bash
  dotnet add package Microsoft.EntityFrameworkCore.InMemory