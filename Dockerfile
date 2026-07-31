FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["HSD_quiz_web.csproj", "./"]
RUN dotnet restore "HSD_quiz_web.csproj"
COPY . .
RUN dotnet publish "HSD_quiz_web.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from:app/publish /app/publish .
ENV ASPNETCORE_URLS=http://+=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "HSD_quiz_web.dll"]
