# How to run

## 1. Build project
Run ```dotnet build``` in root of the project

## 2. Apply migrations and create database if missing
Run this command in root directory

```ps1
dotnet ef database update --project .\src\Data\Data.ChatKnutDB\ --startup-project .\src\Backend\Backend.Api\ --context ChatKnutDBContext
```

## 3. Run application
```ps1
dotnet run --project .\src\Backend\Backend.Api\
```

How to add new migrations

```ps1
dotnet ef migrations add Initial --project .\src\Data\Data.ChatKnutDB\ --startup-project .\src\Backend\Backend.Api\ --context ChatKnutDBContext
```