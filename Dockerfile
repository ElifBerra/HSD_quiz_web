# Build aşaması
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["HSD_quiz_web.csproj", "./"]
RUN dotnet restore "HSD_quiz_web.csproj"
COPY . .
RUN dotnet publish "HSD_quiz_web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final (Çalıştırma) aşaması
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HSD_quiz_web.dll"]
