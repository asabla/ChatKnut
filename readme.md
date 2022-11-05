# How to run

## 1. Build project
Run ```dotnet build``` in root of the project

## 2. Apply migrations and create database if missing
Run this command in root directory
```dotnet ef database update --project .\src\Data\Data.Chat\ --startup-project .\src\Backend\Backend.Api\ --context ChatKnutDbContext```

## 3. Run application
```dotnet run --project .\src\Backend\Backend.Api\```

How to add new migrations

```dotnet ef migrations add <NAME HERE> --project .\src\Data\Data.Chat\ --startup-project .\src\Backend\Backend.Api\ --context ChatKnutDbContext```